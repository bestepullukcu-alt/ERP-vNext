using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// BL-065 — the due-soon reminder, which had a manifest event code and <b>no sender at all</b>.
///
/// <para>Measured before this handler existed: <c>TaskNotificationEvents.DueSoon</c> appeared exactly once in the
/// repository, in the manifest. So "remind me before the deadline" was a promise nothing kept — which is why the
/// preference and the sender had to land together; a preference feeding no sender is the same defect one layer
/// up.</para>
///
/// <para><b>Idempotency lives on the task</b>, not in the scheduler. The sweep runs hourly and a lead window is
/// days wide, so without a claim the same deadline would be announced once an hour for days. Each task carries
/// the key of the deadline it was last reminded about, stamped under an EXPECTED-VERSION write BEFORE the send —
/// the same discipline the recurrence sweep uses. A guard that only lives in the scheduler protects nothing when
/// someone runs the command by hand, and a lost concurrency race must not consume a reminder.</para>
///
/// <para>The key is the DUE DATE, so a postponed task earns a fresh reminder when it reaches its new deadline. A
/// "reminded" boolean would have silenced it permanently.</para>
///
/// <para>Nothing here re-implements notification policy: the send goes through
/// <see cref="ITaskNotificationService"/>, so the master switch, the per-event preference, the recipient resolver
/// and the locale all apply exactly as they do to every other task email.</para>
/// </summary>
public sealed class SendDueSoonRemindersHandler
    : IRequestHandler<SendDueSoonRemindersCommand, Response<SendDueSoonRemindersResponse>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskNotificationService _notifications;
    private readonly ILogger<SendDueSoonRemindersHandler> _logger;

    public SendDueSoonRemindersHandler(
        ITaskItemRepository tasks,
        ITaskNotificationService notifications,
        ILogger<SendDueSoonRemindersHandler> logger)
    {
        _tasks = tasks;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>The deadline a reminder was sent for. Ordinal date: the lead time is counted in whole days.</summary>
    public static string ReminderKey(DateTimeOffset dueAt) => dueAt.UtcDateTime.ToString("yyyy-MM-dd");

    public async Task<Response<SendDueSoonRemindersResponse>> Handle(
        SendDueSoonRemindersCommand command, CancellationToken ct)
    {
        var now = command.NowUtc ?? DateTimeOffset.UtcNow;
        var max = command.MaxTasks <= 0 ? 200 : command.MaxTasks;

        var candidates = (await _tasks.GetAllForTenantAsync(ct))
            .Where(task => IsDue(task, now))
            .Take(max)
            .ToList();

        var sent = 0;
        var alreadyReminded = 0;
        var notDelivered = 0;
        var failed = 0;

        foreach (var task in candidates)
        {
            ct.ThrowIfCancellationRequested();

            var key = ReminderKey(task.DueAt!.Value);
            if (string.Equals(task.LastDueSoonReminderKey, key, StringComparison.Ordinal))
            {
                alreadyReminded++;
                continue;
            }

            /*
             * ASK BEFORE CLAIMING. A claim is spent per deadline and never returned, so stamping one for a task
             * that is then filtered out by the notification policy silences that deadline permanently: untick
             * "due date approaching", let one sweep pass, tick it again — and the reminder for that date can
             * never arrive. With the default lead time preselected, that was the ordinary path, not an edge.
             *
             * The question is asked, not answered here: the rule has one owner (TaskNotificationPolicy, reached
             * through the service), and copying it into IsDue is exactly the mirror this file removed a round ago.
             */
            if (!_notifications.WouldNotify(task, TaskNotificationEvents.DueSoon))
            {
                continue;
            }

            try
            {
                /*
                 * CLAIM FIRST — but the claim is only KEPT once the send lands.
                 *
                 * Claiming before sending is what makes a crash between the two err toward silence rather than
                 * toward a duplicate, and that is still the right way round. What was wrong was keeping the claim
                 * when the send did NOT land: the first live run met PROVIDER_REJECTED, the stamp stayed, and that
                 * deadline could never be reminded again. A claim exists to stop a SECOND send; a refused first
                 * send has not earned it.
                 */
                var expectedVersion = task.Version;
                task.LastDueSoonReminderKey = key;
                if (!await _tasks.UpdateAsync(task, expectedVersion, ct))
                {
                    // A LOST RACE is not a failed send: another sweep owns this deadline and is sending it. Stay
                    // silent, and do not release a claim that belongs to somebody else.
                    alreadyReminded++;
                    continue;
                }

                // The SAME audience rule the create/claim paths use, from the same place — a pool task is owed
                // to whoever currently holds the position, and that resolution is the notification service's.
                var audience = task.AssignmentTarget == TaskAssignmentTarget.PositionPool
                    ? await TaskNotificationSafely.ResolvePoolHoldersAsync(_notifications, _logger, task, ct)
                    : task.AssigneeUserId is { } assignee ? new[] { assignee } : (IReadOnlyCollection<Guid>)[];

                // Guid.Empty as the actor: a scheduled reminder has no acting user to exclude from the audience.
                var outcome = await _notifications.NotifyAsync(
                    task, TaskNotificationEvents.DueSoon, audience, Guid.Empty, ct);

                /*
                 * Every outcome is counted as what it IS. The notification service catches its own transport
                 * errors and reports them as Failed rather than throwing, so a send that blew up reaches here as
                 * an outcome, not as an exception — counting it under "not delivered" would hide a transport
                 * failure among ordinary "nobody to tell" results.
                 */
                switch (outcome)
                {
                    case TaskNotificationOutcome.Dispatched:
                        sent++;
                        break;
                    case TaskNotificationOutcome.Failed:
                        failed++;
                        await ReleaseClaimAsync(task, outcome.ToString(), ct);
                        break;
                    default:   // Skipped, NoRecipients — attempted, nothing delivered.
                        notDelivered++;
                        await ReleaseClaimAsync(task, outcome.ToString(), ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(
                    ex,
                    "task.duesoon.send_failed TaskId={TaskId} DueAt={DueAt}. Releasing the claim so a later sweep "
                    + "retries this deadline.",
                    task.Id, task.DueAt);
                await ReleaseClaimAsync(task, "Exception", ct);
            }
        }

        return Response<SendDueSoonRemindersResponse>.Success(
            new SendDueSoonRemindersResponse(candidates.Count, sent, alreadyReminded, notDelivered, failed),
            correlationId: command.CorrelationId);
    }

    /*
     * Give the deadline back, so the next sweep tries again.
     *
     * Written under expected-version like the claim itself. If THIS write loses — someone edited the task, or
     * another runner is mid-flight — the deadline simply stays claimed, which is exactly the behaviour that
     * shipped before this method existed. So the release can never make things worse than they already were; it
     * can only recover a deadline that was previously lost for good.
     *
     * A bounded "attempted N times then give up" counter was the alternative. It buys nothing here: it needs a
     * second field and a retry policy, and its terminal state is the same permanent loss with more machinery in
     * front of it. Releasing needs one write and reaches the same place — the reminder arrives late instead of
     * never — and the lead window is days wide, so an hourly sweep has many chances inside it.
     */
    private async Task ReleaseClaimAsync(TaskItem task, string reason, CancellationToken ct)
    {
        try
        {
            var expectedVersion = task.Version;
            task.LastDueSoonReminderKey = null;
            if (!await _tasks.UpdateAsync(task, expectedVersion, ct))
            {
                _logger.LogWarning(
                    "task.duesoon.claim_release_lost TaskId={TaskId} DueAt={DueAt} Reason={Reason}. The deadline "
                    + "stays claimed and will not be retried.",
                    task.Id, task.DueAt, reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "task.duesoon.claim_release_failed TaskId={TaskId} DueAt={DueAt} Reason={Reason}.",
                task.Id, task.DueAt, reason);
        }
    }

    /// <summary>
    /// Inside its lead window, still open, and asked to be reminded. A task whose owner never set a lead time is
    /// not reminded at all: the sweep does not invent a default deadline warning nobody asked for.
    /// </summary>
    private static bool IsDue(TaskItem task, DateTimeOffset now)
    {
        if (task.DueAt is not { } dueAt) { return false; }
        if (task.ReminderLeadDays is not { } leadDays || leadDays < 0) { return false; }
        if (task.CompletedAt is not null || task.CancelledAt is not null) { return false; }
        if (task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled) { return false; }

        // Inside the window, and not already past the deadline — an overdue task is a different message, and
        // sending "due soon" after the date would be wrong rather than late.
        var windowOpensAt = dueAt.AddDays(-leadDays);
        return now >= windowOpensAt && now <= dueAt;
    }

}
