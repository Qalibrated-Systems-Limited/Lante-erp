using Microsoft.AspNetCore.Authorization;

namespace ComplianceService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
