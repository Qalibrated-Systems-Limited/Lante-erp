using LicenseService.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LicenseService.Core.ServiceRegistration;

public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddScoped<ILicenseService, LicenseService.Core.Services.LicenseService>();
        return services;
    }
}
