using Diten.BuildingBlocks.Eventing;
using Diten.BuildingBlocks.Security.Secrets;
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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

public sealed class NotificationsSmtpIntegrationTests
{
    [Fact]
    public async Task QueueEmail_EndToEnd_WithSmtpProvider_AndMockedTransport_ShouldReachSent()
    {
        var tenantId = Guid.NewGuid();
        var settingsRepo = CreateSettingsRepository(tenantId);
        var dispatches = new InMemoryNotificationDispatchRepository();
        var templates = CreateTemplateRepository();

        var transport = new RecordingTransport
        {
            SendResponse = "OK 250 accepted-by-mock"
        };
        var smtpProvider = CreateSmtpProvider(settingsRepo, transport);
        var resolver = new TestProviderResolver(smtpProvider);
        var handler = new QueueEmailNotificationHandler(
            new TenantMessagingSettingsResolver(settingsRepo),
            templates,
            new EmailTemplateRenderer(),
            dispatches,
            resolver,
            new RecordingEventBus(),
            NullLogger<QueueEmailNotificationHandler>.Instance);

        var request = new QueueEmailNotificationRequest(
            TemplateKey: "tenant.invite.email",
            Locale: "en",
            Variables: new Dictionary<string, object?>
            {
                ["tenantName"] = "Acme",
                ["inviteUrl"] = "https://example.test/invite"
            },
            To: [new EmailRecipientDto("user@example.com", "User")]);

        var response = await handler.Handle(
            new QueueEmailNotificationCommand(tenantId, request, "corr-smtp-1"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(NotificationDispatchStatus.Sent.ToString(), response.Data!.Status);
        Assert.Equal("Smtp", response.Data.ProviderCode);
        Assert.False(string.IsNullOrWhiteSpace(response.Data.ProviderMessageId));
        Assert.True(transport.ConnectCount > 0);
        Assert.True(transport.AuthenticateCount > 0);
        Assert.Single(dispatches.Items);
        Assert.Equal(NotificationDispatchStatus.Sent, dispatches.Items[0].Status);
    }

    [Fact]
    public async Task QueueEmail_WithBrokenSmtp_ShouldReachFailed_WithRedactedErrorMetadata()
    {
        var tenantId = Guid.NewGuid();
        var settingsRepo = CreateSettingsRepository(tenantId);
        var dispatches = new InMemoryNotificationDispatchRepository();
        var templates = CreateTemplateRepository();

        var transport = new RecordingTransport
        {
            AuthenticateThrow = new System.Security.Authentication.AuthenticationException(
                "535 5.7.8 password=BAD_RAW_SECRET")
        };
        var smtpProvider = CreateSmtpProvider(settingsRepo, transport);
        var resolver = new TestProviderResolver(smtpProvider);
        var handler = new QueueEmailNotificationHandler(
            new TenantMessagingSettingsResolver(settingsRepo),
            templates,
            new EmailTemplateRenderer(),
            dispatches,
            resolver,
            new RecordingEventBus(),
            NullLogger<QueueEmailNotificationHandler>.Instance);

        var request = new QueueEmailNotificationRequest(
            TemplateKey: "tenant.invite.email",
            Locale: "en",
            Variables: new Dictionary<string, object?>
            {
                ["tenantName"] = "Acme",
                ["inviteUrl"] = "https://example.test/invite"
            },
            To: [new EmailRecipientDto("user@example.com", "User")]);

        var response = await handler.Handle(
            new QueueEmailNotificationCommand(tenantId, request, "corr-smtp-broken"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        var dispatch = Assert.Single(dispatches.Items);
        Assert.Equal(NotificationDispatchStatus.Failed, dispatch.Status);
        Assert.Equal(MessagingProviderErrorCodes.ProviderAuthFailed, dispatch.ErrorCode);
        Assert.NotNull(dispatch.ErrorMessage);
        Assert.DoesNotContain("BAD_RAW_SECRET", dispatch.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", dispatch.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    private static InMemoryTenantMessagingSettingsRepository CreateSettingsRepository(Guid tenantId)
    {
        var repo = new InMemoryTenantMessagingSettingsRepository();
        repo.CreateAsync(new TenantMessagingSettings
        {
            TenantId = tenantId,
            IsPlatformDefault = false,
            ProviderCode = MessagingProviderCode.Smtp,
            SenderEmail = "sender@example.com",
            SenderName = "Sender",
            Host = "smtp.example.test",
            Port = 587,
            UseSsl = true,
            CredentialSecretRef = "secret:platform:smtp:default",
            IsEnabled = true,
            FallbackPolicy = NotificationFallbackPolicy.UsePlatformDefault
        }).GetAwaiter().GetResult();

        return repo;
    }

    private static InMemoryNotificationTemplateRepository CreateTemplateRepository()
    {
        var repo = new InMemoryNotificationTemplateRepository();
        repo.CreateAsync(new NotificationTemplate
        {
            IsPlatformDefault = true,
            TemplateKey = "tenant.invite.email",
            Channel = NotificationChannelCode.Email,
            Locale = "en",
            SubjectTemplate = "Welcome {{tenantName}}",
            BodyHtmlTemplate = "<p>{{inviteUrl}}</p>",
            BodyTextTemplate = "Open {{inviteUrl}}",
            Status = NotificationTemplateStatus.Active,
            Variables =
            [
                new TemplateVariableDefinition { Name = "tenantName", Type = TemplateVariableType.String, IsRequired = true },
                new TemplateVariableDefinition { Name = "inviteUrl", Type = TemplateVariableType.Url, IsRequired = true }
            ]
        }).GetAwaiter().GetResult();
        return repo;
    }

    private static SmtpMessagingProvider CreateSmtpProvider(
        ITenantMessagingSettingsRepository repository,
        RecordingTransport transport)
    {
        var factory = new RecordingFactory(transport);
        var monitor = new NotificationsSmtpProviderTests.StaticOptionsMonitor<SmtpProviderOptions>(
            new SmtpProviderOptions());
        var secrets = new NotificationsSmtpProviderTests.InMemorySecretsProvider("resolved-password");
        var secretResolver = new SecretReferenceResolver(secrets);
        var env = new NotificationsSmtpProviderTests.TestHostEnvironment("Development");
        return new SmtpMessagingProvider(monitor, repository, factory, secretResolver, env, NullLogger<SmtpMessagingProvider>.Instance);
    }

    internal sealed class RecordingFactory : ISmtpClientFactory
    {
        private readonly RecordingTransport _transport;
        public RecordingFactory(RecordingTransport transport) => _transport = transport;
        public ISmtpTransport Create() => _transport;
    }

    internal sealed class RecordingTransport : ISmtpTransport
    {
        public Exception? ConnectThrow { get; set; }
        public Exception? AuthenticateThrow { get; set; }
        public Exception? SendThrow { get; set; }
        public string SendResponse { get; set; } = string.Empty;

        public int ConnectCount { get; private set; }
        public int AuthenticateCount { get; private set; }
        public int Timeout { get; set; }

        public Task ConnectAsync(string host, int port, SecureSocketOptions socketOptions, CancellationToken ct)
        {
            ConnectCount++;
            if (ConnectThrow is not null) throw ConnectThrow;
            return Task.CompletedTask;
        }

        public Task AuthenticateAsync(string userName, string password, CancellationToken ct)
        {
            AuthenticateCount++;
            if (AuthenticateThrow is not null) throw AuthenticateThrow;
            return Task.CompletedTask;
        }

        public Task<string> SendAsync(MimeKit.MimeMessage message, CancellationToken ct)
        {
            if (SendThrow is not null) throw SendThrow;
            return Task.FromResult(SendResponse);
        }

        public Task DisconnectAsync(bool quit, CancellationToken ct) => Task.CompletedTask;

        public void Dispose() { }
    }

    internal sealed class TestProviderResolver : IMessagingProviderResolver
    {
        private readonly IMessagingProvider _provider;
        public TestProviderResolver(IMessagingProvider provider) => _provider = provider;
        public Response<IMessagingProvider> Resolve(MessagingProviderCode providerCode) =>
            providerCode == _provider.ProviderCode
                ? Response<IMessagingProvider>.Success(_provider)
                : Response<IMessagingProvider>.Fail($"{providerCode} provider unavailable.", 400);
    }

    internal sealed class RecordingEventBus : IEventBus
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

    internal sealed class InMemoryTenantMessagingSettingsRepository : ITenantMessagingSettingsRepository
    {
        private readonly List<TenantMessagingSettings> _items = [];

        public Task<TenantMessagingSettings> CreateAsync(TenantMessagingSettings settings, CancellationToken ct = default)
        {
            _items.Add(settings);
            return Task.FromResult(settings);
        }

        public Task<TenantMessagingSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && !x.IsPlatformDefault && x.TenantId == tenantId));

        public Task<TenantMessagingSettings?> GetPlatformDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && x.IsPlatformDefault && x.TenantId is null));

        public Task<TenantMessagingSettings?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && x.TenantId == tenantId && x.Id == id));

        public Task<TenantMessagingSettings?> GetPlatformDefaultByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && x.IsPlatformDefault && x.Id == id));

        public Task UpdateAsync(TenantMessagingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task SoftDeleteTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SoftDeletePlatformDefaultAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class InMemoryNotificationTemplateRepository : INotificationTemplateRepository
    {
        private readonly List<NotificationTemplate> _items = [];

        public Task<NotificationTemplate> CreateAsync(NotificationTemplate template, CancellationToken ct = default)
        {
            _items.Add(template);
            return Task.FromResult(template);
        }

        public Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && x.Id == id));

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

    internal sealed class InMemoryNotificationDispatchRepository : INotificationDispatchRepository
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
            Task.FromResult<IReadOnlyList<NotificationDispatch>>(Items
                .Where(x => !x.IsDeleted && x.TenantId == tenantId)
                .Skip(skip).Take(take).ToArray());

        public Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<NotificationDispatchRetryHandle>> FindDueRetriesAsync(DateTimeOffset asOfUtc, int maxRetryCount, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationDispatchRetryHandle>>([]);
    }
}
