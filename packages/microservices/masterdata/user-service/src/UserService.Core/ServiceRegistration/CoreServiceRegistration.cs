using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UserService.Core.Interfaces.Services;
using UserService.Core.Services;

namespace UserService.Core.ServiceRegistration;

public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddScoped<UserService.Core.Services.UserService>();
        services.AddScoped<IUserService>(provider =>
        {
            var baseService = provider.GetRequiredService<UserService.Core.Services.UserService>();
            var cacheService = provider.GetRequiredService<ICacheService>();
            return new CachedUserService(baseService, cacheService);
        });

        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionsService, PermissionsService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        // Loads the RSA signing key(s) once per process (#215) — only user-service signs tokens.
        services.AddSingleton<JwtSigningKeyStore>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();
        services.AddScoped<PasswordPolicyService>();
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ISystemModuleService, SystemModuleService>();
        services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();

        return services;
    }
}
