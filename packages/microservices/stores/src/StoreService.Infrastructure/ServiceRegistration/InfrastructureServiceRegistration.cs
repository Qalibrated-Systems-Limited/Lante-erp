using StoreService.Core.Interfaces.Repositories;
using StoreService.Infrastructure.Repositories;
using StoreService.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace StoreService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // One generic repository, reused for every entity in this service.
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        return services;
    }
}
