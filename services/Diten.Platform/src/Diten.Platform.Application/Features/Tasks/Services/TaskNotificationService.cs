using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>What a dispatch attempt actually did. Reported so a caller never has to assume it worked.</summary>
public enum TaskNotificationOutcome
{
    /// <summary>Handed to the notification layer for at least one recipient.</summary>
    Dispatched = 0,

    /// <summary>Nothing was attempted: the task opted out, or the only recipient was the actor.</summary>
    Skipped = 1,

    /// <summary>Somebody should have been told and nobody could be reached.</summary>
    NoRecipients = 2,

    /// <summary>The notification layer refused or threw. Logged; never the caller's problem.</summary>
    Failed = 3
}

/// <summary>
/// WC-4 — the ONE place a task notification is sent from.
///
/// <para><b>Why one place.</b> Four events now dispatch, and each carries the same four rules: honour the task's
/// opt-out, skip the actor's own action, resolve real addresses, and never let a notification failure fail the
/// write that triggered it. Written out four times, the fourth copy is the one that forgets a rule — the shape
/// this session has already met with approval's validation and the reviewer requirement.</para>
///
/// <para><b>Language: one dispatch, in the tenant's language — and why not one per reader.</b> Templates are seeded
/// in seven languages, so the obvious shape is to group recipients by language and dispatch per group. It is not
/// implemented, because the input for grouping does not exist: <c>TaskNotificationRecipient</c> carries id, e-mail
/// and display name, and the AuthService <c>User</c> behind it has no language field at all. Grouping on data
/// nobody supplies yields exactly one group every time — the same single dispatch, plus a fan-out path that is
/// never exercised and rots. So this service states what it knows (nothing about the reader's language, passed as
/// <c>Locale: null</c>) and <c>INotificationLocaleResolver</c> answers with the tenant's own configured language.
/// The seven languages are not wasted by that — the tenant picks which one is used; what is still missing is a
/// per-reader choice. Adding one means a language column on <c>User</c>, the <c>internal/users/contacts</c>
/// endpoint returning it, a field on <c>TaskNotificationRecipient</c>, and only then the grouping loop here.</para>
/// </summary>
public interface ITaskNotificationService
{
    /// <summary>
    /// Notify <paramref name="candidateUserIds"/> about <paramref name="eventCode"/> for this task.
    ///
    /// <para><paramref name="actingUserId"/> is EXCLUDED. Telling someone about a thing they just did themselves
    /// is noise, and noise is how people learn to ignore notifications that matter.</para>
    /// </summary>
    Task<TaskNotificationOutcome> NotifyAsync(
        TaskItem task,
        string eventCode,
        IReadOnlyCollection<Guid> candidateUserIds,
        Guid actingUserId,
        CancellationToken ct);

    /// <summary>Everyone currently holding the task's pool position — the "offered to a pool" audience.</summary>
    Task<IReadOnlyList<Guid>> ResolvePoolHoldersAsync(TaskItem task, CancellationToken ct);

    /// <summary>
    /// Would this task send this event at all, on its own settings? Asked by callers that must do something
    /// IRREVERSIBLE before sending — the due-soon sweep stamps a per-deadline claim, and a claim spent on a
    /// notification that was then filtered out silences that deadline forever.
    ///
    /// <para>Audience is deliberately NOT considered: it is the caller's to resolve, and "nobody to tell" is a
    /// different answer from "this task does not want this event".</para>
    ///
    /// <para>A DEFAULT implementation delegating to <see cref="TaskNotificationPolicy"/>, so the rule has exactly
    /// one owner. Restating it in the service — or in a test double — is how the previous copy drifted to a
    /// different string comparer without anything failing.</para>
    /// </summary>
    bool WouldNotify(TaskItem task, string eventCode) => TaskNotificationPolicy.WouldNotify(task, eventCode);
}

/// <summary>
/// The task's OWN answer to "do you want this event?", in one place.
///
/// <para>Two rules, in order: the master switch, then the per-event preference. Null preferences mean the owner
/// never chose, which is what every task written before BL-065 carries and must keep behaving as — every event
/// goes out. An empty list is a choice, and means none.</para>
/// </summary>
public static class TaskNotificationPolicy
{
    public static bool WouldNotify(TaskItem task, string eventCode)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (!task.EmailNotificationsEnabled) { return false; }

        return task.NotifyOnEvents is not { } chosenEvents
               || chosenEvents.Contains(eventCode, StringComparer.Ordinal);
    }
}

/// <summary>
/// Calls <see cref="ITaskNotificationService"/> and swallows anything it throws.
///
/// <para><b>Why a call-site guard when the service already catches.</b> Because "a notification never fails the
/// write" is a rule about the CALLER, and a rule that lives only inside the callee is one refactor away from
/// being gone. A handler-level test proved this: with a throwing implementation, task completion failed —
/// the write's safety depended entirely on the service's internal try/catch. It does not any more.</para>
/// </summary>
public static class TaskNotificationSafely
{
    public static async Task NotifyAsync(
        ITaskNotificationService notifications,
        ILogger logger,
        TaskItem task,
        string eventCode,
        IReadOnlyCollection<Guid> candidateUserIds,
        Guid actingUserId,
        CancellationToken ct)
    {
        try
        {
            await notifications.NotifyAsync(task, eventCode, candidateUserIds, actingUserId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "task.notification.call_failed EventCode={EventCode} TaskId={TaskId}", eventCode, task.Id);
        }
    }

    /// <summary>Pool holders, or an empty audience when the lookup itself fails. Same rule, same reason.</summary>
    public static async Task<IReadOnlyList<Guid>> ResolvePoolHoldersAsync(
        ITaskNotificationService notifications,
        ILogger logger,
        TaskItem task,
        CancellationToken ct)
    {
        try
        {
            return await notifications.ResolvePoolHoldersAsync(task, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "task.notification.pool_lookup_failed TaskId={TaskId}", task.Id);
            return [];
        }
    }
}

public sealed class TaskNotificationService : ITaskNotificationService
{
    private readonly INotificationEventDispatchAdapter _notifications;
    private readonly INotificationLocaleResolver _localeResolver;
    private readonly ITaskNotificationRecipientResolver _recipients;
    private readonly ITaskSeatDirectory _seats;
    private readonly IUserNotificationRepository _userNotifications;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TaskNotificationService> _logger;

    public TaskNotificationService(
        INotificationEventDispatchAdapter notifications,
        INotificationLocaleResolver localeResolver,
        ITaskNotificationRecipientResolver recipients,
        ITaskSeatDirectory seats,
        IUserNotificationRepository userNotifications,
        ITenantContext tenantContext,
        ILogger<TaskNotificationService> logger)
    {
        _notifications = notifications;
        _localeResolver = localeResolver;
        _recipients = recipients;
        _seats = seats;
        _userNotifications = userNotifications;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<TaskNotificationOutcome> NotifyAsync(
        TaskItem task,
        string eventCode,
        IReadOnlyCollection<Guid> candidateUserIds,
        Guid actingUserId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        /*
         * The task's own settings — master switch, then the per-event preference — honoured before anything is
         * resolved or sent. Both live in TaskNotificationPolicy so the sweep can ask the SAME question before it
         * spends a claim, rather than a second copy of it drifting away from this one.
         */
        if (!TaskNotificationPolicy.WouldNotify(task, eventCode))
        {
            return TaskNotificationOutcome.Skipped;
        }

        var audience = (candidateUserIds ?? [])
            .Where(id => id != Guid.Empty && id != actingUserId)
            .Distinct()
            .ToList();

        if (audience.Count == 0)
        {
            // Everyone who would have been told IS the person who did it. Nothing to send, nothing wrong.
            return TaskNotificationOutcome.Skipped;
        }

        try
        {
            var recipients = await _recipients.ResolveAsync(audience, ct);

            /*
             * NOBODY REACHABLE is reported, never treated as success.
             *
             * The alternative — returning "dispatched" because we tried — is the failure mode this whole ticket
             * is about: a system that believes it notified people it never reached. The caller logs it, the task
             * still succeeds, and the log names the event and the task so it is findable.
             */
            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "task.notification.no_recipients EventCode={EventCode} TaskId={TaskId} Candidates={Candidates}. "
                    + "Nobody was notified.",
                    eventCode, task.Id, audience.Count);
                return TaskNotificationOutcome.NoRecipients;
            }

            /*
             * ── THE SECOND CHANNEL ───────────────────────────────────────────────────────────────────────
             *
             * BL-025. This is where the recipient's USER ID stops being thrown away. The projection two
             * statements below — `new EmailRecipientDto(r.Email, r.DisplayName)` — is the exact line at which
             * a resolved TaskNotificationRecipient(UserId, Email, DisplayName) loses its id, and with it any
             * chance of ever answering "what are MY unread notifications?".
             *
             * ⚠ PLACED HERE, AFTER THE FILTERS, DELIBERATELY. Everything that decides whether anybody hears
             * about this event has already run: the task's master switch and its per-event preference (in
             * TaskNotificationPolicy, above), the actor exclusion, and the address resolution. A second
             * channel that re-derived those rules would be a second place for them to drift; a second channel
             * placed BEFORE them would notify people who switched notifications off. There is no separate
             * in-app preference and there must not be one — the task's answer is the task's answer.
             *
             * ⚠ AND IT RUNS BEFORE THE DISPATCH, GUARDED SEPARATELY. Both orderings were available and this
             * one is the survivable half: written first behind its own try/catch, an in-app row exists even
             * when the mail transport is down, and a database that refuses cannot stop the e-mail — the
             * enclosing catch would otherwise swallow the write attempt and never reach the send.
             */
            await WriteInAppNotificationsAsync(task, eventCode, recipients, ct);

            var response = await _notifications.DispatchByEventCodeAsync(
                new NotificationEventDispatchRequest(
                    TenantId: _tenantContext.TenantId,
                    EventCode: eventCode,
                    To: recipients
                        .Select(r => new EmailRecipientDto(r.Email, r.DisplayName))
                        .ToList(),
                    Variables: BuildVariables(task),
                    /*
                     * Stated, not omitted. MOD-0024 does NOT know what language these recipients read: the
                     * resolver seam it added returns id, e-mail and display name, and the AuthService User entity
                     * behind it carries no language field at all. Passing null says exactly that, and
                     * INotificationLocaleResolver answers with the tenant's own configured language.
                     *
                     * Guessing here would have been the worse option in both directions: the actor's UI culture is
                     * the SENDER's language, not the reader's, and a hard-coded "en" would throw away six of the
                     * seven seeded template languages. A per-recipient language — and with it a dispatch per
                     * language group — becomes possible the moment User carries one; see the class doc.
                     */
                    Locale: null),
                ct);

            if (!response.IsSuccessful)
            {
                /*
                 * An ops condition, not a task failure — but one somebody has to be able to DIAGNOSE.
                 *
                 * This line used to say only "ReasonCode={ReasonCode}", and the codes it reported were null,
                 * because none of QueueEmailNotificationHandler's six refusals carried one. "Not dispatched,
                 * reason null" is not a diagnosis; it sent a whole round of investigation at the templates while
                 * the real refusal was a missing platform-default messaging settings row.
                 *
                 * Now: the code, the message behind it, and the locale that was actually looked up. The resolved
                 * TEMPLATE KEY is on the adapter's notification.dispatch_failed line, emitted immediately before
                 * this one for the same tenant and event — it lives there because the adapter is the only place
                 * that knows it, and Response has nowhere to carry it back.
                 */
                var resolvedLocale = await _localeResolver.ResolveAsync(_tenantContext.TenantId, null, ct);

                _logger.LogWarning(
                    "task.notification.not_dispatched EventCode={EventCode} TaskId={TaskId} ReasonCode={ReasonCode} "
                    + "Locale={Locale} Reason={Reason}",
                    eventCode,
                    task.Id,
                    response.ReasonCode ?? "<none>",
                    resolvedLocale,
                    string.Join(" | ", response.Errors));
                return TaskNotificationOutcome.Failed;
            }

            return TaskNotificationOutcome.Dispatched;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The rule the whole feature hangs on: a notification never fails the write that triggered it.
            _logger.LogWarning(ex, "task.notification.threw EventCode={EventCode} TaskId={TaskId}", eventCode, task.Id);
            return TaskNotificationOutcome.Failed;
        }
    }

    public async Task<IReadOnlyList<Guid>> ResolvePoolHoldersAsync(TaskItem task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (task.AssignmentTarget != TaskAssignmentTarget.PositionPool || task.PoolPositionId is not { } positionId)
        {
            return [];
        }

        return await _seats.HoldersOfAsync(new HashSet<Guid> { positionId }, ct);
    }

    /// <summary>
    /// BL-025 — one in-app row per resolved recipient, carrying the id the e-mail projection drops.
    ///
    /// <para><b>Never throws, and never affects the e-mail.</b> Its own try/catch, not the caller's: the
    /// enclosing handler treats any exception as "the whole notification failed", which would mean a Mongo
    /// hiccup silently costing the e-mail that would otherwise have gone out. A failure here is logged and
    /// the round continues.</para>
    ///
    /// <para><b>What is written, and what deliberately is not.</b> The row carries the stable event code and
    /// the task's OWN title — data the tenant typed. No sentence is composed here, because the reader's
    /// language is not known at write time and a row written in one language cannot be re-read in another;
    /// a surface resolves its label from the event code instead. <c>Severity</c> is <c>Info</c> for every
    /// task event: a per-event severity is a product decision nobody has made, and inventing a mapping now
    /// would ship one by accident.</para>
    /// </summary>
    private async Task WriteInAppNotificationsAsync(
        TaskItem task,
        string eventCode,
        IReadOnlyList<TaskNotificationRecipient> recipients,
        CancellationToken ct)
    {
        try
        {
            foreach (var recipient in recipients)
            {
                if (recipient.UserId == Guid.Empty)
                {
                    continue;
                }

                await _userNotifications.CreateAsync(
                    new UserNotification
                    {
                        TenantId = _tenantContext.TenantId,
                        UserId = recipient.UserId,
                        EventCode = eventCode,
                        Title = task.Title,
                        TargetUrl = TaskDeepLink(task.Id),
                        Severity = UserNotificationSeverity.Info
                    },
                    ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "task.notification.in_app_write_failed EventCode={EventCode} TaskId={TaskId} Recipients={Recipients}",
                eventCode, task.Id, recipients.Count);
        }
    }

    /// <summary>
    /// Where an in-app notification about a task points. Mirrors the deep link
    /// <c>TaskWorkItemProvider</c> already publishes for the same records, so the bell and the work list send
    /// the reader to the same place.
    /// </summary>
    private static string TaskDeepLink(Guid taskId) => $"/Tasks/{taskId}";

    /// <summary>
    /// The variables every task event declares. Kept in ONE place so a template rendering a blank is a
    /// mismatch somebody can find, rather than a per-call-site oversight.
    ///
    /// <para><c>DueAt</c> is supplied even by events that declare it optional: an absent variable renders as an
    /// empty string either way, and supplying it means a tenant that adds it to their own template override gets
    /// a date rather than a hole.</para>
    /// </summary>
    private static Dictionary<string, object?> BuildVariables(TaskItem task) => new()
    {
        ["TaskTitle"] = task.Title,
        ["TaskId"] = task.Id.ToString(),
        ["DueAt"] = task.DueAt?.ToString("yyyy-MM-dd") ?? string.Empty
    };
}
