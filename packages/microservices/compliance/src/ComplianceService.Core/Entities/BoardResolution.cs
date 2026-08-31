namespace ComplianceService.Core.Entities;

// COMP-007: every board resolution logged with date, reference number and scanned copy.
public class BoardResolution : BaseEntity
{
    public string ReferenceNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime ResolutionDate { get; set; }
    public string? Summary { get; set; }
    public string? ScannedCopyUrl { get; set; }
}
