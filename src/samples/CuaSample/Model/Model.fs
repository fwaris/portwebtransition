namespace FsPlaySamples.Cua
open Fabulous
open FsPlan
open FsPlaySamples.Cua.Agentic
open RTFlow

exception InputKeyExn

type DataPoint = {X:float; Y:float; Label:string option} with member this.ItemLabel with get() = this.Label |> Option.defaultValue ""
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
       Summary       : string option
    }
    with
        static member Default = {
            Summary = None
        }

type Model = 
    {
        mailbox         : System.Threading.Channels.Channel<Msg> //background messages
        settings        : Settings.SettingsModel        
        isActive        : bool
        item            : string        
        pointer         : (int*int) option
        action          : string option
        stepping        : bool
        summary         : ArticleSummary
        isOpenSettings  : bool
        isOpenNavBar    : bool
        runSteps        : int
        flow            : IFlow<FlowMsg,AgentMsg> option
        usage           : AICore.UsageMap
        lastError       : string option
    }

and Msg =
    | StartFlow
    | StopFlow
    | EventError of exn        
    | CheckPreview of bool
    | Nop
    | ToggleSettings
    | ToggleNavBar
    | Init
    | PostInit
    | ViewCreds
    | Nav of Msg
    | MenuSelect of int
    | ViewSummary
    | ViewStats
    | Active
    | InActive
    | BackButtonPressed
    | PreviewClear
    | WebviewInteraction of FsPlay.WvEvent
    | FromRunningTask of FromAgent

module Model =
    open System.Threading
    open Microsoft.Maui.Controls

    let webviewCache = ViewRef<Microsoft.Maui.Controls.WebView>()
    
    let webviewWrapper = lazy(FsPlay.Service.createWebViewWrapper(webviewCache.Value))
    
    let postInit() =
        FsPlay.MauiWebViewDriver.initialize(webviewWrapper.Value)
         
    let settingsValid () =
        let a = Settings.Environment.apiKey() |> isEmpty
        let b = Settings.Environment.userid() |> isEmpty
        let c = Settings.Environment.pwd() |> isEmpty
        let d = Settings.Environment.url() |> isEmpty
        (a || b || c || d) |> not
        
    let rec startPlan cancelToken cfg waitForPreview poster stepper =
        let iflow,bus = Agentic.StateMachine.create poster
        let cfg = Utils.configuration.Value
        cfg.[AICore.ConfigKeys.ANTHROPIC_API_KEY] <- Settings.Environment.apiKey()
        let context : AICore.AIContext = {
            backend = AICore.AIBackend.AnthropicLike
            kernel = Utils.services.Value
            jsonSerializationOptions = None
            toolsCache =  AICore.Toolbox.makeTools None [ Agentic.ArticleTools(poster) ]
            optionsConfigurator = None            
        }
        let taskRunner = PlanAgent.taskRunner (0,0) bus 
        let runner : FsPlan.Runner<Cu_Task,Cu_Task_Output> = FsPlan.createRunner context LnkPlan.testPlan taskRunner
        iflow.PostToAgent (Ag_Plan_Run runner)
        