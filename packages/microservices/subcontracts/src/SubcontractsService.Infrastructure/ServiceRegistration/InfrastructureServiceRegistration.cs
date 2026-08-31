using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubcontractsService.Core.Interfaces.Repositories;
using SubcontractsService.Infrastructure.BackgroundServices;
using SubcontractsService.Infrastructure.Repositories;

namespace SubcontractsService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddHealthChecks();
        services.AddMemoryCache();

        // One open-generic registration covers persistence for every Subcontracts entity.
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        services.AddHostedService<SubcontractsAlertsBackgroundService>();

        return services;
    }
}
