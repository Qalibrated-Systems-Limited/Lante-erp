using Microsoft.AspNetCore.Authorization;

namespace ReportingService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
