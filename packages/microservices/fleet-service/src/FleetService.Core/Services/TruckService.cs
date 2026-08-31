using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface ITruckService : IService<Truck>
{
    Task<(IEnumerable<Truck> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? driverId, string? search);
    Task<IEnumerable<Truck>> GetByDriverIdAsync(string driverId);
    Task<Truck?> UpdateDetailsAsync(string id, string licensePlate, string model, string? driverId,
        string? vehicleClassId, DateTime? insuranceExpiryDate, DateTime? nextServiceDate, decimal? odometer, TruckStatus? status,
        string? assetId = null, DateTime? lastServiceDate = null, decimal? lastServiceOdometer = null, decimal? serviceIntervalKm = null);

    Task<bool> TryAdvanceOdometerAsync(string truckId, decimal newOdometer);
}

public class TruckService(ITruckRepository repository) : Service<Truck>(repository), ITruckService
{
    private readonly ITruckRepository _truckRepo = repository;

    public Task<(IEnumerable<Truck> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? driverId, string? search)
        => _truckRepo.GetPagedAsync(pageNumber, pageSize, driverId, search);

    public Task<IEnumerable<Truck>> GetByDriverIdAsync(string driverId)
        => _repository.FindAsync(t => t.DriverId == driverId);

    public Task<bool> TryAdvanceOdometerAsync(string truckId, decimal newOdometer)
        => _truckRepo.TryAdvanceOdometerAsync(truckId, newOdometer);

    public async Task<Truck?> UpdateDetailsAsync(string id, string licensePlate, string model, string? driverId,
        string? vehicleClassId, DateTime? insuranceExpiryDate, DateTime? nextServiceDate, decimal? odometer, TruckStatus? status,
        string? assetId = null, DateTime? lastServiceDate = null, decimal? lastServiceOdometer = null, decimal? serviceIntervalKm = null)
    {
        var truck = await _repository.GetByIdAsync(id);
        if (truck == null) return null;
        truck.LicensePlate = licensePlate;
        truck.Model = model;
        truck.DriverId = driverId;
        truck.VehicleClassId = vehicleClassId;
        truck.InsuranceExpiryDate = insuranceExpiryDate;
        truck.NextServiceDate = nextServiceDate;
        if (odometer.HasValue) truck.Odometer = odometer.Value;
        truck.Status = status ?? truck.Status;
        truck.AssetId = assetId ?? truck.AssetId;
        truck.LastServiceDate = lastServiceDate;
        truck.LastServiceOdometer = lastServiceOdometer;
        truck.ServiceIntervalKm = serviceIntervalKm;
        return await _repository.UpdateAsync(truck);
    }
}
