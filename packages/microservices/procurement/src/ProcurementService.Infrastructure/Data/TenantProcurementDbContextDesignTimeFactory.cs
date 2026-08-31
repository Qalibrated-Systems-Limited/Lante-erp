using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ProcurementService.Infrastructure.Data;

/// <summary>
/// Design-time factory for <see cref="TenantProcurementDbContext"/>. Emits schema-agnostic migrations
/// that apply into whichever tenant schema the connection's search_path points at.
/// </summary>
public class TenantProcurementDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TenantProcurementDbContext>
{
    private const string ApiProject = "ProcurementService.Api";

    public TenantProcurementDbContext CreateDbContext(string[] args)
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
                $"Set ConnectionStrings__DefaultConnection or ensure {ApiProject}/appsettings.{env}.json is configured.");

        var optionsBuilder = new DbContextOptionsBuilder<TenantProcurementDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.MigrationsAssembly("ProcurementService.Infrastructure"));

        return new TenantProcurementDbContext(optionsBuilder.Options);
    }
}
