using Microsoft.AspNetCore.Mvc;
using CrmService.Api.Authorization;
using CrmService.Core.DTOs.Customers;
using CrmService.Core.DTOs.Integrations;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

/// <summary>
/// D8 — service-to-service seam with the ticketing service (Module 7). Internal-key auth; the tenant
/// schema is resolved from the <c>X-Tenant-Schema</c> header the caller forwards (no JWT). D8-2/D8-3
/// ingest is best-effort (always 200 with a match flag so a CRM-side miss never breaks ticket handling);
/// D8-1 exposes the CRM customer master read so ticketing's create-ticket picker can source from it.
/// </summary>
[ApiController]
[Route("internal/crm")]
[ServiceKeyAuthorize]
public class InternalCrmSyncController(
    ICrmIngestService ingest,
    ICustomerService customers,
    ILogger<InternalCrmSyncController> logger)
    : ControllerBase
{
    private const string SyncActor = "ticketing-sync";

    // ── D8-1 — CRM customer master read (for ticketing's ticket-create picker) ──
    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] string? search)
    {
        var res = await customers.GetAllAsync(new CustomerFilterParams { Search = search, PageSize = 25 });
        return Ok(new { data = res.Items.Select(c => new { c.Id, c.Name, c.Email, c.Phone, c.Status }) });
    }

    [HttpGet("customers/{id}")]
    public async Task<IActionResult> Customer(string id)
    {
        var c = await customers.GetByIdAsync(id);
        return c is null
            ? NotFound(new { message = "Customer not found." })
            : Ok(new { data = new { c.Id, c.Name, c.Email, c.Phone, c.ClientReference } });
    }

    [HttpPost("customer-interactions")]
    public async Task<IActionResult> CustomerInteraction([FromBody] CustomerInteractionIngestDto dto)
    {
        var r = await ingest.RecordCustomerInteractionAsync(dto, SyncActor);
        logger.LogInformation("Ticketing interaction ingest ({Ref}): {Msg}", dto.TicketReference, r.Message);
        return Ok(new { r.Matched, r.Message });
    }

    [HttpPost("complaint-closures")]
    public async Task<IActionResult> ComplaintClosure([FromBody] ComplaintClosureIngestDto dto)
    {
        var r = await ingest.RecordComplaintClosureAsync(dto, SyncActor);
        logger.LogInformation("Ticketing complaint ingest ({Ref}): {Msg}", dto.TicketReference, r.Message);
        return Ok(new { r.Matched, r.Message });
    }
}
