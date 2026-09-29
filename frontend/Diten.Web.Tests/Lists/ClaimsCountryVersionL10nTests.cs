using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// WP-CL-FE-4 — the country version resx family (ClaimsCountryVersion.{7}.resx) and the shared evidence module:
/// the seven languages carry the same non-empty keys without key echo; every key country-version.js reads exists in
/// ClaimsCountryVersion and every key claim-evidence.js reads exists in ClaimsForm; the dynamic families cover their
/// values; and every published evidence type (the BRD seed) has an EvType_{code} label in ClaimsForm — the evidence
/// type picker shows the label, never the bare code (live finding).
/// </summary>
public sealed class ClaimsCountryVersionL10nTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void Country_version_family_has_the_same_non_empty_keys_in_seven_languages_without_echo()
    {
        var reference = Values("ClaimsCountryVersion", "en").Keys.ToHashSet(StringComparer.Ordinal);
        Assert.True(reference.Count > 100);
        foreach (var language in Languages)
        {
            var values = Values("ClaimsCountryVersion", language);
            Assert.True(reference.SetEquals(values.Keys),
                $"{language}: missing [{string.Join(", ", reference.Except(values.Keys))}] extra [{string.Join(", ", values.Keys.Except(reference))}]");
            foreach (var (key, value) in values)
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language}: '{key}' is empty");
                Assert.NotEqual(key, value);
            }
        }
    }

    [Theory]
    [InlineData("country-version.js", "ClaimsCountryVersion")]
    [InlineData("claim-evidence.js", "ClaimsForm")]
    [InlineData("form.js", "ClaimsForm")]
    public void Every_key_a_script_reads_exists_in_its_family(string script, string family)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "wwwroot", "assets", "js", "CRM", "Claims", script));
        var used = Regex.Matches(text, @"\bt\('([A-Za-z0-9_\-]+)'\)").Select(m => m.Groups[1].Value)
            .Concat(Regex.Matches(text, @"L\['([A-Za-z0-9_\-]+)'\]").Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        Assert.NotEmpty(used);
        var keys = Values(family, "en");
        Assert.All(used, key => Assert.True(keys.ContainsKey(key), $"{script} reads '{key}' which {family}.en.resx lacks"));
    }

    [Theory]
    [InlineData("Err_", "core_not_approved", "country_closed", "country_version_exists", "languages_incomplete",
        "audience_not_narrowing", "evidence_required", "evidence_locked", "in_review_locked", "approval_template_missing",
        "workflow_unavailable", "reference_set_missing", "adaptation_reason_required", "invalid_validity")]
    [InlineData("State_", "draft", "in-review", "approved", "review-required", "inactive", "archived")]
    [InlineData("Adapt_", "verbatim", "narrowed", "softened")]
    [InlineData("FlowReason_", "WorkflowTemplateMissing", "WorkflowTemplateNotPublished", "WorkflowPermissionMissing", "WorkflowUnavailable")]
    public void Country_version_dynamic_families_cover_every_value(string prefix, params string[] values)
    {
        var keys = Values("ClaimsCountryVersion", "en");
        Assert.All(values, v => Assert.True(keys.ContainsKey(prefix + v), $"missing {prefix}{v}"));
    }

    [Fact]
    public void Every_published_evidence_type_has_a_label_in_all_seven_languages()
    {
        var seed = Path.Combine(RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.API", "Seed",
            "business-reference-data", "crm-claims-reference.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(seed));
        var codes = FindSet(doc.RootElement, "evidence-type")
            .GetProperty("values").EnumerateArray().Select(v => v.GetProperty("value_code").GetString()!).ToList();
        Assert.NotEmpty(codes);
        foreach (var language in Languages)
        {
            var values = Values("ClaimsForm", language);
            Assert.All(codes, code => Assert.True(values.TryGetValue("EvType_" + code, out var label) && label != code,
                $"{language}: EvType_{code} missing"));
        }
    }

    private static JsonElement FindSet(JsonElement element, string setCode)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("set_code", out var code) && code.GetString() == setCode) return element;
            foreach (var p in element.EnumerateObject())
            {
                var found = FindSet(p.Value, setCode);
                if (found.ValueKind != JsonValueKind.Undefined) return found;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = FindSet(item, setCode);
                if (found.ValueKind != JsonValueKind.Undefined) return found;
            }
        }

        return default;
    }

    private static Dictionary<string, string> Values(string family, string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", "Claims", $"{family}.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
