namespace HrService.Core.DTOs.Commission;

/// <summary>The result shape every H11 write returns, matching H5–H10.</summary>
public record CommissionActionResult(string Status, string Message, string? Id = null)
{
    public List<string> Warnings { get; init; } = [];
}

// ── Bands (P29 step 29.1) ──
public class CommissionBandDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal MinPercent { get; set; }
    public decimal? MaxPercent { get; set; }
    public decimal CommissionRatePercent { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    /// <summary>"Above 100% — 7% of revenue", in words.</summary>
    public string Band { get; set; } = string.Empty;
}

public class SaveCommissionBandDto
{
    public string Label { get; set; } = string.Empty;
    public decimal MinPercent { get; set; }
    public decimal? MaxPercent { get; set; }
    public decimal CommissionRatePercent { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public int DisplayOrder { get; set; }
    public bool? IsActive { get; set; }
}

// ── Plans (P29 step 29.2) ──
public class CommissionPlanDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public int Year { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Basis { get; set; }
    public string? Notes { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Read live from CRM — HR holds no copy. Null when CRM could not be read.</summary>
    public decimal? AnnualTargetFromCrm { get; set; }
    public decimal? RevenueAchievedFromCrm { get; set; }
    public decimal? AttainmentPercent { get; set; }
    public string? TargetSourceNote { get; set; }
}

public class SaveCommissionPlanDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? Basis { get; set; }
    public string? Notes { get; set; }
}

public class DecideCommissionPlanDto
{
    /// <summary>Approve | Reject.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

// ── Statements (P30) ──
public class CommissionStatementDto
{
    public string Id { get; set; } = string.Empty;
    public string StatementNumber { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public decimal AnnualTarget { get; set; }
    public decimal QuarterTarget { get; set; }
    public decimal RevenueAchieved { get; set; }
    /// <summary>Against the full annual target — this is what selects the band.</summary>
    public decimal AttainmentPercent { get; set; }
    /// <summary>Against the target pro-rated to the quarters elapsed — this is what the red flags test.</summary>
    public decimal ProRatedAttainmentPercent { get; set; }
    /// <summary>Annual target x quarters elapsed / 4 — the denominator behind the pro-rated figure.</summary>
    public decimal YtdTarget { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public string? SourceNotes { get; set; }

    public string? BandLabel { get; set; }
    public decimal CommissionRatePercent { get; set; }
    /// <summary>What the year-to-date revenue has earned in total at this quarter's rate.</summary>
    public decimal CommissionEarnedToDate { get; set; }
    /// <summary>What the year's earlier quarters already settled.</summary>
    public decimal PriorCommissionThisYear { get; set; }
    /// <summary>This quarter's settlement — earned-to-date less what earlier quarters settled.</summary>
    public decimal CommissionAmount { get; set; }
    /// <summary>Set only when earlier quarters settled more than the year has now earned.</summary>
    public decimal? UnrecoveredOverpayment { get; set; }

    public string Status { get; set; } = string.Empty;
    public DateTime ComputedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? PayrollPeriodCode { get; set; }
    public string? PayrollRunId { get; set; }
    public decimal? PaidAmount { get; set; }
    public string? RedFlag { get; set; }
    public string? CancellationReason { get; set; }
    public bool HasOpenDispute { get; set; }
    public string NextStep { get; set; } = string.Empty;
}

public class ComputeStatementsDto
{
    public int? Year { get; set; }
    /// <summary>1–4. Defaults to the quarter that has just ended.</summary>
    public int? Quarter { get; set; }
}

public class DecideStatementDto
{
    /// <summary>Approve | Cancel.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
    /// <summary>The payroll period the commission is paid in. Defaults to the current open one.</summary>
    public string? PayrollPeriodId { get; set; }
}

// ── Disputes (P31) ──
public class CommissionDisputeDto
{
    public string Id { get; set; } = string.Empty;
    public string CommissionStatementId { get; set; } = string.Empty;
    public string? StatementNumber { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? DisputedAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RaisedAt { get; set; }
    public string? Findings { get; set; }
    public string? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public decimal? AdjustedAmount { get; set; }
}

public class RaiseDisputeDto
{
    public string Description { get; set; } = string.Empty;
    public decimal? DisputedAmount { get; set; }
}

public class ResolveDisputeDto
{
    /// <summary>Upheld | Rejected.</summary>
    public string Outcome { get; set; } = string.Empty;
    public string? Findings { get; set; }
    public string Resolution { get; set; } = string.Empty;
    /// <summary>Required when upholding — the corrected commission.</summary>
    public decimal? AdjustedAmount { get; set; }
}

// ── Summary ──
public class CommissionSummaryDto
{
    public int Year { get; set; }
    public int BandsInForce { get; set; }
    public int PlansApproved { get; set; }
    public int PlansAwaitingMd { get; set; }
    public int StatementsComputed { get; set; }
    public int StatementsApproved { get; set; }
    public int StatementsPaid { get; set; }
    public decimal CommissionEarnedYtd { get; set; }
    public decimal CommissionPaidYtd { get; set; }
    public int OpenDisputes { get; set; }
    public int RedFlagsBelow50 { get; set; }
    public int RedFlags50To69 { get; set; }
    /// <summary>Set when CRM could not be reached — the figures above are then incomplete, not zero.</summary>
    public string? SourceNote { get; set; }
}

// ── Quarterly sweep (COM-004) ──
public class CommissionSweepResultDto
{
    /// <summary>The quarter the sweep looked at — the one that has most recently ended.</summary>
    public int Year { get; set; }
    public int Quarter { get; set; }
    public int StatementsIssued { get; set; }
    public int RedFlagsRaised { get; set; }
    public List<string> Notes { get; set; } = [];
}
