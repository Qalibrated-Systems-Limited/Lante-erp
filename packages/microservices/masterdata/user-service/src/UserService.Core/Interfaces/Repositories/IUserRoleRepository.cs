using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface IUserRoleRepository : IGenericRepository<UserRole>
{
    Task<UserRole?> GetByUserAndRoleAsync(string userId, string roleId);
    Task<IEnumerable<UserRole>> GetByUserIdAsync(string userId);
}
