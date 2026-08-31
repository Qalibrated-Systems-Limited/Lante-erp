using System.Security.Claims;
using OperationsService.Core.Entities;
using OperationsService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OperationsService.Tests;

/// <summary>
/// The operations audit trail (#216). <c>OperationsAuditLog</c> covers every entity except
/// <c>CalibrationAuditLog</c> itself, which stays on its own purpose-built ISO-17025 event trail and
/// is explicitly excluded so it is never double-logged.
///
/// <para>These tests run against SQLite rather than EF InMemory because the interceptor adds entities
/// during <c>SavingChanges</c>, and whether those additions are actually written in the same save is a
/// property of the real save pipeline, not of an in-memory dictionary.</para>
/// </summary>
public class OperationsAuditTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly OperationsDbContext _db;

    public OperationsAuditTests() : this(actorId: "user-42") { }

    private OperationsAuditTests(string? actorId)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var accessor = new StubAccessor(actorId);

        _db = new OperationsDbContext(new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new OperationsAuditInterceptor(accessor))
            .Options);
        _db.Database.EnsureCreated();
    }

    private static OperationsDbContext ContextWithoutRequest(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new OperationsDbContext(new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new OperationsAuditInterceptor(new StubAccessor(null)))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private ReferenceStandard AddReferenceStandard(string assetId = "RS-1")
    {
        var rs = new ReferenceStandard { AssetId = assetId, Description = "1kg mass standard" };
        _db.ReferenceStandards.Add(rs);
        _db.SaveChanges();
        return rs;
    }

    // ── It records at all ────────────────────────────────────────────────────────

    [Fact]
    public void A_create_is_recorded()
    {
        AddReferenceStandard();

        var log = _db.OperationsAuditLogs.AsNoTracking().Single();
        log.Entity.Should().Be(nameof(ReferenceStandard));
        log.Action.Should().Be("Created");
        log.Actor.Should().Be("user-42");
    }

    [Fact]
    public void An_update_is_recorded_with_what_actually_changed()
    {
        var rs = AddReferenceStandard();

        rs.NominalValue = "2 kg set";
        _db.SaveChanges();

        var update = _db.OperationsAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("NominalValue");
        update.Details.Should().Contain("2 kg set");
    }

    [Fact]
    public void A_delete_is_recorded()
    {
        var rs = AddReferenceStandard();

        _db.ReferenceStandards.Remove(rs);
        _db.SaveChanges();

        _db.OperationsAuditLogs.AsNoTracking().Should().Contain(l => l.Action == "Deleted");
    }

    [Fact]
    public void The_audited_row_is_identified_by_its_primary_key()
    {
        var rs = AddReferenceStandard();

        _db.OperationsAuditLogs.AsNoTracking().Single().EntityId.Should().Be(rs.Id);
    }

    // ── It cannot audit itself, and it does not double-log calibration's own trail ──

    [Fact]
    public void Audit_rows_are_not_themselves_audited()
    {
        AddReferenceStandard();

        _db.OperationsAuditLogs.AsNoTracking().Should().HaveCount(1);
    }

    [Fact]
    public void Calibration_audit_log_writes_are_not_duplicated_here()
    {
        // CalibrationAuditLog is a purpose-built ISO-17025 event trail, not this generic log. Writing
        // to it must not also produce an OperationsAuditLog row for itself (though the calibration
        // certificate/standard it's attached to, if changed in the same save, still should).
        _db.CalibrationAuditLogs.Add(new CalibrationAuditLog
        {
            LabWorkOrderId = "LWO-1",
            Action = "StandardLinked",
            PerformedById = "user-42",
        });
        _db.SaveChanges();

        _db.OperationsAuditLogs.AsNoTracking().Should().BeEmpty();
    }

    [Fact]
    public void Repeated_saves_do_not_compound()
    {
        AddReferenceStandard("RS-1");
        AddReferenceStandard("RS-2");
        AddReferenceStandard("RS-3");

        _db.OperationsAuditLogs.AsNoTracking().Should().HaveCount(3);
    }

    // ── Noise control ────────────────────────────────────────────────────────────

    [Fact]
    public void A_save_that_changes_nothing_records_nothing()
    {
        AddReferenceStandard();
        var before = _db.OperationsAuditLogs.AsNoTracking().Count();

        _db.SaveChanges();

        _db.OperationsAuditLogs.AsNoTracking().Count().Should().Be(before);
    }

    [Fact]
    public void A_property_marked_modified_but_unchanged_is_not_reported()
    {
        var rs = AddReferenceStandard("RS-1");

        rs.AssetId = "RS-1";                  // reassigned to the same value
        rs.Description = "recalibrated mass";  // genuinely changed
        _db.SaveChanges();

        var update = _db.OperationsAuditLogs.AsNoTracking().Single(l => l.Action == "Updated");
        update.Details.Should().Contain("Description");
        update.Details.Should().NotContain("AssetId");
    }

    [Fact]
    public void A_create_records_no_field_level_detail()
    {
        AddReferenceStandard();

        _db.OperationsAuditLogs.AsNoTracking().Single().Details.Should().BeNull();
    }

    // ── Attribution ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_write_with_no_request_behind_it_is_attributed_to_the_system()
    {
        using var db = ContextWithoutRequest(out var conn);
        using (conn)
        {
            db.ReferenceStandards.Add(new ReferenceStandard { AssetId = "RS-1", Description = "mass standard" });
            db.SaveChanges();

            var log = db.OperationsAuditLogs.AsNoTracking().Single();
            log.Actor.Should().BeNull();
            log.CreatedBy.Should().Be("system");
        }
    }

    [Fact]
    public void Every_entity_type_is_covered_not_a_hand_listed_subset()
    {
        var project = new Project { Name = "Test Project", DepartmentId = "dept-1", ProjectManagerId = "user-42" };
        _db.Projects.Add(project);
        _db.SaveChanges();

        _db.Milestones.Add(new Milestone { ProjectId = project.Id, Title = "Kickoff", DueDate = DateTime.UtcNow });
        _db.SaveChanges();

        // The point of intercepting SaveChanges rather than calling a logger at each site: an entity
        // unrelated to calibration is covered without anyone remembering to add it.
        _db.OperationsAuditLogs.AsNoTracking()
            .Should().Contain(l => l.Entity == nameof(Milestone));
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    /// <summary>
    /// A per-instance IHttpContextAccessor. The framework's HttpContextAccessor stores HttpContext in a
    /// STATIC AsyncLocal, so a second instance created in the same async flow still sees the first
    /// test's user — a shared-state surprise worth a note rather than a silent workaround.
    /// </summary>
    private sealed class StubAccessor(string? actorId) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = actorId is null
            ? null
            : new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, actorId)], "TestAuth")),
            };
    }
}
