using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Comments can be rewritten and withdrawn — WITH A TRAIL (owner decision, 2026-08-14).
///
/// <para>This REVERSES a written decision, and the reversal is the reason the tests are worth reading. Comments
/// were immutable and both the controller and the entity said so: "There is deliberately no PUT and no DELETE."
/// That reasoning was sound — changing a sentence somebody has already replied to can make their reply nonsense.
/// What changed is that the compromise was found: an edit that SAYS it was edited and a withdrawal that leaves a
/// marker keep the property immutability was protecting, which was "nothing changes or disappears silently".</para>
///
/// <para>So these tests pin the TRAIL, not the mutation: the stamp, the tombstone, the author-only rule, and the
/// fact that neither write emails anybody.</para>
/// </summary>
public sealed class TaskCommentTrailTests
{
    // ── The edit ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_edited_comment_keeps_its_new_text_and_says_it_was_edited()
    {
        var fixture = new Fixture();
        var id = await fixture.PostAsync("ilk hali");

        var edited = await fixture.EditAsync(id, "düzeltilmiş hali");

        Assert.Equal(204, StatusOf(edited));
        var entry = Assert.Single((await fixture.ProjectAsync()).Activity!);
        Assert.Equal("düzeltilmiş hali", entry.Text);
        Assert.NotNull(entry.EditedAt);
    }

    [Fact]
    public async Task A_comment_nobody_edited_carries_no_mark()
    {
        var fixture = new Fixture();
        await fixture.PostAsync("hiç dokunulmadı");

        Assert.Null(Assert.Single((await fixture.ProjectAsync()).Activity!).EditedAt);
    }

    [Fact]
    public async Task An_edit_never_moves_the_original_instant()
    {
        // The feed is ordered by WHEN IT WAS SAID. An edit that bumped `at` would jump a months-old comment to
        // the top of a conversation, which is a different sentence in a different place.
        var fixture = new Fixture();
        var id = await fixture.PostAsync("ilk");
        var before = Assert.Single((await fixture.ProjectAsync()).Activity!).At;

        await fixture.EditAsync(id, "sonra");

        Assert.Equal(before, Assert.Single((await fixture.ProjectAsync()).Activity!).At);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_edit_to_nothing_is_refused(string text)
    {
        var fixture = new Fixture();
        var id = await fixture.PostAsync("bir şey");

        Assert.Equal(400, StatusOf(await fixture.EditAsync(id, text)));
    }

    // ── The withdrawal ───────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (tombstone). A withdrawal must leave the ROW: a comment that vanished entirely would
    /// renumber a conversation other people quoted, and "somebody said something here and took it back" is
    /// itself information.
    /// </summary>
    [Fact]
    public async Task A_withdrawn_comment_leaves_its_row_and_loses_its_words()
    {
        var fixture = new Fixture();
        var id = await fixture.PostAsync("geri alınacak");

        Assert.Equal(204, StatusOf(await fixture.WithdrawAsync(id)));

        var entry = Assert.Single((await fixture.ProjectAsync()).Activity!);
        Assert.NotNull(entry.WithdrawnAt);
        Assert.True(string.IsNullOrEmpty(entry.Text), "the withdrawn text is still on the wire");
    }

    /// <summary>
    /// The words are gone AT REST, not merely withheld by the projection. "I deleted that" has to be true in the
    /// database too — a withdrawn sentence still sitting in storage is one query away from being read back.
    /// </summary>
    [Fact]
    public async Task A_withdrawal_clears_the_stored_text_too()
    {
        var fixture = new Fixture();
        var id = await fixture.PostAsync("saklanmayacak");

        await fixture.WithdrawAsync(id);

        var stored = Assert.Single(fixture.Comments.Comments);
        Assert.True(string.IsNullOrEmpty(stored.Text), "the withdrawn text is still in storage");
        Assert.NotNull(stored.WithdrawnAt);
    }

    [Fact]
    public async Task Withdrawing_twice_is_refused()
    {
        var fixture = new Fixture();
        var id = await fixture.PostAsync("bir kez");
        await fixture.WithdrawAsync(id);

        Assert.Equal(409, StatusOf(await fixture.WithdrawAsync(id)));
    }

    [Fact]
    public async Task A_withdrawn_comment_can_no_longer_be_edited()
    {
        var fixture = new Fixture();
        var id = await fixture.PostAsync("geri alındı");
        await fixture.WithdrawAsync(id);

        Assert.Equal(409, StatusOf(await fixture.EditAsync(id, "geri getirmeye çalış")));
    }

    // ── Only the author ──────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (authority). No manager exception and no administrator override — nobody asked for one,
    /// and an authority over other people's words is far easier to grant than to take back.
    ///
    /// <para>403, not 404, and the difference from the personal-note rule is deliberate: a note's very existence
    /// is private, so "not yours" would leak it. A comment is already on screen for everyone who can read the
    /// task, so denying its existence would be a confusing lie rather than a protective one.</para>
    /// </summary>
    [Fact]
    public async Task Somebody_elses_comment_cannot_be_edited()
    {
        var fixture = new Fixture();
        var id = await fixture.PostAsync("benim cümlem");

        var asRival = new Fixture(actingAs: TaskTestData.Rival, sharing: fixture);
        var refused = await asRival.EditAsync(id, "senin cümlen");

        Assert.Equal(403, StatusOf(refused));
        Assert.Equal("benim cümlem", Assert.Single(fixture.Comments.Comments).Text);
    }

    [Fact]
    public async Task Somebody_elses_comment_cannot_be_withdrawn()
    {
        var fixture = new Fixture();
        var id = await fixture.PostAsync("benim cümlem");

        var asRival = new Fixture(actingAs: TaskTestData.Rival, sharing: fixture);

        Assert.Equal(403, StatusOf(await asRival.WithdrawAsync(id)));
        Assert.Equal("benim cümlem", Assert.Single(fixture.Comments.Comments).Text);
    }

    /// <summary>
    /// The task id in the route is LOAD-BEARING. Without this check it would be decorative, and a caller could
    /// act on any comment in the tenant by pairing its id with a task they happen to see.
    /// </summary>
    [Fact]
    public async Task A_comment_id_paired_with_the_wrong_task_answers_404()
    {
        var fixture = new Fixture();
        var id = await fixture.PostAsync("başka görevin yorumu");

        Assert.Equal(404, StatusOf(await fixture.EditAsync(id, "yeni", taskId: Guid.NewGuid())));
    }

    // ── Who may act, decided on the SERVER ───────────────────────────────────

    [Fact]
    public async Task The_projection_offers_the_controls_to_the_author_only()
    {
        var fixture = new Fixture();
        await fixture.PostAsync("benim");

        Assert.True(Assert.Single((await fixture.ProjectAsync()).Activity!).Editable);
        Assert.False(Assert.Single((await fixture.ProjectAsync(TaskTestData.Rival)).Activity!).Editable);
    }

    [Fact]
    public async Task A_withdrawn_comment_offers_no_controls_to_anybody()
    {
        var fixture = new Fixture();
        var id = await fixture.PostAsync("geri alınacak");
        await fixture.WithdrawAsync(id);

        Assert.False(Assert.Single((await fixture.ProjectAsync()).Activity!).Editable);
    }

    // ── Notification ─────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (the actor). Telling somebody about a thing they just did is noise, and noise is how
    /// people learn to ignore the notifications that matter.
    ///
    /// <para>The exclusion is NOT re-implemented here: <c>ITaskNotificationService</c> owns it for every event in
    /// the module, and the double delegates to the same policy production uses.</para>
    /// </summary>
    [Fact]
    public async Task A_new_comment_tells_the_holder_the_requester_and_the_watchers_but_not_the_writer()
    {
        var fixture = new Fixture(assignee: TaskTestData.Other, requester: TaskTestData.Rival);
        await fixture.AddWatcherAsync(TaskTestData.Watcher);

        await fixture.PostAsync("kim haber alacak?");

        var sent = Assert.Single(fixture.Notifications.Notifications);
        Assert.Equal(TaskNotificationEvents.Commented, sent.EventCode);
        Assert.Contains(TaskTestData.Other, sent.Candidates);
        Assert.Contains(TaskTestData.Rival, sent.Candidates);
        Assert.Contains(TaskTestData.Watcher, sent.Candidates);
        Assert.DoesNotContain(TaskTestData.Me, sent.Candidates);
    }

    /// <summary>
    /// Whoever already spoke here has declared an interest no assignment field records. Answering into silence is
    /// how a feed stops being used.
    /// </summary>
    [Fact]
    public async Task A_new_comment_also_tells_whoever_commented_before()
    {
        var fixture = new Fixture(assignee: TaskTestData.Other, requester: TaskTestData.Other);
        fixture.Seed("önceki soru", TaskTestData.Rival);

        await fixture.PostAsync("cevap");

        Assert.Contains(TaskTestData.Rival, Assert.Single(fixture.Notifications.Notifications).Candidates);
    }

    [Fact]
    public async Task Editing_and_withdrawing_send_nothing()
    {
        // A typo correction does not earn anybody's inbox, and a retraction that emailed everyone would shout
        // louder than the sentence it takes back.
        var fixture = new Fixture(assignee: TaskTestData.Other, requester: TaskTestData.Rival);
        var id = await fixture.PostAsync("ilk");
        fixture.Notifications.Notifications.Clear();

        await fixture.EditAsync(id, "düzeltildi");
        await fixture.WithdrawAsync(id);

        Assert.Empty(fixture.Notifications.Notifications);
    }

    [Fact]
    public async Task A_task_whose_owner_switched_email_off_sends_nothing()
    {
        // The task's own settings, honoured by the service — not re-implemented at the call site.
        var fixture = new Fixture(assignee: TaskTestData.Other, requester: TaskTestData.Rival, emailEnabled: false);

        await fixture.PostAsync("kimse duymayacak");

        Assert.Empty(fixture.Notifications.Notifications);
    }

    [Fact]
    public async Task A_task_whose_owner_chose_other_events_does_not_send_this_one()
    {
        // An EMPTY list means "chose none"; a list without this code means "chose others". Both must stay silent
        // here, and an ABSENT list (nobody chose) must not — that case is covered by the audience test above.
        var fixture = new Fixture(
            assignee: TaskTestData.Other, requester: TaskTestData.Rival,
            notifyOn: [TaskNotificationEvents.Assigned]);

        await fixture.PostAsync("bu olay seçilmedi");

        Assert.Empty(fixture.Notifications.Notifications);
    }

    private static int StatusOf(IActionResult result)
        => Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode!.Value;

    private sealed class Fixture
    {
        private readonly FakeTaskItemRepository _tasks;
        private readonly TasksController _controller;

        public Fixture(
            Guid? actingAs = null,
            Fixture? sharing = null,
            Guid? assignee = null,
            Guid? requester = null,
            bool emailEnabled = true,
            IReadOnlyList<string>? notifyOn = null)
        {
            Task = sharing?.Task ?? new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "CT probe",
                AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
                AssigneeUserId = assignee ?? TaskTestData.Me,
                CreatedByUserId = requester ?? TaskTestData.Me,
                OrganizationUnitId = Guid.NewGuid(),
                Lifecycle = TaskLifecycle.InProgress,
                EmailNotificationsEnabled = emailEnabled,
                NotifyOnEvents = notifyOn,
                Version = 1
            };
            // A SHARED store when a second actor is simulated: two stores would let "the rival cannot edit it"
            // pass because the comment was never there, which proves nothing.
            _tasks = sharing?._tasks ?? new FakeTaskItemRepository(Task);
            Comments = sharing?.Comments ?? new FakeTaskCommentRepository();
            Watchers = sharing?.Watchers ?? new FakeTaskWatcherRepository();
            Notifications = sharing?.Notifications ?? new FakeTaskNotificationService();

            var user = new FakeCurrentUserContext(actingAs ?? TaskTestData.Me);
            var tenant = new FakeTenantContext(TaskTestData.Tenant);
            var mediator = new DirectMediator(
                new AddTaskCommentHandler(
                    _tasks, Comments, user, new FakeUserDisplayNameResolver(), tenant,
                    Watchers, Notifications, NullLogger<AddTaskCommentHandler>.Instance),
                new UpdateTaskCommentHandler(Comments, user),
                new WithdrawTaskCommentHandler(Comments, user));

            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            _controller = new TasksController(mediator, correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public TaskItem Task { get; }

        public FakeTaskCommentRepository Comments { get; }

        public FakeTaskWatcherRepository Watchers { get; }

        public FakeTaskNotificationService Notifications { get; }

        public async Task<Guid> PostAsync(string text)
        {
            var result = await _controller.AddComment(
                Task.Id, new AddTaskCommentRequest(text), CancellationToken.None);
            // The id the endpoint answers with, unwrapped from the envelope the controller returns — the same
            // one a client would then edit or withdraw.
            var envelope = Assert.IsType<Diten.Platform.Application.Common.Response<Guid>>(
                Assert.IsType<CreatedResult>(result).Value);
            return envelope.Data;
        }

        public Task<IActionResult> EditAsync(Guid commentId, string text, Guid? taskId = null)
            => _controller.UpdateComment(
                taskId ?? Task.Id, commentId, new UpdateTaskCommentRequest(text), CancellationToken.None);

        public Task<IActionResult> WithdrawAsync(Guid commentId)
            => _controller.WithdrawComment(Task.Id, commentId, CancellationToken.None);

        /// <summary>Writes a comment by SOMEBODY ELSE directly — the API always writes as the caller.</summary>
        public void Seed(string text, Guid authorUserId)
            => Comments.CreateAsync(
                new TaskComment
                {
                    TenantId = TaskTestData.Tenant,
                    TaskItemId = Task.Id,
                    Text = text,
                    AuthorUserId = authorUserId
                },
                CancellationToken.None).GetAwaiter().GetResult();

        public System.Threading.Tasks.Task AddWatcherAsync(Guid userId)
            => Watchers.CreateAsync(
                new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = Task.Id, UserId = userId },
                CancellationToken.None);

        public async Task<WorkItemProjectionDto> ProjectAsync(Guid? asUser = null)
        {
            var provider = new TaskWorkItemProvider(
                _tasks,
                new FakePositionAssignmentRepository(),
                new TaskLifecycleService(),
                new TaskAssignmentResolver(),
                new FakeUserDisplayNameResolver(),
                new FakeChecklistRunRepository(),
                new FakeTaskApprovalService(),
                new FakeTaskDependencyRepository(),
                Comments,
                new FakeTaskTransitionRepository(),
                new FakeTaskPersonalOverlayRepository(),
                Watchers,
                TaskActors.PermitAll(),
                new FakePositionRepository(),
                new FakeOrganizationUnitRepository(),
                SlaForTests.Real(),
                new FakeTaskFieldDefinitionRepository());

            var user = asUser ?? TaskTestData.Me;
            var previousAssignee = Task.AssigneeUserId;
            // Whoever is reading has to hold the task for it to be in their list; the point under test is the
            // per-reader `editable` flag, not who the work belongs to.
            Task.AssigneeUserId = user;
            try
            {
                var items = await provider.GetWorkItemsAsync(
                    new WorkItemActor(user, IsPlatformActor: true, new HashSet<string>()), CancellationToken.None);
                return Assert.Single(items.Where(item => item.Id == Task.Id.ToString()));
            }
            finally
            {
                Task.AssigneeUserId = previousAssignee;
            }
        }
    }
}
