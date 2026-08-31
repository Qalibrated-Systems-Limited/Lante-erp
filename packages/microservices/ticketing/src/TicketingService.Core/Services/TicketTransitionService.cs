using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

/// <inheritdoc cref="ITicketTransitionService"/>
public class TicketTransitionService(
    ITicketRepository ticketRepository,
    ITicketHistoryService historyService) : ITicketTransitionService
{
    private static readonly Dictionary<TicketStatus, TicketStatus[]> ValidTransitions = new()
    {
        { TicketStatus.New,        [TicketStatus.Assigned, TicketStatus.Escalated] },
        { TicketStatus.Assigned,   [TicketStatus.InProgress, TicketStatus.Escalated] },
        { TicketStatus.InProgress, [TicketStatus.Pending, TicketStatus.Resolved, TicketStatus.Escalated] },
        { TicketStatus.Pending,    [TicketStatus.InProgress, TicketStatus.Resolved] },
        { TicketStatus.Escalated,  [TicketStatus.InProgress, TicketStatus.Resolved] },
        { TicketStatus.Resolved,   [TicketStatus.Closed, TicketStatus.Reopened] },
        { TicketStatus.Closed,     [TicketStatus.Reopened] },
        { TicketStatus.Reopened,   [TicketStatus.Assigned, TicketStatus.InProgress] }
    };

    public bool IsAllowed(TicketStatus from, TicketStatus to) =>
        ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public async Task<TicketStatus> TransitionAsync(
        Ticket ticket,
        TicketStatus newStatus,
        TransitionSource source,
        string actor,
        string? notes = null,
        string? historyAction = null,
        bool enforceEvidence = false)
    {
        var oldStatus = ticket.Status;

        // Manual transitions are governed by the table. Automated sources (work-update callback,
        // workflow rules, escalation, follow-up) may force — preserving prior direct-write behaviour,
        // now funnelled through this one method so stamping/history/evidence are consistent.
        if (source == TransitionSource.Manual && newStatus != oldStatus && !IsAllowed(oldStatus, newStatus))
            throw new InvalidOperationException($"Cannot transition ticket from {oldStatus} to {newStatus}.");

        // Evidence gate. Today only manual resolves set this; #2 extends it to the ops callback
        // (landing on Pending instead of throwing).
        if (enforceEvidence && newStatus == TicketStatus.Resolved
            && ticket.RequiresEvidence && !ticket.Attachments.Any())
            throw new InvalidOperationException(
                "This ticket requires evidence (attachments) before it can be resolved.");

        // D3-3: a manual resolve must record a root cause (distinct from resolution notes). Because
        // Closed is only reachable via Resolved, this also blocks closing without a root cause.
        // Automated/forced sources (ops work-update, follow-up auto-close) are exempt.
        if (source == TransitionSource.Manual && newStatus == TicketStatus.Resolved
            && string.IsNullOrWhiteSpace(ticket.RootCause))
            throw new InvalidOperationException(
                "A root cause is required before this ticket can be resolved.");

        // #4: pause the SLA clock while the ticket waits on the customer (Pending); on resume, add
        // the paused span back to the deadlines so waiting-on-client time doesn't cause a breach.
        var nowUtc = DateTime.UtcNow;
        if (newStatus == TicketStatus.Pending && oldStatus != TicketStatus.Pending)
        {
            ticket.SlaPausedAt = nowUtc;
        }
        else if (oldStatus == TicketStatus.Pending && newStatus != TicketStatus.Pending && ticket.SlaPausedAt.HasValue)
        {
            var paused = nowUtc - ticket.SlaPausedAt.Value;
            if (ticket.ResolutionDueAt.HasValue) ticket.ResolutionDueAt = ticket.ResolutionDueAt.Value.Add(paused);
            if (ticket.ResponseDueAt.HasValue && ticket.FirstResponseAt == null)
                ticket.ResponseDueAt = ticket.ResponseDueAt.Value.Add(paused);
            ticket.SlaPausedHours += paused.TotalHours;
            ticket.SlaPausedAt = null;
            ticket.PendingWarningSentAt = null;   // #1: fresh warning window next time it's Pending
        }

        ticket.Status = newStatus;
        switch (newStatus)
        {
            case TicketStatus.Resolved: ticket.ResolvedAt = nowUtc; break;
            case TicketStatus.Closed:   ticket.ClosedAt = nowUtc; break;
            case TicketStatus.Reopened: ticket.ResolvedAt = null; ticket.ClosedAt = null; break;
        }
        ticket.UpdatedBy = actor;
        await ticketRepository.UpdateAsync(ticket);   // stamps UpdatedAt

        await historyService.AppendAsync(
            ticket.Id, actor,
            historyAction ?? "StatusChanged",
            oldStatus.ToString(), newStatus.ToString(), notes);

        return oldStatus;
    }
}
