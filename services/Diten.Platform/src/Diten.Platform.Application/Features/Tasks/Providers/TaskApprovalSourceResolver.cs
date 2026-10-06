using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Providers;

/// <summary>
/// BL-437 — MOD-0024 answering, for its own tasks, what an approval over one of them is about: the task's title,
/// who sent it for the decision, and where the task lives.
///
/// <para><b>Three object types, one task behind each.</b> MOD-0024 hands work to MOD-0023 under three identities —
/// approval (<see cref="TaskApprovalService.ApprovalObjectType"/>), review (<see cref="TaskReviewService.ReviewObjectType"/>)
/// and upward work request (<see cref="TaskUpwardRequestService.RequestObjectType"/>) — and every one of them uses the
/// task's own id as its ObjectId. So one read of the tasks answers all three.</para>
///
/// <para><b>WHO SENT IT differs by type, and that is the point of the owner's complaint.</b> "A task I assigned to
/// someone comes back to me for approval and I cannot tell why": that is a REVIEW — the holder finished the work
/// and handed it to its requester. Naming the task's creator there would name the reader themselves. So:</para>
/// <list type="bullet">
/// <item>review → whoever performed the latest <see cref="TaskTransitionKind.SubmittedForReview"/>, the act that
/// opened this round; the current holder when no such entry exists (history older than the transition log).</item>
/// <item>approval, work request → the task's creator. Both are opened by creating the task (an approval may also be
/// switched on by a later edit, and the edit log records no field for it — the creator is still the task's own
/// requester, the same answer the task projection gives for <c>Requester</c>).</item>
/// </list>
///
/// <para>READ ONLY and tenant-scoped through the ordinary repositories: a task in another tenant is never read,
/// and its approval keeps the generic title. Nothing here touches the approval, the workflow or a permission.</para>
/// </summary>
public sealed class TaskApprovalSourceResolver : IApprovalSourceResolver
{
    private static readonly HashSet<string> OwnedObjectTypes = new(StringComparer.Ordinal)
    {
        TaskApprovalService.ApprovalObjectType,
        TaskReviewService.ReviewObjectType,
        TaskUpwardRequestService.RequestObjectType
    };

    private readonly ITaskItemRepository _tasks;
    private readonly ITaskTransitionRepository _transitions;
    private readonly IUserDisplayNameResolver _displayNames;

    public TaskApprovalSourceResolver(
        ITaskItemRepository tasks,
        ITaskTransitionRepository transitions,
        IUserDisplayNameResolver displayNames)
    {
        _tasks = tasks;
        _transitions = transitions;
        _displayNames = displayNames;
    }

    public bool Handles(string objectType) => OwnedObjectTypes.Contains(objectType);

    public async Task<IReadOnlyDictionary<Guid, ApprovalSourceContext>> ResolveAsync(
        IReadOnlyCollection<WorkflowInstance> instances,
        WorkItemActor actor,
        CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, ApprovalSourceContext>();

        // ObjectId is free text in the engine; anything that is not a task id is simply not ours to answer.
        var owned = instances
            .Where(i => Handles(i.ObjectType))
            .Select(i => (Instance: i, TaskId: Guid.TryParse(i.ObjectId, out var id) ? id : Guid.Empty))
            .Where(x => x.TaskId != Guid.Empty)
            .ToList();
        if (owned.Count == 0)
        {
            return result;
        }

        var taskIds = owned.Select(x => x.TaskId).Distinct().ToList();
        var tasks = (await _tasks.ListByIdsAsync(taskIds, ct)).ToDictionary(t => t.Id);

        // Only review needs the history, so only reviewed tasks pay for reading it.
        var reviewedIds = owned
            .Where(x => x.Instance.ObjectType == TaskReviewService.ReviewObjectType && tasks.ContainsKey(x.TaskId))
            .Select(x => x.TaskId)
            .Distinct()
            .ToList();
        var submitters = reviewedIds.Count == 0
            ? new Dictionary<Guid, Guid>()
            : LatestSubmitters(await _transitions.ListByTaskIdsAsync(reviewedIds, ct));

        var requesterIds = new Dictionary<Guid, Guid?>();
        foreach (var (instance, taskId) in owned)
        {
            if (!tasks.TryGetValue(taskId, out var task))
            {
                continue;
            }

            requesterIds[instance.Id] = instance.ObjectType == TaskReviewService.ReviewObjectType
                ? (submitters.TryGetValue(taskId, out var submitter) ? submitter : task.AssigneeUserId)
                : task.CreatedByUserId;
        }

        var names = await _displayNames.ResolveAsync(
            requesterIds.Values.OfType<Guid>().Where(id => id != Guid.Empty).Distinct().ToList(), ct);

        foreach (var (instance, taskId) in owned)
        {
            if (!tasks.TryGetValue(taskId, out var task))
            {
                continue;
            }

            result[instance.Id] = new ApprovalSourceContext(
                Title: task.Title,
                Requester: Person(requesterIds[instance.Id], actor, names),
                DeepLink: TaskLinks.Record(task.Id));
        }

        return result;
    }

    // The repository returns each task's history newest first; the first submit seen per task is its latest.
    private static Dictionary<Guid, Guid> LatestSubmitters(IReadOnlyList<TaskTransition> transitions)
    {
        var latest = new Dictionary<Guid, Guid>();
        foreach (var transition in transitions
                     .Where(t => t.Kind == TaskTransitionKind.SubmittedForReview && t.ActorUserId is { } a && a != Guid.Empty)
                     .OrderByDescending(t => t.CreatedAt))
        {
            latest.TryAdd(transition.TaskItemId, transition.ActorUserId!.Value);
        }

        return latest;
    }

    // Same rule as the task projection's own Person(): the id is always real, an unresolved name stays null (never
    // the id in its place), and IsCurrentUser is the one thing the server can state for certain.
    private static WorkItemPersonDto? Person(Guid? userId, WorkItemActor actor, IReadOnlyDictionary<Guid, string> names)
    {
        if (userId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var resolved = names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : null;
        return new WorkItemPersonDto(id.ToString(), DisplayName: resolved, IsCurrentUser: id == actor.UserId);
    }
}
