namespace ReportingService.Core.DTOs;

/// <summary>
/// Report #12 — Payroll Summary &amp; Cost Report (#225). What payroll cost, per run and in total.
/// </summary>
public class PayrollSummaryReportDto
{
    public PayrollSummaryTotalsDto Totals { get; set; } = new();
    public List<PayrollRunRowDto> Runs { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class PayrollSummaryTotalsDto
{
    public int RunCount { get; set; }

    /// <summary>Headcount from the most recent run, not the sum across runs. Summing would count the same
    /// employee once per period and produce a figure nobody can use.</summary>
    public int LatestHeadcount { get; set; }

    public decimal TotalGross { get; set; }
    public decimal TotalPaye { get; set; }
    public decimal TotalStatutory { get; set; }
    public decimal TotalOtherDeductions { get; set; }
    public decimal TotalNet { get; set; }

    /// <summary>The employer's own contributions — NSSF, housing levy. Not deducted from staff, so it is
    /// not in TotalDeductions and does not reconcile against net pay.</summary>
    public decimal TotalEmployerCost { get; set; }

    /// <summary>Gross plus employer contributions: what payroll actually costs the business. The figure
    /// people mean by "what does payroll cost" and the one gross alone understates.</summary>
    public decimal TotalCostOfEmployment { get; set; }

    /// <summary>Runs approved but whose journal never reached the ledger. Surfaced because such a run is
    /// money owed to staff with no entry in the accounts — see the retry path in FinanceService.</summary>
    public int RunsWithUnpostedJournal { get; set; }
}
