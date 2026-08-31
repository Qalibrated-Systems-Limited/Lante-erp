using TicketingService.Core.Enums;

namespace TicketingService.Core.DTOs.Tickets;

public class CreateTicketDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public TicketSource Source { get; set; } = TicketSource.Manual;
    public string DepartmentId { get; set; } = string.Empty;
    public LinkedEntityType LinkedEntityType { get; set; } = LinkedEntityType.None;
    public string? LinkedEntityId { get; set; }
    public DateTime? DueDate { get; set; }
    /// #1 — external requester's email (portal submissions) for the pre-auto-close warning.
    public string? RequesterEmail { get; set; }
    /// D1-2/D1-3 — client the ticket belongs to. Either an existing customer id, or leave it null and
    /// pass ClientName (+ optional company/reference) to resolve-or-create the client on the fly.
    public string? CustomerId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientCompany { get; set; }
    public string? ClientReference { get; set; }
    /// D8-1 — a CRM customer master id picked in the create-ticket combobox. Wins over CustomerId /
    /// ClientName: resolves-or-creates a local client stamped with this CrmCustomerId so the ticket
    /// links to the org-wide CRM customer (and D8-2/D8-3 write-backs match by id).
    public string? CrmCustomerId { get; set; }
    /// D7-1 — internal IT-helpdesk context (for IT-category tickets).
    public string? EmployeeId { get; set; }
    public string? BranchId { get; set; }
    public string? SystemAffected { get; set; }
}

// #19 — merge a duplicate ticket into a surviving one.
public class MergeTicketDto
{
    public string IntoTicketId { get; set; } = string.Empty;
}
