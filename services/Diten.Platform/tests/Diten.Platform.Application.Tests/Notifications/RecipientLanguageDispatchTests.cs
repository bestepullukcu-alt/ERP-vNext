using System.Net;
using System.Text;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-EMAIL-LANG-01 — every person is written to in their own language: the recipient's preference, else the tenant's
/// language, else en. The event-code adapter groups recipients by language, one dispatch per group.
/// </summary>
public sealed class RecipientLanguageDispatchTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private const string TenantLanguage = "tr";

    [Fact]
    public async Task Mixed_language_recipients_get_one_dispatch_per_language_and_each_recipient_exactly_once()
    {
        var (adapter, queue) = Build();

        var result = await adapter.DispatchByEventCodeAsync(Request(
            new EmailRecipientDto("a@acme.test", "A", "fr"),
            new EmailRecipientDto("b@acme.test", "B"),
            new EmailRecipientDto("c@acme.test", "C", "fr"),
            new EmailRecipientDto("d@acme.test", "D", "ru"),
            new EmailRecipientDto("e@acme.test", "E", null)));

        Assert.True(result.IsSuccessful);
        var byLocale = queue.Commands.ToDictionary(c => c.Request.Locale, c => c.Request.To.Select(r => r.Email).ToList());
        Assert.Equal(3, queue.Commands.Count);
        Assert.Equal(["a@acme.test", "c@acme.test"], byLocale["fr"]);
        Assert.Equal(["b@acme.test", "e@acme.test"], byLocale[TenantLanguage]); // no preference: the tenant's language
        Assert.Equal(["d@acme.test"], byLocale["ru"]);
        var everyone = queue.Commands.SelectMany(c => c.Request.To).Select(r => r.Email).ToList();
        Assert.Equal(everyone.Distinct().Count(), everyone.Count); // nobody twice
        Assert.Equal(5, everyone.Count);                            // nobody dropped
        Assert.All(queue.Commands.SelectMany(c => c.Request.To), r => Assert.Null(r.Locale)); // not forwarded below
    }

    [Fact]
    public async Task A_recipient_without_a_language_is_written_to_in_the_tenants_language()
    {
        var (adapter, queue) = Build();

        await adapter.DispatchByEventCodeAsync(Request(new EmailRecipientDto("solo@acme.test", "Solo")));

        var command = Assert.Single(queue.Commands);
        Assert.Equal(TenantLanguage, command.Request.Locale);
        Assert.Equal("solo@acme.test", Assert.Single(command.Request.To).Email);
    }

    [Theory]
    [InlineData("fr", "fr")]
    [InlineData("FR-ca", "fr")]  // a regional form reads as its language
    [InlineData("de", TenantLanguage)] // outside the seven: the tenant's language, never sent as is
    public async Task A_recipients_own_language_outranks_the_tenants(string preferred, string expected)
    {
        var (adapter, queue) = Build();

        await adapter.DispatchByEventCodeAsync(Request(new EmailRecipientDto("one@acme.test", "One", preferred)));

        Assert.Equal(expected, Assert.Single(queue.Commands).Request.Locale);
    }

    [Fact]
    public async Task Cc_and_Bcc_travel_once_with_the_first_group_only()
    {
        var (adapter, queue) = Build();
        var request = Request(new EmailRecipientDto("a@acme.test", "A", "fr"), new EmailRecipientDto("b@acme.test", "B", "es")) with
        {
            Cc = [new EmailRecipientDto("cc@acme.test", "Cc")],
            Bcc = [new EmailRecipientDto("bcc@acme.test", "Bcc")]
        };

        await adapter.DispatchByEventCodeAsync(request);

        Assert.Equal(2, queue.Commands.Count);
        Assert.Single(queue.Commands, c => c.Request.Cc is { Count: 1 });
        Assert.Single(queue.Commands, c => c.Request.Bcc is { Count: 1 });
        Assert.Same(queue.Commands[0], queue.Commands.Single(c => c.Request.Cc is { Count: 1 }));
    }

    [Fact]
    public async Task Each_language_group_is_its_own_dispatch_holding_only_its_own_recipients()
    {
        // A dispatch is retried by its own id (EmailDispatchJob → the row's To): so a group's retry can only ever resend
        // to that group. What makes that true is that no group's row carries another group's recipients.
        var (adapter, queue) = Build();

        await adapter.DispatchByEventCodeAsync(Request(
            new EmailRecipientDto("a@acme.test", "A", "fr"),
            new EmailRecipientDto("b@acme.test", "B", "zh"),
            new EmailRecipientDto("c@acme.test", "C")));

        Assert.Equal(3, queue.DispatchIds.Distinct().Count());
        var sets = queue.Commands.Select(c => c.Request.To.Select(r => r.Email).ToHashSet()).ToList();
        for (var i = 0; i < sets.Count; i++)
        {
            for (var j = i + 1; j < sets.Count; j++)
            {
                Assert.Empty(sets[i].Intersect(sets[j]));
            }
        }
    }

    [Fact]
    public async Task A_group_that_fails_does_not_fail_the_call_and_the_other_groups_are_still_sent()
    {
        // FIX1 (R1) — the queued readers have their mail: the call SUCCEEDS and names the partial failure.
        var (adapter, queue) = Build(failLocale: "fr");

        var result = await adapter.DispatchByEventCodeAsync(Request(
            new EmailRecipientDto("a@acme.test", "A", "fr"),
            new EmailRecipientDto("b@acme.test", "B")));

        Assert.True(result.IsSuccessful);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonPartialRecipientFailure, result.ReasonCode);
        Assert.Equal(2, queue.Commands.Count);
    }

    // ---------------------------------------------------------------- the contacts client

    [Fact]
    public async Task The_contacts_client_reads_each_persons_language_and_null_from_an_older_AuthService()
    {
        var withLanguage = Guid.NewGuid();
        var withoutField = Guid.NewGuid();
        var body = "[{\"id\":\"" + withLanguage + "\",\"displayName\":\"Fay\",\"email\":\"fay@acme.test\",\"preferredLanguage\":\"fr\"},"
                   + "{\"id\":\"" + withoutField + "\",\"displayName\":\"Old\",\"email\":\"old@acme.test\"}]";
        var context = new TenantContext();
        context.SetTenant(Tenant);
        var client = new AuthTaskNotificationRecipientClient(
            new OneAnswer(body),
            Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.test", InternalApiKey = "lang-test-only-key" }),
            context,
            NullLogger<AuthTaskNotificationRecipientClient>.Instance);

        var people = await client.ResolveAsync([withLanguage, withoutField]);

        Assert.Equal("fr", Assert.Single(people, p => p.UserId == withLanguage).PreferredLanguage);
        Assert.Null(Assert.Single(people, p => p.UserId == withoutField).PreferredLanguage);
    }

    // ---------------------------------------------------------------- helpers

    private static (NotificationEventDispatchAdapter Adapter, RecordingQueue Queue) Build(string? failLocale = null)
    {
        var queue = new RecordingQueue(failLocale);
        var events = new OneEvent(new NotificationEventDefinition
        {
            EventCode = "task.assigned",
            SourceType = NotificationEventSourceType.PlatformSeed,
            OwnerModuleId = "MOD-0024",
            Channel = NotificationChannelCode.Email,
            DefaultTemplateKey = "task.assigned.email",
            FallbackDisplayName = "task.assigned",
            Status = NotificationEventStatus.Active
        });
        return (new NotificationEventDispatchAdapter(events, queue, new TenantLanguageResolver(),
            NullLogger<NotificationEventDispatchAdapter>.Instance), queue);
    }

    private static NotificationEventDispatchRequest Request(params EmailRecipientDto[] to) =>
        new(Tenant, "task.assigned", to, new Dictionary<string, object?> { ["TaskTitle"] = "Review" });

    /// <summary>The real chain's shape: what was asked for, else the tenant's language.</summary>
    private sealed class TenantLanguageResolver : INotificationLocaleResolver
    {
        public Task<string> ResolveAsync(Guid tenantId, string? requested, CancellationToken ct = default) =>
            Task.FromResult(string.IsNullOrWhiteSpace(requested) ? TenantLanguage : requested);
    }

    private sealed class RecordingQueue(string? failLocale) : IMediator
    {
        public List<QueueEmailNotificationCommand> Commands { get; } = [];
        public List<Guid> DispatchIds { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var command = (QueueEmailNotificationCommand)(object)request;
            Commands.Add(command);
            if (command.Request.Locale == failLocale)
            {
                return Task.FromResult((TResponse)(object)Response<NotificationDispatchDto>.Fail("provider down", 502, "PROVIDER_UNAVAILABLE"));
            }

            var id = Guid.NewGuid();
            DispatchIds.Add(id);
            return Task.FromResult((TResponse)(object)Response<NotificationDispatchDto>.Success(new NotificationDispatchDto(
                id, Tenant, command.Request.TemplateKey, null, command.Request.Locale, "Email", "Smtp", null, "Queued",
                command.Request.To, 0, 0, "", null, null, "{}", DateTimeOffset.UtcNow, null, null, 0, null, null, null), 202));
        }

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

    private sealed class OneAnswer(string body) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(body));

        private sealed class Handler(string body) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}

/// <summary>
/// WP-EMAIL-LANG-01 item 3 — a person can now get ANY tenant mail in their own language, so every template the platform
/// seeds must exist in all seven tenant languages: one missing language would send that reader a 404 instead of a mail.
/// </summary>
public sealed class NotificationTemplateLanguageParityTests
{
    [Fact]
    public void Every_seeded_email_template_exists_in_all_seven_tenant_languages()
    {
        var seeded = Diten.Platform.Infrastructure.Persistence.Configurations.NotificationTemplateSeed.CreatePlatformDefaults();
        var expected = Diten.Platform.Infrastructure.Persistence.Configurations.NotificationTemplateSeed.TenantLocales.OrderBy(x => x).ToArray();

        Assert.NotEmpty(seeded);
        Assert.All(seeded.GroupBy(t => t.TemplateKey), group =>
            Assert.True(expected.SequenceEqual(group.Select(t => t.Locale).OrderBy(x => x)),
                $"{group.Key}: {string.Join(",", group.Select(t => t.Locale).OrderBy(x => x))}"));
    }

    [Fact]
    public void The_dispatch_adapters_recipient_languages_are_the_seeded_languages()
    {
        // A recipient language the adapter accepts must have a template; one it refuses falls back to the tenant's.
        Assert.Equal(
            Diten.Platform.Infrastructure.Persistence.Configurations.NotificationTemplateSeed.TenantLocales.OrderBy(x => x),
            NotificationEventDispatchAdapter.RecipientLocales.OrderBy(x => x));
    }
}
