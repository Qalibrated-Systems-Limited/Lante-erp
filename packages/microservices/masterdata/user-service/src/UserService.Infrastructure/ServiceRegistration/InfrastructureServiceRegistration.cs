using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;
using UserService.Core.Interfaces.Emails;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;
using UserService.Infrastructure.Repositories;
using UserService.Infrastructure.Services;

namespace UserService.Infrastructure.ServiceRegistration;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Email
        services.AddSingleton<EmailQueueService>();
        services.AddScoped<IEmailQueueService>(sp => sp.GetRequiredService<EmailQueueService>());
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddHostedService<EmailProcessorService>();
        services.AddSingleton<IEmailCredentialProtector, AesEmailCredentialProtector>();
        services.AddScoped<IEmailSettingsService, EmailSettingsService>();

        // HTTP context
        services.AddHttpContextAccessor();

        // Health checks
        services.AddHealthChecks();

        // Caching
        RegisterCachingServices(services, configuration);

        // Repositories
        RegisterRepositories(services);

        // Tenancy / provisioning (schema-per-tenant)
        RegisterTenancy(services);

        // Platform admin: backups control page (talks to the Kubernetes API directly)
        services.AddScoped<Core.Interfaces.Services.IBackupOperationsService, KubernetesBackupOperationsService>();

        return services;
    }

    private static void RegisterTenancy(IServiceCollection services)
    {
        // Scoped: one bound schema per unit of work. The provisioning service sets its own
        // schema when building ad-hoc TenantDbContexts, so this default (null) is fine here.
        services.AddScoped<Data.Tenancy.ITenantSchemaProvider, Data.Tenancy.TenantSchemaProvider>();
        services.AddScoped<Core.Interfaces.Services.ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<Core.Interfaces.Services.ITenantAuthenticator, TenantAuthenticator>();
        // Control-plane user directory (public.Users) for single-login + invites.
        services.AddScoped<Core.Interfaces.Services.IUserDirectory, UserDirectory>();
        services.AddScoped<Core.Interfaces.Services.IPublicRoleDirectorySync, PublicRoleDirectorySync>();

        // Cross-service orchestrator: fans out to business services' internal /provision endpoints.
        // Bounded timeout + retry so one hung/flaky business service doesn't block the whole
        // fan-out for the ~100s HttpClient default, or cause a permanent Failed on a blip.
        // Kept to 3 attempts (not more) because the background retry sweep
        // (ProvisioningRetryBackgroundService) also retries a Failed row up to 5 times — stacking
        // this with a larger count here would let one stuck tenant hammer a genuinely-down service
        // far more than either layer intends on its own.
        services.AddHttpClient("provisioning", client => client.Timeout = TimeSpan.FromSeconds(20))
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
        services.AddScoped<Core.Interfaces.Services.ITenantOrchestrator, TenantOrchestrationService>();
        services.AddHostedService<ProvisioningRetryBackgroundService>();
    }

    private static void RegisterCachingServices(IServiceCollection services, IConfiguration configuration)
    {
        var useRedis = configuration.GetValue<bool>("UseRedis", false);
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (useRedis && !string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "LanteUserService";
            });

            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(redisConnectionString));

            services.AddScoped<ICacheService, RedisCacheService>();
            Serilog.Log.Information("Redis cache configured for Lante.");
        }
        else
        {
            services.AddMemoryCache();
            services.AddScoped<ICacheService, MemoryCacheService>();
            Serilog.Log.Information("Memory cache configured for Lante.");
        }
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionsRepository, PermissionsRepository>();
        services.AddScoped<ITokenRepository, TokenRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IPasswordPolicyRepository, PasswordPolicyRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<ITenantEmailSettingsRepository, TenantEmailSettingsRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ISystemModuleRepository, SystemModuleRepository>();
        services.AddScoped<IDocumentTemplateRepository, DocumentTemplateRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
    }
}
