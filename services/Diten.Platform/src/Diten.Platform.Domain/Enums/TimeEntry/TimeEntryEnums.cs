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

/// <summary>Where a time entry's minutes came from (pack §4.2). The person types <see cref="Manual"/> rows and accepts
/// <see cref="Plan"/> ones; the timer writes <see cref="Timer"/> drafts; an accepted meeting suggestion writes
/// <see cref="Meeting"/>.</summary>
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

/// <summary>Why a timer segment started (pack §4.1).</summary>
public enum TimerStartSource
{
    /// <summary>The holder started the task (MOD-0024 <c>Started</c>) — the transition hook.</summary>
    TaskStarted = 0,

    /// <summary>The holder resumed the task from Waiting (MOD-0024 <c>Resumed</c>) — the transition hook.</summary>
    TaskResumed = 1,

    /// <summary>The person pressed start on the timer itself (a task already InProgress, or a category).</summary>
    TimerControl = 2,

    /// <summary>The person undid a switch: the previous target starts again from now, never backdated.</summary>
    UndoSwitch = 3
}

/// <summary>Why a timer segment stopped (pack §4.1).</summary>
public enum TimerStopReason
{
    /// <summary>The person pressed stop.</summary>
    TimerControl = 0,

    /// <summary>Another target was started (or a switch was undone); one running timer per person (D2).</summary>
    Switch = 1,

    /// <summary>The task left InProgress or changed holder (the transition hook).</summary>
    TaskTransition = 2,

    /// <summary>The person's tenant-local midnight passed (D3) — by the job or on the first read after it.</summary>
    LocalMidnight = 3,

    /// <summary>A read found the timer's task no longer InProgress / held by the person, or gone (the hook missed it).</summary>
    Reconcile = 4,

    /// <summary>The person's legal entity has the timer switched off (D12).</summary>
    SwitchedOff = 5
}

/// <summary>What the person did with a meeting suggestion (pack §4.5, D8). "Confirmed by minutes", "withdrawn" and
/// "conflict" are NOT states: they are derived at read time from the meeting's current minutes attendance.</summary>
public enum TimeSuggestionState
{
    Open = 0,
    Accepted = 1,
    Dismissed = 2
}
