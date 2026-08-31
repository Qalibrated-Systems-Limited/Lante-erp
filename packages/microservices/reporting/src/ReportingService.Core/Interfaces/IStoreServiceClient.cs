using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

public interface IStoreServiceClient
{
    Task<PagedResult<GrnReadDto>?> GetGrnPageAsync(DateTime? fromDate, DateTime? toDate, string? supplierId, int page, int pageSize);
    Task<PagedResult<SupplierReadDto>?> GetSuppliersPageAsync(int page, int pageSize);
    Task<PagedResult<ItemMasterReadDto>?> GetItemsPageAsync(int page, int pageSize);
}
