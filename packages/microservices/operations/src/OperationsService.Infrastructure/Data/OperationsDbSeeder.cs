using Microsoft.EntityFrameworkCore;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;

namespace OperationsService.Infrastructure.Data;

public static class OperationsDbSeeder
{
    public static async Task SeedAsync(OperationsDbContext db)
    {
        if (await db.Projects.AnyAsync()) return;

        const string adminId = "seed-admin";
        const string adminName = "System Admin";

        // ── Projects ──────────────────────────────────────────────────────────

        var proj1 = new Project
        {
            Name = "Nairobi Water Treatment Upgrade",
            ClientName = "Nairobi City Water",
            ClientReference = "NCW-2026-001",
            TenderReference = "TENDER/2026/003",
            ScopeSummary = "Supply, install and commission advanced filtration units across 3 treatment sites.",
            Notes = "Critical infrastructure project. Site access requires NCC permit.",
            Type = ProjectType.Construction,
            Status = ProjectStatus.Active,
            RiskLevel = RiskLevel.High,
            DepartmentId = "",
            ContractValue = 12_500_000,
            PlannedBudget = 11_200_000,
            ActualCost = 4_300_000,
            StartDate = new DateTime(2026, 1, 15),
            ExpectedEndDate = new DateTime(2026, 9, 30),
            ProjectManagerId = adminId,
            CreatedBy = adminId,
        };

        var proj2 = new Project
        {
            Name = "ICT Infrastructure Rollout – Mombasa Branch",
            ClientName = "Safaricom PLC",
            ClientReference = "SAF-ICT-2026",
            ScopeSummary = "Structured cabling, server room setup and network deployment for new regional branch.",
            Type = ProjectType.ICT,
            Status = ProjectStatus.Planning,
            RiskLevel = RiskLevel.Medium,
            DepartmentId = "",
            ContractValue = 3_800_000,
            PlannedBudget = 3_500_000,
            ActualCost = 0,
            StartDate = new DateTime(2026, 4, 1),
            ExpectedEndDate = new DateTime(2026, 7, 15),
            ProjectManagerId = adminId,
            CreatedBy = adminId,
        };

        var proj3 = new Project
        {
            Name = "Fire Safety Audit & Equipment Calibration",
            ClientName = "Kenya Ports Authority",
            ClientReference = "KPA-HSE-001",
            ScopeSummary = "Annual calibration of fire detection and suppression equipment across Port facilities.",
            Type = ProjectType.Calibration,
            Status = ProjectStatus.Draft,
            RiskLevel = RiskLevel.Low,
            DepartmentId = "",
            ContractValue = 850_000,
            PlannedBudget = 700_000,
            ActualCost = 0,
            StartDate = new DateTime(2026, 5, 1),
            ExpectedEndDate = new DateTime(2026, 6, 30),
            ProjectManagerId = adminId,
            CreatedBy = adminId,
        };

        await db.Projects.AddRangeAsync(proj1, proj2, proj3);
        await db.SaveChangesAsync();

        // ── Milestones & tasks for proj1 ──────────────────────────────────────

        var ms1 = new Milestone
        {
            ProjectId = proj1.Id,
            Title = "Site Survey & Engineering Design",
            Description = "Complete topographic surveys and produce detailed engineering drawings.",
            Status = MilestoneStatus.Completed,
            Order = 1,
            DueDate = new DateTime(2026, 2, 28),
            CreatedBy = adminId,
        };
        var ms2 = new Milestone
        {
            ProjectId = proj1.Id,
            Title = "Procurement & Equipment Delivery",
            Description = "Source and procure filtration units, piping and control panels.",
            Status = MilestoneStatus.InProgress,
            Order = 2,
            DueDate = new DateTime(2026, 4, 30),
            CreatedBy = adminId,
        };
        var ms3 = new Milestone
        {
            ProjectId = proj1.Id,
            Title = "Installation & Commissioning",
            Description = "Install and commission all filtration units at all three sites.",
            Status = MilestoneStatus.NotStarted,
            Order = 3,
            DueDate = new DateTime(2026, 8, 31),
            CreatedBy = adminId,
        };

        await db.Milestones.AddRangeAsync(ms1, ms2, ms3);
        await db.SaveChangesAsync();

        await db.ProjectTasks.AddRangeAsync(
            new ProjectTask { MilestoneId = ms1.Id, ProjectId = proj1.Id, Title = "Topographic survey – Site A", Status = Core.Enums.TaskStatus.Done, AssignedToUserId = adminId, CreatedBy = adminId },
            new ProjectTask { MilestoneId = ms1.Id, ProjectId = proj1.Id, Title = "Engineering drawings review & sign-off", Status = Core.Enums.TaskStatus.Done, AssignedToUserId = adminId, CreatedBy = adminId },
            new ProjectTask { MilestoneId = ms2.Id, ProjectId = proj1.Id, Title = "Issue LPOs for filtration units", Status = Core.Enums.TaskStatus.InProgress, AssignedToUserId = adminId, CreatedBy = adminId },
            new ProjectTask { MilestoneId = ms2.Id, ProjectId = proj1.Id, Title = "Confirm delivery schedule with supplier", Status = Core.Enums.TaskStatus.NotStarted, AssignedToUserId = adminId, CreatedBy = adminId },
            new ProjectTask { MilestoneId = ms3.Id, ProjectId = proj1.Id, Title = "Install units at Site A", Status = Core.Enums.TaskStatus.NotStarted, AssignedToUserId = adminId, CreatedBy = adminId },
            new ProjectTask { MilestoneId = ms3.Id, ProjectId = proj1.Id, Title = "Commissioning tests and sign-off", Status = Core.Enums.TaskStatus.NotStarted, AssignedToUserId = adminId, CreatedBy = adminId }
        );

        await db.BudgetLines.AddRangeAsync(
            new BudgetLine { ProjectId = proj1.Id, Category = BudgetCategory.Labour, Description = "Engineering team – 9 months", PlannedAmount = 4_200_000, ActualAmount = 1_400_000, CreatedBy = adminId },
            new BudgetLine { ProjectId = proj1.Id, Category = BudgetCategory.Materials, Description = "Filtration units & piping", PlannedAmount = 5_500_000, ActualAmount = 2_600_000, CreatedBy = adminId },
            new BudgetLine { ProjectId = proj1.Id, Category = BudgetCategory.Fleet, Description = "Site transport & logistics", PlannedAmount = 800_000, ActualAmount = 220_000, CreatedBy = adminId },
            new BudgetLine { ProjectId = proj1.Id, Category = BudgetCategory.Other, Description = "Permits & contingency", PlannedAmount = 700_000, ActualAmount = 80_000, CreatedBy = adminId }
        );

        // ── Milestone for proj2 ───────────────────────────────────────────────

        var ms4 = new Milestone
        {
            ProjectId = proj2.Id,
            Title = "Requirements Gathering",
            Description = "Document network requirements and server room specifications.",
            Status = MilestoneStatus.InProgress,
            Order = 1,
            DueDate = new DateTime(2026, 4, 30),
            CreatedBy = adminId,
        };
        await db.Milestones.AddAsync(ms4);
        await db.SaveChangesAsync();

        await db.ProjectTasks.AddRangeAsync(
            new ProjectTask { MilestoneId = ms4.Id, ProjectId = proj2.Id, Title = "Site visit – Mombasa branch", Status = Core.Enums.TaskStatus.InProgress, AssignedToUserId = adminId, CreatedBy = adminId },
            new ProjectTask { MilestoneId = ms4.Id, ProjectId = proj2.Id, Title = "Draft network topology diagram", Status = Core.Enums.TaskStatus.NotStarted, AssignedToUserId = adminId, CreatedBy = adminId }
        );

        // ── Assignments ───────────────────────────────────────────────────────

        var asgn1 = new Assignment
        {
            Title = "Emergency Generator Repair – Eldoret",
            Description = "Client reports generator not starting. Attend site and diagnose fault.",
            SourceType = AssignmentSourceType.Standalone,
            DepartmentId = "",
            DepartmentType = DepartmentType.Technical,
            ManagerId = adminId,
            Status = AssignmentStatus.InProgress,
            Priority = AssignmentPriority.High,
            NatureOfVisit = NatureOfVisit.Repair,
            LocationName = "Eldoret Industrial Area",
            LocationAddress = "Plot 45, Industrial Area, Eldoret",
            LocationLatitude = 0.5199,
            LocationLongitude = 35.2697,
            StartedAt = DateTime.UtcNow.AddDays(-2),
            AcceptedAt = DateTime.UtcNow.AddDays(-2),
            CreatedBy = adminId,
        };
        asgn1.Technicians.Add(new AssignedTechnician { UserId = adminId, UserName = adminName, AssignedAt = DateTime.UtcNow.AddDays(-2), CreatedBy = adminId });

        var asgn2 = new Assignment
        {
            Title = "Network Equipment Installation – Kisumu Port",
            Description = "Install and configure managed switches and access points in warehouse block B.",
            SourceType = AssignmentSourceType.Standalone,
            DepartmentId = "",
            DepartmentType = DepartmentType.ICT,
            ManagerId = adminId,
            Status = AssignmentStatus.Accepted,
            Priority = AssignmentPriority.Normal,
            NatureOfVisit = NatureOfVisit.Installation,
            LocationName = "Kisumu Port – Block B",
            LocationAddress = "Kisumu Port Authority, Kisumu",
            AcceptedAt = DateTime.UtcNow.AddDays(-1),
            CreatedBy = adminId,
        };
        asgn2.Technicians.Add(new AssignedTechnician { UserId = adminId, UserName = adminName, AssignedAt = DateTime.UtcNow.AddDays(-1), CreatedBy = adminId });

        var asgn3 = new Assignment
        {
            Title = "Pressure Gauge Calibration – Mombasa Refinery",
            Description = "Quarterly calibration of all process pressure gauges in Unit 3.",
            SourceType = AssignmentSourceType.Standalone,
            DepartmentId = "",
            DepartmentType = DepartmentType.Technical,
            ManagerId = adminId,
            Status = AssignmentStatus.Pending,
            Priority = AssignmentPriority.Normal,
            NatureOfVisit = NatureOfVisit.Maintenance,
            LocationName = "Kenya Petroleum Refineries",
            LocationAddress = "Changamwe, Mombasa",
            CreatedBy = adminId,
        };

        await db.Assignments.AddRangeAsync(asgn1, asgn2, asgn3);
        await db.SaveChangesAsync();

        // ── Check-in + daily summary for asgn1 ───────────────────────────────

        await db.CheckIns.AddAsync(new CheckIn
        {
            AssignmentId = asgn1.Id,
            UserId = adminId,
            Latitude = 0.5199,
            Longitude = 35.2697,
            CheckInTime = DateTime.UtcNow.AddDays(-2).AddHours(8),
            CheckOutTime = DateTime.UtcNow.AddDays(-2).AddHours(17),
            Status = CheckInStatus.CheckedOut,
            Notes = "Arrived at site. Diagnosed starter motor fault.",
            CreatedBy = adminId,
        });

        await db.DailySummaries.AddAsync(new DailySummary
        {
            AssignmentId = asgn1.Id,
            SubmittedByUserId = adminId,
            SubmittedByName = adminName,
            Date = DateTime.UtcNow.AddDays(-2).Date,
            Summary = "Diagnosed starter motor fault. Removed and inspected – armature winding shorted. Ordered replacement part from Nairobi.",
            HoursWorked = 9,
            Challenges = "Replacement part not in local stock. Delivery expected in 2 days.",
            NextDayPlan = "Await part delivery, then reinstall and test generator under load.",
            ExpensesIncurred = 3_500,
            CreatedBy = adminId,
        });

        // ── Financial records for asgn1 ───────────────────────────────────────

        await db.Requisitions.AddAsync(new Requisition
        {
            AssignmentId = asgn1.Id,
            TechnicianId = adminId,
            Type = RequisitionType.CashAdvance,
            Description = "Cash advance for parts purchase and site expenses",
            Amount = 35_000,
            Justification = "Need to purchase starter motor and consumables locally in Eldoret.",
            ItemsList = "[{\"description\":\"Starter motor\",\"qty\":1,\"unitPrice\":28000},{\"description\":\"Lubricant & consumables\",\"qty\":1,\"unitPrice\":4000},{\"description\":\"Transport Nairobi–Eldoret\",\"qty\":1,\"unitPrice\":3000}]",
            Status = RequisitionStatus.TmApproved,
            TmReviewedBy = adminId,
            TmReviewedAt = DateTime.UtcNow.AddDays(-1),
            TmComments = "Approved. Please proceed with procurement.",
            CreatedBy = adminId,
        });

        await db.Claims.AddAsync(new Claim
        {
            AssignmentId = asgn1.Id,
            TechnicianId = adminId,
            TechnicianName = adminName,
            Description = "Meal allowance – 2 days on site in Eldoret",
            Amount = 4_000,
            Justification = "Per diem meals during site stay",
            Status = ClaimStatus.Pending,
            CreatedBy = adminId,
        });

        await db.PettyCashForms.AddAsync(new PettyCashAdvanceForm
        {
            AssignmentId = asgn1.Id,
            PreparedBy = adminId,
            Sum = 5_000,
            Description = "Petty cash for miscellaneous site expenses (parking, printing, hardware)",
            Status = PettyCashStatus.Approved,
            ApprovedBy = adminId,
            ApprovedAt = DateTime.UtcNow.AddDays(-1),
            ApprovalComments = "Approved.",
            CreatedBy = adminId,
        });

        await db.SaveChangesAsync();
    }
}
