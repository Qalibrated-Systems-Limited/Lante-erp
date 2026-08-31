using Microsoft.Extensions.Logging;
using TicketingService.Core.DTOs.SLA;
using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

public class EscalationService(
    ITicketRepository ticketRepository,
    IEscalationRuleRepository escalationRuleRepository,
    ITicketEscalationRepository ticketEscalationRepository,
    ITicketHistoryService historyService,
    IAlertService alertService,
    ITicketTransitionService transitionService,
    IGenericRepository<TicketAssignment> assignmentRepository,
    ILogger<EscalationService> logger) : IEscalationService
{
    public async Task EvaluateEscalationsAsync(string tenantId)
    {
        // #4: skip Pending (clock paused — a ticket waiting on the customer must not escalate to the MD).
        var activeTickets = (await ticketRepository.GetAllAsync())
            .Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed
                        && t.Status != TicketStatus.Pending && !t.IsEscalated)
            .ToList();

        foreach (var ticket in activeTickets)
        {
            var rules = await escalationRuleRepository.GetByCategoryAndPriorityAsync(ticket.CategoryId, ticket.Priority);
            // #4: measure active hours open, excluding time the SLA clock was paused.
            var hoursOpen = (DateTime.UtcNow - ticket.CreatedAt).TotalHours - ticket.SlaPausedHours;

            foreach (var rule in rules)
            {
                if (hoursOpen >= rule.TriggerAfterHours)
                {
                    await TriggerEscalationAsync(tenantId, ticket, rule);
                    break;
                }
            }
        }
    }

    private async Task TriggerEscalationAsync(string tenantId, Ticket ticket, EscalationRule rule)
    {
        var escalateTo = rule.EscalateToUserId ?? "system";

        var escalation = new TicketEscalation
        {
            TicketId = ticket.Id,
            EscalationLevel = rule.EscalationLevel,
            EscalatedToUserId = escalateTo,
            EscalatedByUserId = null,
            Reason = $"Auto-escalated after {rule.TriggerAfterHours} hours",
            EscalatedAt = DateTime.UtcNow
        };

        await ticketEscalationRepository.CreateAsync(escalation);

        ticket.IsEscalated = true;
        ticket.EscalationLevel = rule.EscalationLevel;
        // #10: reassign to the rule's target when one is configured (not the "system" fallback).
        if (!string.IsNullOrWhiteSpace(rule.EscalateToUserId))
        {
            ticket.AssignedToUserId = rule.EscalateToUserId;
            // D2-5: record the reassignment in the ownership trail.
            await assignmentRepository.CreateAsync(new TicketAssignment
            {
                TicketId         = ticket.Id,
                AssignedToUserId = rule.EscalateToUserId,
                AssignedByUserId = "system",
                AssignedAt       = DateTime.UtcNow,
                Notes            = $"Auto-reassigned on escalation to {rule.EscalationLevel}",
                IsPrimary        = true,
                CreatedBy        = "system",
            });
        }
        await transitionService.TransitionAsync(
            ticket, TicketStatus.Escalated, TransitionSource.Escalation, "system",
            $"Auto-escalated to {rule.EscalationLevel} after {rule.TriggerAfterHours} hours", "Escalated");

        logger.LogInformation("Ticket {TicketId} auto-escalated to {Level}", ticket.Id, rule.EscalationLevel);

        try
        {
            await alertService.CreateAsync(
                tenantId, source: "Escalation", severity: "Critical",
                title: $"Escalated ({rule.EscalationLevel}) — {ticket.Title}",
                message: $"Ticket \"{ticket.Title}\" was auto-escalated to {rule.EscalationLevel} after {rule.TriggerAfterHours} hours open.",
                ticketId: ticket.Id, ticketTitle: ticket.Title,
                // Only notify a real user — "system" (the fallback when no rule.EscalateToUserId
                // is set) isn't a real account, so skip the notification attempt but still create
                // the alert itself so it shows up in the general list.
                assignedToUserId: rule.EscalateToUserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create escalation alert for ticket {TicketId}", ticket.Id);
        }
    }

    public async Task<IEnumerable<TicketEscalationDto>> GetByTicketIdAsync(string ticketId)
    {
        var escalations = await ticketEscalationRepository.GetByTicketIdAsync(ticketId);
        return escalations.Select(e => new TicketEscalationDto
        {
            Id                 = e.Id,
            TicketId           = e.TicketId,
            EscalationLevel    = e.EscalationLevel,
            EscalatedToUserId  = e.EscalatedToUserId,
            EscalatedByUserId  = e.EscalatedByUserId,
            Reason             = e.Reason,
            EscalatedAt        = e.EscalatedAt,
            IsAcknowledged     = e.IsAcknowledged,
            AcknowledgedAt     = e.AcknowledgedAt,
        });
    }

    public async Task<bool> AcknowledgeAsync(string ticketId, string escalationId, string acknowledgedByUserId)
    {
        var escalation = await ticketEscalationRepository.GetByIdAsync(escalationId);
        if (escalation == null || escalation.TicketId != ticketId) return false;

        escalation.IsAcknowledged = true;
        escalation.AcknowledgedAt = DateTime.UtcNow;
        escalation.UpdatedBy = acknowledgedByUserId;
        await ticketEscalationRepository.UpdateAsync(escalation);

        await historyService.AppendAsync(ticketId, acknowledgedByUserId, "EscalationAcknowledged",
            null, escalationId, "Escalation acknowledged");

        logger.LogInformation("Escalation {EscalationId} on ticket {TicketId} acknowledged by {UserId}",
            escalationId, ticketId, acknowledgedByUserId);

        return true;
    }

    public async Task<IEnumerable<EscalationRuleReadDto>> GetRulesByCategoryAsync(string categoryId)
    {
        var all = await escalationRuleRepository.GetAllAsync();
        return all
            .Where(r => r.CategoryId == categoryId)
            .Select(r => new EscalationRuleReadDto
            {
                Id               = r.Id,
                CategoryId       = r.CategoryId,
                Priority         = r.Priority,
                EscalationLevel  = r.EscalationLevel,
                TriggerAfterHours = r.TriggerAfterHours,
                EscalateToUserId = r.EscalateToUserId,
                CreatedAt        = r.CreatedAt,
            });
    }

    public async Task<EscalationRuleReadDto> AddRuleAsync(CreateEscalationRuleDto dto, string createdByUserId)
    {
        var rule = new EscalationRule
        {
            CategoryId       = dto.CategoryId,
            Priority         = dto.Priority,
            EscalationLevel  = dto.EscalationLevel,
            TriggerAfterHours = dto.TriggerAfterHours,
            EscalateToUserId = dto.EscalateToUserId,
            CreatedBy        = createdByUserId,
        };
        var created = await escalationRuleRepository.CreateAsync(rule);
        return new EscalationRuleReadDto
        {
            Id               = created.Id,
            CategoryId       = created.CategoryId,
            Priority         = created.Priority,
            EscalationLevel  = created.EscalationLevel,
            TriggerAfterHours = created.TriggerAfterHours,
            EscalateToUserId = created.EscalateToUserId,
            CreatedAt        = created.CreatedAt,
        };
    }
}
