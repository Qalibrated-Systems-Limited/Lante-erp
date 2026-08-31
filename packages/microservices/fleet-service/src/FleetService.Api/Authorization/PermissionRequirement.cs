using Microsoft.AspNetCore.Authorization;

namespace FleetService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
