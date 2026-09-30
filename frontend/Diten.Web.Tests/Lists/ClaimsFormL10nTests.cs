using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// WP-CL-FE-3 — the claim form resx family (ClaimsForm.{7}.resx): the seven tenant languages carry the same keys with
/// non-empty values and no key echo, every key form.js reads literally exists, and the dynamic families form.js builds
/// at runtime (Err_{code}, State_{status}, FlowReason_{reason}, DocState_{state}) cover every value it can receive.
/// </summary>
public sealed class ClaimsFormL10nTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void All_seven_languages_carry_the_same_non_empty_keys_without_echo()
    {
        var reference = Values("en").Keys.ToHashSet(StringComparer.Ordinal);
        Assert.True(reference.Count > 100);
        foreach (var language in Languages)
        {
            var values = Values(language);
            Assert.True(reference.SetEquals(values.Keys),
                $"{language}: missing [{string.Join(", ", reference.Except(values.Keys))}] extra [{string.Join(", ", values.Keys.Except(reference))}]");
            foreach (var (key, value) in values)
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language}: '{key}' is empty");
                Assert.NotEqual(key, value);
            }
        }
    }

    [Fact]
    public void Every_key_form_js_reads_exists()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "wwwroot", "assets", "js", "CRM",
            "Claims", "form.js"));
        var used = Regex.Matches(script, @"\bt\('([A-Za-z0-9_\-]+)'\)").Select(m => m.Groups[1].Value)
            .Concat(Regex.Matches(script, @"L\['([A-Za-z0-9_\-]+)'\]").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();
        Assert.NotEmpty(used);
        var keys = Values("en");
        Assert.All(used, key => Assert.True(keys.ContainsKey(key), $"form.js reads '{key}' which ClaimsForm.en.resx lacks"));
    }

    [Theory]
    // CRM claim / evidence refusal codes the page can receive (ClaimErrorCodes + MOD-0031 reason codes) …
    [InlineData("Err_", "evidence_required", "evidence_locked", "evidence_unavailable", "in_review_locked", "invalid_status",
        "approval_template_missing", "approval_forbidden", "workflow_unavailable", "workflow_request_rejected",
        "withdraw_not_possible", "no_open_review", "approval_via_workflow_only", "product_required", "product_not_found",
        "claim_product_mismatch", "local_country_required", "language_not_allowed", "reference_set_missing",
        "audience_not_found", "dependency_unavailable", "open_version_exists", "invalid_state", "duplicate_code",
        "quote_required", "duplicate_link", "document_not_readable", "document_not_found", "version_required",
        "invalid_evidence_type", "removal_reason_required", "link_not_found")]
    // … the claim statuses …
    [InlineData("State_", "draft", "in-review", "approved", "review-required", "inactive", "archived")]
    // … the workflow-template lookup reasons (ClaimsController.V2) …
    [InlineData("FlowReason_", "WorkflowTemplateMissing", "WorkflowTemplateNotPublished", "WorkflowPermissionMissing", "WorkflowUnavailable")]
    // … and the MOD-0031 document states that need a warning.
    [InlineData("DocState_", "suspended", "retired", "withdrawn")]
    public void Dynamic_key_families_cover_every_value(string prefix, params string[] values)
    {
        var keys = Values("en");
        Assert.All(values, v => Assert.True(keys.ContainsKey(prefix + v), $"missing {prefix}{v}"));
    }

    private static Dictionary<string, string> Values(string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", "Claims",
            $"ClaimsForm.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
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

        throw new DirectoryNotFoundException("Repository root (frontend/Diten.Web/Resources) not found.");
    }
}
