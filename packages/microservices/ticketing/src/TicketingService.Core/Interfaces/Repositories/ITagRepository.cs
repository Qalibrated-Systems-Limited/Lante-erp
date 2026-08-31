using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface ITagRepository : IGenericRepository<Tag>
{
    Task<Tag?> GetByNameAsync(string name);
    Task<IEnumerable<TicketTag>> GetTicketTagsAsync(string ticketId);
    Task<TicketTag?> GetTicketTagAsync(string ticketId, string tagId);
    Task<TicketTag> AddTagToTicketAsync(TicketTag ticketTag);
    Task RemoveTagFromTicketAsync(string ticketId, string tagId);
}
