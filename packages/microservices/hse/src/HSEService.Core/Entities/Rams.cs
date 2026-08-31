using HSEService.Core.Enums;

namespace HSEService.Core.Entities;

// HSE-002: Risk Assessment & Method Statement library — upload, version control, and issue
// tracking per site/project. Each revision is its own row (Version increments per SiteId+Title)
// so the full history stays queryable, matching TicketHistory's append-only convention.
public class Rams : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string? SubcontractorId { get; set; }
    public string? SubcontractorName { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public RamsStatus Status { get; set; } = RamsStatus.Draft;
    public string? FileUrl { get; set; }
    public string? UploadedByUserId { get; set; }
    public string? IssueNotes { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
