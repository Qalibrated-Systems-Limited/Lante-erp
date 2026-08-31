using CrmService.Core.Enums;

namespace CrmService.Core.DTOs.Leads;

public class CreateLeadDto
{
    public LeadSourceType Source { get; set; } = LeadSourceType.Web;
    public string? SourceName { get; set; }
    public string? CampaignId { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Industry { get; set; }
    public decimal EstimatedValue { get; set; }
    public LeadRating Rating { get; set; } = LeadRating.Warm;
    public string? Notes { get; set; }
    /// <summary>Service lines the lead is interested in. See CrmFieldRules.ProductRanges.</summary>
    public List<string> ProductRange { get; set; } = new();
}

public class UpdateLeadDto
{
    /// <summary>Null leaves the selection untouched; an empty list clears it.</summary>
    public List<string>? ProductRange { get; set; }
    public LeadSourceType? Source { get; set; }
    public string? SourceName { get; set; }
    public string? CampaignId { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Industry { get; set; }
    public decimal? EstimatedValue { get; set; }
    public LeadRating? Rating { get; set; }
    public string? Notes { get; set; }
}

public class CreateLeadActivityDto
{
    public string ActivityType { get; set; } = "Note";   // Call / Email / Meeting / Note
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? ActivityDate { get; set; }
}

public class QualifyLeadDto { public LeadRating? Rating { get; set; } public string? Notes { get; set; } }
public class UnqualifyLeadDto { public string Reason { get; set; } = string.Empty; }
public class AssignLeadDto { public string AssignedTo { get; set; } = string.Empty; public string? AssignedToName { get; set; } }

public class LeadActivityDto
{
    public string Id { get; set; } = string.Empty;
    public string LeadId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
}

public class LeadSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Rating { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
    public string? Industry { get; set; }
    public List<string> ProductRange { get; set; } = new();
    public bool IsConverted { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool IsStale { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LeadDetailDto : LeadSummaryDto
{
    public string? SourceName { get; set; }
    public string? CampaignId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public string? UnqualifiedReason { get; set; }
    public string? ConvertedCustomerId { get; set; }
    public string? ConvertedOpportunityId { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public List<LeadActivityDto> Activities { get; set; } = new();
}

public class LeadFilterParams
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? ProductRange { get; set; }
    public string? Source { get; set; }
    public string? Rating { get; set; }
    public string? AssignedTo { get; set; }
    public bool? Stale { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record LeadListResult(List<LeadSummaryDto> Items, int Total, int OpenCount, decimal OpenValue);
public record LeadActionResult(string Status, string Message);
