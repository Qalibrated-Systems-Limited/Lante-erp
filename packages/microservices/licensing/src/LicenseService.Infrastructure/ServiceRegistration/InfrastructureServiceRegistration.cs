using LicenseService.Core.Interfaces.Repositories;
using LicenseService.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LicenseService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        return services;
    }
}
