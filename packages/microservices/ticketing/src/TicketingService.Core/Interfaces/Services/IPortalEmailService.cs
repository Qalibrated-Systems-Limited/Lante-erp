namespace TicketingService.Core.Interfaces.Services;

public interface IPortalEmailService
{
    Task SendSubmissionConfirmationAsync(
        string toName,
        string toEmail,
        string subject,
        string type,
        string reference,
        string message,
        CancellationToken cancellationToken = default);

    /// D4-1 — auto-acknowledge a newly created ticket to its requester (ticket ref, category, SLA due).
    Task SendAcknowledgementAsync(
        string toName,
        string toEmail,
        string reference,
        string category,
        DateTime? resolutionDueAt,
        CancellationToken cancellationToken = default);

    /// #1 — warn a portal requester that their ticket will auto-close soon unless they respond.
    Task SendAutoCloseWarningAsync(
        string toEmail,
        string subject,
        string reference,
        int hoursUntilClose,
        CancellationToken cancellationToken = default);

    Task SendServiceRequestOtpAsync(
        string toName,
        string toEmail,
        string otpCode,
        string formTypeLabel,
        CancellationToken cancellationToken = default);

    Task SendServiceRequestConfirmationAsync(
        string toName,
        string toEmail,
        string referenceNumber,
        string formTypeLabel,
        int    instrumentCount,
        string ticketReference,
        CancellationToken cancellationToken = default);

    Task SendQuotationAsync(
        string toName,
        string toEmail,
        string referenceNumber,
        string quotationNumber,
        string formTypeLabel,
        IEnumerable<(string Description, decimal Quantity, decimal UnitPrice, decimal Amount)> lineItems,
        decimal subtotal,
        decimal vatRate,
        decimal vatAmount,
        decimal totalAmount,
        DateTime? validUntil,
        string? notes,
        string? clientOrganization  = null,
        string? clientAddress       = null,
        string? description         = null,
        CancellationToken cancellationToken = default);

    /// <summary>Generic, multi-recipient send — backs the cross-service internal/email endpoint
    /// (e.g. ReportingService's scheduled report delivery) where the caller supplies its own
    /// fully-formed subject/HTML rather than a fixed portal-specific template.</summary>
    Task SendAsync(
        IEnumerable<string> toEmails,
        string subject,
        string bodyHtml,
        CancellationToken cancellationToken = default);
}
