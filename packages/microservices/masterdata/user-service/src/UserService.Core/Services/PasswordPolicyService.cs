using System.ComponentModel.DataAnnotations;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;

namespace UserService.Core.Services;

public class PasswordPolicyService(IPasswordPolicyRepository passwordPolicyRepository)
{
    public async Task<PasswordPolicy?> GetCurrentPolicyAsync()
    {
        return await passwordPolicyRepository.GetCurrentPolicyAsync();
    }

    public void ValidatePassword(string password, PasswordPolicy policy)
    {
        if (password.Length < policy.MinimumLength)
            throw new ValidationException($"Password must be at least {policy.MinimumLength} characters long.");

        if (policy.RequireUppercase && !password.Any(char.IsUpper))
            throw new ValidationException("Password must contain at least one uppercase letter.");

        if (policy.RequireLowercase && !password.Any(char.IsLower))
            throw new ValidationException("Password must contain at least one lowercase letter.");

        if (policy.RequireDigit && !password.Any(char.IsDigit))
            throw new ValidationException("Password must contain at least one digit.");

        if (policy.RequireSpecialCharacter && !password.Any(c => !char.IsLetterOrDigit(c)))
            throw new ValidationException("Password must contain at least one special character.");
    }
}
