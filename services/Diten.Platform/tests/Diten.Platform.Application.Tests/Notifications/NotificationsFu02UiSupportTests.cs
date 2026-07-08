using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// MOD-0027-FU02 UI-support backend tests: render-preview, dispatch list filters,
/// template list and tenant messaging settings list handlers.
/// </summary>
public sealed class NotificationsFu02UiSupportTests
{
    private static readonly Guid TenantA = Guid.NewGuid();

    // --- Render preview ---

    [Fact]
    public async Task RenderPreview_renders_unsaved_content_with_all_required_variables()
    {
        var handler = new RenderNotificationTemplatePreviewHandler(new EmailTemplateRenderer());
        var request = new RenderTemplatePreviewRequest(
            "Hello {{UserName}}",
            "<p>Hi {{UserName}}, welcome to {{TenantName}}.</p>",
            "Hi {{UserName}}",
            [new TemplateVariableDefinitionDto("UserName", "String", true), new TemplateVariableDefinitionDto("TenantName", "String", true)],
            new Dictionary<string, object?> { ["UserName"] = "Ada", ["TenantName"] = "Acme" });

        var response = await handler.Handle(new RenderNotificationTemplatePreviewQuery(request), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Hello Ada", response.Data!.Subject);
        Assert.Contains("Hi Ada, welcome to Acme.", response.Data.BodyHtml);
        Assert.Contains("Hi Ada", response.Data.BodyText);
    }

    [Fact]
    public async Task RenderPreview_missing_required_variable_returns_400()
    {
        var handler = new RenderNotificationTemplatePreviewHandler(new EmailTemplateRenderer());
        var request = new RenderTemplatePreviewRequest(
            "Hello {{UserName}}",
            "<p>Hi {{UserName}}</p>",
            null,
            [new TemplateVariableDefinitionDto("UserName", "String", true)],
            new Dictionary<string, object?>());

        var response = await handler.Handle(new RenderNotificationTemplatePreviewQuery(request), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains("Missing required template variable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RenderPreview_unknown_variable_type_returns_400()
    {
        var handler = new RenderNotificationTemplatePreviewHandler(new EmailTemplateRenderer());
        var request = new RenderTemplatePreviewRequest(
            "Subject",
            "<p>Body</p>",
            null,
            [new TemplateVariableDefinitionDto("UserName", "Fancy", true)],
            new Dictionary<string, object?> { ["UserName"] = "Ada" });

        var response = await handler.Handle(new RenderNotificationTemplatePreviewQuery(request), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    // --- Dispatch list filters ---

    [Fact]
    public async Task DispatchList_passes_parsed_filters_to_repository()
    {
        var repository = new RecordingDispatchRepository();
        var handler = new GetNotificationDispatchListHandler(repository);
        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow;

        var response = await handler.Handle(
            new GetNotificationDispatchListQuery(TenantA, 2, 10, "sent", from, to, "  Tenant.Invite.Email "),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(TenantA, repository.LastTenantId);
        Assert.Equal(10, repository.LastSkip);
        Assert.Equal(10, repository.LastTake);
        Assert.Equal(NotificationDispatchStatus.Sent, repository.LastStatus);
        Assert.Equal(from, repository.LastQueuedFrom);
        Assert.Equal(to, repository.LastQueuedTo);
        Assert.Equal("tenant.invite.email", repository.LastTemplateKey);
    }

    [Fact]
    public async Task DispatchList_unknown_status_returns_400_without_repository_call()
    {
        var repository = new RecordingDispatchRepository();
        var handler = new GetNotificationDispatchListHandler(repository);

        var response = await handler.Handle(
            new GetNotificationDispatchListQuery(TenantA, Status: "delivered"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.False(repository.ListCalled);
    }

    [Fact]
    public async Task DispatchList_from_after_to_returns_400()
    {
        var repository = new RecordingDispatchRepository();
        var handler = new GetNotificationDispatchListHandler(repository);

        var response = await handler.Handle(
            new GetNotificationDispatchListQuery(
                TenantA,
                QueuedFrom: DateTimeOffset.UtcNow,
                QueuedTo: DateTimeOffset.UtcNow.AddDays(-1)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.False(repository.ListCalled);
    }

    // --- Template list ---

    [Fact]
    public async Task TemplateList_platform_scope_normalizes_filters_and_maps_dtos()
    {
        var repository = new RecordingTemplateRepository();
        repository.Items.Add(new NotificationTemplate
        {
            IsPlatformDefault = true,
            TemplateKey = "tenant.invite.email",
            Locale = "en",
            SubjectTemplate = "Hello {{UserName}}",
            BodyHtmlTemplate = "<p>Hi</p>",
            Status = NotificationTemplateStatus.Active
        });
        var handler = new GetNotificationTemplateListHandler(repository);

        var response = await handler.Handle(
            new GetNotificationTemplateListQuery(null, true, "active", "EN", "email", "Tenant.Invite.Email"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var dto = Assert.Single(response.Data!);
        Assert.Equal("tenant.invite.email", dto.TemplateKey);
        Assert.True(dto.IsPlatformDefault);
        Assert.Equal(NotificationTemplateStatus.Active, repository.LastStatus);
        Assert.Equal("en", repository.LastLocale);
        Assert.Equal(NotificationChannelCode.Email, repository.LastChannel);
        Assert.Equal("tenant.invite.email", repository.LastTemplateKey);
    }

    [Fact]
    public async Task TemplateList_unknown_status_returns_400()
    {
        var handler = new GetNotificationTemplateListHandler(new RecordingTemplateRepository());

        var response = await handler.Handle(
            new GetNotificationTemplateListQuery(null, true, Status: "published"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    // --- Tenant messaging settings list ---

    [Fact]
    public async Task SettingsList_returns_only_tenant_scoped_settings()
    {
        var repository = new RecordingSettingsRepository();
        repository.Items.Add(new TenantMessagingSettings
        {
            TenantId = TenantA,
            IsPlatformDefault = false,
            ProviderCode = MessagingProviderCode.Smtp,
            SenderEmail = "noreply@tenant-a.example",
            FallbackPolicy = NotificationFallbackPolicy.UsePlatformDefault,
            IsEnabled = true
        });
        repository.Items.Add(new TenantMessagingSettings
        {
            TenantId = null,
            IsPlatformDefault = true,
            ProviderCode = MessagingProviderCode.Fake,
            SenderEmail = "noreply@platform.example",
            FallbackPolicy = NotificationFallbackPolicy.UsePlatformDefault,
            IsEnabled = true
        });
        var handler = new GetTenantMessagingSettingsListHandler(repository);

        var response = await handler.Handle(new GetTenantMessagingSettingsListQuery(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var dto = Assert.Single(response.Data!);
        Assert.Equal(TenantA, dto.TenantId);
        Assert.False(dto.IsPlatformDefault);
    }

    // --- Recording fakes ---

    private sealed class RecordingDispatchRepository : INotificationDispatchRepository
    {
        public bool ListCalled { get; private set; }
        public Guid LastTenantId { get; private set; }
        public int LastSkip { get; private set; }
        public int LastTake { get; private set; }
        public NotificationDispatchStatus? LastStatus { get; private set; }
        public DateTimeOffset? LastQueuedFrom { get; private set; }
        public DateTimeOffset? LastQueuedTo { get; private set; }
        public string? LastTemplateKey { get; private set; }

        public Task<NotificationDispatch> CreateAsync(NotificationDispatch dispatch, CancellationToken ct = default) =>
            Task.FromResult(dispatch);

        public Task<NotificationDispatch?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult<NotificationDispatch?>(null);

        public Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(
            Guid tenantId,
            int skip = 0,
            int take = 50,
            NotificationDispatchStatus? status = null,
            DateTimeOffset? queuedFrom = null,
            DateTimeOffset? queuedTo = null,
            string? templateKey = null,
            CancellationToken ct = default)
        {
            ListCalled = true;
            LastTenantId = tenantId;
            LastSkip = skip;
            LastTake = take;
            LastStatus = status;
            LastQueuedFrom = queuedFrom;
            LastQueuedTo = queuedTo;
            LastTemplateKey = templateKey;
            return Task.FromResult<IReadOnlyList<NotificationDispatch>>([]);
        }

        public Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<NotificationDispatchRetryHandle>> FindDueRetriesAsync(DateTimeOffset asOfUtc, int maxRetryCount, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationDispatchRetryHandle>>([]);
    }

    private sealed class RecordingTemplateRepository : INotificationTemplateRepository
    {
        public List<NotificationTemplate> Items { get; } = [];
        public NotificationTemplateStatus? LastStatus { get; private set; }
        public string? LastLocale { get; private set; }
        public NotificationChannelCode? LastChannel { get; private set; }
        public string? LastTemplateKey { get; private set; }

        public Task<NotificationTemplate> CreateAsync(NotificationTemplate template, CancellationToken ct = default)
        {
            Items.Add(template);
            return Task.FromResult(template);
        }

        public Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => !x.IsDeleted && x.Id == id));

        public Task<IReadOnlyList<NotificationTemplate>> ListAsync(
            Guid? tenantId,
            bool isPlatformDefault,
            NotificationTemplateStatus? status = null,
            string? locale = null,
            NotificationChannelCode? channel = null,
            string? templateKey = null,
            int skip = 0,
            int take = 50,
            CancellationToken ct = default)
        {
            LastStatus = status;
            LastLocale = locale;
            LastChannel = channel;
            LastTemplateKey = templateKey;
            return Task.FromResult<IReadOnlyList<NotificationTemplate>>(Items
                .Where(x => !x.IsDeleted
                    && x.TenantId == tenantId
                    && x.IsPlatformDefault == isPlatformDefault
                    && (status is null || x.Status == status)
                    && (locale is null || x.Locale == locale)
                    && (channel is null || x.Channel == channel)
                    && (templateKey is null || x.TemplateKey == templateKey))
                .Skip(skip).Take(take).ToArray());
        }

        public Task<NotificationTemplate?> GetActiveByKeyAsync(
            Guid? tenantId,
            bool isPlatformDefault,
            string templateKey,
            string locale,
            NotificationChannelCode channel,
            CancellationToken ct = default) =>
            Task.FromResult<NotificationTemplate?>(null);

        public Task<NotificationTemplate?> GetBestActiveByKeyAsync(
            Guid tenantId,
            string templateKey,
            string locale,
            NotificationChannelCode channel,
            CancellationToken ct = default) =>
            Task.FromResult<NotificationTemplate?>(null);

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

    private sealed class RecordingSettingsRepository : ITenantMessagingSettingsRepository
    {
        public List<TenantMessagingSettings> Items { get; } = [];

        public Task<TenantMessagingSettings> CreateAsync(TenantMessagingSettings settings, CancellationToken ct = default)
        {
            Items.Add(settings);
            return Task.FromResult(settings);
        }

        public Task<TenantMessagingSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => !x.IsDeleted && !x.IsPlatformDefault && x.TenantId == tenantId));

        public Task<TenantMessagingSettings?> GetPlatformDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => !x.IsDeleted && x.IsPlatformDefault && x.TenantId is null));

        public Task<TenantMessagingSettings?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => !x.IsDeleted && x.TenantId == tenantId && x.Id == id));

        public Task<TenantMessagingSettings?> GetPlatformDefaultByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => !x.IsDeleted && x.IsPlatformDefault && x.Id == id));

        public Task<IReadOnlyList<TenantMessagingSettings>> ListTenantSettingsAsync(int skip = 0, int take = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantMessagingSettings>>(Items
                .Where(x => !x.IsDeleted && !x.IsPlatformDefault)
                .Skip(skip).Take(take).ToArray());

        public Task UpdateAsync(TenantMessagingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task SoftDeleteTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SoftDeletePlatformDefaultAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
