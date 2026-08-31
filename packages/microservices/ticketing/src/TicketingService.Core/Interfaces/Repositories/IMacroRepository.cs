using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface IMacroRepository : IGenericRepository<Macro>
{
    Task<IEnumerable<Macro>> GetByCategoryAsync(string categoryId);
    Task<IEnumerable<Macro>> GetGlobalAsync();
}
