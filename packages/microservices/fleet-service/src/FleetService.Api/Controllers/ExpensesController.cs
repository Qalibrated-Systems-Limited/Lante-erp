using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Services;
using FleetService.Core.DTOs.Expense;
using FleetService.Core.Entities;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.expenses")]
[ApiController]
[Route("api/v1/[controller]")]
public class ExpensesController(IExpenseService expenseService, LocalFileStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? tripId = null)
        => Ok(new { success = true, data = await expenseService.GetAllAsync(tripId) });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var expense = await expenseService.GetByIdAsync(id);
        if (expense == null) return NotFound(new { success = false, message = "Expense not found" });
        return Ok(new { success = true, data = expense });
    }

    [HttpGet("trip/{tripId}")]
    public async Task<IActionResult> GetByTrip(string tripId)
        => Ok(new { success = true, data = await expenseService.GetByTripIdAsync(tripId) });

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    [Consumes("multipart/form-data", "application/json")]
    public async Task<IActionResult> Create([FromForm] CreateExpenseDto dto, IFormFile? image)
    {
        var created = await expenseService.CreateFromDtoAsync(dto);
        var file = storage.ResolveFile(image);
        if (file != null)
        {
            var url = await storage.SaveAsync(file, $"expenses/{created.Id}");
            created = (await expenseService.UpdateReceiptPhotoAsync(created.Id, url)).expense ?? created;
        }
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Expense updated)
    {
        var expense = await expenseService.UpdateDetailsAsync(id, updated.Description, updated.Amount);
        if (expense == null) return NotFound(new { success = false, message = "Expense not found" });
        return Ok(new { success = true, data = expense });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/receipt-photo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadReceiptPhoto(string id, IFormFile? image)
    {
        var file = storage.ResolveFile(image);
        if (file == null) return BadRequest(new { success = false, message = "No image file provided" });
        var url = await storage.SaveAsync(file, $"expenses/{id}");
        var (expense, oldUrl) = await expenseService.UpdateReceiptPhotoAsync(id, url);
        if (expense == null) { storage.Delete(url); return NotFound(new { success = false, message = "Expense not found" }); }
        storage.Delete(oldUrl);
        return Ok(new { success = true, data = new { receiptPhoto = url } });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await expenseService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "Expense not found" });
        return NoContent();
    }
}
