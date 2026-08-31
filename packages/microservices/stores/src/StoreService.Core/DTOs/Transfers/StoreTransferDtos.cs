using System.ComponentModel.DataAnnotations;
using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.Transfers;

public class StoreTransferFilterParameters : PaginationParameters
{
    public string? ItemId { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class CreateStoreTransferDto
{
    [Required]
    public string ItemId { get; set; } = string.Empty;

    [Required]
    public string FromLocationId { get; set; } = string.Empty;

    [Required]
    public string ToLocationId { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Qty { get; set; }
}

public class ApproveStoreTransferDto
{
    [Required]
    public string ApprovedBy { get; set; } = string.Empty;
}

public class StoreTransferReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string FromLocationId { get; set; } = string.Empty;
    public string FromLocationName { get; set; } = string.Empty;
    public string ToLocationId { get; set; } = string.Empty;
    public string ToLocationName { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
