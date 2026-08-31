using HrService.Core.DTOs.Payroll;

namespace HrService.Core.Interfaces.Services;

/// <summary>
/// H6 (HR-007/008/012, P9 + P12) — overtime pre-approval and the monthly payroll run: the engine that turns
/// H5's configuration into payslips, and the finance journal that follows approval.
/// <para><b>H5 configured, H6 computes.</b> Nothing here is edited by hand: every figure on a payslip is
/// derived from the salary assignment, the structure's components, the dated rate tables, approved overtime
/// and H4's unpaid days. If a number looks wrong, one of those inputs is wrong.</para>
/// </summary>
public interface IPayrollRunService
{
    // ── Overtime (P12) ──
    Task<List<OvertimeRequestDto>> ListOvertimeAsync(string? employeeId, string? status, DateTime? from, DateTime? to);
    /// <summary>Pre-approval only — the rate type comes from the date, and retrospective claims for a period
    /// already paid are refused.</summary>
    Task<PayrollActionResult> RequestOvertimeAsync(RequestOvertimeDto dto, string userId);
    Task<PayrollActionResult> DecideOvertimeAsync(string id, DecideOvertimeDto dto, string userId, string? userName);

    // ── Payroll runs (P9) ──
    Task<List<PayrollRunDto>> ListRunsAsync(string? status);
    /// <summary>The run with its payslips and every payslip line.</summary>
    Task<PayrollRunDto?> GetRunAsync(string id);
    Task<List<PayslipDto>> ListPayslipsAsync(string? runId, string? employeeId, int? year);

    /// <summary>Opens a run for a period. Refused when a live run already exists for it (P9 step 9.1).</summary>
    Task<PayrollActionResult> CreateRunAsync(CreatePayrollRunDto dto, string userId);

    /// <summary>
    /// Builds every payslip for the run. Safe to repeat while the run is unapproved: it first releases the
    /// overtime and unpaid-absence claims it previously took, so recomputing cannot double-count.
    /// </summary>
    Task<PayrollActionResult> ComputeRunAsync(string id, string userId);

    /// <summary>The journal the run would post, for inspection before approval commits it.</summary>
    Task<JournalPreviewDto?> PreviewJournalAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Approve (a second officer — never the person who computed it) or cancel. Approval posts the payroll
    /// journal to finance; cancellation releases every claim the run held.
    /// </summary>
    Task<PayrollActionResult> DecideRunAsync(string id, DecidePayrollRunDto dto, string? tenantSchema, string userId, string? userName, CancellationToken ct = default);

    /// <summary>Re-posts the journal for an approved run whose finance hop failed. The run is the record;
    /// posting is retried rather than the pay being recomputed.</summary>
    Task<PayrollActionResult> RetryJournalAsync(string id, string? tenantSchema, string userId, CancellationToken ct = default);
}
