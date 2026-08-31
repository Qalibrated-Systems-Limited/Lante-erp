using ComplianceService.Core.DTOs.Dashboard;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Enums;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Core.Services;

public class ComplianceDashboardService(
    IComplianceCrudService<GiftHospitality> gifts,
    IComplianceCrudService<CoiDeclaration> coiDeclarations,
    IComplianceCrudService<WhistleblowerCase> whistleblowerCases,
    IComplianceCrudService<DataSubjectRequest> dataSubjectRequests,
    IComplianceCrudService<DataBreach> dataBreaches,
    IComplianceCrudService<RegulatoryLicence> licences,
    IComplianceCrudService<AntiBriberyTraining> abcTrainings,
    IComplianceCrudService<RelatedPartyTransaction> relatedPartyTransactions) : IComplianceDashboardService
{
    public async Task<ComplianceDashboardDto> ComputeAsync()
    {
        var now = DateTime.UtcNow;
        var soon = now.AddDays(30);
        var currentYear = now.Year;

        var flaggedGifts = await gifts.FindAsync(g => g.Flagged);
        var coiThisYear = await coiDeclarations.FindAsync(c => c.Year == currentYear && c.Status == CoiStatus.Pending);
        var openWhistleblower = await whistleblowerCases.FindAsync(w => w.Status != WhistleblowerStatus.Closed);
        var openDsr = await dataSubjectRequests.FindAsync(d => d.Status != DsrStatus.Completed);
        var breachesPendingNotification = await dataBreaches.FindAsync(b => b.OdpcNotifiedAt == null);
        var allLicences = await licences.FindAsync(l => l.ExpiryDate <= soon);
        var expiringTraining = await abcTrainings.FindAsync(t => t.NextDueOn <= soon);
        var unreportedRelatedParty = await relatedPartyTransactions.FindAsync(r => !r.Reported);

        return new ComplianceDashboardDto
        {
            GiftsFlaggedPendingReview = flaggedGifts.Count,
            CoiDeclarationsOutstandingThisYear = coiThisYear.Count,
            WhistleblowerCasesOpen = openWhistleblower.Count,
            DsrDueSoon = openDsr.Count(d => d.DueBy <= soon && d.DueBy >= now),
            DsrOverdue = openDsr.Count(d => d.DueBy < now),
            DataBreachesPendingOdpcNotification = breachesPendingNotification.Count,
            LicencesExpiringSoon = allLicences.Count(l => l.ExpiryDate >= now),
            LicencesExpired = allLicences.Count(l => l.ExpiryDate < now),
            AbcTrainingDueSoon = expiringTraining.Count,
            RelatedPartyTransactionsUnreported = unreportedRelatedParty.Count,
        };
    }
}
