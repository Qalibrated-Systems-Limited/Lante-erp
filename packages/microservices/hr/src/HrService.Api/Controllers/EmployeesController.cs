using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Employees;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>H1 (P1/P2, HR-001/002/004) — the employee master record: onboarding, emergency contacts,
/// education, prior employment, bank details, the document vault (verified by a second officer) and
/// professional certifications.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr/employees")]
[Authorize]
public class EmployeesController(IEmployeeService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    // ── Reads ──
    [HttpGet]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> GetAll([FromQuery] EmployeeFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize, pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize) });
    }

    [HttpGet("summary")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    /// <summary>Documents uploaded but not yet verified — the second-officer queue (P2 step 2.3).</summary>
    [HttpGet("documents/awaiting-verification")]
    [Authorize(Policy = "Permission:hr.read.all")]
    public async Task<IActionResult> AwaitingVerification()
        => Ok(new { data = await service.GetDocumentsAwaitingVerificationAsync() });

    /// <summary>Certifications expiring within the window (HR-028, default 30 days).</summary>
    [HttpGet("certifications/expiring")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ExpiringCertifications([FromQuery] int withinDays = 30)
        => Ok(new { data = await service.GetExpiringCertificationsAsync(withinDays) });

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> GetById(string id)
    {
        var e = await service.GetByIdAsync(id);
        return e is null ? NotFound(new { message = "Employee not found." }) : Ok(new { data = e });
    }

    /// <summary>Resolve an employee from a user-service account id — how sibling modules join to HR (HR-DEC-2).</summary>
    [HttpGet("by-user/{userId}")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> GetByUser(string userId)
    {
        var e = await service.GetByUserIdAsync(userId);
        return e is null ? NotFound(new { message = "No employee is linked to that user account." }) : Ok(new { data = e });
    }

    // ── Onboarding ──
    [HttpPost]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDto dto)
        => Act(await service.CreateAsync(dto, UserId, UserName));

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateEmployeeDto dto)
        => Act(await service.UpdateAsync(id, dto, UserId));

    /// <summary>Retry the user-service account creation when onboarding could not reach identity.</summary>
    [HttpPost("{id}/user-account")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CreateUserAccount(string id, [FromBody] CreateAccountRequest? body)
        => Act(await service.CreateUserAccountAsync(id, body?.RoleIds, UserId));

    public record CreateAccountRequest(List<string>? RoleIds);

    // ── Sub-records (P1 steps 1.3, 1.4, 1.6) ──
    [HttpPost("{id}/emergency-contacts")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AddEmergencyContact(string id, [FromBody] EmergencyContactDto dto)
        => Act(await service.AddEmergencyContactAsync(id, dto, UserId));

    [HttpPost("{id}/education")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AddEducation(string id, [FromBody] EducationDto dto)
        => Act(await service.AddEducationAsync(id, dto, UserId));

    [HttpPost("{id}/employment-history")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AddEmploymentHistory(string id, [FromBody] EmploymentHistoryDto dto)
        => Act(await service.AddEmploymentHistoryAsync(id, dto, UserId));

    [HttpPost("{id}/bank-details")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AddBankDetail(string id, [FromBody] BankDetailDto dto)
        => Act(await service.AddBankDetailAsync(id, dto, UserId));

    /// <summary>kind = emergency-contact | education | employment-history | bank-detail</summary>
    [HttpDelete("{kind}/{recordId}")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> RemoveSubRecord(string kind, string recordId)
        => Act(await service.RemoveSubRecordAsync(kind, recordId, UserId));

    // ── Document vault (P2) ──
    [HttpPost("{id}/documents")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> UploadDocument(string id, [FromBody] UploadDocumentDto dto)
        => Act(await service.UploadDocumentAsync(id, dto, UserId));

    /// <summary>P2 step 2.3 — must be a different HR officer from the uploader.</summary>
    [HttpPost("documents/{documentId}/verify")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> VerifyDocument(string documentId, [FromBody] VerifyDocumentDto? dto)
        => Act(await service.VerifyDocumentAsync(documentId, dto ?? new VerifyDocumentDto(), UserId));

    // ── Certifications (P2 step 2.4) ──
    [HttpPost("{id}/certifications")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AddCertification(string id, [FromBody] CreateCertificationDto dto)
        => Act(await service.AddCertificationAsync(id, dto, UserId));

    private IActionResult Act(EmployeeActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
