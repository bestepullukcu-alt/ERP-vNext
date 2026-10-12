using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.SelfRegistration;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T3-07 — the REAL line, end to end, in a disposable mongod: the production template seed, the production
/// manifest reconciled into the event catalog by the production sync, the time-entry notifiers, the production
/// <see cref="Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers.DispatchNotificationByEventCodeHandler"/>
/// behind MediatR with the validation behaviour, the production adapter, locale resolver (the tenant says <c>tr</c>),
/// queue handler, renderer and dispatch store. Only the provider is a double: it records the e-mail it was handed.
///
/// <para>This is the test that catches "event not active", a hyphenated event code, a lower-case variable name or a
/// template placeholder nobody sends: each of them ends in no e-mail, or an e-mail with a <c>{{…}}</c> in it.</para>
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimeEntryNotificationPipelineMongoTests : IAsyncLifetime
{
    private readonly TimeEntryMongoFixture _fixture;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _person = Guid.NewGuid();
    private readonly Guid _approver = Guid.NewGuid();
    private readonly RecordingProvider _provider = new();
    private ServiceProvider _services = null!;

    public TimeEntryNotificationPipelineMongoTests(TimeEntryMongoFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_fixture.DbContext);
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantRegistryRepository, TenantRegistryRepository>();
        services.AddScoped<INotificationEventDefinitionRepository, NotificationEventDefinitionRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationDispatchRepository, NotificationDispatchRepository>();
        services.AddScoped<ITenantMessagingSettingsRepository, TenantMessagingSettingsRepository>();
        services.AddScoped<ITenantMessagingSettingsResolver, TenantMessagingSettingsResolver>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddScoped<INotificationLocaleResolver, TenantNotificationLocaleResolver>();
        services.AddScoped<INotificationEventDispatchAdapter, NotificationEventDispatchAdapter>();
        services.AddSingleton<IMessagingProviderResolver>(new SingleProviderResolver(_provider));
        services.AddSingleton<IEventBus, NullEventBus>();
        services.AddScoped<ITimeEntryNotificationMarkRepository, TimeEntryNotificationMarkRepository>();
        var application = typeof(TimeEntryPermissions).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(application);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(application);
        _services = services.BuildServiceProvider();

        await _fixture.Database.GetCollection<Tenant>(PlatformCollections.Tenants).InsertOneAsync(new Tenant
        {
            Id = _tenant, Code = "T" + _tenant.ToString("N")[..8], Slug = "t-" + _tenant.ToString("N")[..8], Name = "Pipeline",
            DisplayName = "Pipeline", Domain = _tenant.ToString("N")[..8] + ".example", Country = "TR",
            DefaultTimezone = "Europe/Istanbul", DefaultLanguage = "en",
            Settings = new TenantSettings { Timezone = "Europe/Istanbul", Language = "tr" }
        });
        await _fixture.Database.GetCollection<TenantMessagingSettings>(PlatformCollections.TenantMessagingSettings).InsertOneAsync(
            new TenantMessagingSettings
            {
                TenantId = _tenant, IsPlatformDefault = false, ProviderCode = MessagingProviderCode.Fake,
                SenderEmail = "noreply@pipeline.test", SenderName = "Pipeline", IsEnabled = true,
                FallbackPolicy = NotificationFallbackPolicy.FailFast
            });

        // The production seed and the production reconcile of the production manifest.
        await NotificationTemplateSeed.EnsureSeededAsync(_fixture.Database);
        using var scope = _services.CreateScope();
        var sync = new NotificationEventManifestSyncService(
            [new TimeEntryManifestProvider()], scope.ServiceProvider.GetRequiredService<INotificationEventDefinitionRepository>());
        await sync.SyncAsync();
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    [Fact]
    public async Task T3_07_the_manifest_sync_makes_all_seven_events_Active_with_no_validation_issue()
    {
        using var scope = _services.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<INotificationEventDefinitionRepository>();
        foreach (var code in TimeEntryNotificationEvents.All)
        {
            var definition = await events.GetByEventCodeAsync(code);
            Assert.True(definition is not null, $"{code} is not in the event catalog");
            Assert.Equal(NotificationEventStatus.Active, definition!.Status);
            Assert.Equal(code, definition.DefaultTemplateKey);
        }
    }

    [Fact]
    public async Task T3_07_auto_closed_and_reminder_render_a_Turkish_email_with_no_placeholder_left()
    {
        await InTenantAsync(async sp =>
        {
            await AutoCloseNotifier(sp).NotifyAsync(new TimerSegment
            {
                TenantId = _tenant, UserId = _person, CategoryCode = "ADMINISTRATION", LocalDate = new DateOnly(2026, 10, 6),
                WeekKey = "2026-W41", TimeZoneId = "Europe/Istanbul", DurationSeconds = 17 * 3600,
                StartSource = TimerStartSource.TimerControl
            });
            Assert.True(await Notifier(sp).WeekReminderAsync(_person, new DateOnly(2026, 10, 5)));
        });

        var autoClosed = Assert.Single(_provider.Sent, m => m.Subject.StartsWith("Sayacınız gece yarısı durduruldu", StringComparison.Ordinal));
        Assert.Contains("2026-10-06", autoClosed.Subject);
        Assert.Contains("1020 dakika", autoClosed.BodyHtml);
        Assert.Contains("https://web.test/TimeEntry?week=2026-W41", autoClosed.BodyText);

        var reminder = Assert.Single(_provider.Sent, m => m.Subject.StartsWith("Hatırlatma:", StringComparison.Ordinal));
        Assert.Contains("2026-W41 (2026-10-05 – 2026-10-11)", reminder.Subject);

        AssertFullyRendered(_provider.Sent);
        var dispatches = await Dispatches();
        Assert.Equal(2, dispatches.Count);
        Assert.All(dispatches, d =>
        {
            Assert.Equal(NotificationDispatchStatus.Sent, d.Status);
            Assert.Equal("tr", d.Locale);
            Assert.StartsWith("timeentry.", d.TemplateKey, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task T3_07_every_time_entry_event_reaches_the_provider_through_the_real_line()
    {
        var week = new TimesheetWeek
        {
            TenantId = _tenant, UserId = _person, WeekKey = "2026-W41", WeekStartDate = new DateOnly(2026, 10, 5),
            TimeZoneId = "Europe/Istanbul", RevisionNumber = 1, SubmissionCount = 1,
            ApproverCandidateUserIds = [_approver], LastRejectionReason = "Salı eksik."
        };

        await InTenantAsync(async sp =>
        {
            var notifier = Notifier(sp);
            await notifier.WeekSubmittedAsync(week);
            await notifier.WeekWithdrawnAsync(week);
            await notifier.WeekRejectedAsync(week);
            await notifier.WeekApprovedAsync(week);
            await notifier.WeekReminderAsync(_person, week.WeekStartDate);
            await notifier.MinutesConflictAsync(_person, Guid.NewGuid(), "Haftalık toplantı", new DateOnly(2026, 10, 6), AttendanceStatus.Absent);
            await AutoCloseNotifier(sp).NotifyAsync(new TimerSegment
            {
                TenantId = _tenant, UserId = _person, CategoryCode = "ADMINISTRATION", LocalDate = new DateOnly(2026, 10, 6),
                WeekKey = "2026-W41", TimeZoneId = "Europe/Istanbul", DurationSeconds = 3600, StartSource = TimerStartSource.TimerControl
            });
        });

        var dispatches = await Dispatches();
        Assert.Equal(
            TimeEntryNotificationEvents.All.OrderBy(c => c, StringComparer.Ordinal),
            dispatches.Select(d => d.TemplateKey).OrderBy(c => c, StringComparer.Ordinal));
        Assert.All(dispatches, d => Assert.Equal(NotificationDispatchStatus.Sent, d.Status));
        Assert.Equal(7, _provider.Sent.Count);
        AssertFullyRendered(_provider.Sent);

        // Approver e-mails go to the approver and name the person; the rest go to the person alone.
        var approverMail = TestRecipients.Address(_approver);
        Assert.Equal(2, _provider.Sent.Count(m => m.To.Single().Email == approverMail));
        Assert.Contains(_provider.Sent, m => m.BodyText.Contains("Salı eksik.", StringComparison.Ordinal));
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────────────

    private static void AssertFullyRendered(IEnumerable<SentMail> mails)
        => Assert.All(mails, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Subject));
            Assert.False(string.IsNullOrWhiteSpace(m.BodyHtml));
            Assert.False(string.IsNullOrWhiteSpace(m.BodyText));
            Assert.DoesNotContain("{{", m.Subject + m.BodyHtml + m.BodyText);
            Assert.DoesNotContain("}}", m.Subject + m.BodyHtml + m.BodyText);
        });

    private async Task InTenantAsync(Func<IServiceProvider, Task> act)
    {
        using var scope = _services.CreateScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), _tenant))
        {
            await act(scope.ServiceProvider);
        }
    }

    /// <summary>The notifier's door into the line is the MediatR command, so the real handler sits on the path.</summary>
    private static INotificationEventDispatchAdapter ThroughHandler(IServiceProvider sp) => new MediatorDispatch(sp.GetRequiredService<IMediator>());

    private static ITimeEntryLinks Links() => new TimeEntryLinks(Options.Create(new AuthServiceOptions { FrontendBaseUrl = "https://web.test" }));

    private TimerAutoCloseNotifier AutoCloseNotifier(IServiceProvider sp)
        => new(ThroughHandler(sp), new TestRecipients(), Links(), NullLogger<TimerAutoCloseNotifier>.Instance);

    private TimeEntryNotifier Notifier(IServiceProvider sp)
    {
        var names = new TestDisplayNames();
        names.Names[_person] = "Ayşe Yılmaz";
        return new TimeEntryNotifier(
            ThroughHandler(sp), new TestRecipients(), sp.GetRequiredService<ITimeEntryNotificationMarkRepository>(), names, Links(),
            sp.GetRequiredService<ITenantContext>(), TimeProvider.System, NullLogger<TimeEntryNotifier>.Instance);
    }

    private Task<List<NotificationDispatch>> Dispatches()
        => _fixture.Database.GetCollection<NotificationDispatch>(PlatformCollections.NotificationDispatches)
            .Find(d => d.TenantId == _tenant).ToListAsync();

    private sealed class MediatorDispatch(IMediator mediator) : INotificationEventDispatchAdapter
    {
        public Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
            NotificationEventDispatchRequest request, CancellationToken ct = default)
            => mediator.Send(new DispatchNotificationByEventCodeCommand(request), ct);
    }

    public sealed record SentMail(string Subject, string BodyHtml, string BodyText, IReadOnlyList<EmailRecipientDto> To);

    private sealed class RecordingProvider : IMessagingProvider
    {
        private readonly List<SentMail> _sent = [];

        public IReadOnlyList<SentMail> Sent
        {
            get { lock (_sent) { return _sent.ToList(); } }
        }

        public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;

        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default)
        {
            lock (_sent)
            {
                _sent.Add(new SentMail(request.Subject, request.BodyHtml ?? string.Empty, request.BodyText ?? string.Empty, request.To));
            }

            return Task.FromResult(MessagingProviderResult.Success("pipeline-" + request.DispatchId.ToString("N")));
        }
    }

    private sealed class SingleProviderResolver(IMessagingProvider provider) : IMessagingProviderResolver
    {
        public Response<IMessagingProvider> Resolve(MessagingProviderCode providerCode)
            => providerCode == provider.ProviderCode
                ? Response<IMessagingProvider>.Success(provider)
                : Response<IMessagingProvider>.Fail($"{providerCode} provider unavailable.", 400);
    }

    private sealed class NullEventBus : IEventBus
    {
        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent => PublishAsync(@event, new EventPublishOptions(), cancellationToken);

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, EventPublishOptions options, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
            => Task.FromResult(new EventEnvelope<TEvent>(new EventMetadata(
                Guid.NewGuid(), @event.EventName, @event.EventVersion, Guid.NewGuid(), options.CausationId, options.TenantId,
                "test", DateTimeOffset.UtcNow), @event));
    }
}
