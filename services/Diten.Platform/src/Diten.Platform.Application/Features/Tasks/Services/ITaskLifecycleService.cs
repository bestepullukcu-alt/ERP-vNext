using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// MOD-0024 — the SINGLE owner of task lifecycle semantics: the initial state, permitted transitions, the
/// normalized projection, and derived effort. Nothing else in the codebase may compute these, so the engine and
/// the Task Center projection can never disagree (pack §4, §8.4).
/// </summary>
public interface ITaskLifecycleService
{
    /// <summary>
    /// The initial lifecycle for a new task. SYSTEM-decided — a user never picks it. An approval-gated task does
    /// not start life as "startable" (pack §12 Y2).
    /// </summary>
    TaskLifecycle ResolveInitialLifecycle(bool approvalRequired);

    /// <summary>Maps native lifecycle → the contract's normalizedStatus (five-value set).</summary>
    string ToNormalizedStatus(TaskItem task);

    /// <summary>
    /// The waiting reason, or null. The contract treats <c>normalizedStatus == "Waiting"</c> and
    /// <c>waitingContext</c> as a BIDIRECTIONAL pair, so this must be non-null exactly when the normalized
    /// status is Waiting.
    /// </summary>
    TaskWaitingContext? ResolveWaitingContext(TaskItem task);

    /// <summary>Remaining effort = Estimate − Spent, floored at 0. Derived, never stored (pack §12 E4).</summary>
    decimal? CalculateRemainingHours(TaskItem task);

    /// <summary>True when the task is in a terminal state (read-only, no state-changing action).</summary>
    bool IsTerminal(TaskItem task);

    /// <summary>Whether a transition is allowed; the reason code explains a refusal.</summary>
    bool CanTransition(TaskItem task, TaskLifecycle target, out string? reasonCode);
}

/// <summary>Why a task is waiting — projected as the contract's <c>waitingContext</c>.</summary>
public sealed record TaskWaitingContext(string Type, string? WaitingOn, DateTimeOffset? Since, DateTimeOffset? ExpectedUntil);

public static class TaskWaitingTypes
{
    /// <summary>Blocked pending a MOD-0023 approval decision (pack §12 K2).</summary>
    public const string Approval = "approval";

    /// <summary>Owner has sent it for review; MOD-0023 owns the review decision.</summary>
    public const string Review = "review";

    /// <summary>Waiting on external information.</summary>
    public const string ExternalInformation = "externalInformation";
}
