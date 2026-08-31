using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Timesheets;

public class CreateTimesheetDto
{
    public DateTime WeekStartDate { get; set; }   // any date in the target week; normalized to Monday
}

public class CreateTimesheetEntryDto
{
    public DateTime WorkDate { get; set; }
    public string? ProjectId { get; set; }
    public string? AssignmentId { get; set; }
    public string? ProjectTaskId { get; set; }
    public string  Description { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public decimal OvertimeHours { get; set; }
    /// <summary>Defaults to true when omitted — most logged time is chargeable.</summary>
    public bool? IsBillable { get; set; }
}

public class UpdateTimesheetEntryDto
{
    public DateTime? WorkDate { get; set; }
    public string? ProjectId { get; set; }
    public string? AssignmentId { get; set; }
    public string? ProjectTaskId { get; set; }
    public string? Description { get; set; }
    public decimal? Hours { get; set; }
    public decimal? OvertimeHours { get; set; }
    public bool? IsBillable { get; set; }
}

public class RequestOvertimeDto
{
    public string? Reason { get; set; }
}

public class ReviewTimesheetDto
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
}

public class TimesheetEntryReadDto
{
    public string Id { get; set; } = string.Empty;
    public string TimesheetId { get; set; } = string.Empty;
    public DateTime WorkDate { get; set; }
    public string? ProjectId { get; set; }
    public string? AssignmentId { get; set; }
    public string? ProjectTaskId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public decimal OvertimeHours { get; set; }
    public bool IsBillable { get; set; } = true;
    public bool IsOvertimeApproved { get; set; }
    public string? OvertimeRequestRef { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? SourceRef { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TimesheetReadDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public DateTime WeekStartDate { get; set; }
    public DateTime WeekEndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TimesheetEntryReadDto> Entries { get; set; } = new();
}

public class TimesheetFilterParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public string? EmployeeId { get; set; }
    public string? DepartmentId { get; set; }
    public string? Status { get; set; }
    public bool SortDescending { get; set; } = true;
}
