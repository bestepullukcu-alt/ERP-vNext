using System.Reflection;
using Diten.Platform.API.Controllers;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Entities.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-025 — the notification bell gets something true to show.
///
/// <para><b>The defect at the centre.</b> "My unread notifications" could not be asked. There was one channel
/// (<c>Email</c>), the only notification record was MESSAGE-shaped — <c>NotificationDispatch</c>, whose
/// recipients are <c>{ Email, DisplayName }</c> with no user id anywhere — and no concept of "read" existed in
/// the repository at all. Meanwhile the user id WAS resolved one step upstream, in
/// <c>TaskNotificationRecipient(UserId, Email, DisplayName)</c>, and dropped on the floor the line before the
/// send. This file measures that it now lands in a record instead.</para>
///
/// <para><b>Every guard here carries its own non-vacuity check.</b> A scoping test where nobody has any
/// notifications passes whether the scope works or is missing entirely — "returned nothing" and "filtered
/// correctly" are the same observation. So each isolation test gives BOTH people a real row and asserts the
/// other person's row exists and was withheld, not merely that it was absent.</para>
/// </summary>
public sealed class UserNotificationTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    /// <summary>A, the caller.</summary>
    private static readonly Guid Alice = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    /// <summary>B, somebody else in the same tenant whose notifications A must never see.</summary>
    private static readonly Guid Bob = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static readonly Guid Actor = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    // ══ 1. The write: the user id stops being dropped ═══════════════════════════════════════════════

    [Fact]
    public async Task Assigning_a_task_writes_an_in_app_record_carrying_the_recipient_s_OWN_user_id()
    {
        /*
         * ⚠ THE POINT OF THE WHOLE ROUND. The e-mail path already worked and still does; what it could never
         * produce was a row somebody could later ask for by identity. The id asserted here is the one that
         * used to be discarded by `new EmailRecipientDto(r.Email, r.DisplayName)`.
         */
        var harness = new WriteHarness((Alice, "alice@example.test"));

        var outcome = await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Alice]);

        Assert.Equal(TaskNotificationOutcome.Dispatched, outcome);

        var written = Assert.Single(harness.InApp.Written);
        Assert.Equal(Alice, written.UserId);
        Assert.Equal(Tenant, written.TenantId);
        Assert.Equal(TaskNotificationEvents.Assigned, written.EventCode);
        Assert.Equal(harness.Task.Title, written.Title);
        Assert.Null(written.ReadAt);

        // Non-vacuity for "carries the right id": the id is a REAL user id, not an empty guid that would
        // satisfy an equality against a default-initialised field.
        Assert.NotEqual(Guid.Empty, written.UserId);

        // And the e-mail channel is untouched — this is a second channel beside it, not a replacement.
        Assert.Single(harness.Adapter.Requests);
        Assert.Equal("alice@example.test", harness.Adapter.Requests.Single().To.Single().Email);
    }

    [Theory]
    [InlineData(TaskNotificationEvents.Assigned)]
    [InlineData(TaskNotificationEvents.Claimed)]
    [InlineData(TaskNotificationEvents.DueSoon)]
    [InlineData(TaskNotificationEvents.Completed)]
    [InlineData(TaskNotificationEvents.ApprovalRequested)]
    [InlineData(TaskNotificationEvents.Commented)]
    public async Task ALL_SIX_task_events_write_an_in_app_record(string eventCode)
    {
        /*
         * The write lives in the ONE place all six events already funnel through, so all six are earned by a
         * single change rather than by six call-site edits — the fourth of which is the one that forgets.
         * Parameterised so a future event routed around this method shows up as a missing case here.
         */
        var harness = new WriteHarness((Alice, "alice@example.test"));

        await harness.NotifyAsync(eventCode, [Alice]);

        var written = Assert.Single(harness.InApp.Written);
        Assert.Equal(eventCode, written.EventCode);
        Assert.Equal(Alice, written.UserId);
    }

    // ══ 2. The write honours the filters that were already there ════════════════════════════════════

    [Fact]
    public async Task With_notifications_switched_OFF_no_in_app_record_is_written_either()
    {
        /*
         * ⚠ THE FILTER MUST NOT BE HALF-APPLIED. The master switch is named EmailNotificationsEnabled, which
         * makes "it only meant e-mail" a plausible-sounding reading — and under that reading a user who
         * switched notifications off would start getting bell items instead. The switch means the task, not
         * the transport. Placing the write AFTER TaskNotificationPolicy is what makes that true.
         */
        var harness = new WriteHarness((Alice, "alice@example.test"));
        harness.Task.EmailNotificationsEnabled = false;

        var outcome = await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Alice]);

        Assert.Equal(TaskNotificationOutcome.Skipped, outcome);
        Assert.Empty(harness.InApp.Written);
        Assert.Empty(harness.Adapter.Requests);
    }

    [Fact]
    public async Task Non_vacuity_for_the_switch_the_SAME_task_with_it_on_writes_one()
    {
        /*
         * Without this, the test above passes on an implementation that never writes anything at all. Same
         * harness, same event, same audience — only the switch differs.
         */
        var harness = new WriteHarness((Alice, "alice@example.test"));
        harness.Task.EmailNotificationsEnabled = true;

        await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Alice]);

        Assert.Single(harness.InApp.Written);
    }

    [Fact]
    public async Task An_EMPTY_per_event_preference_writes_nothing_while_NULL_writes_everything()
    {
        // The second existing filter, both directions in one test so neither can pass by accident.
        // Null = the owner never chose = every event, which is what every task written before BL-065 carries.
        var never = new WriteHarness((Alice, "alice@example.test"));
        never.Task.NotifyOnEvents = [];
        await never.NotifyAsync(TaskNotificationEvents.Assigned, [Alice]);
        Assert.Empty(never.InApp.Written);

        var always = new WriteHarness((Alice, "alice@example.test"));
        always.Task.NotifyOnEvents = null;
        await always.NotifyAsync(TaskNotificationEvents.Assigned, [Alice]);
        Assert.Single(always.InApp.Written);

        // And a list that names OTHER events still excludes this one — an empty list is a choice, not a bug.
        var others = new WriteHarness((Alice, "alice@example.test"));
        others.Task.NotifyOnEvents = [TaskNotificationEvents.Completed];
        await others.NotifyAsync(TaskNotificationEvents.Assigned, [Alice]);
        Assert.Empty(others.InApp.Written);
    }

    [Fact]
    public async Task The_ACTOR_gets_no_in_app_record_for_their_own_action_but_the_others_still_do()
    {
        /*
         * The third existing filter. Non-vacuity is built in: Bob is in the same audience and MUST get a row,
         * so "nobody got one" cannot masquerade as "the actor was excluded".
         */
        var harness = new WriteHarness((Alice, "alice@example.test"), (Bob, "bob@example.test"));

        await harness.NotifyAsync(TaskNotificationEvents.Completed, [Alice, Bob], actingUserId: Alice);

        var written = Assert.Single(harness.InApp.Written);
        Assert.Equal(Bob, written.UserId);
        Assert.DoesNotContain(harness.InApp.Written, x => x.UserId == Alice);
    }

    [Fact]
    public async Task Every_resolved_recipient_gets_a_row_of_their_own()
    {
        // Person-shaped, not message-shaped: two readers are two records with two independent read states,
        // where the e-mail channel would produce ONE dispatch carrying two addresses.
        var harness = new WriteHarness((Alice, "alice@example.test"), (Bob, "bob@example.test"));

        await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Alice, Bob]);

        Assert.Equal(2, harness.InApp.Written.Count);
        Assert.Contains(harness.InApp.Written, x => x.UserId == Alice);
        Assert.Contains(harness.InApp.Written, x => x.UserId == Bob);
        // One dispatch, two recipients — the e-mail channel's shape, unchanged.
        Assert.Single(harness.Adapter.Requests);
        Assert.Equal(2, harness.Adapter.Requests.Single().To.Count);
    }

    [Fact]
    public async Task An_in_app_write_that_FAILS_never_costs_the_e_mail()
    {
        /*
         * The rule the whole feature hangs on, extended to the new channel: a notification never fails the
         * write that triggered it, and now also never fails the OTHER channel. Without the dedicated guard
         * around the in-app write, a throwing repository would be caught by the outer handler and the e-mail
         * would silently never be attempted.
         */
        var harness = new WriteHarness((Alice, "alice@example.test"));
        harness.InApp.ThrowOnCreate = new InvalidOperationException("mongo is unreachable");

        var outcome = await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Alice]);

        Assert.Equal(TaskNotificationOutcome.Dispatched, outcome);
        Assert.Single(harness.Adapter.Requests);
        Assert.Equal("alice@example.test", harness.Adapter.Requests.Single().To.Single().Email);
    }

    // ══ 3. The read: scope is the token's, never the request's ══════════════════════════════════════

    [Fact]
    public async Task A_s_request_returns_ONLY_A_s_notifications_even_though_B_has_some_too()
    {
        /*
         * ⚠ BOTH PEOPLE HAVE A ROW. With only A's row present, an implementation that ignores the user scope
         * entirely returns exactly the same answer as one that applies it, and this test would certify the
         * broken one. B's row existing — and being withheld — is what makes the assertion mean anything.
         */
        var store = new FakeUserNotificationRepository();
        await store.CreateAsync(Row(Alice, "A's own task"));
        await store.CreateAsync(Row(Bob, "B's own task"));

        var response = await Read(store, asUser: Alice);

        Assert.True(response.IsSuccessful);
        var page = response.Data!;
        var mine = Assert.Single(page.Items);
        Assert.Equal("A's own task", mine.Title);
        Assert.Equal(1L, page.UnreadCount);

        // Non-vacuity, stated rather than assumed: B's row IS in the store and IS visible to B.
        Assert.Equal(2, store.Written.Count);
        var bobsPage = (await Read(store, asUser: Bob)).Data!;
        Assert.Equal("B's own task", Assert.Single(bobsPage.Items).Title);
    }

    [Fact]
    public async Task Sending_B_s_identity_in_the_REQUEST_changes_nothing()
    {
        /*
         * The attack the design forecloses, measured two ways.
         *
         * (1) Structurally: the query carries no field a client could put an id in. Reflection rather than
         *     reading the source, so adding one later goes red here.
         * (2) Functionally: the ONE thing a caller controls — paging — is exercised with B's id present in the
         *     store, and A still gets only A's.
         */
        var identityCarrying = typeof(GetMyNotificationsQuery)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name => name.Contains("User", StringComparison.OrdinalIgnoreCase)
                           || name.Contains("Tenant", StringComparison.OrdinalIgnoreCase)
                           || name.Contains("Subject", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(identityCarrying.Length == 0,
            "GetMyNotificationsQuery gained a bindable identity field — a caller could then name whose "
            + "notifications to read: " + string.Join(", ", identityCarrying));

        // Non-vacuity for the reflection above: the query DOES expose properties, so an empty result is not
        // the reflection call quietly seeing nothing.
        Assert.NotEmpty(typeof(GetMyNotificationsQuery)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance));

        // Same for the controller: no action takes a user id in the route, query or body.
        var identityParameters = typeof(MyNotificationsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetParameters().Select(p => $"{m.Name}({p.Name})"))
            .Where(name => name.Contains("user", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(identityParameters.Length == 0,
            "MyNotificationsController accepts a user id from the request: "
            + string.Join(", ", identityParameters));

        var store = new FakeUserNotificationRepository();
        await store.CreateAsync(Row(Bob, "B's first"));
        await store.CreateAsync(Row(Bob, "B's second"));
        await store.CreateAsync(Row(Alice, "A's only"));

        // A asks for a page big enough to hold everybody's rows. It still holds only A's.
        var page = (await Read(store, asUser: Alice, pageSize: 50)).Data!;

        Assert.Equal("A's only", Assert.Single(page.Items).Title);
        // Non-vacuity: there really were three rows to over-return.
        Assert.Equal(3, store.Written.Count);
    }

    [Fact]
    public async Task Another_TENANT_s_row_for_the_same_person_is_not_mine_either()
    {
        // The same user id in a different tenant is a different person as far as this inbox is concerned.
        var store = new FakeUserNotificationRepository();
        await store.CreateAsync(Row(Alice, "here", tenantId: Tenant));
        await store.CreateAsync(Row(Alice, "elsewhere", tenantId: OtherTenant));

        var page = (await Read(store, asUser: Alice)).Data!;

        Assert.Equal("here", Assert.Single(page.Items).Title);
        Assert.Equal(2, store.Written.Count);
    }

    [Fact]
    public async Task Unread_comes_first_and_newest_first_inside_each_group()
    {
        var store = new FakeUserNotificationRepository();
        var old = Row(Alice, "old unread", createdAt: DateTimeOffset.UtcNow.AddHours(-3));
        var recent = Row(Alice, "recent unread", createdAt: DateTimeOffset.UtcNow.AddHours(-1));
        // NEWEST of the three, and still last: read loses to unread whatever its age.
        var read = Row(Alice, "already read", createdAt: DateTimeOffset.UtcNow);
        Assert.True(read.TryMarkRead(DateTimeOffset.UtcNow));

        await store.CreateAsync(old);
        await store.CreateAsync(recent);
        await store.CreateAsync(read);

        var page = (await Read(store, asUser: Alice)).Data!;

        Assert.Equal(
            new[] { "recent unread", "old unread", "already read" },
            page.Items.Select(x => x.Title).ToArray());
        Assert.Equal(2L, page.UnreadCount);
        Assert.True(page.Items[2].IsRead);
        Assert.False(page.Items[0].IsRead);
    }

    [Fact]
    public async Task The_unread_count_is_the_WHOLE_inbox_not_the_page()
    {
        /*
         * The invented-count defect, forestalled. A badge fed by counting unread rows in a page of 1 would
         * say "1" when the truth is 3 — the same class of lie as the theme's hard-coded 8.
         */
        var store = new FakeUserNotificationRepository();
        await store.CreateAsync(Row(Alice, "one"));
        await store.CreateAsync(Row(Alice, "two"));
        await store.CreateAsync(Row(Alice, "three"));

        var page = (await Read(store, asUser: Alice, pageSize: 1)).Data!;

        Assert.Single(page.Items);
        Assert.Equal(3L, page.UnreadCount);
    }

    // ══ 4. Marking read is scoped the same way ══════════════════════════════════════════════════════

    [Fact]
    public async Task A_cannot_mark_B_s_notification_read()
    {
        var store = new FakeUserNotificationRepository();
        var bobs = Row(Bob, "B's own task");
        await store.CreateAsync(bobs);

        var response = await MarkRead(store, asUser: Alice, notificationId: bobs.Id);

        Assert.True(response.IsSuccessful);
        Assert.Equal(0L, response.Data!.MarkedCount);
        Assert.Null(bobs.ReadAt);

        // Non-vacuity: the very same id, asked for by its OWNER, does get marked.
        var owner = await MarkRead(store, asUser: Bob, notificationId: bobs.Id);
        Assert.Equal(1L, owner.Data!.MarkedCount);
        Assert.NotNull(bobs.ReadAt);
    }

    [Fact]
    public async Task Marking_read_twice_does_not_move_the_timestamp()
    {
        var store = new FakeUserNotificationRepository();
        var mine = Row(Alice, "A's own task");
        await store.CreateAsync(mine);

        await MarkRead(store, asUser: Alice, notificationId: mine.Id);
        var firstRead = mine.ReadAt;
        var again = await MarkRead(store, asUser: Alice, notificationId: mine.Id);

        Assert.Equal(0L, again.Data!.MarkedCount);
        Assert.Equal(firstRead, mine.ReadAt);
        Assert.NotNull(firstRead);
    }

    [Fact]
    public async Task Read_all_marks_only_MINE()
    {
        var store = new FakeUserNotificationRepository();
        var mineA = Row(Alice, "A one");
        var mineB = Row(Alice, "A two");
        var bobs = Row(Bob, "B one");
        await store.CreateAsync(mineA);
        await store.CreateAsync(mineB);
        await store.CreateAsync(bobs);

        var response = await MarkAllRead(store, asUser: Alice);

        Assert.Equal(2L, response.Data!.MarkedCount);
        Assert.NotNull(mineA.ReadAt);
        Assert.NotNull(mineB.ReadAt);
        // Non-vacuity: B's row was there the whole time, and is still unread.
        Assert.Null(bobs.ReadAt);
        Assert.Equal(1L, await store.CountUnreadForUserAsync(Tenant, Bob));
    }

    // ══ 5. The read state is one fact in two shapes, and they never drift ═══════════════════════════

    [Fact]
    public async Task ReadAt_and_IsRead_are_never_allowed_to_disagree()
    {
        /*
         * ⚠ THE PRICE OF A SORTABLE READ STATE, PINNED.
         *
         * The design that wants to exist is "ReadAt == null means unread" — one field, nothing to drift.
         * Mongo cannot sort on it: BL-030 leaves every DateTimeOffset stored as a BSON array, and
         * {ReadAt, CreatedAt} is two arrays, which the server rejects both as a sort ("parallel arrays") and
         * as a compound index. DateTimeOffsetSortGuardTests catches it in CI — it caught exactly this while
         * this feature was being written, after every fake-repository test had already gone green.
         *
         * So IsRead exists as a materialised mirror, and a mirror is only safe while nothing can set it
         * alone. Every transition below is asserted on BOTH fields; if a future edit stamps one without the
         * other, the row sorts into the wrong group while reporting the right timestamp, and the bell shows
         * a read notification as unread forever.
         */
        var store = new FakeUserNotificationRepository();
        var row = Row(Alice, "A's own task");

        // Born unread, in both shapes.
        Assert.Null(row.ReadAt);
        Assert.False(row.IsRead);

        await store.CreateAsync(row);
        await MarkAllRead(store, asUser: Alice);

        // Read, in both shapes.
        Assert.NotNull(row.ReadAt);
        Assert.True(row.IsRead);

        // And the single-row path agrees with the mark-all path.
        var second = Row(Alice, "another");
        await store.CreateAsync(second);
        await MarkRead(store, asUser: Alice, notificationId: second.Id);
        Assert.NotNull(second.ReadAt);
        Assert.True(second.IsRead);

        // The invariant, stated once over everything the store holds.
        Assert.All(store.Written, x => Assert.Equal(x.ReadAt is not null, x.IsRead));
    }

    // ══ 6. The channel enum ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InApp_is_a_second_channel_and_Email_keeps_its_stored_value()
    {
        /*
         * Values are persisted. Email MUST stay 0 or every stored dispatch, template and event definition
         * silently changes meaning — the failure would not be an error, it would be templates resolving to
         * the wrong channel.
         */
        Assert.Equal(0, (int)NotificationChannelCode.Email);
        Assert.Equal(1, (int)NotificationChannelCode.InApp);
        Assert.Equal(2, Enum.GetValues<NotificationChannelCode>().Length);
    }

    // ══ helpers ═════════════════════════════════════════════════════════════════════════════════════

    private static UserNotification Row(
        Guid userId, string title, Guid? tenantId = null, DateTimeOffset? createdAt = null) => new()
    {
        TenantId = tenantId ?? Tenant,
        UserId = userId,
        EventCode = TaskNotificationEvents.Assigned,
        Title = title,
        Severity = UserNotificationSeverity.Info,
        // CreatedAt is init-only on BaseEntity, so the ordering tests set it here rather than after the fact.
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow
    };

    private static Task<Diten.Platform.Application.Common.Response<UserNotificationPageDto>> Read(
        FakeUserNotificationRepository store, Guid asUser, int page = 1, int pageSize = 20)
        => new GetMyNotificationsHandler(store, new FakeTenantContext(Tenant), new FixedUser(asUser))
            .Handle(new GetMyNotificationsQuery(page, pageSize), CancellationToken.None);

    private static Task<Diten.Platform.Application.Common.Response<UserNotificationReadResultDto>> MarkRead(
        FakeUserNotificationRepository store, Guid asUser, Guid notificationId)
        => new MarkMyNotificationReadHandler(store, new FakeTenantContext(Tenant), new FixedUser(asUser))
            .Handle(new MarkMyNotificationReadCommand(notificationId), CancellationToken.None);

    private static Task<Diten.Platform.Application.Common.Response<UserNotificationReadResultDto>> MarkAllRead(
        FakeUserNotificationRepository store, Guid asUser)
        => new MarkAllMyNotificationsReadHandler(store, new FakeTenantContext(Tenant), new FixedUser(asUser))
            .Handle(new MarkAllMyNotificationsReadCommand(), CancellationToken.None);

    /// <summary>The token, as the handlers see it. Nothing in a request can influence what this answers.</summary>
    private sealed class FixedUser : ICurrentUserContext
    {
        public FixedUser(Guid userId) => UserId = userId;

        public Guid UserId { get; }
        public string? Email => "signed-in@example.test";
        public string? DisplayName => "Signed In";
        public string ActorName => "signed-in@example.test";
        public bool IsAuthenticated => true;
    }

    /// <summary>The real <see cref="TaskNotificationService"/>; only its collaborators are doubles.</summary>
    private sealed class WriteHarness
    {
        public WriteHarness(params (Guid Id, string Email)[] reachable)
        {
            Adapter = new RecordingNotificationDispatchAdapter();
            InApp = new FakeUserNotificationRepository();
            Service = new TaskNotificationService(
                Adapter,
                new FakeNotificationLocaleResolver(),
                new FakeTaskNotificationRecipientResolver(reachable),
                new FakePositionAssignmentRepository(),
                InApp,
                new FakeTenantContext(Tenant),
                NullLogger<TaskNotificationService>.Instance);

            Task = new TaskItem
            {
                Id = Guid.NewGuid(),
                TenantId = Tenant,
                Title = "Bildirim gönderilecek görev",
                Lifecycle = TaskLifecycle.InProgress,
                AssignmentTarget = TaskAssignmentTarget.Person,
                AssigneeUserId = Alice,
                CreatedByUserId = Bob,
                OrganizationUnitId = Guid.NewGuid(),
                EmailNotificationsEnabled = true,
                Version = 1
            };
        }

        public RecordingNotificationDispatchAdapter Adapter { get; }

        public FakeUserNotificationRepository InApp { get; }

        public TaskNotificationService Service { get; }

        public TaskItem Task { get; }

        public Task<TaskNotificationOutcome> NotifyAsync(
            string eventCode, Guid[] candidates, Guid? actingUserId = null)
            => Service.NotifyAsync(
                Task, eventCode, candidates, actingUserId ?? Actor, CancellationToken.None);
    }
}
