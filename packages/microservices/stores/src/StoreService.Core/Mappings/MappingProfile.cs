using AutoMapper;
using StoreService.Core.DTOs.Categories;
using StoreService.Core.DTOs.Grn;
using StoreService.Core.DTOs.Issues;
using StoreService.Core.DTOs.Items;
using StoreService.Core.DTOs.Locations;
using StoreService.Core.DTOs.Movements;
using StoreService.Core.DTOs.PriceHistory;
using StoreService.Core.DTOs.SoldItems;
using StoreService.Core.DTOs.StockTake;
using StoreService.Core.DTOs.StockUnits;
using StoreService.Core.DTOs.Suppliers;
using StoreService.Core.DTOs.Transfers;
using StoreService.Core.DTOs.UnitsOfMeasure;
using StoreService.Core.Entities;

namespace StoreService.Core.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Suppliers
        CreateMap<Supplier, SupplierReadDto>()
            .ForMember(d => d.ItemCount, o => o.MapFrom(s => s.Items.Count));
        CreateMap<CreateSupplierDto, Supplier>();
        // PATCH semantics. `.ForAllMembers(Condition(srcMember != null))` reads like it protects
        // omitted fields, and does for reference types — but where a nullable DTO member maps onto a
        // non-nullable entity member it is handed the DESTINATION-typed value, so an omitted bool?
        // arrives as false and an omitted decimal? as 0, the guard passes, and the entity is
        // overwritten. Member-level PreCondition receives the SOURCE, so HasValue reflects what was
        // actually sent. (Same defect was found and fixed in operations-service.)
        CreateMap<UpdateSupplierDto, Supplier>()
            .ForMember(d => d.IsActive, o => o.PreCondition((UpdateSupplierDto s) => s.IsActive.HasValue))
            .ForAllMembers(o => o.Condition((src, dest, member) => member != null));

        // Categories
        CreateMap<Category, CategoryReadDto>()
            .ForMember(d => d.ItemCount, o => o.MapFrom(s => s.Items.Count));
        CreateMap<CreateCategoryDto, Category>();
        CreateMap<UpdateCategoryDto, Category>()
            .ForMember(d => d.IsActive, o => o.PreCondition((UpdateCategoryDto s) => s.IsActive.HasValue))
            .ForAllMembers(o => o.Condition((src, dest, member) => member != null));

        // Units of Measure
        CreateMap<UnitOfMeasure, UnitOfMeasureReadDto>()
            .ForMember(d => d.ItemCount, o => o.MapFrom(s => s.Items.Count));
        CreateMap<CreateUnitOfMeasureDto, UnitOfMeasure>();
        CreateMap<UpdateUnitOfMeasureDto, UnitOfMeasure>()
            .ForMember(d => d.IsActive, o => o.PreCondition((UpdateUnitOfMeasureDto s) => s.IsActive.HasValue))
            .ForAllMembers(o => o.Condition((src, dest, member) => member != null));

        // Locations
        CreateMap<Location, LocationReadDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()));
        CreateMap<CreateLocationDto, Location>()
            .ForMember(d => d.Type, o => o.Ignore());
        CreateMap<UpdateLocationDto, Location>()
            .ForMember(d => d.Type, o => o.Ignore())
            .ForMember(d => d.IsActive, o => o.PreCondition((UpdateLocationDto s) => s.IsActive.HasValue))
            .ForAllMembers(o => o.Condition((src, dest, member) => member != null));

        // Item master
        CreateMap<ItemMaster, ItemMasterReadDto>()
            .ForMember(d => d.SupplierName, o => o.MapFrom(s => s.Supplier != null ? s.Supplier.Name : string.Empty))
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.Name : string.Empty))
            .ForMember(d => d.Uom, o => o.MapFrom(s => s.UnitOfMeasure != null ? s.UnitOfMeasure.Name : string.Empty))
            .ForMember(d => d.IsLowStock, o => o.MapFrom(s => s.QtyOnHand <= s.MinStockLevel))
            .ForMember(d => d.IsLowStockAcknowledged, o => o.MapFrom(s => s.LowStockAcknowledgedAt != null));
        CreateMap<CreateItemMasterDto, ItemMaster>();
        // The worst of the five: without these, renaming an item zeroed its reorder levels and
        // price floor and deactivated it.
        CreateMap<UpdateItemMasterDto, ItemMaster>()
            .ForMember(d => d.MinSellingPrice, o => o.PreCondition((UpdateItemMasterDto s) => s.MinSellingPrice.HasValue))
            .ForMember(d => d.MinStockLevel,   o => o.PreCondition((UpdateItemMasterDto s) => s.MinStockLevel.HasValue))
            .ForMember(d => d.MaxStockLevel,   o => o.PreCondition((UpdateItemMasterDto s) => s.MaxStockLevel.HasValue))
            .ForMember(d => d.ReorderQty,      o => o.PreCondition((UpdateItemMasterDto s) => s.ReorderQty.HasValue))
            .ForMember(d => d.IsActive,        o => o.PreCondition((UpdateItemMasterDto s) => s.IsActive.HasValue))
            .ForAllMembers(o => o.Condition((src, dest, member) => member != null));

        // GRN
        CreateMap<GoodsReceivedNote, GrnReadDto>()
            .ForMember(d => d.ItemCode, o => o.MapFrom(s => s.Item != null ? s.Item.ItemCode : string.Empty))
            .ForMember(d => d.ItemName, o => o.MapFrom(s => s.Item != null ? s.Item.Description : string.Empty))
            .ForMember(d => d.SupplierName, o => o.MapFrom(s => s.Supplier != null ? s.Supplier.Name : string.Empty))
            .ForMember(d => d.LocationName, o => o.MapFrom(s => s.Location != null ? s.Location.Name : string.Empty))
            .ForMember(d => d.UnitCost, o => o.MapFrom(s => s.QtyReceived != 0 ? s.LandedCost / s.QtyReceived : 0))
            .ForMember(d => d.InspectionStatus, o => o.MapFrom(s => s.InspectionStatus.ToString()))
            .ForMember(d => d.VariancePct, o => o.Ignore())
            .ForMember(d => d.AlertLevel, o => o.Ignore())
            .ForMember(d => d.StockUnitCount, o => o.Ignore());

        // Purchase price history
        CreateMap<PurchasePriceHistory, PurchasePriceHistoryReadDto>()
            .ForMember(d => d.ItemCode, o => o.MapFrom(s => s.Item != null ? s.Item.ItemCode : string.Empty))
            .ForMember(d => d.SupplierName, o => o.MapFrom(s => s.Supplier != null ? s.Supplier.Name : string.Empty))
            .ForMember(d => d.AlertLevel, o => o.MapFrom(s => s.AlertLevel.ToString()));

        // Stock units
        CreateMap<StockUnit, StockUnitReadDto>()
            .ForMember(d => d.ItemCode, o => o.MapFrom(s => s.Item != null ? s.Item.ItemCode : string.Empty))
            .ForMember(d => d.LocationName, o => o.MapFrom(s => s.Location != null ? s.Location.Name : null))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // Store issue notes
        CreateMap<StoreIssueNote, StoreIssueNoteReadDto>()
            .ForMember(d => d.ItemCode, o => o.MapFrom(s => s.Item != null ? s.Item.ItemCode : string.Empty))
            .ForMember(d => d.LocationName, o => o.MapFrom(s => s.Location != null ? s.Location.Name : null))
            .ForMember(d => d.IssueType, o => o.MapFrom(s => s.IssueType.ToString()));

        // Sold items
        CreateMap<SoldItem, SoldItemReadDto>()
            .ForMember(d => d.ItemCode, o => o.MapFrom(s => s.Item != null ? s.Item.ItemCode : string.Empty))
            .ForMember(d => d.LocationName, o => o.MapFrom(s => s.StockUnit != null && s.StockUnit.Location != null ? s.StockUnit.Location.Name : null));

        // Stock take
        CreateMap<StockTakeReconciliation, StockTakeReadDto>()
            .ForMember(d => d.ItemCode, o => o.MapFrom(s => s.Item != null ? s.Item.ItemCode : string.Empty))
            .ForMember(d => d.LocationName, o => o.MapFrom(s => s.Location != null ? s.Location.Name : null))
            .ForMember(d => d.Variance, o => o.MapFrom(s => s.Variance))
            .ForMember(d => d.ReasonCode, o => o.MapFrom(s => s.ReasonCode.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // Stock movements
        CreateMap<StockMovement, StockMovementReadDto>()
            .ForMember(d => d.ItemCode, o => o.MapFrom(s => s.Item != null ? s.Item.ItemCode : string.Empty))
            .ForMember(d => d.LocationName, o => o.MapFrom(s => s.Location != null ? s.Location.Name : string.Empty))
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()));

        // Store transfers
        CreateMap<StoreTransfer, StoreTransferReadDto>()
            .ForMember(d => d.ItemCode, o => o.MapFrom(s => s.Item != null ? s.Item.ItemCode : string.Empty))
            .ForMember(d => d.FromLocationName, o => o.MapFrom(s => s.FromLocation != null ? s.FromLocation.Name : string.Empty))
            .ForMember(d => d.ToLocationName, o => o.MapFrom(s => s.ToLocation != null ? s.ToLocation.Name : string.Empty))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
    }
}
