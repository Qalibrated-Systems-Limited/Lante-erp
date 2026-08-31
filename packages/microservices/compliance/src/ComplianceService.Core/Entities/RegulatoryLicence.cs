using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// COMP-008 / STAT-003,004,005,007: the SAME entity backs both the general regulatory licence
// tracker and the statutory-calendar's per-type trackers (NCA, NEMA, KEBS/NMK, DOSHS) — the ERD
// companion doc (section 3, "Statutory Compliance Calendar") is explicit that these are one
// table, not four duplicates: "REGULATORY_LICENCE is generic over NCA, NEMA, KEBS/NMK and DOSHS
// licences with per-licence alert_days driving the 90/30-day reminders."
// No stored lifecycle status: current/expiring/expired is derived from ExpiryDate both
// server-side (dashboard counts, alert job) and client-side (badges), same convention as
// HseTrainingRecord in the HSE module.
public class RegulatoryLicence : BaseEntity
{
    public LicenceType Type { get; set; } = LicenceType.Other;
    public string Authority { get; set; } = string.Empty;
    public string? LicenceNumber { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime ExpiryDate { get; set; }

    // Per-record configurable lead time for the primary reminder tier (STAT-003/004: 90 days for
    // NCA/NEMA). KEBS/NMK licences (STAT-005) additionally always get a fixed second reminder at
    // 30 days regardless of this value — see ComplianceAlertsBackgroundService.
    public int AlertDays { get; set; } = 90;

    // Free-text checklist of what renewal requires (e.g. NCA: "audited accounts, staff, equipment
    // evidence") — STAT-003's explicit ask; just informational, not machine-checked.
    public string? RenewalRequirements { get; set; }
}
