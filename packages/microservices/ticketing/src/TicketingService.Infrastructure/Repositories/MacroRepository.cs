using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class MacroRepository(TicketingDbContext context) : GenericRepository<Macro>(context), IMacroRepository
{
    public async Task<IEnumerable<Macro>> GetByCategoryAsync(string categoryId) =>
        await DbSet.Where(m => m.CategoryId == categoryId).ToListAsync();

    public async Task<IEnumerable<Macro>> GetGlobalAsync() =>
        await DbSet.Where(m => m.IsGlobal).ToListAsync();
}
