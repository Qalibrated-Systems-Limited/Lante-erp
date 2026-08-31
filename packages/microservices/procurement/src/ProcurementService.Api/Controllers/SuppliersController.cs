using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementService.Core.DTOs.Suppliers;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P1 (PROC-001, PROC-007) — Approved Supplier Register: supplier master, compliance documents,
/// conflict-of-interest check, approval and blacklisting.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/suppliers")]
[Authorize]
public class SuppliersController(ISupplierService service, ISupplierSeedService seed) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    // ── DEC-B: import the pre-ASR supplier masters from Finance / Stores ──
    /// <summary>Dry run — reports exactly what an import would do without changing anything.</summary>
    [HttpGet("seed/preview")]
    [Authorize(Policy = "Permission:procurement.read.all")]
    public async Task<IActionResult> SeedPreview([FromQuery] SeedSuppliersDto filter)
        => Ok(new { data = await seed.PreviewAsync(filter ?? new SeedSuppliersDto()) });

    /// <summary>Imports/links the masters. Idempotent — safe to re-run, including after a source that was
    /// unreachable comes back.</summary>
    [HttpPost("seed")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Seed([FromBody] SeedSuppliersDto? dto)
        => Ok(new { data = await seed.SeedAsync(dto ?? new SeedSuppliersDto(), UserId) });

    // ── Reads ──
    [HttpGet]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] SupplierFilterParams filter)
    {
        var result = await service.GetAllAsync(filter);
        return Ok(new
        {
            data = result.Items,
            total = result.Total,
            page = filter.Page,
            pageSize = filter.PageSize,
            pages = (int)Math.Ceiling(result.Total / (double)filter.PageSize),
        });
    }

    [HttpGet("summary")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var s = await service.GetByIdAsync(id);
        return s is null ? NotFound(new { message = "Supplier not found." }) : Ok(new { data = s });
    }

    // ── Create / update ──
    [HttpPost]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Create([FromBody] CreateSupplierDto dto)
        => Ok(new { data = await service.CreateAsync(dto, UserId, UserName) });

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateSupplierDto dto)
    {
        var s = await service.UpdateAsync(id, dto, UserId);
        return s is null ? NotFound(new { message = "Supplier not found." }) : Ok(new { data = s });
    }

    // ── Compliance documents ──
    [HttpGet("{id}/documents")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetDocuments(string id) => Ok(new { data = await service.GetDocumentsAsync(id) });

    [HttpPost("{id}/documents")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> UploadDocument(string id, [FromBody] UploadSupplierDocumentDto dto)
    {
        var d = await service.UploadDocumentAsync(id, dto, UserId);
        return d is null ? NotFound(new { message = "Supplier not found." }) : Ok(new { data = d });
    }

    [HttpPost("documents/{documentId}/verify")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> VerifyDocument(string documentId)
        => Act(await service.VerifyDocumentAsync(documentId, UserId));

    // ── Workflow ──
    [HttpPost("{id}/conflict-check")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> ConflictCheck(string id, [FromBody] ConflictCheckDto dto)
        => Act(await service.RunConflictCheckAsync(id, dto, UserId));

    [HttpPost("{id}/approve")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> Approve(string id) => Act(await service.ApproveAsync(id, UserId));

    [HttpPost("{id}/blacklist")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> Blacklist(string id, [FromBody] BlacklistSupplierDto dto)
        => Act(await service.BlacklistAsync(id, dto, UserId));

    [HttpPost("{id}/reinstate")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> Reinstate(string id) => Act(await service.ReinstateAsync(id, UserId));

    private IActionResult Act(SupplierActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
