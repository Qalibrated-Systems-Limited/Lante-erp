using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>P1 — SUPPLIER_DOCUMENT. Compliance documents per supplier (cert of reg, KRA compliance, tax
/// pin, …), with optional expiry so the P9 compliance score can flag lapsed documents.</summary>
public class SupplierDocument : BaseEntity
{
    public string SupplierId { get; set; } = string.Empty;
    public SupplierDocumentType DocumentType { get; set; } = SupplierDocumentType.Other;
    public string? DocumentName { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public Supplier? Supplier { get; set; }
}
