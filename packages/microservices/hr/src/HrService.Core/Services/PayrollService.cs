using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Payroll;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H5 (P7 + P8) — pay configuration: grades, salary structures and components, payroll periods, per-employee
/// salary assignments, the statutory rate tables, and the deduction catalogue.
/// <para><b>H5 configures, H6 computes.</b> Nothing here works out anyone's actual pay. What H5 owes H6 is a
/// complete, GL-mapped and human-confirmed rule set, which is exactly what
/// <see cref="GetSummaryAsync"/>'s blockers list measures.</para>
/// <para><b>Pay history is append-only.</b> A raise writes a new <see cref="EmployeeSalary"/> and supersedes the
/// old one (P7 design note); a component that has been paid is deactivated, never deleted; a deduction is
/// stopped, never removed (P8 design note). "What were they on in March" must survive every later change.</para>
/// <para><b>Seeded statutory figures are flagged, not trusted.</b> Every rate and band the seeders install
/// carries <c>NeedsConfirmation</c> and a <c>Source</c>: the numbers were right when written, but PAYE bands,
/// NSSF tiers and SHA rates move with each Finance Act, and a number nobody checked must not be mistaken for a
/// verified one. The summary counts unconfirmed figures as a blocker so H6 cannot quietly pay on them.</para>
/// </summary>
public class PayrollService(
    IGenericRepository<Employee> employees,
    IGenericRepository<JobGrade> grades,
    IGenericRepository<SalaryStructure> structures,
    IGenericRepository<SalaryComponent> components,
    IGenericRepository<PayrollPeriod> periods,
    IGenericRepository<EmployeeSalary> salaries,
    IGenericRepository<PayeTaxBand> payeBands,
    IGenericRepository<StatutoryRate> statutoryRates,
    IGenericRepository<PayrollDeductionType> deductionTypes,
    IGenericRepository<PayrollDeduction> deductions,
    IGenericRepository<HrAuditLog> audit,
    IFinanceGateway finance) : IPayrollService
{
    /// <summary>Employment states that are still on the payroll. Leavers keep their history but stop counting
    /// towards "who has no salary assigned".</summary>
    private static readonly EmploymentStatus[] OnPayroll =
        [EmploymentStatus.OnProbation, EmploymentStatus.Active, EmploymentStatus.OnLeave, EmploymentStatus.Suspended];

    /// <summary>The finance account codes the seeders reach for, from the COA finance already ships.</summary>
    private const string GlSalariesAndWages = "5200";
    private const string GlPayePayable = "2210";
    private const string GlNssfPayable = "2220";
    private const string GlShaPayable = "2230";
    private const string GlHousingLevyPayable = "2240";

    // ══════════════════════════════════════════════════════════════════════════════
    // Summary
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<PayrollSetupSummaryDto> GetSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;

        var allComponents = await components.Query().AsNoTracking().ToListAsync();
        var activeComponents = allComponents.Where(c => c.IsActive).ToList();
        var unmappedComponents = activeComponents.Count(c => string.IsNullOrWhiteSpace(c.GlAccountId));

        var allSalaries = await salaries.Query().AsNoTracking().ToListAsync();
        var approved = allSalaries.Where(s => s.Status == SalaryAssignmentStatus.Approved).ToList();
        var staff = await employees.Query().AsNoTracking()
            .Where(e => OnPayroll.Contains(e.Status)).Select(e => e.Id).ToListAsync();
        var withSalary = approved.Select(s => s.EmployeeId).Distinct().ToHashSet();

        var bands = await payeBands.Query().AsNoTracking().Where(b => b.IsActive).ToListAsync();
        var inForceBands = InForce(bands, today, b => b.EffectiveFrom, b => b.EffectiveTo);
        var rates = await statutoryRates.Query().AsNoTracking().Where(r => r.IsActive).ToListAsync();
        var inForceRates = InForce(rates, today, r => r.EffectiveFrom, r => r.EffectiveTo);

        var liveDeductions = await deductions.Query().AsNoTracking().Where(d => d.IsActive).ToListAsync();
        var openPeriods = await periods.Query().AsNoTracking()
            .Where(p => p.Status == PayrollPeriodStatus.Open).ToListAsync();
        var current = await FindCurrentPeriodAsync();

        var dto = new PayrollSetupSummaryDto
        {
            JobGrades = await grades.Query().CountAsync(g => g.IsActive),
            SalaryStructures = await structures.Query().CountAsync(s => s.IsActive),
            ActiveComponents = activeComponents.Count,
            ComponentsWithoutGlAccount = unmappedComponents,

            EmployeesOnApprovedSalary = withSalary.Count,
            EmployeesWithoutSalary = staff.Count(id => !withSalary.Contains(id)),
            SalariesAwaitingApproval = allSalaries.Count(s => s.Status == SalaryAssignmentStatus.Proposed),
            ApprovedMonthlyBasicTotal = CurrentAssignments(approved).Sum(s => s.BasicSalary),

            PayeBandsInForce = inForceBands.Count,
            StatutoryRatesInForce = inForceRates.Count,
            RatesNeedingConfirmation = inForceBands.Count(b => b.NeedsConfirmation)
                                     + inForceRates.Count(r => r.NeedsConfirmation),
            RatesWithoutGlAccount = inForceRates.Count(r => r.RateType != StatutoryRateType.FixedAmount
                                                        && string.IsNullOrWhiteSpace(r.GlAccountId)),

            DeductionTypes = await deductionTypes.Query().CountAsync(t => t.IsActive),
            ActiveDeductions = liveDeductions.Count,
            ActiveDeductionMonthlyTotal = liveDeductions.Sum(d => d.Amount),

            CurrentPeriodCode = current?.Code,
            OpenPeriods = openPeriods.Count,
        };

        // ── Blockers: everything standing between this tenant and an H6 run ──
        if (dto.SalaryStructures == 0)
            dto.Blockers.Add("No salary structure has been configured.");
        if (unmappedComponents > 0)
            dto.Blockers.Add($"{unmappedComponents} active salary component(s) have no GL account — the payroll journal would have nowhere to post them.");
        if (dto.RatesWithoutGlAccount > 0)
            dto.Blockers.Add($"{dto.RatesWithoutGlAccount} statutory rate(s) have no GL account.");
        if (inForceBands.Count == 0)
            dto.Blockers.Add("No PAYE tax bands are in force.");
        else
            dto.Blockers.AddRange(ValidateBands(inForceBands));
        if (dto.RatesNeedingConfirmation > 0)
            dto.Blockers.Add($"{dto.RatesNeedingConfirmation} seeded rate(s)/band(s) have never been checked against the current Finance Act.");
        if (dto.EmployeesWithoutSalary > 0)
            dto.Blockers.Add($"{dto.EmployeesWithoutSalary} employee(s) on the payroll have no approved salary.");
        if (current is null)
            dto.Blockers.Add("No open payroll period covers today.");

        return dto;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Job grades (DS1)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<JobGradeDto>> ListGradesAsync(bool includeInactive)
    {
        var q = grades.Query().AsNoTracking();
        if (!includeInactive) q = q.Where(g => g.IsActive);
        var list = await q.OrderBy(g => g.DisplayOrder).ThenBy(g => g.Code).ToListAsync();

        var counts = await structures.Query().AsNoTracking()
            .Where(s => s.JobGradeId != null)
            .GroupBy(s => s.JobGradeId!)
            .Select(g => new { GradeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GradeId, x => x.Count);

        return list.Select(g => new JobGradeDto
        {
            Id = g.Id, Code = g.Code, Name = g.Name, Description = g.Description,
            MinSalary = g.MinSalary, MaxSalary = g.MaxSalary,
            DisplayOrder = g.DisplayOrder, IsActive = g.IsActive,
            StructureCount = counts.GetValueOrDefault(g.Id),
        }).ToList();
    }

    public async Task<PayrollActionResult> CreateGradeAsync(SaveJobGradeDto dto, string userId)
    {
        var code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return Err("A job grade needs a code.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A job grade needs a name.");
        if (await grades.Query().AnyAsync(g => g.Code == code))
            return Err($"Job grade '{code}' already exists.");
        var invalid = ValidateBand(dto);
        if (invalid is not null) return Err(invalid);

        var entity = new JobGrade { Code = code, CreatedBy = userId, UpdatedBy = userId };
        ApplyGrade(entity, dto);
        var created = await grades.CreateAsync(entity);

        await LogAsync("JobGrade", created.Id, HrAuditAction.JobGradeConfigured,
            $"Job grade {created.Code} ({created.Name}) created{BandLabel(created)}.", userId);
        return new PayrollActionResult("Created", $"Job grade {created.Name} configured.", created.Id);
    }

    public async Task<PayrollActionResult> UpdateGradeAsync(string id, SaveJobGradeDto dto, string userId)
    {
        var grade = await grades.GetByIdAsync(id);
        if (grade is null || grade.IsDeleted) return Err("Job grade not found.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A job grade needs a name.");
        var invalid = ValidateBand(dto);
        if (invalid is not null) return Err(invalid);

        ApplyGrade(grade, dto);
        Touch(grade, userId);
        await grades.UpdateAsync(grade);

        var result = new PayrollActionResult("Updated", $"Job grade {grade.Name} updated.", grade.Id);
        // A band change does not reprice anyone — assignments hold their own figure by design (P7 step 7.4).
        var structureIds = await structures.Query().Where(st => st.JobGradeId == grade.Id).Select(st => st.Id).ToListAsync();
        var priced = structureIds.Count == 0 ? 0 : await salaries.Query()
            .CountAsync(s => s.Status == SalaryAssignmentStatus.Approved && structureIds.Contains(s.SalaryStructureId));
        if (priced > 0)
            result.Warnings.Add($"{priced} approved salary assignment(s) are priced on this grade — their figures are unchanged, as pay history is never rewritten.");

        await LogAsync("JobGrade", grade.Id, HrAuditAction.JobGradeConfigured,
            $"Job grade {grade.Code} updated{BandLabel(grade)}.", userId);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Salary structures and components (P7 steps 7.1–7.3)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<SalaryStructureDto>> ListStructuresAsync(bool includeInactive)
    {
        var q = structures.Query().AsNoTracking();
        if (!includeInactive) q = q.Where(s => s.IsActive);
        var list = await q.OrderBy(s => s.Name).ToListAsync();
        if (list.Count == 0) return [];

        var ids = list.Select(s => s.Id).ToList();
        var comps = await components.Query().AsNoTracking()
            .Where(c => ids.Contains(c.SalaryStructureId)).ToListAsync();
        var assigned = await salaries.Query().AsNoTracking()
            .Where(s => s.Status == SalaryAssignmentStatus.Approved && ids.Contains(s.SalaryStructureId))
            .GroupBy(s => s.SalaryStructureId)
            .Select(g => new { Id = g.Key, Count = g.Select(x => x.EmployeeId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count);

        return list.Select(s => ToDto(s, comps.Where(c => c.SalaryStructureId == s.Id).ToList(),
                                      assigned.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SalaryStructureDto?> GetStructureAsync(string id)
    {
        var s = await structures.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return null;
        var comps = await components.Query().AsNoTracking().Where(c => c.SalaryStructureId == id).ToListAsync();
        var assigned = await salaries.Query().CountAsync(x => x.SalaryStructureId == id
            && x.Status == SalaryAssignmentStatus.Approved);
        return ToDto(s, comps, assigned);
    }

    public async Task<PayrollActionResult> CreateStructureAsync(SaveSalaryStructureDto dto, string userId)
    {
        var name = (dto.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) return Err("A salary structure needs a name.");
        if (await structures.Query().AnyAsync(s => s.Name == name))
            return Err($"A salary structure called '{name}' already exists.");

        JobGrade? grade = null;
        if (!string.IsNullOrWhiteSpace(dto.JobGradeId))
        {
            grade = await grades.GetByIdAsync(dto.JobGradeId!);
            if (grade is null || grade.IsDeleted) return Err("Job grade not found.");
        }

        var entity = new SalaryStructure
        {
            Name = name,
            Description = dto.Description,
            JobGradeId = grade?.Id,
            JobGradeCode = grade?.Code,
            CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "KES" : dto.CurrencyCode!.Trim().ToUpperInvariant(),
            IsActive = dto.IsActive ?? true,
            CreatedBy = userId, UpdatedBy = userId,
        };
        var created = await structures.CreateAsync(entity);

        await LogAsync("SalaryStructure", created.Id, HrAuditAction.SalaryStructureConfigured,
            $"Salary structure {created.Name} created ({created.CurrencyCode}{(grade is null ? "" : $", grade {grade.Code}")}).", userId);
        return new PayrollActionResult("Created", $"{created.Name} created. Add its components next.", created.Id);
    }

    public async Task<PayrollActionResult> UpdateStructureAsync(string id, SaveSalaryStructureDto dto, string userId)
    {
        var s = await structures.GetByIdAsync(id);
        if (s is null || s.IsDeleted) return Err("Salary structure not found.");
        var name = (dto.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) return Err("A salary structure needs a name.");
        if (await structures.Query().AnyAsync(x => x.Name == name && x.Id != id))
            return Err($"A salary structure called '{name}' already exists.");

        JobGrade? grade = null;
        if (!string.IsNullOrWhiteSpace(dto.JobGradeId))
        {
            grade = await grades.GetByIdAsync(dto.JobGradeId!);
            if (grade is null || grade.IsDeleted) return Err("Job grade not found.");
        }

        var result = new PayrollActionResult("Updated", $"{name} updated.", s.Id);
        var deactivating = s.IsActive && dto.IsActive == false;
        if (deactivating)
        {
            var live = await salaries.Query().CountAsync(x => x.SalaryStructureId == id
                && (x.Status == SalaryAssignmentStatus.Approved || x.Status == SalaryAssignmentStatus.Proposed));
            if (live > 0)
                return Err($"{live} live salary assignment(s) use this structure — reassign them before deactivating it.");
        }

        s.Name = name;
        s.Description = dto.Description;
        s.JobGradeId = grade?.Id;
        s.JobGradeCode = grade?.Code;
        if (!string.IsNullOrWhiteSpace(dto.CurrencyCode)) s.CurrencyCode = dto.CurrencyCode!.Trim().ToUpperInvariant();
        if (dto.IsActive.HasValue) s.IsActive = dto.IsActive.Value;
        Touch(s, userId);
        await structures.UpdateAsync(s);

        await LogAsync("SalaryStructure", s.Id, HrAuditAction.SalaryStructureConfigured,
            $"Salary structure {s.Name} updated.", userId);
        return result;
    }

    public async Task<PayrollActionResult> SeedDefaultStructureAsync(string userId)
    {
        const string name = "QSL Standard";
        if (await structures.Query().AnyAsync(s => s.Name == name))
            return new PayrollActionResult("NoChange", $"'{name}' already exists — nothing seeded.");

        var structure = await structures.CreateAsync(new SalaryStructure
        {
            Name = name,
            Description = "Default structure: basic and house allowance, with the statutory deduction lines.",
            CurrencyCode = "KES",
            CreatedBy = userId, UpdatedBy = userId,
        });

        // BASIC is 100% of basic rather than a fixed figure: the employee's own number lives on their salary
        // assignment (P7 step 7.4), so the structure describes the SHAPE of pay and the assignment supplies the
        // amount. That keeps one basic figure per employee instead of a structure-level copy that can drift.
        var seed = new (string Code, string Name, SalaryComponentType Type, ComponentCalculationType Calc,
                        decimal? Pct, StatutoryComponent Stat, bool Taxable, int Order, string Gl)[]
        {
            ("BASIC",         "Basic Salary",           SalaryComponentType.Earning,   ComponentCalculationType.PercentOfBasic, 100m, StatutoryComponent.None,        true,  10, GlSalariesAndWages),
            ("HOUSE",         "House Allowance",        SalaryComponentType.Earning,   ComponentCalculationType.PercentOfBasic, 30m,  StatutoryComponent.None,        true,  20, GlSalariesAndWages),
            ("PAYE",          "PAYE",                   SalaryComponentType.Deduction, ComponentCalculationType.Statutory,      null, StatutoryComponent.Paye,        false, 100, GlPayePayable),
            ("NSSF",          "NSSF",                   SalaryComponentType.Deduction, ComponentCalculationType.Statutory,      null, StatutoryComponent.Nssf,        false, 110, GlNssfPayable),
            ("SHA",           "SHA",                    SalaryComponentType.Deduction, ComponentCalculationType.Statutory,      null, StatutoryComponent.Sha,         false, 120, GlShaPayable),
            ("HOUSING_LEVY",  "Affordable Housing Levy",SalaryComponentType.Deduction, ComponentCalculationType.Statutory,      null, StatutoryComponent.HousingLevy, false, 130, GlHousingLevyPayable),
        };

        var accounts = await SafeAccountsAsync();
        var mapped = 0;
        foreach (var c in seed)
        {
            var entity = new SalaryComponent
            {
                SalaryStructureId = structure.Id,
                Code = c.Code, Name = c.Name,
                ComponentType = c.Type, CalculationType = c.Calc,
                Percentage = c.Pct, Statutory = c.Stat, IsTaxable = c.Taxable,
                ComponentOrder = c.Order,
                CreatedBy = userId, UpdatedBy = userId,
            };
            var account = accounts.FirstOrDefault(a => a.Code == c.Gl);
            if (account is not null)
            {
                entity.GlAccountId = account.Id;
                entity.GlAccountCode = account.Code;
                entity.GlAccountName = account.Name;
                mapped++;
            }
            await components.CreateAsync(entity);
        }

        var result = new PayrollActionResult("Created",
            $"'{name}' created with {seed.Length} components; {mapped} mapped to a GL account.", structure.Id);
        if (mapped < seed.Length)
            result.Warnings.Add(accounts.Count == 0
                ? "Finance was unreachable, so no GL accounts were mapped — map them before running payroll."
                : $"{seed.Length - mapped} component(s) found no matching account in finance's chart of accounts.");
        result.Warnings.Add("Allowances beyond house (transport, airtime, and so on) are tenant-specific and were not assumed.");

        await LogAsync("SalaryStructure", structure.Id, HrAuditAction.SalaryStructureConfigured,
            $"Seeded default structure '{name}' with {seed.Length} components ({mapped} GL-mapped).", userId);
        return result;
    }

    public async Task<PayrollActionResult> AddComponentAsync(string structureId, SaveSalaryComponentDto dto, string userId)
    {
        var structure = await structures.GetByIdAsync(structureId);
        if (structure is null || structure.IsDeleted) return Err("Salary structure not found.");

        var code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return Err("A component needs a code.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A component needs a name.");
        if (await components.Query().AnyAsync(c => c.SalaryStructureId == structureId && c.Code == code))
            return Err($"Component '{code}' already exists on this structure.");

        var parsed = ParseComponent(dto);
        if (parsed.Error is not null) return Err(parsed.Error);

        var entity = new SalaryComponent
        {
            SalaryStructureId = structureId, Code = code,
            CreatedBy = userId, UpdatedBy = userId,
        };
        ApplyComponent(entity, dto, parsed);
        var created = await components.CreateAsync(entity);

        var result = new PayrollActionResult("Created", $"{created.Name} added to {structure.Name}.", created.Id);
        result.Warnings.Add("No GL account is mapped yet — the payroll journal cannot post this line until one is.");

        await LogAsync("SalaryComponent", created.Id, HrAuditAction.SalaryComponentConfigured,
            $"Component {created.Code} ({created.Name}) added to {structure.Name} — {BasisLabel(created)}.", userId);
        return result;
    }

    public async Task<PayrollActionResult> UpdateComponentAsync(string componentId, SaveSalaryComponentDto dto, string userId)
    {
        var c = await components.GetByIdAsync(componentId);
        if (c is null || c.IsDeleted) return Err("Salary component not found.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A component needs a name.");

        var parsed = ParseComponent(dto);
        if (parsed.Error is not null) return Err(parsed.Error);

        ApplyComponent(c, dto, parsed);
        Touch(c, userId);
        await components.UpdateAsync(c);

        await LogAsync("SalaryComponent", c.Id, HrAuditAction.SalaryComponentConfigured,
            $"Component {c.Code} updated — {BasisLabel(c)}.", userId);
        return new PayrollActionResult("Updated", $"{c.Name} updated.", c.Id);
    }

    public async Task<PayrollActionResult> DeactivateComponentAsync(string componentId, string userId)
    {
        var c = await components.GetByIdAsync(componentId);
        if (c is null || c.IsDeleted) return Err("Salary component not found.");
        if (!c.IsActive) return new PayrollActionResult("NoChange", $"{c.Name} is already inactive.", c.Id);

        c.IsActive = false;
        Touch(c, userId);
        await components.UpdateAsync(c);

        await LogAsync("SalaryComponent", c.Id, HrAuditAction.SalaryComponentConfigured,
            $"Component {c.Code} ({c.Name}) deactivated.", userId);
        return new PayrollActionResult("Deactivated",
            $"{c.Name} will not be paid from the next run. It is kept, not deleted, because it is part of pay history.", c.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // GL mapping (P7 step 7.5)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<GlAccountOptionDto>> ListGlAccountsAsync(CancellationToken ct = default)
    {
        var accounts = await finance.ListAccountsAsync(ct);
        // Only accounts finance will actually accept a posting on.
        return accounts
            .Where(a => a is { IsDirectPosting: true, IsActive: true })
            .OrderBy(a => a.Code)
            .Select(a => new GlAccountOptionDto { Id = a.Id, Code = a.Code, Name = a.Name, Classification = a.Classification })
            .ToList();
    }

    public async Task<PayrollActionResult> MapComponentAccountAsync(string componentId, MapGlAccountDto dto, string userId, CancellationToken ct = default)
    {
        var c = await components.GetByIdAsync(componentId);
        if (c is null || c.IsDeleted) return Err("Salary component not found.");

        var (account, error) = await ResolveAccountAsync(dto.GlAccountId, ct);
        if (error is not null) return Err(error);

        c.GlAccountId = account!.Id; c.GlAccountCode = account.Code; c.GlAccountName = account.Name;
        Touch(c, userId);
        await components.UpdateAsync(c);

        await LogAsync("SalaryComponent", c.Id, HrAuditAction.GlAccountMapped,
            $"Component {c.Code} mapped to GL {account.Code} — {account.Name}.", userId);
        return new PayrollActionResult("Mapped", $"{c.Name} posts to {account.Code} — {account.Name}.", c.Id);
    }

    public async Task<PayrollActionResult> MapStatutoryAccountAsync(string rateId, MapGlAccountDto dto, string userId, CancellationToken ct = default)
    {
        var r = await statutoryRates.GetByIdAsync(rateId);
        if (r is null || r.IsDeleted) return Err("Statutory rate not found.");

        var (account, error) = await ResolveAccountAsync(dto.GlAccountId, ct);
        if (error is not null) return Err(error);

        r.GlAccountId = account!.Id; r.GlAccountCode = account.Code; r.GlAccountName = account.Name;
        Touch(r, userId);
        await statutoryRates.UpdateAsync(r);

        await LogAsync("StatutoryRate", r.Id, HrAuditAction.GlAccountMapped,
            $"Statutory rate {r.Code} mapped to GL {account.Code} — {account.Name}.", userId);
        return new PayrollActionResult("Mapped", $"{r.Name} posts to {account.Code} — {account.Name}.", r.Id);
    }

    public async Task<PayrollActionResult> MapDeductionTypeAccountAsync(string typeId, MapGlAccountDto dto, string userId, CancellationToken ct = default)
    {
        var t = await deductionTypes.GetByIdAsync(typeId);
        if (t is null || t.IsDeleted) return Err("Deduction type not found.");

        var (account, error) = await ResolveAccountAsync(dto.GlAccountId, ct);
        if (error is not null) return Err(error);

        t.GlAccountId = account!.Id; t.GlAccountCode = account.Code; t.GlAccountName = account.Name;
        Touch(t, userId);
        await deductionTypes.UpdateAsync(t);

        await LogAsync("PayrollDeductionType", t.Id, HrAuditAction.GlAccountMapped,
            $"Deduction type {t.Code} mapped to GL {account.Code} — {account.Name}.", userId);
        return new PayrollActionResult("Mapped", $"{t.Name} posts to {account.Code} — {account.Name}.", t.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Payroll periods
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<PayrollPeriodDto>> ListPeriodsAsync(int? year)
    {
        var q = periods.Query().AsNoTracking();
        if (year.HasValue) q = q.Where(p => p.Year == year.Value);
        var list = await q.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToListAsync();
        var current = await FindCurrentPeriodAsync();
        return list.Select(p => ToDto(p, p.Id == current?.Id)).ToList();
    }

    public async Task<PayrollActionResult> GeneratePeriodsAsync(GeneratePeriodsDto dto, string userId)
    {
        var year = dto.Year == 0 ? DateTime.UtcNow.Year : dto.Year;
        if (year is < 2000 or > 2100) return Err("That year is outside the range payroll periods are kept for.");
        if (dto.CutOffDay is < 1 or > 31) return Err("The cut-off day must be between 1 and 31.");
        if (dto.PaymentDay is < 1 or > 31) return Err("The payment day must be between 1 and 31.");

        var existing = await periods.Query().Where(p => p.Year == year).Select(p => p.Month).ToListAsync();
        var created = 0;
        for (var month = 1; month <= 12; month++)
        {
            if (existing.Contains(month)) continue;
            var days = DateTime.DaysInMonth(year, month);
            await periods.CreateAsync(new PayrollPeriod
            {
                Code = $"{year:D4}-{month:D2}",
                Year = year, Month = month,
                StartDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(year, month, days, 0, 0, 0, DateTimeKind.Utc),
                // Both days are clamped to the month, so a 31st cut-off still lands in February.
                CutOffDate = dto.CutOffDay is null ? null
                    : new DateTime(year, month, Math.Min(dto.CutOffDay.Value, days), 0, 0, 0, DateTimeKind.Utc),
                PaymentDate = dto.PaymentDay is null ? null
                    : new DateTime(year, month, Math.Min(dto.PaymentDay.Value, days), 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = userId, UpdatedBy = userId,
            });
            created++;
        }

        await LogAsync("PayrollPeriod", year.ToString(), HrAuditAction.PayrollPeriodsGenerated,
            $"Payroll periods for {year}: {created} created, {12 - created} already present.", userId);
        return new PayrollActionResult(created == 0 ? "NoChange" : "Created",
            created == 0 ? $"All 12 periods for {year} already exist." : $"{created} payroll period(s) created for {year}.");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Employee salary assignments (P7 step 7.4)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<EmployeeSalaryDto>> ListSalariesAsync(string? employeeId, string? status, bool currentOnly)
    {
        var q = salaries.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(s => s.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SalaryAssignmentStatus>(status, true, out var st))
            q = q.Where(s => s.Status == st);

        var list = await q.OrderByDescending(s => s.EffectiveFromPeriodCode).ThenByDescending(s => s.ProposedAt).ToListAsync();
        if (currentOnly)
            list = CurrentAssignments(list.Where(s => s.Status == SalaryAssignmentStatus.Approved)).ToList();
        return list.Select(ToDto).ToList();
    }

    public async Task<PayrollActionResult> ProposeSalaryAsync(ProposeSalaryDto dto, string userId)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId ?? string.Empty);
        if (employee is null || employee.IsDeleted) return Err("Employee not found.");
        if (!OnPayroll.Contains(employee.Status))
            return Err($"{employee.FullName} is {employee.Status} and is no longer on the payroll.");
        if (dto.BasicSalary <= 0) return Err("The basic salary must be greater than zero.");

        var structure = await structures.GetByIdAsync(dto.SalaryStructureId ?? string.Empty);
        if (structure is null || structure.IsDeleted) return Err("Salary structure not found.");
        if (!structure.IsActive) return Err($"Salary structure {structure.Name} is not active.");

        var period = string.IsNullOrWhiteSpace(dto.EffectiveFromPeriodId)
            ? await FindCurrentPeriodAsync()
            : await periods.GetByIdAsync(dto.EffectiveFromPeriodId!);
        if (period is null || period.IsDeleted)
            return Err(string.IsNullOrWhiteSpace(dto.EffectiveFromPeriodId)
                ? "No open payroll period covers today — generate this year's periods first."
                : "Payroll period not found.");
        if (period.Status != PayrollPeriodStatus.Open)
            return Err($"Payroll period {period.Code} is {period.Status.ToString().ToLowerInvariant()} — pick a period still open.");

        var live = await salaries.Query()
            .Where(s => s.EmployeeId == employee.Id
                     && (s.Status == SalaryAssignmentStatus.Proposed || s.Status == SalaryAssignmentStatus.Approved))
            .ToListAsync();

        if (live.Any(s => s.EffectiveFromPeriodId == period.Id))
            return Err($"{employee.FullName} already has a live salary assignment effective from {period.Code}.");

        // A raise must move pay FORWARD. Back-dating behind the current approved assignment would be
        // superseded the moment it was approved, so it can only be a mistake.
        var latestApproved = live.Where(s => s.Status == SalaryAssignmentStatus.Approved)
            .OrderByDescending(s => s.EffectiveFromPeriodCode).FirstOrDefault();
        if (latestApproved is not null
            && string.CompareOrdinal(period.Code, latestApproved.EffectiveFromPeriodCode) <= 0)
            return Err($"{employee.FullName} is already on an approved salary from {latestApproved.EffectiveFromPeriodCode} — a new assignment must start in a later period.");

        var entity = new EmployeeSalary
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            SalaryStructureId = structure.Id,
            SalaryStructureName = structure.Name,
            BasicSalary = dto.BasicSalary,
            CurrencyCode = structure.CurrencyCode,
            EffectiveFromPeriodId = period.Id,
            EffectiveFromPeriodCode = period.Code,
            Status = SalaryAssignmentStatus.Proposed,
            ProposedBy = userId,
            ProposedAt = DateTime.UtcNow,
            Notes = dto.Notes,
            CreatedBy = userId, UpdatedBy = userId,
        };
        var created = await salaries.CreateAsync(entity);

        var result = new PayrollActionResult("Proposed",
            $"{employee.FullName} proposed at {Money(dto.BasicSalary, structure.CurrencyCode)} from {period.Code} — awaiting approval.", created.Id);

        var warning = await BandWarningAsync(structure, dto.BasicSalary);
        if (warning is not null) result.Warnings.Add(warning);
        if (latestApproved is not null)
            result.Warnings.Add($"This supersedes {Money(latestApproved.BasicSalary, latestApproved.CurrencyCode)} from {latestApproved.EffectiveFromPeriodCode} once approved.");

        await LogAsync("EmployeeSalary", created.Id, HrAuditAction.SalaryProposed,
            $"{employee.EmployeeNumber} proposed at {Money(dto.BasicSalary, structure.CurrencyCode)} on {structure.Name} from {period.Code}.{(warning is null ? "" : $" {warning}")}", userId);
        return result;
    }

    public async Task<PayrollActionResult> DecideSalaryAsync(string id, DecideSalaryDto dto, string userId, string? userName)
    {
        var salary = await salaries.GetByIdAsync(id);
        if (salary is null || salary.IsDeleted) return Err("Salary assignment not found.");
        if (salary.Status != SalaryAssignmentStatus.Proposed)
            return Err($"This assignment is already {salary.Status.ToString().ToLowerInvariant()} — only a proposed one can be decided.");

        var decision = (dto.Decision ?? string.Empty).Trim();
        var approve = decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        var reject = decision.Equals("Reject", StringComparison.OrdinalIgnoreCase);
        if (!approve && !reject) return Err("The decision must be Approve or Reject.");

        if (reject)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason)) return Err("A rejection needs a reason.");
            salary.Status = SalaryAssignmentStatus.Rejected;
            salary.RejectionReason = dto.Reason!.Trim();
            salary.ApprovedBy = userId;
            salary.ApprovedAt = DateTime.UtcNow;
            Touch(salary, userId);
            await salaries.UpdateAsync(salary);

            await LogAsync("EmployeeSalary", salary.Id, HrAuditAction.SalaryRejected,
                $"{salary.EmployeeNumber} salary from {salary.EffectiveFromPeriodCode} rejected by {userName ?? userId}: {salary.RejectionReason}", userId, userName);
            return new PayrollActionResult("Rejected", $"Proposal for {salary.EmployeeName} rejected.", salary.Id);
        }

        // Segregation of duties (P7 step 7.4): HR proposes, the MD approves. The same person must not do both,
        // for the same reason H1 will not let one person upload and verify a document.
        if (!string.IsNullOrWhiteSpace(salary.ProposedBy) && salary.ProposedBy == userId)
            return Err("The person who proposed a salary cannot approve it — it needs a second officer.");

        var period = await periods.GetByIdAsync(salary.EffectiveFromPeriodId);
        if (period is not null && period.Status == PayrollPeriodStatus.Closed)
            return Err($"Payroll period {period.Code} is closed — that month has already been paid. Re-propose from an open period.");

        salary.Status = SalaryAssignmentStatus.Approved;
        salary.ApprovedBy = userId;
        salary.ApprovedAt = DateTime.UtcNow;
        Touch(salary, userId);
        await salaries.UpdateAsync(salary);

        // Supersede whatever this replaces — the previous approved assignment for the same employee.
        var superseded = await salaries.Query()
            .Where(s => s.EmployeeId == salary.EmployeeId && s.Id != salary.Id
                     && s.Status == SalaryAssignmentStatus.Approved)
            .ToListAsync();
        foreach (var prior in superseded)
        {
            prior.Status = SalaryAssignmentStatus.Superseded;
            prior.SupersededById = salary.Id;
            Touch(prior, userId);
            await salaries.UpdateAsync(prior);
            await LogAsync("EmployeeSalary", prior.Id, HrAuditAction.SalarySuperseded,
                $"{prior.EmployeeNumber} assignment from {prior.EffectiveFromPeriodCode} ({Money(prior.BasicSalary, prior.CurrencyCode)}) superseded by {salary.EffectiveFromPeriodCode}.", userId, userName);
        }

        var result = new PayrollActionResult("Approved",
            $"{salary.EmployeeName} approved at {Money(salary.BasicSalary, salary.CurrencyCode)} from {salary.EffectiveFromPeriodCode}.", salary.Id);
        if (superseded.Count > 0)
            result.Warnings.Add($"{superseded.Count} earlier assignment(s) superseded — kept as history, not deleted.");

        await LogAsync("EmployeeSalary", salary.Id, HrAuditAction.SalaryApproved,
            $"{salary.EmployeeNumber} approved at {Money(salary.BasicSalary, salary.CurrencyCode)} from {salary.EffectiveFromPeriodCode} by {userName ?? userId}.", userId, userName);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // PAYE bands (P7 step 7.3)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<PayeTaxBandDto>> ListPayeBandsAsync(DateTime? asOf, bool includeInactive)
    {
        var q = payeBands.Query().AsNoTracking();
        if (!includeInactive) q = q.Where(b => b.IsActive);
        var list = await q.OrderBy(b => b.EffectiveFrom).ThenBy(b => b.BandOrder).ToListAsync();
        if (asOf.HasValue)
            list = InForce(list, asOf.Value.Date, b => b.EffectiveFrom, b => b.EffectiveTo);
        return list.Select(ToDto).ToList();
    }

    public async Task<PayrollActionResult> SeedPayeBandsAsync(string userId)
    {
        // Kenyan monthly PAYE scale. Bounds are half-open — see PayeTaxBand — so band N starts exactly where
        // band N-1 ended and no shilling falls between two bands.
        var effective = new DateTime(2023, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        const string source = "Finance Act 2023 (seeded from record — NOT verified against the current Act)";

        if (await payeBands.Query().AnyAsync(b => b.EffectiveFrom == effective))
            return new PayrollActionResult("NoChange", $"PAYE bands effective {effective:yyyy-MM-dd} are already installed.");

        var seed = new (decimal Lower, decimal? Upper, decimal Rate)[]
        {
            (0m,       24_000m,  10m),
            (24_000m,  32_333m,  25m),
            (32_333m,  500_000m, 30m),
            (500_000m, 800_000m, 32.5m),
            (800_000m, null,     35m),
        };

        var order = 1;
        foreach (var (lower, upper, rate) in seed)
        {
            await payeBands.CreateAsync(new PayeTaxBand
            {
                LowerBound = lower, UpperBound = upper, Rate = rate,
                BandOrder = order++,
                EffectiveFrom = effective,
                NeedsConfirmation = true,
                Source = source,
                CreatedBy = userId, UpdatedBy = userId,
            });
        }

        var result = new PayrollActionResult("Created", $"{seed.Length} PAYE bands installed, effective {effective:yyyy-MM-dd}.");
        result.Warnings.Add("Every seeded band is flagged as unconfirmed. Check the rates and the effective date against the current Finance Act, then confirm them — H6 counts unconfirmed figures as a blocker.");
        result.Warnings.Add("Personal relief is seeded with the statutory rates, not here.");

        await LogAsync("PayeTaxBand", effective.ToString("yyyy-MM-dd"), HrAuditAction.PayeBandConfigured,
            $"Seeded {seed.Length} PAYE bands effective {effective:yyyy-MM-dd}, all flagged for confirmation.", userId);
        return result;
    }

    public async Task<PayrollActionResult> SavePayeBandAsync(string? id, SavePayeTaxBandDto dto, string userId)
    {
        if (dto.Rate is < 0 or > 100) return Err("A tax rate must be between 0 and 100 percent.");
        if (dto.LowerBound < 0) return Err("A band cannot start below zero.");
        if (dto.UpperBound is not null && dto.UpperBound <= dto.LowerBound)
            return Err("A band's upper bound must be above its lower bound.");
        if (dto.BandOrder <= 0) return Err("A band needs a position in the scale (1 upwards).");

        var effective = DateTime.SpecifyKind((dto.EffectiveFrom ?? DateTime.UtcNow).Date, DateTimeKind.Utc);
        PayeTaxBand band;
        if (string.IsNullOrWhiteSpace(id))
        {
            if (await payeBands.Query().AnyAsync(b => b.EffectiveFrom == effective && b.BandOrder == dto.BandOrder))
                return Err($"Band {dto.BandOrder} already exists for {effective:yyyy-MM-dd}.");
            band = new PayeTaxBand { EffectiveFrom = effective, CreatedBy = userId, UpdatedBy = userId };
        }
        else
        {
            var found = await payeBands.GetByIdAsync(id!);
            if (found is null || found.IsDeleted) return Err("PAYE band not found.");
            band = found;
            band.EffectiveFrom = effective;
        }

        band.LowerBound = dto.LowerBound;
        band.UpperBound = dto.UpperBound;
        band.Rate = dto.Rate;
        band.BandOrder = dto.BandOrder;
        band.IsActive = dto.IsActive ?? band.IsActive;
        // A figure a person typed in is, by definition, one a person has checked.
        band.NeedsConfirmation = false;
        band.Source = string.IsNullOrWhiteSpace(dto.Source) ? band.Source : dto.Source!.Trim();
        Touch(band, userId);

        var saved = string.IsNullOrWhiteSpace(id) ? await payeBands.CreateAsync(band) : await payeBands.UpdateAsync(band);

        var result = new PayrollActionResult(string.IsNullOrWhiteSpace(id) ? "Created" : "Updated",
            $"Band {saved.BandOrder} saved — {BandLabel(saved)}.", saved.Id);

        // The scale as a whole has to be sound; one band at a time cannot tell you that.
        var inForce = InForce(await payeBands.Query().AsNoTracking().Where(b => b.IsActive).ToListAsync(),
            effective, b => b.EffectiveFrom, b => b.EffectiveTo);
        result.Warnings.AddRange(ValidateBands(inForce));

        await LogAsync("PayeTaxBand", saved.Id, HrAuditAction.PayeBandConfigured,
            $"PAYE band {saved.BandOrder} effective {saved.EffectiveFrom:yyyy-MM-dd} saved — {BandLabel(saved)}.", userId);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Statutory rates (NSSF, SHA, Housing Levy, HELB, personal relief)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<StatutoryRateDto>> ListStatutoryRatesAsync(DateTime? asOf, bool includeInactive)
    {
        var q = statutoryRates.Query().AsNoTracking();
        if (!includeInactive) q = q.Where(r => r.IsActive);
        var list = await q.OrderBy(r => r.Component).ThenBy(r => r.Code).ToListAsync();
        if (asOf.HasValue)
            list = InForce(list, asOf.Value.Date, r => r.EffectiveFrom, r => r.EffectiveTo);
        return list.Select(ToDto).ToList();
    }

    public async Task<PayrollActionResult> SeedStatutoryRatesAsync(string userId)
    {
        // Each rule carries the instrument it came from and the date that instrument took effect, because a
        // payroll re-run for an earlier month must find the rates that applied THEN. Every figure is flagged
        // for confirmation — see the class remarks.
        var seed = new (string Code, string Name, StatutoryComponent Component, StatutoryRateType Type,
                        decimal? Rate, decimal? Fixed, decimal? TierLower, decimal? TierUpper,
                        decimal? Min, decimal? Max, decimal? EmployerRate, bool PreTax, DateTime From, string Source, string Gl, string? Notes)[]
        {
            ("NSSF_TIER1", "NSSF Tier I", StatutoryComponent.Nssf, StatutoryRateType.TieredPercent,
                6m, null, 0m, 8_000m, null, 480m, 6m, true,
                new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                "NSSF Act 2013, Year 3 tiers (seeded from record — NOT verified)", GlNssfPayable,
                "The employer matches the employee contribution; H6 posts the employer half separately."),

            ("NSSF_TIER2", "NSSF Tier II", StatutoryComponent.Nssf, StatutoryRateType.TieredPercent,
                6m, null, 8_000m, 72_000m, null, 3_840m, 6m, true,
                new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                "NSSF Act 2013, Year 3 tiers (seeded from record — NOT verified)", GlNssfPayable,
                "Tier bounds are half-open, like the PAYE bands: this tier charges earnings above the lower bound."),

            ("SHA", "Social Health Authority", StatutoryComponent.Sha, StatutoryRateType.PercentOfGross,
                2.75m, null, null, null, 300m, null, null, true,
                new DateTime(2024, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                "Social Health Insurance Act 2023 (seeded from record — NOT verified)", GlShaPayable,
                "Percentage of gross with a monthly floor. SEEDED AS DEDUCTIBLE against taxable pay — the treatment of SHA and the housing levy changed after they were introduced, so confirm the rule AND the date it applies from before running real payroll."),

            ("HOUSING_LEVY", "Affordable Housing Levy", StatutoryComponent.HousingLevy, StatutoryRateType.PercentOfGross,
                1.5m, null, null, null, null, null, 1.5m, true,
                new DateTime(2023, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                "Affordable Housing Act / Finance Act 2023 (seeded from record — NOT verified)", GlHousingLevyPayable,
                "Universal and uncapped; the employer matches it. SEEDED AS DEDUCTIBLE against taxable pay — confirm, as above."),

            ("PERSONAL_RELIEF", "Personal Relief", StatutoryComponent.Paye, StatutoryRateType.FixedAmount,
                null, 2_400m, null, null, null, null, null, false,
                new DateTime(2023, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                "Income Tax Act (seeded from record — NOT verified)", "",
                "Subtracted from computed PAYE, not from gross. No GL account of its own — it reduces the PAYE posting."),

            ("HELB", "HELB Repayment", StatutoryComponent.Helb, StatutoryRateType.PerEmployeeAmount,
                null, null, null, null, null, null, null, false,
                new DateTime(2023, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                "Higher Education Loans Board (per-employee amount)", "",
                "Statutory but owed only by staff with a student loan, so the amount comes from that employee's HELB deduction record."),
        };

        var accounts = await SafeAccountsAsync();
        int created = 0, mapped = 0;
        var unmapped = new List<string>();

        foreach (var s in seed)
        {
            if (await statutoryRates.Query().AnyAsync(r => r.Code == s.Code && r.EffectiveFrom == s.From)) continue;

            var entity = new StatutoryRate
            {
                Code = s.Code, Name = s.Name, Component = s.Component, RateType = s.Type,
                Rate = s.Rate, FixedAmount = s.Fixed,
                TierLowerBound = s.TierLower, TierUpperBound = s.TierUpper,
                MinAmount = s.Min, MaxAmount = s.Max, EmployerRate = s.EmployerRate,
                ReducesTaxableIncome = s.PreTax,
                EffectiveFrom = s.From, NeedsConfirmation = true, Source = s.Source, Notes = s.Notes,
                CreatedBy = userId, UpdatedBy = userId,
            };

            if (!string.IsNullOrEmpty(s.Gl))
            {
                var account = accounts.FirstOrDefault(a => a.Code == s.Gl);
                if (account is not null)
                {
                    entity.GlAccountId = account.Id; entity.GlAccountCode = account.Code; entity.GlAccountName = account.Name;
                    mapped++;
                }
                else unmapped.Add(s.Code);
            }

            await statutoryRates.CreateAsync(entity);
            created++;
        }

        if (created == 0)
            return new PayrollActionResult("NoChange", "The statutory rates are already installed.");

        var result = new PayrollActionResult("Created", $"{created} statutory rule(s) installed; {mapped} mapped to a GL account.");
        result.Warnings.Add("Every seeded figure is flagged as unconfirmed — NSSF tiers, the SHA rate and the levy all move with legislation. Check them, then confirm.");
        if (unmapped.Count > 0)
            result.Warnings.Add($"No GL account found for: {string.Join(", ", unmapped)}.");
        // The known COA gap, called out rather than papered over.
        result.Warnings.Add("HELB has no GL account in finance's chart of accounts — one needs adding there before H6 can post it.");

        await LogAsync("StatutoryRate", "seed", HrAuditAction.StatutoryRateConfigured,
            $"Seeded {created} statutory rate(s) ({mapped} GL-mapped), all flagged for confirmation.", userId);
        return result;
    }

    public async Task<PayrollActionResult> SaveStatutoryRateAsync(string? id, SaveStatutoryRateDto dto, string userId)
    {
        var code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return Err("A statutory rate needs a code.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A statutory rate needs a name.");
        if (!Enum.TryParse<StatutoryComponent>(dto.Component, true, out var component) || component == StatutoryComponent.None)
            return Err("The component must be one of Paye, Nssf, Sha, HousingLevy or Helb.");
        if (!Enum.TryParse<StatutoryRateType>(dto.RateType, true, out var rateType))
            return Err("The rate type must be PercentOfGross, TieredPercent, FixedAmount or PerEmployeeAmount.");

        var problem = ValidateRateShape(rateType, dto);
        if (problem is not null) return Err(problem);

        var effective = DateTime.SpecifyKind((dto.EffectiveFrom ?? DateTime.UtcNow).Date, DateTimeKind.Utc);
        StatutoryRate rate;
        if (string.IsNullOrWhiteSpace(id))
        {
            if (await statutoryRates.Query().AnyAsync(r => r.Code == code && r.EffectiveFrom == effective))
                return Err($"'{code}' already has a rate effective {effective:yyyy-MM-dd}. Use a different effective date to supersede it.");
            rate = new StatutoryRate { Code = code, CreatedBy = userId, UpdatedBy = userId };
        }
        else
        {
            var found = await statutoryRates.GetByIdAsync(id!);
            if (found is null || found.IsDeleted) return Err("Statutory rate not found.");
            rate = found;
            rate.Code = code;
        }

        rate.Name = dto.Name.Trim();
        rate.Component = component;
        rate.RateType = rateType;
        rate.Rate = dto.Rate;
        rate.FixedAmount = dto.FixedAmount;
        rate.TierLowerBound = dto.TierLowerBound;
        rate.TierUpperBound = dto.TierUpperBound;
        rate.MinAmount = dto.MinAmount;
        rate.MaxAmount = dto.MaxAmount;
        rate.EmployerRate = dto.EmployerRate;
        rate.ReducesTaxableIncome = dto.ReducesTaxableIncome;
        rate.EffectiveFrom = effective;
        rate.Notes = dto.Notes;
        rate.IsActive = dto.IsActive ?? rate.IsActive;
        rate.NeedsConfirmation = false;   // typed in by a person, therefore checked by a person
        rate.Source = string.IsNullOrWhiteSpace(dto.Source) ? rate.Source : dto.Source!.Trim();
        Touch(rate, userId);

        var saved = string.IsNullOrWhiteSpace(id)
            ? await statutoryRates.CreateAsync(rate)
            : await statutoryRates.UpdateAsync(rate);

        var result = new PayrollActionResult(string.IsNullOrWhiteSpace(id) ? "Created" : "Updated",
            $"{saved.Name} saved — {BasisLabel(saved)}.", saved.Id);
        if (rateType != StatutoryRateType.FixedAmount && string.IsNullOrWhiteSpace(saved.GlAccountId))
            result.Warnings.Add("No GL account is mapped — the payroll journal cannot post this liability until one is.");

        await LogAsync("StatutoryRate", saved.Id, HrAuditAction.StatutoryRateConfigured,
            $"Statutory rate {saved.Code} effective {saved.EffectiveFrom:yyyy-MM-dd} saved — {BasisLabel(saved)}.", userId);
        return result;
    }

    public async Task<PayrollActionResult> ConfirmRatesAsync(ConfirmRatesDto dto, string userId, string? userName)
    {
        var ids = dto.Ids.Where(i => !string.IsNullOrWhiteSpace(i)).ToHashSet();
        var note = string.IsNullOrWhiteSpace(dto.Source)
            ? $"Confirmed by {userName ?? userId} on {DateTime.UtcNow:yyyy-MM-dd}."
            : $"Confirmed by {userName ?? userId} on {DateTime.UtcNow:yyyy-MM-dd} against {dto.Source!.Trim()}.";

        var bandsToConfirm = await payeBands.Query()
            .Where(b => b.NeedsConfirmation && (ids.Count == 0 || ids.Contains(b.Id))).ToListAsync();
        foreach (var b in bandsToConfirm)
        {
            b.NeedsConfirmation = false;
            b.Source = Append(b.Source, note);
            Touch(b, userId);
            await payeBands.UpdateAsync(b);
        }

        var ratesToConfirm = await statutoryRates.Query()
            .Where(r => r.NeedsConfirmation && (ids.Count == 0 || ids.Contains(r.Id))).ToListAsync();
        foreach (var r in ratesToConfirm)
        {
            r.NeedsConfirmation = false;
            r.Source = Append(r.Source, note);
            Touch(r, userId);
            await statutoryRates.UpdateAsync(r);
        }

        var total = bandsToConfirm.Count + ratesToConfirm.Count;
        if (total == 0) return new PayrollActionResult("NoChange", "Nothing was awaiting confirmation.");

        await LogAsync("StatutoryRate", "confirm", HrAuditAction.StatutoryRatesConfirmed,
            $"{bandsToConfirm.Count} PAYE band(s) and {ratesToConfirm.Count} statutory rate(s) confirmed by {userName ?? userId}.", userId, userName);
        return new PayrollActionResult("Confirmed",
            $"{bandsToConfirm.Count} PAYE band(s) and {ratesToConfirm.Count} statutory rate(s) confirmed.");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Deduction catalogue and applications (P8)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<PayrollDeductionTypeDto>> ListDeductionTypesAsync(bool includeInactive)
    {
        var q = deductionTypes.Query().AsNoTracking();
        if (!includeInactive) q = q.Where(t => t.IsActive);
        var list = await q.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name).ToListAsync();

        var counts = await deductions.Query().AsNoTracking().Where(d => d.IsActive)
            .GroupBy(d => d.DeductionTypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TypeId, x => x.Count);

        return list.Select(t => ToDto(t, counts.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<PayrollActionResult> SeedDefaultDeductionTypesAsync(string userId)
    {
        var seed = new (string Code, string Name, DeductionCategory Category, bool Taxable, bool Recurring, int Order, string Description)[]
        {
            ("HELB",    "HELB Repayment",  DeductionCategory.Statutory, false, true,  10, "The per-employee half of the statutory HELB rule — only staff with a student loan carry it."),
            ("SACCO",   "SACCO Deduction", DeductionCategory.Voluntary, true,  true,  20, "Pre-tax where the SACCO is a registered scheme."),
            ("LOAN",    "Staff Loan",      DeductionCategory.Loan,      false, true,  30, "Repayment of a company loan."),
            ("ADVANCE", "Salary Advance",  DeductionCategory.Advance,   false, false, 40, "Recovery of an advance, taken once."),
            ("COURT",   "Court Order",     DeductionCategory.CourtOrder,false, true,  50, "Attachment of earnings — after tax, and not optional."),
        };

        var created = 0;
        foreach (var s in seed)
        {
            if (await deductionTypes.Query().AnyAsync(t => t.Code == s.Code)) continue;
            await deductionTypes.CreateAsync(new PayrollDeductionType
            {
                Code = s.Code, Name = s.Name, Description = s.Description,
                Category = s.Category, ReducesTaxableIncome = s.Taxable, IsRecurring = s.Recurring,
                DisplayOrder = s.Order,
                CreatedBy = userId, UpdatedBy = userId,
            });
            created++;
        }

        if (created == 0) return new PayrollActionResult("NoChange", "The deduction catalogue is already installed.");

        var result = new PayrollActionResult("Created", $"{created} deduction type(s) installed.");
        result.Warnings.Add("None are GL-mapped yet — map each to the account it should post to.");

        await LogAsync("PayrollDeductionType", "seed", HrAuditAction.DeductionTypeConfigured,
            $"Seeded {created} deduction type(s).", userId);
        return result;
    }

    public async Task<PayrollActionResult> CreateDeductionTypeAsync(SavePayrollDeductionTypeDto dto, string userId)
    {
        var code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return Err("A deduction type needs a code.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A deduction type needs a name.");
        if (!Enum.TryParse<DeductionCategory>(dto.Category, true, out var category))
            return Err("The category must be Statutory, Loan, Voluntary, CourtOrder or Advance.");
        if (await deductionTypes.Query().AnyAsync(t => t.Code == code))
            return Err($"Deduction type '{code}' already exists.");

        var created = await deductionTypes.CreateAsync(new PayrollDeductionType
        {
            Code = code, Name = dto.Name.Trim(), Description = dto.Description,
            Category = category, ReducesTaxableIncome = dto.ReducesTaxableIncome, IsRecurring = dto.IsRecurring,
            IsActive = dto.IsActive ?? true, DisplayOrder = dto.DisplayOrder,
            CreatedBy = userId, UpdatedBy = userId,
        });

        var result = new PayrollActionResult("Created", $"{created.Name} added to the deduction catalogue.", created.Id);
        result.Warnings.Add("No GL account is mapped yet.");

        await LogAsync("PayrollDeductionType", created.Id, HrAuditAction.DeductionTypeConfigured,
            $"Deduction type {created.Code} ({created.Name}) created — {created.Category}, {(created.IsRecurring ? "recurring" : "one-off")}, {(created.ReducesTaxableIncome ? "pre-tax" : "post-tax")}.", userId);
        return result;
    }

    public async Task<PayrollActionResult> UpdateDeductionTypeAsync(string id, SavePayrollDeductionTypeDto dto, string userId)
    {
        var t = await deductionTypes.GetByIdAsync(id);
        if (t is null || t.IsDeleted) return Err("Deduction type not found.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A deduction type needs a name.");
        if (!Enum.TryParse<DeductionCategory>(dto.Category, true, out var category))
            return Err("The category must be Statutory, Loan, Voluntary, CourtOrder or Advance.");

        var result = new PayrollActionResult("Updated", $"{dto.Name} updated.", t.Id);
        var live = await deductions.Query().CountAsync(d => d.DeductionTypeId == id && d.IsActive);
        if (dto.IsActive == false && live > 0)
            return Err($"{live} employee(s) still carry this deduction — stop those first.");
        if (live > 0 && t.ReducesTaxableIncome != dto.ReducesTaxableIncome)
            result.Warnings.Add($"{live} live deduction(s) use this type; changing whether it is taken pre-tax changes their PAYE from the next run.");

        t.Name = dto.Name.Trim();
        t.Description = dto.Description;
        t.Category = category;
        t.ReducesTaxableIncome = dto.ReducesTaxableIncome;
        t.IsRecurring = dto.IsRecurring;
        t.DisplayOrder = dto.DisplayOrder;
        if (dto.IsActive.HasValue) t.IsActive = dto.IsActive.Value;
        Touch(t, userId);
        await deductionTypes.UpdateAsync(t);

        await LogAsync("PayrollDeductionType", t.Id, HrAuditAction.DeductionTypeConfigured,
            $"Deduction type {t.Code} updated.", userId);
        return result;
    }

    public async Task<List<PayrollDeductionDto>> ListDeductionsAsync(string? employeeId, bool includeInactive)
    {
        var q = deductions.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(d => d.EmployeeId == employeeId);
        if (!includeInactive) q = q.Where(d => d.IsActive);
        var list = await q.OrderBy(d => d.EmployeeName).ThenBy(d => d.DeductionTypeName).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<PayrollActionResult> AddDeductionAsync(AddDeductionDto dto, string userId)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId ?? string.Empty);
        if (employee is null || employee.IsDeleted) return Err("Employee not found.");
        if (!OnPayroll.Contains(employee.Status))
            return Err($"{employee.FullName} is {employee.Status} and is no longer on the payroll.");

        var type = await deductionTypes.GetByIdAsync(dto.DeductionTypeId ?? string.Empty);
        if (type is null || type.IsDeleted) return Err("Deduction type not found.");
        if (!type.IsActive) return Err($"Deduction type {type.Name} is not active.");
        if (dto.Amount <= 0) return Err("A deduction must be greater than zero.");

        var start = string.IsNullOrWhiteSpace(dto.StartPeriodId)
            ? await FindCurrentPeriodAsync()
            : await periods.GetByIdAsync(dto.StartPeriodId!);
        if (start is null || start.IsDeleted)
            return Err(string.IsNullOrWhiteSpace(dto.StartPeriodId)
                ? "No open payroll period covers today — generate this year's periods first."
                : "Start payroll period not found.");
        if (start.Status != PayrollPeriodStatus.Open)
            return Err($"Payroll period {start.Code} is {start.Status.ToString().ToLowerInvariant()} — a deduction can only start in an open period.");

        // A one-off is taken once, in the period it starts (P8 step 8.3), so it ends where it begins whatever
        // the caller asked for.
        PayrollPeriod? end = null;
        if (!type.IsRecurring) end = start;
        else if (!string.IsNullOrWhiteSpace(dto.EndPeriodId))
        {
            end = await periods.GetByIdAsync(dto.EndPeriodId!);
            if (end is null || end.IsDeleted) return Err("End payroll period not found.");
            if (string.CompareOrdinal(end.Code, start.Code) < 0)
                return Err("The end period cannot fall before the start period.");
        }

        var duplicate = await deductions.Query().AnyAsync(d => d.EmployeeId == employee.Id
            && d.DeductionTypeId == type.Id && d.IsActive);
        if (duplicate)
            return Err($"{employee.FullName} already has a live {type.Name} deduction — stop it before adding another.");

        var created = await deductions.CreateAsync(new PayrollDeduction
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            DeductionTypeId = type.Id,
            DeductionTypeCode = type.Code,
            DeductionTypeName = type.Name,
            Category = type.Category,
            Amount = dto.Amount,
            StartPeriodId = start.Id, StartPeriodCode = start.Code,
            EndPeriodId = end?.Id, EndPeriodCode = end?.Code,
            AddedBy = userId, AddedAt = DateTime.UtcNow,
            Notes = dto.Notes,
            CreatedBy = userId, UpdatedBy = userId,
        });

        var result = new PayrollActionResult("Created",
            $"{type.Name} of {Money(dto.Amount, "KES")} applied to {employee.FullName} — {ScheduleLabel(created)}.", created.Id);
        if (!type.IsRecurring && !string.IsNullOrWhiteSpace(dto.EndPeriodId) && dto.EndPeriodId != start.Id)
            result.Warnings.Add($"{type.Name} is a one-off type, so it ends in {start.Code} regardless of the end period given.");
        if (string.IsNullOrWhiteSpace(type.GlAccountId))
            result.Warnings.Add($"{type.Name} has no GL account mapped.");

        await LogAsync("PayrollDeduction", created.Id, HrAuditAction.DeductionApplied,
            $"{employee.EmployeeNumber}: {type.Code} {Money(dto.Amount, "KES")} — {ScheduleLabel(created)}.", userId);
        return result;
    }

    public async Task<PayrollActionResult> StopDeductionAsync(string id, StopDeductionDto dto, string userId)
    {
        var d = await deductions.GetByIdAsync(id);
        if (d is null || d.IsDeleted) return Err("Deduction not found.");
        if (!d.IsActive) return new PayrollActionResult("NoChange", "That deduction has already been stopped.", d.Id);

        var end = string.IsNullOrWhiteSpace(dto.EndPeriodId)
            ? await FindCurrentPeriodAsync()
            : await periods.GetByIdAsync(dto.EndPeriodId!);
        if (end is null || end.IsDeleted) return Err("End payroll period not found.");
        if (string.CompareOrdinal(end.Code, d.StartPeriodCode) < 0)
            return Err($"The deduction started in {d.StartPeriodCode} — it cannot be capped before then.");

        d.IsActive = false;
        d.EndPeriodId = end.Id;
        d.EndPeriodCode = end.Code;
        d.RemovedBy = userId;
        d.RemovedAt = DateTime.UtcNow;
        d.Notes = Append(d.Notes, string.IsNullOrWhiteSpace(dto.Reason) ? "Stopped." : $"Stopped: {dto.Reason!.Trim()}");
        Touch(d, userId);
        await deductions.UpdateAsync(d);

        await LogAsync("PayrollDeduction", d.Id, HrAuditAction.DeductionStopped,
            $"{d.EmployeeNumber}: {d.DeductionTypeCode} stopped after {end.Code}.{(string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" Reason: {dto.Reason!.Trim()}")}", userId);
        return new PayrollActionResult("Stopped",
            $"{d.DeductionTypeName} for {d.EmployeeName} stops after {end.Code}. The record is kept, not deleted.", d.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>The period covering today, falling back to the earliest open period so a tenant that has not
    /// generated the current year can still be configured.</summary>
    private async Task<PayrollPeriod?> FindCurrentPeriodAsync()
    {
        var today = DateTime.UtcNow.Date;
        var open = await periods.Query().Where(p => p.Status == PayrollPeriodStatus.Open)
            .OrderBy(p => p.Year).ThenBy(p => p.Month).ToListAsync();
        return open.FirstOrDefault(p => p.StartDate.Date <= today && p.EndDate.Date >= today)
            ?? open.FirstOrDefault(p => p.StartDate.Date > today)
            ?? open.LastOrDefault();
    }

    /// <summary>Rows whose effective window contains <paramref name="on"/>. Where several versions of the same
    /// rule are dated, the latest one that has already started wins.</summary>
    private static List<T> InForce<T>(IEnumerable<T> rows, DateTime on, Func<T, DateTime> from, Func<T, DateTime?> to)
        => rows.Where(r => from(r).Date <= on && (to(r) is null || to(r)!.Value.Date >= on)).ToList();

    /// <summary>One assignment per employee — the approved one with the latest effective period.</summary>
    private static IEnumerable<EmployeeSalary> CurrentAssignments(IEnumerable<EmployeeSalary> approved)
        => approved.GroupBy(s => s.EmployeeId)
                   .Select(g => g.OrderByDescending(s => s.EffectiveFromPeriodCode).First());

    /// <summary>Finance's accounts, or an empty list — a seeder must not fail because finance is down.</summary>
    private async Task<List<GlAccountDto>> SafeAccountsAsync()
    {
        try { return await finance.ListAccountsAsync(); }
        catch { return []; }
    }

    private async Task<(GlAccountDto? Account, string? Error)> ResolveAccountAsync(string? accountId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return (null, "A GL account is required.");
        var accounts = await finance.ListAccountsAsync(ct);
        if (accounts.Count == 0)
            return (null, "Finance's chart of accounts could not be read, so the account cannot be verified. Try again once finance is reachable.");

        var account = accounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null) return (null, "That account is not in finance's chart of accounts.");
        if (!account.IsActive) return (null, $"Account {account.Code} is not active in finance.");
        // Posting to a header account is rejected by finance anyway; catching it here says why.
        if (!account.IsDirectPosting) return (null, $"Account {account.Code} — {account.Name} is a header account and cannot be posted to.");
        return (account, null);
    }

    /// <summary>The scale must cover every shilling exactly once: start at zero, each band picking up where the
    /// last left off, and one open-ended band at the top. A gap silently under-taxes and an overlap double-taxes,
    /// and neither shows up as an error at payroll time — it just produces a wrong number.</summary>
    private static List<string> ValidateBands(List<PayeTaxBand> bands)
    {
        var problems = new List<string>();
        if (bands.Count == 0) return problems;

        var ordered = bands.OrderBy(b => b.BandOrder).ToList();
        if (ordered[0].LowerBound != 0)
            problems.Add($"The PAYE scale starts at {ordered[0].LowerBound:N0} rather than 0 — income below that would not be taxed.");

        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = ordered[i - 1];
            var current = ordered[i];
            if (previous.UpperBound is null)
            {
                problems.Add($"PAYE band {previous.BandOrder} has no upper bound but is not the last band.");
                continue;
            }
            if (previous.UpperBound != current.LowerBound)
                problems.Add(previous.UpperBound < current.LowerBound
                    ? $"PAYE gap between {previous.UpperBound:N0} and {current.LowerBound:N0} — that income would not be taxed."
                    : $"PAYE bands {previous.BandOrder} and {current.BandOrder} overlap between {current.LowerBound:N0} and {previous.UpperBound:N0}.");
        }

        if (ordered[^1].UpperBound is not null)
            problems.Add($"The top PAYE band stops at {ordered[^1].UpperBound:N0} — income above that would not be taxed.");

        return problems;
    }

    private static string? ValidateRateShape(StatutoryRateType type, SaveStatutoryRateDto dto) => type switch
    {
        StatutoryRateType.PercentOfGross when dto.Rate is null or <= 0 => "A percent-of-gross rate needs a percentage.",
        StatutoryRateType.PercentOfGross when dto.Rate > 100 => "A percentage cannot exceed 100.",
        StatutoryRateType.TieredPercent when dto.Rate is null or <= 0 => "A tiered rate needs a percentage.",
        StatutoryRateType.TieredPercent when dto.TierUpperBound is not null && dto.TierLowerBound is not null
            && dto.TierUpperBound <= dto.TierLowerBound => "The tier's upper bound must be above its lower bound.",
        StatutoryRateType.FixedAmount when dto.FixedAmount is null or <= 0 => "A fixed statutory amount needs a figure.",
        _ => dto.MinAmount is not null && dto.MaxAmount is not null && dto.MaxAmount < dto.MinAmount
            ? "The maximum cannot be below the minimum."
            : null,
    };

    private static (string? Error, SalaryComponentType Type, ComponentCalculationType Calc, StatutoryComponent Stat)
        ParseComponent(SaveSalaryComponentDto dto)
    {
        if (!Enum.TryParse<SalaryComponentType>(dto.ComponentType, true, out var type))
            return ("A component must be an Earning or a Deduction.", default, default, default);
        if (!Enum.TryParse<ComponentCalculationType>(dto.CalculationType, true, out var calc))
            return ("The calculation must be FixedAmount, PercentOfBasic, PercentOfGross, Statutory or VariableInput.", default, default, default);

        var stat = StatutoryComponent.None;
        if (!string.IsNullOrWhiteSpace(dto.Statutory) && !Enum.TryParse(dto.Statutory, true, out stat))
            return ("The statutory rule must be None, Paye, Nssf, Sha, HousingLevy or Helb.", default, default, default);

        var error = calc switch
        {
            ComponentCalculationType.FixedAmount when dto.Amount is null or < 0 => "A fixed component needs an amount.",
            ComponentCalculationType.PercentOfBasic or ComponentCalculationType.PercentOfGross
                when dto.Percentage is null or <= 0 => "A percentage component needs a percentage above zero.",
            ComponentCalculationType.PercentOfBasic or ComponentCalculationType.PercentOfGross
                when dto.Percentage > 100 => "A percentage component cannot exceed 100 percent.",
            ComponentCalculationType.Statutory when stat == StatutoryComponent.None
                => "A statutory component must say which rule computes it.",
            _ => null,
        };
        return (error, type, calc, stat);
    }

    private static void ApplyComponent(SalaryComponent entity, SaveSalaryComponentDto dto,
        (string? Error, SalaryComponentType Type, ComponentCalculationType Calc, StatutoryComponent Stat) parsed)
    {
        entity.Name = dto.Name.Trim();
        entity.ComponentType = parsed.Type;
        entity.CalculationType = parsed.Calc;
        entity.Statutory = parsed.Stat;
        // Only the field the calculation actually uses is kept, so a component cannot carry a stale amount
        // from a calculation type it no longer has.
        entity.Amount = parsed.Calc == ComponentCalculationType.FixedAmount ? dto.Amount : null;
        entity.Percentage = parsed.Calc is ComponentCalculationType.PercentOfBasic or ComponentCalculationType.PercentOfGross
            ? dto.Percentage : null;
        entity.IsTaxable = dto.IsTaxable;
        entity.ComponentOrder = dto.ComponentOrder;
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;
    }

    private static void ApplyGrade(JobGrade entity, SaveJobGradeDto dto)
    {
        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description;
        entity.MinSalary = dto.MinSalary;
        entity.MaxSalary = dto.MaxSalary;
        entity.DisplayOrder = dto.DisplayOrder;
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;
    }

    private static string? ValidateBand(SaveJobGradeDto dto)
    {
        if (dto.MinSalary is < 0 || dto.MaxSalary is < 0) return "A pay band cannot be negative.";
        if (dto.MinSalary is not null && dto.MaxSalary is not null && dto.MaxSalary < dto.MinSalary)
            return "The band maximum cannot be below its minimum.";
        return null;
    }

    /// <summary>Out-of-band pay is a warning, never a refusal: paying outside a grade is a real decision HR and
    /// the MD make deliberately, and blocking it would only teach people to edit the grade instead.</summary>
    private async Task<string?> BandWarningAsync(SalaryStructure structure, decimal basic)
    {
        if (string.IsNullOrWhiteSpace(structure.JobGradeId)) return null;
        var grade = await grades.GetByIdAsync(structure.JobGradeId!);
        if (grade is null || grade.IsDeleted) return null;

        if (grade.MinSalary is not null && basic < grade.MinSalary)
            return $"{Money(basic, structure.CurrencyCode)} is below grade {grade.Code}'s minimum of {Money(grade.MinSalary.Value, structure.CurrencyCode)}.";
        if (grade.MaxSalary is not null && basic > grade.MaxSalary)
            return $"{Money(basic, structure.CurrencyCode)} is above grade {grade.Code}'s maximum of {Money(grade.MaxSalary.Value, structure.CurrencyCode)}.";
        return null;
    }

    // ── Mapping ──
    private static SalaryStructureDto ToDto(SalaryStructure s, List<SalaryComponent> comps, int assigned) => new()
    {
        Id = s.Id, Name = s.Name, Description = s.Description,
        JobGradeId = s.JobGradeId, JobGradeCode = s.JobGradeCode,
        CurrencyCode = s.CurrencyCode, IsActive = s.IsActive,
        Components = comps.OrderBy(c => c.ComponentOrder).ThenBy(c => c.Code).Select(ToDto).ToList(),
        AssignedEmployees = assigned,
        ComponentsWithoutGlAccount = comps.Count(c => c.IsActive && string.IsNullOrWhiteSpace(c.GlAccountId)),
    };

    private static SalaryComponentDto ToDto(SalaryComponent c) => new()
    {
        Id = c.Id, SalaryStructureId = c.SalaryStructureId, Code = c.Code, Name = c.Name,
        ComponentType = c.ComponentType.ToString(), CalculationType = c.CalculationType.ToString(),
        Amount = c.Amount, Percentage = c.Percentage, Statutory = c.Statutory.ToString(),
        IsTaxable = c.IsTaxable,
        GlAccountId = c.GlAccountId, GlAccountCode = c.GlAccountCode, GlAccountName = c.GlAccountName,
        ComponentOrder = c.ComponentOrder, IsActive = c.IsActive,
        Basis = BasisLabel(c),
    };

    private static PayrollPeriodDto ToDto(PayrollPeriod p, bool isCurrent) => new()
    {
        Id = p.Id, Code = p.Code, Year = p.Year, Month = p.Month,
        StartDate = p.StartDate, EndDate = p.EndDate,
        CutOffDate = p.CutOffDate, PaymentDate = p.PaymentDate,
        Status = p.Status.ToString(), LockedAt = p.LockedAt, ClosedAt = p.ClosedAt,
        Notes = p.Notes, IsCurrent = isCurrent,
    };

    private static EmployeeSalaryDto ToDto(EmployeeSalary s) => new()
    {
        Id = s.Id, EmployeeId = s.EmployeeId, EmployeeNumber = s.EmployeeNumber, EmployeeName = s.EmployeeName,
        SalaryStructureId = s.SalaryStructureId, SalaryStructureName = s.SalaryStructureName,
        BasicSalary = s.BasicSalary, CurrencyCode = s.CurrencyCode,
        EffectiveFromPeriodId = s.EffectiveFromPeriodId, EffectiveFromPeriodCode = s.EffectiveFromPeriodCode,
        Status = s.Status.ToString(),
        ProposedBy = s.ProposedBy, ProposedAt = s.ProposedAt,
        ApprovedBy = s.ApprovedBy, ApprovedAt = s.ApprovedAt,
        RejectionReason = s.RejectionReason, SupersededById = s.SupersededById, Notes = s.Notes,
    };

    private static PayeTaxBandDto ToDto(PayeTaxBand b) => new()
    {
        Id = b.Id, LowerBound = b.LowerBound, UpperBound = b.UpperBound, Rate = b.Rate,
        BandOrder = b.BandOrder, EffectiveFrom = b.EffectiveFrom, EffectiveTo = b.EffectiveTo,
        NeedsConfirmation = b.NeedsConfirmation, Source = b.Source, IsActive = b.IsActive,
        Band = BandLabel(b),
    };

    private static StatutoryRateDto ToDto(StatutoryRate r) => new()
    {
        Id = r.Id, Code = r.Code, Name = r.Name,
        Component = r.Component.ToString(), RateType = r.RateType.ToString(),
        Rate = r.Rate, FixedAmount = r.FixedAmount,
        TierLowerBound = r.TierLowerBound, TierUpperBound = r.TierUpperBound,
        MinAmount = r.MinAmount, MaxAmount = r.MaxAmount, EmployerRate = r.EmployerRate,
        ReducesTaxableIncome = r.ReducesTaxableIncome,
        GlAccountId = r.GlAccountId, GlAccountCode = r.GlAccountCode, GlAccountName = r.GlAccountName,
        EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo,
        NeedsConfirmation = r.NeedsConfirmation, Source = r.Source, IsActive = r.IsActive, Notes = r.Notes,
        Basis = BasisLabel(r),
    };

    private static PayrollDeductionTypeDto ToDto(PayrollDeductionType t, int active) => new()
    {
        Id = t.Id, Code = t.Code, Name = t.Name, Description = t.Description,
        Category = t.Category.ToString(), ReducesTaxableIncome = t.ReducesTaxableIncome, IsRecurring = t.IsRecurring,
        GlAccountId = t.GlAccountId, GlAccountCode = t.GlAccountCode, GlAccountName = t.GlAccountName,
        IsActive = t.IsActive, DisplayOrder = t.DisplayOrder, ActiveDeductions = active,
    };

    private static PayrollDeductionDto ToDto(PayrollDeduction d) => new()
    {
        Id = d.Id, EmployeeId = d.EmployeeId, EmployeeNumber = d.EmployeeNumber, EmployeeName = d.EmployeeName,
        DeductionTypeId = d.DeductionTypeId, DeductionTypeCode = d.DeductionTypeCode, DeductionTypeName = d.DeductionTypeName,
        Category = d.Category.ToString(), Amount = d.Amount,
        StartPeriodId = d.StartPeriodId, StartPeriodCode = d.StartPeriodCode,
        EndPeriodId = d.EndPeriodId, EndPeriodCode = d.EndPeriodCode,
        IsActive = d.IsActive, AddedBy = d.AddedBy, AddedAt = d.AddedAt,
        RemovedBy = d.RemovedBy, RemovedAt = d.RemovedAt, Notes = d.Notes,
        Schedule = ScheduleLabel(d),
    };

    // ── Labels ──
    private static string Money(decimal amount, string? currency) => $"{currency ?? "KES"} {amount:N2}";

    private static string BandLabel(PayeTaxBand b) => b.UpperBound is null
        ? $"Above {b.LowerBound:N0} — {b.Rate:0.##}%"
        : b.LowerBound == 0
            ? $"On the first {b.UpperBound:N0} — {b.Rate:0.##}%"
            : $"{b.LowerBound:N0} to {b.UpperBound:N0} — {b.Rate:0.##}%";

    private static string BandLabel(JobGrade g) => (g.MinSalary, g.MaxSalary) switch
    {
        (null, null) => "",
        (not null, null) => $" (from {g.MinSalary:N0})",
        (null, not null) => $" (up to {g.MaxSalary:N0})",
        _ => $" ({g.MinSalary:N0}–{g.MaxSalary:N0})",
    };

    private static string BasisLabel(SalaryComponent c) => c.CalculationType switch
    {
        ComponentCalculationType.FixedAmount => Money(c.Amount ?? 0, null),
        ComponentCalculationType.PercentOfBasic => $"{c.Percentage:0.##}% of basic",
        ComponentCalculationType.PercentOfGross => $"{c.Percentage:0.##}% of gross",
        ComponentCalculationType.Statutory => $"{c.Statutory} (statutory)",
        _ => "entered each period",
    };

    private static string BasisLabel(StatutoryRate r)
    {
        var basis = r.RateType switch
        {
            StatutoryRateType.PercentOfGross => $"{r.Rate:0.##}% of gross",
            StatutoryRateType.TieredPercent => $"{r.Rate:0.##}% of earnings from {r.TierLowerBound:N0} to {(r.TierUpperBound is null ? "no ceiling" : $"{r.TierUpperBound:N0}")}",
            StatutoryRateType.FixedAmount => Money(r.FixedAmount ?? 0, null),
            _ => "per-employee amount",
        };
        if (r.MinAmount is not null) basis += $", minimum {r.MinAmount:N0}";
        if (r.MaxAmount is not null) basis += $", capped at {r.MaxAmount:N0}";
        if (r.EmployerRate is not null) basis += $", employer {r.EmployerRate:0.##}%";
        return basis;
    }

    private static string ScheduleLabel(PayrollDeduction d)
    {
        if (d.EndPeriodCode is not null && d.EndPeriodCode == d.StartPeriodCode) return $"one-off in {d.StartPeriodCode}";
        return d.EndPeriodCode is null
            ? $"{Money(d.Amount, null)} monthly from {d.StartPeriodCode}"
            : $"{Money(d.Amount, null)} monthly, {d.StartPeriodCode} to {d.EndPeriodCode}";
    }

    private static string Append(string? existing, string addition)
        => string.IsNullOrWhiteSpace(existing) ? addition : $"{existing} {addition}";

    private static PayrollActionResult Err(string message) => new("Error", message);
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
