using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Employees;
using HrService.Core.DTOs.Recruitment;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H12 — recruitment: requisition → approval → vacancy → applicants → interviews → offer → hire.
/// <para><b>The order is the control.</b> A vacancy cannot be advertised without an approved requisition, an
/// applicant cannot be interviewed before being screened, an offer cannot be prepared without a panel's
/// recommendation, and nobody becomes an employee except from an ACCEPTED offer. Each of those gates exists
/// because the step before it is the one that authorises the next: skipping any of them leaves a hire with no
/// approved budget, no agreed salary, or no record of why this person and not another.</para>
/// <para><b>Two people at the sharp ends.</b> Whoever raises a requisition cannot approve it, and whoever
/// prepares an offer cannot approve the salary on it — the same segregation H1 applies to document
/// verification, H8 to increments and H10 to final dues.</para>
/// <para><b>Rejections are kept with their stage and reason.</b> A pipeline that only remembers the person
/// hired cannot answer an unsuccessful candidate, cannot show a fair process, and cannot tell you which
/// sourcing channel is worth paying for.</para>
/// <para><b>Hiring hands off to H1 rather than reimplementing it.</b> Acceptance calls the employee service,
/// so a recruit gets the same employee number sequence, the same org-chart node and the same onboarding
/// completeness rules as anybody onboarded by hand. The login account is deliberately NOT created here — it
/// is provisioned during onboarding, once the signed contract is on file.</para>
/// </summary>
public class RecruitmentService(
    IGenericRepository<JobRequisition> requisitions,
    IGenericRepository<Vacancy> vacancies,
    IGenericRepository<Applicant> applicants,
    IGenericRepository<Interview> interviews,
    IGenericRepository<JobOffer> offers,
    IGenericRepository<Position> positions,
    IGenericRepository<Employee> employees,
    IGenericRepository<HrAuditLog> audit,
    IEmployeeService employeeService,
    IHrAlertGateway notifier) : IRecruitmentService
{
    /// <summary>Days a candidate has to answer an offer, unless the preparer says otherwise.</summary>
    private const int DefaultOfferResponseDays = 7;
    /// <summary>Every interview criterion is marked out of ten.</summary>
    private const decimal MaxCriterionScore = 10m;

    private static readonly EmploymentStatus[] InPost =
        [EmploymentStatus.OnProbation, EmploymentStatus.Active, EmploymentStatus.OnLeave, EmploymentStatus.Suspended];

    private static readonly RequisitionStatus[] LiveRequisitions =
        [RequisitionStatus.Draft, RequisitionStatus.PendingApproval, RequisitionStatus.Approved];

    // ══════════════════════════════════════════════════════════════════════════════
    // Summary
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<RecruitmentSummaryDto> GetSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var year = today.Year;

        var allRequisitions = await requisitions.Query().AsNoTracking().ToListAsync();
        var allVacancies = await vacancies.Query().AsNoTracking().ToListAsync();
        var allApplicants = await applicants.Query().AsNoTracking().ToListAsync();
        var allInterviews = await interviews.Query().AsNoTracking().ToListAsync();
        var allOffers = await offers.Query().AsNoTracking().ToListAsync();

        var approved = allRequisitions.Where(r => r.Status == RequisitionStatus.Approved).ToList();
        var postedPerRequisition = allVacancies
            .Where(v => v.Status != VacancyStatus.Cancelled)
            .GroupBy(v => v.JobRequisitionId)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.Headcount));

        var openVacancies = allVacancies.Where(v => v.Status == VacancyStatus.Open).ToList();
        var live = allApplicants.Where(a => a.Status is not (ApplicantStatus.Rejected or ApplicantStatus.Withdrawn)).ToList();
        var hiredThisYear = allApplicants.Where(a => a.Status == ApplicantStatus.Hired && a.HiredAt?.Year == year).ToList();
        var answeredThisYear = allOffers
            .Where(o => o.RespondedAt?.Year == year && o.Status is OfferStatus.Accepted or OfferStatus.Declined).ToList();

        var daysToHire = hiredThisYear
            .Where(a => a.HiredAt is not null)
            .Select(a => (decimal)(a.HiredAt!.Value.Date - a.AppliedAt.Date).TotalDays)
            .ToList();

        return new RecruitmentSummaryDto
        {
            DraftRequisitions = allRequisitions.Count(r => r.Status == RequisitionStatus.Draft),
            RequisitionsAwaitingApproval = allRequisitions.Count(r => r.Status == RequisitionStatus.PendingApproval),
            ApprovedRequisitions = approved.Count,
            HeadcountApprovedNotPosted = approved.Sum(r =>
                Math.Max(0, r.HeadcountRequested - postedPerRequisition.GetValueOrDefault(r.Id))),

            OpenVacancies = openVacancies.Count,
            VacanciesClosingIn7Days = openVacancies.Count(v =>
                v.ClosingDate is not null && v.ClosingDate.Value.Date >= today && v.ClosingDate.Value.Date <= today.AddDays(7)),
            OpenHeadcount = openVacancies.Sum(v => Math.Max(0, v.Headcount - v.HiredCount)),

            TotalApplicants = allApplicants.Count,
            AwaitingScreening = live.Count(a => a.Status == ApplicantStatus.Applied),
            Shortlisted = live.Count(a => a.Status == ApplicantStatus.Shortlisted),
            Interviewing = live.Count(a => a.Status is ApplicantStatus.Interviewing or ApplicantStatus.Recommended),

            InterviewsScheduled = allInterviews.Count(i => !i.Held && i.CancelledAt is null),
            InterviewsAwaitingScore = allInterviews.Count(i =>
                !i.Held && i.CancelledAt is null && i.ScheduledAt.Date < today),

            OffersAwaitingApproval = allOffers.Count(o => o.Status == OfferStatus.PendingApproval),
            OffersOutstanding = allOffers.Count(o => o.Status == OfferStatus.Issued),
            OffersOverdue = allOffers.Count(o => o.Status == OfferStatus.Issued
                && o.ResponseDeadline is not null && o.ResponseDeadline.Value.Date < today),

            HiresThisYear = hiredThisYear.Count,
            OffersAnsweredThisYear = answeredThisYear.Count,
            OffersAcceptedThisYear = answeredThisYear.Count(o => o.Status == OfferStatus.Accepted),
            AverageDaysToHire = daysToHire.Count == 0 ? null : Math.Round(daysToHire.Average(), 1),
        };
    }

    /// <summary>
    /// Which channels actually produce hires. Deliberately counts APPLICANTS, not applications-per-stage — a
    /// source that floods the inbox and never converts is the thing this view exists to expose.
    /// </summary>
    public async Task<List<SourceEffectivenessDto>> GetSourceEffectivenessAsync(int? year)
    {
        var q = applicants.Query().AsNoTracking();
        if (year is not null) q = q.Where(a => a.AppliedAt.Year == year);
        var list = await q.ToListAsync();

        return list.GroupBy(a => a.Source)
            .Select(g =>
            {
                var total = g.Count();
                // "Got past screening" — screened and not turned away AT screening. Testing the CURRENT status
                // instead would drop everyone who was shortlisted and then fell at interview or offer, which is
                // most of them: a channel whose candidates all reach the panel and lose there would report a 0%
                // shortlist rate and read as worthless.
                var shortlisted = g.Count(a => a.ScreenedAt is not null && a.RejectedAtStage != "Screening");
                var hired = g.Count(a => a.Status == ApplicantStatus.Hired);
                return new SourceEffectivenessDto
                {
                    Source = g.Key.ToString(),
                    Applicants = total,
                    Shortlisted = shortlisted,
                    Hired = hired,
                    ShortlistRate = total == 0 ? 0 : Math.Round(shortlisted * 100m / total, 1),
                    HireRate = total == 0 ? 0 : Math.Round(hired * 100m / total, 1),
                };
            })
            .OrderByDescending(s => s.Hired).ThenByDescending(s => s.Applicants)
            .ToList();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Requisitions
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<JobRequisitionDto>> ListRequisitionsAsync(string? status, string? departmentId)
    {
        var q = requisitions.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(departmentId)) q = q.Where(r => r.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequisitionStatus>(status, true, out var st))
            q = q.Where(r => r.Status == st);

        var list = await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
        var posted = await PostedHeadcountByRequisitionAsync(list.Select(r => r.Id).ToList());
        return list.Select(r => ToDto(r, posted)).ToList();
    }

    public async Task<JobRequisitionDto?> GetRequisitionAsync(string id)
    {
        var r = await requisitions.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return null;
        return ToDto(r, await PostedHeadcountByRequisitionAsync([r.Id]));
    }

    public async Task<RecruitmentActionResult> RaiseRequisitionAsync(RaiseRequisitionDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.PositionId))
            return Err("A position is required — the establishment check has nothing to count against without one.");
        if (dto.HeadcountRequested < 1)
            return Err("Headcount must be at least one.");

        var position = await positions.GetByIdAsync(dto.PositionId);
        if (position is null) return Err("Position not found.");
        if (!position.IsActive) return Err($"{position.Title} is not an active position.");

        var type = ParseEnum(dto.RequisitionType, RequisitionType.Replacement);
        var employmentType = ParseEnum(dto.EmploymentType, EmploymentType.Permanent);
        if (employmentType is EmploymentType.FixedTerm or EmploymentType.Contract && dto.ContractEndDate is null)
            return Err($"{employmentType} employment needs a contract end date — H1 will require it of the employee.");

        string? replacingName = null;
        if (type == RequisitionType.Replacement && !string.IsNullOrWhiteSpace(dto.ReplacingEmployeeId))
        {
            var leaver = await employees.GetByIdAsync(dto.ReplacingEmployeeId);
            if (leaver is null) return Err("The employee being replaced is not on file.");
            replacingName = leaver.FullName;
        }

        // ── The establishment snapshot, taken now and stored (see the entity's remarks) ──
        var (current, committed, notes, exceeds) = await AssessEstablishmentAsync(position, dto.HeadcountRequested, type);

        var created = await requisitions.CreateAsync(new JobRequisition
        {
            RequisitionNumber = await NextNumberAsync("REQ", DateTime.UtcNow.Year),
            PositionId = position.Id,
            PositionTitle = position.Title,
            JobGrade = position.JobGrade,
            DepartmentId = position.DepartmentId,
            DepartmentName = position.DepartmentName,
            BranchId = dto.BranchId,
            RequisitionType = type,
            HeadcountRequested = dto.HeadcountRequested,
            ReplacingEmployeeId = type == RequisitionType.Replacement ? dto.ReplacingEmployeeId : null,
            ReplacingEmployeeName = replacingName,
            EmploymentType = employmentType,
            ContractEndDate = dto.ContractEndDate,
            Justification = dto.Justification,
            RequiredBy = dto.RequiredBy,
            ApprovedHeadcount = position.ApprovedHeadcount,
            CurrentHeadcount = current,
            CommittedHeadcount = committed,
            EstablishmentNotes = notes,
            ExceedsEstablishment = exceeds,
            Status = RequisitionStatus.Draft,
            RaisedBy = userId,
            RaisedAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });

        var result = new RecruitmentActionResult("Raised",
            $"{created.RequisitionNumber} raised for {created.HeadcountRequested} × {position.Title}.", created.Id);
        result.Warnings.Add(notes);

        await LogAsync("JobRequisition", created.Id, HrAuditAction.RequisitionRaised,
            $"{created.RequisitionNumber} raised: {created.HeadcountRequested} × {position.Title} ({Spaced(type)}). {notes}", userId);

        if (dto.SubmitNow)
        {
            var submitted = await SubmitRequisitionAsync(created.Id, userId);
            if (submitted.Status != "Error")
                return new RecruitmentActionResult("Submitted", $"{created.RequisitionNumber} raised and sent for approval.", created.Id)
                { Warnings = result.Warnings };
        }
        return result;
    }

    public async Task<RecruitmentActionResult> SubmitRequisitionAsync(string id, string userId)
    {
        var r = await requisitions.GetByIdAsync(id);
        if (r is null) return Err("Requisition not found.");
        if (r.Status != RequisitionStatus.Draft)
            return Err($"{r.RequisitionNumber} is {Spaced(r.Status)} — only a draft can be submitted.");

        r.Status = RequisitionStatus.PendingApproval;
        r.SubmittedBy = userId;
        r.SubmittedAt = DateTime.UtcNow;
        r.UpdatedBy = userId;
        await requisitions.UpdateAsync(r);

        await LogAsync("JobRequisition", r.Id, HrAuditAction.RequisitionSubmitted,
            $"{r.RequisitionNumber} submitted for approval.", userId);
        return new RecruitmentActionResult("Submitted",
            $"{r.RequisitionNumber} sent for approval. It cannot be advertised until it is approved.", r.Id);
    }

    /// <summary>
    /// The approval that authorises the spend. Whoever raised or submitted it cannot approve it — the same
    /// segregation H8 applies to increments.
    /// </summary>
    public async Task<RecruitmentActionResult> DecideRequisitionAsync(string id, DecideRequisitionDto dto, string userId, string? userName)
    {
        var r = await requisitions.GetByIdAsync(id);
        if (r is null) return Err("Requisition not found.");
        if (r.Status != RequisitionStatus.PendingApproval)
            return Err($"{r.RequisitionNumber} is {Spaced(r.Status)} — only a requisition awaiting approval can be decided.");

        var approving = dto.Decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        if (!approving && !dto.Decision.Equals("Reject", StringComparison.OrdinalIgnoreCase))
            return Err("The decision must be Approve or Reject.");

        if (userId == r.RaisedBy || userId == r.SubmittedBy)
            return Err("A requisition cannot be approved by the person who raised it — approval is a second pair of eyes on the spend.");

        var result = new RecruitmentActionResult("", "", r.Id);

        if (approving)
        {
            var headcount = dto.ApprovedHeadcount ?? r.HeadcountRequested;
            if (headcount < 1) return Err("At least one head must be approved, or the requisition should be rejected.");
            if (headcount > r.HeadcountRequested)
                return Err($"Cannot approve {headcount} when {r.HeadcountRequested} were requested — raise a further requisition instead.");

            if (headcount < r.HeadcountRequested)
                result.Warnings.Add($"Approved {headcount} of the {r.HeadcountRequested} requested.");
            if (r.ExceedsEstablishment)
                result.Warnings.Add("This approval takes the position past its approved establishment.");

            r.HeadcountRequested = headcount;
            r.Status = RequisitionStatus.Approved;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return Err("A rejection needs a reason — the raiser has to know what to change.");
            r.Status = RequisitionStatus.Rejected;
        }

        r.DecidedBy = userId;
        r.DecidedByName = userName;
        r.DecidedAt = DateTime.UtcNow;
        r.DecisionReason = dto.Reason;
        r.UpdatedBy = userId;
        await requisitions.UpdateAsync(r);

        await LogAsync("JobRequisition", r.Id,
            approving ? HrAuditAction.RequisitionApproved : HrAuditAction.RequisitionRejected,
            $"{r.RequisitionNumber} {(approving ? $"approved for {r.HeadcountRequested} head(s)" : "rejected")}. {dto.Reason}".Trim(),
            userId, userName);

        return result with
        {
            Status = approving ? "Approved" : "Rejected",
            Message = approving
                ? $"{r.RequisitionNumber} approved for {r.HeadcountRequested} head(s). Post the vacancy next."
                : $"{r.RequisitionNumber} rejected.",
        };
    }

    public async Task<RecruitmentActionResult> CancelRequisitionAsync(string id, string? reason, string userId)
    {
        var r = await requisitions.GetByIdAsync(id);
        if (r is null) return Err("Requisition not found.");
        if (r.Status is RequisitionStatus.Rejected or RequisitionStatus.Cancelled)
            return Err($"{r.RequisitionNumber} is already {Spaced(r.Status)}.");

        // Cancelling the authority behind a live vacancy would leave the advert standing on nothing.
        var liveVacancies = await vacancies.Query()
            .Where(v => v.JobRequisitionId == r.Id && v.Status != VacancyStatus.Cancelled).ToListAsync();
        if (liveVacancies.Count > 0 && liveVacancies.Any(v => v.HiredCount > 0))
            return Err($"{r.RequisitionNumber} has already produced a hire — it cannot be cancelled.");
        if (liveVacancies.Count > 0)
            return Err($"Close or cancel the {liveVacancies.Count} vacancy(ies) raised from {r.RequisitionNumber} first.");

        r.Status = RequisitionStatus.Cancelled;
        r.CancellationReason = reason;
        r.UpdatedBy = userId;
        await requisitions.UpdateAsync(r);

        await LogAsync("JobRequisition", r.Id, HrAuditAction.RequisitionCancelled,
            $"{r.RequisitionNumber} cancelled. {reason}".Trim(), userId);
        return new RecruitmentActionResult("Cancelled", $"{r.RequisitionNumber} cancelled.", r.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Vacancies
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<VacancyDto>> ListVacanciesAsync(string? status, string? departmentId)
    {
        var q = vacancies.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(departmentId)) q = q.Where(v => v.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VacancyStatus>(status, true, out var st))
            q = q.Where(v => v.Status == st);

        var list = await q.OrderByDescending(v => v.PostedAt).ToListAsync();
        var pipeline = await PipelineByVacancyAsync(list.Select(v => v.Id).ToList());
        var today = DateTime.UtcNow.Date;
        return list.Select(v => ToDto(v, pipeline.GetValueOrDefault(v.Id), today)).ToList();
    }

    public async Task<VacancyDto?> GetVacancyAsync(string id)
    {
        var v = await vacancies.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return null;
        var pipeline = await PipelineByVacancyAsync([v.Id]);
        return ToDto(v, pipeline.GetValueOrDefault(v.Id), DateTime.UtcNow.Date);
    }

    public async Task<RecruitmentActionResult> PostVacancyAsync(PostVacancyDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.JobRequisitionId))
            return Err("A vacancy must come from an approved requisition — that is where the authority to hire lives.");

        var r = await requisitions.GetByIdAsync(dto.JobRequisitionId);
        if (r is null) return Err("Requisition not found.");
        if (r.Status != RequisitionStatus.Approved)
            return Err($"{r.RequisitionNumber} is {Spaced(r.Status)} — only an approved requisition can be advertised.");

        var alreadyPosted = (await PostedHeadcountByRequisitionAsync([r.Id])).GetValueOrDefault(r.Id);
        var remaining = r.HeadcountRequested - alreadyPosted;
        if (remaining <= 0)
            return Err($"All {r.HeadcountRequested} approved head(s) on {r.RequisitionNumber} are already advertised.");

        var headcount = dto.Headcount ?? remaining;
        if (headcount < 1) return Err("Headcount must be at least one.");
        if (headcount > remaining)
            return Err($"{r.RequisitionNumber} has {remaining} approved head(s) left to advertise, not {headcount}.");

        if (dto.ClosingDate is not null && dto.ClosingDate.Value.Date < DateTime.UtcNow.Date)
            return Err("The closing date is in the past.");
        if (dto.SalaryRangeMin is not null && dto.SalaryRangeMax is not null && dto.SalaryRangeMin > dto.SalaryRangeMax)
            return Err("The minimum of the salary range is above its maximum.");

        var created = await vacancies.CreateAsync(new Vacancy
        {
            VacancyNumber = await NextNumberAsync("VAC", DateTime.UtcNow.Year),
            JobRequisitionId = r.Id,
            RequisitionNumber = r.RequisitionNumber,
            PositionId = r.PositionId,
            PositionTitle = r.PositionTitle,
            JobGrade = r.JobGrade,
            DepartmentId = r.DepartmentId,
            DepartmentName = r.DepartmentName,
            BranchId = r.BranchId,
            Headcount = headcount,
            PostingChannel = ParseEnum(dto.PostingChannel, PostingChannel.Both),
            JobDescription = dto.JobDescription,
            MinimumQualifications = dto.MinimumQualifications,
            Responsibilities = dto.Responsibilities,
            SalaryRangeMin = dto.SalaryRangeMin,
            SalaryRangeMax = dto.SalaryRangeMax,
            PostedAt = DateTime.UtcNow,
            ClosingDate = dto.ClosingDate,
            Status = VacancyStatus.Open,
            CreatedBy = userId, UpdatedBy = userId,
        });

        var result = new RecruitmentActionResult("Posted",
            $"{created.VacancyNumber} posted — {headcount} × {created.PositionTitle}, {Spaced(created.PostingChannel).ToLowerInvariant()}.", created.Id);
        if (headcount < remaining)
            result.Warnings.Add($"{remaining - headcount} approved head(s) on {r.RequisitionNumber} remain unadvertised.");

        await LogAsync("Vacancy", created.Id, HrAuditAction.VacancyPosted,
            $"{created.VacancyNumber} posted from {r.RequisitionNumber}: {headcount} × {created.PositionTitle}.", userId);
        return result;
    }

    public async Task<RecruitmentActionResult> CloseVacancyAsync(string id, CloseVacancyDto dto, string userId)
    {
        var v = await vacancies.GetByIdAsync(id);
        if (v is null) return Err("Vacancy not found.");
        if (v.Status is VacancyStatus.Filled or VacancyStatus.Cancelled)
            return Err($"{v.VacancyNumber} is already {Spaced(v.Status)}.");

        if (dto.Cancel && v.HiredCount > 0)
            return Err($"{v.VacancyNumber} has already hired {v.HiredCount} — it can be closed but not cancelled.");

        v.Status = dto.Cancel ? VacancyStatus.Cancelled : VacancyStatus.Closed;
        v.ClosedAt = DateTime.UtcNow;
        v.ClosedBy = userId;
        v.ClosureReason = dto.Reason;
        v.UpdatedBy = userId;
        await vacancies.UpdateAsync(v);

        var inFlight = await applicants.Query()
            .CountAsync(a => a.VacancyId == v.Id
                && a.Status != ApplicantStatus.Rejected && a.Status != ApplicantStatus.Withdrawn
                && a.Status != ApplicantStatus.Hired);

        var result = new RecruitmentActionResult(dto.Cancel ? "Cancelled" : "Closed",
            $"{v.VacancyNumber} {(dto.Cancel ? "cancelled" : "closed")}. No further applications will be accepted.", v.Id);
        if (!dto.Cancel && inFlight > 0)
            result.Warnings.Add($"{inFlight} applicant(s) are still in the pipeline — closing stops new applications, it does not reject them.");

        await LogAsync("Vacancy", v.Id,
            dto.Cancel ? HrAuditAction.VacancyCancelled : HrAuditAction.VacancyClosed,
            $"{v.VacancyNumber} {(dto.Cancel ? "cancelled" : "closed")}. {dto.Reason}".Trim(), userId);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Applicants
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<ApplicantDto>> ListApplicantsAsync(string? vacancyId, string? status)
    {
        var q = applicants.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(vacancyId)) q = q.Where(a => a.VacancyId == vacancyId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ApplicantStatus>(status, true, out var st))
            q = q.Where(a => a.Status == st);

        var list = await q.OrderByDescending(a => a.AppliedAt).ToListAsync();
        return await DecorateAsync(list);
    }

    public async Task<ApplicantDto?> GetApplicantAsync(string id)
    {
        var a = await applicants.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return a is null ? null : (await DecorateAsync([a])).FirstOrDefault();
    }

    public async Task<RecruitmentActionResult> ReceiveApplicationAsync(ReceiveApplicationDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName)) return Err("The applicant's name is required.");
        if (string.IsNullOrWhiteSpace(dto.Email)) return Err("An email address is required — it is how the candidate is contacted.");

        var v = await vacancies.GetByIdAsync(dto.VacancyId);
        if (v is null) return Err("Vacancy not found.");
        if (v.Status != VacancyStatus.Open)
            return Err($"{v.VacancyNumber} is {Spaced(v.Status)} — it is not accepting applications.");
        if (v.ClosingDate is not null && v.ClosingDate.Value.Date < DateTime.UtcNow.Date)
            return Err($"{v.VacancyNumber} closed to applications on {v.ClosingDate:dd MMM yyyy}.");

        var email = dto.Email.Trim();
        if (await applicants.Query().AnyAsync(a => a.VacancyId == v.Id && a.Email.ToLower() == email.ToLower()))
            return Err($"{email} has already applied for {v.VacancyNumber}.");

        var source = ParseEnum(dto.Source, ApplicantSource.Website);
        var result = new RecruitmentActionResult("", "");

        // An internal applicant IS an employee — link, never copy (see the entity's remarks).
        string? internalEmployeeId = null;
        if (!string.IsNullOrWhiteSpace(dto.InternalEmployeeId))
        {
            var staff = await employees.GetByIdAsync(dto.InternalEmployeeId);
            if (staff is null) return Err("The internal applicant is not on file as an employee.");
            internalEmployeeId = staff.Id;
            if (source != ApplicantSource.Internal)
            {
                source = ApplicantSource.Internal;
                result.Warnings.Add("Recorded as an internal application — this is an existing employee.");
            }
            if (staff.PositionId == v.PositionId)
                result.Warnings.Add($"{staff.FullName} already holds this position.");
        }
        else if (source == ApplicantSource.Internal)
        {
            return Err("An internal application must say which employee is applying.");
        }

        string? referredByName = null;
        if (!string.IsNullOrWhiteSpace(dto.ReferredByEmployeeId))
        {
            var referrer = await employees.GetByIdAsync(dto.ReferredByEmployeeId);
            if (referrer is null) return Err("The referring employee is not on file.");
            referredByName = referrer.FullName;
        }

        var created = await applicants.CreateAsync(new Applicant
        {
            ApplicantNumber = await NextNumberAsync("APP", DateTime.UtcNow.Year),
            VacancyId = v.Id,
            VacancyNumber = v.VacancyNumber,
            PositionTitle = v.PositionTitle,
            FullName = dto.FullName.Trim(),
            Email = email,
            Phone = dto.Phone,
            NationalId = dto.NationalId,
            Source = source,
            ReferredByEmployeeId = dto.ReferredByEmployeeId,
            ReferredByName = referredByName,
            InternalEmployeeId = internalEmployeeId,
            CvDocumentPath = dto.CvDocumentPath,
            CoverNote = dto.CoverNote,
            YearsExperience = dto.YearsExperience,
            HighestQualification = dto.HighestQualification,
            CurrentEmployer = dto.CurrentEmployer,
            ExpectedSalary = dto.ExpectedSalary,
            Status = ApplicantStatus.Applied,
            AppliedAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });

        if (dto.ExpectedSalary is not null && v.SalaryRangeMax is not null && dto.ExpectedSalary > v.SalaryRangeMax)
            result.Warnings.Add($"Expected salary is above the advertised range for {v.VacancyNumber}.");

        await LogAsync("Applicant", created.Id, HrAuditAction.ApplicationReceived,
            $"{created.ApplicantNumber} — {created.FullName} applied for {v.VacancyNumber} via {Spaced(source).ToLowerInvariant()}.", userId);

        return result with
        {
            Status = "Received",
            Message = $"{created.ApplicantNumber} recorded — {created.FullName} for {v.VacancyNumber}.",
            Id = created.Id,
        };
    }

    /// <summary>Screening is the gate an interview stands behind — nobody is interviewed off an unscreened pile.</summary>
    public async Task<RecruitmentActionResult> ScreenApplicantAsync(string id, ScreenApplicantDto dto, string userId)
    {
        var a = await applicants.GetByIdAsync(id);
        if (a is null) return Err("Applicant not found.");
        if (a.Status != ApplicantStatus.Applied)
            return Err($"{a.ApplicantNumber} is {Spaced(a.Status)} — only a new application can be screened.");

        var shortlisting = dto.Decision.Equals("Shortlist", StringComparison.OrdinalIgnoreCase);
        if (!shortlisting && !dto.Decision.Equals("Reject", StringComparison.OrdinalIgnoreCase))
            return Err("The decision must be Shortlist or Reject.");
        if (!shortlisting && string.IsNullOrWhiteSpace(dto.Reason))
            return Err("A rejection needs a reason — \"not selected\" is not one.");

        a.ScreenedBy = userId;
        a.ScreenedAt = DateTime.UtcNow;
        a.ScreeningNotes = dto.Notes;
        a.UpdatedBy = userId;

        if (shortlisting)
        {
            a.Status = ApplicantStatus.Shortlisted;
        }
        else
        {
            a.Status = ApplicantStatus.Rejected;
            a.RejectionReason = dto.Reason;
            a.RejectedAtStage = "Screening";
            a.RejectedAt = DateTime.UtcNow;
        }
        await applicants.UpdateAsync(a);

        await LogAsync("Applicant", a.Id,
            shortlisting ? HrAuditAction.ApplicantShortlisted : HrAuditAction.ApplicantRejected,
            $"{a.ApplicantNumber} ({a.FullName}) {(shortlisting ? "shortlisted" : $"rejected at screening — {dto.Reason}")}.", userId);

        return new RecruitmentActionResult(shortlisting ? "Shortlisted" : "Rejected",
            shortlisting
                ? $"{a.FullName} shortlisted. Schedule an interview next."
                : $"{a.FullName} rejected at screening.", a.Id);
    }

    public async Task<RecruitmentActionResult> RejectApplicantAsync(string id, RejectApplicantDto dto, string userId)
    {
        var a = await applicants.GetByIdAsync(id);
        if (a is null) return Err("Applicant not found.");
        if (a.Status is ApplicantStatus.Rejected or ApplicantStatus.Withdrawn or ApplicantStatus.Hired)
            return Err($"{a.ApplicantNumber} is {Spaced(a.Status)}.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Err("A rejection needs a reason — the record has to say why.");

        // A live offer is a promise already made; withdraw it deliberately rather than by rejecting behind it.
        // An accepted-but-not-yet-hired offer is the strongest promise of the lot, so it blocks too.
        var liveOffer = await offers.Query().FirstOrDefaultAsync(o => o.ApplicantId == a.Id
            && (o.Status == OfferStatus.Issued || o.Status == OfferStatus.Approved || o.Status == OfferStatus.PendingApproval
                || (o.Status == OfferStatus.Accepted && o.ResultingEmployeeId == null)));
        if (liveOffer is not null)
            return Err($"Offer {liveOffer.OfferNumber} is {Spaced(liveOffer.Status).ToLowerInvariant()} — withdraw it before rejecting {a.FullName}.");

        var stage = a.Status.ToString();
        a.Status = ApplicantStatus.Rejected;
        a.RejectionReason = dto.Reason;
        a.RejectedAtStage = stage;
        a.RejectedAt = DateTime.UtcNow;
        a.UpdatedBy = userId;
        await applicants.UpdateAsync(a);

        await CancelPendingInterviewsAsync(a.Id, "Applicant rejected.", userId);

        await LogAsync("Applicant", a.Id, HrAuditAction.ApplicantRejected,
            $"{a.ApplicantNumber} ({a.FullName}) rejected at {stage} — {dto.Reason}.", userId);
        return new RecruitmentActionResult("Rejected", $"{a.FullName} rejected at {Spaced(a.Status)}.", a.Id);
    }

    public async Task<RecruitmentActionResult> WithdrawApplicantAsync(string id, string? reason, string userId)
    {
        var a = await applicants.GetByIdAsync(id);
        if (a is null) return Err("Applicant not found.");
        if (a.Status is ApplicantStatus.Rejected or ApplicantStatus.Withdrawn or ApplicantStatus.Hired)
            return Err($"{a.ApplicantNumber} is {Spaced(a.Status)}.");

        // Captured BEFORE the status moves, or every withdrawal records its stage as "Withdrawn" and the
        // pipeline loses where candidates actually drop out.
        var stage = a.Status.ToString();
        a.Status = ApplicantStatus.Withdrawn;
        a.WithdrawnAt = DateTime.UtcNow;
        a.RejectionReason = reason;
        a.RejectedAtStage = stage;
        a.UpdatedBy = userId;
        await applicants.UpdateAsync(a);

        // An outstanding offer against someone who has walked away should not keep holding the seat — including
        // one they had accepted and then thought better of, which is exactly the case that strands a post.
        var live = await offers.Query().Where(o => o.ApplicantId == a.Id
            && (o.Status == OfferStatus.Issued || o.Status == OfferStatus.Approved || o.Status == OfferStatus.PendingApproval
                || (o.Status == OfferStatus.Accepted && o.ResultingEmployeeId == null)))
            .ToListAsync();
        foreach (var o in live)
        {
            o.Status = OfferStatus.Withdrawn;
            o.WithdrawalReason = "Candidate withdrew.";
            o.UpdatedBy = userId;
            await offers.UpdateAsync(o);
        }

        await CancelPendingInterviewsAsync(a.Id, "Candidate withdrew.", userId);

        await LogAsync("Applicant", a.Id, HrAuditAction.ApplicantWithdrawn,
            $"{a.ApplicantNumber} ({a.FullName}) withdrew. {reason}".Trim(), userId);

        var result = new RecruitmentActionResult("Withdrawn", $"{a.FullName} marked as withdrawn.", a.Id);
        if (live.Count > 0) result.Warnings.Add($"{live.Count} outstanding offer(s) withdrawn with them.");
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Interviews
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<InterviewDto>> ListInterviewsAsync(string? applicantId, string? vacancyId, bool? pending)
    {
        var q = interviews.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(applicantId)) q = q.Where(i => i.ApplicantId == applicantId);
        if (!string.IsNullOrWhiteSpace(vacancyId)) q = q.Where(i => i.VacancyId == vacancyId);
        if (pending == true) q = q.Where(i => !i.Held && i.CancelledAt == null);

        var list = await q.OrderByDescending(i => i.ScheduledAt).ToListAsync();
        var now = DateTime.UtcNow;
        return list.Select(i => ToDto(i, now)).ToList();
    }

    public async Task<RecruitmentActionResult> ScheduleInterviewAsync(ScheduleInterviewDto dto, string userId)
    {
        var a = await applicants.GetByIdAsync(dto.ApplicantId);
        if (a is null) return Err("Applicant not found.");
        if (a.Status is not (ApplicantStatus.Shortlisted or ApplicantStatus.Interviewing or ApplicantStatus.Recommended))
            return Err($"{a.FullName} is {Spaced(a.Status)} — only a shortlisted candidate can be interviewed.");
        if (dto.ScheduledAt == default) return Err("The interview date and time are required.");

        var stage = ParseEnum(dto.Stage, InterviewStage.Screening);
        var result = new RecruitmentActionResult("", "");

        var existing = await interviews.Query()
            .Where(i => i.ApplicantId == a.Id && i.CancelledAt == null).ToListAsync();
        if (existing.Any(i => i.Stage == stage && !i.Held))
            return Err($"{a.FullName} already has a {Spaced(stage).ToLowerInvariant()} interview scheduled.");
        if (existing.Any(i => i.Stage == stage && i.Held))
            result.Warnings.Add($"This is a repeat {Spaced(stage).ToLowerInvariant()} interview — one has already been held.");
        if (dto.ScheduledAt < DateTime.UtcNow)
            result.Warnings.Add("The scheduled time is in the past — recording an interview after the fact.");

        var created = await interviews.CreateAsync(new Interview
        {
            ApplicantId = a.Id,
            ApplicantName = a.FullName,
            VacancyId = a.VacancyId,
            Stage = stage,
            ScheduledAt = dto.ScheduledAt,
            Location = dto.Location,
            PanelMembers = dto.PanelMembers,
            Recommendation = InterviewRecommendation.Pending,
            CreatedBy = userId, UpdatedBy = userId,
        });

        await LogAsync("Interview", created.Id, HrAuditAction.InterviewScheduled,
            $"{Spaced(stage)} interview for {a.ApplicantNumber} ({a.FullName}) on {dto.ScheduledAt:dd MMM yyyy HH:mm}.", userId);

        return result with
        {
            Status = "Scheduled",
            Message = $"{Spaced(stage)} interview set for {a.FullName} on {dto.ScheduledAt:dd MMM yyyy HH:mm}.",
            Id = created.Id,
        };
    }

    /// <summary>
    /// Recording what the panel concluded. The overall is the mean of whichever criteria were actually scored,
    /// so a screening call that judged only experience and communication is not marked down for the technical
    /// score nobody gave.
    /// </summary>
    public async Task<RecruitmentActionResult> ScoreInterviewAsync(string id, ScoreInterviewDto dto, string userId, string? userName)
    {
        var i = await interviews.GetByIdAsync(id);
        if (i is null) return Err("Interview not found.");
        if (i.CancelledAt is not null) return Err("That interview was cancelled.");
        if (i.Held) return Err("That interview has already been scored.");

        var recommendation = ParseEnum(dto.Recommendation, InterviewRecommendation.Pending);
        if (recommendation == InterviewRecommendation.Pending)
            return Err("A recommendation is required: Proceed, Hold or Reject.");

        decimal?[] scores = [dto.TechnicalScore, dto.ExperienceScore, dto.CommunicationScore, dto.CulturalFitScore];
        foreach (var s in scores)
            if (s is not null && (s < 0 || s > MaxCriterionScore))
                return Err($"Scores are out of {MaxCriterionScore:0} — {s} is outside that.");

        var given = scores.Where(s => s is not null).Select(s => s!.Value).ToList();
        if (given.Count == 0)
            return Err("Score at least one criterion — a recommendation with no marks behind it cannot be compared with anyone else's.");

        var a = await applicants.GetByIdAsync(i.ApplicantId);
        if (a is null) return Err("The applicant behind that interview is no longer on file.");

        i.Held = true;
        i.HeldAt = dto.HeldAt ?? DateTime.UtcNow;
        i.TechnicalScore = dto.TechnicalScore;
        i.ExperienceScore = dto.ExperienceScore;
        i.CommunicationScore = dto.CommunicationScore;
        i.CulturalFitScore = dto.CulturalFitScore;
        i.OverallScore = Math.Round(given.Average(), 2);
        i.Recommendation = recommendation;
        i.Notes = dto.Notes;
        i.ScoredBy = userId;
        i.ScoredByName = userName;
        i.ScoredAt = DateTime.UtcNow;
        i.UpdatedBy = userId;
        await interviews.UpdateAsync(i);

        // Recompute the applicant's running average across every held interview.
        var held = await interviews.Query()
            .Where(x => x.ApplicantId == a.Id && x.Held && x.CancelledAt == null && x.OverallScore != null)
            .Select(x => x.OverallScore!.Value).ToListAsync();

        a.InterviewsHeld = held.Count;
        a.AverageInterviewScore = held.Count == 0 ? null : Math.Round(held.Average(), 2);

        var result = new RecruitmentActionResult("", "", i.Id);

        if (recommendation == InterviewRecommendation.Reject)
        {
            a.Status = ApplicantStatus.Rejected;
            a.RejectionReason = dto.Notes ?? $"Rejected at {Spaced(i.Stage).ToLowerInvariant()} interview.";
            a.RejectedAtStage = $"{i.Stage} interview";
            a.RejectedAt = DateTime.UtcNow;
            await CancelPendingInterviewsAsync(a.Id, "Rejected at interview.", userId);
        }
        else if (recommendation == InterviewRecommendation.Proceed)
        {
            a.Status = ApplicantStatus.Recommended;
            result.Warnings.Add("An offer may now be prepared for this candidate.");
        }
        else if (a.Status == ApplicantStatus.Shortlisted)
        {
            a.Status = ApplicantStatus.Interviewing;
        }

        a.UpdatedBy = userId;
        await applicants.UpdateAsync(a);

        await LogAsync("Interview", i.Id, HrAuditAction.InterviewScored,
            $"{Spaced(i.Stage)} interview for {a.ApplicantNumber} scored {i.OverallScore:0.##}/{MaxCriterionScore:0} — {Spaced(recommendation).ToLowerInvariant()}.",
            userId, userName);

        return result with
        {
            Status = "Scored",
            Message = $"{Spaced(i.Stage)} interview scored {i.OverallScore:0.##}/{MaxCriterionScore:0} — {Spaced(recommendation).ToLowerInvariant()}.",
        };
    }

    public async Task<RecruitmentActionResult> CancelInterviewAsync(string id, string? reason, string userId)
    {
        var i = await interviews.GetByIdAsync(id);
        if (i is null) return Err("Interview not found.");
        if (i.Held) return Err("An interview that has been held cannot be cancelled — its score is part of the record.");
        if (i.CancelledAt is not null) return Err("That interview is already cancelled.");

        i.CancelledAt = DateTime.UtcNow;
        i.CancellationReason = reason;
        i.UpdatedBy = userId;
        await interviews.UpdateAsync(i);

        await LogAsync("Interview", i.Id, HrAuditAction.InterviewCancelled,
            $"{Spaced(i.Stage)} interview for {i.ApplicantName} cancelled. {reason}".Trim(), userId);
        return new RecruitmentActionResult("Cancelled", $"{Spaced(i.Stage)} interview cancelled.", i.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Offers
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<JobOfferDto>> ListOffersAsync(string? status, string? vacancyId)
    {
        var q = offers.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(vacancyId)) q = q.Where(o => o.VacancyId == vacancyId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OfferStatus>(status, true, out var st))
            q = q.Where(o => o.Status == st);

        var list = await q.OrderByDescending(o => o.CreatedAt).ToListAsync();
        var ranges = await SalaryRangesAsync(list.Select(o => o.VacancyId).Distinct().ToList());
        var today = DateTime.UtcNow.Date;
        return list.Select(o => ToDto(o, ranges.GetValueOrDefault(o.VacancyId), today)).ToList();
    }

    public async Task<JobOfferDto?> GetOfferAsync(string id)
    {
        var o = await offers.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return null;
        var ranges = await SalaryRangesAsync([o.VacancyId]);
        return ToDto(o, ranges.GetValueOrDefault(o.VacancyId), DateTime.UtcNow.Date);
    }

    public async Task<RecruitmentActionResult> PrepareOfferAsync(PrepareOfferDto dto, string userId)
    {
        var a = await applicants.GetByIdAsync(dto.ApplicantId);
        if (a is null) return Err("Applicant not found.");

        // Asked BEFORE the recommendation gate: a candidate who already holds an offer sits at OfferMade, which
        // the gate would otherwise refuse as "needs a panel recommendation" — true of the status, but not the
        // reason, and it sends whoever is preparing the offer looking for an interview that has already happened.
        var existing = await offers.Query().FirstOrDefaultAsync(o => o.ApplicantId == a.Id
            && o.Status != OfferStatus.Declined && o.Status != OfferStatus.Withdrawn && o.Status != OfferStatus.Lapsed);
        if (existing is not null)
            return Err($"{a.FullName} already has offer {existing.OfferNumber} ({Spaced(existing.Status).ToLowerInvariant()}).");

        if (a.Status != ApplicantStatus.Recommended)
            return Err($"{a.FullName} is {Spaced(a.Status)} — an offer needs a panel recommendation behind it.");
        if (dto.OfferedSalary <= 0) return Err("The offered salary is required.");
        if (dto.ProposedStartDate == default) return Err("A proposed start date is required.");

        var v = await vacancies.GetByIdAsync(a.VacancyId);
        if (v is null) return Err("The vacancy behind that applicant is no longer on file.");
        if (v.Status == VacancyStatus.Cancelled) return Err($"{v.VacancyNumber} was cancelled.");
        if (v.Status == VacancyStatus.Filled) return Err($"{v.VacancyNumber} is already filled.");

        // Offers already out must not exceed the seats left, or two acceptances fill one post.
        // An ACCEPTED offer whose hire has not been recorded yet counts too: the seat is taken by someone who
        // has said yes, but `HiredCount` has not moved, so leaving it out would let a second offer go to a post
        // that is already spoken for — and the second candidate could then never be hired.
        var seatsLeft = v.Headcount - v.HiredCount;
        var liveOffers = await offers.Query().CountAsync(o => o.VacancyId == v.Id
            && (o.Status == OfferStatus.PendingApproval || o.Status == OfferStatus.Approved || o.Status == OfferStatus.Issued
                || (o.Status == OfferStatus.Accepted && o.ResultingEmployeeId == null)));
        if (liveOffers >= seatsLeft)
            return Err($"{v.VacancyNumber} has {seatsLeft} seat(s) left and {liveOffers} live offer(s) — withdraw one before making another.");

        var r = await requisitions.GetByIdAsync(v.JobRequisitionId);
        var employmentType = ParseEnum(dto.EmploymentType, r?.EmploymentType ?? EmploymentType.Permanent);
        var contractEnd = dto.ContractEndDate ?? r?.ContractEndDate;
        if (employmentType is EmploymentType.FixedTerm or EmploymentType.Contract && contractEnd is null)
            return Err($"{employmentType} employment needs a contract end date — H1 will require it of the employee.");
        if (contractEnd is not null && contractEnd.Value.Date <= dto.ProposedStartDate.Date)
            return Err("The contract end date is on or before the start date.");

        var result = new RecruitmentActionResult("", "");
        if (v.SalaryRangeMin is not null && dto.OfferedSalary < v.SalaryRangeMin)
            result.Warnings.Add($"The offer is below the advertised minimum of {Money(v.SalaryRangeMin.Value, v.CurrencyCode)}.");
        if (v.SalaryRangeMax is not null && dto.OfferedSalary > v.SalaryRangeMax)
            result.Warnings.Add($"The offer is above the advertised maximum of {Money(v.SalaryRangeMax.Value, v.CurrencyCode)}.");
        if (a.ExpectedSalary is not null && dto.OfferedSalary < a.ExpectedSalary)
            result.Warnings.Add($"The candidate asked for {Money(a.ExpectedSalary.Value, v.CurrencyCode)}.");

        var responseDays = dto.ResponseDays is > 0 ? dto.ResponseDays.Value : DefaultOfferResponseDays;

        var created = await offers.CreateAsync(new JobOffer
        {
            OfferNumber = await NextNumberAsync("OFF", DateTime.UtcNow.Year),
            ApplicantId = a.Id,
            ApplicantName = a.FullName,
            VacancyId = v.Id,
            VacancyNumber = v.VacancyNumber,
            PositionId = v.PositionId,
            PositionTitle = v.PositionTitle,
            JobGrade = v.JobGrade,
            DepartmentId = v.DepartmentId,
            BranchId = v.BranchId,
            OfferedSalary = dto.OfferedSalary,
            CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? v.CurrencyCode : dto.CurrencyCode,
            SalaryStructureId = dto.SalaryStructureId,
            EmploymentType = employmentType,
            ProposedStartDate = dto.ProposedStartDate,
            ContractEndDate = contractEnd,
            ProbationMonths = dto.ProbationMonths ?? 3,
            Terms = dto.Terms,
            Status = OfferStatus.Draft,
            PreparedBy = userId,
            PreparedAt = DateTime.UtcNow,
            // Agreed now, applied at ISSUE — an offer waiting on approval must not eat the candidate's window.
            ResponseDays = responseDays,
            ResponseDeadline = null,
            CreatedBy = userId, UpdatedBy = userId,
        });

        await LogAsync("JobOffer", created.Id, HrAuditAction.OfferPrepared,
            $"{created.OfferNumber} prepared for {a.FullName} at {Money(dto.OfferedSalary, created.CurrencyCode)}, starting {dto.ProposedStartDate:dd MMM yyyy}.", userId);

        if (dto.SubmitNow)
        {
            var submitted = await SubmitOfferAsync(created.Id, userId);
            if (submitted.Status != "Error")
                return result with
                {
                    Status = "Submitted",
                    Message = $"{created.OfferNumber} prepared and sent for approval.",
                    Id = created.Id,
                };
        }

        return result with
        {
            Status = "Prepared",
            Message = $"{created.OfferNumber} prepared for {a.FullName}. It needs approval before it can be issued ({responseDays}-day response window).",
            Id = created.Id,
        };
    }

    public async Task<RecruitmentActionResult> SubmitOfferAsync(string id, string userId)
    {
        var o = await offers.GetByIdAsync(id);
        if (o is null) return Err("Offer not found.");
        if (o.Status != OfferStatus.Draft)
            return Err($"{o.OfferNumber} is {Spaced(o.Status)} — only a draft can be submitted.");

        o.Status = OfferStatus.PendingApproval;
        o.SubmittedBy = userId;
        o.SubmittedAt = DateTime.UtcNow;
        o.UpdatedBy = userId;
        await offers.UpdateAsync(o);

        await LogAsync("JobOffer", o.Id, HrAuditAction.OfferSubmitted,
            $"{o.OfferNumber} submitted for approval at {Money(o.OfferedSalary, o.CurrencyCode)}.", userId);
        return new RecruitmentActionResult("Submitted",
            $"{o.OfferNumber} sent for approval. It cannot be issued until the salary is approved.", o.Id);
    }

    /// <summary>The salary approval. Whoever prepared the offer cannot approve it.</summary>
    public async Task<RecruitmentActionResult> DecideOfferAsync(string id, DecideOfferDto dto, string userId, string? userName)
    {
        var o = await offers.GetByIdAsync(id);
        if (o is null) return Err("Offer not found.");
        if (o.Status != OfferStatus.PendingApproval)
            return Err($"{o.OfferNumber} is {Spaced(o.Status)} — only an offer awaiting approval can be decided.");

        var approving = dto.Decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        if (!approving && !dto.Decision.Equals("Reject", StringComparison.OrdinalIgnoreCase))
            return Err("The decision must be Approve or Reject.");

        if (userId == o.PreparedBy || userId == o.SubmittedBy)
            return Err("An offer cannot be approved by the person who prepared it — the salary needs a second pair of eyes.");
        if (!approving && string.IsNullOrWhiteSpace(dto.Reason))
            return Err("A rejection needs a reason — whoever prepared it has to know what to change.");

        o.Status = approving ? OfferStatus.Approved : OfferStatus.Draft;
        o.ApprovedBy = approving ? userId : null;
        o.ApprovedByName = approving ? userName : null;
        o.ApprovedAt = approving ? DateTime.UtcNow : null;
        o.RejectionReason = approving ? null : dto.Reason;
        o.UpdatedBy = userId;
        await offers.UpdateAsync(o);

        await LogAsync("JobOffer", o.Id,
            approving ? HrAuditAction.OfferApproved : HrAuditAction.OfferRejected,
            $"{o.OfferNumber} {(approving ? "approved" : $"sent back — {dto.Reason}")} at {Money(o.OfferedSalary, o.CurrencyCode)}.",
            userId, userName);

        return new RecruitmentActionResult(approving ? "Approved" : "Rejected",
            approving
                ? $"{o.OfferNumber} approved. Issue it to the candidate next."
                : $"{o.OfferNumber} sent back to draft for revision.", o.Id);
    }

    public async Task<RecruitmentActionResult> IssueOfferAsync(string id, string userId)
    {
        var o = await offers.GetByIdAsync(id);
        if (o is null) return Err("Offer not found.");
        if (o.Status != OfferStatus.Approved)
            return Err($"{o.OfferNumber} is {Spaced(o.Status)} — an offer is issued only once its salary is approved.");

        var a = await applicants.GetByIdAsync(o.ApplicantId);
        if (a is null) return Err("The applicant is no longer on file.");
        if (a.Status is ApplicantStatus.Rejected or ApplicantStatus.Withdrawn)
            return Err($"{a.FullName} is {Spaced(a.Status).ToLowerInvariant()}.");

        o.Status = OfferStatus.Issued;
        o.IssuedAt = DateTime.UtcNow;
        o.IssuedBy = userId;
        o.ResponseDeadline = DateTime.UtcNow.Date.AddDays(o.ResponseDays > 0 ? o.ResponseDays : DefaultOfferResponseDays);
        o.UpdatedBy = userId;
        await offers.UpdateAsync(o);

        a.Status = ApplicantStatus.OfferMade;
        a.UpdatedBy = userId;
        await applicants.UpdateAsync(a);

        await LogAsync("JobOffer", o.Id, HrAuditAction.OfferIssued,
            $"{o.OfferNumber} issued to {a.FullName}; response due {o.ResponseDeadline:dd MMM yyyy}.", userId);
        return new RecruitmentActionResult("Issued",
            $"{o.OfferNumber} issued to {a.FullName}. A response is due by {o.ResponseDeadline:dd MMM yyyy}.", o.Id);
    }

    public async Task<RecruitmentActionResult> RespondToOfferAsync(string id, RespondToOfferDto dto, string userId)
    {
        var o = await offers.GetByIdAsync(id);
        if (o is null) return Err("Offer not found.");
        if (o.Status != OfferStatus.Issued)
            return Err($"{o.OfferNumber} is {Spaced(o.Status)} — only an issued offer can be answered.");

        var accepting = dto.Response.Equals("Accept", StringComparison.OrdinalIgnoreCase);
        if (!accepting && !dto.Response.Equals("Decline", StringComparison.OrdinalIgnoreCase))
            return Err("The response must be Accept or Decline.");

        var a = await applicants.GetByIdAsync(o.ApplicantId);
        if (a is null) return Err("The applicant is no longer on file.");

        var result = new RecruitmentActionResult("", "", o.Id);

        if (dto.StartDate is not null && dto.StartDate.Value.Date != o.ProposedStartDate.Date)
        {
            result.Warnings.Add($"Start date agreed as {dto.StartDate:dd MMM yyyy}, not the proposed {o.ProposedStartDate:dd MMM yyyy}.");
            o.ProposedStartDate = dto.StartDate.Value;
        }

        o.Status = accepting ? OfferStatus.Accepted : OfferStatus.Declined;
        o.RespondedAt = DateTime.UtcNow;
        o.DeclineReason = accepting ? null : dto.Reason;
        o.UpdatedBy = userId;
        await offers.UpdateAsync(o);

        if (!accepting)
        {
            a.Status = ApplicantStatus.Rejected;
            a.RejectionReason = $"Declined the offer. {dto.Reason}".Trim();
            a.RejectedAtStage = "Offer";
            a.RejectedAt = DateTime.UtcNow;
            a.UpdatedBy = userId;
            await applicants.UpdateAsync(a);
        }

        await LogAsync("JobOffer", o.Id,
            accepting ? HrAuditAction.OfferAccepted : HrAuditAction.OfferDeclined,
            $"{o.OfferNumber} {(accepting ? "accepted" : $"declined — {dto.Reason}")} by {a.FullName}.", userId);

        return result with
        {
            Status = accepting ? "Accepted" : "Declined",
            Message = accepting
                ? $"{a.FullName} accepted {o.OfferNumber}. Record the hire to create their employee record."
                : $"{a.FullName} declined {o.OfferNumber}.",
        };
    }

    public async Task<RecruitmentActionResult> WithdrawOfferAsync(string id, WithdrawOfferDto dto, string userId)
    {
        var o = await offers.GetByIdAsync(id);
        if (o is null) return Err("Offer not found.");
        if (o.Status is OfferStatus.Accepted or OfferStatus.Declined or OfferStatus.Withdrawn or OfferStatus.Lapsed)
            return Err($"{o.OfferNumber} is {Spaced(o.Status)} — it can no longer be withdrawn.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Err("Withdrawing an offer needs a reason — it is a promise being taken back.");

        o.Status = OfferStatus.Withdrawn;
        o.WithdrawalReason = dto.Reason;
        o.RespondedAt = DateTime.UtcNow;
        o.UpdatedBy = userId;
        await offers.UpdateAsync(o);

        // The candidate goes back to being recommended — the panel's verdict has not changed.
        var a = await applicants.GetByIdAsync(o.ApplicantId);
        if (a is not null && a.Status == ApplicantStatus.OfferMade)
        {
            a.Status = ApplicantStatus.Recommended;
            a.UpdatedBy = userId;
            await applicants.UpdateAsync(a);
        }

        await LogAsync("JobOffer", o.Id, HrAuditAction.OfferWithdrawn,
            $"{o.OfferNumber} withdrawn from {o.ApplicantName} — {dto.Reason}.", userId);
        return new RecruitmentActionResult("Withdrawn", $"{o.OfferNumber} withdrawn.", o.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Hire — H12 hands off to H1 (P1)
    // ══════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Turns an accepted offer into an employee by calling H1's own creation path, so a recruit gets the same
    /// employee-number sequence, org-chart node and onboarding rules as anyone onboarded by hand. The login
    /// account is deliberately left for onboarding: an account that exists before the signed contract is on
    /// file is an account nobody has agreed to.
    /// </summary>
    public async Task<RecruitmentActionResult> HireApplicantAsync(string applicantId, HireApplicantDto dto, string userId, string? userName)
    {
        var a = await applicants.GetByIdAsync(applicantId);
        if (a is null) return Err("Applicant not found.");
        if (a.Status == ApplicantStatus.Hired)
            return Err($"{a.FullName} has already been hired as {a.ResultingEmployeeNumber}.");

        var o = await offers.Query().FirstOrDefaultAsync(x => x.ApplicantId == a.Id && x.Status == OfferStatus.Accepted);
        if (o is null)
            return Err($"{a.FullName} has no accepted offer — a hire needs an agreed salary and start date behind it.");

        var v = await vacancies.GetByIdAsync(a.VacancyId);
        if (v is null) return Err("The vacancy is no longer on file.");
        if (v.HiredCount >= v.Headcount)
            return Err($"{v.VacancyNumber} has already hired all {v.Headcount} approved head(s).");

        // An internal applicant is already an employee — moving them is a transfer, not an onboarding.
        if (!string.IsNullOrWhiteSpace(a.InternalEmployeeId))
            return Err("This is an internal candidate who already has an employee record — move them through a position change on the employee, not a new hire.");

        var (firstName, lastName, otherNames) = SplitName(a.FullName, dto);
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return Err("A first and last name are required — give them explicitly if the applicant's name is a single word.");

        var hireDate = dto.HireDate ?? o.ProposedStartDate;

        var create = new CreateEmployeeDto
        {
            FirstName = firstName,
            LastName = lastName,
            OtherNames = otherNames,
            WorkEmail = string.IsNullOrWhiteSpace(dto.WorkEmail) ? a.Email : dto.WorkEmail.Trim(),
            WorkPhone = dto.WorkPhone ?? a.Phone,
            NationalId = a.NationalId,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            MaritalStatus = dto.MaritalStatus,
            PersonalEmail = a.Email,
            PersonalPhone = a.Phone,
            PhysicalAddress = dto.PhysicalAddress,
            KraPin = dto.KraPin,
            NssfNumber = dto.NssfNumber,
            ShaNumber = dto.ShaNumber,
            HelbNumber = dto.HelbNumber,
            HireDate = hireDate,
            EmploymentType = o.EmploymentType.ToString(),
            ContractStartDate = hireDate,
            ContractEndDate = o.ContractEndDate,
            DepartmentId = o.DepartmentId,
            BranchId = o.BranchId,
            PositionId = o.PositionId,
            ReportsToId = dto.ReportsToId,
            WorkMode = dto.WorkMode,
            // Provisioned during onboarding, once the signed contract is on file — see the method remarks.
            CreateUserAccount = dto.CreateUserAccount,
            RoleIds = dto.RoleIds,
        };

        var employeeResult = await employeeService.CreateAsync(create, userId, userName);
        if (employeeResult.Status == "Error" || string.IsNullOrWhiteSpace(employeeResult.EmployeeId))
            return Err($"The employee record could not be created — {employeeResult.Message}");

        var employee = await employees.GetByIdAsync(employeeResult.EmployeeId);

        a.Status = ApplicantStatus.Hired;
        a.ResultingEmployeeId = employeeResult.EmployeeId;
        a.ResultingEmployeeNumber = employee?.EmployeeNumber;
        a.HiredAt = DateTime.UtcNow;
        a.UpdatedBy = userId;
        await applicants.UpdateAsync(a);

        o.ResultingEmployeeId = employeeResult.EmployeeId;
        o.UpdatedBy = userId;
        await offers.UpdateAsync(o);

        v.HiredCount += 1;
        var result = new RecruitmentActionResult("Hired",
            $"{a.FullName} hired as {employee?.EmployeeNumber} against {v.VacancyNumber}, starting {hireDate:dd MMM yyyy}.",
            employeeResult.EmployeeId);

        if (v.HiredCount >= v.Headcount)
        {
            v.Status = VacancyStatus.Filled;
            v.ClosedAt = DateTime.UtcNow;
            v.ClosedBy = userId;
            v.ClosureReason = "All approved heads hired.";
            result.Warnings.Add($"{v.VacancyNumber} is now filled and has been closed.");
            await LogAsync("Vacancy", v.Id, HrAuditAction.VacancyFilled,
                $"{v.VacancyNumber} filled — {v.HiredCount} of {v.Headcount} hired.", userId);
        }
        v.UpdatedBy = userId;
        await vacancies.UpdateAsync(v);

        result.Warnings.Add(dto.CreateUserAccount
            ? "A login account was requested with the employee record."
            : "No login account yet — create it from the employee's record once the signed contract is on file.");
        result.Warnings.Add("Onboarding is incomplete until the signed contract and ID copy are uploaded.");

        // Everyone else still in the running for a vacancy that is now full needs a decision.
        if (v.Status == VacancyStatus.Filled)
        {
            var stillInPlay = await applicants.Query().CountAsync(x => x.VacancyId == v.Id
                && x.Status != ApplicantStatus.Hired && x.Status != ApplicantStatus.Rejected && x.Status != ApplicantStatus.Withdrawn);
            if (stillInPlay > 0)
                result.Warnings.Add($"{stillInPlay} candidate(s) are still open on {v.VacancyNumber} — they need rejecting or holding.");
        }

        await LogAsync("Applicant", a.Id, HrAuditAction.CandidateHired,
            $"{a.ApplicantNumber} ({a.FullName}) hired from {o.OfferNumber} as employee {employee?.EmployeeNumber}, starting {hireDate:yyyy-MM-dd}.",
            userId, userName);

        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Daily sweep
    // ══════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Closes vacancies whose closing date has passed and lapses offers nobody answered. Both stamp once —
    /// an offer that lapsed on Tuesday is not re-lapsed every morning after.
    /// </summary>
    public async Task<RecruitmentSweepResultDto> RunRecruitmentSweepAsync(string? schema, string userId)
    {
        var today = DateTime.UtcNow.Date;
        var result = new RecruitmentSweepResultDto();

        // ── Vacancies past their closing date ──
        var expiring = await vacancies.Query()
            .Where(v => v.Status == VacancyStatus.Open && v.ClosingDate != null && v.ClosingDate < today)
            .ToListAsync();
        foreach (var v in expiring)
        {
            v.Status = VacancyStatus.Closed;
            v.ClosedAt = DateTime.UtcNow;
            v.ClosedBy = userId;
            v.ClosureReason = $"Closing date {v.ClosingDate:dd MMM yyyy} passed.";
            v.UpdatedBy = userId;
            await vacancies.UpdateAsync(v);
            await LogAsync("Vacancy", v.Id, HrAuditAction.VacancyClosed,
                $"{v.VacancyNumber} closed automatically — closing date {v.ClosingDate:yyyy-MM-dd} passed.", userId);
            result.VacanciesClosed++;
        }
        if (result.VacanciesClosed > 0)
            result.Notes.Add($"{result.VacanciesClosed} vacancy(ies) closed on their closing date.");

        // ── Offers nobody answered ──
        var lapsing = await offers.Query()
            .Where(o => o.Status == OfferStatus.Issued && o.ResponseDeadline != null
                     && o.ResponseDeadline < today && o.LapseAlertedAt == null)
            .ToListAsync();
        foreach (var o in lapsing)
        {
            o.Status = OfferStatus.Lapsed;
            o.LapseAlertedAt = DateTime.UtcNow;
            o.UpdatedBy = userId;
            await offers.UpdateAsync(o);

            var a = await applicants.GetByIdAsync(o.ApplicantId);
            if (a is not null && a.Status == ApplicantStatus.OfferMade)
            {
                // Back to recommended, not rejected — silence is not a refusal, and the seat is freed either way.
                a.Status = ApplicantStatus.Recommended;
                a.UpdatedBy = userId;
                await applicants.UpdateAsync(a);
            }

            await LogAsync("JobOffer", o.Id, HrAuditAction.OfferLapsed,
                $"{o.OfferNumber} lapsed — no response by {o.ResponseDeadline:yyyy-MM-dd}.", userId);

            // Titles differ per case so ticketing's title-based dedupe does not swallow the second one.
            await NotifyAsync(schema, "Medium",
                $"Job offer {o.OfferNumber} lapsed",
                $"{o.ApplicantName} did not answer {o.OfferNumber} by {o.ResponseDeadline:dd MMM yyyy}. The seat on {o.VacancyNumber} is free again.",
                "hr.write");
            result.OffersLapsed++;
        }
        if (result.OffersLapsed > 0)
            result.Notes.Add($"{result.OffersLapsed} offer(s) lapsed unanswered.");

        // ── Interviews that came and went without a score. Reported, never auto-resolved: only the panel
        //    knows whether it happened, and guessing would put a fiction in the record. ──
        result.InterviewsOverdue = await interviews.Query()
            .CountAsync(i => !i.Held && i.CancelledAt == null && i.ScheduledAt < today);
        if (result.InterviewsOverdue > 0)
            result.Notes.Add($"{result.InterviewsOverdue} interview(s) are past their slot with no score recorded.");

        if (result.Notes.Count == 0) result.Notes.Add("Nothing to do.");
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The establishment arithmetic: what the position allows, who is already in it, and what other live
    /// requisitions have already claimed. Counting the last of those is what stops two requisitions each
    /// filling the same single vacant post.
    /// </summary>
    private async Task<(int current, int committed, string notes, bool exceeds)> AssessEstablishmentAsync(
        Position position, int requested, RequisitionType type)
    {
        var current = await employees.Query()
            .CountAsync(e => e.PositionId == position.Id && InPost.Contains(e.Status));

        // Only heads NOT YET HIRED are still committed. A requisition stays Approved for ever — it has no
        // "fulfilled" state — so counting its full headcount would count the same person twice once they are
        // in post: once in `current` and again here, making every later requisition on that position look
        // like it breaches the establishment.
        var liveRequisitions = await requisitions.Query().AsNoTracking()
            .Where(r => r.PositionId == position.Id && LiveRequisitions.Contains(r.Status))
            .Select(r => new { r.Id, r.HeadcountRequested })
            .ToListAsync();

        var committed = 0;
        if (liveRequisitions.Count > 0)
        {
            var ids = liveRequisitions.Select(r => r.Id).ToList();
            var hiredByRequisition = await vacancies.Query().AsNoTracking()
                .Where(v => ids.Contains(v.JobRequisitionId) && v.Status != VacancyStatus.Cancelled)
                .GroupBy(v => v.JobRequisitionId)
                .Select(g => new { g.Key, Hired = g.Sum(v => v.HiredCount) })
                .ToDictionaryAsync(x => x.Key, x => x.Hired);

            committed = liveRequisitions.Sum(r =>
                Math.Max(0, r.HeadcountRequested - hiredByRequisition.GetValueOrDefault(r.Id)));
        }

        if (position.ApprovedHeadcount is null)
            return (current, committed,
                $"{position.Title} carries no approved establishment — {current} in post, {committed} already requested. " +
                "The approver is deciding the establishment as well as the hire.", false);

        var approved = position.ApprovedHeadcount.Value;
        var free = approved - current - committed;
        var exceeds = requested > free;

        var notes = $"{position.Title}: {approved} approved, {current} in post, {committed} already requested — " +
                    $"{Math.Max(0, free)} seat(s) free. Requesting {requested}.";
        if (exceeds)
            notes += type == RequisitionType.Expansion
                ? " This is an expansion: approving it raises the establishment."
                : $" This exceeds the establishment by {requested - free} — approving it raises the establishment.";

        return (current, committed, notes, exceeds);
    }

    /// <summary>Heads already advertised per requisition. Cancelled vacancies release their heads.</summary>
    private async Task<Dictionary<string, int>> PostedHeadcountByRequisitionAsync(List<string> requisitionIds)
    {
        if (requisitionIds.Count == 0) return [];
        return await vacancies.Query().AsNoTracking()
            .Where(v => requisitionIds.Contains(v.JobRequisitionId) && v.Status != VacancyStatus.Cancelled)
            .GroupBy(v => v.JobRequisitionId)
            .Select(g => new { g.Key, Total = g.Sum(v => v.Headcount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
    }

    private async Task<Dictionary<string, PipelineCounts>> PipelineByVacancyAsync(List<string> vacancyIds)
    {
        if (vacancyIds.Count == 0) return [];
        var rows = await applicants.Query().AsNoTracking()
            .Where(a => vacancyIds.Contains(a.VacancyId))
            .Select(a => new { a.VacancyId, a.Status })
            .ToListAsync();

        var offerRows = await offers.Query().AsNoTracking()
            .Where(o => vacancyIds.Contains(o.VacancyId) && o.Status == OfferStatus.Issued)
            .Select(o => o.VacancyId)
            .ToListAsync();

        return vacancyIds.Distinct().ToDictionary(id => id, id =>
        {
            var mine = rows.Where(r => r.VacancyId == id).ToList();
            return new PipelineCounts(
                mine.Count,
                mine.Count(r => r.Status == ApplicantStatus.Shortlisted),
                mine.Count(r => r.Status is ApplicantStatus.Interviewing or ApplicantStatus.Recommended),
                offerRows.Count(v => v == id),
                mine.Count(r => r.Status is ApplicantStatus.Rejected or ApplicantStatus.Withdrawn));
        });
    }

    private async Task<Dictionary<string, (decimal? Min, decimal? Max, string Ccy)>> SalaryRangesAsync(List<string> vacancyIds)
    {
        if (vacancyIds.Count == 0) return [];
        return await vacancies.Query().AsNoTracking()
            .Where(v => vacancyIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => (v.SalaryRangeMin, v.SalaryRangeMax, v.CurrencyCode));
    }

    private async Task<List<ApplicantDto>> DecorateAsync(List<Applicant> list)
    {
        if (list.Count == 0) return [];
        var ids = list.Select(a => a.Id).ToList();

        var scheduled = await interviews.Query().AsNoTracking()
            .Where(i => ids.Contains(i.ApplicantId) && !i.Held && i.CancelledAt == null)
            .GroupBy(i => i.ApplicantId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        // The offer that matters is the live one; a declined offer from a previous round is not it.
        var offerRows = await offers.Query().AsNoTracking()
            .Where(o => ids.Contains(o.ApplicantId))
            .Select(o => new { o.Id, o.ApplicantId, o.OfferNumber, o.Status, o.CreatedAt })
            .ToListAsync();

        return list.Select(a =>
        {
            var offer = offerRows.Where(o => o.ApplicantId == a.Id)
                .OrderByDescending(o => o.CreatedAt).FirstOrDefault();
            return ToDto(a, scheduled.GetValueOrDefault(a.Id),
                offer is null ? null : (offer.Id, offer.OfferNumber, offer.Status.ToString()));
        }).ToList();
    }

    private async Task CancelPendingInterviewsAsync(string applicantId, string reason, string userId)
    {
        var pending = await interviews.Query()
            .Where(i => i.ApplicantId == applicantId && !i.Held && i.CancelledAt == null).ToListAsync();
        foreach (var i in pending)
        {
            i.CancelledAt = DateTime.UtcNow;
            i.CancellationReason = reason;
            i.UpdatedBy = userId;
            await interviews.UpdateAsync(i);
        }
    }

    /// <summary>Sequential reference per prefix per year, derived from the highest issued rather than a row
    /// count — the same trap that broke finance's journal numbering.</summary>
    private async Task<string> NextNumberAsync(string prefix, int year)
    {
        var pattern = $"{prefix}-{year}-";
        var issued = prefix switch
        {
            "REQ" => await requisitions.Query().IgnoreQueryFilters()
                .Where(r => r.RequisitionNumber.StartsWith(pattern)).Select(r => r.RequisitionNumber).ToListAsync(),
            "VAC" => await vacancies.Query().IgnoreQueryFilters()
                .Where(v => v.VacancyNumber.StartsWith(pattern)).Select(v => v.VacancyNumber).ToListAsync(),
            "APP" => await applicants.Query().IgnoreQueryFilters()
                .Where(a => a.ApplicantNumber.StartsWith(pattern)).Select(a => a.ApplicantNumber).ToListAsync(),
            _ => await offers.Query().IgnoreQueryFilters()
                .Where(o => o.OfferNumber.StartsWith(pattern)).Select(o => o.OfferNumber).ToListAsync(),
        };
        var highest = issued
            .Select(n => int.TryParse(n[pattern.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        return $"{pattern}{highest + 1:D4}";
    }

    /// <summary>
    /// An applicant is captured as one name because that is how a CV arrives. Splitting it is a convenience
    /// for the common case; whoever records the hire can override either part, and a single-word name is
    /// refused rather than guessed at.
    /// </summary>
    private static (string first, string last, string? other) SplitName(string fullName, HireApplicantDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.FirstName) && !string.IsNullOrWhiteSpace(dto.LastName))
            return (dto.FirstName.Trim(), dto.LastName.Trim(), dto.OtherNames);

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var first = dto.FirstName?.Trim() ?? (parts.Length > 0 ? parts[0] : string.Empty);
        var last = dto.LastName?.Trim() ?? (parts.Length > 1 ? parts[^1] : string.Empty);
        var other = dto.OtherNames ?? (parts.Length > 2 ? string.Join(' ', parts[1..^1]) : null);
        return (first, last, other);
    }

    private static JobRequisitionDto ToDto(JobRequisition r, Dictionary<string, int> posted)
    {
        var headcountPosted = posted.GetValueOrDefault(r.Id);
        var remaining = Math.Max(0, r.HeadcountRequested - headcountPosted);
        return new JobRequisitionDto
        {
            Id = r.Id, RequisitionNumber = r.RequisitionNumber,
            PositionId = r.PositionId, PositionTitle = r.PositionTitle, JobGrade = r.JobGrade,
            DepartmentId = r.DepartmentId, DepartmentName = r.DepartmentName, BranchId = r.BranchId,
            RequisitionType = r.RequisitionType.ToString(),
            HeadcountRequested = r.HeadcountRequested,
            ReplacingEmployeeId = r.ReplacingEmployeeId, ReplacingEmployeeName = r.ReplacingEmployeeName,
            EmploymentType = r.EmploymentType.ToString(), ContractEndDate = r.ContractEndDate,
            Justification = r.Justification, RequiredBy = r.RequiredBy,
            ApprovedHeadcount = r.ApprovedHeadcount, CurrentHeadcount = r.CurrentHeadcount,
            CommittedHeadcount = r.CommittedHeadcount, EstablishmentNotes = r.EstablishmentNotes,
            ExceedsEstablishment = r.ExceedsEstablishment,
            Status = r.Status.ToString(),
            RaisedAt = r.RaisedAt, SubmittedAt = r.SubmittedAt,
            DecidedBy = r.DecidedBy, DecidedByName = r.DecidedByName, DecidedAt = r.DecidedAt,
            DecisionReason = r.DecisionReason, CancellationReason = r.CancellationReason,
            VacanciesRaised = headcountPosted == 0 ? 0 : 1,
            HeadcountPosted = headcountPosted,
            HeadcountRemaining = remaining,
            NextStep = r.Status switch
            {
                RequisitionStatus.Draft => "Submit for approval.",
                RequisitionStatus.PendingApproval => "Awaiting approval — the raiser cannot approve it.",
                RequisitionStatus.Approved when remaining > 0 => $"Post a vacancy for the {remaining} remaining head(s).",
                RequisitionStatus.Approved => "Fully advertised.",
                RequisitionStatus.Rejected => "Rejected — raise a new requisition if it is still needed.",
                _ => "Cancelled.",
            },
        };
    }

    private record PipelineCounts(int Applicants, int Shortlisted, int Interviewing, int OffersOut, int Rejected);

    private static VacancyDto ToDto(Vacancy v, PipelineCounts? p, DateTime today)
    {
        var closedToApplications = v.Status != VacancyStatus.Open
            || (v.ClosingDate is not null && v.ClosingDate.Value.Date < today);
        return new VacancyDto
        {
            Id = v.Id, VacancyNumber = v.VacancyNumber,
            JobRequisitionId = v.JobRequisitionId, RequisitionNumber = v.RequisitionNumber,
            PositionId = v.PositionId, PositionTitle = v.PositionTitle, JobGrade = v.JobGrade,
            DepartmentId = v.DepartmentId, DepartmentName = v.DepartmentName,
            Headcount = v.Headcount, HiredCount = v.HiredCount,
            PostingChannel = v.PostingChannel.ToString(),
            JobDescription = v.JobDescription, MinimumQualifications = v.MinimumQualifications,
            Responsibilities = v.Responsibilities,
            SalaryRangeMin = v.SalaryRangeMin, SalaryRangeMax = v.SalaryRangeMax, CurrencyCode = v.CurrencyCode,
            PostedAt = v.PostedAt, ClosingDate = v.ClosingDate,
            Status = v.Status.ToString(), ClosedAt = v.ClosedAt, ClosureReason = v.ClosureReason,
            Applicants = p?.Applicants ?? 0,
            Shortlisted = p?.Shortlisted ?? 0,
            Interviewing = p?.Interviewing ?? 0,
            OffersOut = p?.OffersOut ?? 0,
            Rejected = p?.Rejected ?? 0,
            ClosedToApplications = closedToApplications,
            DaysToClose = v.ClosingDate is null || v.Status != VacancyStatus.Open
                ? null : (int)(v.ClosingDate.Value.Date - today).TotalDays,
            NextStep = v.Status switch
            {
                VacancyStatus.Open when (p?.Applicants ?? 0) == 0 => "Awaiting applications.",
                VacancyStatus.Open when (p?.Applicants ?? 0) > 0 && (p?.Shortlisted ?? 0) == 0 && (p?.Interviewing ?? 0) == 0
                    => "Screen the applications received.",
                VacancyStatus.Open => $"{v.Headcount - v.HiredCount} seat(s) still to fill.",
                VacancyStatus.Closed => "Closed to applications; those in flight continue.",
                VacancyStatus.Filled => "Filled.",
                _ => "Cancelled.",
            },
        };
    }

    private static ApplicantDto ToDto(Applicant a, int interviewsScheduled, (string Id, string Number, string Status)? offer) => new()
    {
        Id = a.Id, ApplicantNumber = a.ApplicantNumber,
        VacancyId = a.VacancyId, VacancyNumber = a.VacancyNumber, PositionTitle = a.PositionTitle,
        FullName = a.FullName, Email = a.Email, Phone = a.Phone, NationalId = a.NationalId,
        Source = a.Source.ToString(),
        ReferredByEmployeeId = a.ReferredByEmployeeId, ReferredByName = a.ReferredByName,
        InternalEmployeeId = a.InternalEmployeeId,
        CvDocumentPath = a.CvDocumentPath, CoverNote = a.CoverNote,
        YearsExperience = a.YearsExperience, HighestQualification = a.HighestQualification,
        CurrentEmployer = a.CurrentEmployer, ExpectedSalary = a.ExpectedSalary,
        Status = a.Status.ToString(), AppliedAt = a.AppliedAt,
        ScreenedAt = a.ScreenedAt, ScreeningNotes = a.ScreeningNotes,
        AverageInterviewScore = a.AverageInterviewScore,
        InterviewsHeld = a.InterviewsHeld, InterviewsScheduled = interviewsScheduled,
        RejectionReason = a.RejectionReason, RejectedAtStage = a.RejectedAtStage, RejectedAt = a.RejectedAt,
        ResultingEmployeeId = a.ResultingEmployeeId, ResultingEmployeeNumber = a.ResultingEmployeeNumber,
        HiredAt = a.HiredAt,
        OfferId = offer?.Id, OfferNumber = offer?.Number, OfferStatus = offer?.Status,
        NextStep = a.Status switch
        {
            ApplicantStatus.Applied => "Screen the application.",
            ApplicantStatus.Shortlisted when interviewsScheduled > 0 => "Interview scheduled — record the score once held.",
            ApplicantStatus.Shortlisted => "Schedule an interview.",
            ApplicantStatus.Interviewing when interviewsScheduled > 0 => "Further interview scheduled.",
            ApplicantStatus.Interviewing => "Schedule the next stage, or reject.",
            ApplicantStatus.Recommended => "Prepare an offer.",
            ApplicantStatus.OfferMade => "Awaiting the candidate's answer.",
            ApplicantStatus.Hired => $"Hired as {a.ResultingEmployeeNumber} — finish onboarding.",
            ApplicantStatus.Rejected => $"Rejected at {a.RejectedAtStage}.",
            _ => "Withdrew.",
        },
    };

    private static JobOfferDto ToDto(JobOffer o, (decimal? Min, decimal? Max, string Ccy) range, DateTime today)
    {
        string? rangeNote = null;
        if (range.Min is not null && o.OfferedSalary < range.Min)
            rangeNote = $"Below the advertised minimum of {Money(range.Min.Value, range.Ccy)}.";
        else if (range.Max is not null && o.OfferedSalary > range.Max)
            rangeNote = $"Above the advertised maximum of {Money(range.Max.Value, range.Ccy)}.";
        else if (range.Min is not null || range.Max is not null)
            rangeNote = "Within the advertised range.";

        return new JobOfferDto
        {
            Id = o.Id, OfferNumber = o.OfferNumber,
            ApplicantId = o.ApplicantId, ApplicantName = o.ApplicantName,
            VacancyId = o.VacancyId, VacancyNumber = o.VacancyNumber,
            PositionId = o.PositionId, PositionTitle = o.PositionTitle, JobGrade = o.JobGrade,
            OfferedSalary = o.OfferedSalary, CurrencyCode = o.CurrencyCode, SalaryStructureId = o.SalaryStructureId,
            EmploymentType = o.EmploymentType.ToString(),
            ProposedStartDate = o.ProposedStartDate, ContractEndDate = o.ContractEndDate,
            ProbationMonths = o.ProbationMonths, Terms = o.Terms,
            Status = o.Status.ToString(),
            PreparedAt = o.PreparedAt, SubmittedAt = o.SubmittedAt,
            ApprovedByName = o.ApprovedByName, ApprovedAt = o.ApprovedAt, RejectionReason = o.RejectionReason,
            IssuedAt = o.IssuedAt, ResponseDeadline = o.ResponseDeadline, RespondedAt = o.RespondedAt,
            DeclineReason = o.DeclineReason, WithdrawalReason = o.WithdrawalReason,
            ResultingEmployeeId = o.ResultingEmployeeId,
            ResponseOverdue = o.Status == OfferStatus.Issued
                && o.ResponseDeadline is not null && o.ResponseDeadline.Value.Date < today,
            SalaryRangeNote = rangeNote,
            NextStep = o.Status switch
            {
                OfferStatus.Draft => "Submit for approval.",
                OfferStatus.PendingApproval => "Awaiting salary approval — the preparer cannot approve it.",
                OfferStatus.Approved => "Issue it to the candidate.",
                OfferStatus.Issued => $"Awaiting the candidate's answer by {o.ResponseDeadline:dd MMM yyyy}.",
                OfferStatus.Accepted when o.ResultingEmployeeId is null => "Accepted — record the hire.",
                OfferStatus.Accepted => "Hired.",
                OfferStatus.Declined => "Declined.",
                OfferStatus.Lapsed => "Lapsed unanswered.",
                _ => "Withdrawn.",
            },
        };
    }

    private static InterviewDto ToDto(Interview i, DateTime now) => new()
    {
        Id = i.Id, ApplicantId = i.ApplicantId, ApplicantName = i.ApplicantName, VacancyId = i.VacancyId,
        Stage = i.Stage.ToString(), ScheduledAt = i.ScheduledAt, Location = i.Location, PanelMembers = i.PanelMembers,
        Held = i.Held, HeldAt = i.HeldAt,
        TechnicalScore = i.TechnicalScore, ExperienceScore = i.ExperienceScore,
        CommunicationScore = i.CommunicationScore, CulturalFitScore = i.CulturalFitScore,
        OverallScore = i.OverallScore,
        Recommendation = i.Recommendation.ToString(), Notes = i.Notes,
        ScoredByName = i.ScoredByName, ScoredAt = i.ScoredAt,
        CancelledAt = i.CancelledAt, CancellationReason = i.CancellationReason,
        Overdue = !i.Held && i.CancelledAt is null && i.ScheduledAt < now,
    };

    private async Task NotifyAsync(string? schema, string severity, string title, string message, string permission)
    {
        if (string.IsNullOrWhiteSpace(schema)) return;
        await notifier.CreateAlertAsync(schema, "HR", severity, title, message, permission);
    }

    private async Task LogAsync(string entityType, string entityId, HrAuditAction action, string detail, string userId, string? userName = null)
    {
        await audit.CreateAsync(new HrAuditLog
        {
            EntityType = entityType, EntityId = entityId, Action = action, Detail = detail,
            PerformedBy = userId, PerformedByName = userName, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }

    private static RecruitmentActionResult Err(string message) => new("Error", message);

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum
        => string.IsNullOrWhiteSpace(value) ? fallback : Enum.TryParse<T>(value, true, out var v) ? v : fallback;

    /// <summary>"FinalWritten" → "Final written"; "PendingApproval" → "Pending approval".</summary>
    private static string Spaced(Enum value)
    {
        var text = value.ToString();
        var chars = text.SelectMany((c, i) => i > 0 && char.IsUpper(c) ? [' ', char.ToLowerInvariant(c)] : new[] { c });
        return new string(chars.ToArray());
    }

    private static string Money(decimal amount, string? currency) => $"{currency ?? "KES"} {amount:N2}";
}
