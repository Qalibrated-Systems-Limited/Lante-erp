using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface ISystemModuleRepository : IGenericRepository<SystemModule>
{
    Task<SystemModule?> GetByKeyAsync(string moduleKey);
}
