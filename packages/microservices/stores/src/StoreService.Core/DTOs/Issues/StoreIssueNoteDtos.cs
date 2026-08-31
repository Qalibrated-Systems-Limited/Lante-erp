using System.ComponentModel.DataAnnotations;
using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.Issues;

public class StoreIssueNoteFilterParameters : PaginationParameters
{
    public string? ItemId { get; set; }
    public string? CostCenter { get; set; }
    public string? IssueType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class CreateStoreIssueNoteDto
{
    [Required]
    public string ItemId { get; set; } = string.Empty;

    public string? StockUnitId { get; set; }

    /// <summary>Required only when no StockUnitId is given (a bulk issue with no specific unit) —
    /// otherwise the location is resolved from the stock unit itself.</summary>
    public string? LocationId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal QtyIssued { get; set; }

    [Required]
    public string CostCenter { get; set; } = string.Empty;

    [Required]
    public string IssueType { get; set; } = string.Empty;

    [Required]
    public string IssuedTo { get; set; } = string.Empty;
}

public class StoreIssueNoteReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string? StockUnitId { get; set; }
    public string? LocationId { get; set; }
    public string? LocationName { get; set; }
    public decimal QtyIssued { get; set; }
    public string CostCenter { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string IssuedTo { get; set; } = string.Empty;
    public DateTime IssuedOn { get; set; }
    public DateTime CreatedAt { get; set; }
}
