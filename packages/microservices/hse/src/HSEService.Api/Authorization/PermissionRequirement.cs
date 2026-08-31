using Microsoft.AspNetCore.Authorization;

namespace HSEService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
