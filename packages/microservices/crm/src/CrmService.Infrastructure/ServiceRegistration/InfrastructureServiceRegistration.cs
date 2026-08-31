using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;
using CrmService.Core.Services;
using CrmService.Infrastructure.Data;
using CrmService.Infrastructure.Repositories;

namespace CrmService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var appConnection = configuration.GetConnectionString("AppConnection")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddSingleton<TenantDbConnectionInterceptor>();
        services.AddSingleton<CrmAuditInterceptor>();
        services.AddDbContext<CrmDbContext>((sp, options) =>
        {
            options.UseNpgsql(appConnection);
            options.AddInterceptors(sp.GetRequiredService<TenantDbConnectionInterceptor>());
            options.AddInterceptors(sp.GetRequiredService<CrmAuditInterceptor>());
        });

        // Generic repositories
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Domain services (registered per phase).
        services.AddScoped<ICustomerService, CustomerService>();   // C1
        services.AddScoped<ILeadService, LeadService>();           // C2
        services.AddScoped<IOpportunityService, OpportunityService>();  // C3
        services.AddScoped<IQuotationService, QuotationService>();      // C4
        services.AddScoped<IDealService, DealService>();               // C5
        services.AddScoped<ITenderService, TenderService>();           // C6
        services.AddScoped<IActivityService, ActivityService>();       // C7
        services.AddScoped<ITransferService, TransferService>();       // C8
        services.AddScoped<IDashboardService, DashboardService>();     // C9
        services.AddScoped<IMarketingService, MarketingService>();     // C10
        services.AddScoped<IAfterSalesService, AfterSalesService>();   // C11
        services.AddScoped<ILegalService, LegalService>();            // C12
        services.AddScoped<IPaymentAlertService, PaymentAlertService>(); // C13
        services.AddScoped<ICrmIngestService, CrmIngestService>();       // D8-2/D8-3 ticketing ingest

        // C13 — real Finance-service reader (payment/debtor alerts). Named HttpClient + per-schema JWT.
        services.AddHttpClient("FinanceService");
        services.AddScoped<IFinanceReadClient, CrmService.Infrastructure.Services.FinanceReadClient>();

        // C5 — cross-module seams, both now REAL HTTP clients (config-gated on their *Service:BaseUrl,
        // falling back to a no-op result when unset). Deal close → Finance draft AR invoice; create-project
        // → Operations Draft PROJECT linked via Project.ClientId.
        services.AddScoped<IFinanceGateway, CrmService.Infrastructure.Services.FinanceInvoiceGateway>();
        services.AddScoped<IProjectGateway, CrmService.Infrastructure.Services.OperationsProjectGateway>();
        services.AddHttpClient("OperationsService");

        return services;
    }
}
