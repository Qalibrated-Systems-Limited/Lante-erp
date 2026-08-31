using Microsoft.AspNetCore.Mvc;
using ProcurementService.Api.Authorization;
using ProcurementService.Core.DTOs.PurchaseOrders;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P5 (DEC-3) — service-to-service ingest from the Stores service. Internal-key auth; the tenant
/// schema is resolved from the <c>X-Tenant-Schema</c> header the caller forwards. When a Stores GRN raised
/// against an LPO passes inspection, Stores posts the received quantities here to close/update the PO line.</summary>
[ApiController]
[Route("internal/procurement")]
[ServiceKeyAuthorize]
public class InternalProcurementController(IPurchaseOrderService pos, ILogger<InternalProcurementController> logger)
    : ControllerBase
{
    [HttpPost("purchase-orders/{poId}/receipt")]
    public async Task<IActionResult> RecordReceipt(string poId, [FromBody] RecordReceiptDto dto)
    {
        var r = await pos.RecordReceiptAsync(poId, dto, "stores-grn");
        logger.LogInformation("Stores receipt ingest for LPO {Po}: {Msg}", poId, r.Message);
        return r.Status == "Error" ? NotFound(new { message = r.Message }) : Ok(new { data = r });
    }
}
