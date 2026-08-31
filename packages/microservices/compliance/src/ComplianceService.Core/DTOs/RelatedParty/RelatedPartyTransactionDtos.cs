using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.RelatedParty;

public class RelatedPartyTransactionFilterParameters : PaginationParameters
{
    public bool? UnreportedOnly { get; set; }
}

public class RelatedPartyTransactionReadDto
{
    public string Id { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public RelationshipType RelationshipType { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public bool Flagged { get; set; }
    public bool Reported { get; set; }
    public DateTime? ReportedAt { get; set; }
}

public class CreateRelatedPartyTransactionDto
{
    public string PartyName { get; set; } = string.Empty;
    public RelationshipType RelationshipType { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

// Core-field edit — Flagged is always true and Reported/ReportedAt stay workflow-controlled
// (see MarkReported).
public class UpdateRelatedPartyTransactionDto
{
    public string PartyName { get; set; } = string.Empty;
    public RelationshipType RelationshipType { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
