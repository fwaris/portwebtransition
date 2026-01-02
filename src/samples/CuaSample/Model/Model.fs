namespace FsPlaySamples.Cua
open System
open System.Threading
open Fabulous
open WebFlows
open Microsoft.Maui.ApplicationModel
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

type CDomSnapshot = CParsed of DomSnapshot | CRaw of string | CNone

type Highlighted = {element:ElemRef; x:float; y:float; width:float; height:float}

type RunStatus = Init | Stepping | Running | Finished

type CaptureValues = {account:string option; zip:string option; transfer_pin:string option}
    with static member Default = {account=None; zip=None; transfer_pin=None}

type RunResult = {
    start_ts : DateTime
    end_ts   : DateTime option
    errors   : int
    usage    : Map<string,AICore.Usage>
    captured : CaptureValues    
}
    with static member Default =
                        {
                            start_ts = DateTime.Now
                            end_ts = None
                            errors = 0
                            usage = Map.empty
                            captured = CaptureValues.Default
                        }

[<ReferenceEquality>]
type RunState = {
    cancelTokenSource : CancellationTokenSource
    config  : PlanConfig
    runs    : int
    status  : RunStatus
    stepper : StepperHolder
    currentResult : RunResult
    results : RunResult list
}
    with static member Default =
                        {
                            cancelTokenSource = new CancellationTokenSource()
                            config = PlanConfig.Default
                            runs = 0
                            status = Init
                            stepper = StepperHolder()
                            currentResult = RunResult.Default
                            results = []
                        }

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
        log             : string list
        isActive        : bool
        conversation    : string list
        item            : string
        dom             : string
        highlight       : ClickableElement option
        pointer         : (int*int) option
        action          : string option
        clickables      : CDomSnapshot
        fontSize        : float
        flowRun         : FlowRun
        stepping        : bool
 
        accountInfo     : ArticleSummary
        isOpenSettings  : bool
        isOpenNavBar    : bool
        runSteps        : int
        runState        : RunState option
    }

and Msg =
    | EventError of exn        
    | Cn_StartStop of unit
    | CheckStepped of bool
    | CheckPreview of bool
    | RunSteps of float
    | ViewDom    
    | Log_Append of string
    | Log_Clear
    | Nop
    | RunFlow
    | StepFlow
    | PauseFlow
    | SteppedFlow of FlowRun
    | StepError of exn
    | ToggleSettings
    | ToggleNavBar
    | ResetFlow
    | Highlight 
    | GetDom
    | Init
    | PostInit
    | SetDom of string* CDomSnapshot
    | SetHighlight of ClickableElement option
    | ClickAbleTest
    | ViewCreds
    | Nav of Msg
    | MenuSelect of int
    | ViewValues
    | ViewAccountInfo
    | ViewStats
    | GetValues
    | GotValues of Map<string,string>
    | Active
    | InActive
    | BackButtonPressed
    | InputKey of exn
    | ItemStarted
    | ItemAdded of string
    | SubmitCode
    | SetQuery of string
    | SetConsult of string
    | FontLarger
    | FontSmaller
    | PreviewClear
    | UpdateData
    | WebviewInteraction of FsPlay.WvEvent
    | FromRunningTask of RunTaskMessage

module Model =
    open System.Threading
    open Microsoft.Maui.Controls

    let webviewCache = ViewRef<Microsoft.Maui.Controls.WebView>()
    
    let webviewWrapper = lazy(FsPlay.Service.createWebViewWrapper(webviewCache.Value))
    
    let driver = lazy(
        let wv = webviewCache.TryValue |> Option.defaultWith (fun _ -> let m = "webview not ready" in Log.error m; failwith m)
        {new IMobileDriver with
            member this.evaluateJs (js:string) = async {
                let f() = wv.EvaluateJavaScriptAsync(js)
                let! rslt = MainThread.InvokeOnMainThreadAsync<string>(f) |> Async.AwaitTask
                return rslt
            }
            
            member this.goBack() =
                this.evaluateJs("history.back()")
                |> Async.Ignore
           
            member this.goto url = async {
                use h = new ManualResetEvent(false)
                let dlg = EventHandler<WebNavigatedEventArgs>(fun _  _ -> h.Set() |> ignore)
                wv.Navigated.AddHandler dlg
                let f() = wv.Source <- url
                do! MainThread.InvokeOnMainThreadAsync<unit>(f) |> Async.AwaitTask
                let! _ = Async.AwaitWaitHandle(h,2000)
                wv.Navigated.RemoveHandler dlg
            }
        }        
    )
    
    let postInit() =
        FsPlay.MauiWebViewDriver.initialize(webviewWrapper.Value)
         
    let settingsValid () =
        let a = Settings.Environment.apiKey() |> isEmpty
        let b = Settings.Environment.userid() |> isEmpty
        let c = Settings.Environment.pwd() |> isEmpty
        let d = Settings.Environment.url() |> isEmpty
        (a || b || c || d) |> not
        
    let startPlan cancelToken cfg waitForPreview poster stepper =        
        FlowValidator.Anthropic.Client.ApiKeyProvider <- lazy(Settings.Environment.apiKey())
        PortInPlanRunMobile.run cancelToken waitForPreview poster stepper cfg |> Async.Start
        
        