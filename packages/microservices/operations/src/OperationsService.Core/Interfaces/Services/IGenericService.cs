namespace OperationsService.Core.Interfaces.Services;

public interface IGenericService<TRead, TCreate, TUpdate>
{
    Task<TRead?> GetByIdAsync(string id);
    Task<TRead> CreateAsync(TCreate dto, string userId);
    Task<TRead> UpdateAsync(string id, TUpdate dto, string userId);
    Task DeleteAsync(string id, string userId);
}
