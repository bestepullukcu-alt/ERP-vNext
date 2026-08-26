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
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WC-1 — the personal overlay, end to end.
///
/// <para><b>What was measured on 2026-08-14.</b> The detail page had a note box, a save button and a toast that
/// said "Not kaydedildi". No request left the browser: the note was one assignment to a JavaScript object and the
/// next reload took it away. The snooze was the same. The projection's own comment recorded the decision that put
/// it there ("owned by the frontend WorkCenter layer") — a decision whose other half was never built.</para>
///
/// <para>Every write here goes through the real <see cref="TasksController"/> action, not straight to a handler.
/// This module has three times shipped a rule that lived only in the projection and answered 204 when posted to
/// directly, and once shipped an endpoint (<c>inquire</c>) that no client could reach at all.</para>
/// </summary>
public sealed class TaskPersonalOverlayTests
{
    // ── The round trip: does it survive a re-read? ────────────────────────────

    [Fact]
    public async Task A_saved_note_comes_back_on_the_next_read()
    {
        var fixture = new Fixture();

        await fixture.AddNoteAsync("Muhasebeye sormadan kapatma.");

        var personal = (await fixture.ProjectAsync()).Personal;
        Assert.NotNull(personal);
        Assert.Equal("Muhasebeye sormadan kapatma.", Assert.Single(personal!.Notes).Text);
    }

    [Fact]
    public async Task Two_notes_both_survive_and_keep_the_order_they_were_written_in()
    {
        var fixture = new Fixture();

        await fixture.AddNoteAsync("önce bu");
        await fixture.AddNoteAsync("sonra bu");

        var notes = (await fixture.ProjectAsync()).Personal!.Notes;
        Assert.Equal(["önce bu", "sonra bu"], notes.Select(n => n.Text));
    }

    [Fact]
    public async Task Deleting_one_note_leaves_the_other_one_standing()
    {
        var fixture = new Fixture();
        await fixture.AddNoteAsync("kalacak");
        await fixture.AddNoteAsync("silinecek");
        var doomed = (await fixture.ProjectAsync()).Personal!.Notes.Single(n => n.Text == "silinecek");

        await fixture.DeleteNoteAsync(Guid.Parse(doomed.Id));

        Assert.Equal("kalacak", Assert.Single((await fixture.ProjectAsync()).Personal!.Notes).Text);
    }

    [Fact]
    public async Task A_snooze_survives_the_next_read()
    {
        var fixture = new Fixture();
        var until = DateTimeOffset.UtcNow.AddDays(3);

        await fixture.SnoozeAsync(until);

        Assert.Equal(until, (await fixture.ProjectAsync()).Personal!.SnoozedUntil);
    }

    [Fact]
    public async Task Waking_the_task_clears_the_snooze_without_touching_the_notes()
    {
        var fixture = new Fixture();
        await fixture.AddNoteAsync("not");
        await fixture.SnoozeAsync(DateTimeOffset.UtcNow.AddDays(3));

        await fixture.SnoozeAsync(null);

        var personal = (await fixture.ProjectAsync()).Personal;
        Assert.Null(personal!.SnoozedUntil);
        Assert.Single(personal.Notes);
    }

    /// <summary>
    /// The three writes share ONE document. Proved by counting overlays after all three have run — two documents
    /// for one reader would split their notes and make whichever one a read found first look like the list had
    /// lost entries. (The unique index says the same thing; this says it where a test can fail.)
    /// </summary>
    [Fact]
    public async Task The_note_and_the_snooze_live_in_one_document()
    {
        var fixture = new Fixture();

        await fixture.AddNoteAsync("bir");
        await fixture.AddNoteAsync("iki");
        await fixture.SnoozeAsync(DateTimeOffset.UtcNow.AddDays(1));

        var overlay = Assert.Single(fixture.Overlays.Overlays);
        Assert.Equal(2, overlay.Notes.Count);
        Assert.NotNull(overlay.SnoozedUntil);
    }

    // ── The read rule: another person's note is never READ, not merely hidden ─

    /// <summary>
    /// THE AUTHORIZATION TEST. A second actor projects the same task and sees no personal layer at all.
    ///
    /// <para>This is a READ rule, enforced by the repository query, so removing the user filter turns this red —
    /// which is exactly what "the server filters, the client does not hide" has to mean to be worth stating.</para>
    /// </summary>
    [Fact]
    public async Task Another_person_does_not_see_my_note()
    {
        var fixture = new Fixture();
        await fixture.AddNoteAsync("kimseye söyleme");

        var theirs = await fixture.ProjectAsync(TaskTestData.Rival);

        Assert.Null(theirs.Personal);
    }

    [Fact]
    public async Task Another_person_does_not_see_my_snooze()
    {
        var fixture = new Fixture();
        await fixture.SnoozeAsync(DateTimeOffset.UtcNow.AddDays(5));

        Assert.Null((await fixture.ProjectAsync(TaskTestData.Rival)).Personal);
    }

    /// <summary>
    /// Deleting somebody else's note answers 404 — the SAME answer a nonexistent id gets. A distinct "not yours"
    /// would confirm that another person's note exists, which is the one thing the read rule refuses to tell.
    /// </summary>
    [Fact]
    public async Task Deleting_a_note_that_is_not_mine_answers_404()
    {
        var fixture = new Fixture();
        await fixture.AddNoteAsync("benim");
        var mine = Guid.Parse((await fixture.ProjectAsync()).Personal!.Notes.Single().Id);

        var asRival = new Fixture(actingAs: TaskTestData.Rival, sharing: fixture);
        var refused = await asRival.DeleteNoteAsync(mine);

        Assert.Equal(404, StatusOf(refused));
        // And nothing was removed.
        Assert.Single((await fixture.ProjectAsync()).Personal!.Notes);
    }

    // ── The contract: a snooze changes what I SEE, never what the task IS ─────

    [Fact]
    public async Task A_snooze_does_not_move_the_task()
    {
        var fixture = new Fixture();
        var before = await fixture.ProjectAsync();

        await fixture.SnoozeAsync(DateTimeOffset.UtcNow.AddDays(2));
        var after = await fixture.ProjectAsync();

        Assert.Equal(before.NormalizedStatus, after.NormalizedStatus);
        Assert.Equal(before.TaskLifecycle, after.TaskLifecycle);
        Assert.Null(after.WaitingContext);
        Assert.Equal(TaskLifecycle.InProgress, fixture.Task.Lifecycle);
    }

    /// <summary>
    /// An EXPIRED snooze projects as no snooze. Sending the stale date would push "is this still parked?" onto
    /// every client — a decision the server is here to make.
    /// </summary>
    [Fact]
    public async Task A_snooze_whose_date_has_passed_is_projected_as_none()
    {
        var fixture = new Fixture();
        fixture.SeedOverlay(snoozedUntil: DateTimeOffset.UtcNow.AddDays(-1));

        Assert.Null((await fixture.ProjectAsync()).Personal);
    }

    // ── The refusals ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_note_is_refused(string text)
    {
        var refused = await new Fixture().AddNoteAsync(text);

        Assert.Equal(400, StatusOf(refused));
    }

    [Fact]
    public async Task A_note_longer_than_the_limit_is_refused()
    {
        var refused = await new Fixture().AddNoteAsync(new string('x', TaskPersonalNoteLimits.MaxTextLength + 1));

        Assert.Equal(400, StatusOf(refused));
    }

    [Fact]
    public async Task The_text_is_stored_trimmed_and_as_typed()
    {
        var fixture = new Fixture();

        await fixture.AddNoteAsync("   kenar boşlukları   ");

        Assert.Equal("kenar boşlukları", Assert.Single(fixture.Overlays.Overlays).Notes.Single().Text);
    }

    /// <summary>
    /// A snooze into the PAST is refused rather than stored. Storing it would report success and change nothing
    /// visible — the same lie as the toast this whole change removes.
    /// </summary>
    [Fact]
    public async Task A_snooze_date_in_the_past_is_refused()
    {
        var refused = await new Fixture().SnoozeAsync(DateTimeOffset.UtcNow.AddDays(-1));

        Assert.Equal(400, StatusOf(refused));
    }

    [Fact]
    public async Task A_note_on_a_task_that_does_not_exist_answers_404()
    {
        var fixture = new Fixture();

        var refused = await fixture.AddNoteAsync("boşluğa", taskId: Guid.NewGuid());

        Assert.Equal(404, StatusOf(refused));
    }

    /// <summary>
    /// A CLOSED task still takes notes — the opposite of the comment rule, deliberately. A comment addresses other
    /// people about live work; a note addresses oneself, and finished work is exactly what one writes conclusions
    /// about. Nothing another person can read changes, so there is nothing to seal.
    /// </summary>
    [Fact]
    public async Task A_closed_task_still_accepts_a_personal_note()
    {
        var fixture = new Fixture(lifecycle: TaskLifecycle.Done);

        var accepted = await fixture.AddNoteAsync("neden iptal edildiğini unutma");

        Assert.Equal(201, StatusOf(accepted));
    }

    // ── Migration: what a task written before any of this existed looks like ──

    /// <summary>
    /// Every task in the database today has NO overlay document. The container is then omitted entirely rather
    /// than emitted empty — one code path, no back-fill, and nothing for a client to tell apart from "no notes".
    /// </summary>
    [Fact]
    public async Task A_task_that_predates_the_overlay_carries_no_personal_layer()
    {
        var projected = await new Fixture().ProjectAsync();

        Assert.Null(projected.Personal);
    }

    /// <summary>
    /// The status, whatever SHAPE the controller chose. <c>CreateActionResultInstance</c> returns a
    /// <c>CreatedResult</c> for a 201 and an <c>ObjectResult</c> for a refusal, so asserting one concrete type
    /// tests the framework's choice rather than the endpoint's answer.
    /// </summary>
    private static int StatusOf(IActionResult result)
        => Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode!.Value;

    private sealed class Fixture
    {
        private readonly FakeTaskItemRepository _tasks;
        private readonly TasksController _controller;

        public Fixture(
            TaskLifecycle lifecycle = TaskLifecycle.InProgress,
            Guid? actingAs = null,
            Fixture? sharing = null)
        {
            Task = sharing?.Task ?? new TaskItem
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
            _tasks = sharing?._tasks ?? new FakeTaskItemRepository(Task);
            // A SHARED store when a second actor is being simulated: two stores would let "the rival cannot see
            // it" pass because the note was never there, which proves nothing at all.
            Overlays = sharing?.Overlays ?? new FakeTaskPersonalOverlayRepository();

            var user = new FakeCurrentUserContext(actingAs ?? TaskTestData.Me);
            var tenant = new FakeTenantContext(TaskTestData.Tenant);
            var mediator = new DirectMediator(
                new AddTaskPersonalNoteHandler(_tasks, Overlays, user, tenant),
                new DeleteTaskPersonalNoteHandler(Overlays, user),
                new SetTaskSnoozeHandler(_tasks, Overlays, user, tenant));

            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            _controller = new TasksController(mediator, correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public TaskItem Task { get; }

        public FakeTaskPersonalOverlayRepository Overlays { get; }

        public Task<IActionResult> AddNoteAsync(string text, Guid? taskId = null)
            => _controller.AddPersonalNote(
                taskId ?? Task.Id, new AddTaskPersonalNoteRequest(text), CancellationToken.None);

        public Task<IActionResult> DeleteNoteAsync(Guid noteId)
            => _controller.DeletePersonalNote(Task.Id, noteId, CancellationToken.None);

        public Task<IActionResult> SnoozeAsync(DateTimeOffset? until)
            => _controller.SetSnooze(Task.Id, new SetTaskSnoozeRequest(until), CancellationToken.None);

        /// <summary>Writes an overlay directly — the API cannot backdate a snooze, which is the point.</summary>
        public void SeedOverlay(DateTimeOffset? snoozedUntil)
            => Overlays.UpsertAsync(
                new TaskPersonalOverlay
                {
                    TenantId = TaskTestData.Tenant,
                    TaskItemId = Task.Id,
                    UserId = TaskTestData.Me,
                    SnoozedUntil = snoozedUntil
                },
                CancellationToken.None).GetAwaiter().GetResult();

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
                new FakeTaskCommentRepository(),
                new FakeTaskTransitionRepository(),
                // The fixture's OWN overlay store, so what the handlers wrote is what the projection reads.
                Overlays,
                new FakeTaskWatcherRepository(),
                TaskActors.PermitAll(),
                new FakePositionRepository(),
                new FakeOrganizationUnitRepository(),
                SlaForTests.Real(),
                new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository());

            var actor = new WorkItemActor(asUser ?? TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());
            var items = await provider.GetWorkItemsAsync(actor, CancellationToken.None);

            // A rival holds none of this task, so their own list is empty — the projection is asked for the SAME
            // task explicitly instead, which is what the detail page does.
            return items.FirstOrDefault(item => item.Id == Task.Id.ToString())
                ?? await ProjectForOutsiderAsync(provider, asUser ?? TaskTestData.Me);
        }

        private async Task<WorkItemProjectionDto> ProjectForOutsiderAsync(TaskWorkItemProvider provider, Guid user)
        {
            // The rival is not the assignee, so GetWorkItemsAsync returns nothing for them. Temporarily hand them
            // the task to prove the point that matters: even HOLDING it, they do not get my overlay.
            var previousAssignee = Task.AssigneeUserId;
            Task.AssigneeUserId = user;
            try
            {
                var items = await provider.GetWorkItemsAsync(
                    new WorkItemActor(user, IsPlatformActor: true, new HashSet<string>()),
                    CancellationToken.None);
                return Assert.Single(items.Where(item => item.Id == Task.Id.ToString()));
            }
            finally
            {
                Task.AssigneeUserId = previousAssignee;
            }
        }
    }
}
