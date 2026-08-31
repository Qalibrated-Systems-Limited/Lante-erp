using SubcontractsService.Core.Services;
using FluentAssertions;
using Xunit;

namespace SubcontractsService.Tests;

/// <summary>
/// Invariants on a certified subcontractor payment — the first tests in subcontracts-service
/// (58 source files, zero until now, and the last of the four services that had none).
///
/// <para><c>PaymentRetentionsController</c> took <c>CertifiedAmount</c>, <c>RetentionHeld</c> and
/// <c>Wht</c> straight off the request body. This service has <b>no validation layer at all</b> — no
/// FluentValidation, no data annotations — so a caller with <c>subcontracts.write</c> could certify a
/// payment whose deductions exceeded the certificate, or whose retention was negative, and the row
/// was written exactly as sent.</para>
///
/// <para>These bound the figures. They deliberately do not <i>compute</i> retention — see
/// <see cref="Retention_is_recorded_from_the_certificate_rather_than_derived"/>.</para>
/// </summary>
public class RetentionRulesTests
{
    // ── Net payable ─────────────────────────────────────────────────────────────

    [Fact]
    public void The_net_payable_is_the_certificate_less_both_deductions()
    {
        // 1,000,000 certified, 10% retention, 3% WHT.
        RetentionRules.NetPayable(1_000_000m, 100_000m, 30_000m).Should().Be(870_000m);
    }

    [Fact]
    public void A_certificate_with_no_deductions_pays_out_in_full()
    {
        RetentionRules.NetPayable(500_000m, 0m, 0m).Should().Be(500_000m);
    }

    // ── What cannot be certified ────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1_000_000)]
    public void A_certificate_for_nothing_or_less_is_refused(decimal certified)
    {
        var act = () => RetentionRules.Validate(certified, 0m, 0m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void A_negative_retention_is_refused()
    {
        // A negative deduction INCREASES what is paid out. It is not a correction — it is a payment
        // the certificate does not support, and it would be invisible in any report that reads
        // RetentionHeld as an amount withheld.
        var act = () => RetentionRules.Validate(1_000_000m, -50_000m, 0m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Retention held cannot be negative*");
    }

    [Fact]
    public void A_negative_withholding_tax_is_refused()
    {
        var act = () => RetentionRules.Validate(1_000_000m, 0m, -30_000m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Withholding tax cannot be negative*");
    }

    [Fact]
    public void Deductions_that_exceed_the_certificate_are_refused()
    {
        var act = () => RetentionRules.Validate(100_000m, 80_000m, 30_000m);

        // 110,000 out of 100,000 — a negative payable, which nothing downstream reads as a debt
        // owed back to the business.
        act.Should().Throw<InvalidOperationException>().WithMessage("*exceed the certified amount*");
    }

    [Fact]
    public void The_two_deductions_are_tested_together_not_one_at_a_time()
    {
        // Each is individually under the certificate; together they are not. Validating them
        // separately — the natural way to write this — would let this through.
        var act = () => RetentionRules.Validate(100_000m, 60_000m, 60_000m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*exceed the certified amount*");
    }

    // ── Boundaries ──────────────────────────────────────────────────────────────

    [Fact]
    public void Deductions_equal_to_the_certificate_are_allowed_and_pay_nothing()
    {
        // A certificate can legitimately net to zero when retention and WHT consume it. A `>=`
        // instead of `>` would refuse a real, if unhappy, payment certificate.
        var act = () => RetentionRules.Validate(100_000m, 70_000m, 30_000m);

        act.Should().NotThrow();
        RetentionRules.NetPayable(100_000m, 70_000m, 30_000m).Should().Be(0m);
    }

    [Fact]
    public void Zero_deductions_are_allowed()
    {
        // Retention is often released in full on the final certificate, and not every subcontractor
        // is subject to WHT. Zero must stay valid.
        var act = () => RetentionRules.Validate(1_000_000m, 0m, 0m);

        act.Should().NotThrow();
    }

    [Fact]
    public void The_smallest_meaningful_certificate_is_accepted()
    {
        var act = () => RetentionRules.Validate(0.01m, 0m, 0m);

        act.Should().NotThrow();
    }

    // ── The rate that is documented but never applied ───────────────────────────

    [Fact]
    public void Retention_is_recorded_from_the_certificate_rather_than_derived()
    {
        // #367: confirmed with the repo owner. The standard retention rate varies per contract
        // (5%/10% both common), usually steps down at practical completion, and some subcontracts
        // cap it at a percentage of the contract sum — none of which is in the data model
        // (SubcontractAward has no rate/cap, PaymentRetention has no release stage). Hard-coding a
        // rate here would be wrong more often than accepting the QS-prepared certificate's figure.
        //
        // The entity comment that used to claim "10% held" has been corrected (it described an
        // enforcement that never existed) rather than the code changed to match it.
        var act = () => RetentionRules.Validate(1_000_000m, 250_000m, 0m);

        act.Should().NotThrow("25% retention is unusual but not arithmetically impossible");
    }
}
