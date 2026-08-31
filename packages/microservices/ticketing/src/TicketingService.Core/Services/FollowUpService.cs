using Microsoft.Extensions.Logging;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

public class FollowUpService(
    ITicketRepository ticketRepository,
    ISatisfactionRatingRepository ratingRepository,
    INotificationService notificationService,
    ITicketTransitionService transitionService,
    IPortalEmailService emailService,
    ISmsSender smsSender,
    ICustomerService customerService,
    ILogger<FollowUpService> logger) : IFollowUpService
{
    private const int AutoClosePendingHours = 72;
    private const int AutoCloseResolvedHours = 72;
    private const int SatisfactionSurveyAfterHours = 24;
    private const int PendingWarningLeadHours = 24;   // #1: warn this long before auto-close

    public async Task ProcessFollowUpsAsync()
    {
        var activeTickets = (await ticketRepository.GetAllAsync()).ToList();

        await AutoClosePendingTicketsAsync(activeTickets.Where(t => t.Status == TicketStatus.Pending));
        await AutoCloseResolvedTicketsAsync(activeTickets.Where(t => t.Status == TicketStatus.Resolved));
        await SendSatisfactionSurveyRemindersAsync(activeTickets.Where(t => t.Status == TicketStatus.Resolved));
    }

    private async Task AutoClosePendingTicketsAsync(IEnumerable<Core.Entities.Ticket> pendingTickets)
    {
        foreach (var ticket in pendingTickets)
        {
            var hoursSinceUpdate = (DateTime.UtcNow - ticket.UpdatedAt).TotalHours;

            // #1: warn the requester ~24h before the auto-close, once per Pending stint, if we have
            // an email to reach them at. (PendingWarningSentAt is cleared by the gate on un-pend.)
            if (hoursSinceUpdate < AutoClosePendingHours
                && hoursSinceUpdate >= AutoClosePendingHours - PendingWarningLeadHours
                && ticket.PendingWarningSentAt == null
                && !string.IsNullOrWhiteSpace(ticket.RequesterEmail))
            {
                var hoursLeft = Math.Max(1, (int)Math.Round(AutoClosePendingHours - hoursSinceUpdate));
                await emailService.SendAutoCloseWarningAsync(
                    ticket.RequesterEmail!, ticket.Title, ticket.Reference, hoursLeft);
                ticket.PendingWarningSentAt = DateTime.UtcNow;
                await ticketRepository.UpdateAsync(ticket);
                logger.LogInformation("Auto-close warning sent for ticket {TicketId} ({Hours}h left)", ticket.Id, hoursLeft);
                continue;
            }

            if (hoursSinceUpdate < AutoClosePendingHours) continue;

            // #5: never resolved, closed for silence → abandoned (Cancelled), not "done".
            ticket.ClosureType = ClosureType.Cancelled;
            ticket.ClosureReason = $"Auto-closed — no customer response after {AutoClosePendingHours}h.";
            var oldStatus = await transitionService.TransitionAsync(
                ticket, TicketStatus.Closed, TransitionSource.FollowUp, "system",
                $"Auto-closed after {AutoClosePendingHours}h with no customer response", "AutoClosed");

            await notificationService.NotifyStatusChangedAsync(ticket, oldStatus.ToString(),
                TicketStatus.Closed.ToString(), "system");

            logger.LogInformation("Ticket {TicketId} auto-closed after {Hours}h in Pending status",
                ticket.Id, AutoClosePendingHours);
        }
    }

    private async Task AutoCloseResolvedTicketsAsync(IEnumerable<Core.Entities.Ticket> resolvedTickets)
    {
        foreach (var ticket in resolvedTickets)
        {
            if (ticket.ResolvedAt == null) continue;
            var hoursSinceResolved = (DateTime.UtcNow - ticket.ResolvedAt.Value).TotalHours;
            if (hoursSinceResolved < AutoCloseResolvedHours) continue;

            // #5: work was resolved, just auto-tidied — still "done" (keeps ResolvedAt).
            ticket.ClosureType = ClosureType.AutoClosed;
            ticket.ClosureReason = $"Auto-closed {AutoCloseResolvedHours}h after resolution.";
            await transitionService.TransitionAsync(
                ticket, TicketStatus.Closed, TransitionSource.FollowUp, "system",
                $"Auto-closed {AutoCloseResolvedHours}h after resolution", "AutoClosed");

            logger.LogInformation("Ticket {TicketId} auto-closed {Hours}h after resolution", ticket.Id, AutoCloseResolvedHours);
        }
    }

    private async Task SendSatisfactionSurveyRemindersAsync(IEnumerable<Core.Entities.Ticket> resolvedTickets)
    {
        foreach (var ticket in resolvedTickets)
        {
            if (ticket.ResolvedAt == null) continue;
            if (ticket.SurveySentAt != null) continue;   // #7: send the survey once, not every pass
            var hoursSinceResolved = (DateTime.UtcNow - ticket.ResolvedAt.Value).TotalHours;
            if (hoursSinceResolved < SatisfactionSurveyAfterHours) continue;

            var alreadyRated = await ratingRepository.GetByTicketIdAsync(ticket.Id);
            if (alreadyRated != null) continue;

            await notificationService.SendAsync(
                ticket.CreatedByUserId,
                "SatisfactionSurvey",
                $"Your ticket '{ticket.Title}' has been resolved. Please rate your experience.",
                ticket.Id);

            // D6-4 — also send the survey by SMS when the channel is enabled and we have a phone.
            if (smsSender.IsEnabled && !string.IsNullOrEmpty(ticket.CustomerId))
            {
                var customer = await customerService.GetByIdAsync(ticket.CustomerId);
                if (!string.IsNullOrWhiteSpace(customer?.Phone))
                    await smsSender.SendAsync(customer.Phone!,
                        $"Your ticket {ticket.Reference} is resolved. Please rate your experience.");
            }

            ticket.SurveySentAt = DateTime.UtcNow;
            await ticketRepository.UpdateAsync(ticket);

            logger.LogInformation("Satisfaction survey reminder sent for ticket {TicketId}", ticket.Id);
        }
    }
}
