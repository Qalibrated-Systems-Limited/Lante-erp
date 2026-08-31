using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Core.DTOs.Common;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class TrucksController(ITruckService truckService) : ControllerBase
{
    /// <summary>Dual-mode: omit pageNumber/pageSize to get today's unpaged flat-array
    /// response (used by dropdowns elsewhere); pass either to get a PagedResponseDto.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? driverId = null,
        [FromQuery] string? search = null,
        [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null)
    {
        if (pageNumber.HasValue || pageSize.HasValue)
        {
            var pn = pageNumber ?? 1;
            var ps = pageSize ?? 20;
            var (items, total) = await truckService.GetPagedAsync(pn, ps, driverId, search);
            return Ok(new { success = true, data = new PagedResponseDto<FleetService.Core.Entities.Truck> { Items = items, PageNumber = pn, PageSize = ps, TotalCount = total } });
        }

        var trucks = string.IsNullOrEmpty(driverId)
            ? await truckService.GetAllAsync()
            : await truckService.GetByDriverIdAsync(driverId);
        return Ok(new { success = true, data = trucks });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var truck = await truckService.GetByIdAsync(id);
        if (truck == null) return NotFound(new { success = false, message = "Truck not found" });
        return Ok(new { success = true, data = truck });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTruckRequest req)
    {
        var truck = new FleetService.Core.Entities.Truck
        {
            LicensePlate = req.LicensePlate,
            Model = req.Model,
            DriverId = req.DriverId,
            VehicleClassId = req.VehicleClassId,
            InsuranceExpiryDate = req.InsuranceExpiryDate,
            NextServiceDate = req.NextServiceDate,
            Odometer = req.Odometer ?? 0,
            Status = req.Status ?? FleetService.Core.Entities.TruckStatus.Active,
            AssetId = req.AssetId,
            LastServiceDate = req.LastServiceDate,
            LastServiceOdometer = req.LastServiceOdometer,
            ServiceIntervalKm = req.ServiceIntervalKm
        };
        var created = await truckService.CreateAsync(truck);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateTruckRequest req)
    {
        var truck = await truckService.UpdateDetailsAsync(id, req.LicensePlate, req.Model, req.DriverId,
            req.VehicleClassId, req.InsuranceExpiryDate, req.NextServiceDate, req.Odometer, req.Status, req.AssetId,
            req.LastServiceDate, req.LastServiceOdometer, req.ServiceIntervalKm);
        if (truck == null) return NotFound(new { success = false, message = "Truck not found" });
        return Ok(new { success = true, data = truck });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await truckService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "Truck not found" });
        return NoContent();
    }
}

public record CreateTruckRequest(string LicensePlate, string Model, string? DriverId,
    string? VehicleClassId = null, DateTime? InsuranceExpiryDate = null, DateTime? NextServiceDate = null,
    decimal? Odometer = null, FleetService.Core.Entities.TruckStatus? Status = null, string? AssetId = null,
    DateTime? LastServiceDate = null, decimal? LastServiceOdometer = null, decimal? ServiceIntervalKm = null);
