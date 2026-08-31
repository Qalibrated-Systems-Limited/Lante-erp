using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class CustomerRepository(TicketingDbContext context) : GenericRepository<Customer>(context), ICustomerRepository
{
    public async Task<IEnumerable<Customer>> SearchAsync(string? query, bool activeOnly = true)
    {
        var q = DbSet.AsQueryable();
        if (activeOnly) q = q.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Company != null && c.Company.ToLower().Contains(term)) ||
                (c.ClientReference != null && c.ClientReference.ToLower().Contains(term)) ||
                (c.Email != null && c.Email.ToLower().Contains(term)));
        }
        return await q.OrderBy(c => c.Name).Take(50).ToListAsync();
    }

    public Task<Customer?> FindByNameAsync(string name) =>
        DbSet.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

    public Task<Customer?> FindByCrmCustomerIdAsync(string crmCustomerId) =>
        DbSet.FirstOrDefaultAsync(c => c.CrmCustomerId == crmCustomerId);
}
