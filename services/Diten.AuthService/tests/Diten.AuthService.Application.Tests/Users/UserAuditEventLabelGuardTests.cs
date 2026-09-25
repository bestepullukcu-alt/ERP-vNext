using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.AuthService.Application.Features.Users.Services;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-456 — the user-lifecycle events AuthService forwards arrive in Platform's Audit Log as a <c>RequestType</c>; the
/// screen labels them from its own resx (Platform screen = en + tr). Three lists must stay one: the AuthService
/// vocabulary (<see cref="UserAuditEvents"/>), the screen's l10n payload (<c>AuditLogEventLabels</c> in
/// <c>_IndexL10n.cshtml</c>) and the <c>AuditLog.Event.*</c> keys of both resx files. An event added on the Auth side
/// alone shows up on the screen as a bare "Execute" — this is the test that says so.
/// </summary>
public sealed class UserAuditEventLabelGuardTests
{
    private static readonly string[] PlatformLanguages = ["en", "tr"];

    private static IReadOnlySet<string> Vocabulary => UserAuditEvents.Operations.Keys.ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Every_event_constant_is_in_the_forwarded_vocabulary()
    {
        var constants = typeof(UserAuditEvents)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != nameof(UserAuditEvents.EntityType))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(constants.OrderBy(x => x), Vocabulary.OrderBy(x => x));
    }

    [Fact]
    public void The_audit_log_payload_labels_exactly_the_vocabulary()
    {
        var bridge = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Views", "Platform", "AuditLog", "_IndexL10n.cshtml"));
        var published = Regex.Matches(bridge, @"\[""(?<name>[a-z_]+)""\]\s*=\s*Localizer\[""AuditLog\.Event\.\k<name>""\]\.Value")
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(Vocabulary.OrderBy(x => x), published.OrderBy(x => x));
    }

    [Fact]
    public void Both_audit_log_resx_files_carry_a_label_for_every_event()
    {
        var dir = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "Platform", "AuditLog");
        foreach (var language in PlatformLanguages)
        {
            var labels = XDocument.Load(Path.Combine(dir, $"AuditLogIndex.{language}.resx")).Root!.Elements("data")
                .Select(d => (string?)d.Attribute("name"))
                .Where(n => n is not null && n.StartsWith("AuditLog.Event.", StringComparison.Ordinal))
                .Select(n => n!["AuditLog.Event.".Length..])
                .ToHashSet(StringComparer.Ordinal);

            Assert.True(Vocabulary.SetEquals(labels),
                $"AuditLogIndex.{language}.resx AuditLog.Event.* keys differ from UserAuditEvents: "
                + $"missing [{string.Join(", ", Vocabulary.Except(labels))}] extra [{string.Join(", ", labels.Except(Vocabulary))}]");
        }
    }

    [Fact]
    public void The_screen_reads_the_event_label_for_the_operation_column()
    {
        var js = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "wwwroot", "assets", "js", "Platform", "AuditLog", "index.js"));

        Assert.Contains("L.AuditLogEventLabels", js, StringComparison.Ordinal);
        Assert.Contains("render: (data, type, row) => escapeHtml(eventLabel(row) || data)", js, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repo root (frontend/Diten.Web/Resources) from the test output directory.");
    }
}
