using UserService.Core.DTOs.Common;
using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface IPermissionsRepository : IGenericRepository<Permission>
{
    Task<Permission?> GetByNameAsync(string name);
}
