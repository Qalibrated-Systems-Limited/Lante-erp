using AutoMapper;
using Microsoft.AspNetCore.Http;
using TicketingService.Core.DTOs.Complaints;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

/// <inheritdoc cref="IComplaintWorkflowService"/>
public class ComplaintWorkflowService(
    IComplaintWorkflowStepRepository stepRepository,
    ITicketRepository ticketRepository,
    ITicketTransitionService transitionService,
    ITicketHistoryService historyService,
    IAlertService alertService,
    Integrations.ICrmSync crmSync,
    ICustomerService customerService,
    IHttpContextAccessor httpContextAccessor,
    IMapper mapper) : IComplaintWorkflowService
{
    private static readonly ComplaintStepType[] StepOrder =
    {
        ComplaintStepType.Acknowledge,
        ComplaintStepType.Investigate,
        ComplaintStepType.Respond,
        ComplaintStepType.SatisfactionCheck,
        ComplaintStepType.Close,
    };

    private string CurrentSchema =>
        httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value
        ?? httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Schema"].FirstOrDefault()
        ?? string.Empty;

    public async Task InitializeAsync(string ticketId)
    {
        if ((await stepRepository.GetByTicketAsync(ticketId)).Any())
            return;   // idempotent — never double-create

        for (var i = 0; i < StepOrder.Length; i++)
        {
            await stepRepository.CreateAsync(new ComplaintWorkflowStep
            {
                TicketId   = ticketId,
                StepNumber = i + 1,
                StepType   = StepOrder[i],
                Status     = i == 0 ? ComplaintStepStatus.Active : ComplaintStepStatus.Pending,
                CreatedBy  = "system",
            });
        }
    }

    public async Task<IEnumerable<ComplaintStepReadDto>> GetStepsAsync(string ticketId)
    {
        var steps = await stepRepository.GetByTicketAsync(ticketId);
        return mapper.Map<IEnumerable<ComplaintStepReadDto>>(steps);
    }

    public async Task<IEnumerable<ComplaintStepReadDto>> CompleteStepAsync(
        string ticketId, int stepNumber, CompleteComplaintStepDto dto, string actor)
    {
        var steps = (await stepRepository.GetByTicketAsync(ticketId)).ToList();
        if (steps.Count == 0)
            throw new InvalidOperationException("This ticket has no complaint workflow.");

        var step = steps.FirstOrDefault(s => s.StepNumber == stepNumber)
            ?? throw new KeyNotFoundException($"Step {stepNumber} not found.");

        // D5-2 — strict ordering: only the active step (all earlier ones completed) can be completed.
        if (step.Status != ComplaintStepStatus.Active)
            throw new InvalidOperationException(step.Status == ComplaintStepStatus.Completed
                ? "This step is already completed."
                : "Complete the earlier steps first — complaint steps must be done in order.");

        var ticket = await ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} not found.");

        switch (step.StepType)
        {
            case ComplaintStepType.SatisfactionCheck:
                if (dto.SatisfactionMet is null)
                    throw new InvalidOperationException("Record whether the client was satisfied.");
                step.SatisfactionMet = dto.SatisfactionMet;
                // D5-3 — dissatisfaction needs an MD/Dept-Head sign-off before proceeding to close.
                if (dto.SatisfactionMet == false)
                {
                    if (string.IsNullOrWhiteSpace(dto.SignOffByUserId))
                        throw new InvalidOperationException(
                            "The client is dissatisfied — an MD/Dept Head sign-off is required before closing.");
                    step.SignOffByUserId = dto.SignOffByUserId;
                    step.SignOffAt = DateTime.UtcNow;
                    await SafeAlertAsync("ComplaintDissatisfaction", "Critical",
                        $"Complaint dissatisfaction — {ticket.Reference}",
                        $"The client remains dissatisfied with complaint {ticket.Reference}. MD/Dept Head sign-off recorded ({dto.SignOffByUserId}); review before closure.",
                        ticket);
                }
                break;

            case ComplaintStepType.Close:
                // D5-4 — closing a complaint requires both a root cause and a preventive action.
                if (string.IsNullOrWhiteSpace(dto.RootCause) || string.IsNullOrWhiteSpace(dto.PreventiveAction))
                    throw new InvalidOperationException(
                        "Closing a complaint requires both a root cause and a preventive action.");
                step.RootCause = dto.RootCause;
                step.PreventiveAction = dto.PreventiveAction;
                break;
        }

        step.Status = ComplaintStepStatus.Completed;
        step.CompletedByUserId = actor;
        step.CompletedAt = DateTime.UtcNow;
        step.Notes = dto.Notes;
        step.UpdatedBy = actor;
        await stepRepository.UpdateAsync(step);

        await historyService.AppendAsync(ticketId, actor, "ComplaintStepCompleted",
            null, step.StepType.ToString(), dto.Notes);

        var next = steps.FirstOrDefault(s => s.StepNumber == stepNumber + 1);
        if (next != null)
        {
            next.Status = ComplaintStepStatus.Active;
            next.UpdatedBy = actor;
            await stepRepository.UpdateAsync(next);
        }
        else
        {
            // Final step → close the complaint, carrying the root cause captured here. Forced System
            // transitions so it bypasses the manual root-cause gate (D3-3) — the cause is recorded here.
            if (!string.IsNullOrWhiteSpace(step.RootCause)) ticket.RootCause = step.RootCause;
            ticket.ClosureType = ClosureType.Completed;
            if (ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
                await transitionService.TransitionAsync(ticket, TicketStatus.Resolved,
                    TransitionSource.System, actor, "Complaint workflow completed", "Resolved");
            if (ticket.Status != TicketStatus.Closed)
                await transitionService.TransitionAsync(ticket, TicketStatus.Closed,
                    TransitionSource.System, actor,
                    $"Complaint closed — preventive action: {step.PreventiveAction}", "Closed");

            // D5-4 — MD-facing summary (stands in for the monthly CLIENT_COMPLAINT report; CRM link → D8).
            await SafeAlertAsync("ComplaintClosed", "Info",
                $"Complaint closed — {ticket.Reference}",
                $"Complaint {ticket.Reference} closed. Root cause: {step.RootCause}. Preventive action: {step.PreventiveAction}.",
                ticket);

            // D8-3 — record the resolved complaint in the CRM CLIENT_COMPLAINT register (CRM-039), and
            // D8-2 — log the closure as a customer interaction. Both no-op until the CRM adapter exists.
            if (crmSync.IsEnabled)
            {
                var schema = CurrentSchema;
                var satisfaction = steps.FirstOrDefault(s => s.StepType == ComplaintStepType.SatisfactionCheck)?.SatisfactionMet;
                try
                {
                    // D8-1 — prefer the linked CRM customer id so CRM matches by id, not name.
                    var c = string.IsNullOrEmpty(ticket.CustomerId)
                        ? null
                        : await customerService.GetByIdAsync(ticket.CustomerId);
                    var crmId = string.IsNullOrWhiteSpace(c?.CrmCustomerId) ? ticket.CustomerId : c!.CrmCustomerId;
                    var customerName = c?.Name;
                    await crmSync.RecordComplaintClosureAsync(new Integrations.ComplaintClosure(
                        schema, crmId, ticket.Id, ticket.Reference,
                        step.RootCause, step.PreventiveAction, satisfaction, DateTime.UtcNow, customerName));
                    await crmSync.RecordCustomerInteractionAsync(new Integrations.CustomerInteraction(
                        schema, crmId, ticket.Id, ticket.Reference, "ComplaintClosed",
                        $"Complaint {ticket.Reference} resolved and closed.", DateTime.UtcNow, actor, customerName));
                }
                catch { /* integration failure must not block the workflow */ }
            }
        }

        return mapper.Map<IEnumerable<ComplaintStepReadDto>>(await stepRepository.GetByTicketAsync(ticketId));
    }

    private async Task SafeAlertAsync(string source, string severity, string title, string message, Ticket ticket)
    {
        try
        {
            await alertService.CreateAsync(CurrentSchema, source, severity, title, message,
                ticket.Id, ticket.Title, requiredPermission: "tickets.assign");
        }
        catch
        {
            // Non-fatal: a failed alert must not block step completion.
        }
    }
}
