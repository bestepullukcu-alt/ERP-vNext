using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 FIX3 — the two cross-tenant queries the retry sweep runs, against a real MongoDB: which rows the retry window
/// closes, and that a closed (permanent) row is never selected for a retry again. Both scan every tenant, so the test
/// gets the harness's fixed-name isolated database (DB-010), emptied before each test.
/// </summary>
public sealed class NotificationDispatchRetentionMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private NotificationDispatchRepository _dispatches = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync("email_shell_retention", SchemaProfile.Notification);
        _dispatches = new NotificationDispatchRepository(_harness.DbContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_window_selects_only_rows_still_waiting_and_queued_before_the_cutoff()
    {
        var now = DateTimeOffset.UtcNow;
        var oldFailed = await AddAsync(NotificationDispatchStatus.Failed, now.AddHours(-30));
        var oldQueued = await AddAsync(NotificationDispatchStatus.Queued, now.AddHours(-30));
        await AddAsync(NotificationDispatchStatus.Failed, now.AddHours(-2));
        await AddAsync(NotificationDispatchStatus.Sent, now.AddHours(-30));
        await AddAsync(NotificationDispatchStatus.Cancelled, now.AddHours(-30));
        await AddAsync(NotificationDispatchStatus.Failed, now.AddHours(-30), permanent: true);

        var expired = await _dispatches.FindRetryWindowExpiredAsync(now.AddHours(-24), 50);

        Assert.Equal(
            new[] { oldFailed.Id, oldQueued.Id }.OrderBy(x => x),
            expired.Select(h => h.DispatchId).OrderBy(x => x));
    }

    [Fact]
    public async Task A_permanent_failure_is_never_selected_for_a_retry_whatever_its_count_and_due_time_say()
    {
        var now = DateTimeOffset.UtcNow;
        var due = await AddAsync(NotificationDispatchStatus.Failed, now.AddHours(-1));
        await AddAsync(NotificationDispatchStatus.Failed, now.AddHours(-1), permanent: true);

        var handles = await _dispatches.FindDueRetriesAsync(now, maxRetryCount: 5, take: 50);

        Assert.Equal(due.Id, Assert.Single(handles).DispatchId);
    }

    private async Task<NotificationDispatch> AddAsync(NotificationDispatchStatus status, DateTimeOffset queuedAt, bool permanent = false)
    {
        var dispatch = new NotificationDispatch
        {
            TenantId = _harness.TenantId,
            TemplateKey = "platform.tasks.assigned",
            Locale = "en",
            Channel = NotificationChannelCode.Email,
            ProviderCode = MessagingProviderCode.Smtp,
            Status = status,
            To = [new EmailRecipient { Email = "user@example.com" }],
            Subject = "s",
            VariablesJson = "{\"TaskTitle\":\"x\"}",
            QueuedAt = queuedAt,
            RetryCount = 1,
            NextRetryAt = queuedAt.AddMinutes(1),
            PermanentlyFailedNotifiedAt = permanent ? queuedAt.AddMinutes(2) : null
        };
        await _dispatches.CreateAsync(dispatch);
        return dispatch;
    }
}
