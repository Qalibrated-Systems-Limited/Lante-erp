using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.AfterSales;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/aftersales")]
[Authorize]
public class AfterSalesController(IAfterSalesService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("summary")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    // ── Satisfaction surveys ──
    [HttpGet("surveys")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetSurveys([FromQuery] string? customerId, [FromQuery] string? status)
        => Ok(new { data = await service.GetSurveysAsync(customerId, status) });

    [HttpPost("surveys")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> SendSurvey([FromBody] SendSurveyDto dto)
        => Ok(new { data = await service.SendSurveyAsync(dto, UserId) });

    [HttpPost("surveys/{id}/respond")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> RespondSurvey(string id, [FromBody] SurveyResponseDto dto)
    {
        var s = await service.RespondSurveyAsync(id, dto, UserId);
        return s is null ? NotFound(new { message = "Survey not found." }) : Ok(new { data = s });
    }

    // ── Service contracts ──
    [HttpGet("service-contracts")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetContracts([FromQuery] string? customerId, [FromQuery] string? status)
        => Ok(new { data = await service.GetServiceContractsAsync(customerId, status) });

    [HttpGet("service-contracts/{id}")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetContract(string id)
    {
        var c = await service.GetServiceContractAsync(id);
        return c is null ? NotFound(new { message = "Contract not found." }) : Ok(new { data = c });
    }

    [HttpPost("service-contracts")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CreateContract([FromBody] SaveServiceContractDto dto)
        => Ok(new { data = await service.CreateServiceContractAsync(dto, UserId) });

    [HttpPut("service-contracts/{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> UpdateContract(string id, [FromBody] SaveServiceContractDto dto)
    {
        var c = await service.UpdateServiceContractAsync(id, dto, UserId);
        return c is null ? NotFound(new { message = "Contract not found." }) : Ok(new { data = c });
    }

    [HttpPost("service-contracts/{id}/renew")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> RenewContract(string id, [FromBody] RenewServiceContractDto dto)
        => Act(await service.RenewServiceContractAsync(id, dto, UserId));

    [HttpPost("service-contracts/{id}/cancel")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CancelContract(string id)
        => Act(await service.CancelServiceContractAsync(id, UserId));

    // ── Complaints ──
    [HttpGet("complaints")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetComplaints([FromQuery] string? customerId, [FromQuery] string? status)
        => Ok(new { data = await service.GetComplaintsAsync(customerId, status) });

    [HttpPost("complaints")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> RaiseComplaint([FromBody] RaiseComplaintDto dto)
        => Ok(new { data = await service.RaiseComplaintAsync(dto, UserId) });

    [HttpPost("complaints/{id}/assign")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> AssignComplaint(string id, [FromBody] AssignComplaintDto dto)
        => Act(await service.AssignComplaintAsync(id, dto, UserId));

    [HttpPost("complaints/{id}/start")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> StartComplaint(string id)
        => Act(await service.StartComplaintAsync(id, UserId));

    [HttpPost("complaints/{id}/resolve")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> ResolveComplaint(string id, [FromBody] ResolveComplaintDto dto)
        => Act(await service.ResolveComplaintAsync(id, dto, UserId));

    [HttpPost("complaints/{id}/close")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CloseComplaint(string id)
        => Act(await service.CloseComplaintAsync(id, UserId));

    // ── NPS ──
    [HttpGet("nps")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetNps([FromQuery] string? customerId, [FromQuery] int? year)
        => Ok(new { data = await service.GetNpsAsync(customerId, year) });

    [HttpPost("nps")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> SendNps([FromBody] SendNpsDto dto)
        => Ok(new { data = await service.SendNpsAsync(dto, UserId) });

    [HttpPost("nps/{id}/respond")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> RespondNps(string id, [FromBody] NpsResponseDto dto)
    {
        var n = await service.RespondNpsAsync(id, dto, UserId);
        return n is null ? NotFound(new { message = "NPS survey not found." }) : Ok(new { data = n });
    }

    // ── O6 — calibration recall (inbound from Operations service) ──
    [HttpPost("calibration-recall")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CalibrationRecall([FromBody] CalibrationRecallDto dto)
        => Act(await service.RecordCalibrationRecallAsync(dto, UserId));

    private IActionResult Act(AfterSalesActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
