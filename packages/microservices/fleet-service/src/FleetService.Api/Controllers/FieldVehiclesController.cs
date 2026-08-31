using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Core.DTOs.Common;
using FleetService.Core.DTOs.FieldVehicle;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/field-vehicles")]
public class FieldVehiclesController(IFieldVehicleService vehicleService) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ─── Field Vehicle Registry ────────────────────────────────────────────────

    /// <summary>Dual-mode: omit `page` to get today's unpaged flat-array response (used by
    /// AssignmentDetailPage's dropdowns); pass it to get a PagedResponseDto with a TotalCount.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] FieldVehicleFilterParameters filters)
    {
        if (filters.Page.HasValue)
        {
            var pn = filters.Page.Value;
            var (items, total) = await vehicleService.GetPagedVehiclesAsync(pn, filters.PageSize, filters);
            return Ok(new { success = true, data = new PagedResponseDto<FieldVehicleResponseDto> { Items = items, PageNumber = pn, PageSize = filters.PageSize, TotalCount = total } });
        }

        return Ok(new { success = true, data = await vehicleService.GetVehiclesAsync(filters) });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var vehicle = await vehicleService.GetVehicleByIdAsync(id);
        if (vehicle is null) return NotFound(new { success = false, message = "Vehicle not found" });
        return Ok(new { success = true, data = vehicle });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFieldVehicleDto dto)
    {
        var vehicle = await vehicleService.CreateVehicleAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, new { success = true, data = vehicle });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateFieldVehicleDto dto)
    {
        try
        {
            var vehicle = await vehicleService.UpdateVehicleAsync(id, dto);
            return Ok(new { success = true, data = vehicle });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { success = false, message = e.Message });
        }
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await vehicleService.DeleteVehicleAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { success = false, message = e.Message });
        }
    }

    // ─── Dispatches ────────────────────────────────────────────────────────────

    [HttpGet("{vehicleId}/dispatches")]
    public async Task<IActionResult> GetDispatchesByVehicle(string vehicleId)
        => Ok(new { success = true, data = await vehicleService.GetDispatchesByVehicleAsync(vehicleId) });

    [HttpGet("assignments/{assignmentId}/dispatches")]
    public async Task<IActionResult> GetDispatches(string assignmentId)
        => Ok(new { success = true, data = await vehicleService.GetDispatchesByAssignmentAsync(assignmentId) });

    [HttpGet("dispatches/pending-count")]
    public async Task<IActionResult> GetPendingDispatchCount()
        => Ok(new { success = true, data = new { count = await vehicleService.GetPendingDispatchCountAsync() } });

    // Fleet manager's approvals queue — every pending request across all assignments, not scoped
    // to one. Inherits the class-level fleet.read policy; Approve/Reject below still require
    // fleet.write, so viewing this list doesn't imply the ability to act on it.
    [HttpGet("dispatches/pending")]
    public async Task<IActionResult> GetPendingDispatches()
        => Ok(new { success = true, data = await vehicleService.GetPendingDispatchesAsync() });

    [Authorize(Policy = "fleet.dispatch.request")]
    [HttpPost("dispatches")]
    public async Task<IActionResult> CreateDispatch([FromBody] CreateVehicleDispatchDto dto)
    {
        try
        {
            var dispatch = await vehicleService.RequestDispatchAsync(dto, UserId);
            return Ok(new { success = true, data = dispatch });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { success = false, message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("dispatches/{dispatchId}/approve")]
    public async Task<IActionResult> ApproveDispatch(string dispatchId, [FromBody] ApproveDispatchDto dto)
    {
        try
        {
            var dispatch = await vehicleService.ApproveDispatchAsync(dispatchId, dto.ApprovedBy, UserId);
            return Ok(new { success = true, data = dispatch });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { success = false, message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("dispatches/{dispatchId}/reject")]
    public async Task<IActionResult> RejectDispatch(string dispatchId)
    {
        try
        {
            var dispatch = await vehicleService.RejectDispatchAsync(dispatchId);
            return Ok(new { success = true, data = dispatch });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { success = false, message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
    }

    // fleet.dispatch.request also covers this via the same hierarchy that
    // already lets fleet.write/fleet.delete/system.admin satisfy it — lets a
    // technician log the return on a dispatch they requested without needing
    // full fleet.write.
    [Authorize(Policy = "fleet.dispatch.request")]
    [HttpPost("dispatches/{dispatchId}/return")]
    public async Task<IActionResult> LogReturn(string dispatchId, [FromBody] LogReturnDto dto)
    {
        try
        {
            var dispatch = await vehicleService.LogReturnAsync(dispatchId, dto);
            return Ok(new { success = true, data = dispatch });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { success = false, message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
    }

    [Authorize(Policy = "fleet.dispatch.request")]
    [HttpPost("dispatches/{dispatchId}/cancel")]
    public async Task<IActionResult> CancelDispatch(string dispatchId)
    {
        try
        {
            var dispatch = await vehicleService.CancelDispatchAsync(dispatchId);
            return Ok(new { success = true, data = dispatch });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { success = false, message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
    }

    // ─── Fuel Logs ─────────────────────────────────────────────────────────────

    [HttpGet("dispatches/{dispatchId}/fuel-logs")]
    public async Task<IActionResult> GetFuelLogs(string dispatchId)
        => Ok(new { success = true, data = await vehicleService.GetDispatchFuelLogsAsync(dispatchId) });

    [Authorize(Policy = "fleet.dispatch.request")]
    [HttpPost("dispatches/{dispatchId}/fuel-logs")]
    public async Task<IActionResult> AddFuelLog(string dispatchId, [FromBody] CreateDispatchFuelLogDto dto)
    {
        try
        {
            var log = await vehicleService.AddDispatchFuelLogAsync(dispatchId, dto);
            return Ok(new { success = true, data = log });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { success = false, message = e.Message });
        }
    }
}
