namespace CrmService.Core.DTOs.Marketing;

public class CreateCampaignDto
{
    public string Name { get; set; } = string.Empty;
    public string CampaignType { get; set; } = "Digital";
    public string? Description { get; set; }
    public string? TargetAudience { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Budget { get; set; }
}
public class UpdateCampaignDto
{
    public string? Name { get; set; }
    public string? CampaignType { get; set; }
    public string? Description { get; set; }
    public string? TargetAudience { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
}
public class RecordSpendDto { public decimal Amount { get; set; } }

public class CampaignDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CampaignType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TargetAudience { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal ActualSpend { get; set; }
    public decimal SpendPct { get; set; }
    public bool BudgetAlert { get; set; }
    // Attribution (computed).
    public int LeadsGenerated { get; set; }
    public int LeadsConverted { get; set; }
    public decimal ConversionRate { get; set; }
    public decimal RevenueAttributed { get; set; }
    public decimal Roi { get; set; }
    public decimal CostPerLead { get; set; }
}

public class SaveBrandAssetDto
{
    public string AssetName { get; set; } = string.Empty;
    public string AssetType { get; set; } = "Other";
    public string? FileUrl { get; set; }
    public string? Version { get; set; }
}
public class BrandAssetDto
{
    public string Id { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string Version { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MarketingDashboardDto
{
    public int CampaignCount { get; set; }
    public int ActiveCampaigns { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal TotalRevenueAttributed { get; set; }
    public int TotalLeads { get; set; }
    public decimal OverallRoi { get; set; }
    public decimal CostPerLead { get; set; }
    public List<CampaignDto> Campaigns { get; set; } = new();
}

public record CampaignActionResult(string Status, string Message);
