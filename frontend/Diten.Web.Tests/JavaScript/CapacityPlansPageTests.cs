using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

// MOD-0192 Capacity tenant UI (G6, 2026-10-18) — the page contract measured against the production files: the bridge
// and its loader list the same keys (F-1), the seven resx files carry the same complete keys, every key a script reads
// is bridged, the error map covers every code the adapter can answer, every workspace call is scoped and a missing scope
// fails closed, decimals stay strings, there is no polling, and the views draw nothing without permission (UAS-001).
public sealed class CapacityPlansPageTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly HashSet<string> SameWord = new(StringComparer.Ordinal) { "fr:ConstraintSource", "fr:ConstraintVersion" };
    private readonly string _bridge = Read("frontend/Diten.Web/Views/SupplyChain/CapacityPlans/_IndexL10n.cshtml");
    private readonly string _loader = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/CapacityPlans/index.l10n.js");
    private readonly string _index = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/CapacityPlans/index.js");
    private readonly string _details = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/CapacityPlans/details.js");
    private readonly string _indexView = Read("frontend/Diten.Web/Views/SupplyChain/CapacityPlans/Index.cshtml");
    private readonly string _detailsView = Read("frontend/Diten.Web/Views/SupplyChain/CapacityPlans/Details.cshtml");

    private IReadOnlyList<string> BridgeKeys => Regex.Matches(_bridge, "\\[\"([A-Za-z]+)\"\\] = ").Select(m => m.Groups[1].Value).ToList();
    private IReadOnlyList<string> ModuleBridgeKeys => Regex.Matches(_bridge, "\\[\"([A-Za-z]+)\"\\] = Localizer\\[").Select(m => m.Groups[1].Value).ToList();

    [Fact]
    public void Bridge_and_loader_list_exactly_the_same_keys()
    {
        var required = Regex.Match(_loader, @"const requiredKeys = \[(.*?)\];", RegexOptions.Singleline).Groups[1].Value;
        var loaderKeys = Regex.Matches(required, "'([A-Za-z]+)'").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(BridgeKeys, loaderKeys);
        Assert.Equal(BridgeKeys.Count, BridgeKeys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Seven_resources_carry_every_module_key_with_a_real_value()
    {
        var english = Resx("en");
        foreach (var language in Languages)
        {
            var rows = Resx(language);
            Assert.Equal(english.Keys.OrderBy(k => k, StringComparer.Ordinal), rows.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.All(ModuleBridgeKeys, key => Assert.False(string.IsNullOrWhiteSpace(rows.GetValueOrDefault(key)), $"{language}: {key}"));
            Assert.All(rows, row => Assert.NotEqual(row.Key, row.Value.Trim()));
            // No English placeholder; identical only where the word is genuinely the same (fr "Source", "Version").
            if (language != "en") Assert.All(rows.Where(row => !SameWord.Contains($"{language}:{row.Key}")), row => Assert.NotEqual(english[row.Key], row.Value));
        }
    }

    [Fact]
    public void Every_key_a_script_reads_is_bridged()
    {
        var used = Regex.Matches(_index + _details, @"\bt\('([A-Za-z]+)'\)").Select(m => m.Groups[1].Value)
            .Concat(Regex.Matches(_index + _details, @"'(Err[A-Za-z]+|Status[A-Z][A-Za-z]+|Mode[A-Z][A-Za-z]+)'").Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.Contains(key, BridgeKeys));
    }

    [Fact]
    public void The_error_map_covers_every_code_the_adapter_can_answer()
    {
        var published = (HashSet<string>)typeof(SupplyChainCapacityPlansController)
            .GetField("PublishedCodes", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        foreach (var script in new[] { _index, _details })
        {
            Assert.All(published, code => Assert.Matches(new Regex($"\\b{code}: 'Err[A-Za-z]+'"), script));
        }
    }

    [Fact]
    public void Every_workspace_call_is_scoped_and_a_missing_scope_fails_closed()
    {
        Assert.Contains("const legalEntityId = host?.dataset.legalEntityId || ''", _details);
        Assert.Contains("const scoped = (path) => `${path}?legalEntityId=${encodeURIComponent(legalEntityId)}`", _details);
        Assert.Contains("const get = (path) => fetch(scoped(path), { credentials: 'same-origin', headers: traceHeaders() });", _details);
        Assert.Contains("await fetch(scoped(path), { method: 'POST'", _details);
        Assert.Equal(2, Regex.Matches(_details, @"\bfetch\(").Count);
        Assert.Contains("if (!UUID_PATTERN.test(legalEntityId)) { hideSkeleton(); document.getElementById('planName').textContent = t('WorkspaceTitle'); showAlert('workspaceAlert', t('LegalEntityRequired')); return; }", _details);
        Assert.Contains("data-legal-entity-id=\"@ViewBag.LegalEntityId\"", _detailsView);
        Assert.Contains("`${endpoint}/Details/${encodeURIComponent(planId)}?legalEntityId=${encodeURIComponent(legalEntityId)}`", _index);
        Assert.Contains("fetch(withScope(`${endpoint}/api`, legalEntityId)", _index);
        // The opened scenario and evaluation ride the URL with the scope, so a reload or a shared link keeps all three.
        Assert.Contains("const params = new URLSearchParams({ legalEntityId });", _details);
    }

    [Fact]
    public void Decimals_stay_strings_and_there_is_no_polling()
    {
        Assert.Contains("const decimal = (text) => (isText(text) && text !== '' ? ltr(text, 'font-monospace') : notProvided());", _details);
        // Bottlenecks (DataTables v2, G8 M1): each measure is the escaped returned string, right-aligned, never converted.
        Assert.Contains("text('requiredCapacity', 'text-end'), text('availableCapacity', 'text-end'),", _details);
        Assert.Contains("text('shortfall', 'text-end')", _details);
        Assert.Contains("render: (_d, _t, row) => ltrHtml(row[key], className === 'text-end' ? 'font-monospace' : '')", _details);
        Assert.Contains("cell(decimal(a.availableCapacityDelta), 'text-end')", _details);
        foreach (var forbidden in new[] { "parseFloat", "Number(", "toFixed", "setInterval", "setTimeout" })
            Assert.DoesNotContain(forbidden, _details);
        Assert.Contains("document.getElementById('btnRefreshEvaluation')?.addEventListener('click'", _details);
    }

    [Fact]
    public void One_idempotency_key_per_opened_form_and_a_reused_key_blocks_it()
    {
        Assert.Contains("createIntent = { key: uuid(), blocked: false };", _index);
        Assert.Contains("mutationHeaders(createIntent.key)", _index);
        Assert.Contains("intents.scenario = { key: uuid(), blocked: false };", _details);
        Assert.Contains("intents.evaluate = { key: uuid(), blocked: false };", _details);
        Assert.Contains("headers: mutationHeaders(intent.key)", _details);
        Assert.Contains("if (failure.code === 'IDEMPOTENCY_KEY_REUSED') intent.blocked = true;", _details);
        Assert.Contains("if (failure.code === 'IDEMPOTENCY_KEY_REUSED') createIntent.blocked = true;", _index);
    }

    [Fact]
    public void Late_answers_never_overwrite_a_newer_load_and_server_text_is_never_html()
    {
        foreach (var counter in new[] { "planVersion", "scenarioVersion", "evaluationVersion" })
        {
            Assert.Contains($"const version = ++{counter};", _details);
            Assert.Contains($"if (version !== {counter}) return;", _details);
        }
        Assert.DoesNotContain("innerHTML", _details);
        Assert.DoesNotContain("innerHTML", _index);
    }

    [Fact]
    public void Browser_holds_no_token_no_scope_header_and_no_direct_service_url()
    {
        foreach (var script in new[] { _index, _details })
        {
            Assert.DoesNotContain("document.cookie", script);
            Assert.DoesNotContain("Authorization", script);
            Assert.DoesNotContain("X-Legal-Entity-Id", script);
            Assert.DoesNotContain("X-Tenant-Id", script);
            Assert.DoesNotContain("localStorage", script);
            Assert.DoesNotContain(":5000", script);
            Assert.DoesNotContain(":5066", script);
            Assert.DoesNotContain("/api/supply-chain/", script);
        }
    }

    [Fact]
    public void Without_read_the_views_draw_only_the_denied_card()
    {
        foreach (var view in new[] { _indexView, _detailsView })
        {
            var gate = view.IndexOf("@if (!Perms.Has(CapacityUiPermissions.Read))", StringComparison.Ordinal);
            Assert.True(gate > 0);
            Assert.Contains("<partial name=\"_AccessDenied\"", view[gate..view.IndexOf("return;", gate, StringComparison.Ordinal)]);
            // Nothing of the page (skeleton, surface, script) precedes the gate.
            Assert.DoesNotContain("<div", view[..gate]);
        }
    }

    // G8 M1: the Bottlenecks table is the bounded DataTables v2 profile of pack §23.5.
    [Fact]
    public void Bottlenecks_are_the_bounded_DataTables_v2_profile()
    {
        var panel = Read("frontend/Diten.Web/Views/SupplyChain/CapacityPlans/_EvaluationPanel.cshtml");
        Assert.Contains("<table id=\"dt-capacity-bottlenecks\" data-dt-standard=\"v2\"", panel);
        Assert.Contains("window.DtDefaults.create({", _details);
        Assert.Contains("data: [], order: [[1, 'asc']], stateSave: false, serverSide: false,", _details);
        Assert.Contains("config.language.emptyTable = t('NoBottlenecks');", _details);   // after create() (G8 NL-1)
        Assert.DoesNotContain("buttons:", _details);
    }

    // G8 M2: no "Loading…" title survives a failed or denied first load.
    [Fact]
    public void A_failed_or_denied_first_load_leaves_no_loading_title()
    {
        Assert.Contains("<div id=\"workspaceHeader\"", _detailsView);
        Assert.Contains("setVisible(document.getElementById('workspaceHeader'), false);", _details);
        Assert.Contains("document.getElementById('planName').textContent = t('WorkspaceTitle');\n        showAlert('workspaceAlert', failure);", _details);
    }

    // G8 L5a–d / L6 / §23.4/§23.7/§23.8.
    [Fact]
    public void State_gates_link_ids_and_form_mirrors_follow_the_pack()
    {
        Assert.Contains("evaluate.hidden = !permissions.canEvaluate || !scenario || open || evaluationActiveFor === scenario.scenarioId;", _details);
        Assert.Contains("if (failure.code === 'EVALUATION_ALREADY_ACTIVE') { intent.blocked = true;", _details);
        Assert.Contains("if (failure.code === 'CAPACITY_PLAN_STATE_CONFLICT') await loadPlan();", _details);
        Assert.Contains("if (urlState.invalid) showAlert('workspaceAlert', t('InvalidLinkId'));", _details);
        Assert.Contains("['evaluationId', 'evaluationStatus', 'evaluationMode', 'evaluationSubmitted', 'evaluationCompleted']", _details);
        Assert.Contains("const DECIMAL = /^-?\\d+(\\.\\d+)?$/;", _details);
    }

    private static Dictionary<string, string> Resx(string language) =>
        XDocument.Load(Path.Combine(Root(), $"frontend/Diten.Web/Resources/Views/SupplyChain/CapacityPlans/CapacityPlansIndex.{language}.resx"))
            .Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
