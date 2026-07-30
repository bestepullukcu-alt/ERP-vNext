using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-034 item 7 — task comments, end to end.
///
/// <para>The screen already had a composer and a feed, both behind the <c>activity</c> capability that no provider
/// declared, so neither had ever appeared. This covers the half that was missing: the record, the write, the
/// refusal, and what the projection says afterwards.</para>
///
/// <para>The write cases post through the real <see cref="TasksController"/> action, because this module has three
/// times shipped a rule that lived only in the projection and answered 204 when posted to directly.</para>
/// </summary>
public sealed class TaskCommentTests
{
    // ── The round trip ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_posted_comment_comes_back_on_the_next_read()
    {
        var fixture = new Fixture();

        var posted = await fixture.PostAsync("Bütçe onayını bekliyoruz.");

        AssertCreated(posted);
        var entry = Assert.Single((await fixture.ProjectAsync()).Activity!);
        Assert.Equal("comment", entry.Kind);
        Assert.Equal("Bütçe onayını bekliyoruz.", entry.Text);
        Assert.Equal(TaskTestData.MeDisplayName, entry.Actor);
    }

    [Fact]
    public async Task The_text_is_stored_trimmed_and_as_typed()
    {
        var fixture = new Fixture();

        await fixture.PostAsync("   kenar boşlukları   ");

        Assert.Equal("kenar boşlukları", Assert.Single(fixture.Comments.Comments).Text);
    }

    // ── The refusals ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public async Task A_closed_task_refuses_a_comment(TaskLifecycle lifecycle)
    {
        // The composer is already hidden on a terminal task — but hiding a control is presentation, and this
        // module has had to learn that three times (cancel authority, dependencies, subtasks).
        var fixture = new Fixture(lifecycle);

        var result = await fixture.PostAsync("geç kalmış bir not");

        var status = Assert.IsAssignableFrom<IStatusCodeActionResult>(result);
        // 409, not 403: it is the task's STATE that refuses, not the caller's identity. Everyone is equally
        // unable to comment on a closed task.
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);
        Assert.Equal(TaskReasonCodes.CommentTaskClosed, ReasonOf(result));
        Assert.Empty(fixture.Comments.Comments);
    }

    [Fact]
    public async Task A_closed_task_still_SHOWS_its_comments()
    {
        // History is finished, not sealed. Refusing to read it would hide the reasoning behind work already done.
        var fixture = new Fixture();
        await fixture.PostAsync("kapanmadan önce söylenmiş");
        fixture.Task.Lifecycle = TaskLifecycle.Done;

        var projection = await fixture.ProjectAsync();

        Assert.Single(projection.Activity!);
        Assert.Contains("activity", projection.WorkItemCapabilities);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public async Task Empty_text_is_refused(string text)
    {
        var fixture = new Fixture();

        var result = await fixture.PostAsync(text);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        Assert.Equal(TaskReasonCodes.CommentTextInvalid, ReasonOf(result));
        Assert.Empty(fixture.Comments.Comments);
    }

    [Fact]
    public async Task Text_over_the_limit_is_refused_and_text_at_the_limit_is_not()
    {
        var fixture = new Fixture();

        var tooLong = await fixture.PostAsync(new string('x', TaskCommentLimits.MaxTextLength + 1));
        Assert.Equal(TaskReasonCodes.CommentTextInvalid, ReasonOf(tooLong));

        // The boundary itself is allowed — an off-by-one here would reject a legitimate comment.
        AssertCreated(await fixture.PostAsync(new string('x', TaskCommentLimits.MaxTextLength)));
        Assert.Single(fixture.Comments.Comments);
    }

    // ── Order ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_feed_reads_newest_first()
    {
        // The composer sits at the top of the feed, so the newest entry has to be next to it.
        var fixture = new Fixture();
        var at = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);
        fixture.Seed("ilk", at);
        fixture.Seed("ikinci", at.AddHours(1));
        fixture.Seed("üçüncü", at.AddHours(2));

        var texts = (await fixture.ProjectAsync()).Activity!.Select(entry => entry.Text).ToList();

        Assert.Equal(["üçüncü", "ikinci", "ilk"], texts);
    }

    [Fact]
    public async Task Comments_written_in_the_same_instant_keep_a_stable_order()
    {
        // Order is behaviour on this screen: a list that rearranges itself between reads reads as data changing.
        var at = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);
        var fixture = new Fixture();
        fixture.Seed("a", at);
        fixture.Seed("b", at);
        fixture.Seed("c", at);

        var first = (await fixture.ProjectAsync()).Activity!.Select(entry => entry.Text).ToList();
        var second = (await fixture.ProjectAsync()).Activity!.Select(entry => entry.Text).ToList();

        Assert.Equal(first, second);
    }

    // ── What the wire carries ────────────────────────────────────────────────

    [Fact]
    public async Task The_entry_carries_an_absolute_instant_and_no_relative_count()
    {
        /*
         * A server-computed "3 days ago" is stale the moment it is serialized and stays wrong for as long as the
         * tab is open — the same defect class as the frozen "today" this surface already had. The DTO therefore
         * has no such field, and this pins it: adding one would fail here.
         */
        var fixture = new Fixture();
        var at = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);
        fixture.Seed("mutlak", at);

        var entry = Assert.Single((await fixture.ProjectAsync()).Activity!);

        Assert.Equal(at, entry.At);
        Assert.DoesNotContain(
            typeof(WorkItemActivityEntryDto).GetProperties(),
            property => property.Name.Contains("Ago", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Contains("Days", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task An_unresolved_author_is_null_rather_than_an_id()
    {
        // A GUID is not a person's name; the client has its own "name unavailable" label.
        var fixture = new Fixture(resolveNames: false);
        await fixture.PostAsync("anonim");

        Assert.Null(Assert.Single((await fixture.ProjectAsync()).Activity!).Actor);
    }

    [Fact]
    public async Task The_author_name_is_a_snapshot_and_does_not_follow_a_later_rename()
    {
        // The feed records who said it AT THE TIME. Re-resolving on read would silently reattribute history, and
        // would make reading a task depend on AuthService being reachable.
        var fixture = new Fixture();
        await fixture.PostAsync("söylenmiş söz");

        fixture.RenameEveryone("Yeni İsim");

        Assert.Equal(TaskTestData.MeDisplayName, Assert.Single((await fixture.ProjectAsync()).Activity!).Actor);
    }

    // ── Capability ⇔ container ───────────────────────────────────────────────

    [Fact]
    public async Task A_task_nobody_has_commented_on_declares_the_capability_with_an_empty_feed()
    {
        /*
         * Both halves or neither — the contract rejects a declared capability with no container and a container
         * with no capability. Declared-and-empty is the valid state, and it is the RIGHT one here: MOD-0024 owns
         * the conversation, so the composer must appear on a task nobody has written on yet, which is exactly
         * where it is needed.
         */
        var projection = await new Fixture().ProjectAsync();

        Assert.Contains("activity", projection.WorkItemCapabilities);
        Assert.NotNull(projection.Activity);
        Assert.Empty(projection.Activity!);
    }

    [Fact]
    public async Task The_feed_holds_only_comments_and_no_derived_lifecycle_events()
    {
        /*
         * Deliberate scope: there is no lifecycle event log, and deriving one from the four timestamps a task
         * happens to carry would silently omit accept, plan, claim, release and inquire. A partial history is
         * worse than none, because it is read as complete. A real event log is its own slice.
         */
        var fixture = new Fixture();
        fixture.Task.StartAt = DateTimeOffset.UtcNow.AddDays(-2);
        fixture.Task.Lifecycle = TaskLifecycle.InProgress;
        await fixture.PostAsync("tek girdi");

        var entry = Assert.Single((await fixture.ProjectAsync()).Activity!);
        Assert.Equal("comment", entry.Kind);
    }

    // ── The code the client has to understand ────────────────────────────────

    [Theory]
    [InlineData(nameof(TaskReasonCodes.CommentTaskClosed))]
    [InlineData(nameof(TaskReasonCodes.CommentTextInvalid))]
    public void Both_refusal_codes_are_translatable_by_the_frontend_bridge(string which)
    {
        var code = which == nameof(TaskReasonCodes.CommentTaskClosed)
            ? TaskReasonCodes.CommentTaskClosed
            : TaskReasonCodes.CommentTextInvalid;
        var api = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "frontend/Diten.Web/wwwroot/assets/js/Tasks/api.js"));

        Assert.Contains(code, api, StringComparison.Ordinal);
    }

    [Fact]
    public void The_proxy_forwards_the_comments_route()
    {
        // Diten.Web and Platform share no assembly, so this reads the proxy as text — the check that would have
        // caught `inquire` answering 404 in the web tier while Platform's suite stayed green.
        var proxy = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "frontend/Diten.Web/Controllers/TasksController.cs"));

        Assert.Contains("api/{id:guid}/comments", proxy, StringComparison.Ordinal);
    }

    [Fact]
    public void There_is_no_edit_or_delete_endpoint_for_a_comment()
    {
        /*
         * A comment is immutable, and that is a decision rather than an omission: once someone has acted on what
         * a comment said, removing it rewrites the past. If retraction is ever needed it arrives as a "withdrawn"
         * MARK. This guards the decision against being quietly undone.
         */
        var controller = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "services/Diten.Platform/src/Diten.Platform.Api/Controllers/TasksController.cs"));

        Assert.DoesNotContain("HttpPut(\"{id:guid}/comments", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpDelete(\"{id:guid}/comments", controller, StringComparison.Ordinal);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void AssertCreated(IActionResult result)
        => Assert.Equal(
            StatusCodes.Status201Created,
            Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);

    private static string? ReasonOf(IActionResult result)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        return Assert.IsType<Response<Guid>>(objectResult.Value).ReasonCode;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                && Directory.Exists(Path.Combine(directory.FullName, "frontend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root walking up from {AppContext.BaseDirectory}.");
    }

    private sealed class Fixture
    {
        private readonly FakeTaskItemRepository _tasks;
        private readonly MutableDisplayNameResolver _names;
        private readonly TasksController _controller;

        public Fixture(TaskLifecycle lifecycle = TaskLifecycle.InProgress, bool resolveNames = true)
        {
            Task = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "CT probe",
                AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
                AssigneeUserId = TaskTestData.Me,
                CreatedByUserId = TaskTestData.Me,
                OrganizationUnitId = Guid.NewGuid(),
                Lifecycle = lifecycle,
                Version = 1
            };
            _tasks = new FakeTaskItemRepository(Task);
            _names = new MutableDisplayNameResolver(resolveNames ? TaskTestData.MeDisplayName : null);

            var handler = new AddTaskCommentHandler(
                _tasks,
                Comments,
                new FakeCurrentUserContext(TaskTestData.Me),
                _names,
                new FakeTenantContext(TaskTestData.Tenant));

            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            _controller = new TasksController(new DirectMediator(handler), correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public TaskItem Task { get; }

        public FakeTaskCommentRepository Comments { get; } = new();

        public Task<IActionResult> PostAsync(string text)
            => _controller.AddComment(Task.Id, new AddTaskCommentRequest(text), CancellationToken.None);

        /// <summary>Writes a comment directly, with a chosen instant — the API cannot backdate one.</summary>
        public void Seed(string text, DateTimeOffset at)
            => Comments.CreateAsync(
                new TaskComment
                {
                    TenantId = TaskTestData.Tenant,
                    TaskItemId = Task.Id,
                    Text = text,
                    AuthorUserId = TaskTestData.Me,
                    AuthorDisplayName = TaskTestData.MeDisplayName,
                    CreatedAt = at
                },
                CancellationToken.None).GetAwaiter().GetResult();

        public void RenameEveryone(string name) => _names.Name = name;

        public async Task<WorkItemProjectionDto> ProjectAsync()
        {
            var provider = new TaskWorkItemProvider(
                _tasks,
                new FakePositionAssignmentRepository(),
                new TaskLifecycleService(),
                new TaskAssignmentResolver(),
                _names,
                new FakeChecklistRunRepository(),
                new FakeTaskApprovalService(),
                new FakeTaskDependencyRepository(),
                Comments, new FakePositionRepository(), new FakeOrganizationUnitRepository());

            var actor = new WorkItemActor(TaskTestData.Me, IsPlatformActor: false, new HashSet<string>(
                new[] { TaskPermissions.Update, TaskPermissions.Complete, TaskPermissions.Cancel },
                StringComparer.OrdinalIgnoreCase));

            return Assert.Single(
                (await provider.GetWorkItemsAsync(actor, CancellationToken.None))
                    .Where(item => item.Id == Task.Id.ToString()));
        }
    }

    /// <summary>A name resolver whose answer can CHANGE, so a rename can be simulated after the fact.</summary>
    private sealed class MutableDisplayNameResolver : IUserDisplayNameResolver
    {
        public MutableDisplayNameResolver(string? name) => Name = name;

        public string? Name { get; set; }

        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                Name is null
                    ? new Dictionary<Guid, string>()
                    : userIds.ToDictionary(id => id, _ => Name));
    }
}
