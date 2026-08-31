namespace HrService.Core.DTOs.Payroll;

/// <summary>The one result shape every H5 write returns, as H2–H4 do. <c>Status == "Error"</c> is the only
/// thing the controller turns into a 400; everything else is an outcome worth reading.</summary>
public record PayrollActionResult(string Status, string Message, string? Id = null)
{
    /// <summary>Non-blocking observations — an out-of-band salary, a rate that still needs confirming. These
    /// are deliberately NOT errors: out-of-band pay is a real decision HR makes with its eyes open.</summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// A stable machine-readable reason, set only where a caller has to branch on WHICH refusal this is.
    /// Most refusals need no code — the message is for a person. This exists so a client does not have to
    /// pattern-match on prose that will be reworded: the UI keys off <c>UnconfirmedRates</c> to offer an
    /// acknowledgement, and a message tweak must not silently disable that path (#244).
    /// </summary>
    public string? Code { get; init; }
}

// ── Job grades (DS1 JOB_GRADE) ──
public class JobGradeDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    /// <summary>How many salary structures price against this grade.</summary>
    public int StructureCount { get; set; }
}

public class SaveJobGradeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public int DisplayOrder { get; set; }
    public bool? IsActive { get; set; }
}

// ── Salary structures + components (P7 steps 7.1–7.3, 7.5) ──
public class SalaryStructureDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? JobGradeId { get; set; }
    public string? JobGradeCode { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public bool IsActive { get; set; }
    public List<SalaryComponentDto> Components { get; set; } = [];

    /// <summary>How many employees are currently paid on this structure.</summary>
    public int AssignedEmployees { get; set; }
    /// <summary>Active components with no GL account — H6 cannot post a journal until this is zero.</summary>
    public int ComponentsWithoutGlAccount { get; set; }
}

public class SaveSalaryStructureDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? JobGradeId { get; set; }
    public string? CurrencyCode { get; set; }
    public bool? IsActive { get; set; }
}

public class SalaryComponentDto
{
    public string Id { get; set; } = string.Empty;
    public string SalaryStructureId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public string CalculationType { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public decimal? Percentage { get; set; }
    public string Statutory { get; set; } = string.Empty;
    public bool IsTaxable { get; set; }
    public string? GlAccountId { get; set; }
    public string? GlAccountCode { get; set; }
    public string? GlAccountName { get; set; }
    public int ComponentOrder { get; set; }
    public bool IsActive { get; set; }
    /// <summary>"30% of basic", "KES 15,000", "PAYE (statutory)" — what the line means, in words.</summary>
    public string Basis { get; set; } = string.Empty;
}

public class SaveSalaryComponentDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Earning | Deduction.</summary>
    public string ComponentType { get; set; } = "Earning";
    /// <summary>FixedAmount | PercentOfBasic | PercentOfGross | Statutory | VariableInput.</summary>
    public string CalculationType { get; set; } = "FixedAmount";
    public decimal? Amount { get; set; }
    public decimal? Percentage { get; set; }
    /// <summary>None | Paye | Nssf | Sha | HousingLevy | Helb — required when CalculationType is Statutory.</summary>
    public string? Statutory { get; set; }
    public bool IsTaxable { get; set; } = true;
    public int ComponentOrder { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>Points a component, statutory rate or deduction type at one of finance's posting accounts
/// (P7 step 7.5). The code and name are stored alongside the id so a payslip still reads correctly when
/// finance is unreachable.</summary>
public class MapGlAccountDto
{
    public string GlAccountId { get; set; } = string.Empty;
}

public class GlAccountOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Classification { get; set; }
}

// ── Payroll periods ──
public class PayrollPeriodDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? CutOffDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LockedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsCurrent { get; set; }
}

public class GeneratePeriodsDto
{
    public int Year { get; set; }
    /// <summary>HR-008 — day of the month after which changes belong to the next period (QSL uses the 20th).</summary>
    public int? CutOffDay { get; set; }
    /// <summary>Day of the month staff are paid. Clamped to the length of each month.</summary>
    public int? PaymentDay { get; set; }
}

// ── Employee salary assignments (P7 step 7.4) ──
public class EmployeeSalaryDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string SalaryStructureId { get; set; } = string.Empty;
    public string? SalaryStructureName { get; set; }
    public decimal BasicSalary { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public string EffectiveFromPeriodId { get; set; } = string.Empty;
    public string? EffectiveFromPeriodCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProposedBy { get; set; }
    public DateTime? ProposedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? SupersededById { get; set; }
    public string? Notes { get; set; }
    /// <summary>Set when the basic sits outside the grade's advisory band.</summary>
    public string? BandWarning { get; set; }
}

public class ProposeSalaryDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string SalaryStructureId { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    /// <summary>Defaults to the current open period when omitted.</summary>
    public string? EffectiveFromPeriodId { get; set; }
    public string? Notes { get; set; }
}

public class DecideSalaryDto
{
    /// <summary>Approve | Reject.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

// ── PAYE bands + statutory rates (P7 step 7.3) ──
public class PayeTaxBandDto
{
    public string Id { get; set; } = string.Empty;
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal Rate { get; set; }
    public int BandOrder { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool NeedsConfirmation { get; set; }
    public string? Source { get; set; }
    public bool IsActive { get; set; }
    /// <summary>"On the first 24,000 — 10%".</summary>
    public string Band { get; set; } = string.Empty;
}

public class SavePayeTaxBandDto
{
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal Rate { get; set; }
    public int BandOrder { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public string? Source { get; set; }
    public bool? IsActive { get; set; }
}

public class StatutoryRateDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public string RateType { get; set; } = string.Empty;
    public decimal? Rate { get; set; }
    public decimal? FixedAmount { get; set; }
    public decimal? TierLowerBound { get; set; }
    public decimal? TierUpperBound { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public decimal? EmployerRate { get; set; }
    /// <summary>Whether this contribution comes off pay before PAYE is computed (H6).</summary>
    public bool ReducesTaxableIncome { get; set; }
    public string? GlAccountId { get; set; }
    public string? GlAccountCode { get; set; }
    public string? GlAccountName { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool NeedsConfirmation { get; set; }
    public string? Source { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    /// <summary>"2.75% of gross, minimum KES 300" — the rule in words.</summary>
    public string Basis { get; set; } = string.Empty;
}

public class SaveStatutoryRateDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Paye | Nssf | Sha | HousingLevy | Helb.</summary>
    public string Component { get; set; } = string.Empty;
    /// <summary>PercentOfGross | TieredPercent | FixedAmount | PerEmployeeAmount.</summary>
    public string RateType { get; set; } = string.Empty;
    public decimal? Rate { get; set; }
    public decimal? FixedAmount { get; set; }
    public decimal? TierLowerBound { get; set; }
    public decimal? TierUpperBound { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public decimal? EmployerRate { get; set; }
    /// <summary>Whether this contribution is allowable against taxable pay. It moves with legislation, so it
    /// is held per dated rate rather than decided in the engine.</summary>
    public bool ReducesTaxableIncome { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>Clears the "seeded but never checked" flag on a rate or band, recording who checked it.</summary>
public class ConfirmRatesDto
{
    /// <summary>Empty confirms every unconfirmed rate and band in force.</summary>
    public List<string> Ids { get; set; } = [];
    public string? Source { get; set; }
}

// ── Deductions (P8) ──
public class PayrollDeductionTypeDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool ReducesTaxableIncome { get; set; }
    public bool IsRecurring { get; set; }
    public string? GlAccountId { get; set; }
    public string? GlAccountCode { get; set; }
    public string? GlAccountName { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>How many employees currently carry this deduction.</summary>
    public int ActiveDeductions { get; set; }
}

public class SavePayrollDeductionTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Statutory | Loan | Voluntary | CourtOrder | Advance.</summary>
    public string Category { get; set; } = "Voluntary";
    /// <summary>Whether the deduction comes off pay before tax is computed.</summary>
    public bool ReducesTaxableIncome { get; set; }
    public bool IsRecurring { get; set; } = true;
    public bool? IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class PayrollDeductionDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string DeductionTypeId { get; set; } = string.Empty;
    public string? DeductionTypeCode { get; set; }
    public string? DeductionTypeName { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string StartPeriodId { get; set; } = string.Empty;
    public string? StartPeriodCode { get; set; }
    public string? EndPeriodId { get; set; }
    public string? EndPeriodCode { get; set; }
    public bool IsActive { get; set; }
    public string? AddedBy { get; set; }
    public DateTime AddedAt { get; set; }
    public string? RemovedBy { get; set; }
    public DateTime? RemovedAt { get; set; }
    public string? Notes { get; set; }
    /// <summary>"KES 5,000 monthly from 2026-08" / "one-off in 2026-08".</summary>
    public string Schedule { get; set; } = string.Empty;
}

public class AddDeductionDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string DeductionTypeId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Defaults to the current open period.</summary>
    public string? StartPeriodId { get; set; }
    /// <summary>Ignored for a one-off type, which always ends in the period it starts.</summary>
    public string? EndPeriodId { get; set; }
    public string? Notes { get; set; }
}

public class StopDeductionDto
{
    public string? Reason { get; set; }
    /// <summary>Last period to take it in. Defaults to the current open period.</summary>
    public string? EndPeriodId { get; set; }
}

// ── Summary ──
public class PayrollSetupSummaryDto
{
    public int JobGrades { get; set; }
    public int SalaryStructures { get; set; }
    public int ActiveComponents { get; set; }
    public int ComponentsWithoutGlAccount { get; set; }

    public int EmployeesOnApprovedSalary { get; set; }
    public int EmployeesWithoutSalary { get; set; }
    public int SalariesAwaitingApproval { get; set; }
    public decimal ApprovedMonthlyBasicTotal { get; set; }

    public int PayeBandsInForce { get; set; }
    public int StatutoryRatesInForce { get; set; }
    public int RatesNeedingConfirmation { get; set; }
    public int RatesWithoutGlAccount { get; set; }

    public int DeductionTypes { get; set; }
    public int ActiveDeductions { get; set; }
    public decimal ActiveDeductionMonthlyTotal { get; set; }

    public string? CurrentPeriodCode { get; set; }
    public int OpenPeriods { get; set; }

    /// <summary>Everything standing between this tenant and an H6 payroll run. Empty means ready.</summary>
    public List<string> Blockers { get; set; } = [];
}
