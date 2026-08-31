using Microsoft.Extensions.DependencyInjection;

namespace SubcontractsService.Core.ServiceRegistration;

public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(Interfaces.Services.ISubcontractsCrudService<>), typeof(Services.SubcontractsCrudService<>));
        services.AddScoped<Interfaces.Services.IPrequalificationWorkflowService, Services.PrequalificationWorkflowService>();
        services.AddScoped<Interfaces.Services.IScorecardWorkflowService, Services.ScorecardWorkflowService>();
        services.AddScoped<Interfaces.Services.IAwardWorkflowService, Services.AwardWorkflowService>();
        return services;
    }
}
