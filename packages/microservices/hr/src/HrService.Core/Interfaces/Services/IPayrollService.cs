using HrService.Core.DTOs.Payroll;

namespace HrService.Core.Interfaces.Services;

/// <summary>
/// H5 (HR-007/HR-008, P7 + P8) — the pay configuration the H6 engine runs on: grades, salary structures and
/// their components, monthly payroll periods, per-employee salary assignments, the statutory rate tables, and
/// the deduction catalogue with its per-employee applications.
/// <para><b>H5 configures, H6 computes.</b> Nothing here works out what anyone is actually paid this month —
/// that is the payroll run. What H5 owes H6 is a complete, mapped and confirmed rule set, which is what
/// <see cref="GetSummaryAsync"/>'s blockers list measures.</para>
/// </summary>
public interface IPayrollService
{
    /// <summary>Setup state plus the blockers that would stop an H6 run.</summary>
    Task<PayrollSetupSummaryDto> GetSummaryAsync();

    // ── Job grades (DS1) ──
    Task<List<JobGradeDto>> ListGradesAsync(bool includeInactive);
    Task<PayrollActionResult> CreateGradeAsync(SaveJobGradeDto dto, string userId);
    Task<PayrollActionResult> UpdateGradeAsync(string id, SaveJobGradeDto dto, string userId);

    // ── Salary structures and their components (P7 steps 7.1–7.3) ──
    Task<List<SalaryStructureDto>> ListStructuresAsync(bool includeInactive);
    Task<SalaryStructureDto?> GetStructureAsync(string id);
    Task<PayrollActionResult> CreateStructureAsync(SaveSalaryStructureDto dto, string userId);
    Task<PayrollActionResult> UpdateStructureAsync(string id, SaveSalaryStructureDto dto, string userId);
    /// <summary>Installs a standard QSL structure (basic, house, transport, the five statutory lines) for a
    /// tenant with none. Idempotent per structure name.</summary>
    Task<PayrollActionResult> SeedDefaultStructureAsync(string userId);

    Task<PayrollActionResult> AddComponentAsync(string structureId, SaveSalaryComponentDto dto, string userId);
    Task<PayrollActionResult> UpdateComponentAsync(string componentId, SaveSalaryComponentDto dto, string userId);
    /// <summary>Deactivates rather than deletes — a component that has been paid is part of pay history.</summary>
    Task<PayrollActionResult> DeactivateComponentAsync(string componentId, string userId);

    // ── GL mapping (P7 step 7.5) ──
    /// <summary>Finance's posting accounts, read live through the H5 seam. Empty when finance is unreachable.</summary>
    Task<List<GlAccountOptionDto>> ListGlAccountsAsync(CancellationToken ct = default);
    Task<PayrollActionResult> MapComponentAccountAsync(string componentId, MapGlAccountDto dto, string userId, CancellationToken ct = default);
    Task<PayrollActionResult> MapStatutoryAccountAsync(string rateId, MapGlAccountDto dto, string userId, CancellationToken ct = default);
    Task<PayrollActionResult> MapDeductionTypeAccountAsync(string typeId, MapGlAccountDto dto, string userId, CancellationToken ct = default);

    // ── Payroll periods ──
    Task<List<PayrollPeriodDto>> ListPeriodsAsync(int? year);
    /// <summary>Creates the twelve monthly periods for a year. Idempotent per period code.</summary>
    Task<PayrollActionResult> GeneratePeriodsAsync(GeneratePeriodsDto dto, string userId);

    // ── Employee salary assignments (P7 step 7.4) ──
    Task<List<EmployeeSalaryDto>> ListSalariesAsync(string? employeeId, string? status, bool currentOnly);
    /// <summary>HR proposes. A raise writes a NEW record rather than editing the old one, so pay history survives.</summary>
    Task<PayrollActionResult> ProposeSalaryAsync(ProposeSalaryDto dto, string userId);
    /// <summary>The MD approves or rejects. Approval supersedes the employee's previous approved assignment.
    /// The proposer may not be the approver (P7 step 7.4).</summary>
    Task<PayrollActionResult> DecideSalaryAsync(string id, DecideSalaryDto dto, string userId, string? userName);

    // ── PAYE bands (P7 step 7.3) ──
    Task<List<PayeTaxBandDto>> ListPayeBandsAsync(DateTime? asOf, bool includeInactive);
    /// <summary>Installs the Kenyan PAYE scale. Every seeded band is flagged
    /// <c>NeedsConfirmation</c> — the rates were right when written, but tax law moves every Finance Act.</summary>
    Task<PayrollActionResult> SeedPayeBandsAsync(string userId);
    Task<PayrollActionResult> SavePayeBandAsync(string? id, SavePayeTaxBandDto dto, string userId);

    // ── Statutory rates (NSSF, SHA, Housing Levy, HELB, personal relief) ──
    Task<List<StatutoryRateDto>> ListStatutoryRatesAsync(DateTime? asOf, bool includeInactive);
    /// <summary>Installs the non-PAYE statutory rules, likewise flagged for confirmation.</summary>
    Task<PayrollActionResult> SeedStatutoryRatesAsync(string userId);
    Task<PayrollActionResult> SaveStatutoryRateAsync(string? id, SaveStatutoryRateDto dto, string userId);
    /// <summary>Records that a human has checked seeded figures against the current Finance Act.</summary>
    Task<PayrollActionResult> ConfirmRatesAsync(ConfirmRatesDto dto, string userId, string? userName);

    // ── Deduction catalogue and applications (P8) ──
    Task<List<PayrollDeductionTypeDto>> ListDeductionTypesAsync(bool includeInactive);
    /// <summary>Installs the common catalogue — HELB (the per-employee half of the statutory HELB rule),
    /// SACCO, staff loan, salary advance, court order. Idempotent per code.</summary>
    Task<PayrollActionResult> SeedDefaultDeductionTypesAsync(string userId);
    Task<PayrollActionResult> CreateDeductionTypeAsync(SavePayrollDeductionTypeDto dto, string userId);
    Task<PayrollActionResult> UpdateDeductionTypeAsync(string id, SavePayrollDeductionTypeDto dto, string userId);

    Task<List<PayrollDeductionDto>> ListDeductionsAsync(string? employeeId, bool includeInactive);
    Task<PayrollActionResult> AddDeductionAsync(AddDeductionDto dto, string userId);
    /// <summary>Stopping never deletes (P8): the row is closed with who stopped it and when.</summary>
    Task<PayrollActionResult> StopDeductionAsync(string id, StopDeductionDto dto, string userId);
}
