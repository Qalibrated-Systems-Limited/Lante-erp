using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;
using HrService.Core.Services;
using HrService.Infrastructure.Data;
using HrService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HrService.Tests;

/// <summary>
/// A payroll run that can actually be computed, approved and posted.
///
/// <para><b>Why a fixture and not mocks.</b> <see cref="PayrollRunService"/> takes nineteen
/// dependencies, sixteen of which are <c>IGenericRepository&lt;T&gt;</c> over the same DbContext.
/// Mocking sixteen repositories individually means re-implementing LINQ against sixteen fakes, and
/// the engine reads them relationally — a salary joins a structure joins its components. Mocks that
/// return unrelated lists would pass while modelling a payroll that could not exist.</para>
///
/// <para><b>Two users, deliberately.</b> <see cref="Computer"/> computes and <see cref="Approver"/>
/// approves. `PayrollRunService.cs:883` refuses approval when <c>run.ComputedBy == userId</c>, so a
/// single-user fixture cannot reach the approval path at all — the half of the lifecycle where money
/// is committed would be permanently untestable and would look merely "not yet covered".</para>
///
/// <para><b>The chart of accounts is part of the fixture, not decoration.</b> Approval calls
/// BuildJournalAsync and refuses a run whose journal cannot be built (`:895`), which needs a GL
/// account for every earning and deduction plus the net-pay holding account 2110 (`:61`). Seeding
/// accounts is therefore a precondition of testing approval, not an optional extra.</para>
///
/// <para><b>Limits.</b> This is EF Core InMemory, matching the finance LedgerFixture. It does not
/// enforce the unique (PayrollRunId, EmployeeId) index the concurrent-compute comment at `:340`
/// relies on, and it has no transactions — so the double-compute race described there cannot be
/// reproduced here and needs Testcontainers against real Postgres. Nor does it exercise the tenant
/// search_path interceptor. What it does cover is the arithmetic, the state machine, the
/// segregation-of-duties control and the journal shape.</para>
/// </summary>
public sealed class PayrollFixture : IDisposable
{
    public const string Computer = "user-payroll-officer";
    public const string Approver = "user-finance-manager";

    /// <summary>Period codes are compared with <c>string.CompareOrdinal</c> (`:701`), so the format
    /// has to sort lexically — yyyy-MM does, "Mar 2026" would not.</summary>
    public const string PeriodCode = "2026-03";

    public HrDbContext Db { get; }
    public PayrollRunService Runs { get; }
    public FakeFinanceGateway Finance { get; }
    public FakeAlertGateway Alerts { get; }

    public string PeriodId { get; private set; } = string.Empty;
    public string EmployeeId { get; private set; } = string.Empty;
    public string StructureId { get; private set; } = string.Empty;

    public PayrollFixture(
        PayrollPeriodStatus periodStatus = PayrollPeriodStatus.Open,
        decimal basicSalary = 100_000m,
        bool mapGlAccounts = true)
    {
        var options = new DbContextOptionsBuilder<HrDbContext>()
            .UseInMemoryDatabase($"hr-payroll-tests-{Guid.NewGuid()}")
            .Options;
        Db = new HrDbContext(options);

        Finance = new FakeFinanceGateway();
        Alerts = new FakeAlertGateway();

        Seed(periodStatus, basicSalary, mapGlAccounts);

        Runs = new PayrollRunService(
            Repo<Employee>(), Repo<EmployeeSalary>(), Repo<SalaryStructure>(), Repo<SalaryComponent>(),
            Repo<PayrollPeriod>(), Repo<PayeTaxBand>(), Repo<StatutoryRate>(), Repo<PayrollDeduction>(),
            Repo<PayrollDeductionType>(), Repo<OvertimeRequest>(), Repo<CommissionStatement>(),
            Repo<AbsenceRecord>(), Repo<PayrollRun>(), Repo<Payslip>(), Repo<PayslipLine>(),
            Repo<HrAuditLog>(), new FakeWorkCalendar(), Finance, Alerts);
    }

    private IGenericRepository<T> Repo<T>() where T : BaseEntity => new GenericRepository<T>(Db);

    // ── Seed ─────────────────────────────────────────────────────────────────────

    private void Seed(PayrollPeriodStatus periodStatus, decimal basicSalary, bool mapGlAccounts)
    {
        var period = new PayrollPeriod
        {
            Code = PeriodCode, Year = 2026, Month = 3,
            StartDate = new DateTime(2026, 3, 1), EndDate = new DateTime(2026, 3, 31),
            Status = periodStatus, CreatedBy = Computer,
        };
        PeriodId = period.Id;
        Db.PayrollPeriods.Add(period);

        var structure = new SalaryStructure { Name = "Standard", IsActive = true, CreatedBy = Computer };
        StructureId = structure.Id;
        Db.SalaryStructures.Add(structure);

        // Basic is the taxable core; house allowance is taxable, per-diem is not. That split is what
        // makes "taxable income != gross" assertable rather than assumed.
        Db.SalaryComponents.AddRange(
            Component(structure.Id, "BASIC", "Basic Pay", SalaryComponentType.Earning, basicSalary,
                      isTaxable: true, order: 1, gl: mapGlAccounts ? FakeFinanceGateway.SalariesExpense : null),
            Component(structure.Id, "HOUSE", "House Allowance", SalaryComponentType.Earning, 20_000m,
                      isTaxable: true, order: 2, gl: mapGlAccounts ? FakeFinanceGateway.SalariesExpense : null),
            Component(structure.Id, "PERDIEM", "Per Diem", SalaryComponentType.Earning, 5_000m,
                      isTaxable: false, order: 3, gl: mapGlAccounts ? FakeFinanceGateway.SalariesExpense : null),
            // NOTE: no PAYE component here. PAYE takes its posting account from the PAYE StatutoryRate,
            // like every other statutory deduction. Before #254 was fixed, a dummy Deduction-type
            // component was the only way to map it and no run was approvable without one.
            Component(structure.Id, "TRANSPORT", "Transport Allowance", SalaryComponentType.Earning, 0m,
                      isTaxable: true, order: 4, gl: mapGlAccounts ? FakeFinanceGateway.SalariesExpense : null));

        var employee = new Employee
        {
            EmployeeNumber = "EMP-2026-0001", FirstName = "Asha", LastName = "Mwangi",
            UserId = "user-asha", HireDate = new DateTime(2020, 1, 6),
            Status = EmploymentStatus.Active, CreatedBy = Computer,
        };
        EmployeeId = employee.Id;
        Db.Employees.Add(employee);

        Db.EmployeeSalaries.Add(new EmployeeSalary
        {
            EmployeeId = employee.Id, EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = $"{employee.FirstName} {employee.LastName}",
            SalaryStructureId = structure.Id, SalaryStructureName = structure.Name,
            BasicSalary = basicSalary, CurrencyCode = "KES",
            // Effective from an EARLIER period than the one under test, so the run exercises the
            // "assignment in force for this period" lookup rather than an exact-match coincidence.
            EffectiveFromPeriodId = period.Id, EffectiveFromPeriodCode = "2026-01",
            Status = SalaryAssignmentStatus.Approved,
            ApprovedBy = Approver, ApprovedAt = new DateTime(2026, 1, 5),
            CreatedBy = Computer,
        });

        SeedStatutoryTables(mapGlAccounts);
        Db.SaveChanges();
    }

    /// <summary>A Deduction-type component carrying nothing but a GL account for PAYE. No longer required
    /// after #254 — the rate supplies the account — but a tenant that added one to work around the bug must
    /// keep working, and it must still take precedence. Used only by the test that asserts that.</summary>
    public static SalaryComponent PayeAccountCarrier(string structureId, string? gl) => new()
    {
        SalaryStructureId = structureId, Code = "PAYE", Name = "PAYE",
        ComponentType = SalaryComponentType.Deduction, Statutory = StatutoryComponent.Paye,
        CalculationType = ComponentCalculationType.FixedAmount, Amount = 0m,
        ComponentOrder = 90, IsActive = true,
        GlAccountId = gl, GlAccountCode = gl is null ? null : FakeFinanceGateway.CodeOf(gl),
        CreatedBy = Computer,
    };

    private static SalaryComponent Component(
        string structureId, string code, string name, SalaryComponentType type,
        decimal amount, bool isTaxable, int order, string? gl) => new()
    {
        SalaryStructureId = structureId, Code = code, Name = name, ComponentType = type,
        CalculationType = ComponentCalculationType.FixedAmount, Amount = amount,
        IsTaxable = isTaxable, ComponentOrder = order, IsActive = true,
        GlAccountId = gl, GlAccountCode = gl is null ? null : FakeFinanceGateway.CodeOf(gl),
        CreatedBy = Computer,
    };

    /// <summary>
    /// PAYE bands and statutory rates.
    ///
    /// <para>These are the 2024-era Kenyan figures, and the tests built on them assert the
    /// <b>engine</b> — bracket boundaries, relief capping, employer/employee split — never that a
    /// particular rate is the legally current one. Rates are effective-dated data carrying
    /// <c>NeedsConfirmation</c> (#244); a test that pinned a rate would go red on a Finance Act
    /// rather than on a regression, which trains people to edit tests instead of reading them.</para>
    /// </summary>
    private void SeedStatutoryTables(bool mapGlAccounts)
    {
        var from = new DateTime(2024, 1, 1);
        string? gl(string a) => mapGlAccounts ? a : null;

        Db.PayeTaxBands.AddRange(
            // Rates are stored as percentage NUMBERS, not fractions: TaxOn divides by 100 (`:TaxOn`).
            // 0.30m here would tax at 0.3%, and every net-pay assertion would still look plausible.
            Band(1, 0m,        24_000m,  10m,   from),
            Band(2, 24_000m,   32_333m,  25m,   from),
            Band(3, 32_333m,  500_000m,  30m,   from),
            Band(4, 500_000m, 800_000m,  32.5m, from),
            Band(5, 800_000m, null,      35m,   from));

        // Personal relief is modelled as a FixedAmount PAYE rate (`:276`) — it reduces tax, never gross.
        Db.StatutoryRates.AddRange(
            new StatutoryRate
            {
                Code = "RELIEF", Name = "Personal Relief", Component = StatutoryComponent.Paye,
                RateType = StatutoryRateType.FixedAmount, FixedAmount = 2_400m,
                // This GL account is what the PAYE *deduction line* posts to. PAYE's account lives on
                // its own StatutoryRate, exactly as NSSF's and SHA's do — that consistency is what #254
                // restored. The relief amount and the PAYE payable account share this row because both
                // are properties of PAYE as a statutory component.
                GlAccountId = gl(FakeFinanceGateway.StatutoryPayable),
                GlAccountCode = gl(FakeFinanceGateway.StatutoryPayable) is null
                    ? null : FakeFinanceGateway.CodeOf(FakeFinanceGateway.StatutoryPayable),
                EffectiveFrom = from,
                IsActive = true, CreatedBy = Computer,
            },
            new StatutoryRate
            {
                Code = "NSSF", Name = "NSSF Tier I+II", Component = StatutoryComponent.Nssf,
                // Tiered, because NSSF charges only the slice of pay inside the tier — pensionable
                // pay is capped at 72,000, so a 100,000 salary must not contribute 6% of all of it.
                RateType = StatutoryRateType.TieredPercent, Rate = 6m, EmployerRate = 6m,
                TierLowerBound = 0m, TierUpperBound = 72_000m, ReducesTaxableIncome = true,
                GlAccountId = gl(FakeFinanceGateway.StatutoryPayable),
                GlAccountCode = gl(FakeFinanceGateway.StatutoryPayable) is null
                    ? null : FakeFinanceGateway.CodeOf(FakeFinanceGateway.StatutoryPayable),
                EffectiveFrom = from,
                IsActive = true, CreatedBy = Computer,
            },
            new StatutoryRate
            {
                Code = "SHA", Name = "Social Health Authority", Component = StatutoryComponent.Sha,
                RateType = StatutoryRateType.PercentOfGross, Rate = 2.75m,
                MinAmount = 300m, ReducesTaxableIncome = true,
                GlAccountId = gl(FakeFinanceGateway.StatutoryPayable),
                GlAccountCode = gl(FakeFinanceGateway.StatutoryPayable) is null
                    ? null : FakeFinanceGateway.CodeOf(FakeFinanceGateway.StatutoryPayable),
                EffectiveFrom = from,
                IsActive = true, CreatedBy = Computer,
            },
            new StatutoryRate
            {
                Code = "AHL", Name = "Affordable Housing Levy", Component = StatutoryComponent.HousingLevy,
                RateType = StatutoryRateType.PercentOfGross, Rate = 1.5m, EmployerRate = 1.5m,
                ReducesTaxableIncome = true,
                GlAccountId = gl(FakeFinanceGateway.StatutoryPayable),
                GlAccountCode = gl(FakeFinanceGateway.StatutoryPayable) is null
                    ? null : FakeFinanceGateway.CodeOf(FakeFinanceGateway.StatutoryPayable),
                EffectiveFrom = from,
                IsActive = true, CreatedBy = Computer,
            });
    }

    private static PayeTaxBand Band(int order, decimal lower, decimal? upper, decimal rate, DateTime from) => new()
    {
        BandOrder = order, LowerBound = lower, UpperBound = upper, Rate = rate,
        EffectiveFrom = from, IsActive = true, CreatedBy = Computer,
    };

    /// <summary>Adds a flexible (P8) deduction type and one live deduction against it for the seeded
    /// employee, in force for <see cref="PeriodCode"/>. Not part of the default seed — most tests never
    /// touch the deduction catalogue, and adding it unconditionally would shift every other test's
    /// TaxableIncome/Net constants.</summary>
    public void AddDeduction(string code, string name, DeductionCategory category, bool reducesTaxableIncome, decimal amount)
    {
        var type = new PayrollDeductionType
        {
            Code = code, Name = name, Category = category,
            ReducesTaxableIncome = reducesTaxableIncome, IsRecurring = true, IsActive = true,
            CreatedBy = Computer, UpdatedBy = Computer,
        };
        Db.PayrollDeductionTypes.Add(type);
        Db.PayrollDeductions.Add(new PayrollDeduction
        {
            EmployeeId = EmployeeId, DeductionTypeId = type.Id, DeductionTypeCode = type.Code, DeductionTypeName = type.Name,
            Category = category, Amount = amount,
            StartPeriodId = PeriodId, StartPeriodCode = PeriodCode,
            IsActive = true, AddedBy = Computer,
        });
        Db.SaveChanges();
    }

    public void Dispose() => Db.Dispose();
}

// ── Doubles ──────────────────────────────────────────────────────────────────────

/// <summary>
/// A Mon–Fri calendar with no public holidays.
///
/// <para>Real working-day arithmetic rather than a constant, because the overtime hourly rate is
/// <c>basic ÷ (workingDays × 8)</c> — a stubbed constant would make every rate assertion a tautology.
/// No holidays is a deliberate simplification: March 2026 has none in Kenya, and holiday expansion
/// belongs to <c>IWorkCalendar</c>'s own tests, not payroll's.</para>
/// </summary>
public sealed class FakeWorkCalendar : IWorkCalendar
{
    public Task<AttendanceSetting> GetSettingsAsync(string? userId = null) => Task.FromResult(new AttendanceSetting
    {
        Id = "attendance-default",
        WorkDayStartMinutes = 8 * 60, WorkDayEndMinutes = 17 * 60,
        LunchStartMinutes = 13 * 60, LunchMinutes = 60,
        WorksMonday = true, WorksTuesday = true, WorksWednesday = true,
        WorksThursday = true, WorksFriday = true, WorksSaturday = false, WorksSunday = false,
    });

    public Task<HashSet<DateTime>> HolidayDatesAsync(DateTime from, DateTime to) =>
        Task.FromResult(new HashSet<DateTime>());

    public Task<bool> IsWorkingDayAsync(DateTime date) =>
        Task.FromResult(date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));

    public async Task<int> CountWorkingDaysAsync(DateTime from, DateTime to) =>
        (await WorkingDaysBetweenAsync(from, to)).Count;

    public Task<List<DateTime>> WorkingDaysBetweenAsync(DateTime from, DateTime to)
    {
        var days = new List<DateTime>();
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days.Add(d);
        return Task.FromResult(days);
    }
}

/// <summary>
/// Finance, with a chart of accounts and a record of what payroll asked it to post.
///
/// <para>Captures the journal instead of asserting on it, so each test states its own expectation.
/// <see cref="FailPosting"/> exists because a posting failure is explicitly a survivable transient
/// (`:895` distinguishes it from an unbuildable journal) and that distinction needs proving.</para>
/// </summary>
public sealed class FakeFinanceGateway : IFinanceGateway
{
    public const string SalariesExpense  = "acct-5100";
    public const string StatutoryPayable = "acct-2210";
    public const string NetPayHolding    = "acct-2110";

    public static string CodeOf(string accountId) => accountId.Replace("acct-", "");

    public bool FailPosting { get; set; }
    public bool OmitHoldingAccount { get; set; }

    /// <summary>Finance unreachable — an empty chart. Distinct from a bad mapping: the run must be told the
    /// chart could not be read, not that each of its accounts is invalid.</summary>
    public bool ReturnEmptyChart { get; set; }
    public List<(string Schema, DateTime Date, string Description, string SourceDocumentId, List<JournalLineDto> Lines)> Posted { get; } = [];

    public Task<List<GlAccountDto>> ListAccountsAsync(CancellationToken ct = default)
    {
        if (ReturnEmptyChart) return Task.FromResult(new List<GlAccountDto>());

        var accounts = new List<GlAccountDto>
        {
            new(SalariesExpense,  "5100", "Salaries and Wages",   "Expense",   true, true),
            new(StatutoryPayable, "2210", "Statutory Deductions", "Liability", true, true),
        };
        if (!OmitHoldingAccount)
            accounts.Add(new GlAccountDto(NetPayHolding, "2110", "Net Pay Holding", "Liability", true, true));
        return Task.FromResult(accounts);
    }

    public Task<JournalPostResult> PostJournalAsync(
        string tenantSchema, DateTime entryDate, string description, string sourceDocumentId,
        List<JournalLineDto> lines, CancellationToken ct = default)
    {
        if (FailPosting)
            return Task.FromResult(new JournalPostResult(false, null, null, "Finance is unreachable."));

        Posted.Add((tenantSchema, entryDate, description, sourceDocumentId, lines));
        return Task.FromResult(new JournalPostResult(
            true, $"je-{Posted.Count}", $"JE-{Posted.Count:D4}", "Posted."));
    }

    public Task<List<OutstandingAdvanceDto>?> ListOutstandingAdvancesAsync(
        string employeeUserId, CancellationToken ct = default) =>
        Task.FromResult<List<OutstandingAdvanceDto>?>([]);
}

/// <summary>Records alerts so a test can assert one was raised without asserting on delivery.</summary>
public sealed class FakeAlertGateway : IHrAlertGateway
{
    public List<(string Source, string Severity, string Title, string Message)> Alerts { get; } = [];

    public Task CreateAlertAsync(
        string tenantSchema, string source, string severity, string title, string message,
        string? requiredPermission = null, string? assignedToUserId = null, CancellationToken ct = default)
    {
        Alerts.Add((source, severity, title, message));
        return Task.CompletedTask;
    }
}
