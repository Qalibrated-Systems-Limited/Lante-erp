using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using ReportingService.Api.Authorization;
using ReportingService.Api.Middleware;
using ReportingService.Core.Entities;
using ReportingService.Core.Enums;
using ReportingService.Core.Interfaces;
using ReportingService.Core.Interfaces.Services;
using ReportingService.Core.ServiceRegistration;
using ReportingService.Infrastructure.Data;
using ReportingService.Infrastructure.ServiceClients;
using ReportingService.Infrastructure.ServiceRegistration;
using ReportingService.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

try
{
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] TraceId={TraceId} {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    builder.Host.UseSerilog();
    Log.Information("Starting QaliCore ReportingService...");

    var services = builder.Services;

    // #220 -- distributed tracing. Auto-instruments ASP.NET Core + HttpClient (covers every
    // AddHttpClient-registered typed client without touching handler code) and exports via OTLP
    // to the shared Jaeger instance (kubernetes/observability/).
    var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"]
        ?? "http://lante-jaeger-collector.new-erp.svc.cluster.local:4317";
    services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: "reporting-service"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

    services.AddControllers();
    services.AddEndpointsApiExplorer();
    services.AddHttpContextAccessor();

    services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    }).AddMvc();

    // Named HttpClients for each upstream — no BaseAddress here; each *ServiceClient reads its own
    // <Name>:BaseUrl from IConfiguration at call time and string-concatenates the request path.
    services.AddHttpClient("FinanceService");
    services.AddHttpClient("OperationsService");
    services.AddHttpClient("FleetService");
    services.AddHttpClient("StoreService");
    services.AddHttpClient("HseService");
    services.AddHttpClient("ComplianceService");
    // The eighth. Its absence is what blocked reports #12 and #13 (#225).
    services.AddHttpClient("HrService");

    // Upstream service clients (forward the caller's JWT — see BaseServiceClient).
    services.AddScoped<IFinanceServiceClient, FinanceServiceClient>();
    services.AddScoped<IOperationsServiceClient, OperationsServiceClient>();
    services.AddScoped<IFleetServiceClient, FleetServiceClient>();
    services.AddScoped<IStoreServiceClient, StoreServiceClient>();
    services.AddScoped<IHseServiceClient, HseServiceClient>();
    services.AddScoped<IComplianceServiceClient, ComplianceServiceClient>();
    services.AddScoped<IHrServiceClient, HrServiceClient>();

    // Report aggregation/orchestration services.
    services.AddScoped<IManagementAccountsReportService, ManagementAccountsReportService>();
    services.AddScoped<IBudgetVarianceReportService, BudgetVarianceReportService>();
    services.AddScoped<IRevenueVsTargetReportService, RevenueVsTargetReportService>();
    services.AddScoped<IAgedDebtorsReportService, AgedDebtorsReportService>();
    services.AddScoped<ICashFlowForecastReportService, CashFlowForecastReportService>();
    services.AddScoped<IProjectProfitabilityReportService, ProjectProfitabilityReportService>();
    services.AddScoped<IKpiScorecardProgressReportService, KpiScorecardProgressReportService>();
    services.AddScoped<IFixedAssetRegisterReportService, FixedAssetRegisterReportService>();
    services.AddScoped<IPayrollSummaryReportService, PayrollSummaryReportService>();
    services.AddScoped<ILeaveBalanceReportService, LeaveBalanceReportService>();
    services.AddScoped<IFleetCostUtilisationReportService, FleetCostUtilisationReportService>();
    services.AddScoped<IProcurementSpendReportService, ProcurementSpendReportService>();
    services.AddScoped<IHseIncidentsTrirReportService, HseIncidentsTrirReportService>();
    services.AddScoped<IComplianceDashboardReportService, ComplianceDashboardReportService>();

    // RPT-001..005: report definitions/schedules/recipients/runs — the first real persistence
    // this service has had (see ReportingDbContext for why it's schema-per-tenant like Compliance).
    services.AddCoreServices();
    services.AddInfrastructureServices(builder.Configuration);
    services.AddScoped<ITicketingServiceClient, ReportingService.Api.Services.TicketingServiceClient>();
    services.AddHttpClient("TicketingService");

    var appConnection = builder.Configuration.GetConnectionString("AppConnection")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Database connection string not configured.");
    var migrationConnection = builder.Configuration.GetConnectionString("DefaultConnection") ?? appConnection;

    services.AddSingleton<TenantDbConnectionInterceptor>();
    services.AddDbContext<ReportingDbContext>((sp, options) =>
    {
        options.UseNpgsql(appConnection, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("ReportingService.Infrastructure");
            sqlOptions.CommandTimeout(15);
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
        });
        options.AddInterceptors(sp.GetRequiredService<TenantDbConnectionInterceptor>());
    });

    // JWT validation only — tokens are issued by user-service.
    var issuer = builder.Configuration["JwtSettings:Issuer"]
                 ?? builder.Configuration["JWT:Issuer"]
                 ?? builder.Configuration["JWT__Issuer"]
                 ?? "LanteUserService";

    var audience = builder.Configuration["JwtSettings:Audience"]
                   ?? builder.Configuration["JWT:Audience"]
                   ?? builder.Configuration["JWT__Audience"]
                   ?? "LanteUserService";

    var jwksUrl = builder.Configuration["JWT:JwksUrl"] ?? builder.Configuration["JWT__JwksUrl"]
                  ?? "http://lante-user-service:8080/.well-known/jwks.json";
    var jwksResolver = new ReportingService.Api.Authentication.JwtIssuerSigningKeyResolver(new HttpClient(), jwksUrl);

    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = jwksResolver.ResolveSigningKeys,
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("JWT authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });

    services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .Build();
    });

    services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    services.AddHealthChecks();

    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "QaliCore ReportingService API",
            Version = "v1",
            Description = "QaliCore — cross-service reporting, aggregation, and scheduled delivery"
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Format: \"Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseHttpMetrics();
    app.UseCors("AllowAll");

    app.UseSwagger();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "QaliCore ReportingService API V1");
            c.RoutePrefix = string.Empty;
            c.DocumentTitle = "QaliCore ReportingService API (Dev)";
        });
    }
    else
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "QaliCore ReportingService API V1");
            c.RoutePrefix = "swagger";
            c.DocumentTitle = "QaliCore ReportingService API";
        });
    }

    // Serve rendered report files at /uploads — mirrors Fleet's storage:link convention.
    var uploadsPath = builder.Configuration["Storage:BasePath"] ?? "/app/uploads";
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapMetrics();

    using (var scope = app.Services.CreateScope())
    {
        var appLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        appLogger.LogInformation("Applying database migrations...");

        var migrationOptions = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseNpgsql(migrationConnection, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly("ReportingService.Infrastructure");
                sqlOptions.CommandTimeout(60);
            })
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var migrationContext = new ReportingDbContext(migrationOptions);
        await migrationContext.Database.MigrateAsync();
        appLogger.LogInformation("Migrations applied successfully.");

        // Self-heals qalicore_app's table/sequence grants on every startup — the cluster's initdb
        // script (values.yaml's create-databases-and-app-role.sh) only runs once, on a first-ever
        // empty Postgres volume, so it never re-grants after a migration adds a table to an
        // already-running cluster. Idempotent (GRANT/ALTER DEFAULT PRIVILEGES are no-ops if
        // already applied), so this is safe to run on every single boot, not just once.
        await EnsureAppRoleGrantsAsync(migrationContext, builder.Configuration, appLogger);

        await SeedReportDefinitionsAsync(migrationContext, appLogger);

        // Self-heal existing tenant schemas: MigrateAsync above only ever touches `public`, and
        // ITenantProvisioningService.ProvisionAsync otherwise only runs once, at tenant-creation
        // time — so a tenant schema provisioned before a later migration ships stays permanently
        // behind (missing columns/tables) until someone notices. ProvisionAsync's migrate/seed/grant
        // steps are all idempotent, so re-running it here on every startup for every existing
        // tenant_* schema is a no-op once a schema is current and self-heals it otherwise. (No
        // reporting tenant schemas exist in production yet — see project memory — but this closes
        // the gap the moment one is provisioned.)
        appLogger.LogInformation("Re-migrating existing tenant schemas...");
        var tenantSchemas = new List<string>();
        await using (var conn = new Npgsql.NpgsqlConnection(migrationConnection))
        {
            await conn.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(
                "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'tenant\\_%' ESCAPE '\\'", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tenantSchemas.Add(reader.GetString(0));
        }

        var provisioning = scope.ServiceProvider
            .GetRequiredService<ReportingService.Infrastructure.Services.ITenantProvisioningService>();
        foreach (var schema in tenantSchemas)
        {
            var result = await provisioning.ProvisionAsync(schema);
            if (result.Success)
                appLogger.LogInformation("Tenant schema {Schema} up to date.", schema);
            else
                appLogger.LogError("Failed to re-migrate tenant schema {Schema}: {Error}", schema, result.Error);
        }
    }

    Log.Information("QaliCore ReportingService started successfully.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "QaliCore ReportingService terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// The cluster's initdb script (values.yaml's create-databases-and-app-role.sh) grants
// qalicore_app SELECT/INSERT/UPDATE/DELETE + default privileges for future tables, but only runs
// once, on a first-ever empty Postgres data volume — a migration that adds a table to an
// already-running cluster (i.e. every normal deploy after day one) never gets re-granted, and
// qalicore_app's queries start failing with Postgres 42501 "permission denied" while the pod
// itself still reports healthy (this exact incident happened 2026-07-13). Running this on every
// startup, on every schema actually in use (public + every provisioned tenant), makes the service
// self-healing regardless of how or when its database was created — GRANT/ALTER DEFAULT
// PRIVILEGES are idempotent, so this is a safe no-op once permissions are already correct.
static async Task EnsureAppRoleGrantsAsync(ReportingDbContext db, IConfiguration config, Microsoft.Extensions.Logging.ILogger logger)
{
    var appRole = config["ProvisioningAppRole"] ?? "qalicore_app";

    var schemas = await db.Database
        .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
        .ToListAsync();
    schemas.Add("public");

    foreach (var schema in schemas)
    {
        try
        {
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (plus the literal "public"); appRole comes from config, matching TenantProvisioningService's GrantAppRoleAsync convention — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await db.Database.ExecuteSqlRawAsync($"GRANT USAGE ON SCHEMA \"{schema}\" TO {appRole}");
            await db.Database.ExecuteSqlRawAsync($"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"{schema}\" TO {appRole}");
            await db.Database.ExecuteSqlRawAsync($"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA \"{schema}\" TO {appRole}");
            await db.Database.ExecuteSqlRawAsync($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schema}\" GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {appRole}");
            await db.Database.ExecuteSqlRawAsync($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schema}\" GRANT USAGE, SELECT ON SEQUENCES TO {appRole}");
#pragma warning restore EF1002
        }
        catch (Exception ex)
        {
            // Best-effort: if the app role or schema doesn't exist yet for some reason, log and
            // move on rather than blocking startup — matches TenantProvisioningService's
            // GrantAppRoleAsync convention for the same class of grant.
            logger.LogWarning(ex, "Failed to grant {Role} on schema {Schema}; queries against it may fail with permission errors.", appRole, schema);
        }
    }
}

// RPT-001: seeds the catalog of reports ReportsController already knows how to build — run once
// per schema (public plus every provisioned tenant) so ReportSchedulesController has something to
// point a schedule at. Idempotent: only inserts keys not already present.
static async Task SeedReportDefinitionsAsync(ReportingDbContext db, Microsoft.Extensions.Logging.ILogger logger)
{
    var seeds = new (string Key, string Name, ReportCategory Category)[]
    {
        ("management-accounts", "Management Accounts", ReportCategory.Finance),
        ("budget-variance", "Budget Variance", ReportCategory.Finance),
        ("aged-debtors", "Aged Debtors", ReportCategory.Finance),
        ("cash-flow-forecast", "Cash Flow Forecast", ReportCategory.Finance),
        ("project-profitability", "Project Profitability", ReportCategory.Operations),
        ("fleet-cost-utilisation", "Fleet Cost & Utilisation", ReportCategory.Fleet),
        ("procurement-spend", "Procurement Spend", ReportCategory.Stores),
        ("hse-incidents-trir", "HSE Incidents & TRIR", ReportCategory.Hse),
        ("compliance-dashboard", "Compliance Dashboard", ReportCategory.Compliance),
        // #10 (#225). Operations rather than a new category: it reports on how the business is tracking
        // against its own targets, and adding a category for one report would leave the others uneven.
        ("kpi-scorecard-progress", "KPI Scorecard Progress", ReportCategory.Operations),
        // #11 (#225). Finance: the register is a balance-sheet artefact and the depreciation charge is a
        // P&L one, so it belongs with the other finance reports rather than under Operations.
        ("fixed-asset-register", "Fixed Asset Register & Depreciation Schedule", ReportCategory.Finance),
        // #12 and #13 (#225). Finance rather than a new HR category: payroll cost and accrued leave are
        // both P&L and balance-sheet facts, and the audience is the same as the other cost reports.
        ("payroll-summary", "Payroll Summary & Cost Report", ReportCategory.Finance),
        ("leave-balance", "Leave Balance Report", ReportCategory.Finance),
    };

    var schemas = await db.Database
        .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
        .ToListAsync();
    schemas.Add("public");

    // RPT-006/007: one headline DataSource + ReportSource join per report, keyed to the same
    // report Key. MetricKey must match MetricResolverService's extractor dictionary exactly.
    var dataSourceSeeds = new (string ReportKey, string MetricKey, string Name, string Description)[]
    {
        ("management-accounts", "management-accounts.net-profit", "Net Profit", "Net profit for the period from the P&L."),
        // The departmental roll-up, deliberately excluding the company-wide target — a scorecard
        // tracking both would be measuring the same revenue twice.
        ("revenue-vs-target", "revenue-vs-target.department-variance", "Departmental Revenue Variance", "Departmental revenue actual less target, company-wide excluded."),
        ("budget-variance", "budget-variance.total-variance", "Total Budget Variance", "Sum of Actual minus Annual budget across all lines."),
        ("aged-debtors", "aged-debtors.total-overdue", "Total Overdue Receivables", "Sum of the 1-30/31-60/61+ day aging buckets across all customers."),
        ("cash-flow-forecast", "cash-flow-forecast.lowest-projected-balance", "Lowest Projected Cash Balance", "The lowest projected balance across the forecast horizon."),
        ("project-profitability", "project-profitability.utilization-percent", "Portfolio Budget Utilisation %", "Portfolio-wide budget utilisation percentage."),
        ("fleet-cost-utilisation", "fleet-cost-utilisation.total-profit", "Fleet Total Profit", "Total profit (revenue minus cost) across the fleet."),
        ("procurement-spend", "procurement-spend.grand-total", "Total Procurement Spend", "Grand total landed cost across all GRNs in range."),
        ("hse-incidents-trir", "hse-incidents-trir.trir", "TRIR", "Total Recordable Incident Rate."),
        ("compliance-dashboard", "compliance-dashboard.licences-expired", "Expired Regulatory Licences", "Count of regulatory licences currently expired."),
        // Deliberately the count of scorecards NOT on target, not a percentage: a percentage of a
        // handful of scorecards swings wildly on one change, and "3 off target" is what someone acts on.
        ("kpi-scorecard-progress", "kpi-scorecard-progress.off-target-count", "Scorecards Off Target", "Scorecards currently in warning or critical."),
        // Net book value, not acquisition cost: NBV is what the balance sheet carries and what a
        // scorecard target would sensibly be set against.
        ("fixed-asset-register", "fixed-asset-register.net-book-value", "Fixed Asset Net Book Value", "Total net book value of the fixed asset register."),
        // Cost of employment, not gross: gross understates it by the employer's own contributions.
        ("payroll-summary", "payroll-summary.cost-of-employment", "Total Cost of Employment", "Gross pay plus employer contributions across approved runs."),
        // Untaken days are an accrued liability, so this is the metric worth a target rather than days taken.
        ("leave-balance", "leave-balance.days-remaining", "Untaken Leave Days", "Total leave days accrued and not yet taken."),
    };

    foreach (var schema in schemas)
    {
        try
        {
            // Npgsql resets session state (including search_path) when a connection is returned
            // to the pool — must hold the connection open across the SET and the queries that
            // follow, same as ComplianceAlertsBackgroundService/ReportSchedulerBackgroundService.
            await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (plus the literal "public") above, not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002
            var existingKeys = await db.ReportDefinitions.Select(d => d.Key).ToListAsync();

            foreach (var seed in seeds)
            {
                if (existingKeys.Contains(seed.Key)) continue;
                db.ReportDefinitions.Add(new ReportDefinition { Key = seed.Key, Name = seed.Name, Category = seed.Category });
            }
            await db.SaveChangesAsync();

            var definitionsByKey = await db.ReportDefinitions.ToDictionaryAsync(d => d.Key, d => d);
            var existingMetricKeys = await db.DataSources.Select(s => s.MetricKey).ToListAsync();

            foreach (var seed in dataSourceSeeds)
            {
                if (existingMetricKeys.Contains(seed.MetricKey)) continue;
                if (!definitionsByKey.TryGetValue(seed.ReportKey, out var definition)) continue;

                var dataSource = new DataSource
                {
                    Name = seed.Name,
                    ModuleName = definition.Category,
                    MetricKey = seed.MetricKey,
                    Description = seed.Description,
                };
                db.DataSources.Add(dataSource);
                db.ReportSources.Add(new ReportSource { ReportDefinitionId = definition.Id, DataSourceId = dataSource.Id });
            }
            await db.SaveChangesAsync();

            await db.Database.CloseConnectionAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed seeding report definitions for schema {Schema}", schema);
        }
    }
}

public partial class Program { }
