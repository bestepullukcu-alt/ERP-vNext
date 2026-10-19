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
/// on the write side), the manager of the task's holder in the org chart (the subordinate leg, below), the person
/// a WAITING task is asking a question of, while it is asking (BL-439, <see cref="TaskInquiryRules.IsAskedOf"/>),
/// or holds <see cref="TaskPermissions.ReadAll"/>.</para>
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
///
/// <para><b>Why, not only whether</b> (WP-WCN-TASK-REQUEST-01 FIX3, item 8). <see cref="ReadGroundAsync"/> names the
/// kind of leg that admits a reader — a watcher and the person a waiting task asks are OBSERVERS — for the write gate,
/// which refuses observers every change. The legs, their order and the yes/no answer are the same ones
/// <see cref="CanReadAsync"/> gives; <c>TaskReadGroundTests</c> holds the two to one answer, task by task and batched.</para>
/// </summary>
public interface ITaskReadAccessPolicy
{
    Task<bool> CanReadAsync(TaskItem task, Guid actorUserId, CancellationToken ct);

    /// <summary>
    /// The DATA legs alone, ENUMERATED rather than asked about one actor: assignee, pool holders, creator,
    /// watchers, the person a waiting task is asking (BL-439), and the parent task's assignee/pool holders. This is exactly the "future consumer" the class doc
    /// above anticipated — an @mention candidate list needs "who", not "can this one person" — and it is written
    /// as a refactor of <see cref="CanReadAsync"/>'s own legs rather than a second copy of them, so the two
    /// cannot drift apart. Deliberately excludes the scope, subordinate and read-all legs: all three only answer for
    /// the CURRENT caller (see the class doc), so <see cref="CanReadAsync"/> would not admit an arbitrary candidate
    /// through them either — listing their members here would be a second rule.
    /// </summary>
    Task<IReadOnlySet<Guid>> ResolveDataLegCandidatesAsync(TaskItem task, CancellationToken ct);

    /// <summary>
    /// BL-484 — <see cref="CanReadAsync"/> for MANY tasks at once: the ids of those among <paramref name="tasks"/> that
    /// <paramref name="actorUserId"/> may read. The same legs in the same order, task by task; what is shared is only the
    /// reading — the watchers and the parents of all the tasks in one read each, and the caller's scope, team and
    /// ReadAll resolved once instead of once per task. A consumer that shows many tasks (a timesheet week) asks this,
    /// never <see cref="CanReadAsync"/> in a loop.
    /// <para><b>Duplicate ids.</b> Callers pass distinct rows (the time-entry adapter does). Should two rows carry the same
    /// id, the rule judges the FIRST one and ignores the rest.</para>
    /// <para>The default asks <see cref="CanReadAsync"/> task by task — what a test double needs; the rule overrides it.</para>
    /// </summary>
    async Task<IReadOnlySet<Guid>> ReadableTaskIdsAsync(IReadOnlyCollection<TaskItem> tasks, Guid actorUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var readable = new HashSet<Guid>();
        foreach (var task in tasks)
        {
            if (await CanReadAsync(task, actorUserId, ct))
            {
                readable.Add(task.Id);
            }
        }

        return readable;
    }

    /// <summary>
    /// WP-WCN-TASK-REQUEST-01 FIX3 (item 8) — WHY <paramref name="actorUserId"/> may read the task, as far as a write
    /// needs to know it: <see cref="TaskReadGround.None"/> exactly when <see cref="CanReadAsync"/> says no;
    /// <see cref="TaskReadGround.Observer"/> when the ONLY legs that admit them are watching it or being the person a
    /// waiting task is asking (BL-439); <see cref="TaskReadGround.Full"/> for every other leg. The same legs in the same
    /// order — the rule decides nothing new, it names the leg it already used.
    /// <para>The default (a test double that answers only <see cref="CanReadAsync"/>) never says Observer.</para>
    /// </summary>
    async Task<TaskReadGround> ReadGroundAsync(TaskItem task, Guid actorUserId, CancellationToken ct)
        => await CanReadAsync(task, actorUserId, ct) ? TaskReadGround.Full : TaskReadGround.None;

    /// <summary>
    /// FIX3 — <see cref="ReadGroundAsync"/> for MANY tasks at once, batched like <see cref="ReadableTaskIdsAsync"/>
    /// (a timesheet save asks it). A task absent from the answer is <see cref="TaskReadGround.None"/>.
    /// </summary>
    async Task<IReadOnlyDictionary<Guid, TaskReadGround>> ReadGroundsAsync(
        IReadOnlyCollection<TaskItem> tasks, Guid actorUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var grounds = new Dictionary<Guid, TaskReadGround>();
        foreach (var task in tasks.DistinctBy(t => t.Id))
        {
            grounds[task.Id] = await ReadGroundAsync(task, actorUserId, ct);
        }

        return grounds;
    }
}

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX3 (item 8) — which kind of leg admits a reader, as the write gate needs it. A watcher is
/// there to see the work ("visibility only, never action rights", pack §12 K3 / OD-4), and the person a waiting task
/// asks reads it in order to answer (BL-439); neither becomes somebody who changes or administers the task merely
/// because a write endpoint's own permission is in their token.
/// </summary>
public enum TaskReadGround
{
    /// <summary>Cannot read it — to every write, the task does not exist.</summary>
    None = 0,

    /// <summary>Reads it ONLY as a watcher and/or as the person a waiting task is asking.</summary>
    Observer = 1,

    /// <summary>Reads it through any other leg (holder, pool, creator, parent, scope, team, read-all, a request's addressee).</summary>
    Full = 2
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
        => await GroundAsync(task, actorUserId, stopAtFirstLeg: true, ct) != TaskReadGround.None;

    public Task<TaskReadGround> ReadGroundAsync(TaskItem task, Guid actorUserId, CancellationToken ct)
        => GroundAsync(task, actorUserId, stopAtFirstLeg: false, ct);

    /// <summary>
    /// One task, the legs in ONE order: the request's addressee, the data legs, scope, team, ReadAll.
    ///
    /// <para><paramref name="stopAtFirstLeg"/> is <see cref="CanReadAsync"/>: the first leg that admits ends the walk —
    /// its reads, its order and its answer exactly as before FIX3. Without it (<see cref="ReadGroundAsync"/>) an
    /// observer leg does not end the walk, because a FULL leg further on (scope, team, ReadAll) may still answer, and
    /// a watcher who is also in scope is no mere observer. Either way the task is readable exactly when some leg
    /// admits, so the two cannot disagree about WHETHER — only the second says WHY.</para>
    /// </summary>
    private async Task<TaskReadGround> GroundAsync(
        TaskItem task, Guid actorUserId, bool stopAtFirstLeg, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (AddresseeReads(task, actorUserId))
        {
            return TaskReadGround.Full;
        }

        var watchers = await _watchers.ListByTaskIdAsync(task.Id, ct);
        var parent = task.ParentTaskItemId is { } parentId ? await _tasks.GetByIdAsync(parentId, ct) : null;
        var legs = await DataLegsAsync(task, watchers, parent, ct);
        if (legs.Full.Contains(actorUserId))
        {
            return TaskReadGround.Full;
        }

        var observer = legs.Observers.Contains(actorUserId);
        if (observer && stopAtFirstLeg)
        {
            return TaskReadGround.Observer;
        }

        if (await ActorScopeCoversUnitAsync(task.OrganizationUnitId, actorUserId, ct))
        {
            return TaskReadGround.Full;
        }

        if (await ActorTeamHoldsTaskAsync(task, actorUserId, ct))
        {
            return TaskReadGround.Full;
        }

        // ReadAll is a permission grant on the CALLER, not a fact about actorUserId — see the "scope, team and
        // read-all" note on the interface. An arbitrary-actor call (actorUserId != caller) never gets a true
        // answer from this leg.
        if (actorUserId == _currentUser.UserId && _permissions.Has(TaskPermissions.ReadAll))
        {
            return TaskReadGround.Full;
        }

        return observer ? TaskReadGround.Observer : TaskReadGround.None;
    }

    public async Task<IReadOnlySet<Guid>> ResolveDataLegCandidatesAsync(TaskItem task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        var watchers = await _watchers.ListByTaskIdAsync(task.Id, ct);
        var parent = task.ParentTaskItemId is { } parentId ? await _tasks.GetByIdAsync(parentId, ct) : null;
        return (await DataLegsAsync(task, watchers, parent, ct)).All;
    }

    public async Task<IReadOnlySet<Guid>> ReadableTaskIdsAsync(
        IReadOnlyCollection<TaskItem> tasks, Guid actorUserId, CancellationToken ct)
        => (await GroundsAsync(tasks, actorUserId, stopAtFirstLeg: true, ct))
            .Where(ground => ground.Value != TaskReadGround.None)
            .Select(ground => ground.Key)
            .ToHashSet();

    public async Task<IReadOnlyDictionary<Guid, TaskReadGround>> ReadGroundsAsync(
        IReadOnlyCollection<TaskItem> tasks, Guid actorUserId, CancellationToken ct)
        => await GroundsAsync(tasks, actorUserId, stopAtFirstLeg: false, ct);

    /// <summary>
    /// <see cref="GroundAsync"/> for many tasks: the same legs in the same order, task by task; what is shared is only
    /// the reading — the watchers and the parents of all the tasks in one read each, and the caller's scope, team and
    /// ReadAll resolved once instead of once per task (BL-484).
    /// </summary>
    private async Task<Dictionary<Guid, TaskReadGround>> GroundsAsync(
        IReadOnlyCollection<TaskItem> tasks, Guid actorUserId, bool stopAtFirstLeg, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        var grounds = new Dictionary<Guid, TaskReadGround>();
        var distinct = tasks.DistinctBy(t => t.Id).ToList();          // a repeated id: the first row is the one judged
        if (distinct.Count == 0)
        {
            return grounds;
        }

        // The data legs — the SAME candidates ResolveDataLegCandidatesAsync names — with the watchers and the parents of
        // every task read once.
        var watchers = (await _watchers.ListByTaskIdsAsync(distinct.Select(t => t.Id).ToList(), ct)).ToLookup(w => w.TaskItemId);
        var parentIds = distinct.Select(t => t.ParentTaskItemId).OfType<Guid>().Distinct().ToList();
        var parents = parentIds.Count == 0
            ? new Dictionary<Guid, TaskItem>()
            : (await _tasks.ListByIdsAsync(parentIds, ct)).ToDictionary(p => p.Id);

        // FIX4 (item 3) — the pool holders of every task and every parent in ONE seat read (it was one per pool task).
        var poolHolders = await _notifications.ResolvePoolHoldersAsync(distinct.Concat(parents.Values).ToList(), ct);

        var undecided = new List<TaskItem>();
        foreach (var task in distinct)
        {
            var parent = task.ParentTaskItemId is { } parentId && parents.TryGetValue(parentId, out var found) ? found : null;
            if (AddresseeReads(task, actorUserId))
            {
                grounds[task.Id] = TaskReadGround.Full;
                continue;
            }

            var legs = await DataLegsAsync(task, watchers[task.Id], parent, ct, poolHolders);
            if (legs.Full.Contains(actorUserId))
            {
                grounds[task.Id] = TaskReadGround.Full;
                continue;
            }

            // What the data legs say, unless a caller-only leg below says Full.
            var observer = legs.Observers.Contains(actorUserId);
            grounds[task.Id] = observer ? TaskReadGround.Observer : TaskReadGround.None;
            if (!(observer && stopAtFirstLeg))
            {
                undecided.Add(task);
            }
        }

        // Scope, team and ReadAll answer only for the CURRENT caller (interface note) — for anyone else, the data legs
        // were the whole answer, exactly as in CanReadAsync.
        if (undecided.Count == 0 || actorUserId != _currentUser.UserId)
        {
            return grounds;
        }

        // Then, task by task and in CanReadAsync's order: scope, team, ReadAll — each caller-wide fact resolved once, on
        // first need, and each unit read once.
        var units = new Dictionary<Guid, Domain.Entities.Organization.OrganizationUnit?>();
        TaskAssignmentScope? scope = null;
        TaskTeamScope? team = null;
        bool? readAll = null;
        foreach (var task in undecided)
        {
            if (!units.TryGetValue(task.OrganizationUnitId, out var unit))
            {
                unit = await _organizationUnits.GetByIdAsync(task.OrganizationUnitId, ct);
                units[task.OrganizationUnitId] = unit;
            }

            if (unit is not null && !unit.IsArchived)
            {
                scope ??= await _scopes.ResolveAsync(ct);
                if (TaskAssigneeEligibility.AllowsUnit(unit.Id, unit.LegalEntityId, scope))
                {
                    grounds[task.Id] = TaskReadGround.Full;
                    continue;
                }
            }

            team ??= await _team.ResolveTeamAsync(ct);
            if (team.Covers(task) || TaskRequestPolicy.TeamSeesPendingOffer(task, team))
            {
                grounds[task.Id] = TaskReadGround.Full;
                continue;
            }

            readAll ??= _permissions.Has(TaskPermissions.ReadAll);
            if (readAll.Value)
            {
                grounds[task.Id] = TaskReadGround.Full;
            }
        }

        return grounds;
    }

    /// <summary>
    /// The data legs from the task's watchers and parent, however they were read (one task, or a batch) — split by
    /// KIND (FIX3, item 8): <see cref="DataLegs.Observers"/> are the watchers and the person a waiting task is asking;
    /// everything else is <see cref="DataLegs.Full"/>. Who is a candidate at all is unchanged (<see cref="DataLegs.All"/>).
    /// </summary>
    private async Task<DataLegs> DataLegsAsync(
        TaskItem task, IEnumerable<TaskWatcher> watchers, TaskItem? parent, CancellationToken ct,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>? poolHolders = null)
    {
        // FIX4 (item 3) — a batch hands its pool holders in, read once; a single task asks for its own.
        async Task<IReadOnlyList<Guid>> PoolOf(TaskItem pooled) =>
            poolHolders is not null && poolHolders.TryGetValue(pooled.Id, out var known)
                ? known
                : await _notifications.ResolvePoolHoldersAsync(pooled, ct);

        var full = new HashSet<Guid>();
        var observers = new HashSet<Guid>();

        if (task.AssigneeUserId is { } assignee && assignee != Guid.Empty)
        {
            full.Add(assignee);
        }

        if (task.CreatedByUserId is { } creator && creator != Guid.Empty)
        {
            full.Add(creator);
        }

        foreach (var holder in await PoolOf(task))
        {
            full.Add(holder);
        }

        // Watchers/consultants: visibility only, never action rights (pack §12 K3, OD-4) — an OBSERVER leg.
        foreach (var watcher in watchers)
        {
            if (watcher.UserId != Guid.Empty)
            {
                observers.Add(watcher.UserId);
            }
        }

        /*
         * BL-439 — THE PERSON THIS TASK IS ASKING, and only while it is asking (TaskInquiryRules.IsAskedOf — the
         * SAME predicate the answer handler and the inbox item ask). Fail-closed on both ends: the moment the
         * question is answered or withdrawn, ClearWaiting drops the id and the leg answers no.
         *
         * THIS task only — deliberately absent from the parent leg below: being asked about a subtask admits the
         * addressee to that subtask, never to its parent or its siblings.
         *
         * Reading is ALL it grants — an OBSERVER leg (FIX3): the write gate lets them comment and answer, and refuses
         * every change and every administrative verb they would otherwise reach through a key in their token.
         */
        if (task.WaitingOnUserId is { } asked && TaskInquiryRules.IsAskedOf(task, asked))
        {
            observers.Add(asked);
        }

        // BL-509 — the person a request waits on is deliberately NOT a data leg: these candidates are enumerated
        // to every reader (the @mention picker), and who a request was put to is for its two parties only. Their
        // read is the caller-only leg in CanReadAsync (AddresseeReads).

        // The parent leg is narrower than the direct leg on purpose (class doc): only the parent's assignee and
        // pool holders, not its creator or watchers.
        // A parent id that resolves to nothing (deleted, gone) grants nothing.
        if (task.ParentTaskItemId is not null && parent is not null)
        {
            if (parent.AssigneeUserId is { } parentAssignee && parentAssignee != Guid.Empty)
            {
                full.Add(parentAssignee);
            }

            foreach (var holder in await PoolOf(parent))
            {
                full.Add(holder);
            }
        }

        return new DataLegs(full, observers);
    }

    /// <summary>The data-leg candidates by kind; <see cref="All"/> is the candidate set the @mention picker enumerates.</summary>
    private sealed record DataLegs(HashSet<Guid> Full, HashSet<Guid> Observers)
    {
        public IReadOnlySet<Guid> All => Full.Union(Observers).ToHashSet();
    }

    /// <summary>
    /// BL-509 — THE PERSON A REQUEST IS ADDRESSED TO reads it while it waits for their answer
    /// (<see cref="TaskRequestPolicy.IsOfferedTo"/> — the SAME predicate the accept / decline handlers and the inbox
    /// ask). The moment they decline or the sender withdraws, the leg answers no; once they accept they are the
    /// assignee and the first data leg holds.
    ///
    /// <para><b>The CALLER only</b>, like scope and read-all: asked about somebody else (an @mention check) it says
    /// no, so a sender cannot @mention the person asked into a feed the sender's colleagues read.</para>
    /// </summary>
    private bool AddresseeReads(TaskItem task, Guid actorUserId)
        => actorUserId == _currentUser.UserId && TaskRequestPolicy.IsOfferedTo(task, actorUserId);

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
        return team.Covers(task) || TaskRequestPolicy.TeamSeesPendingOffer(task, team);
    }
}
