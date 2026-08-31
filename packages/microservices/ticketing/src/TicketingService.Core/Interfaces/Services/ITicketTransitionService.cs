using TicketingService.Core.Entities;
using TicketingService.Core.Enums;

namespace TicketingService.Core.Interfaces.Services;

/// The single gate every ticket status change goes through (T0). Centralises transition validation,
/// the evidence check, ResolvedAt/ClosedAt stamping, persistence and history — so no caller writes
/// Ticket.Status directly. Notifications and workflow triggers stay with the caller (they vary by
/// source). Callers set any extra fields (e.g. ResolutionNotes, IsEscalated) before calling.
public interface ITicketTransitionService
{
    /// <summary>Apply a status transition and return the previous status.</summary>
    /// <param name="source">Manual transitions are validated against the transition table; automated
    /// sources may force. </param>
    /// <param name="enforceEvidence">When true, blocks a move to Resolved on evidence-required tickets
    /// with no attachments (caller must have loaded attachments).</param>
    Task<TicketStatus> TransitionAsync(
        Ticket ticket,
        TicketStatus newStatus,
        TransitionSource source,
        string actor,
        string? notes = null,
        string? historyAction = null,
        bool enforceEvidence = false);

    /// <summary>Whether a manual transition from → to is permitted by the transition table.</summary>
    bool IsAllowed(TicketStatus from, TicketStatus to);
}
