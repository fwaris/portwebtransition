namespace FsPlaySamples.Cua

open System
open System.IO
open Fabulous
open FsPlan
open FsPlaySamples.Cua
open FsPlaySamples.Cua.Agentic
open Microsoft.Maui.Storage

module UiOps =
    let playIcon (m:Model) =
        m.flow
        |> Option.map(fun r -> Icons.fa_stop)
        |> Option.defaultValue Icons.fa_play
        
    let playMsg (m:Model) =
        m.flow
        |> Option.map(fun r -> StopFlow)
        |> Option.defaultValue StartFlow        
        
    let postMsgDelayed model msg = async {
        do! Async.Sleep 1000
        model.mailbox.Writer.TryWrite msg |> ignore
    }
    
    let postAgentMessage model m=
        model.mailbox.Writer.TryWrite(FromRunningTask m) |> ignore
    
    let startStopFlow (model:Model) = async {
        match model.flow with
        | Some f -> f.Terminate()
                    return None
        | None -> let previewActions = Settings.Environment.previewClicks()
                  let! iflow = Model.startPlan previewActions (postAgentMessage model)
                  return Some iflow        
    }    
    
    let planDone (m:Model) (pr:Runner<Cu_Task,Cu_Task_Output>) =
        try
            match m.flow with
            | Some f -> f.Terminate()
            | None -> ()
            {m with flow=None}
        with ex ->
            Log.exn(ex,"planDone")
            m
            
    let loadTask model (t,d) =
        match t with
        | Some (Target t) -> Model.webviewWrapper.Value.Source <- t
        | None -> ()
        {model with interactiveTask = Some d}, Cmd.none
        
    let doneTask model =
        model.flow |> Option.iter(fun f -> f.PostToAgent(Ag_Task_End))
        {model with interactiveTask = None}, Cmd.none                    
    
    let saveDom (dom:string) =
        let path = FileSystem.Current.AppDataDirectory @@ "dom.html"
        Utils.debug path
        File.WriteAllText(path,dom)
        
    let snapshot (model:Model) = task {
        let! (sn: byte[],_,_) = FsPlay.Service.capture Model.webviewCache.Value
        let path = FileSystem.Current.AppDataDirectory @@ "dom_image.jpeg"
        Utils.debug path
        File.WriteAllBytes(path, sn)
    }
    
    let toRunSteps (s:string) = match Double.TryParse s with true,v->v | _ -> 1.0
    
    let mergeValues (model:Model) (us:AICore.UsageMap)  =
        {model with usage = AICore.Usage.combineUsage model.usage us}           
    
    let wireNavigation (model:Model) =
        Model.webviewCache.TryValue
        |> Option.iter(fun wv ->
            wv.Navigated.Add(fun _ ->
                async {
                    do! Async.Sleep 1000
                    model.mailbox.Writer.TryWrite Nop |> ignore    
                }
                |> Async.Start))

    