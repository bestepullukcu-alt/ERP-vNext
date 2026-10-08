using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-4C — the Targets tab on the REAL script, view, resources and controller: the doctor table from the 3D period
/// status (no link column, consent-blocked never selectable), the quick filters + counted specialty multi-select, the
/// product chips, the picker (suggested products locked with their role, the limit from data, "Done" writes only that
/// doctor's products), the bulk apply (union, overflow message), the save that still sends no products, the names in
/// the selected list, the read-only week, the product search proxy and the seven languages.
/// </summary>
public sealed class VisitPlanningTargetsTabWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ── 1 · the doctor table: 3D columns, no link column, consent-blocked cannot be ticked ──────────────────────────────

    [Fact]
    public void The_doctor_table_reads_the_period_status_has_no_link_column_and_never_selects_a_consent_blocked_doctor()
    {
        var js = Script("details.js");

        // the institution's doctors come from the 3D read, with the plan, the quick filter and a page of 200
        Assert.Contains("const path = '/my-accounts/' + accountId + '/doctors?planningSessionId=' + encodeURIComponent(sessionId) + '&quick=' + quick + '&pageSize=200';", js);
        Assert.Contains("return api(path + weekQuery('&'))", js); // 4M (5): the selected week
        // WP-VP-4J — a PLAIN table row (no DataTable): select · doctor · specialty · products · frequency · done / remaining ·
        // last visit · status — the 4C / 4H cells
        var config = Between(js, "const doctorCheck = row =>", "const docMatches");
        Assert.Contains("'<td class=\"cell-fit\">' + doctorCheck(row) + '</td>'", config);
        Assert.Contains("frequencyCell(row.status)", config);
        Assert.Contains("doneCell(row.status)", config);
        Assert.Contains("lastVisitCell(row.status)", config);
        Assert.Contains("<div class=\"vp-doc-picks\" data-cid=\"' + esc(row.contactId)", config);
        Assert.Contains("statusCell(row)", config); // 4H: the badges moved into the Status column
        Assert.Contains("+ doctorBadges(row);", Between(js, "const statusCell = row =>", "\n"));
        Assert.DoesNotContain("linkId' }", config);

        // the cells speak the 3D fields: "N in the period" / no-frequency badge, done / remaining, last visit, badges
        Assert.Contains("st.requiredVisitCount != null && st.frequencyStatus !== 'unknown'", js);
        Assert.Contains("(L.FrequencyPerPeriod || '{0}').replace('{0}', st.requiredVisitCount)", js);
        Assert.Contains("(L.FrequencyNone || '—')", js); // 4L: or the weekly default from frequencyDefault
        Assert.Contains("VPF.ratio(st.done || 0, st.requiredVisitCount)", js); // CT 4I E4: RTL-safe pair (4M: done / required)
        Assert.Contains("st.lastVisitDate ? esc(dayShort(st.lastVisitDate))", js);
        Assert.Contains("(row.status.segmentBadges || []).map(name =>", js);
        Assert.Contains("row.inactive ? ' <span class=\"badge bg-label-secondary\">' + esc(L.BadgeInactive", js);
        Assert.Contains("String(status.consentStatus || '').toLowerCase() === 'blocked'", js);

        // consent blocked: the box is disabled, a tick is refused, "select all" skips the doctor
        Assert.Contains("(canEditTargets() && !row.blocked ? '' : ' disabled')", config);
        Assert.Contains("if (!canEditTargets() || (c && c.blocked)) { cb.checked = false; return; }", js);
        var selectAll = Between(js, "const selectAllDoctors = () => {", "\n    };");
        Assert.Contains("visibleDoctors().forEach(row =>", selectAll); // "select all" follows the filters
        Assert.Contains("if (row.blocked) return;", selectAll);

        // the view's header has exactly the eight columns (4H: + Status), and no link column
        var view = View("Details.cshtml");
        var head = Between(view, "<table id=\"dt-vp-contacts\"", "</thead>");
        Assert.Equal(8, Regex.Matches(head, "<th>").Count);
        foreach (var key in new[] { "ColFrequency", "ColDoneRemaining", "ColLastVisit", "ColProducts", "ColStatus" })
        {
            Assert.Contains($"Localizer[\"{key}\"]", head);
        }

        Assert.DoesNotContain("ColLinked", view);
        Assert.All(Cultures, c => Assert.DoesNotContain(Resx(c).Values, v => v.Contains("BAĞLANTI", StringComparison.Ordinal)));
    }

    // ── 2 · quick filters (server) + counted specialty multi-select ─────────────────────────────────────────────────────

    [Fact]
    public void Quick_filters_go_to_the_server_and_the_specialty_filter_is_a_counted_multi_select()
    {
        var js = Script("details.js");
        Assert.Contains("const QUICK_FILTERS = ['all', 'due', 'never'];", js);
        var quick = Between(js, "el('vp-quick-filters')?.addEventListener('click', e => {", "\n    });");
        Assert.Contains("QUICK_FILTERS.indexOf(q) === -1", quick);
        Assert.Contains("activeQuick = q;", quick);
        Assert.Contains("loadDoctorTable(activeAccountId)", quick);
        Assert.Contains("fetchAccountDoctors(accountId, activeQuick)", js);

        var view = View("Details.cshtml");
        foreach (var q in new[] { "all", "due", "never" })
        {
            Assert.Contains($"data-quick=\"{q}\"", view);
        }

        // specialty: one checkbox per specialty with its count, several kept at once (regex alternation on the codes)
        var pills = Between(js, "const renderSpecialtyPills = list => {", "\n    };");
        Assert.Contains("counts[c.specialty] = (counts[c.specialty] || 0) + 1", pills);
        Assert.Contains("esc(specLabel(sp)) + ' (' + counts[sp] + ')", pills);
        Assert.Contains("vp-spec-check", pills);
        Assert.Contains("(!activeSpecs.length || activeSpecs.indexOf(row.specialty) > -1)", js); // WP-VP-4J: the plain table's filter
        Assert.Contains("querySelectorAll('.vp-spec-check:checked')", js);
        Assert.DoesNotContain("vp-spec-pill", js); // the single-choice pills are gone
    }

    // ── 3 · product chips: role colour, source icon, no-content warning, legend ─────────────────────────────────────────

    [Fact]
    public void Product_chips_show_the_role_by_colour_the_source_by_icon_and_warn_on_promo_without_content()
    {
        var js = Script("targets.js");
        Assert.Contains("const SOURCE_ICON = { play: 'bx-bulb', 'rep-pick': 'bx-user-check', 'last-visit': 'bx-history', portfolio: 'bx-briefcase' };", js);
        Assert.Contains("const NO_CONTENT_WARNINGS = ['no_approved_content', 'ambiguous_journey'];", js);

        var chip = Between(js, "const chipHtml = it => {", "\n    };");
        Assert.Contains("(promo ? 'bg-label-primary' : 'bg-transparent border text-body')", chip);
        Assert.Contains("SOURCE_ICON[it.source]", chip);
        Assert.Contains("const warn = promo && it.noContent;", chip);
        Assert.Contains("bx-error text-warning", chip);

        // the next visit's list from the preview; the stored pick (as "your pick") when the preview has none
        var items = Between(js, "const chipItems = cid => {", "\n    };");
        Assert.Contains("nextVisit[cid]", items);
        Assert.Contains("savedPicks(cid).map(p => ({ productId: p.productId, productCode: p.productCode, productName: p.productName, role: roleOf(p), source: SOURCE_REP_PICK", items);
        Assert.Contains("(warned || !it.journeyId || it.journeyId === EMPTY_GUID)", js);

        // no product: the yellow badge + "Pick products"
        var cell = Between(js, "const paintCell = cell => {", "\n    };");
        Assert.Contains("bg-label-warning\">' + esc(L.NoProductBadge", cell);
        Assert.Contains("pickButton(cell, L.PickProducts || '')", cell);

        // the legend under the table explains both roles, the three sources and the warning
        var legend = Between(View("Details.cshtml"), "id=\"vp-products-legend\"", "</div>");
        foreach (var key in new[] { "LegendPromo", "LegendNonPromo", "LegendPlay", "LegendLastVisit", "LegendRepPick", "NoApprovedContent" })
        {
            Assert.Contains($"Localizer[\"{key}\"]", legend);
        }
    }

    // ── 4 · the picker: suggested locked (role too), limit from data, Done = only that doctor's products ─────────────────

    [Fact]
    public void The_picker_locks_suggested_products_with_their_role_reads_the_limit_from_data_and_done_writes_only_that_doctor()
    {
        var js = Script("targets.js");

        // suggested (play) products are locked: no remove button, role buttons disabled, both handlers refuse them
        var selected = Between(js, "el('vp-dp-selected').innerHTML = items.map((it, idx) => {", "}).join('');");
        Assert.Contains("(locked ? '' : '<button type=\"button\" class=\"btn btn-sm btn-icon btn-text-danger js-vp-remove\"", selected);
        Assert.Contains("L.SuggestedLockHint", selected);
        Assert.Contains("(locked ? ' disabled' : '')", Between(js, "const roleButtons = (item, idx) => {", "\n    };"));
        var role = Between(js, "const roleBtn = e.target.closest('#vp-dp-selected .js-vp-role');", "return;\n        }");
        Assert.Contains("if (!item || isLocked(item)) return;", role);
        var remove = Between(js, "const rm = e.target.closest('#vp-dp-selected .js-vp-remove');", "return;\n        }");
        Assert.Contains("if (!item || isLocked(item)) return;", remove);
        Assert.Contains("const isLocked = item => !!(item && item.locked);", js);
        Assert.Contains("source: SOURCE_PLAY, noContent: x.noContent, locked: true", Between(js, "const openSingle = cell => {", "\n    };"));

        // the limit indicator and the visit time come from the period capacity — never a constant
        Assert.Contains("maxPromo: x.maxPromoProducts, maxNonPromo: x.maxNonPromoProducts", js);
        Assert.Contains("promoMinutes: x.promoProductTime, nonPromoMinutes: x.nonPromoProductTime", js);
        Assert.Contains("reportMinutes: x.visitModel === 'typical' ? (x.reportMinutesPerVisit || 0) : (x.reportDuration || 0)", js);
        Assert.Contains("const visitMinutes = (cap, p, n) => (cap && cap.promoMinutes != null) ? p * cap.promoMinutes + n * cap.nonPromoMinutes + cap.reportMinutes : null;", js);
        Assert.Contains("const max = v => (capacity && capacity[v] != null ? capacity[v] : '—');", js);
        Assert.DoesNotMatch(@"max(Promo|NonPromo)\s*:\s*3\b", js);
        Assert.Contains("'/capacities?cyclePeriodId=' + encodeURIComponent(periodId)", js);

        // the warnings: over the limit, and the suggested products already filling a role
        Assert.Contains("overLimit(capacity, counts)", js);
        Assert.Contains("lockedCounts.promo >= capacity.maxPromo && pickCounts.promo > 0", js);

        // Done (single): ONLY this doctor carries products (its own picks, never the locked ones); the rest go without
        // the field (null = keep), and the account / pharmacy lists are not sent (null = keep)
        var done = Between(js, "const done = () => {", "\n    };");
        Assert.Contains("save(buildUpdate([{ doctor: picker.doctor, products: picker.picks }]), L.ProductsSaved || '');", done);
        var update = Between(js, "const buildUpdate = changes => {", "\n    };");
        Assert.Contains("const contacts = savedContacts().map(contactInput);", update);
        Assert.Contains("entry.products = ch.products.map(pickInput);", update);
        Assert.Contains("selectedPharmacyIds: null", update);
        Assert.Contains("expectedVersion: session.version", update);
        Assert.DoesNotContain("products", Between(js, "const contactInput = c =>", ";\n"));
        Assert.Contains("base + '/sessions/' + encodeURIComponent(sessionId), { method: 'PUT'", js);
        Assert.Contains("page.request('reload-plan')", js);
        Assert.Contains("page.on('request:reload-plan', () => loadSession().then(preview));", Script("details.js"));

        // S-1 text + the panel skeleton (period view waits for 4D)
        var panel = View("_DoctorPanel.cshtml");
        Assert.Contains("Localizer[\"ProductsS1Note\"]", panel);
        // WP-VP-4D filled the period view (no pending-package marker any more).
        Assert.Contains("id=\"vp-dp-tab-period-item\"", panel);
        Assert.DoesNotContain("data-pending-package", panel);
        Assert.Equal("Değişiklikler bu doktorun onaylanmamış sonraki ziyaretlerine uygulanır.", Resx("tr")["ProductsS1Note"]);
    }

    // ── 5 · bulk apply: union (existing kept), overflow counted in the message ─────────────────────────────────────────

    [Fact]
    public void Bulk_apply_adds_to_each_doctors_products_and_counts_the_doctors_over_the_limit()
    {
        var js = Script("targets.js");
        var union = Between(js, "const unionPicks = (existing, chosen) => {", "\n    };");
        // every existing product stays (with its own role) and comes first; a picked one is added only when new
        Assert.StartsWith("const unionPicks = (existing, chosen) => {\n        const merged = (existing || []).map(p =>", union.Replace("\r\n", "\n"));
        Assert.Contains("if (!merged.some(m => m.productId === p.productId)) merged.push(", union);
        Assert.Contains("return merged;", union);

        var done = Between(js, "const done = () => {", "\n    };");
        Assert.Contains("const merged = unionPicks(savedPicks(d.contactId), picker.picks);", done);
        Assert.Contains("return { doctor: d, products: merged };", done);
        Assert.Contains("if (overLimit(capacity, countRoles(playItems(d.contactId).concat(merged)))) over++;", done);
        Assert.Contains("over ? fmt(L.BulkLimitExceeded || '{0}', over) : null", done);

        // the toolbar button: "Apply products (N)", disabled without a selection
        Assert.Contains("btn.disabled = !canEdit() || n === 0;", js);
        Assert.Contains("id=\"vp-bulk-products\" disabled", View("Details.cshtml"));
        Assert.Equal("{0} doktorda sınır aşıldı; fazla ürünler sonraki ziyarete kalır.", Resx("tr")["BulkLimitExceeded"]);
        Assert.Equal("Seçtiğiniz ürünler {0} doktorun mevcut ürünlerine eklenir. Var olan ürünler korunur.", Resx("tr")["ProductsBulkNote"]);
    }

    // ── 6 · "Save targets" still sends no products; the tab is read-only on an approved / past week ───────────────────

    [Fact]
    public void Saving_the_targets_sends_no_products_and_a_locked_week_makes_the_tab_read_only()
    {
        var details = Script("details.js");
        Assert.DoesNotMatch(@"\bproducts\b", details); // the 3C source rule (VisitPlanningProductPickWebTests) holds
        var save = Regex.Match(details, @"const saveTargets = \(\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        Assert.Contains("if (targetsLocked()) return;", save);
        Assert.Contains("selectedContacts: contacts", save);

        Assert.Contains("const LOCKED_WEEK_STATUSES = ['approved', 'past'];", details);
        Assert.Contains("page.isLegacy() || LOCKED_WEEK_STATUSES.indexOf(page.weekStatus(page.state.weekStart)) > -1", details);
        Assert.Contains("['week-change', 'session', 'preview'].forEach(evt => page.on(evt, () => applyTargetsLock()));", details);
        var lockFn = Between(details, "const applyTargetsLock = () => {", "\n    };");
        Assert.Contains("'vp-save-targets', 'vp-select-all-doctors', 'vp-clear-selection', 'vp-out-territory-open'", lockFn);
        Assert.Contains("el('vp-targets-locked')", lockFn);

        var targets = Script("targets.js");
        Assert.Contains("const LOCKED_WEEK_STATUSES = ['approved', 'past'];", targets);
        Assert.Contains("const canEdit = () => canGenerate && !targetsLocked();", targets);
        Assert.Contains("(canEdit() ? ' ' + pickButton(cell, L.PickProducts || '') : '')", targets);
        Assert.Contains("id=\"vp-targets-locked\"", View("Details.cshtml"));
    }

    // ── 7 · the selected list: name + institution + specialty, never a GUID ────────────────────────────────────────────

    [Fact]
    public void The_selected_list_shows_name_institution_and_specialty_never_an_id()
    {
        var js = Script("details.js");
        var chips = Between(js, "const renderSelectionChips = () => {", "\n    };");
        Assert.Contains("cName(s.accountId, s.contactId), specLabel(cSpec(s.accountId, s.contactId))", chips);
        Assert.Contains("parts.push(groupHead(aid, aName(aid), groups[aid].length))", chips); // grouped under the institution (4H: collapsible)
        Assert.Contains("return c ? c.name : (savedContactNames[cid] || '—');", js);
        Assert.Contains("return c ? c.specialty : (savedContactSpecs[cid] || '');", js);
        Assert.Contains("(savedAccountNames[aid] || '—')", js);
        // the plan's targets read gives every saved doctor its name + specialty before the institution is opened
        var targetsRead = Between(js, "const loadPlanTargets = () =>", "}).catch(() => null);");
        Assert.Contains("savedContactNames[x.contactId] = x.displayName", targetsRead);
        Assert.Contains("savedContactSpecs[x.contactId] = x.specialty", targetsRead);
        // the bulk apply and the summary receive names, not ids
        Assert.Contains("name: cName(sc.accountId, sc.contactId), accountName: aName(sc.accountId), specialty: specLabel(cSpec(sc.accountId, sc.contactId))", js);
    }

    // ── institutions: "x / y selected · N this week", out-of-territory dialog ─────────────────────────────────────────

    [Fact]
    public void Institution_rows_count_selected_and_due_doctors_and_out_of_territory_is_a_dialog()
    {
        var js = Script("details.js");
        var stats = Between(js, "const accountStatParts = id => {", "\n    };"); // 4I (6): parts, drawn as separate items
        Assert.Contains("(L.AccountSelectedOf || '{0} / {1}').replace('{0}', sel).replace('{1}', st.active)", stats);
        Assert.Contains("(L.AccountDueThisWeek || '{0}').replace('{0}', st.due)", stats);
        Assert.Contains("due: rows.filter(x => x.status.dueThisWeek).length", js);
        Assert.Contains("data-aid=\"' + esc(a.id) + '\" data-city=\"' + esc(cityOnly(a)) + '\">' + accountStatsHtml(a.id, cityOnly(a)) + '</span>'", js);

        var view = View("Details.cshtml");
        var modal = Between(view, "id=\"vp-out-territory-modal\"", "</select>");
        Assert.Contains("id=\"vp-add-account-out\"", modal);
        Assert.Contains("data-bs-target=\"#vp-out-territory-modal\"", view);
        Assert.Contains("dropdownParent: window.jQuery(sel.closest('.modal') || document.body)", js);
    }

    // ── the product search proxy: read key, MDM selector, page ≤ 100, explicit "unavailable" ───────────────────────────

    [Fact]
    public async Task The_product_search_is_a_read_proxy_onto_the_mdm_selector_capped_at_100()
    {
        var (gateway, controller) = Arrange(HttpStatusCode.OK,
            """{"data":{"items":[{"id":"6f2a5d2e-0c39-4a61-9a0e-3b7f1e2d4c11","canonicalCode":"TUTUKON","globalProductName":"Tutukon 5 mg"},{"id":"not-a-guid","canonicalCode":"X"}]}}""",
            "crm.visit-plan.read");
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?search=tutu&pageSize=500");

        var result = Assert.IsType<OkObjectResult>(await controller.Products(default));
        var body = JsonDocument.Parse(JsonSerializer.Serialize(result.Value)).RootElement;

        var call = Assert.Single(gateway.Requests);
        Assert.Equal($"{GatewayUrl}/api/global-products/selector?pageNumber=1&pageSize=100&search=tutu", call.Uri);
        Assert.Equal("GET", call.Method);
        Assert.Equal(TenantId.ToString(), call.Tenant);
        Assert.False(body.GetProperty("disabled").GetBoolean());
        var option = Assert.Single(body.GetProperty("options").EnumerateArray());
        Assert.Equal("TUTUKON", option.GetProperty("productCode").GetString());
        Assert.Equal("Tutukon 5 mg", option.GetProperty("productName").GetString());
        Assert.Equal("6f2a5d2e-0c39-4a61-9a0e-3b7f1e2d4c11", option.GetProperty("productId").GetString());
    }

    [Fact]
    public async Task The_product_search_needs_the_read_key_and_says_unavailable_instead_of_an_empty_list()
    {
        var (blocked, denied) = Arrange(HttpStatusCode.OK, "{}");
        Assert.Equal(403, Assert.IsType<ObjectResult>(await denied.Products(default)).StatusCode);
        Assert.Empty(blocked.Requests);

        var (_, refused) = Arrange(HttpStatusCode.Forbidden, "{}", "crm.visit-plan.read");
        var body = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await refused.Products(default)).Value)).RootElement;
        Assert.True(body.GetProperty("disabled").GetBoolean());
        Assert.Equal("ProductPermissionMissing", body.GetProperty("reason").GetString());

        // the picker turns "disabled" into its "cannot be read" line
        var js = Script("targets.js");
        Assert.Contains("picker.unavailable = !d || d.disabled === true;", js);
        Assert.Contains("request(base + '/products?pageSize=100'", js);
    }

    // ── 8 · the new texts: seven languages + neutral, on the bridge, read by the scripts ─────────────────────────────

    [Fact]
    public void The_new_keys_exist_in_seven_languages_and_the_scripts_read_only_bridged_keys()
    {
        var keys = new[]
        {
            "TargetsLockedBand", "AddOutOfTerritoryShort", "OutOfTerritoryDialogHint", "BulkApplyProducts", "QuickFiltersLabel",
            "QuickFilterAll", "QuickFilterDue", "QuickFilterNever", "ColFrequency", "ColDoneRemaining", "ColLastVisit", "ColProducts",
            "LegendPromo", "LegendNonPromo", "LegendPlay", "LegendLastVisit", "LegendRepPick", "LegendPortfolio", "NoApprovedContent",
            "FrequencyPerPeriod", "FrequencyNone", "PlannedCountHint", "LastVisitNever", "BadgeConsentBlocked", "ConsentBlockedHint",
            "BadgeInactive", "SegmentBadgeHint", "SpecialtyAll", "SpecialtySelected", "NoDoctorsForFilter", "AccountSelectedOf",
            "AccountSelectedCount", "AccountDueThisWeek", "EstimatedWeek", "EstimatedWeekShare", "EstimatedOver", "ProductDistribution",
            "DoctorsWithoutProducts", "NoProductBadge", "PickProducts", "EditProducts", "DoctorPanelPeriod", "RolePromo", "RoleNonPromo",
            "ProductsS1Note", "ProductsBulkNote", "ProductsSuggestedNote", "ProductsNoneNote", "PortfolioUndefinedNote", "AllProducts",
            "ProductSearch", "ProductLimitLine", "DurationLine", "DurationUnknown", "LimitExceeded", "PlayLimitFull", "SuggestedLockHint",
            "NoProductMatches", "ProductsDone", "ApplyToSelected", "BulkLimitExceeded", "ProductsSaved", "ProductsUnavailable",
            "RulesTitle", "RuleSourceOrder", "RuleMixedOrder", "RuleOverflow", "RuleUnknownFrequency"
        };
        foreach (var culture in Cultures)
        {
            var values = Resx(culture);
            Assert.All(keys, k => Assert.True(values.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) && v != k, $"{culture}: {k}"));
        }

        // Turkish is real Turkish (dotted / dotless i, ş, ğ, ü, ö, ç kept)
        var tr = Resx("tr");
        Assert.Equal("Bu hafta görülmesi gerekenler", tr["QuickFilterDue"]);
        Assert.Equal("Hiç görülmeyenler", tr["QuickFilterNever"]);
        Assert.Equal("Sınır dolu — eklediğiniz ürün sığmıyor, sonraki ziyarete kalır.", tr["PlayLimitFull"]);
        Assert.Equal("Önerilen ürün; yalnız Planlanan Ziyaret ekranında çıkarılabilir", tr["SuggestedLockHint"]);

        // every L.X targets.js reads is on the bridge; the panel + view read only keys the resx has
        var bridge = View("_IndexL10n.cshtml");
        var read = Regex.Matches(Script("targets.js"), @"(?<![\w.])L\.([A-Z][A-Za-z0-9_]*)").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.True(read.Count > 30);
        Assert.All(read, k => Assert.Matches($@"(?<![A-Za-z0-9_]){Regex.Escape(k)}\s*=", bridge));
        var en = Resx("en");
        var used = Regex.Matches(View("_DoctorPanel.cshtml"), @"(?<!Shared)Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value).Distinct();
        Assert.All(used, k => Assert.True(en.ContainsKey(k), k));

        // the page loads targets.js after the skeleton and the Targets / Route script
        var view = View("Details.cshtml");
        var skeleton = view.IndexOf("VisitPlanning/page.js", StringComparison.Ordinal);
        var route = view.IndexOf("VisitPlanning/details.js", StringComparison.Ordinal);
        var targets = view.IndexOf("VisitPlanning/targets.js", StringComparison.Ordinal);
        Assert.True(skeleton > 0 && skeleton < route && route < targets);
        Assert.Contains("<partial name=\"~/Views/CRM/VisitPlanning/_DoctorPanel.cshtml\" />", view);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private static (Gateway, VisitPlanningController) Arrange(HttpStatusCode status, string body, params string[] permissions)
    {
        var gateway = new Gateway(status, body);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitPlanningController(new HttpClient(gateway), configuration, NullLogger<VisitPlanningController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return (gateway, controller);
    }

    private static string Between(string text, string start, string end)
    {
        text = text.Replace("\r\n", "\n");
        start = start.Replace("\r\n", "\n");
        end = end.Replace("\r\n", "\n");
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, end);
        return text[from..(to + end.Length)];
    }

    private static string Script(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPlanning", file)).Replace("\r\n", "\n");

    private static string View(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPlanning", file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitPlanningIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitPlanning", file)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<(string Uri, string Method, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Method.Method,
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
