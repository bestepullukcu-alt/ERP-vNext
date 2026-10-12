using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

// MOD-0186 Returns — static behaviour contract of index.js / index.l10n.js (pack §32.3–§32.9; RU-01…RU-03, RU-08…RU-10,
// RU-16…RU-21, RU-24, RU-29, RU-SCR-01…05). Runtime behaviour is covered by the Playwright scenarios for the Mac lane
// (Q65b). DRAFT overlay — not built, not run.
public sealed class ReturnIndexBehaviorTests
{
    private readonly string _script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.js");
    private readonly string _l10n = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.l10n.js");

    private static readonly string[] PublishedCodes =
    [
        "INVALID_REQUEST", "RETURN_NOT_FOUND", "SHIPMENT_NOT_FOUND", "SHIPMENT_LINE_NOT_FOUND", "INVALID_RETURN_TRANSITION",
        "DISPOSITION_REQUIRED", "INVALID_RETURN_QUANTITY", "RETURN_QUANTITY_EXCEEDED", "RETURN_UOM_MISMATCH", "DUPLICATE_RETURN_LINE",
        "SHIPMENT_NOT_RETURNABLE", "CORRELATION_ROOT_MISMATCH", "IDEMPOTENCY_KEY_REUSED", "RETURN_SOURCE_CHANGED",
        "RETURN_SHIPMENT_ROOT_INVALID", "DEPENDENCY_RESPONSE_INVALID", "RETURN_SHIPMENT_ROOT_UNAVAILABLE",
        "REFERENCE_STATE_UNAVAILABLE", "DEPENDENCY_UNAVAILABLE", "PERSISTENCE_UNAVAILABLE", "INTERNAL_ERROR"
    ];

    [Fact]
    public void Uses_only_the_same_origin_adapter_and_no_browser_storage_or_timer()
    {
        Assert.Contains("const endpoint = '/SupplyChain/Returns/api';", _script);
        foreach (var forbidden in new[] { ":5000", ":5061", "/api/shipment-bundle", "Authorization", "X-Tenant-Id", "X-Legal-Entity-Id",
                     "tenantId", "legalEntityId", "lifecycleCorrelationId", "localStorage", "sessionStorage", "indexedDB", "document.cookie",
                     "setTimeout", "setInterval" })
        {
            Assert.DoesNotContain(forbidden, _script);
        }
    }

    [Fact]
    public void List_query_is_limited_to_shipmentId_and_single_status()
    {
        var sets = Regex.Matches(_script, @"params\.set\('(?<name>\w+)'").Select(m => m.Groups["name"].Value).ToHashSet();
        Assert.Equal(new HashSet<string> { "shipmentId", "status" }, sets);
        Assert.DoesNotContain("pageSize", _script);
        Assert.DoesNotContain("multiple", Read("frontend/Diten.Web/Views/SupplyChain/Returns/_Filter.cshtml"));
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
        foreach (var token in new[] { "method: 'DELETE'", "method: 'PUT'", "method: 'PATCH'", "/bulk", "/upload", "/import", "/Details/",
                     "js-edit-item", "delete-record", "/inventory", "/warehouse", "remaining" })
        {
            Assert.DoesNotContain(token, _script);
        }
    }

    [Fact]
    public void Quick_view_reads_the_loaded_row_and_never_fetches_by_id()
    {
        var quickView = Slice(_script, "const openQuickView", "// ─── Create");
        Assert.DoesNotContain("fetch(", quickView);
        Assert.Contains("rowsById.get(returnId)", quickView);
        Assert.Contains("textContent", quickView);
    }

    [Fact]
    public void Create_body_is_built_from_typed_text_in_contract_order()
    {
        var lines = Slice(_script, "const selectedLines", "const buildCreateBody");
        Assert.Contains("shipmentLineNumber: row.dataset.lineNumber", lines);
        Assert.Contains("quantity: row.querySelector('.js-line-quantity')?.value ?? ''", lines);
        Assert.Contains("uomId: row.dataset.uomId", lines);
        var build = Slice(_script, "const buildCreateBody", "const submitCreate");
        foreach (var forbidden in new[] { ".trim(", "toUpperCase", "parseFloat", "Number(", "toFixed", "replace(" })
        {
            Assert.DoesNotContain(forbidden, build + lines);
        }

        Assert.True(build.IndexOf("shipmentId", StringComparison.Ordinal) < build.IndexOf("reasonCode", StringComparison.Ordinal));
        Assert.True(build.IndexOf("reasonCode", StringComparison.Ordinal) < build.IndexOf("lines:", StringComparison.Ordinal));
        Assert.True(build.IndexOf("lines:", StringComparison.Ordinal) < build.IndexOf("evidenceReferenceIds", StringComparison.Ordinal));
        Assert.Contains("if (evidence.length) body.evidenceReferenceIds = evidence;", build);
    }

    [Fact]
    public void Resolve_has_a_stale_response_guard_and_leaves_no_shipment_data_on_failure()
    {
        var resolve = Slice(_script, "const resolveShipment", "const selectedLines");
        Assert.Contains("const isCurrent = () => sequence === resolveSequence && input.value === requested;", resolve);
        Assert.True(Regex.Matches(resolve, @"if \(!isCurrent\(\)\) return;").Count >= 3);
        var clear = Slice(_script, "const clearResolvedShipment", "const renderLines");
        Assert.Contains("resolveSequence += 1;", clear);
        Assert.Contains("getElementById('returnLinesBody')?.replaceChildren();", clear);
    }

    [Fact]
    public void Line_rows_are_written_with_text_only_and_keep_unique_accessible_names()
    {
        var render = Slice(_script, "const renderLines", "const openCreate");
        Assert.DoesNotContain("innerHTML", render);
        Assert.Contains(".textContent = ", render);
        Assert.Contains("select.id = `returnLineSelect_", render);
        Assert.Contains("quantity.id = `returnLineQuantity_", render);
        Assert.Contains("setAttribute('aria-label'", render);
    }

    [Fact]
    public void Intent_keeps_key_and_body_for_retry_and_blocks_after_key_reuse_or_root_mismatch()
    {
        Assert.Contains("createIntent.bodyText !== bodyText", _script);
        Assert.Contains("transitionIntent.bodyText !== bodyText", _script);
        Assert.Contains("'Idempotency-Key': key", _script);
        Assert.Contains("failure.code === 'IDEMPOTENCY_KEY_REUSED' || failure.code === 'CORRELATION_ROOT_MISMATCH'", _script);
        Assert.Contains("intent.blocked = true;", _script);
        Assert.Contains("if (createPending)", _script);
        Assert.Contains("if (transitionPending)", _script);
        Assert.Contains("result?.idempotentReplay === true", _script);
    }

    [Fact]
    public void Transition_sends_only_the_fields_listed_for_the_target_and_confirms_through_the_shared_wrapper()
    {
        var build = Slice(_script, "const buildTransitionBody", "const closeTransition");
        Assert.Contains("if (target === 'Dispositioned') body.dispositionCode", build);
        Assert.Contains("if (INVENTORY_REFERENCE_TARGETS.has(target))", build);
        Assert.Contains("if (reference !== '') body.inventoryTransactionReferenceId = reference;", build);
        Assert.DoesNotContain(".trim(", build);
        Assert.Contains("const INVENTORY_REFERENCE_TARGETS = new Set(['Received', 'Dispositioned']);", _script);
        Assert.Contains("const DANGEROUS_TARGETS = new Set(['Rejected', 'Cancelled']);", _script);
        Assert.Contains("window.showConfirm?.(", _script);
        Assert.DoesNotContain("Swal.fire", _script);
        Assert.DoesNotMatch(new Regex(@"(?<![\w.])(alert|confirm|prompt)\("), _script.Replace("showConfirm", string.Empty));
        Assert.DoesNotContain("onclick=", _script);
        Assert.Contains("shipmentId=${encodeURIComponent(context.shipmentId)}", _script);
    }

    [Fact]
    public void Lifecycle_and_eligibility_mirror_the_backend()
    {
        Assert.Contains("InTransit: ['Received'],", _script);
        Assert.Contains("Authorized: ['InTransit', 'Cancelled'],", _script);
        Assert.Contains("const ELIGIBLE_SHIPMENT_STATUSES = new Set(['Delivered', 'Closed']);", _script);
        Assert.Contains("nowWithOffset", _script);
    }

    [Fact]
    public void Every_published_code_is_mapped_to_a_localized_key()
    {
        foreach (var code in PublishedCodes)
        {
            Assert.Matches(new Regex($@"\b{code}: 'Err\w+'"), _script);
        }

        Assert.Contains("const INVALID_REQUEST_NOTICE = Object.freeze({ 401: 'NoticeSessionEnded', 403: 'NoticeForbidden', 415: 'NoticeUnsupportedMedia' });", _script);
    }

    [Fact]
    public void No_hard_coded_user_text_in_script()
    {
        // Visible text comes from window.L10n only; string literals passed to showToast/showConfirm are forbidden.
        Assert.DoesNotMatch(new Regex(@"showToast\?\.\('[A-Za-z]"), _script);
        Assert.DoesNotMatch(new Regex(@"showConfirm\?\.\('[A-Za-z]"), _script);
        Assert.DoesNotMatch(new Regex(@"textContent = '[A-Za-z]"), _script);
        Assert.Contains("const requiredKeys", _l10n);
        Assert.Contains("'ErrPersistenceUnavailable'", _l10n);
        Assert.Contains("document.getElementById('returns-l10n')", _l10n);
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
