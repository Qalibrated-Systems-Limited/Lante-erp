using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReportingService.Core.Entities;
using ReportingService.Infrastructure.BackgroundServices;
using ReportingService.Infrastructure.Data;
using Xunit;

namespace ReportingService.Tests;

/// <summary>
/// Claiming a due report schedule, which is what stops two replicas running and EMAILING the same
/// report (#219).
///
/// <para>The scheduler reads schedules where <c>NextRunAt &lt;= now</c>, then generates the report and
/// delivers it, then writes <c>NextRunAt</c>. Between the read and the write sits report generation plus
/// HTTP calls to other services — so at two replicas both see the same schedule as due, both generate,
/// and both email. <c>replicaCount: 1</c> was the only thing preventing it.</para>
///
/// <para>These tests exercise the claim as the SQL it actually is, against SQLite. The claim's entire
/// behaviour is "how many rows did that UPDATE change", which EF InMemory cannot answer at all —
/// <c>ExecuteSqlRaw</c> needs a relational provider — so the thing under test would be untestable there.
/// The statement is written here rather than invoked through the BackgroundService because that class
/// needs a service scope, an HTTP context and a token issuer to reach the claim; what matters is the
/// exclusion the SQL provides, and that is what is asserted.</para>
/// </summary>
public class ScheduleClaimTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ReportingDbContext _db;

    private static readonly DateTime Now = new(2026, 8, 19, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Next = new(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc);

    public ScheduleClaimTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ReportingDbContext(
            new DbContextOptionsBuilder<ReportingDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();

        // ReportSchedule has an FK to ReportDefinition, which SQLite enforces (and Postgres does too).
        // Seeding it is a precondition rather than scaffolding: without the parent the claim's UPDATE
        // would never have a row to act on.
        _db.ReportDefinitions.Add(new ReportDefinition
        {
            Id = "def-1", Key = "debtors-aging", Name = "Debtors aging", IsActive = true,
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private ReportSchedule Seed(DateTime? nextRunAt, bool isActive = true, bool isDeleted = false)
    {
        var s = new ReportSchedule
        {
            ReportDefinitionId = "def-1", CronExpression = "0 9 * * *",
            IsActive = isActive, IsDeleted = isDeleted, NextRunAt = nextRunAt,
        };
        _db.ReportSchedules.Add(s);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return s;
    }

    /// <summary>
    /// Executes the PRODUCTION statement, not a copy of it. An earlier version of this file held its own
    /// transcription of the SQL — which meant mutating it proved these tests were sensitive to the
    /// semantics, and proved nothing about whether the shipped statement had them.
    /// </summary>
    private Task<int> ClaimAsync(string id) => _db.Database.ExecuteSqlRawAsync(
        ReportSchedulerBackgroundService.ClaimSql, Now, Next, id, Now);

    // ── Exclusion ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_due_schedule_is_claimed_once()
    {
        var s = Seed(nextRunAt: Now.AddMinutes(-5));

        (await ClaimAsync(s.Id)).Should().Be(1);
    }

    [Fact]
    public async Task The_second_claim_on_the_same_schedule_gets_nothing()
    {
        var s = Seed(nextRunAt: Now.AddMinutes(-5));
        await ClaimAsync(s.Id);

        // This is the whole point. Both replicas read the row as due; the UPDATE's own WHERE re-checks
        // due-ness atomically, so the loser changes zero rows and skips instead of generating and
        // emailing a second copy.
        (await ClaimAsync(s.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_never_run_schedule_is_claimable()
    {
        var s = Seed(nextRunAt: null);

        // NextRunAt null legitimately means "never run yet, run now" for a fresh schedule.
        (await ClaimAsync(s.Id)).Should().Be(1);
        (await ClaimAsync(s.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_schedule_not_yet_due_is_not_claimable()
    {
        var s = Seed(nextRunAt: Now.AddHours(1));

        (await ClaimAsync(s.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_schedule_due_exactly_now_is_claimable()
    {
        var s = Seed(nextRunAt: Now);

        // The due query uses <=, so the claim must too, or a schedule landing precisely on the tick
        // would be read as due and then refuse to be claimed — silently never running.
        (await ClaimAsync(s.Id)).Should().Be(1);
    }

    // ── The other conditions must be re-checked, not assumed ────────────────────

    [Fact]
    public async Task An_inactive_schedule_cannot_be_claimed()
    {
        var s = Seed(nextRunAt: Now.AddMinutes(-5), isActive: false);

        // Re-checked in the UPDATE rather than trusted from the read: a schedule deactivated between the
        // read and the claim must not run.
        (await ClaimAsync(s.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_soft_deleted_schedule_cannot_be_claimed()
    {
        var s = Seed(nextRunAt: Now.AddMinutes(-5), isDeleted: true);

        (await ClaimAsync(s.Id)).Should().Be(0);
    }

    // ── What the claim writes ───────────────────────────────────────────────────

    [Fact]
    public async Task Claiming_advances_the_schedule_and_records_the_run_time()
    {
        var s = Seed(nextRunAt: Now.AddMinutes(-5));

        await ClaimAsync(s.Id);

        var after = await _db.ReportSchedules.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        after.LastRunAt.Should().Be(Now);
        after.NextRunAt.Should().Be(Next);
    }

    [Fact]
    public async Task Claiming_one_schedule_leaves_others_claimable()
    {
        var a = Seed(nextRunAt: Now.AddMinutes(-5));
        var b = Seed(nextRunAt: Now.AddMinutes(-5));

        // The claim is per schedule, not per tick, so two instances can each take a different due
        // schedule concurrently rather than one blocking the other.
        (await ClaimAsync(a.Id)).Should().Be(1);
        (await ClaimAsync(b.Id)).Should().Be(1);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
