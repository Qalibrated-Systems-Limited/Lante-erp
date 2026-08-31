using Microsoft.Extensions.DependencyInjection;
using TicketingService.Core.Interfaces.Services;
using TicketingService.Core.Services;

namespace TicketingService.Core.ServiceRegistration;

public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ITicketTransitionService, TicketTransitionService>();
        services.AddScoped<ITicketCategoryService, TicketCategoryService>();
        services.AddScoped<ISLAService, SLAService>();
        services.AddScoped<ITicketHistoryService, TicketHistoryService>();
        services.AddScoped<IEscalationService, EscalationService>();
        services.AddScoped<IComplaintWorkflowService, ComplaintWorkflowService>();   // D5
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();   // D7
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IMacroService, MacroService>();
        services.AddScoped<IWorkflowRuleService, WorkflowRuleService>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<ISatisfactionService, SatisfactionService>();
        services.AddScoped<IFollowUpService, FollowUpService>();
        services.AddScoped<ITicketAttachmentService, TicketAttachmentService>();

        return services;
    }
}
