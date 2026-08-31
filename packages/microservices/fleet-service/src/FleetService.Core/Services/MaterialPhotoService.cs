using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface IMaterialPhotoService : IService<MaterialPhoto>
{
    Task<IEnumerable<MaterialPhoto>> GetByMaterialIdAsync(string materialId);
    Task<MaterialPhoto> AddPhotoAsync(string materialId, string url, string? caption);
    Task<string?> DeletePhotoAsync(string id);
}

public class MaterialPhotoService(IRepository<MaterialPhoto> repository) : Service<MaterialPhoto>(repository), IMaterialPhotoService
{
    public Task<IEnumerable<MaterialPhoto>> GetByMaterialIdAsync(string materialId)
        => _repository.FindAsync(p => p.MaterialId == materialId);

    public Task<MaterialPhoto> AddPhotoAsync(string materialId, string url, string? caption)
        => _repository.CreateAsync(new MaterialPhoto
        {
            MaterialId = materialId,
            PhotoUrl = url,
            Caption = caption
        });

    public async Task<string?> DeletePhotoAsync(string id)
    {
        var photo = await _repository.GetByIdAsync(id);
        if (photo == null) return null;
        var url = photo.PhotoUrl;
        await _repository.DeleteAsync(id);
        return url;
    }
}

public interface IMaterialVariantPhotoService : IService<MaterialVariantPhoto>
{
    Task<IEnumerable<MaterialVariantPhoto>> GetByVariantIdAsync(string variantId);
    Task<MaterialVariantPhoto> AddPhotoAsync(string variantId, string url, string? caption);
    Task<string?> DeletePhotoAsync(string id);
}

public class MaterialVariantPhotoService(IRepository<MaterialVariantPhoto> repository) : Service<MaterialVariantPhoto>(repository), IMaterialVariantPhotoService
{
    public Task<IEnumerable<MaterialVariantPhoto>> GetByVariantIdAsync(string variantId)
        => _repository.FindAsync(p => p.MaterialVariantId == variantId);

    public Task<MaterialVariantPhoto> AddPhotoAsync(string variantId, string url, string? caption)
        => _repository.CreateAsync(new MaterialVariantPhoto
        {
            MaterialVariantId = variantId,
            PhotoUrl = url,
            Caption = caption
        });

    public async Task<string?> DeletePhotoAsync(string id)
    {
        var photo = await _repository.GetByIdAsync(id);
        if (photo == null) return null;
        var url = photo.PhotoUrl;
        await _repository.DeleteAsync(id);
        return url;
    }
}
