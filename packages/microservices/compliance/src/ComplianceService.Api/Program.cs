using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using System.Security.Claims;
using System.Text;
using ComplianceService.Api.Authorization;
using ComplianceService.Api.Middleware;
using ComplianceService.Core.Interfaces.Services;
using ComplianceService.Core.ServiceRegistration;
using ComplianceService.Infrastructure.Data;
using ComplianceService.Infrastructure.ServiceRegistration;

// Npgsql 6+ requires all DateTime values to have Kind=Utc for timestamptz columns.
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
    Log.Information("Starting QaliCore ComplianceService...");

    var services = builder.Services;

    // #220 -- distributed tracing. Auto-instruments ASP.NET Core + HttpClient (covers every
    // AddHttpClient-registered typed client without touching handler code) and exports via OTLP
    // to the shared Jaeger instance (kubernetes/observability/).
    var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"]
        ?? "http://lante-jaeger-collector.new-erp.svc.cluster.local:4317";
    services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: "compliance-service"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

    services.AddControllers();
    services.AddEndpointsApiExplorer();
    services.AddOutputCache();
    services.AddHttpContextAccessor();

    services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    }).AddMvc();

    // Cross-service HTTP client — pushes compliance deadline/renewal alerts into ticketing's shared Alerts table.
    services.AddHttpClient("TicketingService");
    services.AddScoped<ITicketingServiceClient, ComplianceService.Api.Services.TicketingServiceClient>();

    // Rate-limit the anonymous customer-survey portal endpoint (spam prevention). Partition by the
    // real client IP the gateway forwards (X-Forwarded-For), else the connection IP — same pattern
    // as ticketing-service's public portal.
    Func<Microsoft.AspNetCore.Http.HttpContext, string> portalClientKey = ctx =>
        ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("portal-submit", ctx => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            portalClientKey(ctx),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
    });

    // Core + Infrastructure
    services.AddCoreServices();
    services.AddInfrastructureServices(builder.Configuration);

    // PostgreSQL
    var appConnection = builder.Configuration.GetConnectionString("AppConnection")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Database connection string not configured.");

    var migrationConnection = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? appConnection;

    services.AddSingleton<TenantDbConnectionInterceptor>();
    services.AddSingleton<ComplianceAuditInterceptor>();
    services.AddDbContext<ComplianceDbContext>((sp, options) =>
    {
        options.UseNpgsql(appConnection, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("ComplianceService.Infrastructure");
            sqlOptions.CommandTimeout(15);
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });
        options.AddInterceptors(sp.GetRequiredService<TenantDbConnectionInterceptor>());
        options.AddInterceptors(sp.GetRequiredService<ComplianceAuditInterceptor>());
    });

    // Schema-per-tenant provisioning
    services.AddScoped<ComplianceService.Infrastructure.Services.ITenantProvisioningService,
                       ComplianceService.Infrastructure.Services.TenantProvisioningService>();

    // JWT validation only — tokens are issued by user-service
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
    var jwksResolver = new ComplianceService.Api.Authentication.JwtIssuerSigningKeyResolver(new HttpClient(), jwksUrl);

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
            Title = "QaliCore ComplianceService API",
            Version = "v1",
            Description = "QaliCore — Compliance & Governance"
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
    app.UseRateLimiter();
    app.UseHttpMetrics();
    app.UseCors("AllowAll");

    var uploadsPath = builder.Configuration["Storage:BasePath"]
                     ?? Path.Combine(app.Environment.ContentRootPath, "uploads");
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads/compliance"
    });

    app.UseSwagger();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "QaliCore ComplianceService API V1");
            c.RoutePrefix = string.Empty;
            c.DocumentTitle = "QaliCore ComplianceService API (Dev)";
        });
    }
    else
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "QaliCore ComplianceService API V1");
            c.RoutePrefix = "swagger";
            c.DocumentTitle = "QaliCore ComplianceService API";
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseOutputCache();

    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapMetrics();

    // Run migrations using the superuser/BYPASSRLS connection so RLS doesn't block schema changes
    using (var scope = app.Services.CreateScope())
    {
        var appLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        appLogger.LogInformation("Applying database migrations...");

        var migrationOptions = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseNpgsql(migrationConnection, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly("ComplianceService.Infrastructure");
                sqlOptions.CommandTimeout(60);
            })
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var migrationContext = new ComplianceDbContext(migrationOptions);
        await migrationContext.Database.MigrateAsync();
        appLogger.LogInformation("Migrations applied successfully.");

        // Self-heal existing tenant schemas: MigrateAsync above only ever touches `public`, and
        // ITenantProvisioningService.ProvisionAsync otherwise only runs once, at tenant-creation
        // time — so a tenant schema provisioned before a later migration ships stays permanently
        // behind (missing columns/tables) until someone notices. ProvisionAsync's migrate/seed/grant
        // steps are all idempotent, so re-running it here on every startup for every existing
        // tenant_* schema is a no-op once a schema is current and self-heals it otherwise.
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
            .GetRequiredService<ComplianceService.Infrastructure.Services.ITenantProvisioningService>();
        foreach (var schema in tenantSchemas)
        {
            var result = await provisioning.ProvisionAsync(schema);
            if (result.Success)
                appLogger.LogInformation("Tenant schema {Schema} up to date.", schema);
            else
                appLogger.LogError("Failed to re-migrate tenant schema {Schema}: {Error}", schema, result.Error);
        }
    }

    Log.Information("QaliCore ComplianceService started successfully.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "QaliCore ComplianceService terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
