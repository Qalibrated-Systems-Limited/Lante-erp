using TicketingService.Core.DTOs.Tags;

namespace TicketingService.Core.Interfaces.Services;

public interface ITagService
{
    Task<IEnumerable<TagReadDto>> GetAllAsync();
    Task<TagReadDto?> GetByIdAsync(string id);
    Task<TagReadDto> CreateAsync(CreateTagDto dto, string createdByUserId);
    Task<TagReadDto> UpdateAsync(string id, UpdateTagDto dto, string updatedByUserId);
    Task<bool> DeleteAsync(string id);
    Task<IEnumerable<TagReadDto>> GetTicketTagsAsync(string ticketId);
    Task AddTagToTicketAsync(string ticketId, string tagId, string addedByUserId);
    Task RemoveTagFromTicketAsync(string ticketId, string tagId);
}
