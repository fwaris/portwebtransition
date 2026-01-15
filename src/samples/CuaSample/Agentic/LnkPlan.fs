namespace FsPlaySamples.Cua.Agentic

open AICore
open FsPlaySamples.Cua
open System
open System.ComponentModel
open Microsoft.SemanticKernel

type ArticleTools(poster: FromAgent -> unit) = 

    [<KernelFunction("save_summary")>]
    [<Description("Save the article summary")>]
    member this.save_summary(smry:string) = 
        let comp = async {
            try
                Log.info $"[ArticleTools] {nameof this.save_summary} {smry}"
                poster (FromAgent.Summary smry)
                return "summary saved"
            with ex ->
                Log.exn(ex, nameof this.save_summary)
                return $"error occurred while trying to save summary"
        }
        Async.StartAsTask comp
        
type TaskTools(poster: AgentMsg -> unit) = 

    [<KernelFunction("end_task")>]
    [<Description("Ends the current task")>]
    member this.end_task() = 
        let comp = async {
            try
                Log.info $"[TaskTools] ending current task"
                poster Ag_Task_End
                return "ended task"
            with ex ->
                Log.exn(ex, nameof this.end_task)
                return $"error occurred while trying to end task"
        }
        Async.StartAsTask comp

module LnkPlan =    
    open FsPlan
    let getOutput (t:FsTask<Cu_Task>) (kernel:IServiceProvider) = async {
        return ""
    }
    
    let tasks_ = [
        {   id = Tid "intro"
            task = Cu_Interactive (Some (Target "https://linkedin.com"), "Login to LinkedIn")
            description = "introduction"
            toolNames = []
        }
        {   id = Tid "find_gen_ai_posts"
            task = Cu_Cua None
            description = "find gen ai posts and save the summaries to memory using save_memory tool"
            toolNames = [ToolName "save_memory"]                       
        }               
    ]
    
    let tasks = [
        {   id = Tid "intro"
            task = Cu_Interactive (Some (Target "https://twitter.com"), "Login to LinkedIn")
            description = "introduction"
            toolNames = []
        }
        {   id = Tid "find_tweets"
            task = Cu_Cua None
            description = "find all recent posts related to neuro-symbolic ai"
            toolNames = [ToolName "save_memory"]                       
        }               
    ]
    
    let testPlan =
        {
            tasks = tasks |> List.map (fun x ->x.id,x) |> Map.ofList
            flow = FsPlanFlow.Sequential (tasks |> List.map _.id)
        }

(*

module PortPlanMobile =
    ///Shortcut for string 'Contains'
    let internal (<>=) (a:string) (b:string) = a.Contains(b, StringComparison.CurrentCultureIgnoreCase)
    
    let noWaitInstructions = """Note: In most cases you are provided with a screenshot after the 'computer' action has been taken. 
Generally, the 'wait' action is not required.
If lost, use the 'home' tool to get back to the home page.
"""
    let t_Login cfg= 
        {OTask.Create() with 
            id = "login"
            target = OTaskTarget.OLink $"""{cfg.Url}"""
            cua = Some $"""Login with userid: {cfg.UserId} and password {cfg.Pwd}
After the user is logged in, end the task via the 'task_done' tool.
Note: that usually userid and password are in separate pages. Ensure the page has changed before entering the password.
"""
            toolNames = Some (Toolbox.getToolNames [typeof<Functions.FsOpTaskTools>])
        }            
    let t_AccountAndZip cfg = 
        {OTask.Create() with 
            id = "account_zip"
            target = OTaskTarget.OLink $"""{cfg.Url}"""
            cua = Some $"""Get billing zip code and account number. When found, save these using `save_billing_zip` and `save_account_number` tools, respectively.
Try the `person` icon to find the account settings overview area.
The use Contact & Billing and manage addresses to find the zip.
End the task via the 'task_done' tool when account number and billing zip code have been saved.
For forms, you may have to click in the middle of the form and then scroll to simulate touch-base scroll.
{noWaitInstructions}
"""
            toolNames =
                Toolbox.getToolNames [typeof<Functions.FsOpTaskTools>; typeof<Functions.FsOpNavigator>]
                @ (Toolbox.getToolNames [typeof<AccountTools>]
                   |> List.filter (function ToolName t -> t <>= "zip" || t <>= "account")
                )
                |> Some
        }    
    let t_TransferPin cfg = 
        {OTask.Create() with 
            id = "transfer_pin"
            target = OTaskTarget.OLink $"""{cfg.Url}""" //OTaskTarget.ONone
            cua = Some $"""Locate the page from which the transfer can be generate - but DO NOT click the button to generate the pin.
Use tool `located_transfer_pin_page` to notify that the page has been located.
End the task with `task_done` tool after notification of reaching the page.
You may use the search tool (magnifying glass at top left) and enter `generate transfer pin` to quickly locate the page.
{noWaitInstructions}            
"""
            toolNames =
                Toolbox.getToolNames [typeof<Functions.FsOpTaskTools>; typeof<Functions.FsOpNavigator>]
                @ (Toolbox.getToolNames [typeof<AccountTools>]
                   |> List.filter (function ToolName t -> t <>= "located_transfer_pin_page")
                )
                |> Some            
        }    

    let t_DownloadBill cfg= 
        {OTask.Create() with 
            id = "download_bill"
            target = OTaskTarget.OLink $"""{cfg.Url}/digital/nsa/secure/ui/ngd/bill/billdetails""" //OTaskTarget.ONone
            cua = Some $"""Your task is to obtain selected bill details.    
1. You are the bill details page
2. Expand the `Plans` section and get the detail lines (there are the recurring charges).  
4. Use the `save_bill` tool to save the bill with recurring charges.
5. End the task using the `task_done` tool
{noWaitInstructions}            
"""
            toolNames =
                Toolbox.getToolNames [typeof<Functions.FsOpTaskTools>; typeof<Functions.FsOpNavigator>]
                @ (Toolbox.getToolNames [typeof<AccountTools>]
                   |> List.filter (function ToolName t -> t <>= "bill")
                )
                |> Some            
        }    

    let create cfg = 
        let plan =
            { OPlan.Default with
                description = "Port in number"
                root = ONode.Seq {nodes= [ONode.Leaf (t_AccountAndZip cfg); ONode.Leaf (t_TransferPin cfg)]; description=None}
                //root = ONode.Seq {nodes= [ONode.Leaf (t_DownloadBill cfg); ONode.Leaf (t_TransferPin cfg);]; description=None}
                //root = ONode.Seq {nodes= [ONode.Leaf (t_Login cfg); ONode.Leaf (t_AccountAndZip); ONode.Leaf (t_DownloadBill cfg); ONode.Leaf (t_TransferPin cfg);]; description=None}
            }
        plan

module PortInPlanRunMobile = 
    open System.Threading
    open Microsoft.Extensions.DependencyInjection
    open FSharp.Control
    
    let monitorTask poster (h:ManualResetEvent) (completedTask:Ref<TaskState<_,_> option>) (status:Ref<T_Status>) (bus:WBus<_,_>) =
        let comp =
            let channel = bus.agentChannel.Subscribe("app")
            channel.Reader.ReadAllAsync()
            |> AsyncSeq.ofAsyncEnum
            |> AsyncSeq.iterAsync (fun msg -> async {
                match msg with
                | PortInMsgOut.APo_Done t ->
                    completedTask.Value <- Some t.task
                    status.Value <- if t.abnormal then T_Timeout else T_Done
                    h.Set() |> ignore
                    do! t.task.driver.saveState()
                | PortInMsgOut.APo_Error e -> printfn "{a}"; status.Value <- T_Error e;  h.Set() |> ignore
                | PortInMsgOut.APo_Usage us -> OPlan.printTaskUsage us
                | PortInMsgOut.Apo_Preview c -> do! poster (RunTaskMessage.Preview c)
                | PortInMsgOut.APo_Action a ->  printfn $"{a}"
                | _ -> () //ignore messages for other agents
            })
        async {
            match! Async.Catch(comp) with
            | Choice1Of2 _ -> Log.info "task ended"
            |  Choice2Of2 ex -> Log.exn(ex,"monitorTask")
        }
        |> Async.Start

    let runTask waitForPreview poster stepper (ot:OTaskRun) : (Async<OTaskRun>) = async {
        use h = new ManualResetEvent(false)
        let startUrl = (ot.task.target.TargetString())
        let completedTask = ref None
        let taskStatus = ref ot.status
        let driver = FsPlay.MauiWebViewDriver.create().driver
        let bus = WBus.Create<_,_>()
        //configure tools that may be needed tasks
        use provider = ot.kernel.BuildServiceProvider()
        // let memory = provider.GetService<FsOpCore.Functions.FsOpMemory>()
        // if memory = Unchecked.defaultof<_> then failwith "memory service missing"
        let navTools = Functions.FsOpNavigator(startUrl,driver)
        let taskTools = Functions.FsOpTaskTools(fun () -> async {bus.PostToFlow (W_Msg (PortInMsgIn.APi_TerminateTask {|abnormal=false|}))})
        let accountTools = AccountTools poster
        let tools = Toolbox.makeTools [navTools :> obj; taskTools; accountTools]
        //only keep tools that this particular task needs
        let tools = Toolbox.filter ot.task.toolNames tools //only keep tools that are referenced by the task
        
        let t0 = TaskState.Create<PortInMsgIn,PortInMsgOut>  //initial task state
                    ot.cancelToken
                    ot.task.id
                    startUrl
                    bus
                    driver
                    ot.task.cua.Value
                    ot.kernel
                    tools
        let printer = function
            | W_Msg (CUAi_ComputerCall(a,_, x ::_)) -> $"{a} - {x}"
            | x -> $"{x}"
        let flow = TaskFlow.createStepped waitForPreview stepper printer t0
//        let flow = TaskFlow.create t0
        flow.Post PortInMsgIn.APi_Start
        monitorTask poster h completedTask taskStatus bus
        OPlan.startTimer ot.task.allowedSec flow (PortInMsgIn.APi_TerminateTask {|abnormal=true|}) //sends task terminate message when this timer expires
        let! r = Async.AwaitWaitHandle(h,ot.task.allowedSec * 1000 * 3) //max wait for task to finish in case its stuck
        match completedTask.Value,taskStatus.Value with
        | Some t, _ -> return {ot with messages = t.cuaMessages; usage = OPlan.sumUsages t.usage; status=taskStatus.Value}
        | None, T_Error _ 
        | None, T_Timeout -> return {ot with status=taskStatus.Value}
        | None, _         -> return failwith "No output from step"
    }

    let run cancelToken waitForPreview poster stepper cfg =
        let comp = async {
            let runPlan = PortPlanMobile.create cfg
            let kernel = OPlan.defaultKernel Map.empty  None
            let s1r = OPlanRun.Create cancelToken runPlan kernel (runTask waitForPreview poster stepper)
            let! s2r = OPlan.run s1r
            return s2r            
        }
        async {
            match! Async.Catch comp with
            | Choice1Of2 pr -> do! poster (RunTaskMessage.PlanDone pr)
            | Choice2Of2 ex -> Log.exn(ex,"PortInPlanMobile.run")
        }

*)