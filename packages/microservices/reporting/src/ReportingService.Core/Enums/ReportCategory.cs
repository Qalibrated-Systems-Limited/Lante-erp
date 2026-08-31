namespace ReportingService.Core.Enums;

// Mirrors the upstream ServiceClients this service already integrates with (Finance, Operations,
// Fleet, Stores, Hse, Compliance) — see ReportingService.Infrastructure/ServiceClients.
public enum ReportCategory
{
    Finance = 0,
    Operations = 1,
    Fleet = 2,
    Stores = 3,
    Hse = 4,
    Compliance = 5
}
