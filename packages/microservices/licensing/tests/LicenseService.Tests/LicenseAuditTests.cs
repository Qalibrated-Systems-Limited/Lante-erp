using System.Security.Claims;
using LicenseService.Core.Entities;
using LicenseService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LicenseService.Tests;

/// <summary>
/// The licensing field-level audit trail (#216) — same shape as finance's <c>FinanceAuditTests</c>.
///
/// <para>Runs against SQLite rather than EF InMemory because the interceptor adds entities during
/// <c>SavingChanges</c>, and whether those additions are actually written in the same save is a
/// property of the real save pipeline, not of an in-memory dictionary.</para>
/// </summary>
public class LicenseAuditTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly LanteLicenseDbContext _db;

    public LicenseAuditTests() : this(actorId: "user-42") { }

    private LicenseAuditTests(string? actorId)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var accessor = new StubAccessor(actorId);

        _db = new LanteLicenseDbContext(new DbContextOptionsBuilder<LanteLicenseDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new LicenseAuditInterceptor(accessor))
            .Options);
        _db.Database.EnsureCreated();
    }

    private static LanteLicenseDbContext ContextWithoutRequest(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new LanteLicenseDbContext(new DbContextOptionsBuilder<LanteLicenseDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new LicenseAuditInterceptor(new StubAccessor(null)))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private License AddLicense(string customerId = "cust-1", string appId = "qalitrack-frontend")
    {
        var l = new License { Token = $"jwt-token-{Guid.NewGuid()}", CustomerId = customerId, AppId = appId, CreatedBy = "seed" };
        _db.Licenses.Add(l);
        _db.SaveChanges();
        return l;
    }

    // ── It records at all ────────────────────────────────────────────────────────

    [Fact]
    public void A_create_is_recorded()
    {
        AddLicense();

        var log = _db.LicenseAuditLogs.AsNoTracking().Single();
        log.Entity.Should().Be(nameof(License));
        log.Action.Should().Be("Created");
        log.Actor.Should().Be("user-42");
    }

    [Fact]
    public void An_update_is_recorded_with_what_actually_changed()
    {
        var l = AddLicense();

        l.CustomerName = "Acme Ltd";
        _db.SaveChanges();

        var update = _db.LicenseAuditLogs.AsNoTracking().Single(x => x.Action == "Updated");
        update.Details.Should().Contain("CustomerName");
        update.Details.Should().Contain("Acme Ltd");
    }

    [Fact]
    public void A_delete_is_recorded()
    {
        var l = AddLicense();

        _db.Licenses.Remove(l);
        _db.SaveChanges();

        _db.LicenseAuditLogs.AsNoTracking().Should().Contain(x => x.Action == "Deleted");
    }

    [Fact]
    public void The_audited_row_is_identified_by_its_primary_key()
    {
        var l = AddLicense();

        _db.LicenseAuditLogs.AsNoTracking().Single().EntityId.Should().Be(l.Id);
    }

    // ── It cannot audit itself ───────────────────────────────────────────────────

    [Fact]
    public void Audit_rows_are_not_themselves_audited()
    {
        AddLicense();

        _db.LicenseAuditLogs.AsNoTracking().Should().HaveCount(1);
    }

    [Fact]
    public void Repeated_saves_do_not_compound()
    {
        AddLicense("cust-1", "qalitrack-frontend");
        AddLicense("cust-2", "qalitrack-mobile");
        AddLicense("cust-3", "qalitrack-web");

        _db.LicenseAuditLogs.AsNoTracking().Should().HaveCount(3);
    }

    // ── Noise control ────────────────────────────────────────────────────────────

    [Fact]
    public void A_save_that_changes_nothing_records_nothing()
    {
        AddLicense();
        var before = _db.LicenseAuditLogs.AsNoTracking().Count();

        _db.SaveChanges();

        _db.LicenseAuditLogs.AsNoTracking().Count().Should().Be(before);
    }

    [Fact]
    public void A_property_marked_modified_but_unchanged_is_not_reported()
    {
        var l = AddLicense("cust-1", "qalitrack-frontend");

        l.AppId = "qalitrack-frontend";     // reassigned to the same value
        l.CustomerName = "Acme Ltd";        // genuinely changed
        _db.SaveChanges();

        var update = _db.LicenseAuditLogs.AsNoTracking().Single(x => x.Action == "Updated");
        update.Details.Should().Contain("CustomerName");
        update.Details.Should().NotContain("AppId");
    }

    [Fact]
    public void A_create_records_no_field_level_detail()
    {
        AddLicense();

        _db.LicenseAuditLogs.AsNoTracking().Single().Details.Should().BeNull();
    }

    // ── Attribution ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_write_with_no_request_behind_it_is_attributed_to_the_system()
    {
        using var db = ContextWithoutRequest(out var conn);
        using (conn)
        {
            db.Licenses.Add(new License { Token = "jwt-token-1", CustomerId = "cust-1", AppId = "qalitrack-frontend", CreatedBy = "seed" });
            db.SaveChanges();

            var log = db.LicenseAuditLogs.AsNoTracking().Single();
            log.Actor.Should().BeNull();
            log.CreatedBy.Should().Be("system");
        }
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
