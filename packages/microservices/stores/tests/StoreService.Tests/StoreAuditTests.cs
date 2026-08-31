using System.Security.Claims;
using StoreService.Core.Entities;
using StoreService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace StoreService.Tests;

/// <summary>
/// The store field-level audit trail (#216) — same shape as finance's <c>FinanceAuditTests</c>.
///
/// <para>Runs against SQLite rather than EF InMemory because the interceptor adds entities during
/// <c>SavingChanges</c>, and whether those additions are actually written in the same save is a
/// property of the real save pipeline, not of an in-memory dictionary.</para>
/// </summary>
public class StoreAuditTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly StoreDbContext _db;

    public StoreAuditTests() : this(actorId: "user-42") { }

    private StoreAuditTests(string? actorId)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var accessor = new StubAccessor(actorId);

        _db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new StoreAuditInterceptor(accessor))
            .Options);
        _db.Database.EnsureCreated();
    }

    private static StoreDbContext ContextWithoutRequest(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new StoreAuditInterceptor(new StubAccessor(null)))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private Category AddCategory(string code = "CONS", string name = "Consumables")
    {
        var c = new Category { Code = code, Name = name };
        _db.Categories.Add(c);
        _db.SaveChanges();
        return c;
    }

    // ── It records at all ────────────────────────────────────────────────────────

    [Fact]
    public void A_create_is_recorded()
    {
        AddCategory();

        var log = _db.StoreAuditLogs.AsNoTracking().Single();
        log.Entity.Should().Be(nameof(Category));
        log.Action.Should().Be("Created");
        log.Actor.Should().Be("user-42");
    }

    [Fact]
    public void An_update_is_recorded_with_what_actually_changed()
    {
        var c = AddCategory("CONS", "Consumables");

        c.Name = "General Consumables";
        _db.SaveChanges();

        var update = _db.StoreAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Name");
        update.Details.Should().Contain("Consumables").And.Contain("General Consumables");
    }

    [Fact]
    public void A_delete_is_recorded()
    {
        var c = AddCategory();

        _db.Categories.Remove(c);
        _db.SaveChanges();

        _db.StoreAuditLogs.AsNoTracking().Should().Contain(l => l.Action == "Deleted");
    }

    [Fact]
    public void The_audited_row_is_identified_by_its_primary_key()
    {
        var c = AddCategory();

        _db.StoreAuditLogs.AsNoTracking().Single().EntityId.Should().Be(c.Id);
    }

    // ── It cannot audit itself ───────────────────────────────────────────────────

    [Fact]
    public void Audit_rows_are_not_themselves_audited()
    {
        AddCategory();

        _db.StoreAuditLogs.AsNoTracking().Should().HaveCount(1);
    }

    [Fact]
    public void Repeated_saves_do_not_compound()
    {
        AddCategory("CONS", "Consumables");
        AddCategory("PPE", "PPE & Safety Equipment");
        AddCategory("TOOL", "Tools");

        _db.StoreAuditLogs.AsNoTracking().Should().HaveCount(3);
    }

    // ── Noise control ────────────────────────────────────────────────────────────

    [Fact]
    public void A_save_that_changes_nothing_records_nothing()
    {
        AddCategory();
        var before = _db.StoreAuditLogs.AsNoTracking().Count();

        _db.SaveChanges();

        _db.StoreAuditLogs.AsNoTracking().Count().Should().Be(before);
    }

    [Fact]
    public void A_property_marked_modified_but_unchanged_is_not_reported()
    {
        var c = AddCategory("CONS", "Consumables");

        c.Code = "CONS";               // reassigned to the same value
        c.Name = "General Consumables"; // genuinely changed
        _db.SaveChanges();

        var update = _db.StoreAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Name");
        update.Details.Should().NotContain("Code");
    }

    [Fact]
    public void A_create_records_no_field_level_detail()
    {
        AddCategory();

        _db.StoreAuditLogs.AsNoTracking().Single().Details.Should().BeNull();
    }

    // ── Attribution ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_write_with_no_request_behind_it_is_attributed_to_the_system()
    {
        using var db = ContextWithoutRequest(out var conn);
        using (conn)
        {
            db.Categories.Add(new Category { Code = "CONS", Name = "Consumables" });
            db.SaveChanges();

            var log = db.StoreAuditLogs.AsNoTracking().Single();
            log.Actor.Should().BeNull();
            log.CreatedBy.Should().Be("system");
        }
    }

    [Fact]
    public void Every_entity_type_is_covered_not_a_hand_listed_subset()
    {
        _db.UnitsOfMeasure.Add(new UnitOfMeasure { Name = "kg" });
        _db.SaveChanges();

        _db.StoreAuditLogs.AsNoTracking()
            .Should().Contain(l => l.Entity == nameof(UnitOfMeasure));
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
