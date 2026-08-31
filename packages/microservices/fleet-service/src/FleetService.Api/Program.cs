using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using System.Text;
using FleetService.Api.Authorization;
using FleetService.Api.Services;
using FleetService.Core.Interfaces;
using FleetService.Core.Mappings;
using FleetService.Core.Services;
using FleetService.Infrastructure.BackgroundServices;
using FleetService.Infrastructure.Data;
using FleetService.Infrastructure.Repositories;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
        .Enrich.WithSpan()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] TraceId={TraceId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/fleet-service-.txt", rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] TraceId={TraceId} {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
builder.Host.UseSerilog();

// #220 -- distributed tracing. Auto-instruments ASP.NET Core + HttpClient (covers every
// AddHttpClient-registered typed client without touching handler code) and exports via OTLP
// to the shared Jaeger instance (kubernetes/observability/).
var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"]
    ?? "http://lante-jaeger-collector.new-erp.svc.cluster.local:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: "fleet-service"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

// Swagger with JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Fleet Service API", Version = "v1", Description = "QaliCore - Fleet Management" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(FleetMappingProfile));

// Database — PostgreSQL
// AppConnection = qalicore_app (RLS enforced); DefaultConnection = lante_user (BYPASSRLS, migrations only)
var fleetAppConn = builder.Configuration.GetConnectionString("AppConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Database connection string not configured.");
var fleetMigrationConn = builder.Configuration.GetConnectionString("DefaultConnection") ?? fleetAppConn;

builder.Services.AddSingleton<TenantDbConnectionInterceptor>();
builder.Services.AddSingleton<FleetAuditInterceptor>();
builder.Services.AddDbContext<FleetServiceDbContext>((sp, options) =>
{
    options.UseNpgsql(
        fleetAppConn,
        npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null));
    options.AddInterceptors(sp.GetRequiredService<TenantDbConnectionInterceptor>());
    options.AddInterceptors(sp.GetRequiredService<FleetAuditInterceptor>());
});

// Schema-per-tenant provisioning (Phase 2)
builder.Services.AddScoped<FleetService.Infrastructure.Services.ITenantProvisioningService,
                           FleetService.Infrastructure.Services.TenantProvisioningService>();

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ITripRepository, TripRepository>();
builder.Services.AddScoped<IDriverProfileRepository, DriverProfileRepository>();
builder.Services.AddScoped<IMaterialRepository, MaterialRepository>();
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<ITripDepositRepository, TripDepositRepository>();
builder.Services.AddScoped<ITruckRepository, TruckRepository>();
builder.Services.AddScoped<ITripTypeRepository, TripTypeRepository>();
builder.Services.AddScoped<IFieldVehicleRepository, FieldVehicleRepository>();

// Services
builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddScoped<ITruckService, TruckService>();
builder.Services.AddScoped<IVehicleClassService, VehicleClassService>();
builder.Services.AddScoped<IDriverProfileService, DriverProfileService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IMaterialVariantService, MaterialVariantService>();
builder.Services.AddScoped<IMaterialPhotoService, MaterialPhotoService>();
builder.Services.AddScoped<IMaterialVariantPhotoService, MaterialVariantPhotoService>();
builder.Services.AddScoped<IMaterialCostService, MaterialCostService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ITripDepositService, TripDepositService>();
builder.Services.AddScoped<ITripTypeService, TripTypeService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<ILicenseClassService, LicenseClassService>();
builder.Services.AddScoped<IFieldVehicleService, FieldVehicleService>();
builder.Services.AddScoped<IFieldVehiclePhotoService, FieldVehiclePhotoService>();

// File storage
builder.Services.AddSingleton<LocalFileStorageService>();

// Cross-service HTTP client — calls back to TicketingService when trip status changes
builder.Services.AddHttpClient("TicketingService");
builder.Services.AddScoped<ITicketingServiceClient, TicketingServiceClient>();

// Cross-service HTTP client — looks up a requester's email/phone from UserService so
// ApproveDispatchAsync can notify them (see FieldVehicleService.ApproveDispatchAsync).
builder.Services.AddHttpClient("UserService");
builder.Services.AddScoped<IUserServiceClient, UserServiceClient>();
builder.Services.AddHttpClient("OperationsService");
builder.Services.AddScoped<IOperationsServiceClient, OperationsServiceClient>();

// Background services
builder.Services.AddHostedService<LicenseExpiryBackgroundService>();
builder.Services.AddHostedService<VehicleExpiryBackgroundService>();
builder.Services.AddHostedService<TripSyncBackgroundService>();

// JWT Authentication — compatible with QaliCore UserService
var jwtSection = builder.Configuration.GetSection("JWT");
var jwksUrl = jwtSection["JwksUrl"] ?? "http://lante-user-service:8080/.well-known/jwks.json";
var jwksResolver = new FleetService.Api.Authentication.JwtIssuerSigningKeyResolver(new HttpClient(), jwksUrl);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = jwksResolver.ResolveSigningKeys,
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

builder.Services.AddHealthChecks();

var app = builder.Build();

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var migrationOptions = new DbContextOptionsBuilder<FleetServiceDbContext>()
            .UseNpgsql(fleetMigrationConn, o => o.CommandTimeout(60))
            // Startup migration must not hard-crash the service on EF9's model-validation warning.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var migrationDb = new FleetServiceDbContext(migrationOptions);
        await migrationDb.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied for FleetService");
        await SeedTripTypesAsync(migrationDb);
        await SeedMaterialsAsync(migrationDb);
        await SeedTrucksAsync(migrationDb);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating database schema");
        throw;
    }

    // Replay pending migrations into every already-provisioned tenant schema. Tenant schemas are
    // only migrated once, at provisioning time (TenantProvisioningService) — without this, any
    // migration added after a tenant was provisioned would silently apply to `public` on deploy
    // but never reach that tenant's schema (see AddVehicleClass, which crashed every
    // tenant_qsl request with "column VehicleClassId does not exist" for this exact reason).
    try
    {
        var tenantSchemas = new List<string>();
        await using (var conn = new Npgsql.NpgsqlConnection(fleetMigrationConn))
        {
            await conn.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(
                "SELECT nspname FROM pg_namespace WHERE nspname LIKE 'tenant\\_%' ESCAPE '\\'", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) tenantSchemas.Add(reader.GetString(0));
        }
        logger.LogInformation("Found {Count} existing tenant schema(s) to check for pending migrations: {Schemas}",
            tenantSchemas.Count, string.Join(", ", tenantSchemas));

        foreach (var schema in tenantSchemas)
        {
            try
            {
                var tenantConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(fleetMigrationConn) { SearchPath = schema }.ConnectionString;
                var tenantOptions = new DbContextOptionsBuilder<FleetService.Infrastructure.Data.TenantFleetServiceDbContext>()
                    .UseNpgsql(tenantConnectionString, o =>
                    {
                        o.MigrationsAssembly("FleetService.Infrastructure");
                        o.MigrationsHistoryTable("__EFMigrationsHistory", schema);
                        o.CommandTimeout(60);
                    })
                    .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                    .Options;
                await using var tenantDb = new FleetService.Infrastructure.Data.TenantFleetServiceDbContext(tenantOptions);
                await tenantDb.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied for FleetService tenant schema {Schema}", schema);
            }
            catch (Exception ex)
            {
                // Non-fatal and per-schema: one tenant's migration failing (e.g. a history/DDL drift
                // like AddFieldVehicleTables hit for tenant_qsl) must not crash-loop the whole
                // service for every OTHER tenant too.
                logger.LogError(ex, "Failed to re-migrate tenant schema {Schema}", schema);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error discovering existing tenant schemas");
    }
}

app.UseMiddleware<FleetService.Api.Middleware.GlobalExceptionMiddleware>();
app.UseCors("AllowAll");
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("swagger/v1/swagger.json", "Fleet Service API V1");
    c.RoutePrefix = string.Empty;
});

// Serve uploaded files at /uploads — mirrors Laravel's storage:link
var uploadsPath = builder.Configuration["Storage:BasePath"] ?? "/app/uploads";
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseHttpMetrics();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics();

Log.Information("FleetService starting — Department: Fleet Management Department");
app.Run();

static async Task SeedTrucksAsync(FleetService.Infrastructure.Data.FleetServiceDbContext db)
{
    if (db.Trucks.Any()) return;

    var trucks = new[]
    {
        new FleetService.Core.Entities.Truck { LicensePlate = "ABC 001 GP", Model = "Volvo FH16" },
        new FleetService.Core.Entities.Truck { LicensePlate = "ABC 002 GP", Model = "Mercedes Actros" },
        new FleetService.Core.Entities.Truck { LicensePlate = "ABC 003 GP", Model = "MAN TGX" },
        new FleetService.Core.Entities.Truck { LicensePlate = "ABC 004 GP", Model = "Scania R450" },
        new FleetService.Core.Entities.Truck { LicensePlate = "ABC 005 GP", Model = "DAF XF" },
    };

    db.Trucks.AddRange(trucks);
    await db.SaveChangesAsync();
}

static async Task SeedMaterialsAsync(FleetService.Infrastructure.Data.FleetServiceDbContext db)
{
    if (db.Materials.Any()) return;

    var materials = new[]
    {
        new FleetService.Core.Entities.Material { Name = "Sand", Description = "Construction sand" },
        new FleetService.Core.Entities.Material { Name = "Gravel", Description = "Crushed stone / gravel" },
        new FleetService.Core.Entities.Material { Name = "Cement", Description = "Portland cement bags" },
        new FleetService.Core.Entities.Material { Name = "Steel", Description = "Steel rods and sheets" },
        new FleetService.Core.Entities.Material { Name = "Timber", Description = "Sawn timber and planks" },
        new FleetService.Core.Entities.Material { Name = "Bricks", Description = "Clay / concrete bricks" },
        new FleetService.Core.Entities.Material { Name = "Fuel", Description = "Diesel / petrol" },
        new FleetService.Core.Entities.Material { Name = "Other", Description = "Miscellaneous goods" },
    };

    db.Materials.AddRange(materials);
    await db.SaveChangesAsync();
}

static async Task SeedTripTypesAsync(FleetService.Infrastructure.Data.FleetServiceDbContext db)
{
    if (db.TripTypes.Any()) return;

    var types = new[]
    {
        new FleetService.Core.Entities.TripType { Name = "Loaded Trip", Description = "Trip carrying a full load", Category = FleetService.Core.Entities.TripCategory.Loaded, MaterialRequirement = FleetService.Core.Entities.MaterialRequirement.Mandatory, EmptyTripOption = FleetService.Core.Entities.EmptyTripOption.NotAllowed, CreatedByUserId = "system" },
        new FleetService.Core.Entities.TripType { Name = "Empty Trip", Description = "Trip with no load", Category = FleetService.Core.Entities.TripCategory.Empty, MaterialRequirement = FleetService.Core.Entities.MaterialRequirement.None, EmptyTripOption = FleetService.Core.Entities.EmptyTripOption.Required, CreatedByUserId = "system" },
        new FleetService.Core.Entities.TripType { Name = "Maintenance Trip", Description = "Trip for vehicle servicing or repairs", Category = FleetService.Core.Entities.TripCategory.Maintenance, MaterialRequirement = FleetService.Core.Entities.MaterialRequirement.None, EmptyTripOption = FleetService.Core.Entities.EmptyTripOption.Allowed, CreatedByUserId = "system" },
        new FleetService.Core.Entities.TripType { Name = "Other", Description = "Miscellaneous trip", Category = FleetService.Core.Entities.TripCategory.Other, MaterialRequirement = FleetService.Core.Entities.MaterialRequirement.Optional, EmptyTripOption = FleetService.Core.Entities.EmptyTripOption.Allowed, CreatedByUserId = "system" },
    };

    db.TripTypes.AddRange(types);
    await db.SaveChangesAsync();
}
