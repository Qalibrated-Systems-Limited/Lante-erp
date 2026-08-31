using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface IPasswordPolicyRepository : IGenericRepository<PasswordPolicy>
{
    Task<PasswordPolicy?> GetCurrentPolicyAsync();
}
