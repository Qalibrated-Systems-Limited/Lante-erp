using HSEService.Core.DTOs.Dashboard;
using HSEService.Core.Entities;
using HSEService.Core.Enums;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Core.Services;

public class HseDashboardService(
    IHseCrudService<HseIncident> incidents,
    IHseCrudService<CorrectiveAction> correctiveActions,
    IHseCrudService<Rams> rams,
    IHseCrudService<HseTrainingRecord> trainingRecords,
    IHseCrudService<StatutoryInspection> inspections) : IHseDashboardService
{
    public async Task<HseDashboardDto> ComputeAsync(decimal hoursWorkedYtd)
    {
        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ytdIncidents = await incidents.FindAsync(i => i.OccurredAt >= yearStart);

        // First-aid-only cases are NOT recordable. The 200,000-hour base below is the OSHA
        // convention, and under it the recordability line is "medical treatment BEYOND first aid" —
        // a plaster or an eye wash is the canonical excluded case. IncidentType already models the
        // distinction, with FirstAid and MedicalTreatment as separate members; counting both
        // overstated TRIR by every first-aid case on the register. See #365.
        var recordable = ytdIncidents.Count(i => i.Type is IncidentType.MedicalTreatment or IncidentType.LostTimeInjury);
        var lostTimeInjuries = ytdIncidents.Count(i => i.Type == IncidentType.LostTimeInjury);
        var nearMisses = ytdIncidents.Count(i => i.Type == IncidentType.NearMiss);

        var openCapas = await correctiveActions.FindAsync(c => c.Status != CorrectiveActionStatus.Completed);
        var overdueCapas = openCapas.Count(c => c.Status != CorrectiveActionStatus.Completed && c.DueDate < DateTime.UtcNow);

        var pendingRams = await rams.FindAsync(r => r.Status == RamsStatus.Submitted || r.Status == RamsStatus.UnderReview);

        var soon = DateTime.UtcNow.AddDays(30);
        var expiringTraining = await trainingRecords.FindAsync(t => t.ExpiresOn != null && t.ExpiresOn <= soon);
        var dueInspections = await inspections.FindAsync(i => i.DueDate <= soon && i.Status != InspectionStatus.Passed);

        return new HseDashboardDto
        {
            Trir = hoursWorkedYtd > 0 ? Math.Round(recordable * 200_000m / hoursWorkedYtd, 2) : 0,
            Ltif = hoursWorkedYtd > 0 ? Math.Round(lostTimeInjuries * 1_000_000m / hoursWorkedYtd, 2) : 0,
            NearMissCount = nearMisses,
            TotalIncidentsYtd = ytdIncidents.Count,
            OpenCorrectiveActions = openCapas.Count,
            OverdueCorrectiveActions = overdueCapas,
            RamsPendingApproval = pendingRams.Count,
            TrainingCertificatesExpiringSoon = expiringTraining.Count,
            StatutoryInspectionsDueSoon = dueInspections.Count,
        };
    }
}
