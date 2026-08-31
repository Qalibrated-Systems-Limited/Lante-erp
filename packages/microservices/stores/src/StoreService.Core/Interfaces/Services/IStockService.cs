using StoreService.Core.DTOs.Common;
using StoreService.Core.DTOs.Issues;
using StoreService.Core.DTOs.Locations;
using StoreService.Core.DTOs.Movements;
using StoreService.Core.DTOs.SoldItems;
using StoreService.Core.DTOs.StockTake;
using StoreService.Core.DTOs.StockUnits;
using StoreService.Core.DTOs.Transfers;

namespace StoreService.Core.Interfaces.Services;

/// <summary>Hold/move/sell side: Stock Unit -> Store Issue Note / Sold Item -> Stock-Take Reconciliation,
/// plus the Location master and the StockMovement ledger that backs per-location balances.</summary>
public interface IStockService
{
    // Locations
    Task<PaginatedResult<LocationReadDto>> GetLocationsAsync(LocationFilterParameters filters);
    Task<LocationReadDto?> GetLocationByIdAsync(string id);
    Task<LocationReadDto> CreateLocationAsync(CreateLocationDto dto, string userId);
    Task<LocationReadDto> UpdateLocationAsync(string id, UpdateLocationDto dto, string userId);
    Task DeleteLocationAsync(string id, string userId);

    // Stock units
    Task<PaginatedResult<StockUnitReadDto>> GetStockUnitsAsync(StockUnitFilterParameters filters);
    Task<StockUnitReadDto?> GetStockUnitByIdAsync(string id);
    Task<StockUnitReadDto> UpdateStockUnitAsync(string id, UpdateStockUnitDto dto, string userId);
    Task DeleteStockUnitAsync(string id, string userId);

    // Store issue notes
    Task<PaginatedResult<StoreIssueNoteReadDto>> GetStoreIssuesAsync(StoreIssueNoteFilterParameters filters);
    Task<StoreIssueNoteReadDto?> GetStoreIssueByIdAsync(string id);
    Task<StoreIssueNoteReadDto> CreateStoreIssueAsync(CreateStoreIssueNoteDto dto, string userId);

    // Sold items
    Task<PaginatedResult<SoldItemReadDto>> GetSoldItemsAsync(SoldItemFilterParameters filters);
    Task<SoldItemReadDto?> GetSoldItemByIdAsync(string id);
    Task<SoldItemReadDto> CreateSoldItemAsync(CreateSoldItemDto dto, string userId);

    // Stock-take reconciliation
    Task<PaginatedResult<StockTakeReadDto>> GetStockTakesAsync(StockTakeFilterParameters filters);
    Task<StockTakeReadDto?> GetStockTakeByIdAsync(string id);
    Task<StockTakeReadDto> CreateStockTakeAsync(CreateStockTakeDto dto, string userId);
    Task<StockTakeReadDto> ApproveStockTakeAsync(string id, ApproveStockTakeDto dto, string userId);

    // Stock movement ledger / balances
    Task<PaginatedResult<StockMovementReadDto>> GetMovementsAsync(StockMovementFilterParameters filters);
    Task<ItemBalancesDto> GetBalancesForItemAsync(string itemId);

    // Store transfers
    Task<PaginatedResult<StoreTransferReadDto>> GetTransfersAsync(StoreTransferFilterParameters filters);
    Task<StoreTransferReadDto?> GetTransferByIdAsync(string id);
    Task<StoreTransferReadDto> CreateTransferAsync(CreateStoreTransferDto dto, string userId);
    Task<StoreTransferReadDto> ApproveTransferAsync(string id, ApproveStoreTransferDto dto, string userId);
    Task<StoreTransferReadDto> CancelTransferAsync(string id, string userId);
}
