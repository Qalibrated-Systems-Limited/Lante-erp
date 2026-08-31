using Microsoft.Extensions.DependencyInjection;
using HSEService.Core.Interfaces.Services;
using HSEService.Core.Services;

namespace HSEService.Core.ServiceRegistration;

public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // One open-generic registration covers CRUD for every HSE entity — see IHseCrudService<T>.
        services.AddScoped(typeof(IHseCrudService<>), typeof(HseCrudService<>));

        // Multi-entity business processes built on top of the generic CRUD service.
        services.AddScoped<IHseIncidentWorkflowService, HseIncidentWorkflowService>();
        services.AddScoped<IRamsWorkflowService, RamsWorkflowService>();
        services.AddScoped<IToolboxTalkWorkflowService, ToolboxTalkWorkflowService>();
        services.AddScoped<IHseDashboardService, HseDashboardService>();

        return services;
    }
}
