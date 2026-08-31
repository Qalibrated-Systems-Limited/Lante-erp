using HSEService.Core.DTOs.Common;

namespace HSEService.Core.DTOs.Training;

public class HseTrainingRecordReadDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string Course { get; set; } = string.Empty;
    public DateTime CompletedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? CertificateUrl { get; set; }
}

public class CreateHseTrainingRecordDto
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string Course { get; set; } = string.Empty;
    public DateTime CompletedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? CertificateUrl { get; set; }
}

// Core-field edit — EmployeeUserId/EmployeeName stay fixed after creation.
public class UpdateHseTrainingRecordDto
{
    public string Course { get; set; } = string.Empty;
    public DateTime CompletedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? CertificateUrl { get; set; }
}

public class HseTrainingRecordFilterParameters : PaginationParameters
{
    public string? EmployeeUserId { get; set; }
}
