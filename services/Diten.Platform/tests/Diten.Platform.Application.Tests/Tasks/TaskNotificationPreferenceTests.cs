using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-065 — per-task notification preferences, all three layers.
///
/// <para>The card in the form asked for two choices: WHICH events send an email, and HOW MANY DAYS before the due
/// date to remind. Neither had anywhere to live: the task carried one boolean, and — measured — <b>nothing in the
/// repository ever dispatched the duesoon event</b>; it existed only as a code in the manifest. So a preference
/// alone would have been a control that writes to a field nobody reads, feeding an email nobody sends.</para>
///
/// <para>What is pinned here: the master switch still wins; a task written BEFORE these fields existed keeps
/// behaving exactly as it does today; the reminder is derived from a NUMBER of days rather than a stored instant
/// (BL-030: a stored DateTimeOffset serialises as an array and breaks the query); and the same reminder is never
/// sent twice, guarded on the task itself the way the recurrence claim is — not in the scheduler, which protects
/// nothing when the command is run by hand.</para>
/// </summary>
public sealed class TaskNotificationPreferenceTests
{
    private static readonly Guid Assignee = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Actor = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid Unit = Guid.Parse("0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f");
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

    // ── Layer 3a — the service reads the preference ─────────────────────────────────────────────────────────

    [Fact]
    public async Task An_event_the_task_did_not_ask_for_is_not_sent()
    {
        var harness = Notifications(notifyOn: [TaskNotificationEvents.Completed]);

        var outcome = await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]);

        Assert.Equal(TaskNotificationOutcome.Skipped, outcome);
        Assert.Empty(harness.Dispatched);
    }

    [Fact]
    public async Task An_event_the_task_asked_for_is_sent()
    {
        // Non-vacuity for the case above: the SAME harness sends when the event is listed.
        var harness = Notifications(notifyOn: [TaskNotificationEvents.Assigned]);

        var outcome = await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]);

        Assert.Equal(TaskNotificationOutcome.Dispatched, outcome);
        Assert.Equal(TaskNotificationEvents.Assigned, harness.Dispatched.Single().EventCode);
    }

    [Fact]
    public async Task A_task_written_before_the_preference_existed_still_notifies()
    {
        /*
         * The back-compat rule, and the reason the field is NULLABLE rather than an empty list by default: a
         * legacy document has no such field, deserialises to null, and null means "never chosen" → every event
         * still goes out, exactly as today. An empty list would have made every existing task go silent on
         * deploy, which is a data migration disguised as a default value.
         */
        var harness = Notifications(notifyOn: null);

        Assert.Equal(TaskNotificationOutcome.Dispatched,
            await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]));
    }

    [Fact]
    public async Task Choosing_NO_events_is_respected_and_is_not_the_same_as_never_choosing()
    {
        var harness = Notifications(notifyOn: []);

        Assert.Equal(TaskNotificationOutcome.Skipped,
            await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]));
    }

    [Fact]
    public async Task The_master_switch_still_wins_over_any_preference()
    {
        var harness = Notifications(notifyOn: [TaskNotificationEvents.Assigned], emailEnabled: false);

        Assert.Equal(TaskNotificationOutcome.Skipped,
            await harness.NotifyAsync(TaskNotificationEvents.Assigned, [Assignee]));
        Assert.Empty(harness.Dispatched);
    }

    // ── Layer 2 — the preferences survive the round trip ────────────────────────────────────────────────────

    [Fact]
    public async Task An_edit_stores_the_chosen_events_and_lead_days()
    {
        var task = PlainTask();
        var (handler, tasks) = UpdateHandler(task);

        var response = await handler.Handle(
            Update(task, notifyOn: [TaskNotificationEvents.Assigned, TaskNotificationEvents.DueSoon], leadDays: 3),
            CancellationToken.None);

        Assert.Equal(204, response.StatusCode);
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal([TaskNotificationEvents.Assigned, TaskNotificationEvents.DueSoon], stored!.NotifyOnEvents);
        Assert.Equal(3, stored.ReminderLeadDays);
    }

    [Fact]
    public async Task An_edit_that_does_not_mention_the_preferences_leaves_them_alone()
    {
        // The round trip this repository has lost data on three times: a payload that omits a field must not be
        // read as "the user cleared it".
        var task = PlainTask();
        task.NotifyOnEvents = [TaskNotificationEvents.Completed];
        task.ReminderLeadDays = 7;
        var (handler, tasks) = UpdateHandler(task);

        await handler.Handle(Update(task, notifyOn: null, leadDays: null), CancellationToken.None);

        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal([TaskNotificationEvents.Completed], stored!.NotifyOnEvents);
        Assert.Equal(7, stored.ReminderLeadDays);
    }

    [Fact]
    public async Task An_edit_CAN_clear_the_reminder_by_editing_the_block()
    {
        /*
         * "Not editing this" and "clearing this" were the same null, so choosing "no reminder" in the form saved
         * nothing and the sweep kept emailing. The two are told apart by the BLOCK: NotifyOnEvents non-null means
         * the caller is editing the notification preferences, and inside that edit the lead time is applied
         * verbatim — null included.
         */
        var task = PlainTask();
        task.NotifyOnEvents = [TaskNotificationEvents.DueSoon];
        task.ReminderLeadDays = 3;
        var (handler, tasks) = UpdateHandler(task);

        await handler.Handle(
            Update(task, notifyOn: [TaskNotificationEvents.DueSoon], leadDays: null), CancellationToken.None);

        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Null(stored!.ReminderLeadDays);
    }

    [Fact]
    public async Task A_caller_editing_only_the_lead_time_without_the_block_changes_nothing()
    {
        /*
         * The documented cost of reading the block as one unit: an API caller that sends ONLY a lead time is not
         * editing the preferences, so it is ignored rather than half-applied. Such a caller must send the event
         * list it wants kept — which is what the form does on every save.
         */
        var task = PlainTask();
        task.ReminderLeadDays = 3;
        var (handler, tasks) = UpdateHandler(task);

        await handler.Handle(Update(task, notifyOn: null, leadDays: 7), CancellationToken.None);

        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(3, stored!.ReminderLeadDays);
    }

    // ── Layer 3b — the reminder that had no sender at all ───────────────────────────────────────────────────

    [Fact]
    public async Task A_task_due_within_its_lead_time_gets_exactly_one_reminder()
    {
        var task = DueTask(dueInDays: 3, leadDays: 3);
        var (handler, dispatched, tasks) = Reminders(task);

        var first = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Equal(1, first.Data!.RemindersSent);
        Assert.Equal(TaskNotificationEvents.DueSoon, dispatched.Requests.Single().EventCode);

        // The second sweep — an hourly job runs many times inside one lead window.
        var second = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c2"), CancellationToken.None);

        Assert.Equal(0, second.Data!.RemindersSent);
        Assert.Single(dispatched.Requests);
        // The claim lives on the TASK, so a hand-run command is guarded too, not just the scheduler.
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(stored!.LastDueSoonReminderKey));
    }

    [Fact]
    public async Task A_lost_claim_race_sends_NOTHING()
    {
        /*
         * THE ORDER, made observable. The handler claims the deadline under an expected-version write and only
         * then sends; inverting those two lines leaves every other test in this file green, because nothing else
         * can make the conditional write fail. Here it fails on purpose: another sweep owns this deadline, so
         * this one must send no email at all — otherwise the reminder arrives twice, which is the whole reason
         * the claim exists.
         */
        var task = DueTask(dueInDays: 2, leadDays: 3);
        var (handler, dispatched, tasks) = Reminders(task);
        tasks.ForcedUpdateConflicts = 1;

        var result = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Empty(dispatched.Requests);
        Assert.Equal(0, result.Data!.RemindersSent);
        Assert.Equal(1, result.Data.AlreadyReminded);
    }

    [Fact]
    public async Task A_task_still_outside_its_lead_time_is_left_alone()
    {
        var (handler, dispatched, _) = Reminders(DueTask(dueInDays: 10, leadDays: 3));

        var result = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Equal(0, result.Data!.RemindersSent);
        Assert.Empty(dispatched.Requests);
    }

    [Fact]
    public async Task A_task_with_no_lead_days_is_never_reminded()
    {
        // No preference, no reminder: the sweep does not invent a default lead time for tasks whose owner never
        // asked to be reminded.
        var (handler, dispatched, _) = Reminders(DueTask(dueInDays: 1, leadDays: null));

        var result = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Equal(0, result.Data!.RemindersSent);
        Assert.Empty(dispatched.Requests);
    }

    [Fact]
    public async Task A_moved_due_date_earns_a_new_reminder()
    {
        /*
         * The claim is keyed on the DUE DATE, not on "reminded once ever". Postponing a task and then reaching
         * its new deadline is a new event; a task-level boolean would have silenced it forever.
         */
        var task = DueTask(dueInDays: 3, leadDays: 3);
        var (handler, dispatched, tasks) = Reminders(task);

        await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        // The task is postponed in the STORE (a read hands back a detached copy, like Mongo does).
        task.DueAt = Now.AddDays(2);

        var second = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c2"), CancellationToken.None);

        Assert.Equal(1, second.Data!.RemindersSent);
        Assert.Equal(2, dispatched.Requests.Count);
    }

    [Fact]
    public async Task A_completed_task_is_not_reminded_about()
    {
        var task = DueTask(dueInDays: 1, leadDays: 3);
        task.Lifecycle = TaskLifecycle.Done;
        var (handler, dispatched, _) = Reminders(task);

        await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Empty(dispatched.Requests);
    }

    [Fact]
    public async Task The_reminder_goes_through_the_same_service_every_other_event_does()
    {
        /*
         * Not a second notification path. The sweep asks TaskNotificationService, so the master switch, the
         * per-event preference, the recipient resolver and the locale all apply to the reminder for free — and a
         * task that did not ask for duesoon gets none, which is asserted through the real filter above.
         */
        var task = DueTask(dueInDays: 2, leadDays: 3);
        task.NotifyOnEvents = [TaskNotificationEvents.Assigned];   // duesoon deliberately NOT chosen
        var (handler, dispatched, _) = Reminders(task);

        var result = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Equal(0, result.Data!.RemindersSent);
        Assert.Empty(dispatched.Requests);
    }

    [Fact]
    public async Task A_task_that_declined_the_reminder_keeps_its_claim_UNSPENT()
    {
        /*
         * The claim was burned by a task that never wanted the email.
         *
         * IsDue only knew about dates and lifecycle; the preference filter lives one layer down, in the
         * notification service — and the stamp was written BEFORE asking it. So unticking "due date approaching"
         * (while the lead time stays at its default 3 days, because the hidden control still posts its value)
         * stamped the deadline, sent nothing, and any later change of mind was permanently too late for THAT
         * deadline. Not an edge case: the default lead time is preselected, so this is the ordinary path.
         */
        var task = DueTask(dueInDays: 2, leadDays: 3);
        task.NotifyOnEvents = [TaskNotificationEvents.Assigned];   // duesoon deliberately declined
        var (handler, dispatched, tasks) = Reminders(task);

        var result = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Empty(dispatched.Requests);
        Assert.Equal(0, result.Data!.RemindersSent);
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Null(stored!.LastDueSoonReminderKey);
    }

    [Fact]
    public async Task Changing_your_mind_inside_the_window_still_earns_the_reminder()
    {
        // The consequence that makes the case above matter: the deadline was never claimed, so turning the
        // preference back on inside the same window still produces the email.
        var task = DueTask(dueInDays: 2, leadDays: 3);
        task.NotifyOnEvents = [TaskNotificationEvents.Assigned];
        var (handler, dispatched, _) = Reminders(task);

        await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);
        task.NotifyOnEvents = [TaskNotificationEvents.Assigned, TaskNotificationEvents.DueSoon];

        var second = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c2"), CancellationToken.None);

        Assert.Equal(1, second.Data!.RemindersSent);
        Assert.Equal(TaskNotificationEvents.DueSoon, dispatched.Requests.Single().EventCode);
    }

    [Fact]
    public async Task A_task_with_email_switched_OFF_keeps_its_claim_unspent_too()
    {
        var task = DueTask(dueInDays: 2, leadDays: 3);
        task.EmailNotificationsEnabled = false;
        var (handler, dispatched, tasks) = Reminders(task);

        await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Empty(dispatched.Requests);
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Null(stored!.LastDueSoonReminderKey);

        // And switching it back on inside the window still delivers.
        task.EmailNotificationsEnabled = true;
        var second = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c2"), CancellationToken.None);
        Assert.Equal(1, second.Data!.RemindersSent);
    }

    [Fact]
    public async Task The_notification_DOUBLE_skips_exactly_what_the_real_service_skips()
    {
        /*
         * The double's own comment claimed "the SAME two skip rules the real service does", and it applied ONE:
         * the master switch. The per-event preference — the second rule, and the one BL-065 added — was missing,
         * so thirteen handler test files were measuring a policy that no longer matched production. A double that
         * says "no notification" for a different reason than the system does is worse than a permissive one: the
         * tests read as proof.
         *
         * Asserted as an EQUIVALENCE rather than as two separate expectations, because what matters is not what
         * either one answers but that they cannot disagree.
         */
        var task = DueTask(dueInDays: 2, leadDays: 3);
        task.NotifyOnEvents = [TaskNotificationEvents.Assigned];   // completed deliberately NOT chosen

        var doubleService = new FakeTaskNotificationService();
        var real = new TaskNotificationService(
            new RecordingNotificationDispatchAdapter(),
            new FakeNotificationLocaleResolver(),
            new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")),
            new FakePositionAssignmentRepository(),
            new FakeTenantContext(TaskTestData.Tenant),
            NullLogger<TaskNotificationService>.Instance);

        var fromDouble = await doubleService.NotifyAsync(
            task, TaskNotificationEvents.Completed, [Assignee], Actor, CancellationToken.None);
        var fromReal = await real.NotifyAsync(
            task, TaskNotificationEvents.Completed, [Assignee], Actor, CancellationToken.None);

        Assert.Equal(TaskNotificationOutcome.Skipped, fromReal);
        Assert.Equal(fromReal, fromDouble);
        Assert.Empty(doubleService.EventCodes);
    }

    // ── A + B: a send that did not land must not consume the deadline, and must not read as a clean run ──────

    [Fact]
    public async Task A_REJECTED_send_leaves_the_deadline_unclaimed_and_is_retried()
    {
        /*
         * Measured in production on the first live run: the SMTP provider answered PROVIDER_REJECTED (a dummy
         * credential against a server announcing no AUTH), the claim had already been stamped, and that deadline
         * went silent forever — recoverable only by moving the due date. The file called that a documented
         * trade-off. It is not: the claim exists to prevent a SECOND send, and nothing about a refused first send
         * warrants spending it.
         */
        var task = DueTask(dueInDays: 2, leadDays: 3);
        var (handler, dispatched, tasks) = Reminders(task);
        dispatched.FailWithReasonCode = "PROVIDER_REJECTED";

        var first = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Equal(0, first.Data!.RemindersSent);
        var afterFailure = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Null(afterFailure!.LastDueSoonReminderKey);

        // The provider recovers; the very next sweep delivers the SAME deadline.
        dispatched.FailWithReasonCode = null;
        var second = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c2"), CancellationToken.None);

        Assert.Equal(1, second.Data!.RemindersSent);
        var afterSuccess = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal(SendDueSoonRemindersHandler.ReminderKey(task.DueAt!.Value), afterSuccess!.LastDueSoonReminderKey);
    }

    [Fact]
    public async Task A_send_that_THROWS_also_leaves_the_deadline_unclaimed()
    {
        var task = DueTask(dueInDays: 2, leadDays: 3);
        var (handler, _, tasks) = Reminders(task, throwOnDispatch: true);

        var result = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Equal(0, result.Data!.RemindersSent);
        Assert.Equal(1, result.Data.Failed);
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Null(stored!.LastDueSoonReminderKey);
    }

    [Fact]
    public async Task A_DELIVERED_reminder_is_still_sent_only_once()
    {
        // The regression that matters: releasing the claim on failure must not release it on success.
        var task = DueTask(dueInDays: 2, leadDays: 3);
        var (handler, dispatched, _) = Reminders(task);

        await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);
        var second = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c2"), CancellationToken.None);

        Assert.Single(dispatched.Requests);
        Assert.Equal(0, second.Data!.RemindersSent);
        Assert.Equal(1, second.Data.AlreadyReminded);
    }

    [Fact]
    public async Task A_sweep_that_delivered_NOTHING_does_not_report_a_clean_run()
    {
        /*
         * The counters said Sent=0 AlreadyReminded=0 FailedTasks=0 FailedTenants=0 on the run that lost a
         * reminder — four numbers, all of them "nothing to see". Only Dispatched was counted as a send and only
         * an exception as a failure, so every other outcome fell through every counter. A sweep that loses work
         * and reports a clean run is the same defect this area has produced five times: reporting success it did
         * not have.
         */
        var task = DueTask(dueInDays: 2, leadDays: 3);
        var (handler, dispatched, _) = Reminders(task);
        dispatched.FailWithReasonCode = "PROVIDER_REJECTED";

        var result = await handler.Handle(new SendDueSoonRemindersCommand(Now, 100, "c1"), CancellationToken.None);

        Assert.Equal(1, result.Data!.TasksConsidered);
        Assert.Equal(0, result.Data.RemindersSent);
        /*
         * Which loss bucket a provider refusal lands in is the handler's business (it reports it as the outcome
         * the notification service returned). What this test holds is the property that was violated: a
         * considered task cannot vanish between the counters.
         */
        Assert.True(result.Data.NotDelivered + result.Data.Failed == 1,
            $"a refused send was counted as neither: NotDelivered={result.Data.NotDelivered} "
            + $"Failed={result.Data.Failed}");
        Assert.Equal(
            result.Data.TasksConsidered,
            result.Data.RemindersSent + result.Data.AlreadyReminded + result.Data.NotDelivered + result.Data.Failed);
    }

    // ── the test double's own fidelity ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_read_hands_back_a_document_whose_COLLECTIONS_are_detached_too()
    {
        /*
         * The double promises what Mongo does: every read is a fresh document. Reflection-copying properties kept
         * that promise for scalars and broke it for lists — Tags and FieldValues were the SAME object, so a
         * handler appending to its copy would reach into the stored task with no write at all. Harmless while
         * every test happened to change only scalars, which is precisely the kind of "harmless" that stops being
         * harmless without warning.
         */
        var task = PlainTask();
        task.Tags = ["kalite"];
        var repository = new FakeTaskItemRepository(task);

        var read = repository.GetByIdAsync(task.Id, CancellationToken.None).Result!;
        read.Tags.Add("sızıntı");
        read.FieldValues.Add(new TaskFieldValue
        {
            DefinitionCode = "leak", ValueType = TaskFieldValueType.Text, Value = "x"
        });

        Assert.Equal(["kalite"], repository.Items.Single().Tags);
        Assert.Empty(repository.Items.Single().FieldValues);
    }

    [Fact]
    public void A_read_detaches_the_ELEMENTS_too_not_only_the_list()
    {
        /*
         * Re-allocating the list is not detachment when its elements are mutable. TaskFieldValue's Value,
         * Redacted and AccessState all have setters, so a handler editing a value IN PLACE on its own copy still
         * reached the stored document — with no write, no version check and nothing to see. Tags escape this only
         * because strings cannot be edited in place.
         */
        var task = PlainTask();
        task.FieldValues =
        [
            new TaskFieldValue { DefinitionCode = "note", ValueType = TaskFieldValueType.Text, Value = "orijinal" }
        ];
        var repository = new FakeTaskItemRepository(task);

        var read = repository.GetByIdAsync(task.Id, CancellationToken.None).Result!;
        read.FieldValues[0].Value = "sızıntı";

        Assert.Equal("orijinal", repository.Items.Single().FieldValues[0].Value);
    }

    [Fact]
    public void A_field_value_survives_detachment_with_its_CLASSIFICATION_intact()
    {
        /*
         * The concrete loss. Classification carries the BL-024 rule that decides whether a value may reach the
         * browser at all — alongside Redacted — so a copy that drops it turns a Confidential value into a Normal
         * one on the way out of the store. (The enum's values are Normal/Internal/Confidential/Restricted; there
         * is no "Sensitive".)
         */
        var task = PlainTask();
        task.FieldValues =
        [
            new TaskFieldValue
            {
                DefinitionCode = "salary",
                ValueType = TaskFieldValueType.Text,
                Value = "gizli",
                Classification = TaskFieldClassification.Confidential,
                AccessState = TaskFieldAccessState.Masked,
                Redacted = true
            }
        ];
        var repository = new FakeTaskItemRepository(task);

        var read = repository.GetByIdAsync(task.Id, CancellationToken.None).Result!;

        Assert.Equal(TaskFieldClassification.Confidential, read.FieldValues[0].Classification);
    }

    [Fact]
    public void EVERY_writable_member_of_a_field_value_survives_detachment()
    {
        /*
         * The real finding was not one dropped member but a DISCIPLINE GAP inside one file: CopyWritableFields
         * carries new properties automatically because it uses reflection, while the element clone listed its
         * members by hand — so the promise held for today's shape and would quietly stop holding for the next
         * property anyone adds. Classification was simply the first to prove it.
         *
         * So the member list is DERIVED here rather than restated: every writable property is given a value that
         * differs from its default, and every one must come back. Add a property to TaskFieldValue and forget the
         * clone, and this fails on the next run instead of on a future incident.
         */
        var probe = new TaskFieldValue { DefinitionCode = "probe", ValueType = TaskFieldValueType.Number };
        var writable = typeof(TaskFieldValue)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true })
            .ToList();
        Assert.NotEmpty(writable);

        foreach (var property in writable)
        {
            property.SetValue(probe, DistinctValueFor(property.PropertyType, property.GetValue(probe)));
        }

        var task = PlainTask();
        task.FieldValues = [probe];
        var repository = new FakeTaskItemRepository(task);

        var read = repository.GetByIdAsync(task.Id, CancellationToken.None).Result!.FieldValues[0];

        foreach (var property in writable)
        {
            Assert.Equal(property.GetValue(probe)?.ToString(), property.GetValue(read)?.ToString());
        }
    }

    /// <summary>A value of the right type that is NOT what the property already holds, so a dropped copy shows.</summary>
    private static object DistinctValueFor(Type type, object? current)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;

        if (target == typeof(string)) { return $"detach-probe-{Guid.NewGuid():N}"; }
        if (target == typeof(bool)) { return !(current as bool? ?? false); }
        if (target.IsEnum)
        {
            return Enum.GetValues(target).Cast<object>()
                .First(value => !Equals(value, current));
        }

        throw new NotSupportedException(
            $"TaskFieldValue gained a {target.Name} member; teach this probe how to vary it rather than "
            + "narrowing what the test covers.");
    }

    [Fact]
    public void A_SUCCESSFUL_write_does_not_hand_the_store_the_caller_s_own_lists()
    {
        // The other half: after a conditional write the double copied the caller's list REFERENCES into storage,
        // so everything the caller did to them afterwards silently became stored state.
        var task = PlainTask();
        task.FieldValues =
        [
            new TaskFieldValue { DefinitionCode = "note", ValueType = TaskFieldValueType.Text, Value = "orijinal" }
        ];
        var repository = new FakeTaskItemRepository(task);

        var edit = repository.GetByIdAsync(task.Id, CancellationToken.None).Result!;
        edit.FieldValues[0].Value = "kaydedilen";
        Assert.True(repository.UpdateAsync(edit, edit.Version, CancellationToken.None).Result);

        // The write landed…
        Assert.Equal("kaydedilen", repository.Items.Single().FieldValues[0].Value);
        // …and the caller's later edits are NOT stored state.
        edit.FieldValues[0].Value = "yazılmadı";
        Assert.Equal("kaydedilen", repository.Items.Single().FieldValues[0].Value);
    }

    // ── harnesses ───────────────────────────────────────────────────────────────────────────────────────────

    private static NotificationHarness Notifications(
        IReadOnlyList<string>? notifyOn, bool emailEnabled = true)
        => new(notifyOn, emailEnabled);

    private sealed class NotificationHarness
    {
        private readonly TaskNotificationService _service;

        public NotificationHarness(IReadOnlyList<string>? notifyOn, bool emailEnabled)
        {
            Adapter = new RecordingNotificationDispatchAdapter();
            _service = new TaskNotificationService(
                Adapter,
                new FakeNotificationLocaleResolver(),
                new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")),
                new FakePositionAssignmentRepository(),
                new FakeTenantContext(TaskTestData.Tenant),
                NullLogger<TaskNotificationService>.Instance);

            Task = new TaskItem
            {
                Id = Guid.NewGuid(),
                TenantId = TaskTestData.Tenant,
                Title = "Bildirim tercihi olan görev",
                Lifecycle = TaskLifecycle.InProgress,
                AssignmentTarget = TaskAssignmentTarget.Person,
                AssigneeUserId = Assignee,
                CreatedByUserId = Actor,
                OrganizationUnitId = Unit,
                EmailNotificationsEnabled = emailEnabled,
                NotifyOnEvents = notifyOn,
                Version = 1
            };
        }

        public RecordingNotificationDispatchAdapter Adapter { get; }

        public TaskItem Task { get; }

        public IReadOnlyList<NotificationEventDispatchRequest> Dispatched => Adapter.Requests;

        public Task<TaskNotificationOutcome> NotifyAsync(string eventCode, Guid[] candidates)
            => _service.NotifyAsync(Task, eventCode, candidates, Actor, CancellationToken.None);
    }

    private static (UpdateTaskItemHandler Handler, FakeTaskItemRepository Tasks) UpdateHandler(TaskItem task)
    {
        var tasks = new FakeTaskItemRepository(task);
        var handler = new UpdateTaskItemHandler(
            tasks,
            new FakeOrganizationUnitRepository(new OrganizationUnit
            {
                Id = Unit,
                TenantId = TaskTestData.Tenant,
                Name = "HQ",
                Code = "HQ",
                LegalEntityId = Guid.NewGuid()
            }),
            new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
            new FakeCurrentUserContext(TaskTestData.Me),
            new FakeTaskApprovalService(),
            new FakeTaskReviewService(),
            NullLogger<UpdateTaskItemHandler>.Instance);
        return (handler, tasks);
    }

    private static UpdateTaskItemCommand Update(
        TaskItem task, IReadOnlyList<string>? notifyOn, int? leadDays)
        => new(
            task.Id,
            new UpdateTaskItemRequest(
                Title: task.Title,
                Description: null,
                Priority: TaskPriority.Medium,
                OrganizationUnitId: null,
                DueAt: null,
                StartAt: null,
                PlannedDate: null,
                EstimateHours: null,
                Tags: null,
                ReviewRequired: false,
                EmailNotificationsEnabled: true,
                DelegationAllowed: false,
                FieldValues: null,
                ExpectedVersion: task.Version,
                NotifyOnEvents: notifyOn,
                ReminderLeadDays: leadDays),
            Guid.NewGuid().ToString());

    /*
     * The sweep is exercised against the REAL TaskNotificationService.
     *
     * The first version of this harness used a stand-in that RE-IMPLEMENTED the master switch and the per-event
     * filter. That made the test named "goes through the same service every other event does" pass while proving
     * the opposite: deleting the production filter left it green, and the copy had already drifted (it compared
     * with the default comparer, production with StringComparer.Ordinal). One fact, one place — so the double is
     * gone and what is recorded now is what the notification layer was actually asked to dispatch.
     */
    private static (SendDueSoonRemindersHandler Handler, RecordingNotificationDispatchAdapter Dispatched,
        FakeTaskItemRepository Tasks) Reminders(TaskItem task, bool throwOnDispatch = false)
    {
        var tasks = new FakeTaskItemRepository(task);
        var adapter = new RecordingNotificationDispatchAdapter { ThrowOnDispatch = throwOnDispatch };
        var notifications = new TaskNotificationService(
            adapter,
            new FakeNotificationLocaleResolver(),
            new FakeTaskNotificationRecipientResolver((Assignee, "alice@example.test")),
            new FakePositionAssignmentRepository(),
            new FakeTenantContext(TaskTestData.Tenant),
            NullLogger<TaskNotificationService>.Instance);

        var handler = new SendDueSoonRemindersHandler(
            tasks,
            notifications,
            NullLogger<SendDueSoonRemindersHandler>.Instance);
        return (handler, adapter, tasks);
    }

    private static TaskItem PlainTask() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = "Write the report",
        Lifecycle = TaskLifecycle.Open,
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Unit,
        EmailNotificationsEnabled = true,
        Version = 1
    };

    private static TaskItem DueTask(int dueInDays, int? leadDays)
    {
        var task = PlainTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.AssigneeUserId = Assignee;
        task.Lifecycle = TaskLifecycle.InProgress;
        task.DueAt = Now.AddDays(dueInDays);
        task.ReminderLeadDays = leadDays;
        return task;
    }
}
