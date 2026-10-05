/**
 * MOD-0155-FU06 Cycle Capacities — DataTables Index (Golden Compact aligned, proxy profile).
 *  - Native toolbar search, Select2 filter chips mounted under the toolbar
 *  - SaveView (filter + search + colvis + colorder) via personalizationClient
 *  - Row actions: Details + Edit + Archive. Create/Edit/Details are their OWN PAGES (Golden Compact).
 *  - All traffic via same-origin MVC proxy /CRM/CycleCapacities/api (never a Gateway URL / bearer token)
 *  - There is NO delete and NO bulk delete anywhere (retiring a capacity is Archive), and no approve action:
 *    approving an ESTIMATE is follow-up F-APPROVAL, so this page cannot offer it.
 *  - WP-CYC-UI-2: the estimated-visit / average-FTE / "not calculable" cells are filled LAZILY for the rows on the
 *    current page only — one calculation read per visible row, cached for the life of the page. The estimate reaches
 *    the working calendar, so it is never asked for the whole set; and the figure is the CRM's (totals.visits), never
 *    computed here. K-4: an unresolved calendar shows "not calculable", never a number.
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-cycle-capacities');
    if (!tableEl) return;

    const endpoint = '/CRM/CycleCapacities/api';
    const pageRoot = '/CRM/CycleCapacities';
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'CycleCapacities' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8];
    const totalColumnCount = 10;
    const baseOrder = [[1, 'asc']];

    let L = window.CycleCapacitiesL10n || window.L10n || {};
    // The create affordance exists ONCE, in the DataTable toolbar, and it obeys the same server-side permission the
    // page uses. A parse failure is read as "no permission", because guessing the permissive answer is the wrong way
    // to be wrong.
    let canManage = false;
    try { canManage = !!JSON.parse(document.getElementById('cyclecapacity-page-flags')?.textContent || '{}').canManage; }
    catch (e) { canManage = false; }

    let dt = null;
    let contract = null;
    let addNewBound = false;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    const emptyFilters = () => ({ year: '', cyclePeriodId: '', calendarCountryCode: [], archived: '' });
    let appliedFilters = emptyFilters();
    let allRows = [];

    const getAuthHeaders = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    // One fixed presentation for every date on this page, matching the sibling CyclePeriods grid exactly: a window that
    // reads one way there and another way here is a window nobody trusts.
    // DAY_FORMAT pins timeZone: 'UTC' because a period's window is stored as a UTC-midnight DAY — a browser west of
    // Greenwich would otherwise render that instant as the previous evening and drop a day.
    // STAMP_FORMAT does NOT pin it: UpdatedAt is a real moment, and "when was this last touched?" is answered against
    // the reader's own clock.
    // WP-CYC-UI-2: dates and numbers in the READER's language (document lang), not a fixed en-US.
    const lang = document.documentElement.lang || undefined;
    const DAY_FORMAT = { month: 'short', day: '2-digit', year: '2-digit', timeZone: 'UTC' };
    const STAMP_FORMAT = { month: 'short', day: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' };
    const day = v => v ? new Date(v).toLocaleDateString(lang, DAY_FORMAT) : '—';
    const stamp = v => v ? new Date(v).toLocaleString(lang, STAMP_FORMAT) : '—';
    const numberFormat = new Intl.NumberFormat(lang);
    const fteFormat = new Intl.NumberFormat(lang, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const hasVal = v => Array.isArray(v) ? normArr(v).length > 0 : norm(v).length > 0;

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
    const syncMultiSelectSummary = $select => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;
        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) { $summary = window.jQuery('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = window.jQuery('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = window.jQuery('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = window.jQuery('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }
        const placeholder = norm($select.data('placeholder')) || '';
        const selectedValues = normArr($select.val());
        const selectedTexts = ($select.select2('data') || []).map(i => norm(i.text)).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clear = window.jQuery('<span class="dt-multi-clear-btn" role="button" title="' + (L.Reset || '') + '">&times;</span>');
            $clear.on('mousedown', e => { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clear);
        }
    };
    const initSelect2 = () => {
        if (!window.jQuery || !window.jQuery.fn.select2) return;
        const $body = window.jQuery(document.body);
        window.jQuery('#inlineFilterHost .select2').each(function () {
            const $s = window.jQuery(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            if ($s.prop('multiple')) {
                $s.select2({ dropdownParent: $body, dropdownCssClass: 'dt-inline-filter-dropdown', containerCssClass: 'dt-inline-filter-multi', selectionCssClass: 'form-select form-select-sm', placeholder: $s.data('placeholder') || '', minimumResultsForSearch: Infinity, width: 'element', closeOnSelect: false });
                $s.off('change.select2-summary').on('change.select2-summary', () => syncMultiSelectSummary($s));
                window.requestAnimationFrame(() => syncMultiSelectSummary($s));
            } else {
                $s.select2({ dropdownParent: $body, dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm', placeholder: $s.data('placeholder') || '', minimumResultsForSearch: Infinity, width: 'element', allowClear: true });
            }
        });
    };

    // Filter options come from the LOADED ROWS. A hardcoded list would offer a value the runtime does not know, and a
    // fixed one would go stale the day a new period is authored.
    const loadFilterOptions = () => {
        const years = new Set();
        const periods = new Map();
        const countries = new Set();
        allRows.forEach(r => {
            if (r.cycleYear) years.add(String(r.cycleYear));
            if (r.cyclePeriodId) periods.set(r.cyclePeriodId, `${r.cycleCode || ''} · ${r.cycleName || ''}`.trim());
            if (r.calendarCountryCode) countries.add(r.calendarCountryCode);
        });
        fillSelect('filterYear', Array.from(years).sort().reverse().map(v => ({ value: v, text: v })), true);
        fillSelect('filterCyclePeriodId', Array.from(periods, ([value, text]) => ({ value, text })), true);
        fillSelect('filterCalendarCountryCode', Array.from(countries).sort().map(v => ({ value: v, text: v })), false);
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
    const bindInlineFilterA11y = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const el = document.getElementById(filterCollapseId);
        if (!btn || !el || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        el.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        el.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };

    const matchesMulti = (sel, val) => { const n = normArr(sel); return !n.length || n.includes(norm(val)); };
    const matchesSingle = (sel, val) => { const n = norm(sel); return !n || norm(val) === n; };
    const matchesArchived = (sel, row) => {
        const n = norm(sel);
        if (n === 'all') return true;
        if (n === 'only') return !!row.isArchived;
        return !row.isArchived;
    };
    const registerTableFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || tableEl.dataset.filterBound === '1') return;
        tableEl.dataset.filterBound = '1';
        window.jQuery.fn.dataTable.ext.search.push((settings, _d, dataIndex, row) => {
            if (settings.nTable !== tableEl) return true;
            const r = row || dt?.row(dataIndex)?.data?.();
            if (!r) return true;
            return matchesSingle(appliedFilters.year, r.cycleYear)
                && matchesSingle(appliedFilters.cyclePeriodId, r.cyclePeriodId)
                && matchesMulti(appliedFilters.calendarCountryCode, r.calendarCountryCode)
                && matchesArchived(appliedFilters.archived, r);
        });
    };
    const getAppliedFilterCount = () => [
        appliedFilters.year, appliedFilters.cyclePeriodId,
        appliedFilters.calendarCountryCode, appliedFilters.archived
    ].filter(hasVal).length;

    const readControls = () => ({
        year: document.getElementById('filterYear')?.value || '',
        cyclePeriodId: document.getElementById('filterCyclePeriodId')?.value || '',
        calendarCountryCode: window.jQuery('#filterCalendarCountryCode').val() || [],
        archived: document.getElementById('filterArchived')?.value || ''
    });
    const writeControls = f => {
        window.jQuery('#filterYear').val(f.year || '').trigger('change');
        window.jQuery('#filterCyclePeriodId').val(f.cyclePeriodId || '').trigger('change');
        window.jQuery('#filterCalendarCountryCode').val(normArr(f.calendarCountryCode)).trigger('change');
        window.jQuery('#filterArchived').val(f.archived || '').trigger('change');
    };

    // A closed period freezes its capacity in every direction, so a frozen row offers no mutation action at all.
    // An archived row offers none either: archiving is one-way here (a fresh capacity is created instead).
    const actions = row => {
        const id = esc(row.cycleCapacityId);
        const items = [{
            key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show',
            attrs: { 'data-id': id, title: L.QuickView }
        }];
        if (canManage && row.isEditable && !row.isArchived) {
            items.push({ key: 'edit', className: 'js-edit-capacity', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': id } });
            items.push({ className: 'js-archive-capacity text-warning', icon: 'bx bx-archive-in', text: L.ArchiveCycleCapacity, attrs: { 'data-id': id, 'data-name': esc(row.cycleName || row.cycleCode || '') } });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    // ── lazy per-row estimate (visible rows only, cached) ─────────────────────────────────────────────────────────
    // id → { state: 'pending' | 'done' | 'error', resolution, visits, averageFte }
    const calcCache = new Map();
    const calcOf = row => calcCache.get(row.cycleCapacityId);

    const fetchCalculation = async id => {
        try {
            const response = await fetch(`${endpoint}/capacities/${encodeURIComponent(id)}/calculation`, { credentials: 'same-origin', headers: getAuthHeaders() });
            // An unresolved calendar answers 503 WITH a body — the body is the answer.
            const body = await response.json().catch(() => null);
            const data = body?.data;
            if (!data) { calcCache.set(id, { state: 'error' }); return; }
            const resolved = data.resolution === 'resolved' && !!data.totals;
            calcCache.set(id, {
                state: 'done',
                resolution: data.resolution,
                visits: resolved ? data.totals.visits : null,
                averageFte: resolved ? data.totals.averageFte : null
            });
        } catch (e) {
            calcCache.set(id, { state: 'error' });
        }
    };

    let calcBatchRunning = false;
    const fillVisibleCalculations = async api => {
        if (calcBatchRunning || !api) return;
        const ids = api.rows({ page: 'current', search: 'applied' }).data().toArray()
            .map(r => r.cycleCapacityId).filter(id => id && !calcCache.has(id));
        if (ids.length === 0) return;
        calcBatchRunning = true;
        ids.forEach(id => calcCache.set(id, { state: 'pending' }));
        try {
            await Promise.all(ids.map(fetchCalculation));
        } finally {
            calcBatchRunning = false;
        }
        api.rows().invalidate('data');
        api.draw(false);
    };

    const pendingCell = () => `<span class="spinner-border spinner-border-sm text-muted" role="status" aria-label="${esc(L.Calculating || '')}"></span>`;

    const visitsCell = row => {
        const c = calcOf(row);
        if (!c || c.state === 'pending') return pendingCell();
        if (c.state === 'error' || c.visits === null || c.visits === undefined) return '<span class="text-muted">—</span>';
        return `<span class="fw-medium text-heading">${esc(numberFormat.format(c.visits))}</span>`;
    };

    const fteCell = row => {
        const c = calcOf(row);
        if (!c || c.state === 'pending') return pendingCell();
        return c.averageFte === null || c.averageFte === undefined ? '—' : esc(fteFormat.format(c.averageFte));
    };

    const periodCell = row => `<div class="d-flex flex-column" title="${esc(row.cycleName || '')}">
            <span class="fw-medium text-heading">${esc(row.cycleCode || '—')}</span>
            <small class="text-muted">${esc(windowCell(row))}</small>
        </div>`;

    // The typical visit (min) — with an "old model" badge when the row still uses the legacy arithmetic.
    const typicalCell = row => {
        const minutes = row.typicalVisitMinutes ?? row.minutesPerVisit;
        const legacy = row.visitModel === 'legacy' ? ` ${badge(L.LegacyBadge, 'info')}` : '';
        return `${esc(minutes === null || minutes === undefined ? '—' : numberFormat.format(minutes))} ${esc(L.UnitMinutesShort || '')}${legacy}`;
    };

    const limitsCell = row => `${esc(row.maxPromoProducts ?? '—')} / ${esc(row.maxNonPromoProducts ?? '—')}`;

    // One Status cell: archived, then whether the PINNED PERIOD has closed (editability is derived — the capacity has no
    // status of its own), then whether the estimate could be computed at all.
    const statusCell = row => {
        if (row.isArchived) return badge(L.ArchivedOnly, 'secondary');
        if (!row.isEditable) return badge(L.PeriodClosedLock, 'secondary');
        const c = calcOf(row);
        if (c?.state === 'error' || (c?.state === 'done' && c.resolution !== 'resolved')) return badge(L.StatusNotCalculable, 'warning');
        return badge(L.StatusEditable, 'success');
    };

    const windowCell = row => (row.cycleStartDate && row.cycleEndDate)
        ? `${day(row.cycleStartDate)} – ${day(row.cycleEndDate)}`
        : '—';

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        colReorder: { columns: ':gt(0):not(:last-child)' },
        order: baseOrder,
        columns: [
            { data: null, defaultContent: '' }, { data: 'cycleCode' }, { data: 'calendarCountryCode' },
            { data: null }, { data: 'typicalVisitMinutes' }, { data: null },
            { data: 'maxPromoProducts' }, { data: 'isArchived' }, { data: 'updatedAt' }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 1, render: (v, t, row) => t === 'display' ? periodCell(row) : `${row.cycleYear || ''}-${String(row.cycleSequenceInYear || 0).padStart(2, '0')} ${v || ''}` },
            { targets: 3, className: 'text-end', render: (v, t, row) => t === 'display' ? visitsCell(row) : (calcOf(row)?.visits ?? -1) },
            { targets: 4, render: (v, t, row) => t === 'display' ? typicalCell(row) : (v ?? 0) },
            { targets: 5, className: 'text-end', render: (v, t, row) => t === 'display' ? fteCell(row) : (calcOf(row)?.averageFte ?? -1) },
            { targets: 6, render: (v, t, row) => t === 'display' ? limitsCell(row) : (v ?? 0) },
            { targets: 7, render: (v, t, row) => t === 'display' ? statusCell(row) : (v ? '1' : '0') },
            { targets: 8, render: v => stamp(v) },
            { targets: 9, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: L.EmptyState, processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(canManage ? (L.CreateCycleCapacity || '') : '', { href: `${pageRoot}/Create` }, {
            filterBtn: { text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' }, action: () => toggleInlineFilter() },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn', attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    try { await saveDefaultView(currentView(api || dt)); setSaveFilterVisible(false); window.showToast?.(L.SaveView || '', 'success'); }
                    catch (err) { if (!err?.authHandled) { console.error(err); window.showToast?.(L.ErrorOccurred, 'error'); } }
                }
            }
        }, { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes }),
        initComplete: function () {
            mountInlineFilter();
            bindInlineFilterA11y();
            void setupFilters(this.api());
            // Golden Compact: authoring is a page, so the toolbar button navigates instead of opening a panel.
            if (canManage && !addNewBound) { document.querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); window.location.assign(`${pageRoot}/Create`); }); addNewBound = true; }
            setTimeout(() => { saveFilterArmed = true; }, 0);
        },
        drawCallback: function () {
            window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount());
            void fillVisibleCalculations(this.api());
        }
    });

    const setupFilters = async api => {
        loadFilterOptions();
        try { api.rows().invalidate().draw(false); } catch (e) { /* table not ready */ }
        applySavedTableState(api, defaultViewState);
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = readControls();
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            const el = document.getElementById(filterCollapseId);
            if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', e => {
            e.preventDefault();
            applySavedTableState(api, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });
    };

    const loadContract = async () => {
        try {
            contract = await envelope(await fetch(`${endpoint}/contract`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            if (!contract?.isReady || !contract?.features?.supportsCycleCapacity) throw new Error(L.ContractUnavailable);
            return true;
        } catch (error) {
            window.showToast?.(error.message || L.ContractUnavailable, 'error');
            return false;
        }
    };

    // includeArchived is always true: the ARCHIVED filter is a view choice made client-side, so a reader can flip it
    // without a round trip and the saved view can remember it.
    const fetchRows = async () => (await envelope(await fetch(`${endpoint}/capacities?includeArchived=true`, { credentials: 'same-origin', headers: getAuthHeaders() })))?.items || [];

    const reload = async () => {
        allRows = await fetchRows();
        calcCache.clear();
        if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); }
        loadFilterOptions();
    };

    // Every mutating action lands back on the SAME lifecycle: reload the rows, then toast.
    // NOTE: the shared DitenDataTable.reloadWithToast helper is deliberately NOT used here — it drives
    // dt.ajax.reload(), and this table is client-side (`data: allRows`, like its CRM siblings), so calling it would
    // throw. The verifier check for that shared helper is therefore an expected N/A for this module.
    const reloadAndToast = async messageKey => {
        await reload();
        window.showToast?.(messageKey, 'success');
    };

    const post = async (url, successKey) => {
        try {
            await envelope(await fetch(url, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
            await reloadAndToast(successKey);
        } catch (error) { window.showToast?.(error.message || L.ErrorOccurred, 'error'); }
    };

    const captureColVis = api => { const r = {}; saveViewColumnIndexes.forEach(ci => { try { r[ci] = !!api.column(ci).visible(); } catch (e) {} }); return r; };
    const captureColOrder = api => { try { const o = api?.colReorder?.order?.(); return Array.isArray(o) && o.length === totalColumnCount ? o.map(Number) : null; } catch (e) { return null; } };
    const applyColVis = (api, cv) => { if (!cv) return; saveViewColumnIndexes.forEach(ci => { if (typeof cv[ci] === 'boolean') { try { api.column(ci).visible(cv[ci], false); } catch (e) {} } }); };
    const applyColOrder = (api, co) => { if (!Array.isArray(co) || co.length !== totalColumnCount || typeof api?.colReorder?.order !== 'function') return; try { api.colReorder.order(co, true); } catch (e) {} };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = true; return a; }, {});
    const currentView = api => ({ filters: Object.assign({}, appliedFilters), search: norm(api.search()), colVis: captureColVis(api), columnOrder: captureColOrder(api), order: api.order() });
    const serializeView = v => JSON.stringify({
        filters: Object.keys(v?.filters || {}).sort().reduce((a, k) => { a[k] = Array.isArray(v.filters[k]) ? normArr(v.filters[k]).slice().sort() : norm(v.filters[k]); return a; }, {}),
        search: norm(v?.search), colVis: v?.colVis || defaultColVis(),
        columnOrder: Array.isArray(v?.columnOrder) ? v.columnOrder : Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });
    const getResetBaselineState = () => ({ filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder });
    const setSaveFilterVisible = show => { const b = document.querySelector('.dt-save-filter-btn'); if (!b) return; b.classList.toggle('d-none', !show); window.DtDefaults?.refreshButtonGroupRadii?.(); };
    const isDirtyComparedToDefault = api => serializeView(currentView(api)) !== serializeView(defaultViewState || getResetBaselineState());

    const getViewId = sv => sv?.id || sv?.Id || sv?._id || null;
    const getSavedViewName = sv => sv?.viewName || sv?.ViewName || '';
    const getViewDef = sv => { const raw = sv?.viewDefinition ?? sv?.ViewDefinition ?? {}; if (typeof raw === 'string') { try { return JSON.parse(raw); } catch (e) { return {}; } } return raw || {}; };
    const mapViewToState = sv => { const d = getViewDef(sv); return { filters: Object.assign(emptyFilters(), d.filters || {}), search: norm(d.search), colVis: d.colVis || null, columnOrder: Array.isArray(d.columnOrder) ? d.columnOrder : null, order: Array.isArray(d.order) ? d.order : null }; };
    const loadDefaultView = async () => {
        defaultViewRecord = null; defaultViewState = null;
        if (!personalizationClient?.getViews) return;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(v => v?.isDefault === true || v?.IsDefault === true) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapViewToState(defaultViewRecord) : null;
        } catch (e) { if (!e?.authHandled) console.error('[CycleCapacities SaveView] load failed', e); }
    };
    const saveDefaultView = async view => {
        if (!personalizationClient?.saveView) return;
        const payload = { moduleKey: personalizationContext.moduleKey, pageKey: personalizationContext.pageKey, viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Default').trim(), viewDefinition: view, isDefault: true, visibility: 'private' };
        const id = getViewId(defaultViewRecord);
        const saved = id ? await personalizationClient.updateView(id, payload) : await personalizationClient.saveView(payload);
        const rec = saved?.data || saved?.Data || saved;
        defaultViewRecord = rec && typeof rec === 'object' ? rec : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = view;
    };
    const applySavedTableState = (api, view) => {
        const v = view || getResetBaselineState();
        appliedFilters = Object.assign(emptyFilters(), v.filters || {});
        writeControls(appliedFilters);
        applyColOrder(api, v.columnOrder);
        applyColVis(api, v.colVis);
        api.search(v.search || '');
        api.order(v.order || baseOrder);
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        registerTableFilter();
        try {
            if (!(await loadContract())) return;
            await loadDefaultView();
            allRows = await fetchRows();
            dt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(buildConfig()) : buildConfig());
            dt.on('column-visibility.dt search.dt order.dt column-reorder.dt columns-reordered.dt', () => {
                window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            });
        } catch (error) {
            window.showToast?.(error.message || L.ErrorOccurred, 'error');
        } finally {
            // WP-CYC-UI-FIX-1 — the shared placeholder hides the table for as long as it EXISTS, so it is removed (the
            // same act dt-defaults.js performs on the first draw); this also covers an init that failed before drawing.
            document.getElementById('skeleton-loader')?.remove();
        }
    };

    // ── quick view (WP-CYC-UI-FIX-1) ─────────────────────────────────────────────────────────────────────────────
    // The golden #offcanvasDetailsPreview, filled from the row ALREADY in allRows and from the per-row estimate ONLY if
    // the lazy read already produced it (calcCache) — no request of any kind. Every value is written with textContent.
    const quickViewEl = document.getElementById('offcanvasDetailsPreview');
    const qvText = name => quickViewEl?.dataset?.[name] || '';
    const qvSet = (id, value) => {
        const el = document.getElementById(id);
        if (el) el.textContent = value === null || value === undefined || value === '' ? '—' : String(value);
    };

    const openCapacityQuickView = id => {
        const row = allRows.find(r => String(r.cycleCapacityId) === String(id));
        if (!row || !quickViewEl) return false;

        qvSet('capacityPreviewTitle', row.cycleCode);
        qvSet('capacityPreviewSubtitle', row.cycleName);
        qvSet('cvPeriod', `${row.cycleCode || '—'} · ${windowCell(row)}`);
        qvSet('cvCountry', row.calendarCountryCode);

        // Typical visit (min), with the "old model" badge as a separate element.
        const typical = document.getElementById('cvTypicalVisit');
        if (typical) {
            const minutes = row.typicalVisitMinutes ?? row.minutesPerVisit;
            typical.textContent = minutes === null || minutes === undefined ? '—' : `${numberFormat.format(minutes)} ${qvText('textMinutes')}`.trim();
            if (row.visitModel === 'legacy') {
                const legacy = document.createElement('span');
                legacy.className = 'badge bg-label-info ms-2';
                legacy.textContent = qvText('textLegacy');
                typical.appendChild(legacy);
            }
        }

        // The estimate: only what the lazy per-row read already answered (K-4: unresolved = "not calculable").
        const c = calcOf(row);
        if (!c || c.state === 'pending') {
            qvSet('cvVisits', qvText('textCalculating'));
            qvSet('cvFte', qvText('textCalculating'));
        } else if (c.state === 'error' || c.visits === null || c.visits === undefined) {
            qvSet('cvVisits', qvText('textNotCalculable'));
            qvSet('cvFte', null);
        } else {
            qvSet('cvVisits', numberFormat.format(c.visits));
            qvSet('cvFte', c.averageFte === null || c.averageFte === undefined ? null : fteFormat.format(c.averageFte));
        }

        qvSet('cvLimits', `${row.maxPromoProducts ?? '—'} / ${row.maxNonPromoProducts ?? '—'}`);

        const details = document.getElementById('capacityPreviewDetails');
        if (details) details.setAttribute('href', `${pageRoot}/Details/${encodeURIComponent(row.cycleCapacityId)}`);

        window.bootstrap?.Offcanvas?.getOrCreateInstance(quickViewEl).show();
        return true;
    };
    // ── end quick view ───────────────────────────────────────────────────────────────────────────────────────────

    // Quick View opens the side summary (WP-CYC-UI-FIX-1); Edit NAVIGATES to its own page (Golden Compact).
    document.addEventListener('click', event => {
        const quickView = event.target.closest('.js-quick-view');
        if (quickView) {
            event.preventDefault();
            // A row the page somehow does not hold falls back to the details page rather than doing nothing.
            if (quickView.dataset.id && !openCapacityQuickView(quickView.dataset.id)) {
                window.location.assign(`${pageRoot}/Details/${encodeURIComponent(quickView.dataset.id)}`);
            }
            return;
        }

        const edit = event.target.closest('.js-edit-capacity');
        if (edit) {
            event.preventDefault();
            if (edit.dataset.id) window.location.assign(`${pageRoot}/Edit/${edit.dataset.id}`);
            return;
        }

        const archive = event.target.closest('.js-archive-capacity');
        if (!archive) return;
        event.preventDefault();
        window.showConfirm?.(L.ArchiveCycleCapacityConfirm, () => post(`${endpoint}/capacities/${archive.dataset.id}/archive`, L.RecordArchived),
            { entityName: archive.dataset.name, type: 'warning', confirmButtonText: L.ArchiveCycleCapacity });
    });

    init();
})(window, document);
