using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface IMaterialService : IService<Material>
{
    Task<IEnumerable<Material>> GetAllWithDetailsAsync();
    Task<Material?> GetByIdWithDetailsAsync(string id);
    Task<(IEnumerable<Material> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search);
    Task<Material?> UpdateDetailsAsync(string id, string name, string? description);
}

public class MaterialService(IMaterialRepository repository) : Service<Material>(repository), IMaterialService
{
    private readonly IMaterialRepository _materialRepo = repository;

    public Task<IEnumerable<Material>> GetAllWithDetailsAsync() => _materialRepo.GetAllWithDetailsAsync();
    public Task<Material?> GetByIdWithDetailsAsync(string id) => _materialRepo.GetByIdWithDetailsAsync(id);
    public Task<(IEnumerable<Material> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search)
        => _materialRepo.GetPagedAsync(pageNumber, pageSize, search);

    public async Task<Material?> UpdateDetailsAsync(string id, string name, string? description)
    {
        var material = await _repository.GetByIdAsync(id);
        if (material == null) return null;
        material.Name = name;
        material.Description = description;
        return await _repository.UpdateAsync(material);
    }
}

public interface IMaterialVariantService : IService<MaterialVariant>
{
    Task<IEnumerable<MaterialVariant>> GetByMaterialIdAsync(string materialId);
    Task<MaterialVariant?> GetByIdWithPhotosAsync(string id);
}

public class MaterialVariantService(IRepository<MaterialVariant> repository) : Service<MaterialVariant>(repository), IMaterialVariantService
{
    public Task<IEnumerable<MaterialVariant>> GetByMaterialIdAsync(string materialId)
        => _repository.FindAsync(v => v.MaterialId == materialId);

    public Task<MaterialVariant?> GetByIdWithPhotosAsync(string id)
        => _repository.GetByIdAsync(id);
}
