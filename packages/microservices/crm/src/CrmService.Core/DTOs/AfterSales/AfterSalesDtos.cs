namespace CrmService.Core.DTOs.AfterSales;

// ── Satisfaction surveys (CSAT) ──
public class SendSurveyDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
}
public class SurveyResponseDto { public int Score { get; set; } public string? Feedback { get; set; } }
public class SurveyDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? Score { get; set; }
    public string? Feedback { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

// ── Service contracts ──
public class SaveServiceContractDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string ContractType { get; set; } = "Maintenance";
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Value { get; set; }
    public string? BillingFrequency { get; set; }
    public bool AutoRenew { get; set; }
}
public class RenewServiceContractDto { public DateTime NewEndDate { get; set; } public decimal? NewValue { get; set; } }
public class ServiceContractDto
{
    public string Id { get; set; } = string.Empty;
    public string ContractNumber { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string ContractType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Value { get; set; }
    public string? BillingFrequency { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool AutoRenew { get; set; }
    public int DaysToExpiry { get; set; }
    public string? RenewedFromContractId { get; set; }
}

// ── Complaints ──
public class RaiseComplaintDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string Severity { get; set; } = "Medium";
}
public class AssignComplaintDto { public string AssignedTo { get; set; } = string.Empty; public string? AssignedToName { get; set; } }
public class ResolveComplaintDto { public string Resolution { get; set; } = string.Empty; }
public class ComplaintDto
{
    public string Id { get; set; } = string.Empty;
    public string ComplaintNumber { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public string? Resolution { get; set; }
    public DateTime RaisedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

// ── NPS ──
public class SendNpsDto { public string CustomerId { get; set; } = string.Empty; public string? CustomerName { get; set; } public int? Year { get; set; } }
public class NpsResponseDto { public int Score { get; set; } public string? Feedback { get; set; } }
public class NpsDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public int Year { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? Score { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Feedback { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

// ── After-sales summary (dashboard) ──
public class NpsYearSummary
{
    public int Year { get; set; }
    public int Responses { get; set; }
    public int Promoters { get; set; }
    public int Passives { get; set; }
    public int Detractors { get; set; }
    public decimal NpsScore { get; set; }   // %promoters − %detractors, −100..100
}
public class AfterSalesSummaryDto
{
    public decimal AverageCsat { get; set; }        // 1..5
    public int CsatResponses { get; set; }
    public int SurveysPending { get; set; }
    public decimal CurrentNps { get; set; }
    public int OpenComplaints { get; set; }
    public int ResolvedThisMonth { get; set; }
    public int ActiveServiceContracts { get; set; }
    public int ContractsExpiringSoon { get; set; }  // ≤60 days
    public List<NpsYearSummary> NpsTrend { get; set; } = new();
}

public record AfterSalesActionResult(string Status, string Message);

// ── O6 — calibration recall (inbound from Operations) ──
/// <summary>Notice from Operations that a calibration certificate has a next-due date. CRM turns it into a
/// recall follow-up task for the client's account owner so they schedule the re-calibration in good time.</summary>
public class CalibrationRecallDto
{
    public string CertificateNumber { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? ClientEmail { get; set; }
    public DateTime NextDueDate { get; set; }
}
