using Asp.Versioning;
using StoreService.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using StoreService.Core.ServiceRegistration;
using StoreService.Infrastructure.Data;
using StoreService.Infrastructure.ServiceRegistration;
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
    Log.Information("Starting QaliCore StoreService...");

    var services = builder.Services;

    // #220 -- distributed tracing. Auto-instruments ASP.NET Core + HttpClient (covers every
    // AddHttpClient-registered typed client without touching handler code) and exports via OTLP
    // to the shared Jaeger instance (kubernetes/observability/).
    var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"]
        ?? "http://lante-jaeger-collector.new-erp.svc.cluster.local:4317";
    services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: "store-service"))
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

    // P5 (DEC-3) — outbound callback to Procurement (Module 4) when an LPO-linked GRN passes inspection.
    services.AddHttpClient("ProcurementService");
    services.AddScoped<StoreService.Core.Interfaces.Services.IProcurementReceiptGateway,
                       StoreService.Api.Services.ProcurementReceiptGateway>();

    // PostgreSQL
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection not configured.");

    var appConnection = builder.Configuration.GetConnectionString("AppConnection") ?? connectionString;

    services.AddSingleton<TenantDbConnectionInterceptor>();
    services.AddSingleton<StoreAuditInterceptor>();
    services.AddDbContext<StoreDbContext>((sp, options) =>
    {
        options.UseNpgsql(appConnection, sql =>
        {
            sql.MigrationsAssembly("StoreService.Infrastructure");
            sql.CommandTimeout(15);
            sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        });
        options.AddInterceptors(sp.GetRequiredService<TenantDbConnectionInterceptor>());
        options.AddInterceptors(sp.GetRequiredService<StoreAuditInterceptor>());
    });

    // Schema-per-tenant provisioning
    services.AddScoped<StoreService.Infrastructure.Services.ITenantProvisioningService,
                       StoreService.Infrastructure.Services.TenantProvisioningService>();

    // JWT (validates tokens issued by UserService)
    var jwksUrl = builder.Configuration["JWT:JwksUrl"] ?? builder.Configuration["JWT__JwksUrl"]
                  ?? "http://lante-user-service:8080/.well-known/jwks.json";
    var jwksResolver = new StoreService.Api.Authentication.JwtIssuerSigningKeyResolver(new HttpClient(), jwksUrl);

    var issuer   = builder.Configuration["JwtSettings:Issuer"]   ?? "LanteStoreService";
    var audience = builder.Configuration["JwtSettings:Audience"] ?? "LanteStoreService";

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

    services.AddSingleton<StoreService.Api.Services.LocalFileStorageService>();

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
            Title       = "QaliCore StoreService API",
            Version     = "v1",
            Description = "QaliCore — Stores, Inventory & Stock Management"
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

    app.UseMiddleware<StoreService.Api.Middleware.GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseHttpMetrics();
    app.UseCors("AllowAll");
    app.UseSwagger();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "QaliCore StoreService API V1");
            c.RoutePrefix = string.Empty;
        });
    }
    else
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "QaliCore StoreService API V1");
            c.RoutePrefix = "swagger";
        });
    }

    // Serve uploaded item photos at /uploads — mirrors fleet-service's LocalFileStorageService
    var uploadsPath = app.Configuration["Storage:BasePath"] ?? "/app/uploads";
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapMetrics();

    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Applying database migrations...");

        var migrationOptions = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql(connectionString, sql =>
            {
                sql.MigrationsAssembly("StoreService.Infrastructure");
                sql.CommandTimeout(60);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            })
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var migrationDb = new StoreDbContext(migrationOptions);
        await migrationDb.Database.MigrateAsync();
        logger.LogInformation("Migrations applied.");

        // Self-heal existing tenant schemas: MigrateAsync above only ever touches `public`, and
        // ITenantProvisioningService.ProvisionAsync otherwise only runs once, at tenant-creation
        // time — so a tenant schema provisioned before a later migration ships stays permanently
        // behind (missing columns/tables) until someone notices. ProvisionAsync's migrate/seed/grant
        // steps are all idempotent, so re-running it here on every startup for every existing
        // tenant_* schema is a no-op once a schema is current and self-heals it otherwise. (No store
        // tenant schemas exist in production yet — see project memory — but this closes the gap the
        // moment one is provisioned.)
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
            .GetRequiredService<StoreService.Infrastructure.Services.ITenantProvisioningService>();
        foreach (var schema in tenantSchemas)
        {
            var result = await provisioning.ProvisionAsync(schema);
            if (result.Success)
                logger.LogInformation("Tenant schema {Schema} up to date.", schema);
            else
                logger.LogError("Failed to re-migrate tenant schema {Schema}: {Error}", schema, result.Error);
        }
    }

    Log.Information("QaliCore StoreService started on {Urls}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "QaliCore StoreService terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
