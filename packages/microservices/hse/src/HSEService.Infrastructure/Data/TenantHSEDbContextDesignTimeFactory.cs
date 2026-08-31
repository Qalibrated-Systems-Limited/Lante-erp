using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HSEService.Infrastructure.Data;

/// <summary>
/// Design-time factory for <see cref="TenantHSEDbContext"/>. The model is schema-agnostic, so
/// `dotnet ef migrations add` emits unqualified operations that apply into whichever tenant
/// schema the connection's search_path points at.
/// </summary>
public class TenantHSEDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TenantHSEDbContext>
{
    private const string ApiProject = "HSEService.Api";

    public TenantHSEDbContext CreateDbContext(string[] args)
    {
        // See DesignTimeDbContextFactory.cs's identical line for why this has to match
        // Program.cs's AppContext.SetSwitch call exactly.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var cwd = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(cwd, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(cwd, $"appsettings.{env}.json"), optional: true)
            .AddJsonFile(Path.Combine(cwd, "..", ApiProject, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(cwd, "..", ApiProject, $"appsettings.{env}.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("${"))
            throw new InvalidOperationException(
                "DefaultConnection is unset or still an unexpanded '${...}' placeholder. " +
                "Set the ConnectionStrings__DefaultConnection environment variable, or ensure " +
                $"{ApiProject}/appsettings.{env}.json provides a real connection string, before running EF tooling.");

        var optionsBuilder = new DbContextOptionsBuilder<TenantHSEDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.MigrationsAssembly("HSEService.Infrastructure"));

        return new TenantHSEDbContext(optionsBuilder.Options);
    }
}
