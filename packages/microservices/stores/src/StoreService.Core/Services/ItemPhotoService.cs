using Microsoft.EntityFrameworkCore;
using StoreService.Core.Entities;
using StoreService.Core.Interfaces.Repositories;
using StoreService.Core.Interfaces.Services;

namespace StoreService.Core.Services;

public class ItemPhotoService(IGenericRepository<ItemPhoto> repository) : IItemPhotoService
{
    public async Task<IEnumerable<ItemPhoto>> GetByItemIdAsync(string itemId)
        => await repository.Query().Where(p => p.ItemId == itemId).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public Task<ItemPhoto?> GetByIdAsync(string id) => repository.GetByIdAsync(id);

    public Task<ItemPhoto> AddPhotoAsync(string itemId, string url, string? caption)
        => repository.CreateAsync(new ItemPhoto
        {
            ItemId = itemId,
            PhotoUrl = url,
            Caption = caption
        });

    public async Task<string?> DeletePhotoAsync(string id)
    {
        var photo = await repository.GetByIdAsync(id);
        if (photo == null) return null;
        var url = photo.PhotoUrl;
        await repository.DeleteAsync(photo);
        return url;
    }
}
