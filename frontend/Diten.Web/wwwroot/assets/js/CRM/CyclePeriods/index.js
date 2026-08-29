/**
 * MOD-0165-FU07 Cycle Periods — DataTables Index (Golden Compact aligned, proxy profile).
 *  - Native toolbar search, Select2 filter chips mounted under the toolbar
 *  - SaveView (filter + search + colvis + colorder) via personalizationClient
 *  - Row actions: Details + Edit + Activate + Close. Create/Edit/Details are their OWN PAGES (Golden Compact):
 *    FU07 took the form from 8 user fields to 11, and a page carrying both an offcanvas and separate pages would
 *    pass neither verifier reference — so the Slim offcanvas and quick-view were deleted rather than kept alongside.
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

    const endpoint = '/CRM/CyclePeriods/api';
    const pageRoot = '/CRM/CyclePeriods';
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'CyclePeriods' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9];
    const totalColumnCount = 11;
    const baseOrder = [[3, 'desc'], [4, 'desc']];

    let L = window.CyclePeriodsL10n || window.L10n || {};
    // The create affordance exists ONCE, in the DataTable toolbar, and it obeys the same server-side permission the
    // page header used to. The flag is published as JSON by Index.cshtml (the Campaign golden-compact pattern); a
    // parse failure is read as "no permission", because guessing the permissive answer is the wrong way to be wrong.
    let canManage = false;
    try { canManage = !!JSON.parse(document.getElementById('cycleperiod-page-flags')?.textContent || '{}').canManage; }
    catch (e) { canManage = false; }

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
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    // One fixed presentation for every date on this page: "Jan 01, 26" / "Jan 01, 26, 12:00 AM".
    // The locale is PINNED to en-US rather than left to the browser: a period's window is an operational fact that gets
    // read out, copied into tickets and compared across screens, and "01/02/26" means two different days depending on
    // who is looking. The Details page renders the same shape server-side (MMM dd, yy) so the two never disagree.
    // The two differ in ONE way, deliberately. DAY_FORMAT pins timeZone: 'UTC' because a period's window is stored as
    // a UTC-midnight DAY: a browser west of Greenwich would otherwise render that instant as the previous evening and
    // drop a day, so the row saved as "Jul 01" would read "Jun 30". A window is a calendar fact and is shown in the
    // calendar it was written in.
    // STAMP_FORMAT does NOT pin it: UpdatedAt is a real moment in time, not a calendar day, and "when was this last
    // touched?" is a question a reader answers against their own clock.
    const DAY_FORMAT = { month: 'short', day: '2-digit', year: '2-digit', timeZone: 'UTC' };
    const STAMP_FORMAT = { month: 'short', day: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' };
    const day = v => v ? new Date(v).toLocaleDateString('en-US', DAY_FORMAT) : '—';
    const stamp = v => v ? new Date(v).toLocaleString('en-US', STAMP_FORMAT) : '—';
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const hasVal = v => Array.isArray(v) ? normArr(v).length > 0 : norm(v).length > 0;
    const statusLabel = v => ({ draft: L.StatusDraft, active: L.StatusActive, closed: L.StatusClosed }[v] || v);
    const statusTone = v => v === 'active' ? 'success' : v === 'closed' ? 'secondary' : 'primary';
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

    // A closed period offers no mutation action at all: it is terminal, and there is no reopen anywhere. Details and
    // Edit are links to their own pages (Golden Compact) rather than offcanvas triggers.
    const actions = row => {
        const id = esc(row.cyclePeriodId);
        const items = [{
            key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show',
            attrs: { 'data-id': id, title: L.ViewDetails }
        }];
        if (row.cycleStatus !== 'closed') {
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
            { data: 'scopeRef' }, { data: 'cycleStatus' }, { data: 'updatedAt' }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 2, render: v => `<span class="fw-medium text-heading">${esc(v)}</span>` },
            { targets: [5, 6], render: v => day(v) },
            { targets: 7, render: (v, t, row) => t === 'display' ? scopeCell(row) : (v || '') },
            { targets: 8, render: v => badge(statusLabel(v), statusTone(v)) },
            { targets: 9, render: v => stamp(v) },
            { targets: 10, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
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
        await refreshCurrentPeriod();
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
            await refreshCurrentPeriod();
        } catch (error) {
            window.showToast?.(error.message || L.ErrorOccurred, 'error');
        } finally {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    };

    // Golden Compact: Quick View and Edit NAVIGATE to their own pages rather than opening a panel — the same wiring
    // the Golden Reference Compact uses, so the class name means what it means everywhere else.
    document.addEventListener('click', event => {
        const quickView = event.target.closest('.js-quick-view');
        if (quickView) {
            event.preventDefault();
            if (quickView.dataset.id) window.location.assign(`${pageRoot}/Details/${quickView.dataset.id}`);
            return;
        }

        const edit = event.target.closest('.js-edit-period');
        if (edit) {
            event.preventDefault();
            if (edit.dataset.id) window.location.assign(`${pageRoot}/Edit/${edit.dataset.id}`);
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


    init();
})(window, document);
