using CrmService.Core.DTOs.Customers;
using CrmService.Core.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CrmService.Tests;

/// <summary>
/// Duplicate detection and KRA PIN handling. CRM had no tests at all (#247).
///
/// <para>Duplicates matter here beyond tidiness: a client split across two records can be invoiced
/// twice under one tax PIN, credit limits stop being enforceable against the real exposure, and the
/// aging report double-counts.</para>
/// </summary>
public class CustomerDuplicateTests
{
    private static CreateCustomerDto New(string name, string? email = null, string? phone = null,
                                         string? kraPin = null) =>
        new() { Name = name, Email = email, Phone = phone, KraPin = kraPin };

    // ── The duplicate query, which must translate ────────────────────────────────

    [Fact]
    public async Task A_customer_with_the_same_name_is_refused()
    {
        using var f = new CustomerFixture();
        f.Seed("Acme Ltd");

        var act = () => f.Customers.CreateAsync(New("Acme Ltd"), CustomerFixture.Actor, "Sales");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Name_matching_ignores_case_and_surrounding_space()
    {
        using var f = new CustomerFixture();
        f.Seed("Acme Ltd");

        var act = () => f.Customers.CreateAsync(New("  acme ltd  "), CustomerFixture.Actor, "Sales");

        // ToLower() on both sides has to reach the database. If it ever stops translating, this either
        // starts pulling every customer into memory on each check, or stops matching at all.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task A_matching_email_is_enough_on_its_own()
    {
        using var f = new CustomerFixture();
        f.Seed("Acme Ltd", email: "ap@acme.co.ke");

        var act = () => f.Customers.CreateAsync(
            New("Completely Different Name", email: "AP@ACME.CO.KE"), CustomerFixture.Actor, "Sales");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*email*");
    }

    [Fact]
    public async Task A_matching_phone_is_enough_on_its_own()
    {
        using var f = new CustomerFixture();
        f.Seed("Acme Ltd", phone: "+254700000001");

        var act = () => f.Customers.CreateAsync(
            New("Another Name", phone: "+254700000001"), CustomerFixture.Actor, "Sales");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*phone*");
    }

    [Fact]
    public async Task The_refusal_names_which_field_collided()
    {
        using var f = new CustomerFixture();
        f.Seed("Acme Ltd", email: "ap@acme.co.ke", phone: "+254700000001", kraPin: "P051234567M");

        var act = () => f.Customers.CreateAsync(
            New("Unrelated", kraPin: "P051234567M"), CustomerFixture.Actor, "Sales");

        // "A matching name, email, or phone" leaves the user guessing which of three values to change.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*KRA PIN*");
    }

    [Fact]
    public async Task A_genuinely_new_customer_is_created()
    {
        using var f = new CustomerFixture();
        f.Seed("Acme Ltd", email: "ap@acme.co.ke");

        var created = await f.Customers.CreateAsync(
            New("Beta Works", email: "finance@beta.co.ke"), CustomerFixture.Actor, "Sales");

        created.Should().NotBeNull();
        (await f.Db.Customers.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task An_empty_search_matches_nothing_rather_than_everything()
    {
        using var f = new CustomerFixture();
        f.Seed("Acme Ltd");

        var result = await f.Customers.CheckDuplicateAsync(null, null, null, null);

        // The early return is a performance guard, not a correctness one: every clause in the query is
        // itself gated on `x != null`, so all-null criteria produce false||false||false||false and match
        // nothing either way. I first wrote this comment claiming the opposite — that without the early
        // return every row would match — and deleting the early return changed no test outcome, which is
        // how I found out. Recorded because the wrong version is the intuitive reading.
        result.IsDuplicate.Should().BeFalse();
    }

    [Fact]
    public async Task A_null_column_never_matches_a_supplied_value()
    {
        using var f = new CustomerFixture();
        f.Seed("Acme Ltd");                       // no email, no phone, no PIN

        var result = await f.Customers.CheckDuplicateAsync(null, "someone@example.com", null, null);

        // SQL three-valued logic: a NULL column compared to a value is neither true nor false. The
        // explicit != null guards are what keep that from silently swallowing the clause.
        result.IsDuplicate.Should().BeFalse();
    }

    // ── KRA PIN ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_malformed_KRA_PIN_is_refused_before_anything_is_written()
    {
        using var f = new CustomerFixture();

        var act = () => f.Customers.CreateAsync(New("Acme Ltd", kraPin: "NOTAPIN"), CustomerFixture.Actor, "Sales");

        // A mistyped PIN silently produces invalid tax invoices months later.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not a valid KRA PIN*");
        (await f.Db.Customers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_KRA_PIN_is_normalised_before_it_is_stored()
    {
        using var f = new CustomerFixture();

        await f.Customers.CreateAsync(New("Acme Ltd", kraPin: " p05-1234567 m "), CustomerFixture.Actor, "Sales");

        // Stored one way, compared one way. If the stored form varied, the duplicate check would miss
        // the same PIN entered with different spacing.
        (await f.Db.Customers.SingleAsync()).KraPin.Should().Be("P051234567M");
    }

    [Fact]
    public async Task Two_customers_can_be_given_the_same_KRA_PIN_directly()
    {
        using var f = new CustomerFixture();
        f.Seed("Acme Ltd", kraPin: "P051234567M");
        f.Seed("Acme Limited", kraPin: "P051234567M");

        // BUG: there is no unique index on Customer.KraPin — CrmDbContext indexes Name, Email, Status
        // and AccountOwnerId, and no migration adds one. The uniqueness rule lives entirely in
        // CustomerService, as a read followed by a write, so it is check-then-act with nothing
        // underneath it.
        //
        // Concretely: request A creates "Acme Ltd" with PIN P051234567M while request B creates
        // "Acme Limited" with the same PIN. Both run FindDuplicatesAsync, both see nothing, both
        // insert. The service's own comment says why that matters — two records "can invoice under
        // one PIN and the duplicate check stops meaning anything".
        //
        // This test pins the gap by writing what the race would produce. It passes today and must
        // fail once a unique index exists. Tracked in #284.
        (await f.Db.Customers.CountAsync(c => c.KraPin == "P051234567M")).Should().Be(2,
            because: "BUG #284 — no unique index backs the PIN check");
    }

    [Fact]
    public async Task Updating_a_customer_onto_another_customers_PIN_is_refused()
    {
        using var f = new CustomerFixture();
        f.Seed("Acme Ltd", kraPin: "P051234567M");
        var beta = f.Seed("Beta Works");

        var act = () => f.Customers.UpdateAsync(beta.Id,
            new UpdateCustomerDto { KraPin = "P051234567M" }, CustomerFixture.Actor);

        // The sequential path is guarded; only the concurrent one is not (#284).
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already belongs to*");
    }

    [Fact]
    public async Task Re_saving_a_customer_with_its_own_PIN_unchanged_is_not_a_clash()
    {
        using var f = new CustomerFixture();
        var acme = f.Seed("Acme Ltd", kraPin: "P051234567M");

        var updated = await f.Customers.UpdateAsync(acme.Id,
            new UpdateCustomerDto { KraPin = "P051234567M", Phone = "+254700000009" },
            CustomerFixture.Actor);

        // Re-saving the same PIN skips the clash check entirely — it only runs when the PIN is
        // CHANGING (`pin != c.KraPin`). So this asserts the skip, not the `x.Id != c.Id` filter.
        //
        // And that filter turns out to be unreachable: the check only fires when the new PIN differs
        // from this row's PIN, so this row can never be among the matches it excludes. Deleting
        // `x.Id != c.Id` changes no behaviour and no test. Harmless, but it reads as protection against
        // a case that cannot arise — noted on #284 rather than removed here, since this PR is tests only.
        updated.Should().NotBeNull();
    }
}
