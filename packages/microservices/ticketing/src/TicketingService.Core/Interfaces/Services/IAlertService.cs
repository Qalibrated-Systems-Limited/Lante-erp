using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Services;

public interface IAlertService
{
    Task<IEnumerable<Alert>> GetOpenAsync(string tenantId);
    Task<IEnumerable<Alert>> GetHistoryAsync(string tenantId);
    Task<Alert?> GetByIdAsync(string tenantId, string id);
    Task CreateAsync(string tenantId, string source, string severity, string title, string message, string? ticketId = null, string? ticketTitle = null, string? assignedToUserId = null, string? requiredPermission = null);
    Task MarkSeenAsync(string tenantId, string id, string userId);
    Task AcknowledgeAsync(string tenantId, string id, string userId);
}
