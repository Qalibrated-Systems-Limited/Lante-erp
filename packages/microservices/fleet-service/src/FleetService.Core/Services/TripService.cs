using FleetService.Core.DTOs.Trip;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface ITripService : IService<Trip>
{
    Task<(IEnumerable<Trip> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? driverId, string? status);
    Task<IEnumerable<Trip>> GetByDriverIdAsync(string driverId);
    Task<IEnumerable<Trip>> GetByTruckIdAsync(string truckId);
    Task<Trip?> GetByIdWithDetailsAsync(string id);
    Task<bool> TripTypeExistsAsync(string tripTypeId);
    Task<IEnumerable<object>> GetTripTypesAsync();
    Task<bool> MaterialExistsAsync(string materialId);
    Task<bool> MaterialVariantExistsAsync(string variantId);
    Task<Trip?> UpdateStatusAsync(string id, TripStatus status);
    Task<Trip?> StartTripAsync(string id);
    Task<Trip?> UpdateDetailsAsync(string id, UpdateTripDto dto);
    Task<(Trip? trip, string? oldUrl)> UpdateMaterialPhotoAsync(string id, string url);
    Task<(Trip? trip, string? oldUrl)> UpdateOdometerStartAsync(string id, string url);
    Task<Trip?> AddTripPhotoAsync(string id, string url);
    Task<(Trip? trip, string? oldUrl)> UpdateOdometerEndAsync(string id, string url);
}

public class TripService(ITripRepository repository) : Service<Trip>(repository), ITripService
{
    private readonly ITripRepository _tripRepo = repository;

    public Task<(IEnumerable<Trip> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? driverId, string? status)
        => _tripRepo.GetPagedAsync(pageNumber, pageSize, driverId, status);

    public Task<IEnumerable<Trip>> GetByDriverIdAsync(string driverId)
        => _tripRepo.GetByDriverIdAsync(driverId);

    public Task<IEnumerable<Trip>> GetByTruckIdAsync(string truckId)
        => _tripRepo.GetByTruckIdAsync(truckId);

    public Task<Trip?> GetByIdWithDetailsAsync(string id)
        => _tripRepo.GetByIdWithDetailsAsync(id);

    public Task<bool> TripTypeExistsAsync(string tripTypeId)
        => _tripRepo.TripTypeExistsAsync(tripTypeId);

    public Task<IEnumerable<object>> GetTripTypesAsync()
        => _tripRepo.GetTripTypesAsync();

    public Task<bool> MaterialExistsAsync(string materialId)
        => _tripRepo.MaterialExistsAsync(materialId);

    public Task<bool> MaterialVariantExistsAsync(string variantId)
        => _tripRepo.MaterialVariantExistsAsync(variantId);

    public async Task<Trip?> UpdateStatusAsync(string id, TripStatus status)
    {
        var trip = await _repository.GetByIdAsync(id);
        if (trip == null) return null;
        trip.Status = status;
        return await _repository.UpdateAsync(trip);
    }

    public async Task<Trip?> StartTripAsync(string id)
    {
        var trip = await _repository.GetByIdAsync(id);
        if (trip == null) return null;
        trip.Status = TripStatus.InProgress;
        return await _repository.UpdateAsync(trip);
    }

    public async Task<Trip?> UpdateDetailsAsync(string id, UpdateTripDto dto)
    {
        var trip = await _repository.GetByIdAsync(id);
        if (trip == null) return null;
        trip.CustomTripType = dto.CustomTripType;
        trip.StartLocation = dto.StartLocation;
        trip.EndLocation = dto.EndLocation;
        trip.CurrentLocationLatitude = dto.CurrentLocationLatitude;
        trip.CurrentLocationLongitude = dto.CurrentLocationLongitude;
        trip.EndMileage = dto.EndMileage;
        trip.MaterialId = dto.MaterialId;
        trip.MaterialVariantId = dto.MaterialVariantId;
        trip.MaterialCost = dto.MaterialCost;
        return await _repository.UpdateAsync(trip);
    }

    public async Task<(Trip? trip, string? oldUrl)> UpdateMaterialPhotoAsync(string id, string url)
    {
        var trip = await _repository.GetByIdAsync(id);
        if (trip == null) return (null, null);
        var oldUrl = trip.MaterialPhotoUrl;
        trip.MaterialPhotoUrl = url;
        return (await _repository.UpdateAsync(trip), oldUrl);
    }

    public async Task<(Trip? trip, string? oldUrl)> UpdateOdometerStartAsync(string id, string url)
    {
        var trip = await _repository.GetByIdAsync(id);
        if (trip == null) return (null, null);
        var oldUrl = trip.OdometerStartPhotoUrl;
        trip.OdometerStartPhotoUrl = url;
        return (await _repository.UpdateAsync(trip), oldUrl);
    }

    public async Task<Trip?> AddTripPhotoAsync(string id, string url)
    {
        var trip = await _repository.GetByIdAsync(id);
        if (trip == null) return null;
        var photos = string.IsNullOrEmpty(trip.TripPhotosJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(trip.TripPhotosJson) ?? new List<string>();
        photos.Add(url);
        trip.TripPhotosJson = System.Text.Json.JsonSerializer.Serialize(photos);
        return await _repository.UpdateAsync(trip);
    }

    public async Task<(Trip? trip, string? oldUrl)> UpdateOdometerEndAsync(string id, string url)
    {
        var trip = await _repository.GetByIdAsync(id);
        if (trip == null) return (null, null);
        var oldUrl = trip.OdometerEndPhotoUrl;
        trip.OdometerEndPhotoUrl = url;
        return (await _repository.UpdateAsync(trip), oldUrl);
    }
}
