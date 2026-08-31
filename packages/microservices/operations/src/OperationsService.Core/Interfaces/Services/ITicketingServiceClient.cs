namespace OperationsService.Core.Interfaces.Services;

public interface ITicketingServiceClient
{
    Task NotifyWorkUpdateAsync(string ticketId, string assignmentId, string updateType, string? notes, string? bearerToken);
    Task RecordCertificateOnSrAsync(string serviceRequestId, string certificateNumber, DateTime issuedAt, string? bearerToken);

    /// <summary>
    /// O5.3 — reverse status sync. When an SR advances in operations (quotation sent/approved,
    /// dispatched, completed, rejected) notify the linked helpdesk ticket so support sees progress.
    /// Best-effort; failures are logged, never thrown.
    /// </summary>
    Task NotifyServiceRequestStatusAsync(string ticketId, string referenceNumber, string status, string? note, string? bearerToken);
}
