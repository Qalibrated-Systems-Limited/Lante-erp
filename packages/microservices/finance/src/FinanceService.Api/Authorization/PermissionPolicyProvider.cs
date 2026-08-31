using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FinanceService.Api.Authorization;

/// <summary>
/// Turns any <c>[Authorize(Policy = "finance.write")]</c> into a permission requirement, so policies do
/// not have to be registered one by one. Same shape as the other twelve services.
/// </summary>
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = new AuthorizationPolicyBuilder();
        policy.AddRequirements(new PermissionRequirement(policyName));
        return Task.FromResult(policy.Build())!;
    }
}
