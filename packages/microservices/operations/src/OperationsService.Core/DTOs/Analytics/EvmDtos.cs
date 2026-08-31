namespace OperationsService.Core.DTOs.Analytics;

// PR4a — earned value. Ratios are nullable rather than zero-filled: SPI with no planned value and CPI
// with no spend are undefined, not "0", and a zero would read as catastrophic performance on a
// project that has simply not started.

public class EvmMetricsDto
{
    public string  ProjectId   { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public string? Status      { get; set; }
    public DateTime AsOf       { get; set; }

    /// <summary>Budget at completion — the approved baseline, or the planned budget if never baselined.</summary>
    public decimal Bac { get; set; }
    /// <summary>True when BAC fell back to PlannedBudget because no baseline has been approved.</summary>
    public bool    IsBaselined { get; set; }

    /// <summary>Planned value: what the baseline said should be earned by now.</summary>
    public decimal Pv { get; set; }
    /// <summary>Earned value: milestone worth × its progress.</summary>
    public decimal Ev { get; set; }
    /// <summary>Actual cost booked to the project up to the as-of date.</summary>
    public decimal Ac { get; set; }

    public decimal ScheduleVariance { get; set; }   // EV - PV
    public decimal CostVariance     { get; set; }   // EV - AC

    public decimal? Spi { get; set; }               // EV / PV
    public decimal? Cpi { get; set; }               // EV / AC

    /// <summary>Estimate at completion, BAC / CPI. Null when nothing has been spent yet.</summary>
    public decimal? Eac { get; set; }
    /// <summary>Estimate to complete, EAC - AC.</summary>
    public decimal? Etc { get; set; }
    /// <summary>Variance at completion, BAC - EAC. Negative means the project is forecast to overrun.</summary>
    public decimal? Vac { get; set; }

    public int PercentComplete { get; set; }
    public int PercentSpent    { get; set; }

    /// <summary>
    /// Plain-language read of SPI/CPI, so the tile does not depend on the reader remembering which
    /// way round the indices go.
    /// </summary>
    public string ScheduleVerdict { get; set; } = string.Empty;
    public string CostVerdict     { get; set; } = string.Empty;

    /// <summary>Set when the numbers rest on something incomplete — no baseline, unpriced milestones.</summary>
    public List<string> Caveats { get; set; } = new();
}

public class SCurvePointDto
{
    public DateTime Date { get; set; }
    public decimal  Pv   { get; set; }
    public decimal  Ev   { get; set; }
    public decimal  Ac   { get; set; }
    /// <summary>False for points after today — the PV curve continues, EV and AC stop.</summary>
    public bool     IsActual { get; set; }
}

public class ProjectEvmDto
{
    public EvmMetricsDto     Metrics { get; set; } = new();
    public List<SCurvePointDto> SCurve { get; set; } = new();
}

public class PortfolioEvmDto
{
    public DateTime AsOf { get; set; }
    public int      ProjectCount { get; set; }

    public decimal  Bac { get; set; }
    public decimal  Pv  { get; set; }
    public decimal  Ev  { get; set; }
    public decimal  Ac  { get; set; }

    public decimal? Spi { get; set; }
    public decimal? Cpi { get; set; }
    public decimal? Eac { get; set; }
    public decimal? Vac { get; set; }

    /// <summary>Projects forecast to finish over budget (VAC &lt; 0), worst first.</summary>
    public int OverrunCount { get; set; }
    /// <summary>Projects behind schedule (SPI &lt; 1).</summary>
    public int BehindCount { get; set; }

    public List<EvmMetricsDto> Projects { get; set; } = new();
}
