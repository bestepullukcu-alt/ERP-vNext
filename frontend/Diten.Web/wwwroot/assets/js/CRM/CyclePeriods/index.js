/**
 * MOD-0165-FU07 + WP-CYC-UI-1 Cycle Periods — DataTables Index (proxy profile).
 *  - Native toolbar search, Select2 filter chips mounted under the toolbar
 *  - SaveView (filter + search + colvis + colorder) via personalizationClient
 *  - Row actions: Details (page) + Edit (right-side PANEL) + Activate + Close + Capacity. WP-CYC-UI-1: create / edit
 *    moved into the panel (product-owner decision 2026-10-05 — a known deviation from Golden Compact's own pages).
 *  - Two views: the table, and the year timeline rendered from /api/overview (axis = filtered year ± 1, scope lanes,
 *    gaps / overlaps / today computed server-side). The "open periods without a capacity" band comes from the same call.
 *  - The effective-period finder asks /api/finder (ACTIVE periods only; resolved / none / ambiguous).
 *  - All traffic via same-origin MVC proxy /CRM/CyclePeriods/api (never a Gateway URL / bearer token)
 *  - There is NO delete and NO bulk delete anywhere (ending a period is Close), no reopen (closed is terminal),
 *    and no apply/generate: applying a plan to a period is MOD-0155, so this page cannot offer it.
 *  - The "current period" badge is a READ: it never resolves an ambiguous answer to a period of its own choosing,
 *    and it names the SCOPE that answered — "my unit has its own calendar" and "my unit follows the tenant's" are
 *    different facts.
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-cycle-periods');
    if (!tableEl) return;

    const S = window.CyclePeriodsShared;
    const endpoint = '/CRM/CyclePeriods/api';
    const pageRoot = '/CRM/CyclePeriods';
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'CyclePeriods' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13];
    const totalColumnCount = 15;
    const baseOrder = [[3, 'desc'], [4, 'desc']];

    let L = window.CyclePeriodsL10n || window.L10n || {};
    // The create affordance exists ONCE, in the DataTable toolbar, and it obeys the same server-side permission the
    // page header used to. The flag is published as JSON by Index.cshtml (the Campaign golden-compact pattern); a
    // parse failure is read as "no permission", because guessing the permissive answer is the wrong way to be wrong.
    const canManage = !!S?.canManage;

    let dt = null;
    let contract = null;
    let addNewBound = false;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    const emptyFilters = () => ({ cycleStatus: [], year: '', scopeType: '', country: '', businessUnitId: '' });
    let appliedFilters = emptyFilters();
    let allRows = [];

    const getAuthHeaders = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const esc = S.esc;
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    // WP-CYC-UI-1 — dates and numbers in the READER's culture (shared.js). A period day is a stored UTC-midnight DAY
    // and is shown in UTC so it never drifts; UpdatedAt is a real instant and follows the reader's clock.
    const day = S.day;
    const stamp = S.stamp;
    const dayCount = row => {
        const a = S.toDate(String(row.startDate || '').slice(0, 10)), b = S.toDate(String(row.endDate || '').slice(0, 10));
        return a && b ? Math.round((b - a) / 86400000) + 1 : null;
    };
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const hasVal = v => Array.isArray(v) ? normArr(v).length > 0 : norm(v).length > 0;
    const statusLabel = S.statusLabel;
    const statusTone = S.statusTone;
    // Scope labels come from the l10n bridge keyed by the CONTRACT vocabulary — never a hardcoded list here.
    const scopeLabel = v => ({
        tenant: L.ScopeTypeTenant,
        country: L.ScopeTypeCountry,
        'legal-entity': L.ScopeTypeLegalEntity,
        'business-unit': L.ScopeTypeBusinessUnit
    }[v] || v);

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

    const distinct = key => Array.from(new Set(allRows.map(r => r[key]).filter(v => v !== null && v !== undefined && v !== ''))).map(v => ({ value: String(v), text: String(v) }));

    // Status and scope-type options come from the CONTRACT vocabulary; years, countries and business units come from
    // the loaded rows. No hardcoded list anywhere.
    const loadFilterOptions = () => {
        fillSelect('filterCycleStatus', (contract?.vocabularies?.cycleStatuses || []).map(v => ({ value: v, text: statusLabel(v) })), false);
        fillSelect('filterScopeType', (contract?.vocabularies?.scopeTypes || []).map(v => ({ value: v, text: scopeLabel(v) })), true);
        fillSelect('filterYear', distinct('year'), true);
        fillSelect('filterCountry', distinct('countryScope'), true);
        fillSelect('filterBusinessUnitId', distinct('businessUnitId'), true);
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
    const registerTableFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || tableEl.dataset.filterBound === '1') return;
        tableEl.dataset.filterBound = '1';
        window.jQuery.fn.dataTable.ext.search.push((settings, _d, dataIndex, row) => {
            if (settings.nTable !== tableEl) return true;
            const r = row || dt?.row(dataIndex)?.data?.();
            if (!r) return true;
            // Each scope filter narrows its own level. They stack rather than fall back: a listing shows what exists,
            // and reproducing the resolver's precedence here would quietly give the grid a second opinion.
            return matchesMulti(appliedFilters.cycleStatus, r.cycleStatus)
                && matchesSingle(appliedFilters.year, r.year)
                && matchesSingle(appliedFilters.scopeType, r.scopeType)
                && matchesSingle(appliedFilters.country, r.countryScope)
                && matchesSingle(appliedFilters.businessUnitId, r.businessUnitId);
        });
    };
    const getAppliedFilterCount = () => [
        appliedFilters.cycleStatus, appliedFilters.year, appliedFilters.scopeType,
        appliedFilters.country, appliedFilters.businessUnitId
    ].filter(hasVal).length;

    const readControls = () => ({
        cycleStatus: window.jQuery('#filterCycleStatus').val() || [],
        year: document.getElementById('filterYear')?.value || '',
        scopeType: document.getElementById('filterScopeType')?.value || '',
        country: document.getElementById('filterCountry')?.value || '',
        businessUnitId: document.getElementById('filterBusinessUnitId')?.value || ''
    });
    const writeControls = f => {
        window.jQuery('#filterCycleStatus').val(normArr(f.cycleStatus)).trigger('change');
        window.jQuery('#filterYear').val(f.year || '').trigger('change');
        window.jQuery('#filterScopeType').val(f.scopeType || '').trigger('change');
        window.jQuery('#filterCountry').val(f.country || '').trigger('change');
        window.jQuery('#filterBusinessUnitId').val(f.businessUnitId || '').trigger('change');
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
        } catch (e) { if (!e?.authHandled) console.error('[CyclePeriods SaveView] load failed', e); }
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

    // A closed period offers no mutation action at all: it is terminal, and there is no reopen anywhere. Details is a
    // page; Edit opens the right-side panel (WP-CYC-UI-1).
    const actions = row => {
        const id = esc(row.cyclePeriodId);
        const items = [{
            key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show',
            attrs: { 'data-id': id, title: L.ViewDetails }
        }];
        if (canManage && row.cycleStatus !== 'closed') {
            items.push({ key: 'edit', className: 'js-edit-period', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': id } });
        }
        if (row.cycleStatus === 'draft') {
            items.push({ className: 'js-activate-period text-success', icon: 'bx bx-play-circle', text: L.ActivateCyclePeriod, attrs: { 'data-id': id, 'data-name': esc(row.cycleName) } });
        }
        if (row.cycleStatus !== 'closed') {
            items.push({ className: 'js-close-period text-warning', icon: 'bx bx-lock-alt', text: L.CloseCyclePeriod, attrs: { 'data-id': id, 'data-name': esc(row.cycleName) } });
        }
        // MOD-0155-FU06 - an ADDITIVE navigation link, and nothing more. CyclePeriod gains no field, no column, no
        // endpoint and no knowledge that Cycle Capacity exists: the target route resolves for itself whether the
        // period already has a capacity. Offered for every status, closed included, because a closed period's capacity
        // stays READABLE even though it can no longer be edited.
        items.push({ className: 'js-cycle-capacity', icon: 'bx bx-tachometer', text: L.CycleCapacity, attrs: { 'data-id': id } });
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    // One Scope cell: the level as a badge plus its reference, so a reader can see at a glance which calendar a row
    // belongs to. Tenant-wide rows say so rather than showing an empty cell.
    const scopeCell = row => {
        const label = badge(scopeLabel(row.scopeType), 'primary');
        const ref = norm(row.scopeRef);
        if (!ref) {
            return `${label} <span class="ms-1 text-muted">${esc(L.TenantWide || '—')}</span>`;
        }

        // A business unit shows the country it was chosen under ("TR / alpha"), because the unit list is derived from
        // the territory plans covering a country and a bare code loses that. The country is CONTEXT, not identity: the
        // scope reference is still the unit alone, which is why it is rendered muted and only ever as a prefix. A row
        // written before the field existed simply shows its unit.
        const context = norm(row.businessUnitCountryContext);
        return context
            ? `${label} <span class="ms-1 text-muted">${esc(context)}</span><span class="text-muted"> / </span><span>${esc(ref)}</span>`
            : `${label} <span class="ms-1">${esc(ref)}</span>`;
    };

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        colReorder: { columns: ':gt(0):not(:last-child)' },
        order: baseOrder,
        columns: [
            { data: null, defaultContent: '' }, { data: 'cycleCode' }, { data: 'cycleName' },
            { data: 'year' }, { data: 'sequenceInYear' }, { data: 'startDate' }, { data: 'endDate' },
            { data: null }, { data: 'scopeRef' }, { data: 'cycleStatus' },
            { data: 'hasCapacity' }, { data: 'campaignCount' }, { data: 'plannedVisitCount' },
            { data: 'updatedAt' }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 1, render: (v, t) => t === 'display' ? `<span class="fw-medium text-primary">${esc(v)}</span>` : v },
            { targets: 2, render: v => `<span class="fw-medium text-heading">${esc(v)}</span>` },
            { targets: [5, 6], render: (v, t) => t === 'display' || t === 'filter' ? day(v) : v },
            { targets: 7, className: 'text-end', render: (v, t, row) => { const n = dayCount(row); return t === 'display' ? esc(S.number(n)) : n; } },
            { targets: 8, render: (v, t, row) => t === 'display' ? scopeCell(row) : (v || '') },
            { targets: 9, render: (v, t) => t === 'display' ? badge(statusLabel(v), statusTone(v)) : v },
            // WP-CYC-UI-1 — the batch usage summary; null means the read failed (unknown), never "none".
            { targets: 10, render: (v, t) => t !== 'display' ? (v === true ? 1 : v === false ? 0 : -1)
                : v === true ? `<i class="icon-base bx bx-check text-success" aria-label="${esc(L.HasCapacity || '')}"></i>`
                : v === false ? `<span class="badge bg-label-warning">${esc(L.NoCapacity || '')}</span>` : '—' },
            { targets: [11, 12], className: 'text-end', render: (v, t) => t === 'display' ? esc(S.number(v)) : (v ?? -1) },
            { targets: 13, render: (v, t) => t === 'display' ? stamp(v) : v },
            { targets: 14, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: L.EmptyState, processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(canManage ? (L.CreateCyclePeriod || '') : '', { }, {
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
            // WP-CYC-UI-1: the toolbar's create button opens the right-side panel.
            if (canManage && !addNewBound) { document.querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); window.CyclePeriodPanel?.openCreate(); }); addNewBound = true; }
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
            void refreshOverview();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            const el = document.getElementById(filterCollapseId);
            if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', e => {
            e.preventDefault();
            applySavedTableState(api, getResetBaselineState());
            void refreshOverview();
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });
    };

    const loadContract = async () => {
        try {
            contract = await envelope(await fetch(`${endpoint}/contract`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            if (!contract?.isReady || !contract?.features?.supportsCyclePeriod) throw new Error(L.ContractUnavailable);
            return true;
        } catch (error) {
            window.showToast?.(error.message || L.ContractUnavailable, 'error');
            return false;
        }
    };

    const fetchRows = async () => (await envelope(await fetch(`${endpoint}/periods`, { credentials: 'same-origin', headers: getAuthHeaders() })))?.items || [];

    // The "which period is in force today?" badge. It is a READ, and an ambiguous answer is shown AS ambiguous:
    // silently picking the first candidate would hide a data defect behind a plausible label. The page asks WITHOUT
    // naming a country, legal entity or business unit, so only tenant-wide periods answer — a level nobody named must
    // not leak into a general-purpose badge.
    const refreshCurrentPeriod = async () => {
        const badgeEl = document.getElementById('currentPeriodBadge');
        const scopeEl = document.getElementById('currentPeriodScope');
        const windowEl = document.getElementById('currentPeriodWindow');
        if (!badgeEl) return;
        const setScope = value => {
            if (!scopeEl) return;
            scopeEl.classList.toggle('d-none', !value);
            scopeEl.textContent = value ? scopeLabel(value) : '';
        };
        try {
            const at = encodeURIComponent(new Date().toISOString());
            const res = await envelope(await fetch(`${endpoint}/periods/resolve-active?at=${at}`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            if (res?.outcome === 'resolved' && res.period) {
                badgeEl.className = 'badge bg-label-success';
                badgeEl.textContent = `${res.period.cycleCode} · ${res.period.cycleName}`;
                setScope(res.resolvedScopeType);
                if (windowEl) windowEl.textContent = `${day(res.period.startDate)} – ${day(res.period.endDate)}`;
            } else if (res?.outcome === 'ambiguous') {
                badgeEl.className = 'badge bg-label-warning';
                badgeEl.textContent = L.AmbiguousPeriod;
                setScope(res.resolvedScopeType);
                if (windowEl) windowEl.textContent = res.reason || '';
            } else {
                badgeEl.className = 'badge bg-label-secondary';
                badgeEl.textContent = L.NoActivePeriod;
                setScope(null);
                if (windowEl) windowEl.textContent = '';
            }
        } catch (error) {
            badgeEl.className = 'badge bg-label-secondary';
            badgeEl.textContent = L.NotAvailable;
            setScope(null);
        }
    };

    const reload = async () => {
        allRows = await fetchRows();
        if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); }
        loadFilterOptions();
        await Promise.all([refreshCurrentPeriod(), refreshOverview()]);
    };

    // ── WP-CYC-UI-1: overview (timeline + band) ─────────────────────────────────────────────────────────────
    const overviewQuery = () => {
        const q = new URLSearchParams();
        if (norm(appliedFilters.year)) q.set('year', norm(appliedFilters.year));
        if (norm(appliedFilters.scopeType)) q.set('scopeType', norm(appliedFilters.scopeType));
        if (norm(appliedFilters.country)) q.set('country', norm(appliedFilters.country));
        normArr(appliedFilters.cycleStatus).forEach(s => q.append('status', s));
        return q.toString();
    };
    const renderBand = items => {
        const band = document.getElementById('noCapacityBand');
        const host = document.getElementById('noCapacityBandItems');
        if (!band || !host) return;
        band.classList.toggle('d-none', !items.length);
        host.innerHTML = items.map(i =>
            `<a class="badge bg-label-warning text-decoration-none" href="/CRM/CycleCapacities/Index?cyclePeriodId=${encodeURIComponent(i.cyclePeriodId)}&returnTo=cycleperiods"`
            + ` title="${esc(i.cycleName)}">${esc(i.cycleCode)} <i class="icon-base bx bx-plus"></i></a>`).join('');
    };
    const laneLabel = lane => `<span class="fw-medium">${esc(lane.scopeRef || L.TenantWide || '')}</span><small>${esc(S.scopeLabel(lane.scopeType))}</small>`;
    const renderTimeline = timeline => {
        const grid = document.getElementById('timelineGrid');
        const empty = document.getElementById('timelineEmpty');
        if (!grid) return;
        const lanes = timeline?.lanes || [];
        empty?.classList.toggle('d-none', lanes.length > 0);
        grid.classList.toggle('d-none', lanes.length === 0);
        if (!lanes.length) { grid.innerHTML = ''; return; }
        const pos = (offset, width) => `inset-inline-start:${Number(offset)}%;inline-size:${Number(width)}%`;
        const today = timeline.todayPct === null || timeline.todayPct === undefined ? '' : `<span class="cp-today" style="inset-inline-start:${Number(timeline.todayPct)}%"></span>`;
        const gridlines = (timeline.years || []).map(y => `<span class="cp-tl-gridline cp-tl-gridline--year" style="inset-inline-start:${Number(y.offsetPct)}%"></span>`).join('')
            + (timeline.quarterPcts || []).map(q => `<span class="cp-tl-gridline" style="inset-inline-start:${Number(q)}%"></span>`).join('');
        const head = `<div class="cp-tl-label cp-tl-head"></div><div class="cp-tl-track cp-tl-head">`
            + (timeline.years || []).map(y => `<span class="cp-tl-year" style="${pos(y.offsetPct, y.widthPct)}">${esc(S.plain(y.year))}</span>`).join('')
            + gridlines + today + '</div>';
        const rows = lanes.map(lane => {
            // Bars that share days within a lane stack onto sub-rows so none hides another.
            const subEnds = [];
            const placed = lane.bars.map(b => {
                let idx = subEnds.findIndex(end => end < b.offsetPct);
                if (idx < 0) { idx = subEnds.length; subEnds.push(0); }
                subEnds[idx] = b.offsetPct + b.widthPct;
                return { b, idx };
            });
            const height = 0.625 + Math.max(1, subEnds.length) * 2.125;
            const bars = placed.map(({ b, idx }) =>
                `<button type="button" role="listitem" class="cp-bar cp-bar--${esc(b.cycleStatus)} js-timeline-bar" data-id="${esc(b.cyclePeriodId)}"`
                + ` style="${pos(b.offsetPct, b.widthPct)};inset-block-start:${0.3125 + idx * 2.125}rem"`
                + ` title="${esc(`${b.cycleCode} · ${b.cycleName} · ${day(b.startDate)} – ${day(b.endDate)} · ${statusLabel(b.cycleStatus)}`)}">${esc(b.cycleCode)}</button>`).join('');
            const marks = (lane.marks || []).map(m => {
                const days = Math.round((S.toDate(m.to) - S.toDate(m.from)) / 86400000) + 1;
                const title = m.kind === 'gap'
                    ? String(L.TimelineGapTitle || '{0} – {1}').replace('{0}', day(m.from)).replace('{1}', day(m.to)).replace('{2}', S.number(days))
                    : String(L.TimelineOverlapTitle || '{0}').replace('{0}', (m.codes || []).join(' ↔ ')).replace('{1}', S.number(days));
                const label = m.kind === 'gap' && days >= 20 ? esc(String(L.TimelineGapLabel || '{0}').replace('{0}', S.number(days))) : '';
                return `<span class="cp-mark cp-mark--${esc(m.kind)}" style="${pos(m.offsetPct, m.widthPct)}" title="${esc(title)}">${label}</span>`;
            }).join('');
            return `<div class="cp-tl-label">${laneLabel(lane)}</div>`
                + `<div class="cp-tl-track" style="min-block-size:${height}rem">${gridlines}${marks}${bars}${today}</div>`;
        }).join('');
        grid.innerHTML = head + rows;
    };
    let overviewSeq = 0;
    const refreshOverview = async () => {
        const seq = ++overviewSeq;
        const loading = document.getElementById('timelineLoading');
        const error = document.getElementById('timelineError');
        loading?.classList.remove('d-none');
        error?.classList.add('d-none');
        try {
            const data = await S.getJson(`/overview?${overviewQuery()}`);
            if (seq !== overviewSeq) return;
            renderTimeline(data?.timeline);
            renderBand(data?.openWithoutCapacity || []);
        } catch (e) {
            if (seq !== overviewSeq) return;
            error?.classList.remove('d-none');
            document.getElementById('timelineGrid')?.classList.add('d-none');
        } finally {
            if (seq === overviewSeq) loading?.classList.add('d-none');
        }
    };

    // ── view toggle (table / timeline) ──────────────────────────────────────────────────────────────────────
    const setView = view => {
        const timeline = view === 'timeline';
        document.getElementById('cyclePeriodsTableView')?.classList.toggle('d-none', timeline);
        document.getElementById('cyclePeriodsTimelineView')?.classList.toggle('d-none', !timeline);
        [['btnViewTable', !timeline], ['btnViewTimeline', timeline]].forEach(([id, on]) => {
            const b = document.getElementById(id);
            b?.classList.toggle('active', on);
            b?.setAttribute('aria-pressed', String(on));
        });
        if (!timeline) dt?.columns?.adjust?.();
    };

    // ── finder (K-6) ────────────────────────────────────────────────────────────────────────────────────────
    const finderLevel = () => document.getElementById('finderLevel')?.value || 'tenant';
    const applyFinderLevel = () => {
        document.querySelectorAll('#finderCard .finder-ref').forEach(b => b.classList.toggle('d-none', b.dataset.finderRef !== finderLevel()));
    };
    const runFinder = async () => {
        const result = document.getElementById('finderResult');
        const date = document.getElementById('finderDate')?.value;
        if (!result || !date) return;
        const q = new URLSearchParams({ at: `${date}T12:00:00Z` });
        const level = finderLevel();
        const country = document.getElementById('finderCountry')?.value;
        const le = document.getElementById('finderLegalEntity')?.value;
        const bu = document.getElementById('finderBusinessUnit')?.value;
        if (level === 'country' && country) q.set('country', country);
        if (level === 'legal-entity' && le) q.set('legalEntityId', le);
        if (level === 'business-unit' && bu) q.set('businessUnitId', bu);
        result.innerHTML = `<p class="text-muted mb-0">${esc(L.Loading || '')}</p>`;
        try {
            const r = await S.getJson(`/finder?${q.toString()}`);
            const message = esc(L[r.messageKey] || r.messageKey || '');
            if (r.outcome === 'resolved' && r.period) {
                result.innerHTML = `<div class="alert alert-success mb-0" role="status"><div class="fw-medium">${message}</div>`
                    + `<div class="mt-1"><a href="${pageRoot}/Details/${encodeURIComponent(r.period.cyclePeriodId)}" class="fw-medium">${esc(r.period.cycleCode)}</a>`
                    + ` · ${esc(r.period.cycleName)} · ${esc(day(r.period.startDate))} – ${esc(day(r.period.endDate))}</div>`
                    + (r.resolvedScopeType ? `<div class="mt-1 small">${esc(L.FinderAnsweredBy || '')}: ${badge(S.scopeLabel(r.resolvedScopeType), 'primary')}</div>` : '')
                    + '</div>';
            } else if (r.outcome === 'ambiguous') {
                result.innerHTML = `<div class="alert alert-warning mb-0" role="alert">${message}`
                    + (r.resolvedScopeType ? ` ${badge(S.scopeLabel(r.resolvedScopeType), 'warning')}` : '') + '</div>';
            } else {
                result.innerHTML = `<div class="alert alert-secondary mb-0" role="status">${message}</div>`;
            }
        } catch (e) {
            result.innerHTML = `<div class="alert alert-danger mb-0" role="alert">${esc(e.message || L.ErrorOccurred || '')}</div>`;
        }
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
            await Promise.all([refreshCurrentPeriod(), refreshOverview()]);
            openFromQuery();
        } catch (error) {
            window.showToast?.(error.message || L.ErrorOccurred, 'error');
        } finally {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    };

    /** ?create=1 / ?edit={id} open the panel on arrival (links from the details page). */
    const openFromQuery = () => {
        const q = new URLSearchParams(window.location.search);
        if (!canManage || !window.CyclePeriodPanel) return;
        if (q.get('edit')) window.CyclePeriodPanel.openEdit(q.get('edit'));
        else if (q.get('create') === '1') window.CyclePeriodPanel.openCreate();
    };

    // Quick View navigates to the details page; Edit opens the right-side panel (WP-CYC-UI-1); a timeline bar opens
    // the period's details.
    document.addEventListener('click', event => {
        const quickView = event.target.closest('.js-quick-view') || event.target.closest('.js-timeline-bar');
        if (quickView) {
            event.preventDefault();
            if (quickView.dataset.id) window.location.assign(`${pageRoot}/Details/${encodeURIComponent(quickView.dataset.id)}`);
            return;
        }

        const edit = event.target.closest('.js-edit-period');
        if (edit) {
            event.preventDefault();
            if (edit.dataset.id) window.CyclePeriodPanel?.openEdit(edit.dataset.id);
            return;
        }

        // MOD-0155-FU06 - the additive link. It NAVIGATES and nothing else: no capacity is read, created or
        // implied here, and /CRM/CycleCapacities decides server-side whether to open the detail page or a prefilled
        // create form.
        const capacity = event.target.closest('.js-cycle-capacity');
        if (capacity) {
            event.preventDefault();
            // returnTo names where the author started, so Save and Cancel over there can bring them back here
            // instead of stranding them on the capacity list they never asked for.
            if (capacity.dataset.id) window.location.assign(`/CRM/CycleCapacities/Index?cyclePeriodId=${encodeURIComponent(capacity.dataset.id)}&returnTo=cycleperiods`);
            return;
        }

        const activate = event.target.closest('.js-activate-period');
        if (activate) {
            event.preventDefault();
            window.showConfirm?.(L.ActivateCyclePeriodConfirm, () => post(`${endpoint}/periods/${activate.dataset.id}/activate`, L.RecordActivated),
                { entityName: activate.dataset.name, type: 'question', confirmButtonText: L.ActivateCyclePeriod });
            return;
        }

        const close = event.target.closest('.js-close-period');
        if (!close) return;
        event.preventDefault();
        window.showConfirm?.(L.CloseCyclePeriodConfirm, () => post(`${endpoint}/periods/${close.dataset.id}/close`, L.RecordClosed),
            { entityName: close.dataset.name, type: 'warning', confirmButtonText: L.CloseCyclePeriod });
    });

    document.getElementById('btnViewTable')?.addEventListener('click', () => setView('table'));
    document.getElementById('btnViewTimeline')?.addEventListener('click', () => setView('timeline'));
    document.getElementById('timelineRetry')?.addEventListener('click', () => { void refreshOverview(); });
    document.getElementById('btnFinderToggle')?.addEventListener('click', event => {
        const card = document.getElementById('finderCard');
        if (!card) return;
        const open = card.classList.toggle('d-none') === false;
        event.currentTarget.setAttribute('aria-expanded', String(open));
        const date = document.getElementById('finderDate');
        if (open && date && !date.value) date.value = S.isoDay(new Date());
        if (open) document.getElementById('finderLevel')?.focus();
    });
    document.getElementById('finderLevel')?.addEventListener('change', applyFinderLevel);
    document.getElementById('finderForm')?.addEventListener('submit', event => { event.preventDefault(); void runFinder(); });
    applyFinderLevel();
    window.CyclePeriodPanel?.onSaved(() => { void reload(); });

    init();
})(window, document);
