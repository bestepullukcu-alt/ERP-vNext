using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// What stops a task from moving, in ONE place — so the projection that greys a button and the handler that
/// refuses the write can never describe the rule differently.
///
/// <para><b>Three different things block, and they block different acts:</b></para>
/// <list type="bullet">
///   <item><description>a BLOCKING CHECKLIST item blocks COMPLETION (owned by ITaskChecklistService);</description></item>
///   <item><description>an open SUBTASK blocks COMPLETION (BL-035, below);</description></item>
///   <item><description>a DEPENDENCY blocks according to its TYPE, below.</description></item>
/// </list>
///
/// <para>Nothing CANCELLED ever blocks — not a predecessor, not a subtask. Called-off work is not pending work,
/// and treating it as unmet would let a task that turned out to be unnecessary lock its parent forever. It is why
/// the state vocabulary distinguishes `cancelled` from `not-started` at all.</para>
/// </summary>
public static class TaskBlockingRules
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

    /// <summary>
    /// The subtasks that stop their parent from being completed (BL-035).
    ///
    /// <para><b>This reverses an earlier decision, deliberately.</b> Until now open subtasks were reported and not
    /// enforced, on the reasoning that two competing blocking mechanisms would make "why can't I finish this?"
    /// unanswerable. The owner reversed it (2026-07-29) because the alternative sentence is worse: work split into
    /// three, two of them not done, and the whole thing complete. The original objection is also answered by
    /// something that did not exist when it was written — <c>blockedState.blockers[]</c> now names every blocker
    /// individually, so the question has an answer on screen.</para>
    ///
    /// <para>Industry practice is genuinely split: Jira and Asana warn without blocking, ServiceNow blocks, and MS
    /// Project derives the parent's state from its children. This product blocks.</para>
    ///
    /// <para>OPEN means neither Done nor Cancelled. A subtask cannot itself have subtasks (one level only), so this
    /// never recurses and a subtask is never subject to the rule — the query simply comes back empty.</para>
    /// </summary>
    public static IReadOnlyList<TaskItem> OpenSubtasksBlockingCompletion(IEnumerable<TaskItem> children)
        => children.Where(IsOpen).ToList();

    public static bool IsOpen(TaskItem task)
        => task.Lifecycle is not (TaskLifecycle.Done or TaskLifecycle.Cancelled);

    /// <summary>The predecessor's state in the shared task-state vocabulary the contract declares.</summary>
    public static string StateOf(TaskItem task) => task.Lifecycle switch
    {
        TaskLifecycle.Done => "done",
        TaskLifecycle.Cancelled => "cancelled",
        TaskLifecycle.InProgress or TaskLifecycle.PendingReview or TaskLifecycle.Waiting => "in-progress",
        _ => "not-started"
    };
}
