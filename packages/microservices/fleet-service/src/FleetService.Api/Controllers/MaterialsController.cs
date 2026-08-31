using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Core.DTOs.Common;
using FleetService.Core.Entities;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class MaterialsController(IMaterialService materialService) : ControllerBase
{
    /// <summary>Dual-mode: omit pageNumber/pageSize to get today's unpaged flat-array
    /// response (used by CreateTripPage's dropdown); pass either to get a PagedResponseDto.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null)
    {
        if (pageNumber.HasValue || pageSize.HasValue)
        {
            var pn = pageNumber ?? 1;
            var ps = pageSize ?? 20;
            var (items, total) = await materialService.GetPagedAsync(pn, ps, search);
            return Ok(new { success = true, data = new PagedResponseDto<Material> { Items = items, PageNumber = pn, PageSize = ps, TotalCount = total } });
        }

        return Ok(new { success = true, data = await materialService.GetAllWithDetailsAsync() });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var material = await materialService.GetByIdWithDetailsAsync(id);
        if (material == null) return NotFound(new { success = false, message = "Material not found" });
        return Ok(new { success = true, data = material });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaterialRequest req)
    {
        var material = new Material { Name = req.Name, Description = req.Description };
        var created = await materialService.CreateAsync(material);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateMaterialRequest req)
    {
        var material = await materialService.UpdateDetailsAsync(id, req.Name, req.Description);
        if (material == null) return NotFound(new { success = false, message = "Material not found" });
        return Ok(new { success = true, data = material });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await materialService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "Material not found" });
        return NoContent();
    }
}

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/material-variants")]
public class MaterialVariantsController(IMaterialVariantService materialVariantService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? materialId = null)
    {
        var items = string.IsNullOrEmpty(materialId)
            ? await materialVariantService.GetAllAsync()
            : await materialVariantService.GetByMaterialIdAsync(materialId);
        return Ok(new { success = true, data = items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var variant = await materialVariantService.GetByIdWithPhotosAsync(id);
        if (variant == null) return NotFound(new { success = false, message = "Variant not found" });
        return Ok(new { success = true, data = variant });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVariantRequest req)
    {
        var variant = new MaterialVariant { MaterialId = req.MaterialId, Name = req.Name, Description = req.Description };
        var created = await materialVariantService.CreateAsync(variant);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await materialVariantService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "Variant not found" });
        return NoContent();
    }
}

public record CreateMaterialRequest(string Name, string? Description);
public record CreateVariantRequest(string MaterialId, string Name, string? Description);
