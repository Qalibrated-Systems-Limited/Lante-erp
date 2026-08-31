using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Infrastructure.Services;

/// <summary>
/// O4 — default HR seam: a config-gated no-op. Overtime is accepted unverified against a synthetic
/// reference and payroll postings are logged (not sent) so timesheets are never blocked before
/// hr-service exists. <c>Hr:Enabled=true</c> + <c>HrService:BaseUrl</c> swaps in
/// <see cref="HttpHrGateway"/>, which verifies against HR's real overtime register.
/// </summary>
public class NoOpHrGateway(
    IConfiguration config,
    ILogger<NoOpHrGateway> logger) : IHrGateway
{
    /// <summary>
    /// Accepts the overtime without checking anything — there is no HR register to check against. The
    /// reference is marked UNVERIFIED so an entry approved by the stub is distinguishable from one
    /// backed by a real HR record.
    /// </summary>
    public Task<OvertimeApprovalResult> ResolveOvertimeApprovalAsync(
        OvertimeApprovalQuery query, CancellationToken ct = default)
    {
        // Deterministic synthetic ref (no Guid/clock — those are unavailable / non-deterministic here).
        var reference = $"OT-UNVERIFIED-{query.TimesheetEntryId}";
        if (config.GetValue("Hr:Enabled", false))
            logger.LogWarning("Hr:Enabled=true but no HR gateway is wired — accepting overtime {Ref} ({Hours}h) unverified.",
                reference, query.OvertimeHours);
        else
            logger.LogInformation("[HR stub] Overtime {Ref}: employee {Emp} {Hours}h on {Date:yyyy-MM-dd} — accepted unverified.",
                reference, query.EmployeeId, query.OvertimeHours, query.WorkDate);

        return Task.FromResult(new OvertimeApprovalResult(
            true, reference, "HR integration is disabled — overtime was accepted without verification."));
    }

    public Task PostTimesheetToPayrollAsync(PayrollPosting posting, CancellationToken ct = default)
    {
        logger.LogInformation("[HR stub] Payroll posting: employee {Emp} week-ending {Week:yyyy-MM-dd} — {Total}h ({OT}h OT).",
            posting.EmployeeId, posting.WeekEndDate, posting.TotalHours, posting.OvertimeHours);
        return Task.CompletedTask;
    }

    public Task PostPayrollDeductionAsync(PayrollDeduction deduction, CancellationToken ct = default)
    {
        logger.LogInformation("[HR stub] Payroll deduction: employee {Emp} amount {Amount:0.00} ({Ref}: {Reason}).",
            deduction.EmployeeId, deduction.Amount, deduction.IncidentReference, deduction.Reason);
        return Task.CompletedTask;
    }
}
