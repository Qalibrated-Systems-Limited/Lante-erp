using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FleetService.Infrastructure.Data;

/// <summary>
/// Used by EF Core tooling (dotnet ef migrations add / database update).
/// Resolves the connection string exactly like the running app does:
/// appsettings.json → appsettings.{Environment}.json → environment variables.
/// Works whether `dotnet ef` runs with cwd at the Api or the Infrastructure project.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FleetServiceDbContext>
{
    private const string ApiProject = "FleetService.Api";

    public FleetServiceDbContext CreateDbContext(string[] args)
    {
        // Must match Program.cs's own AppContext.SetSwitch call exactly, or `dotnet ef` computes a
        // DIFFERENT model than the running app does: with the switch on (as Program.cs sets it),
        // Npgsql maps DateTime to "timestamp without time zone"; without it, EF/Npgsql 9's own
        // default is "timestamp with time zone". A migration generated without this line looks
        // correct to every `dotnet ef` command — including `has-pending-model-changes` — and then
        // fails PendingModelChangesWarning the moment the real app tries to apply it, because the
        // real app's model (computed with the switch on) never agreed with what got generated.
        // Found live in production (#227): the EF Core 8->9 bump added exactly this migration,
        // passed every local check, and crash-looped on deploy.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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

        var optionsBuilder = new DbContextOptionsBuilder<FleetServiceDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.MigrationsAssembly("FleetService.Infrastructure"));

        return new FleetServiceDbContext(optionsBuilder.Options);
    }
}
