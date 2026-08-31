namespace ComplianceService.Core.DTOs.Dashboard;

public class ComplianceDashboardDto
{
    public int GiftsFlaggedPendingReview { get; set; }
    public int CoiDeclarationsOutstandingThisYear { get; set; }
    public int WhistleblowerCasesOpen { get; set; }
    public int DsrDueSoon { get; set; }
    public int DsrOverdue { get; set; }
    public int DataBreachesPendingOdpcNotification { get; set; }
    public int LicencesExpiringSoon { get; set; }
    public int LicencesExpired { get; set; }
    public int AbcTrainingDueSoon { get; set; }
    public int RelatedPartyTransactionsUnreported { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
