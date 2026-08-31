using Asp.Versioning;
using StoreService.Core.DTOs.Categories;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/categories")]
public class CategoriesController(IPurchasingService purchasing) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] CategoryFilterParameters filters)
    {
        var result = await purchasing.GetCategoriesAsync(filters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var category = await purchasing.GetCategoryByIdAsync(id);
        if (category is null) return NotFoundResult($"Category {id} not found.");
        return OkResult(category);
    }

    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        var category = await purchasing.CreateCategoryAsync(dto, CurrentUserId);
        return CreatedResult(category, "Category created.");
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateCategoryDto dto)
    {
        try
        {
            var category = await purchasing.UpdateCategoryAsync(id, dto, CurrentUserId);
            return OkResult(category, "Category updated.");
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
            await purchasing.DeleteCategoryAsync(id, CurrentUserId);
            return OkResult(new { id }, "Category deleted.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }
}
