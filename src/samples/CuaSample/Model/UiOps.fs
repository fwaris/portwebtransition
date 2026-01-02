namespace FsPlaySamples.Cua

open System
open System.IO
open System.Text.Json
open System.Threading
open FsOpCore
open Microsoft.Maui.ApplicationModel
open Microsoft.Maui.Storage
open WebFlows

module UiOps =
    
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
    
    let mergeValues (model:Model) (vs:Map<string,string>)  =
        let vs = Flows.mergeValues model.flowRun.Values vs
        {model with flowRun = {model.flowRun with Values = vs}}
           
    let getDom (model:Model) = task {
        try
            match Model.webviewCache.TryValue with 
            | Some wv ->
                let f() = wv.EvaluateJavaScriptAsync("document.documentElement.outerHTML")
                let! dom = MainThread.InvokeOnMainThreadAsync<string>(f)
                saveDom dom
                do! snapshot model
                let f() = wv.EvaluateJavaScriptAsync(Scripts.clickables.Value)
                let! clickables = MainThread.InvokeOnMainThreadAsync<string>(f)
                let ctype =
                   try
                        let str = "\"" + clickables + "\""
                        let js = JsonSerializer.Deserialize<string>(str,FlowsJs.flSerOpts.Value)
                        debug js
                        let domS = JsonSerializer.Deserialize<DomSnapshot>(js,FlowsJs.flSerOpts.Value)
                        let domS = Flows.filterHiddenClickables domS 
                        CParsed domS
                   with ex ->
                       debug $"c2 error: {ex.Message}"
                       CRaw clickables
                return dom, ctype
            | None -> return "",CNone
        with ex ->
           debug ex.Message
           return raise ex
    }
    
    let serializeClickables (model:Model) =
        match model.clickables with
        | CParsed xs -> JsonSerializer.Serialize(xs)
        | CRaw s -> s
        | CNone -> "not set"

    let stepFlowBack model = task {
        match Model.webviewCache.TryValue with
        | Some wv -> return! Flows.stepBack Model.driver.Value model.flowRun
        | None -> return model.flowRun
    }    

    let highlight model = task {
        match Model.webviewCache.TryValue,  model.flowRun.ToDo with
        | Some wv, Clicks refs::_ -> let! x = Flows.lastVisibleRef Model.driver.Value refs
                                     return x |> Option.map snd
        | _ -> return None
    }
    
    let wireNavigation (model:Model) =
        Model.webviewCache.TryValue
        |> Option.iter(fun wv ->
            wv.Navigated.Add(fun _ ->
                async {
                    do! Async.Sleep 1000
                    model.mailbox.Writer.TryWrite GetValues |> ignore    
                }
                |> Async.Start))

    let canStep (m:Model) =
        m.runState
        |> Option.map(fun r ->  r.status.IsInit || r.status.IsFinished || r.status.IsStepping )
        |> Option.defaultValue true
        
    let playIcon (m:Model) =
        m.runState
        |> Option.map(fun r -> if r.status.IsRunning then Icons.fa_stop else Icons.fa_play)
        |> Option.defaultValue Icons.fa_play
        
    let playMsg (m:Model) =
        m.runState
        |> Option.map(fun r -> if r.status.IsRunning then PauseFlow else RunFlow)
        |> Option.defaultValue RunFlow

    let canPlayOrPause (m:Model) =
        m.runState
        |> Option.map(fun r -> r.status.IsStepping || r.status.IsRunning)
        |> Option.defaultValue false
        
    let runCount (m:Model) =
        m.runState
        |> Option.map(fun r -> r.runs)
        |> Option.defaultValue 0
                        
    let postAcctMsg model (msg: RunTaskMessage) = async {
        model.mailbox.Writer.TryWrite (FromRunningTask msg) |> ignore
    }
    
    let postMsgDelayed model msg = async {
        do! Async.Sleep 1000
        model.mailbox.Writer.TryWrite msg |> ignore
    }
                         
    let stepFlow (m:Model) =
        match m.runState with
        | Some r when r.status.IsStepping ->
            match r.stepper.Value() with
            | Some h -> h.Set() |> ignore; m
            | None -> m        
        | _ ->
            m.runState |> Option.iter (fun r -> r.cancelTokenSource.Cancel())
            let cfg = {Pwd=Settings.Environment.pwd(); UserId = Settings.Environment.userid(); Url = Settings.Environment.url();  }
            let r = {RunState.Default with runs=0; status=Stepping; config = cfg; }
            r.stepper.MakeStepped(true)
            let waitForPreview = Settings.Environment.previewClicks()
            Model.startPlan r.cancelTokenSource.Token cfg waitForPreview (postAcctMsg m) r.stepper 
            {m with runState = Some r}

    let runFlow (m:Model) =
        match m.runState with
        | Some r -> let r = {r with status = Running}
                    r.stepper.MakeFree()
                    {m with runState = Some r}
        | None -> m
        
    let pauseFlow (m:Model) =
        match m.runState with
        | Some r when r.status.IsRunning ->
            let r = {r with status = Running}
            r.stepper.MakeStepped(false)
            {m with runState = Some r}
        | _ -> m
        
    let updateRunState (m:Model) =
        match m.runState with
        | Some r when r.status.IsRunning ->
            let r = {r with
                        currentResult.captured.account = m.accountInfo.AccountNumber
                        currentResult.captured.zip = m.accountInfo.BillingZip
                     }
            {m with runState = Some r}
        | _ -> m

    let updateFromPlanRun (r:RunState) (pr:OPlanRun) =
       let usage = pr.completedTasks |> List.map _.usage |> List.reduce FlUtils.combineUsage
       let errors = pr.completedTasks |> List.map _.status |> List.filter (fun x -> x.IsT_Timeout || x.IsT_Error) |> List.length
       let cr = {r.currentResult with end_ts = Some DateTime.Now; usage= usage; errors=errors} 
       {r with runs = r.runs + 1; results = cr::r.results; currentResult = RunResult.Default}        
    
    let planDone (m:Model) (pr:OPlanRun) =
        try 
            match m.runState with
            | Some r when r.status.IsRunning && (r.runs + 1) < m.runSteps ->
               let r = updateFromPlanRun r pr
               let waitForPreview = Settings.Environment.previewClicks()
               Model.startPlan r.cancelTokenSource.Token r.config waitForPreview (postAcctMsg m) r.stepper 
               {m with runState = Some r}
            | Some r ->
                let r = {updateFromPlanRun r pr with status = Finished}
                r.cancelTokenSource.Cancel()
                {m with runState = Some r}
            | None   -> m
        with ex ->
            Log.exn(ex,"planDone")
            m
                        
    let resetFlow (m:Model) =
        match m.runState with
        | Some r -> r.stepper.Reset()
                    r.cancelTokenSource.Cancel()
        | _ -> ()
        {m with runState = None; action = None;}
        
    let sampleCharts = lazy(
        let rng = System.Random()
        [
            {Chart.Default with Title="Sample Bar"; Values= [{X=1; Y=1; Label=Some "A"};{X=2; Y=2; Label=Some "B"}]; ChartType=Bar}
            { Chart.Default with
                Title="Sample Histogram"
                XTitle = Some "Bins"
                YTitle = Some "Frequency"
                ChartType=Histogram 0.2
                Values= [
                 for i in 1 .. 50 do
                    {X= rng.Next(1,10); Y=rng.NextDouble(); Label=None}
                ]
                |> List.sortBy _.X
            }
        ])
    
    let buildDurationChart (xs:RunResult list) =
        {
            Title = "Task Duration Distribution"
            ChartType = Histogram 0.3
            XTitle = Some "Minutes"
            YTitle = Some "Frequency"
            Values =
                xs
                |> List.choose(fun r ->
                       r.end_ts
                       |> Option.map(fun ets ->
                           let dur = (ets - r.start_ts).TotalMinutes
                           {X=dur; Y=dur; Label=None}                           
                       )                                                            
                    )
        }
       
    let buildTokenChart (xs:RunResult list) =
        {
            Title = "Total Tokens Distribution"
            ChartType = Histogram 30.
            XTitle = Some "Tokens"
            YTitle = Some "Frequency"
            Values =
                xs
                |> List.map (fun x -> x.usage |> Map.toSeq |> Seq.map snd |> Seq.sumBy (fun y -> y.total_tokens))
                |> List.map (fun v ->{X=v; Y=v; Label=None}) 
        }
        
    let dataAcc (xs:RunResult list) =
        let zipSet = xs |> List.map _.captured.zip |> set                    //for 100% acc there should only be 1 value for each
        let acctSet = xs |> List.map _.captured.account |> set
        let tranferPinSet = xs |> List.map _.captured.transfer_pin |> set
        let all = float (zipSet.Count + acctSet.Count + tranferPinSet.Count)
        let innAccPct = (3.0 - all) / float xs.Length
        1.0 - innAccPct
        
    let errorRate (xs:RunResult list ) =
        let errs = xs |> List.sumBy _.errors |> float
        errs / float xs.Length
                
    let buildAccAndErrsChart (xs:RunResult list) =
        {
            Title = "Other metrics"
            ChartType = Bar
            XTitle = None
            YTitle = Some "Percentage"
            Values = [
                {X=1; Y=dataAcc xs; Label = Some "Accuracy Data"}
                {X=1; Y=errorRate xs; Label = Some "Error Rate"}
            ]
        }        
    
    let buildCharts (m:Model) =
        let runResults = m.runState |> Option.map _.results |> Option.defaultValue []
        match runResults with
        | [] -> sampleCharts.Value
        | xs ->
            [
                buildDurationChart xs
                buildTokenChart xs
                buildAccAndErrsChart xs
            ]