/**
 * MOD-0167-FU04 Strategy Templates — DataTables Index (Golden aligned, proxy profile).
 *  - Native toolbar search, Select2 filter chips mounted under the toolbar
 *  - SaveView (filter + search + colvis + colorder) via personalizationClient
 *  - Row actions: View (Details) + Edit + Activate + New version + Archive; Compact "Create" (.add-new) → /Create
 *  - All traffic via same-origin MVC proxy /CRM/StrategyTemplates/api (never a Gateway URL / bearer token)
 *  - There is NO delete action anywhere (closing a play is Archive) and NO apply/generate action at all:
 *    applying a play to a period is MOD-0155, so this page cannot offer it.
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-strategy-templates');
    if (!tableEl) return;

    const endpoint = '/CRM/StrategyTemplates/api';
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'StrategyTemplates' };
    // Golden Compact consolidated column map: 0 control · 1 OYUN(name+code) · 2 KAPSAM(scope) · 3 SEGMENT · 4 ÜRÜN ·
    // 5 İÇERİK · 6 DURUM(status+vN) · 7 GÜNCELLENDİ · 8 Actions.
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const baseOrder = [[7, 'desc']];

    let L = window.StrategyTemplatesL10n || window.L10n || {};
    let dt = null;
    let contract = null;
    let addNewBound = false;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    const emptyFilters = () => ({ templateStatus: [], frequencyIntentMode: [], subjectType: '', businessUnitId: '', countryScope: '', includeArchived: 'true' });
    let appliedFilters = emptyFilters();
    let allRows = [];

    // WP-ST-SCOPE scope-options — loaded once for the KAPSAM label map + the country filter feed. Missing maps degrade
    // to the raw ref (never a fabricated name), exactly as the spec requires.
    let scopeOptions = null;
    let countryMap = new Map();   // ISO code (upper) → display name
    let leMap = new Map();        // legal-entity id (guid "D") → display name
    let buMap = new Map();        // business-unit code → display name

    const getAuthHeaders = () => ({ Accept: 'application/json' });
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    // GÜNCELLENDİ — dd.MM.yyyy (mockup). Falls back to the raw yyyy-MM-dd slice if the value is not a parseable date.
    const fmtDate = v => {
        const s = v == null ? '' : String(v).trim();
        if (!s) return '—';
        const d = new Date(s);
        if (Number.isNaN(d.getTime())) return s.slice(0, 10);
        const p = n => String(n).padStart(2, '0');
        return `${p(d.getDate())}.${p(d.getMonth() + 1)}.${d.getFullYear()}`;
    };
    const shortId = id => { const s = id == null ? '' : String(id).trim(); return s ? s.slice(0, 8) + '…' : ''; };
    // Title-case a scope code for a display fallback ("legal-entity" → "Legal Entity"); the vocabulary itself is L10n'd.
    const humanize = code => (code == null ? '' : String(code)).split(/[-_\s]+/).filter(Boolean).map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const hasVal = v => Array.isArray(v) ? normArr(v).length > 0 : norm(v).length > 0;

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

    // KAPSAM — scope-type code → localized label (subline). Unknown codes title-case as a graceful fallback.
    const scopeTypeLabel = type => ({
        tenant: L.ScopeTypeTenant,
        country: L.ScopeTypeCountry,
        'legal-entity': L.ScopeTypeLegalEntity,
        'business-unit': L.ScopeTypeBusinessUnit
    }[norm(type)]) || humanize(type);

    // WP-ST-SCOPE feed. Read once; failure leaves the maps empty so KAPSAM degrades to the raw ref (no fabricated name)
    // and the country filter simply offers no options — the list still renders.
    const loadScopeOptions = async () => {
        try {
            scopeOptions = await envelope(await fetch(`${endpoint}/scope-options`, { credentials: 'same-origin', headers: getAuthHeaders() }));
        } catch (e) { scopeOptions = null; }
        countryMap = new Map((scopeOptions?.countries || []).map(o => [norm(o.value).toUpperCase(), norm(o.label)]));
        leMap = new Map((scopeOptions?.legalEntities || []).map(o => [norm(o.value).toLowerCase(), norm(o.label)]));
        buMap = new Map((scopeOptions?.businessUnits || []).map(o => [norm(o.value), norm(o.label)]));
    };

    // KAPSAM — resolved scope name (main line) + scope-type label (subline). tenant → "All company"; otherwise the
    // display name from the scope-options map, degrading to the raw ref when the map has no entry.
    const scopeName = row => {
        const type = norm(row.effectiveScopeType) || norm(row.scopeType) || 'tenant';
        switch (type) {
            case 'tenant': return norm(L.AllCompany) || 'All company';
            case 'country': { const c = norm(row.countryScope).toUpperCase(); return countryMap.get(c) || c || '—'; }
            case 'legal-entity': { const id = norm(row.legalEntityId).toLowerCase(); return leMap.get(id) || shortId(row.legalEntityId) || '—'; }
            case 'business-unit': { const b = norm(row.businessUnitId); return buMap.get(b) || b || '—'; }
            default: return norm(row.scopeRef) || '—';
        }
    };
    const scopeCell = row => {
        const type = norm(row.effectiveScopeType) || norm(row.scopeType) || 'tenant';
        return `<span class="fw-medium text-heading d-block">${esc(scopeName(row))}</span>`
            + `<span class="text-muted small">${esc(scopeTypeLabel(type))}</span>`;
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

    const distinct = key => Array.from(new Set(allRows.map(r => r[key]).filter(Boolean))).map(v => ({ value: v, text: v }));

    // Every option list comes from the CONTRACT vocabulary or from the loaded rows. No hardcoded list.
    const loadFilterOptions = () => {
        fillSelect('filterTemplateStatus', (contract?.vocabularies?.templateStatuses || []).map(v => ({ value: v, text: v })), false);
        fillSelect('filterFrequencyIntentMode', (contract?.vocabularies?.frequencyIntentModes || []).map(v => ({ value: v, text: v })), false);
        fillSelect('filterSubjectType', (contract?.vocabularies?.subjectTypes || []).map(v => ({ value: v, text: v })), true);
        fillSelect('filterBusinessUnitId', distinct('businessUnitId'), true);
        // Country options come from the scope-options feed (governed set), not the loaded rows. Head option = "All countries".
        const countryEl = document.getElementById('filterCountryScope');
        if (countryEl) {
            const head = `<option value="">${esc(L.AllCountries || L.ShowAll || 'All')}</option>`;
            countryEl.innerHTML = head + (scopeOptions?.countries || []).map(o => `<option value="${esc(norm(o.value).toUpperCase())}">${esc(norm(o.label))}</option>`).join('');
        }
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
            if (appliedFilters.includeArchived === 'false' && r.isArchived) return false;
            // Country filter (client-side): only country-scoped rows can match a selected country (WP-ST-LIST). Rows at
            // other scope levels are excluded while a country is chosen.
            const country = norm(appliedFilters.countryScope);
            if (country && !(norm(r.effectiveScopeType) === 'country' && norm(r.countryScope).toUpperCase() === country.toUpperCase())) return false;
            return matchesMulti(appliedFilters.templateStatus, r.templateStatus)
                && matchesMulti(appliedFilters.frequencyIntentMode, r.frequencyIntentMode)
                && matchesSingle(appliedFilters.subjectType, r.subjectType)
                && matchesSingle(appliedFilters.businessUnitId, r.businessUnitId);
        });
    };
    const getAppliedFilterCount = () => [appliedFilters.templateStatus, appliedFilters.frequencyIntentMode, appliedFilters.subjectType, appliedFilters.businessUnitId, appliedFilters.countryScope].filter(hasVal).length + (appliedFilters.includeArchived === 'false' ? 1 : 0);

    const readControls = () => ({
        templateStatus: window.jQuery('#filterTemplateStatus').val() || [],
        frequencyIntentMode: window.jQuery('#filterFrequencyIntentMode').val() || [],
        subjectType: document.getElementById('filterSubjectType')?.value || '',
        businessUnitId: document.getElementById('filterBusinessUnitId')?.value || '',
        countryScope: document.getElementById('filterCountryScope')?.value || '',
        includeArchived: document.getElementById('filterIncludeArchived')?.value || 'true'
    });
    const writeControls = f => {
        window.jQuery('#filterTemplateStatus').val(normArr(f.templateStatus)).trigger('change');
        window.jQuery('#filterFrequencyIntentMode').val(normArr(f.frequencyIntentMode)).trigger('change');
        window.jQuery('#filterSubjectType').val(f.subjectType || '').trigger('change');
        window.jQuery('#filterBusinessUnitId').val(f.businessUnitId || '').trigger('change');
        window.jQuery('#filterCountryScope').val(f.countryScope || '').trigger('change');
        window.jQuery('#filterIncludeArchived').val(f.includeArchived || 'true').trigger('change');
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
        } catch (e) { if (!e?.authHandled) console.error('[StrategyTemplates SaveView] load failed', e); }
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

    const actions = row => {
        const id = esc(row.templateId);
        const items = [{ className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': id, title: L.ViewDetails } }];
        if (!row.isArchived && !row.areBindingsFrozen) {
            items.push({ className: 'js-edit-template', icon: 'bx bx-edit', text: L.EditStrategyTemplate, attrs: { 'data-id': id } });
        }
        if (!row.isArchived && row.templateStatus === 'draft') {
            items.push({ className: 'js-activate-template text-success', icon: 'bx bx-play-circle', text: L.ActivateStrategyTemplate, attrs: { 'data-id': id, 'data-name': esc(row.templateName) } });
        }
        if (!row.isArchived) {
            items.push({ className: 'js-new-version', icon: 'bx bx-git-branch', text: L.NewVersion, attrs: { 'data-id': id, 'data-name': esc(row.templateName) } });
            items.push({ className: 'js-archive-template text-warning', icon: 'bx bx-archive-in', text: L.ArchiveStrategyTemplate, attrs: { 'data-id': id, 'data-name': esc(row.templateName) } });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    // OYUN — play name (prominent) + code (muted) + superseded badge.
    const playCell = row => `<span class="fw-medium text-heading d-block">${esc(row.templateName || '—')}</span>`
        + (norm(row.templateCode) ? `<span class="text-muted small">${esc(row.templateCode)}</span>` : '')
        + (row.superseded ? ` <span class="badge bg-label-secondary">${esc(L.Superseded || 'superseded')}</span>` : '');
    // SEGMENT — subject-type badge (contact=kişi/mavi, account=kurum/turuncu) + bound-segment count beside it (mockup).
    // Unknown/blank subject types degrade to the count alone (no fabricated badge).
    const subjectLabel = row => {
        const t = norm(row.subjectType).toLowerCase();
        if (t === 'contact') return { label: L.SubjectTypeContact || 'kişi', tone: 'primary' };
        if (t === 'account') return { label: L.SubjectTypeAccount || 'kurum', tone: 'warning' };
        return null;
    };
    const segmentCell = row => {
        const s = subjectLabel(row);
        const count = `<span class="fw-medium text-heading">${esc(row.segmentBindingCount ?? 0)}</span>`;
        return s ? `${badge(s.label, s.tone)} ${count}` : count;
    };
    // ÜRÜN — "N · %" (mockup): product-line count · Σ line-weight%. The percentage shows ONLY when the mapper reports a
    // real positive total (every line weighted); a null/zero total prints the count alone (never a misleading "0%").
    const productCell = row => {
        const count = row.productLineCount ?? 0;
        const total = row.productAllocationTotalPercentage;
        const hasPct = total != null && Number(total) > 0;
        const pct = hasPct ? ` · ${Math.round(Number(total))}%` : '';
        return `<span class="fw-medium text-heading">${esc(count)}${esc(pct)}</span>`;
    };
    // DURUM — status badge + version (vN).
    const statusCell = row => badge(row.templateStatus, row.templateStatus === 'archived' ? 'secondary' : row.templateStatus === 'active' ? 'success' : 'primary')
        + `<span class="text-muted small d-block">v${esc(row.templateVersion ?? 1)}</span>`;

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        colReorder: { columns: ':gt(0):not(:last-child)' },
        order: baseOrder,
        columns: [
            { data: null, defaultContent: '' },
            { data: 'templateName' },
            { data: null },
            { data: 'segmentBindingCount' },
            { data: 'productLineCount' },
            { data: 'contentBindingCount' },
            { data: 'templateStatus' },
            { data: 'updatedAt' },
            { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 1, render: (v, t, row) => t === 'display' ? playCell(row) : `${norm(row.templateName)} ${norm(row.templateCode)}` },
            { targets: 2, render: (v, t, row) => t === 'display' ? scopeCell(row) : `${scopeName(row)} ${scopeTypeLabel(norm(row.effectiveScopeType) || norm(row.scopeType))}` },
            { targets: 3, render: (v, t, row) => t === 'display' ? segmentCell(row) : (t === 'sort' || t === 'type' ? (row.segmentBindingCount ?? 0) : `${row.segmentBindingCount ?? 0} ${norm(subjectLabel(row)?.label) || norm(row.subjectType)}`) },
            { targets: 4, render: (v, t, row) => t === 'display' ? productCell(row) : (t === 'sort' || t === 'type' ? (row.productLineCount ?? 0) : `${row.productLineCount ?? 0} ${row.productAllocationTotalPercentage ?? ''}`) },
            { targets: 5, render: v => esc(v ?? 0) },
            { targets: 6, render: (v, t, row) => t === 'display' ? statusCell(row) : norm(row.templateStatus) },
            { targets: 7, render: (v, t) => t === 'display' ? fmtDate(v) : norm(v) },
            { targets: 8, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: L.EmptyState, processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(L.NewPlay || L.CreateStrategyTemplate, { href: '/CRM/StrategyTemplates/Create' }, {
            filterBtn: { text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' }, action: () => toggleInlineFilter() },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn', attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    try { await saveDefaultView(currentView(api || dt)); setSaveFilterVisible(false); window.showToast?.(L.SaveView || '', 'success'); }
                    catch (err) { if (!err?.authHandled) { console.error(err); window.showToast?.(L.ErrorState, 'error'); } }
                }
            }
        }, { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes }),
        initComplete: function () {
            mountInlineFilter();
            bindInlineFilterA11y();
            void setupFilters(this.api());
            if (!addNewBound) { document.querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); window.location.href = '/CRM/StrategyTemplates/Create'; }); addNewBound = true; }
            setTimeout(() => { saveFilterArmed = true; }, 0);
        },
        drawCallback: function () {
            const api = this.api();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            updateCount(api);
        }
    });

    // "N / N oyun" counter — filtered rows / total rows + the plays unit label.
    const updateCount = api => {
        const el = document.getElementById('strategyTemplateCount');
        if (!el || !api) return;
        try {
            const filtered = api.rows({ filter: 'applied' }).count();
            const total = api.rows().count();
            el.textContent = `${filtered} / ${total} ${norm(L.PlaysUnit) || ''}`.trim();
        } catch (e) { /* table not ready */ }
    };

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
            if (!contract?.isReady || !contract?.features?.supportsStrategyTemplateDefinition) throw new Error(L.StrategyTemplateContractUnavailable);
            return true;
        } catch (error) {
            const host = document.getElementById('strategyTemplateContractError');
            if (host) { host.textContent = error.message || L.StrategyTemplateContractUnavailable; host.classList.remove('d-none'); }
            return false;
        }
    };

    const fetchRows = async () => (await envelope(await fetch(`${endpoint}/templates?includeArchived=true`, { credentials: 'same-origin', headers: getAuthHeaders() })))?.items || [];

    const reload = async () => {
        allRows = await fetchRows();
        if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); }
    };

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        registerTableFilter();
        try {
            if (!(await loadContract())) return;
            await loadScopeOptions();
            await loadDefaultView();
            allRows = await fetchRows();
            dt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(buildConfig()) : buildConfig());
            dt.on('column-visibility.dt search.dt order.dt column-reorder.dt columns-reordered.dt', () => {
                window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            });
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
        } finally {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    };

    const post = async (url, successKey) => {
        try {
            await envelope(await fetch(url, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
            window.showToast?.(successKey, 'success');
            await reload();
        } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
    };

    document.addEventListener('click', event => {
        const view = event.target.closest('.js-quick-view');
        if (view) { event.preventDefault(); window.location.href = `/CRM/StrategyTemplates/Details/${view.dataset.id}`; return; }

        const edit = event.target.closest('.js-edit-template');
        if (edit) { event.preventDefault(); window.location.href = `/CRM/StrategyTemplates/Edit/${edit.dataset.id}`; return; }

        const activate = event.target.closest('.js-activate-template');
        if (activate) {
            event.preventDefault();
            window.showConfirm?.(L.ActivateStrategyTemplateConfirm, () => post(`${endpoint}/templates/${activate.dataset.id}/activate`, L.RecordActivated),
                { entityName: activate.dataset.name, type: 'question', confirmButtonText: L.ActivateStrategyTemplate });
            return;
        }

        const newVersion = event.target.closest('.js-new-version');
        if (newVersion) {
            event.preventDefault();
            window.showConfirm?.(L.NewVersionConfirm, async () => {
                try {
                    const created = await envelope(await fetch(`${endpoint}/templates/${newVersion.dataset.id}/new-version`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                    if (created) { window.location.href = `/CRM/StrategyTemplates/Edit/${created}`; return; }
                    await reload();
                } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
            }, { entityName: newVersion.dataset.name, type: 'question', confirmButtonText: L.NewVersion });
            return;
        }

        const archive = event.target.closest('.js-archive-template');
        if (!archive) return;
        event.preventDefault();
        window.showConfirm?.(L.ArchiveStrategyTemplateConfirm, () => post(`${endpoint}/templates/${archive.dataset.id}/archive`, L.RecordArchived),
            { entityName: archive.dataset.name, type: 'warning', confirmButtonText: L.ArchiveStrategyTemplate });
    });

    init();
})(window, document);
