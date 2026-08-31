using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Calibration;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>O6 — see <see cref="IReferenceStandardService"/>.</summary>
public class ReferenceStandardService(
    IGenericRepository<ReferenceStandard> standards,
    IMapper mapper) : IReferenceStandardService
{
    public async Task<ReferenceStandardReadDto?> GetByIdAsync(string id)
    {
        var s = await standards.GetByIdAsync(id);
        return s is null || s.IsDeleted ? null : Map(s);
    }

    public async Task<PaginatedResult<ReferenceStandardReadDto>> GetAllAsync(int page, int pageSize, bool activeOnly)
    {
        var query = standards.Query().Where(s => !s.IsDeleted);
        if (activeOnly) query = query.Where(s => s.Status == ReferenceStandardStatus.Active);
        query = query.OrderBy(s => s.NextDueDate);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<ReferenceStandardReadDto>
        {
            Items = items.Select(Map).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<ReferenceStandardReadDto> CreateAsync(CreateReferenceStandardDto dto, string userId)
    {
        var s = mapper.Map<ReferenceStandard>(dto);
        s.Status = ReferenceStandardStatus.Active;
        s.CreatedBy = userId;
        s.UpdatedBy = userId;
        return Map(await standards.CreateAsync(s));
    }

    public async Task<ReferenceStandardReadDto> UpdateAsync(string id, UpdateReferenceStandardDto dto, string userId)
    {
        var s = await standards.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Reference standard {id} not found.");

        if (dto.Description != null) s.Description = dto.Description;
        if (dto.NominalValue != null) s.NominalValue = dto.NominalValue;
        if (dto.AccuracyClass != null) s.AccuracyClass = dto.AccuracyClass;
        if (dto.TraceabilityCertNo != null) s.TraceabilityCertNo = dto.TraceabilityCertNo;
        if (dto.IssuingBody != null) s.IssuingBody = dto.IssuingBody;
        if (dto.CertUncertainty.HasValue) s.CertUncertainty = dto.CertUncertainty;
        if (dto.CoverageFactor.HasValue) s.CoverageFactor = dto.CoverageFactor.Value;
        if (dto.CalibrationDate.HasValue) s.CalibrationDate = dto.CalibrationDate;
        if (dto.NextDueDate.HasValue)
        {
            s.NextDueDate = dto.NextDueDate;
            // A renewed due date reopens the expiry alerts.
            s.Alert60SentAt = null;
            s.Alert30SentAt = null;
        }
        if (!string.IsNullOrEmpty(dto.Status) && Enum.TryParse<ReferenceStandardStatus>(dto.Status, true, out var st))
            s.Status = st;

        s.UpdatedBy = userId;
        s.UpdatedAt = DateTime.UtcNow;
        return Map(await standards.UpdateAsync(s));
    }

    public async Task DeleteAsync(string id, string userId)
    {
        var s = await standards.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Reference standard {id} not found.");
        s.IsDeleted = true;
        s.UpdatedBy = userId;
        s.UpdatedAt = DateTime.UtcNow;
        await standards.UpdateAsync(s);
    }

    private ReferenceStandardReadDto Map(ReferenceStandard s)
    {
        var dto = mapper.Map<ReferenceStandardReadDto>(s);
        dto.IsExpired = s.NextDueDate.HasValue && s.NextDueDate.Value.Date < DateTime.UtcNow.Date;
        return dto;
    }
}
