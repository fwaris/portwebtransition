namespace FsPlaySamples.PortIn.Agentic

open AICore
open FsPlan
open Microsoft.Extensions.AI
open RTFlow

type Target = Target of string

/// Bill details captured during automation
type Bill = {
    PlanCharges: string list
    TotalAmount: string option
}
    with static member Default = {PlanCharges=[]; TotalAmount=None}

type Cu_Task =
    | Cu_Interactive of (Target option * string)
    | Cu_Cua  of (Target option)
    
type Cu_Task_Status = Done | Error of string    
type Cu_Task_Output = {history:ChatMessage list; usage:UsageMap; status:Cu_Task_Status}
    
type ActionPreview = {click:(int*int) option; action:string}
[<RequireQualifiedAccess>]
type CallType = 
    | Cua of Action list*FunctionCallContent
    | NonCua of FunctionCallContent
    | Invalid of string*FunctionCallContent

type TaskContext = {screenDimensions:int*int; aiContext:AIContext }
    with static member Default = {screenDimensions=0,0; aiContext=AIContext.Default }
type CallsResult = {Handled:AIContent list; Pending:CallType list; taskContext:TaskContext}
         
[<RequireQualifiedAccess>]
type FromAgent =
    | Summary of string*string
    | Preview of ActionPreview
    | PlanDone of Runner<Cu_Task,Cu_Task_Output>
    | LoadTask of (Target option*string)
    // PortIn-specific messages
    | AccountNumber of string
    | BillingZip of string
    | TransferPin of string
    | TransferPinPageLocated
    | Bill of Bill 
    
type AgentMsg =
    | Ag_Plan_Run of Runner<Cu_Task,Cu_Task_Output>
    | Ag_Plan_Next of Runner<Cu_Task,Cu_Task_Output>
    | Ag_Plan_Done of Runner<Cu_Task,Cu_Task_Output>
    | Ag_Plan_DoneTask of Cu_Task_Output 
    //
    | Ag_Task_Continue of {|pendingCalls:CallType list; results:AIContent list; context:TaskContext|}
    | Ag_Task_Run of FsTask<Cu_Task>*TaskContext
    | Ag_Task_Restart of TaskContext
    | Ag_Task_End
    | Ag_Task_Home
    //
    | Ag_App_ComputerCall of CallType list*string option
    | Ag_App_Home of string
    | Ag_Usage of UsageMap
    | Ag_FlowDone of {|abnormal:bool|}
    
type FlowMsg =
    | Fl_Start
    | Fl_Terminate of {|abnormal:bool|}

type CuaBus = WBus<FlowMsg,AgentMsg>
type CuaRunner = Runner<Cu_Task,Cu_Task_Output>
