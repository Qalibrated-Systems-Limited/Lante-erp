using HrService.Core.DTOs.Recruitment;

namespace HrService.Core.Interfaces.Services;

/// <summary>
/// H12 — recruitment, from the request to fill a post through to the employee record the hire creates.
/// </summary>
public interface IRecruitmentService
{
    Task<RecruitmentSummaryDto> GetSummaryAsync();
    Task<List<SourceEffectivenessDto>> GetSourceEffectivenessAsync(int? year);

    // ── Requisitions ──
    Task<List<JobRequisitionDto>> ListRequisitionsAsync(string? status, string? departmentId);
    Task<JobRequisitionDto?> GetRequisitionAsync(string id);
    Task<RecruitmentActionResult> RaiseRequisitionAsync(RaiseRequisitionDto dto, string userId);
    Task<RecruitmentActionResult> SubmitRequisitionAsync(string id, string userId);
    Task<RecruitmentActionResult> DecideRequisitionAsync(string id, DecideRequisitionDto dto, string userId, string? userName);
    Task<RecruitmentActionResult> CancelRequisitionAsync(string id, string? reason, string userId);

    // ── Vacancies ──
    Task<List<VacancyDto>> ListVacanciesAsync(string? status, string? departmentId);
    Task<VacancyDto?> GetVacancyAsync(string id);
    Task<RecruitmentActionResult> PostVacancyAsync(PostVacancyDto dto, string userId);
    Task<RecruitmentActionResult> CloseVacancyAsync(string id, CloseVacancyDto dto, string userId);

    // ── Applicants ──
    Task<List<ApplicantDto>> ListApplicantsAsync(string? vacancyId, string? status);
    Task<ApplicantDto?> GetApplicantAsync(string id);
    Task<RecruitmentActionResult> ReceiveApplicationAsync(ReceiveApplicationDto dto, string userId);
    Task<RecruitmentActionResult> ScreenApplicantAsync(string id, ScreenApplicantDto dto, string userId);
    Task<RecruitmentActionResult> RejectApplicantAsync(string id, RejectApplicantDto dto, string userId);
    Task<RecruitmentActionResult> WithdrawApplicantAsync(string id, string? reason, string userId);

    // ── Interviews ──
    Task<List<InterviewDto>> ListInterviewsAsync(string? applicantId, string? vacancyId, bool? pending);
    Task<RecruitmentActionResult> ScheduleInterviewAsync(ScheduleInterviewDto dto, string userId);
    Task<RecruitmentActionResult> ScoreInterviewAsync(string id, ScoreInterviewDto dto, string userId, string? userName);
    Task<RecruitmentActionResult> CancelInterviewAsync(string id, string? reason, string userId);

    // ── Offers ──
    Task<List<JobOfferDto>> ListOffersAsync(string? status, string? vacancyId);
    Task<JobOfferDto?> GetOfferAsync(string id);
    Task<RecruitmentActionResult> PrepareOfferAsync(PrepareOfferDto dto, string userId);
    Task<RecruitmentActionResult> SubmitOfferAsync(string id, string userId);
    Task<RecruitmentActionResult> DecideOfferAsync(string id, DecideOfferDto dto, string userId, string? userName);
    Task<RecruitmentActionResult> IssueOfferAsync(string id, string userId);
    Task<RecruitmentActionResult> RespondToOfferAsync(string id, RespondToOfferDto dto, string userId);
    Task<RecruitmentActionResult> WithdrawOfferAsync(string id, WithdrawOfferDto dto, string userId);

    /// <summary>Turns an accepted offer into an H1 employee record (H12 → P1).</summary>
    Task<RecruitmentActionResult> HireApplicantAsync(string applicantId, HireApplicantDto dto, string userId, string? userName);

    /// <summary>
    /// Closes vacancies past their closing date and lapses offers past their response deadline. The schema is
    /// passed explicitly because this runs from the daily worker, which has no HttpContext to read it from.
    /// </summary>
    Task<RecruitmentSweepResultDto> RunRecruitmentSweepAsync(string? schema, string userId);
}
