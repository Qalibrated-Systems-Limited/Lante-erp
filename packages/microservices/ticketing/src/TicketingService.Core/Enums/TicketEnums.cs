namespace TicketingService.Core.Enums;

public enum TicketPriority { Low, Medium, High, Critical }

public enum TicketStatus { New, Assigned, InProgress, Pending, Escalated, Resolved, Closed, Reopened }

public enum TicketSource { Manual, SystemTriggered, CRM, SafetyReport, Scheduled }

public enum EscalationLevel { Supervisor, DepartmentHead, MD }

public enum LinkedEntityType { Project, Fleet, Employee, Inventory, Finance, Safety, Quality, None }

public enum WorkflowTriggerEvent
{
    TicketCreated,
    TicketUpdated,
    StatusChanged,
    PriorityChanged,
    CommentAdded,
    AgentReplied,
    SLABreached,
    TicketEscalated
}

public enum WorkflowActionType
{
    AssignToUser,
    AssignToDepartment,
    ChangeStatus,
    AddTag,
    RemoveTag,
    SendNotification,
    AddComment,
    EscalateTo,
    CreateTechnicianAssignment,
    CreateFleetTrip
}

public enum WorkUpdateType { WorkStarted, WorkSubmitted, WorkCompleted, WorkCancelled }

/// Why a ticket ended up Closed — so reports separate work that was done from work that wasn't.
/// Completed/AutoClosed carry a ResolvedAt (count in resolution-time analytics); Cancelled does not.
public enum ClosureType { Completed, Cancelled, AutoClosed }

/// Who is driving a status transition. Manual transitions are governed by the ValidTransitions
/// table; automated sources may force a transition (matching prior direct-write behaviour) while
/// still going through the single gate for stamping, history and the evidence check.
public enum TransitionSource { Manual, WorkUpdate, Workflow, Escalation, FollowUp, System }

/// D5 — the five sequential steps of the complaint-handling workflow (HELP-007). Order is fixed and
/// enforced: Acknowledge → Investigate → Respond → SatisfactionCheck → Close.
public enum ComplaintStepType { Acknowledge, Investigate, Respond, SatisfactionCheck, Close }

/// A complaint step is Pending (locked, earlier steps not done), Active (the current step to work),
/// or Completed. Only the Active step can be completed.
public enum ComplaintStepStatus { Pending, Active, Completed }

public enum WorkflowConditionOperator
{
    Equals,
    NotEquals,
    Contains,
    IsEmpty,
    IsNotEmpty,
    GreaterThan,
    LessThan
}
