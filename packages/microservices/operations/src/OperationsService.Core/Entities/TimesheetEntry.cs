using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// O4 — TIMESHEET_ENTRY: one line on a weekly timesheet (a day's work against a project/assignment).
/// Overtime hours require pre-approval (HR OVERTIME_REQUEST) before the sheet can be submitted.
/// FsrImport entries are auto-created when a Field Service Report is approved.
/// </summary>
public class TimesheetEntry : BaseEntity
{
    public string TimesheetId { get; set; } = string.Empty;
    public DateTime WorkDate  { get; set; }

    public string? ProjectId     { get; set; }   // for the Finance project-actuals journal
    public string? AssignmentId  { get; set; }
    public string? ProjectTaskId { get; set; }   // PR2 — which task the time was spent on
    public string  Description   { get; set; } = string.Empty;

    public decimal Hours         { get; set; }   // regular hours
    public decimal OvertimeHours { get; set; }

    /// <summary>
    /// PR2 — whether the hours are chargeable to the client. Non-billable time (travel, rework,
    /// warranty) still costs the project and still posts to the labour journal; it simply must not
    /// end up on an invoice. Defaults to true so existing manual entries keep their meaning.
    /// </summary>
    public bool IsBillable { get; set; } = true;

    // OT pre-approval (HR OVERTIME_REQUEST seam)
    public bool    IsOvertimeApproved { get; set; }
    public string? OvertimeRequestRef { get; set; }

    public TimesheetEntrySource Source { get; set; } = TimesheetEntrySource.Manual;
    public string? SourceRef { get; set; }   // e.g. ServiceReport id for FsrImport (dedup key)

    public Timesheet Timesheet { get; set; } = null!;
}
