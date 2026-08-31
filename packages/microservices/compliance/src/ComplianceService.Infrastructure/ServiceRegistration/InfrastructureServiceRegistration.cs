using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.BackgroundServices;
using ComplianceService.Infrastructure.Repositories;
using ComplianceService.Infrastructure.Services;

namespace ComplianceService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddHealthChecks();
        services.AddMemoryCache();

        // One open-generic registration covers persistence for every Compliance entity.
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Entity-specific registrations layered on top, purely to back paginated GetAll
        // endpoints that need custom filtering and/or ordering the generic
        // GetPagedAsync(PaginationParameters) can't express. Controllers still use
        // IComplianceCrudService<T> (backed by the open generic above) for every other
        // action — these are additive, not a replacement.
        services.AddScoped<IGiftHospitalityRepository, GiftHospitalityRepository>();
        services.AddScoped<ICoiDeclarationRepository, CoiDeclarationRepository>();
        services.AddScoped<IDataSubjectRequestRepository, DataSubjectRequestRepository>();
        services.AddScoped<IDataBreachRepository, DataBreachRepository>();
        services.AddScoped<IRegulatoryLicenceRepository, RegulatoryLicenceRepository>();
        services.AddScoped<IRelatedPartyTransactionRepository, RelatedPartyTransactionRepository>();
        services.AddScoped<IBoardResolutionRepository, BoardResolutionRepository>();
        services.AddScoped<IStatutoryObligationRepository, StatutoryObligationRepository>();
        services.AddScoped<IStatutoryDeadlineRepository, StatutoryDeadlineRepository>();
        services.AddScoped<ITaxComplianceCertRepository, TaxComplianceCertRepository>();
        services.AddScoped<IAntiBriberyTrainingRepository, AntiBriberyTrainingRepository>();
        services.AddScoped<IWhistleblowerCaseRepository, WhistleblowerCaseRepository>();
        services.AddScoped<ICosecTaskRepository, CosecTaskRepository>();
        services.AddScoped<IAnnualReturnRepository, AnnualReturnRepository>();
        services.AddScoped<IRelatedPartyRepository, RelatedPartyRepository>();
        services.AddScoped<IIcsaRepository, IcsaRepository>();
        services.AddScoped<IIntercompanyTxnRepository, IntercompanyTxnRepository>();

        // Needs direct DbContext transaction access for atomic Code generation — see
        // ISopLibraryService's own doc comment for why this isn't just IComplianceCrudService<T>.
        services.AddScoped<ISopLibraryService, SopLibraryService>();

        services.AddHostedService<ComplianceAlertsBackgroundService>();
        services.AddHostedService<StatutoryCalendarBackgroundService>();

        return services;
    }
}
