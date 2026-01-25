namespace FsPlaySamples.PortIn
open Fabulous
open FsPlan
open FsPlay.Abstractions
open FsPlaySamples.PortIn.Agentic
open RTFlow

exception InputKeyExn

type DataPoint = {X:float; Y:float; Label:string option}
    with member this.ItemLabel with get() = this.Label |> Option.defaultValue ""
    
type ChartType = Histogram of float | Bar
type Chart = {
    Title : string
    XTitle : string option
    YTitle : string option
    ChartType : ChartType 
    Values : DataPoint list        
}
    with static member Default =
                    {
                        Title = ""
                        XTitle = None
                        YTitle = None
                        ChartType = Bar
                        Values = []
                    }

type WebviewScroll = {x:float; y:float;}


type RunStatus = Init | Stepping | Running | Finished

type ArticleSummary =
    {
       Title         : string
       Summary       : string
    }
    with
        static member Default = {
            Title = ""
            Summary = ""
        }

/// Account information captured during PortIn automation
type AccountInfo =
    {
        AccountNumber   : string option
        BillingZip      : string option
        TransferPin     : string option
        TransferPinPageLocated : bool
        Bill            : Bill option
    }
    with
        static member Default = {
            AccountNumber = None
            BillingZip = None
            TransferPin = None
            TransferPinPageLocated = false
            Bill = None
        }

type Model =
    {
        mailbox         : System.Threading.Channels.Channel<Msg> //background messages
        settings        : Settings.SettingsModel
        isActive        : bool
        interactiveTask : string option
        pointer         : (int*int) option
        action          : string option
        stepping        : bool
        summaries       : ArticleSummary list
        accountInfo     : AccountInfo
        isOpenSettings  : bool
        isOpenNavBar    : bool
        runSteps        : int
        flow            : IFlow<FlowMsg,AgentMsg> option
        usage           : FsAICore.UsageMap
        lastError       : string option
        driver          : IUIDriver option
    }

and Msg =
    | StartFlow
    | StopFlow
    | SetFlow of IFlow<FlowMsg,AgentMsg> option
    | EventError of exn
    | CheckPreview of bool
    | DoneInteractiveTask
    | Nop
    | TestSomething
    | ToggleSettings
    | ToggleNavBar
    | Init
    | PostInit
    | ViewCreds
    | Nav of Msg
    | MenuSelect of int
    | ViewSummary
    | ViewAccountInfo
    | ViewStats
    | Active
    | InActive
    | BackButtonPressed
    | PreviewClear
    | WebviewInteraction of FsPlay.WvEvent
    | FromRunningTask of FromAgent

module Model =

    let webviewCache = ViewRef<Microsoft.Maui.Controls.WebView>()
    let webviewWrapper = lazy(FsPlay.Service.createWebViewWrapper(webviewCache.Value))

    
    let settingsValid () =
        let a = Settings.Environment.apiKey() |> isEmpty
        let b = Settings.Environment.userid() |> isEmpty
        let c = Settings.Environment.pwd() |> isEmpty
        let d = Settings.Environment.url() |> isEmpty
        (a || b || c || d) |> not
        
    let rec startPlan driver previewActions poster = async {
        let cfg = Utils.configuration.Value
        cfg.[FsAICore.ConfigKeys.ANTHROPIC_API_KEY] <- Settings.Environment.apiKey()
        let bus = CuaBus.Create()
        let vz = ref VzState.Default
        let poster = Plans.interceptor vz bus poster
        
        let context : FsAICore.AIContext = {
            backend = FsAICore.AIBackend.AnthropicLike
            kernel = Utils.services.Value
            jsonSerializationOptions = None
            toolsCache = FsAICore.Toolbox.makeTools None [ Agentic.AccountTools(poster) ]
            optionsConfigurator = None
        }
        
        let iflow = Agentic.StateMachine.create previewActions context driver poster bus
        // Create plan with current settings
        let planCfg : Agentic.PlanConfig = {
            Url = Settings.Environment.url()
        }
        let plan = Plans.createPortInPlan planCfg
        let taskRunner = PlanAgent.taskRunner driver bus
        let runner : FsPlan.Runner<Cu_Task,Cu_Task_Output> = FsPlan.createRunner context plan taskRunner
        iflow.PostToFlow(Fl_Start)
        do! Async.Sleep 500
        iflow.PostToAgent(Ag_Plan_Run runner)
        return iflow
    }
    
