using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;
using ProcurementService.Core.Services;
using ProcurementService.Infrastructure.Data;
using ProcurementService.Infrastructure.Repositories;

namespace ProcurementService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var appConnection = configuration.GetConnectionString("AppConnection")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddSingleton<TenantDbConnectionInterceptor>();
        services.AddDbContext<ProcurementDbContext>((sp, options) =>
        {
            options.UseNpgsql(appConnection);
            options.AddInterceptors(sp.GetRequiredService<TenantDbConnectionInterceptor>());
        });

        // Generic repositories
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Domain services (registered per phase).
        services.AddScoped<ISupplierService, SupplierService>();                       // P1 — Approved Supplier Register
        services.AddScoped<IPurchaseRequisitionService, PurchaseRequisitionService>(); // P2 — Purchase Requisition
        services.AddScoped<IQuotationService, QuotationService>();                     // P3 — Quotation & comparison
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();             // P4 — LPO generation & approval
        services.AddScoped<IThreeWayMatchService, ThreeWayMatchService>();             // P6 — 3-way match & payment handoff
        services.AddScoped<IInternationalPoService, InternationalPoService>();         // P7 — international sourcing & landed cost
        services.AddScoped<IEmergencyProcurementService, EmergencyProcurementService>(); // P8 — emergency procurement
        services.AddScoped<IPerformanceReviewService, PerformanceReviewService>();      // P9 — supplier performance review
        services.AddScoped<ISupplierSeedService, SupplierSeedService>();                // DEC-B — seed the ASR from finance/stores

        // Cross-module seams (config-gated on *Service:BaseUrl, ServiceToken + named HttpClient).
        // Finance seams: P2 budget check (real HTTP, fail-open) + P6 payables read / match write-back /
        // payment-voucher handoff (real HTTP, fail-CLOSED — they gate a payment).
        services.AddHttpClient("FinanceService");
        services.AddScoped<IBudgetGateway, ProcurementService.Infrastructure.Services.HttpFinanceBudgetGateway>();
        services.AddScoped<IInvoiceGateway, ProcurementService.Infrastructure.Services.HttpFinanceInvoiceGateway>();
        services.AddScoped<IPaymentVoucherGateway, ProcurementService.Infrastructure.Services.HttpFinanceVoucherGateway>();
        services.AddScoped<ICurrencyGateway, ProcurementService.Infrastructure.Services.HttpFinanceCurrencyGateway>();   // P7 FX

        // DEC-B — read-only reads of the pre-ASR supplier masters. Stores is read through its ordinary
        // /api/v1/suppliers endpoint (nothing in Stores is modified); each source degrades independently.
        services.AddHttpClient("StoresService");
        services.AddScoped<ISupplierSourceGateway, ProcurementService.Infrastructure.Services.HttpSupplierSourceGateway>();

        // DEC-C — verify an LPO's Board Resolution against Compliance's COMP-007 register (read-only).
        services.AddHttpClient("ComplianceService");
        services.AddScoped<IBoardResolutionGateway, ProcurementService.Infrastructure.Services.HttpComplianceBoardResolutionGateway>();

        // P9 — runs the biannual review per tenant schema once each half closes (config-gated).
        services.AddHostedService<BackgroundServices.PerformanceReviewBackgroundService>();

        return services;
    }
}
