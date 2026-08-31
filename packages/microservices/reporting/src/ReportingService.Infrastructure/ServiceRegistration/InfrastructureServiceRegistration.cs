using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportingService.Core.Interfaces.Repositories;
using ReportingService.Infrastructure.BackgroundServices;
using ReportingService.Infrastructure.Repositories;
using ReportingService.Infrastructure.Services;

namespace ReportingService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // One open-generic registration covers persistence for every Reporting-owned entity.
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<SystemTokenIssuer>();
        services.AddScoped<ReportGenerationService>();
        services.AddScoped<MetricResolverService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

        services.AddHostedService<ReportSchedulerBackgroundService>();
        services.AddHostedService<RedFlagEvaluationBackgroundService>();

        return services;
    }
}
