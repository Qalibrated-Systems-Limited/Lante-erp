using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LicenseService.Infrastructure.Data;

/// <summary>
/// Used by EF Core tooling (dotnet ef migrations add / database update).
/// Resolves the connection string exactly like the running app does:
/// appsettings.json → appsettings.{Environment}.json → environment variables.
/// Works whether `dotnet ef` runs with cwd at the Api or the Infrastructure project.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LanteLicenseDbContext>
{
    private const string ApiProject = "LicenseService.Api";

    public LanteLicenseDbContext CreateDbContext(string[] args)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var cwd = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            // cwd = Api project (e.g. `dotnet ef ... --startup-project .` run from the Api dir)
            .AddJsonFile(Path.Combine(cwd, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(cwd, $"appsettings.{env}.json"), optional: true)
            // cwd = Infrastructure project — reach the sibling Api project
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

        var optionsBuilder = new DbContextOptionsBuilder<LanteLicenseDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.MigrationsAssembly("LicenseService.Infrastructure"));

        return new LanteLicenseDbContext(optionsBuilder.Options);
    }
}
