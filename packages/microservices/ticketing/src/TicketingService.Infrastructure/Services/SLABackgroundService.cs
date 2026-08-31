using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Services;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Services;

public class SLABackgroundService(IServiceScopeFactory scopeFactory, ILogger<SLABackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SLA Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunChecksAsync();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        logger.LogInformation("SLA Background Service stopped.");
    }

    // Background jobs have no HTTP request to resolve a tenant schema from, so
    // TenantDbConnectionInterceptor falls back to "public" — meaning without this loop, every
    // check below would silently only ever see tenants still in the shared public schema, never
    // a fully-migrated one like tenant_qsl. Discover every provisioned tenant schema directly
    // from Postgres and run the full check suite once per schema.
    private async Task RunChecksAsync()
    {
        foreach (var schema in await GetTenantSchemasAsync())
        {
            try
            {
                await RunChecksForSchemaAsync(schema);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA/escalation/follow-up checks failed for schema {Schema}", schema);
            }
        }
    }

    private async Task<List<string>> GetTenantSchemasAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
        return await context.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync();
    }

    private async Task RunChecksForSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();

        // Pin this scope's connection to the tenant schema, and keep it open for the rest of
        // this method — if it closed and reopened partway through (each service call below
        // could otherwise trigger that independently), the interceptor would silently reset it
        // back to "public" since there's still no HttpContext.
        await context.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync) in this background sweep, filtered to the '^tenant_' pattern — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
        await context.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        var slaService = scope.ServiceProvider.GetRequiredService<ISLAService>();
        var escalationService = scope.ServiceProvider.GetRequiredService<IEscalationService>();
        var workflowEngine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
        var followUpService = scope.ServiceProvider.GetRequiredService<IFollowUpService>();
        var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

        var breachedTickets = (await slaService.CheckSLABreachesAsync()).ToList();
        if (breachedTickets.Count > 0)
        {
            logger.LogWarning("SLA check: {Count} tickets have breached SLA deadlines.", breachedTickets.Count);
            foreach (var ticket in breachedTickets)
            {
                try
                {
                    await workflowEngine.TriggerAsync(WorkflowTriggerEvent.SLABreached, ticket, "system");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to trigger SLABreached workflow for ticket {TicketId}", ticket.Id);
                }

                try
                {
                    await alertService.CreateAsync(
                        schema, source: "SLA", severity: "Warning",
                        title: $"SLA breached — {ticket.Title}",
                        message: $"Ticket \"{ticket.Title}\" has breached its SLA deadline and needs attention.",
                        ticketId: ticket.Id, ticketTitle: ticket.Title, assignedToUserId: ticket.AssignedToUserId);

                    // D7-2: an IT P1 (critical outage, 24/7) breach is escalated to the MD immediately.
                    if (ticket.CategoryId == "cat-it-p1")
                        await alertService.CreateAsync(
                            schema, source: "ITP1Breach", severity: "Critical",
                            title: $"IT P1 SLA breached — {ticket.Title}",
                            message: $"CRITICAL: IT P1 ticket \"{ticket.Title}\" ({ticket.Reference}) has breached its SLA. Immediate MD attention required.",
                            ticketId: ticket.Id, ticketTitle: ticket.Title, requiredPermission: "tickets.resolve");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create SLA alert for ticket {TicketId}", ticket.Id);
                }
            }
        }

        // D2-2: amber ("approaching breach") alerts at the SLA threshold (default 75%). Alerts dedup
        // on (schema, source, title) so this fires once and stays open, not every 5-minute pass.
        try
        {
            foreach (var t in await slaService.GetAmberTicketsAsync())
            {
                await alertService.CreateAsync(
                    schema, source: "SLA-Amber", severity: "Warning",
                    title: $"SLA approaching breach — {t.Title}",
                    message: $"Ticket \"{t.Title}\" has consumed most of its resolution SLA and is approaching breach. Please prioritise it.",
                    ticketId: t.Id, ticketTitle: t.Title,
                    assignedToUserId: t.AssignedToUserId,   // notify the assignee…
                    requiredPermission: "tickets.assign");  // …and surface to Dept Heads/managers
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Amber SLA check failed for schema {Schema}", schema);
        }

        // D2-4: a ticket unassigned for over 30 minutes needs a Dept Head to give it an owner.
        try
        {
            foreach (var t in await slaService.GetUnassignedTicketsAsync(30))
            {
                await alertService.CreateAsync(
                    schema, source: "Unassigned", severity: "Warning",
                    title: $"Unassigned >30m — {t.Title}",
                    message: $"Ticket \"{t.Title}\" has been unassigned for over 30 minutes. A Dept Head should assign an owner.",
                    ticketId: t.Id, ticketTitle: t.Title,
                    requiredPermission: "tickets.assign");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unassigned-ticket check failed for schema {Schema}", schema);
        }

        await escalationService.EvaluateEscalationsAsync(schema);
        await followUpService.ProcessFollowUpsAsync();

        // #12: expire sent quotations whose validity has lapsed, so they stop reading as
        // "awaiting client response" and the TM can revise + resend.
        var expiredQuotes = await context.Quotations
            .Where(q => q.Status == Core.Enums.QuotationStatus.Sent && q.ValidUntil != null && q.ValidUntil < DateTime.UtcNow)
            .ToListAsync();
        if (expiredQuotes.Count > 0)
        {
            foreach (var q in expiredQuotes) { q.Status = Core.Enums.QuotationStatus.Expired; q.UpdatedAt = DateTime.UtcNow; }
            await context.SaveChangesAsync();
            logger.LogInformation("Expired {Count} lapsed quotation(s) in schema {Schema}", expiredQuotes.Count, schema);
        }

        // D8-1b: anchor helpdesk clients to the CRM customer master. Covers clients created before
        // anchoring existed, and any whose CRM record was only added later. Bounded per pass so a
        // tenant with a long tail of unmatchable clients can't turn the sweep into a CRM hammer;
        // the remainder are picked up on subsequent passes.
        var crmDirectory = scope.ServiceProvider.GetRequiredService<Core.Integrations.ICrmCustomerDirectory>();
        if (crmDirectory.IsEnabled)
        {
            try
            {
                var unanchored = await context.Customers
                    .Where(c => c.CrmCustomerId == null && c.Email != null && c.Email != "")
                    .OrderBy(c => c.CreatedAt)
                    .Take(25)
                    .ToListAsync();

                var anchored = 0;
                foreach (var c in unanchored)
                {
                    // Schema passed explicitly — there is no HTTP request here to inherit it from.
                    var crmId = await crmDirectory.ResolveIdByEmailAsync(c.Email!, schema);
                    if (string.IsNullOrWhiteSpace(crmId)) continue;   // no unambiguous match — retry next pass
                    c.CrmCustomerId = crmId;
                    c.UpdatedAt = DateTime.UtcNow;
                    anchored++;
                }

                if (anchored > 0)
                {
                    await context.SaveChangesAsync();
                    logger.LogInformation("Anchored {Count} helpdesk client(s) to CRM in schema {Schema}", anchored, schema);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CRM client anchoring pass failed for schema {Schema}", schema);
            }
        }

        await context.Database.CloseConnectionAsync();
    }
}
