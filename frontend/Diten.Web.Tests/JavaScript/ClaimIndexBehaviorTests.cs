using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

// MOD-0187 Claims — static behaviour contract of index.js / index.l10n.js (pack §32.3–§32.9; CU-01, CU-02, CU-04,
// CU-05, CU-09, CU-13, CU-19, CU-22, CU-26, CU-31, CU-SCR-01…05). Runtime behaviour is covered by the Playwright
// scenarios for the Mac lane (Q64b). Built and run by R-4c.
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
    public void One_intent_per_opened_form_or_panel_and_409_blocks_it()
    {
        // MODULE-RECIPE 3.1 / Q403 (R-4c re-pin): the draft pinned the per-payload key ("createIntent.bodyText !== bodyText"),
        // which lets an edited retry after a lost response create a second claim. The key is minted when the create form or
        // the transition panel opens, is never compared with the payload, and survives every failure until the surface closes.
        Assert.DoesNotContain("bodyText !==", _script);
        Assert.Contains("const newIntent = (extra) => Object.assign({ key: uuid(), blocked: false, blockedFailure: null }, extra);", _script);
        Assert.Contains("createIntent = newIntent();", Slice(_script, "const openCreate", "const resolveShipment"));
        Assert.Contains("transitionIntent = newIntent({ claimId });", Slice(_script, "const openTransition", "const syncTransitionTarget"));
        Assert.DoesNotContain("createIntent = null", Slice(_script, "const handleCreateFailure", "// ─── Transition"));
        Assert.DoesNotContain("transitionIntent = null", Slice(_script, "const handleTransitionFailure", "// ─── Filters"));
        Assert.Contains("'Idempotency-Key': key", _script);
        Assert.Contains("intent.blocked = true;", _script);
        Assert.Contains("if (createPending)", _script);
        Assert.Contains("if (transitionPending)", _script);
    }

    [Fact]
    public void A_failed_list_load_uses_read_wording_and_a_403_draws_the_denied_card()
    {
        // MODULE-RECIPE 3.11 and 3.6 (R-4c): the draft chose the list's error text by code, so a 503 or a dropped connection
        // read "the outcome is not confirmed; try the same request again" on a read, and a 403 drew the error state.
        var states = Slice(_script, "const setListState", "const isListEnvelope");
        Assert.Contains("if (message) message.textContent = t('ListErrorState');", states);
        Assert.DoesNotContain("failureText(failure)", states);
        Assert.Contains("if (response.status === 403) { showListDenied(); callback({ data: [] }); return; }", _script);
        Assert.Contains("surface.hidden = true; surface.setAttribute('inert', ''); denied.hidden = false;", _script);
        Assert.Contains("ajax: (data, callback) => { void loadClaims(data, callback); }", _script);
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

    // ── Q64e: Q64d runtime defects D1 / D2 / D4 (each fails on the v2 script) ────────────────────────────────────────

    [Fact]
    public void Skeleton_state_overrides_the_shared_display_none()
    {
        // Q64d-D1 (CU-05): backbone-custom.css sets .backbone-skeleton { display: none }; the d-none toggle alone never shows it.
        var state = Slice(_script, "const setListState", "if (state === 'error' && failure)");
        Assert.Contains("skeleton.style.display = state === 'skeleton' ? 'block' : 'none';", state);
        Assert.Contains("skeleton.classList.toggle('d-none', state !== 'skeleton');", state);
        // Q64e rc1 runtime timeline: the shared DtDefaults drawCallback fades the skeleton out on DataTables' empty first draw.
        Assert.Contains("window.jQuery?.(skeleton).stop(true);", state);
        Assert.Contains("if (listState === 'skeleton') setListState('skeleton');", Slice(_script, "drawCallback: function", "}));"));
    }

    [Fact]
    public void Transition_surface_takes_keyboard_focus_when_opened()
    {
        // Q64d-D2 (CU-25): opened from a row dropdown item; focus must move into the surface so Escape (bound on the
        // offcanvas element) closes it — at once and again after Bootstrap's focus trap settles on the container.
        Assert.Contains("const focusTransitionField = () => document.getElementById('transitionTarget')?.focus(", _script);
        var open = Slice(_script, "const openTransition", "const syncTransitionTarget");
        Assert.Matches(new Regex(@"offcanvas\('offcanvasClaimTransition'\)\?\.show\(\);\s*focusTransitionField\(\);"), open);
        Assert.Contains("el.addEventListener('shown.bs.offcanvas', focusTransitionField)", _script);
    }

    [Fact]
    public void Create_surface_returns_focus_to_its_opener_on_close()
    {
        // Q64d-D4 (CU-25): after the create offcanvas closes, focus goes back to the control that opened it (Add).
        var open = Slice(_script, "const openCreate", "const resolveShipment");
        Assert.Contains("createOpener = document.activeElement", open);
        var hidden = Slice(_script, "el.addEventListener('hidden.bs.offcanvas'", "});");
        Assert.Contains("restoreCreateOpener();", hidden);
        Assert.Contains("target?.focus({ preventScroll: true });", Slice(_script, "const restoreCreateOpener", "};"));
    }

    // ── Q64f: Q122-D5 (fails on the v3 script) ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_surface_keeps_keyboard_focus_after_a_rejected_submit()
    {
        // Q122-D5 (CU-25): Save is disabled while the request is pending, so after a rejected submit (400/409/422/5xx) focus
        // fell to BODY and Escape (bound on the offcanvas element) did nothing until the user pressed Tab.
        var restore = Slice(_script, "const restoreCreateFocus", "// Exact decimal text");
        Assert.Contains("surface.classList.contains('show')", restore);
        Assert.Contains("surface.classList.contains('hiding')", restore);
        Assert.Contains("if (surface.contains(document.activeElement)) return;", restore);
        Assert.Contains("surface.querySelector('[aria-invalid=\"true\"]:not([disabled]), .is-invalid:not([disabled])')", restore);
        Assert.Contains("invalid || (save && !save.disabled ? save : document.getElementById('claimShipmentId'))", restore);
        Assert.Contains("target?.focus({ preventScroll: true });", restore);

        // Focus is restored only after Save has been re-enabled in the create finally block.
        var createFinally = Slice(Slice(_script, "const submitCreate", "const handleCreateFailure"), "} finally {", "\n    };");
        Assert.Matches(new Regex(@"if \(save\) save\.disabled = [^;]+;\s*restoreCreateFocus\(\);"), createFinally);

        // The transition surface is not affected (focus returns to Submit after the shared confirm) and stays unchanged.
        Assert.DoesNotContain("restoreCreateFocus", Slice(_script, "const submitTransition", "const handleTransitionFailure"));
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
