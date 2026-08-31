using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Governance;
using OperationsService.Core.Enums;
using Xunit;

namespace OperationsService.Tests;

/// <summary>
/// Project change control — the code allowed to move a project's agreed baseline.
///
/// <para>Everything downstream is measured against that baseline: variance, earned value, "are we over
/// budget". So a baseline that moves without a decision, or moves without recording what it moved from,
/// makes every subsequent report unfalsifiable.</para>
///
/// <para>Operations' only existing tests were <c>CalibrationMath.Tests</c>, which reference Core alone and
/// execute no query. This is the service's first data-layer coverage (#247).</para>
/// </summary>
public class ChangeControlTests
{
    private static DecideChangeRequestDto Approve() => new() { Approved = true };
    private static DecideChangeRequestDto Reject(string? reason) => new() { Approved = false, Reason = reason };

    // ── Segregation of duties ────────────────────────────────────────────────────

    [Fact]
    public async Task The_person_who_raised_a_change_request_cannot_approve_it()
    {
        using var f = new GovernanceFixture();
        var cr = await f.SubmittedCrAsync();

        var act = () => f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Requester);

        // Authorising your own baseline move is the whole thing change control exists to prevent.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be approved by the person who raised it*");
    }

    [Fact]
    public async Task A_second_person_can_approve_it()
    {
        using var f = new GovernanceFixture();
        var cr = await f.SubmittedCrAsync();

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        var saved = await f.Db.ChangeRequests.AsNoTracking().SingleAsync(x => x.Id == cr.Id);
        saved.Status.Should().Be(ChangeRequestStatus.Approved);
        saved.DecidedBy.Should().Be(GovernanceFixture.Approver);
    }

    [Fact]
    public async Task A_refused_approval_leaves_the_request_submitted_and_the_baseline_untouched()
    {
        using var f = new GovernanceFixture(baselineBudget: 1_000_000m);
        var cr = await f.SubmittedCrAsync(scheduleImpactDays: 30);
        f.SeedMilestone("Intake works", new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));

        var act = () => f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Requester);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // A control that throws after moving the baseline is not a control.
        var saved = await f.Db.ChangeRequests.AsNoTracking().SingleAsync(x => x.Id == cr.Id);
        saved.Status.Should().Be(ChangeRequestStatus.Submitted);
        var ms = await f.Db.Milestones.AsNoTracking().SingleAsync();
        ms.BaselineDue.Should().Be(new DateTime(2026, 6, 30));
    }

    // ── State guards ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Only_a_submitted_request_can_be_decided()
    {
        using var f = new GovernanceFixture();
        var cr = await f.SubmittedCrAsync();
        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        var act = () => f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        // Deciding twice would shift the baseline twice off one authorisation.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Only a submitted*");
    }

    [Fact]
    public async Task An_unknown_request_is_a_not_found_rather_than_a_silent_no_op()
    {
        using var f = new GovernanceFixture();

        var act = () => f.Governance.DecideChangeRequestAsync("nope", Approve(), GovernanceFixture.Approver);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Rejection ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rejecting_without_a_reason_is_refused()
    {
        using var f = new GovernanceFixture();
        var cr = await f.SubmittedCrAsync();

        var act = () => f.Governance.DecideChangeRequestAsync(cr.Id, Reject(null), GovernanceFixture.Approver);

        // An unexplained rejection tells the requester nothing and gets resubmitted unchanged.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*reason*");
    }

    [Fact]
    public async Task A_rejection_records_the_reason_and_moves_nothing()
    {
        using var f = new GovernanceFixture();
        var cr = await f.SubmittedCrAsync(scheduleImpactDays: 45);
        f.SeedMilestone("Intake works", new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));

        await f.Governance.DecideChangeRequestAsync(cr.Id, Reject("Out of scope for this phase"), GovernanceFixture.Approver);

        var saved = await f.Db.ChangeRequests.AsNoTracking().SingleAsync(x => x.Id == cr.Id);
        saved.Status.Should().Be(ChangeRequestStatus.Rejected);
        saved.DecisionReason.Should().Be("Out of scope for this phase");
        (await f.Db.Milestones.AsNoTracking().SingleAsync()).BaselineDue.Should().Be(new DateTime(2026, 6, 30));
        saved.MilestonesShifted.Should().Be(0);
    }

    // ── The snapshot, which is what makes variance answerable ────────────────────

    [Fact]
    public async Task Approval_snapshots_the_previous_baseline_before_moving_it()
    {
        using var f = new GovernanceFixture(baselineBudget: 1_000_000m);
        var cr = await f.SubmittedCrAsync(scheduleImpactDays: 30);

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        var saved = await f.Db.ChangeRequests.AsNoTracking().SingleAsync(x => x.Id == cr.Id);
        // Without this, "against what did we originally agree" is unanswerable the moment the baseline
        // shifts — and every variance report after it is measured against a number nobody recorded.
        saved.PreviousBaselineBudget.Should().Be(1_000_000m);
        saved.PreviousBaselineSetAt.Should().Be(new DateTime(2026, 1, 15));
    }

    [Fact]
    public async Task The_snapshot_records_each_milestones_baseline_dates()
    {
        using var f = new GovernanceFixture();
        var m = f.SeedMilestone("Intake works", new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));
        var cr = await f.SubmittedCrAsync(scheduleImpactDays: 30);

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        var saved = await f.Db.ChangeRequests.AsNoTracking().SingleAsync(x => x.Id == cr.Id);
        saved.PreviousMilestoneBaselines.Should().NotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(saved.PreviousMilestoneBaselines!);
        var row = doc.RootElement.EnumerateArray().Single();
        row.GetProperty("milestoneId").GetString().Should().Be(m.Id);
        // The PRE-shift dates, not the post-shift ones — the snapshot is taken first, and a snapshot
        // taken afterwards would record the new baseline as if it were the old one.
        row.GetProperty("baselineDue").GetDateTime().Should().Be(new DateTime(2026, 6, 30));
    }

    // ── The schedule shift ──────────────────────────────────────────────────────

    [Fact]
    public async Task An_approved_shift_moves_the_baseline_dates()
    {
        using var f = new GovernanceFixture();
        f.SeedMilestone("Intake works", new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));
        var cr = await f.SubmittedCrAsync(scheduleImpactDays: 30);

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        var ms = await f.Db.Milestones.AsNoTracking().SingleAsync();
        ms.BaselineStart.Should().Be(new DateTime(2026, 5, 1));
        ms.BaselineDue.Should().Be(new DateTime(2026, 7, 30));
    }

    [Fact]
    public async Task The_live_working_dates_are_NOT_moved()
    {
        using var f = new GovernanceFixture();
        f.SeedMilestone("Intake works",
            baselineStart: new DateTime(2026, 4, 1), baselineDue: new DateTime(2026, 6, 30),
            liveStart: new DateTime(2026, 4, 10), liveDue: new DateTime(2026, 7, 15));
        var cr = await f.SubmittedCrAsync(scheduleImpactDays: 30);

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        var ms = await f.Db.Milestones.AsNoTracking().SingleAsync();
        // Re-baselining is about what was AGREED. StartDate/DueDate are the team's working plan and
        // theirs to manage; moving both would erase the very variance the re-baseline is meant to explain.
        ms.StartDate.Should().Be(new DateTime(2026, 4, 10));
        ms.DueDate.Should().Be(new DateTime(2026, 7, 15));
    }

    [Fact]
    public async Task A_milestone_with_no_baseline_at_all_is_skipped_rather_than_given_one()
    {
        using var f = new GovernanceFixture();
        var m = f.SeedMilestone("Unbaselined", new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));
        var tracked = await f.Db.Milestones.SingleAsync(x => x.Id == m.Id);
        tracked.BaselineStart = null;
        tracked.BaselineDue = null;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
        var cr = await f.SubmittedCrAsync(scheduleImpactDays: 30);

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        var ms = await f.Db.Milestones.AsNoTracking().SingleAsync();
        // Shifting a null by 30 days would invent a baseline nobody agreed to.
        ms.BaselineStart.Should().BeNull();
        ms.BaselineDue.Should().BeNull();
        (await f.Db.ChangeRequests.AsNoTracking().SingleAsync()).MilestonesShifted.Should().Be(0);
    }

    [Fact]
    public async Task A_zero_day_impact_shifts_nothing_and_records_zero()
    {
        using var f = new GovernanceFixture();
        f.SeedMilestone("Intake works", new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));
        var cr = await f.SubmittedCrAsync(scheduleImpactDays: 0);

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        var ms = await f.Db.Milestones.AsNoTracking().SingleAsync();
        ms.BaselineDue.Should().Be(new DateTime(2026, 6, 30));
        (await f.Db.ChangeRequests.AsNoTracking().SingleAsync()).MilestonesShifted.Should().Be(0);
    }

    [Fact]
    public async Task A_negative_impact_pulls_the_baseline_earlier()
    {
        using var f = new GovernanceFixture();
        f.SeedMilestone("Intake works", new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));
        var cr = await f.SubmittedCrAsync(scheduleImpactDays: -14);

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        // Change requests can compress a schedule as well as extend it; a shift applied only in one
        // direction would silently ignore half of them.
        (await f.Db.Milestones.AsNoTracking().SingleAsync()).BaselineDue.Should().Be(new DateTime(2026, 6, 16));
    }

    [Fact]
    public async Task Every_milestone_on_the_project_is_shifted_and_counted()
    {
        using var f = new GovernanceFixture();
        f.SeedMilestone("Intake", new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));
        f.SeedMilestone("Pipeline", new DateTime(2026, 7, 1), new DateTime(2026, 9, 30));
        f.SeedMilestone("Commissioning", new DateTime(2026, 10, 1), new DateTime(2026, 11, 30));
        var cr = await f.SubmittedCrAsync(scheduleImpactDays: 15);

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        var due = await f.Db.Milestones.AsNoTracking().OrderBy(m => m.BaselineDue).Select(m => m.BaselineDue).ToListAsync();
        due.Should().Equal(new DateTime(2026, 7, 15), new DateTime(2026, 10, 15), new DateTime(2026, 12, 15));
        (await f.Db.ChangeRequests.AsNoTracking().SingleAsync()).MilestonesShifted.Should().Be(3);
    }

    // ── The budget seam ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Approving_a_linked_budget_version_is_delegated_not_reimplemented()
    {
        using var f = new GovernanceFixture();
        var cr = await f.SubmittedCrAsync(budgetVersionId: "bv-7");

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        // Moving BaselineBudget here as well as in the budget service would give two mechanisms that
        // eventually disagree, and the one that wrote last would win silently.
        f.Budgets.Approved.Should().ContainSingle();
        f.Budgets.Approved[0].Should().Be(("bv-7", GovernanceFixture.Approver));
    }

    [Fact]
    public async Task A_request_with_no_budget_version_touches_the_budget_service_at_all()
    {
        using var f = new GovernanceFixture();
        var cr = await f.SubmittedCrAsync(budgetVersionId: null);

        await f.Governance.DecideChangeRequestAsync(cr.Id, Approve(), GovernanceFixture.Approver);

        // A schedule-only change must not approve a budget version that was never linked.
        f.Budgets.Approved.Should().BeEmpty();
    }
}
