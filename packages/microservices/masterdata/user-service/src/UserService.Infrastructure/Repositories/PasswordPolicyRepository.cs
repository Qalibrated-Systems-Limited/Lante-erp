using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class PasswordPolicyRepository(LanteUserServiceDbContext context)
    : GenericRepository<PasswordPolicy>(context), IPasswordPolicyRepository
{
    public async Task<PasswordPolicy?> GetCurrentPolicyAsync()
    {
        return await Context.PasswordPolicies
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
    }
}
