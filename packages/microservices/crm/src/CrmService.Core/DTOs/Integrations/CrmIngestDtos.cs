namespace CrmService.Core.DTOs.Integrations;

// D8-2 / D8-3 — inbound payloads from the ticketing service (Module 7). Shapes mirror ticketing's
// outbound integration contracts. Customer identity is best-effort: ticketing sends its own customer id
// (rarely CRM-linked yet) plus the display name, so CRM resolves by id then falls back to name.

/// <summary>D8-2 — a closed-ticket customer touchpoint to log against the CRM customer.</summary>
public class CustomerInteractionIngestDto
{
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string TicketReference { get; set; } = string.Empty;
    public string InteractionType { get; set; } = string.Empty;   // "TicketClosed" | "ComplaintClosed"
    public string Summary { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? HandledByUserId { get; set; }
}

/// <summary>D8-3 — a resolved helpdesk complaint to record in the CRM CLIENT_COMPLAINT register.</summary>
public class ComplaintClosureIngestDto
{
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string TicketReference { get; set; } = string.Empty;
    public string? RootCause { get; set; }
    public string? PreventiveAction { get; set; }
    public bool? SatisfactionMet { get; set; }
    public DateTime ClosedAt { get; set; }
}

public record CrmIngestResult(bool Matched, string Message);
