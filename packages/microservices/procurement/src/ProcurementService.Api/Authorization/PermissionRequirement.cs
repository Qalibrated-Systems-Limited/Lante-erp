using Microsoft.AspNetCore.Authorization;

namespace ProcurementService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
