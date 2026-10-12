using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

// MOD-0192 Capacity — static behaviour contract of index.js / details.js / *.l10n.js (pack §23.3–§23.9, F192-LIST,
// F192-POLL). Runtime behaviour is covered by the Playwright scenarios for the Mac lane (Q88b).
// DRAFT overlay — not built, not run.
public sealed class CapacityPlanDetailsBehaviorTests
{
    private const string Js = "frontend/Diten.Web/wwwroot/assets/js/SupplyChain/CapacityPlans/";
    private readonly string _index = Read(Js + "index.js");
    private readonly string _details = Read(Js + "details.js");
    private static readonly string[] PublishedCodes =
    [
        "INVALID_REQUEST", "INVALID_CORRELATION_ID", "UNAUTHENTICATED", "FORBIDDEN", "UNKNOWN_CAPACITY_PLAN",
        "UNKNOWN_CAPACITY_SCENARIO", "UNKNOWN_CAPACITY_EVALUATION", "CAPACITY_PLAN_ALREADY_EXISTS",
        "CAPACITY_SCENARIO_NAME_CONFLICT", "CAPACITY_PLAN_STATE_CONFLICT", "EVALUATION_ALREADY_ACTIVE",
        "IDEMPOTENCY_KEY_REUSED", "INVALID_DEMAND_REFERENCE", "INVALID_CONSTRAINT_REFERENCE", "DEPENDENCY_UNAVAILABLE",
        "COMMIT_RESULT_UNRESOLVED"
    ];

    [Theory]
    [InlineData("index.js")]
    [InlineData("details.js")]
    public void Uses_only_the_same_origin_adapter_and_no_browser_storage(string file)
    {
        var script = Read(Js + file);
        Assert.Contains("/SupplyChain/CapacityPlans/api", script);
        foreach (var forbidden in new[] { ":5000", ":5061", "/api/supply-chain", "Authorization", "X-Tenant-Id", "X-Legal-Entity-Id",
                     "tenantId", "legalEntityId", "localStorage", "sessionStorage", "indexedDB", "document.cookie", "caches.open" })
        {
            Assert.DoesNotContain(forbidden, script);
        }
    }

    [Fact]
    public void Entry_page_makes_no_list_request_and_only_navigates_on_open_by_id()
    {
        // The only fetch on the entry page is the create POST (F192-LIST).
        Assert.Single(Regex.Matches(_index, @"\bfetch\("));
        Assert.Contains("method: 'POST'", _index);
        Assert.Contains("window.location.assign(detailsBase + encodeURIComponent(value))", _index);
        Assert.Contains("if (!UUID_PATTERN.test(value) || value === NIL_UUID)", _index);
    }

    [Fact]
    public void Create_plan_sends_the_seven_schema_fields_and_prefills_nothing()
    {
        var fields = Slice(_index, "const CREATE_FIELDS", "]);");
        foreach (var name in new[] { "'name'", "'horizonStart'", "'horizonEnd'", "'demandPlanId'", "'demandPlanVersion'", "'sourceCapturedAt'", "'sourceChecksum'" })
        {
            Assert.Contains(name, fields);
        }

        Assert.Equal(7, Regex.Matches(fields, @"\['\w+', 'plan\w+'\]").Count);
        Assert.DoesNotContain("toISOString", _index); // sourceCapturedAt is typed, never prefilled (0/7 on first open)
        Assert.Contains("created?.capacityPlanId", _index);
    }

    [Fact]
    public void Workspace_reads_only_the_three_bound_get_operations_and_never_a_list()
    {
        Assert.Contains("getJson(base, 'UNKNOWN_CAPACITY_PLAN')", _details);
        Assert.Contains("getJson(`${base}/scenarios/${encodeURIComponent(scenarioId)}`, 'UNKNOWN_CAPACITY_SCENARIO')", _details);
        Assert.Contains("getJson(`${base}/evaluations/${encodeURIComponent(evaluationId)}`, 'UNKNOWN_CAPACITY_EVALUATION')", _details);
        Assert.Equal(2, Regex.Matches(_details, @"\bfetch\(").Count); // getJson + postIntent
        Assert.DoesNotContain("getJson(`${base}/scenarios`", _details);
        Assert.DoesNotContain("getJson(`${base}/evaluations`", _details);
        foreach (var token in new[] { "method: 'DELETE'", "method: 'PUT'", "method: 'PATCH'", "/status", "/approve", "/archive", "/cancel", "/bulk", "/import", "/export", "?page", "search=" })
        {
            Assert.DoesNotContain(token, _details);
            Assert.DoesNotContain(token, _index);
        }
    }

    [Fact]
    public void Refresh_is_manual_one_get_per_click_with_no_timer()
    {
        foreach (var script in new[] { _index, _details })
        {
            foreach (var token in new[] { "setTimeout", "setInterval", "requestAnimationFrame", "EventSource", "WebSocket" })
            {
                Assert.DoesNotContain(token, script);
            }
        }

        Assert.Contains("const ACTIVE_EVALUATION = new Set(['Accepted', 'Running']);", _details);
        Assert.Contains("const canRefreshNow = () => !closed && evaluation !== null && ACTIVE_EVALUATION.has(evaluation.status);", _details);
        Assert.Contains("if (event.currentTarget.disabled || !canRefreshNow()) return;", _details);
        Assert.Contains("void loadEvaluation(evaluation.evaluationId);", _details);
    }

    [Fact]
    public void Actions_are_gated_by_key_and_loaded_state()
    {
        Assert.Contains("const canCreateScenarioNow = () => permissions.canCreateScenario && !closed && plan !== null;", _details);
        Assert.Contains("permissions.canEvaluate && !closed && plan !== null && scenario !== null", _details);
        Assert.Contains("!evaluationShownActive()", _details);
        Assert.Contains("!evaluationBlocked.has(", _details);
        Assert.DoesNotContain("status:", Slice(_details, "const buildScenarioBody", "const scenarioCheck"));
        Assert.DoesNotContain("status:", Slice(_details, "const buildEvaluateBody", "const evaluateCheck"));
    }

    [Fact]
    public void Bodies_follow_the_published_schemas()
    {
        var scenarioBody = Slice(_details, "const buildScenarioBody", "const scenarioCheck");
        Assert.Contains("constraintRefs: constraintRows().map(", scenarioBody); // always sent, [] when empty
        Assert.Contains("adjustments: adjustmentRows().map(", scenarioBody);
        Assert.Contains("availableCapacityDelta: cell(row, '.js-delta')", scenarioBody); // a JSON string as typed
        Assert.DoesNotContain("Number(", _details);
        Assert.DoesNotContain("parseFloat", _details);
        Assert.Contains("const DECIMAL_PATTERN = /^-?\\d+(\\.\\d+)?$/;", _details);
        Assert.Contains("const EVALUATION_MODES = Object.freeze(['Finite', 'Infinite']);", _details);
        Assert.Contains("resourceRefs: resourceRows().map(", _details);
        Assert.Contains("rows.length === 0", _details);
        Assert.DoesNotContain(".trim(", _details);
        Assert.DoesNotContain(".trim(", _index);
    }

    [Fact]
    public void No_native_dialog_and_no_confirmation_for_non_dangerous_actions()
    {
        foreach (var script in new[] { _index, _details })
        {
            Assert.DoesNotMatch(new Regex(@"(?<![\w.])(alert|confirm|prompt)\("), script);
            Assert.DoesNotContain("Swal.fire", script);
        }
    }

    [Fact]
    public void Idempotency_intents_reuse_key_for_identical_target_and_body_and_block_on_key_reuse()
    {
        Assert.Contains("current.target !== url || current.bodyText !== bodyText", _details);
        Assert.Contains("createIntent.bodyText !== bodyText", _index);
        Assert.Contains("failure.code === 'IDEMPOTENCY_KEY_REUSED'", _details);
        Assert.Contains("failure.code === 'IDEMPOTENCY_KEY_REUSED'", _index);
        Assert.Contains("'Idempotency-Key': key", _details);
        Assert.Contains("failure.code === 'EVALUATION_ALREADY_ACTIVE'", _details);
    }

    [Fact]
    public void Not_found_is_per_resource_and_late_responses_are_ignored()
    {
        Assert.Contains("if (gen !== generation.plan || closed) return;", _details);
        Assert.Contains("if (gen !== generation.scenario || closed) return;", _details);
        Assert.Contains("if (gen !== generation.evaluation || closed) return;", _details);
        Assert.Contains("failure.code === 'UNKNOWN_CAPACITY_PLAN') { closeWorkspace(failure); return true; }", _details);
        Assert.Contains("result.failure.code === 'UNKNOWN_CAPACITY_SCENARIO'", _details);
        Assert.Contains("result.failure.code === 'UNKNOWN_CAPACITY_EVALUATION'", _details);
        Assert.Contains("workspace.hidden = true;", _details);
    }

    [Theory]
    [InlineData("index.js")]
    [InlineData("details.js")]
    public void Every_published_error_code_is_localized(string file)
    {
        var script = Read(Js + file);
        Assert.All(PublishedCodes, code => Assert.Matches(new Regex($@"\b{code}: 'Err\w+'"), script));
    }

    [Theory]
    [InlineData("index.js")]
    [InlineData("details.js")]
    public void No_hard_coded_user_text(string file)
    {
        var script = Read(Js + file);
        Assert.DoesNotMatch(new Regex(@"showToast\?\.\('[A-Za-z]"), script);
        Assert.DoesNotMatch(new Regex(@"textContent = '[A-Za-z]"), script);
        Assert.DoesNotMatch(new Regex(@"textContent = `[A-Za-z]"), script);
    }

    [Theory]
    [InlineData("index.l10n.js", "capacity-index-l10n")]
    [InlineData("details.l10n.js", "capacity-details-l10n")]
    public void L10n_normalizer_warns_and_never_substitutes(string file, string payloadId)
    {
        var script = Read(Js + file);
        Assert.Contains($"document.getElementById('{payloadId}')", script);
        Assert.Contains("const requiredKeys", script);
        Assert.Contains("'ErrEvaluationAlreadyActive'", script);
        Assert.Contains("[L10N WARNING] Missing localization key", script);
    }

    private static string Slice(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        var to = text.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, end);
        return text[from..to];
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
