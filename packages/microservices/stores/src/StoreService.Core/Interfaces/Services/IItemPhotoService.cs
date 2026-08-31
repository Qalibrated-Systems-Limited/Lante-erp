using StoreService.Core.Entities;

namespace StoreService.Core.Interfaces.Services;

public interface IItemPhotoService
{
    Task<IEnumerable<ItemPhoto>> GetByItemIdAsync(string itemId);
    Task<ItemPhoto?> GetByIdAsync(string id);
    Task<ItemPhoto> AddPhotoAsync(string itemId, string url, string? caption);
    Task<string?> DeletePhotoAsync(string id);
}
