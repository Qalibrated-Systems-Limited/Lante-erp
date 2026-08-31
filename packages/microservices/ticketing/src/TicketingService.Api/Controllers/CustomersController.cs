using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.Customers;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
public class CustomersController(ICustomerService customerService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// D1-3 — combobox source: searchable list (name / company / reference / email).
    [HttpGet]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CustomerReadDto>>>> Search([FromQuery] string? q)
    {
        var customers = await customerService.SearchAsync(q);
        return Ok(ApiResponse<IEnumerable<CustomerReadDto>>.Ok(customers));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<CustomerReadDto>>> GetById(string id)
    {
        var c = await customerService.GetByIdAsync(id);
        if (c == null) return NotFound(ApiResponse<CustomerReadDto>.Fail("Customer not found.", 404));
        return Ok(ApiResponse<CustomerReadDto>.Ok(c));
    }

    [HttpPost]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<CustomerReadDto>>> Create([FromBody] CreateCustomerDto dto)
    {
        var created = await customerService.CreateAsync(dto, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1" },
            ApiResponse<CustomerReadDto>.Ok(created, "Customer created successfully."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<CustomerReadDto>>> Update(string id, [FromBody] UpdateCustomerDto dto)
    {
        var updated = await customerService.UpdateAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<CustomerReadDto>.Ok(updated, "Customer updated successfully."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        await customerService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(null!, "Customer deleted successfully."));
    }
}
