using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Legal;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C12 (P13, CRM-059..062) — legal &amp; contract register. NDAs default to a 3-year term;
/// carriers are gated on vetting (Approved) plus in-date compliance docs. Expiry alerts are fired by
/// the background worker (<c>CheckLegalDocExpiryAsync</c>); this service owns CRUD + status transitions.</summary>
public class LegalService(
    IGenericRepository<NdaRegister> ndas,
    IGenericRepository<FrameworkAgreement> frameworks,
    IGenericRepository<SubcontractorAgreement> subcontracts,
    IGenericRepository<CarrierAgreement> carriers,
    IMapper mapper) : ILegalService
{
    // ── NDAs ──
    public async Task<List<NdaDto>> GetNdasAsync(string? status)
    {
        var q = ndas.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<NdaStatus>(status, true, out var st)) q = q.Where(n => n.Status == st);
        var list = await q.OrderBy(n => n.ExpiryDate).ToListAsync();
        return list.Select(ToNdaDto).ToList();
    }

    public async Task<NdaDto> CreateNdaAsync(SaveNdaDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.CounterpartyName)) throw new InvalidOperationException("Counterparty name is required.");
        var n = new NdaRegister
        {
            NdaNumber = await NextNdaNumberAsync(),
            CounterpartyName = dto.CounterpartyName.Trim(), CustomerId = dto.CustomerId, Purpose = dto.Purpose,
            SignedDate = dto.SignedDate,
            ExpiryDate = dto.ExpiryDate ?? dto.SignedDate.AddYears(3),
            FileUrl = dto.FileUrl, Status = NdaStatus.Active, CreatedBy = userId, UpdatedBy = userId,
        };
        if (n.ExpiryDate <= n.SignedDate) throw new InvalidOperationException("Expiry date must be after the signed date.");
        await ndas.CreateAsync(n);
        return ToNdaDto(n);
    }

    public async Task<NdaDto?> UpdateNdaAsync(string id, SaveNdaDto dto, string userId)
    {
        var n = await ndas.GetByIdAsync(id);
        if (n is null) return null;
        n.CounterpartyName = dto.CounterpartyName.Trim(); n.CustomerId = dto.CustomerId; n.Purpose = dto.Purpose;
        n.SignedDate = dto.SignedDate; n.FileUrl = dto.FileUrl;
        var newExpiry = dto.ExpiryDate ?? dto.SignedDate.AddYears(3);
        if (n.ExpiryDate != newExpiry) { n.ExpiryDate = newExpiry; n.ExpiryAlert60SentAt = null; }
        Touch(n, userId);
        await ndas.UpdateAsync(n);
        return ToNdaDto(n);
    }

    public async Task<LegalActionResult> TerminateNdaAsync(string id, string userId)
    {
        var n = await ndas.GetByIdAsync(id);
        if (n is null) return new LegalActionResult("Error", "NDA not found.");
        if (n.Status != NdaStatus.Active) return new LegalActionResult("Error", "Only an active NDA can be terminated.");
        n.Status = NdaStatus.Terminated; Touch(n, userId);
        await ndas.UpdateAsync(n);
        return new LegalActionResult("Terminated", "NDA terminated.");
    }

    // ── Framework agreements ──
    public async Task<List<FrameworkDto>> GetFrameworksAsync(string? status)
    {
        var q = frameworks.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<FrameworkStatus>(status, true, out var st)) q = q.Where(f => f.Status == st);
        var list = await q.OrderBy(f => f.EndDate).ToListAsync();
        return list.Select(ToFrameworkDto).ToList();
    }

    public async Task<FrameworkDto> CreateFrameworkAsync(SaveFrameworkDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.CounterpartyName)) throw new InvalidOperationException("Counterparty name is required.");
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new InvalidOperationException("Title is required.");
        if (dto.EndDate <= dto.StartDate) throw new InvalidOperationException("End date must be after start date.");
        var f = new FrameworkAgreement
        {
            AgreementNumber = await NextFrameworkNumberAsync(),
            CounterpartyName = dto.CounterpartyName.Trim(), CustomerId = dto.CustomerId,
            Title = dto.Title.Trim(), Scope = dto.Scope,
            StartDate = dto.StartDate, EndDate = dto.EndDate, PerformanceReviewDate = dto.PerformanceReviewDate,
            Value = dto.Value, FileUrl = dto.FileUrl, Status = FrameworkStatus.Active, CreatedBy = userId, UpdatedBy = userId,
        };
        await frameworks.CreateAsync(f);
        return ToFrameworkDto(f);
    }

    public async Task<FrameworkDto?> UpdateFrameworkAsync(string id, SaveFrameworkDto dto, string userId)
    {
        var f = await frameworks.GetByIdAsync(id);
        if (f is null) return null;
        if (dto.EndDate <= dto.StartDate) throw new InvalidOperationException("End date must be after start date.");
        f.CounterpartyName = dto.CounterpartyName.Trim(); f.CustomerId = dto.CustomerId;
        f.Title = dto.Title.Trim(); f.Scope = dto.Scope; f.Value = dto.Value; f.FileUrl = dto.FileUrl;
        f.StartDate = dto.StartDate;
        if (f.PerformanceReviewDate != dto.PerformanceReviewDate) { f.PerformanceReviewDate = dto.PerformanceReviewDate; f.ReviewAlertSentAt = null; }
        if (f.EndDate != dto.EndDate) { f.EndDate = dto.EndDate; f.RenewalAlert60SentAt = null; f.RenewalAlert30SentAt = null; }
        Touch(f, userId);
        await frameworks.UpdateAsync(f);
        return ToFrameworkDto(f);
    }

    public async Task<LegalActionResult> TerminateFrameworkAsync(string id, string userId)
    {
        var f = await frameworks.GetByIdAsync(id);
        if (f is null) return new LegalActionResult("Error", "Agreement not found.");
        if (f.Status is FrameworkStatus.Terminated or FrameworkStatus.Expired) return new LegalActionResult("Error", "Agreement is already closed.");
        f.Status = FrameworkStatus.Terminated; Touch(f, userId);
        await frameworks.UpdateAsync(f);
        return new LegalActionResult("Terminated", "Framework agreement terminated.");
    }

    // ── Subcontractor agreements ──
    public async Task<List<SubcontractDto>> GetSubcontractsAsync(string? status)
    {
        var q = subcontracts.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SubcontractStatus>(status, true, out var st)) q = q.Where(s => s.Status == st);
        var list = await q.OrderBy(s => s.EndDate).ToListAsync();
        return list.Select(ToSubcontractDto).ToList();
    }

    public async Task<SubcontractDto> CreateSubcontractAsync(SaveSubcontractDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.SubcontractorName)) throw new InvalidOperationException("Subcontractor name is required.");
        if (dto.EndDate <= dto.StartDate) throw new InvalidOperationException("End date must be after start date.");
        var s = new SubcontractorAgreement
        {
            AgreementNumber = await NextSubcontractNumberAsync(),
            SubcontractorName = dto.SubcontractorName.Trim(), SupplierId = dto.SupplierId, ProjectId = dto.ProjectId,
            ScopeOfWork = dto.ScopeOfWork, StartDate = dto.StartDate, EndDate = dto.EndDate, Value = dto.Value,
            InsuranceExpiryDate = dto.InsuranceExpiryDate, FileUrl = dto.FileUrl,
            Status = SubcontractStatus.Active, CreatedBy = userId, UpdatedBy = userId,
        };
        await subcontracts.CreateAsync(s);
        return ToSubcontractDto(s);
    }

    public async Task<SubcontractDto?> UpdateSubcontractAsync(string id, SaveSubcontractDto dto, string userId)
    {
        var s = await subcontracts.GetByIdAsync(id);
        if (s is null) return null;
        if (dto.EndDate <= dto.StartDate) throw new InvalidOperationException("End date must be after start date.");
        s.SubcontractorName = dto.SubcontractorName.Trim(); s.SupplierId = dto.SupplierId; s.ProjectId = dto.ProjectId;
        s.ScopeOfWork = dto.ScopeOfWork; s.Value = dto.Value; s.FileUrl = dto.FileUrl; s.StartDate = dto.StartDate;
        if (s.InsuranceExpiryDate != dto.InsuranceExpiryDate) { s.InsuranceExpiryDate = dto.InsuranceExpiryDate; s.InsuranceAlertSentAt = null; }
        if (s.EndDate != dto.EndDate) { s.EndDate = dto.EndDate; s.RenewalAlert60SentAt = null; s.RenewalAlert30SentAt = null; }
        Touch(s, userId);
        await subcontracts.UpdateAsync(s);
        return ToSubcontractDto(s);
    }

    public async Task<LegalActionResult> TerminateSubcontractAsync(string id, string userId)
    {
        var s = await subcontracts.GetByIdAsync(id);
        if (s is null) return new LegalActionResult("Error", "Agreement not found.");
        if (s.Status is SubcontractStatus.Terminated or SubcontractStatus.Completed) return new LegalActionResult("Error", "Agreement is already closed.");
        s.Status = SubcontractStatus.Terminated; Touch(s, userId);
        await subcontracts.UpdateAsync(s);
        return new LegalActionResult("Terminated", "Subcontractor agreement terminated.");
    }

    // ── Carrier agreements (+ vetting gate) ──
    public async Task<List<CarrierDto>> GetCarriersAsync(string? vettingStatus)
    {
        var q = carriers.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(vettingStatus) && Enum.TryParse<CarrierVettingStatus>(vettingStatus, true, out var vs)) q = q.Where(c => c.VettingStatus == vs);
        var list = await q.OrderBy(c => c.CarrierName).ToListAsync();
        return list.Select(ToCarrierDto).ToList();
    }

    public async Task<CarrierDto?> GetCarrierAsync(string id)
    {
        var c = await carriers.GetByIdAsync(id);
        return c is null ? null : ToCarrierDto(c);
    }

    public async Task<CarrierDto> CreateCarrierAsync(SaveCarrierDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.CarrierName)) throw new InvalidOperationException("Carrier name is required.");
        var c = new CarrierAgreement
        {
            AgreementNumber = await NextCarrierNumberAsync(),
            CarrierName = dto.CarrierName.Trim(), SupplierId = dto.SupplierId,
            NtsaLicenceNumber = dto.NtsaLicenceNumber, NtsaLicenceExpiry = dto.NtsaLicenceExpiry,
            GoodsInTransitInsuranceExpiry = dto.GoodsInTransitInsuranceExpiry, VehicleInspectionExpiry = dto.VehicleInspectionExpiry,
            FileUrl = dto.FileUrl, VettingStatus = CarrierVettingStatus.Pending, Status = CarrierStatus.Active,
            CreatedBy = userId, UpdatedBy = userId,
        };
        await carriers.CreateAsync(c);
        return ToCarrierDto(c);
    }

    public async Task<CarrierDto?> UpdateCarrierAsync(string id, SaveCarrierDto dto, string userId)
    {
        var c = await carriers.GetByIdAsync(id);
        if (c is null) return null;
        c.CarrierName = dto.CarrierName.Trim(); c.SupplierId = dto.SupplierId; c.NtsaLicenceNumber = dto.NtsaLicenceNumber;
        c.FileUrl = dto.FileUrl;
        if (c.NtsaLicenceExpiry != dto.NtsaLicenceExpiry) { c.NtsaLicenceExpiry = dto.NtsaLicenceExpiry; c.NtsaAlertSentAt = null; }
        if (c.GoodsInTransitInsuranceExpiry != dto.GoodsInTransitInsuranceExpiry) { c.GoodsInTransitInsuranceExpiry = dto.GoodsInTransitInsuranceExpiry; c.InsuranceAlertSentAt = null; }
        if (c.VehicleInspectionExpiry != dto.VehicleInspectionExpiry) { c.VehicleInspectionExpiry = dto.VehicleInspectionExpiry; c.InspectionAlertSentAt = null; }
        Touch(c, userId);
        await carriers.UpdateAsync(c);
        return ToCarrierDto(c);
    }

    public async Task<LegalActionResult> VetCarrierAsync(string id, VetCarrierDto dto, string userId)
    {
        var c = await carriers.GetByIdAsync(id);
        if (c is null) return new LegalActionResult("Error", "Carrier not found.");
        c.VettingStatus = dto.Approve ? CarrierVettingStatus.Approved : CarrierVettingStatus.Rejected;
        c.VettingNotes = dto.Notes; c.VettedAt = DateTime.UtcNow; c.VettedBy = userId;
        if (!dto.Approve) c.Status = CarrierStatus.Suspended;
        Touch(c, userId);
        await carriers.UpdateAsync(c);
        return new LegalActionResult(c.VettingStatus.ToString(), dto.Approve ? "Carrier approved for use." : "Carrier rejected.");
    }

    public async Task<LegalActionResult> SuspendCarrierAsync(string id, string userId)
    {
        var c = await carriers.GetByIdAsync(id);
        if (c is null) return new LegalActionResult("Error", "Carrier not found.");
        c.Status = CarrierStatus.Suspended; Touch(c, userId);
        await carriers.UpdateAsync(c);
        return new LegalActionResult("Suspended", "Carrier suspended.");
    }

    // ── Summary ──
    public async Task<LegalSummaryDto> GetSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var soon = now.AddDays(60);
        var allNdas = await ndas.Query().AsNoTracking().ToListAsync();
        var allFw = await frameworks.Query().AsNoTracking().ToListAsync();
        var allSub = await subcontracts.Query().AsNoTracking().ToListAsync();
        var allCar = await carriers.Query().AsNoTracking().ToListAsync();

        return new LegalSummaryDto
        {
            ActiveNdas = allNdas.Count(n => n.Status == NdaStatus.Active),
            NdasExpiringSoon = allNdas.Count(n => n.Status == NdaStatus.Active && n.ExpiryDate > now && n.ExpiryDate <= soon),
            ActiveFrameworks = allFw.Count(f => f.Status == FrameworkStatus.Active),
            FrameworksExpiringSoon = allFw.Count(f => f.Status == FrameworkStatus.Active && f.EndDate > now && f.EndDate <= soon),
            ReviewsDue = allFw.Count(f => f.Status == FrameworkStatus.Active && f.PerformanceReviewDate != null && f.PerformanceReviewDate <= now),
            ActiveSubcontracts = allSub.Count(s => s.Status == SubcontractStatus.Active),
            SubcontractInsuranceExpiring = allSub.Count(s => s.Status == SubcontractStatus.Active && s.InsuranceExpiryDate != null && s.InsuranceExpiryDate <= soon),
            Carriers = allCar.Count,
            CarriersPendingVetting = allCar.Count(c => c.VettingStatus == CarrierVettingStatus.Pending),
            CarrierComplianceExpiring = allCar.Count(c => c.VettingStatus == CarrierVettingStatus.Approved && HasExpiringCompliance(c, now.AddDays(30))),
        };
    }

    // ── Helpers ──
    private static bool HasExpiringCompliance(CarrierAgreement c, DateTime threshold)
        => (c.NtsaLicenceExpiry != null && c.NtsaLicenceExpiry <= threshold)
        || (c.GoodsInTransitInsuranceExpiry != null && c.GoodsInTransitInsuranceExpiry <= threshold)
        || (c.VehicleInspectionExpiry != null && c.VehicleInspectionExpiry <= threshold);

    private NdaDto ToNdaDto(NdaRegister n)
    {
        var dto = mapper.Map<NdaDto>(n);
        dto.DaysToExpiry = (int)Math.Ceiling((n.ExpiryDate - DateTime.UtcNow).TotalDays);
        return dto;
    }
    private FrameworkDto ToFrameworkDto(FrameworkAgreement f)
    {
        var dto = mapper.Map<FrameworkDto>(f);
        dto.DaysToExpiry = (int)Math.Ceiling((f.EndDate - DateTime.UtcNow).TotalDays);
        return dto;
    }
    private SubcontractDto ToSubcontractDto(SubcontractorAgreement s)
    {
        var dto = mapper.Map<SubcontractDto>(s);
        dto.DaysToExpiry = (int)Math.Ceiling((s.EndDate - DateTime.UtcNow).TotalDays);
        dto.InsuranceExpired = s.InsuranceExpiryDate != null && s.InsuranceExpiryDate < DateTime.UtcNow;
        return dto;
    }
    private CarrierDto ToCarrierDto(CarrierAgreement c)
    {
        var dto = mapper.Map<CarrierDto>(c);
        var now = DateTime.UtcNow;
        var expired = (c.NtsaLicenceExpiry != null && c.NtsaLicenceExpiry < now)
            || (c.GoodsInTransitInsuranceExpiry != null && c.GoodsInTransitInsuranceExpiry < now)
            || (c.VehicleInspectionExpiry != null && c.VehicleInspectionExpiry < now);
        dto.ComplianceExpired = expired;
        dto.IsUsable = c.VettingStatus == CarrierVettingStatus.Approved && c.Status == CarrierStatus.Active && !expired;
        return dto;
    }

    private static string Prefix(string kind) => $"{kind}-{DateTime.UtcNow.Year}-";
    private async Task<string> NextNdaNumberAsync()
    { var p = Prefix("NDA"); return $"{p}{(await ndas.Query().CountAsync(x => x.NdaNumber.StartsWith(p)) + 1):D4}"; }
    private async Task<string> NextFrameworkNumberAsync()
    { var p = Prefix("FRM"); return $"{p}{(await frameworks.Query().CountAsync(x => x.AgreementNumber.StartsWith(p)) + 1):D4}"; }
    private async Task<string> NextSubcontractNumberAsync()
    { var p = Prefix("SUB"); return $"{p}{(await subcontracts.Query().CountAsync(x => x.AgreementNumber.StartsWith(p)) + 1):D4}"; }
    private async Task<string> NextCarrierNumberAsync()
    { var p = Prefix("CAR"); return $"{p}{(await carriers.Query().CountAsync(x => x.AgreementNumber.StartsWith(p)) + 1):D4}"; }

    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
