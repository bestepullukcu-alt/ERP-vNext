using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Validators;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 slice 2 stage C, against a real MongoDB: the seed that brings the tenant lifecycle mails to seven languages
/// never overwrites a template somebody changed; and a dispatch document written before the newer fields existed goes
/// through the retry and the window close end to end. Both subjects are database-global (platform-default templates,
/// a cross-tenant sweep), so the harness's fixed-name isolated database is used, emptied before each test.
/// </summary>
public sealed class EmailSliceTwoStageCMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;

    public async Task InitializeAsync() =>
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync("email_shell_stage_c", SchemaProfile.Notification);

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private IMongoCollection<NotificationTemplate> Templates =>
        _harness.Database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates);

    // ---------------------------------------------------------------- 1. seven languages without overwriting anything

    [Fact]
    public async Task The_seed_adds_the_missing_languages_carries_untouched_rows_forward_and_leaves_changed_ones_alone()
    {
        // What a database written by 1.0.0 holds: en and tr of each lifecycle mail.
        foreach (var locale in NotificationTemplateSeed.LifecycleV1Locales)
        {
            await Templates.InsertManyAsync([
                NotificationTemplateSeed.TenantInviteV1(locale),
                NotificationTemplateSeed.TenantSuspendedV1(locale),
                NotificationTemplateSeed.TenantReactivatedV1(locale)]);
        }

        // An operator rewrote the English invitation on the platform default …
        var operatorEdit = await Templates.Find(t => t.TemplateKey == "tenant.invite.email" && t.Locale == "en" && t.TenantId == null).SingleAsync();
        await Templates.UpdateOneAsync(t => t.Id == operatorEdit.Id, Builders<NotificationTemplate>.Update
            .Set(t => t.BodyHtmlTemplate, "<p>Our own words for {{TenantDisplayName}} ({{TenantId}}).</p>")
            .Set(t => t.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(t => t.UpdatedBy, "operator"));
        // … and a tenant keeps its own French suspension mail.
        var tenantId = Guid.NewGuid();
        var tenantOwn = NotificationTemplateSeed.TenantSuspendedV1("en");
        tenantOwn.TenantId = tenantId;
        tenantOwn.IsPlatformDefault = false;
        tenantOwn.Locale = "fr";
        tenantOwn.SubjectTemplate = "Notre propre objet";
        await Templates.InsertOneAsync(tenantOwn);

        var result = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        // Five of six 1.0.0 rows were untouched and are carried forward; the operator's one is kept.
        Assert.Equal(5, result.Upgraded);
        Assert.Equal(1, result.KeptModified);

        var kept = await Templates.Find(t => t.Id == operatorEdit.Id).SingleAsync();
        Assert.Equal("<p>Our own words for {{TenantDisplayName}} ({{TenantId}}).</p>", kept.BodyHtmlTemplate);
        Assert.Equal("1.0.0", kept.SemanticVersion);
        Assert.Equal("operator", kept.UpdatedBy);

        var own = await Templates.Find(t => t.Id == tenantOwn.Id).SingleAsync();
        Assert.Equal("Notre propre objet", own.SubjectTemplate);
        Assert.Null(own.Shell);

        foreach (var key in new[] { "tenant.invite.email", "tenant.suspended.email", "tenant.reactivated.email" })
        {
            var platform = await Templates.Find(t => t.TemplateKey == key && t.TenantId == null && t.IsPlatformDefault).ToListAsync();
            Assert.Equal(NotificationTemplateSeed.TenantLocales.OrderBy(x => x), platform.Select(t => t.Locale).OrderBy(x => x));
            Assert.All(platform.Where(t => t.Id != operatorEdit.Id), t =>
            {
                // BL-454 slice 2 stage D — the invitation is at 1.2.0 (the set-password link); the other two at 1.1.0.
                Assert.Equal(key == "tenant.invite.email" ? "1.2.0" : "1.1.0", t.SemanticVersion);
                Assert.NotNull(t.Shell);
            });
        }

        // A second start writes nothing.
        var second = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });
        Assert.Equal(0, second.Inserted);
        Assert.Equal(0, second.Upgraded);
    }

    // ---------------------------------------------------------------- 5. a document from before the newer fields

    [Fact]
    public async Task A_dispatch_document_without_the_newer_fields_is_retried_and_closed_by_the_window_end_to_end()
    {
        var dispatches = new NotificationDispatchRepository(_harness.DbContext);
        var tenantId = Guid.NewGuid();
        var due = await InsertLegacyAsync(tenantId, queuedHoursAgo: 1, retryCount: 1);
        var stale = await InsertLegacyAsync(tenantId, queuedHoursAgo: 30, retryCount: 1);
        var ancientPermanent = await InsertLegacyAsync(tenantId, queuedHoursAgo: 200, retryCount: 5); // failed for good before BL-406

        // Read back without error, and the missing marker reads as "not permanent".
        var read = (await dispatches.GetByIdForTenantAsync(tenantId, stale))!;
        Assert.Null(read.PermanentlyFailedNotifiedAt);
        Assert.False(NotificationDispatch.IsPermanentFailurePending(read));

        // The retry query still sees the due one; the window query sees the two old ones; nothing is "pending".
        Assert.Contains(await dispatches.FindDueRetriesAsync(DateTimeOffset.UtcNow, 5, 500), h => h.DispatchId == due);
        var expired = await dispatches.FindRetryWindowExpiredAsync(DateTimeOffset.UtcNow.AddHours(-24), 500);
        Assert.Contains(expired, h => h.DispatchId == stale);
        Assert.Contains(expired, h => h.DispatchId == ancientPermanent);
        Assert.DoesNotContain(await dispatches.FindPermanentFailurePendingAsync(DateTimeOffset.UtcNow.AddHours(1), 500), h => h.TenantId == tenantId);

        var sweep = TestSweeps.Create(
            dispatches, new NothingScheduled(), NullLogger<EmailDispatchSweepJob>.Instance, new Pipeline(dispatches),
            Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = 24 }), new TenantContext(),
            new Diten.Platform.Application.Features.Notifications.Services.NotificationPermanentFailureEffects(null, null, null, null));

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(BatchSize: 500), new BackgroundJobContext(), CancellationToken.None);

        foreach (var id in new[] { stale, ancientPermanent })
        {
            var closed = (await dispatches.GetByIdForTenantAsync(tenantId, id))!;
            Assert.Equal(EmailDispatchSweepJob.RetryWindowExpiredCode, closed.ErrorCode);
            Assert.Equal(NotificationDispatch.ReleasedVariablesJson, closed.VariablesJson);
            Assert.NotNull(closed.PermanentlyFailedNotifiedAt);
            Assert.False(NotificationDispatch.IsPermanentFailurePending(closed)); // the effects ran: the real time, not pending
        }

        var untouched = (await dispatches.GetByIdForTenantAsync(tenantId, due))!;
        Assert.Null(untouched.PermanentlyFailedNotifiedAt);
        Assert.Contains("Legacy", untouched.VariablesJson);
    }

    /// <summary>
    /// A dispatch as a document written before BL-406 / BL-374 / BL-454: the fields those added
    /// (PermanentlyFailedNotifiedAt, TemplateSemanticVersion, Attachments, MeetingAttendeeUserId) are ABSENT, not null.
    /// </summary>
    private async Task<Guid> InsertLegacyAsync(Guid tenantId, int queuedHoursAgo, int retryCount)
    {
        var dispatch = new NotificationDispatch
        {
            TenantId = tenantId,
            TemplateKey = "platform.tasks.assigned",
            Locale = "en",
            Channel = NotificationChannelCode.Email,
            ProviderCode = MessagingProviderCode.Fake,
            Status = NotificationDispatchStatus.Failed,
            To = [new EmailRecipient { Email = "legacy@example.test" }],
            Subject = "Legacy",
            VariablesJson = "{\"TaskTitle\":\"Legacy\"}",
            QueuedAt = DateTimeOffset.UtcNow.AddHours(-queuedHoursAgo),
            RetryCount = retryCount,
            NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            ErrorCode = "ProviderConnectivityFailed"
        };
        var document = dispatch.ToBsonDocument();
        foreach (var field in new[] { "PermanentlyFailedNotifiedAt", "TemplateSemanticVersion", "Attachments", "MeetingAttendeeUserId" })
        {
            if (document.Contains(field))
            {
                document.Remove(field);
            }
        }

        Assert.False(document.Contains("PermanentlyFailedNotifiedAt"));
        await _harness.Database.GetCollection<BsonDocument>(PlatformCollections.NotificationDispatches).InsertOneAsync(document);
        return dispatch.Id;
    }

    /// <summary>The mark-failed command as production runs it: the production validator, then the real handler.</summary>
    private sealed class Pipeline(NotificationDispatchRepository dispatches) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object response = request switch
            {
                MarkNotificationDispatchFailedCommand failed => await new ValidationBehavior<MarkNotificationDispatchFailedCommand, Response<NotificationDispatchDto>>(
                        [new MarkNotificationDispatchFailedValidator()])
                    .Handle(failed, () => new MarkNotificationDispatchFailedHandler(
                            dispatches, new NotificationsSmtpIntegrationTests.RecordingEventBus())
                        .Handle(failed, cancellationToken), cancellationToken),
                _ => throw new NotSupportedException(request.GetType().Name)
            };
            return (TResponse)response;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class NothingScheduled : IBackgroundJobScheduler
    {
        public Task<string> EnqueueAsync<TArgs, THandler>(TArgs args, BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : IBackgroundJobHandler<TArgs> => Task.FromResult("job");

        public Task<string> ScheduleAsync<TArgs, THandler>(TArgs args, DateTimeOffset enqueueAtUtc, BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : IBackgroundJobHandler<TArgs> => Task.FromResult("job");

        public Task RegisterRecurringAsync(RecurringJobRegistration registration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
