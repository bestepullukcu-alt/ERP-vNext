/**
 * MOD-0155-FU05 Visit Planning — planning-session ("draft plan") DataTables Index (Golden Compact, proxy profile).
 *  - Entity = a planning session. Rows come from GET /CRM/VisitPlanning/api/sessions (client-side; few rows).
 * *  - Row actions: Route (POST /preview → Details), Details, Apply (CanApply). Create is its own page.
 *  - All traffic via the same-origin MVC proxy /CRM/VisitPlanning/api (never a Gateway URL / bearer token).
 *  - WP-VP-4J — the status filter offers EVERY status (fixed); the rep filter is a read-all holder's only; "Action" =
 *    archive the SELECTED empty drafts (a row box per empty draft, a confirm, the existing one-by-one archive update).
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-visit-planning');
    if (!tableEl) return;

    const endpoint = '/CRM/VisitPlanning/api';
    const pageRoot = '/CRM/VisitPlanning';
    const filterCollapseId = 'inlineFilterCollapse';

    let L = window.L10n || {};
    let canGenerate = false;
    let canApply = false;
    let canReadAll = false;
    try {
        const flags = JSON.parse(document.getElementById('visitplanning-page-flags')?.textContent || '{}');
        canGenerate = !!flags.canGenerate;
        canApply = !!flags.canApply;
        canReadAll = !!flags.canReadAll;
    } catch (e) { canGenerate = false; canApply = false; canReadAll = false; }

    let dt = null;
    let addNewBound = false;
    let allRows = [];
    const periodMap = {};
    const emptyFilters = () => ({ sessionStatus: [], cyclePeriodId: '', rep: '' });
    let appliedFilters = emptyFilters();

    // Same-origin proxy profile: the browser sends no bearer token (the MVC proxy attaches it server-side).
    const getAuthHeaders = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const bidi = v => window.VisitPlanningFormat.bidi(v); // WP-VP-4I (7) — data names isolated (<bdi>) for RTL
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const date = v => (v ? window.VisitPlanningFormat.dateShort(v) : '—'); // WP-VP-4H — the app's language
    // ISO-8601 week number (Thursday-based, Monday start) — labels the plan's saved target week.
    const isoWeek = dt => { const d = new Date(dt); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() + 3 - ((d.getDay() + 6) % 7)); const w1 = new Date(d.getFullYear(), 0, 4); return 1 + Math.round(((d - w1) / 86400000 - 3 + ((w1.getDay() + 6) % 7)) / 7); };
    const sWeek = s => { const w = s.targetWeekStart || s.TargetWeekStart; return /^\d{4}-\d{2}-\d{2}$/.test(w || '') ? (L.WeekNumberLabel || '{0}. ' + (L.WeekLabel || 'Week')).replace('{0}', isoWeek(new Date(w))) : '—'; };

    // Defensive field readers — the session DTO fields are read with fallbacks (the backend is unchanged / not typed here).
    const sid = s => s.planningSessionId || s.id || s.sessionId || '';
    const sName = s => s.sessionName || s.name || s.planName || ('#' + String(sid(s)).slice(0, 8));
    const sPeriodId = s => s.cyclePeriodId || s.CyclePeriodId || '';
    const sRep = s => s.resourceDisplayName || s.resourceName || s.resourceId || '';
    const sStatus = s => norm(s.status || s.sessionStatus || 'draft');
    // WP-VP-FIX-1 (D1) — the LIST DTO carries counts (selectedContactCount / selectedPharmacyCount), not the selection
    // arrays the old reader looked for (which is why the column always read 0): "122 doctors · 13 pharmacies", or 0.
    // WP-VP-4B — WP-VP-4A's doctorCount / pharmacyCount win when present (graceful: the FIX-1 counts otherwise).
    const sTargets = s => {
        const doctors = Number(s.doctorCount ?? s.selectedContactCount) || 0;
        const pharmacies = Number(s.pharmacyCount ?? s.selectedPharmacyCount) || 0;
        if (!doctors && !pharmacies) return '0';
        return (L.TargetCountFormat || '{0} · {1}').replace('{0}', doctors).replace('{1}', pharmacies);
    };
    // WP-VP-4B — "X approved · Y draft" (3A approvedWeekCount + 4A draftWeekCount). Without draftWeekCount (before 4A)
    // only "X approved"; without either, a dash.
    const sWeeks = s => {
        const approved = s.approvedWeekCount;
        const draft = s.draftWeekCount;
        if (draft != null) return (L.WeekCountFormat || '{0} · {1}').replace('{0}', Number(approved) || 0).replace('{1}', Number(draft) || 0);
        if (approved != null) return (L.ApprovedWeekCountFormat || '{0}').replace('{0}', Number(approved) || 0);
        return '—';
    };
    // WP-VP-4B — an empty draft has no target at all (3A isEmpty; the counts when the flag is absent). WP-VP-4D
    // (E4-4B-5) — a plan with an approved week is never one (the server refuses to archive it), so it neither counts in
    // the notice nor shows "Delete empty drafts".
    const isEmptyDraft = s => sStatus(s) !== 'committed' && sStatus(s) !== 'archived' && !(Number(s.approvedWeekCount) > 0) && (s.isEmpty === true
        || (s.isEmpty == null && !(Number(s.doctorCount ?? s.selectedContactCount) || 0) && !(Number(s.pharmacyCount ?? s.selectedPharmacyCount) || 0)));
    // A legacy plan was approved for the whole period at once (before the week model): read-only.
    const isLegacy = s => sStatus(s) === 'committed';
    const statusCell = s => {
        const main = isLegacy(s)
            ? badge(L.LegacyPlanBadge, 'success')
            : badge(statusLabel(sStatus(s)), statusTone(sStatus(s)));
        return '<div class="d-flex gap-1 flex-wrap">' + main
            + (isEmptyDraft(s) ? ' <span class="badge bg-label-warning">' + esc(L.EmptyDraftBadge || '') + '</span>' : '') + '</div>';
    };
    const sUpdated = s => s.updatedAt || s.updatedOn || s.modifiedAt || s.lastModifiedAt || s.createdAt || null;

    const statusLabel = v => ({ draft: L.StatusDraft, generated: L.StatusGenerated, committed: L.StatusCommitted, archived: L.StatusArchived }[v] || v);
    // WP-VP-FIX-1 (D2) — a committed / archived plan is read-only: no route generation from its row.
    const isLocked = status => status === 'committed' || status === 'archived';
    const statusTone = v => ({ committed: 'success', draft: 'primary' }[v] || 'secondary');

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorOccurred]).join(' · ')), { status: response.status });
        return body.data;
    };

    const fillSelect = (id, options, keepShowAll) => {
        const el = document.getElementById(id);
        if (!el) return;
        const head = keepShowAll ? `<option value="">${esc(L.ShowAll || 'All')}</option>` : '';
        el.innerHTML = head + (options || []).map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };

    const initSelect2 = () => {
        if (!window.jQuery || !window.jQuery.fn.select2) return;
        const $body = window.jQuery(document.body);
        window.jQuery('#inlineFilterHost .select2').each(function () {
            const $s = window.jQuery(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({ dropdownParent: $body, dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm', placeholder: $s.data('placeholder') || '', minimumResultsForSearch: Infinity, width: 'element', allowClear: !$s.prop('multiple'), closeOnSelect: !$s.prop('multiple') });
        });
    };

    const distinct = key => Array.from(new Set(allRows.map(key).filter(Boolean)));
    // WP-VP-4J (4) — the status options are FIXED: every status of a plan (the status vocabulary, local labels), never
    // only those the loaded rows happen to carry. "committed" is the old whole-period approval (its row badge).
    const STATUS_OPTIONS = ['draft', 'committed', 'archived'];
    const statusOptionLabel = v => (v === 'committed' ? (L.LegacyPlanBadge || statusLabel(v)) : statusLabel(v));
    const loadFilterOptions = () => {
        fillSelect('filterSessionStatus', STATUS_OPTIONS.map(v => ({ value: v, text: statusOptionLabel(v) })), false);
        fillSelect('filterCyclePeriod', distinct(sPeriodId).map(v => ({ value: v, text: periodMap[v] || v })), true);
        initSelect2();
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) { toolbarRow.insertAdjacentElement('afterend', host); host.classList.remove('px-6'); host.classList.add('px-3'); }
    };
    const toggleInlineFilter = () => {
        const el = document.getElementById(filterCollapseId);
        if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).toggle();
    };

    const matchesMulti = (sel, val) => { const n = normArr(sel); return !n.length || n.includes(norm(val)); };
    const matchesSingle = (sel, val) => { const n = norm(sel); return !n || norm(val) === n; };
    const registerTableFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || tableEl.dataset.filterBound === '1') return;
        tableEl.dataset.filterBound = '1';
        window.jQuery.fn.dataTable.ext.search.push((settings, _d, dataIndex, row) => {
            if (settings.nTable !== tableEl) return true;
            const r = row || dt?.row(dataIndex)?.data?.();
            if (!r) return true;
            const rep = canReadAll ? norm(appliedFilters.rep) : '';
            if (rep && norm(sRep(r)).toLowerCase().indexOf(rep.toLowerCase()) === -1) return false;
            // An archived plan (e.g. a cleared-away empty draft) shows only when the status filter asks for it.
            if (sStatus(r) === 'archived' && !normArr(appliedFilters.sessionStatus).includes('archived')) return false;
            return matchesMulti(appliedFilters.sessionStatus, sStatus(r))
                && matchesSingle(appliedFilters.cyclePeriodId, sPeriodId(r));
        });
    };
    const getAppliedFilterCount = () => {
        let n = 0;
        if (normArr(appliedFilters.sessionStatus).length) n++;
        ['cyclePeriodId', 'rep'].forEach(k => { if (norm(appliedFilters[k])) n++; });
        return n;
    };

    const readControls = () => ({
        sessionStatus: window.jQuery('#filterSessionStatus').val() || [],
        cyclePeriodId: document.getElementById('filterCyclePeriod')?.value || '',
        rep: canReadAll ? (document.getElementById('filterRep')?.value || '') : ''
    });

    // Row action menu: Route (preview → Details), Details, Apply (CanApply + draft).
    const actions = row => {
        const id = esc(sid(row));
        const status = sStatus(row);
        const items = [{ key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': id, title: L.ViewDetails } }];
        if (canGenerate && !isLocked(status)) {
            items.push({ className: 'js-route text-primary', icon: 'bx bx-map-alt', text: L.RouteAction, attrs: { 'data-id': id } });
        }
        items.push({ className: 'js-details', icon: 'bx bx-detail', text: L.Details, attrs: { 'data-id': id } });
        // WP-VP-3A — a plan with an approved week is approved week by week (Details); the whole-period apply is not offered.
        if (canApply && !isLocked(status) && !(row.approvedWeekCount > 0)) {
            items.push({ className: 'js-apply text-success', icon: 'bx bx-check-circle', text: L.Apply, attrs: { 'data-id': id } });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    // WP-VP-4B (brief §1) — "My plans" is the rep's view: the rep column is hidden there (room for the manager view, MK-5).
    const viewMode = 'mine';
    // The empty list says what to do next (plan the period), with the drawer button when the user may create.
    const emptyListHtml = () => '<div class="py-4 text-center"><div class="fw-medium mb-2">' + esc(L.ListEmptyTitle || L.EmptyState || '') + '</div>'
        + (canGenerate && hasActivePeriod ? '<button type="button" class="btn btn-sm btn-primary" data-vp-new-plan><i class="bx bx-plus me-1"></i>' + esc(L.ListEmptyAction || L.NewSession || '') + '</button>' : '')
        + '</div>';

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        order: [[9, 'desc']],
        columns: [
            { data: null, defaultContent: '' },
            { data: null }, { data: null }, { data: null }, { data: null }, { data: null }, { data: null }, { data: null }, { data: null }, { data: null }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            // WP-VP-4J (5) — the row box: an empty draft only (0 targets); any other row's box is disabled with the reason.
            { targets: 1, visible: canGenerate, orderable: false, searchable: false, className: 'cell-fit', render: (v, t, row) => t === 'display' ? rowCheck(row) : '' },
            { targets: 2, render: (v, t, row) => t === 'display' ? `<span class="fw-medium text-heading">${bidi(sName(row))}</span>` : sName(row) },
            { targets: 3, render: (v, t, row) => t === 'display' ? bidi(periodMap[sPeriodId(row)] || sPeriodId(row) || '—') : (periodMap[sPeriodId(row)] || '') },
            { targets: 4, render: (v, t, row) => esc(sWeek(row)) },
            { targets: 5, visible: viewMode !== 'mine', render: (v, t, row) => t === 'display' ? bidi(sRep(row) || '—') : (sRep(row) || '') },
            { targets: 6, render: (v, t, row) => t === 'display' ? statusCell(row) : sStatus(row) },
            { targets: 7, render: (v, t, row) => esc(sTargets(row)) },
            { targets: 8, orderable: false, render: (v, t, row) => esc(sWeeks(row)) },
            { targets: 9, render: (v, t, row) => t === 'display' ? date(sUpdated(row)) : (sUpdated(row) || '') },
            { targets: 10, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: emptyListHtml(), processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(canGenerate ? (L.NewSession || '') : '', {}, {
            filterBtn: { text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' }, action: () => toggleInlineFilter() },
            // WP-VP-4J (5) — "Action" → "Archive selected empty drafts (N)" (a writer's only; off without a selection).
            collectionBtns: canGenerate ? [{ text: archiveSelectedLabel(), icon: 'bx-archive', className: 'js-vp-archive-selected', action: () => archiveSelected() }] : []
        }, { exportColumns: [2, 3, 4, 5, 6, 7, 8], colvisColumns: [2, 3, 4, 5, 6, 7, 8] }),
        initComplete: function () {
            mountInlineFilter();
            void setupFilters(this.api());
            paintArchiveAction();
            // WP-VP-4B (MK-1) — "New plan" opens the right-hand drawer (new-plan.js); the Create page stays as a fallback.
            // Without an active / future period it is switched off (the band says why).
            const addNew = document.querySelector('.add-new');
            if (addNew && !hasActivePeriod) { addNew.setAttribute('disabled', 'disabled'); addNew.classList.add('disabled'); }
            if (canGenerate && !addNewBound) {
                addNew?.addEventListener('click', e => {
                    e.preventDefault();
                    if (!hasActivePeriod) return;
                    if (window.VisitPlanningNewPlan) window.VisitPlanningNewPlan.open(); else window.location.assign(`${pageRoot}/Create`);
                });
                addNewBound = true;
            }
        },
        drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount()); }
    });

    const setupFilters = async api => {
        loadFilterOptions();
        try { api.rows().invalidate().draw(false); } catch (e) { /* not ready */ }
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = readControls();
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            const el = document.getElementById(filterCollapseId);
            if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', e => {
            e.preventDefault();
            appliedFilters = emptyFilters();
            const repInput = document.getElementById('filterRep'); if (repInput) repInput.value = '';
            window.jQuery('#filterSessionStatus').val(null).trigger('change');
            window.jQuery('#filterCyclePeriod').val('').trigger('change');
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, 0);
        });
    };

    // WP-VP-4B — is there a period a new plan can be made for (active today or still ahead)? Unknown ⇒ assume yes.
    let hasActivePeriod = true;
    const loadPeriods = async () => {
        try {
            const data = await envelope(await fetch(`${endpoint}/cycle-periods`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            const items = data?.items || (Array.isArray(data) ? data : []);
            items.forEach(p => { const id = p.cyclePeriodId || p.id; if (id) periodMap[id] = p.cycleName || p.cycleCode || p.name || id; });
            const today = new Date(); today.setHours(0, 0, 0, 0);
            hasActivePeriod = items.some(p => { const end = new Date(p.endDate || p.end); return !isNaN(end) && end >= today; });
        } catch (e) { /* period names degrade to ids */ }
    };

    // ── WP-VP-4B — page bands (brief §7) + empty drafts ──
    const band = (tone, icon, text) => `<div class="alert alert-${tone} py-2 small" role="status"><i class="bx ${icon} me-1" aria-hidden="true"></i>${esc(text)}</div>`;
    let territoryUnassigned = false;
    const loadTerritoryStatus = async () => {
        try {
            const data = await envelope(await fetch(`${endpoint}/my-accounts?pageSize=1`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            territoryUnassigned = !!data && data.territoryStatus === 'unassigned';
        } catch (e) { territoryUnassigned = false; }
    };
    const renderBands = loadError => {
        const host = document.getElementById('vp-list-bands'); if (!host) return;
        host.innerHTML = (loadError ? band('danger', 'bx-error-circle', L.ListLoadError || L.ErrorOccurred || '') : '')
            + (!hasActivePeriod ? band('warning', 'bx-calendar-exclamation', L.NoActivePeriodBand || '') : '')
            + (territoryUnassigned ? band('warning', 'bx-map-alt', L.TerritoryUnassignedBanner || '') : '');
        const empty = allRows.filter(isEmptyDraft);
        const box = document.getElementById('vp-empty-drafts');
        if (box) {
            box.classList.toggle('d-none', empty.length === 0);
            const t = document.getElementById('vp-empty-drafts-text');
            if (t) t.textContent = (L.EmptyDraftsBand || '{0}').replace('{0}', empty.length);
        }
    };
    // ── WP-VP-4J (5) — archive empty drafts: the band ("Archive empty drafts" = all of them) and the Action menu ("Archive
    // selected empty drafts (N)"). The existing one-by-one archive update (requestedStatus = archived) per draft — no new
    // write endpoint; CRM refuses a plan that is not empty with 409 planning_session_not_empty, and the result says so.
    const selectedIds = new Set();
    const rowCheck = row => {
        const id = sid(row);
        const ok = canGenerate && isEmptyDraft(row);
        return '<input type="checkbox" class="form-check-input js-vp-row-check" data-id="' + esc(id) + '" aria-label="' + esc(L.ColSelected || '') + '"'
            + (ok ? (selectedIds.has(id) ? ' checked' : '') : ' disabled title="' + esc(L.OnlyEmptyDraftsArchivable || '') + '"') + '>';
    };
    const fmt = (tpl, ...args) => args.reduce((t, a, i) => t.split('{' + i + '}').join(String(a)), String(tpl || ''));
    const archiveSelectedLabel = () => '<span class="d-flex align-items-center"><i class="icon-base bx bx-archive me-2"></i>' + esc(fmt(L.ArchiveSelectedDrafts || '{0}', selectedIds.size)) + '</span>';
    const paintArchiveAction = () => {
        if (!dt || !dt.button) return;
        try {
            const b = dt.button('.js-vp-archive-selected');
            b.text(archiveSelectedLabel());
            b.enable(selectedIds.size > 0);
        } catch (e) { /* the Action menu is not drawn (no entry) */ }
    };
    const ARCHIVE_REFUSAL = { planning_session_not_empty: 'ArchiveRefusedNotEmpty' };
    const archiveDrafts = rows => {
        if (!rows.length) { window.showToast?.(L.OnlyEmptyDraftsArchivable || '', 'info'); return; }
        const go = async () => {
            let done = 0;
            const failed = [];
            for (const row of rows) {
                const r = await fetch(`${endpoint}/sessions/${encodeURIComponent(sid(row))}`, {
                    method: 'PUT', credentials: 'same-origin', headers: getAuthHeaders(),
                    body: JSON.stringify({ requestedStatus: 'archived', expectedVersion: row.version })
                }).catch(() => null);
                if (r && r.ok) { done++; continue; }
                const body = r ? await r.json().catch(() => ({})) : {};
                const code = ((body && body.errors) || [])[0] || (r ? 'HTTP ' + r.status : '');
                failed.push(sName(row) + ' (' + (L[ARCHIVE_REFUSAL[code]] || code || L.ErrorOccurred || '') + ')');
            }
            const text = fmt(L.DraftsArchived || '{0}', done) + (failed.length ? ' ' + fmt(L.DraftsNotArchived || '{0} {1}', failed.length, failed.join(', ')) : '');
            window.showToast?.(text, failed.length ? 'warning' : 'success');
            selectedIds.clear();
            await reload();
            renderBands(false);
            paintArchiveAction();
        };
        const text = fmt(L.ArchiveDraftsConfirm || '{0}', rows.length);
        if (window.showConfirm) window.showConfirm(text, () => { void go(); }, { type: 'warning', confirmButtonText: L.ArchiveEmptyDrafts, subtext: '' });
        else if (window.confirm(text)) void go();
    };
    const archiveEmptyDrafts = () => archiveDrafts(allRows.filter(isEmptyDraft));
    const archiveSelected = () => archiveDrafts(allRows.filter(r => selectedIds.has(sid(r)) && isEmptyDraft(r)));

    const fetchRows = async () => {
        const data = await envelope(await fetch(`${endpoint}/sessions`, { credentials: 'same-origin', headers: getAuthHeaders() }));
        // WP-VP-3A — an archived plan (e.g. an empty draft that was cleared away) is out of the list by default; WP-VP-4J —
        // the status filter's "Archived" shows it (the table filter above).
        return data?.items || (Array.isArray(data) ? data : []);
    };

    const reload = async () => { allRows = await fetchRows(); if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); } };

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        registerTableFilter();
        let loadError = false;
        try {
            await Promise.all([loadPeriods(), loadTerritoryStatus()]);
            allRows = await fetchRows();
            dt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(buildConfig()) : buildConfig());
            dt.on('column-visibility.dt search.dt order.dt', () => window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount()));
        } catch (error) {
            loadError = true;
            window.showToast?.(error.message || L.ErrorOccurred, 'error');
        } finally {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
            renderBands(loadError);
        }
    };
    document.getElementById('vp-archive-empty-drafts')?.addEventListener('click', archiveEmptyDrafts);
    tableEl.addEventListener('change', e => {
        const cb = e.target.closest('.js-vp-row-check'); if (!cb) return;
        const row = allRows.find(r => sid(r) === cb.dataset.id);
        if (cb.checked && row && isEmptyDraft(row)) selectedIds.add(cb.dataset.id);
        else { selectedIds.delete(cb.dataset.id); cb.checked = false; }
        paintArchiveAction();
    });

    document.addEventListener('click', event => {
        const quickView = event.target.closest('.js-quick-view');
        if (quickView) { event.preventDefault(); if (quickView.dataset.id) window.location.assign(`${pageRoot}/Details/${quickView.dataset.id}`); return; }

        const details = event.target.closest('.js-details');
        if (details) { event.preventDefault(); if (details.dataset.id) window.location.assign(`${pageRoot}/Details/${details.dataset.id}`); return; }

        const route = event.target.closest('.js-route');
        if (route) {
            event.preventDefault();
            const id = route.dataset.id;
            // Generate the route for this session (dry-run preview), then open Details where the week grid renders.
            fetch(`${endpoint}/preview`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders(), body: JSON.stringify({ planningSessionId: id }) })
                .then(r => r.json().catch(() => ({})).then(b => ({ ok: r.ok, b })))
                .then(({ ok, b }) => { if (!ok) window.showToast?.((b.errors || [L.PreviewFailed]).join(' · '), 'error'); })
                .catch(() => window.showToast?.(L.PreviewFailed, 'error'))
                .finally(() => window.location.assign(`${pageRoot}/Details/${id}`));
            return;
        }

        const apply = event.target.closest('.js-apply');
        if (apply) {
            event.preventDefault();
            const id = apply.dataset.id;
            window.location.assign(`${pageRoot}/Details/${id}`);
            return;
        }
    });

    init();
})(window, document);
