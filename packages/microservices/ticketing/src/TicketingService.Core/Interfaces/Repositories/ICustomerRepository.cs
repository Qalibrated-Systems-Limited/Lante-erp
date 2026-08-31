using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface ICustomerRepository : IGenericRepository<Customer>
{
    Task<IEnumerable<Customer>> SearchAsync(string? query, bool activeOnly = true);
    Task<Customer?> FindByNameAsync(string name);
    Task<Customer?> FindByCrmCustomerIdAsync(string crmCustomerId);
}
