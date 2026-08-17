using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Notifications.Validators;
using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.Notifications;
using Diten.Platform.Infrastructure.Settings;
using FluentValidation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

public sealed class NotificationsBatch1ATests
{
    [Fact]
    public async Task Resolver_ShouldUseTenantSpecificActiveSettings_WhenPresent()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTenantMessagingSettingsRepository();
        await repository.CreateAsync(CreateSettings(tenantId, isPlatformDefault: false, senderEmail: "tenant@example.com"));
        await repository.CreateAsync(CreateSettings(null, isPlatformDefault: true, senderEmail: "default@example.com"));
        var resolver = new TenantMessagingSettingsResolver(repository);

        var response = await resolver.ResolveAsync(tenantId);

        Assert.True(response.IsSuccessful);
        Assert.Equal("tenant@example.com", response.Data!.SenderEmail);
        Assert.False(response.Data.IsPlatformDefault);
    }

    [Fact]
    public async Task Resolver_ShouldFallbackToPlatformDefault_WhenTenantSettingsMissing()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTenantMessagingSettingsRepository();
        await repository.CreateAsync(CreateSettings(null, isPlatformDefault: true, senderEmail: "default@example.com"));
        var resolver = new TenantMessagingSettingsResolver(repository);

        var response = await resolver.ResolveAsync(tenantId);

        Assert.True(response.IsSuccessful);
        Assert.Equal("default@example.com", response.Data!.SenderEmail);
        Assert.True(response.Data.IsPlatformDefault);
        Assert.Equal(tenantId, response.Data.RequestedTenantId);
    }

    [Fact]
    public async Task Resolver_ShouldFail_WhenTenantSettingsDisabledAndFallbackDisabled()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTenantMessagingSettingsRepository();
        await repository.CreateAsync(CreateSettings(tenantId, isPlatformDefault: false, isEnabled: false, policy: NotificationFallbackPolicy.DisableSending));
        await repository.CreateAsync(CreateSettings(null, isPlatformDefault: true));
        var resolver = new TenantMessagingSettingsResolver(repository);

        var response = await resolver.ResolveAsync(tenantId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Resolver_ShouldFail_WhenPlatformDefaultMissing()
    {
        var resolver = new TenantMessagingSettingsResolver(new InMemoryTenantMessagingSettingsRepository());

        var response = await resolver.ResolveAsync(Guid.NewGuid());

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Platform default", response.Errors.Single());
    }

    [Fact]
    public void TemplateRenderer_ShouldRender_WhenRequiredVariablesExist()
    {
        var renderer = new EmailTemplateRenderer();
        var template = CreateTemplate("tenant.invite.email");

        var response = renderer.Render(template, new Dictionary<string, object?>
        {
            ["tenantName"] = "Acme",
            ["inviteUrl"] = "https://example.test/invite"
        });

        Assert.True(response.IsSuccessful);
        Assert.Equal("Welcome Acme", response.Data!.Subject);
        Assert.Contains("https://example.test/invite", response.Data.BodyHtmlPreview);
    }

    [Fact]
    public void TemplateRenderer_ShouldFail_WhenRequiredVariableMissing()
    {
        var renderer = new EmailTemplateRenderer();
        var template = CreateTemplate("tenant.invite.email");

        var response = renderer.Render(template, new Dictionary<string, object?>
        {
            ["tenantName"] = "Acme"
        });

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("inviteUrl", response.Errors.Single());
    }

    [Fact]
    public async Task CreateTemplate_ShouldRejectDuplicateActiveTemplate()
    {
        var repository = new InMemoryNotificationTemplateRepository();
        await repository.CreateAsync(CreateTemplate("tenant.invite.email"));
        var handler = new CreateNotificationTemplateHandler(repository);

        var response = await handler.Handle(
            new CreateNotificationTemplateCommand(null, ValidTemplateRequest()),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public void TenantSettingsValidator_ShouldRejectRawSecretPayload()
    {
        var validator = new UpsertTenantMessagingSettingsValidator();
        var command = new UpsertTenantMessagingSettingsCommand(
            Guid.NewGuid(),
            ValidSettingsRequest() with { CredentialSecretRef = "password=plain-text-secret" });

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("raw password/API key/token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RequestPayloads_ShouldNotExposeTenantId()
    {
        Assert.DoesNotContain(typeof(TenantMessagingSettingsUpsertRequest).GetProperties(), property => property.Name == "TenantId");
        Assert.DoesNotContain(typeof(NotificationTemplateUpsertRequest).GetProperties(), property => property.Name == "TenantId");
    }

    [Fact]
    public async Task SettingsQuery_ShouldReturn404_ForCrossTenantOrSoftDeletedRecord()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var repository = new InMemoryTenantMessagingSettingsRepository();
        await repository.CreateAsync(CreateSettings(tenantB, isPlatformDefault: false));
        var deleted = CreateSettings(tenantA, isPlatformDefault: false);
        deleted.IsDeleted = true;
        await repository.CreateAsync(deleted);
        var handler = new GetTenantMessagingSettingsHandler(repository);

        var response = await handler.Handle(new GetTenantMessagingSettingsQuery(tenantA), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public void NotificationCommands_ShouldUseMod0021AuditSeam()
    {
        var command = new UpsertTenantMessagingSettingsCommand(Guid.NewGuid(), ValidSettingsRequest());

        Assert.IsAssignableFrom<IAuditableCommand>(command);
        var metadata = Assert.IsAssignableFrom<IAuditMetadataProvider>(command).GetAuditMetadata();

        Assert.Equal("MOD-0027", metadata.SourceModule);
        Assert.Equal("notifications.tenant_messaging_settings.upserted", metadata.Metadata!["EventName"]);
        Assert.Equal(AuditCategory.PlatformConfiguration, metadata.Category);
    }

    [Fact]
    public async Task QueueEmail_ShouldPersistDispatchAndMarkSent_WhenFakeProviderEnabled()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryNotificationDispatchRepository();
        var provider = new CountingProvider(MessagingProviderResult.Success("fake-accepted"));
        var handler = CreateQueueHandler(tenantId, dispatches, provider: provider);

        var response = await handler.Handle(new QueueEmailNotificationCommand(tenantId, ValidQueueRequest(), "corr-1"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(NotificationDispatchStatus.Sent.ToString(), response.Data!.Status);
        Assert.Equal(tenantId, response.Data.TenantId);
        Assert.Equal("fake-accepted", response.Data.ProviderMessageId);
        Assert.Equal("u***@example.com", response.Data.To.Single().Email);
        Assert.Equal(1, provider.CallCount);
        Assert.Single(dispatches.Items);
        Assert.Equal("Welcome Acme", dispatches.Items[0].Subject);
        Assert.DoesNotContain("raw-token", dispatches.Items[0].VariablesJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueueEmail_ShouldUsePlatformDefaultSettings_WhenTenantSettingsMissing()
    {
        var tenantId = Guid.NewGuid();
        var settings = new InMemoryTenantMessagingSettingsRepository();
        await settings.CreateAsync(CreateSettings(null, isPlatformDefault: true, senderEmail: "default@example.com"));
        var dispatches = new InMemoryNotificationDispatchRepository();
        var handler = CreateQueueHandler(tenantId, dispatches, settings);

        var response = await handler.Handle(new QueueEmailNotificationCommand(tenantId, ValidQueueRequest(), "corr-2"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Fake", response.Data!.ProviderCode);
        Assert.Single(dispatches.Items);
    }

    [Fact]
    public async Task QueueEmail_ShouldNotCallProviderOrPersistDispatch_WhenRequiredVariableMissing()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryNotificationDispatchRepository();
        var provider = new CountingProvider(MessagingProviderResult.Success("fake-accepted"));
        var handler = CreateQueueHandler(tenantId, dispatches, provider: provider);
        var request = ValidQueueRequest() with { Variables = new Dictionary<string, object?> { ["tenantName"] = "Acme" } };

        var response = await handler.Handle(new QueueEmailNotificationCommand(tenantId, request, "corr-3"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(0, provider.CallCount);
        Assert.Empty(dispatches.Items);
    }

    [Fact]
    public void QueueValidator_ShouldRejectInvalidRecipient()
    {
        var validator = new QueueEmailNotificationValidator();
        var request = ValidQueueRequest() with { To = [new("not-email", "Bad")] };

        var result = validator.Validate(new QueueEmailNotificationCommand(Guid.NewGuid(), request));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueueEmail_ShouldFail_WhenFakeProviderDisabled()
    {
        var tenantId = Guid.NewGuid();
        var dispatches = new InMemoryNotificationDispatchRepository();
        var provider = new CountingProvider(MessagingProviderResult.Fail("FakeProviderDisabled", "Fake messaging provider is disabled."));
        var handler = CreateQueueHandler(tenantId, dispatches, provider: provider);

        var response = await handler.Handle(new QueueEmailNotificationCommand(tenantId, ValidQueueRequest(), "corr-4"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Single(dispatches.Items);
        Assert.Equal(NotificationDispatchStatus.Failed, dispatches.Items[0].Status);
    }

    [Fact]
    public async Task FakeProvider_ShouldBlockProductionEvenWhenEnabled()
    {
        var provider = new FakeMessagingProvider(
            Options.Create(new FakeMessagingProviderOptions { Enabled = true }),
            new TestHostEnvironment("Production"));

        var result = await provider.SendEmailAsync(new MessagingProviderEmailRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "corr-prod",
            "Subject",
            [new("user@example.com", null)],
            [],
            [],
            "preview",
            null));

        Assert.False(result.Accepted);
        Assert.Equal("FakeProviderProductionBlocked", result.ErrorCode);
    }

    [Fact]
    public async Task DispatchQueries_ShouldReturn404_ForCrossTenantOrSoftDeletedDispatch()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var repository = new InMemoryNotificationDispatchRepository();
        var dispatch = await repository.CreateAsync(CreateDispatch(tenantB));
        var handler = new GetNotificationDispatchByIdHandler(repository);

        var crossTenant = await handler.Handle(new GetNotificationDispatchByIdQuery(tenantA, dispatch.Id), CancellationToken.None);
        dispatch.IsDeleted = true;
        var softDeleted = await handler.Handle(new GetNotificationDispatchByIdQuery(tenantB, dispatch.Id), CancellationToken.None);

        Assert.Equal(404, crossTenant.StatusCode);
        Assert.Equal(404, softDeleted.StatusCode);
    }

    [Fact]
    public async Task DispatchTransition_ShouldRejectInvalidTransition()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryNotificationDispatchRepository();
        var dispatch = CreateDispatch(tenantId);
        dispatch.TryMarkSent("provider-1", DateTimeOffset.UtcNow);
        await repository.CreateAsync(dispatch);
        var handler = new CancelNotificationDispatchHandler(repository, new NoOpEventBus());

        var response = await handler.Handle(new CancelNotificationDispatchCommand(tenantId, dispatch.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    private static TenantMessagingSettingsUpsertRequest ValidSettingsRequest() =>
        new(
            "Fake",
            "sender@example.com",
            "Sender",
            null,
            null,
            null,
            true,
            null,
            "secret:platform:notifications:fake",
            true,
            "UsePlatformDefault");

    private static NotificationTemplateUpsertRequest ValidTemplateRequest() =>
        new(
            true,
            "tenant.invite.email",
            "Email",
            "en",
            "Welcome {{tenantName}}",
            "<p>{{inviteUrl}}</p>",
            "Open {{inviteUrl}}",
            [
                new("tenantName", "String", true),
                new("inviteUrl", "Url", true)
            ],
            "Active",
            "1.0.0");

    private static QueueEmailNotificationRequest ValidQueueRequest() =>
        new(
            "tenant.invite.email",
            "en",
            new Dictionary<string, object?>
            {
                ["tenantName"] = "Acme",
                ["inviteUrl"] = "https://example.test/invite",
                ["token"] = "raw-token"
            },
            [new("user@example.com", "User")]);

    private static TenantMessagingSettings CreateSettings(
        Guid? tenantId,
        bool isPlatformDefault,
        string senderEmail = "sender@example.com",
        bool isEnabled = true,
        NotificationFallbackPolicy policy = NotificationFallbackPolicy.UsePlatformDefault) =>
        new()
        {
            TenantId = tenantId,
            IsPlatformDefault = isPlatformDefault,
            ProviderCode = MessagingProviderCode.Fake,
            SenderEmail = senderEmail,
            IsEnabled = isEnabled,
            FallbackPolicy = policy
        };

    private static NotificationTemplate CreateTemplate(string key) =>
        new()
        {
            IsPlatformDefault = true,
            TemplateKey = key,
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
        };

    private static NotificationDispatch CreateDispatch(Guid tenantId) =>
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
            VariablesJson = "{}",
            QueuedAt = DateTimeOffset.UtcNow
        };

    private static QueueEmailNotificationHandler CreateQueueHandler(
        Guid tenantId,
        InMemoryNotificationDispatchRepository dispatches,
        InMemoryTenantMessagingSettingsRepository? settings = null,
        CountingProvider? provider = null)
    {
        settings ??= new InMemoryTenantMessagingSettingsRepository();
        if (!settings.Items.Any())
        {
            settings.CreateAsync(CreateSettings(tenantId, isPlatformDefault: false)).GetAwaiter().GetResult();
        }

        var templates = new InMemoryNotificationTemplateRepository();
        templates.CreateAsync(CreateTemplate("tenant.invite.email")).GetAwaiter().GetResult();
        provider ??= new CountingProvider(MessagingProviderResult.Success("fake-accepted"));

        return new QueueEmailNotificationHandler(
            new TenantMessagingSettingsResolver(settings),
            templates,
            new EmailTemplateRenderer(),
            dispatches,
            new TestProviderResolver(provider),
            new NoOpEventBus(),
            NullLogger<QueueEmailNotificationHandler>.Instance);
    }

    private sealed class NoOpEventBus : IEventBus
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

    private sealed class InMemoryTenantMessagingSettingsRepository : ITenantMessagingSettingsRepository
    {
        private readonly List<TenantMessagingSettings> _items = [];
        public IReadOnlyList<TenantMessagingSettings> Items => _items;

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
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && !x.IsPlatformDefault && x.TenantId == tenantId && x.Id == id));

        public Task<TenantMessagingSettings?> GetPlatformDefaultByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && x.IsPlatformDefault && x.TenantId is null && x.Id == id));

        public Task UpdateAsync(TenantMessagingSettings settings, CancellationToken ct = default) => Task.CompletedTask;

        public Task SoftDeleteTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            foreach (var item in _items.Where(x => x.TenantId == tenantId && !x.IsPlatformDefault))
            {
                item.IsDeleted = true;
            }

            return Task.CompletedTask;
        }

        public Task SoftDeletePlatformDefaultAsync(CancellationToken ct = default)
        {
            foreach (var item in _items.Where(x => x.IsPlatformDefault && x.TenantId is null))
            {
                item.IsDeleted = true;
            }

            return Task.CompletedTask;
        }
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

        public Task<NotificationTemplate?> GetActiveByKeyAsync(
            Guid? tenantId,
            bool isPlatformDefault,
            string templateKey,
            string locale,
            NotificationChannelCode channel,
            CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => Matches(x, tenantId, isPlatformDefault, templateKey, locale, channel)));

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
            Task.FromResult(_items.Any(x => Matches(x, tenantId, isPlatformDefault, templateKey, locale, channel) && x.Id != excludeId));

        public Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default) => Task.CompletedTask;

        public Task ArchiveAsync(Guid id, CancellationToken ct = default)
        {
            var template = _items.FirstOrDefault(x => x.Id == id);
            if (template is not null)
            {
                template.Status = NotificationTemplateStatus.Archived;
                template.IsDeleted = true;
            }

            return Task.CompletedTask;
        }

        private static bool Matches(
            NotificationTemplate template,
            Guid? tenantId,
            bool isPlatformDefault,
            string templateKey,
            string locale,
            NotificationChannelCode channel) =>
            !template.IsDeleted
            && template.Status == NotificationTemplateStatus.Active
            && template.TenantId == tenantId
            && template.IsPlatformDefault == isPlatformDefault
            && template.TemplateKey == templateKey
            && template.Locale == locale
            && template.Channel == channel;
    }

    private sealed class InMemoryNotificationDispatchRepository : INotificationDispatchRepository
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

    private sealed class TestProviderResolver : IMessagingProviderResolver
    {
        private readonly IMessagingProvider _provider;
        public TestProviderResolver(IMessagingProvider provider) => _provider = provider;
        public Response<IMessagingProvider> Resolve(MessagingProviderCode providerCode) =>
            providerCode == _provider.ProviderCode
                ? Response<IMessagingProvider>.Success(_provider)
                : Response<IMessagingProvider>.Fail("Provider unavailable.", 400);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Diten.Platform.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
