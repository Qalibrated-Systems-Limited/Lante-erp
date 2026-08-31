namespace CrmService.Core.Entities;

/// <summary>P9 — REVENUE_SNAPSHOT (CRM-025). Daily per-SE revenue actuals vs target, retained for
/// trend (daily/WTD/MTD/YTD). Revenue proxy = closed-deal contract value (real source = Finance
/// INVOICE via O10 seam).</summary>
public class RevenueSnapshot : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow.Date;
    public decimal DailyRevenue { get; set; }
    public decimal WtdRevenue { get; set; }
    public decimal MtdRevenue { get; set; }
    public decimal YtdRevenue { get; set; }
    public decimal TargetMtd { get; set; }
}
