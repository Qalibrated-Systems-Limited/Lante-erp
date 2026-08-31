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
using System.Security.Claims;
using System.Text;
using UserService.Api.Authorization;
using UserService.Api.Middleware;
using UserService.Core.Mappings;
using UserService.Core.ServiceRegistration;
using UserService.Infrastructure.Data;
using UserService.Infrastructure.ServiceRegistration;

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
    Log.Information("Starting Lante UserService...");

    var services = builder.Services;

    // #220 -- distributed tracing. Auto-instruments ASP.NET Core + HttpClient (covers every
    // AddHttpClient-registered typed client without touching handler code) and exports via OTLP
    // to the shared Jaeger instance (kubernetes/observability/).
    var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"]
        ?? "http://lante-jaeger-collector.new-erp.svc.cluster.local:4317";
    services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: "user-service"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

    services.AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
    services.AddEndpointsApiExplorer();
    services.AddOutputCache();
    services.AddHttpContextAccessor();

    // File storage (tenant logo uploads)
    services.AddSingleton<UserService.Api.Services.LocalFileStorageService>();

    // API Versioning
    services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    }).AddMvc();

    // Core + Infrastructure + AutoMapper
    services.AddCoreServices();
    services.AddInfrastructureServices(builder.Configuration);
    services.AddAutoMapper(typeof(MappingProfile));

    // PostgreSQL
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Database connection string not configured.");
    // Least-privilege runtime connection; DefaultConnection (admin) is reserved for
    // provisioning/migrations (TenantProvisioningService, the startup migration below).
    var appConnectionString = builder.Configuration.GetConnectionString("AppConnection") ?? connectionString;

    services.AddSingleton<UserService.Infrastructure.Data.UserServiceTenantConnectionInterceptor>();
    services.AddDbContext<LanteUserServiceDbContext>((sp, options) =>
    {
        options.UseNpgsql(appConnectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("UserService.Infrastructure");
            // History lives in public (unaffected by dropping the model default schema).
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            sqlOptions.CommandTimeout(15);
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });
        // Bind tenant-plane reads to the caller's schema via search_path (control-plane tables are
        // pinned to public in the model). No tenant claim → public (login / platform admin).
        options.AddInterceptors(sp.GetRequiredService<UserService.Infrastructure.Data.UserServiceTenantConnectionInterceptor>());
        // Model is schema-agnostic now while the snapshot still records public; that intended
        // difference must not crash the startup migration.
        options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });

    // Authorization — handler reads permission claims from JWT, no DB lookup per request
    services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .Build();

        options.AddPolicy("RequireAdminRole", policy =>
            policy.RequireRole("Admin")
                  .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));
    });

    // JWT (#215) — user-service holds the only private key, so it validates against its own
    // in-process key store instead of a round trip through its own JWKS endpoint.
    var issuer = builder.Configuration["JwtSettings:Issuer"]
                 ?? builder.Configuration["JWT__Issuer"]
                 ?? "LanteUserService";

    var audience = builder.Configuration["JwtSettings:Audience"]
                   ?? builder.Configuration["JWT__Audience"]
                   ?? "LanteUserService";

    var jwtKeyStore = new UserService.Core.Services.JwtSigningKeyStore(builder.Configuration);

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
            IssuerSigningKeyResolver = (_, __, kid, ___) =>
                kid is null ? jwtKeyStore.AllPublicSigningKeys : jwtKeyStore.AllPublicSigningKeys.Where(k => k.KeyId == kid),
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

    // Health checks
    services.AddHealthChecks();

    // Swagger
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Lante UserService API",
            Version = "v1",
            Description = "Lante — Authentication, Users, Roles, Departments & Permissions"
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
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Lante UserService API V1");
            c.RoutePrefix = string.Empty;
            c.DocumentTitle = "Lante UserService API (Dev)";
        });
    }
    else
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "Lante UserService API V1");
            c.RoutePrefix = "swagger";
            c.DocumentTitle = "Lante UserService API";
        });
    }

    // Serve uploaded files at /uploads — mirrors Laravel's storage:link
    var uploadsPath = builder.Configuration["Storage:BasePath"] ?? "/app/uploads";
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseOutputCache();

    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapMetrics();

    // Run migrations and seed
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Migrations need DDL rights, so this runs against DefaultConnection (admin) explicitly —
        // the DI-registered LanteUserServiceDbContext above is bound to AppConnection (least
        // privilege) for request-time use and can't create/alter tables.
        logger.LogInformation("Applying database migrations...");
        var migrationOptions = new DbContextOptionsBuilder<LanteUserServiceDbContext>()
            .UseNpgsql(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly("UserService.Infrastructure");
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            })
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using (var migrationDb = new LanteUserServiceDbContext(migrationOptions))
            await migrationDb.Database.MigrateAsync();
        logger.LogInformation("Migrations applied successfully.");

        await DatabaseSeeder.SeedAsync(app);
        logger.LogInformation("Database seeded.");

        // Self-heal existing tenant schemas: ProvisionUserSchemaAsync's TenantDbContext migration
        // path only ever runs once, at tenant-creation time — so a schema provisioned before a
        // later migration ships (e.g. this deploy's SystemSettings/AuditLogs tables) stays
        // permanently behind. Re-apply TenantDbContext's own migrations (the same ones
        // TenantProvisioningService.BuildTenantContext uses) against every existing tenant_*
        // schema on every startup; idempotent once a schema is current.
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

        foreach (var schema in tenantSchemas)
        {
            try
            {
                var tenantConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
                var tenantOptions = new DbContextOptionsBuilder<TenantDbContext>()
                    .UseNpgsql(tenantConnectionString, o =>
                    {
                        o.MigrationsAssembly("UserService.Infrastructure");
                        o.MigrationsHistoryTable("__EFMigrationsHistory", schema);
                    })
                    .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                    .Options;
                await using var tenantContext = new TenantDbContext(tenantOptions);
                await tenantContext.Database.MigrateAsync();

                // Grant the runtime app role so it can read this schema once the main DbContext
                // moves off the admin (DefaultConnection) connection — see TenantProvisioningService's
                // GrantAppRoleAsync for the newly-provisioned-tenant equivalent of this. Schemas
                // provisioned before this grant existed (e.g. tenant_qsl) only get it via this loop.
                var appRole = builder.Configuration["ProvisioningAppRole"] ?? "qalicore_app";
                try
                {
#pragma warning disable EF1002 // schema is read back from information_schema.schemata (an already-existing Postgres identifier, not user input); appRole comes from server configuration, not a request. Neither can be parameterized via ExecuteSqlAsync anyway since these are identifiers, not values.
                    var roleExists = await tenantContext.Database
                        .SqlQueryRaw<int>($"SELECT 1 AS \"Value\" FROM pg_roles WHERE rolname = '{appRole}'")
                        .AnyAsync();
#pragma warning restore EF1002
                    if (roleExists)
                    {
#pragma warning disable EF1002 // schema is read back from information_schema.schemata (an already-existing Postgres identifier, not user input); appRole comes from server configuration, not a request. Neither can be parameterized via ExecuteSqlAsync anyway since these are identifiers, not values.
                        await tenantContext.Database.ExecuteSqlRawAsync($"GRANT USAGE ON SCHEMA \"{schema}\" TO {appRole}");
                        await tenantContext.Database.ExecuteSqlRawAsync($"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"{schema}\" TO {appRole}");
                        await tenantContext.Database.ExecuteSqlRawAsync($"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA \"{schema}\" TO {appRole}");
                        await tenantContext.Database.ExecuteSqlRawAsync($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schema}\" GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {appRole}");
#pragma warning restore EF1002
                    }
                    else
                    {
                        logger.LogWarning("App role {Role} not found; skipping grants on {Schema}.", appRole, schema);
                    }
                }
                catch (Exception grantEx)
                {
                    logger.LogWarning(grantEx, "Failed to grant {Role} on {Schema}.", appRole, schema);
                }

                logger.LogInformation("Tenant schema {Schema} up to date.", schema);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to re-migrate tenant schema {Schema}.", schema);
            }
        }
    }

    Log.Information("Lante UserService started successfully.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Lante UserService terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
