using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using MongoDB.Bson;
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

    [Fact]
    public async Task A_conditional_write_over_a_row_that_moved_on_writes_nothing()
    {
        var row = await AddAsync(NotificationDispatchStatus.Failed, DateTimeOffset.UtcNow.AddHours(-30));
        var read = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        var readVersion = read.Version;

        // A retry sends it in between: the stored row is now Sent, one version on.
        var sent = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        sent.TryMarkSent("sent-meanwhile", DateTimeOffset.UtcNow);
        await _dispatches.UpdateAsync(sent);

        read.TryMarkFailed("RetryWindowExpired", "RetryWindowExpired:None", DateTimeOffset.UtcNow, isPermanent: true);
        var written = await _dispatches.TryUpdateAsync(read, readVersion, NotificationDispatchStatus.Failed);

        Assert.False(written);
        var stored = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        Assert.Equal(NotificationDispatchStatus.Sent, stored.Status);
        Assert.Equal("sent-meanwhile", stored.ProviderMessageId);
    }

    [Fact]
    public async Task A_conditional_write_over_the_row_as_read_is_written()
    {
        var row = await AddAsync(NotificationDispatchStatus.Failed, DateTimeOffset.UtcNow.AddHours(-30));
        var read = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        var readVersion = read.Version;
        read.TryMarkFailed("RetryWindowExpired", "RetryWindowExpired:None", DateTimeOffset.UtcNow, isPermanent: true);

        Assert.True(await _dispatches.TryUpdateAsync(read, readVersion, NotificationDispatchStatus.Failed));
        Assert.Equal("{}", (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!.VariablesJson);
    }

    [Fact]
    public async Task The_window_query_reads_neither_sent_rows_nor_the_history_of_permanent_failures()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 20; i++)
        {
            await AddAsync(NotificationDispatchStatus.Sent, now.AddHours(-30));
            await AddAsync(NotificationDispatchStatus.Failed, now.AddHours(-30), permanent: true); // closed long ago
        }

        await AddAsync(NotificationDispatchStatus.Failed, now.AddHours(-30));
        var collection = _harness.Database.GetCollection<NotificationDispatch>(PlatformCollections.NotificationDispatches);
        var filter = NotificationDispatchRepository.RetryWindowExpiredFilter(now.AddHours(-24))
            .Render(collection.DocumentSerializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry);

        var explain = await _harness.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["explain"] = new BsonDocument
            {
                ["find"] = PlatformCollections.NotificationDispatches,
                ["filter"] = filter,
                ["sort"] = new BsonDocument("QueuedAt", 1)
            },
            ["verbosity"] = "executionStats"
        });

        var plan = explain["queryPlanner"]["winningPlan"].ToJson();
        Assert.Contains("ix_notification_dispatches_retry_window_waiting", plan);
        var stats = explain["executionStats"];
        // The one waiting row is the only document read; the twenty closed ones are outside the index bounds.
        Assert.True(stats["totalDocsExamined"].ToInt32() <= 1, stats.ToJson());
        // DateTimeOffset is stored as [ticks, offset], so each indexed row has two QueuedAt keys: one row, a few keys.
        // Without the partial filter the forty noise rows add their keys (and the twenty Failed ones their documents).
        Assert.True(stats["totalKeysExamined"].ToInt32() <= 4, stats.ToJson());
    }

    [Fact]
    public async Task A_conditional_close_raises_the_version_exactly_once()
    {
        var row = await AddAsync(NotificationDispatchStatus.Failed, DateTimeOffset.UtcNow.AddHours(-30));
        var read = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        var readVersion = read.Version;
        var handler = new Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers.MarkNotificationDispatchFailedHandler(
            _dispatches, new NotificationsSmtpIntegrationTests.RecordingEventBus());

        var response = await handler.Handle(new Diten.Platform.Application.Features.Notifications.Commands.MarkNotificationDispatchFailedCommand(
            _harness.TenantId, row.Id, "RetryWindowExpired", "RetryWindowExpired:None",
            IsPermanentFailure: true, ExpectedVersion: readVersion, ExpectedStatus: NotificationDispatchStatus.Failed), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(readVersion + 1, (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!.Version);
    }

    [Fact]
    public async Task A_conditional_write_never_writes_the_same_id_in_another_tenant()
    {
        var row = await AddAsync(NotificationDispatchStatus.Failed, DateTimeOffset.UtcNow.AddHours(-30));
        var read = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        var readVersion = read.Version;
        read.TryMarkFailed("RetryWindowExpired", "RetryWindowExpired:None", DateTimeOffset.UtcNow, isPermanent: true);
        read.TenantId = Guid.NewGuid(); // the same id, claimed by another tenant

        var written = await _dispatches.TryUpdateAsync(read, readVersion, NotificationDispatchStatus.Failed);

        Assert.False(written);
        var stored = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        Assert.Null(stored.PermanentlyFailedNotifiedAt);
        Assert.Equal("{\"TaskTitle\":\"x\"}", stored.VariablesJson);
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
