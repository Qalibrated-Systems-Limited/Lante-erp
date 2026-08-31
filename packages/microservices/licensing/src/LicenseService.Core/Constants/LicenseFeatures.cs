namespace LicenseService.Core.Constants;

/// <summary>
/// Every licensable feature flag across all Qalibrated Systems products.
///
/// VALUE  — the string embedded in the JWT features[] claim and stored in the DB.
///          Must match exactly what the client app's FeatureLicenseGate expects.
///
/// Adding a new feature:
///   1. Add a constant + FeatureInfo entry here.
///   2. Wrap the module in the client app with &lt;FeatureLicenseGate feature="your_value"&gt;.
///   3. That's it — no other backend changes needed.
/// </summary>
public static class LicenseFeatures
{
    // ── Hardware peripherals ─────────────────────────────────────────────────

    /// <summary>ANPR / NPR camera — automatic number plate recognition on entry/exit.</summary>
    public const string Anpr           = "anpr";

    /// <summary>Thermal ticket printer — auto-print weigh tickets at booth or kiosk.</summary>
    public const string TicketPrinter  = "ticket_printer";

    /// <summary>RFID reader — vehicle identification by UHF tag.</summary>
    public const string Rfid           = "rfid";

    /// <summary>NFC reader — driver identification by NFC card tap.</summary>
    public const string Nfc            = "nfc";

    // ── Software modules ─────────────────────────────────────────────────────

    /// <summary>Self-service weighbridge kiosk terminal.</summary>
    public const string Kiosk          = "kiosk";

    /// <summary>Dual lane / second independent weighbridge.</summary>
    public const string DualLane       = "dual_lane";

    /// <summary>Advanced report builder beyond standard weigh reports.</summary>
    public const string Reports        = "reports";

    /// <summary>Analytics dashboard — charts, trends, KPIs.</summary>
    public const string Analytics      = "analytics";

    /// <summary>User management — create, edit, deactivate operator and admin accounts.</summary>
    public const string UserManagement = "user_management";

    /// <summary>Shifts — define, assign, and track operator work shifts.</summary>
    public const string Shifts         = "shifts";

    /// <summary>Boom barrier / gate controller — auto-open on weigh completion (future).</summary>
    public const string BoomBarrier    = "boom_barrier";

    /// <summary>SMS gateway — automated alerts to drivers and managers (future).</summary>
    public const string SmsAlerts      = "sms_alerts";

    /// <summary>Backup and microservice management — scheduled and manual database backups.</summary>
    public const string Backup         = "backup";

    // ─────────────────────────────────────────────────────────────────────────
    // Catalogue — used for validation and the /metadata endpoint
    // ─────────────────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<FeatureInfo> All = new List<FeatureInfo>
    {
        // Hardware
        new(Anpr,          "ANPR / NPR Camera",               FeatureGroup.Hardware),
        new(TicketPrinter, "Ticket Printer",                  FeatureGroup.Hardware),
        new(Rfid,          "RFID Reader",                     FeatureGroup.Hardware),
        new(Nfc,           "NFC Reader",                      FeatureGroup.Hardware),
        // Modules
        new(Kiosk,         "Unmanned Kiosk",                  FeatureGroup.Modules),
        new(DualLane,      "Dual Lane / Second Scale",        FeatureGroup.Modules),
        new(Reports,       "Advanced Reports",                FeatureGroup.Modules),
        new(Analytics,     "Analytics Dashboard",             FeatureGroup.Modules),
        new(UserManagement,"User Management",                  FeatureGroup.Modules),
        new(Shifts,        "Shifts",                          FeatureGroup.Modules),
        new(BoomBarrier,   "Boom Barrier Controller",          FeatureGroup.Modules),
        new(SmsAlerts,     "SMS Alerts",                      FeatureGroup.Modules),
        new(Backup,        "Backup & Microservice Management",FeatureGroup.Modules),
    };

    private static readonly HashSet<string> _valid =
        new(All.Select(f => f.Value), StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns true if the feature value is in the catalogue.</summary>
    public static bool IsValid(string value) => _valid.Contains(value);

    /// <summary>Returns any values that are not in the catalogue.</summary>
    public static IEnumerable<string> FindUnknown(IEnumerable<string> values) =>
        values.Where(v => !_valid.Contains(v));
}

public enum FeatureGroup { Hardware, Modules }

public record FeatureInfo(string Value, string Label, FeatureGroup Group);
