using System.Text;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-374 — a first send never fails only to have its retry ship different content. Two separate, previously
/// unmeasured gaps: (1) <c>EmailDispatchJob</c> rebuilds its retry from the PERSISTED dispatch row, which never
/// carried an attachment (S5b's own .ics never made it there), and (2) that same row only ever carried the
/// MASKED preview, so a retry's body was permanently degraded even when nothing sensitive was involved. This
/// file proves both are fixed, and that the masking they were fixed around is not weakened doing it.
/// </summary>
public sealed class NotificationDispatchRetryFidelityTests
{
    // ---------- QueueEmailNotificationHandler — what gets persisted for a LATER retry to use ----------

    [Fact]
    public async Task An_attachment_under_the_size_budget_is_persisted_onto_the_dispatch_row()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        var provider = new ScriptedProvider(MessagingProviderResult.Success("ok"));
        var handler = CreateQueueHandler(tenantId, dispatches, provider: provider);
        var icsBytes = Encoding.UTF8.GetBytes("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");

        await handler.Handle(CreateCommand(tenantId, attachments:
        [
            new MessagingProviderAttachment("invite.ics", "text/calendar; charset=utf-8; method=REQUEST", icsBytes)
        ]), CancellationToken.None);

        var persisted = Assert.Single(dispatches.Items).Attachments;
        var attachment = Assert.Single(persisted);
        Assert.Equal("invite.ics", attachment.FileName);
        Assert.Equal("text/calendar; charset=utf-8; method=REQUEST", attachment.ContentType);
        Assert.Equal(icsBytes, attachment.Content);

        // The FIRST send is unaffected either way — it always read straight off the command, never `dispatch`.
        var firstSendAttachment = Assert.Single(Assert.Single(provider.Requests).Attachments!);
        Assert.Equal(icsBytes, firstSendAttachment.Content);
    }

    [Fact]
    public async Task An_attachment_over_the_size_budget_is_NOT_persisted_but_the_first_send_still_carries_it_and_it_is_logged()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        var provider = new ScriptedProvider(MessagingProviderResult.Success("ok"));
        var logger = new CapturingLogger<QueueEmailNotificationHandler>();
        var handler = CreateQueueHandler(tenantId, dispatches, provider: provider, logger: logger);
        var oversized = new byte[260 * 1024];

        await handler.Handle(CreateCommand(tenantId, attachments:
        [
            new MessagingProviderAttachment("big.ics", "text/calendar", oversized)
        ]), CancellationToken.None);

        Assert.Empty(Assert.Single(dispatches.Items).Attachments);
        // The first send still went out with it — only PERSISTENCE was skipped.
        Assert.Single(Assert.Single(provider.Requests).Attachments!);
        Assert.Contains(logger.Entries, e =>
            e.LogLevel == LogLevel.Warning && e.Message.Contains("email.dispatch.attachments_not_persisted"));
    }

    [Fact]
    public async Task The_templates_own_SemanticVersion_is_stamped_onto_the_dispatch_at_queue_time()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        var handler = CreateQueueHandler(tenantId, dispatches, templateSemanticVersion: "1.4.0");

        await handler.Handle(CreateCommand(tenantId), CancellationToken.None);

        Assert.Equal("1.4.0", Assert.Single(dispatches.Items).TemplateSemanticVersion);
    }

    // ---------- EmailDispatchJob — what a retry does with what was persisted ----------

    [Fact]
    public async Task A_retry_replays_the_SAME_attachment_the_first_send_had()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateFailedDispatch(tenantId);
        dispatch.Attachments = [new NotificationDispatchAttachment
        {
            FileName = "invite.ics", ContentType = "text/calendar; method=REQUEST", Content = "BEGIN:VCALENDAR"u8.ToArray()
        }];
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);
        var provider = new ScriptedProvider(MessagingProviderResult.Success("resent"));
        var job = BuildJob(dispatches, provider);

        await job.HandleAsync(new EmailDispatchJobArgs(tenantId, dispatch.Id), new BackgroundJobContext());

        var replayed = Assert.Single(Assert.Single(provider.Requests).Attachments!);
        Assert.Equal("invite.ics", replayed.FileName);
        Assert.Equal("BEGIN:VCALENDAR"u8.ToArray(), replayed.Content);
    }

    [Fact]
    public async Task A_clean_retry_re_renders_the_SAME_full_body_the_original_send_used()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryDispatches();
        var provider = new ScriptedProvider(
            MessagingProviderResult.Fail("ProviderRejected", "boom"),  // first send fails
            MessagingProviderResult.Success("retried"));               // retry succeeds
        var templates = new InMemoryNotificationTemplateRepository();
        var template = CreateTemplate("meeting.invite", semanticVersion: "2.0.0");
        await templates.CreateAsync(template);
        var handler = new QueueEmailNotificationHandler(
            new FixedSettingsResolver(),
            templates,
            new EmailTemplateRenderer(),
            dispatches,
            new SingleProviderResolver(provider),
            new NoOpEventBus(),
            NullLogger<QueueEmailNotificationHandler>.Instance);

        var queued = await handler.Handle(
            new QueueEmailNotificationCommand(
                tenantId,
                new QueueEmailNotificationRequest(
                    TemplateKey: "meeting.invite",
                    Locale: "en",
                    // NotificationParsing.LooksLikeRawSecret (untouched by this WP, per its own YAPMA) treats ANY
                    // value containing a space as secret-shaped and masks it via SanitizeVariables — so a
                    // genuinely "clean" (unredacted) variable here has to be a single token, not "Weekly Review".
                    Variables: new Dictionary<string, object?> { ["MeetingTitle"] = "WeeklyReview" },
                    To: [new EmailRecipientDto("user@example.com", "User")])),
            CancellationToken.None);

        Assert.False(queued.IsSuccessful); // first send rejected, dispatch persisted as Failed
        var originalRequest = provider.Requests[0];
        var dispatch = Assert.Single(dispatches.Items);

        var job = BuildJob(dispatches, provider, templates);
        await job.HandleAsync(new EmailDispatchJobArgs(tenantId, dispatch.Id), new BackgroundJobContext());

        var retryRequest = provider.Requests[1];
        Assert.Equal(originalRequest.BodyHtml, retryRequest.BodyHtml);
        Assert.Equal(originalRequest.BodyText, retryRequest.BodyText);
        Assert.Contains("WeeklyReview", retryRequest.BodyHtml);
    }

    [Fact]
    public async Task A_retry_falls_back_to_the_preview_and_logs_degraded_when_variables_were_redacted()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateFailedDispatch(tenantId);
        dispatch.TemplateId = Guid.NewGuid();
        dispatch.TemplateSemanticVersion = "1.0.0";
        dispatch.VariablesJson = JsonSerializer.Serialize(new Dictionary<string, object?> { ["Password"] = "[REDACTED]" });
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);
        var templates = new InMemoryNotificationTemplateRepository();
        await templates.CreateAsync(CreateTemplate("x", id: dispatch.TemplateId.Value, semanticVersion: "1.0.0"));
        var provider = new ScriptedProvider(MessagingProviderResult.Success("ok"));
        var logger = new CapturingLogger<EmailDispatchJob>();
        var job = BuildJob(dispatches, provider, templates, logger);

        await job.HandleAsync(new EmailDispatchJobArgs(tenantId, dispatch.Id), new BackgroundJobContext());

        var request = Assert.Single(provider.Requests);
        Assert.Null(request.BodyHtml);
        Assert.Null(request.BodyText);
        Assert.Contains(logger.Entries, e =>
            e.LogLevel == LogLevel.Information
            && e.Message.Contains("email.dispatch.retry_degraded")
            && e.Message.Contains("VariablesRedacted"));
    }

    [Fact]
    public async Task A_retry_falls_back_when_the_templates_SemanticVersion_has_moved_on_since_queue_time()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateFailedDispatch(tenantId);
        dispatch.TemplateId = Guid.NewGuid();
        dispatch.TemplateSemanticVersion = "1.0.0"; // what the template was AT QUEUE TIME
        dispatch.VariablesJson = JsonSerializer.Serialize(new Dictionary<string, object?> { ["MeetingTitle"] = "x" });
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);
        var templates = new InMemoryNotificationTemplateRepository();
        await templates.CreateAsync(CreateTemplate("x", id: dispatch.TemplateId.Value, semanticVersion: "2.0.0")); // moved on
        var provider = new ScriptedProvider(MessagingProviderResult.Success("ok"));
        var logger = new CapturingLogger<EmailDispatchJob>();
        var job = BuildJob(dispatches, provider, templates, logger);

        await job.HandleAsync(new EmailDispatchJobArgs(tenantId, dispatch.Id), new BackgroundJobContext());

        var request = Assert.Single(provider.Requests);
        Assert.Null(request.BodyHtml);
        Assert.Contains(logger.Entries, e =>
            e.Message.Contains("email.dispatch.retry_degraded") && e.Message.Contains("TemplateVersionChanged"));
    }

    [Fact]
    public async Task A_retry_falls_back_when_the_dispatch_carries_no_TemplateId()
    {
        var tenantId = Guid.NewGuid();
        var dispatch = CreateFailedDispatch(tenantId);
        dispatch.TemplateId = null;
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);
        var provider = new ScriptedProvider(MessagingProviderResult.Success("ok"));
        var logger = new CapturingLogger<EmailDispatchJob>();
        var job = BuildJob(dispatches, provider, new InMemoryNotificationTemplateRepository(), logger);

        await job.HandleAsync(new EmailDispatchJobArgs(tenantId, dispatch.Id), new BackgroundJobContext());

        var request = Assert.Single(provider.Requests);
        Assert.Null(request.BodyHtml);
        Assert.Contains(logger.Entries, e =>
            e.Message.Contains("email.dispatch.retry_degraded") && e.Message.Contains("TemplateIdMissing"));
    }

    [Fact]
    public async Task A_dispatch_with_no_persisted_attachments_sends_a_null_attachment_list_on_retry()
    {
        // Byte-identical-behaviour proof, mirrored from S5b: a dispatch that never had an attachment must not
        // suddenly grow an empty-but-present one on retry.
        var tenantId = Guid.NewGuid();
        var dispatch = CreateFailedDispatch(tenantId);
        var dispatches = new InMemoryDispatches();
        await dispatches.CreateAsync(dispatch);
        var provider = new ScriptedProvider(MessagingProviderResult.Success("ok"));
        var job = BuildJob(dispatches, provider);

        await job.HandleAsync(new EmailDispatchJobArgs(tenantId, dispatch.Id), new BackgroundJobContext());

        Assert.Null(Assert.Single(provider.Requests).Attachments);
    }

    // ── fixtures ─────────────────────────────────────────────────────────────

    private static QueueEmailNotificationCommand CreateCommand(
        Guid tenantId, IReadOnlyList<MessagingProviderAttachment>? attachments = null) =>
        new(
            tenantId,
            new QueueEmailNotificationRequest(
                TemplateKey: "tenant.invite.email",
                Locale: "en",
                Variables: new Dictionary<string, object?> { ["tenantName"] = "Acme", ["inviteUrl"] = "https://x" },
                To: [new EmailRecipientDto("user@example.com", "User")],
                Attachments: attachments));

    private static QueueEmailNotificationHandler CreateQueueHandler(
        Guid tenantId,
        InMemoryDispatches dispatches,
        ScriptedProvider? provider = null,
        string? templateSemanticVersion = null,
        ILogger<QueueEmailNotificationHandler>? logger = null)
    {
        var settings = new FixedSettingsResolver();
        var templates = new InMemoryNotificationTemplateRepository();
        templates.CreateAsync(CreateTemplate("tenant.invite.email", semanticVersion: templateSemanticVersion)).GetAwaiter().GetResult();
        provider ??= new ScriptedProvider(MessagingProviderResult.Success("ok"));

        return new QueueEmailNotificationHandler(
            settings,
            templates,
            new EmailTemplateRenderer(),
            dispatches,
            new SingleProviderResolver(provider),
            new NoOpEventBus(),
            logger ?? NullLogger<QueueEmailNotificationHandler>.Instance);
    }

    private static EmailDispatchJob BuildJob(
        InMemoryDispatches dispatches,
        ScriptedProvider provider,
        InMemoryNotificationTemplateRepository? templates = null,
        ILogger<EmailDispatchJob>? logger = null)
    {
        var bus = new NoOpEventBus();
        var mediator = new DirectMediator(
            new MarkNotificationDispatchSentHandler(dispatches, bus),
            new MarkNotificationDispatchFailedHandler(dispatches, bus),
            new CancelNotificationDispatchHandler(dispatches, bus));

        return new EmailDispatchJob(
            dispatches,
            new FixedSettingsResolver(),
            new SingleProviderResolver(provider),
            mediator,
            logger ?? NullLogger<EmailDispatchJob>.Instance,
            templates,
            templates is null ? null : new EmailTemplateRenderer());
    }

    private static NotificationTemplate CreateTemplate(string key, Guid? id = null, string? semanticVersion = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        IsPlatformDefault = true,
        TemplateKey = key,
        Channel = NotificationChannelCode.Email,
        Locale = "en",
        SubjectTemplate = "Subject {{MeetingTitle}}{{tenantName}}",
        BodyHtmlTemplate = "<p>{{MeetingTitle}}{{tenantName}}{{inviteUrl}}</p>",
        BodyTextTemplate = "{{MeetingTitle}}{{tenantName}}{{inviteUrl}}",
        Status = NotificationTemplateStatus.Active,
        SemanticVersion = semanticVersion,
        Variables = []
    };

    private static NotificationDispatch CreateFailedDispatch(Guid tenantId) => new()
    {
        TenantId = tenantId,
        TemplateKey = "tenant.invite.email",
        Locale = "en",
        Channel = NotificationChannelCode.Email,
        ProviderCode = MessagingProviderCode.Fake,
        Status = NotificationDispatchStatus.Failed,
        To = [new EmailRecipient { Email = "user@example.com" }],
        Subject = "Subject",
        BodyHtmlPreview = "<p>preview</p>",
        BodyTextPreview = "preview",
        VariablesJson = "{}",
        QueuedAt = DateTimeOffset.UtcNow,
        RetryCount = 1,
        NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        ErrorCode = "ProviderRejected",
        ErrorMessage = "redacted",
        FailedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        CorrelationId = "corr-retry-fidelity"
    };

    // ── fakes ────────────────────────────────────────────────────────────────

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

        public Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(
            Guid tenantId, int skip = 0, int take = 50, NotificationDispatchStatus? status = null,
            DateTimeOffset? queuedFrom = null, DateTimeOffset? queuedTo = null, string? templateKey = null,
            CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => !x.IsDeleted && x.TenantId == tenantId).ToArray() as IReadOnlyList<NotificationDispatch>);

        public Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<NotificationDispatchRetryHandle>> FindDueRetriesAsync(
            DateTimeOffset asOfUtc, int maxRetryCount, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationDispatchRetryHandle>>([]);
    }

    private sealed class InMemoryNotificationTemplateRepository : INotificationTemplateRepository
    {
        private readonly List<NotificationTemplate> _items = [];

        public Task<NotificationTemplate> CreateAsync(NotificationTemplate template, CancellationToken ct = default)
        {
            _items.Add(template);
            return Task.FromResult(template);
        }

        public Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && x.Id == id));

        public Task<IReadOnlyList<NotificationTemplate>> ListAsync(
            Guid? tenantId, bool isPlatformDefault, NotificationTemplateStatus? status = null, string? locale = null,
            NotificationChannelCode? channel = null, string? templateKey = null, int skip = 0, int take = 50,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationTemplate>>(_items.ToArray());

        public Task<NotificationTemplate?> GetActiveByKeyAsync(
            Guid? tenantId, bool isPlatformDefault, string templateKey, string locale, NotificationChannelCode channel,
            CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x =>
                !x.IsDeleted && x.Status == NotificationTemplateStatus.Active && x.TenantId == tenantId
                && x.IsPlatformDefault == isPlatformDefault && x.TemplateKey == templateKey && x.Locale == locale
                && x.Channel == channel));

        public async Task<NotificationTemplate?> GetBestActiveByKeyAsync(
            Guid tenantId, string templateKey, string locale, NotificationChannelCode channel, CancellationToken ct = default) =>
            await GetActiveByKeyAsync(tenantId, false, templateKey, locale, channel, ct)
            ?? await GetActiveByKeyAsync(null, true, templateKey, locale, channel, ct);

        public Task<bool> ActiveTemplateExistsAsync(
            Guid? tenantId, bool isPlatformDefault, string templateKey, string locale, NotificationChannelCode channel,
            Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default) => Task.CompletedTask;

        public Task ArchiveAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FixedSettingsResolver : ITenantMessagingSettingsResolver
    {
        public Task<Response<ResolvedMessagingSettingsDto>> ResolveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Response<ResolvedMessagingSettingsDto>.Success(
                new ResolvedMessagingSettingsDto(
                    Guid.NewGuid(), tenantId, tenantId, false, MessagingProviderCode.Fake.ToString(),
                    "sender@example.com", "Sender", null, true, NotificationFallbackPolicy.UsePlatformDefault.ToString())));
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

    /// <summary>Returns results in order (repeating the last one once exhausted) and records every request.</summary>
    private sealed class ScriptedProvider : IMessagingProvider
    {
        private readonly Queue<MessagingProviderResult> _results;
        private MessagingProviderResult _last;

        public ScriptedProvider(params MessagingProviderResult[] results)
        {
            _results = new Queue<MessagingProviderResult>(results);
            _last = results[^1];
        }

        public List<MessagingProviderEmailRequest> Requests { get; } = [];
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;

        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            _last = _results.Count > 0 ? _results.Dequeue() : _last;
            return Task.FromResult(_last);
        }
    }

    private sealed class NoOpEventBus : IEventBus
    {
        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : IIntegrationEvent => PublishAsync(@event, new EventPublishOptions(), ct);

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(
            TEvent @event, EventPublishOptions options, CancellationToken ct = default)
            where TEvent : IIntegrationEvent =>
            Task.FromResult(new EventEnvelope<TEvent>(
                new EventMetadata(
                    options.EventId ?? Guid.NewGuid(),
                    @event.EventName,
                    @event.EventVersion,
                    options.CorrelationId ?? Guid.NewGuid(),
                    options.CausationId,
                    options.TenantId,
                    "test",
                    options.OccurredAtUtc ?? DateTimeOffset.UtcNow),
                @event));
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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}
