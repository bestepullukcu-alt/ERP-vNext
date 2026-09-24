using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// BL-439 — WHO a waiting task is asking, decided in one place.
///
/// <para>Three readers ask it: <c>AnswerInquiryHandler</c> (who may answer), <see cref="TaskReadAccessPolicy"/>
/// (who may open the task because of the question) and <c>TaskWorkItemProvider</c> (whose inbox holds the
/// question). One predicate, so the person who sees the question, the person who may read the task and the
/// person who may answer it can never be three different people.</para>
///
/// <para>Static and pure rather than a member of <see cref="ITaskLifecycleService"/>: the read rule has no
/// lifecycle dependency and must not grow one to ask a question about two fields.</para>
/// </summary>
public static class TaskInquiryRules
{
    /// <summary>
    /// Is this task asking <paramref name="userId"/> a question RIGHT NOW — Waiting, and naming them in
    /// <see cref="TaskItem.WaitingOnUserId"/>?
    ///
    /// <para>The lifecycle is load-bearing, not a belt to the braces. <c>ClearWaiting</c> runs on every way OUT of
    /// Waiting (transition, answer, release, return, reassign, a parent's cascade-cancel), so the id and the state
    /// should always agree — and if a future path ever forgets, the state wins: a question nobody is still waiting
    /// on grants nothing to anybody.</para>
    /// </summary>
    public static bool IsAskedOf(TaskItem task, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(task);

        return userId != Guid.Empty
               && task.Lifecycle == TaskLifecycle.Waiting
               && task.WaitingOnUserId == userId;
    }
}
