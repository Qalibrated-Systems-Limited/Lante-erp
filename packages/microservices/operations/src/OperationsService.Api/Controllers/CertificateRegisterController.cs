using System.Security.Claims;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Calibration;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.LabWorkOrders;
using OperationsService.Infrastructure.Data;

namespace OperationsService.Api.Controllers;

/// <summary>
/// O6.1 — the register of issued calibration certificates: expiry tracking and the counts an
/// ISO 17025 audit asks for. Read-only over CalibrationCertificate, which is an immutable
/// snapshot written at issue — nothing here may alter an issued certificate. The one write is
/// withdrawal, which records that a certificate is no longer in force without deleting it.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/calibration-certificates")]
[Authorize]
public class CertificateRegisterController(
    OperationsDbContext db,
    IConfiguration config,
    OperationsService.Api.Services.CertificatePdfService pdfService,
    OperationsService.Api.Services.ITenantBrandingClient brandingClient,
    ILogger<CertificateRegisterController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private string UserId   => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? "system";

    /// <summary>Days before the recall date at which a certificate counts as "expiring".</summary>
    private int ExpiringWindowDays => config.GetValue("Calibration:ExpiringWindowDays", 60);

    // ── LIST ─────────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = "Permission:calibration.certificates.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<CertificateRegisterItemDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        [FromQuery] string? validity = null,
        [FromQuery] string? sheetType = null,
        [FromQuery] DateTime? issuedFrom = null,
        [FromQuery] DateTime? issuedTo = null)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = BuildQuery(q, sheetType, issuedFrom, issuedTo);

        // Validity is derived, not stored, so it cannot be filtered in SQL. Translate the requested
        // bucket into the equivalent date predicate rather than pulling the whole table into memory.
        var today  = DateTime.UtcNow.Date;
        var window = today.AddDays(ExpiringWindowDays);
        query = (validity?.ToLowerInvariant()) switch
        {
            "valid"     => query.Where(c => c.WithdrawnAt == null && c.NextCalibrationDue != null && c.NextCalibrationDue > window),
            "expiring"  => query.Where(c => c.WithdrawnAt == null && c.NextCalibrationDue != null
                                         && c.NextCalibrationDue >= today && c.NextCalibrationDue <= window),
            "expired"   => query.Where(c => c.WithdrawnAt == null && c.NextCalibrationDue != null && c.NextCalibrationDue < today),
            "withdrawn" => query.Where(c => c.WithdrawnAt != null),
            "unknown"   => query.Where(c => c.WithdrawnAt == null && c.NextCalibrationDue == null),
            _           => query,
        };

        var total = await query.CountAsync();
        var rows  = await query
            .OrderByDescending(c => c.IssuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new ApiResponse<PaginatedResult<CertificateRegisterItemDto>>
        {
            Success = true,
            Data = new PaginatedResult<CertificateRegisterItemDto>
            {
                Items      = rows.Select(ToItem).ToList(),
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize,
            },
        });
    }

    // ── AUDIT SUMMARY ────────────────────────────────────────────────────────

    /// <summary>
    /// Counts over the same filter as the list, for auditing. Computed across every matching row,
    /// not just the current page — a count that only reflected one page would be worse than none.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = "Permission:calibration.certificates.read")]
    public async Task<ActionResult<ApiResponse<CertificateRegisterSummaryDto>>> GetSummary(
        [FromQuery] string? q = null,
        [FromQuery] string? sheetType = null,
        [FromQuery] DateTime? issuedFrom = null,
        [FromQuery] DateTime? issuedTo = null)
    {
        var rows = await BuildQuery(q, sheetType, issuedFrom, issuedTo)
            .Select(c => new
            {
                c.IssuedAt, c.NextCalibrationDue, c.WithdrawnAt, c.SheetType, c.SignatoryName,
            })
            .ToListAsync();

        var today   = DateTime.UtcNow.Date;
        var summary = new CertificateRegisterSummaryDto { Total = rows.Count };

        foreach (var r in rows)
        {
            switch (Classify(r.NextCalibrationDue, r.WithdrawnAt, today, ExpiringWindowDays))
            {
                case CertificateValidity.Valid:     summary.Valid++;     break;
                case CertificateValidity.Expiring:  summary.Expiring++;  break;
                case CertificateValidity.Expired:   summary.Expired++;   break;
                case CertificateValidity.Withdrawn: summary.Withdrawn++; break;
                default:                            summary.Unknown++;   break;
            }
        }

        summary.IssuedByMonth = rows
            .GroupBy(r => r.IssuedAt.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        summary.IssuedBySheetType = rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.SheetType) ? "Unspecified" : r.SheetType!)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        summary.IssuedBySignatory = rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.SignatoryName) ? "Unspecified" : r.SignatoryName)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(new ApiResponse<CertificateRegisterSummaryDto> { Success = true, Data = summary });
    }

    // ── PDF ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders the certificate PDF from the register.
    ///
    /// Deliberately rendered from the row's own CertJson snapshot rather than re-derived from the
    /// lab work order: the snapshot is what was signed. Re-reading the live LWO would let a later
    /// edit to the data sheet silently change an already-issued certificate, which is exactly what
    /// the immutable snapshot exists to prevent. It also means certificates whose assignment has
    /// since been removed still render.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = "Permission:calibration.certificates.read")]
    public async Task<IActionResult> GetPdf(Guid id)
    {
        var cert = await db.CalibrationCertificates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id.ToString());
        if (cert is null) return NotFound(new { message = "Certificate not found." });

        var snapshot = ReadSnapshot(cert.CertJson);
        if (snapshot is null)
            return Problem($"Certificate {cert.Number} has no readable snapshot to render.", statusCode: 422);

        var branding = await brandingClient.GetBrandingAsync();
        var pdf = pdfService.Generate(snapshot, branding);
        // Inline so the register can preview it in a viewer; the browser still offers "save as".
        Response.Headers.ContentDisposition = $"inline; filename=\"{cert.Number}.pdf\"";
        return File(pdf, "application/pdf");
    }

    // ── WITHDRAW ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Withdraws a certificate (superseded or issued in error). The row is kept — an audit trail
    /// must not lose records — but it stops generating recall reminders and leaves the in-force
    /// counts. Issuing a replacement is a separate action on the lab work order.
    /// </summary>
    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Policy = "Permission:calibration.sign")]
    public async Task<ActionResult<ApiResponse<CertificateRegisterItemDto>>> Withdraw(
        Guid id, [FromBody] WithdrawCertificateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new ApiResponse<CertificateRegisterItemDto>
            { Success = false, Message = "A reason is required to withdraw a certificate." });

        var cert = await db.CalibrationCertificates.FirstOrDefaultAsync(c => c.Id == id.ToString());
        if (cert is null)
            return NotFound(new ApiResponse<CertificateRegisterItemDto>
            { Success = false, Message = "Certificate not found." });

        if (cert.WithdrawnAt != null)
            return BadRequest(new ApiResponse<CertificateRegisterItemDto>
            { Success = false, Message = $"Certificate {cert.Number} was already withdrawn on {cert.WithdrawnAt:yyyy-MM-dd}." });

        cert.WithdrawnAt     = DateTime.UtcNow;
        cert.WithdrawnReason = dto.Reason.Trim();
        cert.UpdatedAt       = DateTime.UtcNow;
        cert.UpdatedBy       = UserId;

        db.CalibrationAuditLogs.Add(new Core.Entities.CalibrationAuditLog
        {
            LabWorkOrderId  = cert.LabWorkOrderId,
            Action          = "CertificateWithdrawn",
            Detail          = $"{cert.Number} withdrawn by {UserName}: {cert.WithdrawnReason}",
            PerformedById   = UserId,
            PerformedByName = UserName,
            CreatedBy       = UserId,
            UpdatedBy       = UserId,
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Certificate {Number} withdrawn by {User}: {Reason}", cert.Number, UserName, cert.WithdrawnReason);

        return Ok(new ApiResponse<CertificateRegisterItemDto> { Success = true, Data = ToItem(cert) });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private IQueryable<Core.Entities.CalibrationCertificate> BuildQuery(
        string? q, string? sheetType, DateTime? issuedFrom, DateTime? issuedTo)
    {
        var query = db.CalibrationCertificates.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Number, $"%{term}%") ||
                (c.ClientName != null    && EF.Functions.ILike(c.ClientName, $"%{term}%")) ||
                EF.Functions.ILike(c.SignatoryName, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(sheetType))
            query = query.Where(c => c.SheetType == sheetType);

        if (issuedFrom.HasValue) query = query.Where(c => c.IssuedAt >= issuedFrom.Value.Date);
        if (issuedTo.HasValue)   query = query.Where(c => c.IssuedAt <  issuedTo.Value.Date.AddDays(1));

        return query;
    }

    private CertificateRegisterItemDto ToItem(Core.Entities.CalibrationCertificate c)
    {
        var today = DateTime.UtcNow.Date;
        var snap  = ReadSnapshot(c.CertJson);

        return new CertificateRegisterItemDto
        {
            Id     = c.Id,
            Number = c.Number,

            ClientName = c.ClientName ?? snap?.CustomerName,
            SheetType  = c.SheetType  ?? snap?.SheetType,
            Equipment  = snap?.Nawi?.EquipmentType ?? (snap?.Mass != null ? "Mass (weight)" : null),
            SerialNo   = snap?.Nawi?.SerialNo ?? snap?.Mass?.BlockResults.FirstOrDefault()?.SerialNo,

            IssuedAt           = c.IssuedAt,
            NextCalibrationDue = c.NextCalibrationDue,
            SignatoryName      = c.SignatoryName,
            CrmCustomerId      = c.CrmCustomerId,

            LabWorkOrderId   = c.LabWorkOrderId,
            AssignmentId     = c.AssignmentId,
            ServiceRequestId = c.ServiceRequestId,
            TraceabilityRef  = c.TraceabilityRef,

            Validity  = Classify(c.NextCalibrationDue, c.WithdrawnAt, today, ExpiringWindowDays),
            DaysToDue = c.NextCalibrationDue.HasValue
                ? (int)(c.NextCalibrationDue.Value.Date - today).TotalDays
                : null,

            Recall60SentAt  = c.Recall60SentAt,
            Recall30SentAt  = c.Recall30SentAt,
            Recall7SentAt   = c.Recall7SentAt,
            WithdrawnAt     = c.WithdrawnAt,
            WithdrawnReason = c.WithdrawnReason,
        };
    }

    // A snapshot that fails to parse must not take the whole register down with it — the row is
    // still a real issued certificate and its stored columns are enough to list and track it.
    private CalibrationCertificateDto? ReadSnapshot(string? certJson)
    {
        if (string.IsNullOrWhiteSpace(certJson)) return null;
        try { return JsonSerializer.Deserialize<CalibrationCertificateDto>(certJson, JsonOpts); }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Unparseable CertJson on a certificate row — listing it from stored columns only.");
            return null;
        }
    }

    internal static CertificateValidity Classify(DateTime? nextDue, DateTime? withdrawnAt, DateTime today, int windowDays)
    {
        if (withdrawnAt != null) return CertificateValidity.Withdrawn;
        if (nextDue is null)     return CertificateValidity.Unknown;

        var days = (nextDue.Value.Date - today).TotalDays;
        if (days < 0)          return CertificateValidity.Expired;
        if (days <= windowDays) return CertificateValidity.Expiring;
        return CertificateValidity.Valid;
    }
}
