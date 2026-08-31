using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// COMP-004: access/erasure/correction requests tracked to a 30-day response
// deadline per the Data Protection Act 2019. DueBy is computed at creation
// (ReceivedOn + 30 days), not recalculated client-side.
public class DataSubjectRequest : BaseEntity
{
    public DsrType Type { get; set; }
    public string RequestorName { get; set; } = string.Empty;
    public string? RequestorContact { get; set; }
    public DateTime ReceivedOn { get; set; }
    public DateTime DueBy { get; set; }
    public DsrStatus Status { get; set; } = DsrStatus.Open;
    public DateTime? CompletedOn { get; set; }
    public string? Notes { get; set; }
}
