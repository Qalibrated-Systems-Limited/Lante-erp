using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface IVehicleClassService : IService<VehicleClass>
{
    Task<VehicleClass?> UpdateDetailsAsync(string id, string name, string? description);
}

public class VehicleClassService(IRepository<VehicleClass> repository) : Service<VehicleClass>(repository), IVehicleClassService
{
    public async Task<VehicleClass?> UpdateDetailsAsync(string id, string name, string? description)
    {
        var vehicleClass = await _repository.GetByIdAsync(id);
        if (vehicleClass == null) return null;
        vehicleClass.Name = name;
        vehicleClass.Description = description;
        return await _repository.UpdateAsync(vehicleClass);
    }
}
