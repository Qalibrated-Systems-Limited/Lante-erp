using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface ITripTypeService : IService<TripType>
{
    Task<(IEnumerable<TripType> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, bool? isActive);
    Task<IEnumerable<TripType>> GetActiveAsync();
    Task<bool> ExistsAsync(string id);
    Task<TripType?> UpdateDetailsAsync(string id, string name, string? description, bool isActive, TripCategory category, EmptyTripOption emptyTripOption, MaterialRequirement materialRequirement);
}

public class TripTypeService(ITripTypeRepository repository) : Service<TripType>(repository), ITripTypeService
{
    private readonly ITripTypeRepository _tripTypeRepo = repository;

    public Task<(IEnumerable<TripType> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, bool? isActive)
        => _tripTypeRepo.GetPagedAsync(pageNumber, pageSize, isActive);

    public Task<IEnumerable<TripType>> GetActiveAsync()
        => _repository.FindAsync(t => t.IsActive);

    public async Task<bool> ExistsAsync(string id)
        => (await _repository.GetByIdAsync(id)) != null;

    public async Task<TripType?> UpdateDetailsAsync(string id, string name, string? description, bool isActive, TripCategory category, EmptyTripOption emptyTripOption, MaterialRequirement materialRequirement)
    {
        var tripType = await _repository.GetByIdAsync(id);
        if (tripType == null) return null;
        tripType.Name = name;
        tripType.Description = description;
        tripType.IsActive = isActive;
        tripType.Category = category;
        tripType.EmptyTripOption = emptyTripOption;
        tripType.MaterialRequirement = materialRequirement;
        return await _repository.UpdateAsync(tripType);
    }
}
