using System.Text.RegularExpressions;

namespace CrmService.Core.Services;

/// <summary>
/// Shared validation/normalisation for CRM fields that carry real-world formats. Kept in one place
/// so the customer and lead paths cannot drift apart on what they accept.
/// </summary>
public static partial class CrmFieldRules
{
    // KRA PINs are one letter, nine digits, one letter — e.g. P051234567M (entities) or A001234567Z
    // (individuals). Deliberately not restricting the leading letter to A/P: KRA has issued others,
    // and rejecting a client's real PIN is worse than accepting an unusual one.
    [GeneratedRegex(@"^[A-Z]\d{9}[A-Z]$")]
    private static partial Regex KraPinPattern();

    /// <summary>
    /// Normalises a KRA PIN to its canonical upper-case, unspaced form. Returns null for blank
    /// input (the field is optional); throws when a value is supplied but malformed, because a
    /// mistyped PIN silently produces invalid tax invoices downstream.
    /// </summary>
    public static string? NormaliseKraPin(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var pin = raw.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
        if (!KraPinPattern().IsMatch(pin))
            throw new InvalidOperationException(
                $"'{raw.Trim()}' is not a valid KRA PIN. Expected one letter, nine digits and one letter, e.g. P051234567M.");

        return pin;
    }

    /// <summary>
    /// The firm's service lines, used as the lead product-range vocabulary. Mirrors the list the
    /// public site offers so a web enquiry and a sales-captured lead describe demand the same way.
    /// </summary>
    public static readonly IReadOnlyList<string> ProductRanges =
    [
        "Calibration Services",
        "Inspection Services",
        "Equipment Repair & Maintenance",
        "Fleet / Asset Management",
        "Training",
        "Other",
    ];

    /// <summary>
    /// Normalises a selected product range to stored form: known values only, de-duplicated, in the
    /// canonical order, joined with ", ". Unknown values are rejected rather than silently dropped —
    /// a lead recorded against a service line the firm doesn't offer would misroute it.
    /// </summary>
    public static string? NormaliseProductRange(IEnumerable<string>? selected)
    {
        if (selected is null) return null;

        var chosen = selected
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .ToList();
        if (chosen.Count == 0) return null;

        var unknown = chosen
            .Where(v => !ProductRanges.Contains(v, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"Unknown product range(s): {string.Join(", ", unknown)}. Accepted values: {string.Join(", ", ProductRanges)}.");

        var ordered = ProductRanges
            .Where(known => chosen.Any(c => string.Equals(c, known, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return string.Join(", ", ordered);
    }

    /// <summary>Splits stored product-range text back into its parts for the API/UI.</summary>
    public static List<string> SplitProductRange(string? stored) =>
        string.IsNullOrWhiteSpace(stored)
            ? []
            : stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
