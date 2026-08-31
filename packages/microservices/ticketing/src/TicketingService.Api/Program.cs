using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Prometheus;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using System.Security.Claims;
using System.Text;
using TicketingService.Api.Authorization;
using TicketingService.Api.Middleware;
using TicketingService.Api.Services;
using TicketingService.Core.Interfaces.Services;
using TicketAssignmentClient = TicketingService.Api.Services.TicketAssignmentClient;
using FleetServiceClient = TicketingService.Api.Services.FleetServiceClient;
using TicketingService.Core.Mappings;
using TicketingService.Core.ServiceRegistration;
using TicketingService.Infrastructure.Data;
using TicketingService.Infrastructure.ServiceRegistration;

// Npgsql 6+ requires all DateTime values to have Kind=Utc for timestamptz columns.
// This switch accepts Unspecified/Local values and treats them as UTC (legacy behaviour).
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
    Log.Information("Starting Lante TicketingService...");

    var services = builder.Services;

    // #220 -- distributed tracing. Auto-instruments ASP.NET Core + HttpClient (covers every
    // AddHttpClient-registered typed client without touching handler code) and exports via OTLP
    // to the shared Jaeger instance (kubernetes/observability/).
    var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"]
        ?? "http://lante-jaeger-collector.new-erp.svc.cluster.local:4317";
    services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: "ticketing-service"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

    services.AddControllers();
    services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    {
        o.MultipartBodyLengthLimit = 5 * 1024 * 1024; // 5 MB total (covers 3 × 1 MB + overhead)
    });
    services.AddEndpointsApiExplorer();
    services.AddOutputCache();
    services.AddHttpContextAccessor();
    services.AddMemoryCache();

    // API Versioning
    services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    }).AddMvc();

    // Portal email
    services.AddScoped<IPortalEmailService, PortalEmailService>();
    services.AddHttpClient("TiaraSms");
    services.AddScoped<ISmsSender, TiaraSmsSender>();   // D4-2 — Tiara Connect; inert until Sms:Tiara:ApiKey/SenderId are set
    // D8 — cross-module integration seams (see INTEGRATIONS.md).
    // D8-2/D8-3 CRM sink is now a REAL HTTP adapter (config-gated on Integrations:Crm:Enabled +
    // CrmService:BaseUrl; inert otherwise). HR employee directory remains a no-op until Module 3 exists.
    services.AddHttpClient("CrmService");
    services.AddScoped<TicketingService.Core.Integrations.ICrmSync, CrmSyncClient>();
    // D8-1 — real CRM customer-master read for the ticket-create picker (config-gated, same flags).
    services.AddScoped<TicketingService.Core.Integrations.ICrmCustomerDirectory, CrmCustomerDirectoryClient>();
    services.AddScoped<TicketingService.Core.Integrations.IEmployeeDirectory, NoOpEmployeeDirectory>();

    // Ticket notifications (department assignment + in-app)
    services.AddHttpClient("UserService");
    services.AddScoped<ITicketNotificationService, TicketNotificationService>();
    services.AddScoped<INotificationService, NotificationService>();
    services.AddScoped<IAlertService, AlertService>();
    services.AddScoped<ITenantEmailSettingsClient, TenantEmailSettingsClient>();
    services.AddScoped<TenantAwareSmtpSender>();
    services.AddScoped<IPortalTenantResolverClient, PortalTenantResolverClient>();

    // Cross-service HTTP clients
    services.AddHttpClient("FleetService");
    services.AddHttpClient("OperationsService");
    services.AddScoped<ITicketAssignmentClient, TicketAssignmentClient>();
    services.AddScoped<IFleetServiceClient, FleetServiceClient>();
    services.AddScoped<IOperationsServiceClient, OperationsServiceClient>();

    // Core + Infrastructure + AutoMapper
    // #16: business-hours calendar for SLA deadlines (config "BusinessHours", else EAT Mon–Fri 8–17).
    var bh = builder.Configuration.GetSection("BusinessHours");
    var calendar = new TicketingService.Core.Services.BusinessCalendar
    {
        UtcOffsetHours = double.TryParse(bh["UtcOffsetHours"], out var off) ? off : 3,
        StartHour = int.TryParse(bh["StartHour"], out var sh) ? sh : 8,
        EndHour = int.TryParse(bh["EndHour"], out var eh) ? eh : 17,
    };
    services.AddSingleton(calendar);

    // #17/#18: rate-limit the anonymous portal endpoints (spam + tracking-ref enumeration).
    // Partition by the real client IP the gateway forwards (X-Forwarded-For), else the connection IP.
    Func<Microsoft.AspNetCore.Http.HttpContext, string> portalClientKey = ctx =>
        ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // Submissions: tight — a human fills a form occasionally, not 6× a minute.
        options.AddPolicy("portal-submit", ctx => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            portalClientKey(ctx),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
        // Tracking/reply: looser (allows legit polling) but caps reference enumeration.
        options.AddPolicy("portal-track", ctx => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            portalClientKey(ctx),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
    });

    services.AddCoreServices();
    services.AddInfrastructureServices(builder.Configuration);
    services.AddAutoMapper(typeof(MappingProfile));

    // PostgreSQL
    // AppConnection uses lante_app (non-superuser — RLS enforced at DB level)
    // DefaultConnection uses lante_user (superuser/BYPASSRLS — used only for migrations)
    var appConnection = builder.Configuration.GetConnectionString("AppConnection")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Database connection string not configured.");

    var migrationConnection = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? appConnection;

    services.AddSingleton<TenantDbConnectionInterceptor>();
    services.AddSingleton<TicketingAuditInterceptor>();
    services.AddDbContext<TicketingDbContext>((sp, options) =>
    {
        options.UseNpgsql(appConnection, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("TicketingService.Infrastructure");
            sqlOptions.CommandTimeout(15);
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });
        options.AddInterceptors(sp.GetRequiredService<TenantDbConnectionInterceptor>());
        options.AddInterceptors(sp.GetRequiredService<TicketingAuditInterceptor>());
    });

    // Schema-per-tenant provisioning (Phase 2)
    services.AddScoped<TicketingService.Infrastructure.Services.ITenantProvisioningService,
                       TicketingService.Infrastructure.Services.TenantProvisioningService>();

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
    var jwksResolver = new TicketingService.Api.Authentication.JwtIssuerSigningKeyResolver(new HttpClient(), jwksUrl);

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

    // CORS
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

    // Swagger
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Lante TicketingService API",
            Version = "v1",
            Description = "Lante — Ticketing, SLA Management & Escalations"
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

    // Serve uploaded ticket attachments from the PVC-backed uploads directory
    var uploadsPath = builder.Configuration["Storage:BasePath"] ?? "/app/uploads";
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
        RequestPath  = "/uploads",
    });

    app.UseSwagger();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Lante TicketingService API V1");
            c.RoutePrefix = string.Empty;
            c.DocumentTitle = "Lante TicketingService API (Dev)";
        });
    }
    else
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "Lante TicketingService API V1");
            c.RoutePrefix = "swagger";
            c.DocumentTitle = "Lante TicketingService API";
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseOutputCache();

    // Anonymous /portal/* requests carry a company as a ?slug= query param, not a JWT — resolve it
    // here (once, server-side, via the internal-key-gated user-service lookup) and stamp the
    // validated schema onto HttpContext.Items so both the DB connection interceptor and
    // TenantEmailSettingsClient can pick it up. Skipped entirely for authenticated requests (they
    // already carry a schema claim) and for anything outside /portal.
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/v1/portal")
            && context.User.FindFirst("schema") is null
            && context.Request.Query.TryGetValue("slug", out var slugValues))
        {
            var resolver = context.RequestServices.GetRequiredService<IPortalTenantResolverClient>();
            var schema = await resolver.ResolveSchemaBySlugAsync(slugValues.FirstOrDefault());
            if (schema is not null)
                context.Items["ResolvedTenantSchema"] = schema;
        }
        await next();
    });

    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapMetrics();

    // Run migrations using lante_user (BYPASSRLS) so RLS policies don't block schema changes
    using (var scope = app.Services.CreateScope())
    {
        var appLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        appLogger.LogInformation("Applying database migrations...");

        var migrationOptions = new DbContextOptionsBuilder<TicketingDbContext>()
            .UseNpgsql(migrationConnection, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly("TicketingService.Infrastructure");
                sqlOptions.CommandTimeout(60);
            })
            // Model is now schema-agnostic (default schema null) while the migration snapshot still
            // records public; that intended difference must not crash the startup migration.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var migrationContext = new TicketingDbContext(migrationOptions);
        await migrationContext.Database.MigrateAsync();
        appLogger.LogInformation("Migrations applied successfully.");

        // Seed using migrationContext (lante_user / BYPASSRLS) so RLS doesn't hide existing rows
        await DatabaseSeeder.SeedAsync(migrationContext);
        appLogger.LogInformation("Database seeded.");

        // Self-heal existing tenant schemas: MigrateAsync above only ever touches `public`, and
        // ITenantProvisioningService.ProvisionAsync otherwise only runs once, at tenant-creation
        // time — so a tenant schema provisioned before a later migration ships stays permanently
        // behind (missing columns/tables) until someone notices. ProvisionAsync's migrate/seed/grant
        // steps are all idempotent, so re-running it here on every startup for every existing
        // tenant_* schema is a no-op once a schema is current and self-heals it otherwise.
        appLogger.LogInformation("Re-migrating existing tenant schemas...");
        var tenantSchemas = new List<string>();
        await using (var conn = new NpgsqlConnection(migrationConnection))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'tenant\\_%' ESCAPE '\\'", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tenantSchemas.Add(reader.GetString(0));
        }

        var provisioning = scope.ServiceProvider
            .GetRequiredService<TicketingService.Infrastructure.Services.ITenantProvisioningService>();
        foreach (var schema in tenantSchemas)
        {
            var result = await provisioning.ProvisionAsync(schema);
            if (result.Success)
                appLogger.LogInformation("Tenant schema {Schema} up to date.", schema);
            else
                appLogger.LogError("Failed to re-migrate tenant schema {Schema}: {Error}", schema, result.Error);
        }
    }

    Log.Information("Lante TicketingService started successfully.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Lante TicketingService terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
