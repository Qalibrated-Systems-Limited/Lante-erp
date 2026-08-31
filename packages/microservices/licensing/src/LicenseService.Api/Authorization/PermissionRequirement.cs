using Microsoft.AspNetCore.Authorization;

namespace LicenseService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
