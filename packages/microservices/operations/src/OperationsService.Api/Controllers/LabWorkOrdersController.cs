using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OperationsService.Core.DTOs.Calibration;
using OperationsService.Core.DTOs.LabWorkOrders;
using OperationsService.Core.DTOs.ServiceRequests;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Services;
using OperationsService.Core.Services;
using OperationsService.Infrastructure.Data;
using System.Security.Claims;

namespace OperationsService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/assignments/{assignmentId}/lab-work-order")]
[Authorize]
public class LabWorkOrdersController(
    OperationsDbContext db,
    IServiceRequestService serviceRequestService,
    ICrmCustomerDirectory crm,
    IConfiguration config,
    DataSheetCalculationService calculator,
    OperationsService.Api.Services.CertificatePdfService pdfService,
    OperationsService.Api.Services.ITenantBrandingClient brandingClient) : ControllerBase
{
    private string UserId   => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;

    private string? BearerToken => Request.Headers.Authorization
        .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static LabWorkOrderDto Map(LabWorkOrder l) => new()
    {
        Id                   = l.Id,
        AssignmentId         = l.AssignmentId,
        ServiceRequestId     = l.ServiceRequestId,
        Status               = l.Status.ToString(),
        CalibrationSubType   = l.CalibrationSubType,
        IntakeDate           = l.IntakeDate,
        IntakeTechnicianName = l.IntakeTechnicianName,
        IntakeFormJson       = l.IntakeFormJson,
        BenchTechnicianName  = l.BenchTechnicianName,
        BenchStartDate       = l.BenchStartDate,
        BenchCompletedDate   = l.BenchCompletedDate,
        BenchNotes           = l.BenchNotes,
        BenchChecklistJson   = l.BenchChecklistJson,
        BenchSubmittedAt     = l.BenchSubmittedAt,
        TmReviewedByName     = l.TmReviewedByName,
        TmReviewedAt         = l.TmReviewedAt,
        TmApprovalNotes      = l.TmApprovalNotes,
        TmRejectionReason    = l.TmRejectionReason,
        JobNumber            = l.JobNumber,
        CertificateNumber    = l.CertificateNumber,
        CertificateIssuedAt  = l.CertificateIssuedAt,
        CertificateNotes     = l.CertificateNotes,
        DispatchDate         = l.DispatchDate,
        DispatchMethod       = l.DispatchMethod,
        DispatchNotes        = l.DispatchNotes,
        ReceivedBy           = l.ReceivedBy,
        DataSheet            = l.DataSheet is null ? null : MapSheet(l.DataSheet),
        CreatedAt            = l.CreatedAt,
    };

    private static LabDataSheetDto MapSheet(LabDataSheet d) => new()
    {
        Id                   = d.Id,
        SheetType            = d.SheetType,
        Status               = d.Status,
        RawDataJson          = d.RawDataJson,
        CalculatedResultsJson= d.CalculatedResultsJson,
        SubmittedAt          = d.SubmittedAt,
        SubmittedByName      = d.SubmittedByName,
        CreatedAt            = d.CreatedAt,
    };

    // ── GET ───────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<LabWorkOrderDto>> Get(string assignmentId)
    {
        var lwo = await db.LabWorkOrders
            .AsNoTracking()
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();
        return Map(lwo);
    }

    // ── CREATE ────────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<LabWorkOrderDto>> Create(string assignmentId)
    {
        var assignment = await db.Assignments.FindAsync(assignmentId);
        if (assignment is null) return NotFound("Assignment not found");

        var existing = await db.LabWorkOrders.FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (existing is not null) return Conflict("Lab work order already exists for this assignment");

        var lwo = new LabWorkOrder
        {
            Id               = Guid.NewGuid().ToString(),
            AssignmentId     = assignmentId,
            ServiceRequestId = assignment.ServiceRequestId ?? string.Empty,
            Status           = LabWorkOrderStatus.Pending,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
        };
        db.LabWorkOrders.Add(lwo);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { assignmentId }, Map(lwo));
    }

    // ── INTAKE ────────────────────────────────────────────────────────────────

    [HttpPost("intake")]
    public async Task<ActionResult<LabWorkOrderDto>> RecordIntake(string assignmentId, [FromBody] RecordIntakeDto dto)
    {
        var lwo = await db.LabWorkOrders
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();
        if (lwo.Status != LabWorkOrderStatus.Pending)
            return BadRequest($"Cannot record intake from status {lwo.Status}");

        var intakeData = new
        {
            dto.CustomerName,
            dto.CustomerAddress,
            dto.ContactPersonName,
            dto.ContactPersonPhone,
            dto.DeliveryPersonName,
            dto.DeliveryPersonId,
            dto.ConditionOnReceipt,
            dto.JobDescription,
            dto.AccessoriesReceived,
            dto.Location,
            dto.StickerNumber,
            receivedByName = string.IsNullOrWhiteSpace(dto.ReceivedByName) ? UserName : dto.ReceivedByName,
        };

        lwo.IntakeDate           = dto.IntakeDate ?? DateTime.UtcNow;
        lwo.IntakeTechnicianId   = UserId;
        lwo.IntakeTechnicianName = UserName;
        lwo.IntakeFormJson       = JsonSerializer.Serialize(intakeData);
        lwo.IntakeConditionNotes = dto.ConditionOnReceipt;
        lwo.CalibrationSubType   = dto.CalibrationSubType;
        lwo.Status               = LabWorkOrderStatus.IntakeComplete;
        lwo.UpdatedAt            = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Map(lwo);
    }

    // ── BENCH START ───────────────────────────────────────────────────────────

    [HttpPost("bench")]
    public async Task<ActionResult<LabWorkOrderDto>> RecordBench(string assignmentId, [FromBody] RecordBenchDto dto)
    {
        var lwo = await db.LabWorkOrders
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();
        if (lwo.Status != LabWorkOrderStatus.IntakeComplete)
            return BadRequest($"Cannot start bench work from status {lwo.Status}");

        lwo.BenchTechnicianId   = UserId;
        lwo.BenchTechnicianName = dto.TechnicianName ?? UserName;
        lwo.BenchStartDate      = DateTime.UtcNow;
        lwo.BenchNotes          = dto.Notes;
        lwo.Status              = LabWorkOrderStatus.InBench;
        lwo.UpdatedAt           = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Map(lwo);
    }

    // ── DATA SHEET — save/update draft ────────────────────────────────────────

    [HttpGet("data-sheet")]
    public async Task<ActionResult<LabDataSheetDto>> GetDataSheet(string assignmentId)
    {
        var lwo = await db.LabWorkOrders
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();
        if (lwo.DataSheet is null) return NotFound("No data sheet recorded yet");
        return MapSheet(lwo.DataSheet);
    }

    [HttpPost("data-sheet")]
    public async Task<ActionResult<LabDataSheetDto>> SaveDataSheet(string assignmentId, [FromBody] SaveDataSheetDto dto)
    {
        var lwo = await db.LabWorkOrders
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();
        if (lwo.Status is not (LabWorkOrderStatus.InBench or LabWorkOrderStatus.AwaitingTmReview))
            return BadRequest("Data sheet can only be saved when status is InBench or AwaitingTmReview");

        // Determine sheet type from assignment calibration type + sub-type
        var assignment = await db.Assignments.FindAsync(assignmentId);
        var sheetType  = DetermineSheetType(assignment?.NatureOfVisit.ToString(), lwo.CalibrationSubType);

        // Run calculations
        string? calculatedJson = sheetType switch
        {
            "Mass"             => calculator.CalculateMass(dto.RawDataJson),
            "NawiBalance"      => calculator.CalculateNawi(dto.RawDataJson),
            "NawiWeighbridge"  => calculator.CalculateNawi(dto.RawDataJson),
            _                  => null,
        };

        if (lwo.DataSheet is null)
        {
            lwo.DataSheet = new LabDataSheet
            {
                Id            = Guid.NewGuid().ToString(),
                LabWorkOrderId= lwo.Id,
                SheetType     = sheetType,
                CreatedAt     = DateTime.UtcNow,
                UpdatedAt     = DateTime.UtcNow,
            };
            db.LabDataSheets.Add(lwo.DataSheet);
        }

        lwo.DataSheet.RawDataJson           = dto.RawDataJson;
        lwo.DataSheet.CalculatedResultsJson = calculatedJson;
        lwo.DataSheet.UpdatedAt             = DateTime.UtcNow;

        if (dto.Submit && lwo.DataSheet.Status != "Submitted")
        {
            lwo.DataSheet.Status        = "Submitted";
            lwo.DataSheet.SubmittedAt   = DateTime.UtcNow;
            lwo.DataSheet.SubmittedById = UserId;
            lwo.DataSheet.SubmittedByName = UserName;

            if (lwo.Status == LabWorkOrderStatus.InBench)
            {
                lwo.BenchSubmittedAt = DateTime.UtcNow;
                lwo.Status           = LabWorkOrderStatus.AwaitingTmReview;
                lwo.UpdatedAt        = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        return MapSheet(lwo.DataSheet);
    }

    // ── RECALCULATE — re-run calculations on an existing sheet without changing status ──

    [HttpPost("data-sheet/recalculate")]
    public async Task<ActionResult<LabDataSheetDto>> RecalculateDataSheet(string assignmentId)
    {
        var lwo = await db.LabWorkOrders
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();
        if (lwo.DataSheet is null) return NotFound("No data sheet found");
        if (string.IsNullOrWhiteSpace(lwo.DataSheet.RawDataJson)) return BadRequest("Data sheet has no raw data");

        var assignment = await db.Assignments.FindAsync(assignmentId);
        var sheetType  = DetermineSheetType(assignment?.NatureOfVisit.ToString(), lwo.CalibrationSubType);

        lwo.DataSheet.SheetType             = sheetType;
        lwo.DataSheet.CalculatedResultsJson = sheetType switch
        {
            "Mass"             => calculator.CalculateMass(lwo.DataSheet.RawDataJson),
            "NawiBalance"      => calculator.CalculateNawi(lwo.DataSheet.RawDataJson),
            "NawiWeighbridge"  => calculator.CalculateNawi(lwo.DataSheet.RawDataJson),
            _                  => null,
        };
        lwo.DataSheet.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return MapSheet(lwo.DataSheet);
    }

    // ── BENCH COMPLETE (legacy checklist path — kept for compatibility) ────────

    [HttpPost("bench-complete")]
    public async Task<ActionResult<LabWorkOrderDto>> CompleteBench(string assignmentId, [FromBody] CompleteBenchDto dto)
    {
        var lwo = await db.LabWorkOrders
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();
        if (lwo.Status != LabWorkOrderStatus.InBench)
            return BadRequest($"Cannot submit bench for review from status {lwo.Status}");

        lwo.BenchNotes         = dto.Notes ?? lwo.BenchNotes;
        lwo.BenchChecklistJson = dto.ChecklistJson;
        lwo.BenchSubmittedAt   = DateTime.UtcNow;
        lwo.Status             = LabWorkOrderStatus.AwaitingTmReview;
        lwo.UpdatedAt          = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Map(lwo);
    }

    // ── TM REVIEW ─────────────────────────────────────────────────────────────

    [HttpPost("tm-review")]
    public async Task<ActionResult<LabWorkOrderDto>> TmReview(string assignmentId, [FromBody] TmReviewLabDto dto)
    {
        var lwo = await db.LabWorkOrders
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();
        if (lwo.Status != LabWorkOrderStatus.AwaitingTmReview)
            return BadRequest($"Cannot review from status {lwo.Status}");

        lwo.TmReviewedById   = UserId;
        lwo.TmReviewedByName = dto.TmName ?? UserName;
        lwo.TmReviewedAt     = DateTime.UtcNow;
        lwo.UpdatedAt        = DateTime.UtcNow;

        if (dto.Approve)
        {
            // O6 — the certificate number is now system-generated at issue (GenerateCertificate),
            // no longer entered here. TM approval just authorizes moving to certificate generation.
            lwo.JobNumber          = dto.JobNumber?.Trim();
            lwo.CertificateNotes   = dto.Notes;
            lwo.TmApprovalNotes    = dto.Notes;
            lwo.TmRejectionReason  = null;
            lwo.Status             = LabWorkOrderStatus.TmApproved;

            await db.SaveChangesAsync();
        }
        else
        {
            lwo.TmRejectionReason  = dto.RejectionReason;
            lwo.TmApprovalNotes    = null;
            lwo.BenchSubmittedAt   = null;
            // Reset data sheet to Draft so technician can correct and resubmit
            if (lwo.DataSheet is not null)
            {
                lwo.DataSheet.Status      = "Draft";
                lwo.DataSheet.SubmittedAt = null;
                lwo.DataSheet.UpdatedAt   = DateTime.UtcNow;
            }
            lwo.Status = LabWorkOrderStatus.InBench;
            await db.SaveChangesAsync();
        }

        return Map(lwo);
    }

    // ── GENERATE CERTIFICATE ─────────────────────────────────────────────────
    // Called from the certificate generation page after TM approval.
    // Re-runs all calculations so the certificate always reflects the latest math,
    // then stamps CertificateIssuedAt and notifies the ticketing service.

    // O6 — link/replace the traceability reference for this work order (a registered standard is
    // preferred; a free-text certificate number is the fallback for standards not yet registered).
    [HttpPost("reference-standard")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<ActionResult<LabWorkOrderDto>> LinkReferenceStandard(string assignmentId, [FromBody] LinkReferenceStandardDto dto)
    {
        var lwo = await db.LabWorkOrders.FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();

        string detail;
        if (!string.IsNullOrEmpty(dto.ReferenceStandardId))
        {
            var std = await db.ReferenceStandards.FirstOrDefaultAsync(s => s.Id == dto.ReferenceStandardId && !s.IsDeleted);
            if (std is null) return BadRequest("Reference standard not found.");
            lwo.ReferenceStandardId = std.Id;
            lwo.TraceabilityRefFallback = null;
            detail = $"Linked registered standard {std.AssetId}";
        }
        else
        {
            lwo.ReferenceStandardId = null;
            lwo.TraceabilityRefFallback = dto.TraceabilityRefFallback?.Trim();
            detail = $"Recorded free-text traceability ref '{lwo.TraceabilityRefFallback}'";
        }
        lwo.UpdatedAt = DateTime.UtcNow;
        db.CalibrationAuditLogs.Add(new CalibrationAuditLog
        {
            LabWorkOrderId = lwo.Id, Action = "StandardLinked", Detail = detail,
            PerformedById = UserId, PerformedByName = UserName, CreatedBy = UserId, UpdatedBy = UserId,
        });
        await db.SaveChangesAsync();
        return Map(lwo);
    }

    [HttpPost("generate-certificate")]
    [Authorize(Policy = "Permission:calibration.sign")]   // O6 — authorized-signatory gate
    public async Task<ActionResult<LabWorkOrderDto>> GenerateCertificate(string assignmentId)
    {
        var lwo = await db.LabWorkOrders
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();
        if (lwo.Status != LabWorkOrderStatus.TmApproved)
            return BadRequest($"Cannot generate certificate from status {lwo.Status}");

        // O6 — traceability gate: require a linked, non-expired registered standard, or a free-text ref.
        ReferenceStandard? refStd = null;
        if (!string.IsNullOrEmpty(lwo.ReferenceStandardId))
        {
            refStd = await db.ReferenceStandards.FirstOrDefaultAsync(s => s.Id == lwo.ReferenceStandardId && !s.IsDeleted);
            if (refStd is null) return BadRequest("The linked reference standard no longer exists.");
            if (refStd.NextDueDate.HasValue && refStd.NextDueDate.Value.Date < DateTime.UtcNow.Date)
                return BadRequest($"Reference standard '{refStd.AssetId}' is out of calibration (due {refStd.NextDueDate:yyyy-MM-dd}) and cannot back a certificate.");
        }
        else if (string.IsNullOrWhiteSpace(lwo.TraceabilityRefFallback))
        {
            return BadRequest("A traceability reference is required: link a registered reference standard or record a traceability certificate number.");
        }

        // Recompute so the certificate reflects the latest math.
        if (lwo.DataSheet is not null && !string.IsNullOrWhiteSpace(lwo.DataSheet.RawDataJson))
        {
            var assignment = await db.Assignments.FindAsync(assignmentId);
            var sheetType  = DetermineSheetType(assignment?.NatureOfVisit.ToString(), lwo.CalibrationSubType);

            lwo.DataSheet.CalculatedResultsJson = sheetType switch
            {
                "Mass"            => calculator.CalculateMass(lwo.DataSheet.RawDataJson),
                "NawiBalance"     => calculator.CalculateNawi(lwo.DataSheet.RawDataJson),
                "NawiWeighbridge" => calculator.CalculateNawi(lwo.DataSheet.RawDataJson),
                _                 => lwo.DataSheet.CalculatedResultsJson,
            };
            lwo.DataSheet.UpdatedAt = DateTime.UtcNow;
        }

        // O6 — system-generated certificate number (was manually entered at TM review).
        var prefix = $"CAL-{DateTime.UtcNow.Year}-";
        var count  = await db.CalibrationCertificates.CountAsync(c => c.Number.StartsWith(prefix));
        lwo.CertificateNumber   = $"{prefix}{(count + 1):D4}";
        lwo.CertificateIssuedAt = DateTime.UtcNow;
        lwo.BenchCompletedDate  = lwo.BenchCompletedDate ?? DateTime.UtcNow;
        lwo.Status              = LabWorkOrderStatus.CertificateIssued;
        lwo.UpdatedAt           = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // O6 — persist the immutable certificate snapshot + audit trail. The signing user is the
        // authorized signatory (Permission:calibration.sign).
        var certDto      = await AssembleCertificateAsync(lwo);
        var recallMonths = config.GetValue("Calibration:RecallMonths", 12);
        var nextDue      = lwo.CertificateIssuedAt.Value.AddMonths(recallMonths);

        // O6.2 — carry the CRM anchor from the originating service request onto the certificate, so
        // a recall a year from now resolves the client's current address exactly.
        var crmCustomerId = string.IsNullOrWhiteSpace(lwo.ServiceRequestId)
            ? null
            : await db.ServiceRequests.Where(r => r.Id == lwo.ServiceRequestId)
                                      .Select(r => r.CrmCustomerId)
                                      .FirstOrDefaultAsync();

        db.CalibrationCertificates.Add(new CalibrationCertificate
        {
            Number               = lwo.CertificateNumber,
            LabWorkOrderId       = lwo.Id,
            AssignmentId         = lwo.AssignmentId,
            ServiceRequestId     = string.IsNullOrEmpty(lwo.ServiceRequestId) ? null : lwo.ServiceRequestId,
            IssuedAt             = lwo.CertificateIssuedAt.Value,
            IssuedById           = UserId,
            SignatoryId          = UserId,
            SignatoryName        = UserName,
            CertJson             = JsonSerializer.Serialize(certDto, JsonOpts),
            EnvConditionsJson    = null,
            ReferenceStandardIds = refStd?.Id,
            TraceabilityRef      = refStd is null ? lwo.TraceabilityRefFallback : refStd.TraceabilityCertNo,
            NextCalibrationDue   = nextDue,
            // O6.1 — denormalised copies so the certificate register can search and group in SQL.
            // CertJson remains the authoritative snapshot.
            ClientName           = certDto.CustomerName,
            SheetType            = certDto.SheetType,
            CrmCustomerId        = crmCustomerId,
            CreatedBy            = UserId,
            UpdatedBy            = UserId,
        });
        db.CalibrationAuditLogs.Add(new CalibrationAuditLog
        {
            LabWorkOrderId  = lwo.Id,
            Action          = "CertificateIssued",
            Detail          = $"{lwo.CertificateNumber} signed by {UserName}; " +
                              (refStd != null ? $"standard {refStd.AssetId}" : "unmanaged traceability ref"),
            PerformedById   = UserId,
            PerformedByName = UserName,
            CreatedBy       = UserId,
            UpdatedBy       = UserId,
        });
        await db.SaveChangesAsync();

        // O5.4 — record the certificate on the in-service SR (completes it + notifies the helpdesk ticket).
        if (!string.IsNullOrWhiteSpace(lwo.ServiceRequestId))
            await serviceRequestService.RecordCertificateAsync(
                lwo.ServiceRequestId,
                new RecordCertificateDto { CertificateNumber = lwo.CertificateNumber, CertificateIssuedAt = lwo.CertificateIssuedAt });

        // O6 — CRM recall seam (best-effort; no-op until crm-service is wired).
        try
        {
            var clientEmail = string.IsNullOrWhiteSpace(crmCustomerId)
                ? null
                : await crm.GetCustomerEmailByIdAsync(crmCustomerId);
            await crm.ReportCalibrationDueAsync(new CalibrationDueNotice(
                lwo.CertificateNumber, certDto.CustomerName, clientEmail, nextDue));
        }
        catch (Exception) { /* recall notice is non-fatal */ }

        return Map(lwo);
    }

    // ── CERTIFICATE DATA ──────────────────────────────────────────────────────
    // Available once the LWO reaches TmApproved or later.
    // Assembles the unified CalibrationCertificateDto from the stored JSON blobs.

    [HttpGet("certificate-data")]
    public async Task<ActionResult<CalibrationCertificateDto>> GetCertificateData(string assignmentId)
    {
        var lwo = await db.LabWorkOrders
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();

        var allowedStatuses = new[]
        {
            LabWorkOrderStatus.TmApproved,
            LabWorkOrderStatus.CertificateIssued,
            LabWorkOrderStatus.Dispatched,
        };
        if (!allowedStatuses.Contains(lwo.Status))
            return BadRequest($"Certificate data is not available in status {lwo.Status}");

        if (lwo.DataSheet is null || string.IsNullOrWhiteSpace(lwo.DataSheet.CalculatedResultsJson))
            return NotFound("Calculated results not found on data sheet");

        return await AssembleCertificateAsync(lwo);
    }

    // O6 — assembles the unified CalibrationCertificateDto from the LWO + data-sheet JSON. Reused by
    // GetCertificateData (read) and GenerateCertificate (to snapshot the immutable CALIBRATION_CERTIFICATE).
    private async Task<CalibrationCertificateDto> AssembleCertificateAsync(LabWorkOrder lwo)
    {
        var assignment = await db.Assignments.FindAsync(lwo.AssignmentId);
        var sheetType  = DetermineSheetType(assignment?.NatureOfVisit.ToString(), lwo.CalibrationSubType);

        // Parse intake form for customer details
        string? customerName = null, customerAddress = null, contactPerson = null,
                contactPhone = null, jobDescription = null,
                location     = null, stickerNumber  = null;
        if (!string.IsNullOrWhiteSpace(lwo.IntakeFormJson))
        {
            try
            {
                var intake = JsonSerializer.Deserialize<JsonElement>(lwo.IntakeFormJson, JsonOpts);
                customerName    = intake.TryGetProperty("customerName",      out var v1) ? v1.GetString() : null;
                customerAddress = intake.TryGetProperty("customerAddress",   out var v2) ? v2.GetString() : null;
                contactPerson   = intake.TryGetProperty("contactPersonName", out var v3) ? v3.GetString() : null;
                contactPhone    = intake.TryGetProperty("contactPersonPhone",out var v4) ? v4.GetString() : null;
                jobDescription  = intake.TryGetProperty("jobDescription",    out var v5) ? v5.GetString() : null;
                location        = intake.TryGetProperty("location",          out var v6) ? v6.GetString() : null;
                stickerNumber   = intake.TryGetProperty("stickerNumber",     out var v7) ? v7.GetString() : null;
            }
            catch { /* leave nulls */ }
        }

        var cert = new CalibrationCertificateDto
        {
            CertificateNumber   = lwo.CertificateNumber,
            JobNumber           = lwo.JobNumber,
            CertificateIssuedAt = lwo.CertificateIssuedAt,
            Notes               = lwo.CertificateNotes,
            SheetType           = sheetType,
            CustomerName        = customerName,
            CustomerAddress     = customerAddress,
            ContactPersonName   = contactPerson,
            ContactPersonPhone  = contactPhone,
            JobDescription      = jobDescription,
            Location            = location,
            StickerNumber       = stickerNumber,
            TmApprovedBy        = lwo.TmReviewedByName,
            TmApprovedAt        = lwo.TmReviewedAt,
        };

        // GetCertificateData already guards DataSheet-is-null before calling in; GenerateCertificate
        // does not (a TmApproved LWO could in theory reach here without a filled-in data sheet), so
        // guard here too rather than risk an NRE on the read below.
        if (lwo.DataSheet is null)
            throw new InvalidOperationException($"Lab work order {lwo.Id} has no data sheet; cannot assemble certificate.");

        if (sheetType is "Mass")
        {
            var raw  = JsonSerializer.Deserialize<MassRawDataDto>(lwo.DataSheet.RawDataJson ?? "{}", JsonOpts);
            var calc = JsonSerializer.Deserialize<MassCalculatedResultsDto>(lwo.DataSheet.CalculatedResultsJson ?? "{}", JsonOpts);
            cert.CalibrationDoneBy = raw?.CalibrationDoneBy;
            cert.CheckedBy         = raw?.CheckedBy;
            var firstRawBlock = raw?.MeasurementBlocks.FirstOrDefault();
            cert.Mass = new MassCertificateSectionDto
            {
                ComparatorModel     = raw?.ComparatorDetails?.Model,
                ComparatorSerialNo  = raw?.ComparatorDetails?.SerialNo,
                ComparatorDivision  = raw?.ComparatorDetails?.Division,
                ReferenceStdClass         = firstRawBlock?.ReferenceStdClass,
                ReferenceStdSerialNo      = firstRawBlock?.ReferenceStdSerialNo,
                ReferenceStdCertificateNo = firstRawBlock?.ReferenceStdCertificateNo,
                EnvironmentalConditions = raw?.EnvironmentalConditions,
                BlockResults = calc?.BlockResults ?? new(),
                Uncertainty  = calc?.Uncertainty,
            };
        }
        else
        {
            var raw  = JsonSerializer.Deserialize<NawiRawDataDto>(lwo.DataSheet.RawDataJson ?? "{}", JsonOpts);
            var calc = JsonSerializer.Deserialize<NawiCalculatedResultsDto>(lwo.DataSheet.CalculatedResultsJson ?? "{}", JsonOpts);
            cert.CalibrationDoneBy = raw?.CalibrationDoneBy;
            cert.CheckedBy         = raw?.CheckedBy;
            cert.LabNo             = raw?.LabNo;
            cert.Nawi = new NawiCertificateSectionDto
            {
                EquipmentType   = raw?.InstrumentDetails?.EquipmentType,
                RangeType       = raw?.InstrumentDetails?.RangeType,
                Manufacturer    = raw?.InstrumentDetails?.Manufacturer,
                Model           = raw?.InstrumentDetails?.Model,
                SerialNo        = raw?.InstrumentDetails?.SerialNo,
                MaximumCapacity = raw?.InstrumentDetails?.MaximumCapacity,
                MinimumCapacity = raw?.InstrumentDetails?.MinimumCapacity,
                Division        = raw?.InstrumentDetails?.Division,
                AccuracyClass   = raw?.InstrumentDetails?.AccuracyClass,
                TestWeightClass         = raw?.TestWeights?.Class,
                TestWeightSerialNo      = raw?.TestWeights?.SerialNumber,
                TestWeightCertificateNo = raw?.TestWeights?.TraceabilityCertificateNo,
                EnvironmentalConditions = raw?.EnvironmentalConditions,
                Eccentricity   = calc?.Eccentricity,
                Repeatability  = calc?.Repeatability,
                Discrimination = calc?.Discrimination,
                Linearity      = calc?.Linearity ?? new(),
                Uncertainty    = calc?.Uncertainty,
                Tolerance      = calc?.Tolerance,
            };
        }

        return cert;
    }

    // ── CERTIFICATE PDF ───────────────────────────────────────────────────────

    [HttpGet("certificate-pdf")]
    public async Task<IActionResult> GetCertificatePdf(string assignmentId)
    {
        var result = await GetCertificateData(assignmentId);
        if (result.Result is not null) return result.Result;

        var cert  = result.Value!;
        var branding = await brandingClient.GetBrandingAsync();
        byte[] pdf = pdfService.Generate(cert, branding);

        string filename = $"{cert.CertificateNumber ?? assignmentId}.pdf";
        return File(pdf, "application/pdf", filename);
    }

    // ── DISPATCH ──────────────────────────────────────────────────────────────

    [HttpPost("dispatch")]
    public async Task<ActionResult<LabWorkOrderDto>> RecordDispatch(string assignmentId, [FromBody] RecordDispatchDto dto)
    {
        var lwo = await db.LabWorkOrders
            .Include(l => l.DataSheet)
            .FirstOrDefaultAsync(l => l.AssignmentId == assignmentId);
        if (lwo is null) return NotFound();
        if (lwo.Status != LabWorkOrderStatus.CertificateIssued)
            return BadRequest($"Cannot record dispatch from status {lwo.Status}");

        lwo.DispatchDate   = dto.DispatchDate ?? DateTime.UtcNow;
        lwo.DispatchMethod = dto.DispatchMethod;
        lwo.DispatchNotes  = dto.Notes;
        lwo.ReceivedBy     = dto.ReceivedBy;
        lwo.Status         = LabWorkOrderStatus.Dispatched;
        lwo.UpdatedAt      = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Map(lwo);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string DetermineSheetType(string? _, string? subType)
    {
        // CalibrationSubType is the primary signal set at intake
        return subType switch
        {
            "Weighbridge"        => "NawiWeighbridge",
            "BalanceAndPlatform" => "NawiBalance",
            _                    => "Mass",
        };
    }
}
