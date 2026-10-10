using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

// MOD-0190 S&OP — static behaviour contract of index.js / details.js / *.l10n.js (pack §23.3–§23.9; SU-01, SU-02,
// SU-05, SU-06…SU-09, SU-11…SU-16, SU-SCR-01…07). Runtime behaviour is covered by the Playwright scenarios for the Mac
// lane (Q84b). DRAFT overlay — not built, not run.
public sealed class SandopPlanDetailsBehaviorTests
{
    private const string Js = "frontend/Diten.Web/wwwroot/assets/js/SupplyChain/SandopPlans/";
    private readonly string _index = Read(Js + "index.js");
    private readonly string _details = Read(Js + "details.js");
    private static readonly string[] PublishedCodes =
    [
        "INVALID_REQUEST", "INVALID_CORRELATION_ID", "UNAUTHENTICATED", "FORBIDDEN", "UNKNOWN_SANDOP_PLAN",
        "SANDOP_PLAN_ALREADY_EXISTS", "SANDOP_PLAN_STATE_CONFLICT", "SANDOP_SIGN_OFF_STATE_CONFLICT", "SIGN_OFF_ALREADY_RECORDED",
        "IDEMPOTENCY_KEY_REUSED", "INVALID_DEMAND_REFERENCE", "INVALID_SNAPSHOT_REFERENCE", "DEPENDENCY_UNAVAILABLE",
        "COMMIT_RESULT_UNRESOLVED"
    ];

    [Theory]
    [InlineData("index.js")]
    [InlineData("details.js")]
    public void Uses_only_the_same_origin_adapter_and_no_browser_storage(string file)
    {
        var script = Read(Js + file);
        Assert.Contains("/SupplyChain/SandopPlans/api", script);
        foreach (var forbidden in new[] { ":5000", ":5061", "/api/supply-chain", "Authorization", "X-Tenant-Id", "X-Legal-Entity-Id",
                     "localStorage", "sessionStorage", "indexedDB", "document.cookie" })
        {
            Assert.DoesNotContain(forbidden, script);
        }
    }

    [Fact]
    public void Entry_page_makes_no_list_request_and_only_navigates_on_open_by_id()
    {
        // The only fetch on the entry page is the create POST (SU-02, F190-LIST).
        Assert.Single(Regex.Matches(_index, @"\bfetch\("));
        Assert.Contains("method: 'POST'", _index);
        Assert.Contains("window.location.assign(detailsBase + encodeURIComponent(value))", _index);
        Assert.Contains("if (!UUID_PATTERN.test(value))", _index);
    }

    [Fact]
    public void Workspace_reads_only_plan_snapshots_and_sign_offs()
    {
        Assert.Contains("getJson(base)", _details);
        Assert.Contains("'snapshots', 'snapshots'", _details);
        Assert.Contains("'signoffs', 'sign-offs'", _details);
        foreach (var token in new[] { "method: 'DELETE'", "method: 'PUT'", "method: 'PATCH'", "/status", "/transition", "/bulk", "/import", "/export", "?page", "search=" })
        {
            Assert.DoesNotContain(token, _details);
            Assert.DoesNotContain(token, _index);
        }
    }

    [Fact]
    public void Tables_use_the_bounded_client_side_v2_profile()
    {
        Assert.Contains("window.DtDefaults.create({", _details);
        Assert.Contains("serverSide: false", _details);
        Assert.Contains("stateSave: false", _details);
        Assert.Contains("buttons: []", _details);
        foreach (var token in new[] { "colvis", "exportButtons", "personalizationClient", "dt-checkboxes", "QuickView" })
        {
            Assert.DoesNotContain(token, _details);
        }
    }

    [Fact]
    public void Actions_are_gated_by_key_and_status_and_never_change_plan_status()
    {
        Assert.Contains("const CAPTURE_STATUSES = new Set(['Draft', 'InReview']);", _details);
        Assert.Contains("const SIGN_OFF_STATUSES = new Set(['InReview']);", _details);
        Assert.Contains("permissions.canCapture && !closed && plan !== null && CAPTURE_STATUSES.has(plan.status)", _details);
        Assert.Contains("snapshots.some(", _details);
        Assert.DoesNotContain("status:", Slice(_details, "const buildCaptureBody", "const captureCheck"));
        Assert.DoesNotContain("status:", Slice(_details, "const buildSignOffBody", "const signOffCheck"));
    }

    [Fact]
    public void Bodies_follow_the_published_schemas()
    {
        Assert.Contains("supplyInputRefs: supplyRows().map(", _details);            // always sent, [] when empty
        Assert.Contains("if (comment !== '') body.comment = comment;", _details);  // optional, omitted when empty
        Assert.Contains("const ROLES = Object.freeze(['DemandPlanning', 'SupplyPlanning', 'Finance', 'Operations', 'Executive']);", _details);
        Assert.Contains("const DECISIONS = Object.freeze(['Approved', 'Rejected']);", _details);
        Assert.Contains("const COMMENT_MAX = 2000;", _details);
        Assert.DoesNotContain(".trim(", _details);
        Assert.DoesNotContain(".trim(", _index);
    }

    [Fact]
    public void Sign_off_confirmation_uses_showConfirm_and_no_native_dialog()
    {
        Assert.Contains("window.showConfirm?.(t('RecordSignOffTitle')", _details);
        foreach (var script in new[] { _index, _details })
        {
            Assert.DoesNotMatch(new Regex(@"(?<![\w.])(alert|confirm|prompt)\("), script);
            Assert.DoesNotContain("Swal.fire", script);
        }
    }

    [Fact]
    public void Idempotency_intents_reuse_key_for_identical_body_and_block_on_key_reuse()
    {
        Assert.Contains("intents[kind].bodyText !== bodyText", _details);
        Assert.Contains("createIntent.bodyText !== bodyText", _index);
        Assert.Contains("failure.code === 'IDEMPOTENCY_KEY_REUSED'", _details);
        Assert.Contains("failure.code === 'IDEMPOTENCY_KEY_REUSED'", _index);
        Assert.Contains("'Idempotency-Key': key", _details);
    }

    [Fact]
    public void Safe_not_found_removes_the_workspace_and_ignores_late_responses()
    {
        Assert.Contains("if (gen !== generation || closed) return;", _details);
        Assert.Contains("failure.status === 404 || failure.status === 403", _details);
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
        Assert.DoesNotMatch(new Regex(@"showConfirm\?\.\('[A-Za-z]"), script);
        Assert.DoesNotMatch(new Regex(@"textContent = '[A-Za-z]"), script);
    }

    [Theory]
    [InlineData("index.l10n.js", "sandop-index-l10n")]
    [InlineData("details.l10n.js", "sandop-details-l10n")]
    public void L10n_normalizer_warns_and_never_substitutes(string file, string payloadId)
    {
        var script = Read(Js + file);
        Assert.Contains($"document.getElementById('{payloadId}')", script);
        Assert.Contains("const requiredKeys", script);
        Assert.Contains("'ErrCommitResultUnresolved'", script);
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
