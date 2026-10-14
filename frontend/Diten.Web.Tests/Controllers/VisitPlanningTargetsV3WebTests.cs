using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-4J — the Targets tab per mockup v3 (a plain doctor table, no DataTable), the product panel's period view, the
/// empty-week feedback, the list filters + archiving the selected empty drafts, the new-plan rep field — on the REAL
/// scripts, views and resources (Acceptance 1–8).
/// </summary>
public sealed class VisitPlanningTargetsV3WebTests
{
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ── 1 · the Targets tab per mockup v3 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_targets_tab_has_the_v3_cards_pills_specialty_select_and_a_plain_doctor_table()
    {
        var view = View("Details.cshtml");
        var pane = Between(view, "id=\"vp-tab-targets\"", "id=\"vp-tab-weeks\"");
        // head card: the icon, the title, the week line, the green "Read-only" badge (no separate band any more)
        Assert.Contains("id=\"vp-targets-head\"", pane);
        Assert.Contains("<i class=\"bx bx-target-lock\"></i>", pane);
        Assert.Contains("id=\"vp-targets-subtitle\"", pane);
        Assert.Contains("badge bg-label-success", Between(pane, "id=\"vp-targets-locked\"", "</div>"));
        Assert.Contains("Localizer[\"ReadOnlyBadge\"]", pane);
        Assert.DoesNotContain("alert alert-info py-2 small mb-3 d-none\" role=\"status\"><i class=\"bx bx-lock-alt me-1\"></i>@Localizer[\"TargetsLockedBand\"]", pane);
        // three columns of cards; the middle one: a tab card + the institution card (building tile + three pills)
        Assert.Contains("id=\"vp-subtabs\"", pane);
        Assert.Contains("class=\"btn vp-midtab active\" id=\"vp-subtab-doctors-btn\"", pane);
        Assert.Contains("<i class=\"bx bx-buildings\"></i>", pane);
        var js = Script("details.js");
        Assert.Contains("pill('bx-map', bidi(meta.city))", js);
        Assert.Contains("pill('bx-user', esc(fmt(L.DoctorCountPill || '{0}', meta.docs)))", js);
        Assert.Contains("pill('bx-plus-medical', esc(fmt(L.LinkedPharmacyCountPill || '{0}', meta.ph)))", js);
        // counted pill quick filters, "due this week" first and picked
        var quick = Between(pane, "id=\"vp-quick-filters\"", "</div>");
        Assert.Contains("class=\"btn vp-pill vp-quick active\" data-quick=\"due\" aria-pressed=\"true\"", quick);
        Assert.True(quick.IndexOf("data-quick=\"due\"", StringComparison.Ordinal) < quick.IndexOf("data-quick=\"never\"", StringComparison.Ordinal)
                    && quick.IndexOf("data-quick=\"never\"", StringComparison.Ordinal) < quick.IndexOf("data-quick=\"all\"", StringComparison.Ordinal));
        Assert.Contains("let activeQuick = 'due';", js);
        // search + the specialty multi-select ("Specialty: All" / "Specialty: N selected" + "Clear selection")
        Assert.Contains("id=\"vp-doctor-search\"", pane);
        var spec = Between(js, "const renderSpecialtyPills = list => {", "\n    };");
        Assert.Contains("L.SpecialtyFilterAll", spec);
        Assert.Contains("js-spec-clear", spec);
        Assert.Contains("fmt(L.SpecialtyFilterSelected || '{0}', activeSpecs.length)", js);
        // "Select all (N)" + "Apply products (N)"
        Assert.Contains("<span id=\"vp-select-all-count\"></span>", pane);
        Assert.Contains("id=\"vp-bulk-products\"", pane);
        // the doctor list: a PLAIN table — no DataTable, no paging, scrolls in its card; 8 columns; the legend under it
        Assert.DoesNotContain("new DataTable(", js);
        Assert.DoesNotContain("contactsDt", js);
        var table = Between(pane, "<table id=\"dt-vp-contacts\"", "</table>");
        Assert.DoesNotContain("card-datatable", Between(pane, "id=\"vp-subtab-doctors\"", "id=\"dt-vp-contacts\""));
        Assert.Contains("<tbody id=\"vp-doc-tbody\"></tbody>", table);
        Assert.Equal(8, Regex.Matches(Between(table, "<thead>", "</thead>"), "<th>").Count);
        Assert.Contains("class=\"vp-doc-scroll border rounded\"", pane);
        Assert.Contains("id=\"vp-products-legend\"", pane);
        var row = Between(js, "const doctorRowHtml = row =>", "</tr>';");
        Assert.Equal(8, Regex.Matches(row, "'<td").Count);
        Assert.Contains("rows.map(doctorRowHtml).join('');", js); // 4M (4): after the plan doctors' group
        // the lavender summary + the selected list; thin scrollbars from the page CSS only
        Assert.Contains("class=\"card vp-summary-card p-4\"", pane);
        Assert.Contains("id=\"vp-selection-chips\"", pane);
        var css = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "css", "visit-planning.css"));
        Assert.Contains("scrollbar-width: thin;", css);
        Assert.Contains("--vp-indigo-line: #c5c9e8;", css);
        foreach (var page in new[] { "Details.cshtml", "Index.cshtml", "Create.cshtml", "Edit.cshtml" })
        {
            Assert.Contains("~/assets/css/visit-planning.css", Between(View(page), "@section Styles {", "}"));
        }
        Assert.DoesNotContain("visit-planning.css", File.ReadAllText(Path.Combine(WebRoot(), "Views", "Shared", "_LayoutTenantShell.cshtml")));
    }

    // ── 2 · selection / save stay as they were ───────────────────────────────────────────────────────────────

    [Fact]
    public void Ticking_select_all_and_the_consent_rule_keep_their_selection_path()
    {
        var js = Script("details.js");
        Assert.Contains("selectedContacts[k] = { contactId: cid, accountId: activeAccountId, accountContactLinkId: (c && c.linkId) || null };", js);
        Assert.Contains("if (!canEditTargets() || (c && c.blocked)) { cb.checked = false; return; }", js);
        Assert.Contains("ensureInPlan(activeAccountId);", js);
        var selectAll = Between(js, "const selectAllDoctors = () => {", "\n    };");
        Assert.Contains("visibleDoctors().forEach(row =>", selectAll);
        Assert.Contains("if (row.blocked) return;", selectAll);
        // the plain table's filter: search (Turkish-insensitive) + the picked specialties
        var visible = Between(js, "const visibleDoctors = () => {", "\n    };");
        Assert.Contains("new RegExp(trSearchPattern(docTerm), 'i')", visible);
        Assert.Contains("activeSpecs.indexOf(row.specialty) > -1", visible);
        // targets.js still fills the picks cells after each draw
        Assert.Contains("page.emit('targets:doctors-drawn', { accountId: activeAccountId });", Between(js, "const drawDoctors = () => {", "\n    };"));
        Assert.Contains("#dt-vp-contacts .vp-doc-picks", Script("targets.js"));
    }

    // ── 3 · the product picker: the period view of its doctor, or why it is empty; bulk hides it ──────────────

    [Fact]
    public void The_product_picker_draws_the_doctors_period_view_and_hides_the_tab_in_bulk_mode()
    {
        var targets = Script("targets.js");
        Assert.Contains("page.emit('picker:open', picker.mode === 'single'", targets);
        Assert.Contains("{ mode: 'single', contactId: picker.doctor.contactId, accountId: picker.doctor.accountId || null }", targets);
        Assert.Contains(": { mode: 'bulk' });", targets);

        var panel = Script("doctor-panel.js");
        var onPicker = Between(panel, "page.on('picker:open', e => {", "\n    });");
        Assert.Contains("if (!e || e.mode !== 'single' || !e.contactId) {", onPicker);
        Assert.Contains("periodItem()?.classList.add('d-none');", onPicker); // bulk: no period tab
        Assert.Contains("periodItem()?.classList.remove('d-none');", onPicker);
        Assert.Contains("render();", onPicker);
        Assert.Contains("panel.addEventListener('hidden.bs.offcanvas', () => periodItem()?.classList.remove('d-none'));", panel);
        // a doctor not saved in the plan yet: an explanatory empty state, never "—"
        var render = Between(panel, "const render = () => {", "\n    };");
        Assert.Contains("if (!doctor && !slots.length) {", render);
        Assert.Contains("esc(L.PeriodViewNotInPlan || '')", render);
        Assert.Equal("Bu doktor henüz plana kaydedilmedi. Hedefleri kaydedince dönem görünümü dolar.", Resx("tr")["PeriodViewNotInPlan"]);
    }

    // ── 4 · the empty week: generate → busy → still empty: a message + why + "Edit targets" ──────────────────

    [Fact]
    public void Generating_an_empty_week_shows_progress_and_says_why_when_it_stays_empty()
    {
        var js = Script("weeks.js");
        var start = Between(js, "const startGenerate = ws => {", "\n    };");
        Assert.Contains("generating = ws;", start);
        Assert.Contains("page.request('reload-plan');", start);
        var finish = Between(js, "const finishGenerate = () => {", "\n    };");
        Assert.Contains("if (w && w.status === 'empty') {", finish);
        Assert.Contains("stillEmpty.add(ws);", finish);
        Assert.Contains("window.showToast?.(L.GenerateWeekStillEmpty || '', 'info');", finish);
        var empty = Between(js, "const emptyWeekHtml = ws => {", "\n    };");
        Assert.Contains("(busy ? ' disabled aria-busy=\"true\"' : '')", empty);
        Assert.Contains("spinner-border spinner-border-sm", empty);
        Assert.Contains("explained ? (L.EmptyWeekNoFrequencyVisits || '') : (L.EmptyWeekHint || '')", empty);
        Assert.Contains("js-wk-edit-targets", empty);
        Assert.Contains("page.on('preview', () => { finishGenerate(); render(); returnToWeeks(); });", js);
        Assert.Contains("else if (k === 'generate') startGenerate(page.state.weekStart);", js);
        Assert.Contains("page.on('request:generate-week', () => startGenerate(page.state.weekStart));", js);
        Assert.Contains("page.request('generate-week')", Script("header.js"));
        // the mockup's "created automatically when the previous week is approved" is not said (it misleads with this engine)
        foreach (var culture in Cultures)
        {
            Assert.DoesNotContain("approv", Resx(culture)["EmptyWeekHint"], StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("onaylandığında", Resx("tr")["EmptyWeekHint"]);
        Assert.StartsWith("Bu haftaya sıklık kurallarına göre ziyaret düşmüyor", Resx("tr")["EmptyWeekNoFrequencyVisits"]);
    }

    // ── 5 · the list filter: fixed status options, visible labels, the rep filter for read-all only ──────────

    [Fact]
    public void The_list_filter_has_fixed_statuses_visible_labels_and_a_rep_filter_for_read_all_only()
    {
        var js = Script("index.js");
        Assert.Contains("const STATUS_OPTIONS = ['draft', 'committed', 'archived'];", js);
        Assert.Contains("fillSelect('filterSessionStatus', STATUS_OPTIONS.map(v => ({ value: v, text: statusOptionLabel(v) })), false);", js);
        Assert.DoesNotContain("distinct(sStatus)", js);
        var filter = View("_Filter.cshtml");
        Assert.Contains("<label class=\"form-label small mb-1\" for=\"filterSessionStatus\">", filter);
        Assert.Contains("<label class=\"form-label small mb-1\" for=\"filterCyclePeriod\">", filter);
        Assert.Contains("data-placeholder=\"@SharedLocalizer[\"Status\"]\"", filter);
        var rep = filter.IndexOf("id=\"filterRep\"", StringComparison.Ordinal);
        Assert.True(rep > filter.LastIndexOf("@if (Model.CanReadAll)", rep, StringComparison.Ordinal) && filter.LastIndexOf("@if (Model.CanReadAll)", rep, StringComparison.Ordinal) > 0);
        Assert.Contains("rep: canReadAll ? (document.getElementById('filterRep')?.value || '') : ''", js);

        var controller = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "VisitPlanningController.cs"));
        Assert.Contains("private const string ReadAllPermission = \"crm.visit-plan.read-all\";", controller);
        Assert.Contains("CanReadAll = HasAnyPermission(ReadAllPermission)", controller);
        Assert.Contains("\"canReadAll\": @(Model.CanReadAll ? \"true\" : \"false\")", View("Index.cshtml"));
    }

    // ── 6 · "Action" = archive the SELECTED empty drafts ───────────────────────────────────────────────────────

    [Fact]
    public void Only_empty_drafts_can_be_selected_and_each_is_archived_through_the_existing_update()
    {
        var js = Script("index.js");
        var check = Between(js, "const rowCheck = row => {", "\n    };");
        Assert.Contains("const ok = canGenerate && isEmptyDraft(row);", check);
        Assert.Contains("' disabled title=\"' + esc(L.OnlyEmptyDraftsArchivable || '') + '\"'", check);
        Assert.Contains("if (cb.checked && row && isEmptyDraft(row)) selectedIds.add(cb.dataset.id);", js);
        // the Action menu entry, counted, off without a selection
        Assert.Contains("collectionBtns: canGenerate ? [{ text: archiveSelectedLabel(), icon: 'bx-archive', className: 'js-vp-archive-selected', action: () => archiveSelected() }] : []", js);
        Assert.Contains("fmt(L.ArchiveSelectedDrafts || '{0}', selectedIds.size)", js);
        Assert.Contains("b.enable(selectedIds.size > 0);", js);
        Assert.Contains("const archiveSelected = () => archiveDrafts(allRows.filter(r => selectedIds.has(sid(r)) && isEmptyDraft(r)));", js);
        // confirmed, then ONE existing update per draft (no new write endpoint); a 409 is named in the result
        var flow = Between(js, "const archiveDrafts = rows => {", "\n    };");
        Assert.Contains("fmt(L.ArchiveDraftsConfirm || '{0}', rows.length)", flow);
        Assert.Contains("window.showConfirm(text,", flow);
        Assert.Contains("for (const row of rows) {", flow);
        Assert.Contains("method: 'PUT'", flow);
        Assert.Contains("body: JSON.stringify({ requestedStatus: 'archived', expectedVersion: row.version })", flow);
        Assert.Contains("L[ARCHIVE_REFUSAL[code]]", flow);
        Assert.Contains("const ARCHIVE_REFUSAL = { planning_session_not_empty: 'ArchiveRefusedNotEmpty' };", js);
        Assert.Contains("fmt(L.DraftsNotArchived || '{0} {1}', failed.length, failed.join(', '))", flow);
        // the mockup band: archive (never delete) every empty draft through the same flow
        Assert.Contains("id=\"vp-archive-empty-drafts\">@Localizer[\"ArchiveEmptyDrafts\"]", View("Index.cshtml"));
        Assert.Contains("const archiveEmptyDrafts = () => archiveDrafts(allRows.filter(isEmptyDraft));", js);
        Assert.DoesNotContain("method: 'DELETE'", js);
        Assert.Equal("{0} boş taslak (0 hedef) var. Aynı dönem ve hafta için tek taslak tutulur.", Resx("tr")["EmptyDraftsBand"]);
        Assert.Equal("Boş taslakları arşivle", Resx("tr")["ArchiveEmptyDrafts"]);
        // an archived plan is never an empty draft (it stays out unless the status filter asks for it)
        Assert.Contains("sStatus(s) !== 'archived'", Between(js, "const isEmptyDraft = s =>", ";\n"));
    }

    // ── 7 · the new-plan drawer: the rep read-only and looking it; the country in the UI language ─────────────

    [Fact]
    public void The_new_plan_rep_is_locked_and_the_country_reads_in_the_ui_language()
    {
        var drawer = View("_NewPlanDrawer.cshtml");
        var rep = Between(drawer, "for=\"vp-np-rep\"", "RepSelfHint");
        Assert.Contains("class=\"input-group vp-locked-field\"", rep);
        Assert.Contains("id=\"vp-np-rep\" class=\"form-control\" disabled aria-readonly=\"true\" tabindex=\"-1\"", rep);
        Assert.Contains("bx-lock-alt", rep);
        Assert.Contains("id=\"vp-np-country-wrap\"", drawer);
        Assert.Contains("Localizer[\"NewPlanAfterSaveHint\"]", drawer);
        Assert.Contains("<i class=\"bx bx-bulb", drawer);
        var js = Script("new-plan.js");
        Assert.Contains("new Intl.DisplayNames([window.VisitPlanningFormat ? window.VisitPlanningFormat.culture() : 'tr'], { type: 'region' })", js);
        Assert.Contains("if (local && local !== code) return local;", js);
        Assert.Contains(".vp-locked-field .form-control:disabled", File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "css", "visit-planning.css")));
    }

    // ── 8 · the new texts in seven languages (+ neutral) and the bridge ───────────────────────────────────────

    [Fact]
    public void The_new_keys_are_in_seven_languages_and_the_bridge()
    {
        var bridged = new[]
        {
            "TargetsReadOnlyApproved", "TargetsReadOnlyPast", "TargetsSubtitle", "DoctorCountPill", "LinkedPharmacyCountPill",
            "SpecialtyFilterAll", "SpecialtyFilterSelected", "SpecialtyClear", "ColRelation", "ColAddress", "PeriodViewNotInPlan",
            "GeneratingWeek", "GenerateWeekStillEmpty", "EmptyWeekNoFrequencyVisits", "EditTargetsLink", "EmptyDraftsBand",
            "ArchiveEmptyDrafts", "ArchiveSelectedDrafts", "ArchiveDraftsConfirm", "DraftsArchived", "DraftsNotArchived",
            "ArchiveRefusedNotEmpty", "OnlyEmptyDraftsArchivable"
        };
        var viewOnly = new[] { "ReadOnlyBadge", "CountrySelfHint", "NewPlanAfterSaveHint" };
        var withArgs = new HashSet<string> { "TargetsReadOnlyApproved", "TargetsReadOnlyPast", "TargetsSubtitle", "DoctorCountPill", "LinkedPharmacyCountPill", "SpecialtyFilterSelected", "EmptyDraftsBand", "ArchiveSelectedDrafts", "ArchiveDraftsConfirm", "DraftsArchived", "DraftsNotArchived" };
        var bridge = View("_IndexL10n.cshtml");
        foreach (var key in bridged.Append("ColSelected"))
        {
            Assert.Contains($"{key} = Localizer[\"{key}\"].Value", bridge);
        }
        var en = Resx("en");
        var tr = Resx("tr");
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in bridged.Concat(viewOnly))
            {
                Assert.True(resx.TryGetValue(key, out var value) && value.Length > 0, $"{key} missing in '{culture}'");
                if (!withArgs.Contains(key)) Assert.DoesNotContain("{0}", value); // an argless localizer value never holds {0}
            }
        }
        foreach (var key in bridged.Concat(viewOnly))
        {
            Assert.NotEqual(en[key], tr[key]);
        }
        Assert.Equal("Salt okunur", tr["ReadOnlyBadge"]);
        Assert.Equal("Uzmanlık: Tümü", tr["SpecialtyFilterAll"]);
        Assert.Equal("{0} · {1} doktor, {2} eczane seçili", tr["TargetsSubtitle"]);
        Assert.Equal("Hedefleri düzenle", tr["EditTargetsLink"]);
        Assert.Equal("Kaydettikten sonra Hedefler sekmesinde bu hafta görülmesi gereken doktorlar önerilir.", tr["NewPlanAfterSaveHint"]);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static string Between(string text, string start, string end)
    {
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
}
