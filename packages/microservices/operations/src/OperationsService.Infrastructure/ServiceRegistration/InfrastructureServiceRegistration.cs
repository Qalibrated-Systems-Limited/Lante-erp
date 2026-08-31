using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OperationsService.Core.Entities;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;
using OperationsService.Core.Services;
using OperationsService.Infrastructure.Data;
using OperationsService.Infrastructure.Repositories;

namespace OperationsService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var appConnection = configuration.GetConnectionString("AppConnection")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddSingleton<TenantDbConnectionInterceptor>();
        services.AddSingleton<OperationsAuditInterceptor>();
        services.AddDbContext<OperationsDbContext>((sp, options) =>
        {
            options.UseNpgsql(appConnection);
            options.AddInterceptors(
                sp.GetRequiredService<TenantDbConnectionInterceptor>(),
                sp.GetRequiredService<OperationsAuditInterceptor>());
        });

        // Generic repositories
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Domain services
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectScheduleService, ProjectScheduleService>();
        services.AddScoped<IProjectBudgetService, ProjectBudgetService>();
        // PR3 — RAID + change control (change requests re-baseline, so this depends on the budget service)
        services.AddScoped<IProjectGovernanceService, ProjectGovernanceService>();
        // PR3b — comment threads + mentions inbox
        services.AddScoped<IProjectCommentService, ProjectCommentService>();
        // PR4a — earned value / S-curve / portfolio (pure computation, no new tables)
        services.AddScoped<IProjectAnalyticsService, ProjectAnalyticsService>();
        // PR4b — templates + recurring work
        services.AddScoped<IProjectTemplateService, ProjectTemplateService>();
        services.AddScoped<IRecurringProjectService, RecurringProjectService>();
        // PR4c — board / calendar / workload projections (read-only, no new tables)
        services.AddScoped<IProjectBoardService, ProjectBoardService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IServiceReportService, ServiceReportService>();
        services.AddScoped<IFinancialService, FinancialService>();
        // #339 -- technician performance scorecards
        services.AddScoped<IPerformanceService, PerformanceService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IFieldVehicleService, FieldVehicleService>();

        // O5 — Service & Calibration Request pipeline (migrated from ticketing)
        services.AddScoped<IServiceRequestService, ServiceRequestService>();
        services.AddScoped<IQuotationEmailSender, OperationsService.Infrastructure.Services.LoggingQuotationEmailSender>();

        // O4 — Timesheets
        services.AddScoped<ITimesheetService, TimesheetService>();

        // O6 — Calibration reference-standard register
        services.AddScoped<IReferenceStandardService, ReferenceStandardService>();

        // O7 — Variation orders + subcontractor-compliance seam
        services.AddScoped<IVariationOrderService, VariationOrderService>();
        services.AddScoped<ISubcontractorComplianceGateway, OperationsService.Infrastructure.Services.NoOpSubcontractorComplianceGateway>();

        // O9 — Handover + negligence
        services.AddScoped<IProjectHandoverService, ProjectHandoverService>();
        services.AddScoped<INegligenceService, NegligenceService>();

        // O1/O3/O4 — cross-module seams (config-gated no-ops until the real clients are wired)
        // O1/O6 — REAL CRM seam: customer verify + calibration recall over HTTP. Config-gated on
        // Crm:Enabled + CrmService:BaseUrl; falls back to permissive no-op behaviour when unset.
        services.AddHttpClient("CrmService");
        services.AddScoped<ICrmCustomerDirectory, OperationsService.Infrastructure.Services.HttpCrmCustomerDirectory>();

        // PR2 — REAL Finance/HR seams. Registered only when the service is actually configured:
        // binding the HTTP client behind an unset BaseUrl would turn every seam call into a warning
        // and lose the stub's readable "[stub]" trace, so an unconfigured environment keeps the no-op.
        services.AddHttpClient("FinanceService");
        services.AddHttpClient("HrService");

        var financeWired = configuration.GetValue("Finance:Enabled", false)
                        && !string.IsNullOrWhiteSpace(configuration["FinanceService:BaseUrl"]);
        if (financeWired)
            services.AddScoped<IFinanceGateway, OperationsService.Infrastructure.Services.HttpFinanceGateway>();
        else
            services.AddScoped<IFinanceGateway, OperationsService.Infrastructure.Services.NoOpFinanceGateway>();

        var hrWired = configuration.GetValue("Hr:Enabled", false)
                   && !string.IsNullOrWhiteSpace(configuration["HrService:BaseUrl"]);
        if (hrWired)
            services.AddScoped<IHrGateway, OperationsService.Infrastructure.Services.HttpHrGateway>();
        else
            services.AddScoped<IHrGateway, OperationsService.Infrastructure.Services.NoOpHrGateway>();

        services.AddScoped<IHseGateway, OperationsService.Infrastructure.Services.NoOpHseGateway>();

        return services;
    }
}
