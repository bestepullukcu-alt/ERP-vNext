using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Smtp = Diten.Platform.Application.Tests.Notifications.NotificationsSmtpIntegrationTests;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-EMAIL-LANG-01 fix round 3 (P-2026-10-10-EMAIL-LANG-01-FIX3) — a refused language group keeps the Cc / Bcc copies
/// on its row ONLY when that row is due for the retry job, and the queue handler says so in its own code. On a server
/// without the retry job (live today, BL-536) the refusal is final: the copies go with the next group. Measured through
/// the REAL QueueEmailNotificationHandler behind the adapter, so the two halves cannot drift apart.
/// </summary>
public sealed class RecipientLanguageFixThreeTests
{
    private static readonly Guid Tenant = Guid.Parse("0f3a3c11-6c1e-4b8e-9f6a-3e2f1d0c9b8a");
    private const string EventCode = "task.assigned";
    private const string Refused = "fay@acme.test";

    [Fact]
    public async Task Without_the_retry_job_a_refused_group_hands_its_copies_to_the_next_group()
    {
        var host = Build(RetriesOff());

        var result = await host.Adapter.DispatchByEventCodeAsync(RequestWithCopies());

        Assert.True(result.IsSuccessful);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonPartialRecipientFailure, result.ReasonCode);
        Assert.Contains($"fr: {QueueEmailNotificationHandler.ReasonProviderRejected}", result.Errors);
        var refusedRow = host.Dispatches.Items.Single(d => d.To.Any(r => r.Email == Refused));
        Assert.Null(refusedRow.NextRetryAt);
        Assert.NotNull(refusedRow.PermanentlyFailedNotifiedAt);
        var delivered = host.Provider.Accepted.Single();
        Assert.Equal(["ruslan@acme.test"], delivered.To.Select(r => r.Email));
        Assert.Equal(["cc@acme.test"], delivered.Cc.Select(r => r.Email));
        Assert.Equal(["bcc@acme.test"], delivered.Bcc.Select(r => r.Email));
    }

    [Fact]
    public async Task With_the_retry_job_a_refused_group_keeps_its_copies_on_its_row()
    {
        var host = Build(NotificationTestJobs.RetriesOn());

        var result = await host.Adapter.DispatchByEventCodeAsync(RequestWithCopies());

        Assert.True(result.IsSuccessful);
        Assert.Contains($"fr: {QueueEmailNotificationHandler.ReasonProviderRejectedRetryPending}", result.Errors);
        var refusedRow = host.Dispatches.Items.Single(d => d.To.Any(r => r.Email == Refused));
        Assert.NotNull(refusedRow.NextRetryAt);
        Assert.Equal(["cc@acme.test"], refusedRow.Cc.Select(r => r.Email));
        Assert.Equal(["bcc@acme.test"], refusedRow.Bcc.Select(r => r.Email));
        var delivered = host.Provider.Accepted.Single();
        Assert.Empty(delivered.Cc);
        Assert.Empty(delivered.Bcc);
    }

    [Fact]
    public async Task The_handler_names_a_pending_retry_and_a_final_refusal_apart()
    {
        var command = new QueueEmailNotificationCommand(Tenant, new QueueEmailNotificationRequest(
            TemplateKey: EventCode + ".email", Locale: "fr", Variables: new Dictionary<string, object?>(),
            To: [new EmailRecipientDto(Refused, "Fay")]), "corr-fix3");

        var final = await Build(RetriesOff()).Handler.Handle(command, CancellationToken.None);
        var pending = await Build(NotificationTestJobs.RetriesOn()).Handler.Handle(command, CancellationToken.None);

        Assert.Equal(QueueEmailNotificationHandler.ReasonProviderRejected, final.ReasonCode);
        Assert.Equal(QueueEmailNotificationHandler.ReasonProviderRejectedRetryPending, pending.ReasonCode);
    }

    private static NotificationEventDispatchRequest RequestWithCopies() =>
        new(Tenant, EventCode,
            [new EmailRecipientDto(Refused, "Fay", "fr"), new EmailRecipientDto("ruslan@acme.test", "Ruslan", "ru")],
            new Dictionary<string, object?>())
        {
            Cc = [new EmailRecipientDto("cc@acme.test", "Cc")],
            Bcc = [new EmailRecipientDto("bcc@acme.test", "Bcc")]
        };

    // The live default: the job settings exist, the e-mail dispatch sweep is not among the enabled jobs.
    private static IOptions<BackgroundJobSchedulerOptions> RetriesOff() => Options.Create(new BackgroundJobSchedulerOptions
    {
        Enabled = true,
        RegisterStandardJobs = true,
        EnabledJobs = new(StringComparer.OrdinalIgnoreCase) { [EmailDispatchSweepJob.JobId] = false }
    });

    private sealed record Host(
        NotificationEventDispatchAdapter Adapter, QueueEmailNotificationHandler Handler,
        Smtp.InMemoryNotificationDispatchRepository Dispatches, RefusingProvider Provider);

    private static Host Build(IOptions<BackgroundJobSchedulerOptions> jobs)
    {
        var settings = new Smtp.InMemoryTenantMessagingSettingsRepository();
        settings.CreateAsync(new TenantMessagingSettings
        {
            TenantId = Tenant, IsPlatformDefault = false, ProviderCode = MessagingProviderCode.Fake,
            SenderEmail = "sender@example.com", SenderName = "Sender", IsEnabled = true,
            FallbackPolicy = NotificationFallbackPolicy.UsePlatformDefault
        }).GetAwaiter().GetResult();
        var templates = new Smtp.InMemoryNotificationTemplateRepository();
        foreach (var locale in new[] { "fr", "ru" })
        {
            templates.CreateAsync(new NotificationTemplate
            {
                IsPlatformDefault = true, TemplateKey = EventCode + ".email", Channel = NotificationChannelCode.Email,
                Locale = locale, SubjectTemplate = "Task", BodyHtmlTemplate = "<p>Task</p>", BodyTextTemplate = "Task",
                Status = NotificationTemplateStatus.Active
            }).GetAwaiter().GetResult();
        }

        var dispatches = new Smtp.InMemoryNotificationDispatchRepository();
        var provider = new RefusingProvider();
        var handler = new QueueEmailNotificationHandler(
            new TenantMessagingSettingsResolver(settings), templates, new EmailTemplateRenderer(), dispatches,
            new Smtp.TestProviderResolver(provider), new Smtp.RecordingEventBus(),
            NullLogger<QueueEmailNotificationHandler>.Instance, jobOptions: jobs);
        var events = new OneEvent(new NotificationEventDefinition
        {
            EventCode = EventCode, SourceType = NotificationEventSourceType.PlatformSeed, OwnerModuleId = "MOD-0024",
            Channel = NotificationChannelCode.Email, DefaultTemplateKey = EventCode + ".email", FallbackDisplayName = EventCode,
            Status = NotificationEventStatus.Active
        });
        var adapter = new NotificationEventDispatchAdapter(events, new HandlerMediator(handler), new AsAsked(),
            NullLogger<NotificationEventDispatchAdapter>.Instance);
        return new Host(adapter, handler, dispatches, provider);
    }

    /// <summary>The provider: refuses every message addressed to <see cref="Refused"/>, accepts (and keeps) the rest.</summary>
    private sealed class RefusingProvider : IMessagingProvider
    {
        public List<MessagingProviderEmailRequest> Accepted { get; } = [];
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;

        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default)
        {
            if (request.To.Any(r => r.Email == Refused))
            {
                return Task.FromResult(MessagingProviderResult.Fail("PROVIDER_REJECTED:550", "Server rejected the message."));
            }

            Accepted.Add(request);
            return Task.FromResult(MessagingProviderResult.Success("accepted-" + Accepted.Count));
        }
    }

    private sealed class AsAsked : INotificationLocaleResolver
    {
        public Task<string> ResolveAsync(Guid tenantId, string? requested, CancellationToken ct = default) =>
            Task.FromResult(string.IsNullOrWhiteSpace(requested) ? "en" : requested);
    }

    private sealed class HandlerMediator(QueueEmailNotificationHandler handler) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            (TResponse)(object)await handler.Handle((QueueEmailNotificationCommand)(object)request, cancellationToken);

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class OneEvent(NotificationEventDefinition definition) : INotificationEventDefinitionRepository
    {
        public Task<NotificationEventDefinition?> GetByEventCodeAsync(string eventCode, CancellationToken ct = default) =>
            Task.FromResult<NotificationEventDefinition?>(eventCode == definition.EventCode ? definition : null);
        public Task<NotificationEventDefinition> CreateAsync(NotificationEventDefinition d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(NotificationEventDefinition d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NotificationEventDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NotificationEventDefinition>> ListAsync(
            string? ownerModuleId = null, NotificationChannelCode? channel = null, NotificationEventStatus? status = null,
            bool? canTenantOverride = null, NotificationEventUsageType? usageType = null,
            int skip = 0, int take = 100, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NotificationEventDefinition>> ListActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
