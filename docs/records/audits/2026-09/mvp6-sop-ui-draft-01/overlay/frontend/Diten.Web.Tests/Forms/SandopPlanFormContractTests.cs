using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Forms;

// MOD-0190 S&OP — form, view and resource contract (pack §23.2, §23.6, §23.9; SU-04, SU-06…SU-09, SU-17).
// Static source checks; runtime behaviour is covered by the Playwright scenarios for the Mac lane (Q84b).
// DRAFT overlay — not built, not run.
public sealed class SandopPlanFormContractTests
{
    private const string Views = "frontend/Diten.Web/Views/SupplyChain/SandopPlans/";
    private const string Resources = "frontend/Diten.Web/Resources/Views/SupplyChain/SandopPlans/";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] ErrorKeys =
    [
        "ErrInvalidRequest", "ErrInvalidCorrelationId", "ErrUnauthenticated", "ErrForbidden", "ErrUnknownSandopPlan",
        "ErrSandopPlanAlreadyExists", "ErrSandopPlanStateConflict", "ErrSandopSignOffStateConflict", "ErrSignOffAlreadyRecorded",
        "ErrIdempotencyKeyReused", "ErrInvalidDemandReference", "ErrInvalidSnapshotReference", "ErrDependencyUnavailable",
        "ErrCommitResultUnresolved"
    ];

    [Fact]
    public void Create_form_has_exactly_the_five_schema_fields_without_client_tightening()
    {
        var form = Read(Views + "_CreateOffcanvas.cshtml");
        var ids = Regex.Matches(form, @"<(input|select|textarea)\b[^>]*\bid=""(?<id>[^""]+)""").Select(m => m.Groups["id"].Value).ToArray();
        Assert.Equal(new[] { "planName", "planHorizonStart", "planHorizonEnd", "planDemandPlanId", "planDemandPlanVersion" }, ids);
        AssertNoTightening(form);
        Assert.Equal(5, Regex.Matches(form, @"class=""diten-field""").Count);
        Assert.Equal(5, Regex.Matches(form, @"diten-field-icon").Count);
        Assert.Contains("id=\"createRequiredProgress\"", form);
    }

    [Fact]
    public void Capture_form_has_four_fields_and_an_ordered_three_part_repeater()
    {
        var form = Read(Views + "_CaptureSnapshotOffcanvas.cshtml");
        foreach (var id in new[] { "captureDemandPlanId", "captureDemandPlanVersion", "captureSourceCapturedAt", "captureSourceChecksum", "captureSupplyRefs" })
        {
            Assert.Contains($"id=\"{id}\"", form);
        }

        foreach (var cls in new[] { "js-ref-source", "js-ref-resource-id", "js-ref-resource-version", "js-remove-supply-ref" })
        {
            Assert.Contains(cls, form);
        }

        Assert.Contains("<template id=\"supplyRefRowTemplate\">", form);
        AssertNoTightening(form);
    }

    [Fact]
    public void Sign_off_form_selects_snapshot_role_decision_and_takes_an_optional_comment()
    {
        var form = Read(Views + "_RecordSignOffOffcanvas.cshtml");
        Assert.Matches(@"<select id=""signOffSnapshotId""", form);
        Assert.Matches(@"<select id=""signOffRole""", form);
        Assert.Matches(@"<select id=""signOffDecision""", form);
        Assert.Matches(@"<textarea id=""signOffComment""", form);
        Assert.DoesNotContain("maxlength", form, StringComparison.OrdinalIgnoreCase); // ≤ 2000 is checked without truncating
        AssertNoTightening(form);
    }

    [Theory]
    [InlineData("Index.cshtml")]
    [InlineData("Details.cshtml")]
    [InlineData("_CreateOffcanvas.cshtml")]
    [InlineData("_CaptureSnapshotOffcanvas.cshtml")]
    [InlineData("_RecordSignOffOffcanvas.cshtml")]
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
        var gate = view.IndexOf("if (!Perms.Has(SandopUiPermissions.Read))", StringComparison.Ordinal);
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
    public void Workspace_tables_use_the_bounded_v2_profile_and_three_distinct_states()
    {
        var view = Read(Views + "Details.cshtml");
        Assert.Equal(2, Regex.Matches(view, @"data-dt-standard=""v2""").Count);
        foreach (var section in new[] { "summary", "snapshots", "signoffs" })
        {
            Assert.Contains($"id=\"{section}-skeleton\"", view);
            Assert.Contains($"id=\"{section}-error\"", view);
        }

        Assert.DoesNotContain("Loading", view); // no spinner or "Loading" text (VIEW-001 §3.1)
    }

    [Theory]
    [InlineData("SandopPlansIndex")]
    [InlineData("SandopPlanDetails")]
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
        foreach (var status in new[] { "Draft", "InReview", "Approved", "Rejected", "Archived" })
        {
            Assert.Contains($"Status.{status}", english);
        }
    }

    [Fact]
    public void Details_resources_localize_every_role_and_decision()
    {
        var keys = LoadResx(Resources + "SandopPlanDetails.en.resx").Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var role in new[] { "DemandPlanning", "SupplyPlanning", "Finance", "Operations", "Executive" })
        {
            Assert.Contains($"Role.{role}", keys);
        }

        Assert.Contains("Decision.Approved", keys);
        Assert.Contains("Decision.Rejected", keys);
    }

    [Theory]
    [InlineData("_IndexL10n.cshtml", "SandopPlansIndex")]
    [InlineData("_DetailsL10n.cshtml", "SandopPlanDetails")]
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
