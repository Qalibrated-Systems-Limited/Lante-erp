using FluentAssertions;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Services;
using HrService.Infrastructure.Data;
using HrService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HrService.Tests;

/// <summary>
/// The statutory figures Lante actually ships, pinned (#212).
///
/// <para><b>How this differs from <see cref="PayrollStatutoryTests"/>, which deliberately refuses to
/// assert rates.</b> That file tests the ENGINE against invented round numbers, and its reasoning is
/// right: baking today's rates into an arithmetic test would fail on a perfectly correct Finance Act
/// change and pass while a figure went stale.</para>
///
/// <para>This file asserts something different — not what Kenyan law says, but <b>what
/// <c>SeedStatutoryRatesAsync</c> puts in the database</b>. Those are our constants, in our source,
/// handed to every new tenant. They had no test at all, so <c>8_000m</c> becoming <c>80_000m</c>, or
/// <c>2.75m</c> becoming <c>27.5m</c>, passed all 78 HR tests and quietly mis-deducted from every
/// employee in every tenant.</para>
///
/// <para>Failing when someone edits a rate is the point, not a flaw. Changing a statutory figure is a
/// legal act; a red test saying "you moved NSSF Tier I from 8,000 — confirm that is intended, and
/// update the effective date" is the review this deserves. Same argument as finance's
/// PersistedEnumOrdinalTests: an assertion that restates a value is worthless when the value is free
/// to change and load-bearing when changes must be deliberate.</para>
///
/// <para>The worked examples matter more than the field assertions. A typo that keeps a constant
/// plausible — 6% becoming 8% — survives a field check by eye but not an assertion that 72,000 of
/// basic yields exactly 4,320.</para>
/// </summary>
public class StatutorySeedDefaultsTests : IDisposable
{
    private readonly HrDbContext _db;
    private readonly PayrollService _svc;

    public StatutorySeedDefaultsTests()
    {
        _db = new HrDbContext(new DbContextOptionsBuilder<HrDbContext>()
            .UseInMemoryDatabase($"hr-statutory-seed-{Guid.NewGuid()}").Options);
        _svc = new PayrollService(
            Repo<Employee>(), Repo<JobGrade>(), Repo<SalaryStructure>(), Repo<SalaryComponent>(),
            Repo<PayrollPeriod>(), Repo<EmployeeSalary>(), Repo<PayeTaxBand>(), Repo<StatutoryRate>(),
            Repo<PayrollDeductionType>(), Repo<PayrollDeduction>(), Repo<HrAuditLog>(),
            new FakeFinanceGateway());
    }

    private IGenericRepository<T> Repo<T>() where T : BaseEntity => new GenericRepository<T>(_db);

    private async Task<StatutoryRate> SeededAsync(string code)
    {
        await _svc.SeedStatutoryRatesAsync("tester");
        _db.ChangeTracker.Clear();
        return await _db.StatutoryRates.SingleAsync(r => r.Code == code);
    }

    // ── NSSF ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NSSF_tier_one_is_six_percent_of_the_first_8000_capped_at_480()
    {
        var t1 = await SeededAsync("NSSF_TIER1");

        t1.RateType.Should().Be(StatutoryRateType.TieredPercent);
        t1.Rate.Should().Be(6m);
        t1.TierLowerBound.Should().Be(0m);
        t1.TierUpperBound.Should().Be(8_000m);
        t1.MaxAmount.Should().Be(480m);
        // The employer matches the employee, and H6 posts that half separately as a cost.
        t1.EmployerRate.Should().Be(6m);
        t1.ReducesTaxableIncome.Should().BeTrue();
    }

    [Fact]
    public async Task NSSF_tier_two_runs_from_8000_to_72000_capped_at_3840()
    {
        var t2 = await SeededAsync("NSSF_TIER2");

        t2.Rate.Should().Be(6m);
        t2.TierLowerBound.Should().Be(8_000m);
        t2.TierUpperBound.Should().Be(72_000m);
        // 6% of the 64,000 the tier spans. If the bound or the rate moves, this stops agreeing.
        t2.MaxAmount.Should().Be(3_840m);
        t2.EmployerRate.Should().Be(6m);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(8_000, 480)]        // tier I exactly full
    [InlineData(20_000, 1_200)]     // 480 + 6% of 12,000
    [InlineData(72_000, 4_320)]     // 480 + 3,840 — the scheme ceiling
    [InlineData(200_000, 4_320)]    // the ceiling holds however high pay goes
    public async Task The_seeded_NSSF_tiers_produce_the_expected_scheme_total(decimal basic, decimal expected)
    {
        await _svc.SeedStatutoryRatesAsync("tester");
        _db.ChangeTracker.Clear();
        var tiers = await _db.StatutoryRates
            .Where(r => r.Component == StatutoryComponent.Nssf).ToListAsync();

        var total = tiers.Sum(t => PayrollRunService.StatutoryAmount(t, basic));

        // Driven through the real engine over the real seeded rows, so this catches a bad constant
        // AND a bad tier interaction — the two halves only compose correctly if the bounds line up.
        total.Should().Be(expected);
    }

    [Fact]
    public async Task The_employer_matches_the_employee_on_NSSF()
    {
        await _svc.SeedStatutoryRatesAsync("tester");
        _db.ChangeTracker.Clear();
        var tiers = await _db.StatutoryRates
            .Where(r => r.Component == StatutoryComponent.Nssf).ToListAsync();

        var employee = tiers.Sum(t => PayrollRunService.StatutoryAmount(t, 72_000m));
        var employer = tiers.Sum(t => PayrollRunService.EmployerAmount(t, 72_000m));

        employer.Should().Be(employee).And.Be(4_320m);
    }

    // ── SHA ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SHA_is_2_75_percent_of_gross_with_a_300_floor()
    {
        var sha = await SeededAsync("SHA");

        sha.RateType.Should().Be(StatutoryRateType.PercentOfGross);
        sha.Rate.Should().Be(2.75m);
        sha.MinAmount.Should().Be(300m);
        sha.MaxAmount.Should().BeNull("SHA is uncapped at the top");
        // Seeded as deductible against taxable pay. Whether that is the correct treatment, and from
        // which date, is the open question in #236 — the seed comment says so too.
        sha.ReducesTaxableIncome.Should().BeTrue();
    }

    [Theory]
    [InlineData(20_000, 550)]
    [InlineData(100_000, 2_750)]
    [InlineData(10_000, 300)]   // 275 computed, floored to the 300 minimum
    public async Task The_seeded_SHA_rate_produces_the_expected_contribution(decimal gross, decimal expected)
    {
        var sha = await SeededAsync("SHA");

        PayrollRunService.StatutoryAmount(sha, gross).Should().Be(expected);
    }

    [Fact]
    public async Task SHA_on_zero_gross_is_floored_to_300_which_is_worth_knowing()
    {
        var sha = await SeededAsync("SHA");

        // Pinned as an OBSERVATION, not an endorsement. Clamp applies MinAmount unconditionally, so an
        // employee with no pay in a period — unpaid leave, a mid-month leaver — is still charged 300
        // and lands on a negative net. Whether Kenya's 300 floor is meant to apply in a zero-pay month
        // is a payroll-law question, not one this test can settle, and it belongs with #236's
        // confirmation of SHA's treatment. Recorded here so the behaviour is known rather than
        // discovered on a payslip.
        PayrollRunService.StatutoryAmount(sha, 0m).Should().Be(300m);
    }

    // ── Housing levy and relief ─────────────────────────────────────────────────

    [Fact]
    public async Task The_housing_levy_is_1_5_percent_matched_by_the_employer_and_uncapped()
    {
        var levy = await SeededAsync("HOUSING_LEVY");

        levy.Rate.Should().Be(1.5m);
        levy.EmployerRate.Should().Be(1.5m);
        levy.MinAmount.Should().BeNull();
        levy.MaxAmount.Should().BeNull("the levy is universal and uncapped");

        PayrollRunService.StatutoryAmount(levy, 50_000m).Should().Be(750m);
        PayrollRunService.EmployerAmount(levy, 50_000m).Should().Be(750m);
    }

    [Fact]
    public async Task Personal_relief_is_a_fixed_2400_that_does_not_reduce_taxable_pay()
    {
        var relief = await SeededAsync("PERSONAL_RELIEF");

        relief.RateType.Should().Be(StatutoryRateType.FixedAmount);
        relief.FixedAmount.Should().Be(2_400m);
        // Subtracted from computed PAYE, not from gross — so it must NOT be pre-tax, or it would
        // relieve the same money twice.
        relief.ReducesTaxableIncome.Should().BeFalse();
    }

    // ── The honesty machinery ───────────────────────────────────────────────────

    [Fact]
    public async Task Every_seeded_rate_is_flagged_as_needing_confirmation()
    {
        await _svc.SeedStatutoryRatesAsync("tester");
        _db.ChangeTracker.Clear();

        var all = await _db.StatutoryRates.ToListAsync();
        all.Should().NotBeEmpty();
        // #244's gate: a run computed from unverified rates cannot be approved without an explicit
        // acknowledgement. If the seeder ever shipped a rate pre-confirmed, that gate would open
        // silently for a figure nobody checked — which is the exact failure the flag exists to stop.
        all.Should().OnlyContain(r => r.NeedsConfirmation);
    }

    [Fact]
    public async Task Every_seeded_rate_records_where_its_figure_came_from()
    {
        await _svc.SeedStatutoryRatesAsync("tester");
        _db.ChangeTracker.Clear();

        var all = await _db.StatutoryRates.ToListAsync();
        // A rate with no cited instrument cannot be re-checked against anything when the next
        // Finance Act lands.
        all.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Source));
    }

    [Fact]
    public async Task Every_seeded_rate_is_effective_dated()
    {
        await _svc.SeedStatutoryRatesAsync("tester");
        _db.ChangeTracker.Clear();

        var all = await _db.StatutoryRates.ToListAsync();
        // Re-running an earlier month must find the rates that applied THEN, which only works if
        // every rate carries the date its instrument took effect.
        all.Should().OnlyContain(r => r.EffectiveFrom != default);
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_the_rates()
    {
        await _svc.SeedStatutoryRatesAsync("tester");
        await _svc.SeedStatutoryRatesAsync("tester");
        _db.ChangeTracker.Clear();

        // Two rows for one component would double every deduction that walks them, and NSSF walks
        // all rows for its component by design.
        var codes = await _db.StatutoryRates.Select(r => r.Code).ToListAsync();
        codes.Should().OnlyHaveUniqueItems();
    }

    public void Dispose() => _db.Dispose();
}
