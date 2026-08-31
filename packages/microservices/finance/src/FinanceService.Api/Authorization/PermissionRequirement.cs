using Microsoft.AspNetCore.Authorization;

namespace FinanceService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
