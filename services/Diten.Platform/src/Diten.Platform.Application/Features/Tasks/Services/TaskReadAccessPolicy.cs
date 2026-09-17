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
/// on the write side), the manager of the task's holder in the org chart (the subordinate leg, below), or holds
/// <see cref="TaskPermissions.ReadAll"/>.</para>
///
/// <para><b>The subordinate leg (BL-417 option a, DCP-004 "Decision amendment 2026-09-15").</b> A task on the
/// caller's Ekibim list is readable by the caller. It is decided by the SAME resolver that builds that list
/// (<see cref="ITaskTeamResolver"/>) and the SAME predicate the list applies (<see cref="TaskTeamScope.Covers"/>),
/// so the list and the detail page cannot answer differently. No legal-entity restriction, by owner decision: the
/// position reporting chain may cross the company boundary, and the team follows it.</para>
///
/// <para><b>The parent leg is narrower than the direct leg on purpose.</b> A subtask is covered by its parent's
/// assignee and pool, not by the parent's creator or watchers — the prompt asks only for "the parent task's
/// assignee and pool holders" (WCN's subtask panel resolves a person who can already open the parent), and
/// widening it further would be a second, unrequested visibility rule.</para>
///
/// <para><b>Scope, team and read-all are asked only for the CURRENT caller.</b> They ride
/// <see cref="ITaskAssignmentScopeResolver"/>, <see cref="ITaskTeamResolver"/> and
/// <see cref="IActorPermissionContext"/>, which resolve "the actor making this request" — there is no
/// per-arbitrary-user variant of any of them in this codebase today. A consumer (e.g. the @mention check) that calls
/// this policy for somebody OTHER than the caller gets a true answer only from the data legs above; that is a real,
/// documented narrowing, not a silent gap.</para>
/// </summary>
public interface ITaskReadAccessPolicy
{
    Task<bool> CanReadAsync(TaskItem task, Guid actorUserId, CancellationToken ct);

    /// <summary>
    /// The DATA legs alone, ENUMERATED rather than asked about one actor: assignee, pool holders, creator,
    /// watchers, and the parent task's assignee/pool holders. This is exactly the "future consumer" the class doc
    /// above anticipated — an @mention candidate list needs "who", not "can this one person" — and it is written
    /// as a refactor of <see cref="CanReadAsync"/>'s own legs rather than a second copy of them, so the two
    /// cannot drift apart. Deliberately excludes the scope, subordinate and read-all legs: all three only answer for
    /// the CURRENT caller (see the class doc), so <see cref="CanReadAsync"/> would not admit an arbitrary candidate
    /// through them either — listing their members here would be a second rule.
    /// </summary>
    Task<IReadOnlySet<Guid>> ResolveDataLegCandidatesAsync(TaskItem task, CancellationToken ct);
}

/// <inheritdoc cref="ITaskReadAccessPolicy"/>
public sealed class TaskReadAccessPolicy : ITaskReadAccessPolicy
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskWatcherRepository _watchers;
    private readonly ITaskNotificationService _notifications;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ITaskAssignmentScopeResolver _scopes;
    private readonly ITaskTeamResolver _team;
    private readonly IActorPermissionContext _permissions;
    private readonly ICurrentUserContext _currentUser;

    public TaskReadAccessPolicy(
        ITaskItemRepository tasks,
        ITaskWatcherRepository watchers,
        ITaskNotificationService notifications,
        IOrganizationUnitRepository organizationUnits,
        ITaskAssignmentScopeResolver scopes,
        ITaskTeamResolver team,
        IActorPermissionContext permissions,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _watchers = watchers;
        _notifications = notifications;
        _organizationUnits = organizationUnits;
        _scopes = scopes;
        _team = team;
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public async Task<bool> CanReadAsync(TaskItem task, Guid actorUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        if ((await ResolveDataLegCandidatesAsync(task, ct)).Contains(actorUserId))
        {
            return true;
        }

        if (await ActorScopeCoversUnitAsync(task.OrganizationUnitId, actorUserId, ct))
        {
            return true;
        }

        if (await ActorTeamHoldsTaskAsync(task, actorUserId, ct))
        {
            return true;
        }

        // ReadAll is a permission grant on the CALLER, not a fact about actorUserId — see the "scope, team and
        // read-all" note on the interface. An arbitrary-actor call (actorUserId != caller) never gets a true
        // answer from this leg.
        return actorUserId == _currentUser.UserId && _permissions.Has(TaskPermissions.ReadAll);
    }

    public async Task<IReadOnlySet<Guid>> ResolveDataLegCandidatesAsync(TaskItem task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        var candidates = new HashSet<Guid>();

        if (task.AssigneeUserId is { } assignee && assignee != Guid.Empty)
        {
            candidates.Add(assignee);
        }

        if (task.CreatedByUserId is { } creator && creator != Guid.Empty)
        {
            candidates.Add(creator);
        }

        foreach (var holder in await _notifications.ResolvePoolHoldersAsync(task, ct))
        {
            candidates.Add(holder);
        }

        foreach (var watcher in await _watchers.ListByTaskIdAsync(task.Id, ct))
        {
            if (watcher.UserId != Guid.Empty)
            {
                candidates.Add(watcher.UserId);
            }
        }

        // The parent leg is narrower than the direct leg on purpose (class doc): only the parent's assignee and
        // pool holders, not its creator or watchers.
        if (task.ParentTaskItemId is { } parentId)
        {
            var parent = await _tasks.GetByIdAsync(parentId, ct);
            if (parent is not null)
            {
                if (parent.AssigneeUserId is { } parentAssignee && parentAssignee != Guid.Empty)
                {
                    candidates.Add(parentAssignee);
                }

                foreach (var holder in await _notifications.ResolvePoolHoldersAsync(parent, ct))
                {
                    candidates.Add(holder);
                }
            }
        }

        return candidates;
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

    /// <summary>
    /// BL-417 (a) — the task is held by one of the caller's subordinates. The resolver and the predicate are the
    /// Ekibim list's own (<see cref="ITaskTeamResolver"/>, <see cref="TaskTeamScope.Covers"/>); nothing about "who
    /// reports to whom" is decided here.
    /// </summary>
    private async Task<bool> ActorTeamHoldsTaskAsync(TaskItem task, Guid actorUserId, CancellationToken ct)
    {
        // The team is the CALLER's org chart. There is no "whose manager is this other person" variant, so an
        // @mention-style call about somebody else never gets a true answer from this leg — see the interface note.
        if (actorUserId != _currentUser.UserId)
        {
            return false;
        }

        var team = await _team.ResolveTeamAsync(ct);
        return team.Covers(task);
    }
}
