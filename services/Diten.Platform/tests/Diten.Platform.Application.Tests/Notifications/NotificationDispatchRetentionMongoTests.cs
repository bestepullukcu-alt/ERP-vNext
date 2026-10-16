using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
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
        // The close raises it once; the permanent-failure effects then claim (+1) and mark (+1) — BL-454 FIX1. Never more.
        var stored = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        Assert.Equal(readVersion + 3, stored.Version);
        Assert.StartsWith(Diten.Platform.Application.Features.Notifications.Services.NotificationPermanentFailureEffects.ClaimActorPrefix + "1:", stored.UpdatedBy);
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

    // ---------------------------------------------------------------- BL-454 FIX1 3: the pending marker on a real MongoDB

    [Fact]
    public async Task A_permanent_failure_whose_publish_throws_is_stored_pending_in_the_same_write_and_found_by_the_pending_query()
    {
        // (a) the production handler over the production repository; the publish throws after the write.
        var row = await AddAsync(NotificationDispatchStatus.Failed, DateTimeOffset.UtcNow.AddHours(-2));
        var handler = new MarkNotificationDispatchFailedHandler(_dispatches, new ThrowingEventBus());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new MarkNotificationDispatchFailedCommand(_harness.TenantId, row.Id, "SMTP_TIMEOUT", "SMTP_TIMEOUT", RetryCount: 5, IsPermanentFailure: true),
            CancellationToken.None));

        // (b) read back: MinValue survived the BSON round trip, in the transition's own write.
        var stored = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        Assert.Equal(NotificationDispatchStatus.Failed, stored.Status);
        Assert.True(NotificationDispatch.IsPermanentFailurePending(stored));
        Assert.Equal(DateTimeOffset.MinValue, stored.PermanentlyFailedNotifiedAt);

        // (c) the pending query returns it — once its grace has passed — and none of the rows around it.
        await AddAsync(NotificationDispatchStatus.Failed, DateTimeOffset.UtcNow.AddHours(-2), permanent: true);       // really marked
        await AddAsync(NotificationDispatchStatus.Failed, DateTimeOffset.UtcNow.AddHours(-2));                        // marker null
        await AddAsync(NotificationDispatchStatus.Sent, DateTimeOffset.UtcNow.AddHours(-2), marker: NotificationDispatch.PermanentFailurePending);
        Assert.Empty(await _dispatches.FindPermanentFailurePendingAsync(DateTimeOffset.UtcNow.AddMinutes(-10), 50)); // just written: in its grace

        var pending = await _dispatches.FindPermanentFailurePendingAsync(DateTimeOffset.UtcNow.AddMinutes(1), 50);

        Assert.Equal(row.Id, Assert.Single(pending).DispatchId);
    }

    [Fact]
    public async Task A_never_stamped_pending_row_is_idle_for_the_pending_query()
    {
        var row = await AddAsync(NotificationDispatchStatus.Failed, DateTimeOffset.UtcNow.AddHours(-2), marker: NotificationDispatch.PermanentFailurePending);
        Assert.Null((await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!.UpdatedAt);

        Assert.Equal(row.Id, Assert.Single(await _dispatches.FindPermanentFailurePendingAsync(DateTimeOffset.UtcNow, 50)).DispatchId);
    }

    [Fact]
    public async Task The_pending_query_reads_only_the_pending_index_entries()
    {
        // (d) twenty noise rows: really marked, never permanent, Sent.
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 20; i++)
        {
            var kind = i % 3;
            await AddAsync(
                kind == 2 ? NotificationDispatchStatus.Sent : NotificationDispatchStatus.Failed,
                now.AddHours(-2),
                permanent: kind == 0);
        }

        await AddAsync(NotificationDispatchStatus.Failed, now.AddHours(-2), marker: NotificationDispatch.PermanentFailurePending, updatedAt: now.AddHours(-1));
        var collection = _harness.Database.GetCollection<NotificationDispatch>(PlatformCollections.NotificationDispatches);
        var filter = NotificationDispatchRepository.PermanentFailurePendingFilter(now.AddMinutes(-10))
            .Render(collection.DocumentSerializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry);

        var explain = await _harness.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["explain"] = new BsonDocument { ["find"] = PlatformCollections.NotificationDispatches, ["filter"] = filter },
            ["verbosity"] = "executionStats"
        });

        var plan = explain["queryPlanner"]["winningPlan"].ToJson();
        Assert.Contains("IXSCAN", plan);
        Assert.Contains("ix_notification_dispatches_permanent_effects_pending", plan);
        var stats = explain["executionStats"];
        Assert.Equal(1, stats["nReturned"].ToInt32());
        Assert.True(stats["totalDocsExamined"].ToInt32() <= 1, stats.ToJson());
    }

    [Fact]
    public async Task A_pending_row_is_already_permanent_for_the_retry_and_the_window_queries_on_mongo()
    {
        // (e) the production queries, not a copy: a pending row is neither retried nor closed by the window again.
        var now = DateTimeOffset.UtcNow;
        var pending = await AddAsync(NotificationDispatchStatus.Failed, now.AddHours(-30), marker: NotificationDispatch.PermanentFailurePending);
        var waiting = await AddAsync(NotificationDispatchStatus.Failed, now.AddHours(-30));

        var due = await _dispatches.FindDueRetriesAsync(now, maxRetryCount: 5, take: 50);
        var expired = await _dispatches.FindRetryWindowExpiredAsync(now.AddHours(-24), 50);

        Assert.DoesNotContain(due, h => h.DispatchId == pending.Id);
        Assert.DoesNotContain(expired, h => h.DispatchId == pending.Id);
        Assert.Contains(due, h => h.DispatchId == waiting.Id);       // the queries do return rows: not empty by accident
        Assert.Contains(expired, h => h.DispatchId == waiting.Id);
    }

    // ---------------------------------------------------------------- BL-454 FIX1 4: a normal permanent failure is not left pending

    [Fact]
    public async Task A_normal_permanent_failure_on_mongo_ends_with_the_real_time_and_leaves_nothing_pending()
    {
        var row = await AddAsync(NotificationDispatchStatus.Failed, DateTimeOffset.UtcNow.AddHours(-2));
        var before = DateTimeOffset.UtcNow;

        var response = await new MarkNotificationDispatchFailedHandler(_dispatches, new NotificationsSmtpIntegrationTests.RecordingEventBus())
            .Handle(new MarkNotificationDispatchFailedCommand(_harness.TenantId, row.Id, "SMTP_TIMEOUT", "SMTP_TIMEOUT", RetryCount: 5, IsPermanentFailure: true), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var stored = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        Assert.False(NotificationDispatch.IsPermanentFailurePending(stored));
        Assert.True(stored.PermanentlyFailedNotifiedAt >= before.AddSeconds(-1), stored.PermanentlyFailedNotifiedAt?.ToString("O"));
        Assert.Empty(await _dispatches.FindPermanentFailurePendingAsync(DateTimeOffset.UtcNow.AddHours(1), 50));
    }

    // ---------------------------------------------------------------- BL-454 FIX1 1: the claim

    [Fact]
    public async Task Two_runs_that_read_the_same_pending_row_apply_the_effects_once_and_the_second_claim_is_refused()
    {
        var meetings = new EmailShellDispatchTests.MeetingDoubles();
        var row = await AddMeetingPendingAsync(meetings, updatedAt: DateTimeOffset.UtcNow.AddHours(-1), pending: true);
        var first = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        var second = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        var effects = meetings.Effects();

        Assert.True(await effects.ApplyAndMarkAsync(first, silent: false, _dispatches, CancellationToken.None));
        Assert.False(await effects.ApplyAndMarkAsync(second, silent: false, _dispatches, CancellationToken.None));

        Assert.Single(meetings.Undelivered);
        Assert.Single(meetings.OrganizerNotices);
        var stored = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        Assert.False(NotificationDispatch.IsPermanentFailurePending(stored));
        Assert.Equal(row.Version + 2, stored.Version); // claim + mark
    }

    [Theory]
    [InlineData(false)] // the sweep runs while the publish is held: the row is inside its grace, the sweep leaves it
    [InlineData(true)]  // the grace has passed meanwhile (a very slow publish): the sweep claims it, the source loses its claim
    public async Task A_sweep_that_runs_while_the_source_publish_is_held_leaves_the_effects_applied_once(bool graceHasPassed)
    {
        var meetings = new EmailShellDispatchTests.MeetingDoubles();
        var row = await AddMeetingPendingAsync(meetings, updatedAt: null, pending: false); // the last retry is about to fail
        var bus = new HeldEventBus();
        var handler = new MarkNotificationDispatchFailedHandler(
            _dispatches, bus, null, meetings.Meetings, meetings.Attendees, meetings.Notifications);

        var source = handler.Handle(
            new MarkNotificationDispatchFailedCommand(_harness.TenantId, row.Id, "SMTP_TIMEOUT", "SMTP_TIMEOUT", RetryCount: 5, IsPermanentFailure: true),
            CancellationToken.None);
        await bus.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(NotificationDispatch.IsPermanentFailurePending((await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!));

        if (graceHasPassed)
        {
            await _harness.Database.GetCollection<NotificationDispatch>(PlatformCollections.NotificationDispatches).UpdateOneAsync(
                Builders<NotificationDispatch>.Filter.Eq(x => x.Id, row.Id),
                Builders<NotificationDispatch>.Update.Set(x => x.UpdatedAt, DateTimeOffset.UtcNow.AddHours(-1)));
        }

        var sweep = new Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJob(
            _dispatches,
            new NothingScheduled(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJob>.Instance,
            new NothingSent(),
            Microsoft.Extensions.Options.Options.Create(new Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchRetentionOptions()),
            new Diten.Platform.Common.Tenancy.TenantContext(),
            meetings.Effects());
        await sweep.HandleAsync(new Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJobArgs(), new Diten.BuildingBlocks.BackgroundJobs.BackgroundJobContext(), CancellationToken.None);
        Assert.Equal(graceHasPassed ? 1 : 0, meetings.OrganizerNotices.Count);

        bus.Release.SetResult();
        Assert.True((await source).IsSuccessful);

        Assert.Single(meetings.Undelivered);
        Assert.Single(meetings.OrganizerNotices);
        Assert.False(NotificationDispatch.IsPermanentFailurePending((await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!));
    }

    // ---------------------------------------------------------------- BL-454 FIX1 8c / CT E2: re-check after the re-read

    [Fact]
    public async Task A_row_marked_between_the_pending_query_and_the_re_read_gets_no_effect_from_the_re_drive()
    {
        // The pending query returns the row; before the sweep re-reads it, the source transition's own mark lands
        // (real time, one version on). The re-drive must see that on the re-read and apply nothing.
        var meetings = new EmailShellDispatchTests.MeetingDoubles();
        var row = await AddMeetingPendingAsync(meetings, updatedAt: DateTimeOffset.UtcNow.AddHours(-1), pending: true);
        var repository = new MarksAfterThePendingQuery(_dispatches);

        var sweep = new Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJob(
            repository,
            new NothingScheduled(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJob>.Instance,
            new NothingSent(),
            Microsoft.Extensions.Options.Options.Create(new Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchRetentionOptions()),
            new Diten.Platform.Common.Tenancy.TenantContext(),
            meetings.Effects());
        await sweep.HandleAsync(new Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJobArgs(), new Diten.BuildingBlocks.BackgroundJobs.BackgroundJobContext(), CancellationToken.None);

        Assert.Equal(1, repository.MarkedInBetween); // the race did happen
        Assert.Empty(meetings.Undelivered);
        Assert.Empty(meetings.OrganizerNotices);
        var stored = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        Assert.False(NotificationDispatch.IsPermanentFailurePending(stored));
    }

    /// <summary>The production repository; right after the pending query answers, each returned row is marked done
    /// (real time, version + 1) — the source transition's own mark landing in between.</summary>
    private sealed class MarksAfterThePendingQuery(NotificationDispatchRepository inner) : Diten.Platform.Domain.Repositories.INotificationDispatchRepository
    {
        public int MarkedInBetween { get; private set; }

        public async Task<IReadOnlyList<NotificationDispatchExpiryHandle>> FindPermanentFailurePendingAsync(DateTimeOffset idleBefore, int take, CancellationToken ct = default)
        {
            var handles = await inner.FindPermanentFailurePendingAsync(idleBefore, take, ct);
            foreach (var handle in handles)
            {
                var row = (await inner.GetByIdForTenantAsync(handle.TenantId, handle.DispatchId, ct))!;
                row.PermanentlyFailedNotifiedAt = DateTimeOffset.UtcNow;
                row.Version++;
                await inner.UpdateAsync(row, ct);
                MarkedInBetween++;
            }

            return handles;
        }

        public Task<NotificationDispatch> CreateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => inner.CreateAsync(dispatch, ct);
        public Task<NotificationDispatch?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default) => inner.GetByIdForTenantAsync(tenantId, id, ct);
        public Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, NotificationDispatchStatus? status = null, DateTimeOffset? queuedFrom = null, DateTimeOffset? queuedTo = null, string? templateKey = null, CancellationToken ct = default) =>
            inner.ListByTenantAsync(tenantId, skip, take, status, queuedFrom, queuedTo, templateKey, ct);
        public Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => inner.UpdateAsync(dispatch, ct);
        public Task<bool> TryUpdateAsync(NotificationDispatch dispatch, int expectedVersion, NotificationDispatchStatus expectedStatus, CancellationToken ct = default) =>
            inner.TryUpdateAsync(dispatch, expectedVersion, expectedStatus, ct);
        public Task<IReadOnlyList<NotificationDispatchRetryHandle>> FindDueRetriesAsync(DateTimeOffset asOfUtc, int maxRetryCount, int take, CancellationToken ct = default) =>
            inner.FindDueRetriesAsync(asOfUtc, maxRetryCount, take, ct);
        public Task<IReadOnlyList<NotificationDispatchExpiryHandle>> FindRetryWindowExpiredAsync(DateTimeOffset queuedBefore, int take, CancellationToken ct = default) =>
            inner.FindRetryWindowExpiredAsync(queuedBefore, take, ct);
    }

    private async Task<NotificationDispatch> AddMeetingPendingAsync(
        EmailShellDispatchTests.MeetingDoubles meetings, DateTimeOffset? updatedAt, bool pending)
    {
        var dispatch = new NotificationDispatch
        {
            TenantId = _harness.TenantId,
            TemplateKey = "platform.meetings.invite",
            Locale = "en",
            Channel = NotificationChannelCode.Email,
            ProviderCode = MessagingProviderCode.Smtp,
            Status = NotificationDispatchStatus.Failed,
            To = [new EmailRecipient { Email = "attendee@example.test" }],
            Subject = "s",
            VariablesJson = "{}",
            QueuedAt = DateTimeOffset.UtcNow.AddHours(-2),
            RetryCount = 4,
            CausationId = meetings.MeetingId,
            MeetingAttendeeUserId = Guid.NewGuid(),
            UpdatedAt = updatedAt,
            PermanentlyFailedNotifiedAt = pending ? NotificationDispatch.PermanentFailurePending : null
        };
        await _dispatches.CreateAsync(dispatch);
        return dispatch;
    }

    /// <summary>The publish waits until the test releases it — a slow broker, measured on purpose.</summary>
    private sealed class HeldEventBus : Diten.BuildingBlocks.Eventing.IEventBus
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Diten.BuildingBlocks.Eventing.EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : Diten.BuildingBlocks.Eventing.IIntegrationEvent => PublishAsync(@event, new Diten.BuildingBlocks.Eventing.EventPublishOptions(), ct);

        public async Task<Diten.BuildingBlocks.Eventing.EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, Diten.BuildingBlocks.Eventing.EventPublishOptions options, CancellationToken ct = default)
            where TEvent : Diten.BuildingBlocks.Eventing.IIntegrationEvent
        {
            Entered.TrySetResult();
            await Release.Task;
            return await new NotificationsSmtpIntegrationTests.RecordingEventBus().PublishAsync(@event, options, ct);
        }
    }

    private sealed class ThrowingEventBus : Diten.BuildingBlocks.Eventing.IEventBus
    {
        public Task<Diten.BuildingBlocks.Eventing.EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : Diten.BuildingBlocks.Eventing.IIntegrationEvent => throw new InvalidOperationException("broker unavailable");

        public Task<Diten.BuildingBlocks.Eventing.EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, Diten.BuildingBlocks.Eventing.EventPublishOptions options, CancellationToken ct = default)
            where TEvent : Diten.BuildingBlocks.Eventing.IIntegrationEvent => throw new InvalidOperationException("broker unavailable");
    }

    private sealed class NothingScheduled : Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobScheduler
    {
        public Task<string> EnqueueAsync<TArgs, THandler>(TArgs args, Diten.BuildingBlocks.BackgroundJobs.BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobHandler<TArgs> => Task.FromResult("job");

        public Task<string> ScheduleAsync<TArgs, THandler>(TArgs args, DateTimeOffset enqueueAtUtc, Diten.BuildingBlocks.BackgroundJobs.BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobHandler<TArgs> => Task.FromResult("job");

        public Task RegisterRecurringAsync(Diten.BuildingBlocks.BackgroundJobs.RecurringJobRegistration registration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>The sweep's window close is not under test here; nothing it sends is answered.</summary>
    private sealed class NothingSent : MediatR.IMediator
    {
        public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken cancellationToken = default) => Task.FromResult(default(TResponse)!);
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : MediatR.IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : MediatR.INotification => Task.CompletedTask;
    }

    private async Task<NotificationDispatch> AddAsync(
        NotificationDispatchStatus status, DateTimeOffset queuedAt, bool permanent = false, DateTimeOffset? marker = null, DateTimeOffset? updatedAt = null)
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
            PermanentlyFailedNotifiedAt = marker ?? (permanent ? queuedAt.AddMinutes(2) : null),
            UpdatedAt = updatedAt
        };
        await _dispatches.CreateAsync(dispatch);
        return dispatch;
    }
}
