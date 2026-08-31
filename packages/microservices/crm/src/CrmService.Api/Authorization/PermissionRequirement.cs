using Microsoft.AspNetCore.Authorization;

namespace CrmService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
