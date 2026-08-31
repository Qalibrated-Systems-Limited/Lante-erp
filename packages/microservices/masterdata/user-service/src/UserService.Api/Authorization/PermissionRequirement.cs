using Microsoft.AspNetCore.Authorization;

namespace UserService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
