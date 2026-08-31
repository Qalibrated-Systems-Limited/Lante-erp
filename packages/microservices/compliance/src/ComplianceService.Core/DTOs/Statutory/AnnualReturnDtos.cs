using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Statutory;

public class AnnualReturnReadDto
{
    public string Id { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime DueDate { get; set; }
    public AnnualReturnStatus Status { get; set; }
    public DateTime? FiledDate { get; set; }
    public string Rag { get; set; } = "Green";
}

public class CreateAnnualReturnDto
{
    public int Year { get; set; }
    public DateTime DueDate { get; set; }
}

public class FileAnnualReturnDto
{
    public DateTime FiledDate { get; set; }
}

// Core-field edit — Status/FiledDate stay workflow-controlled (see FileAnnualReturnDto).
public class UpdateAnnualReturnDto
{
    public int Year { get; set; }
    public DateTime DueDate { get; set; }
}
