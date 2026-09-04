using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WC-4 — task notifications that can actually be delivered.
///
/// <para><b>The defect at the centre.</b> MOD-0024 holds no user directory, so the recipient's USER ID was
/// written into the email address field — the code said so in a comment. Every task notification was therefore
/// addressed to a GUID. Combined with no seeded template (a 404 that creates no dispatch record at all) and a
/// manifest sync nobody ran, "task notifications work" was true of the plumbing and false of the product.</para>
/// </summary>
public sealed class TaskNotificationTests
{
    private static readonly Guid Assignee = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Requester = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid Stranger = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    // ── The seam: swapping the resolver changes what is sent ─────────────────

    [Fact]
    public async Task Swapping_the_recipient_resolver_swaps_the_address()
    {
        /*
         * THE proof this interface is load-bearing rather than decorative. Same task, same event, same
         * notification layer — only the resolver differs, and the address on the wire follows it.
         */
        var toAlice = new Harness(new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")));
        var toBob = new Harness(new FakeTaskNotificationRecipientResolver((Assignee, "bob@example.test")));

        await toAlice.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]);
        await toBob.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]);

        Assert.Equal("alice@example.test", toAlice.Dispatched.Single().To.Single().Email);
        Assert.Equal("bob@example.test", toBob.Dispatched.Single().To.Single().Email);
    }

    [Fact]
    public async Task A_user_id_NEVER_reaches_the_address_field()
    {
        /*
         * The shipped defect, pinned as a property. Before the resolver, the recipient's GUID went straight into
         * the email field, so nothing could ever be delivered — and nothing complained, because the notification
         * layer accepted the string happily.
         */
        var harness = new Harness(new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")));

        await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]);

        var address = harness.Dispatched.Single().To.Single().Email;
        Assert.DoesNotContain(Assignee.ToString(), address, StringComparison.OrdinalIgnoreCase);
        Assert.Contains('@', address);
    }

    [Fact]
    public async Task An_UNRESOLVABLE_recipient_is_skipped_and_the_rest_are_still_told()
    {
        // Partial resolution is normal: a user removed from AuthService, or one with no address on file. The
        // people who CAN be reached must still be, or one stale account silences a whole pool.
        var resolver = new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test"));
        var harness = new Harness(resolver);

        var outcome = await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee, Stranger]);

        Assert.Equal(TaskNotificationOutcome.Dispatched, outcome);
        Assert.Single(harness.Dispatched.Single().To);
        Assert.Equal("alice@example.test", harness.Dispatched.Single().To.Single().Email);
        // It was ASKED about both — the omission is the resolver's answer, not a filter upstream of it.
        Assert.Contains(Stranger, resolver.Asked);
    }

    [Fact]
    public async Task When_NOBODY_can_be_reached_that_is_reported_not_called_success()
    {
        /*
         * THE decision. The alternative — returning "dispatched" because an attempt was made — is the exact
         * failure this ticket exists to end: a system that believes it told people it never reached. Nothing is
         * sent, the outcome says so, and the caller logs it.
         */
        var harness = new Harness(new FakeTaskNotificationRecipientResolver());

        var outcome = await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee, Stranger]);

        Assert.Equal(TaskNotificationOutcome.NoRecipients, outcome);
        Assert.Empty(harness.Dispatched);
    }

    // ── The rules every event shares ─────────────────────────────────────────

    [Fact]
    public async Task The_ACTOR_is_never_told_about_their_own_action()
    {
        // Telling someone about a thing they just did is noise, and noise is how people learn to ignore the
        // notifications that matter.
        var harness = new Harness(new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")));

        var outcome = await harness.NotifyAsync(
            TaskNotificationEvents.Completed, [Assignee], actingUserId: Assignee);

        Assert.Equal(TaskNotificationOutcome.Skipped, outcome);
        Assert.Empty(harness.Dispatched);
    }

    [Fact]
    public async Task But_somebody_ELSE_in_the_same_audience_is_still_told()
    {
        // Non-vacuity: the actor is removed from the audience, not the audience discarded.
        var harness = new Harness(new FakeTaskNotificationRecipientResolver(
            (Assignee, "alice@example.test"), (Requester, "bob@example.test")));

        await harness.NotifyAsync(
            TaskNotificationEvents.Completed, [Assignee, Requester], actingUserId: Assignee);

        Assert.Equal("bob@example.test", harness.Dispatched.Single().To.Single().Email);
    }

    [Fact]
    public async Task A_task_with_notifications_switched_OFF_sends_nothing()
    {
        var harness = new Harness(new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")));
        harness.Task.EmailNotificationsEnabled = false;

        var outcome = await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]);

        Assert.Equal(TaskNotificationOutcome.Skipped, outcome);
        Assert.Empty(harness.Dispatched);
        // Not even resolved: the opt-out is honoured before anyone's address is looked up.
        Assert.Empty(harness.Resolver.Asked);
    }

    // ── A notification never fails the write ─────────────────────────────────

    [Fact]
    public async Task A_REFUSAL_from_the_notification_layer_is_swallowed()
    {
        // The commonest fresh-environment condition: the event is not in the catalogue, or no template is
        // seeded. Both are ops problems, and neither is the task's fault.
        var harness = new Harness(new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")));
        harness.Adapter.FailWithReasonCode = "EVENT_NOT_ACTIVE";

        var outcome = await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]);

        Assert.Equal(TaskNotificationOutcome.Failed, outcome);
    }

    [Fact]
    public async Task A_THROW_from_the_resolver_is_swallowed_too()
    {
        var harness = new Harness(new ThrowingRecipientResolver());

        var outcome = await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]);

        Assert.Equal(TaskNotificationOutcome.Failed, outcome);
    }

    // ── What actually crosses the wire ───────────────────────────────────────

    [Fact]
    public async Task Every_declared_variable_is_supplied()
    {
        /*
         * The manifest declares TaskTitle and TaskId required, DueAt optional. A template rendering a variable
         * nobody supplied produces a BLANK — an email that arrived and says nothing, which nobody reports as a
         * bug because it arrived.
         */
        var harness = new Harness(new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")));
        harness.Task.DueAt = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);

        await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]);

        var variables = harness.Dispatched.Single().Variables;
        Assert.Equal(harness.Task.Title, variables["TaskTitle"]);
        Assert.Equal(harness.Task.Id.ToString(), variables["TaskId"]);
        Assert.Equal("2026-08-15", variables["DueAt"]);
    }

    [Fact]
    public async Task A_task_with_no_due_date_supplies_an_empty_string_not_a_null()
    {
        // A null renders as the literal token in some engines; an empty string renders as nothing, which is what
        // "there is no due date" should look like.
        var harness = new Harness(new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")));
        harness.Task.DueAt = null;

        await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]);

        Assert.Equal(string.Empty, harness.Dispatched.Single().Variables["DueAt"]);
    }

    [Fact]
    public async Task The_event_code_reaches_the_notification_layer_verbatim()
    {
        var harness = new Harness(new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")));

        await harness.NotifyAsync(TaskNotificationEvents.Claimed, [Assignee]);

        Assert.Equal(TaskNotificationEvents.Claimed, harness.Dispatched.Single().EventCode);
    }

    // ── The pool audience ────────────────────────────────────────────────────

    [Fact]
    public async Task A_pooled_task_resolves_every_ACTIVE_holder_of_its_position()
    {
        var positionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var harness = new Harness(
            new FakeTaskNotificationRecipientResolver(),
            new FakePositionAssignmentRepository(
                Holder(positionId, Assignee),
                Holder(positionId, Requester),
                // Ended yesterday: no longer a holder, and telling them would be telling the wrong person.
                Holder(positionId, Stranger, endedDaysAgo: 1)));
        harness.Task.AssignmentTarget = TaskAssignmentTarget.PositionPool;
        harness.Task.PoolPositionId = positionId;

        var holders = await harness.Service.ResolvePoolHoldersAsync(harness.Task, CancellationToken.None);

        Assert.Equal(2, holders.Count);
        Assert.Contains(Assignee, holders);
        Assert.Contains(Requester, holders);
        Assert.DoesNotContain(Stranger, holders);
    }

    [Fact]
    public async Task A_task_that_is_not_pooled_has_no_pool_audience()
    {
        var harness = new Harness(new FakeTaskNotificationRecipientResolver());

        Assert.Empty(await harness.Service.ResolvePoolHoldersAsync(harness.Task, CancellationToken.None));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static PositionAssignment Holder(Guid positionId, Guid userId, int endedDaysAgo = 0)
        => new()
        {
            TenantId = TaskTestData.Tenant,
            PositionId = positionId,
            UserId = userId,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30),
            EffectiveTo = endedDaysAgo > 0 ? DateTimeOffset.UtcNow.AddDays(-endedDaysAgo) : null
        };

    /// <summary>The REAL notification service; only the resolver and the notification layer are doubles.</summary>
    private sealed class Harness
    {
        public Harness(
            ITaskNotificationRecipientResolver resolver,
            FakePositionAssignmentRepository? positionAssignments = null)
        {
            RawResolver = resolver;
            Adapter = new RecordingNotificationDispatchAdapter();
            Service = new TaskNotificationService(
                Adapter,
                new FakeNotificationLocaleResolver(),
                resolver,
                positionAssignments ?? new FakePositionAssignmentRepository(),
                new FakeUserNotificationRepository(),
                new FakeTenantContext(TaskTestData.Tenant),
                NullLogger<TaskNotificationService>.Instance);

            Task = new TaskItem
            {
                Id = Guid.NewGuid(),
                TenantId = TaskTestData.Tenant,
                Title = "Bildirim gönderilecek görev",
                Lifecycle = TaskLifecycle.InProgress,
                AssignmentTarget = TaskAssignmentTarget.Person,
                AssigneeUserId = Assignee,
                CreatedByUserId = Requester,
                OrganizationUnitId = Guid.NewGuid(),
                EmailNotificationsEnabled = true,
                Version = 1
            };
        }

        public ITaskNotificationRecipientResolver RawResolver { get; }

        public FakeTaskNotificationRecipientResolver Resolver => (FakeTaskNotificationRecipientResolver)RawResolver;

        public RecordingNotificationDispatchAdapter Adapter { get; }

        public TaskNotificationService Service { get; }

        public TaskItem Task { get; }

        public IReadOnlyList<NotificationEventDispatchRequestSnapshot> Dispatched
            => Adapter.Requests
                .Select(r => new NotificationEventDispatchRequestSnapshot(r.EventCode, r.To, r.Variables))
                .ToList();

        public Task<TaskNotificationOutcome> NotifyAsync(
            string eventCode, Guid[] candidates, Guid? actingUserId = null)
            => Service.NotifyAsync(
                Task, eventCode, candidates, actingUserId ?? Guid.NewGuid(), CancellationToken.None);
    }

    /// <summary>Flattens the dispatch request so assertions read as sentences rather than as property chains.</summary>
    private sealed record NotificationEventDispatchRequestSnapshot(
        string EventCode,
        IReadOnlyList<EmailRecipientDto> To,
        IReadOnlyDictionary<string, object?> Variables);

    private sealed class ThrowingRecipientResolver : ITaskNotificationRecipientResolver
    {
        public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => throw new InvalidOperationException("AuthService is unreachable.");
    }
}
