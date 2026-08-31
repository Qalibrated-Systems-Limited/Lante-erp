using System.Security.Claims;
using CrmService.Core.Entities;
using CrmService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CrmService.Tests;

/// <summary>
/// The CRM field-level audit trail (#216) — same shape as finance's <c>FinanceAuditTests</c>.
///
/// <para>Runs against SQLite rather than EF InMemory because the interceptor adds entities during
/// <c>SavingChanges</c>, and whether those additions are actually written in the same save is a
/// property of the real save pipeline, not of an in-memory dictionary.</para>
/// </summary>
public class CrmAuditTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly CrmDbContext _db;

    public CrmAuditTests() : this(actorId: "user-42") { }

    private CrmAuditTests(string? actorId)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var accessor = new StubAccessor(actorId);

        _db = new CrmDbContext(new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new CrmAuditInterceptor(accessor))
            .Options);
        _db.Database.EnsureCreated();
    }

    private static CrmDbContext ContextWithoutRequest(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new CrmDbContext(new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new CrmAuditInterceptor(new StubAccessor(null)))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private Customer AddCustomer(string name = "Acme Ltd", string? email = "acme@example.com")
    {
        var c = new Customer { Name = name, Email = email, CreatedBy = "seed" };
        _db.Customers.Add(c);
        _db.SaveChanges();
        return c;
    }

    // ── It records at all ────────────────────────────────────────────────────────

    [Fact]
    public void A_create_is_recorded()
    {
        AddCustomer();

        var log = _db.CrmAuditLogs.AsNoTracking().Single();
        log.Entity.Should().Be(nameof(Customer));
        log.Action.Should().Be("Created");
        log.Actor.Should().Be("user-42");
    }

    [Fact]
    public void An_update_is_recorded_with_what_actually_changed()
    {
        var c = AddCustomer("Acme Ltd");

        c.Name = "Acme Holdings Ltd";
        _db.SaveChanges();

        var update = _db.CrmAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Name");
        update.Details.Should().Contain("Acme Ltd").And.Contain("Acme Holdings Ltd");
    }

    [Fact]
    public void A_delete_is_recorded()
    {
        var c = AddCustomer();

        _db.Customers.Remove(c);
        _db.SaveChanges();

        _db.CrmAuditLogs.AsNoTracking().Should().Contain(l => l.Action == "Deleted");
    }

    [Fact]
    public void The_audited_row_is_identified_by_its_primary_key()
    {
        var c = AddCustomer();

        _db.CrmAuditLogs.AsNoTracking().Single().EntityId.Should().Be(c.Id);
    }

    // ── It cannot audit itself ───────────────────────────────────────────────────

    [Fact]
    public void Audit_rows_are_not_themselves_audited()
    {
        AddCustomer();

        _db.CrmAuditLogs.AsNoTracking().Should().HaveCount(1);
    }

    [Fact]
    public void Repeated_saves_do_not_compound()
    {
        AddCustomer("Acme Ltd", "acme@example.com");
        AddCustomer("Beta Co", "beta@example.com");
        AddCustomer("Gamma Inc", "gamma@example.com");

        _db.CrmAuditLogs.AsNoTracking().Should().HaveCount(3);
    }

    // ── Noise control ────────────────────────────────────────────────────────────

    [Fact]
    public void A_save_that_changes_nothing_records_nothing()
    {
        AddCustomer();
        var before = _db.CrmAuditLogs.AsNoTracking().Count();

        _db.SaveChanges();

        _db.CrmAuditLogs.AsNoTracking().Count().Should().Be(before);
    }

    [Fact]
    public void A_property_marked_modified_but_unchanged_is_not_reported()
    {
        var c = AddCustomer("Acme Ltd", "acme@example.com");

        c.Email = "acme@example.com";        // reassigned to the same value
        c.Name = "Acme Holdings Ltd";        // genuinely changed
        _db.SaveChanges();

        var update = _db.CrmAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Name");
        update.Details.Should().NotContain("Email");
    }

    [Fact]
    public void A_create_records_no_field_level_detail()
    {
        AddCustomer();

        _db.CrmAuditLogs.AsNoTracking().Single().Details.Should().BeNull();
    }

    // ── Attribution ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_write_with_no_request_behind_it_is_attributed_to_the_system()
    {
        using var db = ContextWithoutRequest(out var conn);
        using (conn)
        {
            db.Customers.Add(new Customer { Name = "Acme Ltd", CreatedBy = "seed" });
            db.SaveChanges();

            var log = db.CrmAuditLogs.AsNoTracking().Single();
            log.Actor.Should().BeNull();
            log.CreatedBy.Should().Be("system");
        }
    }

    [Fact]
    public void Every_entity_type_is_covered_not_a_hand_listed_subset()
    {
        _db.PipelineStages.Add(new PipelineStage { Name = "Qualification", CreatedBy = "seed" });
        _db.SaveChanges();

        _db.CrmAuditLogs.AsNoTracking()
            .Should().Contain(l => l.Entity == nameof(PipelineStage));
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    /// <summary>A per-instance IHttpContextAccessor — the framework's stores HttpContext in a
    /// static AsyncLocal, so a second instance in the same async flow would otherwise leak the
    /// first test's actor.</summary>
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
