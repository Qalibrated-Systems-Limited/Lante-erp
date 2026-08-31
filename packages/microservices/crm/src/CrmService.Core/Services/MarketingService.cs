using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Marketing;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C10 (P11, CRM-046..052) — marketing campaigns, budget tracking, brand-asset library and
/// live campaign attribution. Attribution chain: CAMPAIGN → Lead.CampaignId → Lead.ConvertedOpportunityId
/// → closed Deal.OpportunityId → ContractValue. ROI = attributed revenue / actual spend (per design doc);
/// the 80%-budget alert latch (Alert80SentAt) is fired by the background worker.</summary>
public class MarketingService(
    IGenericRepository<Campaign> campaigns,
    IGenericRepository<BrandAsset> brandAssets,
    IGenericRepository<Lead> leads,
    IGenericRepository<Deal> deals,
    IMapper mapper) : IMarketingService
{
    private const decimal BudgetAlertThreshold = 0.8m;

    // ── Campaigns ──
    public async Task<List<CampaignDto>> GetCampaignsAsync(string? status)
    {
        var q = campaigns.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CampaignStatus>(status, true, out var st))
            q = q.Where(c => c.Status == st);
        var list = await q.OrderByDescending(c => c.CreatedAt).ToListAsync();

        var (allLeads, closedDeals) = await LoadAttributionSourcesAsync();
        return list.Select(c => ToDto(c, allLeads, closedDeals)).ToList();
    }

    public async Task<CampaignDto?> GetCampaignAsync(string id)
    {
        var c = await campaigns.GetByIdAsync(id);
        if (c is null) return null;
        var (allLeads, closedDeals) = await LoadAttributionSourcesAsync();
        return ToDto(c, allLeads, closedDeals);
    }

    public async Task<CampaignDto> CreateCampaignAsync(CreateCampaignDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidOperationException("Campaign name is required.");
        var campaign = new Campaign
        {
            Name = dto.Name.Trim(),
            CampaignType = ParseType(dto.CampaignType),
            Description = dto.Description,
            TargetAudience = dto.TargetAudience,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Budget = dto.Budget,
            ActualSpend = 0,
            Status = CampaignStatus.Planned,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        await campaigns.CreateAsync(campaign);
        var (allLeads, closedDeals) = await LoadAttributionSourcesAsync();
        return ToDto(campaign, allLeads, closedDeals);
    }

    public async Task<CampaignDto?> UpdateCampaignAsync(string id, UpdateCampaignDto dto, string userId)
    {
        var c = await campaigns.GetByIdAsync(id);
        if (c is null) return null;
        if (!string.IsNullOrWhiteSpace(dto.Name)) c.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.CampaignType)) c.CampaignType = ParseType(dto.CampaignType);
        if (dto.Description != null) c.Description = dto.Description;
        if (dto.TargetAudience != null) c.TargetAudience = dto.TargetAudience;
        if (dto.StartDate.HasValue) c.StartDate = dto.StartDate;
        if (dto.EndDate.HasValue) c.EndDate = dto.EndDate;
        if (dto.Budget.HasValue)
        {
            c.Budget = dto.Budget.Value;
            // Re-arm the 80% alert so a raised budget can fire again once re-crossed.
            if (c.Budget > 0 && c.ActualSpend < c.Budget * BudgetAlertThreshold) c.Alert80SentAt = null;
        }
        c.UpdatedBy = userId; c.UpdatedAt = DateTime.UtcNow;
        await campaigns.UpdateAsync(c);
        var (allLeads, closedDeals) = await LoadAttributionSourcesAsync();
        return ToDto(c, allLeads, closedDeals);
    }

    public async Task<CampaignActionResult> RecordSpendAsync(string id, RecordSpendDto dto, string userId)
    {
        var c = await campaigns.GetByIdAsync(id);
        if (c is null) return new CampaignActionResult("Error", "Campaign not found.");
        if (dto.Amount <= 0) return new CampaignActionResult("Error", "Spend amount must be positive.");
        if (c.Status is CampaignStatus.Cancelled) return new CampaignActionResult("Error", "Cannot record spend on a cancelled campaign.");

        c.ActualSpend += dto.Amount;
        c.UpdatedBy = userId; c.UpdatedAt = DateTime.UtcNow;
        await campaigns.UpdateAsync(c);

        var pct = c.Budget <= 0 ? 0 : Math.Round(c.ActualSpend / c.Budget * 100, 1);
        var alert = c.Budget > 0 && c.ActualSpend >= c.Budget * BudgetAlertThreshold;
        return new CampaignActionResult("Recorded",
            alert ? $"Spend recorded — campaign is at {pct}% of budget (alert threshold reached)."
                  : $"Spend recorded — campaign is at {pct}% of budget.");
    }

    public async Task<CampaignActionResult> LaunchAsync(string id, string userId)
    {
        var c = await campaigns.GetByIdAsync(id);
        if (c is null) return new CampaignActionResult("Error", "Campaign not found.");
        if (c.Status != CampaignStatus.Planned) return new CampaignActionResult("Error", "Only a planned campaign can be launched.");
        c.Status = CampaignStatus.Active;
        if (c.StartDate is null) c.StartDate = DateTime.UtcNow;
        c.UpdatedBy = userId; c.UpdatedAt = DateTime.UtcNow;
        await campaigns.UpdateAsync(c);
        return new CampaignActionResult("Active", "Campaign launched.");
    }

    public async Task<CampaignActionResult> CompleteAsync(string id, string userId)
    {
        var c = await campaigns.GetByIdAsync(id);
        if (c is null) return new CampaignActionResult("Error", "Campaign not found.");
        if (c.Status != CampaignStatus.Active) return new CampaignActionResult("Error", "Only an active campaign can be completed.");
        c.Status = CampaignStatus.Completed;
        if (c.EndDate is null) c.EndDate = DateTime.UtcNow;
        c.UpdatedBy = userId; c.UpdatedAt = DateTime.UtcNow;
        await campaigns.UpdateAsync(c);
        return new CampaignActionResult("Completed", "Campaign completed.");
    }

    public async Task<CampaignActionResult> CancelAsync(string id, string userId)
    {
        var c = await campaigns.GetByIdAsync(id);
        if (c is null) return new CampaignActionResult("Error", "Campaign not found.");
        if (c.Status == CampaignStatus.Completed) return new CampaignActionResult("Error", "A completed campaign cannot be cancelled.");
        c.Status = CampaignStatus.Cancelled;
        c.UpdatedBy = userId; c.UpdatedAt = DateTime.UtcNow;
        await campaigns.UpdateAsync(c);
        return new CampaignActionResult("Cancelled", "Campaign cancelled.");
    }

    // ── Marketing dashboard ──
    public async Task<MarketingDashboardDto> GetDashboardAsync()
    {
        var all = await campaigns.Query().AsNoTracking().OrderByDescending(c => c.CreatedAt).ToListAsync();
        var (allLeads, closedDeals) = await LoadAttributionSourcesAsync();
        var dtos = all.Select(c => ToDto(c, allLeads, closedDeals)).ToList();

        var totalSpend = dtos.Sum(d => d.ActualSpend);
        var totalRevenue = dtos.Sum(d => d.RevenueAttributed);
        var totalLeads = dtos.Sum(d => d.LeadsGenerated);
        return new MarketingDashboardDto
        {
            CampaignCount = dtos.Count,
            ActiveCampaigns = dtos.Count(d => d.Status == nameof(CampaignStatus.Active)),
            TotalSpend = totalSpend,
            TotalRevenueAttributed = totalRevenue,
            TotalLeads = totalLeads,
            OverallRoi = totalSpend <= 0 ? 0 : Math.Round(totalRevenue / totalSpend, 2),
            CostPerLead = totalLeads == 0 ? 0 : Money.Round(totalSpend / totalLeads),
            Campaigns = dtos,
        };
    }

    // ── Brand-asset library ──
    public async Task<List<BrandAssetDto>> GetBrandAssetsAsync(string? type)
    {
        var q = brandAssets.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<BrandAssetType>(type, true, out var t))
            q = q.Where(a => a.AssetType == t);
        var list = await q.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return mapper.Map<List<BrandAssetDto>>(list);
    }

    public async Task<BrandAssetDto> SaveBrandAssetAsync(SaveBrandAssetDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.AssetName)) throw new InvalidOperationException("Asset name is required.");
        var asset = new BrandAsset
        {
            AssetName = dto.AssetName.Trim(),
            AssetType = Enum.TryParse<BrandAssetType>(dto.AssetType, true, out var at) ? at : BrandAssetType.Other,
            FileUrl = dto.FileUrl,
            Version = string.IsNullOrWhiteSpace(dto.Version) ? "1.0" : dto.Version.Trim(),
            UploadedBy = userId,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        await brandAssets.CreateAsync(asset);
        return mapper.Map<BrandAssetDto>(asset);
    }

    public async Task<bool> DeleteBrandAssetAsync(string id)
    {
        var a = await brandAssets.GetByIdAsync(id);
        if (a is null) return false;
        await brandAssets.DeleteAsync(a);
        return true;
    }

    // ── Helpers ──
    private static CampaignType ParseType(string? raw)
        => Enum.TryParse<CampaignType>(raw, true, out var ct) ? ct : CampaignType.Digital;

    private async Task<(List<Lead> Leads, List<Deal> ClosedDeals)> LoadAttributionSourcesAsync()
    {
        var allLeads = await leads.Query().AsNoTracking().Where(l => l.CampaignId != null).ToListAsync();
        var closedDeals = await deals.Query().AsNoTracking().Where(d => d.Status == DealStatus.Closed).ToListAsync();
        return (allLeads, closedDeals);
    }

    private CampaignDto ToDto(Campaign c, List<Lead> allLeads, List<Deal> closedDeals)
    {
        var dto = mapper.Map<CampaignDto>(c);

        var campaignLeads = allLeads.Where(l => l.CampaignId == c.Id).ToList();
        dto.LeadsGenerated = campaignLeads.Count;
        dto.LeadsConverted = campaignLeads.Count(l => l.IsConverted);
        dto.ConversionRate = dto.LeadsGenerated == 0 ? 0
            : Math.Round((decimal)dto.LeadsConverted / dto.LeadsGenerated * 100, 1);

        var convertedOppIds = campaignLeads
            .Where(l => l.IsConverted && !string.IsNullOrWhiteSpace(l.ConvertedOpportunityId))
            .Select(l => l.ConvertedOpportunityId!).ToHashSet();
        dto.RevenueAttributed = closedDeals
            .Where(d => convertedOppIds.Contains(d.OpportunityId)).Sum(d => d.ContractValue);

        dto.SpendPct = c.Budget <= 0 ? 0 : Math.Round(c.ActualSpend / c.Budget * 100, 1);
        dto.BudgetAlert = c.Budget > 0 && c.ActualSpend >= c.Budget * BudgetAlertThreshold;
        dto.Roi = c.ActualSpend <= 0 ? 0 : Math.Round(dto.RevenueAttributed / c.ActualSpend, 2);
        dto.CostPerLead = dto.LeadsGenerated == 0 ? 0 : Money.Round(c.ActualSpend / dto.LeadsGenerated);
        return dto;
    }
}
