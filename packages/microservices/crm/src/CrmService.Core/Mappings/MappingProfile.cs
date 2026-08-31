using AutoMapper;
using CrmService.Core.DTOs.Customers;
using CrmService.Core.DTOs.Leads;
using CrmService.Core.DTOs.Opportunities;
using CrmService.Core.DTOs.Quotations;
using CrmService.Core.DTOs.Deals;
using CrmService.Core.DTOs.Tenders;
using CrmService.Core.DTOs.Activity;
using CrmService.Core.DTOs.Transfers;
using CrmService.Core.DTOs.Dashboards;
using CrmService.Core.DTOs.Marketing;
using CrmService.Core.DTOs.AfterSales;
using CrmService.Core.DTOs.Legal;
using CrmService.Core.DTOs.Payments;
using CrmService.Core.Entities;

namespace CrmService.Core.Mappings;

/// <summary>AutoMapper profile for the CRM &amp; Sales module. Enum→string maps by convention.</summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // C1 — Customers
        CreateMap<Customer, CustomerSummaryDto>();
        CreateMap<Customer, CustomerDetailDto>();
        CreateMap<CustomerContact, CustomerContactDto>();

        // C2 — Leads
        // ProductRange is stored as comma-separated text but exposed as a list, so both lead maps
        // need it splitting explicitly — AutoMapper cannot infer string -> List<string>.
        CreateMap<Lead, LeadSummaryDto>()
            .ForMember(d => d.ProductRange, o => o.MapFrom(src => Services.CrmFieldRules.SplitProductRange(src.ProductRange)));
        CreateMap<Lead, LeadDetailDto>()
            .ForMember(d => d.ProductRange, o => o.MapFrom(src => Services.CrmFieldRules.SplitProductRange(src.ProductRange)));
        CreateMap<LeadActivity, LeadActivityDto>();

        // C3 — Opportunities & pipeline
        CreateMap<PipelineStage, PipelineStageDto>();
        CreateMap<Opportunity, OpportunitySummaryDto>();
        CreateMap<Opportunity, OpportunityDetailDto>();
        CreateMap<OpportunityActivity, OpportunityActivityDto>();
        CreateMap<OpportunityCompetitor, OpportunityCompetitorDto>();

        // C4 — Quotations
        CreateMap<Quotation, QuotationSummaryDto>();
        CreateMap<Quotation, QuotationDetailDto>();
        CreateMap<QuotationLine, QuotationLineDto>();
        CreateMap<PriceList, PriceListDto>();

        // C5 — Deals & contracts
        CreateMap<Deal, DealSummaryDto>();
        CreateMap<Deal, DealDetailDto>();
        CreateMap<DealProduct, DealProductDto>();
        CreateMap<Contract, ContractDto>();

        // C6 — Tenders
        CreateMap<Tender, TenderSummaryDto>();
        CreateMap<Tender, TenderDetailDto>();
        CreateMap<TenderBidBond, BidBondDto>();

        // C7 — Client interaction & activity
        CreateMap<CustomerInteraction, CustomerInteractionDto>();
        CreateMap<ActivityTask, ActivityTaskDto>();
        CreateMap<ClientVisit, ClientVisitDto>();
        CreateMap<VisitTarget, VisitTargetDto>();
        CreateMap<SalesActivityLog, SalesActivityLogDto>();

        // C8 — Account ownership transfer
        CreateMap<ClientTransferRequest, TransferSummaryDto>();
        CreateMap<ClientTransferRequest, TransferDetailDto>();
        CreateMap<ClientTransferHandover, HandoverDto>();

        // C9 — Sales targets
        CreateMap<SalesTarget, SalesTargetDto>();

        // C10 — Marketing (attribution fields computed in the service, not mapped)
        CreateMap<Campaign, CampaignDto>();
        CreateMap<BrandAsset, BrandAssetDto>();

        // C11 — After-sales & retention
        CreateMap<ClientSatisfactionSurvey, SurveyDto>();
        CreateMap<ServiceContract, ServiceContractDto>();   // DaysToExpiry computed in the service
        CreateMap<ClientComplaint, ComplaintDto>();
        CreateMap<NpsSurvey, NpsDto>();

        // C12 — Legal & contract register (DaysToExpiry / usability flags computed in the service)
        CreateMap<NdaRegister, NdaDto>();
        CreateMap<FrameworkAgreement, FrameworkDto>();
        CreateMap<SubcontractorAgreement, SubcontractDto>();
        CreateMap<CarrierAgreement, CarrierDto>();

        // C13 — Payment / debtor alerts
        CreateMap<PaymentAlertLog, PaymentAlertDto>();
    }
}
