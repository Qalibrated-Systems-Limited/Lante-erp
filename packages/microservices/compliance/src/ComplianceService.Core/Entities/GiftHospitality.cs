using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// COMP-001: log of all gifts/hospitality given or received; flag if value exceeds
// Kshs 5,000 (Kshs 2,000 for govt officials). Flagged is computed server-side at
// creation from Value + IsGovernmentOfficial, not left to the client to assert.
public class GiftHospitality : BaseEntity
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public GiftDirection Direction { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public bool IsGovernmentOfficial { get; set; }
    public string? Description { get; set; }
    public decimal Value { get; set; }
    public DateTime Date { get; set; }
    public bool Flagged { get; set; }
}
