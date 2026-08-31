using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementService.Core.DTOs.Suppliers;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P1 — supplier categories (classification + minimum performance score threshold).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/supplier-categories")]
[Authorize]
public class SupplierCategoriesController(ISupplierService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetAll() => Ok(new { data = await service.GetCategoriesAsync() });

    [HttpPost]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Create([FromBody] SaveSupplierCategoryDto dto)
        => Ok(new { data = await service.SaveCategoryAsync(dto, UserId) });

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Update(string id, [FromBody] SaveSupplierCategoryDto dto)
        => Ok(new { data = await service.SaveCategoryAsync(dto, UserId, id) });
}
