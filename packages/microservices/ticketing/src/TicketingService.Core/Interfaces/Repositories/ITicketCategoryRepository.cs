using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface ITicketCategoryRepository : IGenericRepository<TicketCategory>
{
    Task<IEnumerable<TicketCategory>> GetByDepartmentAsync(string departmentId);
    Task<TicketCategory?> GetByNameAsync(string name);
}
