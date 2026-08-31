using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Budget;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;
using OperationsService.Core.Services;
using OperationsService.Infrastructure.Data;
using OperationsService.Infrastructure.Repositories;

namespace OperationsService.Tests;

/// <summary>
/// Project change control, against a real relational database.
///
/// <para>Operations had a test project — <c>CalibrationMath.Tests</c> — but it references only Core and
/// touches no DbContext, so **not one query had ever been executed** in this service's tests. That is
/// the gap #247 is about, and it is the reason the EF 8→9 bump (#227) has nothing to catch a
/// translation regression here.</para>
///
/// <para>SQLite rather than EF InMemory, for the same reason as #283 and the crm suite: InMemory never
/// builds SQL, so a query that stops translating passes. <c>EnsureCreated</c> succeeds across all 65
/// operations entity types. Limits are the same and worth restating rather than assuming: SQLite is not
/// Postgres — no jsonb, ASCII-only <c>lower()</c>, <c>decimal</c> by type affinity — so a translation
/// that works here and fails on Npgsql still gets through. Testcontainers remains the real answer.</para>
///
/// <para>Change control is the target because it is where a project's agreed baseline moves. Everything
/// downstream — variance, earned value, "are we over budget" — is measured against a baseline that this
/// code is allowed to rewrite.</para>
/// </summary>
public sealed class GovernanceFixture : IDisposable
{
    public const string ProjectId = "proj-1";
    public const string Requester = "engineer-1";
    public const string Approver  = "pm-1";

    private readonly SqliteConnection _conn;

    public OperationsDbContext Db { get; }
    public ProjectGovernanceService Governance { get; }
    public FakeBudgetService Budgets { get; }

    public GovernanceFixture(decimal baselineBudget = 1_000_000m)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        Db = new OperationsDbContext(
            new DbContextOptionsBuilder<OperationsDbContext>().UseSqlite(_conn).Options);
        Db.Database.EnsureCreated();

        Db.Projects.Add(new Project
        {
            Id = ProjectId, Name = "Kilifi water works",
            BaselineBudget = baselineBudget, BaselineSetAt = new DateTime(2026, 1, 15),
            CreatedBy = Requester,
        });
        Db.SaveChanges();
        Db.ChangeTracker.Clear();

        Budgets = new FakeBudgetService();
        Governance = new ProjectGovernanceService(
            Repo<Project>(), Repo<Milestone>(), Repo<RiskEntry>(), Repo<ProjectIssue>(),
            Repo<ChangeRequest>(), Repo<BudgetVersion>(), Repo<ProjectHistory>(), Budgets);
    }

    private IGenericRepository<T> Repo<T>() where T : class => new GenericRepository<T>(Db);

    /// <summary>A milestone with both baseline dates set, so a schedule shift has something to move.</summary>
    public Milestone SeedMilestone(string title, DateTime baselineStart, DateTime baselineDue,
                                   DateTime? liveStart = null, DateTime? liveDue = null)
    {
        var m = new Milestone
        {
            ProjectId = ProjectId, Title = title,
            BaselineStart = baselineStart, BaselineDue = baselineDue,
            StartDate = liveStart ?? baselineStart, DueDate = liveDue ?? baselineDue,
            CreatedBy = Requester,
        };
        Db.Milestones.Add(m);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return m;
    }

    /// <summary>A change request already in Submitted state, which is the only state that can be decided.</summary>
    public async Task<ChangeRequest> SubmittedCrAsync(int scheduleImpactDays = 0, string? budgetVersionId = null)
    {
        var cr = new ChangeRequest
        {
            ProjectId = ProjectId, Number = "CR-2026-0001", Title = "Extend the intake works",
            Description = "Additional 200m of pipeline", Justification = "Revised hydrology survey",
            ScheduleImpactDays = scheduleImpactDays, BudgetVersionId = budgetVersionId,
            Status = ChangeRequestStatus.Submitted,
            RequestedBy = Requester, SubmittedAt = new DateTime(2026, 3, 1),
            CreatedBy = Requester,
        };
        Db.ChangeRequests.Add(cr);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return cr;
    }

    public void Dispose() { Db.Dispose(); _conn.Dispose(); }
}

/// <summary>
/// The budget seam. Approving a change request delegates the actual baseline-budget move to
/// <c>IProjectBudgetService.ApproveAsync</c> rather than reimplementing it — one mechanism, so the two
/// can never disagree. This records the call so a test can assert the delegation happened.
/// </summary>
public sealed class FakeBudgetService : IProjectBudgetService
{
    public List<(string VersionId, string UserId)> Approved { get; } = [];

    public Task<BudgetVersionDto> ApproveAsync(string versionId, string userId)
    {
        Approved.Add((versionId, userId));
        return Task.FromResult(new BudgetVersionDto());
    }

    public Task<List<BudgetVersionDto>> GetVersionsAsync(string projectId) => Task.FromResult(new List<BudgetVersionDto>());
    public Task<BudgetVersionDto?> GetVersionAsync(string versionId) => Task.FromResult<BudgetVersionDto?>(null);
    public Task<BudgetVersionDto> CreateVersionAsync(string projectId, CreateBudgetVersionDto dto, string userId) => Task.FromResult(new BudgetVersionDto());
    public Task<BudgetVersionDto> UpsertLineAsync(string versionId, UpsertBudgetLineDto dto, string userId) => Task.FromResult(new BudgetVersionDto());
    public Task<BudgetVersionDto> DeleteLineAsync(string versionId, string lineId, string userId) => Task.FromResult(new BudgetVersionDto());
    public Task<BudgetVersionDto> SubmitAsync(string versionId, string userId) => Task.FromResult(new BudgetVersionDto());
    public Task<BudgetVersionDto> RejectAsync(string versionId, string reason, string userId) => Task.FromResult(new BudgetVersionDto());
    public Task<List<ContractRateDto>> GetRatesAsync(string projectId, bool activeOnly = false) => Task.FromResult(new List<ContractRateDto>());
    public Task<ContractRateDto> AddRateAsync(string projectId, UpsertContractRateDto dto, string userId) => Task.FromResult(new ContractRateDto());
    public Task DeactivateRateAsync(string rateId, string userId) => Task.CompletedTask;
    public Task<ProjectCommercialsDto> GetCommercialsAsync(string projectId) => Task.FromResult(new ProjectCommercialsDto());
}
