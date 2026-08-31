using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Core.Constants;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Emails;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;
using UserService.Infrastructure.Data;

namespace UserService.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/platform")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize(Roles = "Platform Admin")]
public class PlatformController(
    ITenantRepository tenantRepository,
    IEmailQueueService emailQueueService,
    ITenantOrchestrator orchestrator,
    LanteUserServiceDbContext context,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PlatformController> logger)
    : BaseController
{
    // ── Dashboard ──────────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var tenantCount      = await context.Tenants.CountAsync();
        var branchCount      = await context.Branches.CountAsync();
        var userCount        = await context.Users.CountAsync();
        var activeSubs       = await context.CompanySubscriptions
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial)
            .CountAsync();

        return OkResult(new
        {
            totalTenants      = tenantCount,
            totalBranches     = branchCount,
            totalUsers        = userCount,
            activeSubscriptions = activeSubs
        });
    }

    // ── Companies (Tenants) ────────────────────────────────────────────────────

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies()
    {
        var companies = await context.Tenants
            .IgnoreQueryFilters()
            .Select(t => new
            {
                t.Id, t.Name, t.Slug, t.IsActive, t.CreatedAt,
                branchCount = context.Branches.Count(b => b.TenantId == t.Id && !b.IsDeleted),
                userCount   = context.UserTenants.Count(ut => ut.TenantId == t.Id && !ut.IsDeleted),
                subscription = context.CompanySubscriptions
                    .Where(s => s.TenantId == t.Id && !s.IsDeleted)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new { s.Status, s.Plan!.Name, s.BillingCycle, s.StartDate, s.EndDate })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return OkResult(companies);
    }

    [HttpGet("companies/{id}")]
    public async Task<IActionResult> GetCompany(string id)
    {
        var tenant = await context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFoundResult("Company not found.");

        var branches = await tenantRepository.GetBranchesByTenantAsync(id);
        var subscription = await context.CompanySubscriptions
            .Include(s => s.Plan)
            .Where(s => s.TenantId == id && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        var admins = await context.UserTenants
            .Include(ut => ut.User)
            .Where(ut => ut.TenantId == id && ut.BranchId == null && !ut.IsDeleted)
            .Select(ut => new { ut.User.Id, ut.User.FirstName, ut.User.LastName, ut.User.Email, ut.User.MobileNumber })
            .ToListAsync();

        // Total headcount across all branches, not just company-level admins —
        // must match GetCompanies' userCount so the list and detail views agree.
        var userCount = await context.UserTenants.CountAsync(ut => ut.TenantId == id && !ut.IsDeleted);

        return OkResult(new
        {
            tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive, tenant.CreatedAt,
            branchCount = branches.Count,
            userCount,
            branches = branches.Select(b => new { b.Id, b.Name, b.Code, b.IsHeadOffice, b.IsActive }),
            subscription = subscription == null ? null : new
            {
                subscription.Id, subscription.Status, subscription.BillingCycle,
                Name = subscription.Plan?.Name,
                plan = subscription.Plan == null ? null : new { subscription.Plan.Id, subscription.Plan.Name },
                subscription.StartDate, subscription.EndDate, subscription.TrialEndsAt
            },
            admins
        });
    }

    // ── Company Creation (full onboarding transaction) ─────────────────────────

    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        var slug = dto.Slug?.Trim().ToLowerInvariant() ?? string.Empty;

        // Validate slug format — it doubles as the tenant subdomain and schema name
        if (!TenantSlug.IsValidFormat(slug))
            return BadRequestResult("Slug must be 2–50 lowercase letters, digits, and single hyphens, starting with a letter.");
        if (TenantSlug.IsReserved(slug))
            return ConflictResult($"'{slug}' is a reserved subdomain and cannot be used.");

        // Check slug uniqueness
        var slugExists = await context.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == slug);
        if (slugExists) return ConflictResult("A company with this slug already exists.");

        var emailExists = await context.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email == dto.AdminEmail);
        if (emailExists) return ConflictResult("A user with this email already exists.");

        const string tempPassword = "Welcome@2026!";

        // The DbContext enables retry-on-failure; that execution strategy forbids user-initiated
        // transactions unless the whole unit runs inside strategy.ExecuteAsync.
        //
        // The slugExists check above is racy — two concurrent CreateCompany calls for the same slug
        // can both pass it before either commits. Tenant.Slug is unique-indexed (see
        // LanteUserServiceDbContext), so the loser's SaveChangesAsync below throws DbUpdateException
        // instead of silently creating a duplicate; caught just outside this transaction and turned
        // back into the same friendly Conflict the pre-check would have given the loser.
        var strategy = context.Database.CreateExecutionStrategy();
        (string, string, string) created;
        try
        {
        created = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
            // 1. Create Tenant
            var tenant = new Tenant
            {
                Name       = dto.CompanyName,
                Slug       = slug,
                SchemaName = TenantSlug.ToSchemaName(slug),
                IsActive   = true,
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow
            };
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();

            // 1b. Seed per-service provisioning tracker rows (Pending). These rows are the
            //     authoritative per-company service set the provisioning engine acts on.
            //     Source: the explicit Services selection if given, else the plan's FeaturesJson.
            //     "user" is always included.
            IReadOnlyList<string> seedServices;
            if (dto.Services is { Count: > 0 })
            {
                seedServices = new[] { PlatformServices.User }
                    .Concat(dto.Services
                        .Select(s => s?.Trim().ToLowerInvariant())
                        .Where(s => s is not null && PlatformServices.Business.Contains(s))
                        .Select(s => s!))
                    .Distinct()
                    .ToList();
            }
            else
            {
                string? planFeaturesJson = null;
                if (!string.IsNullOrEmpty(dto.PlanId))
                    planFeaturesJson = await context.SubscriptionPlans
                        .Where(p => p.Id == dto.PlanId)
                        .Select(p => p.FeaturesJson)
                        .FirstOrDefaultAsync();
                seedServices = PlatformServices.ResolveSubscribed(planFeaturesJson);
            }

            foreach (var serviceKey in seedServices)
            {
                context.TenantServiceSchemas.Add(new TenantServiceSchema
                {
                    TenantId   = tenant.Id,
                    ServiceKey = serviceKey,
                    SchemaName = tenant.SchemaName,
                    Status     = ProvisioningStatus.Pending,
                    CreatedAt  = DateTime.UtcNow,
                    UpdatedAt  = DateTime.UtcNow
                });
            }
            await context.SaveChangesAsync();

            // 2. Create HQ Branch
            var hqBranch = new Branch
            {
                TenantId     = tenant.Id,
                Name         = dto.HqBranchName,
                Code         = dto.HqBranchCode.ToUpper(),
                IsHeadOffice = true,
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            };
            context.Branches.Add(hqBranch);
            await context.SaveChangesAsync();

            // 3. Create Company Admin user
            var adminUser = new User
            {
                FirstName        = dto.AdminFirstName,
                LastName         = dto.AdminLastName,
                Email            = dto.AdminEmail,
                MobileNumber     = dto.AdminPhone,
                Password         = BCrypt.Net.BCrypt.HashPassword(tempPassword, workFactor: 12),
                IsActive         = true,
                IsFirstLogin     = true,
                TwoFactorEnabled = true,
                CreatedAt        = DateTime.UtcNow,
                UpdatedAt        = DateTime.UtcNow
            };
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            // 4. Assign Admin role
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Id == "role-admin");
            if (adminRole != null)
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId    = adminUser.Id,
                    RoleId    = "role-admin",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // 5. Create UserTenant — BranchId = null = company admin
            context.UserTenants.Add(new UserTenant
            {
                UserId    = adminUser.Id,
                TenantId  = tenant.Id,
                BranchId  = null,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // 6. Create Subscription (trial by default)
            if (!string.IsNullOrEmpty(dto.PlanId))
            {
                var plan = await context.SubscriptionPlans.FindAsync(dto.PlanId);
                if (plan != null)
                {
                    context.CompanySubscriptions.Add(new CompanySubscription
                    {
                        TenantId     = tenant.Id,
                        PlanId       = dto.PlanId,
                        Status       = SubscriptionStatus.Trial,
                        BillingCycle = BillingCycle.Monthly,
                        StartDate    = DateTime.UtcNow,
                        TrialEndsAt  = DateTime.UtcNow.AddDays(30),
                        CreatedAt    = DateTime.UtcNow,
                        UpdatedAt    = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();
                }
            }

            await transaction.CommitAsync();
            return (tenant.Id, adminUser.Id, hqBranch.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
        }
        catch (DbUpdateException)
        {
            return ConflictResult("A company with this slug already exists.");
        }

        // 7. Auto-provision all subscribed service schemas so the company is immediately usable
        //    (no separate "Provision Schemas" step needed). A provisioning failure is non-fatal to
        //    company creation — the company is already created and provisioning can be retried from
        //    the company detail page — but it must be visible in the response, not just server logs.
        IReadOnlyList<OrchestrationResult> provisioningResults = Array.Empty<OrchestrationResult>();
        try
        {
            provisioningResults = await orchestrator.ProvisionAllAsync(created.Item1, cancellationToken: ct);
            var failed = provisioningResults.Where(r => !r.Success).Select(r => r.ServiceKey).ToList();
            if (failed.Count > 0)
                logger.LogError("Auto-provisioning partially failed for tenant {TenantId}: [{Failed}]; retriable from the company page.",
                    created.Item1, string.Join(", ", failed));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auto-provisioning failed for tenant {TenantId}; retriable from the company page.", created.Item1);
        }

        // 8. Send welcome email (outside the retriable unit so it isn't resent on retry).
        try
        {
            var body = $"""
                <h2>Welcome to Lante — {dto.CompanyName}</h2>
                <p>Your company account has been set up on the Lante platform.</p>
                <p><strong>Login URL:</strong> https://lante.africa/login</p>
                <p><strong>Email:</strong> {dto.AdminEmail}</p>
                <p><strong>Temporary Password:</strong> {tempPassword}</p>
                <p>You will be asked to change your password on first login.</p>
                <p>Your HQ branch <strong>{dto.HqBranchName}</strong> has been created. You can add more branches from Settings.</p>
                <br/><p>Best regards,<br/>The Lante Team</p>
                """;
            await emailQueueService.EnqueueEmailAsync(dto.AdminEmail, $"Welcome to Lante — {dto.CompanyName}", body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Welcome email failed for {Email} but company was created", dto.AdminEmail);
        }

        var provisioningFailed = provisioningResults.Where(r => !r.Success).Select(r => r.ServiceKey).ToList();
        return CreatedResult(new
        {
            tenantId = created.Item1,
            adminId = created.Item2,
            hqBranchId = created.Item3,
            provisioning = new
            {
                allSucceeded = provisioningFailed.Count == 0,
                failedServices = provisioningFailed
            }
        }, "Company created successfully.");
    }

    // ── Schema Provisioning (Phase 2 engine) ───────────────────────────────────

    /// <summary>
    /// Triggers schema-per-tenant provisioning for a company across all services: the user-service
    /// schema in-process, then each business service over its internal /provision endpoint.
    /// CreateCompany itself still writes to the public plane — this is an out-of-band trigger for the
    /// new engine (it flips to run inline during CreateCompany in Phase 3).
    /// </summary>
    [HttpPost("companies/{id}/provision")]
    public async Task<IActionResult> ProvisionCompany(string id, CancellationToken ct)
    {
        var tenant = await context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant == null) return NotFoundResult("Company not found.");

        var results = await orchestrator.ProvisionAllAsync(id, cancellationToken: ct);

        var statuses = await context.TenantServiceSchemas.IgnoreQueryFilters()
            .Where(ts => ts.TenantId == id)
            .Select(ts => new { ts.ServiceKey, ts.SchemaName, status = ts.Status.ToString(), ts.ProvisionedAt, ts.LastError, ts.RetryCount })
            .ToListAsync(ct);

        var allOk = results.All(r => r.Success);
        return OkResult(new
        {
            tenantId = id,
            schema = tenant.SchemaName,
            results = results.Select(r => new { r.ServiceKey, r.Success, r.Error }),
            services = statuses
        }, allOk ? "Provisioning complete." : "Provisioning finished with errors.");
    }

    /// <summary>
    /// Retries only the services currently sitting in a non-Provisioned state for this company —
    /// far cheaper and safer than <see cref="ProvisionCompany"/>'s full re-run, which re-touches
    /// every subscribed service and relies on each one's own idempotency to no-op the healthy ones.
    /// Also the manual-intervention path once <see cref="ProvisioningRetryBackgroundService"/> has
    /// given up on a service after its retry cap — this endpoint has no such cap, since a human
    /// explicitly asking for a retry is the intervention.
    /// </summary>
    [HttpPost("companies/{id}/provision/retry-failed")]
    public async Task<IActionResult> RetryFailedProvisioning(string id, CancellationToken ct)
    {
        var tenant = await context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant == null) return NotFoundResult("Company not found.");

        var failedKeys = await context.TenantServiceSchemas.IgnoreQueryFilters()
            .Where(ts => ts.TenantId == id && ts.Status != ProvisioningStatus.Provisioned)
            .Select(ts => ts.ServiceKey)
            .ToListAsync(ct);

        if (failedKeys.Count == 0)
            return OkResult(new { tenantId = id, results = Array.Empty<object>() }, "Nothing to retry — every service is already provisioned.");

        var results = await orchestrator.ProvisionAllAsync(id, failedKeys, ct);

        var statuses = await context.TenantServiceSchemas.IgnoreQueryFilters()
            .Where(ts => ts.TenantId == id)
            .Select(ts => new { ts.ServiceKey, ts.SchemaName, status = ts.Status.ToString(), ts.ProvisionedAt, ts.LastError, ts.RetryCount })
            .ToListAsync(ct);

        var allOk = results.All(r => r.Success);
        return OkResult(new
        {
            tenantId = id,
            schema = tenant.SchemaName,
            results = results.Select(r => new { r.ServiceKey, r.Success, r.Error }),
            services = statuses
        }, allOk ? "Retry complete." : "Retry finished with errors.");
    }

    /// <summary>Per-service provisioning status for a company (drives the platform UI progress view).</summary>
    [HttpGet("companies/{id}/provisioning")]
    public async Task<IActionResult> GetProvisioningStatus(string id, CancellationToken ct)
    {
        var exists = await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == id, ct);
        if (!exists) return NotFoundResult("Company not found.");

        var statuses = await context.TenantServiceSchemas.IgnoreQueryFilters()
            .Where(ts => ts.TenantId == id)
            .Select(ts => new { ts.ServiceKey, ts.SchemaName, status = ts.Status.ToString(), ts.ProvisionedAt, ts.LastError, ts.RetryCount })
            .ToListAsync(ct);

        return OkResult(statuses);
    }

    [HttpPut("companies/{id}")]
    public async Task<IActionResult> UpdateCompany(string id, [FromBody] UpdateCompanyDto dto)
    {
        var tenant = await context.Tenants.FindAsync(id);
        if (tenant == null) return NotFoundResult("Company not found.");

        var wasActive = tenant.IsActive;

        if (dto.Name  != null) tenant.Name     = dto.Name;
        if (dto.IsActive.HasValue) tenant.IsActive = dto.IsActive.Value;
        tenant.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        if (wasActive && !tenant.IsActive)
            await NotifySuspensionAsync(tenant);

        return OkResult(new { tenant.Id, tenant.Name, tenant.IsActive });
    }

    private async Task NotifySuspensionAsync(Tenant tenant)
    {
        var admins = await context.UserTenants
            .Include(ut => ut.User)
            .Where(ut => ut.TenantId == tenant.Id && ut.BranchId == null && !ut.IsDeleted)
            .Select(ut => new { ut.User.Email, ut.User.FirstName })
            .ToListAsync();

        foreach (var admin in admins)
        {
            try
            {
                await emailQueueService.EnqueueEmailAsync(
                    admin.Email,
                    "[QSL Platform] Your account has been suspended",
                    $"<p>Dear {admin.FirstName},</p>" +
                    $"<p>Your company's account ({tenant.Name}) has been suspended, typically due to a subscription or billing issue. " +
                    "Staff will not be able to sign in until this is resolved. Please contact your account manager to restore access.</p>" +
                    "<p>— QSL Platform Team</p>");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send suspension email to {Email}", admin.Email);
            }
        }
    }

    [HttpDelete("companies/{id}")]
    public async Task<IActionResult> SuspendCompany(string id)
    {
        var tenant = await context.Tenants.FindAsync(id);
        if (tenant == null) return NotFoundResult("Company not found.");

        tenant.IsActive  = false;
        tenant.IsDeleted = true;
        tenant.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return OkResult(new { message = "Company suspended." });
    }

    // ── Subscription Plans ─────────────────────────────────────────────────────

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await context.SubscriptionPlans.IgnoreQueryFilters()
            .Select(p => new
            {
                p.Id, p.Name, p.Description, p.PriceMonthly, p.PriceAnnual,
                p.MaxBranches, p.MaxUsers, p.FeaturesJson, p.IsActive, p.StripeProductId
            })
            .ToListAsync();
        return OkResult(plans);
    }

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        var plan = new SubscriptionPlan
        {
            Name          = dto.Name,
            Description   = dto.Description,
            PriceMonthly  = dto.PriceMonthly,
            PriceAnnual   = dto.PriceAnnual,
            MaxBranches   = dto.MaxBranches,
            MaxUsers      = dto.MaxUsers,
            FeaturesJson  = dto.FeaturesJson ?? "[]",
            IsActive      = true,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow
        };
        context.SubscriptionPlans.Add(plan);
        await context.SaveChangesAsync();
        return CreatedResult(new { plan.Id, plan.Name });
    }

    [HttpPut("plans/{id}")]
    public async Task<IActionResult> UpdatePlan(string id, [FromBody] UpdatePlanDto dto)
    {
        var plan = await context.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFoundResult("Plan not found.");

        if (dto.Name         != null) plan.Name         = dto.Name;
        if (dto.Description  != null) plan.Description  = dto.Description;
        if (dto.PriceMonthly.HasValue) plan.PriceMonthly = dto.PriceMonthly.Value;
        if (dto.PriceAnnual.HasValue)  plan.PriceAnnual  = dto.PriceAnnual.Value;
        if (dto.MaxBranches.HasValue)  plan.MaxBranches  = dto.MaxBranches.Value;
        if (dto.MaxUsers.HasValue)     plan.MaxUsers     = dto.MaxUsers.Value;
        if (dto.FeaturesJson != null) plan.FeaturesJson = dto.FeaturesJson;
        if (dto.IsActive.HasValue)    plan.IsActive     = dto.IsActive.Value;
        plan.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return OkResult(new { plan.Id, plan.Name, plan.IsActive });
    }

    [HttpDelete("plans/{id}")]
    public async Task<IActionResult> DeletePlan(string id)
    {
        var plan = await context.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFoundResult("Plan not found.");

        var inUse = await context.CompanySubscriptions.AnyAsync(s => s.PlanId == id);
        if (inUse) return ConflictResult("Plan is in use by one or more companies.");

        plan.IsDeleted  = true;
        plan.UpdatedAt  = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return OkResult(new { message = "Plan deleted." });
    }

    // ── Subscriptions per Company ──────────────────────────────────────────────

    [HttpPut("companies/{id}/subscription")]
    public async Task<IActionResult> UpdateSubscription(string id, [FromBody] UpdateSubscriptionDto dto)
    {
        var sub = await context.CompanySubscriptions
            .Where(s => s.TenantId == id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (sub == null) return NotFoundResult("No subscription found for this company.");

        if (dto.PlanId   != null) sub.PlanId       = dto.PlanId;
        if (dto.Status.HasValue)  sub.Status        = dto.Status.Value;
        if (dto.EndDate.HasValue) sub.EndDate       = dto.EndDate;
        sub.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return OkResult(new { sub.Id, sub.Status, sub.PlanId });
    }

    // ── Broadcast Notifications ────────────────────────────────────────────────

    // Feeds the "specific admins" picker in the broadcast UI — every active company admin,
    // across every tenant, in one flat list.
    [HttpGet("admins")]
    public async Task<IActionResult> GetAdmins()
    {
        var admins = await context.UserTenants
            .Include(ut => ut.User)
            .Include(ut => ut.Tenant)
            .Where(ut => ut.BranchId == null && ut.Tenant.IsActive && !ut.IsDeleted)
            .Select(ut => new
            {
                ut.User.Id, ut.User.Email, ut.User.FirstName, ut.User.LastName,
                ut.TenantId, TenantName = ut.Tenant.Name,
            })
            .OrderBy(a => a.TenantName).ThenBy(a => a.FirstName)
            .ToListAsync();

        return OkResult(admins);
    }

    [HttpPost("broadcast")]
    public async Task<IActionResult> BroadcastNotification([FromBody] BroadcastDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        // Get all active company admins
        var admins = await context.UserTenants
            .Include(ut => ut.User)
            .Include(ut => ut.Tenant)
            .Where(ut => ut.BranchId == null && ut.Tenant.IsActive && !ut.IsDeleted)
            .Select(ut => new { ut.TenantId, ut.User.Id, ut.User.Email, ut.User.FirstName, ut.User.LastName, TenantName = ut.Tenant.Name, SchemaName = ut.Tenant.SchemaName })
            .ToListAsync();

        // Target specific admins if specified — takes precedence over TenantIds, since picking
        // named people is a narrower ask than picking whole companies.
        if (dto.UserIds?.Any() == true)
            admins = admins.Where(a => dto.UserIds.Contains(a.Id)).ToList();
        else if (dto.TenantIds?.Any() == true)
            admins = admins.Where(a => dto.TenantIds.Contains(a.TenantId)).ToList();

        var ticketingBaseUrl = configuration["ProvisioningTargets:ticketing"];
        var serviceKey = configuration["InternalServices:ServiceKey"];

        var broadcast = new BroadcastMessage
        {
            Subject      = dto.Subject,
            Body         = dto.Body,
            SentByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            SentByName   = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Platform Admin",
            TotalRecipients = admins.Count,
            SentAt       = DateTime.UtcNow,
        };
        context.BroadcastMessages.Add(broadcast);

        var sent = 0;
        var notified = 0;
        var notifyErrors = new List<string>();
        foreach (var admin in admins)
        {
            var recipient = new BroadcastRecipient
            {
                BroadcastMessageId = broadcast.Id,
                UserId     = admin.Id,
                UserName   = $"{admin.FirstName} {admin.LastName}".Trim(),
                Email      = admin.Email,
                TenantId   = admin.TenantId,
                TenantName = admin.TenantName,
                SchemaName = admin.SchemaName,
            };

            try
            {
                await emailQueueService.EnqueueEmailAsync(
                    admin.Email,
                    $"[QSL Platform] {dto.Subject}",
                    $"<p>Dear {admin.FirstName},</p>{dto.Body}<br/><p>— QSL Platform Team</p>");
                sent++;
                recipient.EmailSent = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send broadcast email to {Email}", admin.Email);
            }

            // The in-app Notification table is owned by ticketing-service and lives in each
            // tenant's own schema — there's no JWT on this service-to-service call to resolve
            // it from, so the recipient's schema is set explicitly via X-Tenant-Schema.
            // Best-effort: one admin's failure here shouldn't abort the rest of the broadcast.
            if (!string.IsNullOrEmpty(ticketingBaseUrl))
            {
                try
                {
                    var client = httpClientFactory.CreateClient("provisioning");
                    client.BaseAddress = new Uri(ticketingBaseUrl.TrimEnd('/') + "/");
                    if (!string.IsNullOrEmpty(serviceKey))
                        client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
                    client.DefaultRequestHeaders.Add("X-Tenant-Schema", admin.SchemaName);

                    var response = await client.PostAsJsonAsync("internal/notifications", new
                    {
                        userId = admin.Id,
                        type = "platform_broadcast",
                        message = $"{dto.Subject}: {dto.Body}",
                    });

                    // PostAsJsonAsync only throws on network failure, NOT on HTTP error status
                    // codes — an auth/routing/validation rejection from ticketing-service would
                    // otherwise pass through here completely silently.
                    if (response.IsSuccessStatusCode)
                    {
                        notified++;
                        recipient.NotificationSent = true;
                        var parsed = await response.Content.ReadFromJsonAsync<InternalNotifyResponse>();
                        recipient.NotificationId = parsed?.notificationId;
                    }
                    else
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        var msg = $"{admin.Email}: HTTP {(int)response.StatusCode} — {body}";
                        logger.LogWarning("Failed to create in-app notification for {Email}: HTTP {Status} — {Body}", admin.Email, (int)response.StatusCode, body);
                        notifyErrors.Add(msg);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to create in-app notification for {Email}", admin.Email);
                    notifyErrors.Add($"{admin.Email}: {ex.Message}");
                }
            }

            context.BroadcastRecipients.Add(recipient);
        }

        broadcast.EmailsSent = sent;
        broadcast.NotificationsSent = notified;
        await context.SaveChangesAsync();

        return OkResult(new { broadcastId = broadcast.Id, sent, total = admins.Count, notified, notifyErrors });
    }

    [HttpGet("broadcasts")]
    public async Task<IActionResult> GetBroadcasts()
    {
        var broadcasts = await context.BroadcastMessages
            .OrderByDescending(b => b.SentAt)
            .Take(50)
            .Select(b => new { b.Id, b.Subject, b.SentByName, b.TotalRecipients, b.EmailsSent, b.NotificationsSent, b.SentAt })
            .ToListAsync();

        return OkResult(broadcasts);
    }

    [HttpGet("broadcasts/{id}")]
    public async Task<IActionResult> GetBroadcast(string id)
    {
        var broadcast = await context.BroadcastMessages.FindAsync(id);
        if (broadcast == null) return NotFoundResult("Broadcast not found.");

        var recipients = await context.BroadcastRecipients
            .Where(r => r.BroadcastMessageId == id)
            .ToListAsync();

        var ticketingBaseUrl = configuration["ProvisioningTargets:ticketing"];
        var serviceKey = configuration["InternalServices:ServiceKey"];

        // Live read-status lookup — the Notification row (and its IsRead flag) lives in
        // ticketing-service's per-tenant schema, not here, so this is a real-time check rather
        // than something we can just read off our own recipient row.
        var recipientStatuses = new List<object>();
        foreach (var r in recipients)
        {
            bool? isRead = null;
            if (!string.IsNullOrEmpty(r.NotificationId) && !string.IsNullOrEmpty(ticketingBaseUrl))
            {
                try
                {
                    var client = httpClientFactory.CreateClient("provisioning");
                    client.BaseAddress = new Uri(ticketingBaseUrl.TrimEnd('/') + "/");
                    if (!string.IsNullOrEmpty(serviceKey))
                        client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
                    client.DefaultRequestHeaders.Add("X-Tenant-Schema", r.SchemaName);

                    var response = await client.GetAsync($"internal/notifications/{r.NotificationId}");
                    if (response.IsSuccessStatusCode)
                    {
                        var parsed = await response.Content.ReadFromJsonAsync<InternalReadStatusResponse>();
                        isRead = parsed?.isRead;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to check read status for recipient {Email}", r.Email);
                }
            }

            recipientStatuses.Add(new
            {
                r.UserName, r.Email, r.TenantName, r.EmailSent, r.NotificationSent,
                isRead,
            });
        }

        return OkResult(new
        {
            broadcast.Id, broadcast.Subject, broadcast.Body, broadcast.SentByName, broadcast.SentAt,
            broadcast.TotalRecipients, broadcast.EmailsSent, broadcast.NotificationsSent,
            recipients = recipientStatuses,
        });
    }

    private record InternalNotifyResponse(bool success, string? notificationId);
    private record InternalReadStatusResponse(bool isRead, DateTime? readAt);
}

// ── DTOs ───────────────────────────────────────────────────────────────────────

public record CreateCompanyDto(
    string CompanyName,
    string Slug,
    string HqBranchName,
    string HqBranchCode,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string AdminPhone,
    string? PlanId,
    // Optional per-company business-service selection (keys from PlatformServices.Business).
    // When omitted, the services default to the plan's FeaturesJson.
    List<string>? Services = null);

public record UpdateCompanyDto(string? Name, bool? IsActive);

public record CreatePlanDto(
    string Name,
    string Description,
    decimal PriceMonthly,
    decimal PriceAnnual,
    int MaxBranches,
    int MaxUsers,
    string? FeaturesJson);

public record UpdatePlanDto(
    string? Name,
    string? Description,
    decimal? PriceMonthly,
    decimal? PriceAnnual,
    int? MaxBranches,
    int? MaxUsers,
    string? FeaturesJson,
    bool? IsActive);

public record UpdateSubscriptionDto(
    string? PlanId,
    SubscriptionStatus? Status,
    DateTime? EndDate);

public record BroadcastDto(
    string Subject,
    string Body,
    List<string>? TenantIds,
    List<string>? UserIds = null);
