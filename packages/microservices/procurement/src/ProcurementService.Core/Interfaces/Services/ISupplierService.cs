using ProcurementService.Core.DTOs.Suppliers;

namespace ProcurementService.Core.Interfaces.Services;

/// <summary>P1 (PROC-001, PROC-007) — the Approved Supplier Register. Supplier lifecycle (create → compliance
/// docs → conflict-of-interest check → procurement-manager approval → MD blacklist), plus categories and the
/// anti-bribery gift register. Every state change is written to the procurement audit log.</summary>
public interface ISupplierService
{
    // Supplier master
    Task<SupplierListResult> GetAllAsync(SupplierFilterParams filter);
    Task<SupplierReadDto?> GetByIdAsync(string id);
    Task<SupplierReadDto> CreateAsync(CreateSupplierDto dto, string userId, string? userName);
    Task<SupplierReadDto?> UpdateAsync(string id, UpdateSupplierDto dto, string userId);

    // Compliance documents
    Task<List<SupplierDocumentDto>> GetDocumentsAsync(string supplierId);
    Task<SupplierDocumentDto?> UploadDocumentAsync(string supplierId, UploadSupplierDocumentDto dto, string userId);
    Task<SupplierActionResult> VerifyDocumentAsync(string documentId, string userId);

    // Workflow (PROC-007 conflict → PROC-001 approval → blacklist)
    Task<SupplierActionResult> RunConflictCheckAsync(string supplierId, ConflictCheckDto dto, string userId);
    Task<SupplierActionResult> ApproveAsync(string supplierId, string userId);
    Task<SupplierActionResult> BlacklistAsync(string supplierId, BlacklistSupplierDto dto, string userId);
    Task<SupplierActionResult> ReinstateAsync(string supplierId, string userId);

    // Categories
    Task<List<SupplierCategoryDto>> GetCategoriesAsync();
    Task<SupplierCategoryDto> SaveCategoryAsync(SaveSupplierCategoryDto dto, string userId, string? id = null);

    // Gift register (PROC-007)
    Task<List<GiftDto>> GetGiftsAsync(string? supplierId);
    Task<GiftDto> DeclareGiftAsync(DeclareGiftDto dto, string userId, string? userName);

    // Dashboard
    Task<AsrSummaryDto> GetSummaryAsync();
}
