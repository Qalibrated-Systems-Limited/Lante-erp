namespace ProcurementService.Core.DTOs.Suppliers;

// ── Supplier master ──
public class CreateSupplierDto
{
    public string Name { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    public string? CategoryId { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public class UpdateSupplierDto
{
    public string? Name { get; set; }
    public string? KraPin { get; set; }
    public string? CategoryId { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public class SupplierReadDto
{
    public string Id { get; set; } = string.Empty;
    public string SupplierNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool ConflictChecked { get; set; }
    public bool ConflictFound { get; set; }
    public string? ConflictNotes { get; set; }
    public string? ConflictCheckedBy { get; set; }
    public DateTime? ConflictCheckedAt { get; set; }
    public bool BlacklistFlag { get; set; }
    public string? BlacklistReason { get; set; }
    public string? BlacklistedBy { get; set; }
    public DateTime? BlacklistedAt { get; set; }
    public decimal? OverallScore { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public string? FinanceSupplierId { get; set; }
    public string? StoreSupplierId { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SupplierDocumentDto> Documents { get; set; } = new();
}

public class SupplierSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string SupplierNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    public string? CategoryName { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public bool BlacklistFlag { get; set; }
    public decimal? OverallScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SupplierFilterParams
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? CategoryId { get; set; }
    public bool? ApprovedOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record SupplierListResult(List<SupplierSummaryDto> Items, int Total);

// ── Categories ──
public class SupplierCategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal MinScoreThreshold { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveSupplierCategoryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal? MinScoreThreshold { get; set; }
    public string? Description { get; set; }
}

// ── Documents ──
public class UploadSupplierDocumentDto
{
    public string DocumentType { get; set; } = "Other";
    public string? DocumentName { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
}

public class SupplierDocumentDto
{
    public string Id { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentName { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

// ── Workflow actions ──
public class ConflictCheckDto
{
    public bool ConflictFound { get; set; }
    public string? Notes { get; set; }
}

public class BlacklistSupplierDto
{
    public string Reason { get; set; } = string.Empty;
}

// ── Gifts (PROC-007) ──
public class DeclareGiftDto
{
    public string SupplierId { get; set; } = string.Empty;
    public string? ReceivedBy { get; set; }
    public string? ReceivedByName { get; set; }
    public string GiftDescription { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public string? LinkedPoId { get; set; }
}

public class GiftDto
{
    public string Id { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
    public string? ReceivedByName { get; set; }
    public string GiftDescription { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public DateTime DeclaredAt { get; set; }
    public string? LinkedPoId { get; set; }
}

// ── Dashboard ──
public class AsrSummaryDto
{
    public int TotalSuppliers { get; set; }
    public int Approved { get; set; }
    public int Pending { get; set; }
    public int ConflictFlagged { get; set; }
    public int Blacklisted { get; set; }
    public int ExpiringDocuments { get; set; }     // ≤30 days
    public int GiftsThisMonth { get; set; }
    public decimal GiftValueThisMonth { get; set; }
}

public record SupplierActionResult(string Status, string Message);
