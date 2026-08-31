using System.Security.Claims;
using System.Text;
using FinanceService.Api.Middleware;
using FinanceService.Core.Interfaces;
using FinanceService.Infrastructure.Data;
using FinanceService.Infrastructure.External;
using FinanceService.Infrastructure.Services;
using FinanceService.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Prometheus;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;

// Npgsql legacy timestamp behaviour: treat Unspecified DateTimes as UTC for timestamptz columns.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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
    Log.Information("Starting QaliCore FinanceService...");

    var services = builder.Services;

    // #220 -- distributed tracing. Auto-instruments ASP.NET Core + HttpClient (covers every
    // AddHttpClient-registered typed client without touching handler code) and exports via OTLP
    // to the shared Jaeger instance (kubernetes/observability/).
    var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"]
        ?? "http://lante-jaeger-collector.new-erp.svc.cluster.local:4317";
    services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: "finance-service"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));
    services.AddControllers();
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();
    services.AddHttpContextAccessor();
    services.AddHealthChecks();

    services.AddApiVersioning(o =>
    {
        o.AssumeDefaultVersionWhenUnspecified = true;
        o.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        o.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    }).AddMvc();

    var connString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection not configured.");
    // Least-privilege runtime connection; DefaultConnection (admin) is reserved for provisioning/migrations.
    var appConnString = builder.Configuration.GetConnectionString("AppConnection") ?? connString;

    // Runtime context is schema-agnostic (TenantFinanceDbContext); the interceptor binds search_path
    // from the JWT `schema` claim per request. Exposed as FinanceDbContext for services/controllers.
    services.AddSingleton<TenantDbConnectionInterceptor>();
    // Singleton like its sibling: it holds only IHttpContextAccessor, which is itself a singleton that
    // resolves the CURRENT request each time it is read. Registering it scoped would also work, but a
    // scoped interceptor resolved from the DbContext's provider is a subtler lifetime to reason about.
    services.AddSingleton<FinanceAuditInterceptor>();
    services.AddDbContext<TenantFinanceDbContext>((sp, options) =>
    {
        options.UseNpgsql(appConnString, o =>
        {
            o.MigrationsAssembly("FinanceService.Infrastructure");
            o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        });
        options.AddInterceptors(sp.GetRequiredService<TenantDbConnectionInterceptor>());
        // Every write to finance is recorded. FinanceAuditLog existed from InitialCreate and had never
        // held a row — see FinanceAuditInterceptor for why this is an interceptor and not a call at
        // each site (#285).
        options.AddInterceptors(sp.GetRequiredService<FinanceAuditInterceptor>());
    });
    services.AddScoped<FinanceDbContext>(sp => sp.GetRequiredService<TenantFinanceDbContext>());

    // Schema-per-tenant provisioning (called by user-service's onboarding orchestrator).
    services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

    services.AddScoped<IJournalService, JournalService>();
    services.AddScoped<IInvoiceService, InvoiceService>();
    services.AddScoped<IReceiptService, ReceiptService>();
    services.AddScoped<ISupplierInvoiceService, SupplierInvoiceService>();
    services.AddScoped<IPaymentVoucherService, PaymentVoucherService>();
    services.AddScoped<IBudgetService, BudgetService>();
    services.AddScoped<IMonthEndService, MonthEndService>();
    services.AddScoped<ICashFlowService, CashFlowService>();
    services.AddScoped<IImprestService, ImprestService>();
    services.AddScoped<IBankRecService, BankRecService>();
    services.AddScoped<IStatutoryService, StatutoryService>();
    services.AddSingleton<IApprovalAuthorityService, ApprovalAuthorityService>();
    services.AddScoped<IVatService, VatService>();
    services.AddSingleton<IExchangeRateProvider, StubExchangeRateProvider>();
    services.AddSingleton<IEtimsProvider, StubEtimsProvider>();

    services.AddScoped<IFixedAssetService, FixedAssetService>();
    services.AddScoped<IDepreciationService, DepreciationService>();
    services.AddScoped<IAssetDisposalService, AssetDisposalService>();
    services.AddHostedService<FinanceService.Infrastructure.BackgroundServices.MonthlyDepreciationBackgroundService>();

    // JWT — tokens issued by user-service.
    var issuer = builder.Configuration["JwtSettings:Issuer"] ?? builder.Configuration["JWT:Issuer"] ?? "LanteUserService";
    var audience = builder.Configuration["JwtSettings:Audience"] ?? builder.Configuration["JWT:Audience"] ?? "LanteUserService";
    var jwksUrl = builder.Configuration["JWT:JwksUrl"] ?? "http://lante-user-service:8080/.well-known/jwks.json";
    var jwksResolver = new FinanceService.Api.Authentication.JwtIssuerSigningKeyResolver(new HttpClient(), jwksUrl);

    services.AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.SaveToken = true;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true, IssuerSigningKeyResolver = jwksResolver.ResolveSigningKeys,
            ValidateIssuer = true, ValidIssuer = issuer,
            ValidateAudience = true, ValidAudience = audience,
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.NameIdentifier, RoleClaimType = ClaimTypes.Role,
        };
    });
    // Without these two, every [Authorize(Policy = "finance.…")] on a controller would resolve through
    // the DEFAULT policy provider — which knows nothing about permissions and would let any
    // authenticated user through, silently. The attributes would read as protection and enforce
    // nothing, which is the failure this whole change exists to remove (#277, and the pattern in #276).
    services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    services.AddAuthorization();

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapMetrics();

    // Self-heal existing tenant schemas on every startup: ProvisionAsync's migrate/seed/grant steps
    // are all idempotent, so re-running it here for every existing tenant_* schema is a no-op once a
    // schema is current and self-heals it otherwise (a schema provisioned before a later migration
    // ships would otherwise stay permanently behind). Same pattern as every other schema-per-tenant
    // service in this repo (see crm-service's Program.cs). Also covers the pre-existing tenant_qsl
    // schema that predates this service being wired into the platform onboarding orchestrator.
    using (var scope = app.Services.CreateScope())
    {
        var appLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        appLogger.LogInformation("Re-migrating existing finance tenant schemas...");

        var tenantSchemas = new List<string>();
        await using (var conn = new NpgsqlConnection(connString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'tenant\\_%' ESCAPE '\\'", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tenantSchemas.Add(reader.GetString(0));
        }

        var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
        foreach (var schema in tenantSchemas)
        {
            var result = await provisioning.ProvisionAsync(schema);
            if (result.Success)
                appLogger.LogInformation("Finance tenant schema {Schema} up to date.", schema);
            else
                appLogger.LogError("Failed to re-migrate finance tenant schema {Schema}: {Error}", schema, result.Error);
        }
    }

    Log.Information("QaliCore FinanceService started.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "FinanceService terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
