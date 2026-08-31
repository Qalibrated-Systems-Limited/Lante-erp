namespace TicketingService.Core.DTOs.Tags;

public class CreateTagDto
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Description { get; set; }
}

public class UpdateTagDto
{
    public string? Name { get; set; }
    public string? Color { get; set; }
    public string? Description { get; set; }
}

public class TagReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Description { get; set; }
}

public class AddTagToTicketDto
{
    public string TagId { get; set; } = string.Empty;
}
