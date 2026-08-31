namespace OperationsService.Core.Enums;

public enum ProjectStatus
{
    Draft, Planning, PendingMdApproval, PendingFinanceApproval, Active, OnHold, Completed, Closed, Cancelled
}

public enum ProjectType
{
    Service, Construction, Calibration, ICT, CRM, Sales, General
}

public enum RiskLevel { Low, Medium, High }

public enum MilestoneStatus { NotStarted, InProgress, Completed, Delayed }

public enum TaskStatus { NotStarted, InProgress, Done, Blocked }

// Budget joins MD/Finance so a budget revision lands in the same project approvals trail rather
// than inventing a parallel one.
public enum ApprovalType { MD, Finance, Budget }

public enum ApprovalStatus { Pending, Approved, Rejected }

public enum BudgetCategory { Labour, Materials, Equipment, Fleet, Subcontractor, Other }

/// <summary>
/// Lifecycle of a detailed budget. Only one version per project is Approved at a time, and that
/// version is the project's baseline — revising the budget means raising a new version, so what was
/// originally approved is never overwritten.
/// </summary>
public enum BudgetVersionStatus { Draft, PendingApproval, Approved, Rejected, Superseded }

/// <summary>Unit a contract rate is priced in. Drives qty x rate on a budget line.</summary>
public enum RateUnit { Hour, Day, Item, Visit, Kilometre, LumpSum }
