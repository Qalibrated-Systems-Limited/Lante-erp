using AutoMapper;
using TicketingService.Core.DTOs.Categories;
using TicketingService.Core.DTOs.Comments;
using TicketingService.Core.DTOs.Complaints;
using TicketingService.Core.DTOs.Customers;
using TicketingService.Core.DTOs.KnowledgeBase;
using TicketingService.Core.DTOs.Macros;
using TicketingService.Core.DTOs.Satisfaction;
using TicketingService.Core.DTOs.SLA;
using TicketingService.Core.DTOs.Tags;
using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Entities;

namespace TicketingService.Core.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Ticket
        CreateMap<CreateTicketDto, Ticket>();
        CreateMap<Ticket, TicketReadDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty))
            .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null))
            .ForMember(dest => dest.CustomerCompany, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Company : null))
            .ForMember(dest => dest.ClientReference, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.ClientReference : null))
            .ForMember(dest => dest.AssigneeName, opt => opt.MapFrom(src => src.AssigneeName))
            .ForMember(dest => dest.Comments, opt => opt.MapFrom(src => src.Comments))
            .ForMember(dest => dest.History, opt => opt.MapFrom(src => src.History))
            .ForMember(dest => dest.Attachments, opt => opt.MapFrom(src => src.Attachments))
            .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.Select(tt => tt.Tag)))
            .ForMember(dest => dest.SatisfactionRating, opt => opt.MapFrom(src => src.SatisfactionRating));

        // Customer (D1)
        CreateMap<Customer, CustomerReadDto>();
        CreateMap<CreateCustomerDto, Customer>();

        // TicketHistory
        CreateMap<TicketHistory, TicketHistoryReadDto>();

        // TicketAttachment
        CreateMap<TicketAttachment, TicketAttachmentReadDto>();

        // Category
        CreateMap<CreateCategoryDto, TicketCategory>();
        CreateMap<TicketCategory, CategoryReadDto>();

        // Comment
        CreateMap<TicketComment, CommentReadDto>();

        // SLA
        CreateMap<SLAPolicy, SLAPolicyReadDto>();
        CreateMap<CreateSLAPolicyDto, SLAPolicy>();

        // EscalationRule
        CreateMap<EscalationRule, EscalationRuleReadDto>();
        CreateMap<CreateEscalationRuleDto, EscalationRule>();

        // Tag
        CreateMap<Tag, TagReadDto>();
        CreateMap<CreateTagDto, Tag>();

        // Macro
        CreateMap<Macro, MacroReadDto>();
        CreateMap<CreateMacroDto, Macro>();

        // Satisfaction
        CreateMap<TicketSatisfactionRating, SatisfactionRatingReadDto>();

        // D5 — complaint workflow steps
        CreateMap<ComplaintWorkflowStep, ComplaintStepReadDto>();

        // D7 — knowledge-base articles
        CreateMap<KnowledgeBaseArticle, KbArticleReadDto>();
    }
}
