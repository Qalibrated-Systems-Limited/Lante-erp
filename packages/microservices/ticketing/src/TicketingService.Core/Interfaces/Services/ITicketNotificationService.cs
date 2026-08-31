namespace TicketingService.Core.Interfaces.Services;

public interface ITicketNotificationService
{
    Task SendDepartmentAssignmentNotificationsAsync(
        string ticketId,
        string ticketTitle,
        string departmentId,
        string assignedByUserId,
        string? notes,
        CancellationToken cancellationToken = default);
}
