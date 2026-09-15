using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-PSS-MOD0024-TASK-MENTIONS-01 — @mentions in task comments, end to end against the REAL
/// <see cref="TaskReadAccessPolicy"/> (not a double): K1 (no new permission), K2 (candidate = the read-access
/// data legs), K3 (self-mention and edit-diff notification rules), K4 (the 10-person cap).
/// </summary>
public sealed class TaskMentionTests
{
    private static readonly Guid Me = TaskTestData.Me;
    private static readonly Guid Rival = TaskTestData.Rival;
    private static readonly Guid Other = TaskTestData.Other;
    private static readonly Guid Watcher = TaskTestData.Watcher;

    // ── K2: who may be mentioned ────────────────────────────────────────

    [Fact]
    public async Task Mentioning_a_watcher_who_can_see_the_task_succeeds_and_sends_exactly_one_mention_notification()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
        h.Watchers.CreateAsync(
            new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, UserId = Watcher },
            CancellationToken.None).GetAwaiter().GetResult();

        var result = await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("bak @Watcher", [Watcher]), "corr"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var mentionSent = Assert.Single(h.Notifications.Notifications, n => n.EventCode == TaskNotificationEvents.Mentioned);
        Assert.Equal([Watcher], mentionSent.Candidates);
        // The general "somebody commented" audience excludes the person who was just mentioned — one comment,
        // one email for that reader, not two.
        var commentedSent = Assert.Single(h.Notifications.Notifications, n => n.EventCode == TaskNotificationEvents.Commented);
        Assert.DoesNotContain(Watcher, commentedSent.Candidates);
    }

    [Fact]
    public async Task Mentioning_somebody_who_cannot_see_the_task_is_refused_with_a_reason_code_and_sends_nothing()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();

        // Rival has no relationship to this task at all: not the assignee, not the creator, not a watcher, not a
        // pool holder, no scope, no ReadAll.
        var result = await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("bak @Rival", [Rival]), "corr"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.MentionNotVisible, result.ReasonCode);
        Assert.Empty(h.Comments.Comments);
        Assert.Empty(h.Notifications.Notifications);
    }

    [Fact]
    public async Task Mentioning_yourself_is_allowed_and_sends_zero_notifications()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Me, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();

        var result = await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("not to self @Me", [Me]), "corr"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        // ITaskNotificationService excludes the acting user from any audience — the same rule every other event
        // in this module already relies on, not re-implemented at this call site.
        Assert.DoesNotContain(h.Notifications.Notifications, n => n.EventCode == TaskNotificationEvents.Mentioned);
    }

    [Fact]
    public async Task An_unrelated_user_who_happens_to_hold_a_position_in_another_tenants_data_is_refused_the_same_way()
    {
        // There is no separate "cross-tenant" reason code (K1 — no new rule, no new permission): a foreign
        // tenant's user is simply absent from every one of this task's data legs, so it fails the identical
        // MentionNotVisible check an unrelated same-tenant user fails.
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
        var foreignTenantUser = Guid.NewGuid();

        var result = await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("bak", [foreignTenantUser]), "corr"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.MentionNotVisible, result.ReasonCode);
    }

    [Fact]
    public async Task Eleven_mentions_are_refused_before_anyone_is_checked_for_visibility()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
        var eleven = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList();

        var result = await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("çok kişi", eleven), "corr"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.MentionLimitExceeded, result.ReasonCode);
        Assert.Empty(h.Comments.Comments);
    }

    [Fact]
    public async Task Exactly_ten_mentions_is_allowed()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
        // Ten watchers, all independently visible.
        var ten = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in ten)
        {
            h.Watchers.CreateAsync(
                new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, UserId = id },
                CancellationToken.None).GetAwaiter().GetResult();
        }

        var result = await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("on kişi", ten), "corr"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    // ── K3: editing adds a new mention → only the new person is told ────

    [Fact]
    public async Task Editing_a_comment_to_add_a_new_mention_notifies_only_the_newly_added_person()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
        h.Watchers.CreateAsync(
            new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, UserId = Watcher },
            CancellationToken.None).GetAwaiter().GetResult();

        var posted = await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("ilk hali", [Watcher]), "corr"),
            CancellationToken.None);
        h.Notifications.Notifications.Clear();

        // Rival must independently pass K2 too — made the assignee here so the edit is not ALSO testing K2.
        task.AssigneeUserId = Rival;
        var edited = await h.UpdateHandler().Handle(
            new UpdateTaskCommentCommand(
                task.Id, posted.Data, new UpdateTaskCommentRequest("yeni hali", [Watcher, Rival]), "corr"),
            CancellationToken.None);

        Assert.True(edited.IsSuccessful);
        var mentionSent = Assert.Single(h.Notifications.Notifications, n => n.EventCode == TaskNotificationEvents.Mentioned);
        Assert.Equal([Rival], mentionSent.Candidates);
    }

    [Fact]
    public async Task Editing_a_comment_without_changing_its_mentions_sends_nothing()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
        h.Watchers.CreateAsync(
            new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, UserId = Watcher },
            CancellationToken.None).GetAwaiter().GetResult();
        var posted = await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("ilk hali", [Watcher]), "corr"),
            CancellationToken.None);
        h.Notifications.Notifications.Clear();

        await h.UpdateHandler().Handle(
            new UpdateTaskCommentCommand(
                task.Id, posted.Data, new UpdateTaskCommentRequest("sadece yazım düzeltmesi", [Watcher]), "corr"),
            CancellationToken.None);

        Assert.Empty(h.Notifications.Notifications);
    }

    [Fact]
    public async Task Editing_to_mention_somebody_invisible_is_refused_and_the_stored_mentions_are_unchanged()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
        var posted = await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("ilk hali", null), "corr"),
            CancellationToken.None);

        var edited = await h.UpdateHandler().Handle(
            new UpdateTaskCommentCommand(
                task.Id, posted.Data, new UpdateTaskCommentRequest("yeni hali", [Rival]), "corr"),
            CancellationToken.None);

        Assert.False(edited.IsSuccessful);
        Assert.Equal(TaskReasonCodes.MentionNotVisible, edited.ReasonCode);
        Assert.Empty(Assert.Single(h.Comments.Comments).MentionedUserIds);
        // The TEXT is unchanged too — a refused write must refuse the WHOLE write, not just the mentions half.
        Assert.Equal("ilk hali", Assert.Single(h.Comments.Comments).Text);
    }

    // ── K2: candidates endpoint ──────────────────────────────────────────

    [Fact]
    public async Task The_candidate_list_offers_the_assignee_pool_holders_creator_watcher_and_parent_holder()
    {
        var h = new Harness();
        var parent = h.NewTask(assignee: Watcher, createdBy: Guid.NewGuid());
        h.Tasks.CreateAsync(parent, CancellationToken.None).GetAwaiter().GetResult();
        var task = h.NewTask(assignee: Other, createdBy: Rival, parentId: parent.Id);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
        h.Notifications.PoolHoldersByTaskId[task.Id] = [Me];
        var thirdWatcher = Guid.NewGuid();
        h.Watchers.CreateAsync(
            new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, UserId = thirdWatcher },
            CancellationToken.None).GetAwaiter().GetResult();
        h.Names.Known[Other] = "Ayşe Other";
        h.Names.Known[Rival] = "Rıza Rival";
        h.Names.Known[Me] = "Ben Me";
        h.Names.Known[Watcher] = "Nöbetçi Watcher";
        h.Names.Known[thirdWatcher] = "Üçüncü Watcher";
        // Me can read the task via the pool leg, so the candidate query itself is authorized.

        var result = await h.CandidatesHandler().Handle(
            new GetTaskMentionCandidatesQuery(task.Id, null, "corr"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var ids = result.Data!.Select(c => c.Id).ToHashSet();
        Assert.Contains(Other, ids); // assignee
        Assert.Contains(Rival, ids); // creator
        Assert.Contains(Me, ids); // pool holder
        Assert.Contains(thirdWatcher, ids); // watcher
        Assert.Contains(Watcher, ids); // parent's assignee
    }

    [Fact]
    public async Task An_unrelated_caller_gets_the_same_not_found_a_missing_task_would()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();

        var deniedReal = await h.CandidatesHandler(caller: Rival).Handle(
            new GetTaskMentionCandidatesQuery(task.Id, null, "corr"), CancellationToken.None);
        var missing = await h.CandidatesHandler(caller: Rival).Handle(
            new GetTaskMentionCandidatesQuery(Guid.NewGuid(), null, "corr"), CancellationToken.None);

        Assert.False(deniedReal.IsSuccessful);
        Assert.Equal(404, deniedReal.StatusCode);
        Assert.Equal(TaskReasonCodes.NotFound, deniedReal.ReasonCode);
        Assert.Equal(missing.StatusCode, deniedReal.StatusCode);
        Assert.Equal(missing.ReasonCode, deniedReal.ReasonCode);
    }

    [Fact]
    public async Task An_id_whose_name_cannot_be_resolved_is_omitted_never_shown_as_a_raw_GUID()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
        // Deliberately NOT registering a name for Other in h.Names.

        var result = await h.CandidatesHandler(caller: Other).Handle(
            new GetTaskMentionCandidatesQuery(task.Id, null, "corr"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task The_search_text_filters_by_display_name_case_insensitively()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Rival);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
        h.Names.Known[Other] = "Ayşe Yılmaz";
        h.Names.Known[Rival] = "Rıza Kaya";

        var result = await h.CandidatesHandler(caller: Other).Handle(
            new GetTaskMentionCandidatesQuery(task.Id, "yılmaz", "corr"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal([Other], result.Data!.Select(c => c.Id));
    }

    // ── K1: no new permission is required ────────────────────────────────

    [Fact]
    public void The_comment_and_candidate_endpoints_are_guarded_by_the_existing_READ_permission_only()
    {
        var controller = File.ReadAllText(Path.Combine(
            RepoPaths.Root(), "services/Diten.Platform/src/Diten.Platform.Api/Controllers/TasksController.cs"));

        var start = controller.IndexOf("mention-candidates", StringComparison.Ordinal);
        Assert.True(start >= 0, "mention-candidates route not found in TasksController.");
        var window = controller[start..Math.Min(controller.Length, start + 200)];
        Assert.Contains("HasPermission(TaskPermissions.Read)", window, StringComparison.Ordinal);
        Assert.DoesNotContain("platform.tasks.mention", controller, StringComparison.Ordinal);
    }

    // ── sabotage guards: each reverses a specific rule and pins the break ─

    [Fact]
    public async Task Sabotage_removing_the_K2_check_would_let_an_unrelated_person_be_mentioned()
    {
        // This test asserts the CURRENT (correct) behaviour; it exists to be the one that goes red if somebody
        // deletes the ValidateAsync call in AddTaskCommentHandler.
        var h = new Harness();
        var task = h.NewTask(assignee: Other, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();

        var result = await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("x", [Rival]), "corr"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task Sabotage_removing_the_self_exclusion_would_email_the_author_their_own_mention()
    {
        var h = new Harness();
        var task = h.NewTask(assignee: Me, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();

        await h.AddHandler().Handle(
            new AddTaskCommentCommand(task.Id, new AddTaskCommentRequest("x", [Me]), "corr"),
            CancellationToken.None);

        Assert.DoesNotContain(h.Notifications.Notifications, n => n.EventCode == TaskNotificationEvents.Mentioned);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private sealed class Harness
    {
        public FakeTaskItemRepository Tasks { get; } = new();
        public FakeTaskWatcherRepository Watchers { get; } = new();
        public FakeTaskCommentRepository Comments { get; } = new();
        public FakeTaskNotificationService Notifications { get; } = new();
        public FakeOrganizationUnitRepository OrganizationUnits { get; } = new();
        public FakeUserDisplayNameResolverMutable Names { get; } = new();

        public TaskItem NewTask(
            Guid? assignee = null, Guid? createdBy = null, Guid? parentId = null) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TaskTestData.Tenant,
            Title = "Mention probe",
            Lifecycle = TaskLifecycle.Open,
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = assignee,
            CreatedByUserId = createdBy,
            ParentTaskItemId = parentId,
            OrganizationUnitId = Guid.NewGuid(),
            CreatedBy = "tester"
        };

        private ITaskReadAccessPolicy ReadAccess(Guid caller) => new TaskReadAccessPolicy(
            Tasks, Watchers, Notifications, OrganizationUnits,
            new EmptyScopeResolver(), TaskActors.None(),
            new FakeCurrentUserContext(caller));

        public AddTaskCommentHandler AddHandler(Guid? caller = null) => new(
            Tasks, Comments, new FakeCurrentUserContext(caller ?? Me), Names, new FakeTenantContext(TaskTestData.Tenant),
            Watchers, Notifications, ReadAccess(caller ?? Me), NullLogger<AddTaskCommentHandler>.Instance);

        public UpdateTaskCommentHandler UpdateHandler(Guid? caller = null) => new(
            Tasks, Comments, new FakeCurrentUserContext(caller ?? Me), ReadAccess(caller ?? Me), Notifications,
            NullLogger<UpdateTaskCommentHandler>.Instance);

        public GetTaskMentionCandidatesHandler CandidatesHandler(Guid? caller = null) => new(
            Tasks, ReadAccess(caller ?? Me), Names, new FakeCurrentUserContext(caller ?? Me));
    }

    private sealed class EmptyScopeResolver : ITaskAssignmentScopeResolver
    {
        public Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct) => Task.FromResult(TaskAssignmentScope.Empty);
    }

    /// <summary>A display-name double whose known set can be populated after construction.</summary>
    private sealed class FakeUserDisplayNameResolverMutable : IUserDisplayNameResolver
    {
        public Dictionary<Guid, string> Known { get; } = [];

        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                userIds.Where(Known.ContainsKey).ToDictionary(id => id, id => Known[id]));
    }
}
