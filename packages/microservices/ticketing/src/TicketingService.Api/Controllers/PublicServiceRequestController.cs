using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TicketingService.Core.DTOs.Comments;
using TicketingService.Core.DTOs.ServiceRequests;
using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Services;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Api.Controllers;

/// <summary>
/// Public portal — Service &amp; Calibration Request forms (SRF / CRF-NAWI / CRF-MASS).
/// No authentication required. Email OTP verifies the submitter.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/portal/service-request")]
[AllowAnonymous]
[EnableRateLimiting("portal-track")]
public class PublicServiceRequestController(
    TicketingDbContext db,
    ITicketService ticketService,
    IPortalEmailService emailService,
    IOperationsServiceClient operationsClient,
    ILogger<PublicServiceRequestController> logger) : ControllerBase
{
    private const string CatCalibNawi  = "cat-calibration-nawi";
    private const string CatCalibMass  = "cat-calibration-mass";
    private const string CatTechnical  = "cat-technical-service";
    private const string DeptTechnical = "c7f54ea6-01ce-46a2-85f3-649751dcd9f0";

    private const int OtpExpiryMinutes = 10;
    private const int MaxOtpAttempts   = 5;

    // ── POST /initiate ────────────────────────────────────────────────────────

    [EnableRateLimiting("portal-submit")]
    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiateServiceRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ClientName))  return BadRequest(new { message = "Client name is required." });
        if (string.IsNullOrWhiteSpace(dto.ClientEmail)) return BadRequest(new { message = "Email address is required." });
        if (!IsValidEmail(dto.ClientEmail))              return BadRequest(new { message = "Please enter a valid email address." });
        if (!Enum.TryParse<ServiceRequestFormType>(dto.FormType, true, out var formType))
            return BadRequest(new { message = $"Unknown form type '{dto.FormType}'. Use SRF, CRF_NAWI, or CRF_MASS." });
        if (dto.Instruments.Count == 0)
            return BadRequest(new { message = "At least one instrument must be provided." });

        // Expire any previous unverified pending records for the same email+formType
        var stale = await db.PendingVerifications
            .Where(p => p.Email == dto.ClientEmail.Trim().ToLowerInvariant()
                     && p.FormType == dto.FormType
                     && !p.IsVerified
                     && !p.IsDeleted)
            .ToListAsync();
        foreach (var s in stale) s.IsDeleted = true;

        var otp      = GenerateOtp();
        var otpHash  = HashOtp(otp);
        var formJson = JsonSerializer.Serialize(dto);

        var pending = new PendingVerification
        {
            Email        = dto.ClientEmail.Trim().ToLowerInvariant(),
            OtpCode      = otpHash,
            ExpiresAt    = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
            FormType     = dto.FormType.ToUpperInvariant(),
            FormDataJson = formJson,
            CreatedBy    = "portal-anonymous",
        };

        await db.PendingVerifications.AddAsync(pending);
        await db.SaveChangesAsync();

        _ = emailService.SendServiceRequestOtpAsync(
            dto.ClientName.Trim(),
            dto.ClientEmail.Trim(),
            otp,
            FormTypeLabel(formType));

        return Ok(new
        {
            data = new
            {
                pendingId = pending.Id,
                message   = $"A 6-digit verification code has been sent to {MaskEmail(dto.ClientEmail)}. It expires in {OtpExpiryMinutes} minutes.",
            }
        });
    }

    // ── POST /verify ──────────────────────────────────────────────────────────

    [EnableRateLimiting("portal-submit")]
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PendingId)) return BadRequest(new { message = "PendingId is required." });
        if (string.IsNullOrWhiteSpace(dto.OtpCode))   return BadRequest(new { message = "OTP code is required." });

        var pending = await db.PendingVerifications.FindAsync(dto.PendingId);
        if (pending == null || pending.IsDeleted)
            return NotFound(new { message = "Verification session not found. Please restart the form." });
        if (pending.IsVerified)
            return BadRequest(new { message = "This form has already been verified." });
        if (pending.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "Your verification code has expired. Please restart the form to receive a new one." });
        if (pending.AttemptCount >= MaxOtpAttempts)
            return BadRequest(new { message = "Too many incorrect attempts. Please restart the form." });

        pending.AttemptCount++;

        if (!VerifyOtp(dto.OtpCode.Trim(), pending.OtpCode))
        {
            await db.SaveChangesAsync();
            var remaining = MaxOtpAttempts - pending.AttemptCount;
            return BadRequest(new { message = $"Incorrect code. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining." });
        }

        // OTP valid — create the tracking ticket and hand the request to operations (the SR store).
        pending.IsVerified  = true;
        pending.VerifiedAt  = DateTime.UtcNow;

        var formData = JsonSerializer.Deserialize<InitiateServiceRequestDto>(
            pending.FormDataJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        if (!Enum.TryParse<ServiceRequestFormType>(pending.FormType, true, out var formType))
            formType = ServiceRequestFormType.SRF;

        var serviceLocation = Enum.TryParse<ServiceLocationType>(formData.ServiceLocation, true, out var sloc)
            ? sloc : ServiceLocationType.OnSite;

        // Create the linked helpdesk tracking ticket, then persist the pending-verification update.
        var ticket = await CreateLinkedTicketAsync(formData, formType);
        await db.SaveChangesAsync();

        // O5.5 — operations is the sole SR system of record; ticketing no longer stores SRs. Push the
        // verified request; operations owns the SR-#### sequence and returns the generated reference.
        var opsRef = await operationsClient.PushServiceRequestAsync(new IngestServiceRequestPayload(
            ReferenceNumber:    null,
            FormType:           formType.ToString(),
            ServiceLocation:    serviceLocation.ToString(),
            TicketId:           ticket.Id,
            ClientName:         formData.ClientName.Trim(),
            ClientEmail:        formData.ClientEmail.Trim().ToLowerInvariant(),
            ClientPhone:        formData.ClientPhone?.Trim(),
            ClientOrganization: formData.ClientOrganization?.Trim(),
            ClientAddress:      formData.ClientAddress?.Trim(),
            SiteLocation:       formData.SiteLocation?.Trim(),
            Latitude:           formData.Latitude,
            Longitude:          formData.Longitude,
            Description:        formData.Description?.Trim(),
            SpecialInstructions: formData.SpecialInstructions?.Trim(),
            OtpVerifiedAt:      DateTime.UtcNow,
            Instruments: formData.Instruments.Select((i, idx) => new IngestInstrumentPayload(
                RowNumber:           i.RowNumber > 0 ? i.RowNumber : idx + 1,
                Description:         i.Description?.Trim(),
                Manufacturer:        i.Manufacturer?.Trim(),
                Model:               i.Model?.Trim(),
                SerialNumber:        i.SerialNumber?.Trim(),
                TagNumber:           i.TagNumber?.Trim(),
                Range:               i.Range?.Trim(),
                RangeUnit:           i.RangeUnit?.Trim(),
                Condition:           i.Condition?.Trim(),
                Remarks:             i.Remarks?.Trim(),
                LastCalibrationDate: i.LastCalibrationDate,
                CertificateNumber:   i.CertificateNumber?.Trim(),
                NawiInstrumentType:  i.NawiInstrumentType?.Trim(),
                NawiCapacity:        i.NawiCapacity?.Trim(),
                NawiScaleInterval:   i.NawiScaleInterval?.Trim(),
                NawiAccuracyClass:   i.NawiAccuracyClass?.Trim(),
                MassNominalValue:    i.MassNominalValue?.Trim(),
                MassAccuracyClass:   i.MassAccuracyClass?.Trim(),
                ServiceType:         i.ServiceType?.Trim())).ToList()));

        if (string.IsNullOrEmpty(opsRef))
        {
            logger.LogError("SR push to operations failed after OTP verify (ticket {TicketId}) — no SR created", ticket.Id);
            return StatusCode(502, new { message = "We couldn't complete your submission. Please try again shortly." });
        }

        // Record the SR reference on the ticket for support correlation.
        _ = ticketService.AddCommentAsync(ticket.Id,
            new CreateCommentDto { TicketId = ticket.Id, Content = $"Linked service request: {opsRef}", IsInternal = true },
            "portal-anonymous");

        var ticketRef = ticket.Reference;
        _ = emailService.SendServiceRequestConfirmationAsync(
            formData.ClientName.Trim(),
            formData.ClientEmail.Trim(),
            opsRef,
            FormTypeLabel(formType),
            formData.Instruments.Count,
            ticketRef);

        return Ok(new
        {
            data = new
            {
                referenceNumber = opsRef,
                ticketReference = ticketRef,
                ticketId        = ticket.Id,
                message         = "Your request has been submitted. A confirmation has been sent to your email. We will review your request and send a quotation shortly.",
            }
        });
    }

    // ── POST /resend-otp ──────────────────────────────────────────────────────

    [EnableRateLimiting("portal-submit")]
    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PendingId)) return BadRequest(new { message = "PendingId is required." });

        var pending = await db.PendingVerifications.FindAsync(dto.PendingId);
        if (pending == null || pending.IsDeleted)
            return NotFound(new { message = "Verification session not found. Please restart the form." });
        if (pending.IsVerified)
            return BadRequest(new { message = "This form has already been verified." });
        if (pending.AttemptCount >= MaxOtpAttempts)
            return BadRequest(new { message = "Too many attempts. Please restart the form." });

        var otp = GenerateOtp();
        pending.OtpCode   = HashOtp(otp);
        pending.ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);
        pending.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var formData = JsonSerializer.Deserialize<InitiateServiceRequestDto>(
            pending.FormDataJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        if (!Enum.TryParse<ServiceRequestFormType>(pending.FormType, true, out var formType))
            formType = ServiceRequestFormType.SRF;

        _ = emailService.SendServiceRequestOtpAsync(
            formData.ClientName.Trim(),
            formData.ClientEmail.Trim(),
            otp,
            FormTypeLabel(formType));

        return Ok(new
        {
            data = new
            {
                message = $"A new code has been sent to {MaskEmail(formData.ClientEmail)}. It expires in {OtpExpiryMinutes} minutes.",
            }
        });
    }

    // ── POST /signature ───────────────────────────────────────────────────────

    [EnableRateLimiting("portal-submit")]
    [HttpPost("signature")]
    public async Task<IActionResult> SubmitSignature([FromBody] SubmitSignatureDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ReferenceNumber)) return BadRequest(new { message = "Reference number is required." });
        if (string.IsNullOrWhiteSpace(dto.SignatureData))   return BadRequest(new { message = "Signature data is required." });

        // O5.4 — signature is stored on the operations SR; proxy and relay.
        var proxy = await operationsClient.SubmitServiceRequestSignatureAsync(dto.ReferenceNumber, dto.SignatureData);
        return new ContentResult { StatusCode = proxy.StatusCode, ContentType = "application/json", Content = proxy.Body };
    }

    // ── GET /track/{reference} ────────────────────────────────────────────────

    // O5.4 — the SR system of record is now operations; proxy tracking there and relay the response.
    [HttpGet("track/{reference}")]
    public async Task<IActionResult> Track(string reference)
    {
        var proxy = await operationsClient.GetServiceRequestTrackingAsync(reference);
        return new ContentResult { StatusCode = proxy.StatusCode, ContentType = "application/json", Content = proxy.Body };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Core.DTOs.Tickets.TicketReadDto> CreateLinkedTicketAsync(
        InitiateServiceRequestDto formData, ServiceRequestFormType formType)
    {
        var catId = formType switch
        {
            ServiceRequestFormType.CRF_NAWI => CatCalibNawi,
            ServiceRequestFormType.CRF_MASS => CatCalibMass,
            _                               => CatTechnical,
        };

        var instrSummary = string.Join("\n", formData.Instruments.Select((i, n) =>
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(i.Description))  parts.Add(i.Description);
            if (!string.IsNullOrEmpty(i.Manufacturer)) parts.Add(i.Manufacturer);
            if (!string.IsNullOrEmpty(i.Model))        parts.Add(i.Model);
            if (!string.IsNullOrEmpty(i.SerialNumber)) parts.Add($"S/N: {i.SerialNumber}");
            return $"  {n + 1}. {(parts.Count > 0 ? string.Join(" | ", parts) : "—")}";
        }));

        var formLabel = FormTypeLabel(formType);
        var org       = string.IsNullOrEmpty(formData.ClientOrganization) ? "" : $" ({formData.ClientOrganization})";
        var count     = formData.Instruments.Count;

        // The SR reference (generated by operations) is added to the ticket as an internal comment
        // once the SR is created — it isn't known at ticket-creation time.
        var description = $"""
            === {formLabel.ToUpperInvariant()} — PORTAL SUBMISSION ===
            Client    : {formData.ClientName}{org}
            Email     : {formData.ClientEmail}
            Phone     : {(string.IsNullOrEmpty(formData.ClientPhone) ? "—" : formData.ClientPhone)}
            Site      : {(string.IsNullOrEmpty(formData.SiteLocation) ? "—" : formData.SiteLocation)}
            ========================================================

            Instruments ({count}):
            {instrSummary}

            {(string.IsNullOrEmpty(formData.Description) ? "" : $"Description:\n{formData.Description}\n")}
            {(string.IsNullOrEmpty(formData.SpecialInstructions) ? "" : $"Special Instructions:\n{formData.SpecialInstructions}")}
            """;

        var createDto = new CreateTicketDto
        {
            Title        = $"[{formLabel}] {formData.ClientName}{org} — {count} instrument{(count == 1 ? "" : "s")}",
            Description  = description.Trim(),
            CategoryId   = catId,
            Priority     = TicketPriority.Medium,
            Source       = TicketSource.CRM,
            DepartmentId = DeptTechnical,
        };

        return await ticketService.CreateAsync(createDto, "portal-anonymous");
    }

    private static string GenerateOtp()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var value = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1_000_000;
        return value.ToString("D6");
    }

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool VerifyOtp(string input, string storedHash) =>
        HashOtp(input) == storedHash;

    private static string FormTypeLabel(ServiceRequestFormType t) => t switch
    {
        ServiceRequestFormType.CRF_NAWI => "Calibration Request (NAWI)",
        ServiceRequestFormType.CRF_MASS => "Calibration Request (Mass)",
        _                               => "Service Request Form",
    };

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "****";
        var name   = parts[0];
        var domain = parts[1];
        var masked = name.Length <= 2 ? "**" : name[..2] + new string('*', name.Length - 2);
        return $"{masked}@{domain}";
    }

    private static bool IsValidEmail(string email)
    {
        try { _ = new System.Net.Mail.MailAddress(email); return true; }
        catch { return false; }
    }
}
