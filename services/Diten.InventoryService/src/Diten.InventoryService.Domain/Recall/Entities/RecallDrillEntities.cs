using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Recall.Enums;

namespace Diten.InventoryService.Domain.Recall.Entities;

/// <summary>
/// WP-0177-R5 (pack §4.11, KR-G) — a drill plan of one company: <c>DP-NN</c>, how often, which kind of lot, the next due date, the owner.
/// Mutable head (Version, If-Match). Nothing of a real recall hangs on it.
/// </summary>
public sealed class RecallDrillPlan : EntityBase
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public RecallDrillFrequency Frequency { get; set; }

    /// <summary>Only for <see cref="RecallDrillFrequency.CustomDays"/> (1–730).</summary>
    public int? CustomDays { get; set; }

    public RecallDrillScopeKind ScopeKind { get; set; }

    public DateOnly NextDueOn { get; set; }

    public Guid OwnerUserId { get; set; }

    public bool Active { get; set; } = true;

    public string? CreateIdempotencyKey { get; set; }

    public string? CreateRequestFingerprint { get; set; }

    public Guid CreatedByUser { get; set; }
}

/// <summary>
/// WP-0177-R5 (pack §4.12, §8.10, KR-G) — one drill run: a READ-ONLY rehearsal of a recall on one lot, timed by the SERVER
/// (<see cref="StartedAtUtc"/> at "Başlat", <see cref="EndedAtUtc"/> when step 3 completes), against the target of the settings AT START
/// (<see cref="TargetHours"/>), signed as <c>recall.drill.result</c> (subject <c>recall.drill-run</c>, field list <c>dr-v1</c>). Running →
/// Completed | Cancelled; a closed run never changes again (CT-R17: a cancelled run is kept). 0177 stores only the signature id.
/// </summary>
public sealed class RecallDrillRun : EntityBase
{
    public string Number { get; set; } = string.Empty;

    public Guid PlanId { get; set; }

    /// <summary>The plan's code at start (dr-v1 <c>planCode</c>; a later plan edit does not change a run).</summary>
    public string PlanCode { get; set; } = string.Empty;

    public RecallDrillRunStatus Status { get; set; } = RecallDrillRunStatus.Running;

    public RecallDrillSelectionMode SelectionMode { get; set; }

    public string LotId { get; set; } = string.Empty;

    public string LotNumber { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public List<RecallDrillStepRecord> Steps { get; set; } = [];

    public DateTimeOffset? EndedAtUtc { get; set; }

    public int? DurationMinutes { get; set; }

    /// <summary>The settings' <c>DrillTargetHours</c> when the run started (a later settings change does not move it).</summary>
    public int TargetHours { get; set; }

    public bool? Passed { get; set; }

    public int GapCount { get; set; }

    /// <summary>The run's trace snapshot (step 1, purpose <c>Drill</c>).</summary>
    public Guid? SnapshotId { get; set; }

    public string? SnapshotSha256 { get; set; }

    /// <summary>What step 1 found, for steps 2 and 3 (the scope summary and the customer list come from the SAME trace as the snapshot).</summary>
    public RecallDrillTraceSummary? Trace { get; set; }

    public RecallDrillPendingResult? PendingResult { get; set; }

    public string? ResultNote { get; set; }

    public string? ResultSignatureId { get; set; }

    public Guid? CompletedBy { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? CancelReason { get; set; }

    public Guid? CancelledBy { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public Guid StartedBy { get; set; }

    public string? CreateIdempotencyKey { get; set; }

    public string? CreateRequestFingerprint { get; set; }

    /// <summary>"running" while Running, null once closed — the partial unique index admits ONE running run per company (two concurrent
    /// starts cannot both pass, 409 <c>RECALL_DRILL_ALREADY_RUNNING</c>).</summary>
    public string? RunningKey { get; set; }

    /// <summary>Auto: how many candidates the pick was made from (the selection is audited).</summary>
    public int? CandidateCount { get; set; }
}

public sealed class RecallDrillStepRecord
{
    public RecallDrillStep Step { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }

    public Guid CompletedBy { get; set; }
}

public sealed class RecallDrillTraceSummary
{
    public int NodeCount { get; set; }

    public int LotCount { get; set; }

    public int StockCount { get; set; }

    public int ShipmentCount { get; set; }

    public List<string> Customers { get; set; } = [];

    public bool Truncated { get; set; }
}

/// <summary>The prepared result (R1 / R4 PendingAction pattern): the note to sign, 15 minutes, the preparer only.</summary>
public sealed class RecallDrillPendingResult
{
    public string Note { get; set; } = string.Empty;

    public Guid SetBy { get; set; }

    public DateTimeOffset SetAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;
}

/// <summary>
/// WP-0177-R5 — the timeline of a drill plan / run (insert-only): one row per command with its idempotency key (replay) and the signature id
/// of a signed result.
/// </summary>
public sealed class RecallDrillEvent : EntityBase
{
    public Guid SubjectId { get; set; }

    public RecallDrillEventKind Kind { get; set; }

    public Guid ActorUserId { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    public string? Text { get; set; }

    public Dictionary<string, string?> Refs { get; set; } = [];

    public string? SignatureId { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }

    public Guid CorrelationId { get; set; }
}
