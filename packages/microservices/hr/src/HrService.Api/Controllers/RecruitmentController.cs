using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Recruitment;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>
/// H12 — recruitment: requisition → approval → vacancy → applicants → interviews → offer → hire.
/// <para>Running the pipeline is <c>hr.write</c>. The two decisions that commit money — approving a
/// requisition and approving the salary on an offer — need <c>hr.approve</c>, and the service additionally
/// refuses to let the person who raised or prepared either one approve it.</para>
/// <para>Recording the hire needs <c>hr.write</c> because it creates an employee, which is H1's own bar.
/// Offered salaries are visible at <c>hr.read.dept</c> rather than behind the payroll tier: an offer is not
/// yet anybody's pay, and the hiring manager reading the pipeline has to see what was offered.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class RecruitmentController(IRecruitmentService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");
    private string? Schema => User.FindFirstValue("schema");

    [HttpGet("recruitment/summary")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    /// <summary>Which sourcing channels actually produce hires, not just applications.</summary>
    [HttpGet("recruitment/source-effectiveness")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> SourceEffectiveness([FromQuery] int? year)
        => Ok(new { data = await service.GetSourceEffectivenessAsync(year) });

    // ── Requisitions ──
    [HttpGet("recruitment/requisitions")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListRequisitions([FromQuery] string? status, [FromQuery] string? departmentId)
        => Ok(new { data = await service.ListRequisitionsAsync(status, departmentId) });

    [HttpGet("recruitment/requisitions/{id}")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> GetRequisition(string id)
    {
        var r = await service.GetRequisitionAsync(id);
        return r is null ? NotFound(new { message = "Requisition not found." }) : Ok(new { data = r });
    }

    [HttpPost("recruitment/requisitions")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> RaiseRequisition([FromBody] RaiseRequisitionDto dto)
        => Act(await service.RaiseRequisitionAsync(dto, UserId));

    [HttpPost("recruitment/requisitions/{id}/submit")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> SubmitRequisition(string id)
        => Act(await service.SubmitRequisitionAsync(id, UserId));

    /// <summary>The approval that authorises the spend. Whoever raised it cannot approve it.</summary>
    [HttpPost("recruitment/requisitions/{id}/decide")]
    [Authorize(Policy = "Permission:hr.approve")]
    public async Task<IActionResult> DecideRequisition(string id, [FromBody] DecideRequisitionDto dto)
        => Act(await service.DecideRequisitionAsync(id, dto, UserId, UserName));

    [HttpPost("recruitment/requisitions/{id}/cancel")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CancelRequisition(string id, [FromBody] RejectApplicantDto? dto)
        => Act(await service.CancelRequisitionAsync(id, dto?.Reason, UserId));

    // ── Vacancies ──
    [HttpGet("recruitment/vacancies")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListVacancies([FromQuery] string? status, [FromQuery] string? departmentId)
        => Ok(new { data = await service.ListVacanciesAsync(status, departmentId) });

    [HttpGet("recruitment/vacancies/{id}")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> GetVacancy(string id)
    {
        var v = await service.GetVacancyAsync(id);
        return v is null ? NotFound(new { message = "Vacancy not found." }) : Ok(new { data = v });
    }

    [HttpPost("recruitment/vacancies")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> PostVacancy([FromBody] PostVacancyDto dto)
        => Act(await service.PostVacancyAsync(dto, UserId));

    [HttpPost("recruitment/vacancies/{id}/close")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CloseVacancy(string id, [FromBody] CloseVacancyDto? dto)
        => Act(await service.CloseVacancyAsync(id, dto ?? new CloseVacancyDto(), UserId));

    // ── Applicants ──
    [HttpGet("recruitment/applicants")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListApplicants([FromQuery] string? vacancyId, [FromQuery] string? status)
        => Ok(new { data = await service.ListApplicantsAsync(vacancyId, status) });

    [HttpGet("recruitment/applicants/{id}")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> GetApplicant(string id)
    {
        var a = await service.GetApplicantAsync(id);
        return a is null ? NotFound(new { message = "Applicant not found." }) : Ok(new { data = a });
    }

    [HttpPost("recruitment/applicants")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> ReceiveApplication([FromBody] ReceiveApplicationDto dto)
        => Act(await service.ReceiveApplicationAsync(dto, UserId));

    /// <summary>Screening — the gate an interview stands behind.</summary>
    [HttpPost("recruitment/applicants/{id}/screen")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> ScreenApplicant(string id, [FromBody] ScreenApplicantDto dto)
        => Act(await service.ScreenApplicantAsync(id, dto, UserId));

    [HttpPost("recruitment/applicants/{id}/reject")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> RejectApplicant(string id, [FromBody] RejectApplicantDto dto)
        => Act(await service.RejectApplicantAsync(id, dto, UserId));

    [HttpPost("recruitment/applicants/{id}/withdraw")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> WithdrawApplicant(string id, [FromBody] RejectApplicantDto? dto)
        => Act(await service.WithdrawApplicantAsync(id, dto?.Reason, UserId));

    /// <summary>Creates the H1 employee record from an accepted offer (H12 → P1).</summary>
    [HttpPost("recruitment/applicants/{id}/hire")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> HireApplicant(string id, [FromBody] HireApplicantDto? dto)
        => Act(await service.HireApplicantAsync(id, dto ?? new HireApplicantDto(), UserId, UserName));

    // ── Interviews ──
    [HttpGet("recruitment/interviews")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListInterviews([FromQuery] string? applicantId, [FromQuery] string? vacancyId, [FromQuery] bool? pending)
        => Ok(new { data = await service.ListInterviewsAsync(applicantId, vacancyId, pending) });

    [HttpPost("recruitment/interviews")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> ScheduleInterview([FromBody] ScheduleInterviewDto dto)
        => Act(await service.ScheduleInterviewAsync(dto, UserId));

    [HttpPost("recruitment/interviews/{id}/score")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> ScoreInterview(string id, [FromBody] ScoreInterviewDto dto)
        => Act(await service.ScoreInterviewAsync(id, dto, UserId, UserName));

    [HttpPost("recruitment/interviews/{id}/cancel")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CancelInterview(string id, [FromBody] RejectApplicantDto? dto)
        => Act(await service.CancelInterviewAsync(id, dto?.Reason, UserId));

    // ── Offers ──
    [HttpGet("recruitment/offers")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListOffers([FromQuery] string? status, [FromQuery] string? vacancyId)
        => Ok(new { data = await service.ListOffersAsync(status, vacancyId) });

    [HttpGet("recruitment/offers/{id}")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> GetOffer(string id)
    {
        var o = await service.GetOfferAsync(id);
        return o is null ? NotFound(new { message = "Offer not found." }) : Ok(new { data = o });
    }

    [HttpPost("recruitment/offers")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> PrepareOffer([FromBody] PrepareOfferDto dto)
        => Act(await service.PrepareOfferAsync(dto, UserId));

    [HttpPost("recruitment/offers/{id}/submit")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> SubmitOffer(string id)
        => Act(await service.SubmitOfferAsync(id, UserId));

    /// <summary>The salary approval. Whoever prepared the offer cannot approve it.</summary>
    [HttpPost("recruitment/offers/{id}/decide")]
    [Authorize(Policy = "Permission:hr.approve")]
    public async Task<IActionResult> DecideOffer(string id, [FromBody] DecideOfferDto dto)
        => Act(await service.DecideOfferAsync(id, dto, UserId, UserName));

    [HttpPost("recruitment/offers/{id}/issue")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> IssueOffer(string id)
        => Act(await service.IssueOfferAsync(id, UserId));

    [HttpPost("recruitment/offers/{id}/respond")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> RespondToOffer(string id, [FromBody] RespondToOfferDto dto)
        => Act(await service.RespondToOfferAsync(id, dto, UserId));

    [HttpPost("recruitment/offers/{id}/withdraw")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> WithdrawOffer(string id, [FromBody] WithdrawOfferDto dto)
        => Act(await service.WithdrawOfferAsync(id, dto, UserId));

    /// <summary>Runs the H12 sweep now instead of waiting for the daily tick. Idempotent.</summary>
    [HttpPost("recruitment/sweep")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Sweep()
        => Ok(new { data = await service.RunRecruitmentSweepAsync(Schema, UserId) });

    private IActionResult Act(RecruitmentActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
