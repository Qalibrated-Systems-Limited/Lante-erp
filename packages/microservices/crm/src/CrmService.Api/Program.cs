using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using CrmService.Api.Authorization;
using CrmService.Api.Middleware;
using CrmService.Core.Mappings;
using CrmService.Infrastructure.Data;
using CrmService.Infrastructure.ServiceRegistration;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using System.Security.Claims;
using System.Text;

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
    Log.Information("Starting QaliCore CrmService...");

    var services = builder.Services;

    // #220 -- distributed tracing. Auto-instruments ASP.NET Core + HttpClient (covers every
    // AddHttpClient-registered typed client without touching handler code) and exports via OTLP
    // to the shared Jaeger instance (kubernetes/observability/).
    var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"]
        ?? "http://lante-jaeger-collector.new-erp.svc.cluster.local:4317";
    services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: "crm-service"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

    services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });
    services.AddEndpointsApiExplorer();
    services.AddHttpContextAccessor();

    services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    }).AddMvc();

    // Infrastructure + AutoMapper
    services.AddInfrastructure(builder.Configuration);
    services.AddAutoMapper(typeof(MappingProfile));

    // Schema-per-tenant provisioning
    services.AddScoped<CrmService.Infrastructure.Services.ITenantProvisioningService,
                       CrmService.Infrastructure.Services.TenantProvisioningService>();

    // Per-tenant-schema background sweep (lead/opportunity/contract/tender/dormant/revenue/payment alerts).
    services.AddHostedService<CrmService.Infrastructure.Services.CrmBackgroundService>();

    // JWT
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
    var jwksResolver = new CrmService.Api.Authentication.JwtIssuerSigningKeyResolver(new HttpClient(), jwksUrl);

    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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
            policy.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials());
    });

    services.AddHealthChecks();

    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "QaliCore CrmService API",
            Version = "v1",
            Description = "QaliCore — Sales, CRM & Business Development (Module 6)"
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
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "QaliCore CrmService API V1");
            c.RoutePrefix = string.Empty;
        });
    }
    else
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "QaliCore CrmService API V1");
            c.RoutePrefix = "swagger";
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapMetrics();

    var migrationConn = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection not configured.");

    using (var scope = app.Services.CreateScope())
    {
        var appLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        appLogger.LogInformation("Applying database migrations...");

        var migrationOptions = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(migrationConn, o => o.CommandTimeout(60))
            .Options;

        await using var migrationDb = new CrmDbContext(migrationOptions);
        await migrationDb.Database.MigrateAsync();
        appLogger.LogInformation("Migrations applied successfully.");

        // Self-heal existing tenant schemas: MigrateAsync above only ever touches `public`, and
        // ITenantProvisioningService.ProvisionAsync otherwise only runs once, at tenant-creation
        // time — so a tenant schema provisioned before a later migration ships stays permanently
        // behind (missing columns/tables) until someone notices. ProvisionAsync's migrate/seed/grant
        // steps are all idempotent, so re-running it here on every startup for every existing
        // tenant_* schema is a no-op once a schema is current and self-heals it otherwise. Same
        // pattern as every other schema-per-tenant service in this repo (see compliance-service's
        // Program.cs).
        appLogger.LogInformation("Re-migrating existing tenant schemas...");
        var tenantSchemas = new List<string>();
        await using (var conn = new Npgsql.NpgsqlConnection(migrationConn))
        {
            await conn.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(
                "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'tenant\\_%' ESCAPE '\\'", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tenantSchemas.Add(reader.GetString(0));
        }

        var provisioning = scope.ServiceProvider.GetRequiredService<CrmService.Infrastructure.Services.ITenantProvisioningService>();
        foreach (var schema in tenantSchemas)
        {
            var result = await provisioning.ProvisionAsync(schema);
            if (result.Success)
                appLogger.LogInformation("Tenant schema {Schema} up to date.", schema);
            else
                appLogger.LogError("Failed to re-migrate tenant schema {Schema}: {Error}", schema, result.Error);
        }
    }

    Log.Information("QaliCore CrmService started successfully.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "QaliCore CrmService terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
