using CrmService.Core.DTOs.Marketing;

namespace CrmService.Core.Interfaces.Services;

/// <summary>C10 (P11) — campaigns (+ folded marketing budget), brand-asset library and the
/// live-computed marketing dashboard (lead attribution &amp; ROI).</summary>
public interface IMarketingService
{
    // Campaigns
    Task<List<CampaignDto>> GetCampaignsAsync(string? status);
    Task<CampaignDto?> GetCampaignAsync(string id);
    Task<CampaignDto> CreateCampaignAsync(CreateCampaignDto dto, string userId);
    Task<CampaignDto?> UpdateCampaignAsync(string id, UpdateCampaignDto dto, string userId);
    Task<CampaignActionResult> RecordSpendAsync(string id, RecordSpendDto dto, string userId);
    Task<CampaignActionResult> LaunchAsync(string id, string userId);
    Task<CampaignActionResult> CompleteAsync(string id, string userId);
    Task<CampaignActionResult> CancelAsync(string id, string userId);

    // Marketing dashboard (KPIs + campaign attribution)
    Task<MarketingDashboardDto> GetDashboardAsync();

    // Brand-asset library
    Task<List<BrandAssetDto>> GetBrandAssetsAsync(string? type);
    Task<BrandAssetDto> SaveBrandAssetAsync(SaveBrandAssetDto dto, string userId);
    Task<bool> DeleteBrandAssetAsync(string id);
}
