using System.Diagnostics;
using System.Text.Json;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Diten.Platform.Application.Features.Meetings.SelfRegistration;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-VW-W5 W5-FIX2 (F-1, F-4, F-5) — the internal event dispatch over a REAL MongoDB (DITEN_TEST_MONGOD), the whole chain
/// production runs: <see cref="InternalEventDispatchService"/> → <see cref="NotificationEventDispatchAdapter"/> →
/// <see cref="QueueEmailNotificationHandler"/> → <see cref="NotificationDispatchRepository"/>, the idempotency keys in
/// <see cref="InternalDispatchKeyRepository"/>, the platform templates as the seed writes them, the real
/// <see cref="TenantNotificationLocaleResolver"/>, then <see cref="EmailDispatchSweepJob"/> and <see cref="EmailDispatchJob"/>.
/// Doubled: the messaging provider (slow — it answers after <see cref="SlowSmtp"/>), the tenant registry, the event bus and
/// the Hangfire scheduler (it records what the sweep enqueues; the test runs that job the way Hangfire would).
/// </summary>
public sealed class W5Fix2InternalDispatchMongoTests : IAsyncLifetime
{
    private const string Crm = "MOD-0155";
    private const string PvMailbox = "pv-safety@example.test";
    private static readonly TimeSpan SlowSmtp = TimeSpan.FromSeconds(3);

    private readonly ITestOutputHelper _output;
    private MongoIntegrationHarness _harness = null!;

    public W5Fix2InternalDispatchMongoTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Notification);

    public async Task DisposeAsync()
    {
        var tenant = _harness.TenantId;
        await _harness.Database.GetCollection<NotificationDispatch>(PlatformCollections.NotificationDispatches)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<InternalDispatchKey>(PlatformCollections.InternalDispatchKeys)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.DisposeAsync();
    }

    [Fact]
    public async Task F1_the_internal_call_answers_queued_without_waiting_for_a_slow_mail_server_and_the_sweep_sends_it_once()
    {
        var world = await WorldAsync();

        var watch = Stopwatch.StartNew();
        var response = await world.Service.DispatchAsync(Adverse(key: null));
        watch.Stop();
        _output.WriteLine($"internal dispatch answered in {watch.ElapsedMilliseconds} ms (provider takes {SlowSmtp.TotalMilliseconds} ms)");

        Assert.True(response.IsSuccessful, string.Join("; ", response.Errors));
        Assert.Equal(InternalEventDispatchService.StatusQueued, response.Data!.Status);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1.5), $"the call waited for the provider: {watch.ElapsedMilliseconds} ms");
        Assert.Empty(world.Provider.Requests);

        var row = Assert.Single(await world.Dispatches.ListByTenantAsync(_harness.TenantId));
        Assert.Equal(NotificationDispatchStatus.Queued, row.Status);
        Assert.NotNull(row.NextRetryAt);
        Assert.Equal(Crm, row.SourceModule);
        // F-5 — the mailbox named no language: the tenant's (tr) is the dispatch's, not the platform floor.
        Assert.Equal("tr", row.Locale);
        // F-4 — no doctor on this report: the subject leaves that part out, separator and all.
        Assert.Equal("ACİL · Advers olay bildirimi · 24 saat", row.Subject);

        // The sweep claims it; a second sweep straight after does not enqueue the same send again.
        var first = await SweepAsync(world.Dispatches);
        var enqueued = Assert.Single(first, a => a.TenantId == _harness.TenantId);
        Assert.Equal(row.Id, enqueued.DispatchId);
        Assert.DoesNotContain(await SweepAsync(world.Dispatches), a => a.DispatchId == row.Id);

        await world.Job.HandleAsync(enqueued, new BackgroundJobContext(TenantId: _harness.TenantId), CancellationToken.None);

        var sent = (await world.Dispatches.GetByIdForTenantAsync(_harness.TenantId, row.Id))!;
        Assert.Equal(NotificationDispatchStatus.Sent, sent.Status);
        var mail = Assert.Single(world.Provider.Requests);
        Assert.Equal(PvMailbox, Assert.Single(mail.To).Email);
        Assert.Equal("ACİL · Advers olay bildirimi · 24 saat", mail.Subject);
        Assert.DoesNotContain(await SweepAsync(world.Dispatches), a => a.DispatchId == row.Id);
    }

    /// <summary>
    /// WP-VW-W6 W6-FIX-a (G-12) — the pre-order approval request carries its one-time approval link as a SECRET variable
    /// (BL-454: never stored). Queued, the sweep could only send the masked mail and closed it ACTION_LINK_NOT_RETRYABLE —
    /// the approver got nothing. A mail with a secret is sent IN the request: the provider once, the row Sent, the link
    /// in the mail and nowhere in the row; the sweep has nothing of it to claim.
    /// </summary>
    [Fact]
    public async Task G12_an_approval_request_with_a_secret_link_is_sent_in_the_request_once_and_the_row_keeps_no_link()
    {
        var world = await WorldAsync(slow: false);
        const string Link = "https://web.test/Commercial/PreOrders/ApprovalLink?token=Zm9vYmFyLXNlY3JldC10b2tlbi0xMjM0NTY3ODkw";
        var request = new InternalEventDispatchRequest(
            _harness.TenantId, CrmManifestProvider.PreOrderApprovalRequestedEvent, "CRM",
            new InternalEventRecipients([new InternalEventRecipient(Email: "approver@example.test", DisplayName: "Approver")]),
            new Dictionary<string, string?> { ["orderCode"] = "MG-ORD-000003", ["pharmacyName"] = "Etap Eczanesi", ["approvalUrl"] = Link, ["expiresAt"] = "24.10.2026 10:00" },
            "corr-w6fixa", IdempotencyKey: "preorder:w6fixa:approvalrequested:1");

        var response = await world.Service.DispatchAsync(request);

        Assert.True(response.IsSuccessful, string.Join("; ", response.Errors));
        Assert.Equal(InternalEventDispatchService.StatusSent, response.Data!.Status);
        var mail = Assert.Single(world.Provider.Requests);
        Assert.Equal("approver@example.test", Assert.Single(mail.To).Email);
        Assert.Contains(Link, mail.BodyHtml + mail.BodyText);                                     // the approver gets the link
        var row = Assert.Single(await world.Dispatches.ListByTenantAsync(_harness.TenantId));
        Assert.Equal(NotificationDispatchStatus.Sent, row.Status);
        Assert.Null(row.NextRetryAt);
        Assert.DoesNotContain("Zm9vYmFyLXNlY3JldC10b2tlbi0xMjM0NTY3ODkw", (row.VariablesJson ?? "") + row.BodyHtmlPreview + row.BodyTextPreview + row.Subject);
        Assert.DoesNotContain(await SweepAsync(world.Dispatches), a => a.DispatchId == row.Id);

        // KORU — a mail WITHOUT a secret from the same producer is still queued (F-1)
        var plain = await world.Service.DispatchAsync(Adverse(key: "w6fixa-plain"));
        Assert.Equal(InternalEventDispatchService.StatusQueued, plain.Data!.Status);
    }

    [Fact]
    public async Task F1_the_same_key_twice_opens_one_dispatch_and_answers_the_first_result()
    {
        var world = await WorldAsync();
        var key = Guid.NewGuid().ToString("D");

        var first = await world.Service.DispatchAsync(Adverse(key));
        var second = await world.Service.DispatchAsync(Adverse(key));

        Assert.True(first.IsSuccessful, string.Join("; ", first.Errors));
        Assert.True(second.IsSuccessful, string.Join("; ", second.Errors));
        Assert.Equal(Json(first.Data), Json(second.Data));
        Assert.Single(await world.Dispatches.ListByTenantAsync(_harness.TenantId));
        Assert.Equal(1, world.Adapter.Calls);

        // Another key is another request.
        var other = await world.Service.DispatchAsync(Adverse(Guid.NewGuid().ToString("D")));
        Assert.True(other.IsSuccessful);
        Assert.Equal(2, (await world.Dispatches.ListByTenantAsync(_harness.TenantId)).Count);
    }

    [Fact]
    public async Task F1_two_concurrent_calls_with_one_key_open_one_dispatch()
    {
        var world = await WorldAsync(adapterDelay: TimeSpan.FromMilliseconds(400));
        var key = Guid.NewGuid().ToString("D");

        var results = await Task.WhenAll(
            Task.Run(() => world.Service.DispatchAsync(Adverse(key))),
            Task.Run(() => world.Service.DispatchAsync(Adverse(key))));

        Assert.All(results, r => Assert.True(r.IsSuccessful, string.Join("; ", r.Errors)));
        Assert.Equal(Json(results[0].Data), Json(results[1].Data));
        Assert.Single(await world.Dispatches.ListByTenantAsync(_harness.TenantId));
        Assert.Equal(1, world.Adapter.Calls);
        var keyRow = Assert.Single(await _harness.Database.GetCollection<InternalDispatchKey>(PlatformCollections.InternalDispatchKeys)
            .Find(x => x.TenantId == _harness.TenantId).ToListAsync());
        Assert.Equal(InternalDispatchKeyStates.Done, keyRow.State);
        Assert.Equal(Crm, keyRow.SourceModule);
    }

    [Fact]
    public async Task F1_a_call_that_had_nothing_accepted_gives_its_key_back_and_the_retry_dispatches()
    {
        var world = await WorldAsync();
        var key = Guid.NewGuid().ToString("D");

        world.Adapter.FailNext = true;
        var refused = await world.Service.DispatchAsync(Adverse(key));
        Assert.False(refused.IsSuccessful);
        Assert.Empty(await world.Dispatches.ListByTenantAsync(_harness.TenantId));

        var retried = await world.Service.DispatchAsync(Adverse(key));
        Assert.True(retried.IsSuccessful, string.Join("; ", retried.Errors));
        Assert.Single(await world.Dispatches.ListByTenantAsync(_harness.TenantId));
    }

    [Fact]
    public async Task KORU_the_platform_producers_still_send_in_the_request_and_their_rows_are_not_claimed_by_the_queued_sweep()
    {
        var world = await WorldAsync(slow: false);

        // The path every platform producer (Tasks, TimeEntry, Meetings …) takes: QueueOnly is not set.
        var queued = await world.Handler.Handle(
            new QueueEmailNotificationCommand(
                _harness.TenantId,
                new QueueEmailNotificationRequest(
                    TemplateKey: CrmManifestProvider.VisitReportAdverseEventEvent,
                    Locale: "tr",
                    Variables: Variables(),
                    To: [new EmailRecipientDto("someone@example.test", "Someone")])),
            CancellationToken.None);

        Assert.True(queued.IsSuccessful, string.Join("; ", queued.Errors));
        Assert.Equal("Sent", queued.Data!.Status);
        Assert.Single(world.Provider.Requests);
        var row = Assert.Single(await world.Dispatches.ListByTenantAsync(_harness.TenantId));
        Assert.Equal(NotificationDispatchStatus.Sent, row.Status);
        Assert.Null(row.NextRetryAt);
        Assert.DoesNotContain(await SweepAsync(world.Dispatches), a => a.TenantId == _harness.TenantId);
    }

    // ── the world ──────────────────────────────────────────────────────────────────────────────

    private sealed record World(
        InternalEventDispatchService Service,
        CountingAdapter Adapter,
        QueueEmailNotificationHandler Handler,
        NotificationDispatchRepository Dispatches,
        SlowProvider Provider,
        EmailDispatchJob Job);

    private async Task<World> WorldAsync(bool slow = true, TimeSpan? adapterDelay = null)
    {
        var tenantId = _harness.TenantId;
        var dispatches = new NotificationDispatchRepository(_harness.DbContext);
        var templates = new NotificationTemplateRepository(_harness.DbContext);
        foreach (var template in NotificationTemplateSeed.CreatePlatformDefaults()
                     .Where(t => t.TemplateKey == CrmManifestProvider.VisitReportAdverseEventEvent
                                 || (t.TemplateKey == CrmManifestProvider.PreOrderApprovalRequestedEvent && t.Locale == "tr")))
        {
            template.TenantId = tenantId;
            template.IsPlatformDefault = false;
            await templates.CreateAsync(template);
        }

        var tenants = new TurkishTenant(tenantId);
        var locales = new TenantNotificationLocaleResolver(tenants, NullLogger<TenantNotificationLocaleResolver>.Instance);
        var provider = new SlowProvider(slow ? SlowSmtp : TimeSpan.Zero);
        var bus = new NoOpEventBus();
        var handler = new QueueEmailNotificationHandler(
            new FixedSettingsResolver(), templates, new EmailTemplateRenderer(), dispatches,
            new SingleProviderResolver(provider), bus, NullLogger<QueueEmailNotificationHandler>.Instance,
            localeResolver: locales, jobOptions: NotificationTestJobs.RetriesOn());
        var catalog = InternalNotificationEventDispatchTests.Catalog.FromManifests(new CrmManifestProvider(), new MeetingManifestProvider());
        var adapter = new CountingAdapter(
            new NotificationEventDispatchAdapter(catalog, new QueueMediator(handler), locales, NullLogger<NotificationEventDispatchAdapter>.Instance),
            adapterDelay ?? TimeSpan.Zero);
        var service = new InternalEventDispatchService(
            tenants, catalog, _harness.TenantContext, new NoLineManagers(), new NoContacts(), locales, adapter,
            new InternalDispatchKeyRepository(_harness.DbContext, _harness.TenantContext),
            NullLogger<InternalEventDispatchService>.Instance);
        var job = new EmailDispatchJob(
            dispatches, new FixedSettingsResolver(), new SingleProviderResolver(provider),
            new QueueMediator(handler, new MarkNotificationDispatchSentHandler(dispatches, bus), new MarkNotificationDispatchFailedHandler(dispatches, bus)),
            NullLogger<EmailDispatchJob>.Instance, NoInvitationLedger.Instance, templates, new EmailTemplateRenderer());
        return new World(service, adapter, handler, dispatches, provider, job);
    }

    private InternalEventDispatchRequest Adverse(string? key) => new(
        _harness.TenantId,
        CrmManifestProvider.VisitReportAdverseEventEvent,
        Crm,
        new InternalEventRecipients([new InternalEventRecipient(Email: PvMailbox, DisplayName: "PV")]),
        Variables().ToDictionary(p => p.Key, p => (string?)p.Value?.ToString()),
        "corr-w5fix2",
        IdempotencyKey: key);

    // A visit with no doctor (a pharmacy visit) and no product.
    private static Dictionary<string, object?> Variables() => new()
    {
        ["repName"] = "Ayşe Demir",
        ["doctorName"] = "",
        ["institutionName"] = "Merkez Eczanesi",
        ["visitDate"] = "2026-10-20",
        ["products"] = "",
        ["adverseEventNote"] = "baş dönmesi",
        ["reportUrl"] = "https://crm.test/CRM/VisitReport?plannedVisitId=v1"
    };

    private static string Json(InternalEventDispatchResultDto? result) => JsonSerializer.Serialize(result);

    private static async Task<IReadOnlyList<EmailDispatchJobArgs>> SweepAsync(NotificationDispatchRepository dispatches)
    {
        var scheduler = new RecordingScheduler();
        await TestSweeps.Create(dispatches, scheduler, NullLogger<EmailDispatchSweepJob>.Instance)
            .HandleAsync(new EmailDispatchSweepJobArgs(BatchSize: 500, MaxRetryCount: 5), new BackgroundJobContext(), CancellationToken.None);
        return scheduler.Enqueued;
    }

    // ── doubles ──────────────────────────────────────────────────────────────

    /// <summary>The real adapter, counted; can refuse once, and can hold the call (to make two calls overlap).</summary>
    private sealed class CountingAdapter(INotificationEventDispatchAdapter inner, TimeSpan delay) : INotificationEventDispatchAdapter
    {
        private int _calls;
        public int Calls => _calls;
        public bool FailNext { get; set; }

        public async Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(NotificationEventDispatchRequest request, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }

            if (FailNext)
            {
                FailNext = false;
                return Response<NotificationDispatchDto>.Fail("refused", 503, "TEST_REFUSED");
            }

            return await inner.DispatchByEventCodeAsync(request, ct);
        }
    }

    internal sealed class SlowProvider(TimeSpan delay) : IMessagingProvider
    {
        public List<MessagingProviderEmailRequest> Requests { get; } = [];
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;

        public async Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default)
        {
            await Task.Delay(delay, ct);
            lock (Requests)
            {
                Requests.Add(request);
            }

            return MessagingProviderResult.Success("msg-" + request.DispatchId.ToString("N"));
        }
    }

    /// <summary>A tenant whose language is Turkish (settings and profile alike), Active.</summary>
    private sealed class TurkishTenant(Guid id) : ITenantRegistryRepository
    {
        public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(tenantId == id
                ? new Tenant
                {
                    Id = id, Code = "T", Slug = "t", Name = "t", DisplayName = "T", Domain = "t.test", Status = TenantStatus.Active,
                    DefaultLanguage = "tr", Settings = new TenantSettings { Language = "tr" }
                }
                : null);

        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(Guid tenantId, TenantStatus status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoLineManagers : ILineManagerResolver
    {
        public Task<LineManagers> ResolveAsync(Guid tenantId, Guid userId, CancellationToken ct = default) => Task.FromResult(LineManagers.Nobody);
    }

    private sealed class NoContacts : ITaskNotificationRecipientResolver
    {
        public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TaskNotificationRecipient>>([]);
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

    /// <summary>The queue command to the real handler (the adapter's one call), the job's transitions to theirs.</summary>
    private sealed class QueueMediator(
        QueueEmailNotificationHandler queue,
        MarkNotificationDispatchSentHandler? sent = null,
        MarkNotificationDispatchFailedHandler? failed = null) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            request switch
            {
                QueueEmailNotificationCommand cmd => (TResponse)(object)await queue.Handle(cmd, cancellationToken),
                MarkNotificationDispatchSentCommand cmd when sent is not null => (TResponse)(object)await sent.Handle(cmd, cancellationToken),
                MarkNotificationDispatchFailedCommand cmd when failed is not null => (TResponse)(object)await failed.Handle(cmd, cancellationToken),
                _ => throw new NotSupportedException($"QueueMediator does not handle {request.GetType().Name}.")
            };

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
