using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UserService.Core.Constants;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Services;

namespace UserService.Infrastructure.Data;

public static class DatabaseSeeder
{
    // ── Fixed IDs ──────────────────────────────────────────────────────────────
    // Using fixed IDs so role-permission assignments survive restarts.

    // Fixed tenant/branch IDs (must match migration seed)
    const string QslTenantId  = "11111111-0000-0000-0000-000000000001";
    const string NairobiHqId  = "22222222-0000-0000-0000-000000000001";
    const string QslTenantName = "QSL";
    const string QslTenantSlug = "qsl";

    // Platform admin fixed IDs
    const string PlatformAdminUserId = "00000000-0000-0000-0000-000000000001";

    // Roles
    const string RolePlatformAdmin = "role-platform-admin";
    const string RoleAdmin         = "role-admin";
    const string RoleMD            = "role-md";
    const string RoleConstructMgr  = "role-construction-mgr";
    const string RoleConstructStaff= "role-construction-staff";
    const string RoleTechMgr       = "role-technical-mgr";
    const string RoleTechStaff     = "role-technical-staff";
    const string RoleCalibMgr      = "role-calibration-mgr";
    const string RoleCalibStaff    = "role-calibration-staff";
    const string RoleFinanceMgr    = "role-finance-mgr";
    const string RoleFinanceStaff  = "role-finance-staff";
    const string RoleHRMgr         = "role-hr-mgr";
    const string RoleHRStaff       = "role-hr-staff";
    const string RoleSafetyMgr     = "role-safety-mgr";
    const string RoleSafetyStaff   = "role-safety-staff";
    const string RoleITMgr         = "role-it-mgr";
    const string RoleITStaff       = "role-it-staff";
    const string RoleQualityMgr    = "role-quality-mgr";
    const string RoleQualityStaff  = "role-quality-staff";
    const string RoleSalesMgr      = "role-sales-mgr";
    const string RoleSalesStaff    = "role-sales-staff";
    const string RoleCRMMgr        = "role-crm-mgr";
    const string RoleCRMStaff      = "role-crm-staff";
    const string RoleFleetMgr      = "role-fleet-mgr";
    const string RoleFleetStaff    = "role-fleet-staff";

    // Permissions
    const string PermSystemAdmin    = "perm-system-admin";
    const string PermUsersRead      = "perm-users-read";
    const string PermUsersWrite     = "perm-users-write";
    const string PermUsersDelete    = "perm-users-delete";
    const string PermRolesManage    = "perm-roles-manage";
    const string PermPermsManage    = "perm-permissions-manage";
    const string PermDeptsManage    = "perm-departments-manage";
    const string PermSettingsManage = "perm-settings-manage";
    const string PermTicketsAll     = "perm-tickets-read-all";
    const string PermTicketsDept    = "perm-tickets-read-dept";
    const string PermTicketsOwn     = "perm-tickets-read-own";
    const string PermTicketsWrite   = "perm-tickets-write";
    const string PermTicketsAssign  = "perm-tickets-assign";
    const string PermTicketsResolve = "perm-tickets-resolve";
    const string PermTicketsDelete  = "perm-tickets-delete";
    const string PermProjectsAll    = "perm-projects-read-all";
    const string PermProjectsDept   = "perm-projects-read-dept";
    const string PermProjectsOwn    = "perm-projects-read-own";
    const string PermProjectsWrite  = "perm-projects-write";
    const string PermProjectsApprove= "perm-projects-approve";
    const string PermProjectsDelete = "perm-projects-delete";
    const string PermFinanceRead    = "perm-finance-read";
    const string PermFinanceWrite   = "perm-finance-write";
    const string PermFinanceApprove = "perm-finance-approve";
    const string PermFinanceReports = "perm-finance-reports";
    // #293 — enforced by their services and absent from this catalog, so no role could hold them and
    // HR (244 policy sites), CRM (142) and procurement (79) were reachable only by system.admin.
    // Tenant provisioning resolves grants BY NAME against this catalog, so an absent name cannot be
    // granted to anybody: the grant is not rejected, it just has nothing to resolve.
    const string PermHrReadOwn        = "perm-hr-read-own";
    const string PermHrReadDept       = "perm-hr-read-dept";
    const string PermHrReadAll        = "perm-hr-read-all";
    const string PermHrWrite          = "perm-hr-write";
    const string PermHrManager        = "perm-hr-manager";
    const string PermHrApprove        = "perm-hr-approve";
    const string PermHrPayrollRead    = "perm-hr-payroll-read";
    const string PermHrPayrollWrite   = "perm-hr-payroll-write";
    const string PermHrPayrollApprove = "perm-hr-payroll-approve";
    const string PermCrmReadOwn       = "perm-crm-read-own";
    const string PermCrmWrite         = "perm-crm-write";
    const string PermCrmApproveLine   = "perm-crm-approve-linemanager";
    const string PermCrmApproveBd     = "perm-crm-approve-bd";
    const string PermCrmApproveCfo    = "perm-crm-approve-cfo";
    const string PermCrmApproveMd     = "perm-crm-approve-md";
    const string PermProcReadOwn      = "perm-procurement-read-own";
    const string PermProcReadAll      = "perm-procurement-read-all";
    const string PermProcWrite        = "perm-procurement-write";
    const string PermProcApprove      = "perm-procurement-approve";
    const string PermCalibCertRead    = "perm-calibration-certificates-read";
    const string PermCalibSign        = "perm-calibration-sign";
    const string PermReportsSchedule  = "perm-reports-schedule";

    const string PermReportsView    = "perm-reports-view";
    const string PermReportsExport  = "perm-reports-export";
    const string PermPortalManage   = "perm-portal-manage";
    // Fleet
    const string PermFleetRead     = "perm-fleet-read";
    const string PermFleetWrite    = "perm-fleet-write";
    const string PermFleetDelete   = "perm-fleet-delete";
    const string PermFleetExpenses = "perm-fleet-expenses";
    const string PermFleetTripDeposits = "perm-fleet-trip-deposits";
    // Narrower than PermFleetWrite — lets a technician request a field
    // vehicle dispatch for their own assignment without granting them
    // fleet.write's broader edit/approve/reject/delete capabilities.
    const string PermFleetDispatchRequest = "perm-fleet-dispatch-request";
    // Stores (Stores, Inventory & Stock Management)
    const string PermStoresRead    = "perm-stores-read";
    const string PermStoresWrite   = "perm-stores-write";
    const string PermStoresDelete  = "perm-stores-delete";
    const string PermStoresApprove = "perm-stores-approve";
    // Technician (legacy — kept so existing role-permission rows remain valid)
    const string PermTechRead      = "perm-technician-read";
    const string PermTechWrite     = "perm-technician-write";
    const string PermTechDelete    = "perm-technician-delete";
    const string PermTechApprove   = "perm-technician-approve";
    // Operations (unified projects + assignments service)
    const string PermOpsReadOwn    = "perm-operations-read-own";
    // #282: enforced in operations-service (AssignmentsController/TimesheetsController/
    // FieldVehiclesController + its own PermissionAuthorizationHandler hierarchy) since that
    // service's inception, but never seeded here — so no role could ever hold them and the
    // dept/org-wide visibility tiers were unreachable. Mirror exactly the roles that already hold
    // the Projects analog (PermProjectsDept/PermProjectsAll) below, since Operations is the same
    // "unified projects + assignments" domain.
    const string PermOpsReadDept   = "perm-operations-read-dept";
    const string PermOpsReadAll    = "perm-operations-read-all";
    const string PermOpsWrite      = "perm-operations-write";
    const string PermOpsDelete     = "perm-operations-delete";
    const string PermOpsApprove    = "perm-operations-approve";
    // Licensing
    const string PermLicRead       = "perm-licensing-read";
    const string PermLicWrite      = "perm-licensing-write";
    const string PermLicDelete     = "perm-licensing-delete";
    // HSE
    const string PermHseRead       = "perm-hse-read";
    const string PermHseWrite      = "perm-hse-write";
    const string PermHseDelete     = "perm-hse-delete";
    const string PermHseApprove    = "perm-hse-approve";
    // Compliance
    const string PermComplianceRead    = "perm-compliance-read";
    const string PermComplianceWrite   = "perm-compliance-write";
    const string PermComplianceDelete  = "perm-compliance-delete";
    const string PermComplianceApprove = "perm-compliance-approve";
    const string PermComplianceWbRead  = "perm-compliance-whistleblower-read";
    const string PermComplianceWbWrite = "perm-compliance-whistleblower-write";
    // Statutory Compliance Calendar
    const string PermStatutoryRead    = "perm-statutory-read";
    const string PermStatutoryWrite   = "perm-statutory-write";
    const string PermStatutoryApprove = "perm-statutory-approve";

    const string PermSubcontractsRead    = "perm-subcontracts-read";
    const string PermSubcontractsWrite   = "perm-subcontracts-write";
    const string PermSubcontractsDelete  = "perm-subcontracts-delete";
    const string PermSubcontractsApprove = "perm-subcontracts-approve";
    // Platform (super admin — manages tenants, plans, platform-wide operations)
    const string PermPlatformAdmin     = "perm-platform-admin";
    const string PermTenantsManage     = "perm-tenants-manage";
    const string PermPlansManage       = "perm-plans-manage";
    const string PermPlatformReports   = "perm-platform-reports";
    const string PermBroadcast         = "perm-broadcast-notifications";
    // license-service's customer SaaS license catalog (Token/CustomerId/AppId) is an entirely
    // different, platform-wide resource from the "licensing.read/write/delete" perm-licensing-*
    // grants above (which are vestigial — nothing in fleet-service actually enforces them; they
    // were meant for an unbuilt driver/equipment-license view). Reusing that permission name let
    // any tenant's Admin/MD/Fleet Manager/IT Manager — who all got perm-licensing-* for the
    // unbuilt feature — read and revoke every OTHER customer's license (including the raw token)
    // via license-service, since user-service and license-service share the same JWT signing key.
    // This is the distinct, platform-admin-only claim license-service now requires instead.
    const string PermPlatformLicensing = "perm-platform-licensing-manage";

    public static async Task SeedAsync(IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LanteUserServiceDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ITenantOrchestrator>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LanteUserServiceDbContext>>();
        await SeedDataAsync(context, orchestrator, logger);
    }

    private static async Task SeedDataAsync(
        LanteUserServiceDbContext context, ITenantOrchestrator orchestrator, ILogger logger)
    {
        await SeedRolesAsync(context);
        await SeedPermissionsAsync(context);
        await SeedRolePermissionsAsync(context);
        await SeedAdminUserAsync(context);
        await SeedPlatformAdminAsync(context);
        await SeedDefaultTenantAsync(context);
        await SeedCompanyAdminTenantAsync(context);
        await SeedSubscriptionPlansAsync(context);
        await SeedDefaultTenantSubscriptionAsync(context);
        await SeedDepartmentsAsync(context);
        await SeedPasswordPolicyAsync(context);

        // Provision the default tenant across every service — user-service in-process, then each
        // business service (ticketing/operations/fleet/licensing) over its internal /provision
        // endpoint — the same path CreateCompany uses. Non-fatal: failures are logged and retriable
        // from the platform portal (e.g. if a business service isn't reachable yet at startup).
        var results = await orchestrator.ProvisionAllAsync(QslTenantId);
        foreach (var r in results.Where(r => !r.Success))
            logger.LogWarning(
                "Default tenant provisioning failed for service {Service}: {Error}. Retry from the platform portal.",
                r.ServiceKey, r.Error);
    }

    // ── Default Tenant ─────────────────────────────────────────────────────────
    // Ensures a default tenant + HQ branch exist on a fresh deploy, so the seeded admin
    // accounts have somewhere to sign in without a platform admin manually creating a company first.

    private static async Task SeedDefaultTenantAsync(LanteUserServiceDbContext context)
    {
        var exists = await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == QslTenantId);
        if (exists) return;

        var schemaName = TenantSlug.ToSchemaName(QslTenantSlug);

        await context.Tenants.AddAsync(new Tenant
        {
            Id         = QslTenantId,
            Name       = QslTenantName,
            Slug       = QslTenantSlug,
            SchemaName = schemaName,
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow
        });

        await context.Branches.AddAsync(new Branch
        {
            Id           = NairobiHqId,
            TenantId     = QslTenantId,
            Name         = "Nairobi HQ",
            Code         = "HQ",
            IsHeadOffice = true,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow
        });

        // Track every service (not just user) so ProvisionAllAsync provisions the tenant's schema
        // across the whole platform — mirrors CreateCompany's default when no explicit Services
        // list is given (falls back to the full set).
        foreach (var serviceKey in PlatformServices.All)
        {
            await context.TenantServiceSchemas.AddAsync(new TenantServiceSchema
            {
                TenantId   = QslTenantId,
                ServiceKey = serviceKey,
                SchemaName = schemaName,
                Status     = ProvisioningStatus.Pending,
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    // ── Default Tenant Subscription ────────────────────────────────────────────
    // Gives the default tenant an Enterprise trial subscription so it shows up correctly in the
    // platform portal instead of "no plan". Runs after SeedSubscriptionPlansAsync so the FK target exists.

    private static async Task SeedDefaultTenantSubscriptionAsync(LanteUserServiceDbContext context)
    {
        var hasSubscription = await context.CompanySubscriptions.IgnoreQueryFilters()
            .AnyAsync(s => s.TenantId == QslTenantId);
        if (hasSubscription) return;

        await context.CompanySubscriptions.AddAsync(new CompanySubscription
        {
            TenantId     = QslTenantId,
            PlanId       = "plan-enterprise",
            Status       = SubscriptionStatus.Trial,
            BillingCycle = BillingCycle.Monthly,
            StartDate    = DateTime.UtcNow,
            TrialEndsAt  = DateTime.UtcNow.AddDays(30),
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    // ── Roles ──────────────────────────────────────────────────────────────────

    private static async Task SeedRolesAsync(LanteUserServiceDbContext context)
    {
        var roles = new[]
        {
            (RolePlatformAdmin,  "Platform Admin",         "Lante platform manager — manages tenants, plans and platform-wide settings"),
            (RoleAdmin,          "Admin",                  "System Administrator — full access to all modules"),
            (RoleMD,             "MD",                     "Managing Director — oversight of all operations, approves key processes"),
            (RoleConstructMgr,   "Construction Manager",   "Manages construction projects, teams and site operations"),
            (RoleConstructStaff, "Construction Staff",     "Field engineers and site workers in Construction department"),
            (RoleTechMgr,        "Technical Manager",      "Manages technical service operations and team"),
            (RoleTechStaff,      "Technical Staff",        "Technicians in Technical Services department"),
            (RoleCalibMgr,       "Calibration Manager",    "Manages calibration lab operations and sign-offs"),
            (RoleCalibStaff,     "Calibration Staff",      "Lab technicians in Calibration department"),
            (RoleFinanceMgr,     "Finance Manager",        "Manages all financial operations, approvals and reporting"),
            (RoleFinanceStaff,   "Finance Staff",          "Accounts and finance data entry"),
            (RoleHRMgr,          "HR Manager",             "Manages staff, departments and HR processes"),
            (RoleHRStaff,        "HR Staff",               "HR administrative staff"),
            (RoleSafetyMgr,      "Safety Manager",         "Oversees HSE compliance across all departments"),
            (RoleSafetyStaff,    "Safety Staff",           "HSE field officers"),
            (RoleITMgr,          "IT Manager",             "Manages system users, roles, settings and IT infrastructure"),
            (RoleITStaff,        "IT Staff",               "IT support and helpdesk"),
            (RoleQualityMgr,     "Quality Manager",        "Manages QA/QC processes and non-conformance reporting"),
            (RoleQualityStaff,   "Quality Staff",          "QA/QC inspectors"),
            (RoleSalesMgr,       "Sales Manager",          "Manages sales pipeline and business development"),
            (RoleSalesStaff,     "Sales Staff",            "Sales representatives"),
            (RoleCRMMgr,         "CRM Manager",            "Manages client relationships, complaints and portal submissions"),
            (RoleCRMStaff,       "CRM Staff",              "Client relations staff"),
            (RoleFleetMgr,       "Fleet Manager",          "Manages fleet vehicles, trips, drivers and expenses"),
            (RoleFleetStaff,     "Fleet Staff",            "Drivers and fleet operations staff"),
        };

        var existingIds = await context.Roles.IgnoreQueryFilters().Select(r => r.Id).ToListAsync();
        var existingNames = await context.Roles.IgnoreQueryFilters().Select(r => r.Name).ToListAsync();

        foreach (var (id, name, desc) in roles)
        {
            if (!existingIds.Contains(id) && !existingNames.Contains(name))
            {
                await context.Roles.AddAsync(new Role
                {
                    Id = id, Name = name, Description = desc,
                    IsActive = true, IsSystem = WellKnownRoles.LockedRoleIds.Contains(id),
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                });
            }
        }

        // Backfill. IsSystem was added by its own migration and then never set by anything, so every role
        // in every existing database is IsSystem = false — including Platform Admin. RoleService's
        // "System roles cannot be modified" guard has therefore never once fired, which left the platform
        // operator role renameable and deletable by any holder of roles.manage. Inserting the flag only for
        // NEW rows would fix nothing: no deployment is new.
        var lockedIds = WellKnownRoles.LockedRoleIds;
        var toLock = await context.Roles.Where(r => lockedIds.Contains(r.Id) && !r.IsSystem).ToListAsync();
        foreach (var role in toLock)
        {
            role.IsSystem = true;
            role.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    // ── Permissions ────────────────────────────────────────────────────────────

    private static async Task SeedPermissionsAsync(LanteUserServiceDbContext context)
    {
        var permissions = new[]
        {
            // System
            (PermSystemAdmin,    "system.admin",          "Full system administrator access"),
            (PermUsersRead,      "users.read",            "View all users and their details"),
            (PermUsersWrite,     "users.write",           "Create and update users"),
            (PermUsersDelete,    "users.delete",          "Delete and restore users"),
            (PermRolesManage,    "roles.manage",          "Create, update, delete roles and assign permissions"),
            (PermPermsManage,    "permissions.manage",    "Create and delete system permissions"),
            (PermDeptsManage,    "departments.manage",    "Create, update and delete departments"),
            (PermSettingsManage, "settings.manage",       "Configure system settings"),
            // Tickets
            (PermTicketsAll,     "tickets.read.all",      "View all tickets across all departments"),
            (PermTicketsDept,    "tickets.read.dept",     "View tickets in own department only"),
            (PermTicketsOwn,     "tickets.read.own",      "View own tickets only"),
            (PermTicketsWrite,   "tickets.write",         "Create and update tickets"),
            (PermTicketsAssign,  "tickets.assign",        "Assign tickets to staff members"),
            (PermTicketsResolve, "tickets.resolve",       "Resolve, close and escalate tickets"),
            (PermTicketsDelete,  "tickets.delete",        "Delete tickets"),
            // Projects
            (PermProjectsAll,    "projects.read.all",     "View all projects across all departments"),
            (PermProjectsDept,   "projects.read.dept",    "View projects in own department only"),
            (PermProjectsOwn,    "projects.read.own",     "View own assigned projects only"),
            (PermProjectsWrite,  "projects.write",        "Create and update projects, milestones and tasks"),
            (PermProjectsApprove,"projects.approve",      "Approve project milestones and budget requests (MD sign-off)"),
            (PermProjectsDelete, "projects.delete",       "Delete projects"),
            // Finance
            (PermFinanceRead,    "finance.read",          "View budgets, cost entries and financial data"),
            (PermFinanceWrite,   "finance.write",         "Create and update financial records and cost entries"),
            (PermFinanceApprove, "finance.approve",       "Approve financial transactions and budget changes"),
            (PermFinanceReports, "finance.reports",       "Access financial reports and analytics"),
            // Reports
            // HR (#293)
            (PermHrReadOwn,        "hr.read.own",        "View your own HR records — leave, attendance, appraisals, payslips"),
            (PermHrReadDept,       "hr.read.dept",       "View HR records for your whole department"),
            (PermHrReadAll,        "hr.read.all",        "View HR records across every department"),
            (PermHrWrite,          "hr.write",           "Create and update employees, leave, attendance and disciplinary records"),
            (PermHrManager,        "hr.manager",         "Act as line manager — review appraisals, decide leave and overtime for your reports"),
            (PermHrApprove,        "hr.approve",         "Approve HR records as the HR authority"),
            (PermHrPayrollRead,    "hr.payroll.read",    "View payroll runs, payslips and statutory figures"),
            (PermHrPayrollWrite,   "hr.payroll.write",   "Create and compute payroll runs"),
            (PermHrPayrollApprove, "hr.payroll.approve", "Approve a computed payroll run — the second officer in the segregation-of-duties rule"),
            // CRM (#293)
            (PermCrmReadOwn,       "crm.read.own",       "View your own leads, opportunities, deals and quotations"),
            (PermCrmWrite,         "crm.write",          "Create and update customers, leads, opportunities, deals and quotations"),
            (PermCrmApproveLine,   "crm.approve.linemanager", "First stage of the quotation approval chain — line manager"),
            (PermCrmApproveBd,     "crm.approve.bd",     "Second stage of the quotation approval chain — business development"),
            (PermCrmApproveCfo,    "crm.approve.cfo",    "Third stage of the quotation approval chain — CFO"),
            (PermCrmApproveMd,     "crm.approve.md",     "Final stage of the quotation approval chain — MD"),
            // Procurement (#293)
            (PermProcReadOwn,      "procurement.read.own", "View your own requisitions, quotations and purchase orders"),
            (PermProcReadAll,      "procurement.read.all", "View procurement records across every department"),
            (PermProcWrite,        "procurement.write",  "Create and update requisitions, quotations, purchase orders and suppliers"),
            (PermProcApprove,      "procurement.approve","Approve purchase orders and three-way match exceptions"),
            // Calibration (#293)
            (PermCalibCertRead,    "calibration.certificates.read", "View issued calibration certificates"),
            (PermCalibSign,        "calibration.sign",   "Sign a calibration certificate as the competent authority"),
            // Reporting (#293)
            (PermReportsSchedule,  "reports.schedule",   "Schedule reports to run and be delivered automatically"),
            (PermReportsView,    "reports.view",          "View dashboard and operational reports"),
            (PermReportsExport,  "reports.export",        "Export reports to PDF and Excel"),
            // Portal
            (PermPortalManage,   "portal.manage",         "View and manage client portal submissions"),
            // Fleet
            (PermFleetRead,      "fleet.read",            "View trips, vehicles, drivers and fleet data"),
            (PermFleetWrite,     "fleet.write",           "Create and update trips, vehicles and driver profiles"),
            (PermFleetDelete,    "fleet.delete",          "Delete fleet records"),
            (PermFleetExpenses,  "fleet.expenses",        "View and manage fleet expense records"),
            (PermFleetTripDeposits, "fleet.tripdeposits", "View and manage trip banking/M-Pesa deposit records"),
            (PermFleetDispatchRequest, "fleet.dispatch.request", "Request a field vehicle dispatch for an assignment"),
            // Stores
            (PermStoresRead,     "stores.read",           "View suppliers, items, GRNs and stock records"),
            (PermStoresWrite,    "stores.write",          "Create and update suppliers, items, GRNs and stock movements"),
            (PermStoresDelete,   "stores.delete",         "Delete/void stores records"),
            (PermStoresApprove,  "stores.approve",        "Approve stock-take variance reconciliations"),
            // Technician (legacy)
            (PermTechRead,       "technician.read",       "View technician assignments, check-ins and reports"),
            (PermTechWrite,      "technician.write",      "Create and update technician assignments and daily summaries"),
            (PermTechDelete,     "technician.delete",     "Delete technician records"),
            (PermTechApprove,    "technician.approve",    "Approve requisitions, claims and advance forms"),
            // Operations
            (PermOpsReadOwn,     "operations.read.own",   "View own assignments, service reports and financial forms"),
            (PermOpsReadDept,    "operations.read.dept",  "View assignments, service reports and financial forms in own department"),
            (PermOpsReadAll,     "operations.read.all",   "View assignments, service reports and financial forms across all departments"),
            (PermOpsWrite,       "operations.write",      "Create and update assignments, check-ins, service reports and financial forms"),
            (PermOpsDelete,      "operations.delete",     "Delete operations records"),
            (PermOpsApprove,     "operations.approve",    "Approve requisitions, claims, petty cash, advance returns, refunds and service reports as line manager"),
            // Licensing
            (PermLicRead,        "licensing.read",        "View equipment and driver licenses"),
            (PermLicWrite,       "licensing.write",       "Create and update license records"),
            (PermLicDelete,      "licensing.delete",      "Delete license records"),
            // HSE
            (PermHseRead,        "hse.read",              "View incidents, RAMS, PPE, training and inspection records"),
            (PermHseWrite,       "hse.write",             "Create and update HSE records"),
            (PermHseDelete,      "hse.delete",            "Delete HSE records"),
            (PermHseApprove,     "hse.approve",           "Approve RAMS, record inspection results and subcontractor prequalification"),
            // Compliance
            (PermComplianceRead,    "compliance.read",                     "View gifts register, COI declarations, policies, licences and compliance records"),
            (PermComplianceWrite,   "compliance.write",                    "Create and update compliance records"),
            (PermComplianceDelete,  "compliance.delete",                   "Delete compliance records"),
            (PermComplianceApprove, "compliance.approve",                  "Review COI declarations, close data breaches, mark related-party transactions reported"),
            (PermComplianceWbRead,  "compliance.whistleblower.read",       "View whistleblower cases — restricted, not implied by compliance.read"),
            (PermComplianceWbWrite, "compliance.whistleblower.write",      "Submit and update whistleblower cases — restricted, not implied by compliance.write"),
            // Statutory Compliance Calendar
            (PermStatutoryRead,    "statutory.read",    "View the statutory compliance calendar, annual returns, TCC status and Company Secretary tasks"),
            (PermStatutoryWrite,   "statutory.write",   "Create and update statutory obligations, deadlines, annual returns, TCC and Cosec tasks"),
            (PermStatutoryApprove, "statutory.approve", "Sign off annual return filings — MD/Company Secretary tier, not implied by statutory.read/write"),

            (PermSubcontractsRead,    "subcontracts.read",    "View the Approved Subcontractor Register, prequalifications, awards, scorecards and payment retentions"),
            (PermSubcontractsWrite,   "subcontracts.write",   "Create and update subcontractor, prequalification, award, scorecard and payment retention records"),
            (PermSubcontractsDelete,  "subcontracts.delete",  "Delete subcontracts records"),
            (PermSubcontractsApprove, "subcontracts.approve", "Approve prequalifications, awards and mobilization activation — Head of Projects tier"),
            // Platform
            (PermPlatformAdmin,  "platform.admin",        "Full platform administrator access"),
            (PermTenantsManage,  "platform.tenants",      "Create, update, suspend and delete tenant companies"),
            (PermPlansManage,    "platform.plans",        "Create, update and delete subscription plans"),
            (PermPlatformReports,"platform.reports",      "View cross-tenant reports and analytics"),
            (PermBroadcast,      "platform.broadcast",    "Send broadcast notifications to tenants"),
            (PermPlatformLicensing, "platform.licensing.manage", "Manage every customer's software license record (license-service)"),
        };

        var existingIds = await context.Permissions.IgnoreQueryFilters().Select(p => p.Id).ToListAsync();
        var existingNames = await context.Permissions.IgnoreQueryFilters().Select(p => p.Name).ToListAsync();

        foreach (var (id, name, desc) in permissions)
        {
            if (!existingIds.Contains(id) && !existingNames.Contains(name))
            {
                await context.Permissions.AddAsync(new Permission
                {
                    Id = id, Name = name, Description = desc,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                });
            }
        }
        await context.SaveChangesAsync();
    }

    // ── Role-Permission Mappings ───────────────────────────────────────────────

    private static async Task SeedRolePermissionsAsync(LanteUserServiceDbContext context)
    {
        // Define which permissions each role gets
        var mappings = new Dictionary<string, string[]>
        {
            [RolePlatformAdmin] = new[] {
                PermPlatformAdmin, PermTenantsManage, PermPlansManage,
                PermPlatformReports, PermBroadcast, PermPlatformLicensing
            },

            // PermLicRead/Write/Delete deliberately dropped from every tenant role below: that
            // permission is checked by the separate license-service to gate its cross-customer
            // software-license catalog (Token/CustomerId/AppId), not any feature these tenant
            // roles actually have — nothing in fleet-service enforces "licensing.*" for the
            // driver/equipment-license view it was originally meant for. Granting it here let any
            // tenant Admin/MD/IT Manager read and revoke every OTHER customer's license, since
            // user-service and license-service trust the same JWT signing key. See
            // PermPlatformLicensing above for the real (platform-admin-only) replacement.
            [RoleAdmin] = new[] {
                PermSystemAdmin, PermUsersRead, PermUsersWrite, PermUsersDelete,
                PermRolesManage, PermPermsManage, PermDeptsManage, PermSettingsManage,
                PermTicketsAll, PermTicketsWrite, PermTicketsAssign, PermTicketsResolve, PermTicketsDelete,
                PermProjectsAll, PermProjectsWrite, PermProjectsApprove, PermProjectsDelete,
                PermFinanceRead, PermFinanceWrite, PermFinanceApprove, PermFinanceReports,
                PermReportsView, PermReportsExport, PermPortalManage,
                PermFleetRead, PermFleetWrite, PermFleetDelete, PermFleetExpenses, PermFleetTripDeposits,
                PermStoresRead, PermStoresWrite, PermStoresDelete, PermStoresApprove,
                PermTechRead, PermTechWrite, PermTechDelete, PermTechApprove,
                PermOpsReadAll, PermOpsWrite, PermOpsDelete, PermOpsApprove,
                // #293. Admin already holds PermSystemAdmin and therefore already passed every one of
                // these checks, so listing them is explicit rather than widening — and it means an admin
                // can now SEE and delegate them in the Roles UI, which was the practical problem: a
                // permission absent from this catalog cannot be granted to anybody.
                PermHrReadOwn, PermHrReadDept, PermHrReadAll, PermHrWrite, PermHrManager, PermHrApprove,
                PermHrPayrollRead, PermHrPayrollWrite, PermHrPayrollApprove,
                PermCrmReadOwn, PermCrmWrite,
                PermCrmApproveLine, PermCrmApproveBd, PermCrmApproveCfo, PermCrmApproveMd,
                PermProcReadOwn, PermProcReadAll, PermProcWrite, PermProcApprove,
                PermCalibCertRead, PermCalibSign, PermReportsSchedule
            },

            // 2026-07: MD granted the same system.admin bypass as Admin — full parity "for now"
            // per product decision, with the expectation that specific permissions get trimmed
            // back off MD later via the Roles UI (MD isn't IsSystem-locked, so that's editable).
            [RoleMD] = new[] {
                PermSystemAdmin,
                PermUsersRead,
                PermTicketsAll, PermTicketsAssign, PermTicketsResolve,
                PermProjectsAll, PermProjectsApprove,
                PermFinanceRead, PermFinanceApprove, PermFinanceReports,
                PermReportsView, PermReportsExport, PermPortalManage,
                PermFleetRead, PermFleetExpenses, PermFleetTripDeposits,
                PermStoresRead, PermStoresApprove,
                PermTechRead, PermTechApprove,
                PermOpsReadAll, PermOpsApprove,
                PermHseRead,
                PermComplianceRead, PermComplianceApprove,
                PermComplianceWbRead, PermComplianceWbWrite,
                PermStatutoryRead, PermStatutoryApprove,
                PermSubcontractsRead, PermSubcontractsApprove
            },

            [RoleITMgr] = new[] {
                PermUsersRead, PermUsersWrite, PermUsersDelete,
                PermRolesManage, PermPermsManage, PermDeptsManage, PermSettingsManage,
                PermTicketsAll, PermTicketsWrite, PermTicketsAssign, PermTicketsResolve, PermTicketsDelete,
                PermFleetRead, PermTechRead, PermOpsReadOwn,
                PermReportsView, PermReportsExport
            },

            [RoleITStaff] = new[] {
                PermTicketsDept, PermTicketsWrite
            },

            [RoleConstructMgr] = new[] {
                PermProjectsDept, PermProjectsWrite, PermProjectsDelete,
                PermTicketsDept, PermTicketsWrite, PermTicketsAssign, PermTicketsResolve,
                PermFinanceRead,
                PermOpsReadDept, PermOpsWrite, PermOpsApprove,
                PermReportsView, PermReportsExport,
                PermHseRead, PermHseWrite,
                // Head of Projects tier: approves PQQs, awards and mobilization activation (SUB-002/004/005)
                PermSubcontractsRead, PermSubcontractsWrite, PermSubcontractsDelete, PermSubcontractsApprove
            },

            [RoleConstructStaff] = new[] {
                PermProjectsDept,
                PermTicketsDept, PermTicketsWrite,
                PermOpsReadDept, PermOpsWrite,
                PermHseRead, PermHseWrite,
                PermSubcontractsRead, PermSubcontractsWrite
            },

            [RoleTechMgr] = new[] {
                PermProjectsDept, PermProjectsWrite, PermProjectsDelete,
                PermTicketsDept, PermTicketsWrite, PermTicketsAssign, PermTicketsResolve,
                PermFinanceRead,
                PermTechRead, PermTechWrite, PermTechApprove,
                PermOpsReadDept, PermOpsWrite, PermOpsApprove,
                PermLicRead, PermLicWrite,
                PermReportsView, PermReportsExport
            },

            [RoleTechStaff] = new[] {
                PermProjectsOwn,
                PermTicketsOwn, PermTicketsWrite,
                PermOpsReadOwn,
                PermFleetRead, PermFleetDispatchRequest,
            },

            [RoleCalibMgr] = new[] {
                PermCalibCertRead, PermReportsSchedule,
                PermProjectsDept, PermProjectsWrite,
                PermTicketsDept, PermTicketsWrite, PermTicketsAssign, PermTicketsResolve,
                PermTechRead, PermOpsReadDept, PermLicRead, PermLicWrite,
                PermReportsView, PermReportsExport
            },

            [RoleCalibStaff] = new[] {
                PermCalibCertRead,
                PermProjectsDept,
                PermTicketsDept, PermTicketsWrite,
                PermTechRead, PermOpsReadDept, PermLicRead
            },

            [RoleFinanceMgr] = new[] {
                PermUsersRead,
                PermFinanceRead, PermFinanceWrite, PermFinanceApprove, PermFinanceReports,
                PermProjectsAll,
                PermTicketsDept,
                PermFleetExpenses, PermFleetTripDeposits, PermTechRead, PermOpsReadAll,
                PermReportsView, PermReportsExport,
                PermComplianceRead, PermComplianceWrite, PermComplianceApprove,
                PermStatutoryRead, PermStatutoryWrite,
                // SUB-007: certified invoices, retention balances, WHT deductions
                PermSubcontractsRead, PermSubcontractsWrite
            },

            [RoleFinanceStaff] = new[] {
                PermFinanceRead, PermFinanceWrite,
                PermTicketsOwn, PermTicketsWrite,
                PermReportsView,
                PermSubcontractsRead, PermSubcontractsWrite
            },

            [RoleHRMgr] = new[] {
                PermUsersRead, PermUsersWrite,
                PermDeptsManage,
                PermHrReadOwn, PermHrReadDept, PermHrReadAll, PermHrWrite, PermHrManager,
                PermHrPayrollRead, PermHrPayrollWrite,
                PermTicketsDept, PermTicketsAssign,
                PermReportsView, PermReportsExport, PermReportsSchedule,
                PermComplianceRead, PermComplianceWrite
            },

            [RoleHRStaff] = new[] {
                PermUsersRead,
                PermHrReadOwn, PermHrReadDept, PermHrWrite,
                PermTicketsOwn, PermTicketsWrite
            },

            [RoleSafetyMgr] = new[] {
                PermTicketsAll, PermTicketsWrite, PermTicketsAssign, PermTicketsResolve,
                PermProjectsAll,
                PermFleetRead, PermTechRead, PermOpsReadAll,
                PermReportsView, PermReportsExport,
                PermHseRead, PermHseWrite, PermHseDelete, PermHseApprove,
                // Safety oversight of subcontractor RAMS status feeding the SUB-005 mobilization gate
                PermSubcontractsRead
            },

            [RoleSafetyStaff] = new[] {
                PermTicketsDept, PermTicketsWrite,
                PermHseRead, PermHseWrite
            },

            [RoleQualityMgr] = new[] {
                PermTicketsAll, PermTicketsWrite, PermTicketsAssign,
                PermProjectsAll,
                PermReportsView, PermReportsExport
            },

            [RoleQualityStaff] = new[] {
                PermTicketsDept, PermTicketsWrite
            },

            [RoleSalesMgr] = new[] {
                PermCrmReadOwn, PermCrmWrite,
                PermPortalManage,
                PermTicketsDept, PermTicketsWrite, PermTicketsAssign,
                PermReportsView, PermReportsExport
            },

            [RoleSalesStaff] = new[] {
                PermCrmReadOwn, PermCrmWrite,
                PermPortalManage,
                PermTicketsOwn, PermTicketsWrite
            },

            [RoleCRMMgr] = new[] {
                PermCrmReadOwn, PermCrmWrite,
                PermPortalManage,
                PermTicketsDept, PermTicketsWrite, PermTicketsAssign, PermTicketsResolve,
                PermReportsView, PermReportsExport
            },

            [RoleCRMStaff] = new[] {
                PermPortalManage,
                PermTicketsOwn, PermTicketsWrite
            },

            [RoleFleetMgr] = new[] {
                PermFleetRead, PermFleetWrite, PermFleetDelete, PermFleetExpenses, PermFleetTripDeposits,
                PermTicketsDept, PermTicketsWrite, PermTicketsAssign,
                PermLicRead, PermLicWrite,
                PermReportsView, PermReportsExport,
                PermUsersRead,
            },

            [RoleFleetStaff] = new[] {
                PermFleetRead, PermFleetWrite,
                PermTicketsOwn, PermTicketsWrite
            },
        };

        // Load existing assignments to avoid duplicates
        var existing = await context.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync();
        var existingSet = existing.Select(x => $"{x.RoleId}:{x.PermissionId}").ToHashSet();

        foreach (var (roleId, permIds) in mappings)
        {
            foreach (var permId in permIds)
            {
                var key = $"{roleId}:{permId}";
                if (!existingSet.Contains(key))
                {
                    await context.RolePermissions.AddAsync(new RolePermission
                    {
                        Id           = Guid.NewGuid().ToString(),
                        RoleId       = roleId,
                        PermissionId = permId,
                        AssignedAt   = DateTime.UtcNow,
                        CreatedAt    = DateTime.UtcNow,
                        UpdatedAt    = DateTime.UtcNow
                    });
                    existingSet.Add(key);
                }
            }
        }
        await context.SaveChangesAsync();
    }

    // ── Admin User ─────────────────────────────────────────────────────────────

    private static async Task SeedAdminUserAsync(LanteUserServiceDbContext context)
    {
        if (!await context.Users.AnyAsync())
        {
            var adminUser = new User
            {
                Id               = Guid.NewGuid().ToString(),
                FirstName        = "System",
                LastName         = "Admin",
                Email            = "kleinmelanie04@gmail.com",
                Password         = BCrypt.Net.BCrypt.HashPassword("Admin@123", workFactor: 12),
                MobileNumber     = "0000000000",
                IsActive         = true,
                IsFirstLogin     = false,
                TwoFactorEnabled = true,
                CreatedAt        = DateTime.UtcNow,
                UpdatedAt        = DateTime.UtcNow
            };
            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();

            await context.UserRoles.AddAsync(new UserRole
            {
                Id        = Guid.NewGuid().ToString(),
                UserId    = adminUser.Id,
                RoleId    = RoleAdmin,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
    }

    // ── Platform Admin User ────────────────────────────────────────────────────

    private static async Task SeedPlatformAdminAsync(LanteUserServiceDbContext context)
    {
        const string email = "akinyimelante@gmail.com";

        var exists = await context.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email);

        if (!exists)
        {
            var user = new User
            {
                Id               = PlatformAdminUserId,
                FirstName        = "Platform",
                LastName         = "Admin",
                Email            = email,
                Password         = BCrypt.Net.BCrypt.HashPassword("Platform@2026!", workFactor: 12),
                MobileNumber     = "0000000001",
                IsActive         = true,
                IsFirstLogin     = false,
                TwoFactorEnabled = true,
                CreatedAt        = DateTime.UtcNow,
                UpdatedAt        = DateTime.UtcNow
            };
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();

            await context.UserRoles.AddAsync(new UserRole
            {
                Id        = "role-assign-platform-admin-00000001",
                UserId    = PlatformAdminUserId,
                RoleId    = RolePlatformAdmin,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
    }

    // ── QSL Company Admins — set BranchId = null so JWT marks is_company_admin ─
    // kleinmelanie04@gmail.com (seeded as a tenant-less system admin in
    // SeedAdminUserAsync) never got a UserTenant link, so the QSL company's
    // per-tenant user count showed 0 despite this account being the one
    // actively used to sign in. Link both known QSL admins here.

    private static readonly string[] QslCompanyAdminEmails =
    {
        "joshuaiska@gmail.com",
        "kleinmelanie04@gmail.com",
    };

    private static async Task SeedCompanyAdminTenantAsync(LanteUserServiceDbContext context)
    {
        // Check if the QSL tenant exists (migration may not have run yet on fresh DB)
        var tenantExists = await context.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Id == QslTenantId);
        if (!tenantExists) return;

        foreach (var adminEmail in QslCompanyAdminEmails)
        {
            var admin = await context.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == adminEmail);
            if (admin == null) continue;

            var userTenant = await context.UserTenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(ut => ut.UserId == admin.Id && ut.TenantId == QslTenantId);

            if (userTenant == null)
            {
                // Fresh DB / not yet linked — create the record
                await context.UserTenants.AddAsync(new UserTenant
                {
                    Id        = Guid.NewGuid().ToString(),
                    UserId    = admin.Id,
                    TenantId  = QslTenantId,
                    BranchId  = null,          // null = company admin (sees all branches)
                    IsDefault = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else if (userTenant.BranchId != null)
            {
                // Migration backfill set BranchId = Nairobi — fix to null for company admin
                userTenant.BranchId  = null;
                userTenant.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
    }

    // ── Departments ────────────────────────────────────────────────────────────

    private static async Task SeedDepartmentsAsync(LanteUserServiceDbContext context)
    {
        var desired = new[]
        {
            ("IT",              "Information Technology"),
            ("Technical Service","Technical Services"),
            ("Safety",          "Safety & Compliance (HSE)"),
            ("Fleet",           "Fleet Management"),
            ("CRM",             "Customer Relations"),
            ("General",         "General Administration"),
            ("Quality",         "Quality Assurance & Control"),
            ("Finance",         "Finance & Accounts"),
            ("HR",              "Human Resources"),
            ("Sales",           "Sales & Business Development"),
            ("Calibration Lab", "Equipment Calibration"),
            ("Construction",    "Construction & Site Works"),
        };

        var existing = await context.Departments.Select(d => d.Name).ToListAsync();

        foreach (var (name, desc) in desired)
        {
            if (!existing.Contains(name))
            {
                await context.Departments.AddAsync(new Department
                {
                    Id          = Guid.NewGuid().ToString(),
                    Name        = name,
                    Description = desc,
                    IsActive    = true,
                    CreatedAt   = DateTime.UtcNow,
                    UpdatedAt   = DateTime.UtcNow
                });
            }
        }
        await context.SaveChangesAsync();
    }

    // ── Subscription Plans ─────────────────────────────────────────────────────

    private static async Task SeedSubscriptionPlansAsync(LanteUserServiceDbContext context)
    {
        var plans = new[]
        {
            ("plan-free",         "Free",         "Trial/starter plan for small teams",                          0m,      0m,    1,  5,  "[\"ticketing\"]"),
            ("plan-professional", "Professional", "Full access for growing companies",                           15000m,  150000m, 5, 50, "[\"ticketing\",\"operations\",\"fleet\",\"licensing\",\"stores\",\"hse\",\"compliance\"]"),
            ("plan-enterprise",   "Enterprise",   "Unlimited branches and users, priority support",              35000m, 350000m, -1, -1, "[\"ticketing\",\"operations\",\"fleet\",\"licensing\",\"stores\",\"hse\",\"compliance\",\"subcontracts\",\"reporting\",\"crm\",\"procurement\",\"finance\",\"hr\",\"api_access\",\"sso\"]"),
        };

        var existingIds = await context.SubscriptionPlans.IgnoreQueryFilters()
            .Select(p => p.Id).ToListAsync();

        foreach (var (id, name, desc, monthly, annual, maxBranches, maxUsers, features) in plans)
        {
            if (!existingIds.Contains(id))
            {
                await context.SubscriptionPlans.AddAsync(new SubscriptionPlan
                {
                    Id            = id,
                    Name          = name,
                    Description   = desc,
                    PriceMonthly  = monthly,
                    PriceAnnual   = annual,
                    MaxBranches   = maxBranches,
                    MaxUsers      = maxUsers,
                    FeaturesJson  = features,
                    IsActive      = true,
                    CreatedAt     = DateTime.UtcNow,
                    UpdatedAt     = DateTime.UtcNow
                });
            }
        }
        await context.SaveChangesAsync();
    }

    // ── Password Policy ────────────────────────────────────────────────────────

    private static async Task SeedPasswordPolicyAsync(LanteUserServiceDbContext context)
    {
        if (!await context.PasswordPolicies.AnyAsync())
        {
            await context.PasswordPolicies.AddAsync(new PasswordPolicy
            {
                Id                     = Guid.NewGuid().ToString(),
                MinimumLength          = 8,
                RequireUppercase       = true,
                RequireLowercase       = true,
                RequireDigit           = true,
                RequireSpecialCharacter= true,
                MaxAgeDays             = 90,
                CreatedAt              = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
    }
}
