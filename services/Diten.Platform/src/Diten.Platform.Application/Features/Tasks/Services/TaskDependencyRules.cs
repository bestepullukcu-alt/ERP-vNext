using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// BL-028 — what a dependency edge actually STOPS, in one place.
///
/// <para><b>Three different things block, and they block different acts.</b> Keeping the boundary explicit is the
/// point of this class, because the three were previously described only in prose:</para>
/// <list type="bullet">
///   <item><description>a BLOCKING CHECKLIST item blocks COMPLETION;</description></item>
///   <item><description>an open SUBTASK blocks COMPLETION (BL-035 — not implemented here);</description></item>
///   <item><description>a DEPENDENCY blocks according to its TYPE, below.</description></item>
/// </list>
///
/// <para>A CANCELLED predecessor never blocks anything — called-off work is not pending work. That is the same
/// rule BL-035 will apply to subtasks, and it is why the state vocabulary distinguishes `cancelled` from
/// `not-started` at all.</para>
/// </summary>
public static class TaskDependencyRules
{
    public const string StartActionCode = "start";
    public const string CompleteActionCode = "complete";

    /// <summary>
    /// Which act the edge stops. <c>*ToStart</c> edges constrain when this task may BEGIN; <c>*ToFinish</c> edges
    /// constrain when it may FINISH — the second half of the name is this task's end of the edge.
    /// </summary>
    public static string AffectedActionCode(TaskDependencyType type) => type switch
    {
        TaskDependencyType.FinishToStart or TaskDependencyType.StartToStart => StartActionCode,
        _ => CompleteActionCode
    };

    /// <summary>
    /// Whether the predecessor has reached the state this edge waits for. The FIRST half of the name is the
    /// predecessor's end: <c>Finish*</c> waits for it to be Done, <c>Start*</c> only for it to have begun.
    /// </summary>
    public static bool IsSatisfied(TaskDependencyType type, TaskItem predecessor)
    {
        // Cancelled work will never finish and never start; treating it as an unmet condition would park the
        // dependent task forever with no one able to clear it.
        if (predecessor.Lifecycle == TaskLifecycle.Cancelled)
        {
            return true;
        }

        return type switch
        {
            TaskDependencyType.FinishToStart or TaskDependencyType.FinishToFinish
                => predecessor.Lifecycle == TaskLifecycle.Done,
            _ => HasStarted(predecessor)
        };
    }

    /// <summary>
    /// Started means work has actually begun on it. Waiting and PendingReview count: both are states a task can
    /// only reach by having been started, and a Start-to-Start successor is waiting for the BEGINNING, not for
    /// uninterrupted progress.
    /// </summary>
    private static bool HasStarted(TaskItem task) => task.Lifecycle
        is TaskLifecycle.InProgress
        or TaskLifecycle.Waiting
        or TaskLifecycle.PendingReview
        or TaskLifecycle.Done;

    /// <summary>The predecessor's state in the shared task-state vocabulary the contract declares.</summary>
    public static string StateOf(TaskItem task) => task.Lifecycle switch
    {
        TaskLifecycle.Done => "done",
        TaskLifecycle.Cancelled => "cancelled",
        TaskLifecycle.InProgress or TaskLifecycle.PendingReview or TaskLifecycle.Waiting => "in-progress",
        _ => "not-started"
    };
}
