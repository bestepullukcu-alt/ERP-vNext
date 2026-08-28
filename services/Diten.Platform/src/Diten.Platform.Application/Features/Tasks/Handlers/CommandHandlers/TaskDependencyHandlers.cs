using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// BL-028 — the write side of task dependencies. The SHAPE existed (typed edges, a repository, a detail query
/// that reads them) but nothing could ever create one, so "this cannot start until that finishes" was a rule the
/// system modelled and never enforced.
///
/// <para><b>Own tasks only</b> (pack §12 Y3). Both ends are MOD-0024 tasks; an edge to another module's object is
/// not expressible, because MOD-0024 may manage these edges precisely because it owns both ends. The Task Center
/// renders them and hosts no editor.</para>
/// </summary>
public sealed class AddTaskDependencyHandler : IRequestHandler<AddTaskDependencyCommand, Response<Guid>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskDependencyRepository _dependencies;
    private readonly ITenantContext _tenantContext;

    public AddTaskDependencyHandler(
        ITaskItemRepository tasks,
        ITaskDependencyRepository dependencies,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _dependencies = dependencies;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(AddTaskDependencyCommand command, CancellationToken ct)
    {
        var dependsOn = command.Request.DependsOnTaskItemId;

        // A cycle of length one. Checked first because it is the only case where both ids are the same record and
        // every check below would otherwise report something less true ("duplicate", "not found").
        if (dependsOn == command.TaskItemId)
        {
            return Response<Guid>.Fail(
                "A task cannot depend on itself.", 400, TaskReasonCodes.DependencySelf, command.CorrelationId);
        }

        var task = await _tasks.GetByIdAsync(command.TaskItemId, ct);
        if (task is null)
        {
            return Response<Guid>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        // The read goes through the tenant execution filter, so an edge to another tenant's task reports NOT
        // FOUND rather than refusing — the caller learns nothing about what exists elsewhere.
        var predecessor = await _tasks.GetByIdAsync(dependsOn, ct);
        if (predecessor is null)
        {
            return Response<Guid>.Fail(
                "The task this one would depend on was not found.",
                404,
                TaskReasonCodes.DependencyTaskNotFound,
                command.CorrelationId);
        }

        var existing = await _dependencies.ListByTaskIdAsync(command.TaskItemId, ct);
        if (existing.Any(d => d.DependsOnTaskItemId == dependsOn))
        {
            return Response<Guid>.Fail(
                "That dependency already exists.",
                409,
                TaskReasonCodes.DependencyDuplicate,
                command.CorrelationId);
        }

        if (await WouldCloseCycleAsync(command.TaskItemId, dependsOn, ct))
        {
            return Response<Guid>.Fail(
                "That dependency would create a cycle.",
                400,
                TaskReasonCodes.DependencyCycle,
                command.CorrelationId);
        }

        var dependency = await _dependencies.CreateAsync(
            new TaskDependency
            {
                TenantId = _tenantContext.TenantId,
                TaskItemId = command.TaskItemId,
                DependsOnTaskItemId = dependsOn,
                DependencyType = command.Request.DependencyType
            },
            ct);

        return Response<Guid>.Success(dependency.Id, 201, command.CorrelationId);
    }

    /// <summary>
    /// Walks the predecessor chain FORWARD from the proposed predecessor. If <paramref name="taskItemId"/> is
    /// reachable from it, adding the edge closes a loop: A→B→A means A waits for B and B waits for A, so neither
    /// can ever start.
    ///
    /// <para>The walk is breadth-first over batched reads (one read per LEVEL, not per node) and carries a
    /// visited set — which also makes it terminate on a graph that already contains a cycle, rather than spinning
    /// on data written before this check existed.</para>
    /// </summary>
    private async Task<bool> WouldCloseCycleAsync(Guid taskItemId, Guid dependsOnTaskItemId, CancellationToken ct)
    {
        var visited = new HashSet<Guid> { dependsOnTaskItemId };
        var frontier = new List<Guid> { dependsOnTaskItemId };

        while (frontier.Count > 0)
        {
            var edges = await _dependencies.ListByTaskIdsAsync(frontier, ct);
            var next = new List<Guid>();

            foreach (var edge in edges)
            {
                // Only edges POINTING OUT of the frontier walk forward; ListByTaskIdsAsync returns both
                // directions, and following the other way would report a cycle where there is only a diamond.
                if (!frontier.Contains(edge.TaskItemId))
                {
                    continue;
                }

                if (edge.DependsOnTaskItemId == taskItemId)
                {
                    return true;
                }

                if (visited.Add(edge.DependsOnTaskItemId))
                {
                    next.Add(edge.DependsOnTaskItemId);
                }
            }

            frontier = next;
        }

        return false;
    }
}

/// <summary>Remove one edge from a task. An edge that is not this task's is a 404, never a silent success.</summary>
public sealed class RemoveTaskDependencyHandler : IRequestHandler<RemoveTaskDependencyCommand, Response<NoContent>>
{
    private readonly ITaskDependencyRepository _dependencies;

    public RemoveTaskDependencyHandler(ITaskDependencyRepository dependencies)
    {
        _dependencies = dependencies;
    }

    public async Task<Response<NoContent>> Handle(RemoveTaskDependencyCommand command, CancellationToken ct)
    {
        var dependency = await _dependencies.GetByIdAsync(command.DependencyId, ct);

        // Both conditions are one 404: an edge belonging to a DIFFERENT task must not be removable through this
        // task's URL, and saying "wrong owner" would confirm the edge exists.
        if (dependency is null || dependency.TaskItemId != command.TaskItemId)
        {
            return Response<NoContent>.Fail(
                "Dependency not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        await _dependencies.DeleteAsync(command.DependencyId, ct);
        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}
