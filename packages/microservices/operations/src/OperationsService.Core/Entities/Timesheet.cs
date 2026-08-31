using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// O4 — TIMESHEET: one employee's weekly timesheet. Entries roll up to Total/Overtime hours. Weekly
/// line-manager approval posts labour to Finance (project actuals) and hours to HR (payroll).
/// </summary>
public class Timesheet : BaseEntity
{
    public string EmployeeId   { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;

    public DateTime WeekStartDate { get; set; }   // Monday of the week (date only, UTC)
    public DateTime WeekEndDate   { get; set; }

    public TimesheetStatus Status { get; set; } = TimesheetStatus.Draft;

    public decimal TotalHours    { get; set; }
    public decimal OvertimeHours { get; set; }

    public DateTime? SubmittedAt     { get; set; }
    public string?   ApprovedBy      { get; set; }
    public DateTime? ApprovedAt      { get; set; }
    public string?   RejectionReason { get; set; }

    public ICollection<TimesheetEntry> Entries { get; set; } = new List<TimesheetEntry>();
}
