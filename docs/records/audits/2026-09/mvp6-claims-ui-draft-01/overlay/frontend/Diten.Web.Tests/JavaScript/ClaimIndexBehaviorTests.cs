using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

// MOD-0187 Claims — static behaviour contract of index.js / index.l10n.js (pack §32.3–§32.9; CU-01, CU-02, CU-04,
// CU-05, CU-09, CU-13, CU-19, CU-22, CU-26, CU-31, CU-SCR-01…05). Runtime behaviour is covered by the Playwright
// scenarios for the Mac lane (Q64b). DRAFT overlay — not built, not run.
public sealed class ClaimIndexBehaviorTests
{
    private readonly string _script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Claims/index.js");
    private readonly string _l10n = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Claims/index.l10n.js");

    [Fact]
    public void Uses_only_the_same_origin_adapter()
    {
        Assert.Contains("const endpoint = '/SupplyChain/Claims/api';", _script);
        Assert.DoesNotContain(":5000", _script);
        Assert.DoesNotContain(":5061", _script);
        Assert.DoesNotContain("/api/shipment-bundle", _script);
        Assert.DoesNotContain("Authorization", _script);
        Assert.DoesNotContain("X-Tenant-Id", _script);
        Assert.DoesNotContain("X-Legal-Entity-Id", _script);
        Assert.DoesNotContain("localStorage", _script);
        Assert.DoesNotContain("sessionStorage", _script);
    }

    [Fact]
    public void List_query_is_limited_to_shipmentId_and_single_status()
    {
        var sets = Regex.Matches(_script, @"params\.set\('(?<name>\w+)'").Select(m => m.Groups["name"].Value).ToHashSet();
        Assert.Equal(new HashSet<string> { "shipmentId", "status" }, sets);
        Assert.DoesNotContain("pageSize", _script);
        Assert.DoesNotContain("multiple", Read("frontend/Diten.Web/Views/SupplyChain/Claims/_Filter.cshtml"));
    }

    [Fact]
    public void Uses_bounded_datatables_v2_profile()
    {
        Assert.Contains("new DataTable(tableEl, window.DtDefaults.create({", _script);
        Assert.Contains("serverSide: false", _script);
        Assert.Contains("stateSave: false", _script);
        Assert.Contains("personalizationClient", _script);
        Assert.DoesNotContain("dt-checkboxes", _script);
        Assert.DoesNotContain("createCrudTable", _script); // no bulk wiring
        Assert.Contains("dt-export-collection-btn", _script); // export collection is filtered out
    }

    [Fact]
    public void Omits_unsupported_surfaces()
    {
        foreach (var token in new[] { "method: 'DELETE'", "method: 'PUT'", "method: 'PATCH'", "/bulk", "/upload", "/import", "/Details/", "js-edit-item", "delete-record" })
        {
            Assert.DoesNotContain(token, _script);
        }
    }

    [Fact]
    public void Quick_view_reads_the_loaded_row_and_never_fetches_by_id()
    {
        var quickView = Slice(_script, "const openQuickView", "// ─── Create");
        Assert.DoesNotContain("fetch(", quickView);
        Assert.Contains("rowsById.get(claimId)", quickView);
        Assert.Contains("textContent", quickView);
    }

    [Fact]
    public void Create_body_is_built_from_typed_text_in_contract_order()
    {
        var build = Slice(_script, "const buildCreateBody", "const submitCreate");
        Assert.DoesNotContain(".trim(", build);
        Assert.DoesNotContain("toUpperCase", build);
        Assert.DoesNotContain("parseFloat", build);
        Assert.DoesNotContain("Number(", build);
        Assert.True(build.IndexOf("shipmentId", StringComparison.Ordinal) < build.IndexOf("carrierId", StringComparison.Ordinal));
        Assert.True(build.IndexOf("reasonCode", StringComparison.Ordinal) < build.IndexOf("claimedAmount", StringComparison.Ordinal));
        Assert.True(build.IndexOf("currency", StringComparison.Ordinal) < build.IndexOf("evidenceReferenceIds", StringComparison.Ordinal));
        Assert.Contains("if (evidence.length) body.evidenceReferenceIds = evidence;", build);
    }

    [Fact]
    public void Resolve_has_a_stale_response_guard()
    {
        var resolve = Slice(_script, "const resolveShipment", "const closeCreateAsForbidden");
        Assert.Contains("const isCurrent = () => sequence === resolveSequence && input.value === requested;", resolve);
        Assert.True(Regex.Matches(resolve, @"if \(!isCurrent\(\)\) return;").Count >= 3);
    }

    [Fact]
    public void Intent_keeps_key_and_body_for_retry_and_blocks_after_409()
    {
        Assert.Contains("createIntent.bodyText !== bodyText", _script);
        Assert.Contains("transitionIntent.bodyText !== bodyText", _script);
        Assert.Contains("'Idempotency-Key': key", _script);
        Assert.Contains("intent.blocked = true;", _script);
        Assert.Contains("if (createPending)", _script);
        Assert.Contains("if (transitionPending)", _script);
    }

    [Fact]
    public void Transition_sends_approved_amount_only_for_approved_and_confirms_through_the_shared_wrapper()
    {
        var build = Slice(_script, "const buildTransitionBody", "const closeTransition");
        Assert.Contains("if (target === 'Approved') body.approvedAmount", build);
        Assert.Contains("window.showConfirm?.(", _script);
        Assert.DoesNotContain("Swal.fire", _script);
        Assert.DoesNotMatch(new Regex(@"(?<![\w.])(alert|confirm|prompt)\("), _script.Replace("showConfirm", string.Empty));
        Assert.DoesNotContain("onclick=", _script);
        Assert.Contains("shipmentId=${encodeURIComponent(context.shipmentId)}", _script);
    }

    [Fact]
    public void Amounts_render_as_exact_ltr_text()
    {
        Assert.Contains("return isText(value) ? ltr(value) : notProvided(); // exact wire text", _script);
        Assert.DoesNotContain("toLocaleString", _script);
        Assert.DoesNotContain("Intl.NumberFormat", _script);
    }

    [Fact]
    public void Every_annex_code_is_mapped_to_a_localized_key()
    {
        foreach (var code in new[]
                 {
                     "INVALID_REQUEST", "UNAUTHENTICATED", "FORBIDDEN", "CLAIM_NOT_FOUND", "UNSUPPORTED_MEDIA_TYPE",
                     "CLAIM_SHIPMENT_INELIGIBLE", "CLAIM_CARRIER_MISMATCH", "CLAIM_AMOUNT_INVALID", "CLAIM_APPROVAL_AMOUNT_INVALID",
                     "CLAIM_APPROVED_AMOUNT_NOT_ALLOWED", "INVALID_CLAIM_TRANSITION", "CLAIM_CORRELATION_MISMATCH", "IDEMPOTENCY_KEY_REUSED",
                     "CLAIM_REFERENCE_INVALID", "CLAIM_REFERENCE_INCOMPLETE", "CLAIM_REFERENCE_UNAVAILABLE", "CLAIM_STORAGE_UNAVAILABLE",
                     "INTERNAL_ERROR"
                 })
        {
            Assert.Matches(new Regex($@"\b{code}: 'Err\w+'"), _script);
        }
    }

    [Fact]
    public void No_hard_coded_user_text_in_script()
    {
        // Visible text comes from window.L10n only; string literals passed to showToast/showConfirm are forbidden.
        Assert.DoesNotMatch(new Regex(@"showToast\?\.\('[A-Za-z]"), _script);
        Assert.DoesNotMatch(new Regex(@"showConfirm\?\.\('[A-Za-z]"), _script);
        Assert.DoesNotMatch(new Regex(@"textContent = '[A-Za-z]"), _script);
        Assert.Contains("const requiredKeys", _l10n);
        Assert.Contains("'ErrInternalError'", _l10n);
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
