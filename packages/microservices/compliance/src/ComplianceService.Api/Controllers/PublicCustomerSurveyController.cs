using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ComplianceService.Core.DTOs.Surveys;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Enums;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

/// <summary>
/// Public customer-satisfaction survey. No authentication, no OTP — pure anonymous feedback
/// capture; nothing gets actioned downstream. Aggregated by QualityDashboardService for the
/// internal Quality dashboard.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/portal/customer-survey")]
[AllowAnonymous]
[EnableRateLimiting("portal-submit")]
public class PublicCustomerSurveyController(
    IComplianceCrudService<CustomerSurveyResponse> surveyResponses) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] CreateCustomerSurveyResponseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RespondentName))
            return BadRequest(new { message = "Your name is required." });
        if (string.IsNullOrWhiteSpace(dto.CorporationName))
            return BadRequest(new { message = "Your company/corporation name is required." });
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Email address is required." });
        if (!IsValidEmail(dto.Email))
            return BadRequest(new { message = "Please enter a valid email address." });
        if (string.IsNullOrWhiteSpace(dto.CounsellorName))
            return BadRequest(new { message = "The name of the staff member who served you is required." });
        if (string.IsNullOrWhiteSpace(dto.Details))
            return BadRequest(new { message = "Please provide details of your compliment, complaint, or feedback." });
        if (!Enum.TryParse<SurveyFeedbackType>(dto.FeedbackType, true, out var feedbackType))
            return BadRequest(new { message = "Please select a valid feedback type (Compliment, Complaint, or General Feedback)." });
        if (!Enum.TryParse<ServiceRating>(dto.Rating, true, out var rating))
            return BadRequest(new { message = "Please select a valid service rating." });

        var response = new CustomerSurveyResponse
        {
            RespondentName = dto.RespondentName.Trim(),
            CorporationName = dto.CorporationName.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            CounsellorName = dto.CounsellorName.Trim(),
            FeedbackType = feedbackType,
            Details = dto.Details.Trim(),
            Rating = rating,
            SubmittedAt = DateTime.UtcNow,
            CreatedBy = "portal-anonymous",
        };

        await surveyResponses.CreateAsync(response);

        return Ok(new { message = "Thank you for your feedback." });
    }

    private static bool IsValidEmail(string email)
    {
        try { _ = new System.Net.Mail.MailAddress(email); return true; }
        catch { return false; }
    }
}
