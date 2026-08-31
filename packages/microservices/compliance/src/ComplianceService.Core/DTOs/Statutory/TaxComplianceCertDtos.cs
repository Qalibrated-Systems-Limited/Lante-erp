using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Statutory;

public class TaxComplianceCertReadDto
{
    public string Id { get; set; } = string.Empty;
    public TccStatus Status { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string? ItaxRef { get; set; }
    public int AlertDays { get; set; }
    public string Rag { get; set; } = "Green";
}

public class CreateTaxComplianceCertDto
{
    public DateTime ExpiryDate { get; set; }
    public string? ItaxRef { get; set; }
    public int AlertDays { get; set; } = 60;
}

public class RenewTaxComplianceCertDto
{
    public DateTime NewExpiryDate { get; set; }
    public string? ItaxRef { get; set; }
}

// Core-field edit — Status stays workflow-controlled (see RenewTaxComplianceCertDto).
public class UpdateTaxComplianceCertDto
{
    public DateTime ExpiryDate { get; set; }
    public string? ItaxRef { get; set; }
    public int AlertDays { get; set; } = 60;
}
