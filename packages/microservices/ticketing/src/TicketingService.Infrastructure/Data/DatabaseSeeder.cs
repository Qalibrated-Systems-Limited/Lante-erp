using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;

namespace TicketingService.Infrastructure.Data;

public static class DatabaseSeeder
{
    // Fixed category IDs so ticket seed data can reference them reliably
    private const string CatIT          = "cat-it-helpdesk";
    private const string CatTechnical   = "cat-technical-service";
    private const string CatSafety      = "cat-safety-incident";
    private const string CatFleet       = "cat-fleet-maintenance";
    private const string CatCRM         = "cat-customer-complaint";
    private const string CatGeneral     = "cat-internal-request";
    private const string CatQuality     = "cat-ancr-issue";
    private const string CatCalibNawi   = "cat-calibration-nawi";
    private const string CatCalibMass   = "cat-calibration-mass";

    // Department UUIDs — must match the user-service Departments table
    private const string DeptIT        = "99b951a3-021f-4fd2-affa-ed4fac0b0fb3";
    private const string DeptTechnical = "c7f54ea6-01ce-46a2-85f3-649751dcd9f0";
    private const string DeptSafety    = "30b71369-06c1-4d20-9b08-63303ad635a9";
    private const string DeptFleet     = "4eaa2789-c03e-4ebb-be21-c9f9a705bb85";
    private const string DeptCRM       = "5c96a27a-dd7d-4341-83a7-c3149ee322c6";
    private const string DeptGeneral   = "6b7fd54b-8055-4cad-8022-ff57336536aa";
    private const string DeptQuality   = "bce4193d-37cc-43a1-b156-ada65886bf0a";
    // Calibration categories route to the Technical department
    private const string DeptCalib     = "c7f54ea6-01ce-46a2-85f3-649751dcd9f0";

    public static async Task SeedAsync(IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
        await SeedDataAsync(context);
    }

    public static async Task SeedAsync(TicketingDbContext context)
        => await SeedDataAsync(context);

    private static async Task SeedDataAsync(TicketingDbContext context)
    {
        await SeedCategoriesAsync(context);
        await SeedTagsAsync(context);
        await SeedMacrosAsync(context);
        await SeedWorkflowRulesAsync(context);
        await SeedTicketsAsync(context);
    }

    private static async Task SeedCategoriesAsync(TicketingDbContext context)
    {
        var now = DateTime.UtcNow;

        // Seed base categories only if none exist yet
        if (!await context.TicketCategories.AnyAsync())
        {
            var categories = new[]
            {
                new TicketCategory { Id = CatIT,        Name = "IT Help Desk",       Description = "IT support and infrastructure issues",               IsActive = true, DepartmentId = DeptIT,        DefaultPriority = TicketPriority.Medium,   RequiresEvidence = false, AutoCreateANCR = false, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
                new TicketCategory { Id = CatTechnical, Name = "Technical Service",  Description = "Field technical service and maintenance",             IsActive = true, DepartmentId = DeptTechnical, DefaultPriority = TicketPriority.High,     RequiresEvidence = false, AutoCreateANCR = false, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
                new TicketCategory { Id = CatSafety,    Name = "Safety Incident",    Description = "HSE incidents, near-misses and hazard reports",       IsActive = true, DepartmentId = DeptSafety,    DefaultPriority = TicketPriority.Critical, RequiresEvidence = true,  AutoCreateANCR = true,  CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
                new TicketCategory { Id = CatFleet,     Name = "Fleet Maintenance",  Description = "Vehicle breakdowns and scheduled maintenance",        IsActive = true, DepartmentId = DeptFleet,     DefaultPriority = TicketPriority.Medium,   RequiresEvidence = false, AutoCreateANCR = false, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
                new TicketCategory { Id = CatCRM,       Name = "Customer Complaint", Description = "External client complaints and escalations",          IsActive = true, DepartmentId = DeptCRM,       DefaultPriority = TicketPriority.High,     RequiresEvidence = false, AutoCreateANCR = false, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
                new TicketCategory { Id = CatGeneral,   Name = "Internal Request",   Description = "General administrative and inter-department requests",IsActive = true, DepartmentId = DeptGeneral,   DefaultPriority = TicketPriority.Low,     RequiresEvidence = false, AutoCreateANCR = false, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
                new TicketCategory { Id = CatQuality,   Name = "ANCR Issue",         Description = "Action Non-Conformance Reports and quality issues",   IsActive = true, DepartmentId = DeptQuality,   DefaultPriority = TicketPriority.High,    RequiresEvidence = false, AutoCreateANCR = false, CreatedAt = now, UpdatedAt = now, CreatedBy = "system" },
            };
            await context.TicketCategories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }

        // Calibration categories — idempotent per ID
        if (!await context.TicketCategories.AnyAsync(c => c.Id == CatCalibNawi))
        {
            await context.TicketCategories.AddAsync(new TicketCategory
            {
                Id = CatCalibNawi, Name = "Calibration - NAWI",
                Description    = "Calibration requests for Non-Automatic Weighing Instruments (balances, scales)",
                IsActive       = true, DepartmentId = DeptCalib,
                DefaultPriority = TicketPriority.Medium,
                RequiresEvidence = false, AutoCreateANCR = false,
                CreatedAt = now, UpdatedAt = now, CreatedBy = "system",
            });
        }
        if (!await context.TicketCategories.AnyAsync(c => c.Id == CatCalibMass))
        {
            await context.TicketCategories.AddAsync(new TicketCategory
            {
                Id = CatCalibMass, Name = "Calibration - MASS",
                Description    = "Calibration requests for mass standards and weights",
                IsActive       = true, DepartmentId = DeptCalib,
                DefaultPriority = TicketPriority.Medium,
                RequiresEvidence = false, AutoCreateANCR = false,
                CreatedAt = now, UpdatedAt = now, CreatedBy = "system",
            });
        }
        await context.SaveChangesAsync();

        // SLA policies per category — only if none already exist for a given category
        var slaConfigs = new[]
        {
            new { CatId = CatIT,         RespH = 4,  ResH = 24  },
            new { CatId = CatTechnical,  RespH = 2,  ResH = 8   },
            new { CatId = CatSafety,     RespH = 1,  ResH = 4   },
            new { CatId = CatFleet,      RespH = 4,  ResH = 48  },
            new { CatId = CatCRM,        RespH = 2,  ResH = 24  },
            new { CatId = CatGeneral,    RespH = 8,  ResH = 72  },
            new { CatId = CatQuality,    RespH = 2,  ResH = 24  },
            new { CatId = CatCalibNawi,  RespH = 4,  ResH = 48  },
            new { CatId = CatCalibMass,  RespH = 4,  ResH = 48  },
        };

        foreach (var cfg in slaConfigs)
        {
            if (await context.SLAPolicies.AnyAsync(p => p.CategoryId == cfg.CatId)) continue;
            await context.SLAPolicies.AddRangeAsync(new[]
            {
                new SLAPolicy { Id = Guid.NewGuid().ToString(), CategoryId = cfg.CatId, Priority = TicketPriority.Low,      ResponseTimeHours = cfg.RespH * 2,              ResolutionTimeHours = cfg.ResH * 2,              CreatedAt = now, UpdatedAt = now },
                new SLAPolicy { Id = Guid.NewGuid().ToString(), CategoryId = cfg.CatId, Priority = TicketPriority.Medium,   ResponseTimeHours = cfg.RespH,                  ResolutionTimeHours = cfg.ResH,                  CreatedAt = now, UpdatedAt = now },
                new SLAPolicy { Id = Guid.NewGuid().ToString(), CategoryId = cfg.CatId, Priority = TicketPriority.High,     ResponseTimeHours = Math.Max(1, cfg.RespH / 2), ResolutionTimeHours = Math.Max(2, cfg.ResH / 2), CreatedAt = now, UpdatedAt = now },
                new SLAPolicy { Id = Guid.NewGuid().ToString(), CategoryId = cfg.CatId, Priority = TicketPriority.Critical, ResponseTimeHours = 1,                          ResolutionTimeHours = Math.Max(2, cfg.ResH / 4), CreatedAt = now, UpdatedAt = now },
            });
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedTagsAsync(TicketingDbContext context)
    {
        if (await context.Tags.AnyAsync()) return;
        var now = DateTime.UtcNow;
        var tags = new[]
        {
            new Tag { Id = "tag-urgent",      Name = "Urgent",         Color = "#ef4444", CreatedAt = now, UpdatedAt = now },
            new Tag { Id = "tag-client",      Name = "Client-Facing",  Color = "#f59e0b", CreatedAt = now, UpdatedAt = now },
            new Tag { Id = "tag-safety",      Name = "Safety",         Color = "#dc2626", CreatedAt = now, UpdatedAt = now },
            new Tag { Id = "tag-site",        Name = "On-Site",        Color = "#16a34a", CreatedAt = now, UpdatedAt = now },
            new Tag { Id = "tag-pending-doc", Name = "Pending Docs",   Color = "#7c3aed", CreatedAt = now, UpdatedAt = now },
            new Tag { Id = "tag-warranty",    Name = "Warranty",       Color = "#0284c7", CreatedAt = now, UpdatedAt = now },
            new Tag { Id = "tag-sla-breach",  Name = "SLA Breach",     Color = "#b91c1c", CreatedAt = now, UpdatedAt = now },
            new Tag { Id = "tag-follow-up",   Name = "Follow Up",      Color = "#d97706", CreatedAt = now, UpdatedAt = now },
        };
        await context.Tags.AddRangeAsync(tags);
        await context.SaveChangesAsync();
    }

    private static async Task SeedMacrosAsync(TicketingDbContext context)
    {
        if (await context.Macros.AnyAsync()) return;
        var now = DateTime.UtcNow;
        var macros = new[]
        {
            new Macro { Id = "macro-ack",       Name = "Acknowledge Receipt",      Description = "Standard acknowledgement for newly received tickets",          Content = "Thank you for reaching out. We have received your request and it has been assigned to our team. You can expect an update within our SLA window.",         IsGlobal = true, CreatedAt = now, UpdatedAt = now },
            new Macro { Id = "macro-more-info", Name = "Request More Information",  Description = "Ask the client for additional details needed to proceed",         Content = "Thank you for your submission. To help us resolve this efficiently, could you please provide:\n1. Date and time the issue first occurred\n2. Any error messages or screenshots\n3. Steps taken so far",                                                                                                                           IsGlobal = true, CreatedAt = now, UpdatedAt = now },
            new Macro { Id = "macro-resolved",  Name = "Resolution Confirmation",   Description = "Notify client that the issue has been resolved",                  Content = "We are pleased to inform you that your ticket has been resolved. Please do not hesitate to contact us if you experience any recurrence or have further questions.",                                                                                                                                                                  IsGlobal = true, CreatedAt = now, UpdatedAt = now },
            new Macro { Id = "macro-escalation",Name = "Escalation Notice",         Description = "Inform client that the ticket has been escalated",                Content = "Due to the nature and urgency of your request, this ticket has been escalated to a senior team member for immediate attention. You will receive an update shortly.",                                                                                                                                                                  IsGlobal = true, CreatedAt = now, UpdatedAt = now },
            new Macro { Id = "macro-on-hold",   Name = "Placed On Hold",            Description = "Inform client the ticket is pending an external dependency",      Content = "Your ticket has been placed on hold while we await a response from an external party. We will resume processing as soon as we receive the necessary information.",                                                                                                                                                                   IsGlobal = true, CreatedAt = now, UpdatedAt = now },
        };
        await context.Macros.AddRangeAsync(macros);
        await context.SaveChangesAsync();
    }

    private static async Task SeedWorkflowRulesAsync(TicketingDbContext context)
    {
        if (await context.WorkflowRules.AnyAsync()) return;
        var now = DateTime.UtcNow;
        var rules = new[]
        {
            new WorkflowRule { Id = "wf-safety-escalate", Name = "Auto-Escalate Safety Incidents", Description = "Escalate Critical safety tickets immediately on creation", TriggerEvent = WorkflowTriggerEvent.TicketCreated, ConditionsJson = """[{"field":"priority","op":"eq","value":"Critical"},{"field":"categoryId","op":"eq","value":"cat-safety-incident"}]""", ActionsJson = """[{"type":"escalate","level":"Supervisor"}]""", IsActive = true, RunOrder = 1, CreatedAt = now, UpdatedAt = now },
            new WorkflowRule { Id = "wf-portal-tag",      Name = "Tag Portal Submissions",          Description = "Add Client-Facing tag to all CRM-sourced tickets",          TriggerEvent = WorkflowTriggerEvent.TicketCreated, ConditionsJson = """[{"field":"source","op":"eq","value":"CRM"}]""",                                                                                                                                           ActionsJson = """[{"type":"addTag","tagId":"tag-client"}]""",    IsActive = true, RunOrder = 2, CreatedAt = now, UpdatedAt = now },
            new WorkflowRule { Id = "wf-sla-breach-tag",  Name = "Flag SLA Breaches",               Description = "Add SLA Breach tag when resolution deadline is missed",     TriggerEvent = WorkflowTriggerEvent.SLABreached,   ConditionsJson = """[]""",                                                                                                                                                                                 ActionsJson = """[{"type":"addTag","tagId":"tag-sla-breach"}]""", IsActive = true, RunOrder = 1, CreatedAt = now, UpdatedAt = now },
        };
        await context.WorkflowRules.AddRangeAsync(rules);
        await context.SaveChangesAsync();
    }

    private static async Task SeedTicketsAsync(TicketingDbContext context)
    {
        if (await context.Tickets.AnyAsync(t => t.Id == "seed-ticket-001")) return;

        var now = DateTime.UtcNow;

        var tickets = new List<Ticket>
        {
            // ── IT Help Desk ───────────────────────────────────────────────
            new Ticket
            {
                Id = "seed-ticket-001",
                Title = "VPN connection dropping intermittently for site team",
                Description = "Three engineers on the Kilimani site are experiencing VPN drops every 30-60 minutes, preventing access to the document management system. Issue started after the router firmware was updated on Monday.",
                CategoryId = CatIT,
                Priority = TicketPriority.High,
                Status = TicketStatus.InProgress,
                Source = TicketSource.Manual,
                DepartmentId = DeptIT,
                CreatedByUserId = "seed-user",
                AssignedToUserId = "seed-it-lead",
                ResponseDueAt  = now.AddHours(-6),
                ResolutionDueAt = now.AddHours(2),
                CreatedAt = now.AddDays(-1), UpdatedAt = now.AddHours(-4)
            },
            new Ticket
            {
                Id = "seed-ticket-002",
                Title = "Laptop screen flickering — Engineering workstation LT-042",
                Description = "The screen on the assigned engineering laptop flickers randomly. Workaround of plugging into external monitor confirmed. Laptop still under warranty.",
                CategoryId = CatIT,
                Priority = TicketPriority.Medium,
                Status = TicketStatus.Assigned,
                Source = TicketSource.Manual,
                DepartmentId = DeptIT,
                CreatedByUserId = "seed-user",
                AssignedToUserId = "seed-it-support",
                ResponseDueAt  = now.AddHours(2),
                ResolutionDueAt = now.AddDays(1),
                CreatedAt = now.AddHours(-5), UpdatedAt = now.AddHours(-3)
            },
            new Ticket
            {
                Id = "seed-ticket-003",
                Title = "ERP system login failure after password policy update",
                Description = "Multiple users reporting inability to log in after the new password policy was enforced. Error: 'Account locked after failed attempts'. Affects approximately 8 staff accounts.",
                CategoryId = CatIT,
                Priority = TicketPriority.Critical,
                Status = TicketStatus.Resolved,
                Source = TicketSource.Manual,
                DepartmentId = DeptIT,
                CreatedByUserId = "seed-user",
                AssignedToUserId = "seed-it-lead",
                ResolutionNotes = "Accounts unlocked and users guided to reset passwords via self-service portal. Password policy documentation updated.",
                ResponseDueAt  = now.AddDays(-3),
                ResolutionDueAt = now.AddDays(-3),
                ResolvedAt     = now.AddDays(-2),
                CreatedAt = now.AddDays(-4), UpdatedAt = now.AddDays(-2)
            },

            // ── Technical Service ──────────────────────────────────────────
            new Ticket
            {
                Id = "seed-ticket-004",
                Title = "Transformer oil level low — Substation 7 (KPLC Westlands)",
                Description = "On-site technician reports oil level in main transformer at substation 7 has dropped below minimum threshold. Possible slow leak from valve gasket. Requires immediate attention to prevent equipment damage.",
                CategoryId = CatTechnical,
                Priority = TicketPriority.High,
                Status = TicketStatus.InProgress,
                Source = TicketSource.Manual,
                DepartmentId = DeptTechnical,
                CreatedByUserId = "seed-tech-user",
                AssignedToUserId = "seed-tech-lead",
                IsEscalated = false,
                ResponseDueAt  = now.AddHours(-1),
                ResolutionDueAt = now.AddHours(6),
                CreatedAt = now.AddHours(-8), UpdatedAt = now.AddHours(-2)
            },
            new Ticket
            {
                Id = "seed-ticket-005",
                Title = "Protection relay mis-operation — Substation 12",
                Description = "Zone 2 protection relay tripped without fault present during routine load switching. False trip may indicate calibration drift or relay aging. Substation currently running on backup protection only.",
                CategoryId = CatTechnical,
                Priority = TicketPriority.Critical,
                Status = TicketStatus.Escalated,
                Source = TicketSource.Manual,
                DepartmentId = DeptTechnical,
                CreatedByUserId = "seed-tech-user",
                AssignedToUserId = "seed-tech-lead",
                IsEscalated = true,
                EscalationLevel = EscalationLevel.DepartmentHead,
                ResponseDueAt  = now.AddDays(-1),
                ResolutionDueAt = now.AddHours(4),
                CreatedAt = now.AddDays(-1), UpdatedAt = now.AddHours(-1)
            },
            new Ticket
            {
                Id = "seed-ticket-006",
                Title = "Scheduled calibration certificate renewal — KEBS pressure gauges",
                Description = "Annual renewal due for 24 pressure gauges at KEBS Nairobi laboratory. Current certificates expire in 14 days. Calibration booking already confirmed with lab.",
                CategoryId = CatTechnical,
                Priority = TicketPriority.Medium,
                Status = TicketStatus.New,
                Source = TicketSource.Scheduled,
                DepartmentId = DeptTechnical,
                CreatedByUserId = "seed-system",
                DueDate        = now.AddDays(14),
                ResponseDueAt  = now.AddDays(2),
                ResolutionDueAt = now.AddDays(12),
                CreatedAt = now.AddHours(-2), UpdatedAt = now.AddHours(-2)
            },

            // ── Safety Incident ────────────────────────────────────────────
            new Ticket
            {
                Id = "seed-ticket-007",
                Title = "Near-miss: unsecured scaffolding board on Kilimani site Level 3",
                Description = "A loose scaffolding board was observed on Level 3 of the Kilimani site. Board approximately 4m above the lower platform. Incident reported by site supervisor. Area cordoned off immediately. No injuries.",
                CategoryId = CatSafety,
                Priority = TicketPriority.Critical,
                Status = TicketStatus.InProgress,
                Source = TicketSource.SafetyReport,
                DepartmentId = DeptSafety,
                CreatedByUserId = "seed-safety-user",
                AssignedToUserId = "seed-safety-officer",
                RequiresEvidence = true,
                IsEscalated = false,
                ResponseDueAt  = now.AddHours(-2),
                ResolutionDueAt = now.AddHours(2),
                CreatedAt = now.AddHours(-4), UpdatedAt = now.AddHours(-1)
            },
            new Ticket
            {
                Id = "seed-ticket-008",
                Title = "Toolbox talk compliance gap — Night shift crew",
                Description = "HSE audit found that the night shift crew on the Mombasa project has not had toolbox talks documented for the past 3 weeks. Supervisor to provide corrective action plan.",
                CategoryId = CatSafety,
                Priority = TicketPriority.High,
                Status = TicketStatus.Pending,
                Source = TicketSource.Manual,
                DepartmentId = DeptSafety,
                CreatedByUserId = "seed-safety-user",
                AssignedToUserId = "seed-safety-officer",
                RequiresEvidence = false,
                ResponseDueAt  = now.AddHours(-12),
                ResolutionDueAt = now.AddDays(2),
                CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-1)
            },

            // ── Fleet ──────────────────────────────────────────────────────
            new Ticket
            {
                Id = "seed-ticket-009",
                Title = "Vehicle KAW 412G — Engine warning light on",
                Description = "Field vehicle KAW 412G check engine light illuminated since this morning. Vehicle is currently in the field on the Kenya Power substation route. Driver reports no unusual noise or performance loss. Requires diagnostic.",
                CategoryId = CatFleet,
                Priority = TicketPriority.Medium,
                Status = TicketStatus.Assigned,
                Source = TicketSource.Manual,
                DepartmentId = DeptFleet,
                CreatedByUserId = "seed-fleet-user",
                AssignedToUserId = "seed-fleet-manager",
                ResponseDueAt  = now.AddHours(3),
                ResolutionDueAt = now.AddDays(2),
                CreatedAt = now.AddHours(-3), UpdatedAt = now.AddHours(-1)
            },
            new Ticket
            {
                Id = "seed-ticket-010",
                Title = "Vehicle KBT 088J — 10,000 km service overdue",
                Description = "Fleet management system flagged KBT 088J as overdue for its 10,000 km service (currently at 10,847 km). Vehicle used daily for material deliveries to Kilimani site.",
                CategoryId = CatFleet,
                Priority = TicketPriority.Low,
                Status = TicketStatus.New,
                Source = TicketSource.SystemTriggered,
                DepartmentId = DeptFleet,
                CreatedByUserId = "seed-system",
                ResponseDueAt  = now.AddDays(1),
                ResolutionDueAt = now.AddDays(5),
                CreatedAt = now.AddHours(-6), UpdatedAt = now.AddHours(-6)
            },

            // ── Customer Complaint ─────────────────────────────────────────
            new Ticket
            {
                Id = "seed-ticket-011",
                Title = "Savannah Developers — dust mitigation inadequate on site boundary",
                Description = "Client representative Savannah Developers (Kilimani project) lodged formal complaint that dust from concrete cutting is affecting neighbouring occupied offices. They request immediate corrective action and a written response within 24 hours.",
                CategoryId = CatCRM,
                Priority = TicketPriority.High,
                Status = TicketStatus.InProgress,
                Source = TicketSource.CRM,
                DepartmentId = DeptCRM,
                CreatedByUserId = "seed-crm-user",
                AssignedToUserId = "seed-crm-officer",
                IsEscalated = false,
                ResponseDueAt  = now.AddHours(1),
                ResolutionDueAt = now.AddDays(1),
                CreatedAt = now.AddHours(-7), UpdatedAt = now.AddHours(-2)
            },
            new Ticket
            {
                Id = "seed-ticket-012",
                Title = "KPLC — calibration report discrepancy on certificate CERT-2026-041",
                Description = "KPLC technical team queried a measurement discrepancy on calibration certificate CERT-2026-041 issued in February. Requested re-verification of relay test data on substation 4.",
                CategoryId = CatCRM,
                Priority = TicketPriority.Medium,
                Status = TicketStatus.Resolved,
                Source = TicketSource.Manual,
                DepartmentId = DeptCRM,
                CreatedByUserId = "seed-crm-user",
                AssignedToUserId = "seed-tech-lead",
                ResolutionNotes = "Re-verified measurement data. Original readings confirmed correct. Discrepancy was due to unit conversion error in client's internal system. Clarification letter issued.",
                ResponseDueAt  = now.AddDays(-5),
                ResolutionDueAt = now.AddDays(-4),
                ResolvedAt     = now.AddDays(-3),
                CreatedAt = now.AddDays(-7), UpdatedAt = now.AddDays(-3)
            },

            // ── Internal Request ───────────────────────────────────────────
            new Ticket
            {
                Id = "seed-ticket-013",
                Title = "Request for additional PPE stock — Kilimani site",
                Description = "Current stock of safety helmets and high-visibility vests at the Kilimani site is running low. Requesting a restock of 20 helmets (mixed sizes) and 30 hi-vis vests (large and XL) before end of week.",
                CategoryId = CatGeneral,
                Priority = TicketPriority.Low,
                Status = TicketStatus.New,
                Source = TicketSource.Manual,
                DepartmentId = DeptGeneral,
                CreatedByUserId = "seed-user",
                ResponseDueAt  = now.AddDays(1),
                ResolutionDueAt = now.AddDays(5),
                CreatedAt = now.AddHours(-12), UpdatedAt = now.AddHours(-12)
            },

            // ── ANCR / Quality ─────────────────────────────────────────────
            new Ticket
            {
                Id = "seed-ticket-014",
                Title = "Non-conformance: concrete cube test failure — Kilimani Floor 3 slab",
                Description = "28-day cube test for the Floor 3 slab pour (batch 2026-03-11) returned a compressive strength of 22.4 N/mm² against a specified minimum of 25 N/mm². Non-conformance raised. Structural engineer assessment required before any further loading.",
                CategoryId = CatQuality,
                Priority = TicketPriority.High,
                Status = TicketStatus.InProgress,
                Source = TicketSource.Manual,
                DepartmentId = DeptQuality,
                CreatedByUserId = "seed-quality-user",
                AssignedToUserId = "seed-quality-lead",
                RequiresEvidence = true,
                ResponseDueAt  = now.AddHours(-3),
                ResolutionDueAt = now.AddDays(5),
                CreatedAt = now.AddDays(-1), UpdatedAt = now.AddHours(-2)
            },
            new Ticket
            {
                Id = "seed-ticket-015",
                Title = "Calibration equipment out of recall cycle — reference torque wrench TW-07",
                Description = "Reference torque wrench TW-07 has exceeded its 12-month recall period by 6 weeks. All calibration certificates issued using TW-07 during the overdue period may need to be reviewed. ANCR raised per quality procedure QP-CAL-003.",
                CategoryId = CatQuality,
                Priority = TicketPriority.High,
                Status = TicketStatus.Pending,
                Source = TicketSource.Manual,
                DepartmentId = DeptQuality,
                CreatedByUserId = "seed-quality-user",
                AssignedToUserId = "seed-quality-lead",
                RequiresEvidence = false,
                ResponseDueAt  = now.AddDays(-1),
                ResolutionDueAt = now.AddDays(3),
                CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-1)
            },
        };

        await context.Tickets.AddRangeAsync(tickets);
        await context.SaveChangesAsync();

        // Seed some comments for a few tickets
        var comments = new List<TicketComment>
        {
            new TicketComment { Id = Guid.NewGuid().ToString(), TicketId = "seed-ticket-001", AuthorUserId = "seed-it-lead",   Content = "Reviewed router logs. Firmware downgrade scheduled for tonight at 22:00 to restore stable config.", IsInternal = true,  CreatedAt = now.AddHours(-4) },
            new TicketComment { Id = Guid.NewGuid().ToString(), TicketId = "seed-ticket-001", AuthorUserId = "seed-user",      Content = "Thanks — the team will work on-site during the rollback window. Please confirm when complete.", IsInternal = false, CreatedAt = now.AddHours(-3) },
            new TicketComment { Id = Guid.NewGuid().ToString(), TicketId = "seed-ticket-005", AuthorUserId = "seed-tech-lead", Content = "Relay bench-tested — confirmed timing drift of 18ms. Replacement relay ordered from Schneider, ETA 48 hours. Substation stable on backup protection.", IsInternal = true, CreatedAt = now.AddHours(-2) },
            new TicketComment { Id = Guid.NewGuid().ToString(), TicketId = "seed-ticket-007", AuthorUserId = "seed-safety-officer", Content = "Scaffolding board secured and re-inspected. Photographic evidence uploaded. Site safety briefing to all crew done at 14:00.", IsInternal = false, CreatedAt = now.AddHours(-1) },
            new TicketComment { Id = Guid.NewGuid().ToString(), TicketId = "seed-ticket-011", AuthorUserId = "seed-crm-officer", Content = "Called client site rep — confirmed dust suppression water cart has been deployed. Written response being drafted by PM.", IsInternal = true, CreatedAt = now.AddHours(-1) },
            new TicketComment { Id = Guid.NewGuid().ToString(), TicketId = "seed-ticket-014", AuthorUserId = "seed-quality-lead", Content = "Structural engineer (MK Consult) has been notified. Core samples from the slab will be extracted tomorrow for additional testing.", IsInternal = false, CreatedAt = now.AddHours(-2) },
        };

        await context.TicketComments.AddRangeAsync(comments);
        await context.SaveChangesAsync();
    }
}
