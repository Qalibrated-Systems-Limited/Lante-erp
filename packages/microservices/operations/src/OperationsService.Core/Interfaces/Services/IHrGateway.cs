namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// O4 — cross-module seam to hr-service. Config-gated: the default no-op keeps operations decoupled
/// from HR being present, and <c>Hr:Enabled=true</c> + <c>HrService:BaseUrl</c> swaps in the real HTTP
/// client (PR2).
/// </summary>
public interface IHrGateway
{
    /// <summary>
    /// Confirms that the overtime on a timesheet entry was pre-approved in HR.
    ///
    /// <para>PR2 — this <b>verifies</b>, it does not create. HR's overtime control (ATT-007) is
    /// pre-approval: <c>POST /hr/overtime</c> refuses any date earlier than today, because a claim for
    /// a day already worked defeats the point — the manager can no longer decide whether the cost was
    /// worth incurring. A timesheet is filled in <i>after</i> the work, so operations asking HR to
    /// create the request would fail on nearly every real entry, and the previous behaviour of
    /// assuming approval turned the ops-side gate into a rubber stamp. Instead the manager approves in
    /// HR, where the control lives, and the timesheet checks that the approval exists.</para>
    /// </summary>
    Task<OvertimeApprovalResult> ResolveOvertimeApprovalAsync(OvertimeApprovalQuery query, CancellationToken ct = default);

    /// <summary>
    /// PR2 — deliberately a no-op in every implementation, including the HTTP one.
    ///
    /// <para>HR payroll is salary-based: grades → structures → salaries → a monthly run. There is no
    /// hours-in endpoint, and that is not an oversight — for salaried staff, regular hours do not move
    /// pay. The only time that changes pay is overtime, which reaches payroll through HR's own
    /// pre-approval record (see <see cref="ResolveOvertimeApprovalAsync"/>), not through this call.
    /// Kept on the interface so the fan-out at timesheet approval still reads as the complete picture
    /// rather than looking like a missing hop.</para>
    /// </summary>
    Task PostTimesheetToPayrollAsync(PayrollPosting posting, CancellationToken ct = default);

    /// <summary>O9 — post a negligence payroll deduction against an employee.</summary>
    Task PostPayrollDeductionAsync(PayrollDeduction deduction, CancellationToken ct = default);
}

public record OvertimeApprovalQuery(
    string EmployeeId,
    string TimesheetEntryId,
    DateTime WorkDate,
    decimal OvertimeHours);

/// <param name="Approved">
/// Whether HR holds an approved overtime record covering this employee and date. False leaves the
/// entry un-approved, which keeps the timesheet submit gate closed.
/// </param>
/// <param name="Reference">HR's overtime record id, stored on the entry as the audit trail.</param>
/// <param name="Message">Why it was not approved, surfaced to the person filling the timesheet.</param>
/// <param name="ApprovedHours">
/// The hours HR actually approved. A record approved for fewer hours than the entry claims is not a
/// blanket approval for the larger figure.
/// </param>
public record OvertimeApprovalResult(
    bool Approved,
    string? Reference = null,
    string? Message = null,
    decimal? ApprovedHours = null);

public record PayrollPosting(
    string EmployeeId,
    DateTime WeekEndDate,
    decimal TotalHours,
    decimal OvertimeHours);

public record PayrollDeduction(
    string EmployeeId,
    string IncidentReference,
    decimal Amount,
    string Reason);
