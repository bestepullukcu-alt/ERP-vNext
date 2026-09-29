using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.TimeEntry;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>MOD-0280-FU01 §19.1 — what a MOD-0024 transition means for a timer, as two pure rules.</summary>
public static class TaskTransitionTimerRules
{
    /// <summary>Start/Resume, performed BY the holder, into InProgress. Nobody else's act starts a person's timer.</summary>
    public static bool StartsRun(TaskTransitionObservation observation)
        => observation.Kind is TaskTransitionKind.Started or TaskTransitionKind.Resumed
           && observation.ToLifecycle == TaskLifecycle.InProgress
           && observation.ActorUserId is { } actor
           && actor == observation.CurrentHolderUserId;

    /// <summary>State-based stop: the task is no longer InProgress, or no longer held by the segment's person.</summary>
    public static bool EndsRunFor(TaskTransitionObservation observation, Guid segmentUserId)
        => observation.ToLifecycle != TaskLifecycle.InProgress || observation.CurrentHolderUserId != segmentUserId;

    public static TimerStartSource StartSourceOf(TaskTransitionObservation observation)
        => observation.Kind == TaskTransitionKind.Resumed ? TimerStartSource.TaskResumed : TimerStartSource.TaskStarted;
}
