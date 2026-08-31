using FluentAssertions;
using HrService.Core.DTOs.Payroll;
using HrService.Core.Entities;
using HrService.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HrService.Tests;

/// <summary>
/// The payroll run lifecycle: create, compute, approve, post.
///
/// <para>These are the tests <see cref="PayrollStatutoryTests"/> could not reach. Those cover the
/// arithmetic helpers in isolation; these cover the engine that decides which employees are on the
/// payroll, which salary is in force, what the journal looks like, and who is allowed to approve it.
/// Every figure asserted here was derived by hand from the seeded inputs and then confirmed against a
/// real run — not read off the implementation and pasted back.</para>
///
/// <para>#254 and #255 were both found by building this fixture and are both fixed. The tests that
/// pinned the broken behaviour now assert the correct behaviour instead, and two more cover the paths
/// the fixes introduced: an id finance does not recognise, and finance being unreachable.</para>
/// </summary>
public class PayrollRunTests
{
    // Seeded: basic 100,000 + house 20,000 (taxable) + per diem 5,000 (not taxable).
    private const decimal Gross = 125_000m;
    private const decimal TaxableIncome = 110_367.50m;   // 120,000 taxable earnings − 9,632.50 pre-tax statutory
    private const decimal Paye = 25_493.60m;             // 27,893.60 across the bands, less 2,400 relief
    private const decimal Statutory = 35_126.10m;        // PAYE + AHL 1,875 + SHA 3,437.50 + NSSF 4,320
    private const decimal Net = 89_873.90m;
    private const decimal EmployerCost = 6_195m;         // NSSF 4,320 + AHL 1,875

    private static async Task<string> ComputedRunAsync(PayrollFixture f)
    {
        var created = await f.Runs.CreateRunAsync(
            new CreatePayrollRunDto { PayrollPeriodId = f.PeriodId }, PayrollFixture.Computer);
        created.Id.Should().NotBeNull(because: $"the run should have been created: {created.Message}");
        var computed = await f.Runs.ComputeRunAsync(created.Id!, PayrollFixture.Computer);
        computed.Status.Should().Be("Computed", because: computed.Message);
        return created.Id!;
    }

    private static Task<PayrollActionResult> ApproveAsync(
        PayrollFixture f, string runId, string userId, bool acknowledgeRates = false) =>
        f.Runs.DecideRunAsync(runId,
            new DecidePayrollRunDto { Decision = "Approve", AcknowledgeUnconfirmedRates = acknowledgeRates },
            "tenant_acme", userId, "Test User");

    private static async Task MarkRatesUnconfirmedAsync(PayrollFixture f, bool unconfirmed = true)
    {
        foreach (var rate in await f.Db.StatutoryRates.ToListAsync()) rate.NeedsConfirmation = unconfirmed;
        await f.Db.SaveChangesAsync();
    }

    // ── Segregation of duties ────────────────────────────────────────────────────

    [Fact]
    public async Task The_person_who_computed_a_run_cannot_approve_it()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        var result = await ApproveAsync(f, runId, PayrollFixture.Computer);

        // The largest single payment the business makes each month. One person must not be able to
        // both decide the figures and release them.
        result.Status.Should().Be("Error");
        result.Message.Should().Contain("second officer");
    }

    [Fact]
    public async Task A_second_officer_can_approve_it()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        result.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task A_refused_approval_leaves_the_run_computed_rather_than_half_approved()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Computer);

        var run = await f.Db.PayrollRuns.AsNoTracking().SingleAsync(r => r.Id == runId);
        run.Status.Should().Be(PayrollRunStatus.Computed);
        run.ApprovedBy.Should().BeNull();
        f.Finance.Posted.Should().BeEmpty();
    }

    // ── What the payslip says ────────────────────────────────────────────────────

    [Fact]
    public async Task Gross_is_the_sum_of_the_structure_components()
    {
        using var f = new PayrollFixture();
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        slip.GrossPay.Should().Be(Gross);
        slip.TotalEarnings.Should().Be(Gross);
    }

    [Fact]
    public async Task A_non_taxable_component_is_paid_but_not_taxed()
    {
        using var f = new PayrollFixture();
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        // Per diem (5,000) is in gross but must never reach taxable income. If IsTaxable were ignored,
        // taxable income would be 5,000 higher and every employee would be over-taxed at the margin.
        slip.GrossPay.Should().Be(Gross);
        slip.TaxableIncome.Should().Be(TaxableIncome);
        slip.TaxableIncome.Should().BeLessThan(slip.GrossPay);
    }

    [Fact]
    public async Task Pre_tax_statutory_deductions_reduce_taxable_income_at_the_marginal_rate()
    {
        using var f = new PayrollFixture();
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();

        // NSSF 4,320 + SHA 3,437.50 + AHL 1,875 = 9,632.50, all flagged ReducesTaxableIncome.
        // Taxable = 120,000 taxable earnings − 9,632.50 = 110,367.50.
        // This is the behaviour I once filed an issue claiming was ABSENT (#236) — SHA relief is applied,
        // and applied pre-tax, which relieves at the employee's marginal rate rather than a flat
        // percentage. The test exists so nobody "adds" the relief a second time.
        slip.TaxableIncome.Should().Be(TaxableIncome);
        (120_000m - slip.TaxableIncome).Should().Be(9_632.50m);
    }

    [Fact]
    public async Task A_pre_tax_flexible_deduction_reduces_taxable_income()
    {
        using var f = new PayrollFixture();
        // Mirrors a pension/SACCO-style deduction — the odd-one-out flag this test exists to pin was
        // called IsTaxable but meant the opposite of SalaryComponent.IsTaxable (#239); renamed to
        // ReducesTaxableIncome so the polarity matches its own name.
        f.AddDeduction("SACCO", "SACCO Deduction", DeductionCategory.Voluntary, reducesTaxableIncome: true, amount: 2_000m);
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        (120_000m - slip.TaxableIncome).Should().Be(9_632.50m + 2_000m);
    }

    [Fact]
    public async Task A_post_tax_flexible_deduction_leaves_taxable_income_unchanged()
    {
        using var f = new PayrollFixture();
        // A court order comes off net pay, never off the tax base — the exact failure mode #239
        // warned about: setting this flag true here would under-tax the employee.
        f.AddDeduction("COURT", "Court Order", DeductionCategory.CourtOrder, reducesTaxableIncome: false, amount: 3_000m);
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        slip.TaxableIncome.Should().Be(TaxableIncome);
    }

    [Fact]
    public async Task Personal_relief_reduces_tax_and_is_recorded_separately()
    {
        using var f = new PayrollFixture();
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        slip.PersonalRelief.Should().Be(2_400m);
        slip.Paye.Should().Be(Paye);
        (slip.Paye + slip.PersonalRelief).Should().Be(27_893.60m, because: "relief is subtracted from tax, not from pay");
    }

    [Fact]
    public async Task Relief_never_turns_into_a_refund_when_tax_is_smaller_than_it()
    {
        // Basic 5,000 + the fixed 20,000 house allowance = 30,000 gross. Taxable is
        // 25,000 taxable earnings − 3,075 pre-tax statutory = 21,925, which sits inside the 10% band,
        // so tax before relief is 2,192.50 — less than the 2,400 relief.
        using var f = new PayrollFixture(basicSalary: 5_000m);
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        slip.Paye.Should().Be(0m);
        // Relief is capped at the tax due. Uncapped, a low earner would show negative PAYE and the
        // journal would debit KRA — the business paying tax on the employee's behalf.
        slip.PersonalRelief.Should().BeLessOrEqualTo(2_400m);
        slip.NetPay.Should().BeLessOrEqualTo(slip.GrossPay);
    }

    [Fact]
    public async Task NSSF_charges_only_the_slice_of_pay_inside_its_tier()
    {
        using var f = new PayrollFixture();
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        var nssf = await f.Db.PayslipLines.AsNoTracking()
            .SingleAsync(l => l.PayslipId == slip.Id && l.Code == "NSSF");

        // 6% of the 0–72,000 tier = 4,320, NOT 6% of 125,000 (7,500). Reading the tier as a threshold
        // rather than a slice overcharges every employee above the cap.
        nssf.Amount.Should().Be(4_320m);
    }

    [Fact]
    public async Task Net_is_gross_less_every_deduction()
    {
        using var f = new PayrollFixture();
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        slip.StatutoryDeductions.Should().Be(Statutory);
        slip.TotalDeductions.Should().Be(slip.StatutoryDeductions + slip.OtherDeductions);
        slip.NetPay.Should().Be(Net);
        slip.NetPay.Should().Be(slip.TotalEarnings - slip.TotalDeductions);
    }

    [Fact]
    public async Task Employer_contributions_are_a_cost_to_the_business_and_not_deducted_from_staff()
    {
        using var f = new PayrollFixture();
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        slip.EmployerCost.Should().Be(EmployerCost);
        // The employer's share must not appear in the employee's deductions — that would make staff
        // pay the employer's contribution out of their own salary.
        slip.TotalDeductions.Should().Be(Statutory);
        slip.NetPay.Should().Be(Net);
    }

    // ── The journal ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approval_posts_a_balanced_journal()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Approver);

        f.Finance.Posted.Should().ContainSingle();
        var lines = f.Finance.Posted.Single().Lines;
        lines.Sum(l => l.Debit).Should().Be(lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task The_debit_is_the_full_cost_of_employment()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Approver);

        var lines = f.Finance.Posted.Single().Lines;
        // Gross plus the employer's own contributions — 125,000 + 6,195. Debiting only gross would
        // understate the cost of employment by the employer's share every single month.
        lines.Sum(l => l.Debit).Should().Be(Gross + EmployerCost);
    }

    [Fact]
    public async Task Net_pay_is_credited_to_the_holding_account_and_not_to_the_bank()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Approver);

        var netLine = f.Finance.Posted.Single().Lines
            .Single(l => l.AccountId == FakeFinanceGateway.NetPayHolding);
        // Approval is not payment. Crediting the bank here would claim the money had left before the
        // bank file was even generated, and the reconciliation would never tie.
        netLine.Credit.Should().Be(Net);
        netLine.Debit.Should().Be(0m);
    }

    [Fact]
    public async Task The_employer_contribution_is_debited_as_cost_and_credited_as_a_liability()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Approver);

        var lines = f.Finance.Posted.Single().Lines;
        var payable = lines.Single(l => l.Description.Contains("employer contribution payable"));
        // It is both an expense and money owed to NSSF/the levy. Crediting the account it was debited
        // to would net the whole thing to zero and the liability would never appear.
        payable.Credit.Should().Be(EmployerCost);
        lines.Where(l => l.AccountId == FakeFinanceGateway.SalariesExpense).Sum(l => l.Debit)
             .Should().Be(Gross + EmployerCost);
    }

    [Fact]
    public async Task The_journal_is_tagged_with_the_run_so_finance_can_trace_it_back()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Approver);

        var posted = f.Finance.Posted.Single();
        posted.SourceDocumentId.Should().Be(runId);
        posted.Schema.Should().Be("tenant_acme", because: "the schema is passed explicitly so a background sweep can post too");
    }

    // ── Posting failure is survivable ────────────────────────────────────────────

    [Fact]
    public async Task A_posting_failure_does_not_unwind_the_approval()
    {
        using var f = new PayrollFixture();
        f.Finance.FailPosting = true;
        var runId = await ComputedRunAsync(f);

        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        result.Status.Should().Be("Approved");
        var run = await f.Db.PayrollRuns.AsNoTracking().SingleAsync(r => r.Id == runId);
        // The run is the record of what staff are owed. Finance being unreachable is transient; the
        // ledger catches up on retry rather than the payroll being recomputed.
        run.Status.Should().Be(PayrollRunStatus.Approved);
        run.JournalPostedAt.Should().BeNull();
        run.JournalError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task An_unposted_journal_raises_a_critical_alert_rather_than_only_a_warning()
    {
        using var f = new PayrollFixture();
        f.Finance.FailPosting = true;
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Approver);

        // An approved payroll whose journal never posted is a silent hole in the ledger. A warning on
        // an HTTP response nobody re-reads is not enough.
        f.Alerts.Alerts.Should().Contain(a => a.Severity == "Critical");
    }

    [Fact]
    public async Task A_failed_posting_can_be_retried_without_recomputing()
    {
        using var f = new PayrollFixture();
        f.Finance.FailPosting = true;
        var runId = await ComputedRunAsync(f);
        await ApproveAsync(f, runId, PayrollFixture.Approver);

        f.Finance.FailPosting = false;
        var retry = await f.Runs.RetryJournalAsync(runId, "tenant_acme", PayrollFixture.Approver);

        retry.Status.Should().Be("Posted");
        f.Finance.Posted.Should().ContainSingle();
        var run = await f.Db.PayrollRuns.AsNoTracking().SingleAsync(r => r.Id == runId);
        run.JournalError.Should().BeNull();
    }

    [Fact]
    public async Task Retrying_an_already_posted_run_does_not_post_it_twice()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);
        await ApproveAsync(f, runId, PayrollFixture.Approver);

        var retry = await f.Runs.RetryJournalAsync(runId, "tenant_acme", PayrollFixture.Approver);

        retry.Status.Should().Be("NoChange");
        f.Finance.Posted.Should().ContainSingle(because: "a second journal would double the payroll in the ledger");
    }

    // ── Period and state guards ──────────────────────────────────────────────────

    [Fact]
    public async Task A_run_cannot_be_opened_on_a_closed_period()
    {
        using var f = new PayrollFixture(periodStatus: PayrollPeriodStatus.Closed);

        var created = await f.Runs.CreateRunAsync(
            new CreatePayrollRunDto { PayrollPeriodId = f.PeriodId }, PayrollFixture.Computer);

        created.Status.Should().Be("Error");
        created.Message.Should().Contain("closed");
    }

    [Fact]
    public async Task A_second_run_cannot_be_opened_for_the_same_period()
    {
        using var f = new PayrollFixture();
        await f.Runs.CreateRunAsync(new CreatePayrollRunDto { PayrollPeriodId = f.PeriodId }, PayrollFixture.Computer);

        var second = await f.Runs.CreateRunAsync(
            new CreatePayrollRunDto { PayrollPeriodId = f.PeriodId }, PayrollFixture.Computer);

        second.Status.Should().Be("Error");
        second.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task An_approved_run_is_frozen_against_recompute()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);
        await ApproveAsync(f, runId, PayrollFixture.Approver);

        var recompute = await f.Runs.ComputeRunAsync(runId, PayrollFixture.Computer);

        // Recomputing after approval would change figures somebody has already signed off, and the
        // posted journal would no longer match the payslips.
        recompute.Status.Should().Be("Error");
        recompute.Message.Should().Contain("frozen");
    }

    [Fact]
    public async Task A_successful_posting_closes_the_period()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Approver);

        var period = await f.Db.PayrollPeriods.AsNoTracking().SingleAsync(p => p.Id == f.PeriodId);
        period.Status.Should().Be(PayrollPeriodStatus.Closed);
        period.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_failed_posting_leaves_the_period_open()
    {
        using var f = new PayrollFixture();
        f.Finance.FailPosting = true;
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Approver);

        var period = await f.Db.PayrollPeriods.AsNoTracking().SingleAsync(p => p.Id == f.PeriodId);
        // Closing a period whose journal never reached the ledger would lock the fix out.
        period.Status.Should().NotBe(PayrollPeriodStatus.Closed);
    }

    [Fact]
    public async Task Recompute_before_approval_replaces_the_payslips_rather_than_adding_to_them()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        var again = await f.Runs.ComputeRunAsync(runId, PayrollFixture.Computer);

        again.Status.Should().Be("Computed", because: again.Message);
        // Two payslips for one employee in one run would double what the ledger says is owed.
        (await f.Db.Payslips.AsNoTracking().CountAsync(p => p.PayrollRunId == runId)).Should().Be(1);
    }

    // ── Approval refuses an unpostable run ───────────────────────────────────────

    [Fact]
    public async Task A_run_whose_journal_cannot_be_built_is_not_approved()
    {
        using var f = new PayrollFixture(mapGlAccounts: false);
        var runId = await ComputedRunAsync(f);

        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        // Approval freezes the payslips, so a missing mapping found afterwards leaves the run stuck:
        // unable to post and unable to recompute. Catching it here keeps the fix cheap.
        result.Status.Should().Be("Error");
        result.Message.Should().Contain("No GL account");
        f.Finance.Posted.Should().BeEmpty();
    }

    [Fact]
    public async Task An_account_with_an_id_but_no_code_still_posts()
    {
        using var f = new PayrollFixture();
        // An account id with no display code — plausible, since the id is the foreign key and the code is
        // display data, and nothing on the write path requires both.
        foreach (var rate in await f.Db.StatutoryRates.ToListAsync()) rate.GlAccountCode = null;
        await f.Db.SaveChangesAsync();

        var runId = await ComputedRunAsync(f);
        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        // #255: approval used to validate the id while posting resolved by code, so this approved and then
        // failed to post, reporting that finance had removed an account it never had — leaving the run
        // approved, unpostable, and frozen against recompute. Both steps now use the id.
        result.Status.Should().Be("Approved");
        f.Finance.Posted.Should().ContainSingle();
        var run = await f.Db.PayrollRuns.AsNoTracking().SingleAsync(r => r.Id == runId);
        run.JournalError.Should().BeNull();
        run.JournalPostedAt.Should().NotBeNull();

        // Asserting that *a* journal posted is not enough, and this is the assertion that actually pins the
        // fix: resolving by code drops the code-less lines rather than failing, so a weaker test passes
        // against a journal missing every statutory credit. The debit and credit totals are what prove
        // nothing was lost on the way to finance.
        var lines = f.Finance.Posted.Single().Lines;
        lines.Sum(l => l.Debit).Should().Be(Gross + EmployerCost);
        lines.Sum(l => l.Credit).Should().Be(Gross + EmployerCost);
        lines.Should().Contain(l => l.AccountId == FakeFinanceGateway.StatutoryPayable);
        lines.Single(l => l.AccountId == FakeFinanceGateway.NetPayHolding).Credit.Should().Be(Net);
    }

    [Fact]
    public async Task An_account_id_finance_does_not_recognise_is_refused_at_approval()
    {
        using var f = new PayrollFixture();
        foreach (var rate in await f.Db.StatutoryRates.ToListAsync()) rate.GlAccountId = "acct-deleted";
        await f.Db.SaveChangesAsync();

        var runId = await ComputedRunAsync(f);
        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        // Caught at approval, while recompute is still possible, rather than after approval has frozen the
        // figures. An id that is merely present is not the same as an id finance can post to.
        result.Status.Should().Be("Error");
        result.Message.Should().Contain("No GL account in finance's chart matches");
        f.Finance.Posted.Should().BeEmpty();
    }

    [Fact]
    public async Task Finance_being_unreachable_is_a_survivable_transient_not_a_mapping_error()
    {
        using var f = new PayrollFixture();
        f.Finance.ReturnEmptyChart = true;
        var runId = await ComputedRunAsync(f);

        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        // An outage must not present as bad data. This used to refuse approval with "Account 2110 (the
        // net-pay holding account) is not in finance's chart of accounts. Map the account, then recompute
        // the run." — during an outage, about an account that was mapped correctly. `:896` documents an
        // unreachable finance as a transient the run survives, and now it does.
        result.Status.Should().Be("Approved");
        result.Message.Should().NotContain("Map the account");
        f.Finance.Posted.Should().BeEmpty();
        var run = await f.Db.PayrollRuns.AsNoTracking().SingleAsync(r => r.Id == runId);
        run.JournalError.Should().Contain("could not be read");
    }

    [Fact]
    public async Task A_run_approved_during_a_finance_outage_posts_once_finance_returns()
    {
        using var f = new PayrollFixture();
        f.Finance.ReturnEmptyChart = true;
        var runId = await ComputedRunAsync(f);
        await ApproveAsync(f, runId, PayrollFixture.Approver);

        f.Finance.ReturnEmptyChart = false;
        var retry = await f.Runs.RetryJournalAsync(runId, "tenant_acme", PayrollFixture.Approver);

        // The whole point of treating the outage as transient: no recompute, no re-approval, and the
        // journal that eventually posts is the one built from the payslips that were already signed off.
        retry.Status.Should().Be("Posted");
        var lines = f.Finance.Posted.Single().Lines;
        lines.Sum(l => l.Debit).Should().Be(lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task PAYE_takes_its_posting_account_from_the_PAYE_rate()
    {
        using var f = new PayrollFixture();
        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        var paye = await f.Db.PayslipLines.AsNoTracking()
            .SingleAsync(l => l.PayslipId == slip.Id && l.Code == "PAYE");

        // #254: the fixture's structure has no PAYE component at all. PAYE's account comes from its own
        // StatutoryRate, the same way NSSF's and SHA's do. Before the fix the only candidate was a dummy
        // deduction component, and a structure without one could never be approved.
        paye.GlAccountId.Should().Be(FakeFinanceGateway.StatutoryPayable);
    }

    [Fact]
    public async Task A_PAYE_salary_component_still_overrides_the_rate()
    {
        using var f = new PayrollFixture();
        f.Db.SalaryComponents.Add(
            PayrollFixture.PayeAccountCarrier(f.StructureId, FakeFinanceGateway.SalariesExpense));
        await f.Db.SaveChangesAsync();

        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        var paye = await f.Db.PayslipLines.AsNoTracking()
            .SingleAsync(l => l.PayslipId == slip.Id && l.Code == "PAYE");

        // Tenants who added a carrier component to work around #254 must keep working, and their explicit
        // choice must still win over the rate — otherwise the fix silently reroutes their PAYE postings.
        paye.GlAccountId.Should().Be(FakeFinanceGateway.SalariesExpense);
    }

    [Fact]
    public async Task A_missing_net_pay_holding_account_blocks_approval()
    {
        using var f = new PayrollFixture();
        f.Finance.OmitHoldingAccount = true;
        var runId = await ComputedRunAsync(f);

        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        result.Status.Should().Be("Error");
        result.Message.Should().Contain("2110");
    }

    // ── Who is on the payroll ────────────────────────────────────────────────────

    [Fact]
    public async Task A_proposed_salary_is_not_paid_until_it_is_approved()
    {
        using var f = new PayrollFixture();
        var salary = await f.Db.EmployeeSalaries.SingleAsync();
        salary.Status = SalaryAssignmentStatus.Proposed;
        await f.Db.SaveChangesAsync();

        var created = await f.Runs.CreateRunAsync(
            new CreatePayrollRunDto { PayrollPeriodId = f.PeriodId }, PayrollFixture.Computer);
        var computed = await f.Runs.ComputeRunAsync(created.Id!, PayrollFixture.Computer);

        // The salary-approval control has to be real, not advisory: whoever proposes a figure must not
        // be able to get it paid simply by running payroll. The status filter lives on the query at
        // PayrollRunService.cs:263, not in CurrentSalaryFor — which is why this is worth a test rather
        // than a read. CurrentSalaryFor alone looks like it ignores status.
        computed.Status.Should().Be("Error");
        computed.Message.Should().Contain("Nothing to pay");
        (await f.Db.Payslips.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_superseded_salary_is_still_usable_so_old_periods_can_be_re_run()
    {
        using var f = new PayrollFixture();
        var salary = await f.Db.EmployeeSalaries.SingleAsync();
        salary.Status = SalaryAssignmentStatus.Superseded;
        await f.Db.SaveChangesAsync();

        await ComputedRunAsync(f);

        // Superseded is deliberately included alongside Approved (`:263`). Re-running March next year
        // must use March's salary even though a later assignment has replaced it — otherwise a
        // recomputed historical period silently pays today's rate.
        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        slip.BasicSalary.Should().Be(100_000m);
    }

    [Fact]
    public async Task A_terminated_employee_is_not_on_the_payroll()
    {
        using var f = new PayrollFixture();
        var employee = await f.Db.Employees.SingleAsync();
        employee.Status = EmploymentStatus.Terminated;
        await f.Db.SaveChangesAsync();

        var created = await f.Runs.CreateRunAsync(
            new CreatePayrollRunDto { PayrollPeriodId = f.PeriodId }, PayrollFixture.Computer);
        var computed = await f.Runs.ComputeRunAsync(created.Id!, PayrollFixture.Computer);

        computed.Status.Should().Be("Error");
        computed.Message.Should().Contain("No employees are on the payroll");
    }

    [Fact]
    public async Task A_suspended_employee_is_still_paid()
    {
        using var f = new PayrollFixture();
        var employee = await f.Db.Employees.SingleAsync();
        employee.Status = EmploymentStatus.Suspended;
        await f.Db.SaveChangesAsync();

        await ComputedRunAsync(f);

        // Suspension is not dismissal — withholding pay from a suspended employee without a legal
        // basis is an unlawful deduction under the Employment Act. This is deliberate, not an oversight.
        (await f.Db.Payslips.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_salary_in_force_is_the_one_effective_by_this_period_not_a_later_one()
    {
        using var f = new PayrollFixture();
        var current = await f.Db.EmployeeSalaries.SingleAsync();
        f.Db.EmployeeSalaries.Add(new EmployeeSalary
        {
            EmployeeId = current.EmployeeId, SalaryStructureId = current.SalaryStructureId,
            BasicSalary = 400_000m, CurrencyCode = "KES",
            EffectiveFromPeriodId = f.PeriodId, EffectiveFromPeriodCode = "2026-06",   // a future raise
            Status = SalaryAssignmentStatus.Approved, CreatedBy = PayrollFixture.Computer,
        });
        await f.Db.SaveChangesAsync();

        await ComputedRunAsync(f);

        var slip = await f.Db.Payslips.AsNoTracking().SingleAsync();
        // A June raise must not be paid in March. Re-running an old period next year has to reproduce
        // that period's figures, which is what makes a run auditable.
        slip.BasicSalary.Should().Be(100_000m);
        slip.GrossPay.Should().Be(Gross);
    }

    // ── Unconfirmed rates ────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unconfirmed_personal_relief_figure_warns_on_compute()
    {
        using var f = new PayrollFixture();
        var relief = await f.Db.StatutoryRates.SingleAsync(r => r.Code == "RELIEF");
        relief.NeedsConfirmation = true;
        await f.Db.SaveChangesAsync();

        var created = await f.Runs.CreateRunAsync(
            new CreatePayrollRunDto { PayrollPeriodId = f.PeriodId }, PayrollFixture.Computer);
        var computed = await f.Runs.ComputeRunAsync(created.Id!, PayrollFixture.Computer);

        // Part of #244. The check used to read computedRates, which excludes FixedAmount — so personal
        // relief, which applies to every employee on every run and is exactly what a Finance Act changes,
        // had NeedsConfirmation set and never read. A run could be computed against a stale relief figure,
        // mis-tax everyone, and report no warning at all.
        computed.Warnings.Should().Contain(w => w.Contains("never been checked against the current Finance Act"));
        computed.Warnings.Should().Contain(w => w.Contains("RELIEF"),
            because: "naming the rate is what makes the warning actionable rather than ambient");
    }

    [Fact]
    public async Task An_unconfirmed_HELB_rate_warns_on_compute()
    {
        using var f = new PayrollFixture();
        f.Db.StatutoryRates.Add(new StatutoryRate
        {
            Code = "HELB", Name = "HELB repayment", Component = StatutoryComponent.Helb,
            RateType = StatutoryRateType.PerEmployeeAmount, EffectiveFrom = new DateTime(2024, 1, 1),
            NeedsConfirmation = true, IsActive = true, CreatedBy = PayrollFixture.Computer,
        });
        await f.Db.SaveChangesAsync();

        var created = await f.Runs.CreateRunAsync(
            new CreatePayrollRunDto { PayrollPeriodId = f.PeriodId }, PayrollFixture.Computer);
        var computed = await f.Runs.ComputeRunAsync(created.Id!, PayrollFixture.Computer);

        // PerEmployeeAmount is the other type computedRates dropped. HELB is declarative — the figure comes
        // from the employee's own deduction — but the RULE still carries a confirmation flag worth reading.
        computed.Warnings.Should().Contain(w => w.Contains("HELB"));
    }

    [Fact]
    public async Task Confirmed_rates_raise_no_Finance_Act_warning()
    {
        using var f = new PayrollFixture();
        var created = await f.Runs.CreateRunAsync(
            new CreatePayrollRunDto { PayrollPeriodId = f.PeriodId }, PayrollFixture.Computer);
        var computed = await f.Runs.ComputeRunAsync(created.Id!, PayrollFixture.Computer);

        // The warning has to be silent when everything is confirmed, or it becomes noise that gets ignored
        // on the run where it matters.
        computed.Warnings.Should().NotContain(w => w.Contains("never been checked"));
    }

    // ── Approving on unverified rates is an accountable decision ─────────────────

    [Fact]
    public async Task A_run_computed_from_unverified_rates_cannot_be_approved_silently()
    {
        using var f = new PayrollFixture();
        await MarkRatesUnconfirmedAsync(f);
        var runId = await ComputedRunAsync(f);

        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        // The compute-time warning goes to whoever computed the run, and the second-officer rule guarantees
        // that is not the person approving it. Without this gate the approver signs off figures derived from
        // rates nobody has checked, with nothing in front of them saying so.
        result.Status.Should().Be("Error");
        result.Message.Should().Contain("never been checked against the current Finance Act");
        f.Finance.Posted.Should().BeEmpty();
    }

    [Fact]
    public async Task The_refusal_names_the_rates_so_it_can_be_acted_on()
    {
        using var f = new PayrollFixture();
        await MarkRatesUnconfirmedAsync(f);
        var runId = await ComputedRunAsync(f);

        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        // "Some rates are unverified" is not actionable. Naming them is the difference between a message
        // someone fixes and one they learn to skip past.
        result.Message.Should().Contain("NSSF").And.Contain("SHA").And.Contain("RELIEF");
    }

    [Fact]
    public async Task An_explicit_acknowledgement_lets_the_run_through()
    {
        using var f = new PayrollFixture();
        await MarkRatesUnconfirmedAsync(f);
        var runId = await ComputedRunAsync(f);

        var result = await ApproveAsync(f, runId, PayrollFixture.Approver, acknowledgeRates: true);

        // Deliberately not a hard block: there are legitimate reasons to run before rates are confirmed, and
        // a refusal that cannot be overridden gets routed around instead of respected.
        result.Status.Should().Be("Approved");
        f.Finance.Posted.Should().ContainSingle();
    }

    [Fact]
    public async Task The_acknowledgement_is_attributed_in_the_audit_log()
    {
        using var f = new PayrollFixture();
        await MarkRatesUnconfirmedAsync(f);
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Approver, acknowledgeRates: true);

        var approved = await f.Db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityId == runId && a.Action == HrAuditAction.PayrollRunApproved);
        // An acknowledgement nobody is named for is indistinguishable from no control at all.
        approved.Detail.Should().Contain("ACKNOWLEDGED");
        approved.Detail.Should().Contain("NSSF");
        approved.PerformedBy.Should().Be(PayrollFixture.Approver);
    }

    [Fact]
    public async Task The_unverified_codes_reach_the_durable_audit_entry_at_compute_time()
    {
        using var f = new PayrollFixture();
        await MarkRatesUnconfirmedAsync(f);
        var runId = await ComputedRunAsync(f);

        var computed = await f.Db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityId == runId && a.Action == HrAuditAction.PayrollRunComputed);
        // A warning string on an HTTP response is read once by one person. The audit entry is what the
        // approver and any later auditor actually have.
        computed.Detail.Should().Contain("UNVERIFIED");
        computed.Detail.Should().Contain("RELIEF");
    }

    [Fact]
    public async Task Confirming_the_rates_after_compute_removes_the_need_to_acknowledge()
    {
        using var f = new PayrollFixture();
        await MarkRatesUnconfirmedAsync(f);
        var runId = await ComputedRunAsync(f);

        // Somebody checks the rates against the Finance Act between compute and approval.
        await MarkRatesUnconfirmedAsync(f, unconfirmed: false);
        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        // This is why the check is re-derived at approval rather than read from a flag stored at compute
        // time: a stored flag would still demand an acknowledgement for figures that are now verified, which
        // teaches people to acknowledge reflexively — and a control that is always clicked through is not
        // a control.
        result.Status.Should().Be("Approved");
        f.Finance.Posted.Should().ContainSingle();
    }

    [Fact]
    public async Task Acknowledging_when_nothing_is_unverified_changes_nothing()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);

        await ApproveAsync(f, runId, PayrollFixture.Approver, acknowledgeRates: true);

        var approved = await f.Db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityId == runId && a.Action == HrAuditAction.PayrollRunApproved);
        // A blanket acknowledgement must not manufacture a record of accepting risk that was never taken —
        // that would make the audit trail lie in the safe direction, which is still lying.
        approved.Detail.Should().NotContain("ACKNOWLEDGED");
    }

    [Fact]
    public async Task An_unverified_PAYE_band_alone_is_enough_to_require_acknowledgement()
    {
        using var f = new PayrollFixture();
        var band = await f.Db.PayeTaxBands.FirstAsync(b => b.BandOrder == 3);
        band.NeedsConfirmation = true;
        await f.Db.SaveChangesAsync();
        var runId = await ComputedRunAsync(f);

        var result = await ApproveAsync(f, runId, PayrollFixture.Approver);

        // Bands are as much a Finance Act instrument as the rates, and band 3 is where most salaries land.
        result.Status.Should().Be("Error");
        result.Message.Should().Contain("PAYE band 3");
    }

    // ── Audit ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_lifecycle_step_is_recorded_against_the_run()
    {
        using var f = new PayrollFixture();
        var runId = await ComputedRunAsync(f);
        await ApproveAsync(f, runId, PayrollFixture.Approver);

        var log = await f.Db.AuditLogs.AsNoTracking().Where(a => a.EntityId == runId).ToListAsync();
        log.Select(a => a.Action).Should().Contain(HrAuditAction.PayrollRunApproved);
        // Approval must record WHO, because segregation of duties is only auditable if both names survive.
        log.Should().Contain(a => a.Action == HrAuditAction.PayrollRunApproved
                               && a.PerformedBy == PayrollFixture.Approver);
        // And the computing officer must be on the record too, or the two-person control cannot be proven
        // after the fact — only asserted.
        log.Should().Contain(a => a.PerformedBy == PayrollFixture.Computer);
    }
}
