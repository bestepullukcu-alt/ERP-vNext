using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// Go-live, 2026-09-13 — scenario C, the SECOND half. <c>NotificationsBatch1ATests.QueueEmail_ShouldScheduleTheFirstRetry_WhenTheFirstSendFails</c>
/// proves a failed first send leaves a NextRetryAt behind; the <c>EmailDispatchSweepJob_*</c> tests in
/// <c>NotificationsBatch2Tests</c> prove the sweep's selection — but against an in-memory COPY of
/// <c>FindDueRetriesAsync</c>'s filter, never the Mongo query, and never on a row the queue handler itself wrote.
/// Nothing chained the two, so nothing proved the mail that failed first is actually sent later.
///
/// <para>Real here: <see cref="QueueEmailNotificationHandler"/>, <see cref="NotificationDispatchRepository"/> and
/// <see cref="NotificationTemplateRepository"/> over MongoDB, <see cref="EmailTemplateRenderer"/>,
/// <see cref="EmailDispatchSweepJob"/>, <see cref="EmailDispatchJob"/> and the mark-sent/mark-failed handlers.
/// Doubled: the messaging provider (first call fails, second succeeds), the tenant settings resolver, the event bus,
/// and the Hangfire scheduler, which records what the sweep enqueues so the test can run that job the way Hangfire
/// would. The clock cannot be injected into the sweep, so "time passes" is the dispatch row's own NextRetryAt moved
/// back five minutes — a null NextRetryAt stays null.</para>
/// </summary>
public sealed class EmailDispatchRetrySweepMongoTests : IAsyncLifetime
{
    private const string TemplateKey = "platform.meetings.invite";

    private readonly ITestOutputHelper _output;
    private MongoIntegrationHarness _harness = null!;

    public EmailDispatchRetrySweepMongoTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Notification);

    public async Task DisposeAsync()
    {
        var tenant = _harness.TenantId;
        await _harness.Database.GetCollection<NotificationDispatch>(PlatformCollections.NotificationDispatches)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.DisposeAsync();
    }

    [Fact]
    public async Task A_first_send_that_fails_is_picked_up_by_the_retry_sweep_once_due_and_sent_on_that_run()
    {
        var tenantId = _harness.TenantId;
        var dispatches = new NotificationDispatchRepository(_harness.DbContext);
        var templates = new NotificationTemplateRepository(_harness.DbContext);
        await templates.CreateAsync(new NotificationTemplate
        {
            TenantId = tenantId,
            IsPlatformDefault = false,
            TemplateKey = TemplateKey,
            Channel = NotificationChannelCode.Email,
            Locale = "en",
            SubjectTemplate = "Invitation {{MeetingTitle}}",
            BodyHtmlTemplate = "<p>{{MeetingTitle}}</p>",
            BodyTextTemplate = "{{MeetingTitle}}",
            Status = NotificationTemplateStatus.Active,
            SemanticVersion = "1.0.0",
            Variables = []
        });
        var provider = new ScriptedProvider(
            MessagingProviderResult.Fail("ProviderConnectivityFailed", "smtp down"),
            MessagingProviderResult.Success("sent-on-retry"));
        var bus = new NoOpEventBus();

        // 1. The first, synchronous send fails.
        var queued = await new QueueEmailNotificationHandler(
                new FixedSettingsResolver(), templates, new EmailTemplateRenderer(), dispatches,
                new SingleProviderResolver(provider), bus, NullLogger<QueueEmailNotificationHandler>.Instance)
            .Handle(
                new QueueEmailNotificationCommand(
                    tenantId,
                    new QueueEmailNotificationRequest(
                        TemplateKey: TemplateKey,
                        Locale: "en",
                        Variables: new Dictionary<string, object?> { ["MeetingTitle"] = "WeeklyReview" },
                        To: [new EmailRecipientDto("alice@example.test", "Alice")]),
                    "corr-retry-sweep"),
                CancellationToken.None);
        Assert.False(queued.IsSuccessful);
        var failed = Assert.Single(await dispatches.ListByTenantAsync(tenantId));
        Assert.Equal(NotificationDispatchStatus.Failed, failed.Status);

        // 2. A sweep before the retry is due leaves it alone.
        var early = await SweepAsync(dispatches);
        Assert.DoesNotContain(early, args => args.DispatchId == failed.Id);

        // 3. Time passes: the row's own retry moment is now in the past.
        var row = (await dispatches.GetByIdForTenantAsync(tenantId, failed.Id))!;
        row.NextRetryAt = row.NextRetryAt?.AddMinutes(-5);
        await dispatches.UpdateAsync(row);

        // 4. The next sweep picks it up, and the job it enqueues sends it.
        var due = await SweepAsync(dispatches);
        var enqueued = Assert.Single(due, args => args.TenantId == tenantId);
        Assert.Equal(failed.Id, enqueued.DispatchId);

        var job = new EmailDispatchJob(
            dispatches, new FixedSettingsResolver(), new SingleProviderResolver(provider),
            new DirectMediator(
                new MarkNotificationDispatchSentHandler(dispatches, bus),
                new MarkNotificationDispatchFailedHandler(dispatches, bus),
                new CancelNotificationDispatchHandler(dispatches, bus)),
            NullLogger<EmailDispatchJob>.Instance, templates, new EmailTemplateRenderer());
        await job.HandleAsync(enqueued, new BackgroundJobContext(TenantId: tenantId), CancellationToken.None);

        // The provider DID accept the retry — whatever the row says next is the platform's own bookkeeping.
        Assert.Equal(2, provider.Requests.Count);
        Assert.All(provider.Requests, r => Assert.Equal(failed.Id, r.DispatchId));

        var sent = (await dispatches.GetByIdForTenantAsync(tenantId, failed.Id))!;
        var later = await SweepAsync(dispatches);
        _output.WriteLine(
            $"after retry job: providerCalls={provider.Requests.Count} Status={sent.Status} RetryCount={sent.RetryCount} " +
            $"NextRetryAt={sent.NextRetryAt:o} ProviderMessageId={sent.ProviderMessageId ?? "<null>"} " +
            $"reEnqueuedByNextSweep={later.Any(a => a.DispatchId == failed.Id)}");

        Assert.Equal(NotificationDispatchStatus.Sent, sent.Status);
        Assert.Equal("sent-on-retry", sent.ProviderMessageId);
        // A mail the provider already accepted must never be picked up — and sent — again.
        Assert.DoesNotContain(later, args => args.DispatchId == failed.Id);
    }

    private static async Task<IReadOnlyList<EmailDispatchJobArgs>> SweepAsync(NotificationDispatchRepository dispatches)
    {
        var scheduler = new RecordingScheduler();
        await new EmailDispatchSweepJob(dispatches, scheduler, NullLogger<EmailDispatchSweepJob>.Instance)
            .HandleAsync(new EmailDispatchSweepJobArgs(BatchSize: 500, MaxRetryCount: 5), new BackgroundJobContext(), CancellationToken.None);
        return scheduler.Enqueued;
    }

    // ── doubles ──────────────────────────────────────────────────────────────

    private sealed class ScriptedProvider(params MessagingProviderResult[] results) : IMessagingProvider
    {
        private readonly Queue<MessagingProviderResult> _results = new(results);
        private MessagingProviderResult _last = results[^1];

        public List<MessagingProviderEmailRequest> Requests { get; } = [];
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;

        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            _last = _results.Count > 0 ? _results.Dequeue() : _last;
            return Task.FromResult(_last);
        }
    }

    private sealed class RecordingScheduler : IBackgroundJobScheduler
    {
        public List<EmailDispatchJobArgs> Enqueued { get; } = [];

        public Task<string> EnqueueAsync<TArgs, THandler>(TArgs args, BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : IBackgroundJobHandler<TArgs>
        {
            if (args is EmailDispatchJobArgs targeted)
            {
                Enqueued.Add(targeted);
            }

            return Task.FromResult(Guid.NewGuid().ToString("N"));
        }

        public Task<string> ScheduleAsync<TArgs, THandler>(TArgs args, DateTimeOffset enqueueAtUtc, BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : IBackgroundJobHandler<TArgs> => EnqueueAsync<TArgs, THandler>(args, context, cancellationToken);

        public Task RegisterRecurringAsync(RecurringJobRegistration registration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedSettingsResolver : ITenantMessagingSettingsResolver
    {
        public Task<Response<ResolvedMessagingSettingsDto>> ResolveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Response<ResolvedMessagingSettingsDto>.Success(
                new ResolvedMessagingSettingsDto(
                    Guid.NewGuid(), tenantId, tenantId, false, MessagingProviderCode.Fake.ToString(),
                    "sender@example.com", "Sender", null, true, NotificationFallbackPolicy.UsePlatformDefault.ToString())));
    }

    private sealed class SingleProviderResolver(IMessagingProvider provider) : IMessagingProviderResolver
    {
        public Response<IMessagingProvider> Resolve(MessagingProviderCode providerCode) =>
            providerCode == provider.ProviderCode
                ? Response<IMessagingProvider>.Success(provider)
                : Response<IMessagingProvider>.Fail("Provider unavailable.", 400);
    }

    private sealed class NoOpEventBus : IEventBus
    {
        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : IIntegrationEvent => PublishAsync(@event, new EventPublishOptions(), ct);

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, EventPublishOptions options, CancellationToken ct = default)
            where TEvent : IIntegrationEvent =>
            Task.FromResult(new EventEnvelope<TEvent>(
                new EventMetadata(
                    options.EventId ?? Guid.NewGuid(), @event.EventName, @event.EventVersion,
                    options.CorrelationId ?? Guid.NewGuid(), options.CausationId, options.TenantId, "test",
                    options.OccurredAtUtc ?? DateTimeOffset.UtcNow),
                @event));
    }

    private sealed class DirectMediator(
        MarkNotificationDispatchSentHandler sent,
        MarkNotificationDispatchFailedHandler failed,
        CancelNotificationDispatchHandler cancel) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            request switch
            {
                MarkNotificationDispatchSentCommand cmd => (TResponse)(object)await sent.Handle(cmd, cancellationToken),
                MarkNotificationDispatchFailedCommand cmd => (TResponse)(object)await failed.Handle(cmd, cancellationToken),
                CancelNotificationDispatchCommand cmd => (TResponse)(object)await cancel.Handle(cmd, cancellationToken),
                _ => throw new NotSupportedException($"DirectMediator does not handle {request.GetType().Name}.")
            };

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
