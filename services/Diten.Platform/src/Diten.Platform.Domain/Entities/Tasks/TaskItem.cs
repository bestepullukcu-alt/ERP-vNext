using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Domain.Entities.Tasks;

/// <summary>
/// MOD-0024 — the task aggregate. Named <c>TaskItem</c> (not <c>Task</c>) deliberately: a domain type called
/// <c>Task</c> collides with <see cref="System.Threading.Tasks.Task"/> in a codebase where every method returns
/// <c>Task&lt;T&gt;</c>.
///
/// <para><b>Boundaries.</b> This entity owns the task's NATIVE lifecycle only. It stores no approval/review state
/// machine — when <see cref="ApprovalRequired"/> or <see cref="ReviewRequired"/> is set, MOD-0023 owns the
/// decision and its instance is referenced by <see cref="WorkflowInstanceId"/> (pack §12 K2). Attachments are out
/// of scope (§12 Y4); binary storage belongs to an approved document/storage provider.</para>
///
/// <para><b>Phase-1 completeness.</b> Pool fields, <see cref="ProcessInstanceId"/>, <see cref="WorkflowInstanceId"/>
/// and the field-authorization metadata on <see cref="TaskFieldValue"/> all ship now, so Phases 2–5 are additive
/// with no migration (pack §19).</para>
/// </summary>
public sealed class TaskItem : TenantScopedEntity
{
    // ── Identity / description ───────────────────────────────────────────────
    public required string Title { get; set; }
    public string? Description { get; set; }

    // ── Lifecycle (SYSTEM-owned; a user never selects it — pack §12 Y2) ──────
    public TaskLifecycle Lifecycle { get; set; } = TaskLifecycle.Open;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    // ── Assignment (pack §12 K5) ─────────────────────────────────────────────
    public required TaskAssignmentTarget AssignmentTarget { get; set; }

    /// <summary>The holder. NULL for an unclaimed pool task — that is the whole point of a pool.</summary>
    public Guid? AssigneeUserId { get; set; }

    /// <summary>The offered POSITION for a pool task (MOD-0288 Position; always unit-bound — §12 K4).</summary>
    public Guid? PoolPositionId { get; set; }

    /// <summary>Who created it (used for creator-scope surfaces later — BL-016).</summary>
    public Guid? CreatedByUserId { get; set; }

    // ── Organization context (pack §12 K6) ───────────────────────────────────
    /// <summary>Owning unit/facility; defaults to the assignee's or pool position's unit.</summary>
    public required Guid OrganizationUnitId { get; set; }

    // ── Dates / effort ───────────────────────────────────────────────────────
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? StartAt { get; set; }

    /// <summary>Personal plan date; may differ from <see cref="DueAt"/> (surfaced as a conflict notice).</summary>
    public DateTimeOffset? PlannedDate { get; set; }

    public decimal? EstimateHours { get; set; }

    /// <summary>Accumulated effort. ALWAYS 0 at create and never settable there (pack §12 Y1).</summary>
    public decimal SpentHours { get; set; }

    // Remaining is DERIVED (Estimate - Spent, floored at 0) and intentionally NOT stored (pack §12 E4).

    public List<string> Tags { get; set; } = [];

    // ── Governance flags (decisions belong to other modules) ─────────────────
    /// <summary>Requests a MOD-0023 review. MOD-0024 stores no review state (pack §12 K2).</summary>
    public bool ReviewRequired { get; set; }

    /// <summary>Requests a MOD-0023 approval before work may start (pack §12 K2).</summary>
    public bool ApprovalRequired { get; set; }

    /// <summary>Manager CANDIDATE hint only — MOD-0023/MOD-0018 resolve actual authority.</summary>
    public Guid? ApprovalManagerUserId { get; set; }

    /// <summary>The MOD-0023 instance when an approval/review is running (set in Phase 3).</summary>
    public Guid? WorkflowInstanceId { get; set; }

    public bool EmailNotificationsEnabled { get; set; } = true;

    /// <summary>Policy flag only. Delegation ELIGIBILITY remains MOD-0018's decision (pack §12 Y5).</summary>
    public bool DelegationAllowed { get; set; }

    // ── Subtasks (Phase 2 — pack §12 E2) ─────────────────────────────────────

    /// <summary>
    /// The parent this task is a subtask of, or null for a top-level task.
    ///
    /// <para>A subtask is a FULL <see cref="TaskItem"/>, not a lighter sibling type: it keeps its own lifecycle,
    /// its own assignment (self / person / pool), its own dates and its own projected actions, all through the
    /// same code path. A separate lightweight entity would mean writing a second, half-featured engine —
    /// and then discovering it needs assignment, then dates, then actions.</para>
    ///
    /// <para><b>One level only.</b> A task carrying a parent may not itself be a parent; the server enforces it.
    /// Deeper hierarchies are the source system's business, which the Task Center deep-links to.</para>
    ///
    /// <para>Open subtasks do NOT block the parent's <c>complete</c> — blocking semantics belong to the
    /// checklist, and two competing blocking mechanisms would make "why can't I finish this?" unanswerable.</para>
    /// </summary>
    public Guid? ParentTaskItemId { get; set; }

    // ── Recurrence (Phase 4 behaviour; schema now) ───────────────────────────
    public Guid? RecurrenceRuleId { get; set; }

    /// <summary>Separates recurring instances of the same rule (contract `processInstanceId`).</summary>
    public string? ProcessInstanceId { get; set; }

    // ── Configurable fields (pack §12 K1 — never hard-coded columns) ─────────
    public List<TaskFieldValue> FieldValues { get; set; } = [];

    // ── Closure ──────────────────────────────────────────────────────────────
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? ClosureReasonCode { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// A configurable field's value, embedded on the task. Mirrors the executable contract's businessContext field
/// shape. <see cref="Classification"/>/<see cref="AccessState"/>/<see cref="Redacted"/> are stored from Phase 1
/// so BL-024 field-level authorization is additive; Phase 1 performs NO evaluation.
/// </summary>
public sealed class TaskFieldValue
{
    public required string DefinitionCode { get; set; }
    public required TaskFieldValueType ValueType { get; set; }

    /// <summary>String-encoded value; the declared <see cref="ValueType"/> governs interpretation.</summary>
    public string? Value { get; set; }

    public TaskFieldClassification Classification { get; set; } = TaskFieldClassification.Normal;
    public TaskFieldAccessState AccessState { get; set; } = TaskFieldAccessState.Visible;

    /// <summary>When true the value must be OMITTED from the browser payload — never CSS-hidden.</summary>
    public bool Redacted { get; set; }
}
