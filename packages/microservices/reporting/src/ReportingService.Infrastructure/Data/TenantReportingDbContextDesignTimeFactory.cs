using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ReportingService.Infrastructure.Data;

/// <summary>
/// Design-time factory for <see cref="TenantReportingDbContext"/>. The model is schema-agnostic,
/// so `dotnet ef migrations add` emits unqualified operations that apply into whichever tenant
/// schema the connection's search_path points at.
/// </summary>
public class TenantReportingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TenantReportingDbContext>
{
    private const string ApiProject = "ReportingService.Api";

    public TenantReportingDbContext CreateDbContext(string[] args)
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

        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("${"))
            throw new InvalidOperationException(
                "DefaultConnection is unset or still an unexpanded '${...}' placeholder. " +
                "Set the ConnectionStrings__DefaultConnection environment variable, or ensure " +
                $"{ApiProject}/appsettings.{env}.json provides a real connection string, before running EF tooling.");

        var optionsBuilder = new DbContextOptionsBuilder<TenantReportingDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.MigrationsAssembly("ReportingService.Infrastructure"));

        return new TenantReportingDbContext(optionsBuilder.Options);
    }
}
