/**
 * MOD-0150 FU02 Contacts - DataTables Index Script (Golden Reference Compact).
 * Create/edit use full MVC pages. All API traffic goes through the Gateway; CRM service is never called directly.
 */
'use strict';

const ContactsList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-contacts');
    const apiUrl = window.API?.crm ?? window.ApiBaseUrl;
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'Contacts' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9];
    const totalColumnCount = 11;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9];
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { status: [], contactType: [], countryScope: [], territoryNode: [] };
    // Country-scope + territory-node options come from MOD-0151 Territory Management (distinct across the tenant);
    // a contact's own coverage is the union of its linked accounts'. Loaded once; falls back to row values if empty.
    let territoryLookups = { countryScopes: [], nodes: [] };
    let L = window.L10n || {};

    // MOD-0150 Import/Export Task 1 — permission-gated toolbar actions. Server-side capability flags; the Gateway and
    // CrmService enforce the same permissions, this only decides what is offered.
    const capabilities = (() => {
        const el = document.getElementById('contacts-capabilities');
        if (!el) return {};
        try {
            return JSON.parse(el.textContent || '{}');
        } catch (error) {
            console.error('[Contacts] Capability payload could not be parsed.', error);
            return {};
        }
    })();

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const getAuthHeaders = (includeJson = false) =>
        window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ status: [], contactType: [], countryScope: [], territoryNode: [] });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            status: normalizeArray(source.status),
            contactType: normalizeArray(source.contactType),
            countryScope: normalizeArray(source.countryScope),
            territoryNode: normalizeArray(source.territoryNode)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(normalizeString(actual));
    };
    // A contact's territory is a SET (its linked accounts'); it matches when ANY of its values is selected.
    const matchesAnyFilter = (selected, actualArray) => {
        const norm = normalizeArray(selected);
        if (!norm.length) return true;
        const actual = normalizeArray(actualArray);
        return actual.some((v) => norm.includes(v));
    };

    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const n = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((ci, pos) => {
                if (typeof colVis[ci] === 'boolean') n[ci] = colVis[ci];
                else if (typeof colVis[pos] === 'boolean') n[ci] = colVis[pos];
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((ci) => { if (typeof colVis[ci] === 'boolean') n[ci] = colVis[ci]; });
        }
        return Object.keys(n).length ? n : null;
    };
    const captureColVis = (api) => {
        const r = {};
        saveViewColumnIndexes.forEach((ci) => { try { r[ci] = !!api.column(ci).visible(); } catch (e) { } });
        return r;
    };
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
        return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
    };
    const captureColOrder = (api) => { try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (e) { return null; } };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = defaultVisibleColumnIndexes.includes(ci); return a; }, {});
    const applyColOrder = (api, order) => {
        const n = normalizeColOrder(order);
        if (n && typeof api?.colReorder?.order === 'function') api.colReorder.order(n, true);
    };
    const applyColVis = (api, colVis) => {
        const n = normalizeColVis(colVis);
        if (!n) return;
        saveViewColumnIndexes.forEach((ci) => { if (typeof n[ci] === 'boolean') { try { api.column(ci).visible(n[ci], false); } catch (e) { } } });
    };

    const getSearchVal = (api) => { try { return api.table().container().querySelector('.dt-search input')?.value || ''; } catch (e) { return ''; } };
    const syncSearchInput = (api, v) => { try { const el = api.table().container().querySelector('.dt-search input'); if (el) el.value = v || ''; } catch (e) { } };
    const getCurrentView = (api) => ({
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(api) || api.search()),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });
    const serializeView = (v) => JSON.stringify({
        filters: Object.keys(v?.filters || {}).sort().reduce((acc, key) => { acc[key] = normalizeFilterValue(v.filters[key]); return acc; }, {}),
        search: normalizeString(v?.search),
        // Fill every managed column with its default so a view saved before a column was added does not read as
        // permanently "dirty" against the freshly-captured full colVis.
        colVis: Object.assign(defaultColVis(), normalizeColVis(v?.colVis) || {}),
        columnOrder: normalizeColOrder(v?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });

    const getSavedViewId = (sv) => sv?.id || sv?.Id || sv?._id || null;
    const getSavedViewName = (sv) => sv?.viewName || sv?.ViewName || '';
    const isSavedViewDefault = (sv) => sv?.isDefault === true || sv?.IsDefault === true;
    const unwrapViewResponse = (response) => response?.data || response?.Data || response;
    const getSavedViewDef = (sv) => {
        const raw = sv?.viewDefinition ?? sv?.ViewDefinition ?? {};
        if (typeof raw === 'string') { try { return JSON.parse(raw); } catch (e) { return {}; } }
        return raw || {};
    };
    const mapSavedViewToState = (sv) => {
        const d = getSavedViewDef(sv);
        return {
            filters: normalizeFilters(d.filters || d),
            search: normalizeString(d.search),
            colVis: normalizeColVis(d.colVis),
            columnOrder: normalizeColOrder(d.columnOrder),
            order: Array.isArray(d.order) ? d.order : null
        };
    };
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || view || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getResetBaselineState = () => normalizeViewState({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
        order: baseOrder
    });
    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            filters: emptyFilters(),
            search: '',
            colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
            order: baseOrder
        };
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };

    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[Contacts SaveView] Failed to load saved views.', error);
            return null;
        }
    };
    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const normalizedView = normalizeViewState(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Default').trim(),
            viewDefinition: normalizedView,
            isDefault: true,
            visibility: 'private'
        };
        const existingId = getSavedViewId(defaultViewRecord);
        const savedResponse = existingId
            ? await personalizationClient.updateView(existingId, payload)
            : await personalizationClient.saveView(payload);
        const savedRecord = unwrapViewResponse(savedResponse);
        defaultViewRecord = savedRecord && typeof savedRecord === 'object' ? savedRecord : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };

    const mountInlineFilter = () => {
        const host = document.getElementById(filterHostId);
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };
    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById(filterCollapseId);
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };
    const bindInlineFilterA11y = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById(filterCollapseId);
        if (!btn || !collapseEl || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };
    const registerTableFilters = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.compactFilterBound === '1') return;
        dtTableEl.dataset.compactFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesMultiFilter(appliedFilters.status, row.status)
                && matchesMultiFilter(appliedFilters.contactType, row.contactType)
                && matchesAnyFilter(appliedFilters.countryScope, row.territoryCountryScopes)
                && matchesAnyFilter(appliedFilters.territoryNode, row.territoryNodeNames);
        });
    };

    const syncMultiSelectSummary = ($select) => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;
        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) { $summary = $('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = $('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }
        const placeholder = normalizeString($select.data('placeholder')) || '';
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || []).map((i) => normalizeString(i.text)).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + (L.Reset || '') + '" title="' + (L.Reset || '') + '">&times;</span>');
            $clearBtn.on('mousedown', (e) => { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clearBtn);
        }
    };

    const initSingleSelect2 = ($s) => {
        if (!window.jQuery || !$.fn.select2 || !$s.length) return;
        if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
        $s.select2({
            dropdownParent: $(document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            containerCssClass: 'dt-inline-filter-multi',
            selectionCssClass: 'form-select form-select-sm',
            placeholder: $s.data('placeholder') || '',
            minimumResultsForSearch: Infinity,
            width: 'element',
            closeOnSelect: false
        });
        $s.off('change.select2-summary').on('change.select2-summary', function () { syncMultiSelectSummary($s); });
        requestAnimationFrame(() => syncMultiSelectSummary($s));
    };
    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        $('#filterStatus, #filterContactType, #filterCountryScope, #filterTerritory').each(function () {
            initSingleSelect2($(this));
        });
    };

    const syncFilterControls = (values) => {
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterContactType').val(normalizeArray(values.contactType)).trigger('change');
        $('#filterCountryScope').val(normalizeArray(values.countryScope)).trigger('change');
        $('#filterTerritory').val(normalizeArray(values.territoryNode)).trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.status, appliedFilters.contactType, appliedFilters.countryScope, appliedFilters.territoryNode]
            .filter(hasFilterValue).length;

    const applySavedTableState = (api, view) => {
        if (!api || !view) return;
        const s = normalizeViewState(view);
        appliedFilters = s.filters;
        syncFilterControls(appliedFilters);
        applyColOrder(api, s.columnOrder);
        applyColVis(api, s.colVis);
        api.search(s.search);
        syncSearchInput(api, s.search);
        api.order(s.order);
        try { api.columns.adjust(); } catch (e) { }
        try { api.responsive?.recalc?.(); } catch (e) { }
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };

    const appendOptions = (select, items) => {
        select.innerHTML = '';
        items.forEach((item) => {
            if (!item?.value) return;
            const opt = document.createElement('option');
            opt.value = item.value;
            opt.textContent = item.text || item.value;
            select.appendChild(opt);
        });
    };
    // Filter options are sourced from MOD-0048 published values via the same-origin proxy. Empty on failure — no local fallback.
    const loadLookupOptions = async () => {
        const statusSelect = document.getElementById('filterStatus');
        const contactTypeSelect = document.getElementById('filterContactType');
        if (!statusSelect || !contactTypeSelect) return;
        try {
            const res = await fetch('/CRM/Contacts/lookups', { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() });
            if (!res.ok) return;
            const data = await res.json();
            appendOptions(statusSelect, Array.isArray(data?.statuses) ? data.statuses : []);
            appendOptions(contactTypeSelect, Array.isArray(data?.contactTypes) ? data.contactTypes : []);
        } catch (error) {
            console.error('[Contacts Lookup] Failed.', error);
        }
    };

    // Territory Management is the source of truth for the country-scope + territory-node chips (distinct across the
    // tenant). Falls back to the loaded contact rows' values if unavailable (e.g. no crm.territory.model.read).
    const loadTerritoryLookups = async () => {
        try {
            const res = await fetch('/CRM/Contacts/territory-lookups', { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() });
            if (!res.ok) return;
            const data = await res.json();
            territoryLookups = {
                countryScopes: Array.isArray(data?.countryScopes) ? data.countryScopes.map(normalizeString).filter(Boolean) : [],
                nodes: Array.isArray(data?.nodes)
                    ? data.nodes.map((n) => ({ name: normalizeString(n?.name), countryScope: normalizeString(n?.countryScope) })).filter((n) => n.name)
                    : []
            };
        } catch (error) {
            console.error('[Contacts Territory] Lookup load failed.', error);
        }
    };

    const rowCountryScopes = (api) => {
        const scopes = new Set();
        api?.rows()?.data()?.each((row) => normalizeArray(row?.territoryCountryScopes).forEach((s) => scopes.add(s)));
        return Array.from(scopes);
    };
    const rowTerritoryNodes = (api) => {
        const out = [];
        api?.rows()?.data()?.each((row) => {
            const scopes = normalizeArray(row?.territoryCountryScopes);
            normalizeArray(row?.territoryNodeNames).forEach((name) => out.push({ name, countryScope: scopes[0] || '' }));
        });
        return out;
    };

    const populateCountryScopeOptions = (api) => {
        const select = document.getElementById('filterCountryScope');
        if (!select) return;
        const source = territoryLookups.countryScopes.length ? territoryLookups.countryScopes : rowCountryScopes(api);
        const scopes = Array.from(new Set(source.map(normalizeString).filter(Boolean))).sort((a, b) => a.localeCompare(b));
        appendOptions(select, scopes.map((value) => ({ value, text: value })));
    };
    const populateTerritoryOptions = (api, selectedCountries) => {
        const select = document.getElementById('filterTerritory');
        if (!select) return;
        const countries = normalizeArray(selectedCountries);
        const prevSelected = normalizeArray($('#filterTerritory').val());
        const source = territoryLookups.nodes.length ? territoryLookups.nodes : rowTerritoryNodes(api);
        const names = new Set();
        source.forEach((n) => {
            if (!n.name) return;
            if (countries.length && !countries.includes(normalizeString(n.countryScope))) return;
            names.add(n.name);
        });
        appendOptions(select, Array.from(names).sort((a, b) => a.localeCompare(b)).map((value) => ({ value, text: value })));
        $('#filterTerritory').val(prevSelected.filter((v) => names.has(v)));
    };

    const setupFilters = async (api) => {
        await loadLookupOptions();
        await loadTerritoryLookups();
        populateCountryScopeOptions(api);
        populateTerritoryOptions(api, []);
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });

        $('#filterCountryScope').off('change.territory-cascade').on('change.territory-cascade', function () {
            populateTerritoryOptions(api, $(this).val() || []);
            initSingleSelect2($('#filterTerritory'));
        });
        populateTerritoryOptions(api, $('#filterCountryScope').val() || []);
        initSingleSelect2($('#filterTerritory'));

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterStatus').val() || [],
                contactType: $('#filterContactType').val() || [],
                countryScope: $('#filterCountryScope').val() || [],
                territoryNode: $('#filterTerritory').val() || []
            };
            api.draw();
            window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            applySavedTableState(api, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });
    };

    // Presentation only: maps a published contact-status code to a badge tone. MOD-0048 stays the source of truth.
    const statusBadgeClass = (status) => ({
        active: 'bg-label-success',
        draft: 'bg-label-secondary',
        inactive: 'bg-label-secondary',
        archived: 'bg-label-dark'
    })[normalizeString(status).toLowerCase()] || 'bg-label-primary';

    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        bulkActionSelector: '[data-bulk-action]',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all',
        onBulkAction: {
            delete: async ({ ids }) => {
                if (!ids.length) return;
                const confirmText = (L.BulkDeleteConfirm || '').replace('{0}', ids.length);
                window.showConfirm?.(confirmText, async () => {
                    try {
                        const res = await fetch(`${apiUrl}/api/crm/contacts/bulk`, {
                            method: 'DELETE', credentials: 'include', headers: getAuthHeaders(true), body: JSON.stringify(ids)
                        });
                        if (!res.ok) throw new Error('Bulk delete failed.');
                        reloadWithSuccessToast('BulkDeleteSuccess', String(ids.length));
                    } catch (error) {
                        console.error(error);
                        window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }, { entityName: String(ids.length), type: 'danger', confirmButtonText: L.Delete });
            }
        }
    };
    const reloadWithSuccessToast = (messageKey, interpolationValue) =>
        window.DitenDataTable.reloadWithToast(dt, dtTableEl, messageKey, interpolationValue, bulkOptions);
    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = `/CRM/Contacts/Details/${id}`; },
        edit: ({ id }) => { if (id) window.location.href = `/CRM/Contacts/Edit/${id}`; },
        delete: ({ row }) => {
            if (!row?.id) return;
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const res = await fetch(`${apiUrl}/api/crm/contacts/${row.id}`, { method: 'DELETE', credentials: 'include', headers: getAuthHeaders() });
                    if (!res.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted');
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, { entityName: row.displayName, type: 'danger', confirmButtonText: L.Delete });
        }
    };

    const bindEvents = () => {
        // Quick View delegation is handled by DitenDataTable, equivalent to closest('.js-quick-view').
    };

    // ---- MOD-0150 Import/Export Task 1: template download + export options ----

    // Both endpoints are same-origin MVC proxy actions that call the Gateway server-side (the browser never talks to
    // the CRM service). A plain navigation keeps the browser's own download UX and the auth cookie.
    const startDownload = (url) => {
        window.location.href = url;
        window.showToast?.(L.ExportStarted || '', 'info');
    };

    const downloadTemplate = () => {
        if (!capabilities.canImport || !capabilities.templateUrl) return;
        const params = new URLSearchParams({ includeAccounts: capabilities.canReadAccounts ? 'true' : 'false' });
        startDownload(`${capabilities.templateUrl}?${params.toString()}`);
    };

    const exportOptionsHtml = () => {
        const linkOption = capabilities.canReadAccountContacts
            ? `<div class="form-check mb-2">
                   <input class="form-check-input" type="checkbox" id="contacts-export-links" checked>
                   <label class="form-check-label" for="contacts-export-links">${L.ExportIncludeLinks || ''}</label>
               </div>
               <div class="form-check mb-2 ms-4">
                   <input class="form-check-input" type="checkbox" id="contacts-export-historical">
                   <label class="form-check-label" for="contacts-export-historical">${L.ExportIncludeHistorical || ''}</label>
               </div>`
            : '';

        const accountsOption = capabilities.canReadAccounts
            ? `<div class="form-check mb-2">
                   <input class="form-check-input" type="checkbox" id="contacts-export-accounts">
                   <label class="form-check-label" for="contacts-export-accounts">${L.ExportIncludeAccounts || ''}</label>
               </div>`
            : '';

        return `<div class="text-start">
                    <div class="alert alert-warning py-2 px-3 mb-3" role="alert">${L.ExportPiiWarning || ''}</div>
                    ${linkOption}
                    ${accountsOption}
                    <div class="form-check mb-2">
                        <input class="form-check-input" type="checkbox" id="contacts-export-notes">
                        <label class="form-check-label" for="contacts-export-notes">${L.ExportIncludeNotes || ''}</label>
                    </div>
                    <div class="text-muted small">${L.ExportNotesWarning || ''}</div>
                </div>`;
    };

    const openExportDialog = () => {
        if (!capabilities.canExport || !capabilities.exportUrl) return;
        if (typeof window.showConfirm !== 'function') return;

        window.showConfirm(L.ExportOptions || L.ExportContacts || '', () => {
            const checked = (id) => !!document.getElementById(id)?.checked;
            const includeLinks = capabilities.canReadAccountContacts && checked('contacts-export-links');
            const params = new URLSearchParams({
                includeLinks: includeLinks ? 'true' : 'false',
                includeHistorical: includeLinks && checked('contacts-export-historical') ? 'true' : 'false',
                includeNotes: checked('contacts-export-notes') ? 'true' : 'false',
                includeAccounts: capabilities.canReadAccounts && checked('contacts-export-accounts') ? 'true' : 'false'
            });

            // Reuse whatever the user already filtered the grid by, so the file matches what they see.
            appliedFilters.contactType.forEach((value) => params.set('contactType', value));
            appliedFilters.status.forEach((value) => params.set('status', value));

            startDownload(`${capabilities.exportUrl}?${params.toString()}`);
        }, {
            type: 'warning',
            width: '520px',
            subtext: exportOptionsHtml(),
            confirmButtonText: L.Download || L.ExportContacts || '',
            cancelButtonText: L.Cancel || '',
            didOpen: (popup) => {
                popup.querySelector('.swal2-html-container')?.classList.add('text-start');
                const links = popup.querySelector('#contacts-export-links');
                const historical = popup.querySelector('#contacts-export-historical');
                if (!links || !historical) return;
                // Historical links only make sense when links themselves are exported.
                const syncHistorical = () => {
                    historical.disabled = !links.checked;
                    if (!links.checked) historical.checked = false;
                };
                links.addEventListener('change', syncHistorical);
                syncHistorical();
            }
        });
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[Contacts] window.API.crm (or window.ApiBaseUrl) is required.'); return; }
        syncL10n();
        await loadDefaultView();
        const extraButtons = {
            // Upload → dry-run preview → apply lives on its own compact page (the preview is a table to read, not a
            // toast). Hidden entirely without crm.contact.import; the page and the API re-check server-side.
            importBtn: capabilities.canImport ? {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'data-bs-toggle': 'tooltip' },
                action: () => { window.location.href = '/CRM/Contacts/Import'; }
            } : undefined,
            // Server-side template/export live inside the shared Action dropdown, next to Import — the client-side
            // print/CSV/Excel/PDF entries above them only dump the visible grid.
            collectionBtns: [
                capabilities.canImport ? {
                    text: L.DownloadTemplate,
                    icon: 'bx-file-blank',
                    className: 'dt-template-btn',
                    action: () => downloadTemplate()
                } : null,
                capabilities.canExport ? {
                    text: L.ExportContacts,
                    icon: 'bx-export',
                    className: 'dt-export-contacts-btn',
                    action: () => openExportDialog()
                } : null
            ].filter(Boolean),
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || L.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[Contacts SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        // DitenDataTable wraps the DataTables v2 constructor and shared defaults:
        // new DataTable(...)
        // window.DtDefaults.create(...)
        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            bulk: bulkOptions,
            ajax: {
                url: apiUrl + '/api/crm/contacts?page=1&pageSize=500',
                type: 'GET',
                xhrFields: { withCredentials: true },
                dataSrc: (json) => json?.data?.items ?? json?.Data?.Items ?? []
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'displayName', name: 'displayName' },
                    { data: 'contactType', name: 'contactType' },
                    { data: 'professionalTitle', name: 'professionalTitle' },
                    { data: 'email', name: 'email' },
                    { data: 'phone', name: 'phone' },
                    { data: 'status', name: 'status' },
                    { data: 'territoryCountryScopes', name: 'countryScope' },
                    { data: 'territoryNodeNames', name: 'territoryNode' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    {
                        // Avatar thumbnail + name. Non-display types return the plain name so search/sort/EXPORT use the
                        // name only (the photo never enters a client-side export — PII stays off exports).
                        targets: 2,
                        render: (data, type, full) => {
                            const name = data ?? '';
                            if (type !== 'display') return name;
                            const photo = full && full.photoDataUri;
                            const avatar = photo
                                ? `<img src="${photo}" class="rounded-circle flex-shrink-0" style="width:32px;height:32px;object-fit:cover;" alt="">`
                                : `<span class="rounded-circle border d-inline-flex align-items-center justify-content-center text-muted flex-shrink-0" style="width:32px;height:32px;"><i class="icon-base ti tabler-user"></i></span>`;
                            return `<div class="d-flex align-items-center gap-2">${avatar}<span class="fw-medium text-heading">${name}</span></div>`;
                        }
                    },
                    { targets: 3, render: (data) => data ? `<span class="badge bg-label-info">${data}</span>` : '' },
                    {
                        targets: 7,
                        render: (data, type) =>
                            type === 'display' && data
                                ? `<span class="badge ${statusBadgeClass(data)}">${data}</span>`
                                : (data ?? '')
                    },
                    {
                        // Country scope(s) of the contact's linked accounts. Array → comma text; plain for sort/export.
                        targets: 8,
                        render: (data, type) => {
                            const arr = Array.isArray(data) ? data.map(normalizeString).filter(Boolean) : [];
                            const text = arr.join(', ');
                            if (type !== 'display') return text;
                            return text ? text : '<span class="text-muted">—</span>';
                        }
                    },
                    {
                        // Territory node(s) of the contact's linked accounts. Array → chips for display; plain text otherwise.
                        targets: 9,
                        render: (data, type) => {
                            const arr = Array.isArray(data) ? data.map(normalizeString).filter(Boolean) : [];
                            if (type !== 'display') return arr.join(', ');
                            return arr.length
                                ? arr.map((n) => `<span class="badge bg-label-secondary me-1">${n}</span>`).join('')
                                : '<span class="text-muted">—</span>';
                        }
                    },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, "&#39;");
                            return window.DitenDataTable.renderActions([
                                { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, 'title': L.QuickView } },
                                { key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': full.id, 'data-json': rowJson } },
                                { key: 'delete', className: 'text-danger', icon: 'bx bx-trash', text: L.Delete, attrs: { 'data-json': rowJson } }
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew,
                    { href: '/CRM/Contacts/Create' },
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5, 6, 7, 8, 9], colvisColumns: [2, 3, 4, 5, 6, 7, 8, 9] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/CRM/Contacts/Create';
                    });
                    setTimeout(() => { saveFilterArmed = true; }, 0);
                },
                drawCallback: function () {
                    window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                }
            }
        });
        dt.on('column-visibility.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        dt.on('search.dt order.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        dt.on('column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    return {
        init: function () {
            registerTableFilters();
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => ContactsList.init());
