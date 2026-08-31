using Microsoft.EntityFrameworkCore;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class TicketRepository(TicketingDbContext context)
    : GenericRepository<Ticket>(context), ITicketRepository
{
    public async Task<PaginatedResult<Ticket>> GetPagedAsync(TicketFilterParameters parameters, string? currentUserId = null)
    {
        var query = Context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Customer)
            .Include(t => t.SatisfactionRating)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.ToLower();
            query = query.Where(t =>
                t.Title.ToLower().Contains(search) ||
                t.Description.ToLower().Contains(search));
        }

        if (parameters.Status.HasValue)
            query = query.Where(t => t.Status == parameters.Status.Value);

        if (parameters.Priority.HasValue)
            query = query.Where(t => t.Priority == parameters.Priority.Value);

        if (!string.IsNullOrEmpty(parameters.CategoryId))
            query = query.Where(t => t.CategoryId == parameters.CategoryId);

        if (parameters.DepartmentIds is { Count: > 0 })
            query = query.Where(t => t.DepartmentId != null && parameters.DepartmentIds.Contains(t.DepartmentId));
        else if (!string.IsNullOrEmpty(parameters.DepartmentId))
            query = query.Where(t => t.DepartmentId == parameters.DepartmentId);

        if (!string.IsNullOrEmpty(parameters.AssignedToUserId))
            query = query.Where(t => t.AssignedToUserId == parameters.AssignedToUserId);

        if (parameters.AssignedToMe == true && !string.IsNullOrEmpty(currentUserId))
            query = query.Where(t => t.AssignedToUserId == currentUserId);

        if (parameters.CreatedByMe == true && !string.IsNullOrEmpty(currentUserId))
            query = query.Where(t => t.CreatedByUserId == currentUserId);

        query = parameters.SortDescending
            ? query.OrderByDescending(t => t.CreatedAt)
            : query.OrderBy(t => t.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<Ticket>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }

    public async Task<Ticket?> GetByIdWithDetailsAsync(string id)
    {
        return await Context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Customer)
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .Include(t => t.History.OrderByDescending(h => h.OccurredAt))
            .Include(t => t.Assignments)
            .Include(t => t.Escalations)
            .Include(t => t.Watchers)
            .Include(t => t.SatisfactionRating)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Ticket>> GetOverdueSLATicketsAsync()
    {
        var now = DateTime.UtcNow;
        return await Context.Tickets
            .Where(t =>
                t.Status != TicketStatus.Resolved &&
                t.Status != TicketStatus.Closed &&
                t.Status != TicketStatus.Pending &&   // #4: SLA clock paused while waiting on customer
                // #3: response overdue only counts if no first response has been logged
                ((t.ResponseDueAt.HasValue && t.ResponseDueAt.Value < now && t.FirstResponseAt == null) ||
                 (t.ResolutionDueAt.HasValue && t.ResolutionDueAt.Value < now)))
            .ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetTicketsByUserAsync(string userId)
    {
        return await Context.Tickets
            .Include(t => t.Category)
            .Where(t => t.AssignedToUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetTicketsCreatedByUserAsync(string userId)
    {
        return await Context.Tickets
            .Include(t => t.Category)
            .Where(t => t.CreatedByUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetChildrenAsync(string parentId)
    {
        return await Context.Tickets
            .Include(t => t.Category)
            .Where(t => t.ParentTicketId == parentId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> HasRecentSimilarAsync(string? customerId, string? requesterEmail, string categoryId, DateTime since)
    {
        var hasCustomer = !string.IsNullOrEmpty(customerId);
        var hasEmail = !string.IsNullOrEmpty(requesterEmail);
        if (!hasCustomer && !hasEmail) return false;

        return await Context.Tickets.AnyAsync(t =>
            t.CategoryId == categoryId
            && t.CreatedAt >= since
            && ((hasCustomer && t.CustomerId == customerId)
                || (hasEmail && t.RequesterEmail == requesterEmail)));
    }

    public async Task<int> CountSurveysSentAsync(DateTime? from, DateTime? to)
    {
        var query = Context.Tickets.Where(t => t.SurveySentAt != null);
        if (from.HasValue) query = query.Where(t => t.SurveySentAt >= from.Value);
        if (to.HasValue) query = query.Where(t => t.SurveySentAt <= to.Value);
        return await query.CountAsync();
    }

    public async Task<int> GetNextTicketNumberAsync()
    {
        // Ignore the soft-delete query filter so a deleted ticket's number is never re-issued.
        var max = await Context.Tickets
            .IgnoreQueryFilters()
            .MaxAsync(t => (int?)t.TicketNumber) ?? 0;
        return max + 1;
    }

    public async Task<Ticket?> GetByReferenceAsync(string reference)
    {
        reference = (reference ?? string.Empty).Trim();
        if (reference.Length == 0) return null;

        // New sequential form: TKT-000123 → resolve by the numeric part.
        if (reference.StartsWith("TKT", StringComparison.OrdinalIgnoreCase))
        {
            var digits = new string(reference.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var num) && num > 0)
            {
                var byNumber = await Context.Tickets
                    .Include(t => t.Category)
                    .Include(t => t.Customer)
                    .FirstOrDefaultAsync(t => t.TicketNumber == num);
                if (byNumber != null) return byNumber;
            }
        }

        // Legacy GUID-prefix form (references issued before D1-4).
        var lower = reference.ToLowerInvariant();
        return await Context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Id.ToLower().StartsWith(lower));
    }
}
