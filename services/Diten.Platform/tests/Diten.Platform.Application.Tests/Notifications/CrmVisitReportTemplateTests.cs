using System.Text.RegularExpressions;
using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-VW-W5 PLT-NOTIFY (K-N3, K-N4) — the CRM visit report e-mails as the platform seeds them, read from the seed's own
/// list (a test that listed them itself would pass while the seed shipped none): every event in all seven tenant
/// languages, the variables exactly the manifest's, every required variable rendered somewhere, the adverse event's
/// "urgent / 24 hours / no medical assessment", and the data-protection footnote on every one.
/// </summary>
public sealed class CrmVisitReportTemplateTests
{
    private static readonly string[] Events =
    [
        CrmManifestProvider.VisitReportSubmittedEvent,
        CrmManifestProvider.VisitReportAdverseEventEvent,
        CrmManifestProvider.VisitReportOffLabelRequestEvent,
        CrmManifestProvider.VisitReportOverdueEvent
    ];

    private static readonly Regex Token = new(@"\{\{\s*([A-Za-z][A-Za-z0-9_.]*)\s*\}\}", RegexOptions.Compiled);

    private static IReadOnlyList<NotificationTemplate> Seeded() =>
        NotificationTemplateSeed.CreatePlatformDefaults().Where(t => Events.Contains(t.TemplateKey)).ToList();

    public static IEnumerable<object[]> EveryEventAndLanguage() =>
        Events.SelectMany(e => NotificationTemplateSeed.TenantLocales.Select(l => new object[] { e, l }));

    [Theory]
    [MemberData(nameof(EveryEventAndLanguage))]
    public void Each_event_has_a_platform_default_template_in_each_of_the_seven_languages(string eventCode, string locale)
    {
        var template = Assert.Single(Seeded(), t => t.TemplateKey == eventCode && t.Locale == locale);

        Assert.Null(template.TenantId);
        Assert.True(template.IsPlatformDefault);
        Assert.Equal(NotificationTemplateStatus.Active, template.Status);
        Assert.NotNull(template.Shell);
        Assert.False(string.IsNullOrWhiteSpace(template.Shell!.HeadingTemplate));
        Assert.False(string.IsNullOrWhiteSpace(template.Shell.ActionLabel));
        Assert.Equal("reportUrl", template.Shell.ActionUrlVariable);
        Assert.False(string.IsNullOrWhiteSpace(template.Shell.FootnoteTemplate));
    }

    [Fact]
    public void The_variables_are_the_contract_annex_names_as_crm_sends_them()
    {
        var manifest = new CrmManifestProvider().GetManifest().NotificationEvents!.ToDictionary(e => e.EventCode);
        string[] common = ["repName", "doctorName", "institutionName", "visitDate", "reportUrl"];
        var expected = new Dictionary<string, string[]>
        {
            [CrmManifestProvider.VisitReportSubmittedEvent] = [.. common, "products", "note", "adverseEvent", "offLabelRequest", "followUpDate"],
            [CrmManifestProvider.VisitReportAdverseEventEvent] = [.. common, "products", "adverseEventNote"],
            [CrmManifestProvider.VisitReportOffLabelRequestEvent] = [.. common, "products"],
            [CrmManifestProvider.VisitReportOverdueEvent] = [.. common, "deadline"]
        };

        foreach (var (code, names) in expected)
        {
            var declared = manifest[code];
            Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal),
                declared.RequiredVariables!.Concat(declared.OptionalVariables!).Select(v => v.Name).OrderBy(n => n, StringComparer.Ordinal));
            // Only what CRM always fills is required: an empty required value is refused (REQUIRED_VARIABLE_MISSING).
            Assert.Equal(code == CrmManifestProvider.VisitReportOverdueEvent ? ["repName", "visitDate", "reportUrl", "deadline"] : ["repName", "visitDate", "reportUrl"],
                declared.RequiredVariables!.Select(v => v.Name));
        }
    }

    [Fact]
    public void Every_event_code_passes_the_catalogues_own_format_rule()
    {
        // A code the rule refuses is synced as Draft and refused by the dispatch adapter (INVALID_EVENT_CODE): the mail
        // never goes, and nothing else looks wrong. Lowercase dotted segments of letters and digits — no hyphen.
        Assert.All(Events, code => Assert.True(Diten.Platform.Application.Features.Notifications.NotificationParsing.IsValidTemplateKey(code), code));
    }

    [Fact]
    public void Exactly_four_events_times_seven_languages_are_seeded()
    {
        Assert.Equal(Events.Length * NotificationTemplateSeed.TenantLocales.Length, Seeded().Count);
    }

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
            used.Add(template.Shell!.ActionUrlVariable!);
            Assert.All(required, name => Assert.True(used.Contains(name), $"{template.TemplateKey}/{template.Locale}: {name} is never rendered."));
            Assert.All(used, name => Assert.True(allowed.Contains(name), $"{template.TemplateKey}/{template.Locale}: {{{{{name}}}}} is not declared by the event."));
            // No patient identity: no variable could carry one.
            Assert.DoesNotContain(allowed, name => name.Contains("Patient", StringComparison.OrdinalIgnoreCase));
        }
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
        Assert.NotEqual(english.Shell.ActionLabel, own.Shell.ActionLabel);
        Assert.Equal(english.Shell.InfoRows.Select(r => r.ValueTemplate).Where(v => v.StartsWith("{{", StringComparison.Ordinal)),
            own.Shell.InfoRows.Select(r => r.ValueTemplate).Where(v => v.StartsWith("{{", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_adverse_event_mail_is_urgent_names_the_24_hours_and_says_the_representative_makes_no_medical_assessment()
    {
        foreach (var template in Seeded().Where(t => t.TemplateKey == CrmManifestProvider.VisitReportAdverseEventEvent))
        {
            Assert.Contains("24", template.SubjectTemplate);
            Assert.Contains("24", template.BodyHtmlTemplate);
            // The notice is its own paragraph, after the introduction.
            Assert.Equal(2, Regex.Matches(template.BodyHtmlTemplate!, "<p>").Count);
            Assert.Contains("<strong>", template.BodyHtmlTemplate);
        }

        var tr = Seeded().Single(t => t.TemplateKey == CrmManifestProvider.VisitReportAdverseEventEvent && t.Locale == "tr");
        Assert.StartsWith("ACİL", tr.SubjectTemplate);
        Assert.Contains("Temsilci tıbbi değerlendirme yapmaz", tr.BodyHtmlTemplate);
        var en = Seeded().Single(t => t.TemplateKey == CrmManifestProvider.VisitReportAdverseEventEvent && t.Locale == "en");
        Assert.StartsWith("URGENT", en.SubjectTemplate);
    }

    [Fact]
    public void Every_mail_says_it_carries_no_patient_identity_the_turkish_one_names_kvkk()
    {
        Assert.All(Seeded(), t => Assert.Contains("CRM", t.Shell!.FootnoteTemplate));
        Assert.All(Seeded().Where(t => t.Locale == "tr"), t => Assert.Contains("KVKK", t.Shell!.FootnoteTemplate));
    }

    [Fact]
    public void A_seeded_template_renders_with_every_token_replaced()
    {
        var renderer = new EmailTemplateRenderer();
        var variables = new Dictionary<string, object?>
        {
            ["doctorName"] = "Doç. Dr. Mehmet Yılmaz",
            ["institutionName"] = "Şişli Hamidiye Etfal EAH",
            ["visitDate"] = "2026-10-05",
            ["repName"] = "Ayşe Demir",
            ["reportUrl"] = "https://crm.test/CRM/VisitWorkspace?visit=v1",
            ["deadline"] = "2026-10-07T10:02:00Z"
        };

        foreach (var template in Seeded())
        {
            var rendered = renderer.Render(template, variables);

            Assert.True(rendered.IsSuccessful, $"{template.TemplateKey}/{template.Locale}: {string.Join(", ", rendered.Errors)}");
            Assert.Contains("Mehmet Yılmaz", rendered.Data!.Subject);
            Assert.DoesNotContain("{{", rendered.Data.Subject + rendered.Data.BodyHtml + rendered.Data.BodyText);
            Assert.Contains("https://crm.test/CRM/VisitWorkspace?visit=v1", rendered.Data.BodyText);
        }
    }

    private static IEnumerable<string> Tokens(NotificationTemplate template) =>
        new[] { template.SubjectTemplate, template.BodyHtmlTemplate, template.BodyTextTemplate, template.Shell?.HeadingTemplate, template.Shell?.FootnoteTemplate }
            .Concat(template.Shell?.InfoRows.Select(r => r.ValueTemplate) ?? [])
            .Where(text => text is not null)
            .SelectMany(text => Token.Matches(text!).Select(m => m.Groups[1].Value));
}
