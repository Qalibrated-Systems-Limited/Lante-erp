using UserService.Core.DTOs.Common;
using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface IRoleRepository : IGenericRepository<Role>
{
    Task<Role?> GetByNameAsync(string name);
    Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(string roleId);
    Task<PaginatedResult<Role>> GetDeletedPagedAsync(PaginationParameters parameters);
}
