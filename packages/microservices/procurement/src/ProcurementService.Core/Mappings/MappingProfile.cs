using AutoMapper;
using ProcurementService.Core.DTOs.Emergency;
using ProcurementService.Core.DTOs.International;
using ProcurementService.Core.DTOs.Matching;
using ProcurementService.Core.DTOs.Performance;
using ProcurementService.Core.DTOs.PurchaseOrders;
using ProcurementService.Core.DTOs.Quotations;
using ProcurementService.Core.DTOs.Requisitions;
using ProcurementService.Core.DTOs.Suppliers;
using ProcurementService.Core.Entities;

namespace ProcurementService.Core.Mappings;

/// <summary>AutoMapper profile for the Procurement &amp; Supply Chain module (Module 4).
/// Entity↔DTO maps are added per phase. Enum→string maps by convention.</summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // P1 — Approved Supplier Register
        CreateMap<Supplier, SupplierReadDto>()
            .ForMember(d => d.CategoryName, o => o.Ignore());   // resolved in the service
        CreateMap<Supplier, SupplierSummaryDto>()
            .ForMember(d => d.CategoryName, o => o.Ignore());
        CreateMap<CreateSupplierDto, Supplier>();
        CreateMap<SupplierCategory, SupplierCategoryDto>();
        CreateMap<SupplierDocument, SupplierDocumentDto>();
        CreateMap<GiftRegister, GiftDto>();

        // P2 — Purchase Requisition
        CreateMap<PurchaseRequisition, PrReadDto>()
            .ForMember(d => d.IsOverdue, o => o.Ignore());   // computed in the service
        CreateMap<PurchaseRequisition, PrSummaryRowDto>()
            .ForMember(d => d.IsOverdue, o => o.Ignore());
        CreateMap<PurchaseRequisitionLine, PrLineDto>();

        // P3 — Quotation & comparative analysis
        CreateMap<Quotation, QuotationDto>();
        CreateMap<QuotationLine, QuotationLineDto>();

        // P4 — LPO / Purchase Order
        CreateMap<PurchaseOrder, PoReadDto>()
            .ForMember(d => d.Approvals, o => o.Ignore())         // built explicitly in the service
            .ForMember(d => d.NextApprovalRole, o => o.Ignore());
        CreateMap<PurchaseOrder, PoSummaryRowDto>();

        // P6 — 3-way match & payment voucher
        CreateMap<ThreeWayMatch, MatchReadDto>()
            .ForMember(d => d.Exceptions, o => o.Ignore())        // loaded in the service
            .ForMember(d => d.SupplierName, o => o.Ignore())      // from the LPO
            .ForMember(d => d.ReceiptStatus, o => o.Ignore())     // from the LPO
            .ForMember(d => d.CanRaiseVoucher, o => o.Ignore());  // computed in the service
        CreateMap<ThreeWayMatch, MatchRowDto>()
            .ForMember(d => d.SupplierName, o => o.Ignore())
            .ForMember(d => d.OpenExceptions, o => o.Ignore());
        CreateMap<MatchingException, MatchExceptionDto>();

        // P7 — international sourcing
        CreateMap<InternationalPo, IntlPoReadDto>()
            .ForMember(d => d.Components, o => o.Ignore())          // loaded in the service
            .ForMember(d => d.Customs, o => o.Ignore())
            .ForMember(d => d.PoStatus, o => o.Ignore())            // from the LPO
            .ForMember(d => d.ReceiptStatus, o => o.Ignore())
            .ForMember(d => d.QuantityFromReceipt, o => o.Ignore())
            .ForMember(d => d.CanRequestTt, o => o.Ignore())        // computed in the service
            .ForMember(d => d.CanApproveTt, o => o.Ignore())
            .ForMember(d => d.CanSendTt, o => o.Ignore());
        CreateMap<InternationalPo, IntlPoRowDto>();
        CreateMap<LandedCostComponent, LandedCostComponentDto>();
        CreateMap<CustomsDeclaration, CustomsDeclarationDto>();

        // P8 — emergency procurement
        CreateMap<EmergencyProcurementLog, EmergencyReadDto>()
            .ForMember(d => d.SupplierName, o => o.Ignore())        // from the LPO
            .ForMember(d => d.TotalAmount, o => o.Ignore())
            .ForMember(d => d.PoStatus, o => o.Ignore())
            .ForMember(d => d.ReceiptStatus, o => o.Ignore())
            .ForMember(d => d.PostHocOverdue, o => o.Ignore())      // computed in the service
            .ForMember(d => d.PostHocRaisedLate, o => o.Ignore())
            .ForMember(d => d.AwaitingMdApproval, o => o.Ignore())
            .ForMember(d => d.BoardPackPending, o => o.Ignore());
        // P9 — supplier performance review
        CreateMap<SupplierPerformanceReview, PerformanceReviewDto>();
        CreateMap<SupplierPerformanceReview, PerformanceRowDto>();

        CreateMap<EmergencyProcurementLog, EmergencyRowDto>()
            .ForMember(d => d.SupplierName, o => o.Ignore())
            .ForMember(d => d.TotalAmount, o => o.Ignore())
            .ForMember(d => d.PoStatus, o => o.Ignore())
            .ForMember(d => d.MdApproved, o => o.Ignore())
            .ForMember(d => d.PostHocOverdue, o => o.Ignore())
            .ForMember(d => d.PostHocRaisedLate, o => o.Ignore());
    }
}
