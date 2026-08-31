using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface ITenantEmailSettingsRepository : IGenericRepository<TenantEmailSettings>
{
    Task<TenantEmailSettings?> GetCurrentAsync();
}
