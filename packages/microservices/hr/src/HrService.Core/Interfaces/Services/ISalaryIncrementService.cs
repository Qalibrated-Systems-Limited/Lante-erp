using HrService.Core.DTOs.Payroll;

namespace HrService.Core.Interfaces.Services;

/// <summary>
/// H8 (P13, HR-013) — the salary increment workflow: HR proposes, the MD approves, and approval writes the
/// new salary assignment for the effective month, which the H6 payroll run then picks up on its own.
/// <para><b>Two hard gates</b> (HR-029 mandatory training, HR-035 professional certification) are checked
/// through H7 before a proposal can be raised — and checked AGAIN at approval, because eligibility can lapse
/// in between and the MD's signature must not be the thing that bypasses a control.</para>
/// </summary>
public interface ISalaryIncrementService
{
    Task<List<SalaryIncrementDto>> ListAsync(string? status, string? employeeId);

    /// <summary>What HR needs before proposing: the current salary, the gates, and what is left to fix.</summary>
    Task<IncrementPreviewDto?> PreviewAsync(string employeeId, CancellationToken ct = default);

    /// <summary>Raises a proposal for the MD. Refused outright when a gate fails — a blocked increment is
    /// never written, so nothing ineligible can sit in the MD's queue.</summary>
    Task<PayrollActionResult> ProposeAsync(ProposeIncrementDto dto, string userId, CancellationToken ct = default);

    /// <summary>
    /// MD approval, rejection, or withdrawal by HR. Approval re-checks eligibility, then writes the new
    /// <c>EmployeeSalary</c> as approved and supersedes the previous one — the MD approving the increment IS
    /// the approval of the pay, so asking for it twice would duplicate a control already exercised.
    /// </summary>
    Task<PayrollActionResult> DecideAsync(string id, DecideIncrementDto dto, string? tenantSchema, string userId, string? userName, CancellationToken ct = default);
}
