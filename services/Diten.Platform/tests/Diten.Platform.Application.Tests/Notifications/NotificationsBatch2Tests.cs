using System.Reflection;
using System.Text.Json;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Eventing;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Contracts.Events.Notifications;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

public sealed class NotificationsBatch2Tests
{
    // ---------- EmailDispatchJob (MOD-0026 seam) ----------

    [Fact]
    public void EmailDispatchJob_ShouldImplementMod0026PublicAbstraction()
    {
        Assert.True(typeof(IBackgroundJobHandler<EmailDispatchJobArgs>).IsAssignableFrom(typeof(EmailDispatchJob)));
    }

    [Fact]
    public async Task EmailDispatchJob_ShouldMarkSent_WhenProviderAccepts()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateQueuedDispatch(tenantId);
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);

        var bus = new RecordingEventBus();
        var mediator = BuildMediator(dispatches, bus);
        var provider = new CountingProvider(MessagingProviderResult.Success("fake-ok"));
        var job = BuildJob(dispatches, provider, mediator);

        await job.HandleAsync(new EmailDispatchJobArgs(tenantId, dispatch.Id), new BackgroundJobContext());

        Assert.Equal(NotificationDispatchStatus.Sent, dispatch.Status);
        Assert.Equal("fake-ok", dispatch.ProviderMessageId);
        Assert.NotNull(dispatch.SentAt);
        Assert.Equal(1, provider.CallCount);
        Assert.Contains(bus.Published, e => e is NotificationDispatchSentV1);
    }

    [Fact]
    public async Task EmailDispatchJob_ShouldMarkFailed_AndUpdateRetryMetadata_WhenProviderRejects()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateQueuedDispatch(tenantId);
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);

        var bus = new RecordingEventBus();
        var mediator = BuildMediator(dispatches, bus);
        var provider = new CountingProvider(MessagingProviderResult.Fail("ProviderRejected", "test reject"));
        var job = BuildJob(dispatches, provider, mediator);

        await job.HandleAsync(new EmailDispatchJobArgs(tenantId, dispatch.Id), new BackgroundJobContext());

        Assert.Equal(NotificationDispatchStatus.Failed, dispatch.Status);
        Assert.Equal("ProviderRejected", dispatch.ErrorCode);
        Assert.NotNull(dispatch.FailedAt);
        Assert.Equal(1, dispatch.RetryCount);
        Assert.NotNull(dispatch.NextRetryAt);
        Assert.True(dispatch.NextRetryAt > DateTimeOffset.UtcNow);
        Assert.Contains(bus.Published, e => e is NotificationDispatchFailedV1 failed
            && failed.RetryCount == 1
            && failed.NextRetryAtUtc.HasValue);
    }

    [Fact]
    public async Task EmailDispatchJob_ShouldNotCrash_AndShouldRedact_WhenProviderThrows()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateQueuedDispatch(tenantId);
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);

        var bus = new RecordingEventBus();
        var mediator = BuildMediator(dispatches, bus);
        var provider = new ThrowingProvider(new InvalidOperationException("password=plain-secret token=raw"));
        var job = BuildJob(dispatches, provider, mediator);

        var exception = await Record.ExceptionAsync(() => job.HandleAsync(new EmailDispatchJobArgs(tenantId, dispatch.Id), new BackgroundJobContext()));

        Assert.Null(exception);
        Assert.Equal(NotificationDispatchStatus.Failed, dispatch.Status);
        Assert.Equal("ProviderException", dispatch.ErrorCode);
        Assert.DoesNotContain("plain-secret", dispatch.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailDispatchJob_ShouldSkip_WhenDispatchAlreadySent()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateQueuedDispatch(tenantId);
        dispatch.TryMarkSent("already-sent", DateTimeOffset.UtcNow);
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);

        var bus = new RecordingEventBus();
        var mediator = BuildMediator(dispatches, bus);
        var provider = new CountingProvider(MessagingProviderResult.Success("should-not-be-called"));
        var job = BuildJob(dispatches, provider, mediator);

        await job.HandleAsync(new EmailDispatchJobArgs(tenantId, dispatch.Id), new BackgroundJobContext());

        Assert.Equal(0, provider.CallCount);
        Assert.Equal("already-sent", dispatch.ProviderMessageId);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task EmailDispatchJob_ShouldReturn_WhenDispatchNotFound()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        var bus = new RecordingEventBus();
        var mediator = BuildMediator(dispatches, bus);
        var provider = new CountingProvider(MessagingProviderResult.Success("never"));
        var job = BuildJob(dispatches, provider, mediator);

        await job.HandleAsync(new EmailDispatchJobArgs(tenantId, Guid.NewGuid()), new BackgroundJobContext());

        Assert.Equal(0, provider.CallCount);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task EmailDispatchJob_ShouldEnforceCrossTenantIsolation()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dispatch = CreateQueuedDispatch(tenantB);
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);

        var bus = new RecordingEventBus();
        var mediator = BuildMediator(dispatches, bus);
        var provider = new CountingProvider(MessagingProviderResult.Success("never"));
        var job = BuildJob(dispatches, provider, mediator);

        await job.HandleAsync(new EmailDispatchJobArgs(tenantA, dispatch.Id), new BackgroundJobContext());

        Assert.Equal(0, provider.CallCount);
        Assert.Equal(NotificationDispatchStatus.Queued, dispatch.Status);
    }

    // ---------- EmailDispatchSweepJob (recurring retry sweep) ----------

    [Fact]
    public void EmailDispatchSweepJob_ShouldImplementMod0026PublicAbstraction()
    {
        Assert.True(typeof(IBackgroundJobHandler<EmailDispatchSweepJobArgs>).IsAssignableFrom(typeof(EmailDispatchSweepJob)));
    }

    [Fact]
    public async Task EmailDispatchSweepJob_ShouldEnqueueOneTargetedJobPerDueDispatch()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        var due1 = CreateFailedDispatch(tenantA, retryCount: 1, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        var due2 = CreateFailedDispatch(tenantB, retryCount: 0, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await dispatches.CreateAsync(due1);
        await dispatches.CreateAsync(due2);

        var scheduler = new RecordingScheduler();
        var sweep = new EmailDispatchSweepJob(dispatches, scheduler, NullLogger<EmailDispatchSweepJob>.Instance);

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(BatchSize: 50, MaxRetryCount: 5), new BackgroundJobContext());

        Assert.Equal(2, scheduler.EnqueuedTargetedJobs.Count);
        Assert.Contains(scheduler.EnqueuedTargetedJobs, args => args.TenantId == tenantA && args.DispatchId == due1.Id);
        Assert.Contains(scheduler.EnqueuedTargetedJobs, args => args.TenantId == tenantB && args.DispatchId == due2.Id);
    }

    [Fact]
    public async Task EmailDispatchSweepJob_ShouldSkipTerminalDispatches()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        var sent = CreateFailedDispatch(tenantId, retryCount: 1, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        sent.Status = NotificationDispatchStatus.Sent;
        var cancelled = CreateFailedDispatch(tenantId, retryCount: 1, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        cancelled.Status = NotificationDispatchStatus.Cancelled;
        var queuedOnly = CreateFailedDispatch(tenantId, retryCount: 0, nextRetryAt: null);
        queuedOnly.Status = NotificationDispatchStatus.Queued;
        await dispatches.CreateAsync(sent);
        await dispatches.CreateAsync(cancelled);
        await dispatches.CreateAsync(queuedOnly);

        var scheduler = new RecordingScheduler();
        var sweep = new EmailDispatchSweepJob(dispatches, scheduler, NullLogger<EmailDispatchSweepJob>.Instance);

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext());

        Assert.Empty(scheduler.EnqueuedTargetedJobs);
    }

    [Fact]
    public async Task EmailDispatchSweepJob_ShouldSkipNotYetDueDispatches()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        var notDueYet = CreateFailedDispatch(tenantId, retryCount: 1, nextRetryAt: DateTimeOffset.UtcNow.AddHours(1));
        var noNextRetry = CreateFailedDispatch(tenantId, retryCount: 1, nextRetryAt: null);
        await dispatches.CreateAsync(notDueYet);
        await dispatches.CreateAsync(noNextRetry);

        var scheduler = new RecordingScheduler();
        var sweep = new EmailDispatchSweepJob(dispatches, scheduler, NullLogger<EmailDispatchSweepJob>.Instance);

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext());

        Assert.Empty(scheduler.EnqueuedTargetedJobs);
    }

    [Fact]
    public async Task EmailDispatchSweepJob_ShouldSkipDispatchesAtOrAboveMaxRetryCount()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        var exhausted = CreateFailedDispatch(tenantId, retryCount: 5, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var retryable = CreateFailedDispatch(tenantId, retryCount: 4, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await dispatches.CreateAsync(exhausted);
        await dispatches.CreateAsync(retryable);

        var scheduler = new RecordingScheduler();
        var sweep = new EmailDispatchSweepJob(dispatches, scheduler, NullLogger<EmailDispatchSweepJob>.Instance);

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(BatchSize: 50, MaxRetryCount: 5), new BackgroundJobContext());

        Assert.Single(scheduler.EnqueuedTargetedJobs);
        Assert.Equal(retryable.Id, scheduler.EnqueuedTargetedJobs[0].DispatchId);
    }

    [Fact]
    public async Task EmailDispatchSweepJob_ShouldRespectBatchSize()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        for (var i = 0; i < 10; i++)
        {
            await dispatches.CreateAsync(CreateFailedDispatch(tenantId, retryCount: 0, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(-10 + i)));
        }

        var scheduler = new RecordingScheduler();
        var sweep = new EmailDispatchSweepJob(dispatches, scheduler, NullLogger<EmailDispatchSweepJob>.Instance);

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(BatchSize: 3, MaxRetryCount: 5), new BackgroundJobContext());

        Assert.Equal(3, scheduler.EnqueuedTargetedJobs.Count);
    }

    [Fact]
    public async Task EmailDispatchSweepJob_ShouldCarryTenantContextOntoEachEnqueuedJob()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(CreateFailedDispatch(tenantA, retryCount: 0, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        await dispatches.CreateAsync(CreateFailedDispatch(tenantB, retryCount: 0, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

        var scheduler = new RecordingScheduler();
        var sweep = new EmailDispatchSweepJob(dispatches, scheduler, NullLogger<EmailDispatchSweepJob>.Instance);

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext());

        Assert.Equal(2, scheduler.EnqueuedContexts.Count);
        Assert.Contains(scheduler.EnqueuedContexts, ctx => ctx.TenantId == tenantA);
        Assert.Contains(scheduler.EnqueuedContexts, ctx => ctx.TenantId == tenantB);
        Assert.All(scheduler.EnqueuedContexts, ctx => Assert.Equal(BackgroundJobTriggerTypes.Recurring, ctx.TriggerType));
        Assert.All(scheduler.EnqueuedContexts, ctx => Assert.Equal(nameof(EmailDispatchSweepJob), ctx.TriggeredBy));
    }

    [Fact]
    public async Task EmailDispatchSweepJob_ShouldNotCrash_WhenSchedulerEnqueueThrows()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(CreateFailedDispatch(tenantId, retryCount: 0, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        var scheduler = new RecordingScheduler { ThrowOnEnqueue = true };
        var sweep = new EmailDispatchSweepJob(dispatches, scheduler, NullLogger<EmailDispatchSweepJob>.Instance);

        var exception = await Record.ExceptionAsync(() => sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext()));

        Assert.Null(exception);
    }

    // ---------- Event mapping seam (MOD-0035) ----------

    [Fact]
    public void NotificationEventContracts_ShouldHaveStableNamesAndVersions()
    {
        Assert.Equal("notifications.email.queued.v1", NotificationEmailQueuedV1.Name);
        Assert.Equal(1, NotificationEmailQueuedV1.Version);
        Assert.Equal("notifications.dispatch.sent.v1", NotificationDispatchSentV1.Name);
        Assert.Equal(1, NotificationDispatchSentV1.Version);
        Assert.Equal("notifications.dispatch.failed.v1", NotificationDispatchFailedV1.Name);
        Assert.Equal(1, NotificationDispatchFailedV1.Version);
        Assert.Equal("notifications.dispatch.cancelled.v1", NotificationDispatchCancelledV1.Name);
        Assert.Equal(1, NotificationDispatchCancelledV1.Version);
    }

    [Fact]
    public void NotificationEventContracts_ShouldImplementInternalEventMarker()
    {
        Assert.True(typeof(IInternalEvent).IsAssignableFrom(typeof(NotificationEmailQueuedV1)));
        Assert.True(typeof(IInternalEvent).IsAssignableFrom(typeof(NotificationDispatchSentV1)));
        Assert.True(typeof(IInternalEvent).IsAssignableFrom(typeof(NotificationDispatchFailedV1)));
        Assert.True(typeof(IInternalEvent).IsAssignableFrom(typeof(NotificationDispatchCancelledV1)));
    }

    [Fact]
    public void INotificationEventMapper_ShouldBeAvailableAsInboundSeam()
    {
        var mapperType = typeof(INotificationEventMapper<>);
        Assert.True(mapperType.IsInterface);
        var genericConstraint = mapperType.GetGenericArguments().Single().GetGenericParameterConstraints();
        Assert.Contains(typeof(IIntegrationEvent), genericConstraint);
    }

    [Fact]
    public async Task MarkSentHandler_ShouldPublishDispatchSentEvent()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateQueuedDispatch(tenantId);
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);
        var bus = new RecordingEventBus();
        var handler = new MarkNotificationDispatchSentHandler(dispatches, bus);

        var response = await handler.Handle(new MarkNotificationDispatchSentCommand(tenantId, dispatch.Id, "msg-1"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(bus.Published);
        var sent = Assert.IsType<NotificationDispatchSentV1>(bus.Published[0]);
        Assert.Equal(dispatch.Id, sent.DispatchId);
        Assert.Equal("msg-1", sent.ProviderMessageId);
    }

    [Fact]
    public async Task MarkFailedHandler_ShouldPublishDispatchFailedEvent_WithRetryMetadata()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateQueuedDispatch(tenantId);
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);
        var bus = new RecordingEventBus();
        var handler = new MarkNotificationDispatchFailedHandler(dispatches, bus);
        var nextRetry = DateTimeOffset.UtcNow.AddMinutes(5);

        var response = await handler.Handle(
            new MarkNotificationDispatchFailedCommand(tenantId, dispatch.Id, "ProviderRejected", "rejected", RetryCount: 3, NextRetryAt: nextRetry),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(3, dispatch.RetryCount);
        Assert.Equal(nextRetry, dispatch.NextRetryAt);
        var failed = Assert.IsType<NotificationDispatchFailedV1>(bus.Published.Single());
        Assert.Equal(3, failed.RetryCount);
        Assert.Equal(nextRetry, failed.NextRetryAtUtc);
    }

    [Fact]
    public async Task CancelHandler_ShouldPublishDispatchCancelledEvent()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateQueuedDispatch(tenantId);
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);
        var bus = new RecordingEventBus();
        var handler = new CancelNotificationDispatchHandler(dispatches, bus);

        var response = await handler.Handle(new CancelNotificationDispatchCommand(tenantId, dispatch.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var cancelled = Assert.IsType<NotificationDispatchCancelledV1>(bus.Published.Single());
        Assert.Equal(dispatch.Id, cancelled.DispatchId);
    }

    [Fact]
    public async Task MarkFailedHandler_ShouldRejectInvalidTransition_AndNotPublishEvent()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateQueuedDispatch(tenantId);
        dispatch.TryMarkSent("done", DateTimeOffset.UtcNow);
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);
        var bus = new RecordingEventBus();
        var handler = new MarkNotificationDispatchFailedHandler(dispatches, bus);

        var response = await handler.Handle(
            new MarkNotificationDispatchFailedCommand(tenantId, dispatch.Id, "ProviderRejected", "rejected"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Empty(bus.Published);
    }

    // ---------- No direct RabbitMQ/MassTransit usage in notification handlers ----------

    [Fact]
    public void NotificationFeature_ShouldNotReferenceRabbitMqOrMassTransitDirectly()
    {
        var applicationAssembly = typeof(EmailDispatchJob).Assembly;

        var offenders = applicationAssembly.GetTypes()
            .Where(t => t.Namespace is { } ns && ns.StartsWith("Diten.Platform.Application.Features.Notifications", StringComparison.Ordinal))
            .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(f => f.FieldType)
                .Concat(t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
                .Concat(t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .SelectMany(c => c.GetParameters().Select(p => p.ParameterType))))
            .Where(type => type.FullName is { } fullName
                && (fullName.StartsWith("MassTransit.", StringComparison.Ordinal)
                    || fullName.StartsWith("RabbitMQ.", StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(offenders);
    }

    // ---------- helpers ----------

    private static NotificationDispatch CreateQueuedDispatch(Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            TemplateKey = "tenant.invite.email",
            Locale = "en",
            Channel = NotificationChannelCode.Email,
            ProviderCode = MessagingProviderCode.Fake,
            Status = NotificationDispatchStatus.Queued,
            To = [new EmailRecipient { Email = "user@example.com" }],
            Subject = "Subject",
            VariablesJson = JsonSerializer.Serialize(new { ok = true }),
            QueuedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-batch2"
        };

    private static NotificationDispatch CreateFailedDispatch(Guid tenantId, int retryCount, DateTimeOffset? nextRetryAt)
    {
        var dispatch = CreateQueuedDispatch(tenantId);
        dispatch.Status = NotificationDispatchStatus.Failed;
        dispatch.RetryCount = retryCount;
        dispatch.NextRetryAt = nextRetryAt;
        dispatch.ErrorCode = "ProviderRejected";
        dispatch.ErrorMessage = "redacted";
        dispatch.FailedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        return dispatch;
    }

    private static EmailDispatchJob BuildJob(InMemoryDispatches dispatches, IMessagingProvider provider, IMediator mediator) =>
        new(
            dispatches,
            new FixedSettingsResolver(),
            new SingleProviderResolver(provider),
            mediator,
            NullLogger<EmailDispatchJob>.Instance);

    private static IMediator BuildMediator(InMemoryDispatches dispatches, IEventBus bus) =>
        new DirectMediator(
            new MarkNotificationDispatchSentHandler(dispatches, bus),
            new MarkNotificationDispatchFailedHandler(dispatches, bus),
            new CancelNotificationDispatchHandler(dispatches, bus));

    private sealed class InMemoryDispatches : INotificationDispatchRepository
    {
        public List<NotificationDispatch> Items { get; } = [];

        public Task<NotificationDispatch> CreateAsync(NotificationDispatch dispatch, CancellationToken ct = default)
        {
            Items.Add(dispatch);
            return Task.FromResult(dispatch);
        }

        public Task<NotificationDispatch?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => !x.IsDeleted && x.TenantId == tenantId && x.Id == id));

        public Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => !x.IsDeleted && x.TenantId == tenantId).Skip(skip).Take(take).ToArray() as IReadOnlyList<NotificationDispatch>);

        public Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<NotificationDispatchRetryHandle>> FindDueRetriesAsync(DateTimeOffset asOfUtc, int maxRetryCount, int take, CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(x => !x.IsDeleted
                    && x.Status == NotificationDispatchStatus.Failed
                    && x.RetryCount < maxRetryCount
                    && x.NextRetryAt.HasValue
                    && x.NextRetryAt <= asOfUtc)
                .OrderBy(x => x.NextRetryAt)
                .Take(Math.Max(0, take))
                .Select(x => new NotificationDispatchRetryHandle(x.TenantId, x.Id))
                .ToArray() as IReadOnlyList<NotificationDispatchRetryHandle>);
    }

    private sealed class CountingProvider : IMessagingProvider
    {
        private readonly MessagingProviderResult _result;
        public CountingProvider(MessagingProviderResult result) => _result = result;
        public int CallCount { get; private set; }
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;
        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingProvider : IMessagingProvider
    {
        private readonly Exception _exception;
        public ThrowingProvider(Exception exception) => _exception = exception;
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;
        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default) =>
            throw _exception;
    }

    private sealed class SingleProviderResolver : IMessagingProviderResolver
    {
        private readonly IMessagingProvider _provider;
        public SingleProviderResolver(IMessagingProvider provider) => _provider = provider;
        public Response<IMessagingProvider> Resolve(MessagingProviderCode providerCode) =>
            providerCode == _provider.ProviderCode
                ? Response<IMessagingProvider>.Success(_provider)
                : Response<IMessagingProvider>.Fail("Provider unavailable.", 400);
    }

    private sealed class FixedSettingsResolver : ITenantMessagingSettingsResolver
    {
        public Task<Response<ResolvedMessagingSettingsDto>> ResolveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Response<ResolvedMessagingSettingsDto>.Success(
                new ResolvedMessagingSettingsDto(
                    Guid.NewGuid(),
                    tenantId,
                    tenantId,
                    false,
                    MessagingProviderCode.Fake.ToString(),
                    "sender@example.com",
                    "Sender",
                    null,
                    true,
                    NotificationFallbackPolicy.UsePlatformDefault.ToString())));
    }

    private sealed class RecordingScheduler : IBackgroundJobScheduler
    {
        public List<EmailDispatchJobArgs> EnqueuedTargetedJobs { get; } = [];
        public List<BackgroundJobContext> EnqueuedContexts { get; } = [];
        public bool ThrowOnEnqueue { get; set; }

        public Task<string> EnqueueAsync<TArgs, THandler>(TArgs args, BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : IBackgroundJobHandler<TArgs>
        {
            if (ThrowOnEnqueue)
            {
                throw new InvalidOperationException("scheduler offline");
            }

            if (args is EmailDispatchJobArgs targeted)
            {
                EnqueuedTargetedJobs.Add(targeted);
            }
            EnqueuedContexts.Add(context ?? new BackgroundJobContext());
            return Task.FromResult(Guid.NewGuid().ToString("N"));
        }

        public Task<string> ScheduleAsync<TArgs, THandler>(TArgs args, DateTimeOffset enqueueAtUtc, BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : IBackgroundJobHandler<TArgs> =>
            EnqueueAsync<TArgs, THandler>(args, context, cancellationToken);

        public Task RegisterRecurringAsync(RecurringJobRegistration registration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingEventBus : IEventBus
    {
        public List<IIntegrationEvent> Published { get; } = [];

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent =>
            PublishAsync(@event, new EventPublishOptions(), cancellationToken);

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, EventPublishOptions options, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Published.Add(@event);
            var metadata = new EventMetadata(
                options.EventId ?? Guid.NewGuid(),
                @event.EventName,
                @event.EventVersion,
                options.CorrelationId ?? Guid.NewGuid(),
                options.CausationId,
                options.TenantId,
                string.IsNullOrWhiteSpace(options.Producer) ? "test" : options.Producer,
                options.OccurredAtUtc ?? DateTimeOffset.UtcNow);
            return Task.FromResult(new EventEnvelope<TEvent>(metadata, @event));
        }
    }

    private sealed class DirectMediator : IMediator
    {
        private readonly MarkNotificationDispatchSentHandler _sent;
        private readonly MarkNotificationDispatchFailedHandler _failed;
        private readonly CancelNotificationDispatchHandler _cancel;

        public DirectMediator(
            MarkNotificationDispatchSentHandler sent,
            MarkNotificationDispatchFailedHandler failed,
            CancelNotificationDispatchHandler cancel)
        {
            _sent = sent;
            _failed = failed;
            _cancel = cancel;
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return request switch
            {
                MarkNotificationDispatchSentCommand sentCmd => (TResponse)(object)await _sent.Handle(sentCmd, cancellationToken),
                MarkNotificationDispatchFailedCommand failedCmd => (TResponse)(object)await _failed.Handle(failedCmd, cancellationToken),
                CancelNotificationDispatchCommand cancelCmd => (TResponse)(object)await _cancel.Handle(cancelCmd, cancellationToken),
                _ => throw new NotSupportedException($"DirectMediator does not handle {request.GetType().Name}.")
            };
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
