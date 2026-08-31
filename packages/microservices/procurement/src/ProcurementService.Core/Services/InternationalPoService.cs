using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.DTOs.International;
using ProcurementService.Core.Entities;
using ProcurementService.Core.Enums;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Core.Services;

/// <summary>
/// P7 (PROC-003) — international sourcing. Adds the foreign-currency layer to an existing LPO: FX terms, the
/// T/T advance with its mandatory MD approval, shipment tracking, the customs entry, and the landed-cost
/// roll-up.
/// <para><b>Landed cost</b> = (purchase price + freight + import duty + clearing + port and handling) ÷
/// quantity received. Every amount is converted to KES at the rate prevailing when that cost was incurred, so
/// each component stores its own rate. Rates come from Finance; when Finance cannot be reached the caller must
/// supply the rate explicitly — nothing is ever converted at a guessed rate.</para>
/// <para><b>Customs is a source document, not a separate total.</b> Lodging a declaration creates/refreshes
/// the import-duty, clearing and port-charge components from it, so the roll-up sums components only and
/// customs charges can never be double-counted.</para>
/// </summary>
public class InternationalPoService(
    IGenericRepository<InternationalPo> intlPos,
    IGenericRepository<LandedCostComponent> components,
    IGenericRepository<CustomsDeclaration> customs,
    IGenericRepository<PurchaseOrder> pos,
    IGenericRepository<ProcurementAuditLog> audit,
    IPurchaseOrderService purchaseOrders,
    ICurrencyGateway currencies,
    IPaymentVoucherGateway vouchers,
    IMapper mapper) : IInternationalPoService
{
    private const string BaseCurrency = "KES";

    // ── Reads ──
    public async Task<IntlListResult> GetAllAsync(IntlFilterParams filter)
    {
        var q = intlPos.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<IntlPoStatus>(filter.Status, true, out var st))
            q = q.Where(x => x.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.CurrencyCode))
            q = q.Where(x => x.CurrencyCode == filter.CurrencyCode);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return new IntlListResult(mapper.Map<List<IntlPoRowDto>>(items), total);
    }

    public async Task<IntlPoReadDto?> GetByIdAsync(string id)
    {
        var po = await intlPos.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return po is null ? null : await ToDtoAsync(po);
    }

    public async Task<IntlPoReadDto?> GetByPoAsync(string poId)
    {
        var po = await intlPos.Query().AsNoTracking().FirstOrDefaultAsync(x => x.PoId == poId);
        return po is null ? null : await ToDtoAsync(po);
    }

    public async Task<IntlSummaryDto> GetSummaryAsync()
    {
        var all = await intlPos.Query().AsNoTracking().ToListAsync();
        return new IntlSummaryDto
        {
            Total = all.Count,
            AwaitingTtApproval = all.Count(x => x.TtRequestedAt != null && x.TtApprovedAt == null),
            TtApprovedNotSent = all.Count(x => x.TtApprovedAt != null && x.TtSentAt == null),
            InTransit = all.Count(x => x.Status == IntlPoStatus.Shipped),
            AwaitingCustoms = all.Count(x => x.Status == IntlPoStatus.Shipped && x.BlNumber != null),
            Costed = all.Count(x => x.Status == IntlPoStatus.Costed),
            TotalLandedValueKes = all.Sum(x => x.TotalLandedCostKes),
            TtOutstandingKes = all.Where(x => x.TtApprovedAt != null && x.TtSentAt == null)
                                  .Sum(x => (x.TtAmountFx ?? 0) * x.ExchangeRateAtOrder),
        };
    }

    public async Task<List<CurrencyRateDto>> GetCurrenciesAsync()
    {
        var list = await currencies.ListAsync();
        return list.Select(c => new CurrencyRateDto(c.Code, c.Name, c.Rate, true)).ToList();
    }

    // ── Create ──
    public async Task<IntlActionResult> CreateAsync(CreateIntlPoDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.PoId)) return Err("Select the LPO this order belongs to.");
        if (string.IsNullOrWhiteSpace(dto.CurrencyCode)) return Err("Select the order currency.");
        if (dto.PurchasePriceFx <= 0) return Err("The foreign-currency purchase price must be greater than zero.");
        if (dto.QuantityOrdered <= 0) return Err("The ordered quantity is needed to compute a per-unit landed cost.");

        var po = await pos.GetByIdAsync(dto.PoId);
        if (po is null) return Err("LPO not found.");
        if (await intlPos.Query().AnyAsync(x => x.PoId == dto.PoId))
            return Err("This LPO already has international sourcing detail.");

        var (rate, rateErr) = await ResolveRateAsync(dto.CurrencyCode, dto.ExchangeRate);
        if (rateErr is not null) return Err(rateErr);

        var kes = Round2(dto.PurchasePriceFx * rate);
        var intl = new InternationalPo
        {
            PoId = po.Id,
            PoNumber = po.PoNumber,
            SupplierId = po.SupplierId,
            SupplierName = po.SupplierName,
            CurrencyCode = dto.CurrencyCode.ToUpperInvariant(),
            PurchasePriceFx = dto.PurchasePriceFx,
            ExchangeRateAtOrder = rate,
            PurchasePriceKes = kes,
            ProformaInvoiceUrl = dto.ProformaInvoiceUrl,
            QuantityBasis = dto.QuantityOrdered,
            TotalLandedCostKes = kes,
            LandedCostPerUnitKes = Round2(kes / dto.QuantityOrdered),
            Status = string.IsNullOrWhiteSpace(dto.ProformaInvoiceUrl) ? IntlPoStatus.Draft : IntlPoStatus.ProformaReceived,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        var created = await intlPos.CreateAsync(intl);

        // Restating the LPO's KES value goes through the LPO service so the value-based approval authority is
        // re-derived with it (and refused once anyone has signed). Where it declines — issued LPO, approval
        // already under way — the FX figure is simply recorded for costing, which is the honest outcome.
        // The LPO's value stays denominated in KES — the authority thresholds are KES and the foreign price
        // lives here on the international record (CurrencyCode + PurchasePriceFx).
        var restate = await purchaseOrders.RestateValueAsync(po.Id, kes, BaseCurrency, userId);
        var note = restate.Status switch
        {
            "Restated" => $" {restate.Message}",
            "NotRestated" => $" LPO value left as is: {restate.Message}",
            _ => "",
        };

        await LogAsync(created.Id, AsrAuditAction.IntlPoCreated,
            $"International sourcing attached to {po.PoNumber}: {dto.PurchasePriceFx:N2} {intl.CurrencyCode} @ {rate:N4} = {kes:N2} KES.{note}", userId);
        return new IntlActionResult("Created", $"International detail captured for {po.PoNumber}.{note}", created.Id);
    }

    public async Task<IntlActionResult> UpdateShipmentAsync(string id, ShipmentDto dto, string userId)
    {
        var intl = await intlPos.GetByIdAsync(id);
        if (intl is null) return Err("International order not found.");

        if (dto.BlNumber is not null) intl.BlNumber = dto.BlNumber;
        if (dto.Eta is not null) intl.Eta = dto.Eta;
        if (dto.ProformaInvoiceUrl is not null) intl.ProformaInvoiceUrl = dto.ProformaInvoiceUrl;

        if (!string.IsNullOrWhiteSpace(intl.BlNumber) && intl.Status < IntlPoStatus.Shipped)
            intl.Status = IntlPoStatus.Shipped;
        else if (!string.IsNullOrWhiteSpace(intl.ProformaInvoiceUrl) && intl.Status == IntlPoStatus.Draft)
            intl.Status = IntlPoStatus.ProformaReceived;

        Touch(intl, userId);
        await intlPos.UpdateAsync(intl);
        await LogAsync(id, AsrAuditAction.IntlShipmentUpdated,
            $"Shipment updated for {intl.PoNumber}: BL {intl.BlNumber ?? "—"}, ETA {(intl.Eta?.ToString("yyyy-MM-dd") ?? "—")}.", userId);
        return new IntlActionResult("Ok", "Shipment details updated.", id);
    }

    // ── T/T advance (PROC-003 key control: MD approval before funds move) ──
    public async Task<IntlActionResult> RequestTtAsync(string id, RequestTtDto dto, string userId)
    {
        var intl = await intlPos.GetByIdAsync(id);
        if (intl is null) return Err("International order not found.");
        if (dto.AmountFx <= 0) return Err("The advance amount must be greater than zero.");
        if (dto.AmountFx > intl.PurchasePriceFx)
            return Err($"The advance cannot exceed the order value of {intl.PurchasePriceFx:N2} {intl.CurrencyCode}.");
        if (intl.TtSentAt != null) return Err("The advance has already been sent.");

        var po = await pos.GetByIdAsync(intl.PoId);
        if (po is null || po.Status != PoStatus.Issued)
            return Err("The LPO must be issued before an advance can be requested.");

        intl.TtAmountFx = dto.AmountFx;
        intl.TtRequestedAt = DateTime.UtcNow;
        intl.TtApprovedBy = null;
        intl.TtApprovedAt = null;
        Touch(intl, userId);
        await intlPos.UpdateAsync(intl);
        await LogAsync(id, AsrAuditAction.IntlPoCreated,
            $"T/T advance of {dto.AmountFx:N2} {intl.CurrencyCode} requested for {intl.PoNumber} — awaiting MD approval.", userId);
        return new IntlActionResult("Requested", "Advance requested — awaiting MD approval.", id);
    }

    public async Task<IntlActionResult> ApproveTtAsync(string id, string userId)
    {
        var intl = await intlPos.GetByIdAsync(id);
        if (intl is null) return Err("International order not found.");
        if (intl.TtRequestedAt is null) return Err("No advance has been requested for this order.");
        if (intl.TtApprovedAt != null) return Err("This advance is already approved.");

        intl.TtApprovedBy = userId;
        intl.TtApprovedAt = DateTime.UtcNow;
        if (intl.Status < IntlPoStatus.TtApproved) intl.Status = IntlPoStatus.TtApproved;
        Touch(intl, userId);
        await intlPos.UpdateAsync(intl);
        await LogAsync(id, AsrAuditAction.IntlTtApproved,
            $"MD approved the T/T advance of {intl.TtAmountFx:N2} {intl.CurrencyCode} for {intl.PoNumber}.", userId);
        return new IntlActionResult("TtApproved", "Advance approved — funds may now be transferred.", id);
    }

    public async Task<IntlActionResult> SendTtAsync(string id, string userId)
    {
        var intl = await intlPos.GetByIdAsync(id);
        if (intl is null) return Err("International order not found.");
        if (intl.TtApprovedAt is null)
            return Err("The MD must approve this advance before funds are transferred.");
        if (intl.TtSentAt != null) return Err("The advance has already been sent.");

        var amountKes = Round2((intl.TtAmountFx ?? 0) * intl.ExchangeRateAtOrder);
        var r = await vouchers.RaiseAdHocAsync(
            intl.SupplierName ?? "Foreign supplier", amountKes, $"T/T advance — {intl.PoNumber}");
        if (!r.Raised) return Err(r.Message);

        intl.TtVoucherRef = r.VoucherId;
        intl.TtVoucherNo = r.VoucherNo;
        intl.TtSentAt = DateTime.UtcNow;
        if (intl.Status < IntlPoStatus.TtSent) intl.Status = IntlPoStatus.TtSent;
        Touch(intl, userId);
        await intlPos.UpdateAsync(intl);
        await LogAsync(id, AsrAuditAction.IntlTtSent,
            $"T/T advance of {intl.TtAmountFx:N2} {intl.CurrencyCode} ({amountKes:N2} KES) sent for {intl.PoNumber}. {r.Message}", userId);
        return new IntlActionResult("TtSent", r.Message, id);
    }

    // ── Landed cost ──
    public async Task<IntlActionResult> AddComponentAsync(string id, AddComponentDto dto, string userId)
    {
        var intl = await intlPos.GetByIdAsync(id);
        if (intl is null) return Err("International order not found.");
        if (!Enum.TryParse<LandedCostComponentType>(dto.ComponentType, true, out var type))
            return Err($"Unknown cost component '{dto.ComponentType}'.");
        if (dto.AmountFx <= 0) return Err("The component amount must be greater than zero.");

        var ccy = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? BaseCurrency : dto.CurrencyCode.ToUpperInvariant();
        var (rate, rateErr) = await ResolveRateAsync(ccy, dto.ExchangeRate);
        if (rateErr is not null) return Err(rateErr);

        await components.CreateAsync(new LandedCostComponent
        {
            IntlPoId = intl.Id,
            ComponentType = type,
            AmountFx = dto.AmountFx,
            CurrencyCode = ccy,
            ExchangeRate = rate,
            KesAmount = Round2(dto.AmountFx * rate),
            IncurredOn = dto.IncurredOn ?? DateTime.UtcNow,
            BlNumber = dto.BlNumber ?? intl.BlNumber,
            Eta = dto.Eta ?? intl.Eta,
            Notes = dto.Notes,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        await RecomputeAsync(intl, userId);
        return new IntlActionResult("Ok", $"{type} of {dto.AmountFx:N2} {ccy} added — landed cost recomputed.", intl.Id);
    }

    public async Task<IntlActionResult> RemoveComponentAsync(string componentId, string userId)
    {
        var c = await components.GetByIdAsync(componentId);
        if (c is null) return Err("Cost component not found.");
        if (c.FromCustoms)
            return Err("This component comes from the customs declaration — amend the declaration instead.");

        var intl = await intlPos.GetByIdAsync(c.IntlPoId);
        await components.DeleteAsync(c);
        if (intl is not null) await RecomputeAsync(intl, userId);
        return new IntlActionResult("Ok", "Cost component removed — landed cost recomputed.", intl?.Id);
    }

    public async Task<IntlActionResult> DeclareCustomsAsync(string id, DeclareCustomsDto dto, string userId)
    {
        var intl = await intlPos.GetByIdAsync(id);
        if (intl is null) return Err("International order not found.");
        if (string.IsNullOrWhiteSpace(dto.IdfNumber)) return Err("The IDF number is required.");

        var declaredAt = dto.DeclaredAt ?? DateTime.UtcNow;
        var existing = await customs.Query().FirstOrDefaultAsync(x => x.IntlPoId == intl.Id);
        if (existing is null)
        {
            existing = new CustomsDeclaration { IntlPoId = intl.Id, CreatedBy = userId };
        }
        existing.IdfNumber = dto.IdfNumber;
        existing.EntryNumber = dto.EntryNumber;
        existing.ImportDutyKes = dto.ImportDutyKes;
        existing.ClearingAgentFeeKes = dto.ClearingAgentFeeKes;
        existing.PortChargesKes = dto.PortChargesKes;
        existing.DeclaredAt = declaredAt;
        existing.Notes = dto.Notes;
        Touch(existing, userId);
        if (string.IsNullOrEmpty(existing.Id) || await customs.GetByIdAsync(existing.Id) is null)
            await customs.CreateAsync(existing);
        else
            await customs.UpdateAsync(existing);

        // Replace the customs-derived components so re-lodging never double-counts.
        var derived = await components.Query().Where(c => c.IntlPoId == intl.Id && c.FromCustoms).ToListAsync();
        foreach (var stale in derived) await components.DeleteAsync(stale);

        foreach (var (type, amount) in new[]
        {
            (LandedCostComponentType.ImportDuty, dto.ImportDutyKes),
            (LandedCostComponentType.Clearing, dto.ClearingAgentFeeKes),
            (LandedCostComponentType.PortCharges, dto.PortChargesKes),
        })
        {
            if (amount <= 0) continue;
            await components.CreateAsync(new LandedCostComponent
            {
                IntlPoId = intl.Id,
                ComponentType = type,
                AmountFx = amount,
                CurrencyCode = BaseCurrency,     // customs charges are levied in KES
                ExchangeRate = 1m,
                KesAmount = amount,
                IncurredOn = declaredAt,
                BlNumber = intl.BlNumber,
                Notes = $"From customs declaration {dto.IdfNumber}",
                FromCustoms = true,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
        }

        if (intl.Status < IntlPoStatus.Cleared) intl.Status = IntlPoStatus.Cleared;
        await RecomputeAsync(intl, userId);
        await LogAsync(id, AsrAuditAction.IntlCustomsDeclared,
            $"Customs declared for {intl.PoNumber} (IDF {dto.IdfNumber}): duty {dto.ImportDutyKes:N2}, clearing {dto.ClearingAgentFeeKes:N2}, port {dto.PortChargesKes:N2} KES.", userId);
        return new IntlActionResult("Cleared", "Customs declaration recorded — landed cost recomputed.", id);
    }

    public async Task<IntlActionResult> FinaliseCostAsync(string id, string userId)
    {
        var intl = await intlPos.GetByIdAsync(id);
        if (intl is null) return Err("International order not found.");

        var po = await pos.GetByIdAsync(intl.PoId);
        if (po is null || po.ReceiptStatus != PoReceiptStatus.FullyReceived)
            return Err("The goods must be fully received before the landed cost can be finalised.");

        await RecomputeAsync(intl, userId, finalise: true);
        var refreshed = await intlPos.GetByIdAsync(id);
        await LogAsync(id, AsrAuditAction.IntlLandedCostComputed,
            $"Landed cost finalised for {intl.PoNumber}: {refreshed?.TotalLandedCostKes:N2} KES total, {refreshed?.LandedCostPerUnitKes:N2} KES per unit over {refreshed?.QuantityBasis:N2} units.", userId);
        return new IntlActionResult("Costed",
            $"Landed cost finalised — {refreshed?.LandedCostPerUnitKes:N2} KES per unit.", id);
    }

    // ── Helpers ──
    /// <summary>Purchase price plus every component, divided by the received quantity where Stores has
    /// confirmed it (the PROC-003 divisor) and the ordered quantity until then.</summary>
    private async Task RecomputeAsync(InternationalPo intl, string userId, bool finalise = false)
    {
        var comps = await components.Query().Where(c => c.IntlPoId == intl.Id).ToListAsync();
        var po = await pos.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == intl.PoId);

        var received = po?.ReceivedQty ?? 0m;
        var basis = received > 0 ? received : intl.QuantityBasis;

        intl.TotalLandedCostKes = Round2(intl.PurchasePriceKes + comps.Sum(c => c.KesAmount));
        intl.QuantityBasis = basis;
        intl.LandedCostPerUnitKes = basis > 0 ? Round2(intl.TotalLandedCostKes / basis) : 0m;
        if (finalise) intl.Status = IntlPoStatus.Costed;

        Touch(intl, userId);
        await intlPos.UpdateAsync(intl);
    }

    /// <summary>An explicit rate always wins; otherwise Finance is asked. KES is always 1. If neither yields a
    /// rate the caller is told to supply one — never converted at a guess.</summary>
    private async Task<(decimal Rate, string? Error)> ResolveRateAsync(string currencyCode, decimal? explicitRate)
    {
        if (explicitRate is > 0) return (explicitRate.Value, null);
        if (string.Equals(currencyCode, BaseCurrency, StringComparison.OrdinalIgnoreCase)) return (1m, null);

        var rate = await currencies.GetRateAsync(currencyCode);
        return rate is > 0
            ? (rate.Value, null)
            : (0m, $"No exchange rate for {currencyCode.ToUpperInvariant()} is available from Finance — enter the rate explicitly.");
    }

    private async Task<IntlPoReadDto> ToDtoAsync(InternationalPo intl)
    {
        var dto = mapper.Map<IntlPoReadDto>(intl);
        var comps = await components.Query().AsNoTracking()
            .Where(c => c.IntlPoId == intl.Id).OrderBy(c => c.IncurredOn).ToListAsync();
        dto.Components = mapper.Map<List<LandedCostComponentDto>>(comps);

        var cust = await customs.Query().AsNoTracking().FirstOrDefaultAsync(x => x.IntlPoId == intl.Id);
        dto.Customs = cust is null ? null : mapper.Map<CustomsDeclarationDto>(cust);

        var po = await pos.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == intl.PoId);
        dto.PoStatus = (po?.Status ?? PoStatus.Draft).ToString();
        dto.ReceiptStatus = (po?.ReceiptStatus ?? PoReceiptStatus.NotReceived).ToString();
        dto.QuantityFromReceipt = (po?.ReceivedQty ?? 0) > 0;

        dto.CanRequestTt = po?.Status == PoStatus.Issued && intl.TtSentAt is null;
        dto.CanApproveTt = intl.TtRequestedAt != null && intl.TtApprovedAt is null;
        dto.CanSendTt = intl.TtApprovedAt != null && intl.TtSentAt is null;
        return dto;
    }

    private static IntlActionResult Err(string message) => new("Error", message);
    private static decimal Round2(decimal v) => Money.Round(v);
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }

    private async Task LogAsync(string entityId, AsrAuditAction action, string detail, string userId)
    {
        await audit.CreateAsync(new ProcurementAuditLog
        {
            EntityType = "InternationalPo", EntityId = entityId, Action = action,
            Detail = detail, PerformedBy = userId, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
