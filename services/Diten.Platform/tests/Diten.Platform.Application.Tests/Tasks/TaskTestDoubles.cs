using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using MediatR;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.Tasks;

// MOD-0024 — shared in-memory doubles. Repositories that Phase 1 must NOT write to throw NotSupportedException,
// so an unintended write shows up as a failing test rather than silent behaviour.

internal static class TaskTestData
{
    internal static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid OtherTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    internal static readonly Guid Me = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    internal static readonly Guid Rival = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    /// <summary>The name the resolver hands back for <see cref="Me"/>, so a snapshot can be told from a re-resolve.</summary>
    internal const string MeDisplayName = "Ali Tufanoğlu";

    /// <summary>
    /// A THIRD person. Handing work on needs one: with only two users, "the holder", "the requester" and "the new
    /// assignee" collapse into each other and an authority rule can pass by coincidence.
    /// </summary>
    internal static readonly Guid Other = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
}

internal sealed class FakeCurrentUserContext(Guid userId) : ICurrentUserContext
{
    public Guid UserId { get; } = userId;
    public string? Email => "me@diten.local";
    public string? DisplayName => "Me";
    public string ActorName => Email!;
    public bool IsAuthenticated => true;
}

/// <summary>
/// Stands in for MOD-0018-FU15's <c>OrgDataScopeResolver</c> (BL-057).
///
/// <para>The scopes are handed in rather than derived, so a test states the org reality it wants in one line and
/// the assertion is about the TASK rule rather than about re-deriving the resolver's own behaviour. An empty
/// array is the resolver's real fail-closed answer for a user with no active position assignment.</para>
/// </summary>
internal sealed class FakeDataScopeResolver(params EntitlementDataScope[] scopes) : IDataScopeResolver
{
    public Task<IReadOnlyList<EntitlementDataScope>> ResolveAsync(
        Guid tenantId, Guid userId, string moduleCode, string? featureCode, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<EntitlementDataScope>>(scopes.ToList());
}

internal sealed class FakeTenantContext(Guid tenantId) : ITenantContext
{
    public Guid TenantId { get; private set; } = tenantId;
    public bool IsResolved => true;
    public bool IsPlatformContext => false;
    public Guid? TargetTenantId => null;
    public void SetTenant(Guid tenantId) => TenantId = tenantId;
    public void SetPlatformContext(Guid targetTenantId) { }
    public void ClearTenant() { }
}

/// <summary>
/// In-memory task store that reproduces the ONE behaviour the claim race depends on: the update only applies when
/// the expected version still matches (the repository's conditional write).
/// </summary>
internal sealed class FakeTaskItemRepository : ITaskItemRepository
{
    private readonly List<TaskItem> _items = [];

    public FakeTaskItemRepository(params TaskItem[] seed) => _items.AddRange(seed);

    public IReadOnlyList<TaskItem> Items => _items;

    /// <summary>
    /// How many of the NEXT expected-version writes must lose their race.
    ///
    /// <para>Without this, "claim before send" and "send before claim" pass the same tests: nothing could make the
    /// conditional write fail, so the ORDER of the two steps was never observable. A guard nothing can trip is a
    /// comment, not a guarantee.</para>
    /// </summary>
    public int ForcedUpdateConflicts { get; set; }

    /// <summary>
    /// WC-1 — the lifecycle event log this store writes to, exactly as the real repository does.
    ///
    /// <para><b>It lives HERE rather than being injected</b> because that is where it lives in production: the
    /// decision that a transition happened belongs to the write, not to the writer, so a double that let a handler
    /// test opt out of recording would be testing a system nobody ships. Every handler test therefore gets the
    /// history for free, and <c>repo.Transitions.Events</c> is how a test reads it.</para>
    ///
    /// <para>Hand it to <c>TaskWorkItemProvider</c> when a projection test needs to see what the handlers wrote.</para>
    /// </summary>
    public FakeTaskTransitionRepository Transitions { get; } = new();

    public Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct = default)
    {
        _items.Add(task);

        // The entry that opens the task's history — see TaskItemRepository.CreateAsync for why every new task has
        // one and why nothing older is backfilled.
        var intent = task.ReadDeclaredIntent();
        Record(task.Id, intent?.Kind ?? TaskTransitionKind.Created, task.Lifecycle, task.Lifecycle, intent);
        return Task.FromResult(task);
    }

    /*
     * The pre-image diff, mirroring TaskItemRepository.UpdateAsync's FindOneAndReplace.
     *
     * `stored` is the previous document and has not been overwritten yet at the call site, so the comparison here
     * is the same comparison the driver's ReturnDocument.Before gives production. Keeping the two in step is what
     * makes a handler test's assertion about history mean anything about the running system.
     */
    private void RecordIfMoved(TaskItem previous, TaskItem current)
    {
        var moved = previous.Lifecycle != current.Lifecycle
                    || previous.AssigneeUserId != current.AssigneeUserId
                    || previous.AcceptedByUserId != current.AcceptedByUserId;

        var intent = current.ReadDeclaredIntent();
        if (!moved && intent is null)
        {
            return;
        }

        Record(current.Id, intent?.Kind ?? TaskTransitionKind.Unknown, previous.Lifecycle, current.Lifecycle, intent);
    }

    private void Record(
        Guid taskItemId,
        TaskTransitionKind kind,
        TaskLifecycle from,
        TaskLifecycle to,
        TaskTransitionIntent? intent)
        => Transitions.CreateAsync(new TaskTransition
        {
            TenantId = TaskTestData.Tenant,
            /*
             * A DISTINCT CreatedAt per entry, nudged forward by how many are already logged.
             *
             * Several transitions inside one test can land in the same tick, and the feed's whole contract is that
             * it is time-ordered — an assertion about "created, then accepted, then started" would otherwise pass
             * or fail on clock resolution. The Id tie-break keeps a tied order STABLE; this makes it CORRECT,
             * which is what the test is actually about.
             */
            CreatedAt = DateTimeOffset.UtcNow.AddMilliseconds(Transitions.Count),
            TaskItemId = taskItemId,
            Kind = kind,
            FromLifecycle = from,
            ToLifecycle = to,
            ActorUserId = intent?.ActorUserId,
            Reason = intent?.Reason,
            ReasonCode = intent?.ReasonCode
        });

    public Task<IReadOnlyList<TaskItem>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(_items.Where(x => ids.Contains(x.Id)).ToList());

    // Mirrors the tenant + IsDeleted execution filter: another tenant's row is invisible, not an error.
    //
    // Returns a DETACHED copy, like the real repository: Mongo deserializes a fresh document per read, so a
    // handler that mutates the entity and then loses the expected-version write leaves the stored state
    // untouched. Handing out the stored reference instead made a REJECTED write still appear to take effect —
    // a false green that hid exactly the concurrency behaviour these tests exist to prove.
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(
            x => x.Id == id && x.TenantId == TaskTestData.Tenant && !x.IsDeleted);
        return Task.FromResult(stored is null ? null : Detach(stored));
    }

    private static TaskItem Detach(TaskItem source)
    {
        var copy = new TaskItem
        {
            TenantId = source.TenantId,
            Title = source.Title,
            AssignmentTarget = source.AssignmentTarget,
            OrganizationUnitId = source.OrganizationUnitId
        };

        CopyWritableFields(from: source, to: copy);
        DetachCollections(from: source, to: copy);
        return copy;
    }

    /*
     * The COLLECTIONS, down to their ELEMENTS.
     *
     * Reflection copies references, so without this a handler that appended to Tags or FieldValues on its copy
     * reached into the stored document with no write at all. Re-allocating the lists alone was still a half-
     * truth: TaskFieldValue's Value, Redacted and AccessState all have setters, so editing a value IN PLACE
     * leaked the same way. Tags need no element clone — strings cannot be edited in place.
     */
    private static void DetachCollections(TaskItem from, TaskItem to)
    {
        to.Tags = [.. from.Tags];
        to.FieldValues = from.FieldValues.Select(CloneFieldValue).ToList();
    }

    /*
     * BY REFLECTION, exactly like CopyWritableFields above — and that sameness is the point.
     *
     * The hand-written version listed five of TaskFieldValue's six writable members and dropped Classification,
     * so a Confidential value came back from the store as Normal: the member carrying the BL-024 "omit from the
     * browser payload" rule, silently downgraded. One missing line was the symptom; the cause was two different
     * disciplines in one file, one of which carries new properties automatically and one of which forgets them.
     * A property added to TaskFieldValue tomorrow now travels here without anyone remembering this method —
     * and EVERY_writable_member_of_a_field_value_survives_detachment fails if that ever stops being true.
     */
    private static TaskFieldValue CloneFieldValue(TaskFieldValue source)
    {
        var clone = new TaskFieldValue
        {
            DefinitionCode = source.DefinitionCode,
            ValueType = source.ValueType
        };

        foreach (var property in typeof(TaskFieldValue)
                     .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                     .Where(p => p is { CanRead: true, CanWrite: true }))
        {
            property.SetValue(clone, property.GetValue(source));
        }

        return clone;
    }

    /// <summary>Every writable property, so the double can never decide which fields are persistable.</summary>
    private static void CopyWritableFields(TaskItem from, TaskItem to)
    {
        foreach (var property in typeof(TaskItem)
                     .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                     .Where(p => p is { CanRead: true, CanWrite: true }))
        {
            property.SetValue(to, property.GetValue(from));
        }
    }

    // DETACHED copies, for the same reason GetByIdAsync hands them out: Mongo deserializes a fresh document per
    // read. Handing out the stored reference made every expected-version write from a list read look like a LOST
    // RACE — the handler stamps the entity, and the double then compared the stored object with itself. A sweep
    // that reads a list and writes a claim could not be tested at all until this matched the real repository.
    public Task<IReadOnlyList<TaskItem>> GetAllForTenantAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(
            _items.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted).Select(Detach).ToList());

    public Task<IReadOnlyList<TaskItem>> ListByAssigneeAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(_items
            .Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted && x.AssigneeUserId == userId)
            .ToList());

    public Task<IReadOnlyList<TaskItem>> ListByParentAsync(Guid parentTaskItemId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(_items
            .Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted && x.ParentTaskItemId == parentTaskItemId)
            .Select(Detach)
            .ToList());

    public Task<IReadOnlyList<TaskItem>> ListByParentsAsync(
        IReadOnlyCollection<Guid> parentTaskItemIds,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(_items
            .Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted
                        && x.ParentTaskItemId is not null && parentTaskItemIds.Contains(x.ParentTaskItemId.Value))
            .Select(Detach)
            .ToList());

    public Task<IReadOnlyList<TaskItem>> ListUnclaimedByPositionsAsync(
        IReadOnlyCollection<Guid> positionIds,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(_items
            .Where(x => x.TenantId == TaskTestData.Tenant
                        && !x.IsDeleted
                        && x.AssignmentTarget == Domain.Enums.Tasks.TaskAssignmentTarget.PositionPool
                        && x.AssigneeUserId is null
                        && x.PoolPositionId is not null
                        && positionIds.Contains(x.PoolPositionId.Value))
            .ToList());

    public Task<bool> UpdateAsync(TaskItem task, int expectedVersion, CancellationToken ct = default)
    {
        if (ForcedUpdateConflicts > 0)
        {
            // Exactly what Mongo's conditional write does when another writer got there first: no exception, a
            // FALSE, and the stored document untouched.
            ForcedUpdateConflicts--;
            return Task.FromResult(false);
        }

        // The real repository stamps the passed entity BEFORE its conditional write (TaskRepositories.cs:79), so a
        // handler that writes twice in one request (persist the edit, then store the workflow instance id) can pass
        // task.Version the second time. The double must stamp it too, or that second write always misses.
        task.Version = expectedVersion + 1;

        var stored = _items.FirstOrDefault(x => x.Id == task.Id && x.TenantId == TaskTestData.Tenant);
        if (stored is null || stored.Version != expectedVersion)
        {
            // Conditional write missed → the caller must surface a controlled conflict.
            return Task.FromResult(false);
        }

        // Replace the WHOLE document, like the real repository does. Copying a hand-picked subset of fields made
        // every field outside that list silently unwritable: a handler could persist ApprovalRequired or
        // WorkflowInstanceId, return 204, and the next read would still show the old value — the test double, not
        // the code, decided what "saved" meant.
        // BEFORE the overwrite, while `stored` is still the pre-image — the whole basis of the diff.
        RecordIfMoved(previous: stored, current: task);

        CopyWritableFields(from: task, to: stored);
        // …and the stored document gets its OWN collections. Copying the caller's references would make every
        // later edit of theirs into stored state, with no write and no version check — the same leak as an
        // undetached read, on the way back out.
        DetachCollections(from: task, to: stored);
        stored.Version = expectedVersion + 1;
        return Task.FromResult(true);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == id && x.TenantId == TaskTestData.Tenant);
        if (stored is not null)
        {
            stored.IsDeleted = true;
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// WC-1 — the in-memory lifecycle event log. Ordered NEWEST FIRST with the same <c>Id</c> tie-break the real
/// repository uses, because a projection test that saw a different order from production would prove nothing
/// about the feed the user reads.
/// </summary>
internal sealed class FakeTaskTransitionRepository : ITaskTransitionRepository
{
    private readonly List<TaskTransition> _events = [];

    public IReadOnlyList<TaskTransition> Events => _events;

    /// <summary>How many entries are already in the log — used to keep their timestamps distinct (see
    /// <c>FakeTaskItemRepository.Record</c>).</summary>
    public int Count => _events.Count;

    public Task<TaskTransition> CreateAsync(TaskTransition transition, CancellationToken ct = default)
    {
        _events.Add(transition);
        return Task.FromResult(transition);
    }

    public Task<IReadOnlyList<TaskTransition>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
        => Task.FromResult(Order(_events.Where(x => x.TaskItemId == taskItemId)));

    public Task<IReadOnlyList<TaskTransition>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default)
        => Task.FromResult(Order(_events.Where(x => taskItemIds.Contains(x.TaskItemId))));

    private static IReadOnlyList<TaskTransition> Order(IEnumerable<TaskTransition> transitions)
        => transitions
            .OrderByDescending(transition => transition.CreatedAt)
            .ThenByDescending(transition => transition.Id)
            .ToList();
}

internal sealed class FakeTaskAssignmentRepository : ITaskAssignmentRepository
{
    private readonly List<TaskAssignment> _events = [];

    public IReadOnlyList<TaskAssignment> Events => _events;

    public Task<TaskAssignment> CreateAsync(TaskAssignment assignment, CancellationToken ct = default)
    {
        _events.Add(assignment);
        return Task.FromResult(assignment);
    }

    public Task<IReadOnlyList<TaskAssignment>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskAssignment>>(
            _events.Where(x => x.TaskItemId == taskItemId).ToList());
}

/// <summary>
/// The seeded assignment table, and — since the seat-directory refactor — the seat directory over it.
///
/// <para><b>Why it wears both hats.</b> Nine consumers stopped taking <c>IPositionAssignmentRepository</c> and
/// started taking <see cref="ITaskSeatDirectory"/>. Every existing test constructs them with this double, so the
/// alternative was editing dozens of test bodies for a refactor that changes no behaviour — and an edited test
/// is a test whose evidence you have to re-earn. Implementing the directory here keeps every one of those bodies
/// byte-identical.</para>
///
/// <para>The directory members DELEGATE to the real <see cref="TaskSeatDirectory"/> rather than re-implementing
/// the active-window rule: a second copy in test code would defeat the entire point of the round, and it would
/// be the copy that never fails.</para>
/// </summary>
internal sealed class FakePositionAssignmentRepository(params PositionAssignment[] seed)
    : IPositionAssignmentRepository, ITaskSeatDirectory
{
    private TaskSeatDirectory? _directory;
    private TaskSeatDirectory Directory => _directory ??= new TaskSeatDirectory(this);

    public Task<IReadOnlyList<PositionAssignment>> ActiveAsync(CancellationToken ct)
        => Directory.ActiveAsync(ct);

    public Task<IReadOnlyList<PositionAssignment>> ActiveForUserAsync(Guid userId, CancellationToken ct)
        => Directory.ActiveForUserAsync(userId, ct);

    public Task<IReadOnlyList<Guid>> PositionIdsForUserAsync(Guid userId, CancellationToken ct)
        => Directory.PositionIdsForUserAsync(userId, ct);

    public Task<bool> HoldsAnyAsync(Guid userId, IReadOnlySet<Guid> positionIds, CancellationToken ct)
        => Directory.HoldsAnyAsync(userId, positionIds, ct);

    public Task<IReadOnlyList<Guid>> HoldersOfAsync(IReadOnlySet<Guid> positionIds, CancellationToken ct)
        => Directory.HoldersOfAsync(positionIds, ct);

    public Task<IReadOnlySet<Guid>> EverAssignedUserIdsAsync(CancellationToken ct)
        => Directory.EverAssignedUserIdsAsync(ct);

    // Mirrors the tenant execution filter: another tenant's row is invisible, exactly as TenantRepository<T> makes it.
    public Task<IReadOnlyList<PositionAssignment>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PositionAssignment>>(
            seed.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted).ToList());

    public Task<PositionAssignment> CreateAsync(PositionAssignment a, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 assignments.");

    public Task<PositionAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(seed.FirstOrDefault(x => x.Id == id));

    public Task<bool> HasOverlapAsync(
        Guid positionId, DateTimeOffset from, DateTimeOffset? to, Guid? excludeId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task UpdateAsync(PositionAssignment a, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 assignments.");

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 assignments.");
}

internal sealed class FakePositionRepository(params Position[] seed) : IPositionRepository
{
    public Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(seed.FirstOrDefault(x => x.Id == id));

    // Tenant execution filter, as above.
    public Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Position>>(
            seed.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted).ToList());

    public Task<Position> CreateAsync(Position p, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 positions.");

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task UpdateAsync(Position p, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 positions.");

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 positions.");
}

internal sealed class FakeOrganizationUnitRepository(params OrganizationUnit[] seed) : IOrganizationUnitRepository
{
    public Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(seed.FirstOrDefault(x => x.Id == id));

    // Tenant execution filter, as above.
    public Task<IReadOnlyList<OrganizationUnit>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OrganizationUnit>>(
            seed.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted).ToList());

    public Task<OrganizationUnit> CreateAsync(OrganizationUnit u, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 units.");

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task UpdateAsync(OrganizationUnit u, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 units.");

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 units.");
}

internal sealed class FakeTaskWatcherRepository : ITaskWatcherRepository
{
    private readonly List<TaskWatcher> _watchers = [];

    public IReadOnlyList<TaskWatcher> Watchers => _watchers;

    public Task<TaskWatcher> CreateAsync(TaskWatcher watcher, CancellationToken ct = default)
    {
        _watchers.Add(watcher);
        return Task.FromResult(watcher);
    }

    public Task<IReadOnlyList<TaskWatcher>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskWatcher>>(
            _watchers.Where(x => x.TaskItemId == taskItemId).ToList());

    public Task<IReadOnlyList<TaskWatcher>> ListByUserIdAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskWatcher>>(_watchers.Where(x => x.UserId == userId).ToList());

    public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// In-memory dependency edges. It STORES what it is given, because the cycle check walks the graph it wrote —
/// a double that always answered "no edges" would report every A→B→A as acceptable.
/// </summary>
internal sealed class FakeTaskDependencyRepository : ITaskDependencyRepository
{
    private readonly List<TaskDependency> _edges = [];

    public FakeTaskDependencyRepository(params TaskDependency[] seed) => _edges.AddRange(seed);

    public IReadOnlyList<TaskDependency> Edges => _edges.Where(e => e.DeletedAt is null).ToList();

    public Task<TaskDependency> CreateAsync(TaskDependency d, CancellationToken ct = default)
    {
        _edges.Add(d);
        return Task.FromResult(d);
    }

    public Task<IReadOnlyList<TaskDependency>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskDependency>>(
            Edges.Where(e => e.TaskItemId == taskItemId).ToList());

    // BOTH directions, exactly like the real filter — the cycle walk depends on the far end coming back too.
    public Task<IReadOnlyList<TaskDependency>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskDependency>>(
            Edges.Where(e => taskItemIds.Contains(e.TaskItemId) || taskItemIds.Contains(e.DependsOnTaskItemId))
                .ToList());

    public Task<TaskDependency?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Edges.FirstOrDefault(e => e.Id == id));

    // Soft delete, like the base repository: a removed edge disappears from every read above.
    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var edge = _edges.FirstOrDefault(e => e.Id == id);
        if (edge is not null)
        {
            edge.DeletedAt = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory comments. It ORDERS like the real repository does (newest first, ties broken by id) — a double that
/// returned insertion order would let an ordering defect pass every test that reads the feed.
/// </summary>
internal sealed class FakeTaskCommentRepository : ITaskCommentRepository
{
    private readonly List<TaskComment> _comments = [];

    public FakeTaskCommentRepository(params TaskComment[] seed) => _comments.AddRange(seed);

    public IReadOnlyList<TaskComment> Comments => _comments;

    public Task<TaskComment> CreateAsync(TaskComment comment, CancellationToken ct = default)
    {
        _comments.Add(comment);
        return Task.FromResult(comment);
    }

    public Task<IReadOnlyList<TaskComment>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
        => Task.FromResult(Order(_comments.Where(c => c.TaskItemId == taskItemId)));

    public Task<IReadOnlyList<TaskComment>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default)
        => Task.FromResult(Order(_comments.Where(c => taskItemIds.Contains(c.TaskItemId))));

    private static IReadOnlyList<TaskComment> Order(IEnumerable<TaskComment> comments)
        => comments.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).ToList();
}

internal sealed class FakeTaskFieldDefinitionRepository : ITaskFieldDefinitionRepository
{
    private readonly List<TaskFieldDefinition> _definitions = [];

    public FakeTaskFieldDefinitionRepository(params TaskFieldDefinition[] seed) => _definitions.AddRange(seed);

    /// <summary>Everything stored, retired rows included — for assertions, never for the code under test.</summary>
    public IReadOnlyList<TaskFieldDefinition> All => _definitions;

    public void Remove(Guid id) => _definitions.RemoveAll(x => x.Id == id);

    public Task<TaskFieldDefinition> CreateAsync(TaskFieldDefinition d, CancellationToken ct = default)
    {
        _definitions.Add(d);
        return Task.FromResult(d);
    }

    public Task<TaskFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var stored = _definitions.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(stored is null ? null : Detach(stored));
    }

    public Task<TaskFieldDefinition?> GetByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(_definitions.FirstOrDefault(
            x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Mirrors the real filter: a RETIRED definition is never offered to the value validator, even if its
    /// IsActive flag says otherwise. DeletedAt is the stronger of the two statements.
    /// </summary>
    public Task<IReadOnlyList<TaskFieldDefinition>> ListActiveAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskFieldDefinition>>(
            _definitions.Where(x => x.IsActive && x.DeletedAt is null).ToList());

    /// <summary>Batched reads issued — the N+1 assertion reads this.</summary>
    public int ListAllCalls { get; private set; }

    public Task<IReadOnlyList<TaskFieldDefinition>> ListAllAsync(CancellationToken ct = default)
    {
        ListAllCalls++;
        return Task.FromResult<IReadOnlyList<TaskFieldDefinition>>(_definitions.Select(Detach).ToList());
    }

    public Task<bool> UpdateAsync(TaskFieldDefinition definition, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _definitions.FirstOrDefault(x => x.Id == definition.Id);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        stored.Version = expectedVersion + 1;
        stored.Code = definition.Code;
        stored.LabelResourceKey = definition.LabelResourceKey;
        stored.LabelText = definition.LabelText;
        stored.ValueType = definition.ValueType;
        stored.Section = definition.Section;
        stored.Importance = definition.Importance;
        stored.IsRequired = definition.IsRequired;
        stored.SortOrder = definition.SortOrder;
        stored.OptionsSourceKind = definition.OptionsSourceKind;
        stored.OptionsSourceKey = definition.OptionsSourceKey;
        stored.AppliesToModuleCode = definition.AppliesToModuleCode;
        stored.Classification = definition.Classification;
        stored.DefaultAccessState = definition.DefaultAccessState;
        stored.IsActive = definition.IsActive;
        stored.DeletedAt = definition.DeletedAt;
        return Task.FromResult(true);
    }

    /// <summary>
    /// A detached copy, like the real repository. A handler that mutates an entity and then LOSES the
    /// expected-version write must not have changed the stored state — the recurrence double learned this the
    /// hard way, twice.
    /// </summary>
    private static TaskFieldDefinition Detach(TaskFieldDefinition d) => new()
    {
        Id = d.Id,
        TenantId = d.TenantId,
        Code = d.Code,
        LabelResourceKey = d.LabelResourceKey,
        LabelText = d.LabelText,
        ValueType = d.ValueType,
        Section = d.Section,
        Importance = d.Importance,
        IsRequired = d.IsRequired,
        SortOrder = d.SortOrder,
        OptionsSourceKind = d.OptionsSourceKind,
        OptionsSourceKey = d.OptionsSourceKey,
        AppliesToModuleCode = d.AppliesToModuleCode,
        Classification = d.Classification,
        DefaultAccessState = d.DefaultAccessState,
        IsActive = d.IsActive,
        DeletedAt = d.DeletedAt,
        Version = d.Version,
        CreatedAt = d.CreatedAt
    };
}

/// <summary>
/// Display-name resolver double. Records every call so a test can prove resolution is BATCHED (one call for a
/// page of tasks, not one per user), and can play "AuthService unreachable" by resolving nothing.
/// </summary>
internal sealed class FakeUserDisplayNameResolver(params (Guid Id, string Name)[] known)
    : Diten.Platform.Application.Contracts.IUserDisplayNameResolver
{
    private readonly Dictionary<Guid, string> _known = known.ToDictionary(k => k.Id, k => k.Name);

    /// <summary>One entry per ResolveAsync invocation, holding the ids that call asked about.</summary>
    public List<IReadOnlyCollection<Guid>> Calls { get; } = [];

    /// <summary>When true the resolver returns nothing, as it does when AuthService is down.</summary>
    public bool Unavailable { get; set; }

    public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        Calls.Add(userIds.ToList());

        IReadOnlyDictionary<Guid, string> result = Unavailable
            ? new Dictionary<Guid, string>()
            : userIds.Where(_known.ContainsKey).ToDictionary(id => id, id => _known[id]);

        return Task.FromResult(result);
    }
}

/// <summary>Checklist runs, with the tenant filter and the conditional write the real repository performs.</summary>
internal sealed class FakeChecklistRunRepository(params ChecklistRun[] seed) : IChecklistRunRepository
{
    private readonly List<ChecklistRun> _runs = [.. seed];

    public IReadOnlyList<ChecklistRun> Runs => _runs;

    /// <summary>
    /// Makes the next <see cref="CreateAsync"/> throw, the way a store that is briefly unreachable does.
    ///
    /// <para>Without it, "the task survives when its checklist cannot be written" is untestable — nothing could
    /// make that write fail, so the decision would be a comment rather than a guarantee.</para>
    /// </summary>
    public bool FailNextCreate { get; set; }

    public Task<ChecklistRun> CreateAsync(ChecklistRun run, CancellationToken ct = default)
    {
        if (FailNextCreate)
        {
            FailNextCreate = false;
            throw new InvalidOperationException("Checklist run could not be stored.");
        }

        _runs.Add(run);
        return Task.FromResult(run);
    }

    public Task<ChecklistRun?> GetByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
        => Task.FromResult(_runs.FirstOrDefault(
            x => x.TaskItemId == taskItemId && x.TenantId == TaskTestData.Tenant && !x.IsDeleted));

    public Task<IReadOnlyList<ChecklistRun>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult<IReadOnlyList<ChecklistRun>>(_runs
            .Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted && taskItemIds.Contains(x.TaskItemId))
            .ToList());
    }

    /// <summary>Batch reads issued — the N+1 assertion reads this.</summary>
    public int CallCount { get; private set; }

    public Task<bool> UpdateAsync(ChecklistRun run, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _runs.FirstOrDefault(x => x.Id == run.Id && x.TenantId == TaskTestData.Tenant);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        stored.Version = expectedVersion + 1;
        stored.Items = run.Items;
        stored.Status = run.Status;
        return Task.FromResult(true);
    }
}

/// <summary>Task templates, tenant-filtered like the real repository.</summary>
internal sealed class FakeTaskTemplateRepository(params TaskTemplate[] seed) : ITaskTemplateRepository
{
    public Task<TaskTemplate> CreateAsync(TaskTemplate template, CancellationToken ct = default)
        => Task.FromResult(template);

    public Task<TaskTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(seed.FirstOrDefault(x => x.Id == id && x.TenantId == TaskTestData.Tenant));

    public Task<IReadOnlyList<TaskTemplate>> ListActiveAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskTemplate>>(
            seed.Where(x => x.TenantId == TaskTestData.Tenant && x.IsActive).ToList());
}

internal sealed class FakeChecklistTemplateRepository(params ChecklistTemplate[] seed) : IChecklistTemplateRepository
{
    /// <summary>
    /// A checklist template every instance answers for, so a caller that only needs "a template exists" does not
    /// have to build one. Seeded templates still win — passing one in overrides this entirely.
    /// </summary>
    public static readonly Guid SeededId = Guid.Parse("9c9c9c9c-9c9c-9c9c-9c9c-9c9c9c9c9c9c");

    private static readonly ChecklistTemplate Seeded = new()
    {
        Id = SeededId,
        TenantId = TaskTestData.Tenant,
        Name = "Varsayılan kontrol listesi",
        IsActive = true,
        Code = "DEFAULT-CHECKLIST",
        Items = [new ChecklistTemplateItem { Code = "step-1", LabelText = "İlk adım" }]
    };

    public Task<ChecklistTemplate> CreateAsync(ChecklistTemplate template, CancellationToken ct = default)
        => Task.FromResult(template);

    public Task<ChecklistTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(
            seed.FirstOrDefault(x => x.Id == id && x.TenantId == TaskTestData.Tenant)
            ?? (id == SeededId ? Seeded : null));

    public Task<IReadOnlyList<ChecklistTemplate>> ListActiveAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ChecklistTemplate>>(
            seed.Where(x => x.TenantId == TaskTestData.Tenant && x.IsActive).ToList());
}

/// <summary>
/// Workflow gate double. Defaults to ALLOW so Phase 1/2 tests are unaffected; a Phase 3 test flips it to blocked
/// or makes it throw, which must be treated as blocked (fail-closed).
/// </summary>
internal sealed class FakeWorkflowTransitionGate : Diten.Platform.Application.Contracts.IWorkflowTransitionGate
{
    public bool Blocked { get; set; }
    public bool Throws { get; set; }
    public List<Diten.Platform.Application.Contracts.WorkflowGateRequest> Calls { get; } = [];

    /// <summary>
    /// Block only the named object types (Faz 3b). A task can carry TWO live decisions at once — approval and
    /// review — and <see cref="Blocked"/> alone cannot tell them apart, so a test using it could not prove that
    /// blocking one leaves the other alone. Empty means "fall back to <see cref="Blocked"/>", so every existing
    /// test behaves exactly as before.
    /// </summary>
    public HashSet<string> BlockedObjectTypes { get; } = new(StringComparer.Ordinal);

    /// <summary>Object types the gate reports as REFUSED rather than merely pending.</summary>
    public HashSet<string> RejectedObjectTypes { get; } = new(StringComparer.Ordinal);

    public Task<Diten.Platform.Application.Contracts.WorkflowGateResult> EvaluateAsync(
        Diten.Platform.Application.Contracts.WorkflowGateRequest request, CancellationToken ct = default)
    {
        Calls.Add(request);

        // The REAL gate is fail-closed: an evaluation that cannot complete is a block, never an allow. The double
        // mirrors that rather than propagating the exception, which is what the production seam does.
        if (Throws)
        {
            return Task.FromResult(new Diten.Platform.Application.Contracts.WorkflowGateResult(
                IsAllowed: false, Decision: "blocked", GateStatus: "evaluationFailed",
                BlockingReasonCode: "GATE_EVALUATION_FAILED",
                BlockingMessage: "The workflow gate could not be evaluated.", CorrelationId: null));
        }

        if (RejectedObjectTypes.Contains(request.ObjectType))
        {
            return Task.FromResult(new Diten.Platform.Application.Contracts.WorkflowGateResult(
                IsAllowed: false, Decision: "blocked", GateStatus: "rejected",
                BlockingReasonCode: "WORKFLOW_REJECTED",
                BlockingMessage: "The workflow was rejected.", CorrelationId: null));
        }

        var blocked = BlockedObjectTypes.Count > 0
            ? BlockedObjectTypes.Contains(request.ObjectType)
            : Blocked;

        return Task.FromResult(new Diten.Platform.Application.Contracts.WorkflowGateResult(
            IsAllowed: !blocked,
            Decision: blocked ? "blocked" : "allowed",
            GateStatus: blocked ? "pendingApproval" : "noWorkflow",
            BlockingReasonCode: blocked ? "APPROVAL_PENDING" : null,
            BlockingMessage: blocked ? "Approval is still pending." : null,
            CorrelationId: null));
    }

    public async Task EnsureAllowedOrThrowAsync(
        Diten.Platform.Application.Contracts.WorkflowGateRequest request, CancellationToken ct = default)
    {
        var result = await EvaluateAsync(request, ct);
        if (result.IsBlocked)
        {
            throw new Diten.Platform.Application.Contracts.WorkflowTransitionBlockedException(result);
        }
    }
}

/// <summary>Approval-service double: records starts/cancels and can simulate a workflow that will not start.</summary>
/// <summary>
/// Faz 3b — the REVIEW handoff, stubbed. Deliberately separate from <see cref="FakeTaskApprovalService"/>: the
/// two record their calls independently, so a test can prove that starting a review did NOT touch approval.
/// </summary>
/// <summary>
/// WC-2 — a working-time calculator that answers in a way NO real calendar would, so a test can prove the seam
/// is load-bearing rather than decorative: if swapping this changes nothing, the interface is a comment.
///
/// <para>Every day counts as ten working days here. The number is absurd on purpose — it cannot be arrived at by
/// accident, and it is far enough from the real answer that a threshold comparison lands on the other side.</para>
/// </summary>
internal sealed class TenfoldWorkingTimeCalculator
    : Diten.Platform.Application.Features.WorkAggregation.Services.IWorkingTimeCalculator
{
    public decimal UnitsBetween(DateTimeOffset from, DateTimeOffset to) => (decimal)(to - from).TotalDays * 10m;

    public DateTimeOffset Add(DateTimeOffset from, decimal units) => from.AddDays((double)units / 10d);
}

/// <summary>
/// A stand-in SLA decision, for tests about everything EXCEPT the SLA. Records what it was asked so a test can
/// prove the provider consults it instead of doing the arithmetic itself.
/// </summary>
internal sealed class FakeWorkItemSlaCalculator
    : Diten.Platform.Application.Features.WorkAggregation.Services.IWorkItemSlaCalculator
{
    public FakeWorkItemSlaCalculator(string? answer = null) => Answer = answer;

    /// <summary>Returned for every item with a deadline. Null means "behave like the real one would on no-sla".</summary>
    public string? Answer { get; }

    public List<DateTimeOffset?> Asked { get; } = [];

    public string Resolve(DateTimeOffset? dueAt, DateTimeOffset now)
    {
        Asked.Add(dueAt);
        if (dueAt is null)
        {
            return Diten.Platform.Application.Features.WorkAggregation.WorkItemContract.SlaNoSla;
        }

        return Answer ?? Diten.Platform.Application.Features.WorkAggregation.WorkItemContract.SlaOnTrack;
    }
}

/// <summary>
/// Phase 4 — recurrence rules, stored in one list and READ THROUGH THE TENANT CONTEXT.
///
/// <para>The tenant filter is the point of this double, not decoration. The sweep runs with no user context, so
/// a read that ignored the current tenant would hand one tenant's rules to another's pass and create their work
/// in the wrong place — a leak of WORK rather than of information, and far harder to notice. Honouring
/// ITenantContext here is what lets a test prove TenantScope is load-bearing.</para>
///
/// <para>The expected-version write is real too: it is the mechanism that makes two concurrent sweeps produce
/// one task, so a double that always accepted the write would make the duplicate guard untestable.</para>
/// </summary>
internal sealed class FakeTaskRecurrenceRuleRepository : ITaskRecurrenceRuleRepository
{
    private readonly List<TaskRecurrenceRule> _rules = [];
    private readonly ITenantContext _tenantContext;

    public FakeTaskRecurrenceRuleRepository(ITenantContext tenantContext, params TaskRecurrenceRule[] seed)
    {
        _tenantContext = tenantContext;
        _rules.AddRange(seed);
    }

    /// <summary>Everything stored, across every tenant — for assertions, never for the code under test.</summary>
    public IReadOnlyList<TaskRecurrenceRule> All => _rules;

    public Task<TaskRecurrenceRule> CreateAsync(TaskRecurrenceRule rule, CancellationToken ct = default)
    {
        _rules.Add(rule);
        return Task.FromResult(rule);
    }

    public Task<TaskRecurrenceRule?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var stored = _rules.FirstOrDefault(r => r.Id == id && r.TenantId == _tenantContext.TenantId);
        return Task.FromResult(stored is null ? null : Detach(stored));
    }

    public Task<IReadOnlyList<TaskRecurrenceRule>> ListActiveAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskRecurrenceRule>>(_rules
            .Where(r => r.TenantId == _tenantContext.TenantId && r.IsActive && r.DeletedAt is null)
            .Select(Detach)
            .ToList());

    public Task<IReadOnlyList<TaskRecurrenceRule>> ListAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskRecurrenceRule>>(_rules
            .Where(r => r.TenantId == _tenantContext.TenantId)
            .Select(Detach)
            .ToList());

    public Task<bool> UpdateAsync(TaskRecurrenceRule rule, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _rules.FirstOrDefault(r => r.Id == rule.Id && r.TenantId == _tenantContext.TenantId);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        stored.Version = expectedVersion + 1;
        stored.Name = rule.Name;
        stored.Frequency = rule.Frequency;
        stored.Interval = rule.Interval;
        stored.StartsAt = rule.StartsAt;
        stored.EndsAt = rule.EndsAt;
        stored.TaskTemplateId = rule.TaskTemplateId;
        stored.AssignmentTarget = rule.AssignmentTarget;
        stored.AssigneeUserId = rule.AssigneeUserId;
        stored.PoolPositionId = rule.PoolPositionId;
        stored.OrganizationUnitId = rule.OrganizationUnitId;
        stored.IsActive = rule.IsActive;
        stored.DeletedAt = rule.DeletedAt;
        stored.LastProcessInstanceId = rule.LastProcessInstanceId;
        stored.LastGeneratedAt = rule.LastGeneratedAt;
        return Task.FromResult(true);
    }

    /// <summary>
    /// A detached copy, like the real repository: Mongo deserializes a fresh document per read, so a caller that
    /// mutates an entity and then LOSES the expected-version write must not have changed the stored state. The
    /// task repository double learned this the hard way — handing out the stored reference made a rejected write
    /// still appear to take effect.
    /// </summary>
    private static TaskRecurrenceRule Detach(TaskRecurrenceRule rule) => new()
    {
        Id = rule.Id,
        TenantId = rule.TenantId,
        Name = rule.Name,
        Frequency = rule.Frequency,
        Interval = rule.Interval,
        StartsAt = rule.StartsAt,
        EndsAt = rule.EndsAt,
        TaskTemplateId = rule.TaskTemplateId,
        // The assignment travels with the copy. It did not, briefly, and every generation test went silent:
        // the detached rule carried a Person target with no assignee, so the generator skipped it as unusable.
        AssignmentTarget = rule.AssignmentTarget,
        AssigneeUserId = rule.AssigneeUserId,
        PoolPositionId = rule.PoolPositionId,
        OrganizationUnitId = rule.OrganizationUnitId,
        IsActive = rule.IsActive,
        DeletedAt = rule.DeletedAt,
        LastProcessInstanceId = rule.LastProcessInstanceId,
        LastGeneratedAt = rule.LastGeneratedAt,
        Version = rule.Version,
        CreatedAt = rule.CreatedAt
    };
}

/// <summary>
/// WC-4 — records every notification the handlers ask for, so a test can assert WHICH event went to WHOM.
///
/// <para>Deliberately a double for the SERVICE, not for the adapter beneath it: the rules under test (opt-out,
/// actor skip, requester audience) live in the handlers' choice of arguments, and asserting on those arguments
/// is asserting on the rules rather than on plumbing.</para>
/// </summary>
internal sealed class FakeTaskNotificationService
    : Diten.Platform.Application.Features.Tasks.Services.ITaskNotificationService
{
    public sealed record Sent(Guid TaskId, string EventCode, IReadOnlyList<Guid> Candidates, Guid ActingUserId);

    public List<Sent> Notifications { get; } = [];

    /// <summary>Pool holders this double answers with, so a pooled audience can be arranged.</summary>
    public List<Guid> PoolHolders { get; } = [];

    /// <summary>Makes NotifyAsync throw, to prove a notification failure never fails the write.</summary>
    public bool Throws { get; set; }

    /// <summary>Codes actually dispatched (opt-out and actor-skip resolved), for the common assertion.</summary>
    public IReadOnlyList<string> EventCodes => Notifications.Select(n => n.EventCode).ToList();

    public Task<Diten.Platform.Application.Features.Tasks.Services.TaskNotificationOutcome> NotifyAsync(
        TaskItem task,
        string eventCode,
        IReadOnlyCollection<Guid> candidateUserIds,
        Guid actingUserId,
        CancellationToken ct)
    {
        if (Throws)
        {
            throw new InvalidOperationException("Notification layer is down.");
        }

        /*
         * The task's own settings are asked of TaskNotificationPolicy — the SAME owner production asks — rather
         * than restated here. This used to check the master switch only, while the real service had gained the
         * per-event preference too, so the comment above it ("the same two skip rules") had quietly become false
         * and thirteen handler test files were measuring a policy production no longer used. Delegating means the
         * next rule added to the policy reaches every one of those files without anyone editing this method.
         *
         * A double that recorded every call regardless would be worse still: every "no notification" assertion
         * would pass for the wrong reason.
         */
        if (!Diten.Platform.Application.Features.Tasks.Services.TaskNotificationPolicy.WouldNotify(task, eventCode))
        {
            return Task.FromResult(
                Diten.Platform.Application.Features.Tasks.Services.TaskNotificationOutcome.Skipped);
        }

        var audience = (candidateUserIds ?? [])
            .Where(id => id != Guid.Empty && id != actingUserId)
            .Distinct()
            .ToList();

        if (audience.Count == 0)
        {
            return Task.FromResult(
                Diten.Platform.Application.Features.Tasks.Services.TaskNotificationOutcome.Skipped);
        }

        Notifications.Add(new Sent(task.Id, eventCode, audience, actingUserId));
        return Task.FromResult(
            Diten.Platform.Application.Features.Tasks.Services.TaskNotificationOutcome.Dispatched);
    }

    public Task<IReadOnlyList<Guid>> ResolvePoolHoldersAsync(TaskItem task, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Guid>>(PoolHolders);
}

/// <summary>
/// WC-4 — a recipient resolver a test controls. The seam proof rests on this: swapping what it answers must
/// change what is dispatched, or the interface is decoration.
/// </summary>
internal sealed class FakeTaskNotificationRecipientResolver
    : Diten.Platform.Application.Contracts.ITaskNotificationRecipientResolver
{
    private readonly Dictionary<Guid, string> _addresses = [];

    public FakeTaskNotificationRecipientResolver(params (Guid Id, string Email)[] known)
    {
        foreach (var entry in known)
        {
            _addresses[entry.Id] = entry.Email;
        }
    }

    /// <summary>Ids this resolver was ASKED about — the "unresolvable is skipped" assertion reads it.</summary>
    public List<Guid> Asked { get; } = [];

    public Task<IReadOnlyList<Diten.Platform.Application.Contracts.TaskNotificationRecipient>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default)
    {
        Asked.AddRange(userIds);

        // Unknown ids are OMITTED, exactly as the AuthService client omits a user with no address.
        return Task.FromResult<IReadOnlyList<Diten.Platform.Application.Contracts.TaskNotificationRecipient>>(
            userIds
                .Where(_addresses.ContainsKey)
                .Select(id => new Diten.Platform.Application.Contracts.TaskNotificationRecipient(
                    id, _addresses[id], null))
                .ToList());
    }
}

/// <summary>Records what reached the notification layer — the end of the WC-4 chain.</summary>
internal sealed class RecordingNotificationDispatchAdapter
    : Diten.Platform.Application.Features.Notifications.Services.INotificationEventDispatchAdapter
{
    public List<Diten.Platform.Application.Features.Notifications.Services.NotificationEventDispatchRequest> Requests { get; } = [];

    /// <summary>Makes the notification layer refuse, so "a refusal does not fail the write" can be proved.</summary>
    public string? FailWithReasonCode { get; set; }

    /// <summary>Makes the layer THROW rather than refuse — a transport that died, not one that said no.</summary>
    public bool ThrowOnDispatch { get; set; }

    public Task<Diten.Platform.Application.Common.Response<Diten.Platform.Application.Features.Notifications.NotificationDispatchDto>>
        DispatchByEventCodeAsync(
            Diten.Platform.Application.Features.Notifications.Services.NotificationEventDispatchRequest request,
            CancellationToken ct = default)
    {
        Requests.Add(request);

        if (ThrowOnDispatch)
        {
            throw new InvalidOperationException("The mail transport is down.");
        }

        return Task.FromResult(FailWithReasonCode is null
            ? Diten.Platform.Application.Common.Response<
                Diten.Platform.Application.Features.Notifications.NotificationDispatchDto>.Success(202)
            : Diten.Platform.Application.Common.Response<
                Diten.Platform.Application.Features.Notifications.NotificationDispatchDto>.Fail(
                    "refused", 409, FailWithReasonCode));
    }
}

internal sealed class FakeTaskReviewService : Diten.Platform.Application.Features.Tasks.Services.ITaskReviewService
{
    public bool CannotStart { get; set; }
    public List<Guid> Started { get; } = [];
    public List<Guid> Cancelled { get; } = [];

    /// <summary>What the NEXT start returns. A second round after a refusal hands back a different id.</summary>
    public Guid InstanceId { get; set; } = Guid.Parse("beefbeef-beef-beef-beef-beefbeefbeef");

    public Task<Guid?> TryStartReviewAsync(TaskItem task, CancellationToken ct)
    {
        Started.Add(task.Id);
        return Task.FromResult<Guid?>(CannotStart ? null : InstanceId);
    }

    public Task CancelReviewAsync(TaskItem task, CancellationToken ct)
    {
        Cancelled.Add(task.Id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeTaskApprovalService : Diten.Platform.Application.Features.Tasks.Services.ITaskApprovalService
{
    /// <summary>Instance id → state, as MOD-0023 would report it. Seeded per test.</summary>
    public Dictionary<Guid, Diten.Platform.Application.Features.Tasks.Services.TaskApprovalState> States { get; } = [];

    /// <summary>Batched reads issued — the N+1 assertion reads this.</summary>
    public List<int> StateReadSizes { get; } = [];

    public bool CannotStart { get; set; }
    public List<Guid> Started { get; } = [];
    public List<Guid> Cancelled { get; } = [];
    public Guid InstanceId { get; } = Guid.Parse("abcdabcd-abcd-abcd-abcd-abcdabcdabcd");

    public Task<Guid?> TryStartApprovalAsync(TaskItem task, CancellationToken ct)
    {
        Started.Add(task.Id);
        return Task.FromResult<Guid?>(CannotStart ? null : InstanceId);
    }

    public Task CancelApprovalAsync(TaskItem task, CancellationToken ct)
    {
        Cancelled.Add(task.Id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<Guid, Diten.Platform.Application.Features.Tasks.Services.TaskApprovalState>>
        GetStatesAsync(IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct)
    {
        StateReadSizes.Add(workflowInstanceIds.Count);
        return Task.FromResult<IReadOnlyDictionary<Guid, Diten.Platform.Application.Features.Tasks.Services.TaskApprovalState>>(
            States.Where(kv => workflowInstanceIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    /// <summary>Mark an instance as approved, the way MOD-0023 reports it after a decision.</summary>
    public FakeTaskApprovalService Approved(Guid instanceId)
    {
        States[instanceId] = new Diten.Platform.Application.Features.Tasks.Services.TaskApprovalState(
            IsPending: false, IsApproved: true, IsRejected: false);
        return this;
    }

    /// <summary>Mark an instance as REFUSED — the one approval outcome that lands in MOD-0024's lifecycle.</summary>
    public FakeTaskApprovalService Rejected(Guid instanceId)
    {
        States[instanceId] = new Diten.Platform.Application.Features.Tasks.Services.TaskApprovalState(
            IsPending: false, IsApproved: false, IsRejected: true);
        return this;
    }

    public FakeTaskApprovalService Pending(Guid instanceId)
    {
        States[instanceId] = new Diten.Platform.Application.Features.Tasks.Services.TaskApprovalState(
            IsPending: true, IsApproved: false, IsRejected: false);
        return this;
    }
}

/// <summary>Routes the one command under test to the real handler; anything else is a mistake, loudly.</summary>
/// <summary>
/// Routes the commands under test to their REAL handlers, so the command a CONTROLLER builds is the one the
/// handler receives — the URL→command mapping is part of what the endpoint tests are for. Anything unexpected
/// throws rather than being quietly swallowed.
/// </summary>
internal sealed class DirectMediator : IMediator
{
    private readonly TransitionTaskItemHandler? _transition;
    private readonly AddTaskCommentHandler? _comment;
    private readonly PlanTaskItemHandler? _plan;

    public DirectMediator(TransitionTaskItemHandler handler) => _transition = handler;

    public DirectMediator(AddTaskCommentHandler handler) => _comment = handler;

    public DirectMediator(PlanTaskItemHandler handler) => _plan = handler;

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        => request switch
        {
            TransitionTaskItemCommand command when _transition is not null
                => (Task<TResponse>)(object)_transition.Handle(command, ct),
            AddTaskCommentCommand command when _comment is not null
                => (Task<TResponse>)(object)_comment.Handle(command, ct),
            PlanTaskItemCommand command when _plan is not null
                => (Task<TResponse>)(object)_plan.Handle(command, ct),
            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
        };

    public Task<object?> Send(object request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
        => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : INotification
        => Task.CompletedTask;
}

internal sealed class PassingWorkflowGate : IWorkflowTransitionGate
{
    public Task<WorkflowGateResult> EvaluateAsync(WorkflowGateRequest request, CancellationToken ct = default)
        => Task.FromResult(new WorkflowGateResult(
            IsAllowed: true,
            Decision: "allowed",
            GateStatus: "notRequired",
            BlockingReasonCode: null,
            BlockingMessage: null,
            CorrelationId: null));

    public Task EnsureAllowedOrThrowAsync(WorkflowGateRequest request, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>
/// WC-2 — the REAL SLA decision, wired the way production wires it (24/7 working time, the default window).
/// Existing provider harnesses take this rather than a stub, so the seam is exercised by the whole suite.
/// </summary>
internal static class SlaForTests
{
    public static Diten.Platform.Application.Features.WorkAggregation.Services.IWorkItemSlaCalculator Real(
        decimal dueSoonWithinWorkingDays = 2m)
        => new Diten.Platform.Application.Features.WorkAggregation.Services.WorkItemSlaCalculator(
            new Diten.Platform.Application.Features.WorkAggregation.Services.TwentyFourSevenWorkingTimeCalculator(),
            Microsoft.Extensions.Options.Options.Create(
                new Diten.Platform.Application.Features.WorkAggregation.Services.WorkItemSlaOptions
                {
                    DueSoonWithinWorkingDays = dueSoonWithinWorkingDays
                }));

    /// <summary>The same decision over an ARBITRARY working-time implementation — the seam-is-real proof.</summary>
    public static Diten.Platform.Application.Features.WorkAggregation.Services.IWorkItemSlaCalculator Over(
        Diten.Platform.Application.Features.WorkAggregation.Services.IWorkingTimeCalculator workingTime,
        decimal dueSoonWithinWorkingDays = 2m)
        => new Diten.Platform.Application.Features.WorkAggregation.Services.WorkItemSlaCalculator(
            workingTime,
            Microsoft.Extensions.Options.Options.Create(
                new Diten.Platform.Application.Features.WorkAggregation.Services.WorkItemSlaOptions
                {
                    DueSoonWithinWorkingDays = dueSoonWithinWorkingDays
                }));
}

/// <summary>
/// Answers with the tenant's language without touching a tenant registry. The real chain (caller → tenant runtime
/// language → profile default → "en") is proved by TenantNotificationLocaleResolver's own tests and end to end by
/// TaskNotificationLocaleTests; here it would only be scenery.
/// </summary>
internal sealed class FakeNotificationLocaleResolver(string locale = "en")
    : Diten.Platform.Application.Features.Notifications.Services.INotificationLocaleResolver
{
    public Task<string> ResolveAsync(Guid tenantId, string? requested, CancellationToken ct = default)
        => Task.FromResult(string.IsNullOrWhiteSpace(requested) ? locale : requested.Trim().ToLowerInvariant());
}

/*
 * ── Module-record sources ────────────────────────────────────────────────────────────────────────────────────
 *
 * A source with a fixed set of rows, used to prove the CONTRACT rather than any particular module's query. The
 * real OrganizationUnit and Position sources have their own tests; here the point is that a caller can hold two
 * different sources and not be able to tell them apart.
 */
internal sealed class FakeTaskRecordSource(
    string sourceKey,
    params Diten.Platform.Application.Features.Tasks.Services.TaskRecordDto[] records)
    : Diten.Platform.Application.Features.Tasks.Services.ITaskRecordSource
{
    private readonly List<Diten.Platform.Application.Features.Tasks.Services.TaskRecordDto> _records = [.. records];

    /// <summary>Every term this source was searched with, so a test can prove the search reached the source.</summary>
    internal List<string?> SearchTerms { get; } = [];

    public string SourceKey => sourceKey;
    public string ModuleCode => "test";
    public string LabelResourceKey => $"recordSource_{sourceKey}";

    public Task<IReadOnlyList<Diten.Platform.Application.Features.Tasks.Services.TaskRecordDto>> SearchAsync(
        string? term, int take, CancellationToken ct)
    {
        SearchTerms.Add(term);
        IReadOnlyList<Diten.Platform.Application.Features.Tasks.Services.TaskRecordDto> hits = _records
            .Where(record => string.IsNullOrWhiteSpace(term)
                             || record.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                             || record.Code.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(take)
            .ToList();
        return Task.FromResult(hits);
    }

    public Task<IReadOnlyList<Diten.Platform.Application.Features.Tasks.Services.TaskRecordDto>> ResolveAsync(
        IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        IReadOnlyList<Diten.Platform.Application.Features.Tasks.Services.TaskRecordDto> hits = _records
            .Where(record => ids.Contains(record.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(hits);
    }
}

internal static class TaskRecordSourceDoubles
{
    /// <summary>No module offers records. The default for every suite that is not about record fields.</summary>
    internal static Diten.Platform.Application.Features.Tasks.Services.ITaskRecordSourceRegistry None
        => new Diten.Platform.Application.Features.Tasks.Services.TaskRecordSourceRegistry([]);

    internal static Diten.Platform.Application.Features.Tasks.Services.ITaskRecordSourceRegistry With(
        params Diten.Platform.Application.Features.Tasks.Services.ITaskRecordSource[] sources)
        => new Diten.Platform.Application.Features.Tasks.Services.TaskRecordSourceRegistry(sources);
}

/// <summary>
/// BL-023 — builds a <see cref="Diten.Platform.Application.Features.Tasks.Providers.TaskWorkItemProvider"/> wired
/// for the SCOPE tests: real org data, a real scope resolver and a real team resolver behind it.
///
/// <para>It exists because the provider takes fourteen collaborators and the scope tests care about exactly
/// three of them; spelling the other eleven out per test would bury the one thing being asserted.</para>
/// </summary>
internal static class TaskWorkItemProviderHarness
{
    internal static Diten.Platform.Application.Features.Tasks.Providers.TaskWorkItemProvider Create(
        Position[] positions,
        OrganizationUnit[] units,
        PositionAssignment[] assignments,
        TaskItem[] tasks,
        EntitlementDataScope[]? scopes = null)
    {
        var scopeResolver = new Diten.Platform.Application.Features.Tasks.Services.TaskAssignmentScopeResolver(
            new FakeDataScopeResolver(scopes ?? DefaultScopes(positions, units)),
            new FakePositionRepository(positions),
            new FakeOrganizationUnitRepository(units),
            new FakeTenantContext(TaskTestData.Tenant),
            new FakeCurrentUserContext(TaskTestData.Me));

        return new Diten.Platform.Application.Features.Tasks.Providers.TaskWorkItemProvider(
            new FakeTaskItemRepository(tasks),
            new FakePositionAssignmentRepository(assignments),
            new Diten.Platform.Application.Features.Tasks.Services.TaskLifecycleService(),
            new Diten.Platform.Application.Features.Tasks.Services.TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(),
            new FakePositionRepository(positions),
            new FakeOrganizationUnitRepository(units),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(),
            new Diten.Platform.Application.Features.Tasks.Services.TaskTeamResolver(
                scopeResolver, new FakePositionAssignmentRepository(assignments)));
    }

    /// <summary>The actor's own position plus their unit and legal entity — what MOD-0018-FU15 would emit.</summary>
    private static EntitlementDataScope[] DefaultScopes(Position[] positions, OrganizationUnit[] units)
    {
        var mine = positions.FirstOrDefault(p => p.Name == "Boss");
        var unit = mine is null ? null : units.FirstOrDefault(u => u.Id == mine.OrganizationUnitId);
        if (mine is null || unit is null) { return []; }

        return
        [
            new EntitlementDataScope(EntitlementDataScopeKind.Position, mine.Id, mine.Code),
            new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, unit.Id, unit.Code),
            new EntitlementDataScope(EntitlementDataScopeKind.LegalEntity, unit.LegalEntityId, "LE")
        ];
    }
}
