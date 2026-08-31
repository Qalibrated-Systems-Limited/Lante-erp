using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Infrastructure.Data;
using FinanceService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Runtime.CompilerServices;

namespace FinanceService.Tests;

/// <summary>
/// Mirrors the one AppContext switch FinanceService.Api sets at startup.
///
/// <para>This is not optional plumbing. With the switch off, Npgsql maps <c>DateTime</c> to
/// <c>timestamp with time zone</c> and refuses to write a <c>Kind=Unspecified</c> value at all; with
/// it on, it maps to <c>timestamp without time zone</c> — which is what #345 reconciled every finance
/// column to. So a fixture that omits it builds a schema the application would never build and
/// rejects values the application accepts, and every failure it produced would be an artefact of the
/// test host.</para>
///
/// <para>That makes <b>three</b> places this switch has to be repeated: <c>Program.cs</c>,
/// <c>DesignTimeFactories</c> (#340, and #337 was the fallout of it being missing), and now here.
/// <c>validate_designtime_switch_mirror.py</c> checks the first two against each other; it does not
/// know about test hosts. Worth centralising rather than adding a fourth copy.</para>
///
/// <para>A module initialiser rather than a static constructor, because Npgsql reads the switch when
/// its type mappings are first built — which can happen before any fixture is constructed.</para>
/// </summary>
internal static class NpgsqlTestSwitches
{
    [ModuleInitializer]
    internal static void Init() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
}

/// <summary>
/// A throwaway PostgreSQL database per test, for the paths no other provider can express.
///
/// <para><b>Why this exists.</b> Three things in finance are invisible to the in-memory and SQLite
/// providers and were therefore untested: <c>pg_advisory_xact_lock</c> (no such function),
/// transactions (in-memory has none), and <c>ExecuteUpdateAsync</c> (in-memory does not implement
/// it). Those are exactly the guards that stop money being posted twice, so "untestable" left the
/// highest-value concurrency code in the service with no coverage at all — noted on #230 and #362.</para>
///
/// <para><b>EnsureCreated, not Migrate</b>, deliberately. The schema is built from the current model,
/// which is what the code under test compiles against. Running the migration history instead would
/// make every test in this class fail on unrelated migration drift — the exact problem #337 spent a
/// week on — and these tests are about service behaviour, not about the migrations.</para>
///
/// <para><b>A missing database is not silently skipped in CI.</b> A gate that quietly never runs
/// protects nothing, which is the whole substance of #214 and #253. Locally, an absent
/// <c>LANTE_TEST_POSTGRES</c> skips these tests so a developer without Docker is not blocked; under
/// <c>CI=true</c> the same absence throws, so a workflow that loses its postgres service fails
/// loudly instead of going green on zero tests.</para>
/// </summary>
public sealed class PostgresFixture : IAsyncDisposable
{
    /// <summary>Set by CI and by a developer running a local container. See the class remarks.</summary>
    public const string ConnectionEnvVar = "LANTE_TEST_POSTGRES";

    private readonly string _adminConnectionString;
    private readonly string _databaseName;

    public FinanceDbContext Db { get; }
    public JournalService Journals { get; }
    public DepreciationService Depreciation { get; }

    public const string PeriodId = "per-current";
    public const string AssetId = "asset-1";

    /// <summary>
    /// Null when these tests cannot run, with the reason. Callers skip on it rather than failing —
    /// except in CI, where <see cref="RequireAvailable"/> turns it into a hard failure.
    /// </summary>
    public static string? Unavailable()
    {
        var cs = Environment.GetEnvironmentVariable(ConnectionEnvVar);
        if (!string.IsNullOrWhiteSpace(cs)) return null;

        var inCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
        if (inCi)
            throw new InvalidOperationException(
                $"{ConnectionEnvVar} is not set but CI=true. These tests cover pg_advisory_xact_lock and " +
                "ExecuteUpdateAsync, which no other provider can express — skipping them in CI would mean " +
                "the double-post guards are unverified while the build goes green. Add the postgres service " +
                "back to the workflow.");

        return $"{ConnectionEnvVar} is not set — start a local postgres and set it to run these.";
    }

    /// <summary>The message shown on a skipped test, so the reason is visible in the run output
    /// rather than inferred from an absence.</summary>
    public static string SkipReason =>
        $"{ConnectionEnvVar} is not set — these cover pg_advisory_xact_lock and need a real postgres. "
        + "Start one and set the variable to run them.";

    private PostgresFixture(string adminConnectionString, string databaseName, FinanceDbContext db)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        Db = db;
        Journals = new JournalService(db);
        Depreciation = new DepreciationService(db, Journals);
    }

    public static async Task<PostgresFixture> CreateAsync()
    {
        var baseCs = Environment.GetEnvironmentVariable(ConnectionEnvVar)
            ?? throw new InvalidOperationException($"{ConnectionEnvVar} is not set.");

        // A database per fixture rather than a schema per fixture: these tests exercise transactions
        // and session-scoped advisory locks, and sharing a database would let one test's lock block
        // another's unrelated one. Cheap enough — EnsureCreated on this model takes about a second.
        var builder = new NpgsqlConnectionStringBuilder(baseCs);
        var admin = new NpgsqlConnectionStringBuilder(baseCs) { Database = "postgres" }.ConnectionString;
        var name = $"lante_fin_{Guid.NewGuid():N}";

        await using (var conn = new NpgsqlConnection(admin))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        builder.Database = name;
        var db = new FinanceDbContext(
            new DbContextOptionsBuilder<FinanceDbContext>().UseNpgsql(builder.ConnectionString).Options);
        await db.Database.EnsureCreatedAsync();

        var fixture = new PostgresFixture(admin, name, db);
        await fixture.SeedAsync();
        return fixture;
    }

    private async Task SeedAsync()
    {
        var today = DateTime.UtcNow.Date;
        Db.Currencies.Add(new Currency
        {
            Id = "ccy-kes", Code = "KES", Name = "Kenya Shilling", IsBaseCurrency = true, IsActive = true,
        });
        Db.FiscalYears.Add(new FiscalYear
        {
            Id = "fy", Name = today.Year.ToString(),
            StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31),
        });
        Db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = PeriodId, FiscalYearId = "fy", PeriodNo = today.Month, Name = today.ToString("yyyy-MM"),
            StartDate = new DateTime(today.Year, today.Month, 1),
            EndDate = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            Status = PeriodStatus.Open,
        });
        // Classification lives on AccountType, not on the account, so both are needed.
        Db.AccountTypes.AddRange(
            new AccountType { Id = "at-exp", Code = "EXP", Name = "Expense", Classification = AccountClassification.Expense, NormalBalance = NormalBalance.Debit },
            new AccountType { Id = "at-ast", Code = "AST", Name = "Asset", Classification = AccountClassification.Asset, NormalBalance = NormalBalance.Debit });

        // Only the two accounts depreciation posts to.
        Db.ChartOfAccounts.AddRange(
            new ChartOfAccount { Id = "acc-5600", Code = "5600", Name = "Depreciation Expense", AccountTypeId = "at-exp", IsDirectPosting = true, IsActive = true },
            new ChartOfAccount { Id = "acc-1520", Code = "1520", Name = "Accumulated Depreciation", AccountTypeId = "at-ast", IsDirectPosting = true, IsActive = true });

        Db.AssetCategories.Add(new AssetCategory
        {
            Id = "cat-1", Name = "Motor Vehicles", AnnualRate = 0.20m, UsefulLifeLabel = "5 years",
        });
        Db.FixedAssets.Add(new FixedAsset
        {
            Id = AssetId, CategoryId = "cat-1", AssetTag = "FA-2026-0001", Description = "Pickup",
            AcquisitionCost = 1_200_000m, AccumulatedDepreciation = 0m,
            AcquisitionDate = new DateTime(today.Year, 1, 1), Status = "Active",
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>
    /// A second fixture over the SAME database, for concurrency tests. A DbContext is not
    /// thread-safe, so two racing callers need two contexts — sharing one would serialise them in
    /// the client and test nothing about the lock.
    /// </summary>
    public static Task<PostgresFixture> CreateForSameDatabaseAsync(PostgresFixture other)
    {
        var builder = new NpgsqlConnectionStringBuilder(other.Db.Database.GetConnectionString());
        var db = new FinanceDbContext(
            new DbContextOptionsBuilder<FinanceDbContext>().UseNpgsql(builder.ConnectionString).Options);
        // Shares the database name, so its Dispose must NOT drop it — the owner does that.
        return Task.FromResult(new PostgresFixture(other._adminConnectionString, other._databaseName, db)
        {
            _ownsDatabase = false,
        });
    }

    private bool _ownsDatabase = true;

    /// <summary>The period string RunDepreciationAsync takes, for the current month.</summary>
    public static string CurrentPeriod() => DateTime.UtcNow.ToString("yyyy-MM");

    public async ValueTask DisposeAsync()
    {
        if (!_ownsDatabase)
        {
            await Db.DisposeAsync();
            return;
        }

        await Db.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(_adminConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
