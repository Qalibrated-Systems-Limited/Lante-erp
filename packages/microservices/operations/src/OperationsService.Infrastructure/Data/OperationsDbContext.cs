using Microsoft.EntityFrameworkCore;
using OperationsService.Core.Entities;
using System.Linq;

namespace OperationsService.Infrastructure.Data;

public class OperationsDbContext : DbContext
{
    public OperationsDbContext(DbContextOptions<OperationsDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (e.g. the schema-per-tenant variant).</summary>
    protected OperationsDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<ProjectApproval> ProjectApprovals => Set<ProjectApproval>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<BudgetVersion> BudgetVersions => Set<BudgetVersion>();
    public DbSet<ContractRate> ContractRates => Set<ContractRate>();
    public DbSet<MilestoneDependency> MilestoneDependencies => Set<MilestoneDependency>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<CostEntry> CostEntries => Set<CostEntry>();
    public DbSet<ProjectResource> ProjectResources => Set<ProjectResource>();
    public DbSet<ProjectHistory> ProjectHistories => Set<ProjectHistory>();
    public DbSet<ProjectDailyReport> ProjectDailyReports => Set<ProjectDailyReport>();
    public DbSet<ProjectAlertLog> ProjectAlertLogs => Set<ProjectAlertLog>();
    public DbSet<MilestoneUpdateLog> MilestoneUpdateLogs => Set<MilestoneUpdateLog>();
    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<TimesheetEntry> TimesheetEntries => Set<TimesheetEntry>();
    public DbSet<ReferenceStandard> ReferenceStandards => Set<ReferenceStandard>();
    public DbSet<CalibrationCertificate> CalibrationCertificates => Set<CalibrationCertificate>();
    public DbSet<CalibrationAuditLog> CalibrationAuditLogs => Set<CalibrationAuditLog>();
    public DbSet<OperationsAuditLog> OperationsAuditLogs => Set<OperationsAuditLog>();
    public DbSet<VariationOrder> VariationOrders => Set<VariationOrder>();
    public DbSet<VariationOrderLine> VariationOrderLines => Set<VariationOrderLine>();
    public DbSet<ProjectHandover> ProjectHandovers => Set<ProjectHandover>();
    public DbSet<ProjectHandoverSignature> ProjectHandoverSignatures => Set<ProjectHandoverSignature>();
    public DbSet<ProgramOverrunNotice> ProgramOverrunNotices => Set<ProgramOverrunNotice>();
    public DbSet<NegligenceIncident> NegligenceIncidents => Set<NegligenceIncident>();
    public DbSet<NegligenceResponse> NegligenceResponses => Set<NegligenceResponse>();

    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignedTechnician> AssignedTechnicians => Set<AssignedTechnician>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<ServiceReport> ServiceReports => Set<ServiceReport>();
    public DbSet<FsrEquipment> FsrEquipment => Set<FsrEquipment>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();

    public DbSet<Requisition> Requisitions => Set<Requisition>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<PettyCashAdvanceForm> PettyCashForms => Set<PettyCashAdvanceForm>();
    public DbSet<PerDiemReturnForm> PerDiemReturnForms => Set<PerDiemReturnForm>();
    public DbSet<AdvanceReturnForm> AdvanceReturnForms => Set<AdvanceReturnForm>();
    public DbSet<AdvanceReturnLineItem> AdvanceReturnLineItems => Set<AdvanceReturnLineItem>();
    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<PerformanceMetrics> PerformanceMetrics => Set<PerformanceMetrics>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<FieldVehicle> FieldVehicles => Set<FieldVehicle>();
    public DbSet<VehicleDispatch> VehicleDispatches => Set<VehicleDispatch>();
    public DbSet<FuelLog> FuelLogs => Set<FuelLog>();

    public DbSet<LabWorkOrder>  LabWorkOrders  => Set<LabWorkOrder>();
    public DbSet<LabDataSheet> LabDataSheets  => Set<LabDataSheet>();
    public DbSet<PreDeploymentChecklist> PreDeploymentChecklists => Set<PreDeploymentChecklist>();
    public DbSet<NonConformanceReport> NonConformanceReports => Set<NonConformanceReport>();
    public DbSet<RiskEntry> RiskEntries => Set<RiskEntry>();

    // PR3 — RAID + change control
    public DbSet<ProjectIssue> ProjectIssues => Set<ProjectIssue>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();

    // PR3b — comment threads + mentions
    public DbSet<ProjectComment> ProjectComments => Set<ProjectComment>();
    public DbSet<CommentMention> CommentMentions => Set<CommentMention>();

    // PR4b — templates + recurring work
    public DbSet<ProjectTemplate> ProjectTemplates => Set<ProjectTemplate>();
    public DbSet<ProjectTemplateMilestone> ProjectTemplateMilestones => Set<ProjectTemplateMilestone>();
    public DbSet<ProjectTemplateTask> ProjectTemplateTasks => Set<ProjectTemplateTask>();
    public DbSet<ProjectTemplateBudgetLine> ProjectTemplateBudgetLines => Set<ProjectTemplateBudgetLine>();
    public DbSet<RecurringProjectSchedule> RecurringProjectSchedules => Set<RecurringProjectSchedule>();
    public DbSet<CustomerFeedback> CustomerFeedbacks => Set<CustomerFeedback>();

    // O5 — Service Request / Calibration Request domain (migrated from ticketing)
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<ServiceRequestInstrument> ServiceRequestInstruments => Set<ServiceRequestInstrument>();
    public DbSet<Quotation> Quotations => Set<Quotation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Money-column precision backstop (#383): every decimal defaults to numeric(18,2)
        // unless a naming pattern below says otherwise, or an explicit .HasColumnType(...)
        // further down in this method overrides it for that specific property.
        foreach (var prop in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            var name = prop.Name;
            prop.SetColumnType(
                name.Contains("Latitude") || name.Contains("Longitude") ? "numeric(9,6)"
                : name.EndsWith("Pct") || name.EndsWith("Percent") || name.Contains("Probability") ? "numeric(9,4)"
                : "numeric(18,2)");
        }

        modelBuilder.Entity<Project>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.ContractValue).HasColumnType("decimal(18,2)");
            e.Property(p => p.PlannedBudget).HasColumnType("decimal(18,2)");
            e.Property(p => p.ActualCost).HasColumnType("decimal(18,2)");
            e.Property(p => p.Committed).HasColumnType("decimal(18,2)");
            e.Property(p => p.LdRatePerDay).HasColumnType("decimal(6,4)");
            e.Property(p => p.LdCapPct).HasColumnType("decimal(6,4)");
            e.HasMany(p => p.Milestones).WithOne(m => m.Project).HasForeignKey(m => m.ProjectId);
            e.HasMany(p => p.Approvals).WithOne(a => a.Project).HasForeignKey(a => a.ProjectId);
            e.HasMany(p => p.BudgetLines).WithOne(b => b.Project).HasForeignKey(b => b.ProjectId);
            e.HasMany(p => p.CostEntries).WithOne(c => c.Project).HasForeignKey(c => c.ProjectId);
            e.HasMany(p => p.Resources).WithOne(r => r.Project).HasForeignKey(r => r.ProjectId);
            e.HasQueryFilter(p => !p.IsDeleted);
        });

        modelBuilder.Entity<Milestone>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.LdAmount).HasColumnType("decimal(18,2)");
            e.HasMany(m => m.Tasks).WithOne(t => t.Milestone).HasForeignKey(t => t.MilestoneId);
            e.HasMany(m => m.UpdateLogs).WithOne(u => u.Milestone).HasForeignKey(u => u.MilestoneId);
            e.HasQueryFilter(m => !m.IsDeleted);
        });

        // O3 — MILESTONE_UPDATE_LOG
        modelBuilder.Entity<MilestoneUpdateLog>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.MilestoneId);
            e.HasIndex(u => u.ProjectId);
            e.HasQueryFilter(u => !u.IsDeleted);
        });

        // PROJECT_DAILY_REPORT — project-level daily site reports.
        modelBuilder.Entity<ProjectDailyReport>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.ProjectId);
            e.HasOne(r => r.Project).WithMany().HasForeignKey(r => r.ProjectId);
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // O5-FSR — FSR_EQUIPMENT
        modelBuilder.Entity<FsrEquipment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ServiceReportId);
            e.HasIndex(x => x.SerialNumber);
            e.HasOne(x => x.ServiceReport).WithMany(r => r.Equipment).HasForeignKey(x => x.ServiceReportId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // O4 — TIMESHEET / TIMESHEET_ENTRY
        modelBuilder.Entity<Timesheet>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.EmployeeId, t.WeekStartDate });
            e.Property(t => t.TotalHours).HasColumnType("decimal(9,2)");
            e.Property(t => t.OvertimeHours).HasColumnType("decimal(9,2)");
            e.HasMany(t => t.Entries).WithOne(x => x.Timesheet).HasForeignKey(x => x.TimesheetId);
            e.HasQueryFilter(t => !t.IsDeleted);
        });
        modelBuilder.Entity<TimesheetEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TimesheetId);
            e.HasIndex(x => x.ProjectId);
            e.Property(x => x.Hours).HasColumnType("decimal(9,2)");
            e.Property(x => x.OvertimeHours).HasColumnType("decimal(9,2)");
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // O6 — Calibration: reference-standard register, immutable certificate, audit log
        modelBuilder.Entity<ReferenceStandard>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AssetId);
            e.HasIndex(x => x.NextDueDate);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
        modelBuilder.Entity<CalibrationCertificate>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.LabWorkOrderId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
        modelBuilder.Entity<CalibrationAuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LabWorkOrderId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
        modelBuilder.Entity<OperationsAuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Entity, x.EntityId });
        });

        // O7 — VARIATION_ORDER / VARIATION_ORDER_LINE
        modelBuilder.Entity<VariationOrder>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => v.ProjectId);
            e.HasIndex(v => v.Number).IsUnique();
            e.Property(v => v.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(v => v.VatRate).HasColumnType("decimal(6,4)");
            e.Property(v => v.VatAmount).HasColumnType("decimal(18,2)");
            e.Property(v => v.TotalAmount).HasColumnType("decimal(18,2)");
            e.HasOne(v => v.Project).WithMany().HasForeignKey(v => v.ProjectId);
            e.HasMany(v => v.Lines).WithOne(l => l.VariationOrder).HasForeignKey(l => l.VariationOrderId);
            e.HasQueryFilter(v => !v.IsDeleted);
        });
        modelBuilder.Entity<VariationOrderLine>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.VariationOrderId);
            e.Property(l => l.Quantity).HasColumnType("decimal(12,2)");
            e.Property(l => l.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(l => l.Amount).HasColumnType("decimal(18,2)");
            e.HasQueryFilter(l => !l.IsDeleted);
        });

        // O9 — Handover, program overrun, negligence
        modelBuilder.Entity<ProjectHandover>(e =>
        {
            e.HasKey(h => h.Id);
            e.HasIndex(h => h.ProjectId);
            e.HasIndex(h => h.HandoverNumber).IsUnique();
            e.HasOne(h => h.Project).WithMany().HasForeignKey(h => h.ProjectId);
            e.HasMany(h => h.Signatures).WithOne(s => s.Handover).HasForeignKey(s => s.HandoverId);
            e.HasQueryFilter(h => !h.IsDeleted);
        });
        modelBuilder.Entity<ProjectHandoverSignature>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.HandoverId);
            e.HasQueryFilter(s => !s.IsDeleted);
        });
        modelBuilder.Entity<ProgramOverrunNotice>(e =>
        {
            e.HasKey(n => n.Id);
            e.HasIndex(n => n.ProjectId);
            e.HasQueryFilter(n => !n.IsDeleted);
        });
        modelBuilder.Entity<NegligenceIncident>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.EmployeeId);
            e.HasIndex(i => i.IncidentNumber).IsUnique();
            e.HasMany(i => i.Responses).WithOne(r => r.Incident).HasForeignKey(r => r.IncidentId);
            e.HasQueryFilter(i => !i.IsDeleted);
        });
        modelBuilder.Entity<NegligenceResponse>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.IncidentId);
            e.Property(r => r.PayrollDeductionAmount).HasColumnType("decimal(18,2)");
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // O2 — PROJECT_ALERT_LOG
        modelBuilder.Entity<ProjectAlertLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.ProjectId);
            e.Property(a => a.SpentAmount).HasColumnType("decimal(18,2)");
            e.Property(a => a.CommittedAmount).HasColumnType("decimal(18,2)");
            e.Property(a => a.BudgetAmount).HasColumnType("decimal(18,2)");
            e.Property(a => a.BurnPct).HasColumnType("decimal(6,2)");
            e.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId);
            e.HasQueryFilter(a => !a.IsDeleted);
        });

        modelBuilder.Entity<ProjectTask>(e =>
        {
            e.HasKey(t => t.Id);
            e.ToTable("ProjectTasks");
            e.HasQueryFilter(t => !t.IsDeleted);
        });

        modelBuilder.Entity<Assignment>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasMany(a => a.Technicians).WithOne(t => t.Assignment).HasForeignKey(t => t.AssignmentId);
            e.HasMany(a => a.CheckIns).WithOne(c => c.Assignment).HasForeignKey(c => c.AssignmentId);
            e.HasMany(a => a.Photos).WithOne(p => p.Assignment).HasForeignKey(p => p.AssignmentId);
            e.HasMany(a => a.ServiceReports).WithOne(s => s.Assignment).HasForeignKey(s => s.AssignmentId);
            e.HasMany(a => a.Requisitions).WithOne(r => r.Assignment).HasForeignKey(r => r.AssignmentId);
            e.HasMany(a => a.Claims).WithOne(c => c.Assignment).HasForeignKey(c => c.AssignmentId);
            e.HasMany(a => a.PettyCashForms).WithOne(p => p.Assignment).HasForeignKey(p => p.AssignmentId);
            e.HasMany(a => a.PerDiemForms).WithOne(p => p.Assignment).HasForeignKey(p => p.AssignmentId);
            e.HasMany(a => a.AdvanceReturnForms).WithOne(a2 => a2.Assignment).HasForeignKey(a2 => a2.AssignmentId);
            e.HasMany(a => a.Refunds).WithOne(r => r.Assignment).HasForeignKey(r => r.AssignmentId);
            e.HasOne(a => a.LabWorkOrder).WithOne(l => l.Assignment).HasForeignKey<LabWorkOrder>(l => l.AssignmentId);
            e.HasQueryFilter(a => !a.IsDeleted);
        });

        modelBuilder.Entity<LabWorkOrder>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasOne(l => l.DataSheet).WithOne(d => d.LabWorkOrder).HasForeignKey<LabDataSheet>(d => d.LabWorkOrderId);
            e.HasQueryFilter(l => !l.IsDeleted);
        });

        modelBuilder.Entity<LabDataSheet>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasQueryFilter(d => !d.IsDeleted);
        });

        modelBuilder.Entity<PreDeploymentChecklist>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasOne(p => p.Assignment).WithMany().HasForeignKey(p => p.AssignmentId);
            e.HasQueryFilter(p => !p.IsDeleted);
        });

        modelBuilder.Entity<NonConformanceReport>(e =>
        {
            e.HasKey(n => n.Id);
            e.HasOne(n => n.Assignment).WithMany().HasForeignKey(n => n.AssignmentId);
            e.HasQueryFilter(n => !n.IsDeleted);
        });

        // O5 — Service Request domain
        modelBuilder.Entity<ServiceRequest>(e =>
        {
            e.HasKey(s => s.Id);
            // Unique, not just indexed: ReferenceNumber is minted by counting existing rows
            // (ServiceRequestService.GenerateReferenceNumberAsync) — this constraint is the actual
            // guard against two concurrent creates both computing the same next number.
            e.HasIndex(s => s.ReferenceNumber).IsUnique();
            e.HasMany(s => s.Instruments).WithOne(i => i.ServiceRequest).HasForeignKey(i => i.ServiceRequestId);
            e.HasOne(s => s.Quotation).WithOne(q => q.ServiceRequest).HasForeignKey<Quotation>(q => q.ServiceRequestId);
            e.HasQueryFilter(s => !s.IsDeleted);
        });

        modelBuilder.Entity<ServiceRequestInstrument>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasQueryFilter(i => !i.IsDeleted);
        });

        modelBuilder.Entity<Quotation>(e =>
        {
            e.HasKey(q => q.Id);
            // Unique for the same reason as ServiceRequest.ReferenceNumber above.
            e.HasIndex(q => q.QuotationNumber).IsUnique();
            e.Property(q => q.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(q => q.VatRate).HasColumnType("decimal(6,4)");
            e.Property(q => q.VatAmount).HasColumnType("decimal(18,2)");
            e.Property(q => q.TotalAmount).HasColumnType("decimal(18,2)");
            e.HasQueryFilter(q => !q.IsDeleted);
        });

        modelBuilder.Entity<RiskEntry>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Assignment).WithMany().HasForeignKey(r => r.AssignmentId);
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        modelBuilder.Entity<CustomerFeedback>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasOne(c => c.Assignment).WithMany().HasForeignKey(c => c.AssignmentId);
            e.HasQueryFilter(c => !c.IsDeleted);
        });

        modelBuilder.Entity<AdvanceReturnForm>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasMany(a => a.LineItems).WithOne(li => li.Form).HasForeignKey(li => li.AdvanceReturnFormId);
            e.HasQueryFilter(a => !a.IsDeleted);
        });

        modelBuilder.Entity<Requisition>(e =>
        {
            e.Property(r => r.Amount).HasColumnType("decimal(18,2)");
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        modelBuilder.Entity<Claim>(e =>
        {
            e.Property(c => c.Amount).HasColumnType("decimal(18,2)");
            e.HasQueryFilter(c => !c.IsDeleted);
        });

        modelBuilder.Entity<PettyCashAdvanceForm>(e =>
        {
            e.Property(p => p.Sum).HasColumnType("decimal(18,2)");
            e.HasQueryFilter(p => !p.IsDeleted);
        });

        modelBuilder.Entity<PerDiemReturnForm>(e =>
        {
            e.Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(p => p.FaresOrCarExpense).HasColumnType("decimal(18,2)");
            e.Property(p => p.Mileage).HasColumnType("decimal(18,2)");
            e.Property(p => p.Meals).HasColumnType("decimal(18,2)");
            e.Property(p => p.Medical).HasColumnType("decimal(18,2)");
            e.Property(p => p.Incidentals).HasColumnType("decimal(18,2)");
            e.HasQueryFilter(p => !p.IsDeleted);
        });

        modelBuilder.Entity<AdvanceReturnLineItem>(e =>
        {
            e.Property(a => a.Amount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Refund>(e =>
        {
            e.Property(r => r.Amount).HasColumnType("decimal(18,2)");
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        modelBuilder.Entity<BudgetLine>(e =>
        {
            e.Property(b => b.PlannedAmount).HasColumnType("decimal(18,2)");
            e.Property(b => b.ActualAmount).HasColumnType("decimal(18,2)");
            e.Property(b => b.QuotedAmount).HasColumnType("decimal(18,2)");
            e.Property(b => b.Quantity).HasColumnType("decimal(18,4)");
            e.Property(b => b.UnitCostRate).HasColumnType("decimal(18,4)");
            // A line survives its version being deleted; losing the costing detail is worse than an
            // orphaned line, which still reports against the project.
            e.HasOne(b => b.BudgetVersion).WithMany(v => v.Lines)
             .HasForeignKey(b => b.BudgetVersionId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(b => b.ContractRate).WithMany()
             .HasForeignKey(b => b.ContractRateId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(b => !b.IsDeleted);
        });

        modelBuilder.Entity<BudgetVersion>(e =>
        {
            e.Property(v => v.TotalPlanned).HasColumnType("decimal(18,2)");
            e.Property(v => v.TotalQuoted).HasColumnType("decimal(18,2)");
            e.HasOne(v => v.Project).WithMany(p => p.BudgetVersions)
             .HasForeignKey(v => v.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(v => new { v.ProjectId, v.VersionNo }).IsUnique();
            e.HasQueryFilter(v => !v.IsDeleted);
        });

        modelBuilder.Entity<ContractRate>(e =>
        {
            e.Property(r => r.ClientRate).HasColumnType("decimal(18,4)");
            e.Property(r => r.CostRate).HasColumnType("decimal(18,4)");
            e.HasOne(r => r.Project).WithMany(p => p.ContractRates)
             .HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // Dependencies deliberately carry no navigation to the linked milestone/task: two FKs into
        // the same table would need two nav properties and EF would try to cascade both, which can
        // delete a whole chain when one end is removed. The service resolves ids explicitly.
        modelBuilder.Entity<MilestoneDependency>(e =>
        {
            e.HasOne(d => d.Project).WithMany()
             .HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => new { d.PredecessorMilestoneId, d.SuccessorMilestoneId }).IsUnique();
            e.HasQueryFilter(d => !d.IsDeleted);
        });

        modelBuilder.Entity<TaskDependency>(e =>
        {
            e.HasOne(d => d.Project).WithMany()
             .HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => new { d.PredecessorTaskId, d.SuccessorTaskId }).IsUnique();
            e.HasQueryFilter(d => !d.IsDeleted);
        });

        modelBuilder.Entity<ProjectTask>(e =>
        {
            e.Property(t => t.EstimatedHours).HasColumnType("decimal(18,2)");
            e.HasOne(t => t.ParentTask).WithMany(t => t.Subtasks)
             .HasForeignKey(t => t.ParentTaskId).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.Property(p => p.BaselineBudget).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<CostEntry>(e =>
        {
            e.Property(c => c.Amount).HasColumnType("decimal(18,2)");
            e.HasOne(c => c.BudgetLine).WithMany(b => b.CostEntries).HasForeignKey(c => c.BudgetLineId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Milestone).WithMany(m => m.CostEntries).HasForeignKey(c => c.MilestoneId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(c => !c.IsDeleted);
        });

        modelBuilder.Entity<PerformanceMetrics>(e =>
        {
            e.Property(p => p.TotalRequisitionAmount).HasColumnType("decimal(18,2)");
            e.HasQueryFilter(p => !p.IsDeleted);
        });

        modelBuilder.Entity<DailySummary>(e =>
        {
            e.Property(d => d.ExpensesIncurred).HasColumnType("decimal(18,2)");
            e.HasOne(d => d.Assignment).WithMany().HasForeignKey(d => d.AssignmentId);
            e.HasQueryFilter(d => !d.IsDeleted);
        });

        modelBuilder.Entity<FieldVehicle>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.CurrentOdometer).HasColumnType("decimal(18,2)");
            e.HasMany(v => v.Dispatches).WithOne(d => d.FieldVehicle).HasForeignKey(d => d.FieldVehicleId);
            e.HasQueryFilter(v => !v.IsDeleted);
        });

        modelBuilder.Entity<VehicleDispatch>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.DepartureOdometer).HasColumnType("decimal(18,2)");
            e.Property(d => d.ReturnOdometer).HasColumnType("decimal(18,2)");
            e.HasOne(d => d.Assignment).WithMany(a => a.VehicleDispatches).HasForeignKey(d => d.AssignmentId);
            e.HasMany(d => d.FuelLogs).WithOne(f => f.Dispatch).HasForeignKey(f => f.DispatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(d => !d.IsDeleted);
        });

        modelBuilder.Entity<FuelLog>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.AmountLitres).HasColumnType("decimal(18,2)");
            e.Property(f => f.CostKes).HasColumnType("decimal(18,2)");
            e.HasQueryFilter(f => !f.IsDeleted);
        });

        // Global soft-delete filters for simple entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (typeof(Core.Entities.BaseEntity).IsAssignableFrom(clrType))
            {
                // Filters already applied per-entity above where needed
            }
        }
    }
}
