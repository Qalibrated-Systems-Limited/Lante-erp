using TicketingService.Core.DTOs.Complaints;

namespace TicketingService.Core.Interfaces.Services;

/// D5 — the complaint 5-step handling workflow (HELP-007).
public interface IComplaintWorkflowService
{
    /// Create the 5 steps for a ticket (all Pending except step 1 = Active). Idempotent per ticket.
    Task InitializeAsync(string ticketId);

    Task<IEnumerable<ComplaintStepReadDto>> GetStepsAsync(string ticketId);

    /// Complete the current active step (strict ordering). Applies step-specific rules and, on the
    /// final step, closes the ticket. Returns the refreshed step list.
    Task<IEnumerable<ComplaintStepReadDto>> CompleteStepAsync(
        string ticketId, int stepNumber, CompleteComplaintStepDto dto, string actor);
}
