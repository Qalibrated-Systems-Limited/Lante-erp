using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;
using HrService.Core.Services;
using HrService.Infrastructure.Data;
using HrService.Infrastructure.Repositories;

namespace HrService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var appConnection = configuration.GetConnectionString("AppConnection")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddSingleton<TenantDbConnectionInterceptor>();
        services.AddDbContext<HrDbContext>((sp, options) =>
        {
            options.UseNpgsql(appConnection);
            options.AddInterceptors(sp.GetRequiredService<TenantDbConnectionInterceptor>());
        });

        // Generic repositories
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Domain services (registered per phase).
        services.AddScoped<IEmployeeService, EmployeeService>();   // H1 — employee master, documents, certifications
        services.AddScoped<IOrgService, OrgService>();              // H1 — positions + org chart (HR-DEC-3)
        services.AddScoped<IProbationService, ProbationService>();  // H2 — probation milestones + contract renewal
        services.AddScoped<ILeaveService, LeaveService>();          // H3 — leave config, entitlements, approvals, carry-forward
        services.AddScoped<IAttendanceService, AttendanceService>();// H4 — clock-in/out, absences, scorecards
        services.AddScoped<IPayrollService, PayrollService>();      // H5 — grades, structures, salaries, rates, deductions
        services.AddScoped<IPayrollRunService, PayrollRunService>();// H6 — overtime, payroll runs, payslips, finance journal
        services.AddScoped<IPayrollDocumentService, PayrollDocumentService>(); // H6 pass 2 — bank files, payment journal, P9
        services.AddScoped<ILearningService, LearningService>();    // H7 — LDPs, training hours, mandatory compliance, budgets
        services.AddScoped<ISalaryIncrementService, SalaryIncrementService>(); // H8 — increments, gated on H7
        services.AddScoped<IAppraisalService, AppraisalService>();  // H9 — KPI scorecards, appraisals, 360, PIPs
        services.AddScoped<IDisciplineService, DisciplineService>();// H10 — discipline, warnings, grievances, separation
        services.AddScoped<ICommissionService, CommissionService>();// H11 — commission bands, plans, statements, disputes
        services.AddScoped<IRecruitmentService, RecruitmentService>(); // H12 — requisitions → vacancies → applicants → offers → hire
        // The one answer to "is this a working day", shared by H3 leave and H4 attendance so they cannot drift.
        services.AddScoped<IWorkCalendar, WorkCalendar>();

        // Cross-module seams (config-gated on *Service:BaseUrl, ServiceToken + named HttpClient).
        // H1 — user-service: creates the new hire's login account (fail-open) and supplies the departments and
        // branches that module owns. Later phases add finance (H6), hse/compliance (H7) and crm (H11).
        services.AddHttpClient("UserService");
        services.AddScoped<IUserDirectoryGateway, HrService.Infrastructure.Services.HttpUserDirectoryGateway>();

        // H2 — alert delivery through ticketing's internal alert endpoint (ServiceKey, no JWT minting), the
        // same channel the compliance statutory calendar uses. Best-effort: the milestone rows are the record.
        services.AddHttpClient("TicketingService");
        services.AddScoped<IHrAlertGateway, HrService.Infrastructure.Services.HttpTicketingAlertGateway>();

        // H5 — finance: reads the chart of accounts so salary components, statutory rates and deduction types
        // map to real posting accounts (P7 step 7.5). Read-only — finance stays the ledger (HR-DEC-4); H6 adds
        // the payroll journal + statutory remittance writes.
        services.AddHttpClient("FinanceService");
        services.AddScoped<IFinanceGateway, HrService.Infrastructure.Services.HttpFinanceGateway>();

        // H7 — mandatory-training evidence read from the services that own it (HR-DEC-5). HR keeps the RULE,
        // never a copy of the record. A failed read reports "could not check", never "not done".
        services.AddHttpClient("HseService");
        services.AddHttpClient("ComplianceService");
        services.AddScoped<ITrainingEvidenceGateway, HrService.Infrastructure.Services.HttpTrainingEvidenceGateway>();

        // H11 — CRM owns the sales target and attainment (H11-DEC-1); HR reads them and owns only the
        // commission overlay. A failed read reports "could not read", never a zero target.
        services.AddHttpClient("CrmService");
        services.AddScoped<ICrmRevenueGateway, HrService.Infrastructure.Services.HttpCrmRevenueGateway>();

        // Background schedulers (per tenant schema, config-gated). One daily worker runs every HR sweep for a
        // tenant inside a single scope and search_path, rather than a worker per phase competing for them.
        services.AddHostedService<BackgroundServices.HrMilestoneBackgroundService>();   // H2 milestones + H3 leave

        return services;
    }
}
