namespace OperationsService.Core.Enums;

public enum TimesheetStatus { Draft, Submitted, Approved, Rejected }

/// <summary>
/// Where a timesheet line came from. <c>CheckIn</c> (PR2) is time derived from a field check-in /
/// check-out pair rather than typed in by hand — the geo-stamped clock is the more trustworthy record
/// of when someone was actually on site.
/// </summary>
public enum TimesheetEntrySource { Manual, FsrImport, CheckIn }
