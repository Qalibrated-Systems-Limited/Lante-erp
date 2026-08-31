using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface IMaterialCostService : IService<MaterialCost>
{
    Task<IEnumerable<MaterialCost>> GetByMaterialIdAsync(string materialId);
    Task<MaterialCost?> UpdateCostAsync(string id, decimal cost, string? location);
}

public class MaterialCostService(IRepository<MaterialCost> repository) : Service<MaterialCost>(repository), IMaterialCostService
{
    public Task<IEnumerable<MaterialCost>> GetByMaterialIdAsync(string materialId)
        => _repository.FindAsync(c => c.MaterialId == materialId);

    public async Task<MaterialCost?> UpdateCostAsync(string id, decimal cost, string? location)
    {
        var item = await _repository.GetByIdAsync(id);
        if (item == null) return null;
        item.Cost = cost;
        item.Location = location;
        return await _repository.UpdateAsync(item);
    }
}
