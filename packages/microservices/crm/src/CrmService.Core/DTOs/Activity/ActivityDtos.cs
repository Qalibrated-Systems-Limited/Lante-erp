using CrmService.Core.Enums;

namespace CrmService.Core.DTOs.Activity;

// ── Interactions ──
public class LogInteractionDto
{
    public InteractionType InteractionType { get; set; } = InteractionType.Call;
    public string? ContactId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Outcome { get; set; }
    public DateTime? NextActionDate { get; set; }
}
public class CustomerInteractionDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string InteractionType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Outcome { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime InteractionDate { get; set; }
    public DateTime? NextActionDate { get; set; }
}

// ── Tasks ──
public class CreateTaskDto
{
    public string? CustomerId { get; set; }
    public string? OpportunityId { get; set; }
    public string? AssignedTo { get; set; }
    public string TaskType { get; set; } = "FollowUp";
    public string Subject { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
}
public class ActivityTaskDto
{
    public string Id { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? OpportunityId { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public bool IsAutoCreated { get; set; }
    public DateTime? CompletedAt { get; set; }
}
public class TaskFilterParams
{
    public string? AssignedTo { get; set; }
    public string? CustomerId { get; set; }
    public string? Status { get; set; }
    public bool? Overdue { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
public record TaskListResult(List<ActivityTaskDto> Items, int Total, int OpenCount, int OverdueCount);

// ── Visits ──
public class LogVisitDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string? EmployeeId { get; set; }
    public DateTime? VisitDate { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public string? NextAction { get; set; }
    public double? GpsLat { get; set; }
    public double? GpsLng { get; set; }
}
public class ClientVisitDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public string? NextAction { get; set; }
    public double? GpsLat { get; set; }
    public double? GpsLng { get; set; }
}

// ── Visit targets ──
public class SaveVisitTargetDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int MinVisitsPerWeek { get; set; }
    public int MinVisitsPerMonth { get; set; }
}
public class VisitTargetDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int MinVisitsPerWeek { get; set; }
    public int MinVisitsPerMonth { get; set; }
}

// ── Daily activity log ──
public class LogDailyActivityDto
{
    public string? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime? LogDate { get; set; }
    public int CallsMade { get; set; }
    public int MeetingsHeld { get; set; }
    public int ProposalsSent { get; set; }
    public int Visits { get; set; }
    public string? Notes { get; set; }
}
public class SalesActivityLogDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime LogDate { get; set; }
    public int CallsMade { get; set; }
    public int MeetingsHeld { get; set; }
    public int ProposalsSent { get; set; }
    public int Visits { get; set; }
    public string? Notes { get; set; }
}
