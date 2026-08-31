namespace CrmService.Core.DTOs.Tenders;

public class CreateTenderDto
{
    public string Title { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? CustomerId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal EstimatedValue { get; set; }
    public DateTime SubmissionDeadline { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
}

public class UpdateTenderDto
{
    public string? Title { get; set; }
    public string? Source { get; set; }
    public string? ClientName { get; set; }
    public string? Description { get; set; }
    public decimal? EstimatedValue { get; set; }
    public DateTime? SubmissionDeadline { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
}

public class BidBondDto
{
    public string Id { get; set; } = string.Empty;
    public string GuaranteeNumber { get; set; } = string.Empty;
    public string IssuingBank { get; set; } = string.Empty;
    public DateTime ValidityDate { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}
public class SaveBidBondDto
{
    public string GuaranteeNumber { get; set; } = string.Empty;
    public string IssuingBank { get; set; } = string.Empty;
    public DateTime ValidityDate { get; set; }
    public decimal Amount { get; set; }
}

public class TenderOutcomeDto { public string? Reason { get; set; } public string? Notes { get; set; } }

public class TenderSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string TenderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public DateTime SubmissionDeadline { get; set; }
    public int DaysToDeadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
    public bool HasBidBond { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TenderDetailDto : TenderSummaryDto
{
    public string? Source { get; set; }
    public string? CustomerId { get; set; }
    public string? Description { get; set; }
    public string? OutcomeNotes { get; set; }
    public string? LostReason { get; set; }
    public string? LinkedOpportunityId { get; set; }
    public BidBondDto? BidBond { get; set; }
}

public class TenderFilterParams
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? AssignedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record TenderListResult(List<TenderSummaryDto> Items, int Total, int OpenCount, int DueSoonCount, decimal OpenValue);
public record TenderActionResult(string Status, string Message);
