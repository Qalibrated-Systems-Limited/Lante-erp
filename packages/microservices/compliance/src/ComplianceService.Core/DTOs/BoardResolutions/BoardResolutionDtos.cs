namespace ComplianceService.Core.DTOs.BoardResolutions;

public class BoardResolutionReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ReferenceNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime ResolutionDate { get; set; }
    public string? Summary { get; set; }
    public string? ScannedCopyUrl { get; set; }
}

public class CreateBoardResolutionDto
{
    public string ReferenceNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime ResolutionDate { get; set; }
    public string? Summary { get; set; }
    public string? ScannedCopyUrl { get; set; }
}

public class UpdateBoardResolutionDto
{
    public string ReferenceNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime ResolutionDate { get; set; }
    public string? Summary { get; set; }
    public string? ScannedCopyUrl { get; set; }
}
