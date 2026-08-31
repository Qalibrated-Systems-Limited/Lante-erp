using Asp.Versioning;
using StoreService.Core.DTOs.Suppliers;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/suppliers")]
public class SuppliersController(IPurchasingService purchasing) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] SupplierFilterParameters filters)
    {
        var result = await purchasing.GetSuppliersAsync(filters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var supplier = await purchasing.GetSupplierByIdAsync(id);
        if (supplier is null) return NotFoundResult($"Supplier {id} not found.");
        return OkResult(supplier);
    }

    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreateSupplierDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        var supplier = await purchasing.CreateSupplierAsync(dto, CurrentUserId);
        return CreatedResult(supplier, "Supplier created.");
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateSupplierDto dto)
    {
        try
        {
            var supplier = await purchasing.UpdateSupplierAsync(id, dto, CurrentUserId);
            return OkResult(supplier, "Supplier updated.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "stores.delete")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await purchasing.DeleteSupplierAsync(id, CurrentUserId);
            return OkResult(new { id }, "Supplier deleted.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }
}
