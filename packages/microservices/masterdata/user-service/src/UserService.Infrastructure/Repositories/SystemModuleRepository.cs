using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class SystemModuleRepository(LanteUserServiceDbContext context)
    : GenericRepository<SystemModule>(context), ISystemModuleRepository
{
    public async Task<SystemModule?> GetByKeyAsync(string moduleKey)
    {
        return await Context.SystemModules.FirstOrDefaultAsync(m => m.ModuleKey == moduleKey);
    }
}
