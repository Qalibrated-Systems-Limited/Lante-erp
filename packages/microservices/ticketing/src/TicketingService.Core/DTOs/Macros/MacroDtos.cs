namespace TicketingService.Core.DTOs.Macros;

public class CreateMacroDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public bool IsGlobal { get; set; } = true;
}

public class UpdateMacroDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string? CategoryId { get; set; }
    public bool? IsGlobal { get; set; }
}

public class MacroReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public bool IsGlobal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ApplyMacroDto
{
    public string MacroId { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = false;
}
