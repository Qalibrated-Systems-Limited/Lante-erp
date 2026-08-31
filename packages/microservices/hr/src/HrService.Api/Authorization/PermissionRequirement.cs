using Microsoft.AspNetCore.Authorization;

namespace HrService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
