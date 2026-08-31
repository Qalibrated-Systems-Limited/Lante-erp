using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Legal;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/legal")]
[Authorize]
public class LegalController(ILegalService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("summary")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    // ── NDAs ──
    [HttpGet("ndas")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetNdas([FromQuery] string? status) => Ok(new { data = await service.GetNdasAsync(status) });

    [HttpPost("ndas")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CreateNda([FromBody] SaveNdaDto dto) => Ok(new { data = await service.CreateNdaAsync(dto, UserId) });

    [HttpPut("ndas/{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> UpdateNda(string id, [FromBody] SaveNdaDto dto)
    {
        var n = await service.UpdateNdaAsync(id, dto, UserId);
        return n is null ? NotFound(new { message = "NDA not found." }) : Ok(new { data = n });
    }

    [HttpPost("ndas/{id}/terminate")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> TerminateNda(string id) => Act(await service.TerminateNdaAsync(id, UserId));

    // ── Framework agreements ──
    [HttpGet("frameworks")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetFrameworks([FromQuery] string? status) => Ok(new { data = await service.GetFrameworksAsync(status) });

    [HttpPost("frameworks")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CreateFramework([FromBody] SaveFrameworkDto dto) => Ok(new { data = await service.CreateFrameworkAsync(dto, UserId) });

    [HttpPut("frameworks/{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> UpdateFramework(string id, [FromBody] SaveFrameworkDto dto)
    {
        var f = await service.UpdateFrameworkAsync(id, dto, UserId);
        return f is null ? NotFound(new { message = "Agreement not found." }) : Ok(new { data = f });
    }

    [HttpPost("frameworks/{id}/terminate")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> TerminateFramework(string id) => Act(await service.TerminateFrameworkAsync(id, UserId));

    // ── Subcontractor agreements ──
    [HttpGet("subcontracts")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetSubcontracts([FromQuery] string? status) => Ok(new { data = await service.GetSubcontractsAsync(status) });

    [HttpPost("subcontracts")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CreateSubcontract([FromBody] SaveSubcontractDto dto) => Ok(new { data = await service.CreateSubcontractAsync(dto, UserId) });

    [HttpPut("subcontracts/{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> UpdateSubcontract(string id, [FromBody] SaveSubcontractDto dto)
    {
        var s = await service.UpdateSubcontractAsync(id, dto, UserId);
        return s is null ? NotFound(new { message = "Agreement not found." }) : Ok(new { data = s });
    }

    [HttpPost("subcontracts/{id}/terminate")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> TerminateSubcontract(string id) => Act(await service.TerminateSubcontractAsync(id, UserId));

    // ── Carrier agreements ──
    [HttpGet("carriers")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetCarriers([FromQuery] string? vettingStatus) => Ok(new { data = await service.GetCarriersAsync(vettingStatus) });

    [HttpGet("carriers/{id}")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetCarrier(string id)
    {
        var c = await service.GetCarrierAsync(id);
        return c is null ? NotFound(new { message = "Carrier not found." }) : Ok(new { data = c });
    }

    [HttpPost("carriers")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CreateCarrier([FromBody] SaveCarrierDto dto) => Ok(new { data = await service.CreateCarrierAsync(dto, UserId) });

    [HttpPut("carriers/{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> UpdateCarrier(string id, [FromBody] SaveCarrierDto dto)
    {
        var c = await service.UpdateCarrierAsync(id, dto, UserId);
        return c is null ? NotFound(new { message = "Carrier not found." }) : Ok(new { data = c });
    }

    [HttpPost("carriers/{id}/vet")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> VetCarrier(string id, [FromBody] VetCarrierDto dto) => Act(await service.VetCarrierAsync(id, dto, UserId));

    [HttpPost("carriers/{id}/suspend")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> SuspendCarrier(string id) => Act(await service.SuspendCarrierAsync(id, UserId));

    private IActionResult Act(LegalActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
