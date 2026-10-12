using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Contracts;

/// <summary>
/// One MOD-0024 transition, as it was written: the id of the <c>TaskTransition</c> row (the idempotency key), what moved,
/// who did it and who holds the task now.
/// </summary>
public sealed record TaskTransitionObservation(
    Guid TenantId,
    Guid TaskItemId,
    Guid TransitionId,
    TaskTransitionKind Kind,
    TaskLifecycle FromLifecycle,
    TaskLifecycle ToLifecycle,
    Guid? ActorUserId,
    Guid? PreviousHolderUserId,
    Guid? CurrentHolderUserId,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// MOD-0280-FU01 (pack §3.3, §5.1 item 1, ADR-004) — how MOD-0024 tells time entry that a task moved. Called ONCE by
/// <c>TaskItemRepository.RecordIfMovedAsync</c>, after the transition row is written, in the same process and scope.
///
/// <para><b>Never throws into the caller.</b> The task write has already committed; a timer that could not be started or
/// stopped must never turn that into an error. An implementation logs its failure and the next timer or week read
/// reconciles (pack §13 "Hook failed").</para>
///
/// <para>On extraction (ADR-004) this becomes an outbox event; nothing on the MOD-0024 side changes but the binding.</para>
/// </summary>
public interface ITaskTransitionObserver
{
    Task OnTransitionRecordedAsync(TaskTransitionObservation observation, CancellationToken ct = default);
}
