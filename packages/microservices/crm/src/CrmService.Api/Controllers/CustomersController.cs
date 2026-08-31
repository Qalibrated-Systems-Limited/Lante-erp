using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Customers;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
[Authorize]
public class CustomersController(ICustomerService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    // ── Reads ──
    [HttpGet]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] CustomerFilterParams filter)
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

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var c = await service.GetByIdAsync(id);
        if (c is null) return NotFound(new { message = "Customer not found." });
        return Ok(new { data = c });
    }

    [HttpGet("duplicate-check")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> DuplicateCheck([FromQuery] string? name, [FromQuery] string? email, [FromQuery] string? phone, [FromQuery] string? kraPin)
        => Ok(new { data = await service.CheckDuplicateAsync(name, email, phone, kraPin) });

    // ── Create / update ──
    [HttpPost]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
        => Ok(new { data = await service.CreateAsync(dto, UserId, UserName) });

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateCustomerDto dto)
        => Ok(new { data = await service.UpdateAsync(id, dto, UserId) });

    // ── 4-stage onboarding approval ──
    [HttpPost("{id}/approve/line-manager")]
    [Authorize(Policy = "Permission:crm.approve.linemanager")]
    public async Task<IActionResult> ApproveLineManager(string id)
        => Ok(new { data = await service.ApproveLineManagerAsync(id, UserId) });

    [HttpPost("{id}/approve/head-bd")]
    [Authorize(Policy = "Permission:crm.approve.bd")]
    public async Task<IActionResult> ApproveHeadBd(string id)
        => Ok(new { data = await service.ApproveHeadBdAsync(id, UserId) });

    [HttpPost("{id}/cfo-review")]
    [Authorize(Policy = "Permission:crm.approve.cfo")]
    public async Task<IActionResult> CfoReview(string id, [FromBody] CfoReviewDto dto)
        => Ok(new { data = await service.CfoReviewAsync(id, dto, UserId) });

    [HttpPost("{id}/approve/md")]
    [Authorize(Policy = "Permission:crm.approve.md")]
    public async Task<IActionResult> ApproveMd(string id)
        => Ok(new { data = await service.ApproveMdAsync(id, UserId) });

    [HttpPost("{id}/reject")]
    [Authorize(Policy = "Permission:crm.approve.bd")]
    public async Task<IActionResult> Reject(string id, [FromBody] RejectCustomerDto dto)
        => Ok(new { data = await service.RejectAsync(id, dto, UserId) });

    [HttpPost("{id}/deactivate")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Deactivate(string id)
        => Ok(new { data = await service.DeactivateAsync(id, UserId) });

    // ── Contacts ──
    [HttpPost("{id}/contacts")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> AddContact(string id, [FromBody] CreateCustomerContactDto dto)
        => Ok(new { data = await service.AddContactAsync(id, dto, UserId) });

    [HttpPut("contacts/{contactId}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> UpdateContact(string contactId, [FromBody] CreateCustomerContactDto dto)
        => Ok(new { data = await service.UpdateContactAsync(contactId, dto, UserId) });
}
