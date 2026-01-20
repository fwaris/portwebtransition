namespace FsPlaySamples.Cua.Agentic

open System.Collections.Generic
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.AI
open Microsoft.Extensions.Configuration
open AICore
open FsPlan
open RTFlow
open RTFlow.Functions

module CuaTaskAgent = 
    open System
    open FSharp.Control
    
    let MAX_CUA_LOOP = 20
    
    type internal State = {
        bus: CuaBus
        task : FsTask<Cu_Task> option
        history:ChatMessage list
        cuaLoopCount : int
        usage : UsageMap
    }
        with
            static member Create bus = {
                                bus=bus
                                task=None
                                history=[]
                                cuaLoopCount = 0
                                usage = Map.empty
            }            
        
    let ignoreCase = StringComparison.CurrentCultureIgnoreCase

    let content chooser (asstResp:ChatMessage option) =
        asstResp
        |> Option.map (fun m -> m.Contents|> Seq.cast<AIContent>)
        |> Option.defaultValue Seq.empty
        |> Seq.choose chooser
        |> Seq.toList
        
    let cuaCalls msgs =  
        content (function :? FunctionCallContent as c when c.Name = "computer" -> Some c |  _ -> None) msgs

    let nonCuaCalls msgs = content (function :? FunctionCallContent as c when c.Name <> "computer" -> Some c |  _ -> None) msgs

    let textContent msgs = 
        content (function :? TextContent as c -> Some c.Text | _ -> None) msgs
        |> Seq.tryHead

    let asstMsg (response:ChatResponse) = 
        response.Messages
        |> Seq.rev
        |> Seq.tryFind (fun m -> m.Role = ChatRole.Assistant)
        |> Option.defaultWith (fun _ -> failwith "Assistant response missing after tool invocation")

    let invokeTool (tool:AITool) (args: IDictionary<string,obj>) = async {
        match tool with 
        | :? AIFunction as fc -> return! fc.InvokeAsync(AIFunctionArguments(args)).AsTask() |> Async.AwaitTask
        | _                   -> return $"called {tool.Name}" :> obj
    }
    
    let chatOptions tools = 
        let opts = ChatOptions()
        opts.ModelId <- Anthropic.SDK.Constants.AnthropicModels.Claude45Sonnet
        opts.Tools <-
            tools
            |> Map.toSeq
            |> Seq.map snd
            |> ResizeArray
    
        opts

    let invokeFunction (tools:ToolCache) (funcCall:FunctionCallContent) = async {
        let! result = 
            match tools |> Map.tryFind (ToolName funcCall.Name) with 
            | Some tool -> invokeTool tool funcCall.Arguments
            | None      -> async{return failwith $"Tool or function named {funcCall.Name} not found in tool cache"}
        return FunctionResultContent(funcCall.CallId,result) :> AIContent
    }

    let validateCuaCall (accCua,accInvalid) (call:FunctionCallContent) = 
        try 
            let actions = 
                AICore.Anthropic.ToolUtils.toJsonElement call.Arguments
                |> Option.map AICore.Anthropic.Parser.parseActions
                |> Option.defaultWith(fun _ -> failwith $"unable to extract action for 'computer' tool call")

            let unkAction = actions |> List.tryFind _.IsUnknown
            match unkAction with 
            | Some (Anthropic.Unknown msg) -> accCua,(msg,call)::accInvalid            
            | _                            -> (actions |> List.map Anthropic.Parser.mapToUIDriverAction,call)::accCua,accInvalid
        with ex -> 
            accCua,(ex.Message,call)::accInvalid

    let extractFunctions (msg:ChatMessage) =
        let nonCuaCalls = nonCuaCalls (Some msg)
        let cuaCalls = cuaCalls (Some msg)
        let cuaCalls,invalidCalls = (([],[]),cuaCalls) ||> List.fold validateCuaCall
        seq {
            yield! cuaCalls |> Seq.map CallType.Cua
            yield! nonCuaCalls |> Seq.map CallType.NonCua
            yield! invalidCalls |> Seq.map CallType.Invalid
        }
        |> Seq.toList
        
    let mapUsage (usage:UsageDetails) =
        let input = usage.InputTokenCount.GetValueOrDefault() |> int
        let output = usage.OutputTokenCount.GetValueOrDefault() |> int
        let total =  usage.TotalTokenCount.GetValueOrDefault() |> int
        let total = if total < input + output then input + output else total
        {
          Usage.input_tokens = input
          Usage.output_tokens = output
          Usage.total_tokens = total
        }
        
 
    /// <summary>
    /// This function forms a mutually recursive loop with [<see cref="FsPlaySamples.Cua.Agentic.TaskAgent.handleNonCuaFunctionCalls"/>].<br />
    /// The call to the LLM can generate a response which contains function calls. There are two types of function calls cua and non-cua.<br />
    /// Any non-cua calls are handled by [handleNonCuaFunctionCalls] (but only after any cua calls have been handled first).<br />
    /// The [handleNonCuaFunctionCalls] function internally calls this function to send the response to the LLM (which may in turn generate new function calls). 
    /// </summary>
    let rec internal sendRequest (bus: CuaBus) (taskContext:TaskContext) history = async {
       let cfg = taskContext.aiContext.kernel.GetRequiredService<IConfiguration>()
       let client = AnthropicClient.createClient(cfg)
       let opts = chatOptions taskContext.aiContext.toolsCache
       taskContext.aiContext.optionsConfigurator |> Option.iter (fun c-> c opts)
       Anthropic.ToolUtils.addCuaAnthropicTool taskContext.screenDimensions opts        
       let! resp = client.GetResponseAsync(List.rev history, opts) |> Async.AwaitTask
       let usage = mapUsage resp.Usage
       bus.PostToAgent(Ag_Usage (Map.ofList [resp.ModelId,usage]))        
       let asstMsg = asstMsg resp
       let funcCalls = extractFunctions asstMsg //there may multiple 'parallel' calls that need to be handled
       let history = asstMsg :: history
       if funcCalls |> List.exists _.IsCua then                                       //handle any cua calls first
           return funcCalls, history                                        
       elif funcCalls |> List.exists (fun x -> x.IsInvalid || x.IsNonCua) then        //handle any non cua / invalid calls
           return! handleNonCuaFunctionCalls bus taskContext history funcCalls []
       else 
           return [],history                                                          //no calls in input message
    }    

    /// <summary>
    /// See [<see cref="FsPlaySamples.Cua.Agentic.TaskAgent.sendRequest"/>]
    /// </summary>
    and internal handleNonCuaFunctionCalls bus (taskContext:TaskContext) history (nonCuaCalls:CallType list) (cuaResults:AIContent list) = async {
        let! nonCuaResults = 
            nonCuaCalls
            |> List.choose (function CallType.NonCua f -> Some f | _ -> None)
            |> AsyncSeq.ofSeq
            |> AsyncSeq.mapAsync (invokeFunction taskContext.aiContext.toolsCache)
            |> AsyncSeq.toListAsync
        let invalidCallResults = 
            nonCuaCalls
            |> List.choose (function CallType.Invalid (a,b) -> Some (a,b) | _ -> None)
            |> List.map (fun (m,call) -> FunctionResultContent(call.CallId,m):> AIContent)
        let content = nonCuaResults @ invalidCallResults @ cuaResults
        let history = if content.IsEmpty then history else ChatMessage(ChatRole.User, content  |> ResizeArray) :: history
        return! sendRequest bus taskContext history
    }
    
    let handleRemainingCalls (bus: CuaBus)  context funcCalls = 
        match funcCalls with 
        | [] -> bus.PostToAgent (Ag_Task_Restart context)        //no computer call so restart task  
        | xs -> bus.PostToAgent (Ag_App_ComputerCall (funcCalls,None))
    
    let internal startCuaLoop state context (systemMsg:string) = async {
        let history = [ChatMessage(ChatRole.System,systemMsg)]
        let! funcCalls,history = sendRequest state.bus context history
        if not funcCalls.IsEmpty then  
            state.bus.PostToAgent(Ag_App_ComputerCall (funcCalls,None)) 
        return {state with history = history; cuaLoopCount=0}        
    }
       
    let internal update (state:State) cuaMsg =
        async {
            match cuaMsg with
            | Ag_Task_Run (t,context) when state.task.IsSome ->
                Log.warn $"[CuaTaskAgent] Task already in progress. Ignoring new task"                 
                return state         
            | Ag_Task_Run (t,context) when not t.task.IsCu_Cua ->
                Log.warn $"[CuaTaskAgent] ignoring non-cua task"                 
                return state         
            | Ag_Task_Run (t,context)  ->
                Log.info $"[CuaTaskAgent] Started task {t.id}"
                let! state = startCuaLoop state context t.description
                return {state with usage=Map.empty; task=Some t} //reset usage for a new task
            | Ag_Task_Continue cr when state.cuaLoopCount >= MAX_CUA_LOOP ->
                Log.info $"[CuaTaskAgent] max cua loop count exceeded {state.cuaLoopCount}. Restarting task"
                state.bus.PostToAgent (Ag_Task_Restart cr.context)
                return state
            | Ag_Task_Continue cr ->
                let pending = cr.pendingCalls
                let handled = cr.results
                let! funcCalls,history = handleNonCuaFunctionCalls state.bus cr.context state.history pending handled
                handleRemainingCalls state.bus cr.context funcCalls 
                return {state with history = history; cuaLoopCount = state.cuaLoopCount + 1}
            | Ag_Task_Restart context ->
                let t = state.task |> Option.defaultWith (fun () -> failwith $"[CuaTaskAgent] no task found")
                Log.info $"[CuaTaskAgent] Re-starting task {t.id}"
                return! startCuaLoop state context t.description 
            | Ag_Task_End when state.task.IsSome ->
                state.bus.PostToAgent (Ag_Plan_DoneTask {history=state.history; status=Cu_Task_Status.Done; usage=state.usage})
                Log.info $"[CuaTaskAgent] Ending task {state.task |> Option.map (fun x -> x.id.id) |> Option.defaultValue String.Empty}"                
                return {state with task=None}            
            | Ag_Usage usg -> return {state with usage = Usage.combineUsage state.usage usg}
            | _ -> return state
        }
            
    let start (bus: CuaBus) =
        let channel = bus.agentChannel.Subscribe("task")
        channel.Reader.ReadAllAsync()
        |> AsyncSeq.ofAsyncEnum
        |> AsyncSeq.scanAsync update (State.Create bus)
        |> AsyncSeq.iter(fun _ -> ())
        |> FlowUtils.catch bus.PostToFlow

