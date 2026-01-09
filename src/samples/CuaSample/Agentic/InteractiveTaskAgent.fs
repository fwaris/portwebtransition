namespace FsPlaySamples.Cua.Agentic

open System.Collections.Generic
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.AI
open Microsoft.Extensions.Configuration
open AICore
open FsPlan
open RTFlow
open RTFlow.Functions

module InteractiveTaskAgent = 
    open System
    open FSharp.Control
    
    let MAX_CUA_LOOP = 20
    
    type internal State = {
        bus: CuaBus
        task : FsTask<Cu_Task> option
    }
        with
            static member Create bus = {
                                bus=bus
                                task=None
            }            
               
    let internal update (state:State) cuaMsg =
        async {
            match cuaMsg with
            | Ag_Task_Run (t,context) when state.task.IsSome ->
                Log.warn $"[InteractiveTaskAgent] Task already in progress. Ignoring new task"                 
                return state         
            | Ag_Task_Run (t,context) when t.task.IsCu_Interactive ->
                Log.info $"[InteractiveTaskAgent] starting task"                                 
                let target,desc = match t.task with | Cu_Interactive (t,d) -> t,d | _ -> failwith "no expected"
                state.bus.PostToAgent(Ag_App_Load(target,desc))
                return {state with task = Some t}
            | Ag_Task_End when state.task.IsSome ->
                Log.info $"[InteractiveTaskAgent] task ended"
                state.bus.PostToAgent(Ag_Plan_DoneTask {history=[]; status=Done; usage=Map.empty})
                return {state with task=None}                
            | _ -> return state
        }
            
    let start (bus: CuaBus) =
        let channel = bus.agentChannel.Subscribe("interactive")
        channel.Reader.ReadAllAsync()
        |> AsyncSeq.ofAsyncEnum
        |> AsyncSeq.scanAsync update (State.Create bus)
        |> AsyncSeq.iter(fun _ -> ())
        |> FlowUtils.catch bus.PostToFlow

