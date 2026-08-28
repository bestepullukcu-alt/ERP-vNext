using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU32 — one line of a sweep run's per-subject result. Purely observational: <c>Action</c> names what the
/// sweep did (evaluate / report), never what happened to the subject's lifecycle.
/// </summary>
public sealed class DocumentGovernanceSweepResultItem
{
    /// <summary>The kind of subject, e.g. <c>PeriodicReview</c>, <c>CapaAction</c>, <c>SignatureRequest</c>.</summary>
    public required string SubjectType { get; set; }

    public Guid SubjectId { get; set; }

    /// <summary>What the sweep did — e.g. <c>EvaluateOverdue</c>, <c>ReportMonitoringDue</c>.</summary>
    public required string Action { get; set; }

    public DocumentGovernanceSweepItemOutcome Outcome { get; set; } = DocumentGovernanceSweepItemOutcome.NoActionRequired;

    public string? Message { get; set; }

    public Guid? RelatedFindingId { get; set; }
    public Guid? RelatedEscalationId { get; set; }
}

/// <summary>
/// MOD-0029-FU32 — an append-only record of ONE background governance sweep run for a tenant (GMG-QMS-SOP-0001).
///
/// BOUNDARY: this is a governance EVIDENCE sidecar, the sweep equivalent of the FU31A policy-pack application row.
/// A sweep observes; it never deletes, purges, closes, approves, makes effective, disposes, signs or retires a
/// subject, and it never rewrites an existing lifecycle state machine. The only mutations a sweep can cause are the
/// ones the pre-existing, already-idempotent FU12/FU13/FU20 evaluators perform when invoked explicitly — marking an
/// overdue condition and raising a duplicate-suppressed escalation.
///
/// A run row is created once when the sweep starts and updated only to record completion (status, CompletedAt and
/// the counters). It is never deleted, and a repeat run writes a NEW row rather than revising the previous one.
/// A dry run writes no row at all.
/// </summary>
public sealed class DocumentGovernanceSweepRun : TenantScopedEntity
{
    /// <summary>Stable machine key, e.g. <c>document-governance.periodic-reviews</c> or <c>...run-all</c>.</summary>
    public required string SweepKey { get; set; }

    public required string SweepName { get; set; }
    public required string SweepVersion { get; set; }

    public DocumentGovernanceSweepTriggerType TriggerType { get; set; } = DocumentGovernanceSweepTriggerType.Manual;
    public DocumentGovernanceSweepStatus Status { get; set; } = DocumentGovernanceSweepStatus.Completed;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The as-of instant the candidate selection used. Defaults to <see cref="StartedAt"/>.</summary>
    public DateTimeOffset AsOfDate { get; set; } = DateTimeOffset.UtcNow;

    public Guid? TriggeredByUserId { get; set; }
    public string? CorrelationId { get; set; }

    // ── counters ─────────────────────────────────────────────────────────────────────────────────────────
    public int ItemsScanned { get; set; }

    /// <summary>Subjects for which the sweep produced a report line, a finding or an escalation.</summary>
    public int ItemsAffected { get; set; }

    public int FindingsCreated { get; set; }
    public int EscalationsCreated { get; set; }
    public int ExistingFindingsSkipped { get; set; }
    public int ExistingEscalationsSkipped { get; set; }

    public List<string> Warnings { get; set; } = [];
    public string? ErrorMessage { get; set; }

    /// <summary>The sweep groups this run covered (a run-all covers several).</summary>
    public List<string> SweepKeysExecuted { get; set; } = [];

    public List<DocumentGovernanceSweepResultItem> ResultItems { get; set; } = [];

    /// <summary>Always false for a persisted row — a dry run writes no history. Kept explicit for legibility.</summary>
    public bool DryRun { get; set; }
}
