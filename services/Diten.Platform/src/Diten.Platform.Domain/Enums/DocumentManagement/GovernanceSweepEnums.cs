namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU32 — background governance sweep enums. A sweep is an OBSERVER: it evaluates due/overdue/expiry
// conditions and produces findings, escalations and reports. It never deletes, purges, closes, approves, makes
// effective, disposes, signs or retires anything. These enums carry no lifecycle authority of their own.

/// <summary>MOD-0029-FU32 — the terminal state of one sweep run.</summary>
public enum DocumentGovernanceSweepStatus
{
    /// <summary>Every group ran and produced no warning.</summary>
    Completed = 0,

    /// <summary>At least one group warned or failed, but the run continued (group-level isolation).</summary>
    CompletedWithWarnings = 1,

    /// <summary>The run could not proceed at all (infrastructure failure). Recorded best-effort.</summary>
    Failed = 2
}

/// <summary>MOD-0029-FU32 — what caused the run.</summary>
public enum DocumentGovernanceSweepTriggerType
{
    /// <summary>A user invoked the manual trigger API.</summary>
    Manual = 0,

    /// <summary>A recurring background job invoked it. Deferred in FU32 — see the scheduler decision.</summary>
    Scheduled = 1,

    /// <summary>An internal caller (another governance flow) invoked it.</summary>
    System = 2
}

/// <summary>
/// MOD-0029-FU32 — the outcome of ONE subject inside a sweep group. Every value is observational; none of them
/// implies the subject's own state was closed, approved or destroyed.
/// </summary>
public enum DocumentGovernanceSweepItemOutcome
{
    /// <summary>Evaluated and nothing was due — no action taken.</summary>
    NoActionRequired = 0,

    /// <summary>A due/overdue/expired condition was reported (report-only group).</summary>
    Reported = 1,

    /// <summary>An escalation or finding was created by the existing idempotent evaluator.</summary>
    EscalationCreated = 2,

    /// <summary>An equivalent open escalation/finding already existed — nothing was duplicated.</summary>
    SkippedExisting = 3,

    /// <summary>The subject could not be evaluated; the reason is on the item message and the group warned.</summary>
    Warning = 4,

    /// <summary>Dry run: the condition was detected but nothing at all was written.</summary>
    DryRun = 5
}
