using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FinanceService.Infrastructure.Data;

internal static class DesignTimeConfig
{
    private const string ApiProject = "FinanceService.Api";

    public static string Resolve()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var cwd = Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(cwd, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(cwd, $"appsettings.{env}.json"), optional: true)
            .AddJsonFile(Path.Combine(cwd, "..", ApiProject, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(cwd, "..", ApiProject, $"appsettings.{env}.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();
        var cs = config.GetConnectionString("DefaultConnection")
                 ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs) || cs.Contains("${"))
            throw new InvalidOperationException("DefaultConnection unset/placeholder — set ConnectionStrings__DefaultConnection.");
        return cs!;
    }
}

/// Public-schema (base) context factory for EF tooling.
public class FinanceDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        // Must mirror Program.cs exactly -- this factory never runs Main(), so without this line
        // dotnet ef computes a different model than the real app (timestamptz vs timestamp). See #321.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseNpgsql(DesignTimeConfig.Resolve(), o => o.MigrationsAssembly("FinanceService.Infrastructure"))
            .Options;
        return new FinanceDbContext(options);
    }
}

/// Schema-agnostic tenant context factory (its migrations apply into whatever search_path points at).
public class TenantFinanceDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TenantFinanceDbContext>
{
    public TenantFinanceDbContext CreateDbContext(string[] args)
    {
        // Must mirror Program.cs exactly -- this factory never runs Main(), so without this line
        // dotnet ef computes a different model than the real app (timestamptz vs timestamp). See #321.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var options = new DbContextOptionsBuilder<TenantFinanceDbContext>()
            .UseNpgsql(DesignTimeConfig.Resolve(), o => o.MigrationsAssembly("FinanceService.Infrastructure"))
            .Options;
        return new TenantFinanceDbContext(options);
    }
}
