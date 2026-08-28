using System.Reflection;
using System.Text.RegularExpressions;
using Diten.Platform.Application.Features.Tasks;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Features.Tasks.SelfRegistration;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WC-4 — the seeded <c>platform.tasks.*</c> templates, and their agreement with the manifest.
///
/// <para><b>Why a template's absence was fatal and silent.</b> With no template,
/// <c>QueueEmailNotificationHandler</c> answers 404 and NO dispatch record is created at all — so "the email did
/// not arrive" is indistinguishable from "nothing was ever attempted", and the notification tables stay empty
/// with nothing to investigate. Six tenant templates were seeded; none of the five task events had one.</para>
///
/// <para><b>Why the variable agreement is checked.</b> A template that renders a variable the event does not
/// declare produces a BLANK — an email that arrives and says nothing. Nobody reports that, because it arrived.
/// </para>
/// </summary>
public sealed class TaskNotificationTemplateTests
{
    /// <summary>Tenant surface: all seven, every time. Two would put English in front of five sets of readers.</summary>
    private static readonly string[] Locales = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] TaskTemplateKeys =
    [
        "platform.tasks.assigned",
        "platform.tasks.claimed",
        "platform.tasks.duesoon",
        "platform.tasks.completed",
        "platform.tasks.approvalrequested",
        // Somebody said something on the task (2026-08-14). Its seven languages are asserted by the same loop as
        // the other five — a sixth event added without its templates is an email that silently never arrives.
        "platform.tasks.commented"
    ];

    [Fact]
    public void Every_task_event_has_a_template_in_all_SEVEN_languages()
    {
        var seeded = SeededTemplates();

        foreach (var key in TaskTemplateKeys)
        {
            foreach (var locale in Locales)
            {
                Assert.True(
                    seeded.Any(t => t.TemplateKey == key && t.Locale == locale),
                    $"No seeded template for {key} in '{locale}'.");
            }
        }
    }

    [Fact]
    public void Not_one_of_them_is_a_placeholder_or_left_in_English()
    {
        /*
         * The l10n gate. A missing key shows the key ON SCREEN, and an untranslated one is just as wrong in a
         * different way: the reader sees a language they did not choose and assumes the system is broken.
         */
        var seeded = SeededTemplates();

        foreach (var key in TaskTemplateKeys)
        {
            var english = seeded.Single(t => t.TemplateKey == key && t.Locale == "en");

            foreach (var locale in Locales.Where(l => l != "en"))
            {
                var localized = seeded.Single(t => t.TemplateKey == key && t.Locale == locale);

                Assert.False(string.IsNullOrWhiteSpace(localized.SubjectTemplate), $"{key}/{locale} subject empty.");
                Assert.False(string.IsNullOrWhiteSpace(localized.BodyHtmlTemplate), $"{key}/{locale} html empty.");
                Assert.False(string.IsNullOrWhiteSpace(localized.BodyTextTemplate), $"{key}/{locale} text empty.");

                Assert.NotEqual(english.SubjectTemplate, localized.SubjectTemplate);
                Assert.NotEqual(english.BodyTextTemplate, localized.BodyTextTemplate);

                // The usual placeholder tells.
                Assert.DoesNotContain("TODO", localized.SubjectTemplate, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("TODO", localized.BodyHtmlTemplate, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("lorem", localized.BodyTextTemplate, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Every_variable_a_template_RENDERS_is_one_the_event_DECLARES()
    {
        /*
         * The mismatch that produces a silent blank. Checked in this direction because it is the dangerous one:
         * a template asking for something nobody supplies renders nothing, and the email still "works".
         */
        var seeded = SeededTemplates();
        var declared = DeclaredVariablesByTemplateKey();

        foreach (var template in seeded.Where(t => TaskTemplateKeys.Contains(t.TemplateKey)))
        {
            var available = declared[template.TemplateKey];

            foreach (var used in Placeholders(template.SubjectTemplate)
                         .Concat(Placeholders(template.BodyHtmlTemplate))
                         .Concat(Placeholders(template.BodyTextTemplate))
                         .Distinct())
            {
                Assert.True(
                    available.Contains(used),
                    $"{template.TemplateKey}/{template.Locale} renders {{{{{used}}}}}, which the manifest does not declare.");
            }
        }
    }

    [Fact]
    public void Every_REQUIRED_variable_is_actually_rendered_somewhere()
    {
        // The other direction, and it matters less but still matters: an event that declares a variable no
        // template shows is carrying a field for nobody.
        var seeded = SeededTemplates();

        foreach (var manifestEvent in TaskManifest().NotificationEvents!)
        {
            var templates = seeded.Where(t => t.TemplateKey == manifestEvent.DefaultTemplateKey).ToList();
            Assert.NotEmpty(templates);

            foreach (var required in manifestEvent.RequiredVariables ?? [])
            {
                Assert.True(
                    templates.All(t => Placeholders(t.SubjectTemplate)
                        .Concat(Placeholders(t.BodyHtmlTemplate))
                        .Concat(Placeholders(t.BodyTextTemplate))
                        .Contains(required.Name)),
                    $"{manifestEvent.DefaultTemplateKey} declares {required.Name} but not every locale renders it.");
            }
        }
    }

    [Fact]
    public void The_seeded_keys_match_the_manifests_declared_keys_exactly()
    {
        /*
         * Non-vacuity for everything above, and a real trap: a template seeded under a key no event names is
         * dead weight, and an event naming a key nobody seeded is a 404 with no dispatch record.
         */
        var declared = TaskManifest().NotificationEvents!.Select(e => e.DefaultTemplateKey).OrderBy(k => k).ToList();

        Assert.Equal(TaskTemplateKeys.OrderBy(k => k), declared);
    }

    [Fact]
    public void The_task_templates_are_PLATFORM_defaults_a_tenant_can_override()
    {
        // Platform-default (TenantId null) is what makes tenant override possible — the manifest says
        // CanTenantOverride: true for every one of these, and a tenant-scoped seed would contradict it.
        foreach (var template in SeededTemplates().Where(t => TaskTemplateKeys.Contains(t.TemplateKey)))
        {
            Assert.Null(template.TenantId);
            Assert.True(template.IsPlatformDefault);
            Assert.Equal(NotificationTemplateStatus.Active, template.Status);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the seed's own list through reflection rather than re-declaring it. A test that listed the
    /// templates itself would pass while the seed shipped none.
    /// </summary>
    private static IReadOnlyList<NotificationTemplate> SeededTemplates()
    {
        var seedType = typeof(Diten.Platform.Infrastructure.Persistence.Configurations.NotificationTemplateSeed);
        var factory = seedType.GetMethod("CreatePlatformDefaults", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(factory);

        return (IReadOnlyList<NotificationTemplate>)factory!.Invoke(null, null)!;
    }

    private static ModuleManifestDocument TaskManifest() => new TaskManifestProvider().GetManifest();

    private static Dictionary<string, HashSet<string>> DeclaredVariablesByTemplateKey()
        => TaskManifest().NotificationEvents!.ToDictionary(
            e => e.DefaultTemplateKey,
            e => (e.RequiredVariables ?? []).Concat(e.OptionalVariables ?? [])
                .Select(v => v.Name)
                .ToHashSet(StringComparer.Ordinal));

    private static IEnumerable<string> Placeholders(string? template)
        => template is null
            ? []
            : Regex.Matches(template, @"\{\{\s*([A-Za-z][A-Za-z0-9_.]*)\s*\}\}")
                .Select(m => m.Groups[1].Value);
}
