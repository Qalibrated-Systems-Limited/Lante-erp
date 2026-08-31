namespace HSEService.Core.DTOs.Sites;

public class SiteReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSiteDto
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
}

public class UpdateSiteDto : CreateSiteDto
{
    public bool IsActive { get; set; } = true;
}
