using System.Reflection;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WC-4 — a task event actually RESOLVES a template, which is the step that decided whether anything happened at
/// all.
///
/// <para><b>The measured failure.</b> <c>QueueEmailNotificationHandler</c> looks a template up by
/// (platform-default | tenant) + Active + channel + locale + key. Nothing matched any <c>platform.tasks.*</c> key,
/// so it answered 404 and created NO dispatch record — the notification tables stayed empty and "the email did not
/// arrive" was indistinguishable from "nothing was ever attempted". Every later stage (render, provider, retry,
/// the four broker events) is already covered by the notification suite and was never the problem; this is.</para>
///
/// <para><b>Scope, stated.</b> This drives the RESOLUTION, not the whole queue→send chain — that chain has its own
/// end-to-end tests (NotificationsSmtpIntegrationTests) which prove a resolvable template reaches Sent. Rebuilding
/// that harness here would re-prove someone else's work and leave this file testing plumbing instead of the
/// defect.</para>
/// </summary>
public sealed class TaskNotificationEndToEndTests
{
    private static readonly string[] Locales = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>The four this ticket dispatches. `duesoon` is seeded but not dispatched (its sweep is out of scope).</summary>
    private static readonly string[] DispatchedEvents =
    [
        TaskNotificationEvents.Assigned,
        TaskNotificationEvents.Claimed,
        TaskNotificationEvents.Completed,
        TaskNotificationEvents.ApprovalRequested
    ];

    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    [InlineData("ar")]
    [InlineData("ru")]
    public void Every_dispatched_event_resolves_a_template_in_every_language(string locale)
    {
        foreach (var eventCode in DispatchedEvents)
        {
            Assert.True(
                Resolve(eventCode, locale) is not null,
                $"{eventCode} does not resolve in '{locale}' — the queue handler would answer 404 and create no "
                + "dispatch record at all.");
        }
    }

    [Fact]
    public void A_key_nobody_seeded_resolves_to_NOTHING()
    {
        /*
         * Non-vacuity, and the failure mode kept visible: an unresolvable template is not a failed dispatch, it
         * is NO dispatch. Nothing in the tables, nothing to retry, nothing to investigate.
         */
        Assert.Null(Resolve("platform.tasks.doesnotexist", "en"));
    }

    [Fact]
    public void The_resolved_template_renders_the_task_TITLE_and_its_reference()
    {
        // A template that resolves but renders neither is an email that arrives saying nothing — which nobody
        // reports as a bug, because it arrived.
        foreach (var eventCode in DispatchedEvents)
        {
            foreach (var locale in Locales)
            {
                var template = Resolve(eventCode, locale)!;
                var rendered = template.SubjectTemplate + template.BodyHtmlTemplate + template.BodyTextTemplate;

                Assert.Contains("{{TaskTitle}}", rendered);
                Assert.Contains("{{TaskId}}", rendered);
            }
        }
    }

    [Fact]
    public void A_resolved_template_is_ACTIVE_and_on_the_email_channel()
    {
        // Both are part of the handler's lookup. A Draft template, or one on another channel, resolves to
        // nothing — the same silent 404 by a different route.
        foreach (var eventCode in DispatchedEvents)
        {
            var template = Resolve(eventCode, "en")!;

            Assert.Equal(NotificationTemplateStatus.Active, template.Status);
            Assert.Equal(NotificationChannelCode.Email, template.Channel);
        }
    }

    /// <summary>
    /// The seed's own list, through reflection, filtered exactly the way the queue handler filters: a
    /// platform-default, Active, Email template for this key and locale. A test that carried its own fixture
    /// would pass while the product shipped no templates at all.
    /// </summary>
    private static NotificationTemplate? Resolve(string templateKey, string locale)
    {
        var seedType = typeof(Diten.Platform.Infrastructure.Persistence.Configurations.NotificationTemplateSeed);
        var factory = seedType.GetMethod("CreatePlatformDefaults", BindingFlags.NonPublic | BindingFlags.Static);
        var seeded = (IReadOnlyList<NotificationTemplate>)factory!.Invoke(null, null)!;

        return seeded.FirstOrDefault(t =>
            t.TenantId is null
            && t.IsPlatformDefault
            && t.Status == NotificationTemplateStatus.Active
            && t.Channel == NotificationChannelCode.Email
            && t.Locale == locale
            && t.TemplateKey == templateKey
            && !t.IsDeleted);
    }
}
