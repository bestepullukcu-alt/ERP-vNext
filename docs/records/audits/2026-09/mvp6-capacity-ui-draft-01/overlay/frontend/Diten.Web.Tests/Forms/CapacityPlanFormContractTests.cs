using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Forms;

// MOD-0192 Capacity — form, view and resource contract (pack §23.2, §23.5, §23.6, §23.9).
// Static source checks; runtime behaviour is covered by the Playwright scenarios for the Mac lane (Q88b).
// DRAFT overlay — not built, not run.
public sealed class CapacityPlanFormContractTests
{
    private const string Views = "frontend/Diten.Web/Views/SupplyChain/CapacityPlans/";
    private const string Resources = "frontend/Diten.Web/Resources/Views/SupplyChain/CapacityPlans/";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    // One resource key per published Capacity error code (16; sandop-capacity.openapi.yaml, pack §23.8).
    private static readonly string[] ErrorKeys =
    [
        "ErrInvalidRequest", "ErrInvalidCorrelationId", "ErrUnauthenticated", "ErrForbidden", "ErrUnknownCapacityPlan",
        "ErrUnknownCapacityScenario", "ErrUnknownCapacityEvaluation", "ErrCapacityPlanAlreadyExists",
        "ErrCapacityScenarioNameConflict", "ErrCapacityPlanStateConflict", "ErrEvaluationAlreadyActive",
        "ErrIdempotencyKeyReused", "ErrInvalidDemandReference", "ErrInvalidConstraintReference", "ErrDependencyUnavailable",
        "ErrCommitResultUnresolved"
    ];

    [Fact]
    public void Create_plan_form_has_exactly_the_seven_schema_fields_without_client_tightening()
    {
        var form = Read(Views + "_CreatePlanOffcanvas.cshtml");
        var ids = Regex.Matches(form, @"<(input|select|textarea)\b[^>]*\bid=""(?<id>[^""]+)""").Select(m => m.Groups["id"].Value).ToArray();
        Assert.Equal(new[]
        {
            "planName", "planHorizonStart", "planHorizonEnd", "planDemandPlanId", "planDemandPlanVersion",
            "planSourceCapturedAt", "planSourceChecksum"
        }, ids);
        AssertNoTightening(form);
        Assert.Equal(7, Regex.Matches(form, @"class=""diten-field""").Count);
        Assert.Equal(7, Regex.Matches(form, @"diten-field-icon").Count);
        Assert.Equal(7, Regex.Matches(form, @"js-create-required").Count);
        Assert.Contains("id=\"createRequiredProgress\"", form);
        Assert.DoesNotContain("value=", form); // nothing prefilled: first-open progress is 0/7
    }

    [Fact]
    public void Create_scenario_form_has_name_and_two_ordered_repeaters_with_a_decimal_text_delta()
    {
        var form = Read(Views + "_CreateScenarioOffcanvas.cshtml");
        foreach (var id in new[] { "scenarioName", "scenarioConstraintRefRows", "scenarioAdjustmentRows", "btnAddConstraintRef", "btnAddAdjustment" })
        {
            Assert.Contains($"id=\"{id}\"", form);
        }

        foreach (var cls in new[] { "js-constraint-id", "js-constraint-source", "js-constraint-version", "js-resource-ref", "js-period", "js-delta", "js-uom", "js-remove-row" })
        {
            Assert.Contains(cls, form);
        }

        Assert.Contains("<template id=\"constraintRefRowTemplate\">", form);
        Assert.Contains("<template id=\"adjustmentRowTemplate\">", form);
        Assert.Matches(@"<input type=""text"" inputmode=""decimal""[^>]*js-delta", form);
        Assert.DoesNotContain("type=\"number\"", form); // decimals are strings, never floats
        AssertNoTightening(form);
    }

    [Fact]
    public void Evaluate_form_selects_the_mode_and_repeats_resource_refs()
    {
        var form = Read(Views + "_EvaluateOffcanvas.cshtml");
        Assert.Matches(@"<select id=""evaluationMode""", form);
        Assert.Contains("id=\"evaluateResourceRefRows\"", form);
        Assert.Contains("<template id=\"resourceRefRowTemplate\">", form);
        Assert.DoesNotContain("showConfirm", form); // not a dangerous action (pack §24)
        AssertNoTightening(form);
    }

    [Theory]
    [InlineData("Index.cshtml")]
    [InlineData("Details.cshtml")]
    [InlineData("_CreatePlanOffcanvas.cshtml")]
    [InlineData("_CreateScenarioOffcanvas.cshtml")]
    [InlineData("_EvaluateOffcanvas.cshtml")]
    [InlineData("_IndexL10n.cshtml")]
    [InlineData("_DetailsL10n.cshtml")]
    public void Every_view_states_the_tenant_shell_and_has_no_inline_handler(string file)
    {
        var view = Read(Views + file);
        Assert.Contains("Layout = \"_LayoutTenantShell\";", view);
        Assert.DoesNotMatch(new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase), view);
        Assert.DoesNotContain("/Platform", view);
    }

    [Theory]
    [InlineData("Index.cshtml")]
    [InlineData("Details.cshtml")]
    public void Pages_without_read_render_only_the_shared_denial_inside_the_shell(string file)
    {
        var view = Read(Views + file);
        var gate = view.IndexOf("if (!Perms.Has(CapacityUiPermissions.Read))", StringComparison.Ordinal);
        var denial = view.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal);
        var stop = view.IndexOf("return;", StringComparison.Ordinal);
        Assert.True(gate > 0 && denial > gate && stop > denial, "UAS-001 gate must come first and return after _AccessDenied.");
        Assert.DoesNotContain("<table", view[..stop]);
        Assert.DoesNotContain("<form", view[..stop]);
    }

    [Fact]
    public void Entry_page_has_no_table_and_no_list()
    {
        var view = Read(Views + "Index.cshtml");
        Assert.DoesNotContain("<table", view);
        Assert.DoesNotContain("data-dt-standard", view);
        Assert.Contains("id=\"openPlanId\"", view);
    }

    [Fact]
    public void Workspace_has_one_bounded_table_distinct_states_and_a_manual_refresh()
    {
        var view = Read(Views + "Details.cshtml");
        Assert.Equal(1, Regex.Matches(view, @"data-dt-standard=""v2""").Count);
        Assert.Contains("id=\"dt-capacity-bottlenecks\"", view);
        foreach (var section in new[] { "summary", "scenario", "evaluation" })
        {
            Assert.Contains($"id=\"{section}-skeleton\"", view);
            Assert.Contains($"id=\"{section}-error\"", view);
            Assert.Contains($"id=\"{section}-content\"", view);
        }

        Assert.Contains("id=\"scenario-empty\"", view);
        Assert.Contains("id=\"evaluation-empty\"", view);
        Assert.Contains("id=\"openScenarioId\"", view);
        Assert.Contains("id=\"btnRefreshEvaluation\"", view);
        Assert.DoesNotContain("Loading", view); // no spinner or "Loading" text (VIEW-001 §3.1)
    }

    [Theory]
    [InlineData("CapacityPlansIndex")]
    [InlineData("CapacityPlanDetails")]
    public void Resources_have_seven_languages_with_key_parity_and_no_empty_or_echo_values(string marker)
    {
        var sets = Languages.ToDictionary(lang => lang, lang => LoadResx(Resources + $"{marker}.{lang}.resx"));
        var english = sets["en"].Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var (lang, values) in sets)
        {
            Assert.True(english.SetEquals(values.Keys), $"{marker}.{lang}.resx key set differs from en.");
            Assert.All(values, entry =>
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"{marker}.{lang}: {entry.Key} is empty.");
                Assert.NotEqual(entry.Key, entry.Value);
            });
        }

        Assert.All(ErrorKeys, key => Assert.Contains(key, english));
        foreach (var status in new[] { "Draft", "Evaluating", "Ready", "Approved", "Archived" })
        {
            Assert.Contains($"PlanStatus.{status}", english);
        }
    }

    [Fact]
    public void Details_resources_localize_every_scenario_status_evaluation_status_and_mode()
    {
        var keys = LoadResx(Resources + "CapacityPlanDetails.en.resx").Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var status in new[] { "Draft", "Evaluating", "Evaluated", "Archived" }) Assert.Contains($"ScenarioStatus.{status}", keys);
        foreach (var status in new[] { "Accepted", "Running", "Completed", "Failed" }) Assert.Contains($"EvaluationStatus.{status}", keys);
        Assert.Contains("EvaluationMode.Finite", keys);
        Assert.Contains("EvaluationMode.Infinite", keys);
    }

    [Theory]
    [InlineData("_IndexL10n.cshtml", "CapacityPlansIndex")]
    [InlineData("_DetailsL10n.cshtml", "CapacityPlanDetails")]
    public void Json_bridge_exposes_every_resource_key_once(string bridge, string marker)
    {
        var view = Read(Views + bridge);
        var bridged = Regex.Matches(view, @"(?<!Shared)Localizer\[""(?<key>[^""]+)""\]").Select(m => m.Groups["key"].Value).ToArray();
        Assert.Equal(bridged.Length, bridged.Distinct(StringComparer.Ordinal).Count());
        Assert.True(LoadResx(Resources + $"{marker}.en.resx").Keys.ToHashSet(StringComparer.Ordinal).SetEquals(bridged));
    }

    private static void AssertNoTightening(string form)
    {
        Assert.DoesNotMatch(new Regex(@"\s(required|pattern|maxlength|minlength)(=|\s|>)", RegexOptions.IgnoreCase), form);
    }

    private static Dictionary<string, string> LoadResx(string path) =>
        XDocument.Load(Path.Combine(Root(), path)).Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
