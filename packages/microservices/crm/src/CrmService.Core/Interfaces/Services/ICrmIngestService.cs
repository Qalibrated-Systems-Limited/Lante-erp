using CrmService.Core.DTOs.Integrations;

namespace CrmService.Core.Interfaces.Services;

/// <summary>D8-2 / D8-3 — inbound seam from the ticketing service (Module 7). Lands closed-ticket customer
/// interactions and resolved complaints into the CRM CUSTOMER_INTERACTION / CLIENT_COMPLAINT registers,
/// resolving the CRM customer best-effort by id then display name (ticketing customers are not yet
/// CRM-linked — D8-1). Never assumes a match; unmatched records are reported but not dropped where the
/// register can stand alone.</summary>
public interface ICrmIngestService
{
    Task<CrmIngestResult> RecordCustomerInteractionAsync(CustomerInteractionIngestDto dto, string userId);
    Task<CrmIngestResult> RecordComplaintClosureAsync(ComplaintClosureIngestDto dto, string userId);
}
