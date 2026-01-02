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
            log             = []
            isActive        = false
            conversation    = []
            dom             = ""
            item            = ""
            fontSize        = 11.0
            highlight       = None
            clickables      = CDomSnapshot.CNone
            flowRun         = FlowRun.FromFlow (FlowDefs.vzFlow (Environment.GetEnvironmentVariable("PORT_OUT_URL_2")))
            stepping        = false
            accountInfo     = ArticleSummary.Default
            pointer         = None
            action          = None
            isOpenSettings  = false
            isOpenNavBar    = false
            runSteps        = 1
            runState        = None
        }, Cmd.ofMsg Init
      
    let update nav msg model =
        //Log.info $"%A{msg}"
        match msg with
        | Init -> wireNavigation model; model, Cmd.ofMsg PostInit
        | PostInit -> Model.postInit(); model,Cmd.none
        | StepFlow -> if Model.settingsValid() then stepFlow model, Cmd.none else model, Cmd.ofMsg ViewCreds
        | RunFlow -> runFlow model, Cmd.none
        | ResetFlow -> resetFlow model, Cmd.none
        | PauseFlow -> pauseFlow model, Cmd.none
        | UpdateData -> updateRunState model, Cmd.none
        | CheckPreview b -> model.settings.PreviewClicks <- b; model,Cmd.none
        | RunSteps s -> {model with runSteps = int s |> min 100 |> max 1}, Cmd.none
        | GetDom -> model,Cmd.OfTask.either getDom model SetDom EventError
        | SetDom (d,cs) -> {model with dom=d; clickables=cs},Cmd.ofMsg (WebviewInteraction (WvEvent.WvScroll(0.,0.)))
        | SetHighlight click -> {model with highlight = click},Cmd.none
        | ViewDom -> model, Navigation.navigateToDom nav (match model.clickables with CParsed c -> c.clickables | _ -> [])
        | ViewCreds -> model, Navigation.navigateToSettings nav
        | ViewValues -> model, Navigation.navigateToValues nav (model.flowRun.Values)
        | ViewAccountInfo -> model, Navigation.navigateToAccountInfo nav model.accountInfo
        | ViewStats -> model, Navigation.navigateToStats nav (buildCharts model)
        | Nav msg -> {model with isOpenNavBar=false}, Cmd.ofMsg msg
        | WebviewInteraction e -> model, Cmd.none //Cmd.ofMsg Highlight
        | GetValues -> model, Cmd.none // Cmd.OfTask.either Flows.getValues ß(Model.driver.Value,model.flowRun.Flow.Extractions)  GotValues EventError        
        | Highlight -> model, Cmd.OfTask.either highlight model SetHighlight EventError
        | GotValues vs -> mergeValues model vs, Cmd.none
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
        | FromRunningTask (RunTaskMessage.Summary summary) -> {model with accountInfo.Summary=Some summary},Cmd.ofMsg UpdateData
        | FromRunningTask (RunTaskMessage.AccountNumber acct) -> {model with accountInfo.AccountNumber=Some acct},Cmd.ofMsg UpdateData
        | FromRunningTask (RunTaskMessage.TransferPin pin) -> {model with accountInfo.TransferPin=Some pin},Cmd.ofMsg UpdateData
        | FromRunningTask (RunTaskMessage.Bill bill) -> {model with accountInfo.Bill=Some bill},Cmd.ofMsg UpdateData
        | FromRunningTask (RunTaskMessage.Preview c) -> postMsgDelayed model PreviewClear |> Async.Start; {model with pointer = c.click; action = Some c.action},Cmd.none
        | FromRunningTask (RunTaskMessage.PlanDone pr) -> planDone model pr, Cmd.none       
        | PreviewClear -> {model with pointer=None}, Cmd.none
        | x -> printfn $"not handled {x}"; model, Cmd.none
        