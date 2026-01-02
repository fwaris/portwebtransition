namespace FsPlan

open System
open AICore
open System.Text.Json
open Microsoft.Extensions.AI

type Tid = Tid of string with member this.id with get() = match this with Tid i -> i

type FsTask<'t> = {
    id          : Tid
    description : string
    toolNames   : ToolName list 
    task        : 't
}

type FsTransition = {
    tid : Tid
    prompt : string
    toolNames : ToolName list
    targetTids : Tid list
}

type FsGraph = {root:Tid; transitions:Map<Tid,FsTransition>}

type FsPlan =
    | Sequential of Tid list
    | Graph of FsGraph
    
type Runner<'t,'o> =
    {
        context : AIContext
        tasks : Map<Tid,FsTask<'t>>
        plan  : FsPlan
        completed : (Tid*'o) list
        current: Tid option
        runTask: FsTask<'t> -> IServiceProvider -> Async<'o>
        transitionOutput : 'o -> string
        metricCollector : (Map<string,UsageDetails> -> unit) option
    }
    
type TransitionPromptInput = {
    taskOutput : string
    targets : {|id:string; description:string|} list
}

type TransitionPromptOutput = {
    id : string
    justification : string
}

module FsPlan =
    
    let private findDuplicates (xs:Tid list) = 
        let nonUnique = xs |> List.countBy _.id |> List.filter (fun (id,c) -> c > 1)
        if nonUnique.Length > 0 then
            Some $"Duplicate IDs {nonUnique}"
        else
            None
            
    let private ensureUniqueTargets (xs: FsTransition list) =
        xs
        |> List.choose (fun x -> findDuplicates x.targetTids |> Option.map (fun y->x.tid,y))
        |> List.tryHead
        |> Option.map (fun (e,id) -> failwith $"transition for {id} error: {e}")
        |> ignore
        
    let ensureValidTargets (g:FsGraph) (txns:FsTransition list)=
        let missedTargets = 
            txns
            |> List.collect (fun txn ->
                txn.targetTids
                |> List.choose (fun target ->
                    match g.transitions |> Map.tryFind target with
                    | Some  _ -> None
                    | None    -> Some ($"{txn.tid} -> {target} missing")
                    ))
        if missedTargets.Length > 0 then
            failwith $"No task found for the following targets {missedTargets}" 
            
    let validate = function
        | Sequential xs -> match findDuplicates xs with Some e -> failwith e | None -> ()
        | Graph g -> //transitions to self allowed
            let transitions = (Map.toList g.transitions |> List.map snd)
            ensureUniqueTargets transitions
            ensureValidTargets g transitions
            
            
    let private transitionNextSeq runner xs =
        match runner.completed with
        | [] -> List.tryHead xs
        | (x,_)::_ -> xs |> List.skipWhile (fun y -> y <> x) |> List.tryHead
        
    let inline internal jumpFrom runner (srcTid,output) (g:FsGraph)= async {
        let outStr = runner.transitionOutput output
        match g.transitions |> Map.tryFind srcTid with 
        | None -> return None //no transitions from last completed state
        | Some txn when txn.targetTids.IsEmpty -> return None
        | Some txn ->
            let data = {
                 taskOutput = outStr
                 targets = txn.targetTids |> List.map (fun id -> {|id=id.id; description=runner.tasks.[id].description|})   
            }            
            let msgs = seq  {
                ChatMessage(role = ChatRole.System, content= txn.prompt)
                ChatMessage(role = ChatRole.User, content = JsonSerializer.Serialize(data))                
            }
            let ctx = {runner.context with tools = txn.toolNames}
            let! resp,usage = AIUtils.sendRequest<TransitionPromptOutput> 2 ctx msgs
            runner.metricCollector |> Option.iter(fun x -> x usage)
            let targetTid = resp.id |> checkEmpty |> Option.map Tid
            match targetTid with
            | None -> Log.info $"No transition output from {txn.tid}"
            | Some target -> Log.info $"Txn: {txn.tid.id}->{target.id} - Justification:\r\n{resp.justification}"
            return targetTid
    }
                                                                                                                 
    let inline internal transitionGraph runner (g:FsGraph) = async {
        match runner.completed with
        | [] -> return Some g.root
        | (x,o)::_ -> return! jumpFrom runner (x,o) g
    } 
                    
    let transition runner = async {
        let! tid = 
            match runner.plan with
            | Sequential xs -> async {return transitionNextSeq runner xs}
            | Graph graph -> transitionGraph runner graph
        return {runner with current = tid}
    }
    
    let runTask runner = async {
        match runner.current with
        | Some tid ->
            let t = runner.tasks.[tid]
            let! o = runner.runTask t runner.context.kernel
            let runner = {runner with completed = (o,tid)::runner.completed}
            return! transition runner
        | None -> return runner
    }
