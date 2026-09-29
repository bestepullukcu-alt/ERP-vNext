namespace Diten.Platform.Domain.Enums.TimeEntry;

/// <summary>
/// MOD-0280-FU01 (pack §4.3, D5) — one revision of one person's ISO week. Withdraw and reject return a revision to
/// <see cref="Draft"/>; there is no separate "returned" state — the rejection reason on the week says why.
/// </summary>
public enum TimesheetWeekStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,

    /// <summary>An approved revision a later, approved correction replaced. Kept, never deleted (D5).</summary>
    Superseded = 3
}

/// <summary>Where a time entry's minutes came from (pack §4.2). T1a writes only <see cref="Manual"/>.</summary>
public enum TimeEntrySource
{
    Timer = 0,
    Manual = 1,
    Meeting = 2,
    Plan = 3
}

/// <summary>How the approver candidates of a submitted week were found (pack §4.3, D6).</summary>
public enum TimesheetApproverResolution
{
    /// <summary>Holders of the primary seat's <c>ReportsToPositionId</c>.</summary>
    LineManager = 0,

    /// <summary>The direct manager seat was vacant (or held only by the person); a seat further up answered.</summary>
    ManagerChain = 1,

    /// <summary>No manager anywhere up the chain; the tenant's time-admin pool answered.</summary>
    TimeAdminPool = 2
}
