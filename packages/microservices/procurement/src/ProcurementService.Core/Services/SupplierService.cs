using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.DTOs.Suppliers;
using ProcurementService.Core.Entities;
using ProcurementService.Core.Enums;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Core.Services;

/// <summary>P1 — Approved Supplier Register. Suppliers move Pending → (conflict check) → Approved, and may
/// be Blacklisted (MD, reason mandatory). A supplier is usable in a PO only when IsApproved &amp;&amp; !BlacklistFlag.
/// Every transition is written to the procurement audit log (PROC-005).</summary>
public class SupplierService(
    IGenericRepository<Supplier> suppliers,
    IGenericRepository<SupplierCategory> categories,
    IGenericRepository<SupplierDocument> documents,
    IGenericRepository<GiftRegister> gifts,
    IGenericRepository<ProcurementAuditLog> audit,
    IMapper mapper) : ISupplierService
{
    // ── Supplier master ──
    public async Task<SupplierListResult> GetAllAsync(SupplierFilterParams filter)
    {
        var q = suppliers.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            q = q.Where(x => x.Name.ToLower().Contains(s)
                || (x.KraPin != null && x.KraPin.ToLower().Contains(s))
                || x.SupplierNumber.ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<SupplierStatus>(filter.Status, true, out var st))
            q = q.Where(x => x.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.CategoryId))
            q = q.Where(x => x.CategoryId == filter.CategoryId);
        if (filter.ApprovedOnly == true)
            q = q.Where(x => x.IsApproved && !x.BlacklistFlag);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();

        var dtos = mapper.Map<List<SupplierSummaryDto>>(items);
        var catIds = items.Where(i => i.CategoryId != null).Select(i => i.CategoryId!).Distinct().ToList();
        if (catIds.Count > 0)
        {
            var catMap = await categories.Query().AsNoTracking().Where(c => catIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.CategoryName);
            for (var i = 0; i < items.Count; i++)
                if (items[i].CategoryId is { } cid && catMap.TryGetValue(cid, out var name))
                    dtos[i].CategoryName = name;
        }
        return new SupplierListResult(dtos, total);
    }

    public async Task<SupplierReadDto?> GetByIdAsync(string id)
    {
        var s = await suppliers.Query().AsNoTracking().Include(x => x.Documents).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return null;
        var dto = mapper.Map<SupplierReadDto>(s);
        dto.CategoryName = await CategoryNameAsync(s.CategoryId);
        return dto;
    }

    public async Task<SupplierReadDto> CreateAsync(CreateSupplierDto dto, string userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidOperationException("Supplier name is required.");
        var s = mapper.Map<Supplier>(dto);
        s.SupplierNumber = await GenerateSupplierNumberAsync();
        s.Status = SupplierStatus.Pending;
        s.Source = "native";
        s.CreatedBy = userId;
        s.UpdatedBy = userId;
        var created = await suppliers.CreateAsync(s);
        await LogAsync("Supplier", created.Id, AsrAuditAction.SupplierCreated, $"Supplier {created.SupplierNumber} created.", userId, userName);
        return (await GetByIdAsync(created.Id))!;
    }

    public async Task<SupplierReadDto?> UpdateAsync(string id, UpdateSupplierDto dto, string userId)
    {
        var s = await suppliers.GetByIdAsync(id);
        if (s is null) return null;
        if (dto.Name != null) s.Name = dto.Name;
        if (dto.KraPin != null) s.KraPin = dto.KraPin;
        if (dto.CategoryId != null) s.CategoryId = dto.CategoryId;
        if (dto.ContactPerson != null) s.ContactPerson = dto.ContactPerson;
        if (dto.Phone != null) s.Phone = dto.Phone;
        if (dto.Email != null) s.Email = dto.Email;
        if (dto.Address != null) s.Address = dto.Address;
        Touch(s, userId);
        await suppliers.UpdateAsync(s);
        return await GetByIdAsync(id);
    }

    // ── Documents ──
    public async Task<List<SupplierDocumentDto>> GetDocumentsAsync(string supplierId)
    {
        var docs = await documents.Query().AsNoTracking().Where(d => d.SupplierId == supplierId)
            .OrderByDescending(d => d.UploadedAt).ToListAsync();
        return mapper.Map<List<SupplierDocumentDto>>(docs);
    }

    public async Task<SupplierDocumentDto?> UploadDocumentAsync(string supplierId, UploadSupplierDocumentDto dto, string userId)
    {
        var s = await suppliers.GetByIdAsync(supplierId);
        if (s is null) return null;
        var doc = await documents.CreateAsync(new SupplierDocument
        {
            SupplierId = supplierId,
            DocumentType = Enum.TryParse<SupplierDocumentType>(dto.DocumentType, true, out var t) ? t : SupplierDocumentType.Other,
            DocumentName = dto.DocumentName,
            FileUrl = dto.FileUrl,
            ExpiryDate = dto.ExpiryDate,
            UploadedAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        });
        await LogAsync("Supplier", supplierId, AsrAuditAction.DocumentUploaded, $"Document {doc.DocumentType} uploaded.", userId, null);
        return mapper.Map<SupplierDocumentDto>(doc);
    }

    public async Task<SupplierActionResult> VerifyDocumentAsync(string documentId, string userId)
    {
        var doc = await documents.GetByIdAsync(documentId);
        if (doc is null) return new SupplierActionResult("Error", "Document not found.");
        doc.VerifiedBy = userId;
        doc.VerifiedAt = DateTime.UtcNow;
        Touch(doc, userId);
        await documents.UpdateAsync(doc);
        return new SupplierActionResult("Ok", "Document verified.");
    }

    // ── Workflow ──
    public async Task<SupplierActionResult> RunConflictCheckAsync(string supplierId, ConflictCheckDto dto, string userId)
    {
        var s = await suppliers.GetByIdAsync(supplierId);
        if (s is null) return new SupplierActionResult("Error", "Supplier not found.");
        s.ConflictChecked = true;
        s.ConflictFound = dto.ConflictFound;
        s.ConflictNotes = dto.Notes;
        s.ConflictCheckedBy = userId;
        s.ConflictCheckedAt = DateTime.UtcNow;
        if (dto.ConflictFound)
            s.Status = SupplierStatus.ConflictFlagged;   // suspended pending MD review
        Touch(s, userId);
        await suppliers.UpdateAsync(s);
        await LogAsync("Supplier", supplierId,
            dto.ConflictFound ? AsrAuditAction.ConflictFlagged : AsrAuditAction.ConflictCleared,
            dto.ConflictFound ? $"Conflict flagged: {dto.Notes}" : "Conflict check cleared.", userId, null);
        return new SupplierActionResult(s.Status.ToString(),
            dto.ConflictFound ? "Conflict flagged — supplier suspended pending MD review." : "No conflict — ready for approval.");
    }

    public async Task<SupplierActionResult> ApproveAsync(string supplierId, string userId)
    {
        var s = await suppliers.GetByIdAsync(supplierId);
        if (s is null) return new SupplierActionResult("Error", "Supplier not found.");
        if (s.BlacklistFlag) return new SupplierActionResult("Error", "A blacklisted supplier cannot be approved.");
        if (!s.ConflictChecked) return new SupplierActionResult("Error", "Run the conflict-of-interest check before approval.");
        if (s.ConflictFound) return new SupplierActionResult("Error", "Resolve the flagged conflict before approval.");
        s.IsApproved = true;
        s.Status = SupplierStatus.Approved;
        s.ApprovedBy = userId;
        s.ApprovedAt = DateTime.UtcNow;
        Touch(s, userId);
        await suppliers.UpdateAsync(s);
        await LogAsync("Supplier", supplierId, AsrAuditAction.Approved, $"Supplier {s.SupplierNumber} approved.", userId, null);
        return new SupplierActionResult("Approved", "Supplier approved and added to the register.");
    }

    public async Task<SupplierActionResult> BlacklistAsync(string supplierId, BlacklistSupplierDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) return new SupplierActionResult("Error", "A blacklist reason is mandatory.");
        var s = await suppliers.GetByIdAsync(supplierId);
        if (s is null) return new SupplierActionResult("Error", "Supplier not found.");
        s.BlacklistFlag = true;
        s.IsApproved = false;
        s.Status = SupplierStatus.Blacklisted;
        s.BlacklistReason = dto.Reason;
        s.BlacklistedBy = userId;
        s.BlacklistedAt = DateTime.UtcNow;
        Touch(s, userId);
        await suppliers.UpdateAsync(s);
        await LogAsync("Supplier", supplierId, AsrAuditAction.Blacklisted, $"Blacklisted: {dto.Reason}", userId, null);
        return new SupplierActionResult("Blacklisted", "Supplier blacklisted.");
    }

    public async Task<SupplierActionResult> ReinstateAsync(string supplierId, string userId)
    {
        var s = await suppliers.GetByIdAsync(supplierId);
        if (s is null) return new SupplierActionResult("Error", "Supplier not found.");
        if (!s.BlacklistFlag) return new SupplierActionResult("Error", "Supplier is not blacklisted.");
        s.BlacklistFlag = false;
        s.BlacklistReason = null;
        // Return to Approved if it had been approved before; otherwise back to Pending.
        var wasApproved = s.ApprovedAt is not null;
        s.IsApproved = wasApproved;
        s.Status = wasApproved ? SupplierStatus.Approved : SupplierStatus.Pending;
        Touch(s, userId);
        await suppliers.UpdateAsync(s);
        await LogAsync("Supplier", supplierId, AsrAuditAction.Reinstated, "Blacklist lifted.", userId, null);
        return new SupplierActionResult(s.Status.ToString(), "Supplier reinstated.");
    }

    // ── Categories ──
    public async Task<List<SupplierCategoryDto>> GetCategoriesAsync()
    {
        var cats = await categories.Query().AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync();
        return mapper.Map<List<SupplierCategoryDto>>(cats);
    }

    public async Task<SupplierCategoryDto> SaveCategoryAsync(SaveSupplierCategoryDto dto, string userId, string? id = null)
    {
        SupplierCategory cat;
        if (!string.IsNullOrWhiteSpace(id))
        {
            cat = await categories.GetByIdAsync(id) ?? throw new KeyNotFoundException("Category not found.");
        }
        else
        {
            cat = new SupplierCategory { CreatedBy = userId };
        }
        cat.CategoryName = dto.CategoryName.Trim();
        cat.MinScoreThreshold = dto.MinScoreThreshold ?? cat.MinScoreThreshold;
        cat.Description = dto.Description;
        Touch(cat, userId);
        cat = string.IsNullOrWhiteSpace(id) ? await categories.CreateAsync(cat) : await categories.UpdateAsync(cat);
        return mapper.Map<SupplierCategoryDto>(cat);
    }

    // ── Gifts ──
    public async Task<List<GiftDto>> GetGiftsAsync(string? supplierId)
    {
        var q = gifts.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(supplierId)) q = q.Where(g => g.SupplierId == supplierId);
        return mapper.Map<List<GiftDto>>(await q.OrderByDescending(g => g.DeclaredAt).ToListAsync());
    }

    public async Task<GiftDto> DeclareGiftAsync(DeclareGiftDto dto, string userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(dto.SupplierId)) throw new InvalidOperationException("SupplierId is required.");
        var supplier = await suppliers.GetByIdAsync(dto.SupplierId);
        var gift = await gifts.CreateAsync(new GiftRegister
        {
            SupplierId = dto.SupplierId,
            SupplierName = supplier?.Name,
            ReceivedBy = string.IsNullOrWhiteSpace(dto.ReceivedBy) ? userId : dto.ReceivedBy!,
            ReceivedByName = dto.ReceivedByName ?? userName,
            GiftDescription = dto.GiftDescription.Trim(),
            EstimatedValue = dto.EstimatedValue,
            DeclaredAt = DateTime.UtcNow,
            LinkedPoId = dto.LinkedPoId,
            CreatedBy = userId,
            UpdatedBy = userId,
        });
        await LogAsync("Gift", gift.Id, AsrAuditAction.GiftDeclared,
            $"Gift declared from {supplier?.Name ?? dto.SupplierId} (est. {dto.EstimatedValue:N0}).", userId, userName);
        return mapper.Map<GiftDto>(gift);
    }

    // ── Dashboard ──
    public async Task<AsrSummaryDto> GetSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var soon = now.AddDays(30);
        var all = await suppliers.Query().AsNoTracking().ToListAsync();
        var expiring = await documents.Query().AsNoTracking()
            .CountAsync(d => d.ExpiryDate != null && d.ExpiryDate <= soon && d.ExpiryDate >= now);
        var monthGifts = await gifts.Query().AsNoTracking().Where(g => g.DeclaredAt >= monthStart).ToListAsync();

        return new AsrSummaryDto
        {
            TotalSuppliers = all.Count,
            Approved = all.Count(x => x.IsApproved && !x.BlacklistFlag),
            Pending = all.Count(x => x.Status == SupplierStatus.Pending),
            ConflictFlagged = all.Count(x => x.Status == SupplierStatus.ConflictFlagged),
            Blacklisted = all.Count(x => x.BlacklistFlag),
            ExpiringDocuments = expiring,
            GiftsThisMonth = monthGifts.Count,
            GiftValueThisMonth = monthGifts.Sum(g => g.EstimatedValue),
        };
    }

    // ── Helpers ──
    private async Task<string?> CategoryNameAsync(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId)) return null;
        var c = await categories.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == categoryId);
        return c?.CategoryName;
    }

    private async Task<string> GenerateSupplierNumberAsync()
    {
        var prefix = $"SUP-{DateTime.UtcNow.Year}-";
        var count = await suppliers.Query().CountAsync(s => s.SupplierNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task LogAsync(string entityType, string entityId, AsrAuditAction action, string detail, string userId, string? userName)
    {
        await audit.CreateAsync(new ProcurementAuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Detail = detail,
            PerformedBy = userId,
            PerformedByName = userName,
            OccurredAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        });
    }

    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
