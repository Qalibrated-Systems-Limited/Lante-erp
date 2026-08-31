using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> that genuinely SKIPS when no test Postgres is configured.
///
/// <para>The obvious alternative — an early <c>return</c> inside the test body — reports the test as
/// <b>Passed</b>. That makes "184 passed" mean one thing locally and another in CI, and a green run
/// tell you nothing about whether the advisory-lock guards were exercised. This repo has found that
/// same shape twice already: a workflow that ran nothing (#214) and a check that failed in two
/// seconds while looking like a result (#253/#361). Not worth repeating in the test suite itself.</para>
///
/// <para><c>Skip</c> is evaluated at discovery, which is where the decision belongs — the test then
/// reports as Skipped with the reason attached. Under <c>CI=true</c>
/// <see cref="PostgresFixture.Unavailable"/> throws instead, so a workflow that loses its postgres
/// service fails rather than quietly skipping its way to green.</para>
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (PostgresFixture.Unavailable() is not null)
            Skip = PostgresFixture.SkipReason;
    }
}
