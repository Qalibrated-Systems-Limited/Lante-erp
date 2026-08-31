namespace CrmService.Core.DTOs.Dashboards;

public class SaveSalesTargetDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string PeriodType { get; set; } = "Annual";
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal RevenueTarget { get; set; }
}
public class SalesTargetDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal RevenueTarget { get; set; }
    public string Currency { get; set; } = "KES";
}

// ── MD pipeline dashboard (P10) ──
public record PipelineStageBucket(string StageId, string StageName, int Count, decimal Value, decimal Weighted);
public record TopClient(string CustomerName, decimal OpenValue, int OpportunityCount);
public record PipelineRisk(int StaleOpportunities, int OverdueTasks, int ExpiringBidBonds, int DormantClients);
public class MdDashboardDto
{
    public int OpenCount { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int WonLast12mo { get; set; }
    public int LostLast12mo { get; set; }
    public decimal WinRate { get; set; }
    public List<PipelineStageBucket> ByStage { get; set; } = new();
    public List<TopClient> TopClients { get; set; } = new();
    public PipelineRisk Risk { get; set; } = new(0, 0, 0, 0);
}

// ── SE performance (P9) ──
public class SePerformanceDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int LeadsGenerated { get; set; }
    public int LeadsConverted { get; set; }
    public int OppsWon { get; set; }
    public int OppsLost { get; set; }
    public decimal WinRate { get; set; }
    public decimal Revenue { get; set; }
    public decimal Target { get; set; }
    public decimal AttainmentPct { get; set; }
    public string Rag { get; set; } = "Grey";   // Green / Amber / Red / Grey (no target)
}
