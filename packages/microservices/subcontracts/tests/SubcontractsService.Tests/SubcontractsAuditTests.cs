using System.Security.Claims;
using SubcontractsService.Core.Entities;
using SubcontractsService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SubcontractsService.Tests;

/// <summary>
/// The subcontracts field-level audit trail (#216) — same shape as finance's
/// <c>FinanceAuditTests</c>.
///
/// <para>Runs against SQLite rather than EF InMemory because the interceptor adds entities during
/// <c>SavingChanges</c>, and whether those additions are actually written in the same save is a
/// property of the real save pipeline, not of an in-memory dictionary.</para>
/// </summary>
public class SubcontractsAuditTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly SubcontractsDbContext _db;

    public SubcontractsAuditTests() : this(actorId: "user-42") { }

    private SubcontractsAuditTests(string? actorId)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var accessor = new StubAccessor(actorId);

        _db = new SubcontractsDbContext(new DbContextOptionsBuilder<SubcontractsDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new SubcontractsAuditInterceptor(accessor))
            .Options);
        _db.Database.EnsureCreated();
    }

    private static SubcontractsDbContext ContextWithoutRequest(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new SubcontractsDbContext(new DbContextOptionsBuilder<SubcontractsDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new SubcontractsAuditInterceptor(new StubAccessor(null)))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private Subcontractor AddSubcontractor(string name = "Acme Roofing", string tradeCategory = "Roofing")
    {
        var s = new Subcontractor { Name = name, TradeCategory = tradeCategory, CreatedBy = "seed" };
        _db.Subcontractors.Add(s);
        _db.SaveChanges();
        return s;
    }

    // ── It records at all ────────────────────────────────────────────────────────

    [Fact]
    public void A_create_is_recorded()
    {
        AddSubcontractor();

        var log = _db.SubcontractsAuditLogs.AsNoTracking().Single();
        log.Entity.Should().Be(nameof(Subcontractor));
        log.Action.Should().Be("Created");
        log.Actor.Should().Be("user-42");
    }

    [Fact]
    public void An_update_is_recorded_with_what_actually_changed()
    {
        var s = AddSubcontractor("Acme Roofing");

        s.Name = "Acme Roofing & Cladding";
        _db.SaveChanges();

        var update = _db.SubcontractsAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Name");
        update.Details.Should().Contain("Acme Roofing").And.Contain("Acme Roofing & Cladding");
    }

    [Fact]
    public void A_delete_is_recorded()
    {
        var s = AddSubcontractor();

        _db.Subcontractors.Remove(s);
        _db.SaveChanges();

        _db.SubcontractsAuditLogs.AsNoTracking().Should().Contain(l => l.Action == "Deleted");
    }

    [Fact]
    public void The_audited_row_is_identified_by_its_primary_key()
    {
        var s = AddSubcontractor();

        _db.SubcontractsAuditLogs.AsNoTracking().Single().EntityId.Should().Be(s.Id);
    }

    // ── It cannot audit itself ───────────────────────────────────────────────────

    [Fact]
    public void Audit_rows_are_not_themselves_audited()
    {
        AddSubcontractor();

        _db.SubcontractsAuditLogs.AsNoTracking().Should().HaveCount(1);
    }

    [Fact]
    public void Repeated_saves_do_not_compound()
    {
        AddSubcontractor("Acme Roofing", "Roofing");
        AddSubcontractor("Beta Electrical", "Electrical");
        AddSubcontractor("Gamma Plumbing", "Plumbing");

        _db.SubcontractsAuditLogs.AsNoTracking().Should().HaveCount(3);
    }

    // ── Noise control ────────────────────────────────────────────────────────────

    [Fact]
    public void A_save_that_changes_nothing_records_nothing()
    {
        AddSubcontractor();
        var before = _db.SubcontractsAuditLogs.AsNoTracking().Count();

        _db.SaveChanges();

        _db.SubcontractsAuditLogs.AsNoTracking().Count().Should().Be(before);
    }

    [Fact]
    public void A_property_marked_modified_but_unchanged_is_not_reported()
    {
        var s = AddSubcontractor("Acme Roofing", "Roofing");

        s.TradeCategory = "Roofing";               // reassigned to the same value
        s.Name = "Acme Roofing & Cladding";         // genuinely changed
        _db.SaveChanges();

        var update = _db.SubcontractsAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Name");
        update.Details.Should().NotContain("TradeCategory");
    }

    [Fact]
    public void A_create_records_no_field_level_detail()
    {
        AddSubcontractor();

        _db.SubcontractsAuditLogs.AsNoTracking().Single().Details.Should().BeNull();
    }

    // ── Attribution ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_write_with_no_request_behind_it_is_attributed_to_the_system()
    {
        using var db = ContextWithoutRequest(out var conn);
        using (conn)
        {
            db.Subcontractors.Add(new Subcontractor { Name = "Acme Roofing", TradeCategory = "Roofing", CreatedBy = "seed" });
            db.SaveChanges();

            var log = db.SubcontractsAuditLogs.AsNoTracking().Single();
            log.Actor.Should().BeNull();
            log.CreatedBy.Should().Be("system");
        }
    }

    [Fact]
    public void Every_entity_type_is_covered_not_a_hand_listed_subset()
    {
        _db.Prequalifications.Add(new Prequalification { SubcontractorId = "sub-1", CreatedBy = "seed" });
        _db.SaveChanges();

        _db.SubcontractsAuditLogs.AsNoTracking()
            .Should().Contain(l => l.Entity == nameof(Prequalification));
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
