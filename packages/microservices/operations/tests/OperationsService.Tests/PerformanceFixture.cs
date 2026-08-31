using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Mappings;
using OperationsService.Core.Services;
using OperationsService.Infrastructure.Data;
using OperationsService.Infrastructure.Repositories;

namespace OperationsService.Tests;

/// <summary>
/// #339 -- a technician with one department, one assignment and the records that hang off it,
/// against a real relational database (SQLite, same reasoning as <see cref="GovernanceFixture"/>:
/// EF InMemory never builds SQL, so a query that stops translating would still pass).
/// </summary>
public sealed class PerformanceFixture : IDisposable
{
    public const string TechnicianId = "tech-1";
    public const string TechnicianName = "Jane Tech";
    public const string DepartmentId = "dept-1";
    public static readonly DateTime InPeriod = new(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _conn;

    public OperationsDbContext Db { get; }
    public PerformanceService Performance { get; }

    public PerformanceFixture()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        Db = new OperationsDbContext(
            new DbContextOptionsBuilder<OperationsDbContext>().UseSqlite(_conn).Options);
        Db.Database.EnsureCreated();

        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>()).CreateMapper();
        Performance = new PerformanceService(
            Repo<PerformanceMetrics>(), Repo<Assignment>(), Repo<AssignedTechnician>(),
            Repo<ServiceReport>(), Repo<Requisition>(), mapper);
    }

    private IGenericRepository<T> Repo<T>() where T : class => new GenericRepository<T>(Db);

    /// <summary>An assignment for <see cref="TechnicianId"/>, assigned and (optionally) completed
    /// inside the test period.</summary>
    public Assignment SeedAssignment(AssignmentStatus status = AssignmentStatus.Completed,
                                     DateTime? deadline = null, DateTime? completedAt = null,
                                     DateTime? assignedAt = null)
    {
        var a = new Assignment
        {
            Title = "Calibrate flow meter", DepartmentId = DepartmentId,
            DepartmentType = DepartmentType.Technical, ManagerId = "mgr-1",
            Status = status, Deadline = deadline, CompletedAt = completedAt,
            CreatedBy = "system",
        };
        Db.Assignments.Add(a);
        Db.AssignedTechnicians.Add(new AssignedTechnician
        {
            AssignmentId = a.Id, UserId = TechnicianId, UserName = TechnicianName,
            AssignedAt = assignedAt ?? InPeriod, CreatedBy = "system",
        });
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return a;
    }

    public ServiceReport SeedReport(string assignmentId, ServiceReportStatus status, int? totalMinutes,
                                    DateTime? submittedAt)
    {
        var r = new ServiceReport
        {
            AssignmentId = assignmentId, TechnicianId = TechnicianId, TechnicianName = TechnicianName,
            DepartmentType = DepartmentType.Technical, Status = status,
            CustomerName = "Acme", LocationName = "Site A",
            TotalMinutes = totalMinutes, SubmittedAt = submittedAt, CreatedBy = "system",
        };
        Db.ServiceReports.Add(r);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return r;
    }

    public Requisition SeedRequisition(string assignmentId, decimal amount, RequisitionStatus status,
                                       DateTime? createdAt = null)
    {
        var r = new Requisition
        {
            AssignmentId = assignmentId, TechnicianId = TechnicianId, RequestedByName = TechnicianName,
            Type = RequisitionType.MaterialRequisition, Description = "Parts", Amount = amount,
            Status = status, CreatedBy = "system",
            CreatedAt = createdAt ?? InPeriod,
        };
        Db.Requisitions.Add(r);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return r;
    }

    public void Dispose()
    {
        Db.Dispose();
        _conn.Dispose();
    }
}
