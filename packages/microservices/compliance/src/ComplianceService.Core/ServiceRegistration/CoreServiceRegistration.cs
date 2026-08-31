using Microsoft.Extensions.DependencyInjection;
using ComplianceService.Core.Interfaces.Services;
using ComplianceService.Core.Services;

namespace ComplianceService.Core.ServiceRegistration;

public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // One open-generic registration covers CRUD for every Compliance entity.
        services.AddScoped(typeof(IComplianceCrudService<>), typeof(ComplianceCrudService<>));

        services.AddScoped<IComplianceDashboardService, ComplianceDashboardService>();
        services.AddScoped<IStatutoryDashboardService, StatutoryDashboardService>();
        services.AddScoped<IQualityDashboardService, QualityDashboardService>();

        return services;
    }
}
