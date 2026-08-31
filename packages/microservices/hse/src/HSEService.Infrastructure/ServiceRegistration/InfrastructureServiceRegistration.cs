using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Infrastructure.BackgroundServices;
using HSEService.Infrastructure.Repositories;

namespace HSEService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddHealthChecks();
        services.AddMemoryCache();

        // One open-generic registration covers persistence for every HSE entity.
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Specific repositories layer a filtered/paged query on top of the generic CRUD for the
        // entities whose list endpoints filter (incidentId, siteId, employeeUserId, ...).
        services.AddScoped<ICorrectiveActionRepository, CorrectiveActionRepository>();
        services.AddScoped<IStatutoryInspectionRepository, StatutoryInspectionRepository>();
        services.AddScoped<IPpeIssueRepository, PpeIssueRepository>();
        services.AddScoped<IRamsRepository, RamsRepository>();
        services.AddScoped<IHseTrainingRecordRepository, HseTrainingRecordRepository>();

        services.AddHostedService<HseAlertsBackgroundService>();

        return services;
    }
}
