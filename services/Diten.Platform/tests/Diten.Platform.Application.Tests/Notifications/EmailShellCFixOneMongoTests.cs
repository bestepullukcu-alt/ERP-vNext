using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Prometheus;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 — the A-FIX1 + C fix round (P-2026-10-07-EMAIL-S2C-FIX1) on a real MongoDB: the claim and the mark are targeted
/// conditional writes of the production repository, and the seed keeps an edit of the shell or of the plain-text body.
/// An isolated database of its own (DB-010).
/// </summary>
public sealed class EmailShellCFixOneMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private NotificationDispatchRepository _dispatches = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync("email_shell_cfix1", SchemaProfile.Notification);
        _dispatches = new NotificationDispatchRepository(_harness.DbContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private IMongoCollection<NotificationDispatch> Dispatches =>
        _harness.Database.GetCollection<NotificationDispatch>(PlatformCollections.NotificationDispatches);

    // ---------------------------------------------------------------- 1: a mark that loses, then throws

    [Fact]
    public async Task Effects_whose_mark_first_loses_and_then_throws_run_once_and_the_failed_mark_is_named_and_counted()
    {
        var meetings = new EmailShellDispatchTests.MeetingDoubles();
        var key = "platform.meetings.cfix1t" + Guid.NewGuid().ToString("N");
        var row = await AddPendingAsync(meetings, key);
        var logger = new EmailShellDispatchTests.LinesLogger<NotificationPermanentFailureEffects>();
        var effects = new NotificationPermanentFailureEffects(logger, meetings.Meetings, meetings.Attendees, meetings.Notifications);
        var markFailedBefore = MarkFailed(key);
        var repository = new MarkLosesThenThrows(_dispatches);

        var completed = await effects.ApplyAndMarkAsync((await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!,
            silent: false, repository, CancellationToken.None);

        Assert.True(completed);
        Assert.Single(meetings.Undelivered);       // the effects ran once
        Assert.Single(meetings.OrganizerNotices);
        Assert.Equal(NotificationPermanentFailureEffects.MarkAttempts, repository.MarkCalls); // tried, bounded
        Assert.Contains(logger.Lines, line => line.Contains("mark_lost", StringComparison.Ordinal));
        Assert.Contains(logger.Lines, line => line.Contains("mark_failed", StringComparison.Ordinal) && line.Contains("MarkKeptFailing", StringComparison.Ordinal));
        Assert.Equal(markFailedBefore + 1, MarkFailed(key));
        Assert.True(NotificationDispatch.IsPermanentFailurePending((await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!));
    }

    [Fact]
    public async Task A_row_past_its_last_attempt_is_given_up_without_running_the_effects_again()
    {
        var meetings = new EmailShellDispatchTests.MeetingDoubles();
        var row = await AddPendingAsync(meetings, "platform.meetings.cfix1t" + Guid.NewGuid().ToString("N"),
            updatedBy: $"{NotificationPermanentFailureEffects.ClaimActorPrefix}{NotificationPermanentFailureEffects.MaxAttempts}:host:nonce");
        var logger = new EmailShellDispatchTests.LinesLogger<NotificationPermanentFailureEffects>();
        var effects = new NotificationPermanentFailureEffects(logger, meetings.Meetings, meetings.Attendees, meetings.Notifications);

        Assert.True(await effects.ApplyAndMarkAsync((await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!,
            silent: false, _dispatches, CancellationToken.None));

        Assert.Empty(meetings.Undelivered);
        Assert.Empty(meetings.OrganizerNotices);
        Assert.Contains(logger.Lines, line => line.Contains("effects_given_up", StringComparison.Ordinal) && line.Contains("AttemptsExhausted", StringComparison.Ordinal));
        Assert.False(NotificationDispatch.IsPermanentFailurePending((await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!));
    }

    // ---------------------------------------------------------------- K1: the claim requires the row to be pending

    [Fact]
    public async Task A_claim_over_a_row_marked_without_a_version_bump_is_refused()
    {
        var meetings = new EmailShellDispatchTests.MeetingDoubles();
        var row = await AddPendingAsync(meetings, "platform.meetings.cfix1t" + Guid.NewGuid().ToString("N"));
        var stale = (await _dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        // Another writer marks it done and leaves the version alone.
        await Dispatches.UpdateOneAsync(
            Builders<NotificationDispatch>.Filter.Eq(x => x.Id, row.Id),
            Builders<NotificationDispatch>.Update.Set(x => x.PermanentlyFailedNotifiedAt, DateTimeOffset.UtcNow));

        var claimed = await _dispatches.TryClaimPermanentEffectsAsync(stale, stale.Version, DateTimeOffset.UtcNow, "claim-test", CancellationToken.None);

        Assert.False(claimed);
        Assert.False(await meetings.Effects().ApplyAndMarkAsync(stale, silent: false, _dispatches, CancellationToken.None));
        Assert.Empty(meetings.OrganizerNotices);
    }

    // ---------------------------------------------------------------- K4 (CT M7): an edit of the shell or of the text body is kept

    [Theory]
    [InlineData("shell")]
    [InlineData("text")]
    public async Task A_seed_row_whose_only_edit_is_its_shell_or_its_plain_text_body_is_not_carried_forward(string part)
    {
        var templates = _harness.Database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates);
        var row = NotificationTemplateSeed.TenantSuspendedV1("en");
        if (part == "shell")
        {
            row.Shell = new NotificationTemplateShell { HeadingTemplate = "Our own heading" };
        }
        else
        {
            row.BodyTextTemplate = "Our own words. Reason: {{Reason}} Date: {{SuspendedAtUtc}}";
        }

        // No operator stamp: the edit is in the content alone (an import, a hand-written fix), not in UpdatedBy.
        await templates.InsertOneAsync(row);

        var result = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        // Kept AND counted as an edit: the conditional write would also refuse a changed text body, so the row's content
        // alone does not show that the rule saw the edit — the count does (CT M7 survived on exactly that).
        Assert.Equal(1, result.KeptModified);
        var after = await templates.Find(t => t.Id == row.Id).SingleAsync();
        Assert.Equal("1.0.0", after.SemanticVersion);
        Assert.Equal(row.BodyTextTemplate, after.BodyTextTemplate);
        Assert.Equal(row.Shell?.HeadingTemplate, after.Shell?.HeadingTemplate);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<NotificationDispatch> AddPendingAsync(EmailShellDispatchTests.MeetingDoubles meetings, string key, string? updatedBy = null)
    {
        var dispatch = new NotificationDispatch
        {
            TenantId = _harness.TenantId,
            TemplateKey = key,
            Locale = "en",
            Channel = NotificationChannelCode.Email,
            ProviderCode = MessagingProviderCode.Smtp,
            Status = NotificationDispatchStatus.Failed,
            To = [new EmailRecipient { Email = "attendee@example.test" }],
            Subject = "s",
            VariablesJson = "{}",
            QueuedAt = DateTimeOffset.UtcNow.AddHours(-2),
            RetryCount = 5,
            CausationId = meetings.MeetingId,
            MeetingAttendeeUserId = Guid.NewGuid(),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedBy = updatedBy,
            PermanentlyFailedNotifiedAt = NotificationDispatch.PermanentFailurePending
        };
        await _dispatches.CreateAsync(dispatch);
        return dispatch;
    }

    private static double MarkFailed(string key) =>
        Metrics.CreateCounter(
                "notification_dispatch_permanent_effects_mark_failed",
                "Permanent-failure effects that ran but whose done-mark could not be written; the row stays pending.",
                new CounterConfiguration { LabelNames = ["template_key"] })
            .WithLabels(key).Value;

    /// <summary>The production repository, except its mark: the first call loses, every later one throws (Mongo down).</summary>
    private sealed class MarkLosesThenThrows(NotificationDispatchRepository inner) : INotificationDispatchRepository
    {
        public int MarkCalls { get; private set; }

        public Task<bool> TryMarkPermanentEffectsAppliedAsync(NotificationDispatch dispatch, string claimActor, DateTimeOffset appliedAt, CancellationToken ct = default)
        {
            MarkCalls++;
            return MarkCalls == 1 ? Task.FromResult(false) : throw new MongoException("store unavailable");
        }

        public Task<bool> TryClaimPermanentEffectsAsync(NotificationDispatch dispatch, int expectedVersion, DateTimeOffset claimedAt, string claimActor, CancellationToken ct = default) =>
            inner.TryClaimPermanentEffectsAsync(dispatch, expectedVersion, claimedAt, claimActor, ct);
        public Task<NotificationDispatch> CreateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => inner.CreateAsync(dispatch, ct);
        public Task<NotificationDispatch?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default) => inner.GetByIdForTenantAsync(tenantId, id, ct);
        public Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, NotificationDispatchStatus? status = null, DateTimeOffset? queuedFrom = null, DateTimeOffset? queuedTo = null, string? templateKey = null, CancellationToken ct = default) =>
            inner.ListByTenantAsync(tenantId, skip, take, status, queuedFrom, queuedTo, templateKey, ct);
        public Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => inner.UpdateAsync(dispatch, ct);
        public Task<bool> TryUpdateAsync(NotificationDispatch dispatch, int expectedVersion, NotificationDispatchStatus expectedStatus, CancellationToken ct = default) =>
            inner.TryUpdateAsync(dispatch, expectedVersion, expectedStatus, ct);
        public Task<IReadOnlyList<NotificationDispatchRetryHandle>> FindDueRetriesAsync(DateTimeOffset asOfUtc, int maxRetryCount, int take, CancellationToken ct = default) =>
            inner.FindDueRetriesAsync(asOfUtc, maxRetryCount, take, ct);
        public Task<IReadOnlyList<NotificationDispatchExpiryHandle>> FindPermanentFailurePendingAsync(DateTimeOffset idleBefore, int take, CancellationToken ct = default) =>
            inner.FindPermanentFailurePendingAsync(idleBefore, take, ct);
        public Task<IReadOnlyList<NotificationDispatchExpiryHandle>> FindRetryWindowExpiredAsync(DateTimeOffset queuedBefore, int take, CancellationToken ct = default) =>
            inner.FindRetryWindowExpiredAsync(queuedBefore, take, ct);
    }
}
