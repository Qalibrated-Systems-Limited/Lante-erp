namespace CrmService.Core.DTOs.Opportunities;

public class PipelineStageDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public decimal WinProbability { get; set; }
    public string StageType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateOpportunityDto
{
    public string Name { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? LeadId { get; set; }
    public string? PipelineStageId { get; set; }   // defaults to the first open stage
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public decimal EstimatedValue { get; set; }
    public string? Source { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
}

public class UpdateOpportunityDto
{
    public string? Name { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public decimal? EstimatedValue { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
}

public class AdvanceStageDto { public string PipelineStageId { get; set; } = string.Empty; }

public class MarkLostDto
{
    public string Reason { get; set; } = string.Empty;
    public string? CompetitorName { get; set; }   // winning competitor (CRM-013/014)
    public string? CompetitorNotes { get; set; }
}

public class CreateOpportunityActivityDto
{
    public string ActivityType { get; set; } = "Note";
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Outcome { get; set; }
    public DateTime? NextFollowUp { get; set; }
}

public class OpportunityActivityDto
{
    public string Id { get; set; } = string.Empty;
    public string OpportunityId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Outcome { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public DateTime? NextFollowUp { get; set; }
}

public class OpportunityCompetitorDto
{
    public string Id { get; set; } = string.Empty;
    public string CompetitorName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool WasSelected { get; set; }
}

public class OpportunitySummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string OpportunityNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string PipelineStageId { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public decimal Probability { get; set; }
    public decimal EstimatedValue { get; set; }
    public decimal WeightedValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public bool IsStale { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OpportunityDetailDto : OpportunitySummaryDto
{
    public string? CustomerId { get; set; }
    public string? LeadId { get; set; }
    public string? Source { get; set; }
    public DateTime? ActualCloseDate { get; set; }
    public string? LostReason { get; set; }
    public DateTime StageMovedAt { get; set; }
    public string? DealId { get; set; }
    public List<OpportunityActivityDto> Activities { get; set; } = new();
    public List<OpportunityCompetitorDto> Competitors { get; set; } = new();
}

public class OpportunityFilterParams
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? PipelineStageId { get; set; }
    public string? AssignedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// A pipeline "column": a stage plus its open opportunities and aggregate totals.
public class PipelineColumnDto
{
    public PipelineStageDto Stage { get; set; } = new();
    public List<OpportunitySummaryDto> Opportunities { get; set; } = new();
    public int Count { get; set; }
    public decimal Value { get; set; }
    public decimal WeightedValue { get; set; }
}

public record PipelineBoardResult(List<PipelineColumnDto> Columns, decimal TotalValue, decimal TotalWeighted, int OpenCount);
public record OpportunityListResult(List<OpportunitySummaryDto> Items, int Total);
public record OpportunityActionResult(string Status, string StageName, string Message);
