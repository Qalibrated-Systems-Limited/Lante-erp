using ProcurementService.Core.DTOs.Suppliers;

namespace ProcurementService.Core.Interfaces.Services;

/// <summary>DEC-B — first-run-per-tenant import of the supplier masters that pre-date the ASR (Finance AP
/// vendors and Stores suppliers) into the register procurement owns (DEC-2), de-duplicated by KRA PIN then
/// name and back-referenced by id. Idempotent: re-running links or skips, never duplicates.</summary>
public interface ISupplierSeedService
{
    /// <summary>Dry run — reports exactly what an import would do, changing nothing.</summary>
    Task<SupplierSeedResultDto> PreviewAsync(SeedSuppliersDto dto);
    Task<SupplierSeedResultDto> SeedAsync(SeedSuppliersDto dto, string userId);
}
