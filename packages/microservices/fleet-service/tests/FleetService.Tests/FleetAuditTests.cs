using System.Security.Claims;
using FleetService.Core.Entities;
using FleetService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetService.Tests;

/// <summary>
/// The fleet field-level audit trail (#216) — same shape as finance's <c>FinanceAuditTests</c>.
///
/// <para>Runs against SQLite rather than EF InMemory because the interceptor adds entities during
/// <c>SavingChanges</c>, and whether those additions are actually written in the same save is a
/// property of the real save pipeline, not of an in-memory dictionary.</para>
/// </summary>
public class FleetAuditTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly FleetServiceDbContext _db;

    public FleetAuditTests() : this(actorId: "user-42") { }

    private FleetAuditTests(string? actorId)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var accessor = new StubAccessor(actorId);

        _db = new FleetServiceDbContext(new DbContextOptionsBuilder<FleetServiceDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new FleetAuditInterceptor(accessor))
            .Options);
        _db.Database.EnsureCreated();
    }

    private static FleetServiceDbContext ContextWithoutRequest(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new FleetServiceDbContext(new DbContextOptionsBuilder<FleetServiceDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new FleetAuditInterceptor(new StubAccessor(null)))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private VehicleClass AddVehicleClass(string name = "Flatbed")
    {
        var c = new VehicleClass { Name = name, CreatedBy = "seed" };
        _db.VehicleClasses.Add(c);
        _db.SaveChanges();
        return c;
    }

    // ── It records at all ────────────────────────────────────────────────────────

    [Fact]
    public void A_create_is_recorded()
    {
        AddVehicleClass();

        var log = _db.FleetAuditLogs.AsNoTracking().Single();
        log.Entity.Should().Be(nameof(VehicleClass));
        log.Action.Should().Be("Created");
        log.Actor.Should().Be("user-42");
    }

    [Fact]
    public void An_update_is_recorded_with_what_actually_changed()
    {
        var c = AddVehicleClass("Flatbed");

        c.Name = "Flatbed 20ft";
        _db.SaveChanges();

        var update = _db.FleetAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Name");
        update.Details.Should().Contain("Flatbed").And.Contain("Flatbed 20ft");
    }

    [Fact]
    public void A_delete_is_recorded()
    {
        var c = AddVehicleClass();

        _db.VehicleClasses.Remove(c);
        _db.SaveChanges();

        _db.FleetAuditLogs.AsNoTracking().Should().Contain(l => l.Action == "Deleted");
    }

    [Fact]
    public void The_audited_row_is_identified_by_its_primary_key()
    {
        var c = AddVehicleClass();

        _db.FleetAuditLogs.AsNoTracking().Single().EntityId.Should().Be(c.Id);
    }

    // ── It cannot audit itself ───────────────────────────────────────────────────

    [Fact]
    public void Audit_rows_are_not_themselves_audited()
    {
        AddVehicleClass();

        _db.FleetAuditLogs.AsNoTracking().Should().HaveCount(1);
    }

    [Fact]
    public void Repeated_saves_do_not_compound()
    {
        AddVehicleClass("Flatbed");
        AddVehicleClass("Tanker");
        AddVehicleClass("Box truck");

        _db.FleetAuditLogs.AsNoTracking().Should().HaveCount(3);
    }

    // ── Noise control ────────────────────────────────────────────────────────────

    [Fact]
    public void A_save_that_changes_nothing_records_nothing()
    {
        AddVehicleClass();
        var before = _db.FleetAuditLogs.AsNoTracking().Count();

        _db.SaveChanges();

        _db.FleetAuditLogs.AsNoTracking().Count().Should().Be(before);
    }

    [Fact]
    public void A_property_marked_modified_but_unchanged_is_not_reported()
    {
        var c = AddVehicleClass("Flatbed");

        c.Description = "Flatbed 20ft";       // genuinely changed (was null)
        c.Name = "Flatbed";                   // reassigned to the same value
        _db.SaveChanges();

        var update = _db.FleetAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Description");
        update.Details.Should().NotContain("Name");
    }

    [Fact]
    public void A_create_records_no_field_level_detail()
    {
        AddVehicleClass();

        _db.FleetAuditLogs.AsNoTracking().Single().Details.Should().BeNull();
    }

    // ── Attribution ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_write_with_no_request_behind_it_is_attributed_to_the_system()
    {
        using var db = ContextWithoutRequest(out var conn);
        using (conn)
        {
            db.VehicleClasses.Add(new VehicleClass { Name = "Flatbed", CreatedBy = "seed" });
            db.SaveChanges();

            var log = db.FleetAuditLogs.AsNoTracking().Single();
            log.Actor.Should().BeNull();
            log.CreatedBy.Should().Be("system");
        }
    }

    [Fact]
    public void Every_entity_type_is_covered_not_a_hand_listed_subset()
    {
        _db.LicenseClasses.Add(new LicenseClass { Name = "Class C", CreatedBy = "seed" });
        _db.SaveChanges();

        _db.FleetAuditLogs.AsNoTracking()
            .Should().Contain(l => l.Entity == nameof(LicenseClass));
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
