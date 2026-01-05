namespace FsPlaySamples.Cua.Agentic

type ActionPreview = {click:(int*int) option; action:string}
         
[<RequireQualifiedAccess>]
type FromAgent =
    | Summary of string
    | Preview of ActionPreview
    | PlanDone
    
type AgentMsg =
    | Ag_Preview of ActionPreview
    | Ag_Action of ActionPreview  
 
type FlowMsg =
    | Fl_Start
    | Fl_Terminate
