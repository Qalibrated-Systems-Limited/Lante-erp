namespace ReportingService.Core.DTOs;

/// <summary>
/// Report #14 — Revenue vs KPI Target by Department (#225).
///
/// <para>Built on finance's revenue targets, whose <c>Scope</c> is the department dimension
/// ("Company-wide" or a department / business line). Deliberately NOT built on reporting's own
/// <c>KpiScorecard</c> targets, which the report's title might suggest: a scorecard carries a name, a
/// data source and thresholds, and <b>no department</b>, so that reading is not expressible without a
/// schema change.</para>
///
/// <para>Distinct from report #2 (budget-variance), which returns the same targets <i>flat</i> for a
/// fiscal year. This one separates company-wide from departmental, rolls the departments up, and says
/// which are behind.</para>
/// </summary>
public class RevenueVsTargetReportDto
{
    public string FiscalYearId { get; set; } = string.Empty;

    /// <summary>Departmental rows only — the company-wide target is held separately so it is never
    /// added to the departments that make it up.</summary>
    public List<DepartmentRevenueRowDto> Departments { get; set; } = new();

    /// <summary>The company-wide target, if one is set. Null when only departmental targets exist.</summary>
    public DepartmentRevenueRowDto? CompanyWide { get; set; }

    public RevenueVsTargetTotalsDto Totals { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class DepartmentRevenueRowDto
{
    /// <summary>The department or business line. Finance calls this Scope.</summary>
    public string Department { get; set; } = string.Empty;

    public decimal Target { get; set; }
    public decimal Actual { get; set; }

    /// <summary>Actual less target: positive is ahead. Finance computes this per row; recomputed here
    /// because a department can hold several target rows and the roll-up has to agree with its own
    /// figures rather than with a sum of per-row variances.</summary>
    public decimal Variance { get; set; }

    /// <summary>Actual as a percentage of target. Null when the target is zero — a division by zero
    /// dressed up as 0% reads as "achieved nothing" when the truth is "nothing was asked".</summary>
    public decimal? AchievedPct { get; set; }

    /// <summary>Behind / OnTrack / Achieved, from finance's own grading where a row carries it.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>How many target rows this department had. More than one means the actual could not be
    /// safely aggregated — see the report's warnings.</summary>
    public int TargetRowCount { get; set; }
}

public class RevenueVsTargetTotalsDto
{
    public int DepartmentCount { get; set; }

    /// <summary>Sum of the DEPARTMENTAL targets, excluding company-wide. Adding the company-wide
    /// figure to the departments it already covers would double the target.</summary>
    public decimal TotalDepartmentTarget { get; set; }

    public decimal TotalDepartmentActual { get; set; }
    public decimal TotalVariance { get; set; }

    /// <summary>Departments whose actual is below their target.</summary>
    public int DepartmentsBehind { get; set; }
}
