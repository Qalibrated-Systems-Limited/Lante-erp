using HSEService.Core.Entities;
using HSEService.Core.Enums;
using HSEService.Core.Services;
using FluentAssertions;
using Xunit;

namespace HSEService.Tests;

/// <summary>
/// TRIR, LTIF and the dashboard counts — the first tests in hse-service (87 source files, zero
/// until now).
///
/// <para>TRIR is a <b>regulated safety statistic</b>. It goes to clients in prequalification, to
/// insurers, and to the regulator. A wrong one is not a bug report — and unlike most wrong numbers
/// it is quoted publicly, so it is worth pinning exactly.</para>
///
/// <para>Which incident types count as recordable is settled here per #365: first-aid-only cases are
/// excluded, matching the OSHA convention the 200,000-hour base comes from. These tests pinned the old
/// behaviour when it was still an open question — see
/// <see cref="A_first_aid_only_case_is_not_recordable"/> for what changed and why.</para>
/// </summary>
public class HseDashboardMetricTests
{
    private static HseIncident Incident(IncidentType type, DateTime? at = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Type = type,
        OccurredAt = at ?? new DateTime(DateTime.UtcNow.Year, 1, 15, 0, 0, 0, DateTimeKind.Utc),
    };

    private static HseDashboardService Sut(params HseIncident[] incidents) =>
        new(new FakeHseCrudService<HseIncident>(incidents),
            new FakeHseCrudService<CorrectiveAction>(),
            new FakeHseCrudService<Rams>(),
            new FakeHseCrudService<HseTrainingRecord>(),
            new FakeHseCrudService<StatutoryInspection>());

    // ── TRIR ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TRIR_is_recordable_incidents_per_two_hundred_thousand_hours()
    {
        // 2 recordable over 400,000 hours → 2 × 200,000 / 400,000 = 1.00
        var d = await Sut(Incident(IncidentType.MedicalTreatment), Incident(IncidentType.LostTimeInjury))
            .ComputeAsync(400_000m);

        d.Trir.Should().Be(1.00m);
    }

    [Fact]
    public async Task The_TRIR_base_is_two_hundred_thousand_hours_not_one_million()
    {
        // The constant IS the metric's definition. Swapping it for LTIF's 1,000,000 base quietly
        // multiplies every reported rate by five.
        var d = await Sut(Incident(IncidentType.LostTimeInjury)).ComputeAsync(200_000m);

        d.Trir.Should().Be(1.00m);
    }

    [Fact]
    public async Task LTIF_counts_only_lost_time_injuries_per_million_hours()
    {
        var d = await Sut(
            Incident(IncidentType.LostTimeInjury),
            Incident(IncidentType.MedicalTreatment),
            Incident(IncidentType.NearMiss)).ComputeAsync(1_000_000m);

        // One LTI, and the medical-treatment case must NOT inflate it — LTIF and TRIR count
        // different things over different bases, and conflating them is the easy mistake.
        d.Ltif.Should().Be(1.00m);
    }

    [Fact]
    public async Task Both_rates_are_rounded_to_two_decimals()
    {
        // 1 × 200,000 / 300,000 = 0.6666… → 0.67
        var d = await Sut(Incident(IncidentType.LostTimeInjury)).ComputeAsync(300_000m);

        d.Trir.Should().Be(0.67m);
        d.Ltif.Should().Be(3.33m);
    }

    [Fact]
    public async Task Zero_hours_worked_yields_zero_rather_than_a_divide_by_zero()
    {
        // The interface doc promises this explicitly. A tenant that has not entered hours yet must
        // see 0, not a 500 on the safety dashboard.
        var d = await Sut(Incident(IncidentType.LostTimeInjury)).ComputeAsync(0m);

        d.Trir.Should().Be(0m);
        d.Ltif.Should().Be(0m);
    }

    [Fact]
    public async Task No_incidents_yields_a_zero_rate_rather_than_no_figure()
    {
        var d = await Sut().ComputeAsync(500_000m);

        d.Trir.Should().Be(0m);
        d.TotalIncidentsYtd.Should().Be(0);
    }

    // ── What counts as recordable ───────────────────────────────────────────────

    [Fact]
    public async Task A_first_aid_only_case_is_not_recordable()
    {
        var d = await Sut(Incident(IncidentType.FirstAid)).ComputeAsync(200_000m);

        // The 200,000-hour base is the OSHA convention, under which the recordability line is
        // "medical treatment BEYOND first aid" — a plaster or an eye wash is the canonical excluded
        // case. IncidentType models that distinction with FirstAid and MedicalTreatment as separate
        // members, and the service used to count both, overstating TRIR by every first-aid case on
        // the register (#365).
        //
        // This test previously pinned the OLD behaviour, deliberately, because correcting it lowers a
        // published safety metric and that is not a call to make from a reading of the code. #365 was
        // closed and #366 retitled "TRIR overstated first-aid cases as recordable" — but no fix ever
        // landed, so the metric stayed wrong with its tracking issue shut. This is that fix.
        d.Trir.Should().Be(0m);
        d.TotalIncidentsYtd.Should().Be(1, "it is still an incident, and still reportable");
    }

    [Fact]
    public async Task Medical_treatment_beyond_first_aid_is_recordable()
    {
        var d = await Sut(Incident(IncidentType.MedicalTreatment)).ComputeAsync(200_000m);

        // The other side of the line, so "exclude FirstAid" cannot be over-applied into excluding
        // the treatment cases that are the point of the metric.
        d.Trir.Should().Be(1.00m);
    }

    [Fact]
    public async Task A_first_aid_case_does_not_inflate_a_rate_that_has_real_injuries_in_it()
    {
        var d = await Sut(
            Incident(IncidentType.LostTimeInjury),
            Incident(IncidentType.FirstAid),
            Incident(IncidentType.FirstAid)).ComputeAsync(200_000m);

        // One recordable, not three. The realistic shape: first aid cases outnumber the serious ones
        // on any real register, so including them does not nudge the figure, it dominates it.
        d.Trir.Should().Be(1.00m);
        d.TotalIncidentsYtd.Should().Be(3);
    }

    [Fact]
    public async Task A_near_miss_is_never_recordable_and_a_positive_observation_never_counts()
    {
        var d = await Sut(
            Incident(IncidentType.NearMiss),
            Incident(IncidentType.PositiveObservation)).ComputeAsync(200_000m);

        // Whatever the FirstAid question resolves to, these two are not injuries at all. A near miss
        // inflating TRIR would punish exactly the reporting culture the metric exists to encourage.
        d.Trir.Should().Be(0m);
        d.NearMissCount.Should().Be(1);
        d.TotalIncidentsYtd.Should().Be(2);
    }

    // ── The year window ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Only_this_calendar_years_incidents_count_towards_the_rate()
    {
        var lastYear = new DateTime(DateTime.UtcNow.Year - 1, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var d = await Sut(
            Incident(IncidentType.LostTimeInjury),
            Incident(IncidentType.LostTimeInjury, lastYear)).ComputeAsync(200_000m);

        // The rate is year-to-date and the hours passed in are year-to-date; letting last year's
        // incidents through would divide one period's injuries by another period's hours.
        d.Trir.Should().Be(1.00m);
        d.TotalIncidentsYtd.Should().Be(1);
    }

    [Fact]
    public async Task An_incident_on_the_first_instant_of_the_year_is_included()
    {
        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var d = await Sut(Incident(IncidentType.LostTimeInjury, yearStart)).ComputeAsync(200_000m);

        // The boundary. A `>` instead of `>=` silently drops New Year's Day every year.
        d.Trir.Should().Be(1.00m);
    }
}
