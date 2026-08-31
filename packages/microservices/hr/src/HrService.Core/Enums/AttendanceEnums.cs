namespace HrService.Core.Enums;

/// <summary>
/// H4 (ATT-001, P27 step 27.1) — where someone works, which decides how they may clock in. The DFD reads this
/// off EMPLOYEE ("verify active, get work_location"), so it lives on the employee rather than the position;
/// field-versus-office is mostly role-driven at QSL but individuals move between the two.
/// </summary>
public enum WorkMode
{
    /// <summary>Desktop or biometric clock-in; no GPS expected.</summary>
    Office,
    /// <summary>Mobile clock-in with a GPS stamp (ATT-005).</summary>
    Field,
    /// <summary>Either channel is acceptable — GPS is recorded when supplied but never demanded.</summary>
    Hybrid,
}

/// <summary>H4 (ATT-001) — how a clock-in reached us. <see cref="Manual"/> is an HR correction, which is why it
/// is recorded distinctly rather than being passed off as the employee's own clock-in.</summary>
public enum ClockInMethod
{
    Desktop,
    Biometric,
    Mobile,
    /// <summary>Entered by HR on the employee's behalf — a correction, and auditable as one.</summary>
    Manual,
}

/// <summary>
/// H4 (P27) — what a day looked like for one employee.
/// <para><see cref="OnLeave"/>, <see cref="Holiday"/> and <see cref="NonWorkingDay"/> exist so the monthly
/// metrics can tell "did not work because they were not expected to" apart from "did not turn up", which is the
/// whole basis of the punctuality and absence rates.</para>
/// </summary>
public enum AttendanceStatus
{
    Present,
    /// <summary>Clocked in more than the grace period after the working day started (ATT-002).</summary>
    Late,
    /// <summary>Expected but never clocked in (ATT-003) — carries an <c>AbsenceRecord</c>.</summary>
    Absent,
    /// <summary>Covered by an approved leave request (ATT-004 reconciliation).</summary>
    OnLeave,
    Holiday,
    /// <summary>Weekend or a day the tenant does not work.</summary>
    NonWorkingDay,
}

/// <summary>H4 (P27 step 27.5) — who raised the absence. A detected absence is the scheduler's inference from
/// silence; an HR-recorded one is a statement of fact.</summary>
public enum AbsenceSource
{
    /// <summary>Created by the daily sweep because no clock-in arrived by the cut-off.</summary>
    Detected,
    RecordedByHr,
}
