using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface ITicketRepository : IGenericRepository<Ticket>
{
    Task<PaginatedResult<Ticket>> GetPagedAsync(TicketFilterParameters parameters, string? currentUserId = null);
    Task<Ticket?> GetByIdWithDetailsAsync(string id);
    Task<IEnumerable<Ticket>> GetOverdueSLATicketsAsync();
    Task<IEnumerable<Ticket>> GetTicketsByUserAsync(string userId);
    Task<IEnumerable<Ticket>> GetTicketsCreatedByUserAsync(string userId);

    /// D1-4 — next per-tenant sequential ticket number (current max + 1, 1-based).
    Task<int> GetNextTicketNumberAsync();

    /// D3-1 — direct children of a parent ticket (ParentTicketId == parentId).
    Task<IEnumerable<Ticket>> GetChildrenAsync(string parentId);

    /// D4-3 — is there another ticket from the same client (by customer or requester email) in the
    /// same category created since <paramref name="since"/>? Used to flag 30-day repeat contacts.
    Task<bool> HasRecentSimilarAsync(string? customerId, string? requesterEmail, string categoryId, DateTime since);

    /// D6-1 — count tickets whose satisfaction survey was sent within the range (the denominator for
    /// survey response rate).
    Task<int> CountSurveysSentAsync(DateTime? from, DateTime? to);

    /// D1-4 — resolve a ticket by its human-facing reference: the sequential TKT-000123 form, or the
    /// legacy GUID-prefix form for references issued before D1-4. Null if not found.
    Task<Ticket?> GetByReferenceAsync(string reference);
}
