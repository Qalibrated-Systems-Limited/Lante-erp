using Microsoft.AspNetCore.Authorization;

namespace StoreService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
