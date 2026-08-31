using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface ISystemSettingRepository : IGenericRepository<SystemSetting>
{
    Task<SystemSetting?> GetByKeyAsync(string key);
}
