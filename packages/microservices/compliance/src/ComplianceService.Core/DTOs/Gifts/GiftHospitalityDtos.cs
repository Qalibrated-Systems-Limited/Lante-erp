using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Gifts;

public class GiftHospitalityFilterParameters : PaginationParameters
{
    public bool? FlaggedOnly { get; set; }
}

public class GiftHospitalityReadDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public GiftDirection Direction { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public bool IsGovernmentOfficial { get; set; }
    public string? Description { get; set; }
    public decimal Value { get; set; }
    public DateTime Date { get; set; }
    public bool Flagged { get; set; }
}

public class CreateGiftHospitalityDto
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public GiftDirection Direction { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public bool IsGovernmentOfficial { get; set; }
    public string? Description { get; set; }
    public decimal Value { get; set; }
    public DateTime Date { get; set; }
}

// Same shape as CreateGiftHospitalityDto — Flagged is recomputed from Value/IsGovernmentOfficial
// on every update, same threshold logic as creation.
public class UpdateGiftHospitalityDto
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public GiftDirection Direction { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public bool IsGovernmentOfficial { get; set; }
    public string? Description { get; set; }
    public decimal Value { get; set; }
    public DateTime Date { get; set; }
}
