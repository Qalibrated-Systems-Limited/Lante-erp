using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface ILicenseClassService : IService<LicenseClass>
{
    Task<LicenseClass?> UpdateDetailsAsync(string id, string name, string? description);
}

public class LicenseClassService(IRepository<LicenseClass> repository) : Service<LicenseClass>(repository), ILicenseClassService
{
    public async Task<LicenseClass?> UpdateDetailsAsync(string id, string name, string? description)
    {
        var item = await _repository.GetByIdAsync(id);
        if (item == null) return null;
        item.Name = name;
        item.Description = description;
        return await _repository.UpdateAsync(item);
    }
}
