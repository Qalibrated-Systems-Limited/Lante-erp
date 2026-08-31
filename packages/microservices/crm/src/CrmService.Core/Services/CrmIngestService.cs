using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Integrations;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>D8-2 / D8-3 — ingest of ticketing (Module 7) closures into CRM. A closed ticket becomes a
/// CUSTOMER_INTERACTION (refreshing the client's last-touch + clearing dormancy); a resolved complaint
/// becomes a closed CLIENT_COMPLAINT register row for the monthly MD report. The CRM customer is resolved
/// best-effort by id then display name — ticketing clients aren't CRM-linked yet (D8-1).</summary>
public class CrmIngestService(
    IGenericRepository<Customer> customers,
    IGenericRepository<CustomerInteraction> interactions,
    IGenericRepository<ClientComplaint> complaints) : ICrmIngestService
{
    public async Task<CrmIngestResult> RecordCustomerInteractionAsync(CustomerInteractionIngestDto dto, string userId)
    {
        var customer = await ResolveCustomerAsync(dto.CustomerId, dto.CustomerName);
        if (customer is null)
            return new CrmIngestResult(false, $"No CRM customer matched '{dto.CustomerName ?? dto.CustomerId}'; interaction not logged.");

        await interactions.CreateAsync(new CustomerInteraction
        {
            CustomerId = customer.Id,
            InteractionType = InteractionType.Other,
            Subject = $"[Helpdesk] {dto.InteractionType} — {dto.TicketReference}",
            Description = dto.Summary,
            PerformedBy = string.IsNullOrWhiteSpace(dto.HandledByUserId) ? userId : dto.HandledByUserId!,
            InteractionDate = dto.OccurredAt == default ? DateTime.UtcNow : dto.OccurredAt,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        // A helpdesk touchpoint counts as client contact (P7): refresh last-touch, clear dormancy.
        customer.LastInteractionAt = DateTime.UtcNow;
        customer.DormantSince = null;
        customer.UpdatedBy = userId;
        customer.UpdatedAt = DateTime.UtcNow;
        await customers.UpdateAsync(customer);

        return new CrmIngestResult(true, $"Interaction logged for {customer.Name}.");
    }

    public async Task<CrmIngestResult> RecordComplaintClosureAsync(ComplaintClosureIngestDto dto, string userId)
    {
        var customer = await ResolveCustomerAsync(dto.CustomerId, dto.CustomerName);
        var closedAt = dto.ClosedAt == default ? DateTime.UtcNow : dto.ClosedAt;

        // Idempotency: the same helpdesk complaint must not stack duplicate register rows.
        var subject = $"Helpdesk complaint {dto.TicketReference}";
        if (await complaints.Query().AnyAsync(c => c.Subject == subject))
            return new CrmIngestResult(customer is not null, "Complaint already recorded for this ticket.");

        var resolution = string.Join(" ",
            new[]
            {
                string.IsNullOrWhiteSpace(dto.RootCause) ? null : $"Root cause: {dto.RootCause}.",
                string.IsNullOrWhiteSpace(dto.PreventiveAction) ? null : $"Preventive action: {dto.PreventiveAction}.",
                dto.SatisfactionMet is { } met ? $"Client satisfaction {(met ? "met" : "not met")}." : null,
            }.Where(s => s is not null));

        await complaints.CreateAsync(new ClientComplaint
        {
            ComplaintNumber = await GenerateComplaintNumberAsync(),
            CustomerId = customer?.Id ?? dto.CustomerId ?? string.Empty,
            CustomerName = customer?.Name ?? dto.CustomerName,
            Subject = subject,
            Description = $"Resolved via helpdesk ticket {dto.TicketReference}.",
            Category = "Helpdesk",
            Severity = ComplaintSeverity.Medium,
            Status = ComplaintStatus.Closed,
            Resolution = string.IsNullOrWhiteSpace(resolution) ? "Resolved via helpdesk." : resolution,
            RaisedAt = closedAt,
            ResolvedAt = closedAt,
            ClosedAt = closedAt,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        return customer is null
            ? new CrmIngestResult(false, $"Complaint registered but no CRM customer matched '{dto.CustomerName ?? dto.CustomerId}'.")
            : new CrmIngestResult(true, $"Complaint registered against {customer.Name}.");
    }

    private async Task<Customer?> ResolveCustomerAsync(string? id, string? name)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var byId = await customers.Query().FirstOrDefaultAsync(c => c.Id == id);
            if (byId is not null) return byId;
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name.Trim().ToLower();
            return await customers.Query().FirstOrDefaultAsync(c => c.Name.ToLower() == n);
        }
        return null;
    }

    private async Task<string> GenerateComplaintNumberAsync()
    {
        var prefix = $"CMP-{DateTime.UtcNow.Year}-";
        var count = await complaints.Query().CountAsync(c => c.ComplaintNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }
}
