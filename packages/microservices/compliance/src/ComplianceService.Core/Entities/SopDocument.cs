namespace ComplianceService.Core.Entities;

// Quality module's SOP Library — departmental procedures with a current revision and review
// history. Code (e.g. "QSL/QP/19") is generated server-side (see SopLibraryService) so a client
// can never spoof or collide a code; never trust a Code value from the request body.
public class SopDocument : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Version { get; set; } = "Rev 1";
    public string? FileUrl { get; set; }
    public DateTime LastReviewed { get; set; } = DateTime.UtcNow;
    public DateTime? NextReview { get; set; }
}
