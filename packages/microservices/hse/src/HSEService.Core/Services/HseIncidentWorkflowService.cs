using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Incidents;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Core.Services;

public class HseIncidentWorkflowService(
    IHseCrudService<HseIncident> incidents,
    IHseCrudService<EnvIncident> envIncidents,
    IHseCrudService<CorrectiveAction> correctiveActions,
    ITicketingServiceClient ticketingClient) : IHseIncidentWorkflowService
{
    public async Task<HseIncident> CreateAsync(CreateHseIncidentDto dto, string reportedByUserId, string? reportedByName, string? tenantSchema)
    {
        var incident = new HseIncident
        {
            SiteId = dto.SiteId,
            SiteName = dto.SiteName,
            Type = dto.Type,
            Severity = dto.Severity,
            OccurredAt = dto.OccurredAt,
            Description = dto.Description,
            ReportedByUserId = reportedByUserId,
            ReportedByName = reportedByName,
        };
        await incidents.CreateAsync(incident);

        if (dto.IsEnvironmental)
        {
            var env = new EnvIncident { IncidentId = incident.Id, NemaRef = dto.NemaRef };
            await envIncidents.CreateAsync(env);
            incident.EnvIncident = env;
        }

        if (!string.IsNullOrWhiteSpace(dto.CorrectiveActionDescription))
        {
            var capa = new CorrectiveAction
            {
                IncidentId = incident.Id,
                Description = dto.CorrectiveActionDescription,
                OwnerUserId = dto.CorrectiveActionOwnerUserId,
                OwnerName = dto.CorrectiveActionOwnerName,
                DueDate = dto.CorrectiveActionDueDate ?? DateTime.UtcNow.AddHours(48),
            };
            await correctiveActions.CreateAsync(capa);
            // Not incident.CorrectiveActions.Add(capa) here: incidents/correctiveActions share the
            // same scoped DbContext, so EF's change-tracker fixup already adds capa to this
            // navigation collection once its IncidentId FK is saved — adding it again duplicated
            // every corrective action in the create response.
            await NotifyCorrectiveActionOwnerAsync(capa, tenantSchema);
        }

        return incident;
    }

    public async Task<HseIncident?> GetWithDetailsAsync(string id)
    {
        var incident = await incidents.GetByIdAsync(id);
        if (incident is null) return null;
        return await AttachDetailsAsync(incident);
    }

    public async Task<List<HseIncident>> GetAllWithDetailsAsync()
    {
        var all = (await incidents.GetAllAsync()).OrderByDescending(i => i.OccurredAt).ToList();
        foreach (var incident in all)
            await AttachDetailsAsync(incident);
        return all;
    }

    public async Task<PaginatedResult<HseIncident>> GetPagedWithDetailsAsync(PaginationParameters parameters)
    {
        var paged = await incidents.GetPagedAsync(parameters);
        var items = paged.Items.ToList();
        foreach (var incident in items)
            await AttachDetailsAsync(incident);

        return new PaginatedResult<HseIncident>
        {
            Items = items,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<CorrectiveAction> AddCorrectiveActionAsync(CreateCorrectiveActionDto dto, string? tenantSchema)
    {
        var capa = new CorrectiveAction
        {
            IncidentId = dto.IncidentId,
            Description = dto.Description,
            OwnerUserId = dto.OwnerUserId,
            OwnerName = dto.OwnerName,
            DueDate = dto.DueDate,
        };
        await correctiveActions.CreateAsync(capa);
        await NotifyCorrectiveActionOwnerAsync(capa, tenantSchema);
        return capa;
    }

    private async Task NotifyCorrectiveActionOwnerAsync(CorrectiveAction capa, string? tenantSchema)
    {
        if (string.IsNullOrWhiteSpace(capa.OwnerUserId) || string.IsNullOrWhiteSpace(tenantSchema)) return;

        // Personal-only notice — no requiredPermission, so this doesn't broadcast to every
        // hse.read holder the way e.g. AntiBriberyTraining's dual assignee+oversight alert does.
        // A CAPA assignment is a task for one person, not a domain-wide notice.
        await ticketingClient.CreateAlertAsync(
            tenantSchema: tenantSchema,
            source: "HseCorrectiveAction",
            severity: "Warning",
            title: $"Corrective action assigned to you — due {capa.DueDate:d}",
            message: capa.Description,
            assignedToUserId: capa.OwnerUserId);
    }

    private async Task<HseIncident> AttachDetailsAsync(HseIncident incident)
    {
        var env = (await envIncidents.FindAsync(e => e.IncidentId == incident.Id)).FirstOrDefault();
        incident.EnvIncident = env;
        incident.CorrectiveActions = await correctiveActions.FindAsync(c => c.IncidentId == incident.Id);
        return incident;
    }
}
