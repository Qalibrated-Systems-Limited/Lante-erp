namespace LicenseService.Core.Constants;

/// <summary>
/// Every application that can be licensed.
/// The "app" JWT claim must match one of these values exactly.
/// Client apps declare their own ID in licenseUtils.js → THIS_APP_ID.
/// </summary>
public static class LicenseAppIds
{
    public const string QaliTrackFrontend = "qalitrack-frontend";
    public const string QaliTrackMobile   = "qalitrack-mobile";
    public const string QaliTrackKiosk    = "qalitrack-kiosk";

    public static readonly IReadOnlyList<string> All =
    [
        QaliTrackFrontend,
        QaliTrackMobile,
        QaliTrackKiosk,
    ];

    private static readonly HashSet<string> _valid =
        new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string value) => _valid.Contains(value);
}
