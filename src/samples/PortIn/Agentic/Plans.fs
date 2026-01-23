namespace FsPlaySamples.PortIn.Agentic

open AICore
open FsPlaySamples.PortIn
open System.ComponentModel
open Microsoft.SemanticKernel

/// Configuration for PortIn plan
type PlanConfig = {Url:string;}
    with static member Default = {Url=""}
    
type VzState = {zip:string option; account:string option; transfer_pin:string option; transfer_pin_page:bool }
    with static member Default = {zip=None; account=None; transfer_pin=None; transfer_pin_page=false}
    
/// Tools for saving account-related data during port-in automation
type AccountTools(poster: FromAgent -> unit) =

    [<KernelFunction("save_transfer_pin")>]
    [<Description("Save the transfer pin")>]
    member this.save_transfer_pin(pin:string) =
        let comp = async {
            try
                Log.info $"[AccountTools] Transfer pin {pin}"
                poster (FromAgent.TransferPin pin)
                return "pin saved"
            with ex ->
                Log.exn(ex, nameof this.save_transfer_pin)
                return $"error occurred while trying to save pin"
        }
        Async.StartAsTask comp

    [<KernelFunction("located_transfer_pin_page")>]
    [<Description("Notify that transfer pin page has been located")>]
    member this.located_transfer_pin_page() =
        let comp = async {
            try
                Log.info $"[AccountTools] Transfer pin page reached"
                poster (FromAgent.TransferPinPageLocated)
                return "notification sent"
            with ex ->
                Log.exn(ex, nameof this.located_transfer_pin_page)
                return $"error occurred while record 'reached_transfer_pin_page'"
        }
        Async.StartAsTask comp

    [<KernelFunction("save_bill")>]
    [<Description("Save the bill details")>]
    member this.save_bill(plan_charges: string list, total_amount: string) =
        let comp = async {
            try
                let bill = {PlanCharges = plan_charges; TotalAmount = Some total_amount}
                Log.info $"[AccountTools] Bill: {bill}"
                poster (FromAgent.Bill bill)
                return "bill details saved"
            with ex ->
                Log.exn(ex, nameof this.save_bill)
                return $"error occurred while trying to save bill details"
        }
        Async.StartAsTask comp

    [<KernelFunction("save_account_number")>]
    [<Description("Save the account number")>]
    member this.save_account_number(account_number: string) =
        let comp = async {
            try
                Log.info $"[AccountTools] saved account number {account_number}"
                poster (FromAgent.AccountNumber account_number)
                return "account number saved"
            with ex ->
                Log.exn(ex, nameof this.save_account_number)
                return $"error occurred while trying to save account number"
        }
        Async.StartAsTask comp

    [<KernelFunction("save_billing_zip")>]
    [<Description("Save the billing zip code")>]
    member this.save_billing_zip(zip: string) =
        let comp = async {
            try
                Log.info $"[AccountTools] save billing zip {zip}"
                poster (FromAgent.BillingZip zip)
                return "billing zip saved"
            with ex ->
                Log.exn(ex, nameof this.save_billing_zip)
                return $"error occurred while trying to save billing zip"
        }
        Async.StartAsTask comp

/// Tools for task control
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

    [<KernelFunction("home")>]
    [<Description("Load the home page")>]
    member this.home() =
        let comp = async {
            try
                Log.info $"[TaskTools] load home page"
                poster Ag_Task_Home
                return "loading home"
            with ex ->
                Log.exn(ex, nameof this.end_task)
                return $"error occurred while trying to load home"
        }
        Async.StartAsTask comp

module Plans =
    open FsPlan
    
    ///logic to end a task early when all the relevant data has been collected for the task
    let interceptor (st:Ref<VzState>) (bus:CuaBus) poster msg =
        match msg with
        | FromAgent.TransferPin p ->
            st.Value <- {st.Value with transfer_pin = Some p}
            bus.PostToAgent Ag_Task_End
        | FromAgent.TransferPinPageLocated ->
            st.Value <- {st.Value with transfer_pin_page = true}
            bus.PostToAgent Ag_Task_End
        | FromAgent.AccountNumber a ->
            st.Value <- {st.Value with account = Some a}
            if st.Value.zip.IsSome then bus.PostToAgent Ag_Task_End        
        | FromAgent.BillingZip b ->
            st.Value <- {st.Value with zip = Some b}
            if st.Value.zip.IsSome then bus.PostToAgent Ag_Task_End            
        | _ -> ()
        poster msg

    let noWaitInstructions = """Note: In most cases you are provided with a screenshot after the 'computer' action has been taken.
Generally, the 'wait' action is not required.
If lost, use the 'home' tool to get back to the home page.
"""

    /// Create PortIn tasks based on configuration
    let createPortInTasks (cfg:PlanConfig) = [
        // Login task - interactive,<<<<<<<<<<<, user must login first
        {   id = Tid "login"
            task = Cu_Interactive (Some (Target cfg.Url), $"Login, then click {Icons.next}")
            description = $"Login"
            toolNames = []
        }

        // Account and Zip task - CUA driven
        {   id = Tid "account_zip"
            task = Cu_Cua (Some (Target cfg.Url))
            description = $"""Get billing zip code and account number. When found, save these using `save_billing_zip` and `save_account_number` tools, respectively.
Try the `person` icon to find the account settings overview area.
Then use Contact & Billing and manage addresses to find the zip.
{noWaitInstructions}"""
            toolNames = [ToolName "save_billing_zip"; ToolName "save_account_number"]
        }

        // Transfer PIN task - CUA driven
        {   id = Tid "transfer_pin"
            task = Cu_Cua (Some (Target cfg.Url))
            description = $"""Locate the page from which the transfer PIN can be generated - but DO NOT click the button to generate the pin.
Use tool `located_transfer_pin_page` to notify that the page has been located.
You may use the search tool (magnifying glass at top left) and enter `generate transfer pin` to quickly locate the page.
Scroll down in search results to find the 'number transfer pin` page.
There may be distractive, filler content in the top part of the search results.
{noWaitInstructions}"""
            toolNames = [ToolName "located_transfer_pin_page"]
        }

        // Download Bill task - CUA driven
        {   id = Tid "download_bill"
            task = Cu_Cua (Some (Target $"{cfg.Url}/digital/nsa/secure/ui/ngd/bill/billdetails"))
            description = $"""Your task is to obtain selected bill details.
1. You are on the bill details page
2. Expand the `Plans` section and get the detail lines (these are the recurring charges)
3. Use the `save_bill` tool to save the bill with recurring charges
4. End the task using the `end_task` tool
{noWaitInstructions}"""
            toolNames = [ToolName "save_bill"; ToolName "end_task"]
        }
    ]

    /// Create the PortIn plan with tasks in sequence
    let createPortInPlan (cfg:PlanConfig) =
        let inset = set [Tid "login"; Tid "transfer_pin"; Tid "account_zip"]
        let tasks = createPortInTasks cfg
        let tasks = tasks |> List.filter  (fun x -> inset.Contains x.id)        
        let taskSequence = tasks |> List.map _.id
        {
            tasks = tasks |> List.map (fun x -> x.id, x) |> Map.ofList
            flow = FsPlanFlow.Sequential taskSequence
        }

    /// The current test plan - uses default URL from settings
    let testPlan =
        let cfg = {
            Url = Settings.Environment.url()
        }
        createPortInPlan cfg
