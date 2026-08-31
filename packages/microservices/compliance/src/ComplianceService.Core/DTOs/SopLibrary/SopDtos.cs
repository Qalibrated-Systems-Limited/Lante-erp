namespace ComplianceService.Core.DTOs.SopLibrary;

public class SopReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public DateTime LastReviewed { get; set; }
    public DateTime? NextReview { get; set; }
}

// No Code here on purpose — Code is always generated server-side (SopLibraryService), never
// accepted from the client.
public class CreateSopDto
{
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime? NextReview { get; set; }
    public string? FileUrl { get; set; }
}

public class UpdateSopDto
{
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime? NextReview { get; set; }
    public string? FileUrl { get; set; }

    // Optional revision bump on edit, e.g. "Rev 2" — leave null/blank to keep the current version.
    public string? Version { get; set; }
}
