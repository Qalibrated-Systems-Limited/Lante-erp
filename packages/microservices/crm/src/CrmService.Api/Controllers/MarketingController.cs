using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Marketing;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/marketing")]
[Authorize]
public class MarketingController(IMarketingService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── Dashboard ──
    [HttpGet("dashboard")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Dashboard() => Ok(new { data = await service.GetDashboardAsync() });

    // ── Campaigns ──
    [HttpGet("campaigns")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetCampaigns([FromQuery] string? status)
        => Ok(new { data = await service.GetCampaignsAsync(status) });

    [HttpGet("campaigns/{id}")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetCampaign(string id)
    {
        var c = await service.GetCampaignAsync(id);
        return c is null ? NotFound(new { message = "Campaign not found." }) : Ok(new { data = c });
    }

    [HttpPost("campaigns")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignDto dto)
        => Ok(new { data = await service.CreateCampaignAsync(dto, UserId) });

    [HttpPut("campaigns/{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> UpdateCampaign(string id, [FromBody] UpdateCampaignDto dto)
    {
        var c = await service.UpdateCampaignAsync(id, dto, UserId);
        return c is null ? NotFound(new { message = "Campaign not found." }) : Ok(new { data = c });
    }

    [HttpPost("campaigns/{id}/spend")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> RecordSpend(string id, [FromBody] RecordSpendDto dto)
        => await ActAsync(service.RecordSpendAsync(id, dto, UserId));

    [HttpPost("campaigns/{id}/launch")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Launch(string id) => await ActAsync(service.LaunchAsync(id, UserId));

    [HttpPost("campaigns/{id}/complete")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Complete(string id) => await ActAsync(service.CompleteAsync(id, UserId));

    [HttpPost("campaigns/{id}/cancel")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Cancel(string id) => await ActAsync(service.CancelAsync(id, UserId));

    // ── Brand-asset library ──
    [HttpGet("brand-assets")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetBrandAssets([FromQuery] string? type)
        => Ok(new { data = await service.GetBrandAssetsAsync(type) });

    [HttpPost("brand-assets")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> SaveBrandAsset([FromBody] SaveBrandAssetDto dto)
        => Ok(new { data = await service.SaveBrandAssetAsync(dto, UserId) });

    [HttpDelete("brand-assets/{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> DeleteBrandAsset(string id)
        => await service.DeleteBrandAssetAsync(id)
            ? Ok(new { message = "Brand asset deleted." })
            : NotFound(new { message = "Brand asset not found." });

    private async Task<IActionResult> ActAsync(Task<CampaignActionResult> action)
    {
        var r = await action;
        return r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
    }
}
