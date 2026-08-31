using System.Security.Claims;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// The finance audit trail.
///
/// <para><c>FinanceAuditLog</c> existed from finance's <c>InitialCreate</c> and had never recorded a
/// row — the entity and the DbSet were its only two references anywhere in the service (#285). It
/// shipped to every tenant schema reading as an audit trail while being permanently empty, and it sat
/// behind the 79 endpoints that enforced no permissions until #277.</para>
///
/// <para>These tests run against SQLite rather than EF InMemory because the interceptor adds entities
/// during <c>SavingChanges</c>, and whether those additions are actually written in the same save is a
/// property of the real save pipeline, not of an in-memory dictionary.</para>
/// </summary>
public class FinanceAuditTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly FinanceDbContext _db;

    public FinanceAuditTests() : this(actorId: "user-42") { }

    private FinanceAuditTests(string? actorId)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var accessor = new StubAccessor(actorId);

        _db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new FinanceAuditInterceptor(accessor))
            .Options);
        _db.Database.EnsureCreated();
    }

    private static FinanceDbContext ContextWithoutRequest(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new FinanceAuditInterceptor(new StubAccessor(null)))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private Currency AddCurrency(string code = "KES", decimal rate = 1m)
    {
        var c = new Currency { Code = code, Name = code, ExchangeRate = rate, IsBaseCurrency = code == "KES" };
        _db.Currencies.Add(c);
        _db.SaveChanges();
        return c;
    }

    // ── It records at all ────────────────────────────────────────────────────────

    [Fact]
    public void A_create_is_recorded()
    {
        AddCurrency();

        var log = _db.FinanceAuditLogs.AsNoTracking().Single();
        // The table had never held a row. This is the assertion the whole change exists for.
        log.Entity.Should().Be(nameof(Currency));
        log.Action.Should().Be("Created");
        log.Actor.Should().Be("user-42");
    }

    [Fact]
    public void An_update_is_recorded_with_what_actually_changed()
    {
        var c = AddCurrency("USD", 130m);

        c.ExchangeRate = 200m;
        _db.SaveChanges();

        var update = _db.FinanceAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        // #216's complaint is that the audit log can say which endpoint was called but not what
        // changed. An interceptor sees original and current values, so it can.
        update.Details.Should().Contain("ExchangeRate");
        update.Details.Should().Contain("130").And.Contain("200");
    }

    [Fact]
    public void A_delete_is_recorded()
    {
        var c = AddCurrency("USD", 130m);

        _db.Currencies.Remove(c);
        _db.SaveChanges();

        _db.FinanceAuditLogs.AsNoTracking().Should().Contain(l => l.Action == "Deleted");
    }

    [Fact]
    public void The_audited_row_is_identified_by_its_primary_key()
    {
        var c = AddCurrency();

        _db.FinanceAuditLogs.AsNoTracking().Single().EntityId.Should().Be(c.Id);
    }

    // ── It cannot audit itself ───────────────────────────────────────────────────

    [Fact]
    public void Audit_rows_are_not_themselves_audited()
    {
        AddCurrency();

        // One write, one audit row. If audit rows were audited, each save would append rows describing
        // the rows it just appended — an unbounded loop, not merely noise.
        _db.FinanceAuditLogs.AsNoTracking().Should().HaveCount(1);
    }

    [Fact]
    public void Repeated_saves_do_not_compound()
    {
        AddCurrency("KES");
        AddCurrency("USD", 130m);
        AddCurrency("EUR", 150m);

        _db.FinanceAuditLogs.AsNoTracking().Should().HaveCount(3);
    }

    // ── Noise control ────────────────────────────────────────────────────────────

    [Fact]
    public void A_save_that_changes_nothing_records_nothing()
    {
        AddCurrency();
        var before = _db.FinanceAuditLogs.AsNoTracking().Count();

        _db.SaveChanges();

        // An audit trail full of "nothing happened" entries is one nobody reads.
        _db.FinanceAuditLogs.AsNoTracking().Count().Should().Be(before);
    }

    [Fact]
    public void A_property_marked_modified_but_unchanged_is_not_reported()
    {
        var c = AddCurrency("USD", 130m);

        c.ExchangeRate = 130m;              // reassigned to the same value
        c.Name = "US Dollar";               // genuinely changed
        _db.SaveChanges();

        var update = _db.FinanceAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Name");
        // I first wrote this believing EF marks a property modified on assignment regardless of value,
        // and that the interceptor filtered those out. It does not need to: reassigning the current
        // value leaves IsModified false and the entity Unchanged. So this asserts EF's behaviour, which
        // is still worth pinning — it is the behaviour the change summary depends on, and the EF 8→9
        // bump (#227) is exactly when it could move.
        update.Details.Should().NotContain("ExchangeRate");
    }

    [Fact]
    public void A_create_records_no_field_level_detail()
    {
        AddCurrency();

        // On an insert the row IS the detail. Note this holds for two independent reasons — the
        // explicit state check, and EF reporting no modified properties on an Added entity — so
        // removing either one alone keeps this green. Pinning the outcome rather than the mechanism.
        _db.FinanceAuditLogs.AsNoTracking().Single().Details.Should().BeNull();
    }

    // ── Attribution ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_write_with_no_request_behind_it_is_attributed_to_the_system()
    {
        using var db = ContextWithoutRequest(out var conn);
        using (conn)
        {
            db.Currencies.Add(new Currency { Code = "KES", Name = "KES", IsBaseCurrency = true, ExchangeRate = 1m });
            db.SaveChanges();

            var log = db.FinanceAuditLogs.AsNoTracking().Single();
            // A background sweep has no HttpContext. "system" is honest — nobody clicked. Attributing it
            // to the last known user would be worse than leaving it blank.
            log.Actor.Should().BeNull();
            log.CreatedBy.Should().Be("system");
        }
    }

    [Fact]
    public void Every_entity_type_is_covered_not_a_hand_listed_subset()
    {
        _db.TaxCategories.Add(new TaxCategory { Code = "A", Name = "Standard rate", Rate = 0.16m });
        _db.SaveChanges();

        // The point of intercepting SaveChanges rather than calling a logger at each site: a new entity
        // or a new controller is covered without anyone remembering to add it. Nothing about TaxCategory
        // is named anywhere in the interceptor.
        _db.FinanceAuditLogs.AsNoTracking()
            .Should().Contain(l => l.Entity == nameof(TaxCategory));
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    /// <summary>
    /// A per-instance IHttpContextAccessor. The framework's HttpContextAccessor stores HttpContext in a
    /// STATIC AsyncLocal, so a second instance created in the same async flow still sees the first
    /// test's user — which made the "no request behind it" test read a signed-in actor and fail. The
    /// bug was in the test, not the interceptor, but it is exactly the kind of shared-state surprise
    /// worth a note rather than a silent workaround.
    /// </summary>
    private sealed class StubAccessor(string? actorId) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = actorId is null
            ? null
            : new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId)], "TestAuth")),
            };
    }
}
