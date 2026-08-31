using System.Security.Claims;
using ComplianceService.Core.Entities;
using ComplianceService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ComplianceService.Tests;

/// <summary>
/// The compliance field-level audit trail (#216) — same shape as finance's <c>FinanceAuditTests</c>.
///
/// <para>Runs against SQLite rather than EF InMemory because the interceptor adds entities during
/// <c>SavingChanges</c>, and whether those additions are actually written in the same save is a
/// property of the real save pipeline, not of an in-memory dictionary.</para>
/// </summary>
public class ComplianceAuditTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ComplianceDbContext _db;

    public ComplianceAuditTests() : this(actorId: "user-42") { }

    private ComplianceAuditTests(string? actorId)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var accessor = new StubAccessor(actorId);

        _db = new ComplianceDbContext(new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new ComplianceAuditInterceptor(accessor))
            .Options);
        _db.Database.EnsureCreated();
    }

    private static ComplianceDbContext ContextWithoutRequest(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new ComplianceDbContext(new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new ComplianceAuditInterceptor(new StubAccessor(null)))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private RelatedParty AddRelatedParty(string companyName = "Acme Holdings", string regNo = "REG-001")
    {
        var p = new RelatedParty { CompanyName = companyName, RegNo = regNo, CreatedBy = "seed" };
        _db.RelatedParties.Add(p);
        _db.SaveChanges();
        return p;
    }

    // ── It records at all ────────────────────────────────────────────────────────

    [Fact]
    public void A_create_is_recorded()
    {
        AddRelatedParty();

        var log = _db.ComplianceAuditLogs.AsNoTracking().Single();
        log.Entity.Should().Be(nameof(RelatedParty));
        log.Action.Should().Be("Created");
        log.Actor.Should().Be("user-42");
    }

    [Fact]
    public void An_update_is_recorded_with_what_actually_changed()
    {
        var p = AddRelatedParty("Acme Holdings");

        p.CompanyName = "Acme Holdings Ltd";
        _db.SaveChanges();

        var update = _db.ComplianceAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("CompanyName");
        update.Details.Should().Contain("Acme Holdings").And.Contain("Acme Holdings Ltd");
    }

    [Fact]
    public void A_delete_is_recorded()
    {
        var p = AddRelatedParty();

        _db.RelatedParties.Remove(p);
        _db.SaveChanges();

        _db.ComplianceAuditLogs.AsNoTracking().Should().Contain(l => l.Action == "Deleted");
    }

    [Fact]
    public void The_audited_row_is_identified_by_its_primary_key()
    {
        var p = AddRelatedParty();

        _db.ComplianceAuditLogs.AsNoTracking().Single().EntityId.Should().Be(p.Id);
    }

    // ── It cannot audit itself ───────────────────────────────────────────────────

    [Fact]
    public void Audit_rows_are_not_themselves_audited()
    {
        AddRelatedParty();

        _db.ComplianceAuditLogs.AsNoTracking().Should().HaveCount(1);
    }

    [Fact]
    public void Repeated_saves_do_not_compound()
    {
        AddRelatedParty("Acme Holdings", "REG-001");
        AddRelatedParty("Beta Group", "REG-002");
        AddRelatedParty("Gamma Ltd", "REG-003");

        _db.ComplianceAuditLogs.AsNoTracking().Should().HaveCount(3);
    }

    // ── Noise control ────────────────────────────────────────────────────────────

    [Fact]
    public void A_save_that_changes_nothing_records_nothing()
    {
        AddRelatedParty();
        var before = _db.ComplianceAuditLogs.AsNoTracking().Count();

        _db.SaveChanges();

        _db.ComplianceAuditLogs.AsNoTracking().Count().Should().Be(before);
    }

    [Fact]
    public void A_property_marked_modified_but_unchanged_is_not_reported()
    {
        var p = AddRelatedParty("Acme Holdings", "REG-001");

        p.RegNo = "REG-001";                    // reassigned to the same value
        p.CompanyName = "Acme Holdings Ltd";     // genuinely changed
        _db.SaveChanges();

        var update = _db.ComplianceAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("CompanyName");
        update.Details.Should().NotContain("RegNo");
    }

    [Fact]
    public void A_create_records_no_field_level_detail()
    {
        AddRelatedParty();

        _db.ComplianceAuditLogs.AsNoTracking().Single().Details.Should().BeNull();
    }

    // ── Attribution ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_write_with_no_request_behind_it_is_attributed_to_the_system()
    {
        using var db = ContextWithoutRequest(out var conn);
        using (conn)
        {
            db.RelatedParties.Add(new RelatedParty { CompanyName = "Acme Holdings", RegNo = "REG-001", CreatedBy = "seed" });
            db.SaveChanges();

            var log = db.ComplianceAuditLogs.AsNoTracking().Single();
            log.Actor.Should().BeNull();
            log.CreatedBy.Should().Be("system");
        }
    }

    [Fact]
    public void Every_entity_type_is_covered_not_a_hand_listed_subset()
    {
        _db.Policies.Add(new Policy { Title = "Anti-Bribery Policy", Version = "1.0", CreatedBy = "seed" });
        _db.SaveChanges();

        _db.ComplianceAuditLogs.AsNoTracking()
            .Should().Contain(l => l.Entity == nameof(Policy));
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
