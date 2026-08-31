using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.DTOs.Suppliers;
using ProcurementService.Core.Entities;
using ProcurementService.Core.Enums;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Core.Services;

/// <summary>
/// DEC-B — imports the supplier masters that pre-date the ASR into the register procurement owns (DEC-2).
/// <para><b>Matching.</b> Candidates are merged across sources on a normalised KRA PIN (case/space/dash
/// insensitive) where one exists, otherwise on a normalised name — so the same firm held in both Finance and
/// Stores becomes ONE ASR supplier carrying both back-references. Against the existing register, a candidate
/// matches on an existing back-reference first (the strongest link), then KRA PIN, then name.</para>
/// <para><b>Idempotent by construction.</b> A matched candidate is never re-created: the run only fills in a
/// missing <c>FinanceSupplierId</c>/<c>StoreSupplierId</c> (and blank contact fields), and never overwrites
/// data already curated in the ASR. So the seed can be re-run safely — which is what lets it proceed when one
/// source is unreachable and catch up later.</para>
/// <para><b>Approval is not inherited.</b> Imports land <see cref="SupplierStatus.Pending"/> with the conflict
/// check outstanding: being an existing trading partner is not evidence of having passed PROC-001. Callers who
/// want the register usable immediately can opt into <c>AutoApprove</c>, which is recorded in the audit trail
/// as approval granted by import rather than by review.</para>
/// </summary>
public class SupplierSeedService(
    IGenericRepository<Supplier> suppliers,
    IGenericRepository<ProcurementAuditLog> audit,
    ISupplierSourceGateway sources) : ISupplierSeedService
{
    public Task<SupplierSeedResultDto> PreviewAsync(SeedSuppliersDto dto) => RunAsync(dto, null);

    public Task<SupplierSeedResultDto> SeedAsync(SeedSuppliersDto dto, string userId) => RunAsync(dto, userId);

    /// <summary>One code path for preview and apply — a preview that ran different logic would not be a
    /// preview. <paramref name="userId"/> null means dry run.</summary>
    private async Task<SupplierSeedResultDto> RunAsync(SeedSuppliersDto dto, string? userId)
    {
        var preview = userId is null;
        var result = new SupplierSeedResultDto { Preview = preview, AutoApproved = dto.AutoApprove && !preview };

        var finance = await sources.ListFinanceAsync();
        var stores = await sources.ListStoresAsync();
        result.SourceMessages.Add(finance.Message);
        result.SourceMessages.Add(stores.Message);
        result.FinanceRead = finance.Suppliers.Count;
        result.StoresRead = stores.Suppliers.Count;

        var incoming = finance.Suppliers.Concat(stores.Suppliers)
            .Where(s => dto.IncludeInactive || s.IsActive)
            .ToList();

        if (incoming.Count == 0)
        {
            result.Message = !finance.Reachable && !stores.Reachable
                ? "Neither Finance nor Stores could be read — nothing imported."
                : "No suppliers to import from the sources that answered.";
            return result;
        }

        // ── Merge the two sources into one candidate per real-world supplier ──
        var candidates = new List<Candidate>();
        foreach (var ext in incoming)
        {
            var pin = NormalisePin(ext.KraPin);
            var name = NormaliseName(ext.Name);
            var existing = candidates.FirstOrDefault(c =>
                (pin is not null && c.Pin == pin) || (pin is null && c.Pin is null && c.Name == name));
            if (existing is null)
                candidates.Add(new Candidate(pin, name, [ext]));
            else
                existing.Parts.Add(ext);
        }
        result.Candidates = candidates.Count;

        var register = await suppliers.Query().ToListAsync();
        var nextNumber = await NextNumberSeedAsync();

        foreach (var candidate in candidates)
        {
            var fin = candidate.Parts.FirstOrDefault(p => p.Source == "finance");
            var sto = candidate.Parts.FirstOrDefault(p => p.Source == "stores");
            var sourceLabel = string.Join("+", candidate.Parts.Select(p => p.Source).Distinct().OrderBy(s => s));
            var entry = new SupplierSeedEntryDto
            {
                Name = candidate.Parts[0].Name,
                KraPin = candidate.Parts.Select(p => p.KraPin).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
                Sources = sourceLabel,
            };

            // Strongest match first: an id we already recorded, then KRA PIN, then name.
            var match = register.FirstOrDefault(s =>
                            (fin is not null && s.FinanceSupplierId == fin.Id)
                            || (sto is not null && s.StoreSupplierId == sto.Id));
            entry.MatchedOn = match is not null ? "BackReference" : null;

            if (match is null && candidate.Pin is not null)
            {
                match = register.FirstOrDefault(s => NormalisePin(s.KraPin) == candidate.Pin);
                if (match is not null) entry.MatchedOn = "KraPin";
            }
            if (match is null)
            {
                match = register.FirstOrDefault(s => NormaliseName(s.Name) == candidate.Name);
                if (match is not null) entry.MatchedOn = "Name";
            }

            if (match is not null)
            {
                entry.SupplierId = match.Id;
                entry.SupplierNumber = match.SupplierNumber;

                // Only ever fill gaps — never overwrite what the ASR already holds.
                var changed = false;
                if (fin is not null && string.IsNullOrEmpty(match.FinanceSupplierId)) { match.FinanceSupplierId = fin.Id; changed = true; }
                if (sto is not null && string.IsNullOrEmpty(match.StoreSupplierId)) { match.StoreSupplierId = sto.Id; changed = true; }
                if (string.IsNullOrWhiteSpace(match.KraPin) && entry.KraPin is not null) { match.KraPin = entry.KraPin; changed = true; }
                if (string.IsNullOrWhiteSpace(match.ContactPerson)) { var v = Pick(candidate, p => p.ContactPerson); if (v is not null) { match.ContactPerson = v; changed = true; } }
                if (string.IsNullOrWhiteSpace(match.Phone)) { var v = Pick(candidate, p => p.Phone); if (v is not null) { match.Phone = v; changed = true; } }
                if (string.IsNullOrWhiteSpace(match.Email)) { var v = Pick(candidate, p => p.Email); if (v is not null) { match.Email = v; changed = true; } }
                if (string.IsNullOrWhiteSpace(match.Address)) { var v = Pick(candidate, p => p.Address); if (v is not null) { match.Address = v; changed = true; } }

                if (!changed)
                {
                    entry.Action = "Skip";
                    entry.Reason = $"Already in the register as {match.SupplierNumber} — nothing to add.";
                    result.Skipped++;
                }
                else
                {
                    entry.Action = "Link";
                    entry.Reason = $"Matched {match.SupplierNumber} on {entry.MatchedOn} — back-reference and blank details filled in.";
                    result.Linked++;
                    if (!preview)
                    {
                        match.UpdatedBy = userId;
                        match.UpdatedAt = DateTime.UtcNow;
                        await suppliers.UpdateAsync(match);
                        await LogAsync(match.Id,
                            $"Linked to {sourceLabel} supplier master(s) by DEC-B seed (matched on {entry.MatchedOn}).", userId!);
                    }
                }
                result.Entries.Add(entry);
                continue;
            }

            // ── New to the ASR ──
            entry.Action = "Import";
            entry.Reason = dto.AutoApprove
                ? $"New from {sourceLabel} — imported and approved by import."
                : $"New from {sourceLabel} — imported as Pending, awaiting conflict check and approval.";
            result.Imported++;

            if (!preview)
            {
                var number = $"SUP-{DateTime.UtcNow.Year}-{nextNumber++:D4}";
                var created = await suppliers.CreateAsync(new Supplier
                {
                    SupplierNumber = number,
                    Name = candidate.Parts[0].Name,
                    KraPin = entry.KraPin,
                    ContactPerson = Pick(candidate, p => p.ContactPerson),
                    Phone = Pick(candidate, p => p.Phone),
                    Email = Pick(candidate, p => p.Email),
                    Address = Pick(candidate, p => p.Address),
                    Status = dto.AutoApprove ? SupplierStatus.Approved : SupplierStatus.Pending,
                    IsApproved = dto.AutoApprove,
                    ApprovedBy = dto.AutoApprove ? userId : null,
                    ApprovedAt = dto.AutoApprove ? DateTime.UtcNow : null,
                    ConflictChecked = false,
                    FinanceSupplierId = fin?.Id,
                    StoreSupplierId = sto?.Id,
                    Source = $"seed:{sourceLabel}",
                    // userId is guaranteed non-null here (we're inside `if (!preview)`, and preview = userId is null),
                    // matching the userId! usage elsewhere in this method's non-preview branches.
                    CreatedBy = userId!,
                    UpdatedBy = userId,
                });
                register.Add(created);
                entry.SupplierId = created.Id;
                entry.SupplierNumber = created.SupplierNumber;
                await LogAsync(created.Id,
                    $"Imported {created.SupplierNumber} from {sourceLabel} by DEC-B seed"
                    + (dto.AutoApprove
                        ? " and APPROVED BY IMPORT — approval was not granted by PROC-001 review; the conflict check is still outstanding."
                        : " as Pending — conflict check and approval outstanding."), userId!);
            }
            result.Entries.Add(entry);
        }

        var verb = preview ? "would be" : "were";
        result.Message =
            $"{result.Candidates} candidate(s) from {result.FinanceRead} finance + {result.StoresRead} stores record(s): "
            + $"{result.Imported} {verb} imported, {result.Linked} {verb} linked, {result.Skipped} already complete."
            + (result.Imported > 0 && !dto.AutoApprove ? " Imported suppliers need a conflict check and approval before use on an LPO." : "")
            + (result.Imported > 0 && dto.AutoApprove ? " Imported suppliers were approved by import, not by review." : "");
        return result;
    }

    // ── Helpers ──
    private sealed record Candidate(string? Pin, string Name, List<ExternalSupplier> Parts);

    /// <summary>Finance and Stores hold PINs inconsistently punctuated, so compare on letters and digits only.</summary>
    private static string? NormalisePin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) return null;
        var cleaned = new string(pin.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>Case-insensitive, whitespace-collapsed name comparison — the fallback when there is no PIN.</summary>
    private static string NormaliseName(string name)
        => string.Join(' ', (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();

    /// <summary>First non-blank value across the sources for this candidate (Stores usually carries the
    /// richer contact detail, Finance the AP identity).</summary>
    private static string? Pick(Candidate candidate, Func<ExternalSupplier, string?> selector)
        => candidate.Parts.Select(selector).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Next sequence number, read once — the loop then increments locally so a batch import does not
    /// re-count (and collide) per row.</summary>
    private async Task<int> NextNumberSeedAsync()
    {
        var prefix = $"SUP-{DateTime.UtcNow.Year}-";
        return await suppliers.Query().CountAsync(s => s.SupplierNumber.StartsWith(prefix)) + 1;
    }

    private async Task LogAsync(string supplierId, string detail, string userId)
    {
        await audit.CreateAsync(new ProcurementAuditLog
        {
            EntityType = "Supplier", EntityId = supplierId, Action = AsrAuditAction.Seeded,
            Detail = detail, PerformedBy = userId, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
