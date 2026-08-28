using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Services.Notifications;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// WC-4 — <b>does a task notification actually produce a dispatch row?</b> Against a REAL MongoDB, with the REAL
/// seeds and the REAL repositories.
///
/// <para><b>Why this file exists.</b> WC-4 shipped twice and sent nothing twice, with the suite green both times,
/// because every notification test stopped at a double. The second failure is the sharper lesson:
/// <c>QueueEmailNotificationHandler</c> asks <c>ITenantMessagingSettingsResolver</c> for settings on its FIRST
/// line, no platform-default row had ever been created by anything, and it refused with no reason code — before
/// templates, locale, provider or recipients were consulted. Every existing test substituted the resolver or the
/// whole handler, so the one thing that mattered — that the product SEEDS what it needs to send mail — was never
/// asserted anywhere. The live database confirmed it: 41 templates, 8 event definitions, 0 messaging settings,
/// 0 dispatches.</para>
///
/// <para><b>What is real.</b> Both seeds, all four Mongo repositories, the settings resolver, the renderer, the
/// provider resolver and the queue handler. The provider is <c>FakeMessagingProvider</c> — a test must not open an
/// SMTP socket, and the provider is not what breaks. The assertion is the dispatch ROW, which is written before
/// any provider is called and is exactly what stayed missing in production.</para>
/// </summary>
public sealed class NotificationDispatchMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await MongoIntegrationHarness.CreateIsolatedAsync(
        "notification_dispatch",
        SchemaProfile.Notification,
        SchemaProfile.WorkflowWorkCenter,
        SchemaProfile.Core);

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task A_task_notification_writes_a_dispatch_row_on_a_freshly_seeded_database()
    {
        /*
         * The whole defect in one assertion, and it is RED without the messaging-settings seed: the handler
         * refuses at its first line, returns 400, and notification_dispatches stays empty — which is
         * indistinguishable from "nothing was ever attempted", which is what made this take two rounds.
         */
        await SeedAsync();

        var response = await HandleAsync(TaskNotificationEvents.Assigned, "en");

        Assert.True(
            response.IsSuccessful,
            $"Dispatch refused: {response.ReasonCode ?? "<no reason code>"} — {string.Join(" | ", response.Errors)}");

        var dispatches = await Collection<NotificationDispatch>("notification_dispatches")
            .Find(FilterDefinition<NotificationDispatch>.Empty).ToListAsync();

        var dispatch = Assert.Single(dispatches);
        Assert.Equal(TaskNotificationEvents.Assigned, dispatch.TemplateKey);
        Assert.Equal("en", dispatch.Locale);
        Assert.Equal(_harness.TenantId, dispatch.TenantId);
        Assert.NotEqual(Guid.Empty, dispatch.TemplateId);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    [InlineData("ar")]
    [InlineData("ru")]
    public async Task Every_seeded_language_resolves_a_real_template_and_writes_a_row_in_THAT_language(string locale)
    {
        // The seven languages, proven end to end against the real lookup rather than against the seed's own list.
        // A template that exists in the seed but that GetBestActiveByKeyAsync cannot find is the same silent 404.
        await SeedAsync();

        var response = await HandleAsync(TaskNotificationEvents.Assigned, locale);

        Assert.True(response.IsSuccessful, $"'{locale}' refused: {response.ReasonCode} — {string.Join(" | ", response.Errors)}");

        var dispatch = Assert.Single(await Collection<NotificationDispatch>("notification_dispatches")
            .Find(FilterDefinition<NotificationDispatch>.Empty).ToListAsync());
        Assert.Equal(locale, dispatch.Locale);
    }

    [Fact]
    public async Task With_NO_messaging_settings_the_refusal_names_itself_instead_of_going_quiet()
    {
        /*
         * The exact production state, reproduced: templates seeded, settings not. This is the test whose absence
         * cost a round — and it asserts the REASON CODE, not merely the failure, because "it failed" was already
         * observable and told nobody anything. ReasonCode=null is the defect; MESSAGING_SETTINGS_UNAVAILABLE is
         * the fix.
         */
        await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database);   // templates only — no settings seed

        var response = await HandleAsync(TaskNotificationEvents.Assigned, "en");

        Assert.False(response.IsSuccessful);
        Assert.Equal(QueueEmailNotificationHandler.ReasonMessagingSettingsUnavailable, response.ReasonCode);
        Assert.Empty(await Collection<NotificationDispatch>("notification_dispatches")
            .Find(FilterDefinition<NotificationDispatch>.Empty).ToListAsync());
    }

    [Fact]
    public async Task A_locale_nobody_seeded_is_REFUSED_with_a_named_reason_never_silently()
    {
        /*
         * The gap left open by the previous round, now closed as a stated behaviour rather than an accident. A
         * tenant configured in a language the platform never seeded (say German) gets NO template — and what
         * matters is that this is a named, greppable refusal rather than an empty inbox with an empty log.
         *
         * Refusing is the right answer over silently substituting English: a notification arriving in a language
         * the tenant did not choose is a support ticket, whereas TEMPLATE_NOT_FOUND next to the locale in the log
         * tells an operator exactly which template to author. The adapter's dispatch_failed line carries both.
         */
        await SeedAsync();

        var response = await HandleAsync(TaskNotificationEvents.Assigned, "de");

        Assert.False(response.IsSuccessful);
        Assert.Equal(QueueEmailNotificationHandler.ReasonTemplateNotFound, response.ReasonCode);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task A_regional_locale_narrows_to_its_language_rather_than_falling_off_the_edge()
    {
        // tr-TR is a locale a tenant can plausibly be configured with, and no template is seeded under it.
        // GetBestActiveByKeyAsync's neutral-locale step is what saves it — proven here against the real query,
        // because the previous round could only assert it from reading the repository source.
        await SeedAsync();

        var response = await HandleAsync(TaskNotificationEvents.Assigned, "tr-TR");

        Assert.True(response.IsSuccessful, $"tr-TR refused: {response.ReasonCode}");
        Assert.Equal("tr", Assert.Single(await Collection<NotificationDispatch>("notification_dispatches")
            .Find(FilterDefinition<NotificationDispatch>.Empty).ToListAsync()).Locale);
    }

    [Fact]
    public async Task The_seed_never_overwrites_settings_an_operator_already_configured()
    {
        /*
         * Idempotency is not the point here — clobbering is. This seed runs on EVERY startup; if it replaced the
         * platform default, a tenant's production SMTP configuration would silently revert to whatever the
         * appsettings of that particular host happened to say, on every deploy.
         */
        var settings = Collection<TenantMessagingSettings>("notification_tenant_messaging_settings");
        await settings.InsertOneAsync(new TenantMessagingSettings
        {
            TenantId = null,
            IsPlatformDefault = true,
            ProviderCode = MessagingProviderCode.Fake,
            SenderEmail = "operator-chose-this@diten.com",
            IsEnabled = true
        });

        await NotificationMessagingSettingsSeed.EnsureSeededAsync(_harness.Database, DevSmtp());
        await NotificationMessagingSettingsSeed.EnsureSeededAsync(_harness.Database, DevSmtp());

        var rows = await settings.Find(FilterDefinition<TenantMessagingSettings>.Empty).ToListAsync();
        Assert.Equal("operator-chose-this@diten.com", Assert.Single(rows).SenderEmail);
    }

    [Fact]
    public async Task The_seeded_row_is_the_one_the_RESOLVER_looks_for()
    {
        /*
         * Non-vacuity for every test above, and the failure mode that would make the seed worthless: a row that
         * exists but that GetPlatformDefaultAsync's filter does not match is the same as no row at all. Asserted
         * through the real repository rather than by re-reading the fields.
         */
        await NotificationMessagingSettingsSeed.EnsureSeededAsync(_harness.Database, DevSmtp());

        var resolved = await new TenantMessagingSettingsResolver(
                new TenantMessagingSettingsRepository(_harness.DbContext))
            .ResolveAsync(_harness.TenantId);

        Assert.True(resolved.IsSuccessful);
        Assert.True(resolved.Data!.IsPlatformDefault);
        Assert.Equal(MessagingProviderCode.Smtp.ToString(), resolved.Data.ProviderCode);
    }

    [Fact]
    public void Startup_actually_CALLS_the_messaging_settings_seed()
    {
        /*
         * The gap every test above shares: they all call the seed themselves, so they prove it WORKS while saying
         * nothing about whether the product ever RUNS it. Deleting the call from DependencyInjection leaves all of
         * them green and ships the original defect back — a seed nobody invokes is identical to no seed.
         *
         * Reading the source is the cheap honest option here. Booting the real container needs a Mongo connection,
         * a configuration tree and every hosted service this file has no business starting; a source assertion
         * costs nothing and fails loudly on the one edit that matters.
         */
        var source = File.ReadAllText(DependencyInjectionSourcePath());

        var callSites = System.Text.RegularExpressions.Regex
            .Matches(source, @"NotificationMessagingSettingsSeed\s*\.?\s*EnsureSeededAsync")
            .Count;

        Assert.True(
            callSites >= 2,
            $"Expected the seed to be invoked at BOTH Mongo startup paths, found {callSites}. "
            + "NotificationTemplateSeed is called from both for the same reason: whichever path a host takes, the "
            + "notification tables have to be usable.");
    }

    /// <summary>Walks up from the test binary to the repository copy of the file, so this reads source not output.</summary>
    private static string DependencyInjectionSourcePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.Name != "Diten.Platform")
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "src", "Diten.Platform.Infrastructure", "DependencyInjection.cs");
        Assert.True(File.Exists(path), $"Could not locate DependencyInjection.cs at {path}");
        return path;
    }

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>Exactly what startup does, in the same order.</summary>
    private async Task SeedAsync()
    {
        await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database);
        await NotificationMessagingSettingsSeed.EnsureSeededAsync(_harness.Database, DevSmtp());
    }

    /// <summary>The dev Smtp block, as appsettings.Development.json actually carries it.</summary>
    private static SmtpOptions DevSmtp() => new()
    {
        Host = "localhost",
        Port = 1025,
        Enabled = true,
        EnableSsl = false,
        FromEmail = "no-reply@yourcompany.com",
        FromName = "Diten PPM"
    };

    private IMongoCollection<T> Collection<T>(string name) => _harness.Database.GetCollection<T>(name);

    /// <summary>The real handler over the real repositories. Only the provider and the bus are substituted.</summary>
    private async Task<Diten.Platform.Application.Common.Response<NotificationDispatchDto>> HandleAsync(
        string templateKey, string locale)
    {
        var handler = new QueueEmailNotificationHandler(
            new TenantMessagingSettingsResolver(new TenantMessagingSettingsRepository(_harness.DbContext)),
            new NotificationTemplateRepository(_harness.DbContext),
            new EmailTemplateRenderer(),
            new NotificationDispatchRepository(_harness.DbContext),
            new MessagingProviderResolver([
                new FakeMessagingProvider(Options.Create(new FakeMessagingProviderOptions()), new FakeHostEnvironment()),
                new StubSmtpProvider()]),
            new NoOpEventBus(),
            NullLogger<QueueEmailNotificationHandler>.Instance);

        return await handler.Handle(
            new QueueEmailNotificationCommand(
                _harness.TenantId,
                new QueueEmailNotificationRequest(
                    TemplateKey: templateKey,
                    Locale: locale,
                    Variables: new Dictionary<string, object?>
                    {
                        ["TaskTitle"] = "Gerçek depoya yazılan görev",
                        ["TaskId"] = Guid.NewGuid().ToString()
                    },
                    To: [new EmailRecipientDto("ayse@diten.com", "Ayşe")])),
            CancellationToken.None);
    }

    /// <summary>
    /// Stands in for <c>SmtpMessagingProvider</c> under its own provider code, without opening a socket.
    ///
    /// <para>It has to be REGISTERED rather than swapped for Fake, because the seed deliberately chooses
    /// <c>MessagingProviderCode.Smtp</c> when SMTP is configured, and <c>MessagingProviderResolver</c> refuses a
    /// code nothing implements — BEFORE the dispatch row is written. Seeding a provider code the runtime cannot
    /// resolve would be a fix that swaps one silent failure for another, so this test keeps the seed's real choice
    /// under test. Production registers the concrete SmtpMessagingProvider for this code unconditionally.</para>
    /// </summary>
    private sealed class StubSmtpProvider : IMessagingProvider
    {
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Smtp;

        public Task<MessagingProviderResult> SendEmailAsync(
            MessagingProviderEmailRequest request, CancellationToken ct = default)
            => Task.FromResult(MessagingProviderResult.Success($"stub-{request.DispatchId:N}"));
    }

    private sealed class NoOpEventBus : IEventBus
    {
        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : IIntegrationEvent => PublishAsync(@event, new EventPublishOptions(), ct);

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(
            TEvent @event, EventPublishOptions options, CancellationToken ct = default)
            where TEvent : IIntegrationEvent
            => Task.FromResult(new EventEnvelope<TEvent>(
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

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Diten.Platform.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
