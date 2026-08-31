using System.Security.Claims;
using TicketingService.Core.Entities;
using TicketingService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TicketingService.Tests;

/// <summary>
/// The ticketing field-level audit trail (#216) — same shape as finance's
/// <c>FinanceAuditTests</c>.
///
/// <para>Runs against SQLite rather than EF InMemory because the interceptor adds entities during
/// <c>SavingChanges</c>, and whether those additions are actually written in the same save is a
/// property of the real save pipeline, not of an in-memory dictionary.</para>
/// </summary>
public class TicketingAuditTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly TicketingDbContext _db;

    public TicketingAuditTests() : this(actorId: "user-42") { }

    private TicketingAuditTests(string? actorId)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var accessor = new StubAccessor(actorId);

        _db = new TicketingDbContext(new DbContextOptionsBuilder<TicketingDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new TicketingAuditInterceptor(accessor))
            .Options);
        _db.Database.EnsureCreated();
    }

    private static TicketingDbContext ContextWithoutRequest(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new TicketingDbContext(new DbContextOptionsBuilder<TicketingDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new TicketingAuditInterceptor(new StubAccessor(null)))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private Tag AddTag(string name = "Urgent")
    {
        var t = new Tag { Name = name, CreatedBy = "seed" };
        _db.Tags.Add(t);
        _db.SaveChanges();
        return t;
    }

    // ── It records at all ────────────────────────────────────────────────────────

    [Fact]
    public void A_create_is_recorded()
    {
        AddTag();

        var log = _db.TicketingAuditLogs.AsNoTracking().Single();
        log.Entity.Should().Be(nameof(Tag));
        log.Action.Should().Be("Created");
        log.Actor.Should().Be("user-42");
    }

    [Fact]
    public void An_update_is_recorded_with_what_actually_changed()
    {
        var t = AddTag("Urgent");

        t.Name = "Critical";
        _db.SaveChanges();

        var update = _db.TicketingAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Name");
        update.Details.Should().Contain("Urgent").And.Contain("Critical");
    }

    [Fact]
    public void A_delete_is_recorded()
    {
        var t = AddTag();

        _db.Tags.Remove(t);
        _db.SaveChanges();

        _db.TicketingAuditLogs.AsNoTracking().Should().Contain(l => l.Action == "Deleted");
    }

    [Fact]
    public void The_audited_row_is_identified_by_its_primary_key()
    {
        var t = AddTag();

        _db.TicketingAuditLogs.AsNoTracking().Single().EntityId.Should().Be(t.Id);
    }

    // ── It cannot audit itself ───────────────────────────────────────────────────

    [Fact]
    public void Audit_rows_are_not_themselves_audited()
    {
        AddTag();

        _db.TicketingAuditLogs.AsNoTracking().Should().HaveCount(1);
    }

    [Fact]
    public void Repeated_saves_do_not_compound()
    {
        AddTag("Urgent");
        AddTag("Billing");
        AddTag("Escalated");

        _db.TicketingAuditLogs.AsNoTracking().Should().HaveCount(3);
    }

    // ── Noise control ────────────────────────────────────────────────────────────

    [Fact]
    public void A_save_that_changes_nothing_records_nothing()
    {
        AddTag();
        var before = _db.TicketingAuditLogs.AsNoTracking().Count();

        _db.SaveChanges();

        _db.TicketingAuditLogs.AsNoTracking().Count().Should().Be(before);
    }

    [Fact]
    public void A_property_marked_modified_but_unchanged_is_not_reported()
    {
        var t = AddTag("Urgent");

        t.Color = "#FF0000";     // genuinely changed (was null)
        t.Name = "Urgent";       // reassigned to the same value
        _db.SaveChanges();

        var update = _db.TicketingAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Color");
        update.Details.Should().NotContain("Name");
    }

    [Fact]
    public void A_create_records_no_field_level_detail()
    {
        AddTag();

        _db.TicketingAuditLogs.AsNoTracking().Single().Details.Should().BeNull();
    }

    // ── Attribution ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_write_with_no_request_behind_it_is_attributed_to_the_system()
    {
        using var db = ContextWithoutRequest(out var conn);
        using (conn)
        {
            db.Tags.Add(new Tag { Name = "Urgent", CreatedBy = "seed" });
            db.SaveChanges();

            var log = db.TicketingAuditLogs.AsNoTracking().Single();
            log.Actor.Should().BeNull();
            log.CreatedBy.Should().Be("system");
        }
    }

    [Fact]
    public void Every_entity_type_is_covered_not_a_hand_listed_subset()
    {
        _db.TicketCategories.Add(new TicketCategory { Name = "Billing", DepartmentId = "dept-1", CreatedBy = "seed" });
        _db.SaveChanges();

        _db.TicketingAuditLogs.AsNoTracking()
            .Should().Contain(l => l.Entity == nameof(TicketCategory));
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
