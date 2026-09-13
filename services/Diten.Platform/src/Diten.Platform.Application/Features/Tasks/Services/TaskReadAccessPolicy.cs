using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// BL-349 — who may READ one task's detail/attachments, as opposed to who it is ASSIGNED to
/// (<see cref="TaskAssigneeEligibility"/>/<see cref="ITaskAssignmentGuard"/>, a different question with a
/// different owner set). ONE rule, asked by every read-side endpoint that resolves a single task by id.
///
/// <para><b>Why a task is readable.</b> The actor is the assignee, a current holder of the pool position
/// (<see cref="ITaskNotificationService.ResolvePoolHoldersAsync"/> — reused, not re-derived), the creator, a
/// watcher, the assignee/pool-holder of the PARENT task (so a subtask panel keeps working for whoever can already
/// see the parent), inside their <see cref="TaskAssignmentScope"/> for the task's own organization unit
/// (<see cref="TaskAssigneeEligibility.AllowsUnit"/> — the same scope leg <see cref="TaskAssignmentGuard"/> asks
/// on the write side), or holds <see cref="TaskPermissions.ReadAll"/>.</para>
///
/// <para><b>The parent leg is narrower than the direct leg on purpose.</b> A subtask is covered by its parent's
/// assignee and pool, not by the parent's creator or watchers — the prompt asks only for "the parent task's
/// assignee and pool holders" (WCN's subtask panel resolves a person who can already open the parent), and
/// widening it further would be a second, unrequested visibility rule.</para>
///
/// <para><b>Scope and read-all are asked only for the CURRENT caller.</b> Both ride
/// <see cref="ITaskAssignmentScopeResolver"/> and <see cref="IActorPermissionContext"/>, which resolve "the actor
/// making this request" — there is no per-arbitrary-user variant of either in this codebase today. A future
/// consumer (e.g. an @mention candidate check) that calls this policy for somebody OTHER than the caller gets a
/// true answer only from the data legs above; that is a real, documented narrowing, not a silent gap.</para>
/// </summary>
public interface ITaskReadAccessPolicy
{
    Task<bool> CanReadAsync(TaskItem task, Guid actorUserId, CancellationToken ct);
}

/// <inheritdoc cref="ITaskReadAccessPolicy"/>
public sealed class TaskReadAccessPolicy : ITaskReadAccessPolicy
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskWatcherRepository _watchers;
    private readonly ITaskNotificationService _notifications;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ITaskAssignmentScopeResolver _scopes;
    private readonly IActorPermissionContext _permissions;
    private readonly ICurrentUserContext _currentUser;

    public TaskReadAccessPolicy(
        ITaskItemRepository tasks,
        ITaskWatcherRepository watchers,
        ITaskNotificationService notifications,
        IOrganizationUnitRepository organizationUnits,
        ITaskAssignmentScopeResolver scopes,
        IActorPermissionContext permissions,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _watchers = watchers;
        _notifications = notifications;
        _organizationUnits = organizationUnits;
        _scopes = scopes;
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public async Task<bool> CanReadAsync(TaskItem task, Guid actorUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (task.AssigneeUserId == actorUserId || task.CreatedByUserId == actorUserId)
        {
            return true;
        }

        if ((await _notifications.ResolvePoolHoldersAsync(task, ct)).Contains(actorUserId))
        {
            return true;
        }

        if ((await _watchers.ListByTaskIdAsync(task.Id, ct)).Any(watcher => watcher.UserId == actorUserId))
        {
            return true;
        }

        if (task.ParentTaskItemId is { } parentId && await CanReadAsParentAsync(parentId, actorUserId, ct))
        {
            return true;
        }

        if (await ActorScopeCoversUnitAsync(task.OrganizationUnitId, actorUserId, ct))
        {
            return true;
        }

        // ReadAll is a permission grant on the CALLER, not a fact about actorUserId — see the "scope and
        // read-all" note on the interface. An arbitrary-actor call (actorUserId != caller) never gets a true
        // answer from this leg.
        return actorUserId == _currentUser.UserId && _permissions.Has(TaskPermissions.ReadAll);
    }

    private async Task<bool> CanReadAsParentAsync(Guid parentTaskItemId, Guid actorUserId, CancellationToken ct)
    {
        var parent = await _tasks.GetByIdAsync(parentTaskItemId, ct);
        if (parent is null)
        {
            return false;
        }

        return parent.AssigneeUserId == actorUserId
            || (await _notifications.ResolvePoolHoldersAsync(parent, ct)).Contains(actorUserId);
    }

    private async Task<bool> ActorScopeCoversUnitAsync(Guid organizationUnitId, Guid actorUserId, CancellationToken ct)
    {
        if (actorUserId != _currentUser.UserId)
        {
            return false;
        }

        var unit = await _organizationUnits.GetByIdAsync(organizationUnitId, ct);
        if (unit is null || unit.IsArchived)
        {
            return false;
        }

        var scope = await _scopes.ResolveAsync(ct);
        return TaskAssigneeEligibility.AllowsUnit(unit.Id, unit.LegalEntityId, scope);
    }
}
