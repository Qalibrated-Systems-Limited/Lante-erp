using TicketingService.Core.DTOs.SLA;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

public class SLAService(
    ISLAPolicyRepository slaPolicyRepository,
    ITicketRepository ticketRepository,
    ITicketCategoryRepository categoryRepository,
    BusinessCalendar calendar) : ISLAService
{
    public async Task<SLADeadlines> CalculateSLADeadlinesAsync(string categoryId, TicketPriority priority, DateTime createdAt)
    {
        var policy = await slaPolicyRepository.GetByCategoryAndPriorityAsync(categoryId, priority);
        if (policy == null)
            return new SLADeadlines();

        // #16 / D1-5: business-hours categories skip nights/weekends; 24/7 categories (Emergency,
        // IT-P1) count raw wall-clock hours so their clock never pauses.
        var category = await categoryRepository.GetByIdAsync(categoryId);
        var businessHoursOnly = category?.BusinessHoursOnly ?? true;

        DateTime Add(int hours) => businessHoursOnly
            ? calendar.AddWorkingHours(createdAt, hours)
            : createdAt.AddHours(hours);

        return new SLADeadlines
        {
            ResponseDueAt = Add(policy.ResponseTimeHours),
            ResolutionDueAt = Add(policy.ResolutionTimeHours)
        };
    }

    public async Task<IEnumerable<Ticket>> CheckSLABreachesAsync()
    {
        return await ticketRepository.GetOverdueSLATicketsAsync();
    }

    // D2-2 — amber measures elapsed against the resolution window on the same wall-clock basis as
    // breach (ResolutionDueAt already carries the business-hours + pause extensions). A ticket is
    // amber once now has passed CreatedAt + threshold%×(ResolutionDueAt − CreatedAt) but before the
    // deadline itself. Threshold comes from the matching SLA policy (default 75%).
    public async Task<IEnumerable<Ticket>> GetAmberTicketsAsync()
    {
        var now = DateTime.UtcNow;
        var candidates = (await ticketRepository.GetAllAsync())
            .Where(t => t.ResolutionDueAt.HasValue
                && t.ResolutionDueAt.Value > now                 // not yet breached
                && t.Status != TicketStatus.Resolved
                && t.Status != TicketStatus.Closed
                && t.Status != TicketStatus.Pending)             // clock paused while waiting on customer
            .ToList();

        var amber = new List<Ticket>();
        foreach (var t in candidates)
        {
            var policy = await slaPolicyRepository.GetByCategoryAndPriorityAsync(t.CategoryId, t.Priority);
            var pct = policy is { AmberThresholdPct: > 0 and < 100 } ? policy.AmberThresholdPct : 75;
            var windowMinutes = (t.ResolutionDueAt!.Value - t.CreatedAt).TotalMinutes;
            if (windowMinutes <= 0) continue;
            var amberAt = t.CreatedAt.AddMinutes(windowMinutes * pct / 100.0);
            if (now >= amberAt) amber.Add(t);
        }
        return amber;
    }

    // D2-4 — unassigned longer than `minutes` of ACTIVE time (wall-clock minus paused hours), still
    // in an open, non-paused state. New/Assigned-with-no-owner tickets qualify.
    public async Task<IEnumerable<Ticket>> GetUnassignedTicketsAsync(int minutes)
    {
        var now = DateTime.UtcNow;
        return (await ticketRepository.GetAllAsync())
            .Where(t => string.IsNullOrEmpty(t.AssignedToUserId)
                && t.Status != TicketStatus.Resolved
                && t.Status != TicketStatus.Closed
                && t.Status != TicketStatus.Pending
                && ((now - t.CreatedAt).TotalMinutes - t.SlaPausedHours * 60) >= minutes)
            .ToList();
    }

    public async Task<SLASummaryDto> GetSLASummaryAsync(string? departmentId = null)
    {
        var overdueTickets = await ticketRepository.GetOverdueSLATicketsAsync();
        var overdueList = overdueTickets
            .Where(t => departmentId == null || t.DepartmentId == departmentId)
            .ToList();

        var allActiveTickets = (await ticketRepository.GetAllAsync())
            .Where(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved)
            .Where(t => departmentId == null || t.DepartmentId == departmentId)
            .ToList();

        int responseBreached = allActiveTickets.Count(IsResponseBreached);
        int resolutionBreached = allActiveTickets.Count(IsResolutionBreached);

        return new SLASummaryDto
        {
            TotalTickets = allActiveTickets.Count,
            ResponseBreached = responseBreached,
            ResolutionBreached = resolutionBreached,
            OnTrack = allActiveTickets.Count - Math.Max(responseBreached, resolutionBreached),
            ComplianceRate = allActiveTickets.Count > 0
                ? Math.Round((double)(allActiveTickets.Count - overdueList.Count) / allActiveTickets.Count * 100, 2)
                : 100
        };
    }

    // #3: response is breached when it's past due and no first response has been logged yet —
    // NOT when Status == New (auto-assign leaves New instantly, so status was never a valid proxy).
    // #4: Pending is excluded because the clock is paused while waiting on the customer.
    public bool IsResponseBreached(Ticket ticket) =>
        ticket.ResponseDueAt.HasValue
        && ticket.FirstResponseAt == null
        && ticket.ResponseDueAt.Value < DateTime.UtcNow
        && ticket.Status != TicketStatus.Pending
        && ticket.Status != TicketStatus.Resolved
        && ticket.Status != TicketStatus.Closed;

    public bool IsResolutionBreached(Ticket ticket) =>
        ticket.ResolutionDueAt.HasValue
        && ticket.ResolutionDueAt.Value < DateTime.UtcNow
        && ticket.Status != TicketStatus.Pending   // #4: clock paused while waiting on customer
        && ticket.Status != TicketStatus.Resolved
        && ticket.Status != TicketStatus.Closed;

    public async Task<IEnumerable<SLAPolicyReadDto>> GetPoliciesByCategoryAsync(string categoryId)
    {
        var all = await slaPolicyRepository.GetAllAsync();
        return all
            .Where(p => p.CategoryId == categoryId)
            .Select(p => new SLAPolicyReadDto
            {
                Id                  = p.Id,
                CategoryId          = p.CategoryId,
                Priority            = p.Priority,
                ResponseTimeHours   = p.ResponseTimeHours,
                ResolutionTimeHours = p.ResolutionTimeHours,
                AmberThresholdPct   = p.AmberThresholdPct,
                BranchId            = p.BranchId,
                CreatedAt           = p.CreatedAt,
            });
    }

    public async Task<SLAPolicyReadDto> AddPolicyAsync(CreateSLAPolicyDto dto, string createdByUserId)
    {
        var policy = new SLAPolicy
        {
            CategoryId          = dto.CategoryId,
            Priority            = dto.Priority,
            ResponseTimeHours   = dto.ResponseTimeHours,
            ResolutionTimeHours = dto.ResolutionTimeHours,
            AmberThresholdPct   = dto.AmberThresholdPct <= 0 || dto.AmberThresholdPct >= 100 ? 75 : dto.AmberThresholdPct,
            BranchId            = dto.BranchId,
            CreatedBy           = createdByUserId,
        };
        var created = await slaPolicyRepository.CreateAsync(policy);
        return new SLAPolicyReadDto
        {
            Id                  = created.Id,
            CategoryId          = created.CategoryId,
            Priority            = created.Priority,
            ResponseTimeHours   = created.ResponseTimeHours,
            ResolutionTimeHours = created.ResolutionTimeHours,
            AmberThresholdPct   = created.AmberThresholdPct,
            BranchId            = created.BranchId,
            CreatedAt           = created.CreatedAt,
        };
    }
}
