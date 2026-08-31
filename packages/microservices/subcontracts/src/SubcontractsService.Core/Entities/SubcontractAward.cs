using SubcontractsService.Core.Enums;

namespace SubcontractsService.Core.Entities;

// SUB-004: every award linked to project code, value, approval status per authority matrix,
// signed agreement uploaded before mobilization. SUB-005: hard system control — mobilization
// cannot be activated until RAMS is uploaded AND approved by Head of Projects (checked via a
// cross-service call to HSE at activation time, not just trusted at award-creation time).
public class SubcontractAward : BaseEntity
{
    public string SubcontractorId { get; set; } = string.Empty;
    public string? SubcontractorName { get; set; }
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public decimal Value { get; set; }
    public AwardStatus Status { get; set; } = AwardStatus.PendingApproval;
    public string? SignedAgreementUrl { get; set; }
    public bool RamsApproved { get; set; }
    public DateTime? MobilizationActivatedAt { get; set; }
}
