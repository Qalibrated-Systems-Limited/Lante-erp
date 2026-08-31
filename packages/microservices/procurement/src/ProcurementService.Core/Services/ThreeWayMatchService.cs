using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.DTOs.Matching;
using ProcurementService.Core.Entities;
using ProcurementService.Core.Enums;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Core.Services;

/// <summary>
/// P6 (DEC-4) — the 3-way matching engine. Compares the LPO against the goods received (the PO's receipt
/// state, pushed by the Stores GRN callback in P5) and the supplier invoice Finance recorded against the
/// LPO reference. Clean match → a validated payment voucher is handed to Finance (DEC-A); any failed check
/// → a MatchingException that blocks payment until a Finance Manager resolves it.
/// <para>The PO is header-level (no PO lines), so the checks are receipt-complete, invoice-present and
/// invoice-value-vs-order-value rather than line-by-line.</para>
/// </summary>
public class ThreeWayMatchService(
    IGenericRepository<ThreeWayMatch> matches,
    IGenericRepository<MatchingException> exceptions,
    IGenericRepository<PurchaseOrder> pos,
    IGenericRepository<ProcurementAuditLog> audit,
    IInvoiceGateway invoices,
    IPaymentVoucherGateway vouchers,
    IMapper mapper) : IThreeWayMatchService
{
    /// <summary>Value tolerance on the invoice-vs-order comparison: 1% of the order, never below KES 1
    /// (absorbs rounding and cent-level differences without letting real overbilling through).</summary>
    private const decimal TolerancePercent = 0.01m;
    private const decimal MinTolerance = 1m;

    // ── Reads ──
    public async Task<MatchListResult> GetAllAsync(MatchFilterParams filter)
    {
        var q = matches.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<MatchStatus>(filter.Status, true, out var st))
            q = q.Where(x => x.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.PoId)) q = q.Where(x => x.PoId == filter.PoId);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();

        var ids = items.Select(x => x.Id).ToList();
        var openCounts = await exceptions.Query().AsNoTracking()
            .Where(e => ids.Contains(e.ThreeWayMatchId) && e.Status == MatchExceptionStatus.Open)
            .GroupBy(e => e.ThreeWayMatchId)
            .Select(g => new { MatchId = g.Key, Count = g.Count() })
            .ToListAsync();
        var poIds = items.Select(x => x.PoId).ToList();
        var supplierNames = await pos.Query().AsNoTracking()
            .Where(p => poIds.Contains(p.Id))
            .Select(p => new { p.Id, p.SupplierName }).ToListAsync();

        var rows = mapper.Map<List<MatchRowDto>>(items);
        foreach (var row in rows)
        {
            row.OpenExceptions = openCounts.FirstOrDefault(c => c.MatchId == row.Id)?.Count ?? 0;
            row.SupplierName = supplierNames.FirstOrDefault(s => s.Id == row.PoId)?.SupplierName;
        }
        return new MatchListResult(rows, total);
    }

    public async Task<MatchReadDto?> GetByIdAsync(string id)
    {
        var m = await matches.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return m is null ? null : await ToDtoAsync(m);
    }

    public async Task<MatchReadDto?> GetByPoAsync(string poId)
    {
        var m = await matches.Query().AsNoTracking().FirstOrDefaultAsync(x => x.PoId == poId);
        return m is null ? null : await ToDtoAsync(m);
    }

    public async Task<MatchSummaryDto> GetSummaryAsync()
    {
        var all = await matches.Query().AsNoTracking().ToListAsync();
        var open = await exceptions.Query().AsNoTracking().CountAsync(e => e.Status == MatchExceptionStatus.Open);
        var matchedIds = all.Where(m => m.Status == MatchStatus.Matched).Select(m => m.Id).ToList();
        var blocked = await exceptions.Query().AsNoTracking()
            .Where(e => e.Status == MatchExceptionStatus.Open && matchedIds.Contains(e.ThreeWayMatchId))
            .Select(e => e.ThreeWayMatchId).Distinct().ToListAsync();

        var issued = await pos.Query().AsNoTracking().Where(p => p.Status == PoStatus.Issued).Select(p => p.Id).ToListAsync();
        var everMatched = all.Select(m => m.PoId).ToHashSet();

        return new MatchSummaryDto
        {
            Total = all.Count,
            Matched = all.Count(m => m.Status == MatchStatus.Matched),
            WithExceptions = all.Count(m => m.Status == MatchStatus.Exception),
            OpenExceptions = open,
            AwaitingVoucher = all.Count(m => m.Status == MatchStatus.Matched
                && string.IsNullOrEmpty(m.PaymentVoucherRef) && !blocked.Contains(m.Id)),
            VouchersRaised = all.Count(m => !string.IsNullOrEmpty(m.PaymentVoucherRef)),
            MatchedValue = all.Where(m => m.Status == MatchStatus.Matched).Sum(m => m.InvoiceTotal),
            UnmatchedLpos = issued.Count(id => !everMatched.Contains(id)),
        };
    }

    public async Task<List<MatchExceptionDto>> GetExceptionsAsync(string? status)
    {
        var q = exceptions.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<MatchExceptionStatus>(status, true, out var st))
            q = q.Where(e => e.Status == st);
        var list = await q.OrderByDescending(e => e.RaisedAt).ToListAsync();
        return mapper.Map<List<MatchExceptionDto>>(list);
    }

    // ── The engine ──
    public async Task<MatchActionResult> RunAsync(string poId, string userId)
    {
        var po = await pos.GetByIdAsync(poId);
        if (po is null) return new MatchActionResult("Error", "LPO not found.");
        if (po.Status != PoStatus.Issued)
            return new MatchActionResult("Error", "Only an issued LPO can be matched.");

        var existing = await matches.Query().FirstOrDefaultAsync(x => x.PoId == poId);
        if (!string.IsNullOrEmpty(existing?.PaymentVoucherRef))
            return new MatchActionResult("Error", "A payment voucher has already been raised for this LPO.");

        // Invoice leg — fail closed. An unreachable Finance must not be read as "no invoice".
        var lookup = await invoices.FindByLpoAsync(po.PoNumber);
        if (!lookup.Reachable)
            return new MatchActionResult("Error", $"Cannot match without Finance: {lookup.Message}");

        var inv = lookup.Invoice;
        var m = existing ?? new ThreeWayMatch
        {
            PoId = po.Id,
            PoNumber = po.PoNumber,
            CreatedBy = userId,
        };

        m.PoTotal = po.TotalAmount;
        m.ReceivedQty = po.ReceivedQty;
        m.SupplierInvoiceId = inv?.Id;
        m.InvoiceNumber = inv?.InvoiceNo;

        // Compare on the net (pre-VAT) invoice value where Finance captured it, since the LPO value is
        // net of tax; fall back to the gross total when no subtotal was recorded.
        var invoiceValue = inv is null ? 0m : inv.Subtotal > 0 ? inv.Subtotal : inv.Total;
        m.InvoiceTotal = invoiceValue;

        var tolerance = Math.Max(MinTolerance, po.TotalAmount * TolerancePercent);
        m.ReceivedOk = po.ReceiptStatus == PoReceiptStatus.FullyReceived;
        m.TotalOk = inv is not null && invoiceValue <= po.TotalAmount + tolerance;
        m.PriceOk = inv is not null && Math.Abs(invoiceValue - po.TotalAmount) <= tolerance;

        // Which checks failed, and why.
        var failures = new List<(MatchExceptionType Type, string Detail)>();
        if (!m.ReceivedOk)
            failures.Add((MatchExceptionType.NotFullyReceived,
                $"Goods receipt is {po.ReceiptStatus} (accepted {po.ReceivedQty:N2}). The LPO must be fully received before payment."));
        if (inv is null)
            failures.Add((MatchExceptionType.NoInvoice,
                $"No supplier invoice recorded in Finance against {po.PoNumber}."));
        else if (!m.TotalOk)
            failures.Add((MatchExceptionType.TotalExceedsPo,
                $"Invoice {inv.InvoiceNo} of {invoiceValue:N2} exceeds the LPO value of {po.TotalAmount:N2}."));
        else if (!m.PriceOk)
            failures.Add((MatchExceptionType.PriceMismatch,
                $"Invoice {inv.InvoiceNo} of {invoiceValue:N2} does not agree with the LPO value of {po.TotalAmount:N2}."));

        m.Status = failures.Count == 0 ? MatchStatus.Matched : MatchStatus.Exception;
        if (m.Status == MatchStatus.Matched) { m.MatchedBy = userId; m.MatchedAt = DateTime.UtcNow; }
        else { m.MatchedBy = null; m.MatchedAt = null; }

        m.UpdatedBy = userId;
        m.UpdatedAt = DateTime.UtcNow;
        var saved = existing is null ? await matches.CreateAsync(m) : await matches.UpdateAsync(m);

        var reconciled = await ReconcileExceptionsAsync(saved, failures, userId);
        await WriteBackAsync(saved, failures.Count == 0
            ? $"3-way match passed against {saved.PoNumber}."
            : $"3-way match raised {failures.Count} exception(s) against {saved.PoNumber}.");
        await LogAsync("ThreeWayMatch", saved.Id, AsrAuditAction.MatchRun,
            $"3-way match run for {po.PoNumber}: {saved.Status}"
            + $" (received {(saved.ReceivedOk ? "ok" : "fail")}, invoice {(inv is null ? "missing" : "found")}, value {(saved.PriceOk ? "ok" : "fail")}).", userId);

        return saved.Status == MatchStatus.Matched
            ? new MatchActionResult("Matched", $"{po.PoNumber} matched — ready for payment.", saved.Id)
            : new MatchActionResult("Exception",
                $"{po.PoNumber} did not match — {reconciled} open exception(s) to resolve.", saved.Id);
    }

    public async Task<MatchActionResult> RunPendingAsync(string userId)
    {
        var issued = await pos.Query().AsNoTracking()
            .Where(p => p.Status == PoStatus.Issued)
            .Select(p => p.Id).ToListAsync();
        var settled = await matches.Query().AsNoTracking()
            .Where(m => m.Status == MatchStatus.Matched)
            .Select(m => m.PoId).ToListAsync();

        var todo = issued.Except(settled).ToList();
        int matched = 0, exceptioned = 0, errored = 0;
        foreach (var poId in todo)
        {
            var r = await RunAsync(poId, userId);
            if (r.Status == "Matched") matched++;
            else if (r.Status == "Exception") exceptioned++;
            else errored++;
        }
        return new MatchActionResult("Ok",
            $"Ran {todo.Count} LPO(s): {matched} matched, {exceptioned} with exceptions"
            + (errored > 0 ? $", {errored} could not be run." : "."));
    }

    // ── Exception resolution ──
    public async Task<MatchActionResult> ResolveExceptionAsync(string exceptionId, ResolveExceptionDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Resolution))
            return new MatchActionResult("Error", "A resolution note is mandatory.");

        var ex = await exceptions.GetByIdAsync(exceptionId);
        if (ex is null) return new MatchActionResult("Error", "Matching exception not found.");
        if (ex.Status != MatchExceptionStatus.Open) return new MatchActionResult("Error", "This exception is already resolved.");

        ex.Status = MatchExceptionStatus.Resolved;
        ex.Resolution = dto.Resolution;
        ex.ResolvedBy = userId;
        ex.ResolvedAt = DateTime.UtcNow;
        ex.UpdatedBy = userId;
        ex.UpdatedAt = DateTime.UtcNow;
        await exceptions.UpdateAsync(ex);
        await LogAsync("MatchingException", ex.Id, AsrAuditAction.MatchExceptionResolved,
            $"{ex.ExceptionType} on {ex.PoNumber} resolved: {dto.Resolution}", userId);

        var stillOpen = await exceptions.Query()
            .CountAsync(e => e.ThreeWayMatchId == ex.ThreeWayMatchId && e.Status == MatchExceptionStatus.Open);
        if (stillOpen > 0)
            return new MatchActionResult("Ok", $"Exception resolved — {stillOpen} still open on this LPO.");

        // Last exception cleared: release the match for payment as an explicit, audited override.
        var m = await matches.GetByIdAsync(ex.ThreeWayMatchId);
        if (m is not null && m.Status != MatchStatus.Matched)
        {
            m.Status = MatchStatus.Matched;
            m.MatchedBy = userId;
            m.MatchedAt = DateTime.UtcNow;
            m.UpdatedBy = userId;
            m.UpdatedAt = DateTime.UtcNow;
            await matches.UpdateAsync(m);
            await WriteBackAsync(m, $"All 3-way match exceptions on {m.PoNumber} resolved: {dto.Resolution}");
            await LogAsync("ThreeWayMatch", m.Id, AsrAuditAction.MatchRun,
                $"All exceptions on {m.PoNumber} resolved — match released for payment by override.", userId);
        }
        return new MatchActionResult("Matched", "Last exception resolved — the LPO is released for payment.");
    }

    // ── Payment handoff (DEC-A) ──
    public async Task<MatchActionResult> RaiseVoucherAsync(string matchId, RaiseVoucherDto dto, string userId)
    {
        var m = await matches.GetByIdAsync(matchId);
        if (m is null) return new MatchActionResult("Error", "Match not found.");
        if (!string.IsNullOrEmpty(m.PaymentVoucherRef))
            return new MatchActionResult("Error", $"A payment voucher ({m.PaymentVoucherNo ?? m.PaymentVoucherRef}) has already been raised.");
        if (m.Status != MatchStatus.Matched)
            return new MatchActionResult("Error", "Only a matched LPO can be paid.");

        var open = await exceptions.Query().CountAsync(e => e.ThreeWayMatchId == m.Id && e.Status == MatchExceptionStatus.Open);
        if (open > 0) return new MatchActionResult("Error", $"Resolve the {open} open matching exception(s) first.");
        if (string.IsNullOrEmpty(m.SupplierInvoiceId))
            return new MatchActionResult("Error", "No Finance supplier invoice is linked to this LPO — a voucher cannot be raised.");

        // Re-read the invoice rather than trusting the snapshot taken when the match ran: Finance will not
        // pay an invoice it has not approved itself, and that state can move after the match.
        var lookup = await invoices.FindByLpoAsync(m.PoNumber);
        if (!lookup.Reachable)
            return new MatchActionResult("Error", $"Cannot raise the voucher without Finance: {lookup.Message}");
        if (lookup.Invoice is null)
            return new MatchActionResult("Error", $"Finance no longer holds a supplier invoice against {m.PoNumber}.");
        if (string.Equals(lookup.Invoice.Status, "Received", StringComparison.OrdinalIgnoreCase))
            return new MatchActionResult("Error",
                $"Supplier invoice {lookup.Invoice.InvoiceNo} is still awaiting approval in Finance — approve it there before the payment voucher can be raised.");
        if (string.Equals(lookup.Invoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return new MatchActionResult("Error", $"Supplier invoice {lookup.Invoice.InvoiceNo} has been cancelled in Finance.");

        var r = await vouchers.RaiseAsync(lookup.Invoice.Id, dto.Amount, dto.BankAccountCode, m.PoNumber);
        if (!r.Raised) return new MatchActionResult("Error", r.Message);

        m.PaymentVoucherRef = r.VoucherId;
        m.PaymentVoucherNo = r.VoucherNo;
        m.UpdatedBy = userId;
        m.UpdatedAt = DateTime.UtcNow;
        await matches.UpdateAsync(m);
        await LogAsync("ThreeWayMatch", m.Id, AsrAuditAction.PaymentVoucherRaised,
            $"Payment voucher {r.VoucherNo ?? r.VoucherId} handed to Finance for {m.PoNumber}.", userId);
        return new MatchActionResult("VoucherRaised", r.Message, m.Id);
    }

    // ── Helpers ──
    /// <summary>Brings the exception rows in line with the current failure set: opens what newly failed,
    /// auto-resolves what no longer applies. Returns the open count after reconciliation.</summary>
    private async Task<int> ReconcileExceptionsAsync(
        ThreeWayMatch m, List<(MatchExceptionType Type, string Detail)> failures, string userId)
    {
        var open = await exceptions.Query()
            .Where(e => e.ThreeWayMatchId == m.Id && e.Status == MatchExceptionStatus.Open)
            .ToListAsync();

        foreach (var stale in open.Where(e => failures.All(f => f.Type != e.ExceptionType)))
        {
            stale.Status = MatchExceptionStatus.Resolved;
            stale.Resolution = "Auto-resolved — the check passed on a later match run.";
            stale.ResolvedBy = userId;
            stale.ResolvedAt = DateTime.UtcNow;
            stale.UpdatedBy = userId;
            stale.UpdatedAt = DateTime.UtcNow;
            await exceptions.UpdateAsync(stale);
        }

        foreach (var f in failures)
        {
            var current = open.FirstOrDefault(e => e.ExceptionType == f.Type);
            if (current is not null)
            {
                if (current.Detail == f.Detail) continue;
                current.Detail = f.Detail;
                current.UpdatedBy = userId;
                current.UpdatedAt = DateTime.UtcNow;
                await exceptions.UpdateAsync(current);
                continue;
            }
            await exceptions.CreateAsync(new MatchingException
            {
                ThreeWayMatchId = m.Id,
                PoId = m.PoId,
                PoNumber = m.PoNumber,
                ExceptionType = f.Type,
                Detail = f.Detail,
                Status = MatchExceptionStatus.Open,
                RaisedBy = userId,
                RaisedAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
            await LogAsync("MatchingException", m.Id, AsrAuditAction.MatchExceptionRaised,
                $"{f.Type} raised on {m.PoNumber}: {f.Detail}", userId);
        }

        return failures.Count;
    }

    /// <summary>Pushes the match outcome onto the Finance invoice so Finance's own "don't approve an invoice
    /// whose match is in Exception" control has something to act on. Best-effort by design: this record is
    /// authoritative here and the voucher gate is enforced locally, so a Finance write-back failure is
    /// logged rather than allowed to fail the match the user just ran.</summary>
    private async Task WriteBackAsync(ThreeWayMatch m, string note)
    {
        if (string.IsNullOrEmpty(m.SupplierInvoiceId)) return;
        var status = m.Status switch
        {
            MatchStatus.Matched => "Matched",
            MatchStatus.Exception => "Exception",
            _ => "Pending",
        };
        await invoices.SetMatchStatusAsync(m.SupplierInvoiceId, status, note);
    }

    private async Task<MatchReadDto> ToDtoAsync(ThreeWayMatch m)
    {
        var dto = mapper.Map<MatchReadDto>(m);
        var exs = await exceptions.Query().AsNoTracking()
            .Where(e => e.ThreeWayMatchId == m.Id)
            .OrderByDescending(e => e.RaisedAt).ToListAsync();
        dto.Exceptions = mapper.Map<List<MatchExceptionDto>>(exs);

        var po = await pos.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == m.PoId);
        dto.SupplierName = po?.SupplierName;
        dto.ReceiptStatus = (po?.ReceiptStatus ?? PoReceiptStatus.NotReceived).ToString();
        dto.CanRaiseVoucher = m.Status == MatchStatus.Matched
            && string.IsNullOrEmpty(m.PaymentVoucherRef)
            && !string.IsNullOrEmpty(m.SupplierInvoiceId)
            && exs.All(e => e.Status != MatchExceptionStatus.Open);
        return dto;
    }

    private async Task LogAsync(string entityType, string entityId, AsrAuditAction action, string detail, string userId)
    {
        await audit.CreateAsync(new ProcurementAuditLog
        {
            EntityType = entityType, EntityId = entityId, Action = action,
            Detail = detail, PerformedBy = userId, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
