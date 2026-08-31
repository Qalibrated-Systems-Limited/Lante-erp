using ComplianceService.Core.DTOs.Common;

namespace ComplianceService.Core.DTOs.Training;

public class AntiBriberyTrainingFilterParameters : PaginationParameters
{
    public string? EmployeeUserId { get; set; }
}

public class AntiBriberyTrainingReadDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime CompletedOn { get; set; }
    public DateTime NextDueOn { get; set; }
    public string? CertificateUrl { get; set; }
}

public class CreateAntiBriberyTrainingDto
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime CompletedOn { get; set; }
    public string? CertificateUrl { get; set; }
}

// Core-field edit — EmployeeUserId/EmployeeName stay fixed after creation; NextDueOn is
// recomputed from CompletedOn, same 2-year rule as creation.
public class UpdateAntiBriberyTrainingDto
{
    public DateTime CompletedOn { get; set; }
    public string? CertificateUrl { get; set; }
}
