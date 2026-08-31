using Microsoft.Extensions.DependencyInjection;

namespace ReportingService.Core.ServiceRegistration;

public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(Interfaces.Services.IReportingCrudService<>), typeof(Services.ReportingCrudService<>));
        return services;
    }
}
