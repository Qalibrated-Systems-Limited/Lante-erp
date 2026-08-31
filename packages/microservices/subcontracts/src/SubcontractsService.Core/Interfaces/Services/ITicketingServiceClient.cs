namespace SubcontractsService.Core.Interfaces.Services;

public interface ITicketingServiceClient
{
    Task CreateAlertAsync(string tenantSchema, string source, string severity, string title, string message, string? assignedToUserId = null, string? requiredPermission = null);
}
