using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface IFieldVehiclePhotoService : IService<FieldVehiclePhoto>
{
    Task<IEnumerable<FieldVehiclePhoto>> GetByFieldVehicleIdAsync(string fieldVehicleId);
    Task<FieldVehiclePhoto> AddPhotoAsync(string fieldVehicleId, string url, string? caption);
    Task<string?> DeletePhotoAsync(string id);
}

public class FieldVehiclePhotoService(IRepository<FieldVehiclePhoto> repository) : Service<FieldVehiclePhoto>(repository), IFieldVehiclePhotoService
{
    public Task<IEnumerable<FieldVehiclePhoto>> GetByFieldVehicleIdAsync(string fieldVehicleId)
        => _repository.FindAsync(p => p.FieldVehicleId == fieldVehicleId);

    public Task<FieldVehiclePhoto> AddPhotoAsync(string fieldVehicleId, string url, string? caption)
        => _repository.CreateAsync(new FieldVehiclePhoto
        {
            FieldVehicleId = fieldVehicleId,
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
