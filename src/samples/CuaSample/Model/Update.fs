namespace FsPlaySamples.Cua

open System
open Fabulous
open FsPlay
open FsPlaySamples.Cua.Navigation
open WebFlows
open UiOps

module Update =
    
    let initModel settings = 
        {
            mailbox         = System.Threading.Channels.Channel.CreateBounded<Msg>(30)
            settings        = settings
            isActive        = false
            item            = ""
            fontSize        = 11.0
            highlight       = None
            stepping        = false
            summary         = ArticleSummary.Default
            pointer         = None
            action          = None
            isOpenSettings  = false
            isOpenNavBar    = false
            flow            = None
            runSteps        = 1
            usage           = Map.empty
        }, Cmd.ofMsg Init
      
    let update nav msg model =
        //Log.info $"%A{msg}"
        match msg with
        | Init -> wireNavigation model; model, Cmd.ofMsg PostInit
        | PostInit -> Model.postInit(); model,Cmd.none
        | CheckPreview b -> model.settings.PreviewClicks <- b; model,Cmd.none
        | SetHighlight click -> {model with highlight = click},Cmd.none
        | ViewCreds -> model, Navigation.navigateToSettings nav
        | ViewSummary -> model, Navigation.navigateToAccountInfo nav model.summary
        | Nav msg -> {model with isOpenNavBar=false}, Cmd.ofMsg msg
        | WebviewInteraction e -> model, Cmd.none //Cmd.ofMsg Highlight
        | StepError ex -> {model with stepping=false}, Cmd.none
        | ToggleSettings -> {model with isOpenSettings = not model.isOpenSettings}, Cmd.none
        | ToggleNavBar -> {model with isOpenNavBar = not model.isOpenNavBar}, Cmd.none
        | SteppedFlow f -> {model with stepping=false; flowRun=f}, Cmd.batch [Cmd.ofMsg Highlight]
        | EventError exn -> debug exn.Message; {model with log=exn.Message::model.log}, Cmd.none
        | Log_Append s -> { model with log = s::model.log |> List.truncate C.MAX_LOG }, Cmd.none
        | Log_Clear -> { model with log = [] }, Cmd.none
        | InputKey _ -> model, Cmd.ofMsg GetDom
        | Nop -> model, Cmd.none
        | BackButtonPressed -> model, Navigation.navigateBack nav
        | Active -> {model with isActive = true},Cmd.none
        | InActive -> {model with isActive = false},Cmd.none
        | ItemStarted -> {model with item=""}, Cmd.none
        | ItemAdded txt -> {model with item = model.item + txt}, Cmd.none
        | FontLarger -> {model with fontSize = model.fontSize + 1.0}, Cmd.none
        | FontSmaller -> {model with fontSize = model.fontSize - 1.0}, Cmd.none
        | FromRunningTask (Agentic.FromAgent.Summary summary) -> {model with summary.Summary=Some summary},Cmd.ofMsg UpdateData
        | FromRunningTask (Agentic.FromAgent.Preview c) -> postMsgDelayed model PreviewClear |> Async.Start; {model with pointer = c.click; action = Some c.action},Cmd.none
        | FromRunningTask (Agentic.FromAgent.PlanDone rnr) -> planDone model rnr, Cmd.none       
        | PreviewClear -> {model with pointer=None}, Cmd.none
        | x -> printfn $"not handled {x}"; model, Cmd.none
        