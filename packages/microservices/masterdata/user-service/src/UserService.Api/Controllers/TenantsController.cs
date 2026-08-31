using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/tenants")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize]
public class TenantsController(ITenantRepository tenantRepository, ISystemSettingService settingService) : BaseController
{
    // GET /api/v1/tenants — global tenant listing, platform-level only (tenant users must
    // not be able to enumerate other customers; the platform UI uses /api/v1/platform/companies)
    [HttpGet]
    [Authorize(Roles = "Platform Admin")]
    public async Task<IActionResult> GetTenants()
    {
        var tenants = await tenantRepository.GetAllTenantsAsync();
        return OkResult(tenants.Select(t => new { t.Id, t.Name, t.Slug, t.IsActive }));
    }

    // GET /api/v1/tenants/{id}/branches — every caller is an authenticated page (departments,
    // settings, users); drops to the class-level [Authorize] (#388). The AllowAnonymous here used
    // to work around a 401 from the old Ocelot gateway's claim header transform — that gateway is
    // long gone, and YARP's ClaimsToHeaderTransformProvider silently skips a missing claim instead
    // of failing the request, so the original problem this was working around no longer exists.
    [HttpGet("{id}/branches")]
    public async Task<IActionResult> GetBranches(string id)
    {
        var tenant = await tenantRepository.GetByIdAsync(id);
        if (tenant == null) return NotFoundResult("Tenant not found.");

        var branches = await tenantRepository.GetBranchesByTenantAsync(id);
        return OkResult(branches.Select(b => new { b.Id, b.Name, b.Code, b.IsHeadOffice, b.IsActive }));
    }

    // GET /api/v1/tenants/public/{slug} — AllowAnonymous. Backs the public portal's usePortalTenant
    // hook: resolves a :slug route segment into the handful of fields safe to show an anonymous
    // visitor (name, logo) before the tenant is otherwise identifiable. The client-supplied slug is
    // untrusted; only the schema this lookup itself resolves from the real Tenants table — never a
    // client-suppliable value — is used to read the tenant's own branding.logo_url setting, the same
    // safe pattern GetBySlug (InternalController) already uses for service-to-service calls.
    [HttpGet("public/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicBranding(string slug)
    {
        var tenant = await tenantRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant());
        if (tenant == null || !tenant.IsActive) return NotFoundResult("Tenant not found.");

        Request.Headers["X-Tenant-Schema"] = tenant.SchemaName;
        var settings = (await settingService.GetAllAsync()).ToDictionary(s => s.Key, s => s.Value);

        return OkResult(new
        {
            name = tenant.Name,
            slug = tenant.Slug,
            logoUrl = settings.GetValueOrDefault("branding.logo_url", "")
        });
    }

    // POST /api/v1/tenants
    [HttpPost]
    [Authorize(Roles = "Platform Admin")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        var tenant = new Tenant
        {
            Name     = dto.Name,
            Slug     = dto.Slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await tenantRepository.CreateTenantAsync(tenant);
        return CreatedResult(new { created.Id, created.Name, created.Slug });
    }

    // POST /api/v1/tenants/{id}/branches
    [HttpPost("{id}/branches")]
    [Authorize(Roles = "Platform Admin")]
    public async Task<IActionResult> CreateBranch(string id, [FromBody] CreateBranchDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        var tenant = await tenantRepository.GetByIdAsync(id);
        if (tenant == null) return NotFoundResult("Tenant not found.");

        var branch = new Branch
        {
            TenantId     = id,
            Name         = dto.Name,
            Code         = dto.Code,
            IsHeadOffice = dto.IsHeadOffice,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow
        };

        var created = await tenantRepository.CreateBranchAsync(branch);
        return CreatedResult(new { created.Id, created.Name, created.Code, created.IsHeadOffice });
    }

    // POST /api/v1/tenants/{id}/members
    [HttpPost("{id}/members")]
    [Authorize(Roles = "Platform Admin")]
    public async Task<IActionResult> AddUserToTenant(string id, [FromBody] AddUserToTenantDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        var existing = await tenantRepository.GetUserTenantAsync(dto.UserId, id);
        if (existing != null) return ConflictResult("User already belongs to this tenant.");

        var userTenant = new UserTenant
        {
            UserId    = dto.UserId,
            TenantId  = id,
            BranchId  = dto.BranchId,
            IsDefault = dto.IsDefault,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await tenantRepository.CreateUserTenantAsync(userTenant);
        return CreatedResult(new { created.Id, created.UserId, created.TenantId, created.BranchId });
    }
}

public record CreateTenantDto(string Name, string Slug);
public record CreateBranchDto(string Name, string Code, bool IsHeadOffice = false);
public record AddUserToTenantDto(string UserId, string? BranchId, bool IsDefault = true);
