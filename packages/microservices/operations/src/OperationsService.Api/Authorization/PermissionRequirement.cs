using Microsoft.AspNetCore.Authorization;

namespace OperationsService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
