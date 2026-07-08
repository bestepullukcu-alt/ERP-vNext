using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.Notifications;
using Diten.Platform.Infrastructure.Settings;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

public sealed class NotificationsSmtpProviderBodyAndAuthTests
{
    private const int PreviewMaxLength = 2000;

    [Fact]
    public void EmailTemplateRenderer_ShouldRenderFullBody_AndTruncatedPreview()
    {
        var renderer = new EmailTemplateRenderer();
        var longContent = new string('A', PreviewMaxLength + 500);
        var template = new NotificationTemplate
        {
            IsPlatformDefault = true,
            TemplateKey = "tenant.long.email",
            Channel = NotificationChannelCode.Email,
            Locale = "en",
            SubjectTemplate = "S",
            BodyHtmlTemplate = "<p>{{content}}</p>",
            BodyTextTemplate = "{{content}}",
            Status = NotificationTemplateStatus.Active,
            Variables =
            [
                new TemplateVariableDefinition { Name = "content", Type = TemplateVariableType.String, IsRequired = true }
            ]
        };

        var response = renderer.Render(template, new Dictionary<string, object?> { ["content"] = longContent });

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.NotNull(response.Data!.BodyHtml);
        Assert.NotNull(response.Data.BodyText);
        Assert.Contains(longContent, response.Data.BodyHtml);
        Assert.Contains(longContent, response.Data.BodyText);
        Assert.True(response.Data.BodyHtml!.Length > PreviewMaxLength);
        Assert.True(response.Data.BodyText!.Length > PreviewMaxLength);
        Assert.NotNull(response.Data.BodyHtmlPreview);
        Assert.NotNull(response.Data.BodyTextPreview);
        Assert.True(response.Data.BodyHtmlPreview!.Length <= PreviewMaxLength);
        Assert.True(response.Data.BodyTextPreview!.Length <= PreviewMaxLength);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldSendFullBody_NotTruncatedPreview()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new NotificationsSmtpProviderTests.FakeSmtpTransport
        {
            SendBehavior = (_, _) => Task.FromResult("OK 250 ok")
        };
        var factory = new NotificationsSmtpProviderTests.RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        var fullHtml = new string('B', PreviewMaxLength + 250);
        var fullText = new string('C', PreviewMaxLength + 250);
        var truncatedHtml = fullHtml[..PreviewMaxLength];
        var truncatedText = fullText[..PreviewMaxLength];

        var request = new MessagingProviderEmailRequest(
            DispatchId: Guid.NewGuid(),
            TenantId: tenantId,
            CorrelationId: "corr-body",
            Subject: "Subject",
            To: [new EmailRecipientDto("user@example.com", "User")],
            Cc: [],
            Bcc: [],
            BodyHtmlPreview: truncatedHtml,
            BodyTextPreview: truncatedText,
            BodyHtml: fullHtml,
            BodyText: fullText);

        var result = await provider.SendEmailAsync(request);

        Assert.True(result.Accepted);
        Assert.NotNull(transport.LastSentMessage);
        Assert.NotNull(transport.LastSentMessage!.HtmlBody);
        Assert.NotNull(transport.LastSentMessage.TextBody);
        Assert.Contains(fullHtml, transport.LastSentMessage.HtmlBody);
        Assert.Contains(fullText, transport.LastSentMessage.TextBody);
        Assert.True(transport.LastSentMessage.HtmlBody!.Length > PreviewMaxLength);
        Assert.True(transport.LastSentMessage.TextBody!.Length > PreviewMaxLength);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldFallBackToPreview_WhenFullBodyMissing()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new NotificationsSmtpProviderTests.FakeSmtpTransport
        {
            SendBehavior = (_, _) => Task.FromResult("OK 250 ok")
        };
        var factory = new NotificationsSmtpProviderTests.RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        // Retry path: EmailDispatchJob reconstructs MessagingProviderEmailRequest from the persisted
        // dispatch, which only stores BodyHtmlPreview/BodyTextPreview. BodyHtml/BodyText are absent.
        var request = new MessagingProviderEmailRequest(
            DispatchId: Guid.NewGuid(),
            TenantId: tenantId,
            CorrelationId: "corr-retry",
            Subject: "Subject",
            To: [new EmailRecipientDto("user@example.com", "User")],
            Cc: [],
            Bcc: [],
            BodyHtmlPreview: "<p>preview-only-html</p>",
            BodyTextPreview: "preview-only-text");

        var result = await provider.SendEmailAsync(request);

        Assert.True(result.Accepted);
        Assert.NotNull(transport.LastSentMessage);
        Assert.Contains("preview-only-html", transport.LastSentMessage!.HtmlBody);
        Assert.Contains("preview-only-text", transport.LastSentMessage.TextBody);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldUseSenderEmailAsSmtpAuthUsername_AsMvpDefault()
    {
        var tenantId = Guid.NewGuid();
        var senderEmail = "auth-username@example.com";
        var settings = CreateRepository(CreateSettings(tenantId, senderEmail: senderEmail));
        var transport = new NotificationsSmtpProviderTests.FakeSmtpTransport
        {
            SendBehavior = (_, _) => Task.FromResult("OK 250 ok")
        };
        var factory = new NotificationsSmtpProviderTests.RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        var request = new MessagingProviderEmailRequest(
            DispatchId: Guid.NewGuid(),
            TenantId: tenantId,
            CorrelationId: "corr-auth",
            Subject: "Subject",
            To: [new EmailRecipientDto("user@example.com", "User")],
            Cc: [],
            Bcc: [],
            BodyHtmlPreview: "<p>hi</p>",
            BodyTextPreview: "hi");

        var result = await provider.SendEmailAsync(request);

        Assert.True(result.Accepted);
        Assert.Equal(senderEmail, transport.LastAuthUserName);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldNotLogFullBody_EvenWhenFullProvided()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new NotificationsSmtpProviderTests.FakeSmtpTransport
        {
            SendBehavior = (_, _) => Task.FromResult("OK 250 ok")
        };
        var factory = new NotificationsSmtpProviderTests.RecordingSmtpClientFactory(transport);
        var logger = new NotificationsSmtpProviderTests.CapturingLogger<SmtpMessagingProvider>();
        var provider = CreateProvider(settings, factory, logger: logger);

        var fullSensitive = "FULL_BODY_THAT_MUST_NEVER_APPEAR_IN_LOGS";
        var request = new MessagingProviderEmailRequest(
            DispatchId: Guid.NewGuid(),
            TenantId: tenantId,
            CorrelationId: "corr-redact",
            Subject: "VERY_SENSITIVE_SUBJECT_TOKEN",
            To: [new EmailRecipientDto("secret-rcpt@example.com", "Secret")],
            Cc: [],
            Bcc: [],
            BodyHtmlPreview: $"<p>{fullSensitive}-PREVIEW</p>",
            BodyTextPreview: $"{fullSensitive}-PREVIEW",
            BodyHtml: $"<p>{fullSensitive}</p>",
            BodyText: fullSensitive);

        var result = await provider.SendEmailAsync(request);

        Assert.True(result.Accepted);
        foreach (var entry in logger.Entries)
        {
            var rendered = entry.Render();
            Assert.DoesNotContain(fullSensitive, rendered, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VERY_SENSITIVE_SUBJECT_TOKEN", rendered, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-rcpt@example.com", rendered, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task QueueEmail_EndToEnd_ShouldForwardFullBody_ToSmtpProvider()
    {
        var tenantId = Guid.NewGuid();
        var settingsRepo = CreateSettingsRepoForIntegration(tenantId);
        var dispatches = new InMemoryDispatches();

        var longContent = new string('Z', PreviewMaxLength + 500);
        var templateKey = "tenant.long.email";
        var templates = new InMemoryTemplates();
        await templates.CreateAsync(new NotificationTemplate
        {
            IsPlatformDefault = true,
            TemplateKey = templateKey,
            Channel = NotificationChannelCode.Email,
            Locale = "en",
            SubjectTemplate = "Welcome",
            BodyHtmlTemplate = "<p>{{content}}</p>",
            BodyTextTemplate = "{{content}}",
            Status = NotificationTemplateStatus.Active,
            Variables =
            [
                new TemplateVariableDefinition { Name = "content", Type = TemplateVariableType.String, IsRequired = true }
            ]
        });

        var transport = new NotificationsSmtpProviderTests.FakeSmtpTransport
        {
            SendBehavior = (_, _) => Task.FromResult("OK 250 ok")
        };
        var factory = new NotificationsSmtpProviderTests.RecordingSmtpClientFactory(transport);
        var smtpProvider = CreateProvider(settingsRepo, factory);

        var handler = new QueueEmailNotificationHandler(
            new TenantMessagingSettingsResolver(settingsRepo),
            templates,
            new EmailTemplateRenderer(),
            dispatches,
            new TestResolver(smtpProvider),
            new NoOpBus(),
            NullLogger<QueueEmailNotificationHandler>.Instance);

        var request = new QueueEmailNotificationRequest(
            TemplateKey: templateKey,
            Locale: "en",
            Variables: new Dictionary<string, object?> { ["content"] = longContent },
            To: [new EmailRecipientDto("user@example.com", "User")]);

        var response = await handler.Handle(
            new QueueEmailNotificationCommand(tenantId, request, "corr-full-body"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(NotificationDispatchStatus.Sent.ToString(), response.Data!.Status);
        var dispatch = Assert.Single(dispatches.Items);
        Assert.NotNull(transport.LastSentMessage);

        // Persisted dispatch only stores the truncated preview (audit-safe).
        Assert.True(dispatch.BodyHtmlPreview!.Length <= PreviewMaxLength);
        Assert.True(dispatch.BodyTextPreview!.Length <= PreviewMaxLength);

        // Provider received and sent the full body, not the preview.
        Assert.NotNull(transport.LastSentMessage!.HtmlBody);
        Assert.NotNull(transport.LastSentMessage.TextBody);
        Assert.True(transport.LastSentMessage.HtmlBody!.Length > PreviewMaxLength);
        Assert.True(transport.LastSentMessage.TextBody!.Length > PreviewMaxLength);
        Assert.Contains(longContent, transport.LastSentMessage.HtmlBody);
        Assert.Contains(longContent, transport.LastSentMessage.TextBody);
    }

    private static SmtpMessagingProvider CreateProvider(
        ITenantMessagingSettingsRepository repository,
        ISmtpClientFactory factory,
        ILogger<SmtpMessagingProvider>? logger = null) =>
        new(
            new NotificationsSmtpProviderTests.StaticOptionsMonitor<SmtpProviderOptions>(new SmtpProviderOptions()),
            repository,
            factory,
            new SecretReferenceResolver(new NotificationsSmtpProviderTests.InMemorySecretsProvider("resolved-password")),
            new NotificationsSmtpProviderTests.TestHostEnvironment("Development"),
            logger ?? NullLogger<SmtpMessagingProvider>.Instance);

    private static NotificationsSmtpProviderTests.InMemoryTenantMessagingSettingsRepository CreateRepository(params TenantMessagingSettings[] items)
    {
        var repo = new NotificationsSmtpProviderTests.InMemoryTenantMessagingSettingsRepository();
        foreach (var item in items)
        {
            repo.CreateAsync(item).GetAwaiter().GetResult();
        }
        return repo;
    }

    private static NotificationsSmtpProviderTests.InMemoryTenantMessagingSettingsRepository CreateSettingsRepoForIntegration(Guid tenantId)
    {
        var repo = new NotificationsSmtpProviderTests.InMemoryTenantMessagingSettingsRepository();
        repo.CreateAsync(CreateSettings(tenantId)).GetAwaiter().GetResult();
        return repo;
    }

    private static TenantMessagingSettings CreateSettings(
        Guid tenantId,
        string senderEmail = "sender@example.com") =>
        new()
        {
            TenantId = tenantId,
            IsPlatformDefault = false,
            ProviderCode = MessagingProviderCode.Smtp,
            SenderEmail = senderEmail,
            SenderName = "Sender",
            Host = "smtp.example.test",
            Port = 587,
            UseSsl = true,
            CredentialSecretRef = "secret:platform:smtp:default",
            IsEnabled = true,
            FallbackPolicy = NotificationFallbackPolicy.UsePlatformDefault
        };

    private sealed class TestResolver : IMessagingProviderResolver
    {
        private readonly IMessagingProvider _provider;
        public TestResolver(IMessagingProvider provider) => _provider = provider;
        public Response<IMessagingProvider> Resolve(MessagingProviderCode providerCode) =>
            providerCode == _provider.ProviderCode
                ? Response<IMessagingProvider>.Success(_provider)
                : Response<IMessagingProvider>.Fail("unavailable", 400);
    }

    private sealed class NoOpBus : IEventBus
    {
        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent =>
            PublishAsync(@event, new EventPublishOptions(), cancellationToken);

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, EventPublishOptions options, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
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

    private sealed class InMemoryTemplates : INotificationTemplateRepository
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
            Guid? tenantId,
            bool isPlatformDefault,
            NotificationTemplateStatus? status = null,
            string? locale = null,
            NotificationChannelCode? channel = null,
            string? templateKey = null,
            int skip = 0,
            int take = 50,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationTemplate>>(_items
                .Where(x => !x.IsDeleted
                    && x.TenantId == tenantId
                    && x.IsPlatformDefault == isPlatformDefault
                    && (status is null || x.Status == status)
                    && (locale is null || x.Locale == locale)
                    && (channel is null || x.Channel == channel)
                    && (templateKey is null || x.TemplateKey == templateKey))
                .Skip(skip).Take(take).ToArray());

        public Task<NotificationTemplate?> GetActiveByKeyAsync(
            Guid? tenantId,
            bool isPlatformDefault,
            string templateKey,
            string locale,
            NotificationChannelCode channel,
            CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x =>
                !x.IsDeleted
                && x.Status == NotificationTemplateStatus.Active
                && x.TenantId == tenantId
                && x.IsPlatformDefault == isPlatformDefault
                && x.TemplateKey == templateKey
                && x.Locale == locale
                && x.Channel == channel));

        public async Task<NotificationTemplate?> GetBestActiveByKeyAsync(
            Guid tenantId,
            string templateKey,
            string locale,
            NotificationChannelCode channel,
            CancellationToken ct = default) =>
            await GetActiveByKeyAsync(tenantId, false, templateKey, locale, channel, ct)
            ?? await GetActiveByKeyAsync(null, true, templateKey, locale, channel, ct);

        public Task<bool> ActiveTemplateExistsAsync(
            Guid? tenantId,
            bool isPlatformDefault,
            string templateKey,
            string locale,
            NotificationChannelCode channel,
            Guid? excludeId = null,
            CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default) => Task.CompletedTask;
        public Task ArchiveAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

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

        public Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, NotificationDispatchStatus? status = null, DateTimeOffset? queuedFrom = null, DateTimeOffset? queuedTo = null, string? templateKey = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationDispatch>>([]);

        public Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<NotificationDispatchRetryHandle>> FindDueRetriesAsync(DateTimeOffset asOfUtc, int maxRetryCount, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationDispatchRetryHandle>>([]);
    }
}
