using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HSEService.Infrastructure.Data;

/// <summary>Used by `dotnet ef migrations add / database update` for the public-plane HSEDbContext.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HSEDbContext>
{
    private const string ApiProject = "HSEService.Api";

    public HSEDbContext CreateDbContext(string[] args)
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

        var optionsBuilder = new DbContextOptionsBuilder<HSEDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.MigrationsAssembly("HSEService.Infrastructure"));

        return new HSEDbContext(optionsBuilder.Options);
    }
}
