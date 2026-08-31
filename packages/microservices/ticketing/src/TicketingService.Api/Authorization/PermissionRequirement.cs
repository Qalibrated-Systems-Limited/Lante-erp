using Microsoft.AspNetCore.Authorization;

namespace TicketingService.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
