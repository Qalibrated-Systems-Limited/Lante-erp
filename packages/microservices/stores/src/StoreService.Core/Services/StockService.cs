using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StoreService.Core.DTOs.Common;
using StoreService.Core.DTOs.Issues;
using StoreService.Core.DTOs.Locations;
using StoreService.Core.DTOs.Movements;
using StoreService.Core.DTOs.SoldItems;
using StoreService.Core.DTOs.StockTake;
using StoreService.Core.DTOs.StockUnits;
using StoreService.Core.DTOs.Transfers;
using StoreService.Core.Entities;
using StoreService.Core.Enums;
using StoreService.Core.Interfaces.Repositories;
using StoreService.Core.Interfaces.Services;

namespace StoreService.Core.Services;

/// <summary>Hold/move/sell-side domain service: Stock Unit -> Store Issue Note / Sold Item ->
/// Stock-Take Reconciliation, plus the Location master and the StockMovement ledger that backs
/// per-location balances. Consumes the one shared <see cref="IGenericRepository{T}"/> for every
/// entity it manages, plus ItemMaster's repository to keep the total QtyOnHand in sync.</summary>
public class StockService : IStockService
{
    private readonly IGenericRepository<ItemMaster> _items;
    private readonly IGenericRepository<Location> _locations;
    private readonly IGenericRepository<StockUnit> _stockUnits;
    private readonly IGenericRepository<StoreIssueNote> _issues;
    private readonly IGenericRepository<SoldItem> _soldItems;
    private readonly IGenericRepository<StockTakeReconciliation> _stockTakes;
    private readonly IGenericRepository<StockMovement> _movements;
    private readonly IGenericRepository<StoreTransfer> _transfers;
    private readonly IMapper _mapper;

    public StockService(
        IGenericRepository<ItemMaster> items,
        IGenericRepository<Location> locations,
        IGenericRepository<StockUnit> stockUnits,
        IGenericRepository<StoreIssueNote> issues,
        IGenericRepository<SoldItem> soldItems,
        IGenericRepository<StockTakeReconciliation> stockTakes,
        IGenericRepository<StockMovement> movements,
        IGenericRepository<StoreTransfer> transfers,
        IMapper mapper)
    {
        _items = items;
        _locations = locations;
        _stockUnits = stockUnits;
        _issues = issues;
        _soldItems = soldItems;
        _stockTakes = stockTakes;
        _movements = movements;
        _transfers = transfers;
        _mapper = mapper;
    }

    // ─── Locations ────────────────────────────────────────────────────────────

    public async Task<PaginatedResult<LocationReadDto>> GetLocationsAsync(LocationFilterParameters filters)
    {
        var query = _locations.Query().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var s = filters.Search.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(s) || x.Code.ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(filters.Type) && Enum.TryParse<LocationType>(filters.Type, true, out var type))
            query = query.Where(x => x.Type == type);
        if (filters.IsActive.HasValue)
            query = query.Where(x => x.IsActive == filters.IsActive.Value);

        query = query.OrderBy(x => x.Name);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<LocationReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<LocationReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<LocationReadDto?> GetLocationByIdAsync(string id)
    {
        var location = await _locations.GetByIdAsync(id);
        return location is null ? null : _mapper.Map<LocationReadDto>(location);
    }

    public async Task<LocationReadDto> CreateLocationAsync(CreateLocationDto dto, string userId)
    {
        if (!Enum.TryParse<LocationType>(dto.Type, true, out var type))
            throw new InvalidOperationException($"Unknown location type '{dto.Type}'. Valid: Warehouse, Site, Vehicle, Vendor.");

        var location = _mapper.Map<Location>(dto);
        location.Type = type;
        location.CreatedBy = userId;
        location.UpdatedBy = userId;
        await _locations.CreateAsync(location);
        return _mapper.Map<LocationReadDto>(location);
    }

    public async Task<LocationReadDto> UpdateLocationAsync(string id, UpdateLocationDto dto, string userId)
    {
        var location = await _locations.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Location {id} not found.");

        _mapper.Map(dto, location);
        if (!string.IsNullOrWhiteSpace(dto.Type) && Enum.TryParse<LocationType>(dto.Type, true, out var type))
            location.Type = type;

        location.UpdatedBy = userId;
        location.UpdatedAt = DateTime.UtcNow;
        await _locations.UpdateAsync(location);
        return _mapper.Map<LocationReadDto>(location);
    }

    public async Task DeleteLocationAsync(string id, string userId)
    {
        var location = await _locations.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Location {id} not found.");

        location.IsDeleted = true;
        location.UpdatedBy = userId;
        location.UpdatedAt = DateTime.UtcNow;
        await _locations.UpdateAsync(location);
    }

    // ─── Stock units ──────────────────────────────────────────────────────────

    public async Task<PaginatedResult<StockUnitReadDto>> GetStockUnitsAsync(StockUnitFilterParameters filters)
    {
        var query = _stockUnits.Query().Include(u => u.Item).Include(u => u.Location).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.ItemId))
            query = query.Where(u => u.ItemId == filters.ItemId);
        if (!string.IsNullOrWhiteSpace(filters.GrnId))
            query = query.Where(u => u.GrnId == filters.GrnId);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<StockUnitStatus>(filters.Status, true, out var status))
            query = query.Where(u => u.Status == status);
        if (!string.IsNullOrWhiteSpace(filters.LocationId))
            query = query.Where(u => u.LocationId == filters.LocationId);

        query = query.OrderByDescending(u => u.CreatedAt);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<StockUnitReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<StockUnitReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<StockUnitReadDto?> GetStockUnitByIdAsync(string id)
    {
        var unit = await _stockUnits.Query().Include(u => u.Item).Include(u => u.Location).FirstOrDefaultAsync(u => u.Id == id);
        return unit is null ? null : _mapper.Map<StockUnitReadDto>(unit);
    }

    public async Task<StockUnitReadDto> UpdateStockUnitAsync(string id, UpdateStockUnitDto dto, string userId)
    {
        var unit = await _stockUnits.Query().Include(u => u.Item).Include(u => u.Location).FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new KeyNotFoundException($"Stock unit {id} not found.");

        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<StockUnitStatus>(dto.Status, true, out var status))
            unit.Status = status;
        if (dto.LocationId is not null) unit.LocationId = dto.LocationId;

        unit.UpdatedBy = userId;
        unit.UpdatedAt = DateTime.UtcNow;
        await _stockUnits.UpdateAsync(unit);
        return _mapper.Map<StockUnitReadDto>(unit);
    }

    public async Task DeleteStockUnitAsync(string id, string userId)
    {
        var unit = await _stockUnits.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Stock unit {id} not found.");

        unit.Status = StockUnitStatus.WrittenOff;
        unit.IsDeleted = true;
        unit.UpdatedBy = userId;
        unit.UpdatedAt = DateTime.UtcNow;
        await _stockUnits.UpdateAsync(unit);
    }

    // ─── Store issue notes (SIN) ──────────────────────────────────────────────

    public async Task<PaginatedResult<StoreIssueNoteReadDto>> GetStoreIssuesAsync(StoreIssueNoteFilterParameters filters)
    {
        var query = _issues.Query().Include(i => i.Item).Include(i => i.Location).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.ItemId))
            query = query.Where(i => i.ItemId == filters.ItemId);
        if (!string.IsNullOrWhiteSpace(filters.CostCenter))
            query = query.Where(i => i.CostCenter == filters.CostCenter);
        if (!string.IsNullOrWhiteSpace(filters.IssueType) &&
            Enum.TryParse<IssueType>(filters.IssueType, true, out var issueType))
            query = query.Where(i => i.IssueType == issueType);
        if (filters.FromDate.HasValue)
            query = query.Where(i => i.IssuedOn >= filters.FromDate.Value);
        if (filters.ToDate.HasValue)
            query = query.Where(i => i.IssuedOn <= filters.ToDate.Value);

        query = query.OrderByDescending(i => i.IssuedOn);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<StoreIssueNoteReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<StoreIssueNoteReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<StoreIssueNoteReadDto?> GetStoreIssueByIdAsync(string id)
    {
        var issue = await _issues.Query().Include(i => i.Item).Include(i => i.Location).FirstOrDefaultAsync(i => i.Id == id);
        return issue is null ? null : _mapper.Map<StoreIssueNoteReadDto>(issue);
    }

    public async Task<StoreIssueNoteReadDto> CreateStoreIssueAsync(CreateStoreIssueNoteDto dto, string userId)
    {
        // A non-positive issue is never legitimate, and a negative one ran the whole method BACKWARDS:
        // the movement is written as `Quantity = -dto.QtyIssued`, so issuing -100 recorded a +100
        // receipt, and `item.QtyOnHand -= dto.QtyIssued` raised the total to match. That is inventory
        // created from nothing — no GRN, no supplier, no cost, and it survives every existing guard:
        // `-100 > stockUnit.Qty` is false, and the atomic `WHERE Qty >= -100` passes and then
        // INCREASES the lot. Checked first so both the lot and no-lot paths are covered by one guard.
        if (dto.QtyIssued <= 0)
            throw new InvalidOperationException("Issued quantity must be greater than zero.");

        var item = await _items.GetByIdAsync(dto.ItemId)
            ?? throw new KeyNotFoundException($"Item {dto.ItemId} not found.");

        if (!Enum.TryParse<IssueType>(dto.IssueType, true, out var issueType))
            throw new InvalidOperationException($"Unknown issue type '{dto.IssueType}'. Valid: Sale, Internal.");

        StockUnit? stockUnit = null;
        if (!string.IsNullOrWhiteSpace(dto.StockUnitId))
        {
            stockUnit = await _stockUnits.GetByIdAsync(dto.StockUnitId)
                ?? throw new KeyNotFoundException($"Stock unit {dto.StockUnitId} not found.");

            if (dto.QtyIssued > stockUnit.Qty)
                throw new InvalidOperationException($"Insufficient quantity in this lot — available: {stockUnit.Qty}.");

            // Atomic conditional decrement (WHERE Qty >= QtyIssued) instead of read-modify-write:
            // two concurrent issues against the same lot can both pass the in-memory check above
            // before either commits, driving Qty negative. This UPDATE is the real guard — the
            // check above is just for a fast, friendly error on the common (non-racing) path.
            var decremented = await _stockUnits.Query()
                .Where(u => u.Id == stockUnit.Id && u.Qty >= dto.QtyIssued)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.Qty, u => u.Qty - dto.QtyIssued)
                    .SetProperty(u => u.Status, u => (u.Qty - dto.QtyIssued) <= 0 ? StockUnitStatus.Issued : StockUnitStatus.InStock)
                    .SetProperty(u => u.UpdatedBy, userId)
                    .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));

            if (decremented == 0)
                throw new InvalidOperationException($"Insufficient quantity in this lot — available: {stockUnit.Qty}.");

            stockUnit.Qty -= dto.QtyIssued;
            stockUnit.Status = stockUnit.Qty <= 0 ? StockUnitStatus.Issued : StockUnitStatus.InStock;
        }

        var locationId = stockUnit?.LocationId ?? dto.LocationId
            ?? throw new InvalidOperationException("LocationId is required when no StockUnitId is given.");
        _ = await _locations.GetByIdAsync(locationId)
            ?? throw new KeyNotFoundException($"Location {locationId} not found.");

        if (stockUnit is null)
        {
            // Without a lot there was NO availability check at all — the whole guard above sits inside
            // the `if (StockUnitId)` block. Issuing 1,000 of an item with 5 on hand drove QtyOnHand to
            // -995 and wrote a movement that took the location balance negative with it. That is not
            // cosmetic: QtyOnHand pins the item in PurchasingService's low-stock alerts for ever, and
            // AvgWeightedCost x quantity is the COGS snapshot taken at sale.
            //
            // Deliberately NOT atomic, unlike the lot path, and the difference is worth knowing: a lot
            // is a single row, so `UPDATE ... WHERE Qty >= n` serialises concurrent issues. A location
            // balance is a SUM over movements with no row to lock, so two concurrent issues can both
            // pass this check. It closes the ordinary hole and leaves the race; making it airtight
            // needs an advisory lock or a per-(item, location) balance row. See #362.
            var available = await GetLocationBalanceAsync(dto.ItemId, locationId);
            if (dto.QtyIssued > available)
                throw new InvalidOperationException(
                    $"Insufficient stock at this location — available: {available}.");
        }

        var issue = new StoreIssueNote
        {
            ItemId = dto.ItemId,
            StockUnitId = dto.StockUnitId,
            LocationId = locationId,
            QtyIssued = dto.QtyIssued,
            CostCenter = dto.CostCenter,
            IssueType = issueType,
            IssuedTo = dto.IssuedTo,
            IssuedOn = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        await _issues.CreateAsync(issue);

        item.QtyOnHand -= dto.QtyIssued;
        item.UpdatedBy = userId;
        item.UpdatedAt = DateTime.UtcNow;
        await _items.UpdateAsync(item);

        // stockUnit.Qty/.Status were already decremented atomically above (before the issue/item
        // mutations), so there's nothing left to persist here.

        var balanceAfter = await GetLocationBalanceAsync(dto.ItemId, locationId) - dto.QtyIssued;
        await _movements.CreateAsync(new StockMovement
        {
            ItemId = dto.ItemId,
            LocationId = locationId,
            Type = MovementType.Issue,
            Quantity = -dto.QtyIssued,
            BalanceAfter = balanceAfter,
            Reference = issue.Id,
            OccurredAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        var result = _mapper.Map<StoreIssueNoteReadDto>(issue);
        result.ItemCode = item.ItemCode;
        return result;
    }

    // ─── Sold items ───────────────────────────────────────────────────────────

    public async Task<PaginatedResult<SoldItemReadDto>> GetSoldItemsAsync(SoldItemFilterParameters filters)
    {
        var query = _soldItems.Query().Include(s => s.Item)
            .Include(s => s.StockUnit!).ThenInclude(u => u.Location)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.ItemId))
            query = query.Where(s => s.ItemId == filters.ItemId);
        if (!string.IsNullOrWhiteSpace(filters.ClientId))
            query = query.Where(s => s.ClientId == filters.ClientId);
        if (!string.IsNullOrWhiteSpace(filters.InvoiceNo))
            query = query.Where(s => s.InvoiceNo == filters.InvoiceNo);
        if (filters.FromDate.HasValue)
            query = query.Where(s => s.SoldOn >= filters.FromDate.Value);
        if (filters.ToDate.HasValue)
            query = query.Where(s => s.SoldOn <= filters.ToDate.Value);

        query = query.OrderByDescending(s => s.SoldOn);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<SoldItemReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<SoldItemReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<SoldItemReadDto?> GetSoldItemByIdAsync(string id)
    {
        var sold = await _soldItems.Query().Include(s => s.Item)
            .Include(s => s.StockUnit!).ThenInclude(u => u.Location)
            .FirstOrDefaultAsync(s => s.Id == id);
        return sold is null ? null : _mapper.Map<SoldItemReadDto>(sold);
    }

    public async Task<SoldItemReadDto> CreateSoldItemAsync(CreateSoldItemDto dto, string userId)
    {
        var item = await _items.GetByIdAsync(dto.ItemId)
            ?? throw new KeyNotFoundException($"Item {dto.ItemId} not found.");

        // Same backwards-running hole as the issue path: the sale movement is `Quantity = -dto.Qty`,
        // and `-100 > stockUnit.Qty` is false, so a negative sale added stock and booked a negative COGS.
        if (dto.Qty <= 0)
            throw new InvalidOperationException("Sale quantity must be greater than zero.");

        var stockUnit = await _stockUnits.GetByIdAsync(dto.StockUnitId)
            ?? throw new KeyNotFoundException($"Stock unit {dto.StockUnitId} not found.");

        if (string.IsNullOrWhiteSpace(stockUnit.LocationId))
            throw new InvalidOperationException("This stock unit has no location assigned and cannot be sold.");

        if (dto.Qty > stockUnit.Qty)
            throw new InvalidOperationException($"Insufficient quantity in this lot — available: {stockUnit.Qty}.");

        // Atomic conditional decrement — see CreateStoreIssueAsync for why the check above alone
        // isn't enough under concurrent sales against the same lot.
        var decremented = await _stockUnits.Query()
            .Where(u => u.Id == stockUnit.Id && u.Qty >= dto.Qty)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Qty, u => u.Qty - dto.Qty)
                .SetProperty(u => u.Status, u => (u.Qty - dto.Qty) <= 0 ? StockUnitStatus.Sold : StockUnitStatus.InStock)
                .SetProperty(u => u.UpdatedBy, userId)
                .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));

        if (decremented == 0)
            throw new InvalidOperationException($"Insufficient quantity in this lot — available: {stockUnit.Qty}.");

        stockUnit.Qty -= dto.Qty;
        stockUnit.Status = stockUnit.Qty <= 0 ? StockUnitStatus.Sold : StockUnitStatus.InStock;

        var sold = new SoldItem
        {
            ItemId = dto.ItemId,
            StockUnitId = dto.StockUnitId,
            ClientId = dto.ClientId,
            SerialNo = stockUnit.SerialNo,
            Qty = dto.Qty,
            SalePrice = dto.SalePrice,
            InvoiceNo = dto.InvoiceNo,
            SoldOn = DateTime.UtcNow,
            CostAtSale = item.AvgWeightedCost * dto.Qty, // immutable COGS snapshot, total for this transaction
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        await _soldItems.CreateAsync(sold);

        // stockUnit.Qty/.Status were already decremented atomically above.

        item.QtyOnHand -= dto.Qty;
        item.UpdatedBy = userId;
        item.UpdatedAt = DateTime.UtcNow;
        await _items.UpdateAsync(item);

        var balanceAfter = await GetLocationBalanceAsync(dto.ItemId, stockUnit.LocationId) - dto.Qty;
        await _movements.CreateAsync(new StockMovement
        {
            ItemId = dto.ItemId,
            LocationId = stockUnit.LocationId,
            Type = MovementType.Sale,
            Quantity = -dto.Qty,
            BalanceAfter = balanceAfter,
            Reference = sold.Id,
            OccurredAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        stockUnit.Location ??= await _locations.GetByIdAsync(stockUnit.LocationId!);
        sold.StockUnit = stockUnit;

        var result = _mapper.Map<SoldItemReadDto>(sold);
        result.ItemCode = item.ItemCode;
        return result;
    }

    // ─── Stock-take reconciliation ────────────────────────────────────────────

    public async Task<PaginatedResult<StockTakeReadDto>> GetStockTakesAsync(StockTakeFilterParameters filters)
    {
        var query = _stockTakes.Query().Include(s => s.Item).Include(s => s.Location).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.ItemId))
            query = query.Where(s => s.ItemId == filters.ItemId);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<StockTakeApprovalStatus>(filters.Status, true, out var status))
            query = query.Where(s => s.Status == status);
        if (filters.FromDate.HasValue)
            query = query.Where(s => s.CreatedAt >= filters.FromDate.Value);
        if (filters.ToDate.HasValue)
            query = query.Where(s => s.CreatedAt <= filters.ToDate.Value);

        query = query.OrderByDescending(s => s.CreatedAt);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<StockTakeReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<StockTakeReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<StockTakeReadDto?> GetStockTakeByIdAsync(string id)
    {
        var take = await _stockTakes.Query().Include(s => s.Item).Include(s => s.Location).FirstOrDefaultAsync(s => s.Id == id);
        return take is null ? null : _mapper.Map<StockTakeReadDto>(take);
    }

    public async Task<StockTakeReadDto> CreateStockTakeAsync(CreateStockTakeDto dto, string userId)
    {
        var item = await _items.GetByIdAsync(dto.ItemId)
            ?? throw new KeyNotFoundException($"Item {dto.ItemId} not found.");
        _ = await _locations.GetByIdAsync(dto.LocationId)
            ?? throw new KeyNotFoundException($"Location {dto.LocationId} not found.");

        var reasonCode = AdjustmentReasonCode.CountCorrection;
        if (!string.IsNullOrWhiteSpace(dto.ReasonCode) && !Enum.TryParse(dto.ReasonCode, true, out reasonCode))
            throw new InvalidOperationException($"Unknown reason code '{dto.ReasonCode}'.");

        // System count is scoped to THIS location — the running balance from the movement ledger,
        // not the item's global QtyOnHand (which spans every location).
        var systemCount = await GetLocationBalanceAsync(dto.ItemId, dto.LocationId);

        var take = new StockTakeReconciliation
        {
            ItemId = dto.ItemId,
            StockUnitId = dto.StockUnitId,
            LocationId = dto.LocationId,
            PhysicalCount = dto.PhysicalCount,
            SystemCount = systemCount,
            ReasonCode = reasonCode,
            Status = StockTakeApprovalStatus.Pending,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        await _stockTakes.CreateAsync(take);

        var result = _mapper.Map<StockTakeReadDto>(take);
        result.ItemCode = item.ItemCode;
        return result;
    }

    public async Task<StockTakeReadDto> ApproveStockTakeAsync(string id, ApproveStockTakeDto dto, string userId)
    {
        var take = await _stockTakes.Query().Include(t => t.Location).FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException($"Stock take {id} not found.");

        if (take.Status == StockTakeApprovalStatus.Approved)
            throw new InvalidOperationException("This stock take has already been approved.");

        // Atomic conditional transition (WHERE Status != Approved) instead of check-then-write:
        // two concurrent approve calls can both pass the in-memory check above before either
        // commits, and both would go on to apply the variance below — double-adjusting QtyOnHand.
        var approvedNow = await _stockTakes.Query()
            .Where(t => t.Id == id && t.Status != StockTakeApprovalStatus.Approved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.ApprovedBy, dto.ApprovedBy)
                .SetProperty(t => t.ApprovedAt, DateTime.UtcNow)
                .SetProperty(t => t.Status, StockTakeApprovalStatus.Approved)
                .SetProperty(t => t.UpdatedBy, userId)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));

        if (approvedNow == 0)
            throw new InvalidOperationException("This stock take has already been approved.");

        take.Status = StockTakeApprovalStatus.Approved;

        // Approval auto-adjusts the books to match the physical count — at THIS location only.
        // The item's global QtyOnHand moves by the same delta, since it's a maintained sum across
        // every location's movement history.
        var item = await _items.GetByIdAsync(take.ItemId);
        var variance = take.Variance;
        if (item is not null && variance != 0)
        {
            item.QtyOnHand += variance;
            item.UpdatedBy = userId;
            item.UpdatedAt = DateTime.UtcNow;
            ClearLowStockAckIfRecovered(item);
            await _items.UpdateAsync(item);
        }

        if (!string.IsNullOrWhiteSpace(take.LocationId) && variance != 0)
        {
            await _movements.CreateAsync(new StockMovement
            {
                ItemId = take.ItemId,
                LocationId = take.LocationId,
                Type = MovementType.Adjustment,
                Quantity = variance,
                BalanceAfter = take.PhysicalCount,
                Reference = take.Id,
                OccurredAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
        }

        var result = _mapper.Map<StockTakeReadDto>(take);
        result.ItemCode = item?.ItemCode ?? string.Empty;
        return result;
    }

    // ─── Stock movement ledger / balances ─────────────────────────────────────

    public async Task<PaginatedResult<StockMovementReadDto>> GetMovementsAsync(StockMovementFilterParameters filters)
    {
        var query = _movements.Query().Include(m => m.Item).Include(m => m.Location).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.ItemId))
            query = query.Where(m => m.ItemId == filters.ItemId);
        if (!string.IsNullOrWhiteSpace(filters.LocationId))
            query = query.Where(m => m.LocationId == filters.LocationId);
        if (!string.IsNullOrWhiteSpace(filters.Type) && Enum.TryParse<MovementType>(filters.Type, true, out var type))
            query = query.Where(m => m.Type == type);
        if (filters.FromDate.HasValue)
            query = query.Where(m => m.OccurredAt >= filters.FromDate.Value);
        if (filters.ToDate.HasValue)
            query = query.Where(m => m.OccurredAt <= filters.ToDate.Value);

        query = query.OrderByDescending(m => m.OccurredAt);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<StockMovementReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<StockMovementReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<ItemBalancesDto> GetBalancesForItemAsync(string itemId)
    {
        var item = await _items.GetByIdAsync(itemId)
            ?? throw new KeyNotFoundException($"Item {itemId} not found.");

        var grouped = await _movements.Query()
            .Where(m => m.ItemId == itemId)
            .GroupBy(m => m.LocationId)
            .Select(g => new { LocationId = g.Key, Quantity = g.Sum(m => m.Quantity) })
            .Where(g => g.Quantity != 0)
            .ToListAsync();

        var balances = new List<ItemLocationBalanceDto>();
        foreach (var g in grouped)
        {
            var location = await _locations.GetByIdAsync(g.LocationId);
            balances.Add(new ItemLocationBalanceDto
            {
                LocationId = g.LocationId,
                LocationName = location?.Name ?? "Unknown",
                Quantity = g.Quantity,
            });
        }

        return new ItemBalancesDto
        {
            ItemId = itemId,
            ItemCode = item.ItemCode,
            Balances = balances,
        };
    }

    // ─── Store transfers ──────────────────────────────────────────────────────

    public async Task<PaginatedResult<StoreTransferReadDto>> GetTransfersAsync(StoreTransferFilterParameters filters)
    {
        var query = _transfers.Query()
            .Include(t => t.Item).Include(t => t.FromLocation).Include(t => t.ToLocation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.ItemId))
            query = query.Where(t => t.ItemId == filters.ItemId);
        if (!string.IsNullOrWhiteSpace(filters.Status) && Enum.TryParse<TransferStatus>(filters.Status, true, out var status))
            query = query.Where(t => t.Status == status);
        if (filters.FromDate.HasValue)
            query = query.Where(t => t.CreatedAt >= filters.FromDate.Value);
        if (filters.ToDate.HasValue)
            query = query.Where(t => t.CreatedAt <= filters.ToDate.Value);

        query = query.OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<StoreTransferReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<StoreTransferReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<StoreTransferReadDto?> GetTransferByIdAsync(string id)
    {
        var transfer = await _transfers.Query()
            .Include(t => t.Item).Include(t => t.FromLocation).Include(t => t.ToLocation)
            .FirstOrDefaultAsync(t => t.Id == id);
        return transfer is null ? null : _mapper.Map<StoreTransferReadDto>(transfer);
    }

    public async Task<StoreTransferReadDto> CreateTransferAsync(CreateStoreTransferDto dto, string userId)
    {
        if (dto.FromLocationId == dto.ToLocationId)
            throw new InvalidOperationException("Source and destination locations must differ.");

        _ = await _items.GetByIdAsync(dto.ItemId)
            ?? throw new KeyNotFoundException($"Item {dto.ItemId} not found.");
        _ = await _locations.GetByIdAsync(dto.FromLocationId)
            ?? throw new KeyNotFoundException($"Location {dto.FromLocationId} not found.");
        _ = await _locations.GetByIdAsync(dto.ToLocationId)
            ?? throw new KeyNotFoundException($"Location {dto.ToLocationId} not found.");

        var available = await GetLocationBalanceAsync(dto.ItemId, dto.FromLocationId);
        if (available < dto.Qty)
            throw new InvalidOperationException($"Insufficient stock at source location — available: {available}.");

        var transfer = new StoreTransfer
        {
            ItemId = dto.ItemId,
            FromLocationId = dto.FromLocationId,
            ToLocationId = dto.ToLocationId,
            Qty = dto.Qty,
            Status = TransferStatus.Pending,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        await _transfers.CreateAsync(transfer);

        return await EnrichTransferDtoAsync(transfer);
    }

    public async Task<StoreTransferReadDto> ApproveTransferAsync(string id, ApproveStoreTransferDto dto, string userId)
    {
        var transfer = await _transfers.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Transfer {id} not found.");

        if (transfer.Status != TransferStatus.Pending)
            throw new InvalidOperationException("Only pending transfers can be approved.");

        // Re-validate — stock levels may have changed since the request was made.
        var available = await GetLocationBalanceAsync(transfer.ItemId, transfer.FromLocationId);
        if (available < transfer.Qty)
            throw new InvalidOperationException($"Insufficient stock at source location — available: {available}.");

        var fromBalanceAfter = available - transfer.Qty;
        await _movements.CreateAsync(new StockMovement
        {
            ItemId = transfer.ItemId,
            LocationId = transfer.FromLocationId,
            Type = MovementType.TransferOut,
            Quantity = -transfer.Qty,
            BalanceAfter = fromBalanceAfter,
            Reference = transfer.Id,
            OccurredAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        var toBalanceAfter = await GetLocationBalanceAsync(transfer.ItemId, transfer.ToLocationId) + transfer.Qty;
        await _movements.CreateAsync(new StockMovement
        {
            ItemId = transfer.ItemId,
            LocationId = transfer.ToLocationId,
            Type = MovementType.TransferIn,
            Quantity = transfer.Qty,
            BalanceAfter = toBalanceAfter,
            Reference = transfer.Id,
            OccurredAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        transfer.Status = TransferStatus.Completed;
        transfer.ApprovedBy = dto.ApprovedBy;
        transfer.ApprovedAt = DateTime.UtcNow;
        transfer.UpdatedBy = userId;
        transfer.UpdatedAt = DateTime.UtcNow;
        await _transfers.UpdateAsync(transfer);

        return await EnrichTransferDtoAsync(transfer);
    }

    public async Task<StoreTransferReadDto> CancelTransferAsync(string id, string userId)
    {
        var transfer = await _transfers.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Transfer {id} not found.");

        if (transfer.Status != TransferStatus.Pending)
            throw new InvalidOperationException("Only pending transfers can be cancelled.");

        // Never touched the balance (transfers only affect stock once approved) — just flip status.
        transfer.Status = TransferStatus.Cancelled;
        transfer.UpdatedBy = userId;
        transfer.UpdatedAt = DateTime.UtcNow;
        await _transfers.UpdateAsync(transfer);

        return await EnrichTransferDtoAsync(transfer);
    }

    // ─── Shared helpers ────────────────────────────────────────────────────────

    /// <summary>Current per-location balance for an item — SUM of every StockMovement recorded
    /// there. The single source of truth for "Balances by Location".</summary>
    private async Task<decimal> GetLocationBalanceAsync(string itemId, string locationId)
    {
        return await _movements.Query()
            .Where(m => m.ItemId == itemId && m.LocationId == locationId)
            .SumAsync(m => (decimal?)m.Quantity) ?? 0;
    }

    /// <summary>A low-stock acknowledgment only suppresses the alert until stock genuinely
    /// recovers — clear it once QtyOnHand rises back above MinStockLevel so a future dip re-alerts.</summary>
    private static void ClearLowStockAckIfRecovered(ItemMaster item)
    {
        if (item.LowStockAcknowledgedAt is not null && item.QtyOnHand > item.MinStockLevel)
        {
            item.LowStockAcknowledgedAt = null;
            item.LowStockAcknowledgedBy = null;
        }
    }

    private async Task<StoreTransferReadDto> EnrichTransferDtoAsync(StoreTransfer transfer)
    {
        if (transfer.Item is null) transfer.Item = await _items.GetByIdAsync(transfer.ItemId);
        if (transfer.FromLocation is null) transfer.FromLocation = await _locations.GetByIdAsync(transfer.FromLocationId);
        if (transfer.ToLocation is null) transfer.ToLocation = await _locations.GetByIdAsync(transfer.ToLocationId);
        return _mapper.Map<StoreTransferReadDto>(transfer);
    }
}
