using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class UserRoleRepository(LanteUserServiceDbContext context)
    : GenericRepository<UserRole>(context), IUserRoleRepository
{
    public async Task<UserRole?> GetByUserAndRoleAsync(string userId, string roleId)
    {
        return await Context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && !ur.IsDeleted);
    }

    public async Task<IEnumerable<UserRole>> GetByUserIdAsync(string userId)
    {
        return await Context.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId && !ur.IsDeleted)
            .ToListAsync();
    }
}
