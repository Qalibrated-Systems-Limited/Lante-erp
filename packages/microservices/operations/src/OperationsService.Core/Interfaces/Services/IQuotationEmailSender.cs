using OperationsService.Core.DTOs.ServiceRequests;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// O5 — outbound quotation email seam. Operations has no email infrastructure of its own yet, so the
/// default implementation is a config-gated logging stub (mirrors the ticketing PortalEmailService
/// contract). A real SMTP/provider implementation can be swapped in later without touching the SR
/// pipeline. Kept behind an interface so ServiceRequestService stays in Core.
/// </summary>
public interface IQuotationEmailSender
{
    Task SendQuotationAsync(QuotationEmailModel model);
}

public class QuotationEmailModel
{
    public string ToName             { get; set; } = string.Empty;
    public string ToEmail            { get; set; } = string.Empty;
    public string ReferenceNumber    { get; set; } = string.Empty;
    public string QuotationNumber    { get; set; } = string.Empty;
    public string FormTypeLabel      { get; set; } = string.Empty;
    public List<QuotationLineItemDto> LineItems { get; set; } = new();
    public decimal Subtotal          { get; set; }
    public decimal VatRate           { get; set; }
    public decimal VatAmount         { get; set; }
    public decimal TotalAmount       { get; set; }
    public DateTime? ValidUntil      { get; set; }
    public string? Notes             { get; set; }
    public string? ClientOrganization { get; set; }
    public string? ClientAddress     { get; set; }
    public string? Description       { get; set; }
}
