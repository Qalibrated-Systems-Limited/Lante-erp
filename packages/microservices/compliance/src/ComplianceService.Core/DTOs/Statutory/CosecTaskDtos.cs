using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Statutory;

public class CosecTaskReadDto
{
    public string Id { get; set; } = string.Empty;
    public string? ObligationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ResponsiblePersonUserId { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public DateTime DueDate { get; set; }
    public CosecTaskStatus Status { get; set; }
}

public class CreateCosecTaskDto
{
    public string? ObligationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ResponsiblePersonUserId { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public DateTime DueDate { get; set; }
}

public class UpdateCosecTaskStatusDto
{
    public CosecTaskStatus Status { get; set; }
}

// Core-field edit — Status stays workflow-controlled (see UpdateCosecTaskStatusDto).
public class UpdateCosecTaskDto
{
    public string? ObligationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ResponsiblePersonUserId { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public DateTime DueDate { get; set; }
}
