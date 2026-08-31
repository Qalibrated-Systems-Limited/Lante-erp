using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class TagRepository(TicketingDbContext context) : GenericRepository<Tag>(context), ITagRepository
{
    public Task<Tag?> GetByNameAsync(string name) =>
        DbSet.FirstOrDefaultAsync(t => t.Name.ToLower() == name.ToLower());

    public async Task<IEnumerable<TicketTag>> GetTicketTagsAsync(string ticketId) =>
        await Context.Set<TicketTag>()
            .Include(tt => tt.Tag)
            .Where(tt => tt.TicketId == ticketId && !tt.IsDeleted)
            .ToListAsync();

    public Task<TicketTag?> GetTicketTagAsync(string ticketId, string tagId) =>
        Context.Set<TicketTag>()
            .FirstOrDefaultAsync(tt => tt.TicketId == ticketId && tt.TagId == tagId && !tt.IsDeleted);

    public async Task<TicketTag> AddTagToTicketAsync(TicketTag ticketTag)
    {
        await Context.Set<TicketTag>().AddAsync(ticketTag);
        await Context.SaveChangesAsync();
        return ticketTag;
    }

    public async Task RemoveTagFromTicketAsync(string ticketId, string tagId)
    {
        var ticketTag = await Context.Set<TicketTag>()
            .FirstOrDefaultAsync(tt => tt.TicketId == ticketId && tt.TagId == tagId && !tt.IsDeleted);

        if (ticketTag == null) return;
        ticketTag.IsDeleted = true;
        ticketTag.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();
    }
}
