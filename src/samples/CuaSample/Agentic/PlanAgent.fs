namespace FsPlaySamples.Cua.Agentic

open AICore
open FSharp.Control
open FsPlan
open RTFlow
open RTFlow.Functions

module PlanAgent =
        
    type internal State= {
        bus     : CuaBus
        runner  : CuaRunner option
    }
    
    let taskRunner dims (bus:CuaBus) (t:FsTask<Cu_Task>) (context:AIContext) : Async<Cu_Task_Output> = async {     
         let n = "task_listener"
         let taskContext = {screenDimensions=dims; aiContext=context}
         let! msg = bus.AwaitAgentMsg(fun m -> m.IsAg_Plan_DoneTask)
         match msg with
         | Some(Ag_Plan_DoneTask output) -> return output
         | _                             -> return {history=[]; status = Cu_Task_Status.Error "timeout"; usage=Map.empty}
    }  
    
    let startTask (bus: CuaBus) runner= async {
         let! runner = FsPlan.runTask runner
         match runner.current with
         | Some _ -> bus.PostToAgent(Ag_Plan_Next runner)
         | None -> bus.PostToAgent(Ag_Plan_Done runner)
    }
           
    let internal update (state:State) msg = async {
        match msg with
        | Ag_Plan_Run _ when state.runner.IsSome ->
            Log.warn $"[PlanAgent] plan already running"
            return state
        | Ag_Plan_Run runner ->
            Log.info $"[PlanAgent] starting plan with task {FsPlan.peekNextTasks runner}"
            startTask state.bus runner |> Async.Start
            return {state with runner = Some runner}
        | Ag_Plan_Next runner ->
            Log.info $"[PlanAgent] transitioning to {FsPlan.peekNextTasks runner}"
            startTask state.bus runner |> Async.Start
            return {state with runner = Some runner}
        | Ag_Plan_Done runner ->
            Log.info $"[PlanAgent] done plan"            
            return {state with runner = None}
        | _ -> return state
    }

    let start<'t,'o> (bus:CuaBus) =        
        let st0 = {bus=bus; runner=None}
        let channel = bus.agentChannel.Subscribe("plan")
        channel.Reader.ReadAllAsync()
        |> AsyncSeq.ofAsyncEnum
        |> AsyncSeq.scanAsync update st0
        |> AsyncSeq.iter(fun _ -> ())
        |> FlowUtils.catch bus.PostToFlow
