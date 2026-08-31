using SubcontractsService.Core.Enums;

namespace SubcontractsService.Core.Entities;

// SUB-002: PQQ sent to prospective subcontractor via ERP; completed and scored online; documents
// attached; approval by Head of Projects; ASR status updated automatically on approval.
public class Prequalification : BaseEntity
{
    public string SubcontractorId { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public string? DocumentUrl { get; set; }
    public PrequalificationStatus Status { get; set; } = PrequalificationStatus.Sent;
    public DateTime? SubmittedOn { get; set; }
    public string? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedOn { get; set; }
}
