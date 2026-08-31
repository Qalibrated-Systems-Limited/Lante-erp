using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Services;
using FleetService.Core.DTOs.Common;
using FleetService.Core.DTOs.Trip;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class TripsController(
    ITripService tripService,
    ITicketingServiceClient ticketingClient,
    ITruckService truckService,
    IMapper mapper,
    LocalFileStorageService storage) : ControllerBase
{
    /// <summary>Fraud-prevention sanity cap: a single trip's mileage entry shouldn't jump more
    /// than this many km above the reference reading (the truck's last recorded odometer for a
    /// trip start, or the trip's own start mileage for its end) — catches obviously-fake entries
    /// like 1,000,000 without rejecting genuinely long hauls. An absolute cap doesn't work since
    /// a truck's real lifetime odometer legitimately exceeds this.</summary>
    private const decimal MaxMileageDeltaKm = 2000m;

    /// <summary>Keeps the truck's own odometer in sync with the trip that just finished on it.
    /// Atomic conditional update, not read-then-write: two trips for the same truck completing
    /// concurrently must not let the smaller endMileage clobber a larger one already committed.</summary>
    private Task SyncTruckMileageAsync(string truckId, decimal endMileage)
        => truckService.TryAdvanceOdometerAsync(truckId, endMileage);

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? driverId = null,
        [FromQuery] string? status = null)
    {
        var (items, total) = await tripService.GetPagedAsync(pageNumber, pageSize, driverId, status);
        var dtos = mapper.Map<IEnumerable<TripResponseDto>>(items);
        return Ok(new { success = true, data = new PagedResponseDto<TripResponseDto> { Items = dtos, PageNumber = pageNumber, PageSize = pageSize, TotalCount = total } });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var trip = await tripService.GetByIdWithDetailsAsync(id);
        if (trip == null) return NotFound(new { success = false, message = "Trip not found" });
        return Ok(new { success = true, data = mapper.Map<TripResponseDto>(trip) });
    }

    [HttpGet("driver/{driverId}")]
    public async Task<IActionResult> GetByDriver(string driverId)
        => Ok(new { success = true, data = mapper.Map<IEnumerable<TripResponseDto>>(await tripService.GetByDriverIdAsync(driverId)) });

    [HttpGet("truck/{truckId}")]
    public async Task<IActionResult> GetByTruck(string truckId)
        => Ok(new { success = true, data = mapper.Map<IEnumerable<TripResponseDto>>(await tripService.GetByTruckIdAsync(truckId)) });

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    [Consumes("multipart/form-data", "application/json")]
    public async Task<IActionResult> Create([FromForm] CreateTripDto dto, IFormFile? odometerStartPhoto, IFormFile? materialPhoto, List<IFormFile>? photos)
    {
        if (!await tripService.TripTypeExistsAsync(dto.TripTypeId))
        {
            var available = await tripService.GetTripTypesAsync();
            return BadRequest(new { success = false, message = $"TripTypeId '{dto.TripTypeId}' not found. Call GET /api/v1/TripTypes for valid IDs.", availableTripTypes = available });
        }
        if (!string.IsNullOrEmpty(dto.MaterialId) && !await tripService.MaterialExistsAsync(dto.MaterialId))
            return BadRequest(new { success = false, message = $"MaterialId '{dto.MaterialId}' not found." });
        if (!string.IsNullOrEmpty(dto.MaterialVariantId) && !await tripService.MaterialVariantExistsAsync(dto.MaterialVariantId))
            return BadRequest(new { success = false, message = $"MaterialVariantId '{dto.MaterialVariantId}' not found." });
        var truckForMileageCheck = await truckService.GetByIdAsync(dto.TruckId);
        if (truckForMileageCheck != null)
        {
            var startDelta = dto.StartMileage - truckForMileageCheck.Odometer;
            if (startDelta < 0)
                return BadRequest(new { success = false, message = $"Start Mileage ({dto.StartMileage:N0} km) is below the truck's last recorded odometer ({truckForMileageCheck.Odometer:N0} km) — odometer readings can't go backward. Double-check the value." });
            if (startDelta > MaxMileageDeltaKm)
                return BadRequest(new { success = false, message = $"Start Mileage is {startDelta:N0} km above the truck's last recorded odometer ({truckForMileageCheck.Odometer:N0} km) — that's more than the {MaxMileageDeltaKm:N0} km sanity limit per entry. Double-check the value." });
        }
        var trip = mapper.Map<Trip>(dto);
        var created = await tripService.CreateAsync(trip);
        var odomFile = storage.ResolveFile(odometerStartPhoto, "odometerStartPhoto");
        if (odomFile != null)
        {
            var url = await storage.SaveAsync(odomFile, $"trips/{created.Id}");
            var (updatedCreated, _) = await tripService.UpdateOdometerStartAsync(created.Id, url);
            if (updatedCreated != null) created = updatedCreated;
        }
        var matFile = storage.ResolveFile(materialPhoto, "materialPhoto");
        if (matFile != null)
        {
            var url = await storage.SaveAsync(matFile, $"trips/{created.Id}");
            var (updatedCreated, _) = await tripService.UpdateMaterialPhotoAsync(created.Id, url);
            if (updatedCreated != null) created = updatedCreated;
        }
        if (photos is { Count: > 0 })
        {
            foreach (var photo in photos)
            {
                var url = await storage.SaveAsync(photo, $"trips/{created.Id}");
                var updated = await tripService.AddTripPhotoAsync(created.Id, url);
                if (updated != null) created = updated;
            }
        }
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = mapper.Map<TripResponseDto>(created!) });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateTripDto dto)
    {
        var trip = await tripService.UpdateDetailsAsync(id, dto);
        if (trip == null) return NotFound(new { success = false, message = "Trip not found" });
        return Ok(new { success = true, data = mapper.Map<TripResponseDto>(trip) });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await tripService.DeleteAsync(id);
        return result ? NoContent() : NotFound(new { success = false, message = "Trip not found" });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartTrip(string id)
    {
        var trip = await tripService.StartTripAsync(id);
        if (trip == null) return NotFound(new { success = false, message = "Trip not found" });
        if (!string.IsNullOrWhiteSpace(trip.LinkedTicketId))
            await ticketingClient.NotifyTicketAsync(trip.LinkedTicketId, trip.Id, FleetWorkUpdateType.WorkStarted,
                $"Driver departed for trip: {trip.StartLocation} → {trip.EndLocation}");
        return Ok(new { success = true, data = mapper.Map<TripResponseDto>(trip) });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/complete")]
    [Consumes("multipart/form-data", "application/json")]
    public async Task<IActionResult> CompleteTrip(string id, [FromForm] CompleteTripDto? dto = null, IFormFile? odometerEndPhoto = null)
    {
        var trip = await tripService.GetByIdAsync(id);
        if (trip == null) return NotFound(new { success = false, message = "Trip not found" });
        trip.Status = TripStatus.Completed;
        if (dto?.EndMileage.HasValue == true)
        {
            if (trip.StartMileage.HasValue)
            {
                var endDelta = dto.EndMileage.Value - trip.StartMileage.Value;
                if (endDelta < 0)
                    return BadRequest(new { success = false, message = $"End Mileage ({dto.EndMileage.Value:N0} km) is below the trip's Start Mileage ({trip.StartMileage.Value:N0} km) — odometer readings can't go backward. Double-check the value." });
                if (endDelta > MaxMileageDeltaKm)
                    return BadRequest(new { success = false, message = $"End Mileage is {endDelta:N0} km above the trip's Start Mileage ({trip.StartMileage.Value:N0} km) — that's more than the {MaxMileageDeltaKm:N0} km sanity limit per entry. Double-check the value." });
            }
            trip.EndMileage = dto.EndMileage;
            if (trip.StartMileage.HasValue) trip.TotalMileage = trip.EndMileage - trip.StartMileage;
            await SyncTruckMileageAsync(trip.TruckId, dto.EndMileage.Value);
        }
        trip.Profit = trip.Revenue - trip.TotalCost;
        await tripService.UpdateAsync(trip);
        var odomFile = storage.ResolveFile(odometerEndPhoto, "odometerEndPhoto");
        if (odomFile != null)
        {
            var url = await storage.SaveAsync(odomFile, $"trips/{id}");
            (trip, _) = await tripService.UpdateOdometerEndAsync(id, url);
        }
        if (!string.IsNullOrWhiteSpace(trip!.LinkedTicketId))
            await ticketingClient.NotifyTicketAsync(trip.LinkedTicketId, trip.Id, FleetWorkUpdateType.WorkCompleted,
                $"Trip completed: {trip.StartLocation} → {trip.EndLocation}. Mileage: {trip.TotalMileage} km.");
        return Ok(new { success = true, data = mapper.Map<TripResponseDto>(trip) });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelTrip(string id)
    {
        var trip = await tripService.UpdateStatusAsync(id, TripStatus.Cancelled);
        if (trip == null) return NotFound(new { success = false, message = "Trip not found" });
        if (!string.IsNullOrWhiteSpace(trip.LinkedTicketId))
            await ticketingClient.NotifyTicketAsync(trip.LinkedTicketId, trip.Id, FleetWorkUpdateType.WorkCancelled,
                $"Trip cancelled: {trip.StartLocation} → {trip.EndLocation}");
        return Ok(new { success = true, data = mapper.Map<TripResponseDto>(trip) });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/end")]
    [Consumes("multipart/form-data", "application/json")]
    public async Task<IActionResult> EndTrip(string id, [FromForm] CompleteTripDto? dto = null, IFormFile? odometerEndPhoto = null)
    {
        var trip = await tripService.GetByIdAsync(id);
        if (trip == null) return NotFound(new { success = false, message = "Trip not found" });
        trip.Status = TripStatus.Completed;
        if (dto?.EndMileage.HasValue == true)
        {
            if (trip.StartMileage.HasValue)
            {
                var endDelta = dto.EndMileage.Value - trip.StartMileage.Value;
                if (endDelta < 0)
                    return BadRequest(new { success = false, message = $"End Mileage ({dto.EndMileage.Value:N0} km) is below the trip's Start Mileage ({trip.StartMileage.Value:N0} km) — odometer readings can't go backward. Double-check the value." });
                if (endDelta > MaxMileageDeltaKm)
                    return BadRequest(new { success = false, message = $"End Mileage is {endDelta:N0} km above the trip's Start Mileage ({trip.StartMileage.Value:N0} km) — that's more than the {MaxMileageDeltaKm:N0} km sanity limit per entry. Double-check the value." });
            }
            trip.EndMileage = dto.EndMileage;
            if (trip.StartMileage.HasValue) trip.TotalMileage = trip.EndMileage - trip.StartMileage;
            await SyncTruckMileageAsync(trip.TruckId, dto.EndMileage.Value);
        }
        trip.Profit = trip.Revenue - trip.TotalCost;
        await tripService.UpdateAsync(trip);
        var odomFile = storage.ResolveFile(odometerEndPhoto, "odometerEndPhoto");
        if (odomFile != null)
        {
            var url = await storage.SaveAsync(odomFile, $"trips/{id}");
            (trip, _) = await tripService.UpdateOdometerEndAsync(id, url);
        }
        return Ok(new { success = true, data = mapper.Map<TripResponseDto>(trip!) });
    }

    [HttpGet("{id}/images")]
    public async Task<IActionResult> GetImages(string id)
    {
        var trip = await tripService.GetByIdAsync(id);
        if (trip == null) return NotFound(new { success = false, message = "Trip not found" });
        var tripPhotos = string.IsNullOrEmpty(trip.TripPhotosJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(trip.TripPhotosJson) ?? new List<string>();
        return Ok(new { success = true, data = new { tripId = id, materialPhoto = trip.MaterialPhotoUrl, odometerStart = trip.OdometerStartPhotoUrl, tripPhotos, odometerEnd = trip.OdometerEndPhotoUrl } });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/recalculate-cost")]
    public async Task<IActionResult> RecalculateCost(string id)
    {
        var trip = await tripService.GetByIdWithDetailsAsync(id);
        if (trip == null) return NotFound(new { success = false, message = "Trip not found" });
        var expenseTotal = trip.Expenses.Sum(e => e.Amount);
        trip.TotalCost = expenseTotal + (trip.MaterialCost ?? 0);
        trip.Profit = trip.Revenue - trip.TotalCost;
        await tripService.UpdateAsync(trip);
        return Ok(new { success = true, data = mapper.Map<TripResponseDto>(trip) });
    }
}

public class CompleteTripDto
{
    public decimal? EndMileage { get; set; }
}
