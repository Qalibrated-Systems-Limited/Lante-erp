using HrService.Core.DTOs.Employees;

namespace HrService.Core.Interfaces.Services;

/// <summary>H1 (P1/P2, HR-001/002/004) — the employee master: onboarding, the six data-capture steps, the
/// document vault with second-officer verification, and professional certifications.</summary>
public interface IEmployeeService
{
    Task<EmployeeListResult> GetAllAsync(EmployeeFilterParams filter);
    Task<EmployeeReadDto?> GetByIdAsync(string id);
    /// <summary>Lookup by the user-service account id — how sibling modules resolve an employee (HR-DEC-2).</summary>
    Task<EmployeeReadDto?> GetByUserIdAsync(string userId);
    Task<EmployeeSummaryDto> GetSummaryAsync();

    Task<EmployeeActionResult> CreateAsync(CreateEmployeeDto dto, string userId, string? userName);
    Task<EmployeeActionResult> UpdateAsync(string id, UpdateEmployeeDto dto, string userId);
    /// <summary>Retries the user-service account creation for an employee who has none.</summary>
    Task<EmployeeActionResult> CreateUserAccountAsync(string id, List<string>? roleIds, string userId);

    // ── Sub-records (P1 steps 1.3, 1.4, 1.6) ──
    Task<EmployeeActionResult> AddEmergencyContactAsync(string id, EmergencyContactDto dto, string userId);
    Task<EmployeeActionResult> AddEducationAsync(string id, EducationDto dto, string userId);
    Task<EmployeeActionResult> AddEmploymentHistoryAsync(string id, EmploymentHistoryDto dto, string userId);
    Task<EmployeeActionResult> AddBankDetailAsync(string id, BankDetailDto dto, string userId);
    Task<EmployeeActionResult> RemoveSubRecordAsync(string kind, string recordId, string userId);

    // ── Document vault (P2) ──
    Task<EmployeeActionResult> UploadDocumentAsync(string id, UploadDocumentDto dto, string userId);
    /// <summary>P2 step 2.3 — verification by a <b>second</b> HR officer; the uploader cannot verify.</summary>
    Task<EmployeeActionResult> VerifyDocumentAsync(string documentId, VerifyDocumentDto dto, string userId);
    Task<List<DocumentDto>> GetDocumentsAwaitingVerificationAsync();

    // ── Certifications (P2 step 2.4) ──
    Task<EmployeeActionResult> AddCertificationAsync(string id, CreateCertificationDto dto, string userId);
    Task<List<CertificationDto>> GetExpiringCertificationsAsync(int withinDays);
}
