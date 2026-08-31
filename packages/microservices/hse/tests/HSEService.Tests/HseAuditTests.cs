using System.Security.Claims;
using HSEService.Core.Entities;
using HSEService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HSEService.Tests;

/// <summary>
/// The HSE field-level audit trail (#216) — same shape as finance's <c>FinanceAuditTests</c>.
///
/// <para>Runs against SQLite rather than EF InMemory because the interceptor adds entities during
/// <c>SavingChanges</c>, and whether those additions are actually written in the same save is a
/// property of the real save pipeline, not of an in-memory dictionary.</para>
/// </summary>
public class HseAuditTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly HSEDbContext _db;

    public HseAuditTests() : this(actorId: "user-42") { }

    private HseAuditTests(string? actorId)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var accessor = new StubAccessor(actorId);

        _db = new HSEDbContext(new DbContextOptionsBuilder<HSEDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new HseAuditInterceptor(accessor))
            .Options);
        _db.Database.EnsureCreated();
    }

    private static HSEDbContext ContextWithoutRequest(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new HSEDbContext(new DbContextOptionsBuilder<HSEDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new HseAuditInterceptor(new StubAccessor(null)))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private Site AddSite(string name = "Nairobi Yard")
    {
        var s = new Site { Name = name, CreatedBy = "seed" };
        _db.Sites.Add(s);
        _db.SaveChanges();
        return s;
    }

    // ── It records at all ────────────────────────────────────────────────────────

    [Fact]
    public void A_create_is_recorded()
    {
        AddSite();

        var log = _db.HseAuditLogs.AsNoTracking().Single();
        log.Entity.Should().Be(nameof(Site));
        log.Action.Should().Be("Created");
        log.Actor.Should().Be("user-42");
    }

    [Fact]
    public void An_update_is_recorded_with_what_actually_changed()
    {
        var s = AddSite("Nairobi Yard");

        s.Name = "Nairobi Yard 2";
        _db.SaveChanges();

        var update = _db.HseAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Name");
        update.Details.Should().Contain("Nairobi Yard").And.Contain("Nairobi Yard 2");
    }

    [Fact]
    public void A_delete_is_recorded()
    {
        var s = AddSite();

        _db.Sites.Remove(s);
        _db.SaveChanges();

        _db.HseAuditLogs.AsNoTracking().Should().Contain(l => l.Action == "Deleted");
    }

    [Fact]
    public void The_audited_row_is_identified_by_its_primary_key()
    {
        var s = AddSite();

        _db.HseAuditLogs.AsNoTracking().Single().EntityId.Should().Be(s.Id);
    }

    // ── It cannot audit itself ───────────────────────────────────────────────────

    [Fact]
    public void Audit_rows_are_not_themselves_audited()
    {
        AddSite();

        _db.HseAuditLogs.AsNoTracking().Should().HaveCount(1);
    }

    [Fact]
    public void Repeated_saves_do_not_compound()
    {
        AddSite("Nairobi Yard");
        AddSite("Mombasa Yard");
        AddSite("Kisumu Yard");

        _db.HseAuditLogs.AsNoTracking().Should().HaveCount(3);
    }

    // ── Noise control ────────────────────────────────────────────────────────────

    [Fact]
    public void A_save_that_changes_nothing_records_nothing()
    {
        AddSite();
        var before = _db.HseAuditLogs.AsNoTracking().Count();

        _db.SaveChanges();

        _db.HseAuditLogs.AsNoTracking().Count().Should().Be(before);
    }

    [Fact]
    public void A_property_marked_modified_but_unchanged_is_not_reported()
    {
        var s = AddSite("Nairobi Yard");

        s.Location = "Industrial Area";       // genuinely changed (was null)
        s.Name = "Nairobi Yard";              // reassigned to the same value
        _db.SaveChanges();

        var update = _db.HseAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Location");
        update.Details.Should().NotContain("Name");
    }

    [Fact]
    public void A_create_records_no_field_level_detail()
    {
        AddSite();

        _db.HseAuditLogs.AsNoTracking().Single().Details.Should().BeNull();
    }

    // ── Attribution ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_write_with_no_request_behind_it_is_attributed_to_the_system()
    {
        using var db = ContextWithoutRequest(out var conn);
        using (conn)
        {
            db.Sites.Add(new Site { Name = "Nairobi Yard", CreatedBy = "seed" });
            db.SaveChanges();

            var log = db.HseAuditLogs.AsNoTracking().Single();
            log.Actor.Should().BeNull();
            log.CreatedBy.Should().Be("system");
        }
    }

    [Fact]
    public void Every_entity_type_is_covered_not_a_hand_listed_subset()
    {
        _db.ToolboxTalks.Add(new ToolboxTalk
        {
            SiteId = "site-1", SupervisorUserId = "user-1", Topic = "Ladder safety",
            HeldOn = new DateTime(2026, 1, 15), CreatedBy = "seed",
        });
        _db.SaveChanges();

        _db.HseAuditLogs.AsNoTracking()
            .Should().Contain(l => l.Entity == nameof(ToolboxTalk));
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
