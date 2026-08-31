namespace TicketingService.Core.Entities;

/// <summary>
/// D1-1 — a helpdesk client. Local to the ticketing service for now (the create-ticket flow reads
/// this list; new clients can be typed in). Designed to be swapped for / synced with CRM Module 6's
/// CUSTOMER record later (Phase D8) — hence the CrmCustomerId back-reference field kept nullable.
/// </summary>
public class Customer : BaseEntity
{
    /// Primary display name — the client / contact name.
    public string Name { get; set; } = string.Empty;
    /// Organisation the client belongs to (nullable for individuals).
    public string? Company { get; set; }
    /// The client's own reference (e.g. their PO / account number). Free text.
    public string? ClientReference { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    /// Back-reference to CRM Module 6 once integrated (D8-1). Null while using the local list.
    public string? CrmCustomerId { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
