using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Repositories;
using TicketingService.Infrastructure.Services;

namespace TicketingService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddHealthChecks();
        services.AddMemoryCache();

        RegisterRepositories(services);

        services.AddHostedService<SLABackgroundService>();

        return services;
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ITicketCategoryRepository, TicketCategoryRepository>();
        services.AddScoped<ITicketHistoryRepository, TicketHistoryRepository>();
        services.AddScoped<ITicketCommentRepository, TicketCommentRepository>();
        services.AddScoped<ITicketEscalationRepository, TicketEscalationRepository>();
        services.AddScoped<ISLAPolicyRepository, SLAPolicyRepository>();
        services.AddScoped<IEscalationRuleRepository, EscalationRuleRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IComplaintWorkflowStepRepository, ComplaintWorkflowStepRepository>();   // D5
        services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();   // D7
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IMacroRepository, MacroRepository>();
        services.AddScoped<IWorkflowRuleRepository, WorkflowRuleRepository>();
        services.AddScoped<ISatisfactionRatingRepository, SatisfactionRatingRepository>();
        services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
        services.AddScoped<ITicketWatcherRepository, TicketWatcherRepository>();
        services.AddScoped<IGenericRepository<TicketAssignment>, GenericRepository<TicketAssignment>>();
    }
}
