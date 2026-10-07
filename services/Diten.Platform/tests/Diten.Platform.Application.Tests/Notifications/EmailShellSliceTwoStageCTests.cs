using System.Text.RegularExpressions;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>BL-454 — WP-EMAIL-SHELL-01 slice 2, stage C.</summary>
public sealed partial class EmailShellDispatchTests
{
    private static readonly string[] SevenLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] LifecycleKeys = ["tenant.invite.email", "tenant.suspended.email", "tenant.reactivated.email"];

    public static TheoryData<string, string> LifecycleMailsInSevenLanguages()
    {
        var data = new TheoryData<string, string>();
        foreach (var key in LifecycleKeys)
        {
            foreach (var language in SevenLanguages)
            {
                data.Add(key, language);
            }
        }

        return data;
    }

    // ---------------------------------------------------------------- 1. lifecycle mails: seven languages, shell form

    [Theory]
    [MemberData(nameof(LifecycleMailsInSevenLanguages))]
    public void Each_tenant_lifecycle_mail_is_seeded_in_this_language_in_the_shell_form_with_the_same_variables(string key, string language)
    {
        var seeded = SeededTemplates().Where(t => t.TemplateKey == key).ToList();
        var mail = Assert.Single(seeded, t => t.Locale == language);
        var english = Assert.Single(seeded, t => t.Locale == "en");

        Assert.Equal(key == "tenant.invite.email" ? "1.2.0" : "1.1.0", mail.SemanticVersion); // stage D: the link
        Assert.NotNull(mail.Shell);
        Assert.False(string.IsNullOrWhiteSpace(mail.Shell!.HeadingTemplate));
        Assert.False(string.IsNullOrWhiteSpace(mail.Shell.FootnoteTemplate));
        Assert.NotEmpty(mail.Shell.InfoRows);

        // The same keys in every language: variables, and the placeholders each part renders.
        Assert.Equal(english.Variables.Select(v => (v.Name, v.IsRequired)), mail.Variables.Select(v => (v.Name, v.IsRequired)));
        Assert.Equal(Placeholders(english), Placeholders(mail));
        Assert.All(mail.Variables.Where(v => v.IsRequired), v => Assert.Contains(v.Name, Placeholders(mail)));

        if (language != "en")
        {
            // Written in this language, not English left in place.
            Assert.NotEqual(english.SubjectTemplate, mail.SubjectTemplate);
            Assert.NotEqual(english.BodyHtmlTemplate, mail.BodyHtmlTemplate);
            Assert.NotEqual(english.Shell!.HeadingTemplate, mail.Shell.HeadingTemplate);
            Assert.NotEqual(english.Shell.FootnoteTemplate, mail.Shell.FootnoteTemplate);
        }
    }

    [Theory]
    [MemberData(nameof(LifecycleMailsInSevenLanguages))]
    public async Task Each_tenant_lifecycle_mail_leaves_in_its_language_inside_the_shell(string key, string language)
    {
        var rig = new Rig { TenantLocale = language };
        var seeded = rig.AddSeeded(SeededTemplates().Single(t => t.TemplateKey == key && t.Locale == language));

        await rig.QueueAsync(language, new()
        {
            ["TenantId"] = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ["TenantDisplayName"] = "Diten Pharma",
            ["Reason"] = "Unpaid invoice",
            ["SuspendedAtUtc"] = "2026-10-06 09:00",
            ["ReactivatedAtUtc"] = "2026-10-06 10:00",
            // Stage D — the invitation's link and its expiry (ignored by the other two).
            ["SetPasswordUrl"] = "https://app.example.test/account/set-password?email=a%40b.test&token=stage-c-test-only",
            ["LinkExpiresAtUtc"] = "2026-10-13 09:00"
        });

        var sent = rig.Sent;
        Assert.Equal(seeded.SubjectTemplate, sent.Subject);
        Assert.Contains(seeded.Shell!.HeadingTemplate!, sent.TextBody);
        Assert.Contains(seeded.Shell.InfoRows[0].Label, sent.TextBody);
        Assert.Contains(seeded.Shell.FootnoteTemplate!, sent.TextBody);
        Assert.Contains("<html", sent.HtmlBody, StringComparison.OrdinalIgnoreCase); // framed, not a bare fragment
    }

    private static List<string> Placeholders(NotificationTemplate template)
    {
        var parts = new[] { template.SubjectTemplate, template.BodyHtmlTemplate, template.BodyTextTemplate, template.Shell?.HeadingTemplate, template.Shell?.FootnoteTemplate }
            .Concat(template.Shell?.InfoRows.Select(r => r.ValueTemplate) ?? [])
            // The action's address is rendered too — by NAME (stage D: the invitation's set-password link).
            .Concat(template.Shell?.ActionUrlVariable is { } action ? ["{{" + action + "}}"] : []);
        return parts
            .Where(p => p is not null)
            .SelectMany(p => Regex.Matches(p!, @"\{\{\s*([A-Za-z][A-Za-z0-9_.]*)\s*\}\}").Select(m => m.Groups[1].Value))
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    // ---------------------------------------------------------------- 3. the tenant's fallback policy decides

    [Theory]
    [InlineData(NotificationFallbackPolicy.UsePlatformDefault, null)]
    [InlineData(NotificationFallbackPolicy.DisableSending, MessagingSettingsSelection.ReasonTenantSendingDisabled)]
    [InlineData(NotificationFallbackPolicy.FailFast, MessagingSettingsSelection.ReasonTenantSettingsDisabled)]
    [InlineData((NotificationFallbackPolicy)99, MessagingSettingsSelection.ReasonTenantFallbackPolicyUnknown)] // C-FIX1 2
    public async Task A_disabled_tenant_row_follows_its_own_fallback_policy_in_the_resolver_and_in_the_provider(
        NotificationFallbackPolicy policy, string? refusal)
    {
        var rig = new Rig();
        var own = Rig.SettingsRow(rig.TenantId, "Diten Pharma", null);
        own.IsEnabled = false;
        own.FallbackPolicy = policy;
        await rig.Settings.CreateAsync(own);

        var resolved = await new TenantMessagingSettingsResolver(rig.Settings).ResolveAsync(rig.TenantId);
        var provided = await rig.Provider.SendEmailAsync(new MessagingProviderEmailRequest(
            Guid.NewGuid(), rig.TenantId, "corr", "Subject",
            [new Diten.Platform.Application.Features.Notifications.EmailRecipientDto("user@example.com", "User")], [], [],
            "<p>x</p>", "x", "<p>x</p>", "x", null, null));

        if (refusal is null)
        {
            Assert.True(resolved.IsSuccessful);
            Assert.True(resolved.Data!.IsPlatformDefault);
            Assert.True(provided.Accepted);
            Assert.NotNull(rig.Transport.LastSentMessage);
        }
        else
        {
            Assert.False(resolved.IsSuccessful);
            Assert.Equal(refusal, resolved.ReasonCode);
            Assert.False(provided.Accepted);
            Assert.Equal(refusal, provided.ErrorCode);
            Assert.Null(rig.Transport.LastSentMessage); // nothing went out through the platform's mailbox
        }
    }

    [Fact]
    public async Task Without_a_tenant_row_the_platform_default_is_used_as_before()
    {
        var rig = new Rig();

        var resolved = await new TenantMessagingSettingsResolver(rig.Settings).ResolveAsync(rig.TenantId);

        Assert.True(resolved.IsSuccessful);
        Assert.True(resolved.Data!.IsPlatformDefault);
    }

    [Fact]
    public async Task A_retry_refused_by_the_tenants_policy_keeps_the_named_reason_on_the_dispatch()
    {
        var rig = new Rig();
        var own = Rig.SettingsRow(rig.TenantId, "Diten Pharma", null);
        own.IsEnabled = false;
        own.FallbackPolicy = NotificationFallbackPolicy.DisableSending;
        await rig.Settings.CreateAsync(own);
        var template = rig.AddTemplate("en", "<p>x</p>", "x");
        var row = rig.AddFailedDispatch("{}", template.Id);
        var mediator = new ValidatingMediator(rig.Dispatches);

        await new EmailDispatchJob(
                rig.Dispatches, new TenantMessagingSettingsResolver(rig.Settings),
                new NotificationsSmtpIntegrationTests.TestProviderResolver(rig.Provider), mediator,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<EmailDispatchJob>.Instance, NoInvitationLedger.Instance, rig.Templates, new EmailTemplateRenderer(), rig.Composer)
            .HandleAsync(new EmailDispatchJobArgs(rig.TenantId, row.Id), new BackgroundJobContext(), CancellationToken.None);

        Assert.Empty(mediator.Refusals);
        Assert.Equal(MessagingSettingsSelection.ReasonTenantSendingDisabled, row.ErrorCode);
        Assert.Null(rig.Transport.LastSentMessage);
    }
}
