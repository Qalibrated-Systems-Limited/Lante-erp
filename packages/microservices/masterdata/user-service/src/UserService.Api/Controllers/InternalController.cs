using Microsoft.AspNetCore.Mvc;
using UserService.Api.Authorization;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;
using System.Linq;

namespace UserService.Api.Controllers;

/// <summary>
/// Service-to-service endpoints, authenticated by the shared internal key (not JWT).
/// This controller is the TEMPLATE the four business services will copy: the orchestrator in
/// user-service calls each subscribed service's <c>POST /internal/tenants/provision</c> to create
/// and migrate that service's tenant schema. For user-service itself, provisioning also runs
/// in-process via the platform trigger — this endpoint lets it be re-run out of band.
/// </summary>
[ApiController]
[Route("internal/tenants")]
[ServiceKeyAuthorize]
public class InternalController(
    ITenantProvisioningService provisioning,
    ITenantRepository tenantRepository,
    IEmailSettingsService emailSettingsService,
    ISystemSettingService systemSettingService,
    ILogger<InternalController> logger)
    : ControllerBase
{
    [HttpPost("provision")]
    public async Task<IActionResult> Provision([FromBody] ProvisionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            return BadRequest(new { message = "tenantId is required." });

        logger.LogInformation("Internal provision requested for tenant {TenantId}", request.TenantId);
        var result = await provisioning.ProvisionUserSchemaAsync(request.TenantId, ct);

        return result.Success
            ? Ok(new { result.Success, result.ServiceKey, result.Schema })
            : StatusCode(StatusCodes.Status500InternalServerError,
                new { result.Success, result.ServiceKey, result.Schema, result.Error });
    }

    // Polled by the gateway (short-TTL cached there) so a suspended tenant's already-issued
    // JWTs stop working within seconds, not just at their next login.
    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(string id)
    {
        var tenant = await tenantRepository.GetByIdAsync(id);
        if (tenant == null) return NotFound(new { message = "Tenant not found." });
        return Ok(new { isActive = tenant.IsActive });
    }

    // Called by other services (ticketing) to resolve the public portal's :slug route segment into
    // a real tenant before touching any tenant-scoped data — e.g. so an anonymous /portal/:slug
    // submission lands in that company's own schema instead of the caller's own (unvalidated)
    // say-so. Deliberately returns SchemaName here (unlike the public-facing tenants/public/{slug}
    // endpoint, which only exposes Name/Slug) — this is internal-key-gated, and the caller needs the
    // schema to actually route into. Callers must treat the slug as untrusted input; this endpoint
    // does the validation and only ever returns a schema that's really this tenant's own.
    [HttpGet("by-slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var tenant = await tenantRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant());
        if (tenant == null || !tenant.IsActive) return NotFound(new { message = "Tenant not found." });
        return Ok(new { tenant.Id, tenant.Name, tenant.Slug, tenant.SchemaName, tenant.IsActive });
    }

    // Called by any other service before sending an email on a tenant's behalf. The caller sets
    // X-Tenant-Schema on this request from ITS OWN resolved tenant context (matching the schema
    // the caller is currently operating in); TenantDbConnectionInterceptor routes this query to
    // that schema since the request already passed [ServiceKeyAuthorize]'s X-Internal-Key check.
    // Returns 204 (not a null body) when the tenant hasn't configured/enabled custom SMTP, so
    // callers can tell "use the platform default" apart from "the call itself failed".
    [HttpGet("email-settings")]
    public async Task<IActionResult> GetEmailSettings()
    {
        var settings = await emailSettingsService.GetForInternalAsync();
        return settings is null ? NoContent() : Ok(settings);
    }

    // Called by other services (operations, for calibration certificate PDFs) to render each
    // tenant's own company identity instead of a hardcoded brand string. Same X-Tenant-Schema
    // trust model as email-settings above. Returns 204 when the tenant hasn't set a legal name —
    // callers fall back to their own default identity in that case.
    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding()
    {
        var settings = (await systemSettingService.GetAllAsync()).ToDictionary(s => s.Key, s => s.Value);
        string? Get(string key) => settings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        var legalName = Get("company.legal_name") ?? Get("branding.company_display_name");
        if (legalName is null) return NoContent();

        return Ok(new
        {
            legalName,
            displayName = Get("branding.company_display_name"),
            address = Get("company.address"),
            phone = Get("company.phone"),
            email = Get("company.email"),
            docCodePrefix = Get("branding.doc_code_prefix"),
        });
    }

    public record ProvisionRequest(string TenantId);
}
