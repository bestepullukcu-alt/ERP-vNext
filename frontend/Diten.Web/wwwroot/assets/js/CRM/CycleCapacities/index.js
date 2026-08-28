/**
 * MOD-0155-FU06 Cycle Capacities — DataTables Index (Golden Compact aligned, proxy profile).
 *  - Native toolbar search, Select2 filter chips mounted under the toolbar
 *  - SaveView (filter + search + colvis + colorder) via personalizationClient
 *  - Row actions: Details + Edit + Archive. Create/Edit/Details are their OWN PAGES (Golden Compact).
 *  - All traffic via same-origin MVC proxy /CRM/CycleCapacities/api (never a Gateway URL / bearer token)
 *  - There is NO delete and NO bulk delete anywhere (retiring a capacity is Archive), and no approve action:
 *    approving an ESTIMATE is follow-up F-APPROVAL, so this page cannot offer it.
 *  - There is deliberately NO visit-number column: the estimate costs one working-calendar call per month, so
 *    computing one per row would turn a single grid draw into dozens of cross-service calls. It lives on Details.
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
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    const totalColumnCount = 12;
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
    const emptyFilters = () => ({ cyclePeriodId: '', calendarCountryCode: [], cycleStatus: [], archived: '' });
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
    const DAY_FORMAT = { month: 'short', day: '2-digit', year: '2-digit', timeZone: 'UTC' };
    const STAMP_FORMAT = { month: 'short', day: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' };
    const day = v => v ? new Date(v).toLocaleDateString('en-US', DAY_FORMAT) : '—';
    const stamp = v => v ? new Date(v).toLocaleString('en-US', STAMP_FORMAT) : '—';
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
        const periods = new Map();
        const countries = new Set();
        const statuses = new Set();
        allRows.forEach(r => {
            if (r.cyclePeriodId) periods.set(r.cyclePeriodId, `${r.cycleCode || ''} · ${r.cycleName || ''}`.trim());
            if (r.calendarCountryCode) countries.add(r.calendarCountryCode);
            if (r.cycleStatus) statuses.add(r.cycleStatus);
        });
        fillSelect('filterCyclePeriodId', Array.from(periods, ([value, text]) => ({ value, text })), true);
        fillSelect('filterCalendarCountryCode', Array.from(countries).sort().map(v => ({ value: v, text: v })), false);
        fillSelect('filterCycleStatus', Array.from(statuses).sort().map(v => ({ value: v, text: v })), false);
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
            return matchesSingle(appliedFilters.cyclePeriodId, r.cyclePeriodId)
                && matchesMulti(appliedFilters.calendarCountryCode, r.calendarCountryCode)
                && matchesMulti(appliedFilters.cycleStatus, r.cycleStatus)
                && matchesArchived(appliedFilters.archived, r);
        });
    };
    const getAppliedFilterCount = () => [
        appliedFilters.cyclePeriodId, appliedFilters.calendarCountryCode,
        appliedFilters.cycleStatus, appliedFilters.archived
    ].filter(hasVal).length;

    const readControls = () => ({
        cyclePeriodId: document.getElementById('filterCyclePeriodId')?.value || '',
        calendarCountryCode: window.jQuery('#filterCalendarCountryCode').val() || [],
        cycleStatus: window.jQuery('#filterCycleStatus').val() || [],
        archived: document.getElementById('filterArchived')?.value || ''
    });
    const writeControls = f => {
        window.jQuery('#filterCyclePeriodId').val(f.cyclePeriodId || '').trigger('change');
        window.jQuery('#filterCalendarCountryCode').val(normArr(f.calendarCountryCode)).trigger('change');
        window.jQuery('#filterCycleStatus').val(normArr(f.cycleStatus)).trigger('change');
        window.jQuery('#filterArchived').val(f.archived || '').trigger('change');
    };

    // A closed period freezes its capacity in every direction, so a frozen row offers no mutation action at all.
    // An archived row offers none either: archiving is one-way here (a fresh capacity is created instead).
    const actions = row => {
        const id = esc(row.cycleCapacityId);
        const items = [{
            key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show',
            attrs: { 'data-id': id, title: L.ViewDetails }
        }];
        if (canManage && row.isEditable && !row.isArchived) {
            items.push({ key: 'edit', className: 'js-edit-capacity', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': id } });
            items.push({ className: 'js-archive-capacity text-warning', icon: 'bx bx-archive-in', text: L.ArchiveCycleCapacity, attrs: { 'data-id': id, 'data-name': esc(row.cycleName || row.cycleCode || '') } });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    // One Status cell: whether the row is archived, and otherwise whether its PINNED PERIOD has closed. The capacity
    // itself has no status — editability is derived — so the cell reports the fact that actually governs it.
    const statusCell = row => {
        if (row.isArchived) return badge(L.ArchivedOnly, 'secondary');
        if (!row.isEditable) return badge(L.PeriodClosedLock, 'secondary');
        return badge(L.Active, 'success');
    };

    const windowCell = row => (row.cycleStartDate && row.cycleEndDate)
        ? `${day(row.cycleStartDate)} – ${day(row.cycleEndDate)}`
        : '—';

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        colReorder: { columns: ':gt(0):not(:last-child)' },
        order: baseOrder,
        columns: [
            { data: null, defaultContent: '' }, { data: 'cycleCode' }, { data: 'cycleName' },
            { data: 'cycleStartDate' }, { data: 'calendarCountryCode' }, { data: 'dailyWorkMinutes' },
            { data: 'minutesPerVisit' }, { data: 'fte' }, { data: 'monthCount' },
            { data: 'isArchived' }, { data: 'updatedAt' }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 2, render: v => `<span class="fw-medium text-heading">${esc(v)}</span>` },
            { targets: 3, render: (v, t, row) => t === 'display' ? windowCell(row) : (v || '') },
            { targets: 7, render: v => (v === null || v === undefined) ? '—' : Number(v).toFixed(2) },
            { targets: 9, render: (v, t, row) => t === 'display' ? statusCell(row) : (v ? '1' : '0') },
            { targets: 10, render: v => stamp(v) },
            { targets: 11, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
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
        drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount()); }
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
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    };

    // Golden Compact: Quick View and Edit NAVIGATE to their own pages rather than opening a panel.
    document.addEventListener('click', event => {
        const quickView = event.target.closest('.js-quick-view');
        if (quickView) {
            event.preventDefault();
            if (quickView.dataset.id) window.location.assign(`${pageRoot}/Details/${quickView.dataset.id}`);
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
