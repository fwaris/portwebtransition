namespace FsPlaySamples.Cua.Agentic

open AICore
open FsPlaySamples.Cua
open System
open System.ComponentModel
open Microsoft.SemanticKernel

type ArticleTools(poster: FromAgent -> unit) = 

    [<KernelFunction("save_summary")>]
    [<Description("Save the article summary")>]
    member this.save_summary(title:string, summary:string) = 
        let comp = async {
            try
                Log.info $"[ArticleTools] {nameof this.save_summary} {title}\n{summary}"
                poster (FromAgent.Summary (title,summary))
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

module Plans =    
    open FsPlan
    let getOutput (t:FsTask<Cu_Task>) (kernel:IServiceProvider) = async {
        return ""
    }
    
    let amazonTasks = [
        {   id = Tid "intro"
            task = Cu_Interactive (Some (Target "https://amazon.com"), "Login, then click Continue")
            description = "introduction"
            toolNames = []
        }
        {   id = Tid "find_camera_case"
            task = Cu_Cua None
            description = "Find a camera case for iphone 16 max that has built-in screen protector. For all suitable case use `save_summary` tool to save the data."
            toolNames = [ToolName "save_summary"]                       
        }               
    ]
    
    let twitterTasks = [
        {   id = Tid "intro"
            task = Cu_Interactive (Some (Target "https://x.com"), "Login, then click Continue")
            description = "introduction"
            toolNames = []
        }
        {   id = Tid "find_gen_ai_posts"
            task = Cu_Cua None
            description = "search for posts related to NeuroSymbolic AI and save the summaries using the `save_summary` tool. Save as soon as you find an interesting post. Scroll down to see relevant posts"
            toolNames = [ToolName "save_summary"]                       
        }        
    ]
    
    let redditTasks = [
        {   id = Tid "intro"
            task = Cu_Interactive (Some (Target "https://reddit.com"), "Login, then click Continue")
            description = "introduction"
            toolNames = []
        }
        {   id = Tid "find_gen_ai_posts"
            task = Cu_Cua None
            description = """Find posts related to 'neurosymbolic ai'. 
Make sure to click the 'Ask' button after entering a search term.
Summarize each thread and the `save_summary` tool to save the thread summary.
"""
            toolNames = [ToolName "save_summary"]                       
        }               
    ]          
    let testPlan =
        let tasks = amazonTasks //redditTasks //amazonTasks //twitterTasks
        {
            tasks = tasks |> List.map (fun x ->x.id,x) |> Map.ofList
            flow = FsPlanFlow.Sequential (tasks |> List.map _.id)
        }
