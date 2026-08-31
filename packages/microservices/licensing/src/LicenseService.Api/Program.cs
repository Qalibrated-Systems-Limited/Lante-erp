using Asp.Versioning;
using LicenseService.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using LicenseService.Core.ServiceRegistration;
using LicenseService.Infrastructure.Data;
using LicenseService.Infrastructure.ServiceRegistration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using System.Text;

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
    Log.Information("Starting Lante LicenseService...");

    var services = builder.Services;

    // #220 -- distributed tracing. Auto-instruments ASP.NET Core + HttpClient (covers every
    // AddHttpClient-registered typed client without touching handler code) and exports via OTLP
    // to the shared Jaeger instance (kubernetes/observability/).
    var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"]
        ?? "http://lante-jaeger-collector.new-erp.svc.cluster.local:4317";
    services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: "license-service"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

    services.AddControllers();
    services.AddEndpointsApiExplorer();
    services.AddHttpContextAccessor();

    // API Versioning
    services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.ApiVersionReader  = new UrlSegmentApiVersionReader();
    }).AddMvc();

    // Core + Infrastructure
    services.AddCoreServices();
    services.AddInfrastructureServices();

    // PostgreSQL
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection not configured.");

    var appConnection = builder.Configuration.GetConnectionString("AppConnection") ?? connectionString;

    services.AddSingleton<LicenseService.Infrastructure.Data.TenantDbConnectionInterceptor>();
    services.AddSingleton<LicenseAuditInterceptor>();
    services.AddDbContext<LanteLicenseDbContext>((sp, options) =>
    {
        options.UseNpgsql(appConnection, sql =>
        {
            sql.MigrationsAssembly("LicenseService.Infrastructure");
            sql.CommandTimeout(15);
            sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        });
        options.AddInterceptors(sp.GetRequiredService<LicenseService.Infrastructure.Data.TenantDbConnectionInterceptor>());
        options.AddInterceptors(sp.GetRequiredService<LicenseAuditInterceptor>());
    });

    // Schema-per-tenant provisioning (Phase 2)
    services.AddScoped<LicenseService.Infrastructure.Services.ITenantProvisioningService,
                       LicenseService.Infrastructure.Services.TenantProvisioningService>();

    // Cross-service HTTP client — pushes into ticketing-service's central Alert store
    services.AddHttpClient("TicketingService");
    services.AddScoped<LicenseService.Core.Interfaces.Services.ITicketingServiceClient,
                       LicenseService.Api.Services.TicketingServiceClient>();
    services.AddHostedService<LicenseService.Infrastructure.BackgroundServices.LicenseExpiryBackgroundService>();

    // JWT (for protecting admin endpoints — validates tokens from UserService)
    var jwksUrl = builder.Configuration["JWT:JwksUrl"] ?? builder.Configuration["JWT__JwksUrl"]
                  ?? "http://lante-user-service:8080/.well-known/jwks.json";
    var jwksResolver = new LicenseService.Api.Authentication.JwtIssuerSigningKeyResolver(new HttpClient(), jwksUrl);

    var issuer   = builder.Configuration["JwtSettings:Issuer"]   ?? "LanteLicenseService";
    var audience = builder.Configuration["JwtSettings:Audience"] ?? "LanteLicenseService";

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeyResolver = jwksResolver.ResolveSigningKeys,
                ValidateIssuer   = true,
                ValidIssuer      = issuer,
                ValidateAudience = true,
                ValidAudience    = audience,
                ValidateLifetime = true,
                ClockSkew        = TimeSpan.Zero,
            };
        });

    services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    services.AddAuthorization();

    // CORS — allow client apps to call /validate
    services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials());
    });

    services.AddHealthChecks();

    // Swagger
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = "Lante LicenseService API",
            Version     = "v1",
            Description = "Lante — Software License Issuance, Validation & Revocation"
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Format: \"Bearer {token}\"",
            Name        = "Authorization",
            In          = ParameterLocation.Header,
            Type        = SecuritySchemeType.Http,
            Scheme      = "bearer",
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

    app.UseMiddleware<LicenseService.Api.Middleware.GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseHttpMetrics();
    app.UseCors("AllowAll");
    app.UseSwagger();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Lante LicenseService API V1");
            c.RoutePrefix = string.Empty;
        });
    }
    else
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "Lante LicenseService API V1");
            c.RoutePrefix = "swagger";
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapMetrics();

    // Run migrations using lante_user (BYPASSRLS) so RLS policies don't block schema changes
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Applying database migrations...");

        var migrationOptions = new DbContextOptionsBuilder<LanteLicenseDbContext>()
            .UseNpgsql(connectionString, sql =>
            {
                sql.MigrationsAssembly("LicenseService.Infrastructure");
                sql.CommandTimeout(60);
                // Model is now schema-agnostic; the existing migration history lives in public
                // (EF's default location — unaffected by the old HasDefaultSchema("licensing")).
                // Keep looking there so startup Migrate is a no-op, not a re-apply.
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            })
            // schema-agnostic model vs "licensing"-pinned snapshot is the intended difference.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var migrationDb = new LanteLicenseDbContext(migrationOptions);
        await migrationDb.Database.MigrateAsync();
        logger.LogInformation("Migrations applied.");

        // Self-heal existing tenant schemas: MigrateAsync above only ever touches `public`, and
        // ITenantProvisioningService.ProvisionAsync otherwise only runs once, at tenant-creation
        // time — so a tenant schema provisioned before a later migration ships stays permanently
        // behind (missing columns/tables) until someone notices. ProvisionAsync's migrate/seed/grant
        // steps are all idempotent, so re-running it here on every startup for every existing
        // tenant_* schema is a no-op once a schema is current and self-heals it otherwise.
        logger.LogInformation("Re-migrating existing tenant schemas...");
        var tenantSchemas = new List<string>();
        await using (var conn = new Npgsql.NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(
                "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'tenant\\_%' ESCAPE '\\'", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tenantSchemas.Add(reader.GetString(0));
        }

        var provisioning = scope.ServiceProvider
            .GetRequiredService<LicenseService.Infrastructure.Services.ITenantProvisioningService>();
        foreach (var schema in tenantSchemas)
        {
            var result = await provisioning.ProvisionAsync(schema);
            if (result.Success)
                logger.LogInformation("Tenant schema {Schema} up to date.", schema);
            else
                logger.LogError("Failed to re-migrate tenant schema {Schema}: {Error}", schema, result.Error);
        }
    }

    Log.Information("Lante LicenseService started on {Urls}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Lante LicenseService terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
