using StoreService.Core.DTOs.Categories;
using StoreService.Core.DTOs.Common;
using StoreService.Core.DTOs.Grn;
using StoreService.Core.DTOs.Items;
using StoreService.Core.DTOs.PriceHistory;
using StoreService.Core.DTOs.Suppliers;
using StoreService.Core.DTOs.UnitsOfMeasure;

namespace StoreService.Core.Interfaces.Services;

/// <summary>Buy-side: Supplier -> Item Master -> GRN -> Purchase Price History.</summary>
public interface IPurchasingService
{
    // Suppliers
    Task<PaginatedResult<SupplierReadDto>> GetSuppliersAsync(SupplierFilterParameters filters);
    Task<SupplierReadDto?> GetSupplierByIdAsync(string id);
    Task<SupplierReadDto> CreateSupplierAsync(CreateSupplierDto dto, string userId);
    Task<SupplierReadDto> UpdateSupplierAsync(string id, UpdateSupplierDto dto, string userId);
    Task DeleteSupplierAsync(string id, string userId);

    // Categories
    Task<PaginatedResult<CategoryReadDto>> GetCategoriesAsync(CategoryFilterParameters filters);
    Task<CategoryReadDto?> GetCategoryByIdAsync(string id);
    Task<CategoryReadDto> CreateCategoryAsync(CreateCategoryDto dto, string userId);
    Task<CategoryReadDto> UpdateCategoryAsync(string id, UpdateCategoryDto dto, string userId);
    Task DeleteCategoryAsync(string id, string userId);

    // Units of Measure
    Task<PaginatedResult<UnitOfMeasureReadDto>> GetUnitsOfMeasureAsync(UnitOfMeasureFilterParameters filters);
    Task<UnitOfMeasureReadDto?> GetUnitOfMeasureByIdAsync(string id);
    Task<UnitOfMeasureReadDto> CreateUnitOfMeasureAsync(CreateUnitOfMeasureDto dto, string userId);
    Task<UnitOfMeasureReadDto> UpdateUnitOfMeasureAsync(string id, UpdateUnitOfMeasureDto dto, string userId);
    Task DeleteUnitOfMeasureAsync(string id, string userId);

    // Item master
    Task<PaginatedResult<ItemMasterReadDto>> GetItemsAsync(ItemMasterFilterParameters filters);
    Task<ItemMasterReadDto?> GetItemByIdAsync(string id);
    Task<ItemMasterReadDto> CreateItemAsync(CreateItemMasterDto dto, string userId);
    Task<ItemMasterReadDto> UpdateItemAsync(string id, UpdateItemMasterDto dto, string userId);
    Task DeleteItemAsync(string id, string userId);
    Task<IEnumerable<ItemMasterReadDto>> GetLowStockItemsAsync();
    Task<ItemMasterReadDto> AcknowledgeLowStockAsync(string itemId, string userId);
    Task<IEnumerable<PurchasePriceHistoryReadDto>> GetPriceHistoryForItemAsync(string itemId);

    // GRN
    Task<PaginatedResult<GrnReadDto>> GetGrnsAsync(GrnFilterParameters filters);
    Task<GrnReadDto?> GetGrnByIdAsync(string id);
    Task<GrnReadDto> CreateGrnAsync(CreateGrnDto dto, string userId);
    Task<GrnReadDto> UpdateGrnAsync(string id, UpdateGrnDto dto, string userId);
    Task<GrnReadDto> InspectGrnAsync(string id, InspectGrnDto dto, string userId);

    // Purchase price history
    Task<PaginatedResult<PurchasePriceHistoryReadDto>> GetPriceHistoryAsync(PurchasePriceHistoryFilterParameters filters);
    Task<PurchasePriceHistoryReadDto> CreatePriceHistoryAsync(CreatePurchasePriceHistoryDto dto, string userId);
}
