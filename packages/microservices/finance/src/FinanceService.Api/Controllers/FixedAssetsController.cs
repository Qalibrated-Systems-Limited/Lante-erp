using FinanceService.Core.DTOs;
using FinanceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceService.Api.Controllers;

// ── Fixed Asset Register & Depreciation (ASSET-001..008) ──
public class FixedAssetsController : BaseFinanceController
{
    private readonly IFixedAssetService _assets;
    private readonly IDepreciationService _depreciation;
    private readonly IAssetDisposalService _disposals;

    public FixedAssetsController(IFixedAssetService assets, IDepreciationService depreciation, IAssetDisposalService disposals)
    {
        _assets = assets; _depreciation = depreciation; _disposals = disposals;
    }

    [HttpGet("fixed-assets/categories")]
    public async Task<IActionResult> Categories() => Ok(ApiResponse<List<AssetCategoryReadDto>>.Ok(await _assets.ListCategoriesAsync()));

    [HttpGet("fixed-assets")]
    public async Task<IActionResult> List() => Ok(ApiResponse<List<FixedAssetReadDto>>.Ok(await _assets.ListAsync()));

    [HttpGet("fixed-assets/{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var asset = await _assets.GetAsync(id);
        return asset == null ? NotFound(ApiResponse<FixedAssetReadDto>.Fail("Asset not found.", 404)) : Ok(ApiResponse<FixedAssetReadDto>.Ok(asset));
    }

    [HttpPost("fixed-assets")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] CreateFixedAssetDto dto)
        => Ok(ApiResponse<FixedAssetReadDto>.Ok(await _assets.CreateAsync(dto, CurrentUserId), "Asset registered."));

    [HttpPost("fixed-assets/depreciation/run")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> RunDepreciation([FromQuery] string period)
    {
        var result = await _depreciation.RunDepreciationAsync(period, CurrentUserId);
        var message = result.AlreadyRun
            ? $"Depreciation for {period} was already run — no changes made."
            : $"Depreciation posted: {result.AssetsProcessed} asset(s), Kshs {result.TotalCharge:N2}.";
        return Ok(ApiResponse<RunDepreciationResultDto>.Ok(result, message));
    }

    [HttpGet("fixed-assets/depreciation/schedule")]
    public async Task<IActionResult> Schedule([FromQuery] string? period = null)
        => Ok(ApiResponse<List<DepreciationEntryReadDto>>.Ok(await _depreciation.GetScheduleAsync(period)));

    [HttpGet("fixed-assets/disposals")]
    public async Task<IActionResult> ListDisposals() => Ok(ApiResponse<List<AssetDisposalReadDto>>.Ok(await _disposals.ListAsync()));

    [HttpPost("fixed-assets/{id}/dispose")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Dispose(string id, [FromBody] CreateAssetDisposalDto dto)
    {
        dto.AssetId = id;
        return Ok(ApiResponse<AssetDisposalReadDto>.Ok(await _disposals.CreateAsync(dto, CurrentUserId), "Disposal submitted for MD approval."));
    }

    [HttpPost("fixed-assets/disposals/{id}/approve-md")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> ApproveMd(string id)
        => Ok(ApiResponse<AssetDisposalReadDto>.Ok(await _disposals.ApproveMdAsync(id, CurrentUserId, ApprovalCtx), "Disposal approved by MD."));

    [HttpPost("fixed-assets/disposals/{id}/approve-board")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> ApproveBoard(string id)
        => Ok(ApiResponse<AssetDisposalReadDto>.Ok(await _disposals.ApproveBoardAsync(id, CurrentUserId), "Board approval recorded — asset disposed."));
}
