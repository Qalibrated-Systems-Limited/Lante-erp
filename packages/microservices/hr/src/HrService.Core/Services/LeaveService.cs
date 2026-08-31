using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Leave;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H3 (P4 + P5 + P6) — leave configuration, entitlements, applications with tiered approval, and the year-end
/// carry-forward cycle.
/// <para><b>The balance is always derived</b> (entitled − taken) rather than stored, so it cannot drift away
/// from the approved requests no matter which path — approval, cancellation, carry-forward, expiry, manual
/// adjustment — last touched the row.</para>
/// <para><b>Pending requests reserve their days.</b> The DFD checks the balance when applying but only
/// decrements it on final approval (P5 steps 5.1 gate and 5.5); with a multi-tier chain that leaves a window
/// where two applications each pass the gate and together overdraw the entitlement. A new request is therefore
/// measured against balance − days already committed to requests still in a chain.</para>
/// <para><b>The approval chain is derived from the leave type's flags, not its name</b> (see
/// <see cref="LeaveType"/>), and every step is written up front so the chain is visible and auditable before
/// anyone acts on it.</para>
/// </summary>
public class LeaveService(
    IGenericRepository<Employee> employees,
    IGenericRepository<LeaveType> types,
    IGenericRepository<LeaveEntitlement> entitlements,
    IGenericRepository<LeaveRequest> requests,
    IGenericRepository<LeaveApprovalLog> approvals,
    IGenericRepository<LeaveCarryForward> carryForwards,
    IGenericRepository<EmployeeDocument> documents,
    IGenericRepository<HrAuditLog> audit,
    IWorkCalendar calendar,
    IHrAlertGateway notifier) : ILeaveService
{
    /// <summary>Carried annual leave must be used in Q1 (P6 step 6.5).</summary>
    private const int CarryExpiryMonth = 3;
    private const int CarryExpiryDay = 31;

    // ══ Summary ══
    public async Task<LeaveSummaryDto> GetSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var year = today.Year;

        var allRequests = await requests.Query().AsNoTracking().ToListAsync();
        var pending = allRequests.Where(r => r.Status == LeaveRequestStatus.Pending).ToList();
        var pendingSteps = pending.Count == 0
            ? []
            : await approvals.Query().AsNoTracking()
                .Where(a => a.Action == LeaveApprovalAction.Pending)
                .ToListAsync();

        var awaiting = pending
            .Select(r => pendingSteps.FirstOrDefault(s => s.LeaveRequestId == r.Id && s.Step == r.CurrentStep)?.Role)
            .Where(role => role is not null)
            .GroupBy(role => role!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var attachedTo = await documents.Query().AsNoTracking()
            .Where(d => d.LeaveRequestId != null).Select(d => d.LeaveRequestId!).Distinct().ToListAsync();

        var activeCarry = await carryForwards.Query().AsNoTracking()
            .Where(c => c.Status == CarryForwardStatus.Active).ToListAsync();

        return new LeaveSummaryDto
        {
            LeaveTypesConfigured = await types.Query().CountAsync(t => t.IsActive),
            EmployeesWithEntitlements = await entitlements.Query().Where(e => e.Year == year)
                .Select(e => e.EmployeeId).Distinct().CountAsync(),

            PendingRequests = pending.Count,
            AwaitingLineManager = awaiting.GetValueOrDefault(LeaveApprovalRole.LineManager),
            AwaitingHr = awaiting.GetValueOrDefault(LeaveApprovalRole.Hr),
            AwaitingBoard = awaiting.GetValueOrDefault(LeaveApprovalRole.Board),
            MissingRequiredDocument = pending.Count(r => r.DocumentRequired && !attachedTo.Contains(r.Id)),

            OnLeaveToday = allRequests.Count(r => r.Status == LeaveRequestStatus.Approved
                                              && r.StartDate.Date <= today && r.EndDate.Date >= today),
            ApprovedUpcoming = allRequests.Count(r => r.Status == LeaveRequestStatus.Approved && r.StartDate.Date > today),
            RejectedThisYear = allRequests.Count(r => r.Status == LeaveRequestStatus.Rejected && r.SubmittedAt.Year == year),
            DaysTakenThisYear = await entitlements.Query().Where(e => e.Year == year).SumAsync(e => e.DaysTaken),

            CarryForwardActive = activeCarry.Count,
            DaysCarriedActive = activeCarry.Sum(c => c.DaysCarried - c.DaysExpired),
            CarryForwardExpiringIn30Days = activeCarry.Count(c => c.ExpiryDate.Date >= today
                                                              && c.ExpiryDate.Date <= today.AddDays(30)),
        };
    }

    // ══ Leave types (P4 step 4.1) ══
    public async Task<List<LeaveTypeDto>> ListTypesAsync(bool includeInactive)
    {
        var q = types.Query().AsNoTracking();
        if (!includeInactive) q = q.Where(t => t.IsActive);
        var list = await q.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<LeaveActionResult> CreateTypeAsync(SaveLeaveTypeDto dto, string userId)
    {
        var code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return Err("A leave type needs a code.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A leave type needs a name.");
        if (await types.Query().AnyAsync(t => t.Code == code))
            return Err($"Leave type '{code}' already exists.");

        var invalid = Validate(dto);
        if (invalid is not null) return Err(invalid);

        var entity = new LeaveType { Code = code, CreatedBy = userId, UpdatedBy = userId };
        Apply(entity, dto);
        var created = await types.CreateAsync(entity);

        await LogAsync("LeaveType", created.Id, HrAuditAction.LeaveTypeConfigured,
            $"Leave type {created.Code} ({created.Name}) created — {created.DaysAllowed} day(s), chain: {ChainLabel(created)}.", userId, null);
        return new LeaveActionResult("Created", $"{created.Name} configured.", created.Id);
    }

    public async Task<LeaveActionResult> UpdateTypeAsync(string id, SaveLeaveTypeDto dto, string userId)
    {
        var entity = await types.GetByIdAsync(id);
        if (entity is null) return Err("Leave type not found.");

        var invalid = Validate(dto);
        if (invalid is not null) return Err(invalid);

        Apply(entity, dto);
        if (dto.IsActive is not null) entity.IsActive = dto.IsActive.Value;
        Touch(entity, userId);
        await types.UpdateAsync(entity);

        await LogAsync("LeaveType", entity.Id, HrAuditAction.LeaveTypeConfigured,
            $"Leave type {entity.Code} updated — {entity.DaysAllowed} day(s), chain: {ChainLabel(entity)}.", userId, null);
        return new LeaveActionResult("Updated", $"{entity.Name} updated. Existing entitlements keep the days they were assigned.", entity.Id);
    }

    /// <summary>
    /// The QSL policy defaults. Seeded rather than hard-coded so a tenant can change any of it afterwards; the
    /// numbers come from the HR policy manual and the Employment Act 2007.
    /// </summary>
    public async Task<LeaveActionResult> SeedDefaultTypesAsync(string userId)
    {
        var defaults = new[]
        {
            // code, name, days, carries, cap, docAfter, docType, hr, board, boardAfter, workingDays, proRate, fullPay, paid
            new LeaveType { Code = "ANNUAL", Name = "Annual Leave", DaysAllowed = 21, CarriesForward = true,
                MaxCarryForwardDays = 10, CountsWorkingDaysOnly = true, ProRateFirstYear = true, DisplayOrder = 1,
                Description = "21 working days a year. Up to 10 unused days carry into the new year and expire on 31 March." },
            new LeaveType { Code = "SICK", Name = "Sick Leave", DaysAllowed = 14, FullPayDays = 7,
                RequiresDocumentAfterDays = 3, DocumentTypeRequired = EmployeeDocumentType.MedicalCertificate,
                CountsWorkingDaysOnly = true, DisplayOrder = 2,
                Description = "7 days at full pay then 7 at half pay (Employment Act 2007). A medical certificate is required beyond 3 days." },
            new LeaveType { Code = "MATERNITY", Name = "Maternity Leave", DaysAllowed = 90,
                RequiresDocumentAfterDays = 0, DocumentTypeRequired = EmployeeDocumentType.AntenatalBooking,
                RequiresHrApproval = true, CountsWorkingDaysOnly = false, DisplayOrder = 3,
                Description = "90 calendar days. Line Manager then HR. Antenatal booking required." },
            new LeaveType { Code = "PATERNITY", Name = "Paternity Leave", DaysAllowed = 14,
                RequiresDocumentAfterDays = 0, DocumentTypeRequired = EmployeeDocumentType.BirthCertificate,
                CountsWorkingDaysOnly = false, DisplayOrder = 4,
                Description = "14 calendar days. Birth certificate required." },
            new LeaveType { Code = "COMPASSIONATE", Name = "Compassionate Leave", DaysAllowed = 3,
                RequiresHrApproval = true, CountsWorkingDaysOnly = true, DisplayOrder = 5,
                Description = "3 working days. Line Manager then HR." },
            new LeaveType { Code = "STUDY", Name = "Study Leave", DaysAllowed = 0,
                RequiresDocumentAfterDays = 0, DocumentTypeRequired = EmployeeDocumentType.InstitutionLetter,
                RequiresHrApproval = true, RequiresBoardApproval = true, BoardApprovalAfterDays = 14,
                CountsWorkingDaysOnly = true, DisplayOrder = 6,
                Description = "At MD discretion — no fixed entitlement. Over 2 weeks also needs Board approval. Institution letter required." },
            new LeaveType { Code = "UNPAID", Name = "Unpaid Leave", DaysAllowed = 0, IsPaid = false,
                RequiresHrApproval = true, CountsWorkingDaysOnly = true, DisplayOrder = 7,
                Description = "Unpaid absence approved in advance. Line Manager then HR; deducted by payroll." },
        };

        var existing = await types.Query().Select(t => t.Code).ToListAsync();
        var added = 0;
        foreach (var t in defaults.Where(d => !existing.Contains(d.Code)))
        {
            t.CreatedBy = userId;
            t.UpdatedBy = userId;
            await types.CreateAsync(t);
            added++;
        }

        if (added > 0)
            await LogAsync("LeaveType", "seed", HrAuditAction.LeaveTypeConfigured,
                $"{added} default leave type(s) installed from QSL policy.", userId, null);

        return new LeaveActionResult("Seeded",
            added == 0 ? "All default leave types already exist — nothing to add." : $"{added} leave type(s) installed.");
    }

    // ══ Entitlements (P4 step 4.2) ══
    public async Task<List<LeaveEntitlementDto>> ListEntitlementsAsync(string? employeeId, int? year, string? leaveTypeId)
    {
        var q = entitlements.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(e => e.EmployeeId == employeeId);
        if (year is not null) q = q.Where(e => e.Year == year);
        if (!string.IsNullOrWhiteSpace(leaveTypeId)) q = q.Where(e => e.LeaveTypeId == leaveTypeId);

        var list = await q.OrderBy(e => e.EmployeeNumber).ThenBy(e => e.LeaveTypeCode).ToListAsync();
        var pending = await PendingDaysByEntitlementAsync(list);
        return list.Select(e => ToDto(e, pending.GetValueOrDefault(Key(e)))).ToList();
    }

    /// <summary>
    /// P4 step 4.2 — give every active employee this year's allowance for each entitled leave type.
    /// <para>Idempotent on (employee, type, year): an existing row is left exactly as it is, because it may
    /// already carry taken days, carried-in days or a manual adjustment. Types with no fixed entitlement
    /// (study, unpaid) get no row — there is nothing to allocate.</para>
    /// </summary>
    public async Task<LeaveActionResult> AssignEntitlementsAsync(int year, string? employeeId, string userId)
    {
        if (year < 2000 || year > 2100) return Err("That is not a plausible year.");

        var activeTypes = await types.Query().Where(t => t.IsActive && t.DaysAllowed > 0).ToListAsync();
        if (activeTypes.Count == 0)
            return Err("No leave types with an entitlement are configured — install the defaults first.");

        var staffQuery = employees.Query().Where(e => e.Status != EmploymentStatus.Resigned
                                                   && e.Status != EmploymentStatus.Terminated);
        if (!string.IsNullOrWhiteSpace(employeeId)) staffQuery = staffQuery.Where(e => e.Id == employeeId);
        var staff = await staffQuery.ToListAsync();
        if (staff.Count == 0) return Err("No active employees to assign entitlements to.");

        var created = await AssignForAsync(staff, activeTypes, year, userId);

        return new LeaveActionResult("Assigned",
            created == 0
                ? $"Every active employee already has {year} entitlements — nothing to add."
                : $"{created} entitlement row(s) created for {year}.");
    }

    public async Task<LeaveActionResult> AdjustEntitlementAsync(string entitlementId, AdjustEntitlementDto dto, string userId)
    {
        var row = await entitlements.GetByIdAsync(entitlementId);
        if (row is null) return Err("Entitlement not found.");
        if (dto.Days == 0) return Err("An adjustment of zero days changes nothing.");
        if (string.IsNullOrWhiteSpace(dto.Reason)) return Err("An entitlement adjustment needs a reason on the record.");

        var newEntitled = row.DaysEntitled + dto.Days;
        if (newEntitled < row.DaysTaken)
            return Err($"That would drop the allowance to {newEntitled} day(s), below the {row.DaysTaken} already taken.");

        row.DaysEntitled = newEntitled;
        row.Notes = string.IsNullOrWhiteSpace(row.Notes)
            ? $"{dto.Days:+0.##;-0.##} day(s): {dto.Reason}"
            : $"{row.Notes} | {dto.Days:+0.##;-0.##} day(s): {dto.Reason}";
        Touch(row, userId);
        await entitlements.UpdateAsync(row);

        await LogAsync("LeaveEntitlement", row.Id, HrAuditAction.LeaveEntitlementAdjusted,
            $"{row.EmployeeNumber} {row.LeaveTypeCode} {row.Year} entitlement adjusted by {dto.Days:+0.##;-0.##} to {newEntitled}: {dto.Reason}", userId, null);
        return new LeaveActionResult("Adjusted",
            $"{row.LeaveTypeName} allowance is now {newEntitled} day(s); balance {newEntitled - row.DaysTaken}.", row.Id);
    }

    // ══ Requests (P5) ══
    public async Task<List<LeaveRequestDto>> ListRequestsAsync(string? status, string? employeeId, string? awaitingRole)
    {
        var q = requests.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeaveRequestStatus>(status, true, out var st))
            q = q.Where(r => r.Status == st);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(r => r.EmployeeId == employeeId);

        var list = await q.OrderByDescending(r => r.SubmittedAt).ToListAsync();
        var ids = list.Select(r => r.Id).ToList();

        var steps = await approvals.Query().AsNoTracking().Where(a => ids.Contains(a.LeaveRequestId))
            .OrderBy(a => a.Step).ToListAsync();
        var attached = await documents.Query().AsNoTracking()
            .Where(d => d.LeaveRequestId != null && ids.Contains(d.LeaveRequestId!))
            .Select(d => d.LeaveRequestId!).Distinct().ToListAsync();

        var rows = list.Select(r => ToDto(r, steps.Where(s => s.LeaveRequestId == r.Id).ToList(), attached.Contains(r.Id))).ToList();

        if (!string.IsNullOrWhiteSpace(awaitingRole) && Enum.TryParse<LeaveApprovalRole>(awaitingRole, true, out var role))
            rows = rows.Where(r => r.Status == nameof(LeaveRequestStatus.Pending)
                                && string.Equals(r.AwaitingRole, role.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
        return rows;
    }

    public async Task<LeaveRequestDto?> GetRequestAsync(string id)
    {
        var r = await requests.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return null;
        var steps = await approvals.Query().AsNoTracking().Where(a => a.LeaveRequestId == id).OrderBy(a => a.Step).ToListAsync();
        var attached = await documents.Query().AsNoTracking().AnyAsync(d => d.LeaveRequestId == id);
        return ToDto(r, steps, attached);
    }

    public async Task<LeavePreviewDto> PreviewAsync(CreateLeaveRequestDto dto)
    {
        var type = await types.Query().AsNoTracking().FirstOrDefaultAsync(t => t.Id == dto.LeaveTypeId);
        if (type is null) return new LeavePreviewDto { Warning = "Leave type not found." };

        var days = await CountDaysAsync(dto.StartDate, dto.EndDate, type);
        var available = await AvailableDaysAsync(dto.EmployeeId, type, dto.StartDate.Year);
        var docRequired = DocumentIsRequired(type, days);

        return new LeavePreviewDto
        {
            DaysRequested = days,
            CountsWorkingDaysOnly = type.CountsWorkingDaysOnly,
            DaysAvailable = available,
            // Types with no fixed entitlement (study, unpaid) are approved on their merits, not against a balance.
            SufficientBalance = type.DaysAllowed <= 0 || days <= available,
            DocumentRequired = docRequired,
            RequiredDocumentType = docRequired ? type.DocumentTypeRequired?.ToString() : null,
            ApprovalChain = BuildChain(type, days).Select(r => Label(r)).ToList(),
            Warning = type.DaysAllowed <= 0
                ? $"{type.Name} has no fixed entitlement — it is granted on its merits, so no balance is checked."
                : days > available ? $"Only {available} day(s) available." : null,
        };
    }

    public async Task<LeaveActionResult> CreateRequestAsync(CreateLeaveRequestDto dto, string? tenantSchema, string userId, string? userName)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId);
        if (employee is null) return Err("Employee not found.");
        if (employee.Status is EmploymentStatus.Resigned or EmploymentStatus.Terminated)
            return Err($"{employee.FullName} has left — leave cannot be applied for.");

        var type = await types.GetByIdAsync(dto.LeaveTypeId);
        if (type is null) return Err("Leave type not found.");
        if (!type.IsActive) return Err($"{type.Name} is no longer available.");

        if (dto.EndDate.Date < dto.StartDate.Date)
            return Err("The end date cannot be before the start date.");
        if (dto.ReturnDate is not null && dto.ReturnDate.Value.Date <= dto.EndDate.Date)
            return Err("The return date must be after the last day of leave.");

        var days = await CountDaysAsync(dto.StartDate, dto.EndDate, type);
        if (days <= 0)
            return Err(type.CountsWorkingDaysOnly
                ? "That range contains no working days — weekends and public holidays do not consume leave."
                : "That range covers no days.");

        // A second request over the same dates is a double-booking, not a top-up.
        var clash = await requests.Query()
            .Where(r => r.EmployeeId == employee.Id
                     && (r.Status == LeaveRequestStatus.Pending || r.Status == LeaveRequestStatus.Approved)
                     && r.StartDate.Date <= dto.EndDate.Date && r.EndDate.Date >= dto.StartDate.Date)
            .FirstOrDefaultAsync();
        if (clash is not null)
            return Err($"{employee.FullName} already has {clash.Status.ToString().ToLowerInvariant()} leave "
                     + $"({clash.LeaveTypeName}) from {clash.StartDate:yyyy-MM-dd} to {clash.EndDate:yyyy-MM-dd}.");

        // P5 step 5.1 gate — measured against days NOT already committed to a request in flight.
        if (type.DaysAllowed > 0)
        {
            var available = await AvailableDaysAsync(employee.Id, type, dto.StartDate.Year);
            if (days > available)
                return Err($"Insufficient balance: {days} day(s) requested, {available} available "
                         + $"(anything already awaiting approval is counted as committed).");
        }

        if (!string.IsNullOrWhiteSpace(dto.CoverEmployeeId) && dto.CoverEmployeeId == employee.Id)
            return Err("An employee cannot be their own cover.");
        var cover = string.IsNullOrWhiteSpace(dto.CoverEmployeeId) ? null : await employees.GetByIdAsync(dto.CoverEmployeeId);

        var docRequired = DocumentIsRequired(type, days);
        var chain = BuildChain(type, days);

        var request = await requests.CreateAsync(new LeaveRequest
        {
            RequestNumber = await NextRequestNumberAsync(),
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            DepartmentId = employee.DepartmentId,
            LeaveTypeId = type.Id,
            LeaveTypeCode = type.Code,
            LeaveTypeName = type.Name,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            ReturnDate = dto.ReturnDate?.Date,
            DaysRequested = days,
            Reason = dto.Reason,
            HandoverNotes = dto.HandoverNotes,
            CoverEmployeeId = cover?.Id,
            CoverEmployeeName = cover?.FullName,
            Status = LeaveRequestStatus.Pending,
            CurrentStep = 1,
            TotalSteps = chain.Count,
            SubmittedAt = DateTime.UtcNow,
            SubmittedBy = userId,
            DocumentRequired = docRequired,
            RequiredDocumentType = docRequired ? type.DocumentTypeRequired : null,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        // Every step written up front — the chain is visible before anyone acts on it.
        for (var i = 0; i < chain.Count; i++)
        {
            await approvals.CreateAsync(new LeaveApprovalLog
            {
                LeaveRequestId = request.Id,
                Step = i + 1,
                Role = chain[i],
                Action = LeaveApprovalAction.Pending,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
        }

        await LogAsync("LeaveRequest", request.Id, HrAuditAction.LeaveRequested,
            $"{request.RequestNumber}: {employee.EmployeeNumber} applied for {days} day(s) {type.Name} "
            + $"({dto.StartDate:yyyy-MM-dd} to {dto.EndDate:yyyy-MM-dd}); chain {ChainLabel(chain)}.", userId, userName);

        await NotifyAsync(tenantSchema, "LeaveRequest", "Warning",
            $"Leave approval needed — {employee.FullName} ({request.RequestNumber})",
            $"{employee.EmployeeNumber} applied for {days} day(s) {type.Name} from {dto.StartDate:d} to {dto.EndDate:d}. "
            + $"Waiting on {Label(chain[0])}."
            + (docRequired ? $" {WithArticle(type.DocumentTypeRequired?.ToString())} must be attached." : string.Empty),
            chain[0] == LeaveApprovalRole.LineManager ? "hr.manager" : "hr.approve", null);

        var note = docRequired
            ? $" {WithArticle(type.DocumentTypeRequired?.ToString())} is required before it can be approved."
            : string.Empty;
        return new LeaveActionResult("Created",
            $"{request.RequestNumber} submitted — {days} day(s), waiting on {Label(chain[0])}.{note}", request.Id);
    }

    /// <summary>
    /// P5 steps 5.4–5.5 — records one step's decision.
    /// <para>The entitlement is only touched on the FINAL approval, so a request that dies at HR never moved
    /// anybody's balance. A rejection closes the remaining steps as Skipped rather than leaving them Pending,
    /// which is what keeps "awaiting HR" counts honest.</para>
    /// </summary>
    public async Task<LeaveActionResult> DecideAsync(string requestId, DecideLeaveDto dto, string? tenantSchema, string userId, string? userName)
    {
        var request = await requests.GetByIdAsync(requestId);
        if (request is null) return Err("Leave request not found.");
        if (request.Status != LeaveRequestStatus.Pending)
            return Err($"This request is already {request.Status.ToString().ToLowerInvariant()}.");

        var approve = string.Equals(dto.Decision, "Approve", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(dto.Decision, "Approved", StringComparison.OrdinalIgnoreCase);
        var reject = string.Equals(dto.Decision, "Reject", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(dto.Decision, "Rejected", StringComparison.OrdinalIgnoreCase);
        if (!approve && !reject) return Err("The decision must be Approve or Reject.");
        if (reject && string.IsNullOrWhiteSpace(dto.Comments))
            return Err("Rejecting a leave request requires a reason on the record.");

        var steps = await approvals.Query().Where(a => a.LeaveRequestId == request.Id).OrderBy(a => a.Step).ToListAsync();
        var step = steps.FirstOrDefault(s => s.Step == request.CurrentStep);
        if (step is null) return Err("This request's approval chain is incomplete — it cannot be actioned.");

        var now = DateTime.UtcNow;

        // The document rule is enforced at the point of approval, not merely advertised at submission.
        if (approve && request.DocumentRequired
            && !await documents.Query().AnyAsync(d => d.LeaveRequestId == request.Id))
            return Err($"{Capitalise(WithArticle(request.RequiredDocumentType?.ToString()))} must be attached before this can be approved.");

        step.Action = approve ? LeaveApprovalAction.Approved : LeaveApprovalAction.Rejected;
        step.ApproverId = userId;
        step.ApproverName = userName;
        step.Comments = dto.Comments;
        step.ActionedAt = now;
        Touch(step, userId);
        await approvals.UpdateAsync(step);

        await LogAsync("LeaveRequest", request.Id, HrAuditAction.LeaveApprovalStepRecorded,
            $"{request.RequestNumber} step {step.Step} ({Label(step.Role)}) {step.Action.ToString().ToLowerInvariant()}"
            + (string.IsNullOrWhiteSpace(dto.Comments) ? "." : $": {dto.Comments}"), userId, userName);

        if (reject)
        {
            foreach (var later in steps.Where(s => s.Step > step.Step && s.Action == LeaveApprovalAction.Pending))
            {
                later.Action = LeaveApprovalAction.Skipped;
                later.Comments = $"Closed — rejected at step {step.Step} ({Label(step.Role)}).";
                Touch(later, userId);
                await approvals.UpdateAsync(later);
            }

            request.Status = LeaveRequestStatus.Rejected;
            request.RejectionReason = dto.Comments;
            request.DecidedAt = now;
            request.CurrentStep = 0;
            Touch(request, userId);
            await requests.UpdateAsync(request);

            await LogAsync("LeaveRequest", request.Id, HrAuditAction.LeaveRejected,
                $"{request.RequestNumber} rejected at {Label(step.Role)}: {dto.Comments}", userId, userName);
            await NotifyEmployeeAsync(request, tenantSchema, "Critical", $"Leave rejected — {request.RequestNumber}",
                $"Your {request.LeaveTypeName} request for {request.StartDate:d}–{request.EndDate:d} was rejected at {Label(step.Role)}: {dto.Comments}");

            return new LeaveActionResult("Rejected",
                $"{request.RequestNumber} rejected. No days were deducted.", request.Id);
        }

        // More tiers to go — hand on rather than settling anything.
        var next = steps.FirstOrDefault(s => s.Step == step.Step + 1);
        if (next is not null)
        {
            request.CurrentStep = next.Step;
            Touch(request, userId);
            await requests.UpdateAsync(request);

            await NotifyAsync(tenantSchema, "LeaveRequest", "Warning",
                $"Leave approval needed at {Label(next.Role)} — {request.EmployeeName} ({request.RequestNumber})",
                $"{request.EmployeeNumber}'s {request.DaysRequested} day(s) {request.LeaveTypeName} request was approved at "
                + $"{Label(step.Role)} and now needs {Label(next.Role)}.",
                next.Role == LeaveApprovalRole.LineManager ? "hr.manager" : "hr.approve", null);

            return new LeaveActionResult("Approved",
                $"Approved at {Label(step.Role)} — {request.RequestNumber} now needs {Label(next.Role)}.", request.Id);
        }

        // ── Final approval (P5 step 5.5) ──
        request.Status = LeaveRequestStatus.Approved;
        request.DecidedAt = now;
        request.CurrentStep = 0;
        Touch(request, userId);
        await requests.UpdateAsync(request);

        var deducted = await DeductAsync(request, userId);

        await LogAsync("LeaveRequest", request.Id, HrAuditAction.LeaveApproved,
            $"{request.RequestNumber} fully approved — {request.DaysRequested} day(s) {request.LeaveTypeName} "
            + $"({request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}). {deducted}", userId, userName);
        await NotifyEmployeeAsync(request, tenantSchema, "Warning", $"Leave approved — {request.RequestNumber}",
            $"Your {request.DaysRequested} day(s) {request.LeaveTypeName} from {request.StartDate:d} to {request.EndDate:d} is approved."
            + (request.ReturnDate is not null ? $" You are expected back on {request.ReturnDate:d}." : string.Empty));

        return new LeaveActionResult("Approved", $"{request.RequestNumber} approved. {deducted}", request.Id);
    }

    /// <summary>
    /// Withdraws a request. An already-approved one gives its days back — otherwise a cancelled holiday would
    /// silently cost the employee their entitlement.
    /// </summary>
    public async Task<LeaveActionResult> CancelAsync(string requestId, CancelLeaveDto dto, string userId)
    {
        var request = await requests.GetByIdAsync(requestId);
        if (request is null) return Err("Leave request not found.");
        if (request.Status is LeaveRequestStatus.Cancelled or LeaveRequestStatus.Rejected)
            return Err($"This request is already {request.Status.ToString().ToLowerInvariant()}.");

        var wasApproved = request.Status == LeaveRequestStatus.Approved;
        if (wasApproved && request.EndDate.Date < DateTime.UtcNow.Date)
            return Err("This leave has already been taken — it cannot be cancelled. Adjust the entitlement instead.");

        var now = DateTime.UtcNow;
        var restored = string.Empty;
        if (wasApproved)
        {
            var row = await FindEntitlementAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);
            if (row is not null)
            {
                row.DaysTaken = Math.Max(0, row.DaysTaken - request.DaysRequested);
                Touch(row, userId);
                await entitlements.UpdateAsync(row);
                restored = $" {request.DaysRequested} day(s) returned to the {row.Year} {row.LeaveTypeName} balance (now {row.DaysEntitled - row.DaysTaken}).";
            }
        }

        foreach (var s in await approvals.Query()
                     .Where(a => a.LeaveRequestId == request.Id && a.Action == LeaveApprovalAction.Pending).ToListAsync())
        {
            s.Action = LeaveApprovalAction.Skipped;
            s.Comments = "Closed — request cancelled.";
            Touch(s, userId);
            await approvals.UpdateAsync(s);
        }

        request.Status = LeaveRequestStatus.Cancelled;
        request.CancelledAt = now;
        request.CancellationReason = dto.Reason;
        request.CurrentStep = 0;
        Touch(request, userId);
        await requests.UpdateAsync(request);

        await LogAsync("LeaveRequest", request.Id, HrAuditAction.LeaveCancelled,
            $"{request.RequestNumber} cancelled{(string.IsNullOrWhiteSpace(dto.Reason) ? "" : $": {dto.Reason}")}.{restored}", userId, null);
        return new LeaveActionResult("Cancelled", $"{request.RequestNumber} cancelled.{restored}", request.Id);
    }

    /// <summary>P5 step 5.3a — the supporting document lands in the H1 vault keyed to this request (HR-DEC-6),
    /// so it is on the employee's file as well as on the application.</summary>
    public async Task<LeaveActionResult> AttachDocumentAsync(string requestId, AttachLeaveDocumentDto dto, string userId)
    {
        var request = await requests.GetByIdAsync(requestId);
        if (request is null) return Err("Leave request not found.");
        if (request.Status is LeaveRequestStatus.Rejected or LeaveRequestStatus.Cancelled)
            return Err($"This request is {request.Status.ToString().ToLowerInvariant()} — nothing more to attach.");
        if (string.IsNullOrWhiteSpace(dto.FileUrl)) return Err("A document needs a file URL.");

        var docType = request.RequiredDocumentType ?? EmployeeDocumentType.Other;
        if (!string.IsNullOrWhiteSpace(dto.DocumentType)
            && Enum.TryParse<EmployeeDocumentType>(dto.DocumentType, true, out var parsed))
            docType = parsed;

        var doc = await documents.CreateAsync(new EmployeeDocument
        {
            EmployeeId = request.EmployeeId,
            LeaveRequestId = request.Id,
            DocumentType = docType,
            DocumentName = dto.DocumentName ?? $"{Spaced(docType.ToString())} — {request.RequestNumber}",
            FileUrl = dto.FileUrl.Trim(),
            UploadedAt = DateTime.UtcNow,
            UploadedBy = userId,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        await LogAsync("LeaveRequest", request.Id, HrAuditAction.LeaveDocumentAttached,
            $"{Spaced(docType.ToString())} attached to {request.RequestNumber} for {request.EmployeeNumber}.", userId, null);
        return new LeaveActionResult("Attached",
            $"{Spaced(docType.ToString())} attached to {request.RequestNumber}. It is also on the employee's document vault, unverified.", doc.Id);
    }

    // ══ Carry-forward (P4 steps 4.3–4.5 / P6) ══
    public async Task<List<LeaveCarryForwardDto>> ListCarryForwardAsync(int? toYear, string? status)
    {
        var q = carryForwards.Query().AsNoTracking();
        if (toYear is not null) q = q.Where(c => c.ToYear == toYear);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CarryForwardStatus>(status, true, out var st))
            q = q.Where(c => c.Status == st);
        var list = await q.OrderByDescending(c => c.ToYear).ThenBy(c => c.EmployeeNumber).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    /// <summary>
    /// The leave half of the daily sweep: current-year entitlements, the 31 December carry-forward, and the
    /// 31 March expiry.
    /// <para><b>Catch-up rather than calendar-triggered.</b> The DFD says the carry-forward "runs on 31
    /// December"; a job that only fires when today happens to be 31 December never runs for a tenant whose
    /// service was restarting that evening, and the year's balances are then silently wrong. Instead the sweep
    /// asks whether the boundary has passed and no carry-forward rows exist for that year yet, so it lands
    /// correctly on 31 December and still self-heals on 4 January.</para>
    /// </summary>
    public async Task<LeaveSweepResultDto> RunLeaveSweepAsync(string? tenantSchema, string userId)
    {
        var today = DateTime.UtcNow.Date;
        var result = new LeaveSweepResultDto();

        var activeTypes = await types.Query().Where(t => t.IsActive && t.DaysAllowed > 0).ToListAsync();
        var staff = await employees.Query()
            .Where(e => e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated)
            .ToListAsync();

        // ── Current-year entitlements (P4 step 4.2) — also catches anyone hired since the last run ──
        if (activeTypes.Count > 0 && staff.Count > 0)
            result.EntitlementsAssigned = await AssignForAsync(staff, activeTypes, today.Year, userId);

        // ── Year-end carry-forward (P6 steps 6.1–6.4) ──
        // The boundary for year Y is 31 December Y: on that day Y is due, and on any day after it Y is overdue.
        var dueYear = today is { Month: 12, Day: 31 } ? today.Year : today.Year - 1;

        foreach (var type in activeTypes.Where(t => t.CarriesForward))
        {
            foreach (var employee in staff)
            {
                if (await carryForwards.Query().AnyAsync(c => c.EmployeeId == employee.Id
                                                           && c.LeaveTypeId == type.Id && c.FromYear == dueYear))
                    continue;   // already carried for that year — the row is the guard

                var row = await FindEntitlementAsync(employee.Id, type.Id, dueYear);
                if (row is null) continue;   // nothing was ever allocated for that year

                var balance = Math.Max(0, row.DaysEntitled - row.DaysTaken);
                var carried = Math.Min(balance, type.MaxCarryForwardDays);
                var forfeited = balance - carried;

                var record = await carryForwards.CreateAsync(new LeaveCarryForward
                {
                    EmployeeId = employee.Id,
                    EmployeeNumber = employee.EmployeeNumber,
                    EmployeeName = employee.FullName,
                    LeaveTypeId = type.Id,
                    LeaveTypeCode = type.Code,
                    FromYear = dueYear,
                    ToYear = dueYear + 1,
                    DaysCarried = carried,
                    DaysForfeited = forfeited,
                    ExpiryDate = new DateTime(dueYear + 1, CarryExpiryMonth, CarryExpiryDay, 0, 0, 0, DateTimeKind.Utc),
                    // A zero carry is closed immediately — there is nothing to expire later (P6 step 6.2c
                    // still wants the row, for completeness).
                    Status = carried > 0 ? CarryForwardStatus.Active : CarryForwardStatus.FullyUsed,
                    Notes = forfeited > 0
                        ? $"Balance {balance} exceeded the {type.MaxCarryForwardDays}-day cap — {forfeited} day(s) forfeited."
                        : carried == 0 ? "No balance to carry." : null,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                });
                result.CarryForwardRecordsWritten++;
                result.DaysCarried += carried;
                result.DaysForfeited += forfeited;

                // Close the old year so its remaining balance cannot be spent retroactively.
                row.ForfeitedDays += forfeited;
                row.DaysEntitled = row.DaysTaken + carried;
                row.Notes = Append(row.Notes, $"Year closed: {carried} carried to {dueYear + 1}, {forfeited} forfeited.");
                Touch(row, userId);
                await entitlements.UpdateAsync(row);

                // P6 step 6.4 — the new year's allowance includes the carried days.
                if (carried > 0)
                {
                    var target = await FindEntitlementAsync(employee.Id, type.Id, dueYear + 1);
                    if (target is not null)
                    {
                        target.CarriedForwardDays += carried;
                        target.DaysEntitled += carried;
                        target.Notes = Append(target.Notes, $"Includes {carried} day(s) carried from {dueYear}, expiring {record.ExpiryDate:yyyy-MM-dd}.");
                        Touch(target, userId);
                        await entitlements.UpdateAsync(target);
                    }
                }

                await LogAsync("LeaveCarryForward", record.Id, HrAuditAction.LeaveCarriedForward,
                    $"{employee.EmployeeNumber} {type.Code} {dueYear}: {carried} day(s) carried to {dueYear + 1} "
                    + $"(expires {record.ExpiryDate:yyyy-MM-dd}), {forfeited} forfeited.", userId, null);

                if (forfeited > 0)
                {
                    await LogAsync("LeaveCarryForward", record.Id, HrAuditAction.LeaveForfeited,
                        $"{employee.EmployeeNumber} forfeited {forfeited} day(s) of {dueYear} {type.Code} above the {type.MaxCarryForwardDays}-day cap.", userId, null);
                    await NotifyAsync(tenantSchema, "LeaveCarryForward", "Warning",
                        $"{forfeited} leave day(s) forfeited — {employee.FullName} ({dueYear})",
                        $"{employee.EmployeeNumber} ended {dueYear} with {balance} day(s) of {type.Name}. The cap is "
                        + $"{type.MaxCarryForwardDays}, so {carried} carried into {dueYear + 1} (use by {record.ExpiryDate:d}) and {forfeited} were forfeited.",
                        "hr.manager", employee.UserId);
                }
                else if (carried > 0)
                {
                    await NotifyAsync(tenantSchema, "LeaveCarryForward", "Warning",
                        $"{carried} leave day(s) carried into {dueYear + 1} — {employee.FullName}",
                        $"{employee.EmployeeNumber} carried {carried} day(s) of {type.Name} into {dueYear + 1}. "
                        + $"They must be used by {record.ExpiryDate:d} or they expire.",
                        "hr.manager", employee.UserId);
                }
            }
        }

        // ── 31 March expiry (P4 step 4.5 / P6 step 6.5) ──
        foreach (var row in await carryForwards.Query()
                     .Where(c => c.Status == CarryForwardStatus.Active && c.ExpiryDate < today).ToListAsync())
        {
            var target = await FindEntitlementAsync(row.EmployeeId, row.LeaveTypeId, row.ToYear);
            // Carried days are spent first, so what is left of them is the lesser of the days carried and the
            // balance still standing — anything more than that was this year's own allowance.
            var unused = target is null
                ? row.DaysCarried
                : Math.Max(0, Math.Min(row.DaysCarried, target.DaysEntitled - target.DaysTaken));

            row.DaysExpired = unused;
            row.Status = CarryForwardStatus.Expired;
            row.ExpiredAt = DateTime.UtcNow;
            row.Notes = Append(row.Notes, unused > 0
                ? $"{unused} of {row.DaysCarried} carried day(s) expired unused on {row.ExpiryDate:yyyy-MM-dd}."
                : "All carried days were used before expiry.");
            Touch(row, userId);
            await carryForwards.UpdateAsync(row);
            result.CarryForwardExpired++;
            result.DaysExpired += unused;

            if (unused > 0 && target is not null)
            {
                target.DaysEntitled -= unused;
                target.ForfeitedDays += unused;
                target.Notes = Append(target.Notes, $"{unused} carried day(s) expired on {row.ExpiryDate:yyyy-MM-dd}.");
                Touch(target, userId);
                await entitlements.UpdateAsync(target);
            }

            await LogAsync("LeaveCarryForward", row.Id, HrAuditAction.LeaveCarryForwardExpired,
                $"{row.EmployeeNumber} {row.LeaveTypeCode}: {unused} of {row.DaysCarried} carried day(s) expired on {row.ExpiryDate:yyyy-MM-dd}.", userId, null);

            if (unused > 0)
                await NotifyAsync(tenantSchema, "LeaveCarryForward", "Warning",
                    $"{unused} carried leave day(s) expired — {row.EmployeeName} ({row.ToYear})",
                    $"{row.EmployeeNumber} did not use {unused} day(s) carried from {row.FromYear}. They expired on "
                    + $"{row.ExpiryDate:d} and have been taken off the {row.ToYear} balance.",
                    "hr.manager", null);
        }

        result.Message =
            $"{result.EntitlementsAssigned} entitlement(s) assigned, {result.CarryForwardRecordsWritten} carry-forward record(s) written "
            + $"({result.DaysCarried} day(s) carried, {result.DaysForfeited} forfeited), "
            + $"{result.CarryForwardExpired} expired ({result.DaysExpired} day(s) lost).";
        return result;
    }

    // ══ Internals ══

    private async Task<int> AssignForAsync(List<Employee> staff, List<LeaveType> activeTypes, int year, string userId)
    {
        var created = 0;
        foreach (var employee in staff)
        {
            // Nobody earns an allowance for a year they had not joined — assigning 2025 leave to a 2026 hire
            // would hand them a balance to carry forward out of a year they never worked.
            if (employee.HireDate.Year > year) continue;

            foreach (var type in activeTypes)
            {
                if (await entitlements.Query().AnyAsync(e => e.EmployeeId == employee.Id
                                                          && e.LeaveTypeId == type.Id && e.Year == year))
                    continue;

                var (days, proRated) = EntitledDays(type, employee, year);
                var row = await entitlements.CreateAsync(new LeaveEntitlement
                {
                    EmployeeId = employee.Id,
                    EmployeeNumber = employee.EmployeeNumber,
                    EmployeeName = employee.FullName,
                    LeaveTypeId = type.Id,
                    LeaveTypeCode = type.Code,
                    LeaveTypeName = type.Name,
                    Year = year,
                    DaysEntitled = days,
                    DaysTaken = 0,
                    WasProRated = proRated,
                    Notes = proRated
                        ? $"Pro-rated for a {employee.HireDate:yyyy-MM-dd} start — {days} of {type.DaysAllowed} day(s)."
                        : null,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                });
                created++;

                await LogAsync("LeaveEntitlement", row.Id, HrAuditAction.LeaveEntitlementAssigned,
                    $"{employee.EmployeeNumber} allocated {days} day(s) {type.Code} for {year}"
                    + (proRated ? " (pro-rated for a mid-year start)." : "."), userId, null);
            }
        }
        return created;
    }

    /// <summary>
    /// H3-DEC-1 — a mid-year joiner's first year is pro-rated by the months they will actually serve, so a
    /// December hire gets ~2 days of annual leave rather than a full 21. Only the joining year is affected;
    /// every following year is the full entitlement.
    /// </summary>
    private static (decimal Days, bool ProRated) EntitledDays(LeaveType type, Employee employee, int year)
    {
        if (!type.ProRateFirstYear || employee.HireDate.Year != year || employee.HireDate.Month == 1)
            return (type.DaysAllowed, false);

        var monthsServed = 12 - employee.HireDate.Month + 1;
        var days = Math.Round(type.DaysAllowed * monthsServed / 12m, 1, MidpointRounding.AwayFromZero);
        return (days, true);
    }

    /// <summary>
    /// Days a new request can draw on: the derived balance less anything already committed to requests still
    /// working through a chain. Without the pending leg, two applications could each pass the balance gate.
    /// </summary>
    private async Task<decimal> AvailableDaysAsync(string employeeId, LeaveType type, int year)
    {
        var row = await FindEntitlementAsync(employeeId, type.Id, year);
        var balance = row is null ? 0 : row.DaysEntitled - row.DaysTaken;
        var committed = await requests.Query()
            .Where(r => r.EmployeeId == employeeId && r.LeaveTypeId == type.Id
                     && r.Status == LeaveRequestStatus.Pending && r.StartDate.Year == year)
            .SumAsync(r => (decimal?)r.DaysRequested) ?? 0;
        return balance - committed;
    }

    private Task<LeaveEntitlement?> FindEntitlementAsync(string employeeId, string leaveTypeId, int year)
        => entitlements.Query().FirstOrDefaultAsync(e => e.EmployeeId == employeeId
                                                      && e.LeaveTypeId == leaveTypeId && e.Year == year);

    private async Task<string> DeductAsync(LeaveRequest request, string userId)
    {
        var row = await FindEntitlementAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);
        if (row is null)
            return $"No {request.StartDate.Year} {request.LeaveTypeName} entitlement exists for this employee, "
                 + "so nothing was deducted — this type is granted on its merits.";

        row.DaysTaken += request.DaysRequested;
        Touch(row, userId);
        await entitlements.UpdateAsync(row);
        return $"{request.DaysRequested} day(s) deducted — {row.DaysEntitled - row.DaysTaken} of {row.DaysEntitled} left for {row.Year}.";
    }

    /// <summary>Committed days per (employee, type, year), used to fill DaysPending on the entitlement rows.</summary>
    private async Task<Dictionary<string, decimal>> PendingDaysByEntitlementAsync(List<LeaveEntitlement> rows)
    {
        if (rows.Count == 0) return [];
        var employeeIds = rows.Select(r => r.EmployeeId).Distinct().ToList();
        var pending = await requests.Query().AsNoTracking()
            .Where(r => r.Status == LeaveRequestStatus.Pending && employeeIds.Contains(r.EmployeeId))
            .Select(r => new { r.EmployeeId, r.LeaveTypeId, Year = r.StartDate.Year, r.DaysRequested })
            .ToListAsync();

        return pending
            .GroupBy(p => $"{p.EmployeeId}|{p.LeaveTypeId}|{p.Year}")
            .ToDictionary(g => g.Key, g => g.Sum(x => x.DaysRequested));
    }

    private static string Key(LeaveEntitlement e) => $"{e.EmployeeId}|{e.LeaveTypeId}|{e.Year}";

    /// <summary>
    /// Working days exclude the tenant's non-working weekdays AND public holidays, via the shared
    /// <see cref="IWorkCalendar"/> that attendance also uses — so a leave day is never charged for a day the
    /// attendance report says nobody was expected. (H3 originally excluded weekends only, with holidays deferred
    /// to H4 under H3-DEC-3; this is that debt paid.)
    /// <para>Calendar-day types (maternity's 90, paternity's 14) count every day, holidays included, because the
    /// Employment Act expresses them that way.</para>
    /// </summary>
    private async Task<decimal> CountDaysAsync(DateTime start, DateTime end, LeaveType type)
    {
        var from = start.Date;
        var to = end.Date;
        if (to < from) return 0;
        if (!type.CountsWorkingDaysOnly) return (decimal)(to - from).TotalDays + 1;
        return await calendar.CountWorkingDaysAsync(from, to);
    }

    private static bool DocumentIsRequired(LeaveType type, decimal days)
        => type.RequiresDocumentAfterDays is not null && days > type.RequiresDocumentAfterDays.Value;

    /// <summary>
    /// P5 step 5.4 — Line Manager always, HR when the type says so, the Board only when the type says so AND the
    /// request is longer than the type's threshold (QSL policy: study leave over 2 weeks).
    /// </summary>
    private static List<LeaveApprovalRole> BuildChain(LeaveType type, decimal days)
    {
        var chain = new List<LeaveApprovalRole> { LeaveApprovalRole.LineManager };
        if (type.RequiresHrApproval) chain.Add(LeaveApprovalRole.Hr);
        if (type.RequiresBoardApproval && days > type.BoardApprovalAfterDays)
        {
            if (!chain.Contains(LeaveApprovalRole.Hr)) chain.Add(LeaveApprovalRole.Hr);
            chain.Add(LeaveApprovalRole.Board);
        }
        return chain;
    }

    private static string? Validate(SaveLeaveTypeDto dto)
    {
        if (dto.DaysAllowed < 0) return "Days allowed cannot be negative.";
        if (dto.MaxCarryForwardDays < 0) return "The carry-forward cap cannot be negative.";
        if (dto.CarriesForward && dto.MaxCarryForwardDays <= 0)
            return "A type that carries forward needs a carry-forward cap above zero.";
        if (!dto.CarriesForward && dto.MaxCarryForwardDays > 0)
            return "Set 'carries forward' before giving it a carry-forward cap.";
        if (dto.DaysAllowed > 0 && dto.MaxCarryForwardDays > dto.DaysAllowed)
            return "The carry-forward cap cannot exceed the annual entitlement.";
        if (dto.FullPayDays is not null && dto.DaysAllowed > 0 && dto.FullPayDays > dto.DaysAllowed)
            return "Full-pay days cannot exceed the entitlement.";
        if (dto.RequiresDocumentAfterDays is < 0) return "The document threshold cannot be negative.";
        if (dto.RequiresDocumentAfterDays is not null && string.IsNullOrWhiteSpace(dto.DocumentTypeRequired))
            return "Say which document is required when a document threshold is set.";
        if (!string.IsNullOrWhiteSpace(dto.DocumentTypeRequired)
            && !Enum.TryParse<EmployeeDocumentType>(dto.DocumentTypeRequired, true, out _))
            return $"'{dto.DocumentTypeRequired}' is not a document type this vault holds.";
        if (dto.RequiresBoardApproval && dto.BoardApprovalAfterDays < 0)
            return "The board threshold cannot be negative.";
        return null;
    }

    private static void Apply(LeaveType e, SaveLeaveTypeDto dto)
    {
        e.Name = dto.Name.Trim();
        e.Description = dto.Description;
        e.DaysAllowed = dto.DaysAllowed;
        e.FullPayDays = dto.FullPayDays;
        e.IsPaid = dto.IsPaid;
        e.CarriesForward = dto.CarriesForward;
        e.MaxCarryForwardDays = dto.MaxCarryForwardDays;
        e.RequiresDocumentAfterDays = dto.RequiresDocumentAfterDays;
        e.DocumentTypeRequired = string.IsNullOrWhiteSpace(dto.DocumentTypeRequired)
            ? null
            : Enum.Parse<EmployeeDocumentType>(dto.DocumentTypeRequired, true);
        e.RequiresHrApproval = dto.RequiresHrApproval;
        e.RequiresBoardApproval = dto.RequiresBoardApproval;
        e.BoardApprovalAfterDays = dto.BoardApprovalAfterDays;
        e.CountsWorkingDaysOnly = dto.CountsWorkingDaysOnly;
        e.ProRateFirstYear = dto.ProRateFirstYear;
        e.DisplayOrder = dto.DisplayOrder;
    }

    private async Task<string> NextRequestNumberAsync()
    {
        var prefix = $"LV-{DateTime.UtcNow.Year}-";
        var count = await requests.Query().CountAsync(r => r.RequestNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task NotifyEmployeeAsync(LeaveRequest request, string? schema, string severity, string title, string message)
    {
        var userIdOfEmployee = await employees.Query().AsNoTracking()
            .Where(e => e.Id == request.EmployeeId).Select(e => e.UserId).FirstOrDefaultAsync();
        await NotifyAsync(schema, "LeaveRequest", severity, title, message, "hr.manager", userIdOfEmployee);
    }

    /// <summary>See the note on <c>ProbationService.NotifyAsync</c>: ticketing drops an incoming alert when an
    /// OPEN one shares its (tenant, source, title), so every tier and every event needs a distinct title. The
    /// request number in each title is what keeps two employees' — or one employee's two — requests apart.</summary>
    private async Task NotifyAsync(string? schema, string source, string severity, string title, string message,
        string? permission, string? assignedToUserId)
    {
        if (string.IsNullOrWhiteSpace(schema)) return;
        await notifier.CreateAlertAsync(schema, source, severity, title, message, permission, assignedToUserId);
    }

    private static LeaveTypeDto ToDto(LeaveType t) => new()
    {
        Id = t.Id, Code = t.Code, Name = t.Name, Description = t.Description,
        DaysAllowed = t.DaysAllowed, FullPayDays = t.FullPayDays, IsPaid = t.IsPaid,
        CarriesForward = t.CarriesForward, MaxCarryForwardDays = t.MaxCarryForwardDays,
        RequiresDocumentAfterDays = t.RequiresDocumentAfterDays,
        DocumentTypeRequired = t.DocumentTypeRequired?.ToString(),
        RequiresHrApproval = t.RequiresHrApproval, RequiresBoardApproval = t.RequiresBoardApproval,
        BoardApprovalAfterDays = t.BoardApprovalAfterDays,
        CountsWorkingDaysOnly = t.CountsWorkingDaysOnly, ProRateFirstYear = t.ProRateFirstYear,
        IsActive = t.IsActive, DisplayOrder = t.DisplayOrder,
        ApprovalChain = ChainLabel(t),
    };

    private static LeaveEntitlementDto ToDto(LeaveEntitlement e, decimal pending)
    {
        var balance = e.DaysEntitled - e.DaysTaken;
        return new LeaveEntitlementDto
        {
            Id = e.Id, EmployeeId = e.EmployeeId, EmployeeNumber = e.EmployeeNumber, EmployeeName = e.EmployeeName,
            LeaveTypeId = e.LeaveTypeId, LeaveTypeCode = e.LeaveTypeCode, LeaveTypeName = e.LeaveTypeName,
            Year = e.Year, DaysEntitled = e.DaysEntitled, DaysTaken = e.DaysTaken,
            CarriedForwardDays = e.CarriedForwardDays, ForfeitedDays = e.ForfeitedDays,
            WasProRated = e.WasProRated, Notes = e.Notes,
            DaysBalance = balance, DaysPending = pending, DaysAvailable = balance - pending,
        };
    }

    private static LeaveRequestDto ToDto(LeaveRequest r, List<LeaveApprovalLog> steps, bool documentAttached)
    {
        var today = DateTime.UtcNow.Date;
        return new LeaveRequestDto
        {
            Id = r.Id, RequestNumber = r.RequestNumber,
            EmployeeId = r.EmployeeId, EmployeeNumber = r.EmployeeNumber, EmployeeName = r.EmployeeName,
            LeaveTypeId = r.LeaveTypeId, LeaveTypeCode = r.LeaveTypeCode, LeaveTypeName = r.LeaveTypeName,
            StartDate = r.StartDate, EndDate = r.EndDate, ReturnDate = r.ReturnDate,
            DaysRequested = r.DaysRequested, Reason = r.Reason, HandoverNotes = r.HandoverNotes,
            CoverEmployeeId = r.CoverEmployeeId, CoverEmployeeName = r.CoverEmployeeName,
            Status = r.Status.ToString(), CurrentStep = r.CurrentStep, TotalSteps = r.TotalSteps,
            SubmittedAt = r.SubmittedAt, DecidedAt = r.DecidedAt,
            RejectionReason = r.RejectionReason, CancellationReason = r.CancellationReason,
            DocumentRequired = r.DocumentRequired, RequiredDocumentType = r.RequiredDocumentType?.ToString(),
            DocumentAttached = documentAttached,
            AwaitingRole = r.Status == LeaveRequestStatus.Pending
                ? steps.FirstOrDefault(s => s.Step == r.CurrentStep)?.Role.ToString()
                : null,
            IsCurrentlyOnLeave = r.Status == LeaveRequestStatus.Approved
                              && r.StartDate.Date <= today && r.EndDate.Date >= today,
            ApprovalChain = steps.OrderBy(s => s.Step).Select(s => new LeaveApprovalStepDto
            {
                Id = s.Id, Step = s.Step, Role = s.Role.ToString(), Action = s.Action.ToString(),
                ApproverId = s.ApproverId, ApproverName = s.ApproverName, Comments = s.Comments, ActionedAt = s.ActionedAt,
            }).ToList(),
        };
    }

    private static LeaveCarryForwardDto ToDto(LeaveCarryForward c) => new()
    {
        Id = c.Id, EmployeeId = c.EmployeeId, EmployeeNumber = c.EmployeeNumber, EmployeeName = c.EmployeeName,
        LeaveTypeCode = c.LeaveTypeCode, FromYear = c.FromYear, ToYear = c.ToYear,
        DaysCarried = c.DaysCarried, DaysForfeited = c.DaysForfeited, DaysExpired = c.DaysExpired,
        ExpiryDate = c.ExpiryDate, Status = c.Status.ToString(), ExpiredAt = c.ExpiredAt, Notes = c.Notes,
        DaysToExpiry = (int)(c.ExpiryDate.Date - DateTime.UtcNow.Date).TotalDays,
    };

    private static string Label(LeaveApprovalRole role) => role switch
    {
        LeaveApprovalRole.LineManager => "Line Manager",
        LeaveApprovalRole.Hr => "HR",
        _ => "Board",
    };

    private static string ChainLabel(List<LeaveApprovalRole> chain) => string.Join(" → ", chain.Select(Label));

    private static string ChainLabel(LeaveType t)
    {
        var label = "Line Manager";
        if (t.RequiresHrApproval) label += " → HR";
        if (t.RequiresBoardApproval)
        {
            if (!t.RequiresHrApproval) label += " → HR";
            label += $" → Board (over {t.BoardApprovalAfterDays} days)";
        }
        return label;
    }

    /// <summary>"MedicalCertificate" → "medical certificate", for messages meant for people.</summary>
    private static string Spaced(string? pascal)
    {
        if (string.IsNullOrWhiteSpace(pascal)) return "document";
        var chars = pascal.SelectMany((c, i) => i > 0 && char.IsUpper(c) ? [' ', char.ToLowerInvariant(c)] : new[] { char.ToLowerInvariant(c) });
        return new string(chars.ToArray());
    }

    /// <summary>"institution letter" → "an institution letter". The document names are data, so the article
    /// cannot be baked into the message string.</summary>
    private static string WithArticle(string? pascal)
    {
        var phrase = Spaced(pascal);
        return $"{("aeiou".Contains(phrase[0]) ? "an" : "a")} {phrase}";
    }

    private static string Capitalise(string phrase)
        => string.IsNullOrEmpty(phrase) ? phrase : char.ToUpperInvariant(phrase[0]) + phrase[1..];

    private static string Append(string? existing, string addition)
        => string.IsNullOrWhiteSpace(existing) ? addition : $"{existing} {addition}";

    private static LeaveActionResult Err(string message) => new("Error", message);
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
