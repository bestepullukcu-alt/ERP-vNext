using System.Net;
using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-VW-W5 W5-FIX2 — what needs no database: the idempotency key's shape at the HTTP edge, that the internal dispatch
/// asks the queue for a background send, and the subject rule (F-4) on every CRM visit template in every language. The
/// key's real behaviour (one dispatch, concurrency) is measured on MongoDB in <see cref="W5Fix2InternalDispatchMongoTests"/>.
/// </summary>
public sealed class W5Fix2InternalDispatchTests
{
    [Theory]
    [InlineData(129)]
    [InlineData(0)]
    public async Task An_idempotency_key_that_is_empty_or_longer_than_128_characters_is_refused(int length)
    {
        using var host = new InternalNotificationEventDispatchTests.Host();
        var body = InternalNotificationEventDispatchTests.Body(to: [new InternalEventRecipient(Email: "pv@example.test")]) with
        {
            IdempotencyKey = new string('k', length)
        };

        var response = await host.PostAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(host.Adapter.Requests);
    }

    [Fact]
    public async Task The_internal_dispatch_asks_for_a_background_send_and_a_128_character_key_is_accepted()
    {
        using var host = new InternalNotificationEventDispatchTests.Host();
        var body = InternalNotificationEventDispatchTests.Body(to: [new InternalEventRecipient(Email: "pv@example.test")]) with
        {
            IdempotencyKey = new string('k', InternalEventDispatchService.MaxIdempotencyKeyLength)
        };

        var response = await host.PostAsync(body);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.True(Assert.Single(host.Adapter.Requests).QueueOnly);
    }

    [Fact]
    public async Task Over_http_the_same_key_twice_reaches_the_adapter_once_and_a_key_is_scoped_by_the_tenant()
    {
        using var host = new InternalNotificationEventDispatchTests.Host();
        var body = InternalNotificationEventDispatchTests.Body(to: [new InternalEventRecipient(Email: "pv@example.test")]) with
        {
            IdempotencyKey = "row-1"
        };

        var first = await host.ReadAsync(await host.PostAsync(body));
        var second = await host.ReadAsync(await host.PostAsync(body));
        Assert.Single(host.Adapter.Requests);
        Assert.Equal(first.Dispatches.Single().DispatchId, second.Dispatches.Single().DispatchId);

        // The same key in another tenant is another request.
        await host.ReadAsync(await host.PostAsync(body with { TenantId = InternalNotificationEventDispatchTests.TenantB }));
        Assert.Equal(2, host.Adapter.Requests.Count);
    }

    public static IEnumerable<object[]> EveryVisitTemplate() =>
        NotificationTemplateSeed.CreatePlatformDefaults()
            .Where(t => t.TemplateKey.StartsWith("crm.visitreport.", StringComparison.Ordinal))
            .Select(t => new object[] { t.TemplateKey, t.Locale });

    [Theory]
    [MemberData(nameof(EveryVisitTemplate))]
    public void F4_an_empty_subject_part_is_left_out_with_its_separator_in_every_visit_template(string key, string locale)
    {
        var template = NotificationTemplateSeed.CreatePlatformDefaults().Single(t => t.TemplateKey == key && t.Locale == locale);
        var empty = Names(template).ToDictionary(n => n, _ => (object?)string.Empty, StringComparer.OrdinalIgnoreCase);
        empty["repName"] = "Ayşe Demir";
        empty["visitDate"] = "2026-10-20";
        empty["reportUrl"] = "https://crm.test/r";
        empty["deadline"] = "2026-10-21T10:00:00Z";

        var rendered = new EmailTemplateRenderer().Render(template, empty);

        Assert.True(rendered.IsSuccessful, string.Join("; ", rendered.Errors));
        var subject = rendered.Data!.Subject;
        Assert.DoesNotContain("·  ·", subject);
        Assert.DoesNotContain("· ·", subject);
        Assert.False(subject.TrimEnd().EndsWith('·'), subject);
        Assert.False(subject.TrimStart().StartsWith('·'), subject);
        // Nothing else is lost: every non-empty part of the full subject is still there.
        var full = new EmailTemplateRenderer().Render(template, Filled(template)).Data!.Subject;
        Assert.True(subject.Split(" · ").Length < full.Split(" · ").Length, $"{full} → {subject}");
    }

    [Fact]
    public void F4_the_adverse_event_subject_without_a_doctor_reads_as_the_mockup_without_the_dash()
    {
        Assert.Equal("ACİL · Advers olay bildirimi · 24 saat", EmailTemplateRenderer.WithoutEmptySegments("ACİL · Advers olay bildirimi ·  · 24 saat"));
        Assert.Equal("Ziyaret raporu · 2026-10-20", EmailTemplateRenderer.WithoutEmptySegments("Ziyaret raporu ·  · 2026-10-20"));
        Assert.Equal("Endikasyon dışı bilgi talebi", EmailTemplateRenderer.WithoutEmptySegments("Endikasyon dışı bilgi talebi · "));
    }

    [Theory]
    [InlineData("Invitation WeeklyReview")]
    [InlineData("Your timesheet was returned: 2026-W42")]
    [InlineData("A · B · C")]
    [InlineData("")]
    public void KORU_a_subject_with_no_empty_part_is_returned_exactly_as_rendered(string subject)
        => Assert.Equal(subject, EmailTemplateRenderer.WithoutEmptySegments(subject));

    private static Dictionary<string, object?> Filled(NotificationTemplate template) =>
        Names(template).ToDictionary(n => n, n => (object?)("x-" + n), StringComparer.OrdinalIgnoreCase);

    // Every variable the template declares or its subject names (the subject's own tokens are the point here).
    private static IEnumerable<string> Names(NotificationTemplate template) =>
        template.Variables.Select(v => v.Name)
            .Concat(System.Text.RegularExpressions.Regex.Matches(template.SubjectTemplate, @"\{\{\s*([A-Za-z][A-Za-z0-9_.]*)\s*\}\}").Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// WP-VW-W5 W5-FIX2 — <see cref="IInternalDispatchKeyRepository"/> in memory, for tests that are not about MongoDB: the
/// same rules as the real one (unique per tenant, module and key; released and stale claims taken over), on the AMBIENT
/// tenant like the tenant execution filter.
/// </summary>
internal sealed class InMemoryInternalDispatchKeys(ITenantContext tenant, List<InternalDispatchKey> rows) : IInternalDispatchKeyRepository
{
    private readonly List<InternalDispatchKey> _rows = rows;

    public Task<InternalDispatchKeyClaim> ClaimAsync(string sourceModule, string key, DateTimeOffset now, TimeSpan staleAfter, CancellationToken ct = default)
    {
        lock (_rows)
        {
            var existing = Find(sourceModule, key);
            if (existing is null)
            {
                var row = new InternalDispatchKey
                {
                    TenantId = tenant.TenantId, SourceModule = sourceModule, Key = key,
                    State = InternalDispatchKeyStates.Pending, ClaimToken = Guid.NewGuid(), ClaimedAtUtcTicks = now.UtcTicks
                };
                _rows.Add(row);
                return Task.FromResult(new InternalDispatchKeyClaim(InternalDispatchKeyOutcome.Claimed, row));
            }

            if (existing.State == InternalDispatchKeyStates.Done)
            {
                return Task.FromResult(new InternalDispatchKeyClaim(InternalDispatchKeyOutcome.Done, existing));
            }

            if (existing.State == InternalDispatchKeyStates.Released || existing.ClaimedAtUtcTicks < (now - staleAfter).UtcTicks)
            {
                existing.State = InternalDispatchKeyStates.Pending;
                existing.ClaimToken = Guid.NewGuid();
                existing.ClaimedAtUtcTicks = now.UtcTicks;
                return Task.FromResult(new InternalDispatchKeyClaim(InternalDispatchKeyOutcome.Claimed, existing));
            }

            return Task.FromResult(new InternalDispatchKeyClaim(InternalDispatchKeyOutcome.InFlight, existing));
        }
    }

    public Task<InternalDispatchKey?> GetAsync(string sourceModule, string key, CancellationToken ct = default)
    {
        lock (_rows)
        {
            return Task.FromResult(Find(sourceModule, key));
        }
    }

    public Task<bool> CompleteAsync(Guid id, Guid claimToken, string resultJson, DateTimeOffset now, CancellationToken ct = default)
        => Transition(id, claimToken, row =>
        {
            row.State = InternalDispatchKeyStates.Done;
            row.ResultJson = resultJson;
            row.CompletedAtUtcTicks = now.UtcTicks;
        });

    public Task<bool> ReleaseAsync(Guid id, Guid claimToken, DateTimeOffset now, CancellationToken ct = default)
        => Transition(id, claimToken, row => row.State = InternalDispatchKeyStates.Released);

    private Task<bool> Transition(Guid id, Guid claimToken, Action<InternalDispatchKey> apply)
    {
        lock (_rows)
        {
            var row = _rows.FirstOrDefault(r => r.Id == id && r.TenantId == tenant.TenantId);
            if (row is null || row.ClaimToken != claimToken || row.State != InternalDispatchKeyStates.Pending)
            {
                return Task.FromResult(false);
            }

            apply(row);
            return Task.FromResult(true);
        }
    }

    private InternalDispatchKey? Find(string sourceModule, string key) =>
        _rows.FirstOrDefault(r => r.TenantId == tenant.TenantId && r.SourceModule == sourceModule && r.Key == key);
}
