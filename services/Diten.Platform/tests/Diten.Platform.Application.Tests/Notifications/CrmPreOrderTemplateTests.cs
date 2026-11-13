using System.Text.RegularExpressions;
using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Diten.Platform.Application.Features.Meetings.SelfRegistration;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-VW-W6 W6-BE-c1 — the seven MOD-0168 pre-order e-mails as the platform seeds them, read from the seed's own list: every
/// event in all seven tenant languages; the codes pinned (CRM pins the same strings); the variables exactly the manifest's;
/// the subject = event + order number + pharmacy and never a person; the depot and the pharmacy mails carry no CRM link and
/// say the depot invoices; the events sync clean and Active.
/// </summary>
public sealed class CrmPreOrderTemplateTests
{
    private static readonly string[] Events = CrmManifestProvider.PreOrderEvents.ToArray();
    private static readonly string[] External = [CrmManifestProvider.PreOrderTransmittedDepotEvent, CrmManifestProvider.PreOrderPharmacyCopyEvent];
    private static readonly Regex Token = new(@"\{\{\s*([A-Za-z][A-Za-z0-9_.]*)\s*\}\}", RegexOptions.Compiled);

    private static IReadOnlyList<NotificationTemplate> Seeded() =>
        NotificationTemplateSeed.CreatePlatformDefaults().Where(t => Events.Contains(t.TemplateKey)).ToList();

    public static IEnumerable<object[]> EveryEventAndLanguage() =>
        Events.SelectMany(e => NotificationTemplateSeed.TenantLocales.Select(l => new object[] { e, l }));

    [Fact]
    public void The_seven_codes_are_pinned_without_a_hyphen_and_pass_the_catalogues_format_rule()
    {
        Assert.Equal(
            new[]
            {
                "commercial.preorder.approvalrequested", "commercial.preorder.autoapproved", "commercial.preorder.changesrequested",
                "commercial.preorder.rejected", "commercial.preorder.transmitteddepot", "commercial.preorder.pharmacycopy",
                "commercial.preorder.cancelled"
            },
            Events);
        Assert.All(Events, code => Assert.True(Diten.Platform.Application.Features.Notifications.NotificationParsing.IsValidTemplateKey(code), code));
    }

    [Theory]
    [MemberData(nameof(EveryEventAndLanguage))]
    public void Each_event_has_a_platform_default_template_in_each_of_the_seven_languages(string eventCode, string locale)
    {
        var template = Assert.Single(Seeded(), t => t.TemplateKey == eventCode && t.Locale == locale);

        Assert.Null(template.TenantId);
        Assert.True(template.IsPlatformDefault);
        Assert.Equal(NotificationTemplateStatus.Active, template.Status);
        Assert.False(string.IsNullOrWhiteSpace(template.Shell!.HeadingTemplate));
        Assert.False(string.IsNullOrWhiteSpace(template.Shell.FootnoteTemplate));
        Assert.Contains("CRM", template.Shell.FootnoteTemplate);
    }

    [Fact]
    public void Exactly_seven_events_times_seven_languages_are_seeded()
        => Assert.Equal(Events.Length * NotificationTemplateSeed.TenantLocales.Length, Seeded().Count);

    [Fact]
    public void The_templates_variables_are_the_manifests_and_every_required_one_is_rendered()
    {
        var manifest = new CrmManifestProvider().GetManifest().NotificationEvents!.ToDictionary(e => e.EventCode);

        foreach (var template in Seeded())
        {
            var declared = manifest[template.TemplateKey];
            var required = declared.RequiredVariables!.Select(v => v.Name).ToList();
            var allowed = required.Concat(declared.OptionalVariables!.Select(v => v.Name)).ToHashSet(StringComparer.Ordinal);

            Assert.Equal(required, template.Variables.Where(v => v.IsRequired).Select(v => v.Name));
            var used = Tokens(template).ToHashSet(StringComparer.Ordinal);
            if (template.Shell!.ActionUrlVariable is { } action)
            {
                used.Add(action);
            }

            Assert.All(required, name => Assert.True(used.Contains(name), $"{template.TemplateKey}/{template.Locale}: {name} is never rendered."));
            Assert.All(used, name => Assert.True(allowed.Contains(name), $"{template.TemplateKey}/{template.Locale}: {{{{{name}}}}} is not declared by the event."));
        }
    }

    [Fact]
    public void The_subject_is_the_event_the_order_number_and_the_pharmacy_never_a_persons_name()
    {
        foreach (var template in Seeded())
        {
            var subjectTokens = Token.Matches(template.SubjectTemplate).Select(m => m.Groups[1].Value).ToList();
            Assert.Contains("orderCode", subjectTokens);
            Assert.Contains("pharmacyName", subjectTokens);
            Assert.DoesNotContain("repName", subjectTokens);
            Assert.DoesNotContain("sellerName", subjectTokens);
        }
    }

    [Fact]
    public void The_depot_and_the_pharmacy_mails_carry_no_crm_link_and_say_the_depot_invoices()
    {
        foreach (var template in Seeded().Where(t => External.Contains(t.TemplateKey)))
        {
            Assert.Null(template.Shell!.ActionUrlVariable);
            Assert.DoesNotContain("orderUrl", template.BodyTextTemplate);
            Assert.DoesNotContain("approvalUrl", template.BodyTextTemplate);
            Assert.Contains("{{sellerName}}", template.Shell.FootnoteTemplate);
        }

        var tr = Seeded().Single(t => t.TemplateKey == CrmManifestProvider.PreOrderTransmittedDepotEvent && t.Locale == "tr");
        Assert.Contains("faturayı depo düzenler", tr.Shell!.FootnoteTemplate);
        // The cancellation reaches the depot too (after a transmission): its plain-text part writes no CRM link either.
        Assert.All(Seeded().Where(t => t.TemplateKey == CrmManifestProvider.PreOrderCancelledEvent),
            t => Assert.DoesNotContain("{{orderUrl}}", t.BodyTextTemplate));
    }

    [Fact]
    public void No_mail_shows_a_discount_percentage()
    {
        // The effective discount is internal (DESIGN-GAP §3): no template has a slot for it.
        Assert.All(Seeded(), t => Assert.DoesNotContain(Tokens(t), name => name.Contains("discount", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [MemberData(nameof(EveryEventAndLanguage))]
    public void Every_language_is_its_own_text_not_the_english_one(string eventCode, string locale)
    {
        if (locale == "en")
        {
            return;
        }

        var english = Seeded().Single(t => t.TemplateKey == eventCode && t.Locale == "en");
        var own = Seeded().Single(t => t.TemplateKey == eventCode && t.Locale == locale);

        Assert.NotEqual(english.SubjectTemplate, own.SubjectTemplate);
        Assert.NotEqual(english.Shell!.HeadingTemplate, own.Shell!.HeadingTemplate);
        Assert.NotEqual(english.Shell.FootnoteTemplate, own.Shell.FootnoteTemplate);
        Assert.Equal(english.Shell.InfoRows.Select(r => r.ValueTemplate), own.Shell.InfoRows.Select(r => r.ValueTemplate));
    }

    [Fact]
    public void A_seeded_template_renders_with_every_token_replaced_and_an_empty_delivery_date_leaves_its_subject_part_out()
    {
        var renderer = new EmailTemplateRenderer();
        var variables = new Dictionary<string, object?>
        {
            ["orderCode"] = "MG-ORD-000123", ["pharmacyName"] = "Merkez Eczanesi", ["repName"] = "Ayşe Demir", ["depotName"] = "Selçuk Ecza",
            ["deliveryDate"] = "", ["lineCount"] = "3", ["totalText"] = "1250.00 TRY", ["orderUrl"] = "https://crm.test/Commercial/PreOrders/Details/1",
            ["approvalUrl"] = "https://crm.test/Commercial/PreOrders/ApprovalLink?token=t", ["expiresAt"] = "2026-10-23", ["reason"] = "MF too high",
            ["note"] = "Kapıya bırakın", ["sellerName"] = "Grand Medical"
        };

        foreach (var template in Seeded())
        {
            var rendered = renderer.Render(template, variables);

            Assert.True(rendered.IsSuccessful, $"{template.TemplateKey}/{template.Locale}: {string.Join(", ", rendered.Errors)}");
            Assert.Contains("MG-ORD-000123", rendered.Data!.Subject);
            Assert.DoesNotContain("{{", rendered.Data.Subject + rendered.Data.BodyHtml + rendered.Data.BodyText);
            Assert.DoesNotContain("·  ·", rendered.Data.Subject);
            Assert.False(rendered.Data.Subject.TrimEnd().EndsWith('·'), rendered.Data.Subject);
        }
    }

    [Fact]
    public void The_seven_events_sync_from_the_crm_manifest_clean_and_active()
    {
        var catalog = InternalNotificationEventDispatchTests.Catalog.FromManifests(new CrmManifestProvider(), new MeetingManifestProvider());
        Assert.All(Events, code =>
        {
            var definition = catalog.Single(code);
            Assert.Equal(NotificationEventStatus.Active, definition.Status);
            Assert.Equal("CRM", definition.OwnerModuleId);
            Assert.Equal(code, definition.DefaultTemplateKey);
        });
    }

    private static IEnumerable<string> Tokens(NotificationTemplate template) =>
        new[] { template.SubjectTemplate, template.BodyHtmlTemplate, template.BodyTextTemplate, template.Shell?.HeadingTemplate, template.Shell?.FootnoteTemplate }
            .Concat(template.Shell?.InfoRows.Select(r => r.ValueTemplate) ?? [])
            .Where(text => text is not null)
            .SelectMany(text => Token.Matches(text!).Select(m => m.Groups[1].Value));
}
