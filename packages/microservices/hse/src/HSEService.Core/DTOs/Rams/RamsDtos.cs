using HSEService.Core.DTOs.Common;
using HSEService.Core.Enums;

namespace HSEService.Core.DTOs.Rams;

public class RamsReadDto
{
    public string Id { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string? SubcontractorId { get; set; }
    public string? SubcontractorName { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Version { get; set; }
    public RamsStatus Status { get; set; }
    public string? FileUrl { get; set; }
    public string? IssueNotes { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Uploading against an existing SiteId+Title auto-increments Version (HSE-002 version control).
public class CreateRamsDto
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string? SubcontractorId { get; set; }
    public string? SubcontractorName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? IssueNotes { get; set; }
}

public class UpdateRamsStatusDto
{
    public RamsStatus Status { get; set; }
    public string? IssueNotes { get; set; }
}

// Core-field edit — Status/Version stay workflow-controlled (see UpdateRamsStatusDto / upload versioning).
public class UpdateRamsDto
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string? SubcontractorId { get; set; }
    public string? SubcontractorName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? IssueNotes { get; set; }
}

public class RamsFilterParameters : PaginationParameters
{
    public string? SiteId { get; set; }
}
