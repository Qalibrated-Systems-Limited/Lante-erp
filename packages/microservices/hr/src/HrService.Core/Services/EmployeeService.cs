using AutoMapper;
using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Employees;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H1 (P1/P2) — the employee master. Creates the record with an auto-assigned number and
/// <see cref="EmploymentStatus.OnProbation"/>, captures the six onboarding data sets, and manages the document
/// vault and professional certifications.
/// <para><b>Onboarding completeness</b> (P1 design note) needs a signed contract <i>and</i> an ID copy on file;
/// it is recomputed whenever documents change rather than being a flag someone remembers to set.</para>
/// <para><b>Document verification is segregated</b> (P2 step 2.3): the verifier must not be the uploader, so
/// one person cannot both file and attest a document.</para>
/// <para><b>The login account is best-effort</b> (HR-DEC-2): the employee record is the master, so a
/// user-service outage records an error on the employee and leaves it retryable instead of failing onboarding.</para>
/// </summary>
public class EmployeeService(
    IGenericRepository<Employee> employees,
    IGenericRepository<EmployeeEmergencyContact> contacts,
    IGenericRepository<EmployeeEducation> education,
    IGenericRepository<EmployeeEmploymentHistory> history,
    IGenericRepository<EmployeeDocument> documents,
    IGenericRepository<EmployeeBankDetail> bankDetails,
    IGenericRepository<EmployeeCertification> certifications,
    IGenericRepository<Position> positions,
    IGenericRepository<OrgChartNode> orgNodes,
    IGenericRepository<HrAuditLog> audit,
    IUserDirectoryGateway directory,
    IMapper mapper) : IEmployeeService
{
    /// <summary>Documents that must be on file before onboarding counts as complete (P1 design note).</summary>
    private static readonly EmployeeDocumentType[] MandatoryDocuments =
        [EmployeeDocumentType.SignedContract, EmployeeDocumentType.IdCopy];

    private const int CertificationAlertDays = 30;   // HR-028

    // ── Reads ──
    public async Task<EmployeeListResult> GetAllAsync(EmployeeFilterParams filter)
    {
        var q = employees.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            q = q.Where(e => e.FirstName.ToLower().Contains(s)
                          || e.LastName.ToLower().Contains(s)
                          || e.EmployeeNumber.ToLower().Contains(s)
                          || e.WorkEmail.ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<EmploymentStatus>(filter.Status, true, out var st))
            q = q.Where(e => e.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.DepartmentId)) q = q.Where(e => e.DepartmentId == filter.DepartmentId);
        if (!string.IsNullOrWhiteSpace(filter.PositionId)) q = q.Where(e => e.PositionId == filter.PositionId);
        if (filter.OnboardingComplete is not null) q = q.Where(e => e.OnboardingComplete == filter.OnboardingComplete);

        var total = await q.CountAsync();
        var items = await q.OrderBy(e => e.EmployeeNumber)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();

        var rows = items.Select(e => new EmployeeRowDto
        {
            Id = e.Id,
            EmployeeNumber = e.EmployeeNumber,
            FullName = e.FullName,
            WorkEmail = e.WorkEmail,
            DepartmentName = e.DepartmentName,
            JobTitle = e.JobTitle,
            WorkMode = e.WorkMode.ToString(),
            Status = e.Status.ToString(),
            ExitDate = e.ExitDate,
            EmploymentType = e.EmploymentType.ToString(),
            HireDate = e.HireDate,
            ContractEndDate = e.ContractEndDate,
            OnboardingComplete = e.OnboardingComplete,
            HasUserAccount = !string.IsNullOrEmpty(e.UserId),
        }).ToList();
        return new EmployeeListResult(rows, total);
    }

    public async Task<EmployeeReadDto?> GetByIdAsync(string id)
    {
        var e = await LoadFullAsync(x => x.Id == id);
        return e is null ? null : await ToDtoAsync(e);
    }

    public async Task<EmployeeReadDto?> GetByUserIdAsync(string userId)
    {
        var e = await LoadFullAsync(x => x.UserId == userId);
        return e is null ? null : await ToDtoAsync(e);
    }

    public async Task<EmployeeSummaryDto> GetSummaryAsync()
    {
        var all = await employees.Query().AsNoTracking().ToListAsync();
        var now = DateTime.UtcNow;
        var horizon = now.AddDays(CertificationAlertDays);
        var certs = await certifications.Query().AsNoTracking().Where(c => c.IsActive).ToListAsync();

        return new EmployeeSummaryDto
        {
            Total = all.Count,
            Active = all.Count(e => e.Status == EmploymentStatus.Active),
            OnProbation = all.Count(e => e.Status == EmploymentStatus.OnProbation),
            OnLeave = all.Count(e => e.Status == EmploymentStatus.OnLeave),
            Suspended = all.Count(e => e.Status == EmploymentStatus.Suspended),
            Separated = all.Count(e => e.Status is EmploymentStatus.Resigned or EmploymentStatus.Terminated),
            OnboardingIncomplete = all.Count(e => !e.OnboardingComplete && !IsSeparated(e)),
            WithoutUserAccount = all.Count(e => string.IsNullOrEmpty(e.UserId) && !IsSeparated(e)),
            DocumentsAwaitingVerification = await documents.Query().AsNoTracking().CountAsync(d => !d.IsVerified),
            CertificationsExpiringIn30Days = certs.Count(c => c.ExpiryDate != null && c.ExpiryDate > now && c.ExpiryDate <= horizon),
            ExpiredCertifications = certs.Count(c => c.ExpiryDate != null && c.ExpiryDate <= now),
        };
    }

    // ── Create / update ──
    public async Task<EmployeeActionResult> CreateAsync(CreateEmployeeDto dto, string userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
            return Err("First and last name are required.");
        if (string.IsNullOrWhiteSpace(dto.WorkEmail))
            return Err("A work email is required — it is the login identity for the employee's account.");
        if (dto.HireDate == default)
            return Err("The hire date is required.");

        var email = dto.WorkEmail.Trim();
        if (await employees.Query().AnyAsync(e => e.WorkEmail.ToLower() == email.ToLower()))
            return Err($"An employee already exists with the work email {email}.");

        var type = ParseEnum(dto.EmploymentType, EmploymentType.Permanent);
        if (type is EmploymentType.FixedTerm or EmploymentType.Contract && dto.ContractEndDate is null)
            return Err($"{type} employment needs a contract end date — it drives the renewal alerts.");

        if (!string.IsNullOrWhiteSpace(dto.ReportsToId)
            && !await employees.Query().AnyAsync(e => e.Id == dto.ReportsToId))
            return Err("The specified line manager is not an employee.");

        string? positionTitle = null;
        if (!string.IsNullOrWhiteSpace(dto.PositionId))
        {
            var position = await positions.GetByIdAsync(dto.PositionId);
            if (position is null) return Err("Position not found.");
            positionTitle = position.Title;
        }

        // Departments and branches live in user-service (HR-DEC-3) — resolve the name for display, but do not
        // block onboarding when identity is unreachable (the id is still recorded).
        var departmentName = await ResolveUnitNameAsync(dto.DepartmentId, directory.ListDepartmentsAsync);
        var branchName = await ResolveUnitNameAsync(dto.BranchId, directory.ListBranchesAsync);

        var employee = new Employee
        {
            EmployeeNumber = await NextEmployeeNumberAsync(),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            OtherNames = dto.OtherNames,
            NationalId = dto.NationalId,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            MaritalStatus = dto.MaritalStatus,
            PersonalEmail = dto.PersonalEmail,
            PersonalPhone = dto.PersonalPhone,
            PhysicalAddress = dto.PhysicalAddress,
            KraPin = dto.KraPin,
            NssfNumber = dto.NssfNumber,
            ShaNumber = dto.ShaNumber,
            HelbNumber = dto.HelbNumber,
            WorkEmail = email,
            WorkPhone = dto.WorkPhone,
            HireDate = dto.HireDate,
            Status = EmploymentStatus.OnProbation,      // P1 step 1.2
            EmploymentType = type,
            ContractStartDate = dto.ContractStartDate ?? dto.HireDate,
            ContractEndDate = dto.ContractEndDate,
            DepartmentId = dto.DepartmentId,
            DepartmentName = departmentName,
            BranchId = dto.BranchId,
            BranchName = branchName,
            PositionId = dto.PositionId,
            JobTitle = positionTitle,
            WorkMode = ParseEnum(dto.WorkMode, WorkMode.Office),
            ReportsToId = dto.ReportsToId,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        var created = await employees.CreateAsync(employee);

        await LogAsync("Employee", created.Id, HrAuditAction.EmployeeCreated,
            $"Employee {created.EmployeeNumber} ({created.FullName}) onboarded — status on probation, hired {created.HireDate:yyyy-MM-dd}.", userId, userName);

        // P32 step 32.3 — the org-chart node follows a new hire automatically.
        await UpsertOrgNodeAsync(created, userId);

        var accountNote = "";
        if (dto.CreateUserAccount)
        {
            var account = await directory.CreateAccountAsync(
                created.FirstName, created.LastName, created.WorkEmail, dto.WorkPhone,
                created.DepartmentId, created.BranchId, dto.RoleIds);
            if (account.Created)
            {
                created.UserId = account.UserId;
                created.AccountCreationError = null;
                await LogAsync("Employee", created.Id, HrAuditAction.UserAccountCreated, account.Message, userId, userName);
            }
            else
            {
                created.AccountCreationError = account.Message;
                await LogAsync("Employee", created.Id, HrAuditAction.UserAccountFailed,
                    $"Login account not created: {account.Message}", userId, userName);
            }
            Touch(created, userId);
            await employees.UpdateAsync(created);
            accountNote = $" {account.Message}";
        }

        return new EmployeeActionResult("Created",
            $"Employee {created.EmployeeNumber} created on probation. Onboarding needs a signed contract and ID copy on file.{accountNote}",
            created.Id);
    }

    public async Task<EmployeeActionResult> UpdateAsync(string id, UpdateEmployeeDto dto, string userId)
    {
        var e = await employees.GetByIdAsync(id);
        if (e is null) return Err("Employee not found.");

        var previousManager = e.ReportsToId;

        if (dto.FirstName is not null) e.FirstName = dto.FirstName.Trim();
        if (dto.LastName is not null) e.LastName = dto.LastName.Trim();
        if (dto.OtherNames is not null) e.OtherNames = dto.OtherNames;
        if (dto.WorkPhone is not null) e.WorkPhone = dto.WorkPhone;
        if (dto.NationalId is not null) e.NationalId = dto.NationalId;
        if (dto.DateOfBirth is not null) e.DateOfBirth = dto.DateOfBirth;
        if (dto.Gender is not null) e.Gender = dto.Gender;
        if (dto.MaritalStatus is not null) e.MaritalStatus = dto.MaritalStatus;
        if (dto.PersonalEmail is not null) e.PersonalEmail = dto.PersonalEmail;
        if (dto.PersonalPhone is not null) e.PersonalPhone = dto.PersonalPhone;
        if (dto.PhysicalAddress is not null) e.PhysicalAddress = dto.PhysicalAddress;
        if (dto.KraPin is not null) e.KraPin = dto.KraPin;
        if (dto.NssfNumber is not null) e.NssfNumber = dto.NssfNumber;
        if (dto.ShaNumber is not null) e.ShaNumber = dto.ShaNumber;
        if (dto.HelbNumber is not null) e.HelbNumber = dto.HelbNumber;
        if (dto.ContractStartDate is not null) e.ContractStartDate = dto.ContractStartDate;
        if (dto.ContractEndDate is not null) e.ContractEndDate = dto.ContractEndDate;

        if (!string.IsNullOrWhiteSpace(dto.WorkEmail) && !string.Equals(dto.WorkEmail, e.WorkEmail, StringComparison.OrdinalIgnoreCase))
        {
            var email = dto.WorkEmail.Trim();
            if (await employees.Query().AnyAsync(x => x.Id != id && x.WorkEmail.ToLower() == email.ToLower()))
                return Err($"Another employee already uses the work email {email}.");
            e.WorkEmail = email;
        }

        if (!string.IsNullOrWhiteSpace(dto.EmploymentType))
        {
            e.EmploymentType = ParseEnum(dto.EmploymentType, e.EmploymentType);
            if (e.EmploymentType is EmploymentType.FixedTerm or EmploymentType.Contract && e.ContractEndDate is null)
                return Err($"{e.EmploymentType} employment needs a contract end date.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            if (!Enum.TryParse<EmploymentStatus>(dto.Status, true, out var status))
                return Err($"Unknown employment status '{dto.Status}'.");
            e.Status = status;
        }

        if (dto.DepartmentId is not null && dto.DepartmentId != e.DepartmentId)
        {
            e.DepartmentId = dto.DepartmentId;
            e.DepartmentName = await ResolveUnitNameAsync(dto.DepartmentId, directory.ListDepartmentsAsync);
        }
        if (dto.BranchId is not null && dto.BranchId != e.BranchId)
        {
            e.BranchId = dto.BranchId;
            e.BranchName = await ResolveUnitNameAsync(dto.BranchId, directory.ListBranchesAsync);
        }
        if (dto.PositionId is not null && dto.PositionId != e.PositionId)
        {
            var position = await positions.GetByIdAsync(dto.PositionId);
            if (position is null) return Err("Position not found.");
            e.PositionId = position.Id;
            e.JobTitle = position.Title;
        }
        if (dto.ReportsToId is not null)
        {
            if (dto.ReportsToId == id) return Err("An employee cannot report to themselves.");
            if (dto.ReportsToId.Length > 0 && !await employees.Query().AnyAsync(x => x.Id == dto.ReportsToId))
                return Err("The specified line manager is not an employee.");
            if (await WouldCycleAsync(id, dto.ReportsToId))
                return Err("That reporting line would create a cycle in the org chart.");
            e.ReportsToId = string.IsNullOrWhiteSpace(dto.ReportsToId) ? null : dto.ReportsToId;
        }
        if (!string.IsNullOrWhiteSpace(dto.WorkMode))
        {
            if (!Enum.TryParse<WorkMode>(dto.WorkMode, true, out var mode))
                return Err("Work mode must be Office, Field or Hybrid.");
            e.WorkMode = mode;
        }

        Touch(e, userId);
        await employees.UpdateAsync(e);
        await RecomputeOnboardingAsync(e, userId);

        if (e.ReportsToId != previousManager)
        {
            await UpsertOrgNodeAsync(e, userId);
            await LogAsync("OrgChart", e.Id, HrAuditAction.ReportingLineChanged,
                $"{e.FullName} now reports to {(e.ReportsToId is null ? "nobody (apex)" : await NameOfAsync(e.ReportsToId))}.", userId, null);
        }

        await LogAsync("Employee", e.Id, HrAuditAction.EmployeeUpdated, $"Employee {e.EmployeeNumber} updated.", userId, null);
        return new EmployeeActionResult("Ok", "Employee updated.", e.Id);
    }

    public async Task<EmployeeActionResult> CreateUserAccountAsync(string id, List<string>? roleIds, string userId)
    {
        var e = await employees.GetByIdAsync(id);
        if (e is null) return Err("Employee not found.");
        if (!string.IsNullOrEmpty(e.UserId)) return Err("This employee already has a login account.");
        if (IsSeparated(e)) return Err($"This employee is {e.Status} — no login account will be created.");

        var account = await directory.CreateAccountAsync(
            e.FirstName, e.LastName, e.WorkEmail, e.WorkPhone, e.DepartmentId, e.BranchId, roleIds);
        if (!account.Created)
        {
            e.AccountCreationError = account.Message;
            Touch(e, userId);
            await employees.UpdateAsync(e);
            await LogAsync("Employee", e.Id, HrAuditAction.UserAccountFailed, account.Message, userId, null);
            return Err(account.Message);
        }

        e.UserId = account.UserId;
        e.AccountCreationError = null;
        Touch(e, userId);
        await employees.UpdateAsync(e);
        await LogAsync("Employee", e.Id, HrAuditAction.UserAccountCreated, account.Message, userId, null);
        return new EmployeeActionResult("Ok", account.Message, e.Id);
    }

    // ── Sub-records ──
    public async Task<EmployeeActionResult> AddEmergencyContactAsync(string id, EmergencyContactDto dto, string userId)
    {
        var e = await employees.GetByIdAsync(id);
        if (e is null) return Err("Employee not found.");
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Phone))
            return Err("An emergency contact needs at least a name and phone number.");

        // Only one primary contact — promoting a new one demotes the old.
        if (dto.IsPrimary)
            foreach (var existing in await contacts.Query().Where(c => c.EmployeeId == id && c.IsPrimary).ToListAsync())
            {
                existing.IsPrimary = false;
                Touch(existing, userId);
                await contacts.UpdateAsync(existing);
            }

        await contacts.CreateAsync(new EmployeeEmergencyContact
        {
            EmployeeId = id, Name = dto.Name.Trim(), Relationship = dto.Relationship, Phone = dto.Phone,
            AlternatePhone = dto.AlternatePhone, IsPrimary = dto.IsPrimary, CreatedBy = userId, UpdatedBy = userId,
        });
        return new EmployeeActionResult("Ok", "Emergency contact added.", id);
    }

    public async Task<EmployeeActionResult> AddEducationAsync(string id, EducationDto dto, string userId)
    {
        if (await employees.GetByIdAsync(id) is null) return Err("Employee not found.");
        if (string.IsNullOrWhiteSpace(dto.Institution) || string.IsNullOrWhiteSpace(dto.Qualification))
            return Err("Institution and qualification are required.");
        await education.CreateAsync(new EmployeeEducation
        {
            EmployeeId = id, Institution = dto.Institution.Trim(), Qualification = dto.Qualification.Trim(),
            FieldOfStudy = dto.FieldOfStudy, YearCompleted = dto.YearCompleted, CertificateUrl = dto.CertificateUrl,
            CreatedBy = userId, UpdatedBy = userId,
        });
        return new EmployeeActionResult("Ok", "Education record added.", id);
    }

    public async Task<EmployeeActionResult> AddEmploymentHistoryAsync(string id, EmploymentHistoryDto dto, string userId)
    {
        if (await employees.GetByIdAsync(id) is null) return Err("Employee not found.");
        if (string.IsNullOrWhiteSpace(dto.EmployerName)) return Err("The previous employer's name is required.");
        if (dto.StartDate is not null && dto.EndDate is not null && dto.EndDate < dto.StartDate)
            return Err("The end date cannot precede the start date.");
        await history.CreateAsync(new EmployeeEmploymentHistory
        {
            EmployeeId = id, EmployerName = dto.EmployerName.Trim(), JobTitle = dto.JobTitle,
            StartDate = dto.StartDate, EndDate = dto.EndDate, ReasonForLeaving = dto.ReasonForLeaving,
            CreatedBy = userId, UpdatedBy = userId,
        });
        return new EmployeeActionResult("Ok", "Employment history added.", id);
    }

    public async Task<EmployeeActionResult> AddBankDetailAsync(string id, BankDetailDto dto, string userId)
    {
        if (await employees.GetByIdAsync(id) is null) return Err("Employee not found.");
        if (string.IsNullOrWhiteSpace(dto.BankName) || string.IsNullOrWhiteSpace(dto.AccountNumber))
            return Err("Bank name and account number are required.");

        // Payroll pays exactly one account, so a new primary demotes the previous one.
        if (dto.IsPrimary)
            foreach (var existing in await bankDetails.Query().Where(b => b.EmployeeId == id && b.IsPrimary).ToListAsync())
            {
                existing.IsPrimary = false;
                Touch(existing, userId);
                await bankDetails.UpdateAsync(existing);
            }

        await bankDetails.CreateAsync(new EmployeeBankDetail
        {
            EmployeeId = id, BankName = dto.BankName.Trim(), BranchName = dto.BranchName,
            AccountNumber = dto.AccountNumber.Trim(), AccountName = dto.AccountName,
            IsPrimary = dto.IsPrimary, IsActive = true, CreatedBy = userId, UpdatedBy = userId,
        });
        return new EmployeeActionResult("Ok", "Bank details added.", id);
    }

    public async Task<EmployeeActionResult> RemoveSubRecordAsync(string kind, string recordId, string userId)
    {
        switch (kind.ToLowerInvariant())
        {
            case "emergency-contact":
                var c = await contacts.GetByIdAsync(recordId);
                if (c is null) return Err("Emergency contact not found.");
                await contacts.DeleteAsync(c); break;
            case "education":
                var ed = await education.GetByIdAsync(recordId);
                if (ed is null) return Err("Education record not found.");
                await education.DeleteAsync(ed); break;
            case "employment-history":
                var h = await history.GetByIdAsync(recordId);
                if (h is null) return Err("Employment history record not found.");
                await history.DeleteAsync(h); break;
            case "bank-detail":
                var b = await bankDetails.GetByIdAsync(recordId);
                if (b is null) return Err("Bank detail not found.");
                await bankDetails.DeleteAsync(b); break;
            default:
                return Err($"Unknown record type '{kind}'.");
        }
        return new EmployeeActionResult("Ok", "Record removed.");
    }

    // ── Document vault ──
    public async Task<EmployeeActionResult> UploadDocumentAsync(string id, UploadDocumentDto dto, string userId)
    {
        var e = await employees.GetByIdAsync(id);
        if (e is null) return Err("Employee not found.");
        if (!Enum.TryParse<EmployeeDocumentType>(dto.DocumentType, true, out var type))
            return Err($"Unknown document type '{dto.DocumentType}'.");
        if (string.IsNullOrWhiteSpace(dto.FileUrl)) return Err("The document file is required.");

        await documents.CreateAsync(new EmployeeDocument
        {
            EmployeeId = id, DocumentType = type, DocumentName = dto.DocumentName, FileUrl = dto.FileUrl,
            UploadedAt = DateTime.UtcNow, UploadedBy = userId, ExpiryDate = dto.ExpiryDate,
            IsVerified = false,                     // P2 step 2.2 — always unverified on arrival
            CreatedBy = userId, UpdatedBy = userId,
        });

        await LogAsync("EmployeeDocument", id, HrAuditAction.DocumentUploaded,
            $"{type} uploaded for {e.EmployeeNumber} — awaiting verification by a second officer.", userId, null);

        var completed = await RecomputeOnboardingAsync(e, userId);
        return new EmployeeActionResult("Ok",
            completed ? $"{type} uploaded — onboarding documents are now complete." : $"{type} uploaded, awaiting verification.", id);
    }

    public async Task<EmployeeActionResult> VerifyDocumentAsync(string documentId, VerifyDocumentDto dto, string userId)
    {
        var doc = await documents.GetByIdAsync(documentId);
        if (doc is null) return Err("Document not found.");
        if (doc.IsVerified) return Err("This document has already been verified.");

        // P2 step 2.3 — segregation of duties: whoever filed it cannot be the one who attests it.
        if (string.Equals(doc.UploadedBy, userId, StringComparison.OrdinalIgnoreCase))
            return Err("A document must be verified by a second HR officer — you uploaded this one.");

        doc.IsVerified = true;
        doc.VerifiedBy = userId;
        doc.VerifiedAt = DateTime.UtcNow;
        doc.VerificationNotes = dto.Notes;
        Touch(doc, userId);
        await documents.UpdateAsync(doc);

        await LogAsync("EmployeeDocument", doc.Id, HrAuditAction.DocumentVerified,
            $"{doc.DocumentType} verified (uploaded by {doc.UploadedBy}, verified by {userId}).", userId, null);
        return new EmployeeActionResult("Ok", $"{doc.DocumentType} verified.", doc.EmployeeId);
    }

    public async Task<List<DocumentDto>> GetDocumentsAwaitingVerificationAsync()
    {
        var docs = await documents.Query().AsNoTracking().Where(d => !d.IsVerified)
            .OrderBy(d => d.UploadedAt).ToListAsync();
        return docs.Select(ToDocumentDto).ToList();
    }

    // ── Certifications ──
    public async Task<EmployeeActionResult> AddCertificationAsync(string id, CreateCertificationDto dto, string userId)
    {
        var e = await employees.GetByIdAsync(id);
        if (e is null) return Err("Employee not found.");
        if (string.IsNullOrWhiteSpace(dto.CertificationName)) return Err("The certification name is required.");
        if (dto.IssueDate is not null && dto.ExpiryDate is not null && dto.ExpiryDate < dto.IssueDate)
            return Err("The expiry date cannot precede the issue date.");

        await certifications.CreateAsync(new EmployeeCertification
        {
            EmployeeId = id, CertificationName = dto.CertificationName.Trim(), IssuingBody = dto.IssuingBody,
            CertificationNumber = dto.CertificationNumber, IssueDate = dto.IssueDate, ExpiryDate = dto.ExpiryDate,
            CertificateUrl = dto.CertificateUrl, IsActive = true, CreatedBy = userId, UpdatedBy = userId,
        });

        await LogAsync("EmployeeCertification", id, HrAuditAction.CertificationRegistered,
            $"Certification {dto.CertificationName} registered for {e.EmployeeNumber}"
            + (dto.ExpiryDate is null ? " (no expiry)." : $", expires {dto.ExpiryDate:yyyy-MM-dd}."), userId, null);
        return new EmployeeActionResult("Ok", "Certification registered.", id);
    }

    public async Task<List<CertificationDto>> GetExpiringCertificationsAsync(int withinDays)
    {
        var now = DateTime.UtcNow;
        var horizon = now.AddDays(withinDays <= 0 ? CertificationAlertDays : withinDays);
        var list = await certifications.Query().AsNoTracking()
            .Where(c => c.IsActive && c.ExpiryDate != null && c.ExpiryDate <= horizon)
            .OrderBy(c => c.ExpiryDate).ToListAsync();
        return list.Select(ToCertificationDto).ToList();
    }

    // ── Helpers ──
    private async Task<Employee?> LoadFullAsync(System.Linq.Expressions.Expression<Func<Employee, bool>> predicate)
        => await employees.Query().AsNoTracking()
            .Include(x => x.EmergencyContacts).Include(x => x.Education).Include(x => x.EmploymentHistory)
            .Include(x => x.Documents).Include(x => x.BankDetails).Include(x => x.Certifications)
            .FirstOrDefaultAsync(predicate);

    private async Task<EmployeeReadDto> ToDtoAsync(Employee e)
    {
        var dto = mapper.Map<EmployeeReadDto>(e);
        dto.FullName = e.FullName;
        dto.Status = e.Status.ToString();
        dto.ExitDate = e.ExitDate;
        dto.EmploymentType = e.EmploymentType.ToString();
        dto.HasUserAccount = !string.IsNullOrEmpty(e.UserId);
        dto.ReportsToName = e.ReportsToId is null ? null : await NameOfAsync(e.ReportsToId);
        dto.WorkMode = e.WorkMode.ToString();

        dto.EmergencyContacts = e.EmergencyContacts.Select(c => new EmergencyContactDto
        {
            Id = c.Id, Name = c.Name, Relationship = c.Relationship, Phone = c.Phone,
            AlternatePhone = c.AlternatePhone, IsPrimary = c.IsPrimary,
        }).OrderByDescending(c => c.IsPrimary).ToList();

        dto.Education = e.Education.Select(x => new EducationDto
        {
            Id = x.Id, Institution = x.Institution, Qualification = x.Qualification,
            FieldOfStudy = x.FieldOfStudy, YearCompleted = x.YearCompleted, CertificateUrl = x.CertificateUrl,
        }).OrderByDescending(x => x.YearCompleted).ToList();

        dto.EmploymentHistory = e.EmploymentHistory.Select(x => new EmploymentHistoryDto
        {
            Id = x.Id, EmployerName = x.EmployerName, JobTitle = x.JobTitle,
            StartDate = x.StartDate, EndDate = x.EndDate, ReasonForLeaving = x.ReasonForLeaving,
        }).OrderByDescending(x => x.StartDate).ToList();

        dto.Documents = e.Documents.Select(ToDocumentDto).OrderBy(d => d.DocumentType).ToList();

        dto.BankDetails = e.BankDetails.Select(b => new BankDetailDto
        {
            Id = b.Id, BankName = b.BankName, BranchName = b.BranchName, AccountNumber = b.AccountNumber,
            AccountName = b.AccountName, IsPrimary = b.IsPrimary, IsActive = b.IsActive,
        }).OrderByDescending(b => b.IsPrimary).ToList();

        dto.Certifications = e.Certifications.Select(ToCertificationDto).OrderBy(c => c.ExpiryDate).ToList();

        dto.MissingMandatoryDocuments = MandatoryDocuments
            .Where(t => e.Documents.All(d => d.DocumentType != t))
            .Select(t => t.ToString()).ToList();
        dto.HasExpiredCertification = dto.Certifications.Any(c => c.IsExpired);
        return dto;
    }

    private static DocumentDto ToDocumentDto(EmployeeDocument d) => new()
    {
        Id = d.Id, DocumentType = d.DocumentType.ToString(), DocumentName = d.DocumentName, FileUrl = d.FileUrl,
        UploadedAt = d.UploadedAt, UploadedBy = d.UploadedBy, ExpiryDate = d.ExpiryDate,
        IsVerified = d.IsVerified, VerifiedBy = d.VerifiedBy, VerifiedAt = d.VerifiedAt,
        VerificationNotes = d.VerificationNotes,
        IsExpired = d.ExpiryDate is not null && d.ExpiryDate <= DateTime.UtcNow,
    };

    private static CertificationDto ToCertificationDto(EmployeeCertification c)
    {
        var now = DateTime.UtcNow;
        return new CertificationDto
        {
            Id = c.Id, CertificationName = c.CertificationName, IssuingBody = c.IssuingBody,
            CertificationNumber = c.CertificationNumber, IssueDate = c.IssueDate, ExpiryDate = c.ExpiryDate,
            CertificateUrl = c.CertificateUrl, IsActive = c.IsActive, Alert30SentAt = c.Alert30SentAt,
            IsExpired = c.ExpiryDate is not null && c.ExpiryDate <= now,
            IsExpiringSoon = c.ExpiryDate is not null && c.ExpiryDate > now && c.ExpiryDate <= now.AddDays(CertificationAlertDays),
        };
    }

    /// <summary>Onboarding is complete once the mandatory documents exist — recomputed rather than remembered,
    /// so it cannot drift from what is actually on file.</summary>
    private async Task<bool> RecomputeOnboardingAsync(Employee e, string userId)
    {
        var held = await documents.Query().AsNoTracking()
            .Where(d => d.EmployeeId == e.Id).Select(d => d.DocumentType).ToListAsync();
        var complete = MandatoryDocuments.All(held.Contains);
        if (complete == e.OnboardingComplete) return complete;

        e.OnboardingComplete = complete;
        e.OnboardingCompletedAt = complete ? DateTime.UtcNow : null;
        Touch(e, userId);
        await employees.UpdateAsync(e);
        if (complete)
            await LogAsync("Employee", e.Id, HrAuditAction.OnboardingCompleted,
                $"Onboarding documents complete for {e.EmployeeNumber}.", userId, null);
        return complete;
    }

    private async Task UpsertOrgNodeAsync(Employee e, string userId)
    {
        var node = await orgNodes.Query().FirstOrDefaultAsync(n => n.EmployeeId == e.Id);
        var level = await DepthOfAsync(e.ReportsToId);
        if (node is null)
        {
            var siblings = await orgNodes.Query().AsNoTracking().CountAsync(n => n.ParentEmployeeId == e.ReportsToId);
            await orgNodes.CreateAsync(new OrgChartNode
            {
                EmployeeId = e.Id, ParentEmployeeId = e.ReportsToId, Level = level,
                DepartmentId = e.DepartmentId, DepartmentName = e.DepartmentName,
                DisplayOrder = siblings, IsActive = !IsSeparated(e), CreatedBy = userId, UpdatedBy = userId,
            });
            return;
        }
        node.ParentEmployeeId = e.ReportsToId;
        node.Level = level;
        node.DepartmentId = e.DepartmentId;
        node.DepartmentName = e.DepartmentName;
        node.IsActive = !IsSeparated(e);
        Touch(node, userId);
        await orgNodes.UpdateAsync(node);
    }

    /// <summary>Walks up the reporting chain, bounded so a pre-existing cycle cannot hang the request.</summary>
    private async Task<int> DepthOfAsync(string? managerId)
    {
        var depth = 0;
        var seen = new HashSet<string>();
        var current = managerId;
        while (!string.IsNullOrEmpty(current) && seen.Add(current) && depth < 20)
        {
            current = await employees.Query().AsNoTracking()
                .Where(x => x.Id == current).Select(x => x.ReportsToId).FirstOrDefaultAsync();
            depth++;
        }
        return depth;
    }

    /// <summary>True when making <paramref name="managerId"/> the manager of <paramref name="employeeId"/>
    /// would close a loop.</summary>
    private async Task<bool> WouldCycleAsync(string employeeId, string? managerId)
    {
        var seen = new HashSet<string>();
        var current = managerId;
        while (!string.IsNullOrEmpty(current) && seen.Add(current))
        {
            if (current == employeeId) return true;
            current = await employees.Query().AsNoTracking()
                .Where(x => x.Id == current).Select(x => x.ReportsToId).FirstOrDefaultAsync();
        }
        return false;
    }

    private async Task<string?> NameOfAsync(string employeeId)
    {
        var e = await employees.Query().AsNoTracking()
            .Where(x => x.Id == employeeId)
            .Select(x => new { x.FirstName, x.OtherNames, x.LastName }).FirstOrDefaultAsync();
        return e is null ? null : string.Join(' ', new[] { e.FirstName, e.OtherNames, e.LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static async Task<string?> ResolveUnitNameAsync(string? id, Func<CancellationToken, Task<List<DTOs.Org.OrgUnitDto>>> load)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var units = await load(CancellationToken.None);
        return units.FirstOrDefault(u => u.Id == id)?.Name;
    }

    private async Task<string> NextEmployeeNumberAsync()
    {
        var prefix = $"EMP-{DateTime.UtcNow.Year}-";
        var count = await employees.Query().CountAsync(e => e.EmployeeNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private static bool IsSeparated(Employee e)
        => e.Status is EmploymentStatus.Resigned or EmploymentStatus.Terminated;

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;

    private static EmployeeActionResult Err(string message) => new("Error", message);
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }

    private async Task LogAsync(string entityType, string entityId, HrAuditAction action, string detail, string userId, string? userName)
    {
        await audit.CreateAsync(new HrAuditLog
        {
            EntityType = entityType, EntityId = entityId, Action = action, Detail = detail,
            PerformedBy = userId, PerformedByName = userName, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
