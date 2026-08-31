namespace ProcurementService.Core.Interfaces.Services;

// Cross-module seams owned by procurement. Each ships a config-gated no-op default (so call sites are
// wired and inert until the real *Service:BaseUrl is set), mirroring the CRM seam pattern. Real adapters
// mint a per-schema service token and call the sibling service over HTTP.

/// <summary>Outcome of the P2 hard budget check against the Finance budget ("budget line").</summary>
public record BudgetCheckResult(bool Available, string? BudgetName, string Message);

/// <summary>P2 (LPO step 1) — Finance budget seam. Verifies the requisition's budget line exists, is active
/// and covers the requested amount before a PR may be submitted. Degrades to "available" (fail-open) when
/// Finance is not configured or unreachable, so procurement is never hard-blocked by a Finance outage.</summary>
public interface IBudgetGateway
{
    Task<BudgetCheckResult> CheckAsync(string? budgetId, decimal amount, CancellationToken ct = default);
}

// NOTE (P4): there is deliberately no journal seam here. An issued LPO is an executory contract, not a
// liability, so nothing is posted to the general ledger at LPO issue — Finance posts the single AP credit
// for the purchase when it approves the supplier invoice. A commitment journal here would double-book
// payables. (The earlier HttpFinanceJournalGateway is in git history if a later phase — e.g. P7 landed
// cost — needs a real GL posting seam.)

/// <summary>A supplier invoice as held by Finance (the invoice leg of the 3-way match).</summary>
public record SupplierInvoiceRef(
    string Id, string InvoiceNo, string? SupplierId, string? SupplierName,
    decimal Subtotal, decimal Total, decimal Balance, string Status, string MatchStatus);

/// <summary>Result of looking an invoice up by LPO reference. <paramref name="Reachable"/> distinguishes
/// "Finance answered, there is no such invoice" (Reachable=true, Invoice=null) from "Finance could not be
/// consulted" — the matching engine must not treat an outage as a missing invoice.</summary>
public record InvoiceLookup(bool Reachable, SupplierInvoiceRef? Invoice, string Message);

/// <summary>P6 — Finance payables read seam. Finds the supplier invoice Finance recorded against an LPO
/// (finance.SupplierInvoice.LpoReference). Unlike the P2 budget and P4 journal seams this one is
/// <b>fail-closed</b>: it guards a payment, so an unreachable Finance blocks the match instead of
/// silently passing or failing it.</summary>
public interface IInvoiceGateway
{
    Task<InvoiceLookup> FindByLpoAsync(string poNumber, CancellationToken ct = default);

    /// <summary>Writes the match outcome back onto the Finance invoice. Finance owns a
    /// <c>MatchStatus</c> field for exactly this ("the match itself is owned by Procurement") and refuses to
    /// approve an invoice whose match is in Exception — so this is what makes that Finance-side control live.
    /// Best-effort: procurement's own record is authoritative and the voucher gate is enforced here, so a
    /// write-back failure is reported but never blocks the match.</summary>
    Task<bool> SetMatchStatusAsync(string supplierInvoiceId, string status, string? note, CancellationToken ct = default);
}

/// <summary>Outcome of handing a validated payment voucher to Finance.</summary>
public record VoucherResult(bool Raised, string? VoucherId, string? VoucherNo, string Message);

/// <summary>P6 (DEC-4/DEC-A) — payment handoff seam. A clean 3-way match hands the validated voucher to
/// Finance, which materialises it as its own PaymentVoucher and routes it through the payment-authority
/// matrix; procurement keeps no voucher table of its own. Fail-closed — no reference is stamped unless
/// Finance confirms.</summary>
public interface IPaymentVoucherGateway
{
    Task<VoucherResult> RaiseAsync(string supplierInvoiceId, decimal? amount, string? bankAccountCode, string poNumber, CancellationToken ct = default);

    /// <summary>P7 — an advance (T/T) payment to a foreign supplier, raised before any invoice exists, so it
    /// goes to Finance as an ad-hoc voucher (payee + amount) rather than against a supplier invoice. Finance
    /// still routes it through its payment-authority matrix. Fail-closed like the invoice-backed variant.</summary>
    Task<VoucherResult> RaiseAdHocAsync(string payee, decimal amountKes, string reference, CancellationToken ct = default);
}

/// <summary>Outcome of checking a board-resolution reference against Compliance (COMP-007, which is the
/// system of record for resolutions). <paramref name="Reachable"/> false means Compliance is disabled or could
/// not be consulted — distinct from Reachable+!Found, which means Compliance answered and holds no such
/// resolution.</summary>
public record BoardResolutionCheck(
    bool Reachable, bool Found, string? Id, string? ReferenceNo, string? Title,
    DateTime? ResolutionDate, string? ScannedCopyUrl, string Message);

/// <summary>DEC-C (P4) — verifies the Board Resolution behind an LPO above 500,000 against Compliance, which
/// owns <c>BoardResolution</c>. Accepts either the compliance record id or its reference number, since a user
/// types the reference off the minutes. Local capture remains the sanctioned fallback when Compliance is not
/// wired, so a resolution can still be recorded — just marked unverified.</summary>
public interface IBoardResolutionGateway
{
    Task<BoardResolutionCheck> VerifyAsync(string reference, CancellationToken ct = default);
}

/// <summary>A supplier as held by a downstream module, normalised across Finance (AP vendors) and Stores.</summary>
public record ExternalSupplier(
    string Source, string Id, string Name, string? KraPin,
    string? ContactPerson, string? Phone, string? Email, string? Address, bool IsActive);

/// <summary>One source's contribution to the seed. <paramref name="Reachable"/> false means the module was
/// disabled or unreachable — the seed then proceeds with whatever other sources answered rather than failing,
/// and says so, because a partial import is useful and re-running is safe.</summary>
public record SupplierSourceResult(bool Reachable, List<ExternalSupplier> Suppliers, string Message);

/// <summary>DEC-B — reads the supplier masters that pre-date the ASR so they can be imported into it.
/// Procurement owns the ASR (DEC-2), and Finance/Stores keep their own rows referenced by id.</summary>
public interface ISupplierSourceGateway
{
    Task<SupplierSourceResult> ListFinanceAsync(CancellationToken ct = default);
    Task<SupplierSourceResult> ListStoresAsync(CancellationToken ct = default);
}

/// <summary>P7 — Finance FX seam. Reads the tenant's currency table so international costs convert at a real
/// rate. Rates are <b>base per 1 foreign unit</b> (KES per USD), so foreign → KES multiplies.
/// Fail-closed: when Finance cannot be reached the caller must supply an explicit rate, because a silently
/// wrong rate would corrupt the landed cost.</summary>
public interface ICurrencyGateway
{
    /// <summary>Null when Finance is unreachable or does not know the code — never a guessed rate.</summary>
    Task<decimal?> GetRateAsync(string currencyCode, CancellationToken ct = default);
    Task<List<(string Code, string? Name, decimal Rate)>> ListAsync(CancellationToken ct = default);
}
