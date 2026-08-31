using Microsoft.EntityFrameworkCore;
using HrService.Core.Entities;

namespace HrService.Infrastructure.Data;

/// <summary>
/// HR &amp; Payroll module context (Module 3). Schema-per-tenant: the base model is schema-agnostic; the tenant
/// schema is bound via the connection's Postgres search_path (see <see cref="TenantDbConnectionInterceptor"/>
/// at request time, and TenantProvisioningService for migration). DbSets are added per phase.
/// </summary>
public class HrDbContext : DbContext
{
    public HrDbContext(DbContextOptions<HrDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (e.g. the schema-per-tenant variant).</summary>
    protected HrDbContext(DbContextOptions options) : base(options) { }

    // ── H1: employee master, documents, positions, org chart ──
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts => Set<EmployeeEmergencyContact>();
    public DbSet<EmployeeEducation> EmployeeEducation => Set<EmployeeEducation>();
    public DbSet<EmployeeEmploymentHistory> EmployeeEmploymentHistory => Set<EmployeeEmploymentHistory>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeBankDetail> EmployeeBankDetails => Set<EmployeeBankDetail>();
    public DbSet<EmployeeCertification> EmployeeCertifications => Set<EmployeeCertification>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<OrgChartNode> OrgChartNodes => Set<OrgChartNode>();
    public DbSet<HrAuditLog> AuditLogs => Set<HrAuditLog>();

    // ── H2: probation tracking & contract renewal ──
    public DbSet<ProbationReview> ProbationReviews => Set<ProbationReview>();
    public DbSet<ContractRenewalAlert> ContractRenewalAlerts => Set<ContractRenewalAlert>();

    // ── H3: leave configuration, entitlements, applications, carry-forward ──
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveEntitlement> LeaveEntitlements => Set<LeaveEntitlement>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveApprovalLog> LeaveApprovalLogs => Set<LeaveApprovalLog>();
    public DbSet<LeaveCarryForward> LeaveCarryForwards => Set<LeaveCarryForward>();

    // ── H4: attendance, absences, working-time configuration, holidays, reporting ──
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AbsenceRecord> AbsenceRecords => Set<AbsenceRecord>();
    public DbSet<AttendanceScorecard> AttendanceScorecards => Set<AttendanceScorecard>();
    public DbSet<AttendanceMonthlyReport> AttendanceMonthlyReports => Set<AttendanceMonthlyReport>();
    public DbSet<AttendanceSetting> AttendanceSettings => Set<AttendanceSetting>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();

    // ── H5: grades, salary structures, pay assignments, statutory rates, deductions ──
    public DbSet<JobGrade> JobGrades => Set<JobGrade>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<EmployeeSalary> EmployeeSalaries => Set<EmployeeSalary>();
    public DbSet<PayeTaxBand> PayeTaxBands => Set<PayeTaxBand>();
    public DbSet<StatutoryRate> StatutoryRates => Set<StatutoryRate>();
    public DbSet<PayrollDeductionType> PayrollDeductionTypes => Set<PayrollDeductionType>();
    public DbSet<PayrollDeduction> PayrollDeductions => Set<PayrollDeduction>();

    // ── H6: overtime, payroll runs and payslips ──
    public DbSet<OvertimeRequest> OvertimeRequests => Set<OvertimeRequest>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<PayslipLine> PayslipLines => Set<PayslipLine>();
    public DbSet<BankPaymentFile> BankPaymentFiles => Set<BankPaymentFile>();

    // ── H7: learning & development, training hours, mandatory compliance, knowledge sharing, budgets ──
    public DbSet<LearningDevelopmentPlan> LearningDevelopmentPlans => Set<LearningDevelopmentPlan>();
    public DbSet<LdpObjective> LdpObjectives => Set<LdpObjective>();
    public DbSet<TrainingEvent> TrainingEvents => Set<TrainingEvent>();
    public DbSet<TrainingHoursLog> TrainingHoursLogs => Set<TrainingHoursLog>();
    public DbSet<MandatoryTrainingRequirement> MandatoryTrainingRequirements => Set<MandatoryTrainingRequirement>();
    public DbSet<KnowledgeSharingSession> KnowledgeSharingSessions => Set<KnowledgeSharingSession>();
    public DbSet<KnowledgeSharingAttendance> KnowledgeSharingAttendance => Set<KnowledgeSharingAttendance>();
    public DbSet<LdBudget> LdBudgets => Set<LdBudget>();
    public DbSet<LearningRedFlag> LearningRedFlags => Set<LearningRedFlag>();

    // ── H8: salary increments ──
    public DbSet<SalaryIncrement> SalaryIncrements => Set<SalaryIncrement>();

    // ── H9: KPI scorecards, targets, appraisals, 360 feedback, improvement plans ──
    public DbSet<KpiScorecard> KpiScorecards => Set<KpiScorecard>();
    public DbSet<KpiScorecardItem> KpiScorecardItems => Set<KpiScorecardItem>();
    public DbSet<KpiTarget> KpiTargets => Set<KpiTarget>();
    public DbSet<AppraisalCycle> AppraisalCycles => Set<AppraisalCycle>();
    public DbSet<Appraisal> Appraisals => Set<Appraisal>();
    public DbSet<AppraisalItemScore> AppraisalItemScores => Set<AppraisalItemScore>();
    public DbSet<Feedback360> Feedback360s => Set<Feedback360>();
    public DbSet<PerformanceImprovementPlan> PerformanceImprovementPlans => Set<PerformanceImprovementPlan>();

    // ── H10: discipline, warnings, grievances, separation ──
    public DbSet<DisciplinaryCase> DisciplinaryCases => Set<DisciplinaryCase>();
    public DbSet<WarningRecord> WarningRecords => Set<WarningRecord>();
    public DbSet<GrievanceCase> GrievanceCases => Set<GrievanceCase>();
    public DbSet<Separation> Separations => Set<Separation>();

    // ── H11: commission bands, plans, statements, disputes ──
    public DbSet<CommissionBand> CommissionBands => Set<CommissionBand>();
    public DbSet<CommissionPlan> CommissionPlans => Set<CommissionPlan>();
    public DbSet<CommissionStatement> CommissionStatements => Set<CommissionStatement>();
    public DbSet<CommissionDispute> CommissionDisputes => Set<CommissionDispute>();

    // ── H12: recruitment — requisitions, vacancies, applicants, interviews, offers ──
    public DbSet<JobRequisition> JobRequisitions => Set<JobRequisition>();
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();

    /// <summary>Money columns. Kenyan payroll never approaches the ceiling; two decimals is the cent.</summary>
    private const int MoneyPrecision = 18, MoneyScale = 2;

    /// <summary>Percentage columns, stored as the percent itself (30 for 30%, 2.75 for 2.75%).</summary>
    private const int RatePrecision = 7, RateScale = 4;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.EmployeeNumber).IsUnique();
            e.HasIndex(x => x.UserId);            // the HR-DEC-2 join key to the rest of the estate
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DepartmentId);
            e.HasIndex(x => x.ReportsToId);
            e.Ignore(x => x.FullName);

            e.HasMany(x => x.EmergencyContacts).WithOne(c => c.Employee!).HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Education).WithOne(c => c.Employee!).HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.EmploymentHistory).WithOne(c => c.Employee!).HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Documents).WithOne(c => c.Employee!).HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.BankDetails).WithOne(c => c.Employee!).HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Certifications).WithOne(c => c.Employee!).HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        foreach (var child in new[]
        {
            typeof(EmployeeEmergencyContact), typeof(EmployeeEducation), typeof(EmployeeEmploymentHistory),
            typeof(EmployeeDocument), typeof(EmployeeBankDetail), typeof(EmployeeCertification),
        })
        {
            modelBuilder.Entity(child).HasIndex(nameof(EmployeeEmergencyContact.EmployeeId));
        }

        modelBuilder.Entity<EmployeeEmergencyContact>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<EmployeeEducation>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<EmployeeEmploymentHistory>().HasQueryFilter(x => !x.IsDeleted);

        modelBuilder.Entity<EmployeeDocument>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.DocumentType);
            e.HasIndex(x => x.IsVerified);
        });

        modelBuilder.Entity<EmployeeBankDetail>().HasQueryFilter(x => !x.IsDeleted);

        modelBuilder.Entity<EmployeeCertification>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.ExpiryDate);        // the H2 30-day expiry sweep
        });

        modelBuilder.Entity<Position>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Title);
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<OrgChartNode>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.EmployeeId).IsUnique();   // one node per employee
            e.HasIndex(x => x.ParentEmployeeId);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProbationReview>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One review per employee per milestone — this is what stops the daily sweep raising duplicates.
            e.HasIndex(x => new { x.EmployeeId, x.ReviewType, x.ScheduledDate }).IsUnique();
            e.HasIndex(x => x.Outcome);
            e.HasIndex(x => x.ScheduledDate);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractRenewalAlert>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One alert per contract period; a new end date opens a new row rather than rewriting history.
            e.HasIndex(x => new { x.EmployeeId, x.ContractEndDate }).IsUnique();
            e.HasIndex(x => x.Outcome);
            e.HasIndex(x => x.ContractEndDate);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeaveType>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // The code is the stable key the seeder and the carry-forward job match on.
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.DaysAllowed).HasPrecision(6, 2);
            e.Property(x => x.FullPayDays).HasPrecision(6, 2);
            e.Property(x => x.MaxCarryForwardDays).HasPrecision(6, 2);
        });

        modelBuilder.Entity<LeaveEntitlement>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One allowance per employee, type and year — this is what makes assignment idempotent.
            e.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
            e.HasIndex(x => x.Year);
            e.Property(x => x.DaysEntitled).HasPrecision(6, 2);
            e.Property(x => x.DaysTaken).HasPrecision(6, 2);
            e.Property(x => x.CarriedForwardDays).HasPrecision(6, 2);
            e.Property(x => x.ForfeitedDays).HasPrecision(6, 2);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeaveRequest>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.RequestNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.EmployeeId, x.StartDate });   // the overlap check
            e.HasIndex(x => x.StartDate);
            e.Property(x => x.DaysRequested).HasPrecision(6, 2);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ApprovalLog).WithOne(a => a.LeaveRequest!).HasForeignKey(a => a.LeaveRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeaveApprovalLog>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One row per step of a request's chain.
            e.HasIndex(x => new { x.LeaveRequestId, x.Step }).IsUnique();
            e.HasIndex(x => x.Action);
        });

        modelBuilder.Entity<LeaveCarryForward>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // The row's existence is the year-end job's idempotence guard.
            e.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.FromYear }).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ExpiryDate);          // the 31 March expiry pass
            e.Property(x => x.DaysCarried).HasPrecision(6, 2);
            e.Property(x => x.DaysForfeited).HasPrecision(6, 2);
            e.Property(x => x.DaysExpired).HasPrecision(6, 2);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmployeeDocument>().HasIndex(x => x.LeaveRequestId);   // H3 leave attachments

        modelBuilder.Entity<AttendanceRecord>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One row per employee per day — stops a double clock-in and makes the daily sweep idempotent.
            e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.Status);
            e.Property(x => x.HoursWorked).HasPrecision(6, 2);
            // Decimal degrees: 8 digits with 6 decimals resolves to about 10 cm, ample for a site stamp.
            e.Property(x => x.ClockInLatitude).HasPrecision(9, 6);
            e.Property(x => x.ClockInLongitude).HasPrecision(9, 6);
            e.Property(x => x.ClockOutLatitude).HasPrecision(9, 6);
            e.Property(x => x.ClockOutLongitude).HasPrecision(9, 6);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AbsenceRecord>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.IsAuthorised);
            e.HasIndex(x => x.ReleasedToPayrollAt);      // the H6 unpaid-deduction feed
            e.Property(x => x.UnpaidDays).HasPrecision(6, 2);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AttendanceScorecard>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.EmployeeId, x.Year }).IsUnique();
            e.HasIndex(x => x.Year);
            e.Property(x => x.PunctualityRate).HasPrecision(5, 1);
            e.Property(x => x.AbsenceRate).HasPrecision(5, 1);
            e.Property(x => x.OvertimeHours).HasPrecision(8, 2);
            e.Property(x => x.Score).HasPrecision(5, 1);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AttendanceMonthlyReport>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // The row's existence per department and month is the monthly job's idempotence guard.
            e.HasIndex(x => new { x.DepartmentId, x.Year, x.Month }).IsUnique();
            e.HasIndex(x => new { x.Year, x.Month });
            e.Property(x => x.UnpaidDays).HasPrecision(8, 2);
            e.Property(x => x.OvertimeHours).HasPrecision(8, 2);
            e.Property(x => x.PunctualityRate).HasPrecision(5, 1);
            e.Property(x => x.AbsenceRate).HasPrecision(5, 1);
        });

        modelBuilder.Entity<AttendanceSetting>().HasQueryFilter(x => !x.IsDeleted);

        modelBuilder.Entity<PublicHoliday>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<JobGrade>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Code).IsUnique();     // the stable key a structure and any import match on
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.MinSalary).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.MaxSalary).HasPrecision(MoneyPrecision, MoneyScale);
        });

        modelBuilder.Entity<SalaryStructure>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // Two structures with the same name is always a mistake — the name is what HR picks from.
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.JobGradeId);
            e.HasIndex(x => x.IsActive);
            e.HasMany(x => x.Components).WithOne(c => c.SalaryStructure!).HasForeignKey(c => c.SalaryStructureId)
                .OnDelete(DeleteBehavior.Cascade);
            // Restrict, not cascade: removing a grade must not silently take the structures priced against it.
            e.HasOne(x => x.JobGrade!).WithMany().HasForeignKey(x => x.JobGradeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalaryComponent>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One BASIC (or PAYE, or HOUSE) per structure — the H6 engine matches on the code, so a duplicate
            // would make "the basic pay line" ambiguous.
            e.HasIndex(x => new { x.SalaryStructureId, x.Code }).IsUnique();
            e.HasIndex(x => x.ComponentType);
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.Amount).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.Percentage).HasPrecision(RatePrecision, RateScale);
        });

        modelBuilder.Entity<PayrollPeriod>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // "2026-07" is derived from Year+Month, so one unique index covers both.
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.Year, x.Month });
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<EmployeeSalary>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => new { x.EmployeeId, x.Status });      // "what is this person on now"
            e.HasIndex(x => x.EffectiveFromPeriodId);
            e.Property(x => x.BasicSalary).HasPrecision(MoneyPrecision, MoneyScale);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.SalaryStructure!).WithMany().HasForeignKey(x => x.SalaryStructureId)
                .OnDelete(DeleteBehavior.Restrict);

            // At most ONE live assignment per employee per effective period. Superseded and Rejected rows are
            // history and must be free to pile up, which is why this is a filtered index rather than a plain
            // unique one — without the filter, a raise could never supersede anything.
            // NOTE: the filter names SalaryAssignmentStatus by its stored ordinal (0 Proposed, 1 Approved).
            // Reordering that enum changes what this index means, silently — add new members at the END.
            e.HasIndex(x => new { x.EmployeeId, x.EffectiveFromPeriodId })
                .IsUnique()
                .HasFilter("\"Status\" IN (0, 1) AND NOT \"IsDeleted\"");
        });

        modelBuilder.Entity<PayeTaxBand>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One band per position per rate set — a re-run of an old month must find exactly one scale.
            e.HasIndex(x => new { x.EffectiveFrom, x.BandOrder }).IsUnique();
            e.HasIndex(x => x.EffectiveFrom);
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.LowerBound).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.UpperBound).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.Rate).HasPrecision(RatePrecision, RateScale);
        });

        modelBuilder.Entity<StatutoryRate>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One rule per code per effective date — same reason as the PAYE bands.
            e.HasIndex(x => new { x.Code, x.EffectiveFrom }).IsUnique();
            e.HasIndex(x => x.Component);
            e.HasIndex(x => x.EffectiveFrom);
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.Rate).HasPrecision(RatePrecision, RateScale);
            e.Property(x => x.EmployerRate).HasPrecision(RatePrecision, RateScale);
            e.Property(x => x.FixedAmount).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.TierLowerBound).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.TierUpperBound).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.MinAmount).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.MaxAmount).HasPrecision(MoneyPrecision, MoneyScale);
        });

        modelBuilder.Entity<PayrollDeductionType>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Code).IsUnique();     // configure once, apply to many (P8)
            e.HasIndex(x => x.Category);
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<PayrollDeduction>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.EmployeeId, x.IsActive });    // what a payroll run collects per employee
            e.HasIndex(x => x.DeductionTypeId);
            e.HasIndex(x => x.StartPeriodId);
            e.HasIndex(x => x.EndPeriodId);
            e.Property(x => x.Amount).HasPrecision(MoneyPrecision, MoneyScale);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            // Restrict: a deduction type still in use cannot be deleted out from under the deductions that
            // reference it — stopping a deduction is a status change (P8), never a delete.
            e.HasOne(x => x.DeductionType!).WithMany().HasForeignKey(x => x.DeductionTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OvertimeRequest>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One overtime request per employee per day — the same evening cannot be claimed twice.
            e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.PayrollRunId);          // "what has this run already taken"
            e.Property(x => x.Hours).HasPrecision(6, 2);
            e.Property(x => x.Multiplier).HasPrecision(4, 2);
            e.Property(x => x.PaidAmount).HasPrecision(MoneyPrecision, MoneyScale);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PayrollRun>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PayrollPeriodId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.RunNumber);
            // ONE LIVE RUN PER PERIOD (P9 step 9.1) — the duplicate check that stops a month being paid twice.
            // Filtered so a cancelled run can be replaced: 3 is PayrollRunStatus.Cancelled.
            // NOTE: this names the enum's stored ORDINAL. Append new members, never insert them.
            e.HasIndex(x => x.PayrollPeriodId)
                .IsUnique()
                .HasDatabaseName("IX_PayrollRuns_LivePerPeriod")
                .HasFilter("\"Status\" <> 3 AND NOT \"IsDeleted\"");
            foreach (var money in new[]
            {
                nameof(PayrollRun.TotalGross), nameof(PayrollRun.TotalTaxable), nameof(PayrollRun.TotalPaye),
                nameof(PayrollRun.TotalStatutory), nameof(PayrollRun.TotalOtherDeductions),
                nameof(PayrollRun.TotalDeductions), nameof(PayrollRun.TotalNet), nameof(PayrollRun.TotalEmployerCost),
            })
                e.Property(money).HasPrecision(MoneyPrecision, MoneyScale);
            e.HasMany(x => x.Payslips).WithOne(p => p.PayrollRun!).HasForeignKey(p => p.PayrollRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payslip>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One payslip per employee per run — a recompute replaces rather than accumulates.
            e.HasIndex(x => new { x.PayrollRunId, x.EmployeeId }).IsUnique();
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.PayrollPeriodCode);     // the P9 annual certificate reads by employee and year
            foreach (var money in new[]
            {
                nameof(Payslip.BasicSalary), nameof(Payslip.GrossPay), nameof(Payslip.TaxableIncome),
                nameof(Payslip.Paye), nameof(Payslip.PersonalRelief), nameof(Payslip.StatutoryDeductions),
                nameof(Payslip.OtherDeductions), nameof(Payslip.TotalEarnings), nameof(Payslip.TotalDeductions),
                nameof(Payslip.NetPay), nameof(Payslip.EmployerCost), nameof(Payslip.OvertimePay),
                nameof(Payslip.UnpaidDeduction),
            })
                e.Property(money).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.OvertimeHours).HasPrecision(6, 2);
            e.Property(x => x.UnpaidDays).HasPrecision(6, 2);
            e.HasMany(x => x.Lines).WithOne(l => l.Payslip!).HasForeignKey(l => l.PayslipId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PayslipLine>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.PayslipId, x.LineOrder });
            e.HasIndex(x => x.LineType);
            e.Property(x => x.Amount).HasPrecision(MoneyPrecision, MoneyScale);
        });

        modelBuilder.Entity<BankPaymentFile>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PayrollRunId);
            e.HasIndex(x => x.Format);
            e.Property(x => x.TotalAmount).HasPrecision(MoneyPrecision, MoneyScale);
            e.HasOne(x => x.PayrollRun!).WithMany().HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LearningDevelopmentPlan>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One plan per employee per year — the row's existence answers "has this person filed one",
            // which is what makes the 31 January deadline sweep idempotent.
            e.HasIndex(x => new { x.EmployeeId, x.PlanYear }).IsUnique();
            e.HasIndex(x => x.PlanYear);
            e.HasIndex(x => x.Status);
            e.HasMany(x => x.Objectives).WithOne(o => o.Plan!).HasForeignKey(o => o.LearningDevelopmentPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LdpObjective>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.LearningDevelopmentPlanId);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<TrainingEvent>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.TrainingDate);
            e.HasIndex(x => x.DepartmentId);
            e.HasIndex(x => x.MandatoryTrainingCode);
            e.HasIndex(x => x.KnowledgeSharingSessionId);
            e.Property(x => x.DurationHours).HasPrecision(6, 2);
            e.Property(x => x.Cost).HasPrecision(MoneyPrecision, MoneyScale);
            e.HasMany(x => x.Attendance).WithOne(a => a.TrainingEvent!).HasForeignKey(a => a.TrainingEventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrainingHoursLog>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One attendance row per person per event — logging the same course twice would inflate the
            // YTD total that the 40/60-hour target is measured against.
            e.HasIndex(x => new { x.TrainingEventId, x.EmployeeId }).IsUnique();
            e.HasIndex(x => new { x.EmployeeId, x.TrainingDate });
            e.HasIndex(x => x.LdpObjectiveId);
            e.Property(x => x.Hours).HasPrecision(6, 2);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MandatoryTrainingRequirement>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<KnowledgeSharingSession>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.SessionDate);          // the monthly "at least two" count (HR-030)
            e.HasIndex(x => x.FacilitatorEmployeeId);
            e.Property(x => x.DurationHours).HasPrecision(6, 2);
            e.HasMany(x => x.Attendance).WithOne(a => a.Session!).HasForeignKey(a => a.KnowledgeSharingSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeSharingAttendance>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.KnowledgeSharingSessionId, x.EmployeeId }).IsUnique();
            e.HasIndex(x => x.EmployeeId);
        });

        modelBuilder.Entity<LdBudget>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One budget per department per year — the assignment idempotence key.
            e.HasIndex(x => new { x.DepartmentId, x.Year }).IsUnique();
            e.HasIndex(x => x.Year);
            e.Property(x => x.BudgetedAmount).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.ActualSpend).HasPrecision(MoneyPrecision, MoneyScale);
        });

        modelBuilder.Entity<LearningRedFlag>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // The key IS the idempotence guard — a flag already raised cannot be raised again.
            e.HasIndex(x => new { x.FlagType, x.FlagKey }).IsUnique();
            e.HasIndex(x => x.EmployeeId);
        });

        modelBuilder.Entity<SalaryIncrement>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.EffectivePeriodId);
            // At most ONE increment awaiting the MD per employee — two proposals in the queue would let the
            // same rise be approved twice. 0 is SalaryIncrementStatus.PendingMd.
            // NOTE: names the enum's stored ordinal. Append new members, never insert them.
            e.HasIndex(x => x.EmployeeId)
                .IsUnique()
                .HasDatabaseName("IX_SalaryIncrements_OnePendingPerEmployee")
                .HasFilter("\"Status\" = 0 AND NOT \"IsDeleted\"");
            e.Property(x => x.CurrentSalary).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.ProposedSalary).HasPrecision(MoneyPrecision, MoneyScale);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KpiScorecard>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One scorecard per role per year — two would leave "which one am I measured on" unanswerable.
            e.HasIndex(x => new { x.PositionId, x.Year }).IsUnique();
            e.HasIndex(x => x.Year);
            e.Property(x => x.PipThreshold).HasPrecision(5, 2);
            e.HasMany(x => x.Items).WithOne(i => i.Scorecard!).HasForeignKey(i => i.KpiScorecardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KpiScorecardItem>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.KpiScorecardId);
            e.Property(x => x.WeightPercent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<KpiTarget>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One target per employee per item per year — a duplicate would be double-counted in the score.
            e.HasIndex(x => new { x.EmployeeId, x.KpiScorecardItemId, x.Year }).IsUnique();
            e.HasIndex(x => new { x.EmployeeId, x.Year });
            e.Property(x => x.TargetValue).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.ActualValue).HasPrecision(MoneyPrecision, MoneyScale);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppraisalCycle>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One cycle per type per year — the guard that stops a window being opened twice.
            e.HasIndex(x => new { x.Year, x.CycleType }).IsUnique();
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<Appraisal>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One appraisal per employee per cycle.
            e.HasIndex(x => new { x.AppraisalCycleId, x.EmployeeId }).IsUnique();
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.Status);
            foreach (var score in new[]
            {
                nameof(Appraisal.SelfScore), nameof(Appraisal.LineManagerScore),
                nameof(Appraisal.Feedback360Score), nameof(Appraisal.FinalScore), nameof(Appraisal.PipThreshold),
            })
                e.Property(score).HasPrecision(5, 2);
            e.HasMany(x => x.ItemScores).WithOne(i => i.Appraisal!).HasForeignKey(i => i.AppraisalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppraisalItemScore>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.AppraisalId, x.DisplayOrder });
            e.Property(x => x.WeightPercent).HasPrecision(5, 2);
            e.Property(x => x.TargetValue).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.ActualValue).HasPrecision(MoneyPrecision, MoneyScale);
            foreach (var score in new[]
            {
                nameof(AppraisalItemScore.SelfScore), nameof(AppraisalItemScore.ManagerScore),
                nameof(AppraisalItemScore.FinalScore),
            })
                e.Property(score).HasPrecision(5, 2);
        });

        modelBuilder.Entity<Feedback360>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One request per reviewer per appraisal — nobody gets two votes.
            e.HasIndex(x => new { x.AppraisalId, x.ReviewerEmployeeId }).IsUnique();
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.OverallScore).HasPrecision(5, 2);
        });

        modelBuilder.Entity<PerformanceImprovementPlan>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.AppraisalId);
            e.Property(x => x.TriggerScore).HasPrecision(5, 2);
            e.Property(x => x.Threshold).HasPrecision(5, 2);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DisciplinaryCase>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CaseNumber).IsUnique();
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.IncidentDate);
            e.HasIndex(x => x.ResponseDeadline);        // the show-cause overdue sweep
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WarningRecord>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.EmployeeId, x.IssuedDate });   // the 3-in-12-months count
            e.HasIndex(x => x.IsActive);
            e.HasIndex(x => x.ExpiryDate);                          // the daily expiry sweep
            e.HasIndex(x => x.DisciplinaryCaseId);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GrievanceCase>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CaseNumber).IsUnique();
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.AcknowledgementDeadline);             // the 2-working-day SLA sweep
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Separation>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.SeparationNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.EffectiveDate);
            // At most ONE live separation per employee — two would compute the same dues twice.
            // 4 is SeparationStatus.Cancelled. NOTE: names the enum's stored ordinal; append members only.
            e.HasIndex(x => x.EmployeeId)
                .IsUnique()
                .HasDatabaseName("IX_Separations_OneLivePerEmployee")
                .HasFilter("\"Status\" <> 4 AND NOT \"IsDeleted\"");
            foreach (var money in new[]
            {
                nameof(Separation.MonthlySalary), nameof(Separation.DailyRate), nameof(Separation.LeavePayout),
                nameof(Separation.ProRataSalary), nameof(Separation.NoticePay), nameof(Separation.OtherEarnings),
                nameof(Separation.AdvanceRecovery), nameof(Separation.OtherDeductions), nameof(Separation.NetDues),
            })
                e.Property(money).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.LeaveDaysBalance).HasPrecision(6, 2);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommissionBand>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One band per position per dated scale — the same shape as the PAYE bands.
            e.HasIndex(x => new { x.EffectiveFrom, x.DisplayOrder }).IsUnique();
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.MinPercent).HasPrecision(7, 2);
            e.Property(x => x.MaxPercent).HasPrecision(7, 2);
            e.Property(x => x.CommissionRatePercent).HasPrecision(7, 4);
        });

        modelBuilder.Entity<CommissionPlan>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            // One plan per employee per year.
            e.HasIndex(x => new { x.EmployeeId, x.Year }).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommissionStatement>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.StatementNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.PayrollRunId);          // "what has this run already paid"
            // One LIVE statement per employee per quarter — two would pay the same quarter twice.
            // 4 is CommissionStatementStatus.Cancelled. NOTE: ordinal-dependent; append enum members only.
            e.HasIndex(x => new { x.EmployeeId, x.Year, x.Quarter })
                .IsUnique()
                .HasDatabaseName("IX_CommissionStatements_OneLivePerQuarter")
                .HasFilter("\"Status\" <> 4 AND NOT \"IsDeleted\"");
            foreach (var money in new[]
            {
                nameof(CommissionStatement.AnnualTarget), nameof(CommissionStatement.QuarterTarget),
                nameof(CommissionStatement.RevenueAchieved), nameof(CommissionStatement.CommissionAmount),
                nameof(CommissionStatement.PaidAmount), nameof(CommissionStatement.CommissionEarnedToDate),
                nameof(CommissionStatement.PriorCommissionThisYear), nameof(CommissionStatement.UnrecoveredOverpayment),
            })
                e.Property(money).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.AttainmentPercent).HasPrecision(9, 2);
            e.Property(x => x.ProRatedAttainmentPercent).HasPrecision(9, 2);
            e.Property(x => x.CommissionRatePercent).HasPrecision(7, 4);
            e.HasOne(x => x.Employee!).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommissionDispute>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CommissionStatementId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.DisputedAmount).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.AdjustedAmount).HasPrecision(MoneyPrecision, MoneyScale);
        });

        // ── H12: recruitment ──
        modelBuilder.Entity<JobRequisition>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.RequisitionNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DepartmentId);
            // The establishment check counts live requisitions per position on every raise.
            e.HasIndex(x => new { x.PositionId, x.Status });
            e.HasOne(x => x.Position!).WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Vacancy>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.VacancyNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.JobRequisitionId);      // "how many heads has this requisition advertised"
            e.HasIndex(x => x.DepartmentId);
            e.HasIndex(x => x.ClosingDate);           // the daily closing-date sweep
            e.Property(x => x.SalaryRangeMin).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.SalaryRangeMax).HasPrecision(MoneyPrecision, MoneyScale);
            e.HasOne(x => x.JobRequisition!).WithMany().HasForeignKey(x => x.JobRequisitionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Applicant>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.ApplicantNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.VacancyId);
            e.HasIndex(x => x.InternalEmployeeId);
            e.HasIndex(x => x.Source);                // the source-effectiveness view
            // One application per email per vacancy — the same person cannot apply twice for one post.
            e.HasIndex(x => new { x.VacancyId, x.Email })
                .IsUnique()
                .HasDatabaseName("IX_Applicants_OnePerEmailPerVacancy")
                .HasFilter("NOT \"IsDeleted\"");
            e.Property(x => x.ExpectedSalary).HasPrecision(MoneyPrecision, MoneyScale);
            e.Property(x => x.AverageInterviewScore).HasPrecision(5, 2);
            e.HasOne(x => x.Vacancy!).WithMany().HasForeignKey(x => x.VacancyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Interview>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.ApplicantId);
            e.HasIndex(x => x.VacancyId);
            e.HasIndex(x => x.ScheduledAt);           // the overdue-interview count
            foreach (var score in new[]
            {
                nameof(Interview.TechnicalScore), nameof(Interview.ExperienceScore),
                nameof(Interview.CommunicationScore), nameof(Interview.CulturalFitScore),
                nameof(Interview.OverallScore),
            })
                e.Property(score).HasPrecision(5, 2);
            e.HasOne(x => x.Applicant!).WithMany().HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobOffer>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.OfferNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.VacancyId);
            e.HasIndex(x => x.ResponseDeadline);      // the daily lapse sweep
            // At most ONE live offer per applicant — two would each hold a seat for the same person.
            // 5, 6 and 7 are Declined, Lapsed and Withdrawn. NOTE: ordinal-dependent; append enum members only.
            e.HasIndex(x => x.ApplicantId)
                .IsUnique()
                .HasDatabaseName("IX_JobOffers_OneLivePerApplicant")
                .HasFilter("\"Status\" NOT IN (5, 6, 7) AND NOT \"IsDeleted\"");
            e.Property(x => x.OfferedSalary).HasPrecision(MoneyPrecision, MoneyScale);
            e.HasOne(x => x.Applicant!).WithMany().HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Vacancy!).WithMany().HasForeignKey(x => x.VacancyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HrAuditLog>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.OccurredAt);
        });
    }
}
