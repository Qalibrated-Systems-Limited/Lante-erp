using Microsoft.AspNetCore.Authorization;

namespace SubcontractsService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
