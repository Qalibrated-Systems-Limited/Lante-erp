using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Infrastructure.Data;
using TicketingService.Infrastructure.Repositories;
using Xunit;

namespace TicketingService.Tests;

/// <summary>
/// Alert deduplication under more than one replica.
///
/// <para><c>AlertService.CreateAsync</c> calls <c>ExistsOpenAsync</c> and then inserts. That is
/// check-then-act, and at two replicas both timers see "no open alert" and both insert — so one SLA
/// breach raises two alerts and notifies twice (#219). <c>replicaCount: 1</c> was the only thing
/// preventing it, which made a config value load-bearing for correctness.</para>
///
/// <para><b>SQLite, not EF InMemory.</b> InMemory does not enforce unique indexes at all, so these tests
/// would pass on it whether or not the index exists — the exact opposite of their purpose. SQLite
/// enforces partial unique indexes, so the constraint is genuinely exercised.</para>
///
/// <para>The race itself is not simulated with threads: two sequential inserts that both pass the
/// existence check reproduce the same end state the interleaving produces, and deterministically.
/// What is being asserted is that the DATABASE refuses the second one, which is what makes the outcome
/// correct regardless of interleaving.</para>
/// </summary>
public class AlertDedupTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly TicketingDbContext _db;
    private readonly AlertRepository _repo;

    public AlertDedupTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new TicketingDbContext(
            new DbContextOptionsBuilder<TicketingDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new AlertRepository(_db);
    }

    private static Alert Breach(string title = "SLA breached on TKT-001", string source = "SLA",
                                string tenantId = "tenant-a") => new()
    {
        TenantId = tenantId, Source = source, Title = title,
        Severity = "Warning", Message = "Response time exceeded.",
    };

    [Fact]
    public async Task The_first_open_alert_is_inserted()
    {
        (await _repo.AddAsync(Breach())).Should().BeTrue();
        (await _db.Alerts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task An_identical_open_alert_is_refused_by_the_database()
    {
        await _repo.AddAsync(Breach());

        // Asserted at the DATABASE, not through AddAsync's swallow. Both callers pass their existence
        // check; what makes only one row exist is the index, and that is provider-independent. The
        // swallow itself is Postgres-specific by design — see IsOpenAlertDuplicateTests, which covers it
        // as a pure function rather than making this project depend on a test-only provider.
        _db.Alerts.Add(Breach());
        var act = () => _db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
        _db.ChangeTracker.Clear();
        (await _db.Alerts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_index_covers_exactly_the_condition_ExistsOpenAsync_checks()
    {
        await _repo.AddAsync(Breach());

        // ExistsOpenAsync filters on TenantId + Source + Title while not acknowledged (and the global
        // query filter adds not deleted). If the index and that predicate ever diverge, the check and the
        // constraint would disagree about what a duplicate IS — one would pass what the other rejects.
        var open = await _repo.ExistsOpenAsync("tenant-a", "SLA", "SLA breached on TKT-001");
        open.Should().BeTrue();

        _db.Alerts.Add(Breach());
        await ((Func<Task>)(() => _db.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Acknowledging_an_alert_lets_the_same_condition_raise_a_fresh_one()
    {
        await _repo.AddAsync(Breach());
        var existing = await _db.Alerts.SingleAsync();
        existing.IsAcknowledged = true;
        existing.AcknowledgedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var again = await _repo.AddAsync(Breach());

        // Why the index is PARTIAL rather than plain. Acknowledging says "I have dealt with this"; if the
        // condition recurs it must be able to alert again, or acknowledging one breach would silence that
        // ticket forever.
        again.Should().BeTrue();
        (await _db.Alerts.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_soft_deleted_alert_does_not_block_a_new_one_either()
    {
        await _repo.AddAsync(Breach());
        var existing = await _db.Alerts.SingleAsync();
        existing.IsDeleted = true;
        await _db.SaveChangesAsync();

        (await _repo.AddAsync(Breach())).Should().BeTrue();
    }

    [Fact]
    public async Task Different_titles_are_not_deduped_against_each_other()
    {
        await _repo.AddAsync(Breach(title: "SLA breached on TKT-001"));

        (await _repo.AddAsync(Breach(title: "SLA breached on TKT-002"))).Should().BeTrue();
    }

    [Fact]
    public async Task Different_sources_are_not_deduped_against_each_other()
    {
        await _repo.AddAsync(Breach(source: "SLA"));

        // The same title raised by escalation is a different alert from the one raised by SLA.
        (await _repo.AddAsync(Breach(source: "Escalation"))).Should().BeTrue();
    }

    [Fact]
    public async Task Two_tenants_can_hold_the_same_alert()
    {
        await _repo.AddAsync(Breach(tenantId: "tenant-a"));

        // Alerts are a shared platform-wide table keyed by TenantId, so the index must be scoped by it
        // or one tenant's breach would suppress another's.
        (await _repo.AddAsync(Breach(tenantId: "tenant-b"))).Should().BeTrue();
        (await _db.Alerts.CountAsync()).Should().Be(2);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}

/// <summary>
/// The swallow decision, as a pure function.
///
/// <para>Separated from the database tests deliberately. Recognising a duplicate is Postgres-specific —
/// it turns on SQLSTATE 23505 and the constraint name — and this project must not take a dependency on a
/// test-only provider to exercise it. Keeping the decision pure means the narrowness can be asserted,
/// which matters: swallowing broadly on a background worker is how a queue silently stops working.</para>
/// </summary>
public class IsOpenAlertDuplicateTests
{
    [Fact]
    public void A_unique_violation_on_the_open_alert_index_is_a_duplicate()
        => AlertRepository.IsOpenAlertDuplicate("23505", "IX_Alerts_TenantId_Source_Title")
            .Should().BeTrue();

    [Fact]
    public void A_unique_violation_with_no_constraint_name_is_accepted()
        // Not every Npgsql path populates it, and a violation reaching this repository can only come
        // from the Alerts table it writes.
        => AlertRepository.IsOpenAlertDuplicate("23505", null).Should().BeTrue();

    [Fact]
    public void A_unique_violation_on_a_DIFFERENT_index_is_not_swallowed()
        // The ticket-number index is also unique. Treating its violation as "an alert already exists"
        // would discard a real write failure and report success.
        => AlertRepository.IsOpenAlertDuplicate("23505", "IX_Tickets_TenantId_TicketNumber")
            .Should().BeFalse();

    [Theory]
    [InlineData("23503")]   // foreign_key_violation
    [InlineData("23502")]   // not_null_violation
    [InlineData("40001")]   // serialization_failure
    [InlineData("57014")]   // query_canceled
    [InlineData(null)]
    public void Any_other_failure_propagates(string? sqlState)
        // A background worker that swallows serialization failures and cancellations looks healthy while
        // doing nothing, which is worse than crashing.
        => AlertRepository.IsOpenAlertDuplicate(sqlState, "IX_Alerts_TenantId_Source_Title")
            .Should().BeFalse();
}
