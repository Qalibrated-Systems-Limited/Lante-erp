using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class SystemSettingRepository(LanteUserServiceDbContext context)
    : GenericRepository<SystemSetting>(context), ISystemSettingRepository
{
    public async Task<SystemSetting?> GetByKeyAsync(string key)
    {
        return await Context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
    }
}
