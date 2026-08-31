namespace TicketingService.Core.Integrations;

// ─────────────────────────────────────────────────────────────────────────────
// D8 — cross-module integration seams (CRM · HR · Projects).
//
// These are OUTBOUND boundaries owned by ticketing: their shape is defined by what
// ticketing produces, NOT by the (not-yet-built) target modules' APIs. Each ships with a
// config-gated no-op default, so the call sites are wired and inert today and light up when
// the real adapter is registered. See INTEGRATIONS.md for the full picture, including the two
// deferred READ integrations (D8-1 CRM customer read, D8-4 Projects/FSR resolve) that are NOT
// built here on purpose — they need the target modules' real read contracts.
// ─────────────────────────────────────────────────────────────────────────────

/// D8-2 payload — a customer-facing interaction to log in CRM (written when a ticket closes).
/// CustomerName lets CRM resolve the client best-effort (ticketing customers aren't CRM-linked yet, D8-1).
public record CustomerInteraction(
    string TenantSchema,
    string? CustomerId,
    string TicketId,
    string TicketReference,
    string InteractionType,   // e.g. "TicketClosed", "ComplaintClosed"
    string Summary,
    DateTime OccurredAt,
    string? HandledByUserId,
    string? CustomerName = null);

/// D8-3 payload — a resolved complaint to record in the CRM CLIENT_COMPLAINT register (CRM-039),
/// feeding the monthly MD report. Written when the D5 complaint workflow closes a complaint.
public record ComplaintClosure(
    string TenantSchema,
    string? CustomerId,
    string TicketId,
    string TicketReference,
    string? RootCause,
    string? PreventiveAction,
    bool? SatisfactionMet,
    DateTime ClosedAt,
    string? CustomerName = null);

/// D8-5 payload — an employee resolved from the HR directory (Module 3) for an IT ticket.
public record EmployeeRef(
    string EmployeeId,
    string? Name,
    string? BranchId,
    string? Department);

/// D8-2 / D8-3 — the CRM (Module 6) integration seam. Default impl is a config-gated no-op; a real
/// adapter posts to the CRM service. Never throws — integration failures must not break ticketing.
public interface ICrmSync
{
    bool IsEnabled { get; }
    Task RecordCustomerInteractionAsync(CustomerInteraction interaction, CancellationToken ct = default);
    Task RecordComplaintClosureAsync(ComplaintClosure closure, CancellationToken ct = default);
}

/// D8-5 — the HR employee directory (Module 3) seam. Default impl is a config-gated no-op returning
/// null; a real adapter looks the employee up in HR. Used to validate/enrich IT-ticket employees.
public interface IEmployeeDirectory
{
    bool IsEnabled { get; }
    Task<EmployeeRef?> ResolveAsync(string employeeId, CancellationToken ct = default);
}

/// D8-1 payload — a customer read from the CRM (Module 6) customer master.
public record CrmCustomerRef(
    string Id,
    string Name,
    string? Email,
    string? Phone,
    string? ClientReference = null);

/// D8-1 — READ seam to the CRM customer master (Module 6). Lets the ticket-create picker draw from the
/// org-wide CUSTOMER register instead of only the local list, and lets a picked client be stamped with
/// its CrmCustomerId so the D8-2/D8-3 write-backs match by id. Config-gated no-op (empty/null) until the
/// real adapter is registered. See INTEGRATIONS.md.
public interface ICrmCustomerDirectory
{
    bool IsEnabled { get; }
    Task<IReadOnlyList<CrmCustomerRef>> SearchAsync(string? query, CancellationToken ct = default);
    Task<CrmCustomerRef?> GetByIdAsync(string crmCustomerId, CancellationToken ct = default);

    /// <summary>
    /// D8-1b — resolve the CRM customer id for an email address, so a client typed straight into
    /// the helpdesk still ends up anchored to the org-wide CUSTOMER register rather than living as
    /// an unlinked local row.
    ///
    /// Email is the key because it is unique and stable. Implementations must return null unless
    /// exactly one CRM customer carries that exact address — a wrong anchor is silent and sticky,
    /// and it would misdirect the D8-2/D8-3 write-backs to another client's record.
    /// </summary>
    /// <param name="schema">
    /// Tenant schema to scope the CRM read to. Required from the background anchoring pass: it has
    /// no HTTP request, so an implementation that forwards the schema header from HttpContext would
    /// send none and CRM would answer from the wrong schema. Null means "use the current request".
    /// </param>
    Task<string?> ResolveIdByEmailAsync(string email, string? schema = null, CancellationToken ct = default);
}
