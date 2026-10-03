using System.Text.Json;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-509 (WP-WCN-TASK-REQUEST-01, slice D1) — the request's rules, each measured where it is decided. The journey
/// on the wire is <see cref="TaskRequestHttpMongoTests"/>; this file pins the pieces a journey cannot isolate: the
/// persisted numbers, the wire spellings, the owner-decision points, and the two sweeps that must not count a
/// request nobody accepted.
/// </summary>
public sealed class TaskRequestRulesTests
{
    private static readonly Guid Sender = TaskTestData.Me;
    private static readonly Guid Asked = TaskTestData.Other;
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    // ── persisted numbers and wire spellings ─────────────────────────────────────────────────────────────────

    /// <summary>Persisted, append-only: a renumbering rewrites the meaning of every stored document.</summary>
    [Fact]
    public void The_new_enum_members_keep_the_numbers_they_were_stored_with()
    {
        Assert.Equal(3, (int)TaskAssignmentTarget.Offered);
        Assert.Equal(18, (int)TaskTransitionKind.Offered);
        Assert.Equal(19, (int)TaskTransitionKind.Declined);
        Assert.Equal(20, (int)TaskTransitionKind.Withdrawn);
        Assert.Equal(7, (int)TaskAssignmentEventType.Offered);
        Assert.Equal(8, (int)TaskAssignmentEventType.Declined);
        Assert.Equal(9, (int)TaskAssignmentEventType.Withdrawn);

        // …and the ones that were there before did not move.
        Assert.Equal((0, 1, 2), ((int)TaskAssignmentTarget.SelfAssigned, (int)TaskAssignmentTarget.Person,
            (int)TaskAssignmentTarget.PositionPool));
        Assert.Equal(17, (int)TaskTransitionKind.Unplanned);
        Assert.Equal(6, (int)TaskAssignmentEventType.Reassigned);
    }

    /// <summary>The target crosses the wire as its NAME in both directions, never as a number.</summary>
    [Fact]
    public void The_offered_target_crosses_the_wire_as_its_name()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        Assert.Equal("\"Offered\"", JsonSerializer.Serialize(TaskAssignmentTarget.Offered, options));

        var request = JsonSerializer.Deserialize<CreateTaskItemRequest>(
            """{"title":"t","priority":"Medium","assignmentTarget":"Offered","offeredToUserId":"ffffffff-ffff-ffff-ffff-ffffffffffff"}""",
            options)!;
        Assert.Equal(TaskAssignmentTarget.Offered, request.AssignmentTarget);
        Assert.Equal(Asked, request.OfferedToUserId);
    }

    [Fact]
    public void The_three_request_events_have_their_wire_codes()
    {
        Assert.Equal("offered", TaskTransitionCodes.For(TaskTransitionKind.Offered));
        Assert.Equal("declined", TaskTransitionCodes.For(TaskTransitionKind.Declined));
        Assert.Equal("withdrawn", TaskTransitionCodes.For(TaskTransitionKind.Withdrawn));
    }

    [Fact]
    public void An_unanswered_request_projects_as_an_offer_nobody_owns()
    {
        var projection = new TaskAssignmentResolver().Resolve(Request());

        Assert.Equal(("offered", "unowned", "pendingOffer"),
            (projection.AssignmentMode, projection.OwnershipState, projection.AdmissionState));
    }

    // ── nobody works on what nobody accepted ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskLifecycle.Planned)]
    [InlineData(TaskLifecycle.InProgress)]
    [InlineData(TaskLifecycle.Waiting)]
    [InlineData(TaskLifecycle.PendingReview)]
    [InlineData(TaskLifecycle.Done)]
    public void An_unaccepted_request_can_move_nowhere_but_cancelled(TaskLifecycle target)
    {
        var lifecycle = new TaskLifecycleService();

        Assert.False(lifecycle.CanTransition(Request(), target, out var reason));
        Assert.Equal(TaskReasonCodes.RequestNotAccepted, reason);

        // A declined one — nobody it is addressed to — is no more workable than a pending one.
        var declined = Request();
        declined.OfferedToUserId = null;
        Assert.False(lifecycle.CanTransition(declined, target, out _));
    }

    [Fact]
    public void An_unaccepted_request_can_be_called_off()
        => Assert.True(new TaskLifecycleService().CanTransition(Request(), TaskLifecycle.Cancelled, out _));

    // ── a request is a person asking a person ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_recurrence_rule_cannot_send_a_request()
    {
        var refusal = TaskRecurrenceRules.Validate(
            TaskRecurrenceFrequency.Weekly, null, null, TaskAssignmentTarget.Offered, null, null);

        Assert.Equal(TaskReasonCodes.AssignmentTargetInvalid, refusal?.ReasonCode);
    }

    [Fact]
    public void A_template_cannot_default_to_a_request()
        => Assert.Equal(TaskReasonCodes.TemplateAssignmentInvalid,
            TaskTemplateRules.ValidateAssignment(TaskAssignmentTarget.Offered, null)?.ReasonCode);

    [Fact]
    public void The_intent_rule_accepts_a_request_only_where_one_may_be_sent_and_only_with_an_addressee()
    {
        Assert.Null(TaskAssignmentIntentRules.Validate(
            TaskAssignmentTarget.Offered, null, null, allowSelfAssigned: true, Asked, allowOffered: true));

        // No addressee · a holder on something nobody holds · sent from where no request can be · an addressee on
        // something that is not a request.
        Assert.NotNull(TaskAssignmentIntentRules.Validate(
            TaskAssignmentTarget.Offered, null, null, allowSelfAssigned: true, null, allowOffered: true));
        Assert.NotNull(TaskAssignmentIntentRules.Validate(
            TaskAssignmentTarget.Offered, Asked, null, allowSelfAssigned: true, Asked, allowOffered: true));
        Assert.NotNull(TaskAssignmentIntentRules.Validate(
            TaskAssignmentTarget.Offered, null, null, allowSelfAssigned: true, Asked, allowOffered: false));
        Assert.NotNull(TaskAssignmentIntentRules.Validate(
            TaskAssignmentTarget.Person, Asked, null, allowSelfAssigned: true, Asked, allowOffered: true));
    }

    // ── the owner-decision points ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The four decisions as they stand today (owner defaults, 2026-10-02). Changing one is one line in
    /// <see cref="TaskRequestPolicy"/> — and this line, deliberately: a decision should not change silently.
    /// </summary>
    [Fact]
    public void The_owner_decisions_stand_as_decided()
    {
        Assert.True(TaskRequestPolicy.RecipientNeedsActiveSeat);            // 6 — no seat, no request received
        Assert.False(TaskRequestPolicy.SenderNeedsActiveSeat);              // 6 — the key is enough to send
        Assert.False(TaskRequestPolicy.SenderMayReassignAfterAcceptance);   // 3 — the sender only watches
        Assert.False(TaskRequestPolicy.SenderMayEditAfterAcceptance);       // 3
        Assert.False(TaskRequestPolicy.RecipientManagerSeesPendingOffer);   // 4 — not before it is accepted
    }

    [Fact]
    public void Decision_3_only_the_sender_of_an_ACCEPTED_request_is_the_one_who_no_longer_directs()
    {
        var accepted = Request();
        accepted.AssignmentTarget = TaskAssignmentTarget.Person;
        accepted.OfferedToUserId = null;
        accepted.AssigneeUserId = Asked;

        Assert.True(TaskRequestPolicy.IsSenderOfAcceptedRequest(accepted, Sender));
        Assert.False(TaskRequestPolicy.IsSenderOfAcceptedRequest(accepted, Asked));      // the holder directs their own work
        Assert.False(TaskRequestPolicy.IsSenderOfAcceptedRequest(Request(), Sender));    // not accepted yet: still theirs

        // An ordinary assignment's requester keeps every lever they had.
        accepted.IsRequest = false;
        Assert.False(TaskRequestPolicy.IsSenderOfAcceptedRequest(accepted, Sender));

        // Handed back to the sender, they hold it — and a holder directs.
        var returned = Request();
        returned.AssignmentTarget = TaskAssignmentTarget.Person;
        returned.AssigneeUserId = Sender;
        Assert.False(TaskRequestPolicy.IsSenderOfAcceptedRequest(returned, Sender));
    }

    [Fact]
    public void Decision_4_a_pending_request_is_not_on_the_addressees_managers_team()
    {
        var team = new TaskTeamScope(HasTeam: true, UserIds: [Asked]);

        Assert.False(team.Covers(Request()));                                   // nobody holds it
        Assert.False(TaskRequestPolicy.TeamSeesPendingOffer(Request(), team));  // and the switch is off
    }

    [Fact]
    public void Who_a_request_waits_on_is_one_predicate()
    {
        Assert.True(TaskRequestPolicy.IsOfferedTo(Request(), Asked));
        Assert.False(TaskRequestPolicy.IsOfferedTo(Request(), Sender));
        Assert.False(TaskRequestPolicy.IsOfferedTo(Request(), Guid.Empty));

        var cancelled = Request();
        cancelled.Lifecycle = TaskLifecycle.Cancelled;
        Assert.False(TaskRequestPolicy.IsOfferedTo(cancelled, Asked));

        var accepted = Request();
        accepted.AssigneeUserId = Asked;
        Assert.False(TaskRequestPolicy.IsPendingOffer(accepted));
    }

    // ── a request nobody accepted is nobody's reminder ───────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (no holder on an offer). The due-soon sweep reminds whoever HOLDS the work. A request that
    /// was not accepted has no holder, so the person asked is not reminded about work they never took on; the
    /// moment they accept, they are.
    /// </summary>
    [Fact]
    public async Task The_due_soon_sweep_does_not_remind_the_person_asked_until_they_accept()
    {
        var request = Request();
        request.DueAt = Now.AddDays(1);
        request.ReminderLeadDays = 2;
        var tasks = new FakeTaskItemRepository(request);
        var notifications = new FakeTaskNotificationService();
        var sweep = new SendDueSoonRemindersHandler(tasks, notifications, NullLogger<SendDueSoonRemindersHandler>.Instance);

        await sweep.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.DoesNotContain(notifications.Notifications, n => n.Candidates.Contains(Asked));

        // Accepted: an ordinary held task, reminded like any other.
        var held = (await tasks.GetByIdAsync(request.Id))!;
        held.AssignmentTarget = TaskAssignmentTarget.Person;
        held.AssigneeUserId = Asked;
        held.OfferedToUserId = null;
        held.LastDueSoonReminderKey = null;
        Assert.True(await tasks.UpdateAsync(held, held.Version));

        await sweep.Handle(new SendDueSoonRemindersCommand(Now, 100, "c2"), CancellationToken.None);

        Assert.Contains(notifications.Notifications,
            n => n.EventCode == TaskNotificationEvents.DueSoon && n.Candidates.SequenceEqual([Asked]));
    }

    private static TaskItem Request() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Stabilite raporunu gözden geçir",
        AssignmentTarget = TaskAssignmentTarget.Offered,
        OfferedToUserId = Asked,
        IsRequest = true,
        CreatedByUserId = Sender,
        OrganizationUnitId = Guid.NewGuid(),
        CreatedBy = "d1"
    };
}
