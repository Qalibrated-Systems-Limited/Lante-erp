using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Payroll;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H6 (P9 + P12) — overtime pre-approval and the monthly payroll engine.
/// <para><b>Every payslip figure is derived, never entered.</b> The inputs are the employee's salary
/// assignment, the structure's components, the dated PAYE and statutory tables, approved overtime and H4's
/// unpaid days. That is what makes a run reproducible: recomputing the same period from the same inputs must
/// produce the same payslips.</para>
/// <para><b>Recompute is safe by construction.</b> A run claims overtime and unpaid absences by stamping them
/// with its own id; recomputing releases exactly what it stamped before it starts again. Without that, a
/// second compute would pay the same overtime twice — the single most likely way a payroll engine defrauds
/// somebody, in either direction.</para>
/// <para><b>The journal is summed from the payslip lines</b>, not calculated a second time. Two independent
/// calculations of the same thing eventually disagree, and the one that reaches the ledger is the one nobody
/// checked.</para>
/// <para><b>Historical correctness.</b> Salary lookup takes the assignment in force for the period being run —
/// including a superseded one — and rates are read as at the period end. Re-running March next year uses
/// March's salary and March's tax bands.</para>
/// </summary>
public class PayrollRunService(
    IGenericRepository<Employee> employees,
    IGenericRepository<EmployeeSalary> salaries,
    IGenericRepository<SalaryStructure> structures,
    IGenericRepository<SalaryComponent> components,
    IGenericRepository<PayrollPeriod> periods,
    IGenericRepository<PayeTaxBand> payeBands,
    IGenericRepository<StatutoryRate> statutoryRates,
    IGenericRepository<PayrollDeduction> deductions,
    IGenericRepository<PayrollDeductionType> deductionTypes,
    IGenericRepository<OvertimeRequest> overtime,
    IGenericRepository<CommissionStatement> commissionStatements,
    IGenericRepository<AbsenceRecord> absences,
    IGenericRepository<PayrollRun> runs,
    IGenericRepository<Payslip> payslips,
    IGenericRepository<PayslipLine> payslipLines,
    IGenericRepository<HrAuditLog> audit,
    IWorkCalendar calendar,
    IFinanceGateway finance,
    IHrAlertGateway notifier) : IPayrollRunService
{
    private static readonly EmploymentStatus[] OnPayroll =
        [EmploymentStatus.OnProbation, EmploymentStatus.Active, EmploymentStatus.OnLeave, EmploymentStatus.Suspended];

    /// <summary>Employment Act 2007 overtime multipliers.</summary>
    private const decimal WeekdayMultiplier = 1.5m, RestDayMultiplier = 2.0m;

    /// <summary>Hours in a working day, for the overtime hourly rate (P12 design note:
    /// hourly = basic ÷ (working days × 8)).</summary>
    private const decimal HoursPerWorkingDay = 8m;

    /// <summary>The holding account net pay sits in between run approval and the bank payment
    /// (DEC: two journals via 2110). Posting straight to the bank at approval would claim the money had left
    /// before the file was even generated.</summary>
    private const string GlNetPayHolding = "2110";

    // ══════════════════════════════════════════════════════════════════════════════
    // Overtime (P12)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<OvertimeRequestDto>> ListOvertimeAsync(string? employeeId, string? status, DateTime? from, DateTime? to)
    {
        var q = overtime.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(o => o.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OvertimeStatus>(status, true, out var st))
            q = q.Where(o => o.Status == st);
        if (from.HasValue) q = q.Where(o => o.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(o => o.Date <= to.Value.Date);

        var list = await q.OrderByDescending(o => o.Date).ThenBy(o => o.EmployeeName).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<PayrollActionResult> RequestOvertimeAsync(RequestOvertimeDto dto, string userId)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId ?? string.Empty);
        if (employee is null || employee.IsDeleted) return Err("Employee not found.");
        if (!OnPayroll.Contains(employee.Status))
            return Err($"{employee.FullName} is {employee.Status} and is no longer on the payroll.");
        if (dto.Hours <= 0) return Err("Overtime must be more than zero hours.");
        if (dto.Hours > 24) return Err("A single day cannot carry more than 24 hours of overtime.");

        var date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);
        if (date < employee.HireDate.Date) return Err($"{date:dd MMM yyyy} is before {employee.FullName} was hired.");

        // ATT-007: pre-approval means BEFORE the work. A claim for a day already worked defeats the control —
        // the manager can no longer decide whether the cost was worth incurring.
        if (date < DateTime.UtcNow.Date)
            return Err("Overtime must be approved before it is worked — a retrospective claim cannot be raised here.");

        if (await overtime.Query().AnyAsync(o => o.EmployeeId == employee.Id && o.Date == date))
            return Err($"{employee.FullName} already has an overtime request for {date:dd MMM yyyy}.");

        // The rate follows the CALENDAR, not the requester: a Sunday or a gazetted holiday is a fact.
        var isWorkingDay = await calendar.IsWorkingDayAsync(date);
        var rateType = isWorkingDay ? OvertimeRateType.Weekday : OvertimeRateType.RestDayOrHoliday;
        var multiplier = isWorkingDay ? WeekdayMultiplier : RestDayMultiplier;

        var created = await overtime.CreateAsync(new OvertimeRequest
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            Date = date,
            Hours = dto.Hours,
            RateType = rateType,
            Multiplier = multiplier,
            Reason = dto.Reason,
            RequestedBy = userId,
            RequestedAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });

        await LogAsync("OvertimeRequest", created.Id, HrAuditAction.OvertimeRequested,
            $"{employee.EmployeeNumber}: {dto.Hours:0.##} h on {date:dd MMM yyyy} at {multiplier:0.#}x — awaiting approval.", userId);
        return new PayrollActionResult("Requested",
            $"{dto.Hours:0.##} h requested for {date:dd MMM yyyy} at {multiplier:0.#}x — awaiting the line manager.", created.Id);
    }

    public async Task<PayrollActionResult> DecideOvertimeAsync(string id, DecideOvertimeDto dto, string userId, string? userName)
    {
        var request = await overtime.GetByIdAsync(id);
        if (request is null || request.IsDeleted) return Err("Overtime request not found.");
        if (request.Status != OvertimeStatus.Pending)
            return Err($"That request is already {request.Status.ToString().ToLowerInvariant()} — only a pending one can be decided.");

        var decision = (dto.Decision ?? string.Empty).Trim();
        var approve = decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        var reject = decision.Equals("Reject", StringComparison.OrdinalIgnoreCase);
        if (!approve && !reject) return Err("The decision must be Approve or Reject.");
        if (reject && string.IsNullOrWhiteSpace(dto.Reason)) return Err("A rejection needs a reason.");

        // The requester must not approve their own overtime — the same second-officer rule the salary
        // workflow uses, and for the same reason: this one costs the company money.
        if (approve && !string.IsNullOrWhiteSpace(request.RequestedBy) && request.RequestedBy == userId)
            return Err("The person who requested overtime cannot approve it — it needs the line manager.");

        request.Status = approve ? OvertimeStatus.Approved : OvertimeStatus.Rejected;
        request.DecidedBy = userId;
        request.DecidedAt = DateTime.UtcNow;
        request.DecisionReason = dto.Reason?.Trim();
        Touch(request, userId);
        await overtime.UpdateAsync(request);

        await LogAsync("OvertimeRequest", request.Id,
            approve ? HrAuditAction.OvertimeApproved : HrAuditAction.OvertimeRejected,
            $"{request.EmployeeNumber}: {request.Hours:0.##} h on {request.Date:dd MMM yyyy} {decision.ToLowerInvariant()}d by {userName ?? userId}." +
            (string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" {dto.Reason!.Trim()}"), userId, userName);

        return new PayrollActionResult(approve ? "Approved" : "Rejected",
            approve
                ? $"{request.Hours:0.##} h approved for {request.EmployeeName} — the next payroll run will pay it at {request.Multiplier:0.#}x."
                : $"Overtime for {request.EmployeeName} rejected.", request.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Payroll runs (P9)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<PayrollRunDto>> ListRunsAsync(string? status)
    {
        var q = runs.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PayrollRunStatus>(status, true, out var st))
            q = q.Where(r => r.Status == st);
        var list = await q.OrderByDescending(r => r.PayrollPeriodCode).ToListAsync();
        return list.Select(r => ToDto(r, [])).ToList();
    }

    public async Task<PayrollRunDto?> GetRunAsync(string id)
    {
        var run = await runs.Query().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (run is null) return null;

        var slips = await payslips.Query().AsNoTracking().Where(p => p.PayrollRunId == id)
            .OrderBy(p => p.EmployeeNumber).ToListAsync();
        var ids = slips.Select(p => p.Id).ToList();
        var lines = ids.Count == 0 ? [] : await payslipLines.Query().AsNoTracking()
            .Where(l => ids.Contains(l.PayslipId)).ToListAsync();

        return ToDto(run, slips.Select(p => ToDto(p, lines.Where(l => l.PayslipId == p.Id).ToList())).ToList());
    }

    public async Task<List<PayslipDto>> ListPayslipsAsync(string? runId, string? employeeId, int? year)
    {
        var q = payslips.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(runId)) q = q.Where(p => p.PayrollRunId == runId);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(p => p.EmployeeId == employeeId);
        if (year.HasValue) q = q.Where(p => p.PayrollPeriodCode.StartsWith(year.Value.ToString("D4")));

        var slips = await q.OrderBy(p => p.PayrollPeriodCode).ThenBy(p => p.EmployeeNumber).ToListAsync();
        if (slips.Count == 0) return [];

        var ids = slips.Select(p => p.Id).ToList();
        var lines = await payslipLines.Query().AsNoTracking().Where(l => ids.Contains(l.PayslipId)).ToListAsync();
        return slips.Select(p => ToDto(p, lines.Where(l => l.PayslipId == p.Id).ToList())).ToList();
    }

    public async Task<PayrollActionResult> CreateRunAsync(CreatePayrollRunDto dto, string userId)
    {
        var period = string.IsNullOrWhiteSpace(dto.PayrollPeriodId)
            ? await FindCurrentPeriodAsync()
            : await periods.GetByIdAsync(dto.PayrollPeriodId!);
        if (period is null || period.IsDeleted)
            return Err(string.IsNullOrWhiteSpace(dto.PayrollPeriodId)
                ? "No open payroll period covers today — generate this year's periods first."
                : "Payroll period not found.");
        if (period.Status == PayrollPeriodStatus.Closed)
            return Err($"Payroll period {period.Code} is closed — it has already been paid and posted.");

        // P9 step 9.1 — the duplicate check. The database enforces this too; catching it here says why.
        var existing = await runs.Query()
            .FirstOrDefaultAsync(r => r.PayrollPeriodId == period.Id && r.Status != PayrollRunStatus.Cancelled);
        if (existing is not null)
            return Err($"{existing.RunNumber} already exists for {period.Code} and is {existing.Status.ToString().ToLowerInvariant()}. Cancel it before starting another.");

        var created = await runs.CreateAsync(new PayrollRun
        {
            RunNumber = $"PR-{period.Code}",
            PayrollPeriodId = period.Id,
            PayrollPeriodCode = period.Code,
            PeriodStart = period.StartDate,
            PeriodEnd = period.EndDate,
            CutOffDate = period.CutOffDate,
            Notes = dto.Notes,
            CreatedBy = userId, UpdatedBy = userId,
        });

        await LogAsync("PayrollRun", created.Id, HrAuditAction.PayrollRunCreated,
            $"Payroll run {created.RunNumber} opened for {period.Code}.", userId);
        return new PayrollActionResult("Created", $"{created.RunNumber} opened for {period.Code}. Compute it to build the payslips.", created.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // The engine
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<PayrollActionResult> ComputeRunAsync(string id, string userId)
    {
        var run = await runs.GetByIdAsync(id);
        if (run is null || run.IsDeleted) return Err("Payroll run not found.");
        if (run.Status is PayrollRunStatus.Approved or PayrollRunStatus.Cancelled)
            return Err($"{run.RunNumber} is {run.Status.ToString().ToLowerInvariant()} — its figures are frozen.");

        var period = await periods.GetByIdAsync(run.PayrollPeriodId);
        if (period is null) return Err("The run's payroll period no longer exists.");

        // ── Release anything a previous compute claimed, so a recompute cannot double-count ──
        await ReleaseClaimsAsync(run.Id, userId);
        var priorSlips = await payslips.Query().Where(p => p.PayrollRunId == run.Id).ToListAsync();
        foreach (var slip in priorSlips) await payslips.DeleteAsync(slip);

        // ── Load every input once ──
        var asOf = run.PeriodEnd.Date;
        var staff = await employees.Query().AsNoTracking()
            .Where(e => OnPayroll.Contains(e.Status) && e.HireDate <= run.PeriodEnd)
            .OrderBy(e => e.EmployeeNumber).ToListAsync();
        if (staff.Count == 0) return Err("No employees are on the payroll for this period.");

        var allSalaries = await salaries.Query().AsNoTracking()
            .Where(s => s.Status == SalaryAssignmentStatus.Approved || s.Status == SalaryAssignmentStatus.Superseded)
            .ToListAsync();
        var allStructures = await structures.Query().AsNoTracking().ToListAsync();
        var allComponents = await components.Query().AsNoTracking().Where(c => c.IsActive).ToListAsync();

        var bands = InForce(await payeBands.Query().AsNoTracking().Where(b => b.IsActive).ToListAsync(), asOf,
                b => b.EffectiveFrom, b => b.EffectiveTo)
            .OrderBy(b => b.BandOrder).ToList();
        if (bands.Count == 0)
            return Err("No PAYE tax bands are in force for this period — payroll cannot compute tax without them.");

        var rates = InForce(await statutoryRates.Query().AsNoTracking().Where(r => r.IsActive).ToListAsync(), asOf,
            r => r.EffectiveFrom, r => r.EffectiveTo);
        var relief = rates.FirstOrDefault(r => r.RateType == StatutoryRateType.FixedAmount
                                            && r.Component == StatutoryComponent.Paye)?.FixedAmount ?? 0m;
        // The PAYE rate is carried separately from computedRates below, which excludes FixedAmount and so
        // excludes the only Component==Paye row the design creates. Every other statutory deduction takes
        // its posting account from its own StatutoryRate; PAYE could not, which left a dummy salary
        // component as the only way to map it and made runs unapprovable without one (#254).
        var payeRate = rates.FirstOrDefault(r => r.Component == StatutoryComponent.Paye);
        // PerEmployeeAmount rules (HELB) are declarative — the figure comes from the employee's own deduction.
        var computedRates = rates.Where(r => r.RateType != StatutoryRateType.FixedAmount
                                          && r.RateType != StatutoryRateType.PerEmployeeAmount).ToList();

        var liveDeductions = await deductions.Query().AsNoTracking().Where(d => d.IsActive).ToListAsync();
        var dTypes = await deductionTypes.Query().AsNoTracking().ToListAsync();

        var approvedOt = await overtime.Query()
            .Where(o => o.Status == OvertimeStatus.Approved && o.PayrollRunId == null
                     && o.Date >= run.PeriodStart && o.Date <= run.PeriodEnd)
            .ToListAsync();

        // H11/COM-005 — approved commission for this period, not yet paid by any run. Same claim-and-release
        // shape as overtime, so it is taxed correctly and cannot be paid twice.
        var approvedCommission = await commissionStatements.Query()
            .Where(c => c.Status == CommissionStatementStatus.Approved
                     && c.PayrollPeriodId == run.PayrollPeriodId && c.PayrollRunId == null)
            .ToListAsync();

        var unpaid = await absences.Query()
            .Where(a => a.UnpaidDays > 0 && a.ReleasedToPayrollAt == null
                     && a.Date >= run.PeriodStart && a.Date <= run.PeriodEnd)
            .ToListAsync();

        var workingDays = await calendar.CountWorkingDaysAsync(run.PeriodStart, run.PeriodEnd);
        if (workingDays <= 0) return Err("The period contains no working days — check the holiday calendar.");

        // ── Compute ──
        var exclusions = new List<string>();
        var built = new List<Payslip>();

        foreach (var employee in staff)
        {
            var salary = CurrentSalaryFor(allSalaries, employee.Id, run.PayrollPeriodCode);
            if (salary is null) { exclusions.Add($"{employee.EmployeeNumber} {employee.FullName} — no approved salary effective by {run.PayrollPeriodCode}."); continue; }

            var structure = allStructures.FirstOrDefault(s => s.Id == salary.SalaryStructureId);
            if (structure is null) { exclusions.Add($"{employee.EmployeeNumber} {employee.FullName} — salary structure missing."); continue; }

            var structureComponents = allComponents.Where(c => c.SalaryStructureId == structure.Id)
                .OrderBy(c => c.ComponentOrder).ToList();

            var slip = BuildPayslip(
                run, employee, salary, structure, structureComponents,
                approvedOt.Where(o => o.EmployeeId == employee.Id).ToList(),
                unpaid.Where(a => a.EmployeeId == employee.Id).ToList(),
                approvedCommission.Where(c => c.EmployeeId == employee.Id).ToList(),
                liveDeductions.Where(d => d.EmployeeId == employee.Id
                                       && InPeriod(d, run.PayrollPeriodCode)).ToList(),
                dTypes, bands, computedRates, relief, payeRate, workingDays, userId);

            built.Add(slip);
        }

        if (built.Count == 0)
            return Err("Nothing to pay: no employee on the payroll has an approved salary for this period.");

        // ── Persist payslips, then stamp the claims they consumed ──
        // The lines ride in on the payslip's own navigation collection; inserting them a second time through
        // the line repository would collide on the primary key.
        //
        // Concurrent ComputeRunAsync calls for the same run both pass the status/claim checks above
        // before either commits here — the unique (PayrollRunId, EmployeeId) index on Payslip is what
        // actually stops a double-pay, but without this catch the loser gets a raw 500 instead of a
        // clear "someone else is already computing this" message.
        try
        {
            foreach (var slip in built) await payslips.CreateAsync(slip);
        }
        catch (DbUpdateException)
        {
            return Err($"{run.RunNumber} is already being computed by another request — please retry.");
        }

        var paidEmployeeIds = built.Select(b => b.EmployeeId).ToHashSet();
        foreach (var ot in approvedOt.Where(o => paidEmployeeIds.Contains(o.EmployeeId)))
        {
            ot.PayrollRunId = run.Id;
            ot.Status = OvertimeStatus.Paid;
            ot.PaidAmount = built.First(b => b.EmployeeId == ot.EmployeeId).OvertimePay > 0
                ? Round(ot.Hours * HourlyRate(built.First(b => b.EmployeeId == ot.EmployeeId).BasicSalary, workingDays) * ot.Multiplier)
                : 0m;
            Touch(ot, userId);
            await overtime.UpdateAsync(ot);
        }
        foreach (var c in approvedCommission.Where(c => paidEmployeeIds.Contains(c.EmployeeId)))
        {
            c.PayrollRunId = run.Id;
            c.PaidAmount = c.CommissionAmount;
            c.Status = CommissionStatementStatus.Paid;
            Touch(c, userId);
            await commissionStatements.UpdateAsync(c);
            // COM-005 — the moment a commission statement stops being a promise and becomes pay. The commission
            // module cannot log this itself: paying it is the payroll run's act, not the statement's.
            await LogAsync("CommissionStatement", c.Id, HrAuditAction.CommissionStatementPaid,
                $"{c.StatementNumber}: {c.CommissionAmount:N2} paid through payroll run {run.RunNumber}.", userId);
        }
        foreach (var absence in unpaid.Where(a => paidEmployeeIds.Contains(a.EmployeeId)))
        {
            absence.ReleasedToPayrollAt = DateTime.UtcNow;
            absence.PayrollRunId = run.Id;
            Touch(absence, userId);
            await absences.UpdateAsync(absence);
        }

        // ── Totals ──
        run.EmployeeCount = built.Count;
        run.TotalGross = Round(built.Sum(p => p.GrossPay));
        run.TotalTaxable = Round(built.Sum(p => p.TaxableIncome));
        run.TotalPaye = Round(built.Sum(p => p.Paye));
        run.TotalStatutory = Round(built.Sum(p => p.StatutoryDeductions));
        run.TotalOtherDeductions = Round(built.Sum(p => p.OtherDeductions));
        run.TotalDeductions = Round(built.Sum(p => p.TotalDeductions));
        run.TotalNet = Round(built.Sum(p => p.NetPay));
        run.TotalEmployerCost = Round(built.Sum(p => p.EmployerCost));
        run.CurrencyCode = built[0].CurrencyCode;
        run.Status = PayrollRunStatus.Computed;
        run.ComputedBy = userId;
        run.ComputedAt = DateTime.UtcNow;
        run.Exclusions = exclusions.Count == 0 ? null : string.Join(" | ", exclusions);
        Touch(run, userId);
        await runs.UpdateAsync(run);

        // Freeze the configuration while a run sits on it, so the figures cannot shift underneath the approval.
        if (period.Status == PayrollPeriodStatus.Open)
        {
            period.Status = PayrollPeriodStatus.Locked;
            period.LockedAt = DateTime.UtcNow;
            period.LockedBy = userId;
            Touch(period, userId);
            await periods.UpdateAsync(period);
        }

        var result = new PayrollActionResult("Computed",
            $"{run.RunNumber}: {built.Count} payslip(s), gross {Money(run.TotalGross, run.CurrencyCode)}, net {Money(run.TotalNet, run.CurrencyCode)}.", run.Id);
        if (exclusions.Count > 0)
            result.Warnings.Add($"{exclusions.Count} employee(s) were left out — {string.Join("; ", exclusions.Take(5))}{(exclusions.Count > 5 ? " …" : "")}");
        var unmapped = built.SelectMany(b => b.Lines).Count(l => string.IsNullOrWhiteSpace(l.GlAccountId));
        if (unmapped > 0)
            result.Warnings.Add($"{unmapped} payslip line(s) have no GL account — the journal cannot post until they are mapped.");
        // `rates`, not `computedRates`. The filtered list drops FixedAmount and PerEmployeeAmount — which
        // is personal relief and HELB. Relief applies to every employee on every run and is exactly what a
        // Finance Act changes, so checking the filtered list meant the one figure most likely to be stale
        // could never raise the warning: NeedsConfirmation was set on it and never read. Part of #244.
        var unconfirmed = bands.Where(b => b.NeedsConfirmation).Select(b => $"PAYE band {b.BandOrder}")
            .Concat(rates.Where(r => r.NeedsConfirmation).Select(r => r.Code))
            .Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (unconfirmed.Count > 0)
            result.Warnings.Add($"This run used rates that have never been checked against the current Finance Act: {string.Join(", ", unconfirmed)}.");
        result.Warnings.Add($"{run.PayrollPeriodCode} is now locked — salary and deduction changes for it are frozen until the run is approved or cancelled.");

        // The unconfirmed codes belong in the audit entry, not only in a warning string on the HTTP response
        // the computing officer sees once. Segregation of duties guarantees a DIFFERENT person approves, and
        // the audit trail is what that person and any later auditor actually read (#244).
        await LogAsync("PayrollRun", run.Id, HrAuditAction.PayrollRunComputed,
            $"{run.RunNumber} computed: {built.Count} payslip(s), gross {run.TotalGross:N2}, PAYE {run.TotalPaye:N2}, net {run.TotalNet:N2}, employer cost {run.TotalEmployerCost:N2}. {exclusions.Count} excluded."
            + (unconfirmed.Count > 0
                ? $" Computed from UNVERIFIED statutory rates: {string.Join(", ", unconfirmed)}."
                : string.Empty), userId);
        return result;
    }

    /// <summary>
    /// One employee's pay for one period. Kept as a single pure-ish pass so the whole calculation can be read
    /// top to bottom in the order money actually moves: earn, then contribute, then be taxed, then be deducted.
    /// </summary>
    private Payslip BuildPayslip(
        PayrollRun run, Employee employee, EmployeeSalary salary, SalaryStructure structure,
        List<SalaryComponent> structureComponents, List<OvertimeRequest> employeeOt,
        List<AbsenceRecord> employeeUnpaid, List<CommissionStatement> employeeCommission,
        List<PayrollDeduction> employeeDeductions,
        // NAMED computedRates, not rates: it deliberately excludes FixedAmount and PerEmployeeAmount,
        // because the loop below emits one deduction line per entry and personal relief must not become a
        // deduction on top of being applied as relief. Calling it `rates` here shadowed the wider list in
        // ComputeRunAsync and is what hid #254 — the same name meant two different sets in two scopes.
        List<PayrollDeductionType> dTypes, List<PayeTaxBand> bands, List<StatutoryRate> computedRates,
        decimal relief, StatutoryRate? payeRate, int workingDays, string userId)
    {
        var slip = new Payslip
        {
            PayrollRunId = run.Id,
            PayrollPeriodCode = run.PayrollPeriodCode,
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            DepartmentId = employee.DepartmentId,
            DepartmentName = employee.DepartmentName,
            KraPin = employee.KraPin,
            NssfNumber = employee.NssfNumber,
            ShaNumber = employee.ShaNumber,
            EmployeeSalaryId = salary.Id,
            SalaryStructureName = structure.Name,
            BasicSalary = salary.BasicSalary,
            CurrencyCode = structure.CurrencyCode,
            CreatedBy = userId, UpdatedBy = userId,
        };

        var lines = new List<PayslipLine>();
        var order = 0;

        // ── 1. Earnings from the structure ────────────────────────────────────────
        // Percent-of-basic and fixed lines resolve directly; percent-of-GROSS needs a base, and that base is
        // everything else earned. Computing it in two passes avoids a circular definition where a
        // percent-of-gross component would be a percentage of itself.
        var basic = salary.BasicSalary;
        decimal earnedSoFar = 0m, taxableSoFar = 0m;
        var deferred = new List<SalaryComponent>();

        foreach (var c in structureComponents.Where(c => c.ComponentType == SalaryComponentType.Earning))
        {
            if (c.CalculationType == ComponentCalculationType.PercentOfGross) { deferred.Add(c); continue; }

            var (amount, basis) = c.CalculationType switch
            {
                ComponentCalculationType.PercentOfBasic => (Round(basic * (c.Percentage ?? 0m) / 100m), $"{c.Percentage:0.##}% of basic"),
                ComponentCalculationType.FixedAmount => (Round(c.Amount ?? 0m), "fixed"),
                // Variable inputs other than overtime have no source in this pass; they contribute nothing
                // rather than being guessed at.
                _ => (0m, "entered each period — nothing supplied"),
            };
            if (amount == 0m && c.CalculationType == ComponentCalculationType.VariableInput) continue;

            lines.Add(Line(c.Code, c.Name, PayslipLineType.Earning, amount, ++order, basis, c.IsTaxable, false, c, userId));
            earnedSoFar += amount;
            if (c.IsTaxable) taxableSoFar += amount;
        }

        foreach (var c in deferred)
        {
            var amount = Round(earnedSoFar * (c.Percentage ?? 0m) / 100m);
            lines.Add(Line(c.Code, c.Name, PayslipLineType.Earning, amount, ++order,
                $"{c.Percentage:0.##}% of gross", c.IsTaxable, false, c, userId));
            earnedSoFar += amount;
            if (c.IsTaxable) taxableSoFar += amount;
        }

        // ── 2. Overtime (P12 step 12.5) ───────────────────────────────────────────
        var hourly = HourlyRate(basic, workingDays);
        var otHours = employeeOt.Sum(o => o.Hours);
        var otPay = Round(employeeOt.Sum(o => o.Hours * hourly * o.Multiplier));
        if (otPay > 0)
        {
            var weekday = employeeOt.Where(o => o.RateType == OvertimeRateType.Weekday).Sum(o => o.Hours);
            var rest = employeeOt.Where(o => o.RateType == OvertimeRateType.RestDayOrHoliday).Sum(o => o.Hours);
            var basis = string.Join(", ", new[]
            {
                weekday > 0 ? $"{weekday:0.##} h at 1.5x" : null,
                rest > 0 ? $"{rest:0.##} h at 2x" : null,
            }.Where(s => s is not null));

            // Overtime posts where basic posts — it is the same cost of employment.
            var basicComponent = structureComponents.FirstOrDefault(c => c.Code == "BASIC")
                ?? structureComponents.FirstOrDefault(c => c.ComponentType == SalaryComponentType.Earning);
            lines.Add(Line("OVERTIME", "Overtime", PayslipLineType.Earning, otPay, ++order,
                $"{basis} (hourly {hourly:N2})", true, false, basicComponent, userId));
            earnedSoFar += otPay;
            taxableSoFar += otPay;
        }
        slip.OvertimeHours = otHours;
        slip.OvertimePay = otPay;

        // ── 2b. Commission (COM-005) ──
        // Commission is taxable earnings like any other, so it joins the gross rather than being paid
        // separately — that is the whole point of routing it through payroll.
        var commissionPay = Round(employeeCommission.Sum(c => c.CommissionAmount));
        if (commissionPay > 0)
        {
            var basicComponent = structureComponents.FirstOrDefault(c => c.Code == "BASIC")
                ?? structureComponents.FirstOrDefault(c => c.ComponentType == SalaryComponentType.Earning);
            var basis = string.Join(", ", employeeCommission.Select(c => $"{c.Year} Q{c.Quarter} at {c.CommissionRatePercent:0.##}%"));
            lines.Add(Line("COMMISSION", "Sales commission", PayslipLineType.Earning, commissionPay, ++order,
                basis, true, false, basicComponent, userId));
            earnedSoFar += commissionPay;
            taxableSoFar += commissionPay;
        }

        // ── 3. Unpaid days from H4 ────────────────────────────────────────────────
        // A day not worked and not covered by paid leave is pay NOT EARNED, so it reduces gross — and with it
        // the tax base. Treating it as a deduction after tax would tax the employee on money they never got.
        var unpaidDays = employeeUnpaid.Sum(a => a.UnpaidDays);
        var dailyRate = workingDays > 0 ? basic / workingDays : 0m;
        var unpaidAmount = Round(unpaidDays * dailyRate);
        if (unpaidAmount > 0)
        {
            var basicComponent = structureComponents.FirstOrDefault(c => c.Code == "BASIC")
                ?? structureComponents.FirstOrDefault(c => c.ComponentType == SalaryComponentType.Earning);
            lines.Add(Line("UNPAID", "Unpaid absence", PayslipLineType.Earning, -unpaidAmount, ++order,
                $"{unpaidDays:0.##} day(s) at {dailyRate:N2}", true, false, basicComponent, userId));
            earnedSoFar -= unpaidAmount;
            taxableSoFar -= unpaidAmount;
        }
        slip.UnpaidDays = unpaidDays;
        slip.UnpaidDeduction = unpaidAmount;

        var gross = Round(earnedSoFar);
        var taxableGross = Round(Math.Max(0m, taxableSoFar));

        // ── 4. Statutory contributions ────────────────────────────────────────────
        decimal statutoryTotal = 0m, employerTotal = 0m, preTaxStatutory = 0m;
        var employerLines = new List<PayslipLine>();

        foreach (var rate in computedRates.OrderBy(r => r.Component).ThenBy(r => r.Code))
        {
            var amount = StatutoryAmount(rate, gross);
            if (amount <= 0m && rate.EmployerRate is null) continue;

            if (amount > 0m)
            {
                lines.Add(Line(rate.Code, rate.Name, PayslipLineType.Deduction, amount, ++order,
                    BasisLabel(rate), false, true, null, userId, rate.GlAccountId, rate.GlAccountCode, rate.GlAccountName));
                statutoryTotal += amount;
                // Whether a contribution is allowable against taxable pay is DATA (see StatutoryRate) — it
                // moves with legislation, so the engine reads the flag rather than deciding for itself.
                if (rate.ReducesTaxableIncome) preTaxStatutory += amount;
            }

            if (rate.EmployerRate is > 0m)
            {
                var employer = EmployerAmount(rate, gross);
                if (employer > 0m)
                {
                    // The employer's share is charged to the expense account salary itself posts to, and owed
                    // to the same statutory body as the employee's share. Both legs are carried on one line.
                    var expense = structureComponents.FirstOrDefault(c => c.Code == "BASIC")
                        ?? structureComponents.FirstOrDefault(c => c.ComponentType == SalaryComponentType.Earning);
                    var line = Line($"{rate.Code}_ER", $"{rate.Name} — employer", PayslipLineType.EmployerCost,
                        employer, ++order, $"{rate.EmployerRate:0.##}% employer contribution", false, true, expense, userId);
                    line.ContraGlAccountId = rate.GlAccountId;
                    line.ContraGlAccountCode = rate.GlAccountCode;
                    line.ContraGlAccountName = rate.GlAccountName;
                    employerLines.Add(line);
                    employerTotal += employer;
                }
            }
        }

        // ── 5. Flexible deductions (P8) ───────────────────────────────────────────
        decimal otherTotal = 0m, preTaxOther = 0m;
        foreach (var d in employeeDeductions.OrderBy(d => d.DeductionTypeCode))
        {
            var type = dTypes.FirstOrDefault(t => t.Id == d.DeductionTypeId);
            var amount = Round(d.Amount);
            if (amount <= 0m) continue;

            lines.Add(Line(d.DeductionTypeCode ?? "DEDUCTION", d.DeductionTypeName ?? "Deduction",
                PayslipLineType.Deduction, amount, ++order,
                type?.ReducesTaxableIncome == true ? "before tax" : "after tax", false, false, null, userId,
                type?.GlAccountId, type?.GlAccountCode, type?.GlAccountName));
            lines[^1].DeductionId = d.Id;
            otherTotal += amount;
            if (type?.ReducesTaxableIncome == true) preTaxOther += amount;
        }

        // ── 6. PAYE ───────────────────────────────────────────────────────────────
        var taxableIncome = Round(Math.Max(0m, taxableGross - preTaxStatutory - preTaxOther));
        var taxBeforeRelief = Round(TaxOn(taxableIncome, bands));
        var reliefApplied = Math.Min(relief, taxBeforeRelief);   // relief reduces tax; it never becomes a refund
        var paye = Round(Math.Max(0m, taxBeforeRelief - reliefApplied));

        if (paye > 0m || taxBeforeRelief > 0m)
        {
            // payeRate is resolved in ComputeRunAsync from the UNFILTERED rate list and passed in, rather
            // than looked up from computedRates here, which cannot contain a Component==Paye row — that is
            // why the old lookup was unreachable (#254). Widening the parameter instead would have made
            // personal relief be charged as a deduction as well as applied as relief. A structure component
            // still wins if one exists, so tenants who worked around the bug keep working.
            var payeComponent = structureComponents.FirstOrDefault(c => c.Statutory == StatutoryComponent.Paye);
            lines.Add(Line("PAYE", "PAYE", PayslipLineType.Deduction, paye, ++order,
                reliefApplied > 0 ? $"tax {taxBeforeRelief:N2} less relief {reliefApplied:N2}" : "progressive bands",
                false, true, payeComponent, userId,
                payeComponent?.GlAccountId ?? payeRate?.GlAccountId,
                payeComponent?.GlAccountCode ?? payeRate?.GlAccountCode,
                payeComponent?.GlAccountName ?? payeRate?.GlAccountName));
            statutoryTotal += paye;
        }

        slip.GrossPay = gross;
        slip.TaxableIncome = taxableIncome;
        slip.Paye = paye;
        slip.PersonalRelief = reliefApplied;
        slip.StatutoryDeductions = Round(statutoryTotal);
        slip.OtherDeductions = Round(otherTotal);
        slip.TotalEarnings = gross;
        slip.TotalDeductions = Round(statutoryTotal + otherTotal);
        slip.NetPay = Round(gross - statutoryTotal - otherTotal);
        slip.EmployerCost = Round(employerTotal);

        lines.AddRange(employerLines);
        slip.Lines = lines;
        return slip;
    }

    /// <summary>Progressive tax over half-open bands: each band charges the slice of income above its lower
    /// bound up to its upper bound (see <see cref="PayeTaxBand"/>).</summary>
    internal static decimal TaxOn(decimal taxable, List<PayeTaxBand> bands)
    {
        if (taxable <= 0m) return 0m;
        decimal tax = 0m;
        foreach (var band in bands)
        {
            if (taxable <= band.LowerBound) break;
            var ceiling = band.UpperBound ?? taxable;
            var slice = Math.Min(taxable, ceiling) - band.LowerBound;
            if (slice <= 0m) continue;
            tax += slice * band.Rate / 100m;
        }
        return tax;
    }

    internal static decimal StatutoryAmount(StatutoryRate rate, decimal gross) => rate.RateType switch
    {
        StatutoryRateType.PercentOfGross => Clamp(Round(gross * (rate.Rate ?? 0m) / 100m), rate),
        // A tier charges only the slice of pay inside it — the same half-open reading as the PAYE bands.
        StatutoryRateType.TieredPercent => Clamp(Round(
            Math.Max(0m, Math.Min(gross, rate.TierUpperBound ?? gross) - (rate.TierLowerBound ?? 0m))
            * (rate.Rate ?? 0m) / 100m), rate),
        _ => 0m,
    };

    internal static decimal EmployerAmount(StatutoryRate rate, decimal gross) => rate.RateType switch
    {
        StatutoryRateType.PercentOfGross => Round(gross * (rate.EmployerRate ?? 0m) / 100m),
        StatutoryRateType.TieredPercent => Clamp(Round(
            Math.Max(0m, Math.Min(gross, rate.TierUpperBound ?? gross) - (rate.TierLowerBound ?? 0m))
            * (rate.EmployerRate ?? 0m) / 100m), rate),
        _ => 0m,
    };

    internal static decimal Clamp(decimal amount, StatutoryRate rate)
    {
        if (rate.MaxAmount is not null && amount > rate.MaxAmount) amount = rate.MaxAmount.Value;
        if (rate.MinAmount is not null && amount < rate.MinAmount) amount = rate.MinAmount.Value;
        return amount;
    }

    internal static decimal HourlyRate(decimal basic, int workingDays)
        => workingDays > 0 ? basic / workingDays / HoursPerWorkingDay : 0m;

    /// <summary>
    /// The assignment that was in force for the period being run — the latest one effective ON OR BEFORE it,
    /// including a superseded one. This is what makes re-running an old month correct: March's payroll must use
    /// March's salary, not today's.
    /// </summary>
    private static EmployeeSalary? CurrentSalaryFor(List<EmployeeSalary> all, string employeeId, string periodCode)
        => all.Where(s => s.EmployeeId == employeeId
                       && string.CompareOrdinal(s.EffectiveFromPeriodCode ?? "", periodCode) <= 0)
              .OrderByDescending(s => s.EffectiveFromPeriodCode)
              .FirstOrDefault();

    private static bool InPeriod(PayrollDeduction d, string periodCode)
        => string.CompareOrdinal(d.StartPeriodCode ?? "", periodCode) <= 0
        && (d.EndPeriodCode is null || string.CompareOrdinal(d.EndPeriodCode, periodCode) >= 0);

    /// <summary>Releases every overtime and unpaid-absence claim a run holds. Called before a recompute and on
    /// cancellation — the two moments a run stops owning what it took.</summary>
    private async Task ReleaseClaimsAsync(string runId, string userId)
    {
        var claimedOt = await overtime.Query().Where(o => o.PayrollRunId == runId).ToListAsync();
        foreach (var ot in claimedOt)
        {
            ot.PayrollRunId = null;
            ot.PaidAmount = null;
            ot.Status = OvertimeStatus.Approved;
            Touch(ot, userId);
            await overtime.UpdateAsync(ot);
        }

        var claimedCommission = await commissionStatements.Query().Where(c => c.PayrollRunId == runId).ToListAsync();
        foreach (var c in claimedCommission)
        {
            c.PayrollRunId = null;
            c.PaidAmount = null;
            c.Status = CommissionStatementStatus.Approved;
            Touch(c, userId);
            await commissionStatements.UpdateAsync(c);
        }

        var claimedAbsences = await absences.Query().Where(a => a.PayrollRunId == runId).ToListAsync();
        foreach (var absence in claimedAbsences)
        {
            absence.ReleasedToPayrollAt = null;
            absence.PayrollRunId = null;
            Touch(absence, userId);
            await absences.UpdateAsync(absence);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Approval and the finance journal
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<JournalPreviewDto?> PreviewJournalAsync(string id, CancellationToken ct = default)
    {
        var run = await runs.Query().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) return null;
        var (lines, problems) = await BuildJournalAsync(run, ct);

        return new JournalPreviewDto
        {
            RunNumber = run.RunNumber,
            Lines = lines,
            TotalDebit = Round(lines.Sum(l => l.Debit)),
            TotalCredit = Round(lines.Sum(l => l.Credit)),
            Balances = Round(lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit)) == 0m,
            Problems = problems,
        };
    }

    /// <summary>
    /// Builds the payroll journal by SUMMING THE PAYSLIP LINES, so the ledger cannot disagree with the payslips
    /// it represents. Earnings and employer costs are the debits; employee deductions and net pay are the
    /// credits, net pay resting in the holding account until the bank file is confirmed.
    /// </summary>
    private async Task<(List<JournalPreviewLineDto> Lines, List<string> Problems)> BuildJournalAsync(PayrollRun run, CancellationToken ct)
    {
        var problems = new List<string>();
        var slips = await payslips.Query().AsNoTracking().Where(p => p.PayrollRunId == run.Id).ToListAsync(ct);
        if (slips.Count == 0) return ([], ["The run has no payslips — compute it first."]);

        var slipIds = slips.Select(s => s.Id).ToList();
        var lines = await payslipLines.Query().AsNoTracking().Where(l => slipIds.Contains(l.PayslipId)).ToListAsync(ct);

        var accounts = await finance.ListAccountsAsync(ct);
        var chartRead = accounts.Count > 0;
        if (!chartRead)
            problems.Add("Finance's chart of accounts could not be read, so the accounts on this journal cannot be verified.");
        // Keyed by ID, because the id is what the journal actually posts with. Validating one identifier
        // here and resolving a different one at posting time is what let a run approve and then fail (#255).
        var byId = accounts.ToDictionary(a => a.Id, a => a);

        var journal = new List<JournalPreviewLineDto>();

        void Add(string? accountId, string? code, string? name, string description, decimal debit, decimal credit)
        {
            if (Round(debit) == 0m && Round(credit) == 0m) return;
            if (string.IsNullOrWhiteSpace(accountId)) { problems.Add($"No GL account for {description}."); return; }
            // Checked only when the chart was actually read: finance being unreachable is a transient the
            // run survives, and treating it as "every account is invalid" would refuse approval for an
            // outage. Phrased to start with "No GL account" so DecideRunAsync's mapping filter catches it
            // and the run is stopped at approval, while it is still cheap to fix.
            if (chartRead && !byId.ContainsKey(accountId))
            {
                problems.Add($"No GL account in finance's chart matches {code ?? accountId} for {description}.");
                return;
            }
            journal.Add(new JournalPreviewLineDto
            {
                AccountId = accountId, AccountCode = code ?? "", AccountName = name ?? "",
                Description = description, Debit = Round(debit), Credit = Round(credit),
            });
        }

        // Debits — the cost of employment, grouped by the account each component posts to.
        foreach (var g in lines.Where(l => l.LineType is PayslipLineType.Earning or PayslipLineType.EmployerCost)
                               .GroupBy(l => new { l.GlAccountId, l.GlAccountCode, l.GlAccountName }))
            Add(g.Key.GlAccountId, g.Key.GlAccountCode, g.Key.GlAccountName,
                $"{run.RunNumber} — {string.Join(", ", g.Select(l => l.Name).Distinct().Take(4))}",
                g.Sum(l => l.Amount), 0m);

        // Credits — what is withheld from staff and owed to somebody else.
        foreach (var g in lines.Where(l => l.LineType == PayslipLineType.Deduction)
                               .GroupBy(l => new { l.GlAccountId, l.GlAccountCode, l.GlAccountName }))
            Add(g.Key.GlAccountId, g.Key.GlAccountCode, g.Key.GlAccountName,
                $"{run.RunNumber} — {string.Join(", ", g.Select(l => l.Name).Distinct().Take(4))}",
                0m, g.Sum(l => l.Amount));

        // The employer contribution is a cost AND a liability. It is debited above to the expense account with
        // the other earnings; here it is credited to the statutory body it is owed to. Crediting the account it
        // was debited to would net the whole thing to nothing.
        foreach (var g in lines.Where(l => l.LineType == PayslipLineType.EmployerCost)
                               .GroupBy(l => new { l.ContraGlAccountId, l.ContraGlAccountCode, l.ContraGlAccountName }))
            Add(g.Key.ContraGlAccountId, g.Key.ContraGlAccountCode, g.Key.ContraGlAccountName,
                $"{run.RunNumber} — employer contribution payable", 0m, g.Sum(l => l.Amount));

        // Net pay waits in the holding account until the bank file is confirmed — crediting the bank now would
        // claim the money had already left it.
        var holding = accounts.FirstOrDefault(a => a.Code == GlNetPayHolding);
        if (holding is null && chartRead)
            // Only claimed when the chart was actually read. An unreadable chart cannot tell you an account
            // is absent, and saying so turned a finance OUTAGE into "Map the account, then recompute the
            // run" — sending someone to fix a mapping that was never wrong, and refusing an approval that
            // `:896` explicitly documents as survivable. With the chart unread the only problem raised is
            // that it could not be read, which is not a mapping problem, so the run approves and the
            // journal is retried once finance is back.
            problems.Add($"Account {GlNetPayHolding} (the net-pay holding account) is not in finance's chart of accounts.");
        else if (holding is not null)
            Add(holding.Id, holding.Code, holding.Name, $"{run.RunNumber} — net pay due to staff", 0m, run.TotalNet);

        return (journal, problems);
    }

    public async Task<PayrollActionResult> DecideRunAsync(string id, DecidePayrollRunDto dto, string? tenantSchema,
        string userId, string? userName, CancellationToken ct = default)
    {
        var run = await runs.GetByIdAsync(id);
        if (run is null || run.IsDeleted) return Err("Payroll run not found.");

        var decision = (dto.Decision ?? string.Empty).Trim();
        var approve = decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        var cancel = decision.Equals("Cancel", StringComparison.OrdinalIgnoreCase);
        if (!approve && !cancel) return Err("The decision must be Approve or Cancel.");

        var period = await periods.GetByIdAsync(run.PayrollPeriodId);

        if (cancel)
        {
            if (run.Status == PayrollRunStatus.Approved)
                return Err($"{run.RunNumber} is approved and posted — reverse the journal in finance rather than cancelling the run.");
            if (run.Status == PayrollRunStatus.Cancelled)
                return new PayrollActionResult("NoChange", $"{run.RunNumber} is already cancelled.", run.Id);
            if (string.IsNullOrWhiteSpace(dto.Reason)) return Err("Cancelling a payroll run needs a reason.");

            await ReleaseClaimsAsync(run.Id, userId);
            var slips = await payslips.Query().Where(p => p.PayrollRunId == run.Id).ToListAsync(ct);
            foreach (var slip in slips) await payslips.DeleteAsync(slip);

            run.Status = PayrollRunStatus.Cancelled;
            run.CancelledBy = userId;
            run.CancelledAt = DateTime.UtcNow;
            run.CancellationReason = dto.Reason!.Trim();
            run.EmployeeCount = 0;
            Touch(run, userId);
            await runs.UpdateAsync(run);

            if (period is not null && period.Status == PayrollPeriodStatus.Locked)
            {
                period.Status = PayrollPeriodStatus.Open;
                period.LockedAt = null;
                period.LockedBy = null;
                Touch(period, userId);
                await periods.UpdateAsync(period);
            }

            await LogAsync("PayrollRun", run.Id, HrAuditAction.PayrollRunCancelled,
                $"{run.RunNumber} cancelled by {userName ?? userId}: {run.CancellationReason}. {slips.Count} payslip(s) discarded and every overtime and absence claim released.", userId, userName);
            return new PayrollActionResult("Cancelled",
                $"{run.RunNumber} cancelled. {slips.Count} payslip(s) discarded, {run.PayrollPeriodCode} reopened, and all overtime released for a later run.", run.Id);
        }

        // ── Approve ──
        if (run.Status != PayrollRunStatus.Computed)
            return Err($"{run.RunNumber} is {run.Status.ToString().ToLowerInvariant()} — only a computed run can be approved.");

        // The person who ran payroll must not be the person who signs it off (P9 step 9.8) — the same
        // second-officer rule as a salary assignment, on a much larger number.
        if (!string.IsNullOrWhiteSpace(run.ComputedBy) && run.ComputedBy == userId)
            return Err("The person who computed a payroll run cannot approve it — it needs a second officer.");

        // Unverified statutory rates must be an accountable decision, not a warning nobody sees. The warning
        // at compute time goes to the person who COMPUTED the run, and the second-officer rule above
        // guarantees that is not the person approving it — so without this gate the approver signs off
        // figures derived from rates nobody has checked, with nothing in front of them saying so (#244).
        //
        // Deliberately NOT a refusal by default: there are legitimate reasons to run before rates are
        // confirmed, and a hard block would be routed around. The gate makes the decision explicit and
        // attributable instead.
        var unconfirmedRates = await UnconfirmedRateCodesAsync(run.PeriodEnd.Date, ct);
        if (unconfirmedRates.Count > 0 && !dto.AcknowledgeUnconfirmedRates)
            return Err($"{run.RunNumber} was computed from statutory rates that have never been checked against "
                     + $"the current Finance Act: {string.Join(", ", unconfirmedRates)}. Confirm those rates, or "
                     + "approve again acknowledging that the run uses unverified figures — the acknowledgement "
                     + "is recorded against your name.", code: "UnconfirmedRates");

        // A run whose journal cannot even be BUILT must not be approved. Approval freezes the payslips, so a
        // missing GL mapping discovered afterwards would leave the run permanently stuck: unable to post
        // (the line has no account) and unable to recompute (the figures are frozen). Catching it here keeps
        // the fix cheap — map the account and recompute — instead of impossible.
        // Note this is deliberately NOT the same as finance being unreachable: that is a transient failure the
        // run survives and retries, and it is checked at posting time rather than here.
        var (_, buildProblems) = await BuildJournalAsync(run, ct);
        var mappingProblems = buildProblems.Where(p => p.StartsWith("No GL account", StringComparison.Ordinal)
                                                    || p.Contains("holding account", StringComparison.Ordinal)).ToList();
        if (mappingProblems.Count > 0)
            return Err($"{run.RunNumber} cannot be approved until its journal can be built: {string.Join(" ", mappingProblems)} Map the account, then recompute the run.");

        run.Status = PayrollRunStatus.Approved;
        run.ApprovedBy = userId;
        run.ApprovedAt = DateTime.UtcNow;
        Touch(run, userId);
        await runs.UpdateAsync(run);

        var result = new PayrollActionResult("Approved",
            $"{run.RunNumber} approved — {run.EmployeeCount} payslip(s), net {Money(run.TotalNet, run.CurrencyCode)}.", run.Id);

        await LogAsync("PayrollRun", run.Id, HrAuditAction.PayrollRunApproved,
            $"{run.RunNumber} approved by {userName ?? userId}: gross {run.TotalGross:N2}, net {run.TotalNet:N2}, {run.EmployeeCount} employee(s)."
            + (unconfirmedRates.Count > 0
                ? $" {userName ?? userId} ACKNOWLEDGED approving on unverified statutory rates: {string.Join(", ", unconfirmedRates)}."
                : string.Empty), userId, userName);

        if (unconfirmedRates.Count > 0)
            result.Warnings.Add($"Approved on statutory rates that have never been verified: {string.Join(", ", unconfirmedRates)}. "
                              + "Confirm them before the next run.");

        var posting = await PostJournalAsync(run, tenantSchema, userId, userName, ct);
        result.Warnings.AddRange(posting);

        if (period is not null && run.JournalPostedAt is not null && period.Status != PayrollPeriodStatus.Closed)
        {
            period.Status = PayrollPeriodStatus.Closed;
            period.ClosedAt = DateTime.UtcNow;
            Touch(period, userId);
            await periods.UpdateAsync(period);
        }
        return result;
    }

    public async Task<PayrollActionResult> RetryJournalAsync(string id, string? tenantSchema, string userId, CancellationToken ct = default)
    {
        var run = await runs.GetByIdAsync(id);
        if (run is null || run.IsDeleted) return Err("Payroll run not found.");
        if (run.Status != PayrollRunStatus.Approved)
            return Err($"{run.RunNumber} is {run.Status.ToString().ToLowerInvariant()} — only an approved run has a journal to post.");
        if (run.JournalPostedAt is not null)
            return new PayrollActionResult("NoChange", $"{run.RunNumber} is already posted as {run.JournalEntryNo ?? run.JournalEntryId}.", run.Id);

        var warnings = await PostJournalAsync(run, tenantSchema, userId, null, ct);
        var result = new PayrollActionResult(run.JournalPostedAt is null ? "Failed" : "Posted",
            run.JournalPostedAt is null
                ? $"The journal for {run.RunNumber} still could not be posted."
                : $"Payroll journal {run.JournalEntryNo} posted for {run.RunNumber}.", run.Id);
        result.Warnings.AddRange(warnings);

        if (run.JournalPostedAt is not null)
        {
            var period = await periods.GetByIdAsync(run.PayrollPeriodId);
            if (period is not null && period.Status != PayrollPeriodStatus.Closed)
            {
                period.Status = PayrollPeriodStatus.Closed;
                period.ClosedAt = DateTime.UtcNow;
                Touch(period, userId);
                await periods.UpdateAsync(period);
            }
        }
        return result;
    }

    /// <summary>Posts the run's journal and records the outcome on the run. A failure never unwinds the
    /// approval: the run is the record of what staff are owed, and the ledger catches up on retry.</summary>
    private async Task<List<string>> PostJournalAsync(PayrollRun run, string? tenantSchema, string userId, string? userName, CancellationToken ct)
    {
        var warnings = new List<string>();
        var (previewLines, problems) = await BuildJournalAsync(run, ct);

        if (problems.Count > 0)
        {
            run.JournalError = string.Join(" ", problems);
            Touch(run, userId);
            await runs.UpdateAsync(run);
            warnings.Add($"The payroll journal was NOT posted: {string.Join(" ", problems)} The run stands — fix the mapping and retry.");
            await LogAsync("PayrollRun", run.Id, HrAuditAction.PayrollJournalFailed,
                $"{run.RunNumber} journal not built: {run.JournalError}", userId, userName);
            await NotifyAsync(tenantSchema, "Critical", $"Payroll journal not posted — {run.RunNumber} ({run.PayrollPeriodCode})",
                $"{run.RunNumber} was approved but its journal could not be built: {run.JournalError}", "hr.payroll.approve");
            return warnings;
        }

        // No second chart read and no second lookup. BuildJournalAsync above already validated every
        // account id against the chart it read, so the id on the preview line is the id to post with.
        // This used to resolve by CODE while the build validated the ID, so a mapping carrying an id and no
        // code passed approval and then matched nothing here. The count-mismatch guard that used to sit
        // below caught that and aborted before calling finance, so the ledger was left EMPTY rather than
        // holding a partial or unbalanced entry — the run was simply approved, unpostable, frozen against
        // recompute, and reporting that finance had removed an account it never had (#255). The guard is
        // gone because it can no longer trigger: every preview line maps one-to-one onto a posted line.
        var lines = previewLines
            .Select(l => new JournalLineDto(l.AccountId, l.Description, l.Debit, l.Credit))
            .ToList();

        var posted = await finance.PostJournalAsync(
            tenantSchema ?? string.Empty, run.PeriodEnd,
            $"Payroll {run.PayrollPeriodCode} — {run.RunNumber} ({run.EmployeeCount} employees)",
            run.Id, lines, ct);

        if (posted.Posted)
        {
            run.JournalEntryId = posted.JournalEntryId;
            run.JournalEntryNo = posted.JournalEntryNo;
            run.JournalPostedAt = DateTime.UtcNow;
            run.JournalError = null;
            Touch(run, userId);
            await runs.UpdateAsync(run);

            await LogAsync("PayrollRun", run.Id, HrAuditAction.PayrollJournalPosted,
                $"{run.RunNumber} posted to finance as {posted.JournalEntryNo} ({lines.Count} lines, {run.TotalGross:N2} gross).", userId, userName);
            warnings.Add($"{posted.Message} {run.PayrollPeriodCode} is now closed.");
        }
        else
        {
            run.JournalError = posted.Message;
            Touch(run, userId);
            await runs.UpdateAsync(run);

            await LogAsync("PayrollRun", run.Id, HrAuditAction.PayrollJournalFailed,
                $"{run.RunNumber} approved but the journal did not post: {posted.Message}", userId, userName);
            await NotifyAsync(tenantSchema, "Critical", $"Payroll journal not posted — {run.RunNumber} ({run.PayrollPeriodCode})",
                $"{run.RunNumber} was approved but the finance posting failed: {posted.Message}", "hr.payroll.approve");
            warnings.Add($"{posted.Message} Approval stands and the posting can be retried.");
        }
        return warnings;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════
    private async Task<PayrollPeriod?> FindCurrentPeriodAsync()
    {
        var today = DateTime.UtcNow.Date;
        var open = await periods.Query().Where(p => p.Status == PayrollPeriodStatus.Open)
            .OrderBy(p => p.Year).ThenBy(p => p.Month).ToListAsync();
        return open.FirstOrDefault(p => p.StartDate.Date <= today && p.EndDate.Date >= today)
            ?? open.LastOrDefault();
    }

    /// <summary>
    /// The codes of every PAYE band and statutory rate in force on <paramref name="asOf"/> that nobody has
    /// checked against the current Finance Act.
    ///
    /// <para><b>Re-derived rather than read from a flag stored at compute time.</b> Two reasons. If the rates
    /// are confirmed between computing a run and approving it, a stored flag would still demand an
    /// acknowledgement for figures that are now verified — training people to acknowledge reflexively, which
    /// is how a control becomes a click-through. And a stored flag can only ever be as correct as the moment
    /// it was written, whereas the approver needs to know the position now.</para>
    ///
    /// <para>Reads the FULL rate list, not the computed subset: personal relief (FixedAmount) and HELB
    /// (PerEmployeeAmount) are excluded from that subset and are exactly the figures a Finance Act changes.
    /// See #244.</para>
    /// </summary>
    private async Task<List<string>> UnconfirmedRateCodesAsync(DateTime asOf, CancellationToken ct = default)
    {
        var bands = InForce(await payeBands.Query().AsNoTracking().Where(b => b.IsActive).ToListAsync(ct), asOf,
            b => b.EffectiveFrom, b => b.EffectiveTo);
        var rates = InForce(await statutoryRates.Query().AsNoTracking().Where(r => r.IsActive).ToListAsync(ct), asOf,
            r => r.EffectiveFrom, r => r.EffectiveTo);

        return bands.Where(b => b.NeedsConfirmation).Select(b => $"PAYE band {b.BandOrder}")
            .Concat(rates.Where(r => r.NeedsConfirmation).Select(r => r.Code))
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
    }

    private static List<T> InForce<T>(IEnumerable<T> rows, DateTime on, Func<T, DateTime> from, Func<T, DateTime?> to)
        => rows.Where(r => from(r).Date <= on && (to(r) is null || to(r)!.Value.Date >= on)).ToList();

    private PayslipLine Line(string code, string name, PayslipLineType type, decimal amount, int order,
        string? basis, bool taxable, bool statutory, SalaryComponent? component, string userId,
        string? glId = null, string? glCode = null, string? glName = null) => new()
        {
            Code = code, Name = name, LineType = type, Amount = amount, LineOrder = order,
            Basis = basis, IsTaxable = taxable, IsStatutory = statutory,
            SalaryComponentId = component?.Id,
            GlAccountId = glId ?? component?.GlAccountId,
            GlAccountCode = glCode ?? component?.GlAccountCode,
            GlAccountName = glName ?? component?.GlAccountName,
            CreatedBy = userId, UpdatedBy = userId,
        };

    private async Task NotifyAsync(string? schema, string severity, string title, string message, string permission)
    {
        if (string.IsNullOrWhiteSpace(schema)) return;
        await notifier.CreateAlertAsync(schema, "HR", severity, title, message, permission);
    }

    private static decimal Round(decimal amount) => HrService.Core.Services.Money.Round(amount);
    private static string Money(decimal amount, string? currency) => $"{currency ?? "KES"} {amount:N2}";

    private static string BasisLabel(StatutoryRate r)
    {
        var basis = r.RateType switch
        {
            StatutoryRateType.PercentOfGross => $"{r.Rate:0.##}% of gross",
            StatutoryRateType.TieredPercent => $"{r.Rate:0.##}% of {r.TierLowerBound:N0}–{(r.TierUpperBound is null ? "∞" : $"{r.TierUpperBound:N0}")}",
            _ => "statutory",
        };
        if (r.MinAmount is not null) basis += $", min {r.MinAmount:N0}";
        if (r.MaxAmount is not null) basis += $", capped {r.MaxAmount:N0}";
        return basis;
    }

    private static OvertimeRequestDto ToDto(OvertimeRequest o) => new()
    {
        Id = o.Id, EmployeeId = o.EmployeeId, EmployeeNumber = o.EmployeeNumber, EmployeeName = o.EmployeeName,
        Date = o.Date, Hours = o.Hours, RateType = o.RateType.ToString(), Multiplier = o.Multiplier,
        Reason = o.Reason, Status = o.Status.ToString(),
        RequestedBy = o.RequestedBy, RequestedAt = o.RequestedAt,
        DecidedBy = o.DecidedBy, DecidedAt = o.DecidedAt, DecisionReason = o.DecisionReason,
        PayrollRunId = o.PayrollRunId, PaidAmount = o.PaidAmount,
        Basis = $"{o.Hours:0.##} h on {(o.RateType == OvertimeRateType.Weekday ? "a working day" : "a rest day or holiday")} at {o.Multiplier:0.#}x",
    };

    private static PayrollRunDto ToDto(PayrollRun r, List<PayslipDto> slips) => new()
    {
        Id = r.Id, RunNumber = r.RunNumber,
        PayrollPeriodId = r.PayrollPeriodId, PayrollPeriodCode = r.PayrollPeriodCode,
        PeriodStart = r.PeriodStart, PeriodEnd = r.PeriodEnd, CutOffDate = r.CutOffDate,
        Status = r.Status.ToString(), CurrencyCode = r.CurrencyCode,
        EmployeeCount = r.EmployeeCount, TotalGross = r.TotalGross, TotalTaxable = r.TotalTaxable,
        TotalPaye = r.TotalPaye, TotalStatutory = r.TotalStatutory, TotalOtherDeductions = r.TotalOtherDeductions,
        TotalDeductions = r.TotalDeductions, TotalNet = r.TotalNet, TotalEmployerCost = r.TotalEmployerCost,
        ComputedBy = r.ComputedBy, ComputedAt = r.ComputedAt,
        ApprovedBy = r.ApprovedBy, ApprovedAt = r.ApprovedAt,
        CancelledBy = r.CancelledBy, CancelledAt = r.CancelledAt, CancellationReason = r.CancellationReason,
        JournalEntryId = r.JournalEntryId, JournalEntryNo = r.JournalEntryNo,
        JournalPostedAt = r.JournalPostedAt, JournalError = r.JournalError,
        Exclusions = string.IsNullOrWhiteSpace(r.Exclusions) ? [] : r.Exclusions.Split(" | ").ToList(),
        Notes = r.Notes, Payslips = slips,
    };

    private static PayslipDto ToDto(Payslip p, List<PayslipLine> lines) => new()
    {
        Id = p.Id, PayrollRunId = p.PayrollRunId, PayrollPeriodCode = p.PayrollPeriodCode,
        EmployeeId = p.EmployeeId, EmployeeNumber = p.EmployeeNumber, EmployeeName = p.EmployeeName,
        DepartmentName = p.DepartmentName, KraPin = p.KraPin, SalaryStructureName = p.SalaryStructureName,
        BasicSalary = p.BasicSalary, CurrencyCode = p.CurrencyCode,
        GrossPay = p.GrossPay, TaxableIncome = p.TaxableIncome, Paye = p.Paye, PersonalRelief = p.PersonalRelief,
        StatutoryDeductions = p.StatutoryDeductions, OtherDeductions = p.OtherDeductions,
        TotalEarnings = p.TotalEarnings, TotalDeductions = p.TotalDeductions,
        NetPay = p.NetPay, EmployerCost = p.EmployerCost,
        OvertimeHours = p.OvertimeHours, OvertimePay = p.OvertimePay,
        UnpaidDays = p.UnpaidDays, UnpaidDeduction = p.UnpaidDeduction,
        PdfUrl = p.PdfUrl, EmailedAt = p.EmailedAt, ViewedAt = p.ViewedAt,
        Lines = lines.OrderBy(l => l.LineOrder).Select(l => new PayslipLineDto
        {
            Id = l.Id, Code = l.Code, Name = l.Name, LineType = l.LineType.ToString(),
            Amount = l.Amount, LineOrder = l.LineOrder, Basis = l.Basis,
            IsTaxable = l.IsTaxable, IsStatutory = l.IsStatutory,
            GlAccountCode = l.GlAccountCode, GlAccountName = l.GlAccountName,
        }).ToList(),
    };

    private static PayrollActionResult Err(string message, string? code = null) => new("Error", message) { Code = code };
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }

    private async Task LogAsync(string entityType, string entityId, HrAuditAction action, string detail, string userId, string? userName = null)
    {
        await audit.CreateAsync(new HrAuditLog
        {
            EntityType = entityType, EntityId = entityId, Action = action, Detail = detail,
            PerformedBy = userId, PerformedByName = userName, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
