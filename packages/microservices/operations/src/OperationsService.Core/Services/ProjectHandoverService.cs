using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Handovers;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>O9 — see <see cref="IProjectHandoverService"/>.</summary>
public class ProjectHandoverService(
    IGenericRepository<ProjectHandover> handovers,
    IGenericRepository<ProjectHandoverSignature> signatures,
    IGenericRepository<Project> projects,
    IHseGateway hse,
    IMapper mapper) : IProjectHandoverService
{
    private static readonly HandoverSignatureRole[] MandatoryRoles = Enum.GetValues<HandoverSignatureRole>();

    public async Task<HandoverReadDto?> GetByIdAsync(string id)
    {
        var h = await handovers.Query()
            .Include(x => x.Signatures.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return h is null ? null : Map(h);
    }

    public async Task<IEnumerable<HandoverReadDto>> GetByProjectAsync(string projectId)
    {
        var items = await handovers.Query()
            .Include(x => x.Signatures.Where(s => !s.IsDeleted))
            .Where(x => x.ProjectId == projectId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return items.Select(Map).ToList();
    }

    public async Task<HandoverReadDto> CreateAsync(CreateHandoverDto dto, string userId)
    {
        _ = await projects.GetByIdAsync(dto.ProjectId)
            ?? throw new KeyNotFoundException($"Project {dto.ProjectId} not found.");

        var h = await handovers.CreateAsync(new ProjectHandover
        {
            ProjectId      = dto.ProjectId,
            HandoverNumber = await GenerateNumberAsync(),
            Status         = HandoverStatus.Draft,
            StepsJson      = dto.StepsJson,
            ClientName     = dto.ClientName,
            ClientRepName  = dto.ClientRepName,
            Notes          = dto.Notes,
            CreatedBy      = userId,
            UpdatedBy      = userId,
        });
        return (await GetByIdAsync(h.Id))!;
    }

    public async Task<HandoverReadDto> UpdateAsync(string id, UpdateHandoverDto dto, string userId)
    {
        var h = await LoadEditableAsync(id);
        if (dto.StepsJson != null) h.StepsJson = dto.StepsJson;
        if (dto.ClientName != null) h.ClientName = dto.ClientName;
        if (dto.ClientRepName != null) h.ClientRepName = dto.ClientRepName;
        if (dto.Notes != null) h.Notes = dto.Notes;
        if (h.Status == HandoverStatus.Draft) h.Status = HandoverStatus.InProgress;
        h.UpdatedBy = userId;
        h.UpdatedAt = DateTime.UtcNow;
        await handovers.UpdateAsync(h);
        return (await GetByIdAsync(h.Id))!;
    }

    public async Task<HandoverReadDto> AddSignatureAsync(string id, AddHandoverSignatureDto dto, string userId)
    {
        var h = await LoadEditableAsync(id);
        if (string.IsNullOrWhiteSpace(dto.SignatoryName))
            throw new InvalidOperationException("A signatory name is required.");

        // One signature per role — replace an existing one for the same role.
        var existing = await signatures.Query()
            .FirstOrDefaultAsync(s => s.HandoverId == h.Id && s.Role == dto.Role && !s.IsDeleted);
        if (existing != null)
        {
            existing.SignatoryName = dto.SignatoryName.Trim();
            existing.SignatureData = dto.SignatureData;
            existing.SignedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId;
            existing.UpdatedAt = DateTime.UtcNow;
            await signatures.UpdateAsync(existing);
        }
        else
        {
            await signatures.CreateAsync(new ProjectHandoverSignature
            {
                HandoverId    = h.Id,
                Role          = dto.Role,
                SignatoryName = dto.SignatoryName.Trim(),
                SignatureData = dto.SignatureData,
                SignedAt      = DateTime.UtcNow,
                CreatedBy     = userId,
                UpdatedBy     = userId,
            });
        }

        if (h.Status == HandoverStatus.Draft) { h.Status = HandoverStatus.InProgress; await handovers.UpdateAsync(h); }
        return (await GetByIdAsync(h.Id))!;
    }

    public async Task<HandoverReadDto> CompleteAsync(string id, string userId)
    {
        var h = await handovers.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Handover {id} not found.");
        if (h.Status == HandoverStatus.Completed)
            throw new InvalidOperationException("This handover is already completed.");

        var signed = await signatures.Query()
            .Where(s => s.HandoverId == h.Id && !s.IsDeleted)
            .Select(s => s.Role)
            .ToListAsync();
        var missing = MandatoryRoles.Where(r => !signed.Contains(r)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"All mandatory signatures are required to complete the handover. Missing: {string.Join(", ", missing)}.");

        // O8 — HSE close-out gate (seam to hse-service): a project can't be handed over while a
        // serious/LTI incident is open. No-op / clear until the HSE seam is enabled.
        var hseSummary = await hse.GetProjectHseSummaryAsync(h.ProjectId);
        if (!hseSummary.ClearForCloseOut)
            throw new InvalidOperationException(
                $"Handover blocked: {hseSummary.OpenSeriousIncidents} open serious/LTI HSE incident(s) must be closed first.");

        h.Status      = HandoverStatus.Completed;
        h.CompletedAt = DateTime.UtcNow;
        h.CompletedBy = userId;
        h.IsPermanent = true;   // compliance: a completed handover is permanent and undeletable
        h.UpdatedBy   = userId;
        h.UpdatedAt   = DateTime.UtcNow;
        await handovers.UpdateAsync(h);
        return (await GetByIdAsync(h.Id))!;
    }

    public async Task DeleteAsync(string id, string userId)
    {
        var h = await handovers.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Handover {id} not found.");
        if (h.IsPermanent || h.Status == HandoverStatus.Completed)
            throw new InvalidOperationException("A completed handover is permanent and cannot be deleted.");
        h.IsDeleted = true;
        h.UpdatedBy = userId;
        h.UpdatedAt = DateTime.UtcNow;
        await handovers.UpdateAsync(h);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<ProjectHandover> LoadEditableAsync(string id)
    {
        var h = await handovers.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Handover {id} not found.");
        if (h.Status == HandoverStatus.Completed)
            throw new InvalidOperationException("A completed handover cannot be modified.");
        return h;
    }

    private HandoverReadDto Map(ProjectHandover h)
    {
        var dto = mapper.Map<HandoverReadDto>(h);
        var signed = h.Signatures.Where(s => !s.IsDeleted).Select(s => s.Role).ToHashSet();
        dto.MissingMandatoryRoles = MandatoryRoles.Where(r => !signed.Contains(r)).Select(r => r.ToString()).ToList();
        return dto;
    }

    private async Task<string> GenerateNumberAsync()
    {
        var prefix = $"HO-{DateTime.UtcNow.Year}-";
        var count = await handovers.Query().CountAsync(h => h.HandoverNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }
}
