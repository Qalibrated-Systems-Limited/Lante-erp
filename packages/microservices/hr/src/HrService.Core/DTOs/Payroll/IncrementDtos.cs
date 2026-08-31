namespace HrService.Core.DTOs.Payroll;

// ── Salary increments (P13, HR-013) ──
public class SalaryIncrementDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public decimal CurrentSalary { get; set; }
    public decimal ProposedSalary { get; set; }
    /// <summary>The rise itself, and as a percentage — what the MD is actually deciding on.</summary>
    public decimal IncreaseAmount { get; set; }
    public decimal IncreasePercent { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public string? SalaryStructureName { get; set; }
    public string EffectivePeriodId { get; set; } = string.Empty;
    public string? EffectivePeriodCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public string? ProposedBy { get; set; }
    public DateTime ProposedAt { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public string? EligibilityNotes { get; set; }
    public string? ResultingEmployeeSalaryId { get; set; }
}

public class ProposeIncrementDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public decimal ProposedSalary { get; set; }
    /// <summary>Defaults to the next open period after the employee's current assignment.</summary>
    public string? EffectivePeriodId { get; set; }
    public string? Justification { get; set; }
}

public class DecideIncrementDto
{
    /// <summary>Approve | Reject | Withdraw.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

/// <summary>Everything HR needs before proposing: the current salary, the gates, and what is left to fix.</summary>
public class IncrementPreviewDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public bool HasCurrentSalary { get; set; }
    public decimal CurrentSalary { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public string? SalaryStructureName { get; set; }
    public string? CurrentPeriodCode { get; set; }
    /// <summary>The earliest period a rise could take effect from.</summary>
    public string? SuggestedPeriodId { get; set; }
    public string? SuggestedPeriodCode { get; set; }

    public bool Eligible { get; set; }
    /// <summary>HR-029 / HR-035 — hard blocks. A proposal cannot be raised while any of these stand.</summary>
    public List<string> Blockers { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public decimal HoursYtd { get; set; }
    public int TargetHours { get; set; }
    /// <summary>An increment already awaiting the MD, which must be settled first.</summary>
    public string? PendingIncrementId { get; set; }
}
