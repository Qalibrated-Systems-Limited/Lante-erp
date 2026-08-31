using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>P6 — a 3-way-match discrepancy raised for Finance-Manager resolution. Blocks payment-voucher
/// generation until resolved. (MATCHING_EXCEPTION — new in procurement; the DFD wrongly listed it as a
/// Finance-reused table, but it does not exist there.)</summary>
public class MatchingException : BaseEntity
{
    public string ThreeWayMatchId { get; set; } = string.Empty;
    public string PoId { get; set; } = string.Empty;
    public string? PoNumber { get; set; }
    public MatchExceptionType ExceptionType { get; set; }
    public string? Detail { get; set; }
    public MatchExceptionStatus Status { get; set; } = MatchExceptionStatus.Open;
    public string RaisedBy { get; set; } = string.Empty;
    public DateTime RaisedAt { get; set; } = DateTime.UtcNow;
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}
