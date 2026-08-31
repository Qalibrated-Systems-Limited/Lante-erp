using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StoreService.Core.DTOs.Categories;
using StoreService.Core.DTOs.Common;
using StoreService.Core.DTOs.Grn;
using StoreService.Core.DTOs.Items;
using StoreService.Core.DTOs.PriceHistory;
using StoreService.Core.DTOs.Suppliers;
using StoreService.Core.DTOs.UnitsOfMeasure;
using StoreService.Core.Entities;
using StoreService.Core.Enums;
using StoreService.Core.Interfaces.Repositories;
using StoreService.Core.Interfaces.Services;

namespace StoreService.Core.Services;

/// <summary>Buy-side domain service: Supplier -> Item Master -> GRN -> Purchase Price History.
/// Consumes the one shared <see cref="IGenericRepository{T}"/> for every entity it manages —
/// no per-entity repository classes.</summary>
public class PurchasingService : IPurchasingService
{
    private readonly IGenericRepository<Supplier> _suppliers;
    private readonly IGenericRepository<Category> _categories;
    private readonly IGenericRepository<UnitOfMeasure> _unitsOfMeasure;
    private readonly IGenericRepository<ItemMaster> _items;
    private readonly IGenericRepository<GoodsReceivedNote> _grns;
    private readonly IGenericRepository<PurchasePriceHistory> _priceHistory;
    private readonly IGenericRepository<StockUnit> _stockUnits;
    private readonly IGenericRepository<Location> _locations;
    private readonly IGenericRepository<StockMovement> _movements;
    private readonly IGenericRepository<GoodsRejectionNote> _rejections;
    private readonly IProcurementReceiptGateway _procurement;
    private readonly IMapper _mapper;

    public PurchasingService(
        IGenericRepository<Supplier> suppliers,
        IGenericRepository<Category> categories,
        IGenericRepository<UnitOfMeasure> unitsOfMeasure,
        IGenericRepository<ItemMaster> items,
        IGenericRepository<GoodsReceivedNote> grns,
        IGenericRepository<PurchasePriceHistory> priceHistory,
        IGenericRepository<StockUnit> stockUnits,
        IGenericRepository<Location> locations,
        IGenericRepository<StockMovement> movements,
        IGenericRepository<GoodsRejectionNote> rejections,
        IProcurementReceiptGateway procurement,
        IMapper mapper)
    {
        _suppliers = suppliers;
        _categories = categories;
        _unitsOfMeasure = unitsOfMeasure;
        _items = items;
        _grns = grns;
        _priceHistory = priceHistory;
        _stockUnits = stockUnits;
        _locations = locations;
        _movements = movements;
        _rejections = rejections;
        _procurement = procurement;
        _mapper = mapper;
    }

    // ─── Suppliers ────────────────────────────────────────────────────────────

    public async Task<PaginatedResult<SupplierReadDto>> GetSuppliersAsync(SupplierFilterParameters filters)
    {
        var query = _suppliers.Query().Include(s => s.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var s = filters.Search.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(s) || (x.KraPin != null && x.KraPin.ToLower().Contains(s)));
        }
        if (filters.IsActive.HasValue)
            query = query.Where(x => x.IsActive == filters.IsActive.Value);

        query = query.OrderBy(x => x.Name);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<SupplierReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<SupplierReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<SupplierReadDto?> GetSupplierByIdAsync(string id)
    {
        var supplier = await _suppliers.Query().Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        return supplier is null ? null : _mapper.Map<SupplierReadDto>(supplier);
    }

    public async Task<SupplierReadDto> CreateSupplierAsync(CreateSupplierDto dto, string userId)
    {
        var supplier = _mapper.Map<Supplier>(dto);
        supplier.CreatedBy = userId;
        supplier.UpdatedBy = userId;
        await _suppliers.CreateAsync(supplier);
        return _mapper.Map<SupplierReadDto>(supplier);
    }

    public async Task<SupplierReadDto> UpdateSupplierAsync(string id, UpdateSupplierDto dto, string userId)
    {
        var supplier = await _suppliers.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Supplier {id} not found.");

        _mapper.Map(dto, supplier);
        supplier.UpdatedBy = userId;
        supplier.UpdatedAt = DateTime.UtcNow;
        await _suppliers.UpdateAsync(supplier);
        return _mapper.Map<SupplierReadDto>(supplier);
    }

    public async Task DeleteSupplierAsync(string id, string userId)
    {
        var supplier = await _suppliers.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Supplier {id} not found.");

        supplier.IsDeleted = true;
        supplier.UpdatedBy = userId;
        supplier.UpdatedAt = DateTime.UtcNow;
        await _suppliers.UpdateAsync(supplier);
    }

    // ─── Categories ───────────────────────────────────────────────────────────

    public async Task<PaginatedResult<CategoryReadDto>> GetCategoriesAsync(CategoryFilterParameters filters)
    {
        var query = _categories.Query().Include(c => c.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var s = filters.Search.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(s) || x.Code.ToLower().Contains(s));
        }
        if (filters.IsActive.HasValue)
            query = query.Where(x => x.IsActive == filters.IsActive.Value);

        query = query.OrderBy(x => x.Name);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<CategoryReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<CategoryReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<CategoryReadDto?> GetCategoryByIdAsync(string id)
    {
        var category = await _categories.Query().Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id);
        return category is null ? null : _mapper.Map<CategoryReadDto>(category);
    }

    public async Task<CategoryReadDto> CreateCategoryAsync(CreateCategoryDto dto, string userId)
    {
        var category = _mapper.Map<Category>(dto);
        category.CreatedBy = userId;
        category.UpdatedBy = userId;
        await _categories.CreateAsync(category);
        return _mapper.Map<CategoryReadDto>(category);
    }

    public async Task<CategoryReadDto> UpdateCategoryAsync(string id, UpdateCategoryDto dto, string userId)
    {
        var category = await _categories.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Category {id} not found.");

        _mapper.Map(dto, category);
        category.UpdatedBy = userId;
        category.UpdatedAt = DateTime.UtcNow;
        await _categories.UpdateAsync(category);
        return _mapper.Map<CategoryReadDto>(category);
    }

    public async Task DeleteCategoryAsync(string id, string userId)
    {
        var category = await _categories.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Category {id} not found.");

        category.IsDeleted = true;
        category.UpdatedBy = userId;
        category.UpdatedAt = DateTime.UtcNow;
        await _categories.UpdateAsync(category);
    }

    // ─── Units of Measure ─────────────────────────────────────────────────────

    public async Task<PaginatedResult<UnitOfMeasureReadDto>> GetUnitsOfMeasureAsync(UnitOfMeasureFilterParameters filters)
    {
        var query = _unitsOfMeasure.Query().Include(u => u.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var s = filters.Search.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(s));
        }
        if (filters.IsActive.HasValue)
            query = query.Where(x => x.IsActive == filters.IsActive.Value);

        query = query.OrderBy(x => x.Name);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<UnitOfMeasureReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<UnitOfMeasureReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<UnitOfMeasureReadDto?> GetUnitOfMeasureByIdAsync(string id)
    {
        var unitOfMeasure = await _unitsOfMeasure.Query().Include(u => u.Items).FirstOrDefaultAsync(u => u.Id == id);
        return unitOfMeasure is null ? null : _mapper.Map<UnitOfMeasureReadDto>(unitOfMeasure);
    }

    public async Task<UnitOfMeasureReadDto> CreateUnitOfMeasureAsync(CreateUnitOfMeasureDto dto, string userId)
    {
        var unitOfMeasure = _mapper.Map<UnitOfMeasure>(dto);
        unitOfMeasure.CreatedBy = userId;
        unitOfMeasure.UpdatedBy = userId;
        await _unitsOfMeasure.CreateAsync(unitOfMeasure);
        return _mapper.Map<UnitOfMeasureReadDto>(unitOfMeasure);
    }

    public async Task<UnitOfMeasureReadDto> UpdateUnitOfMeasureAsync(string id, UpdateUnitOfMeasureDto dto, string userId)
    {
        var unitOfMeasure = await _unitsOfMeasure.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Unit of Measure {id} not found.");

        _mapper.Map(dto, unitOfMeasure);
        unitOfMeasure.UpdatedBy = userId;
        unitOfMeasure.UpdatedAt = DateTime.UtcNow;
        await _unitsOfMeasure.UpdateAsync(unitOfMeasure);
        return _mapper.Map<UnitOfMeasureReadDto>(unitOfMeasure);
    }

    public async Task DeleteUnitOfMeasureAsync(string id, string userId)
    {
        var unitOfMeasure = await _unitsOfMeasure.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Unit of Measure {id} not found.");

        unitOfMeasure.IsDeleted = true;
        unitOfMeasure.UpdatedBy = userId;
        unitOfMeasure.UpdatedAt = DateTime.UtcNow;
        await _unitsOfMeasure.UpdateAsync(unitOfMeasure);
    }

    // ─── Item master ──────────────────────────────────────────────────────────

    public async Task<PaginatedResult<ItemMasterReadDto>> GetItemsAsync(ItemMasterFilterParameters filters)
    {
        var query = _items.Query().Include(i => i.Supplier).Include(i => i.Category).Include(i => i.UnitOfMeasure).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var s = filters.Search.ToLower();
            query = query.Where(x => x.ItemCode.ToLower().Contains(s) || x.Description.ToLower().Contains(s)
                || (x.Barcode != null && x.Barcode.ToLower().Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(filters.SupplierId))
            query = query.Where(x => x.SupplierId == filters.SupplierId);
        if (!string.IsNullOrWhiteSpace(filters.CategoryId))
            query = query.Where(x => x.CategoryId == filters.CategoryId);
        if (filters.IsActive.HasValue)
            query = query.Where(x => x.IsActive == filters.IsActive.Value);
        if (filters.LowStockOnly == true)
            query = query.Where(x => x.QtyOnHand <= x.MinStockLevel);

        query = query.OrderBy(x => x.ItemCode);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<ItemMasterReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<ItemMasterReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<ItemMasterReadDto?> GetItemByIdAsync(string id)
    {
        var item = await _items.Query().Include(i => i.Supplier).Include(i => i.Category).Include(i => i.UnitOfMeasure).FirstOrDefaultAsync(i => i.Id == id);
        return item is null ? null : _mapper.Map<ItemMasterReadDto>(item);
    }

    public async Task<ItemMasterReadDto> CreateItemAsync(CreateItemMasterDto dto, string userId)
    {
        var supplier = await _suppliers.GetByIdAsync(dto.SupplierId)
            ?? throw new KeyNotFoundException($"Supplier {dto.SupplierId} not found.");
        var category = await _categories.GetByIdAsync(dto.CategoryId)
            ?? throw new KeyNotFoundException($"Category {dto.CategoryId} not found.");
        var unitOfMeasure = await _unitsOfMeasure.GetByIdAsync(dto.UomId)
            ?? throw new KeyNotFoundException($"Unit of Measure {dto.UomId} not found.");

        var item = _mapper.Map<ItemMaster>(dto);
        item.CreatedBy = userId;
        item.UpdatedBy = userId;
        await _items.CreateAsync(item);

        item.Supplier = supplier; // enrich the response's SupplierName/CategoryName/Uom without a round-trip
        item.Category = category;
        item.UnitOfMeasure = unitOfMeasure;
        return _mapper.Map<ItemMasterReadDto>(item);
    }

    public async Task<ItemMasterReadDto> UpdateItemAsync(string id, UpdateItemMasterDto dto, string userId)
    {
        var item = await _items.Query().Include(i => i.Supplier).Include(i => i.Category).Include(i => i.UnitOfMeasure).FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new KeyNotFoundException($"Item {id} not found.");

        _mapper.Map(dto, item);
        item.UpdatedBy = userId;
        item.UpdatedAt = DateTime.UtcNow;
        await _items.UpdateAsync(item);

        if (dto.SupplierId is not null && dto.SupplierId != item.Supplier?.Id)
            item.Supplier = await _suppliers.GetByIdAsync(item.SupplierId);
        if (dto.CategoryId is not null && dto.CategoryId != item.Category?.Id)
            item.Category = await _categories.GetByIdAsync(item.CategoryId);
        if (dto.UomId is not null && dto.UomId != item.UnitOfMeasure?.Id)
            item.UnitOfMeasure = await _unitsOfMeasure.GetByIdAsync(item.UomId);

        return _mapper.Map<ItemMasterReadDto>(item);
    }

    public async Task DeleteItemAsync(string id, string userId)
    {
        var item = await _items.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Item {id} not found.");

        item.IsDeleted = true;
        item.UpdatedBy = userId;
        item.UpdatedAt = DateTime.UtcNow;
        await _items.UpdateAsync(item);
    }

    public async Task<IEnumerable<ItemMasterReadDto>> GetLowStockItemsAsync()
    {
        // Excludes acknowledged alerts — AcknowledgeLowStockAsync suppresses until QtyOnHand
        // recovers above MinStockLevel, at which point the flag auto-clears (see ClearLowStockAckIfRecovered).
        var items = await _items.Query()
            .Include(i => i.Supplier)
            .Include(i => i.Category)
            .Include(i => i.UnitOfMeasure)
            .Where(i => i.IsActive && i.QtyOnHand <= i.MinStockLevel && i.LowStockAcknowledgedAt == null)
            .OrderBy(i => i.ItemCode)
            .ToListAsync();

        return items.Select(x => _mapper.Map<ItemMasterReadDto>(x));
    }

    public async Task<ItemMasterReadDto> AcknowledgeLowStockAsync(string itemId, string userId)
    {
        var item = await _items.Query().Include(i => i.Supplier).Include(i => i.Category).Include(i => i.UnitOfMeasure).FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new KeyNotFoundException($"Item {itemId} not found.");

        item.LowStockAcknowledgedAt = DateTime.UtcNow;
        item.LowStockAcknowledgedBy = userId;
        item.UpdatedBy = userId;
        item.UpdatedAt = DateTime.UtcNow;
        await _items.UpdateAsync(item);

        return _mapper.Map<ItemMasterReadDto>(item);
    }

    public async Task<IEnumerable<PurchasePriceHistoryReadDto>> GetPriceHistoryForItemAsync(string itemId)
    {
        var history = await _priceHistory.Query()
            .Include(p => p.Item)
            .Include(p => p.Supplier)
            .Where(p => p.ItemId == itemId)
            .OrderByDescending(p => p.PurchasedOn)
            .ToListAsync();

        return history.Select(x => _mapper.Map<PurchasePriceHistoryReadDto>(x));
    }

    // ─── GRN ──────────────────────────────────────────────────────────────────

    public async Task<PaginatedResult<GrnReadDto>> GetGrnsAsync(GrnFilterParameters filters)
    {
        var query = _grns.Query().Include(g => g.Item).Include(g => g.Supplier).Include(g => g.Location).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.ItemId))
            query = query.Where(g => g.ItemId == filters.ItemId);
        if (!string.IsNullOrWhiteSpace(filters.SupplierId))
            query = query.Where(g => g.SupplierId == filters.SupplierId);
        if (!string.IsNullOrWhiteSpace(filters.InspectionStatus) &&
            Enum.TryParse<InspectionStatus>(filters.InspectionStatus, true, out var status))
            query = query.Where(g => g.InspectionStatus == status);
        if (filters.FromDate.HasValue)
            query = query.Where(g => g.CreatedAt >= filters.FromDate.Value);
        if (filters.ToDate.HasValue)
            query = query.Where(g => g.CreatedAt <= filters.ToDate.Value);

        query = query.OrderByDescending(g => g.CreatedAt);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        var dtos = new List<GrnReadDto>();
        foreach (var g in pageItems)
            dtos.Add(await EnrichGrnDtoAsync(g));

        return new PaginatedResult<GrnReadDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<GrnReadDto?> GetGrnByIdAsync(string id)
    {
        var grn = await _grns.Query().Include(g => g.Item).Include(g => g.Supplier).Include(g => g.Location).FirstOrDefaultAsync(g => g.Id == id);
        return grn is null ? null : await EnrichGrnDtoAsync(grn);
    }

    public async Task<GrnReadDto> CreateGrnAsync(CreateGrnDto dto, string userId)
    {
        var item = await _items.GetByIdAsync(dto.ItemId)
            ?? throw new KeyNotFoundException($"Item {dto.ItemId} not found.");
        _ = await _locations.GetByIdAsync(dto.LocationId)
            ?? throw new KeyNotFoundException($"Location {dto.LocationId} not found.");

        var grn = new GoodsReceivedNote
        {
            ItemId = dto.ItemId,
            SupplierId = dto.SupplierId,
            LocationId = dto.LocationId,
            QtyReceived = dto.QtyReceived,
            LandedCost = dto.LandedCost,
            InspectionStatus = InspectionStatus.Pending,
            Notes = dto.Notes,
            // P5 — procurement LPO link (optional).
            PoId = dto.PoId,
            PoNumber = dto.PoNumber,
            AcceptedQty = dto.AcceptedQty,
            RejectedQty = dto.RejectedQty,
            ConditionNotes = dto.ConditionNotes,
            SerialNumber = dto.SerialNumber,
            DeliveryNoteUrl = dto.DeliveryNoteUrl,
            SupplierInvoiceUrl = dto.SupplierInvoiceUrl,
            PartialDelivery = dto.PartialDelivery,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        await _grns.CreateAsync(grn);

        // Business rule (a): 10%/25% purchase-price red-flag, recorded against price history.
        var unitCost = grn.QtyReceived != 0 ? grn.LandedCost / grn.QtyReceived : 0;
        var (variancePct, alertLevel) = await ComputeVarianceAsync(dto.ItemId, dto.SupplierId, unitCost);

        var priceHistory = new PurchasePriceHistory
        {
            ItemId = dto.ItemId,
            SupplierId = dto.SupplierId,
            Price = unitCost,
            PurchasedOn = DateTime.UtcNow,
            VariancePct = variancePct,
            AlertLevel = alertLevel,
            SourceGrnId = grn.Id,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        await _priceHistory.CreateAsync(priceHistory);

        // Business rule (b): perpetual weighted-average-cost recalculation on receipt.
        var qtyBefore = item.QtyOnHand;
        var newAvgCost = qtyBefore + grn.QtyReceived == 0
            ? item.AvgWeightedCost
            : ((item.AvgWeightedCost * qtyBefore) + grn.LandedCost) / (qtyBefore + grn.QtyReceived);

        item.AvgWeightedCost = newAvgCost;
        item.QtyOnHand = qtyBefore + grn.QtyReceived;
        item.UpdatedBy = userId;
        item.UpdatedAt = DateTime.UtcNow;
        ClearLowStockAckIfRecovered(item);
        await _items.UpdateAsync(item);

        // Movement ledger entry for the receiving location.
        var balanceAfter = await GetLocationBalanceAsync(dto.ItemId, dto.LocationId) + grn.QtyReceived;
        await _movements.CreateAsync(new StockMovement
        {
            ItemId = dto.ItemId,
            LocationId = dto.LocationId,
            Type = MovementType.Receipt,
            Quantity = grn.QtyReceived,
            BalanceAfter = balanceAfter,
            Reference = grn.Id,
            OccurredAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        return await EnrichGrnDtoAsync(grn, priceHistory);
    }

    public async Task<GrnReadDto> UpdateGrnAsync(string id, UpdateGrnDto dto, string userId)
    {
        var grn = await _grns.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"GRN {id} not found.");

        if (grn.InspectionStatus != InspectionStatus.Pending)
            throw new InvalidOperationException("Only GRNs pending inspection can be edited.");

        if (dto.QtyReceived.HasValue) grn.QtyReceived = dto.QtyReceived.Value;
        if (dto.LandedCost.HasValue) grn.LandedCost = dto.LandedCost.Value;
        if (dto.Notes is not null) grn.Notes = dto.Notes;

        grn.UpdatedBy = userId;
        grn.UpdatedAt = DateTime.UtcNow;
        await _grns.UpdateAsync(grn);
        return await EnrichGrnDtoAsync(grn);
    }

    public async Task<GrnReadDto> InspectGrnAsync(string id, InspectGrnDto dto, string userId)
    {
        var grn = await _grns.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"GRN {id} not found.");

        if (grn.InspectionStatus != InspectionStatus.Pending)
            throw new InvalidOperationException("This GRN has already been inspected.");

        grn.InspectionStatus = dto.Passed ? InspectionStatus.Passed : InspectionStatus.Failed;
        grn.InspectedBy = userId;
        grn.InspectedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            grn.Notes = string.IsNullOrWhiteSpace(grn.Notes) ? dto.Notes : $"{grn.Notes}\n{dto.Notes}";

        grn.UpdatedBy = userId;
        grn.UpdatedAt = DateTime.UtcNow;
        await _grns.UpdateAsync(grn);

        if (dto.Passed)
        {
            // One stock-unit lot per GRN (bulk tracking — SerialNo stays null for non-serialized items).
            var stockUnit = new StockUnit
            {
                ItemId = grn.ItemId,
                GrnId = grn.Id,
                LocationId = grn.LocationId,
                SerialNo = grn.SerialNumber,
                Qty = grn.QtyReceived,
                Status = StockUnitStatus.InStock,
                CreatedBy = userId,
                UpdatedBy = userId,
            };
            await _stockUnits.CreateAsync(stockUnit);

            // P5 (DEC-3) — an LPO-linked receipt closes/updates the procurement PO line (best-effort).
            if (!string.IsNullOrWhiteSpace(grn.PoId))
                await _procurement.NotifyReceiptAsync(new PoReceiptNotice(
                    grn.PoId!, grn.AcceptedQty ?? grn.QtyReceived, grn.RejectedQty ?? 0, grn.PartialDelivery, grn.Id));
        }
        else
        {
            // P5 (DFD 5.2a) — failed inspection raises a rejection note; the LPO stays open for a redelivery.
            await _rejections.CreateAsync(new GoodsRejectionNote
            {
                GrnId = grn.Id,
                PoId = grn.PoId,
                PoNumber = grn.PoNumber,
                SupplierId = grn.SupplierId,
                ItemId = grn.ItemId,
                RejectedQty = grn.RejectedQty ?? grn.QtyReceived,
                Reason = dto.Notes ?? "Failed inspection.",
                NotifiedAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
        }

        return await EnrichGrnDtoAsync(grn);
    }

    // ─── Purchase price history ───────────────────────────────────────────────

    public async Task<PaginatedResult<PurchasePriceHistoryReadDto>> GetPriceHistoryAsync(PurchasePriceHistoryFilterParameters filters)
    {
        var query = _priceHistory.Query().Include(p => p.Item).Include(p => p.Supplier).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.ItemId))
            query = query.Where(p => p.ItemId == filters.ItemId);
        if (!string.IsNullOrWhiteSpace(filters.SupplierId))
            query = query.Where(p => p.SupplierId == filters.SupplierId);
        if (!string.IsNullOrWhiteSpace(filters.AlertLevel) &&
            Enum.TryParse<PriceAlertLevel>(filters.AlertLevel, true, out var level))
            query = query.Where(p => p.AlertLevel == level);
        if (filters.FromDate.HasValue)
            query = query.Where(p => p.PurchasedOn >= filters.FromDate.Value);
        if (filters.ToDate.HasValue)
            query = query.Where(p => p.PurchasedOn <= filters.ToDate.Value);

        query = query.OrderByDescending(p => p.PurchasedOn);

        var total = await query.CountAsync();
        var pageItems = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<PurchasePriceHistoryReadDto>
        {
            Items = pageItems.Select(x => _mapper.Map<PurchasePriceHistoryReadDto>(x)).ToList(),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    public async Task<PurchasePriceHistoryReadDto> CreatePriceHistoryAsync(CreatePurchasePriceHistoryDto dto, string userId)
    {
        var (variancePct, alertLevel) = await ComputeVarianceAsync(dto.ItemId, dto.SupplierId, dto.Price);

        var entry = new PurchasePriceHistory
        {
            ItemId = dto.ItemId,
            SupplierId = dto.SupplierId,
            Price = dto.Price,
            PurchasedOn = dto.PurchasedOn ?? DateTime.UtcNow,
            VariancePct = variancePct,
            AlertLevel = alertLevel,
            SourceGrnId = null,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        await _priceHistory.CreateAsync(entry);

        var item = await _items.Query().Include(i => i.Supplier).FirstOrDefaultAsync(i => i.Id == dto.ItemId);
        var result = _mapper.Map<PurchasePriceHistoryReadDto>(entry);
        result.ItemCode = item?.ItemCode ?? string.Empty;
        result.SupplierName = item?.Supplier?.Name ?? string.Empty;
        return result;
    }

    // ─── Shared business-rule helpers ─────────────────────────────────────────

    /// <summary>Business rule (a): compares a new unit price against the most recent purchase
    /// price for the same item+supplier and flags 10%/25% increases.</summary>
    private async Task<(decimal variancePct, PriceAlertLevel level)> ComputeVarianceAsync(
        string itemId, string supplierId, decimal newPrice)
    {
        var previous = await _priceHistory.Query()
            .Where(p => p.ItemId == itemId && p.SupplierId == supplierId)
            .OrderByDescending(p => p.PurchasedOn)
            .FirstOrDefaultAsync();

        if (previous is null || previous.Price == 0)
            return (0, PriceAlertLevel.None);

        var variancePct = (newPrice - previous.Price) / previous.Price * 100m;
        var level = variancePct >= 25 ? PriceAlertLevel.Critical
                  : variancePct >= 10 ? PriceAlertLevel.Warning
                  : PriceAlertLevel.None;
        return (variancePct, level);
    }

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

    private async Task<GrnReadDto> EnrichGrnDtoAsync(GoodsReceivedNote grn, PurchasePriceHistory? knownPriceHistory = null)
    {
        if (grn.Item is null) grn.Item = await _items.GetByIdAsync(grn.ItemId);
        if (grn.Supplier is null) grn.Supplier = await _suppliers.GetByIdAsync(grn.SupplierId);
        if (grn.Location is null) grn.Location = await _locations.GetByIdAsync(grn.LocationId);

        var dto = _mapper.Map<GrnReadDto>(grn);

        var priceHistory = knownPriceHistory ?? await _priceHistory.Query()
            .Where(p => p.SourceGrnId == grn.Id)
            .OrderByDescending(p => p.PurchasedOn)
            .FirstOrDefaultAsync();

        if (priceHistory is not null)
        {
            dto.VariancePct = priceHistory.VariancePct;
            dto.AlertLevel = priceHistory.AlertLevel.ToString();
        }

        dto.StockUnitCount = await _stockUnits.Query().CountAsync(u => u.GrnId == grn.Id);
        return dto;
    }
}
