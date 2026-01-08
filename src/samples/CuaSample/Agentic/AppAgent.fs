namespace FsPlaySamples.Cua.Agentic
open FSharp.Control
open FsPlay.Abstractions
open RTFlow
open RTFlow.Functions

//sniff agent messages and notify app for selected ones
module AppAgent =
    type internal State = {
        poster : FromAgent -> unit//background messages
        bus : CuaBus
        driver : IUIDriver
    }
        with member this.Send (msg:FromAgent) =  this.poster msg
    
    let internal update (st:State) msg = async {
        match msg with
        | Ag_App_ComputerCall (callTypes,msg) -> ()// st.Send(FromAgent.Preview p)
        | _ -> ()
        return st
    }

    let start (driver:IUIDriver) poster (bus: CuaBus) =
        let st0 = {poster = poster; bus=bus; driver = driver}
        let channel = bus.agentChannel.Subscribe("app")
        channel.Reader.ReadAllAsync()
        |> AsyncSeq.ofAsyncEnum
        |> AsyncSeq.scanAsync update st0
        |> AsyncSeq.iter(fun _ -> ())
        |> FlowUtils.catch bus.PostToFlow