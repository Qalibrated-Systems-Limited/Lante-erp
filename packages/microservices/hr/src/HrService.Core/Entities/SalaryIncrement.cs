using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H8 (P13, DS4 SALARY_INCREMENT, HR-013) — a proposed pay rise: HR proposes, the MD approves, and approval
/// writes the new <see cref="EmployeeSalary"/> for the effective month.
/// <para><b>Two hard gates stand before the MD ever sees it</b> (P13 step 13.2): incomplete mandatory training
/// (HR-029) and any expired professional certification (HR-035). The eligibility verdict is stored on the
/// proposal so the MD can see what was checked and when — an approval screen that only says "eligible" is
/// asking someone to take the system's word for it.</para>
/// <para>The increment is the DECISION; the salary assignment it creates is the RECORD OF PAY. Keeping them
/// apart means a rejected or withdrawn increment leaves no trace on what anybody is actually paid.</para>
/// </summary>
public class SalaryIncrement : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }

    /// <summary>The salary in force when the proposal was raised, kept so the increase can still be explained
    /// after later changes.</summary>
    public decimal CurrentSalary { get; set; }
    public decimal ProposedSalary { get; set; }
    public string CurrencyCode { get; set; } = "KES";

    /// <summary>The assignment being replaced, and the structure the new one inherits.</summary>
    public string? CurrentEmployeeSalaryId { get; set; }
    public string SalaryStructureId { get; set; } = string.Empty;
    public string? SalaryStructureName { get; set; }

    public string EffectivePeriodId { get; set; } = string.Empty;
    public string? EffectivePeriodCode { get; set; }

    public SalaryIncrementStatus Status { get; set; } = SalaryIncrementStatus.PendingMd;
    public string? Justification { get; set; }

    public string? ProposedBy { get; set; }
    public DateTime ProposedAt { get; set; } = DateTime.UtcNow;
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }

    /// <summary>What the eligibility gates said when the proposal was raised, in words. Stored rather than
    /// recomputed on read so the record shows what the proposer actually saw.</summary>
    public string? EligibilityNotes { get; set; }

    /// <summary>The salary assignment approval created (P13 step 13.3b). Null until then.</summary>
    public string? ResultingEmployeeSalaryId { get; set; }

    public Employee? Employee { get; set; }
}
