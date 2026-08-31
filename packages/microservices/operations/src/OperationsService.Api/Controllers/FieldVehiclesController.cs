using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Vehicles;
using OperationsService.Core.Interfaces.Services;
using System.Security.Claims;

namespace OperationsService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/field-vehicles")]
[Authorize]
public class FieldVehiclesController(IFieldVehicleService vehicleService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private bool IsAdmin => User.HasClaim("permission", "system.admin");
    private bool CanWrite => IsAdmin || User.HasClaim("permission", "operations.write")
                                     || User.HasClaim("permission", "operations.approve");
    private bool CanRead => CanWrite || User.HasClaim("permission", "operations.read.all")
                                     || User.HasClaim("permission", "operations.read.dept")
                                     || User.HasClaim("permission", "operations.read.own");

    // ─── Field Vehicle Registry ────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<FieldVehicleReadDto>>>> GetAll(
        [FromQuery] FieldVehicleFilterParameters filters)
    {
        var result = await vehicleService.GetVehiclesAsync(filters);
        return Ok(new ApiResponse<PaginatedResult<FieldVehicleReadDto>> { Success = true, Data = result });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<FieldVehicleReadDto>>> GetById(Guid id)
    {
        var vehicle = await vehicleService.GetVehicleByIdAsync(id.ToString());
        if (vehicle is null)
            return NotFound(new ApiResponse<FieldVehicleReadDto> { Success = false, Message = "Vehicle not found." });
        return Ok(new ApiResponse<FieldVehicleReadDto> { Success = true, Data = vehicle });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<FieldVehicleReadDto>>> Create([FromBody] CreateFieldVehicleDto dto)
    {
        var vehicle = await vehicleService.CreateVehicleAsync(dto, UserId);
        return CreatedAtAction(nameof(GetById), new { id = vehicle.Id },
            new ApiResponse<FieldVehicleReadDto> { Success = true, Data = vehicle });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<FieldVehicleReadDto>>> Update(Guid id, [FromBody] UpdateFieldVehicleDto dto)
    {
        try
        {
            var vehicle = await vehicleService.UpdateVehicleAsync(id.ToString(), dto, UserId);
            return Ok(new ApiResponse<FieldVehicleReadDto> { Success = true, Data = vehicle });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse<FieldVehicleReadDto> { Success = false, Message = e.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id)
    {
        try
        {
            await vehicleService.DeleteVehicleAsync(id.ToString(), UserId);
            return Ok(new ApiResponse<object> { Success = true, Message = "Vehicle deleted." });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = e.Message });
        }
    }

    // ─── Dispatches ────────────────────────────────────────────────────────────

    [HttpGet("{vehicleId:guid}/dispatches")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<VehicleDispatchReadDto>>>> GetDispatchesByVehicle(Guid vehicleId)
    {
        var dispatches = await vehicleService.GetDispatchesByVehicleAsync(vehicleId.ToString());
        return Ok(new ApiResponse<IEnumerable<VehicleDispatchReadDto>> { Success = true, Data = dispatches });
    }

    [HttpGet("assignments/{assignmentId:guid}/dispatches")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<VehicleDispatchReadDto>>>> GetDispatches(Guid assignmentId)
    {
        var dispatches = await vehicleService.GetDispatchesByAssignmentAsync(assignmentId.ToString());
        return Ok(new ApiResponse<IEnumerable<VehicleDispatchReadDto>> { Success = true, Data = dispatches });
    }

    [HttpPost("dispatches")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<ActionResult<ApiResponse<VehicleDispatchReadDto>>> CreateDispatch([FromBody] CreateVehicleDispatchDto dto)
    {
        try
        {
            var dispatch = await vehicleService.CreateDispatchAsync(dto, UserId);
            return Ok(new ApiResponse<VehicleDispatchReadDto> { Success = true, Data = dispatch });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse<VehicleDispatchReadDto> { Success = false, Message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new ApiResponse<VehicleDispatchReadDto> { Success = false, Message = e.Message });
        }
    }

    [HttpPost("dispatches/{dispatchId:guid}/approve")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<VehicleDispatchReadDto>>> ApproveDispatch(Guid dispatchId, [FromBody] ApproveDispatchDto dto)
    {
        try
        {
            var dispatch = await vehicleService.ApproveDispatchAsync(dispatchId.ToString(), dto.ApprovedBy, UserId);
            return Ok(new ApiResponse<VehicleDispatchReadDto> { Success = true, Data = dispatch });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse<VehicleDispatchReadDto> { Success = false, Message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new ApiResponse<VehicleDispatchReadDto> { Success = false, Message = e.Message });
        }
    }

    [HttpPost("dispatches/{dispatchId:guid}/reject")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<VehicleDispatchReadDto>>> RejectDispatch(Guid dispatchId)
    {
        try
        {
            var dispatch = await vehicleService.RejectDispatchAsync(dispatchId.ToString(), UserId);
            return Ok(new ApiResponse<VehicleDispatchReadDto> { Success = true, Data = dispatch });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse<VehicleDispatchReadDto> { Success = false, Message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new ApiResponse<VehicleDispatchReadDto> { Success = false, Message = e.Message });
        }
    }

    [HttpPost("dispatches/{dispatchId:guid}/return")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<ActionResult<ApiResponse<VehicleDispatchReadDto>>> LogReturn(Guid dispatchId, [FromBody] LogReturnDto dto)
    {
        try
        {
            var dispatch = await vehicleService.LogReturnAsync(dispatchId.ToString(), dto, UserId);
            return Ok(new ApiResponse<VehicleDispatchReadDto> { Success = true, Data = dispatch });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse<VehicleDispatchReadDto> { Success = false, Message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new ApiResponse<VehicleDispatchReadDto> { Success = false, Message = e.Message });
        }
    }

    [HttpPost("dispatches/{dispatchId:guid}/cancel")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<VehicleDispatchReadDto>>> CancelDispatch(Guid dispatchId)
    {
        try
        {
            var dispatch = await vehicleService.CancelDispatchAsync(dispatchId.ToString(), UserId);
            return Ok(new ApiResponse<VehicleDispatchReadDto> { Success = true, Data = dispatch });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse<VehicleDispatchReadDto> { Success = false, Message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new ApiResponse<VehicleDispatchReadDto> { Success = false, Message = e.Message });
        }
    }

    // ─── Fuel Logs ─────────────────────────────────────────────────────────────

    [HttpGet("dispatches/{dispatchId:guid}/fuel-logs")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<FuelLogReadDto>>>> GetFuelLogs(Guid dispatchId)
    {
        var logs = await vehicleService.GetFuelLogsAsync(dispatchId.ToString());
        return Ok(new ApiResponse<IEnumerable<FuelLogReadDto>> { Success = true, Data = logs });
    }

    [HttpPost("dispatches/{dispatchId:guid}/fuel-logs")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<ActionResult<ApiResponse<FuelLogReadDto>>> AddFuelLog(Guid dispatchId, [FromBody] CreateFuelLogDto dto)
    {
        dto.DispatchId = dispatchId.ToString();
        try
        {
            var log = await vehicleService.AddFuelLogAsync(dto, UserId);
            return Ok(new ApiResponse<FuelLogReadDto> { Success = true, Data = log });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse<FuelLogReadDto> { Success = false, Message = e.Message });
        }
    }
}
