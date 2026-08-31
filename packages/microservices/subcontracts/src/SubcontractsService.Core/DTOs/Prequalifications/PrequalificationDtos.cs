namespace SubcontractsService.Core.DTOs.Prequalifications;

// SUB-002: PQQ sent/completed/scored/approved workflow.
public class PrequalificationReadDto
{
    public string Id { get; set; } = string.Empty;
    public string SubcontractorId { get; set; } = string.Empty;
    public string? SubcontractorName { get; set; }
    public decimal? Score { get; set; }
    public string? DocumentUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedOn { get; set; }
    public string? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedOn { get; set; }
}

public class CreatePrequalificationDto
{
    public string SubcontractorId { get; set; } = string.Empty;
    public string? DocumentUrl { get; set; }
    public DateTime? SubmittedOn { get; set; }
}

public class ApprovePrequalificationDto
{
    public decimal Score { get; set; }
}

// Core-field edit — Score/ApprovedBy*/ApprovedOn stay approval-workflow-controlled (see
// ApprovePrequalificationDto). SubcontractorId is fixed after creation. Status only recomputes
// (Sent/Completed based on DocumentUrl) while still pre-approval — an already Approved/Rejected
// record keeps its status.
public class UpdatePrequalificationDto
{
    public string? DocumentUrl { get; set; }
    public DateTime? SubmittedOn { get; set; }
}
