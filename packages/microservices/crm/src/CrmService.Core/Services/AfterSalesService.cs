using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.AfterSales;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C11 (P12, CRM-055..058) — after-sales &amp; retention. Surveys/NPS update the denormalised
/// score on CUSTOMER on response; complaints run Open→Assigned→InProgress→Resolved→Closed; service
/// contracts renew into a fresh contract. Renewal alerts are fired by the background worker.</summary>
public class AfterSalesService(
    IGenericRepository<ClientSatisfactionSurvey> surveys,
    IGenericRepository<ServiceContract> contracts,
    IGenericRepository<ClientComplaint> complaints,
    IGenericRepository<NpsSurvey> nps,
    IGenericRepository<Customer> customers,
    IGenericRepository<ActivityTask> tasks,
    IMapper mapper) : IAfterSalesService
{
    // ── Satisfaction surveys (CSAT) ──
    public async Task<List<SurveyDto>> GetSurveysAsync(string? customerId, string? status)
    {
        var q = surveys.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(customerId)) q = q.Where(s => s.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SurveyStatus>(status, true, out var st)) q = q.Where(s => s.Status == st);
        return mapper.Map<List<SurveyDto>>(await q.OrderByDescending(s => s.SentAt).ToListAsync());
    }

    public async Task<SurveyDto> SendSurveyAsync(SendSurveyDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerId)) throw new InvalidOperationException("CustomerId is required.");
        var customer = await customers.GetByIdAsync(dto.CustomerId);
        var survey = new ClientSatisfactionSurvey
        {
            CustomerId = dto.CustomerId,
            CustomerName = customer?.Name,
            ProjectId = dto.ProjectId,
            ProjectName = dto.ProjectName,
            Source = string.IsNullOrWhiteSpace(dto.ProjectId) ? SurveySource.Manual : SurveySource.ProjectClose,
            Status = SurveyStatus.Pending,
            CreatedBy = userId, UpdatedBy = userId,
        };
        await surveys.CreateAsync(survey);
        return mapper.Map<SurveyDto>(survey);
    }

    public async Task<SurveyDto?> RespondSurveyAsync(string id, SurveyResponseDto dto, string userId)
    {
        var s = await surveys.GetByIdAsync(id);
        if (s is null) return null;
        if (dto.Score < 1 || dto.Score > 5) throw new InvalidOperationException("Score must be between 1 and 5.");
        s.Score = dto.Score; s.Feedback = dto.Feedback; s.Status = SurveyStatus.Completed;
        s.RespondedAt = DateTime.UtcNow; Touch(s, userId);
        await surveys.UpdateAsync(s);

        // Denormalise the latest CSAT onto the customer for the account view.
        var customer = await customers.GetByIdAsync(s.CustomerId);
        if (customer is not null) { customer.LastSatisfactionScore = dto.Score; Touch(customer, userId); await customers.UpdateAsync(customer); }
        return mapper.Map<SurveyDto>(s);
    }

    // ── Service contracts ──
    public async Task<List<ServiceContractDto>> GetServiceContractsAsync(string? customerId, string? status)
    {
        var q = contracts.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(customerId)) q = q.Where(c => c.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ServiceContractStatus>(status, true, out var st)) q = q.Where(c => c.Status == st);
        var list = await q.OrderBy(c => c.EndDate).ToListAsync();
        return list.Select(ToContractDto).ToList();
    }

    public async Task<ServiceContractDto?> GetServiceContractAsync(string id)
    {
        var c = await contracts.GetByIdAsync(id);
        return c is null ? null : ToContractDto(c);
    }

    public async Task<ServiceContractDto> CreateServiceContractAsync(SaveServiceContractDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerId)) throw new InvalidOperationException("CustomerId is required.");
        if (dto.EndDate <= dto.StartDate) throw new InvalidOperationException("End date must be after start date.");
        var customer = await customers.GetByIdAsync(dto.CustomerId);
        var c = new ServiceContract
        {
            ContractNumber = await GenerateNumberAsync(),
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName ?? customer?.Name,
            ContractType = ParseType(dto.ContractType),
            Description = dto.Description,
            StartDate = dto.StartDate, EndDate = dto.EndDate, Value = dto.Value,
            BillingFrequency = dto.BillingFrequency, AutoRenew = dto.AutoRenew,
            Status = ServiceContractStatus.Active,
            CreatedBy = userId, UpdatedBy = userId,
        };
        await contracts.CreateAsync(c);
        return ToContractDto(c);
    }

    public async Task<ServiceContractDto?> UpdateServiceContractAsync(string id, SaveServiceContractDto dto, string userId)
    {
        var c = await contracts.GetByIdAsync(id);
        if (c is null) return null;
        if (c.Status != ServiceContractStatus.Active) throw new InvalidOperationException("Only an active contract can be edited.");
        if (dto.EndDate <= dto.StartDate) throw new InvalidOperationException("End date must be after start date.");
        c.ContractType = ParseType(dto.ContractType);
        c.Description = dto.Description;
        c.Value = dto.Value; c.BillingFrequency = dto.BillingFrequency; c.AutoRenew = dto.AutoRenew;
        c.StartDate = dto.StartDate;
        // A changed end date re-arms the renewal alerts.
        if (c.EndDate != dto.EndDate) { c.EndDate = dto.EndDate; c.RenewalAlert60SentAt = null; c.RenewalAlert30SentAt = null; }
        if (!string.IsNullOrWhiteSpace(dto.CustomerName)) c.CustomerName = dto.CustomerName;
        Touch(c, userId);
        await contracts.UpdateAsync(c);
        return ToContractDto(c);
    }

    public async Task<AfterSalesActionResult> RenewServiceContractAsync(string id, RenewServiceContractDto dto, string userId)
    {
        var c = await contracts.GetByIdAsync(id);
        if (c is null) return new AfterSalesActionResult("Error", "Contract not found.");
        if (c.Status is ServiceContractStatus.Renewed or ServiceContractStatus.Cancelled)
            return new AfterSalesActionResult("Error", "Contract is already renewed or cancelled.");
        if (dto.NewEndDate <= c.EndDate) return new AfterSalesActionResult("Error", "New end date must be after the current end date.");

        c.Status = ServiceContractStatus.Renewed; Touch(c, userId);
        await contracts.UpdateAsync(c);

        var renewed = new ServiceContract
        {
            ContractNumber = await GenerateNumberAsync(),
            CustomerId = c.CustomerId, CustomerName = c.CustomerName,
            ContractType = c.ContractType, Description = c.Description,
            StartDate = c.EndDate, EndDate = dto.NewEndDate,
            Value = dto.NewValue ?? c.Value, BillingFrequency = c.BillingFrequency,
            AutoRenew = c.AutoRenew, Status = ServiceContractStatus.Active,
            RenewedFromContractId = c.Id,
            CreatedBy = userId, UpdatedBy = userId,
        };
        await contracts.CreateAsync(renewed);
        return new AfterSalesActionResult("Renewed", $"Renewed as {renewed.ContractNumber}.");
    }

    public async Task<AfterSalesActionResult> CancelServiceContractAsync(string id, string userId)
    {
        var c = await contracts.GetByIdAsync(id);
        if (c is null) return new AfterSalesActionResult("Error", "Contract not found.");
        if (c.Status != ServiceContractStatus.Active) return new AfterSalesActionResult("Error", "Only an active contract can be cancelled.");
        c.Status = ServiceContractStatus.Cancelled; Touch(c, userId);
        await contracts.UpdateAsync(c);
        return new AfterSalesActionResult("Cancelled", "Contract cancelled.");
    }

    // ── Complaints ──
    public async Task<List<ComplaintDto>> GetComplaintsAsync(string? customerId, string? status)
    {
        var q = complaints.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(customerId)) q = q.Where(c => c.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ComplaintStatus>(status, true, out var st)) q = q.Where(c => c.Status == st);
        return mapper.Map<List<ComplaintDto>>(await q.OrderByDescending(c => c.RaisedAt).ToListAsync());
    }

    public async Task<ComplaintDto> RaiseComplaintAsync(RaiseComplaintDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerId)) throw new InvalidOperationException("CustomerId is required.");
        if (string.IsNullOrWhiteSpace(dto.Subject)) throw new InvalidOperationException("Subject is required.");
        var customer = await customers.GetByIdAsync(dto.CustomerId);
        var c = new ClientComplaint
        {
            ComplaintNumber = await GenerateComplaintNumberAsync(),
            CustomerId = dto.CustomerId, CustomerName = dto.CustomerName ?? customer?.Name,
            Subject = dto.Subject.Trim(), Description = dto.Description, Category = dto.Category,
            Severity = Enum.TryParse<ComplaintSeverity>(dto.Severity, true, out var sv) ? sv : ComplaintSeverity.Medium,
            Status = ComplaintStatus.Open, CreatedBy = userId, UpdatedBy = userId,
        };
        await complaints.CreateAsync(c);
        return mapper.Map<ComplaintDto>(c);
    }

    public async Task<AfterSalesActionResult> AssignComplaintAsync(string id, AssignComplaintDto dto, string userId)
    {
        var c = await complaints.GetByIdAsync(id);
        if (c is null) return new AfterSalesActionResult("Error", "Complaint not found.");
        if (c.Status is ComplaintStatus.Resolved or ComplaintStatus.Closed) return new AfterSalesActionResult("Error", "Complaint is already resolved.");
        if (string.IsNullOrWhiteSpace(dto.AssignedTo)) return new AfterSalesActionResult("Error", "AssignedTo is required.");
        c.AssignedTo = dto.AssignedTo; c.AssignedToName = dto.AssignedToName;
        c.Status = ComplaintStatus.Assigned; c.AssignedAt = DateTime.UtcNow; Touch(c, userId);
        await complaints.UpdateAsync(c);
        return new AfterSalesActionResult("Assigned", "Complaint assigned.");
    }

    public async Task<AfterSalesActionResult> StartComplaintAsync(string id, string userId)
    {
        var c = await complaints.GetByIdAsync(id);
        if (c is null) return new AfterSalesActionResult("Error", "Complaint not found.");
        if (c.Status != ComplaintStatus.Assigned) return new AfterSalesActionResult("Error", "Only an assigned complaint can move to in-progress.");
        c.Status = ComplaintStatus.InProgress; Touch(c, userId);
        await complaints.UpdateAsync(c);
        return new AfterSalesActionResult("InProgress", "Complaint in progress.");
    }

    public async Task<AfterSalesActionResult> ResolveComplaintAsync(string id, ResolveComplaintDto dto, string userId)
    {
        var c = await complaints.GetByIdAsync(id);
        if (c is null) return new AfterSalesActionResult("Error", "Complaint not found.");
        if (c.Status is ComplaintStatus.Resolved or ComplaintStatus.Closed) return new AfterSalesActionResult("Error", "Complaint is already resolved.");
        if (string.IsNullOrWhiteSpace(dto.Resolution)) return new AfterSalesActionResult("Error", "Resolution is required.");
        c.Resolution = dto.Resolution; c.Status = ComplaintStatus.Resolved; c.ResolvedAt = DateTime.UtcNow; Touch(c, userId);
        await complaints.UpdateAsync(c);
        return new AfterSalesActionResult("Resolved", "Complaint resolved.");
    }

    public async Task<AfterSalesActionResult> CloseComplaintAsync(string id, string userId)
    {
        var c = await complaints.GetByIdAsync(id);
        if (c is null) return new AfterSalesActionResult("Error", "Complaint not found.");
        if (c.Status != ComplaintStatus.Resolved) return new AfterSalesActionResult("Error", "Only a resolved complaint can be closed.");
        c.Status = ComplaintStatus.Closed; c.ClosedAt = DateTime.UtcNow; Touch(c, userId);
        await complaints.UpdateAsync(c);
        return new AfterSalesActionResult("Closed", "Complaint closed.");
    }

    // ── NPS ──
    public async Task<List<NpsDto>> GetNpsAsync(string? customerId, int? year)
    {
        var q = nps.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(customerId)) q = q.Where(n => n.CustomerId == customerId);
        if (year.HasValue) q = q.Where(n => n.Year == year.Value);
        return mapper.Map<List<NpsDto>>(await q.OrderByDescending(n => n.Year).ThenByDescending(n => n.SentAt).ToListAsync());
    }

    public async Task<NpsDto> SendNpsAsync(SendNpsDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerId)) throw new InvalidOperationException("CustomerId is required.");
        var year = dto.Year ?? DateTime.UtcNow.Year;
        var existing = await nps.Query().FirstOrDefaultAsync(n => n.CustomerId == dto.CustomerId && n.Year == year);
        if (existing is not null) throw new InvalidOperationException($"An NPS survey already exists for this client for {year}.");
        var customer = await customers.GetByIdAsync(dto.CustomerId);
        var n = new NpsSurvey
        {
            CustomerId = dto.CustomerId, CustomerName = dto.CustomerName ?? customer?.Name,
            Year = year, Status = NpsStatus.Pending, Category = NpsCategory.None,
            CreatedBy = userId, UpdatedBy = userId,
        };
        await nps.CreateAsync(n);
        return mapper.Map<NpsDto>(n);
    }

    public async Task<NpsDto?> RespondNpsAsync(string id, NpsResponseDto dto, string userId)
    {
        var n = await nps.GetByIdAsync(id);
        if (n is null) return null;
        if (dto.Score < 0 || dto.Score > 10) throw new InvalidOperationException("Score must be between 0 and 10.");
        n.Score = dto.Score; n.Feedback = dto.Feedback; n.Category = Categorise(dto.Score);
        n.Status = NpsStatus.Completed; n.RespondedAt = DateTime.UtcNow; Touch(n, userId);
        await nps.UpdateAsync(n);

        var customer = await customers.GetByIdAsync(n.CustomerId);
        if (customer is not null) { customer.LastNpsScore = dto.Score; Touch(customer, userId); await customers.UpdateAsync(customer); }
        return mapper.Map<NpsDto>(n);
    }

    // ── Retention summary ──
    public async Task<AfterSalesSummaryDto> GetSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var allSurveys = await surveys.Query().AsNoTracking().ToListAsync();
        var responded = allSurveys.Where(s => s.Status == SurveyStatus.Completed && s.Score != null).ToList();
        var allComplaints = await complaints.Query().AsNoTracking().ToListAsync();
        var allContracts = await contracts.Query().AsNoTracking().ToListAsync();
        var allNps = await nps.Query().AsNoTracking().Where(n => n.Status == NpsStatus.Completed && n.Score != null).ToListAsync();

        var npsTrend = allNps.GroupBy(n => n.Year).OrderBy(g => g.Key).Select(g =>
        {
            var total = g.Count();
            var prom = g.Count(n => n.Category == NpsCategory.Promoter);
            var det = g.Count(n => n.Category == NpsCategory.Detractor);
            return new NpsYearSummary
            {
                Year = g.Key, Responses = total, Promoters = prom,
                Passives = g.Count(n => n.Category == NpsCategory.Passive), Detractors = det,
                NpsScore = total == 0 ? 0 : Math.Round((decimal)(prom - det) / total * 100, 0),
            };
        }).ToList();

        return new AfterSalesSummaryDto
        {
            AverageCsat = responded.Count == 0 ? 0 : Math.Round((decimal)responded.Average(s => s.Score!.Value), 2),
            CsatResponses = responded.Count,
            SurveysPending = allSurveys.Count(s => s.Status == SurveyStatus.Pending),
            CurrentNps = npsTrend.LastOrDefault()?.NpsScore ?? 0,
            OpenComplaints = allComplaints.Count(c => c.Status != ComplaintStatus.Resolved && c.Status != ComplaintStatus.Closed),
            ResolvedThisMonth = allComplaints.Count(c => c.ResolvedAt != null && c.ResolvedAt >= monthStart),
            ActiveServiceContracts = allContracts.Count(c => c.Status == ServiceContractStatus.Active),
            ContractsExpiringSoon = allContracts.Count(c => c.Status == ServiceContractStatus.Active && c.EndDate > now && (c.EndDate - now).TotalDays <= 60),
            NpsTrend = npsTrend,
        };
    }

    // ── O6 — calibration recall (inbound from Operations) ──
    public async Task<AfterSalesActionResult> RecordCalibrationRecallAsync(CalibrationRecallDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.CertificateNumber))
            return new AfterSalesActionResult("Error", "Certificate number is required.");

        // Best-effort resolve the CRM customer by name (Operations only knows the client name).
        Customer? customer = null;
        if (!string.IsNullOrWhiteSpace(dto.ClientName))
        {
            var name = dto.ClientName.Trim();
            customer = await customers.Query()
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
        }

        var due = dto.NextDueDate == default ? DateTime.UtcNow.AddYears(1) : dto.NextDueDate;
        var subject = $"Calibration recall — cert {dto.CertificateNumber} due {due:yyyy-MM-dd}";

        // Idempotency: Operations may re-issue the same certificate; don't stack duplicate recalls.
        var existing = await tasks.Query()
            .AnyAsync(t => t.TaskType == "CalibrationRecall" && t.Subject == subject);
        if (existing)
            return new AfterSalesActionResult("Skipped", "Recall task already exists for this certificate.");

        await tasks.CreateAsync(new ActivityTask
        {
            CustomerId = customer?.Id,
            AssignedTo = string.IsNullOrWhiteSpace(customer?.AccountOwnerId) ? userId : customer!.AccountOwnerId,
            TaskType = "CalibrationRecall",
            Subject = subject,
            DueDate = due,
            Status = ActivityTaskStatus.Open,
            IsAutoCreated = true,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        return customer is null
            ? new AfterSalesActionResult("Unmatched", $"Recall task created but no CRM client matched '{dto.ClientName}'.")
            : new AfterSalesActionResult("Ok", $"Calibration recall scheduled for {customer.Name}.");
    }

    // ── Helpers ──
    private ServiceContractDto ToContractDto(ServiceContract c)
    {
        var dto = mapper.Map<ServiceContractDto>(c);
        dto.DaysToExpiry = (int)Math.Ceiling((c.EndDate - DateTime.UtcNow).TotalDays);
        return dto;
    }

    private static ServiceContractType ParseType(string? raw)
        => Enum.TryParse<ServiceContractType>(raw, true, out var t) ? t : ServiceContractType.Maintenance;

    private static NpsCategory Categorise(int score)
        => score >= 9 ? NpsCategory.Promoter : score >= 7 ? NpsCategory.Passive : NpsCategory.Detractor;

    private async Task<string> GenerateNumberAsync()
    {
        var prefix = $"SVC-{DateTime.UtcNow.Year}-";
        var count = await contracts.Query().CountAsync(c => c.ContractNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task<string> GenerateComplaintNumberAsync()
    {
        var prefix = $"CMP-{DateTime.UtcNow.Year}-";
        var count = await complaints.Query().CountAsync(c => c.ComplaintNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
