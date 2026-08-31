using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class TenantEmailSettingsRepository(LanteUserServiceDbContext context)
    : GenericRepository<TenantEmailSettings>(context), ITenantEmailSettingsRepository
{
    public async Task<TenantEmailSettings?> GetCurrentAsync()
    {
        return await Context.TenantEmailSettings
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }
}
