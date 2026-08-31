using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OperationsService.Core.DTOs.Assignments;
using OperationsService.Core.DTOs.ServiceRequests;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>O5 — see <see cref="IServiceRequestService"/>. Ported from the ticketing
/// ServiceRequestsController; create-assignment is now an in-process call to AssignmentService.</summary>
public class ServiceRequestService(
    IGenericRepository<ServiceRequest> srs,
    IGenericRepository<ServiceRequestInstrument> instruments,
    IGenericRepository<Quotation> quotations,
    IAssignmentService assignmentService,
    IQuotationEmailSender emailSender,
    ITicketingServiceClient ticketing,
    ICrmCustomerDirectory crm,
    ILogger<ServiceRequestService> logger) : IServiceRequestService
{
    // Fallback department when the dispatch DTO omits one (Technical). Matches the prior ticketing default.
    private const string DefaultDepartmentId = "c7f54ea6-01ce-46a2-85f3-649751dcd9f0";

    private static readonly JsonSerializerOptions JsonRead = new() { PropertyNameCaseInsensitive = true };

    // A request can be cancelled up until field work begins. Once InProgress a live Assignment
    // exists (and Completed/Rejected/Cancelled are terminal), so those states block cancellation.
    private static readonly ServiceRequestStatus[] CancellableStatuses =
    {
        ServiceRequestStatus.Submitted,
        ServiceRequestStatus.UnderReview,
        ServiceRequestStatus.QuotationDraft,
        ServiceRequestStatus.QuotationSent,
        ServiceRequestStatus.QuotationApproved,
        ServiceRequestStatus.QuotationRejected,
    };

    // ── Ingest (service-to-service intake from the ticketing portal) ────────────

    public async Task<IngestResult> IngestAsync(IngestServiceRequestDto dto)
        => await PersistNewAsync(dto, "portal-intake");

    // Staff-side create (walk-in / phone-in client who won't use the portal). Same shape as an
    // ingest, but attributed to the logged-in user and never OTP-gated — lands at Submitted.
    public async Task<IngestResult> CreateAsync(IngestServiceRequestDto dto, string userId)
        => await PersistNewAsync(dto, string.IsNullOrWhiteSpace(userId) ? "staff" : userId);

    private async Task<IngestResult> PersistNewAsync(IngestServiceRequestDto dto, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(dto.ClientName))
            throw new InvalidOperationException("Client name is required.");
        if (string.IsNullOrWhiteSpace(dto.ClientEmail))
            throw new InvalidOperationException("Client email is required.");

        // Captured right after the guards above: the intervening `await` and retry loop below make
        // the compiler lose track of the non-null narrowing on the (settable) dto properties.
        var clientName  = dto.ClientName.Trim();
        var clientEmail = dto.ClientEmail.Trim().ToLowerInvariant();

        var formType = Enum.TryParse<ServiceRequestFormType>(dto.FormType, true, out var ft)
            ? ft : ServiceRequestFormType.SRF;
        var serviceLocation = Enum.TryParse<ServiceLocationType>(dto.ServiceLocation, true, out var sl)
            ? sl : ServiceLocationType.OnSite;

        // O6.2 — anchor the CRM customer once, here, where we still have the client's email. Every
        // downstream client lookup (notably certificate recalls) then keys off an exact id instead
        // of matching on client name. Best-effort: CRM may be disabled or the client may be new, and
        // an unanchored request must still be accepted.
        string? crmCustomerId = null;
        try
        {
            crmCustomerId = await crm.ResolveCustomerIdAsync(dto.ClientEmail?.Trim(), dto.ClientName?.Trim());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM customer resolve failed for {Email} — request will be created unanchored.",
                dto.ClientEmail);
        }

        // Prefer the reference ticketing already generated so the two systems stay aligned; if it
        // omitted one (or a duplicate arrives), fall back to / regenerate a local SR number.
        // ReferenceNumber is now a unique-indexed column (see OperationsDbContext), so a collision
        // here — either from two concurrent creates both minting the same generated number, or two
        // concurrent ingests both quoting the same ticketing-supplied reference — throws instead of
        // silently duplicating; retry with a freshly generated number rather than failing the request.
        var reference = dto.ReferenceNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(reference) ||
            await srs.Query().AnyAsync(r => r.ReferenceNumber == reference))
            reference = await GenerateReferenceNumberAsync();

        ServiceRequest sr = null!;
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            sr = new ServiceRequest
            {
                ReferenceNumber     = reference,
                FormType            = formType,
                Status              = ServiceRequestStatus.Submitted,
                TicketId            = string.IsNullOrWhiteSpace(dto.TicketId) ? null : dto.TicketId,
                ClientName          = clientName,
                ClientEmail         = clientEmail,
                CrmCustomerId       = crmCustomerId,
                ClientPhone         = dto.ClientPhone?.Trim(),
                ClientOrganization  = dto.ClientOrganization?.Trim(),
                ClientAddress       = dto.ClientAddress?.Trim(),
                SiteLocation        = dto.SiteLocation?.Trim(),
                Latitude            = dto.Latitude,
                Longitude           = dto.Longitude,
                Description         = dto.Description?.Trim(),
                SpecialInstructions = dto.SpecialInstructions?.Trim(),
                ServiceLocation     = serviceLocation,
                OtpVerifiedAt       = dto.OtpVerifiedAt,
                CreatedBy           = createdBy,
                UpdatedBy           = createdBy,
            };
            try
            {
                await srs.CreateAsync(sr);
                break;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                reference = await GenerateReferenceNumberAsync();
            }
        }

        var idx = 0;
        foreach (var i in dto.Instruments)
        {
            idx++;
            await instruments.CreateAsync(new ServiceRequestInstrument
            {
                ServiceRequestId    = sr.Id,
                RowNumber           = i.RowNumber > 0 ? i.RowNumber : idx,
                Description         = i.Description?.Trim(),
                Manufacturer        = i.Manufacturer?.Trim(),
                Model               = i.Model?.Trim(),
                SerialNumber        = i.SerialNumber?.Trim(),
                TagNumber           = i.TagNumber?.Trim(),
                Range               = i.Range?.Trim(),
                RangeUnit           = i.RangeUnit?.Trim(),
                Condition           = i.Condition?.Trim(),
                Remarks             = i.Remarks?.Trim(),
                LastCalibrationDate = i.LastCalibrationDate,
                CertificateNumber   = i.CertificateNumber?.Trim(),
                NawiInstrumentType  = i.NawiInstrumentType?.Trim(),
                NawiCapacity        = i.NawiCapacity?.Trim(),
                NawiScaleInterval   = i.NawiScaleInterval?.Trim(),
                NawiAccuracyClass   = i.NawiAccuracyClass?.Trim(),
                MassNominalValue    = i.MassNominalValue?.Trim(),
                MassAccuracyClass   = i.MassAccuracyClass?.Trim(),
                ServiceType         = i.ServiceType?.Trim(),
                CreatedBy           = createdBy,
                UpdatedBy           = createdBy,
            });
        }

        logger.LogInformation("Created SR {Ref} ({Count} instrument(s)) by {By}", sr.ReferenceNumber, idx, createdBy);
        return new IngestResult(sr.Id, sr.ReferenceNumber, sr.Status.ToString());
    }

    // ── Public tracking (proxied by the ticketing portal) ───────────────────────

    public async Task<ServiceRequestSummaryDto?> TrackAsync(string reference)
    {
        var refUpper = reference.Trim().ToUpperInvariant();
        var sr = await srs.Query()
            .Include(r => r.Instruments)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReferenceNumber == refUpper);
        if (sr == null) return null;

        return new ServiceRequestSummaryDto
        {
            ReferenceNumber    = sr.ReferenceNumber,
            FormType           = sr.FormType.ToString(),
            Status             = sr.Status.ToString(),
            ServiceLocation    = sr.ServiceLocation.ToString(),
            ClientName         = sr.ClientName,
            ClientEmail        = sr.ClientEmail,
            ClientOrganization = sr.ClientOrganization,
            SiteLocation       = sr.SiteLocation,
            InstrumentCount    = sr.Instruments.Count,
            CreatedAt          = sr.CreatedAt,
            TicketId           = sr.TicketId,
            HasQuotation       = sr.QuotationId != null,
        };
    }

    public async Task<SrSignatureResult> SubmitSignatureAsync(string reference, string signatureData)
    {
        if (string.IsNullOrWhiteSpace(signatureData))
            throw new InvalidOperationException("Signature data is required.");

        var refUpper = reference.Trim().ToUpperInvariant();
        var sr = await srs.Query().FirstOrDefaultAsync(r => r.ReferenceNumber == refUpper)
            ?? throw new KeyNotFoundException("Service request not found.");

        if (sr.ClientSignatureData != null)
            throw new InvalidOperationException("A signature has already been recorded for this request.");

        sr.ClientSignatureData = signatureData;
        sr.ClientSignedAt      = DateTime.UtcNow;
        sr.UpdatedAt           = DateTime.UtcNow;
        await srs.UpdateAsync(sr);

        return new SrSignatureResult("Signature recorded.");
    }

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<ServiceRequestListResult> GetAllAsync(ServiceRequestFilterParams filter)
    {
        var query = srs.Query()
            .Include(r => r.Instruments)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<ServiceRequestStatus>(filter.Status, true, out var statusEnum))
            query = query.Where(r => r.Status == statusEnum);

        if (!string.IsNullOrWhiteSpace(filter.FormType) &&
            Enum.TryParse<ServiceRequestFormType>(filter.FormType, true, out var ftEnum))
            query = query.Where(r => r.FormType == ftEnum);

        if (filter.From.HasValue) query = query.Where(r => r.CreatedAt >= filter.From.Value);
        if (filter.To.HasValue)   query = query.Where(r => r.CreatedAt <= filter.To.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(r => new ServiceRequestSummaryDto
            {
                ReferenceNumber    = r.ReferenceNumber,
                FormType           = r.FormType.ToString(),
                Status             = r.Status.ToString(),
                ServiceLocation    = r.ServiceLocation.ToString(),
                ClientName         = r.ClientName,
                ClientEmail        = r.ClientEmail,
                ClientOrganization = r.ClientOrganization,
                SiteLocation       = r.SiteLocation,
                InstrumentCount    = r.Instruments.Count,
                CreatedAt          = r.CreatedAt,
                TicketId           = r.TicketId,
                HasQuotation       = r.QuotationId != null,
            })
            .ToListAsync();

        return new ServiceRequestListResult(items, total);
    }

    // ── Detail ────────────────────────────────────────────────────────────────

    public async Task<ServiceRequestDetailDto?> GetByIdAsync(string id)
    {
        var sr = await srs.Query()
            .Include(r => r.Instruments)
            .Include(r => r.Quotation)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id || r.ReferenceNumber == id);

        return sr == null ? null : MapDetail(sr);
    }

    // ── Review (approve / reject) ───────────────────────────────────────────────

    public async Task<SrReviewResult> ReviewAsync(string id, TmReviewDto dto, string userId)
    {
        var sr = await FindTrackedAsync(id);

        if (sr.Status != ServiceRequestStatus.Submitted && sr.Status != ServiceRequestStatus.UnderReview)
            throw new InvalidOperationException($"Cannot review a request with status '{sr.Status}'.");

        if (dto.Approve)
        {
            sr.Status              = ServiceRequestStatus.UnderReview;
            sr.TmComments          = dto.TmComments?.Trim();
            sr.ReviewedByTmId      = userId;
            sr.TmReviewedAt        = DateTime.UtcNow;
            sr.ReviewChecklistJson = dto.ReviewChecklistJson;
            sr.PlannedServiceDate  = dto.PlannedServiceDate;
            sr.AuthorizingName     = dto.AuthorizingName?.Trim();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.RejectionReason))
                throw new InvalidOperationException("A rejection reason is required.");

            sr.Status              = ServiceRequestStatus.Rejected;
            sr.RejectionReason     = dto.RejectionReason.Trim();
            sr.TmComments          = dto.TmComments?.Trim();
            sr.ReviewedByTmId      = userId;
            sr.TmReviewedAt        = DateTime.UtcNow;
            sr.ReviewChecklistJson = dto.ReviewChecklistJson;
        }

        sr.UpdatedBy = userId;
        sr.UpdatedAt = DateTime.UtcNow;
        await srs.UpdateAsync(sr);

        if (!dto.Approve)
            await SyncStatusAsync(sr, $"Service request {sr.ReferenceNumber} was rejected by the technical manager.");

        return new SrReviewResult(
            sr.Status.ToString(),
            dto.Approve ? "Request marked as Under Review." : "Request rejected.");
    }

    // ── Create quotation draft ──────────────────────────────────────────────────

    public async Task<QuotationDetailDto> CreateQuotationAsync(string id, CreateQuotationDto dto, string userId)
    {
        var sr = await srs.Query()
            .Include(r => r.Quotation)
            .FirstOrDefaultAsync(r => r.Id == id || r.ReferenceNumber == id)
            ?? throw new KeyNotFoundException("Service request not found.");

        if (sr.Quotation != null)
            throw new InvalidOperationException("A quotation already exists. Use PUT to update it.");
        if (sr.Status != ServiceRequestStatus.UnderReview)
            throw new InvalidOperationException(
                $"A quotation can only be drafted while the request is Under Review. Current status: {sr.Status}.");
        if (dto.LineItems.Count == 0)
            throw new InvalidOperationException("At least one line item is required.");

        foreach (var li in dto.LineItems)
            li.Amount = Math.Round(li.Quantity * li.UnitPrice, 2);

        var subtotal = dto.LineItems.Sum(li => li.Amount);
        var vatRate  = dto.VatRate <= 0 ? 0.16m : dto.VatRate;
        var vatAmt   = Math.Round(subtotal * vatRate, 2);
        var total    = subtotal + vatAmt;

        // QuotationNumber is unique-indexed (see OperationsDbContext); retry with a freshly
        // generated number if a concurrent create already took the one GenerateQuotationNumberAsync
        // just counted.
        Quotation quotation = null!;
        const int maxQuoteAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            quotation = new Quotation
            {
                ServiceRequestId = sr.Id,
                QuotationNumber  = await GenerateQuotationNumberAsync(),
                Status           = QuotationStatus.Draft,
                ValidUntil       = dto.ValidUntil,
                LineItemsJson    = JsonSerializer.Serialize(dto.LineItems),
                Subtotal         = subtotal,
                VatRate          = vatRate,
                VatAmount        = vatAmt,
                TotalAmount      = total,
                Notes            = dto.Notes?.Trim(),
                CreatedBy        = userId,
                UpdatedBy        = userId,
            };
            try
            {
                await quotations.CreateAsync(quotation);
                break;
            }
            catch (DbUpdateException) when (attempt < maxQuoteAttempts)
            {
                // retry with the next attempt's freshly generated number
            }
        }

        sr.QuotationId = quotation.Id;
        sr.Status      = ServiceRequestStatus.QuotationDraft;
        sr.UpdatedBy   = userId;
        sr.UpdatedAt   = DateTime.UtcNow;
        await srs.UpdateAsync(sr);

        return MapQuotation(quotation);
    }

    // ── Update quotation draft ──────────────────────────────────────────────────

    public async Task<QuotationDetailDto> UpdateQuotationAsync(string id, UpdateQuotationDto dto, string userId)
    {
        var sr = await srs.Query()
            .Include(r => r.Quotation)
            .FirstOrDefaultAsync(r => r.Id == id || r.ReferenceNumber == id)
            ?? throw new KeyNotFoundException("Service request not found.");

        var q = sr.Quotation
            ?? throw new KeyNotFoundException("No quotation exists. Use POST to create one.");

        if (sr.Status != ServiceRequestStatus.QuotationDraft)
            throw new InvalidOperationException(
                $"The quotation can only be edited while it is a draft. Current status: {sr.Status}.");
        if (q.Status != QuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot edit a quotation with status '{q.Status}'. Resend to update.");

        if (dto.ValidUntil.HasValue) q.ValidUntil = dto.ValidUntil;
        if (dto.Notes != null)       q.Notes = dto.Notes.Trim();

        if (dto.LineItems != null && dto.LineItems.Count > 0)
        {
            foreach (var li in dto.LineItems)
                li.Amount = Math.Round(li.Quantity * li.UnitPrice, 2);

            q.LineItemsJson = JsonSerializer.Serialize(dto.LineItems);
            q.Subtotal      = dto.LineItems.Sum(li => li.Amount);
            if (dto.VatRate.HasValue) q.VatRate = dto.VatRate.Value;
            q.VatAmount   = Math.Round(q.Subtotal * q.VatRate, 2);
            q.TotalAmount = q.Subtotal + q.VatAmount;
        }

        q.UpdatedBy  = userId;
        q.UpdatedAt  = DateTime.UtcNow;
        await quotations.UpdateAsync(q);

        return MapQuotation(q);
    }

    // ── Send quotation to client ────────────────────────────────────────────────

    public async Task<SrSendResult> SendQuotationAsync(string id)
    {
        var sr = await srs.Query()
            .Include(r => r.Quotation)
            .FirstOrDefaultAsync(r => r.Id == id || r.ReferenceNumber == id)
            ?? throw new KeyNotFoundException("Service request not found.");

        var q = sr.Quotation
            ?? throw new KeyNotFoundException("No quotation exists. Create one first.");

        if (sr.Status != ServiceRequestStatus.QuotationDraft && sr.Status != ServiceRequestStatus.QuotationSent)
            throw new InvalidOperationException(
                $"A quotation can only be sent from draft (or re-sent once sent). Current status: {sr.Status}.");
        if (q.Status == QuotationStatus.Accepted)
            throw new InvalidOperationException("This quotation has already been accepted.");

        var lineItems = JsonSerializer.Deserialize<List<QuotationLineItemDto>>(q.LineItemsJson, JsonRead)
                        ?? new List<QuotationLineItemDto>();

        await emailSender.SendQuotationAsync(new QuotationEmailModel
        {
            ToName             = sr.ClientName,
            ToEmail            = sr.ClientEmail,
            ReferenceNumber    = sr.ReferenceNumber,
            QuotationNumber    = q.QuotationNumber,
            FormTypeLabel      = FormTypeLabel(sr.FormType),
            LineItems          = lineItems,
            Subtotal           = q.Subtotal,
            VatRate            = q.VatRate,
            VatAmount          = q.VatAmount,
            TotalAmount        = q.TotalAmount,
            ValidUntil         = q.ValidUntil,
            Notes              = q.Notes,
            ClientOrganization = sr.ClientOrganization,
            ClientAddress      = sr.ClientAddress,
            Description        = sr.Description,
        });

        q.Status    = QuotationStatus.Sent;
        q.SentAt     = DateTime.UtcNow;
        q.UpdatedAt  = DateTime.UtcNow;
        await quotations.UpdateAsync(q);

        sr.Status    = ServiceRequestStatus.QuotationSent;
        sr.UpdatedAt = DateTime.UtcNow;
        await srs.UpdateAsync(sr);

        logger.LogInformation("Quotation {Quot} sent for SR {Ref}", q.QuotationNumber, sr.ReferenceNumber);
        await SyncStatusAsync(sr, $"Quotation {q.QuotationNumber} (total {q.TotalAmount:0.00}) was sent to the client for {sr.ReferenceNumber}.");

        return new SrSendResult($"Quotation {q.QuotationNumber} sent to {sr.ClientEmail}.", q.SentAt);
    }

    // ── Record LPO / acceptance ─────────────────────────────────────────────────

    public async Task<SrLpoResult> RecordLpoAsync(string id, RecordLpoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.LpoNumber))
            throw new InvalidOperationException("LPO number is required.");

        var sr = await srs.Query()
            .Include(r => r.Quotation)
            .FirstOrDefaultAsync(r => r.Id == id || r.ReferenceNumber == id)
            ?? throw new KeyNotFoundException("Service request not found.");

        var q = sr.Quotation ?? throw new KeyNotFoundException("No quotation exists.");

        if (sr.Status != ServiceRequestStatus.QuotationSent)
            throw new InvalidOperationException(
                $"An LPO can only be recorded after a quotation has been sent. Current status: {sr.Status}.");

        q.LpoNumber     = dto.LpoNumber.Trim();
        q.LpoReceivedAt = DateTime.UtcNow;
        q.AcceptedAt    = DateTime.UtcNow;
        q.Status        = QuotationStatus.Accepted;
        q.UpdatedAt     = DateTime.UtcNow;
        await quotations.UpdateAsync(q);

        sr.Status    = ServiceRequestStatus.QuotationApproved;
        sr.UpdatedAt = DateTime.UtcNow;
        await srs.UpdateAsync(sr);

        await SyncStatusAsync(sr, $"Client LPO {q.LpoNumber} received for {sr.ReferenceNumber} — quotation approved, ready for dispatch.");

        return new SrLpoResult(
            $"LPO {dto.LpoNumber} recorded. Request moved to QuotationApproved.",
            q.Status.ToString());
    }

    // ── Revise (reopen a sent/rejected/expired quotation) ───────────────────────

    public async Task<SrReviseResult> ReviseQuotationAsync(string id)
    {
        var sr = await srs.Query()
            .Include(r => r.Quotation)
            .FirstOrDefaultAsync(r => r.Id == id || r.ReferenceNumber == id)
            ?? throw new KeyNotFoundException("Service request not found.");

        var q = sr.Quotation ?? throw new KeyNotFoundException("No quotation exists to revise.");

        if (sr.Status != ServiceRequestStatus.QuotationSent && sr.Status != ServiceRequestStatus.QuotationRejected)
            throw new InvalidOperationException(
                $"Only a sent or client-declined quotation can be revised. Current status: {sr.Status}.");
        if (q.Status == QuotationStatus.Accepted)
            throw new InvalidOperationException("This quotation was accepted (LPO recorded) and can't be revised.");
        if (q.Status == QuotationStatus.Draft)
            throw new InvalidOperationException("This quotation is already a draft.");

        q.Status     = QuotationStatus.Draft;
        q.SentAt     = null;
        q.RejectedAt = null;
        q.AcceptedAt = null;
        q.UpdatedAt  = DateTime.UtcNow;
        await quotations.UpdateAsync(q);

        sr.Status    = ServiceRequestStatus.QuotationDraft;
        sr.UpdatedAt = DateTime.UtcNow;
        await srs.UpdateAsync(sr);

        return new SrReviseResult("Quotation reopened as a draft — edit and resend.", q.Status.ToString());
    }

    // ── Client declines a sent quotation ────────────────────────────────────────

    public async Task<SrQuotationRejectResult> RejectQuotationAsync(string id, RejectQuotationDto dto, string userId)
    {
        var sr = await srs.Query()
            .Include(r => r.Quotation)
            .FirstOrDefaultAsync(r => r.Id == id || r.ReferenceNumber == id)
            ?? throw new KeyNotFoundException("Service request not found.");

        if (sr.Status != ServiceRequestStatus.QuotationSent)
            throw new InvalidOperationException(
                $"Only a sent quotation can be declined by the client. Current status: {sr.Status}.");

        var q = sr.Quotation ?? throw new KeyNotFoundException("No quotation exists.");

        q.Status     = QuotationStatus.Rejected;
        q.RejectedAt = DateTime.UtcNow;
        q.UpdatedBy  = userId;
        q.UpdatedAt  = DateTime.UtcNow;
        await quotations.UpdateAsync(q);

        sr.Status          = ServiceRequestStatus.QuotationRejected;
        sr.RejectionReason = dto.Reason?.Trim();
        sr.UpdatedBy       = userId;
        sr.UpdatedAt       = DateTime.UtcNow;
        await srs.UpdateAsync(sr);

        var reasonNote = string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" Reason: {dto.Reason.Trim()}.";
        await SyncStatusAsync(sr, $"Client declined quotation {q.QuotationNumber} for {sr.ReferenceNumber}.{reasonNote} It can be revised and resent.");

        return new SrQuotationRejectResult(sr.Status.ToString(), "Quotation marked as declined by the client.");
    }

    // ── Record certificate ──────────────────────────────────────────────────────

    public async Task<SrCertificateResult> RecordCertificateAsync(string id, RecordCertificateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CertificateNumber))
            throw new InvalidOperationException("Certificate number is required.");

        var sr = await FindTrackedAsync(id);

        sr.CertificateNumber   = dto.CertificateNumber.Trim();
        sr.CertificateIssuedAt = dto.CertificateIssuedAt ?? DateTime.UtcNow;
        // The certificate is the calibration deliverable — issuing it completes the request.
        if (sr.Status is ServiceRequestStatus.InProgress or ServiceRequestStatus.QuotationApproved)
            sr.Status = ServiceRequestStatus.Completed;
        sr.UpdatedAt = DateTime.UtcNow;
        await srs.UpdateAsync(sr);

        await SyncStatusAsync(sr, $"Certificate {sr.CertificateNumber} issued for {sr.ReferenceNumber}.");

        return new SrCertificateResult(sr.CertificateNumber, sr.Status.ToString(), "Certificate recorded.");
    }

    // ── Dispatch — create the operations Assignment in-process ──────────────────

    public async Task<SrAssignmentResult> CreateAssignmentAsync(string id, CreateSrAssignmentDto dto, string userId)
    {
        var sr = await srs.Query()
            .Include(r => r.Instruments)
            .FirstOrDefaultAsync(r => r.Id == id || r.ReferenceNumber == id)
            ?? throw new KeyNotFoundException("Service request not found.");

        if (sr.Status != ServiceRequestStatus.QuotationApproved)
            throw new InvalidOperationException(
                $"Assignment can only be created when status is QuotationApproved. Current status: {sr.Status}.");

        if (!string.IsNullOrEmpty(sr.OperationsAssignmentId))
            throw new InvalidOperationException(
                $"An assignment already exists for this service request ({sr.OperationsAssignmentId}).");

        // Atomically claim the SR (flip to InProgress) before creating the Assignment below — the
        // checks above are check-then-act: two concurrent dispatch calls on the same SR can both
        // pass them before either writes back, each creating its own Assignment (and technician
        // dispatch) for one service request. This conditional UPDATE is the real guard.
        var claimed = await srs.Query()
            .Where(r => r.Id == sr.Id && r.Status == ServiceRequestStatus.QuotationApproved && r.OperationsAssignmentId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ServiceRequestStatus.InProgress)
                .SetProperty(r => r.UpdatedBy, userId)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));
        if (claimed == 0)
            throw new InvalidOperationException(
                $"An assignment already exists for this service request, or it is no longer QuotationApproved.");

        var formLabel = FormTypeLabel(sr.FormType);
        var org       = string.IsNullOrEmpty(sr.ClientOrganization) ? "" : $" ({sr.ClientOrganization})";

        var srSnapshot = JsonSerializer.Serialize(new
        {
            referenceNumber     = sr.ReferenceNumber,
            formType            = sr.FormType.ToString(),
            serviceLocation     = sr.ServiceLocation.ToString(),
            clientName          = sr.ClientName,
            clientEmail         = sr.ClientEmail,
            clientPhone         = sr.ClientPhone,
            clientOrg           = sr.ClientOrganization,
            siteLocation        = sr.SiteLocation,
            latitude            = sr.Latitude,
            longitude           = sr.Longitude,
            description         = sr.Description,
            specialInstructions = sr.SpecialInstructions,
            instrumentCount     = sr.Instruments.Count,
            instruments         = sr.Instruments.Select(i => new
            {
                rowNumber          = i.RowNumber,
                manufacturer       = i.Manufacturer,
                model              = i.Model,
                serialNumber       = i.SerialNumber,
                tagNumber          = i.TagNumber,
                serviceType        = i.ServiceType,
                nawiInstrumentType = i.NawiInstrumentType,
                nawiCapacity       = i.NawiCapacity,
                massNominalValue   = i.MassNominalValue,
                massAccuracyClass  = i.MassAccuracyClass,
            }),
        });

        var natureOfVisit = Enum.TryParse<NatureOfVisit>(dto.NatureOfVisit, true, out var nov)
            ? nov
            : NatureOfVisit.CorrectiveMaintenance;

        var createDto = new CreateAssignmentDto
        {
            Title                  = $"[{formLabel}] {sr.ClientName}{org} — {sr.ReferenceNumber}",
            Description            = sr.Description ?? $"{formLabel} for {sr.ClientName}{org}. {sr.Instruments.Count} instrument(s).",
            SourceType             = AssignmentSourceType.Ticket,
            LinkedTicketId         = string.IsNullOrEmpty(sr.TicketId) ? null : sr.TicketId,
            ServiceRequestId       = sr.Id,
            ServiceRequestDataJson = srSnapshot,
            DepartmentId           = string.IsNullOrEmpty(dto.DepartmentId) ? DefaultDepartmentId : dto.DepartmentId,
            Priority               = AssignmentPriority.Normal,
            NatureOfVisit          = natureOfVisit,
            ServiceType            = formLabel,
            Deadline               = dto.Deadline,
            LocationName           = sr.ClientOrganization ?? sr.ClientName,
            LocationAddress        = sr.SiteLocation,
            LocationLatitude       = sr.Latitude,
            LocationLongitude      = sr.Longitude,
            Notes                  = dto.Notes,
            TechnicianIds          = dto.TechnicianIds,
            TechnicianNames        = dto.TechnicianNames,
        };

        AssignmentReadDto assignment;
        try
        {
            assignment = await assignmentService.CreateAsync(createDto, userId);
        }
        catch
        {
            // Release the claim so a failed dispatch doesn't leave the SR permanently stuck
            // InProgress with no assignment ever linked.
            await srs.Query().Where(r => r.Id == sr.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ServiceRequestStatus.QuotationApproved));
            throw;
        }

        await srs.Query().Where(r => r.Id == sr.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.OperationsAssignmentId, assignment.Id)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));

        sr.OperationsAssignmentId = assignment.Id;
        sr.Status                 = ServiceRequestStatus.InProgress;
        sr.UpdatedBy              = userId;
        sr.UpdatedAt              = DateTime.UtcNow;

        logger.LogInformation("Assignment {AssignmentId} created for SR {Ref}", assignment.Id, sr.ReferenceNumber);
        await SyncStatusAsync(sr, $"{sr.ReferenceNumber} dispatched to field operations (assignment {assignment.Id}).");

        return new SrAssignmentResult(assignment.Id, "Assignment created and SR status updated to In Progress.");
    }

    // ── Cancel a service request (before field work begins) ─────────────────────

    public async Task<SrCancelResult> CancelAsync(string id, CancelServiceRequestDto dto, string userId)
    {
        var sr = await FindTrackedAsync(id);

        if (!CancellableStatuses.Contains(sr.Status))
            throw new InvalidOperationException(
                $"A request in status '{sr.Status}' can't be cancelled — cancellation is only allowed before field work begins.");

        sr.Status          = ServiceRequestStatus.Cancelled;
        sr.RejectionReason = dto.Reason?.Trim();
        sr.UpdatedBy       = userId;
        sr.UpdatedAt       = DateTime.UtcNow;
        await srs.UpdateAsync(sr);

        var reasonNote = string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" Reason: {dto.Reason.Trim()}.";
        await SyncStatusAsync(sr, $"Service request {sr.ReferenceNumber} was cancelled.{reasonNote}");

        return new SrCancelResult(sr.Status.ToString(), "Service request cancelled.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    // Reverse status sync — best-effort note to the linked helpdesk ticket (client swallows errors).
    private async Task SyncStatusAsync(ServiceRequest sr, string note)
    {
        if (string.IsNullOrEmpty(sr.TicketId)) return;
        await ticketing.NotifyServiceRequestStatusAsync(sr.TicketId, sr.ReferenceNumber, sr.Status.ToString(), note, null);
    }

    private async Task<ServiceRequest> FindTrackedAsync(string id) =>
        await srs.Query().FirstOrDefaultAsync(r => r.Id == id || r.ReferenceNumber == id)
        ?? throw new KeyNotFoundException("Service request not found.");

    private async Task<string> GenerateReferenceNumberAsync()
    {
        var year   = DateTime.UtcNow.Year;
        var prefix = $"SR-{year}-";
        var count  = await srs.Query().CountAsync(r => r.ReferenceNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task<string> GenerateQuotationNumberAsync()
    {
        var year   = DateTime.UtcNow.Year;
        var prefix = $"QT-{year}-";
        var count  = await quotations.Query().CountAsync(q => q.QuotationNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private static string FormTypeLabel(ServiceRequestFormType t) => t switch
    {
        ServiceRequestFormType.CRF_NAWI => "Calibration Request (NAWI)",
        ServiceRequestFormType.CRF_MASS => "Calibration Request (Mass)",
        _                               => "Service Request Form",
    };

    private static ServiceRequestDetailDto MapDetail(ServiceRequest sr) => new()
    {
        Id                     = sr.Id,
        ReferenceNumber        = sr.ReferenceNumber,
        FormType               = sr.FormType.ToString(),
        Status                 = sr.Status.ToString(),
        ServiceLocation        = sr.ServiceLocation.ToString(),
        ClientName             = sr.ClientName,
        ClientEmail            = sr.ClientEmail,
        ClientPhone            = sr.ClientPhone,
        ClientOrganization     = sr.ClientOrganization,
        ClientAddress          = sr.ClientAddress,
        SiteLocation           = sr.SiteLocation,
        Latitude               = sr.Latitude,
        Longitude              = sr.Longitude,
        Description            = sr.Description,
        SpecialInstructions    = sr.SpecialInstructions,
        HasSignature           = sr.ClientSignatureData != null,
        ClientSignedAt         = sr.ClientSignedAt,
        OtpVerifiedAt          = sr.OtpVerifiedAt,
        TicketId               = sr.TicketId,
        OperationsAssignmentId = sr.OperationsAssignmentId,
        CertificateNumber      = sr.CertificateNumber,
        CertificateIssuedAt    = sr.CertificateIssuedAt,
        ReviewedByTmId         = sr.ReviewedByTmId,
        TmReviewedAt           = sr.TmReviewedAt,
        TmComments             = sr.TmComments,
        RejectionReason        = sr.RejectionReason,
        ReviewChecklistJson    = sr.ReviewChecklistJson,
        PlannedServiceDate     = sr.PlannedServiceDate,
        AuthorizingName        = sr.AuthorizingName,
        CreatedAt              = sr.CreatedAt,
        UpdatedAt              = sr.UpdatedAt,
        Instruments = sr.Instruments.OrderBy(i => i.RowNumber).Select(i => new InstrumentDetailDto
        {
            Id                  = i.Id,
            RowNumber           = i.RowNumber,
            Description         = i.Description,
            Manufacturer        = i.Manufacturer,
            Model               = i.Model,
            SerialNumber        = i.SerialNumber,
            TagNumber           = i.TagNumber,
            Range               = i.Range,
            RangeUnit           = i.RangeUnit,
            Condition           = i.Condition,
            Remarks             = i.Remarks,
            LastCalibrationDate = i.LastCalibrationDate,
            CertificateNumber   = i.CertificateNumber,
            NawiInstrumentType  = i.NawiInstrumentType,
            NawiCapacity        = i.NawiCapacity,
            NawiScaleInterval   = i.NawiScaleInterval,
            NawiAccuracyClass   = i.NawiAccuracyClass,
            MassNominalValue    = i.MassNominalValue,
            MassAccuracyClass   = i.MassAccuracyClass,
            ServiceType         = i.ServiceType,
        }).ToList(),
        Quotation = sr.Quotation == null ? null : MapQuotation(sr.Quotation),
    };

    private static QuotationDetailDto MapQuotation(Quotation q)
    {
        var lineItems = JsonSerializer.Deserialize<List<QuotationLineItemDto>>(q.LineItemsJson, JsonRead)
                        ?? new List<QuotationLineItemDto>();

        return new QuotationDetailDto
        {
            Id              = q.Id,
            QuotationNumber = q.QuotationNumber,
            Status          = q.Status.ToString(),
            ValidUntil      = q.ValidUntil,
            LineItems       = lineItems,
            Subtotal        = q.Subtotal,
            VatRate         = q.VatRate,
            VatAmount       = q.VatAmount,
            TotalAmount     = q.TotalAmount,
            Notes           = q.Notes,
            SentAt          = q.SentAt,
            LpoNumber       = q.LpoNumber,
            LpoReceivedAt   = q.LpoReceivedAt,
            AcceptedAt      = q.AcceptedAt,
            RejectedAt      = q.RejectedAt,
            CreatedAt       = q.CreatedAt,
        };
    }
}
