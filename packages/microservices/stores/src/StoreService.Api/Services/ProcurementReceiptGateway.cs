using System.Text;
using System.Text.Json;
using StoreService.Core.Interfaces.Services;

namespace StoreService.Api.Services;

/// <summary>
/// P5 (DEC-3) — REAL Procurement receipt callback. When a GRN linked to an LPO passes inspection, POSTs the
/// received quantities to procurement's internal receipt endpoint so the PO line is closed/updated. Service
/// -to-service auth via the shared <c>X-Internal-Key</c>; tenant carried on <c>X-Tenant-Schema</c> forwarded
/// from the ambient request (JWT schema claim or header). Config-gated on <c>Procurement:Enabled</c> +
/// <c>ProcurementService:BaseUrl</c>; never throws.
/// </summary>
public class ProcurementReceiptGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ProcurementReceiptGateway> logger) : IProcurementReceiptGateway
{
    private string? BaseUrl => config["ProcurementService:BaseUrl"]?.TrimEnd('/');
    private bool Enabled =>
        config.GetValue("Procurement:Enabled", false) && !string.IsNullOrWhiteSpace(BaseUrl);

    public async Task NotifyReceiptAsync(PoReceiptNotice notice, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(notice.PoId)) return;
        var serviceKey = config["InternalServices:ServiceKey"];
        try
        {
            var client = httpClientFactory.CreateClient("ProcurementService");
            if (!string.IsNullOrWhiteSpace(serviceKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            var ctx = httpContextAccessor.HttpContext;
            var schema = ctx?.User.FindFirst("schema")?.Value
                         ?? ctx?.Request.Headers["X-Tenant-Schema"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(schema))
                client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var body = new
            {
                acceptedQty = notice.AcceptedQty,
                rejectedQty = notice.RejectedQty,
                partialDelivery = notice.PartialDelivery,
                grnId = notice.GrnId,
                source = "StoreService",
            };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{BaseUrl}/internal/procurement/purchase-orders/{notice.PoId}/receipt", content, ct);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Procurement receipt callback returned {Status} for PO {Po}.", resp.StatusCode, notice.PoId);
            else
                logger.LogInformation("Notified procurement of receipt against LPO {Po} (accepted {Acc}).", notice.PoId, notice.AcceptedQty);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Procurement receipt callback failed for PO {Po} (best-effort).", notice.PoId);
        }
    }
}
