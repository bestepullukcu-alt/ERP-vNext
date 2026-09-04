/**
 * MOD-0149 Accounts - DataTables Index Script (Golden Reference Compact).
 * Large-field pattern: create/edit use full MVC pages.
 * All API traffic goes through the Gateway; the CRM service is never called directly.
 */
'use strict';

const AccountsList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    // Server-side processing: the DataTables `draw` counter of the in-flight request, echoed back by dataFilter so
    // DataTables can match the response to its request (43,374-account tenant — the grid MUST page on the backend).
    let lastDrawToken = 0;

    const dtTableEl = document.querySelector('.datatables-accounts');
    const apiUrl = window.API?.crm ?? window.ApiBaseUrl;
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'Accounts' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8];
    const totalColumnCount = 10;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6, 7, 8];
    // AccountName ascending (column 3) is the server-side default sort; keep the SaveView baseline in sync so the
    // grid does not read as "dirty" on first load.
    const baseOrder = [[3, 'asc']];
    let appliedFilters = { status: [], accountType: [], countryScope: [], territoryNode: [] };
    // Country-scope + territory-node options come from MOD-0151 Territory Management (distinct across the tenant),
    // not just from the accounts on this page. Loaded once in setupFilters; falls back to row-derived values if empty.
    let territoryLookups = { countryScopes: [], nodes: [] };
    let L = window.L10n || {};

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
    const emptyFilters = () => ({ status: [], accountType: [], countryScope: [], territoryNode: [] });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            status: normalizeArray(source.status),
            accountType: normalizeArray(source.accountType),
            countryScope: normalizeArray(source.countryScope),
            territoryNode: normalizeArray(source.territoryNode)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(normalizeString(actual));
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
        filters: Object.keys(v?.filters || {}).sort().reduce((acc, key) => {
            acc[key] = normalizeFilterValue(v.filters[key]);
            return acc;
        }, {}),
        search: normalizeString(v?.search),
        // Fill every managed column with its default so a view saved before a column was added (its stored colVis
        // omits the new column) does not read as permanently "dirty" against the freshly-captured full colVis.
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
            console.error('[Accounts SaveView] Failed to load saved views.', error);
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
        defaultViewRecord = savedRecord && typeof savedRecord === 'object'
            ? savedRecord
            : Object.assign({}, defaultViewRecord || {}, payload);
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
                && matchesMultiFilter(appliedFilters.accountType, row.accountType)
                && matchesMultiFilter(appliedFilters.countryScope, row.territoryCountryScope)
                && matchesMultiFilter(appliedFilters.territoryNode, row.territoryNodeName);
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
        $('#filterStatus, #filterAccountType, #filterCountryScope, #filterTerritory').each(function () {
            initSingleSelect2($(this));
        });
    };

    const syncFilterControls = (values) => {
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterAccountType').val(normalizeArray(values.accountType)).trigger('change');
        $('#filterCountryScope').val(normalizeArray(values.countryScope)).trigger('change');
        $('#filterTerritory').val(normalizeArray(values.territoryNode)).trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.status, appliedFilters.accountType, appliedFilters.countryScope, appliedFilters.territoryNode]
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
    // Filter options are sourced from MOD-0048 published values via the same-origin proxy.
    // If the reference set is unavailable the chip stays empty on purpose - never a local fallback list.
    const loadLookupOptions = async () => {
        const statusSelect = document.getElementById('filterStatus');
        const accountTypeSelect = document.getElementById('filterAccountType');
        if (!statusSelect || !accountTypeSelect) return;
        try {
            const res = await fetch('/CRM/Accounts/lookups', {
                method: 'GET',
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            if (!res.ok) return;
            const data = await res.json();
            appendOptions(statusSelect, Array.isArray(data?.statuses) ? data.statuses : []);
            appendOptions(accountTypeSelect, Array.isArray(data?.accountTypes) ? data.accountTypes : []);
        } catch (error) {
            console.error('[Accounts Lookup] Failed.', error);
        }
    };

    // Territory Management is the source of truth for the country-scope and territory-node chips (distinct across the
    // tenant). Loaded once; if it is unavailable (e.g. no crm.territory.model.read) the chips fall back to whatever
    // values the loaded account rows carry so filtering still works on what is on screen.
    const loadTerritoryLookups = async () => {
        try {
            const res = await fetch('/CRM/Accounts/territory-lookups', {
                method: 'GET',
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            if (!res.ok) return;
            const data = await res.json();
            territoryLookups = {
                countryScopes: Array.isArray(data?.countryScopes) ? data.countryScopes.map(normalizeString).filter(Boolean) : [],
                // Each node carries its id (the value sent to the backend for precise coverage filtering), its display
                // name, and its owning model's country scope (drives the cascade).
                nodes: Array.isArray(data?.nodes)
                    ? data.nodes.map((n) => ({ id: normalizeString(n?.id), name: normalizeString(n?.name), countryScope: normalizeString(n?.countryScope) })).filter((n) => n.id && n.name)
                    : []
            };
        } catch (error) {
            console.error('[Accounts Territory] Lookup load failed.', error);
        }
    };

    const rowCountryScopes = (api) => {
        const scopes = new Set();
        api?.rows()?.data()?.each((row) => { const s = normalizeString(row?.territoryCountryScope); if (s) scopes.add(s); });
        return Array.from(scopes);
    };
    const rowTerritoryNodes = (api) => {
        const out = [];
        api?.rows()?.data()?.each((row) => {
            const id = normalizeString(row?.territoryNodeId);
            const name = normalizeString(row?.territoryNodeName);
            if (id && name) out.push({ id, name, countryScope: normalizeString(row?.territoryCountryScope) });
        });
        return out;
    };

    // Country-scope chip: distinct scopes from Territory Management (fallback: the loaded rows).
    const populateCountryScopeOptions = (api) => {
        const select = document.getElementById('filterCountryScope');
        if (!select) return;
        const source = territoryLookups.countryScopes.length ? territoryLookups.countryScopes : rowCountryScopes(api);
        const scopes = Array.from(new Set(source.map(normalizeString).filter(Boolean))).sort((a, b) => a.localeCompare(b));
        appendOptions(select, scopes.map((value) => ({ value, text: value })));
    };
    // Territory-node chip cascades from the selected country scope(s): only nodes whose owning model is in a selected
    // country are offered (no country selected ⇒ all). Any still-valid current selection is preserved.
    const populateTerritoryOptions = (api, selectedCountries) => {
        const select = document.getElementById('filterTerritory');
        if (!select) return;
        const countries = normalizeArray(selectedCountries);
        const prevSelected = normalizeArray($('#filterTerritory').val());
        const source = territoryLookups.nodes.length ? territoryLookups.nodes : rowTerritoryNodes(api);
        // The chip VALUE is the node id (sent to the backend); the TEXT is the display name. Distinct on id, narrowed
        // to the selected country scope(s).
        const byId = new Map();
        source.forEach((n) => {
            if (!n.id || !n.name) return;
            if (countries.length && !countries.includes(normalizeString(n.countryScope))) return;
            if (!byId.has(n.id)) byId.set(n.id, n.name);
        });
        appendOptions(select, Array.from(byId.entries())
            .sort((a, b) => a[1].localeCompare(b[1]))
            .map(([value, text]) => ({ value, text })));
        $('#filterTerritory').val(prevSelected.filter((v) => byId.has(v)));
    };

    const setupFilters = async (api) => {
        await loadLookupOptions();
        await loadTerritoryLookups();
        populateCountryScopeOptions(api);
        populateTerritoryOptions(api, []);
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });

        // Cascade: changing the country scope re-narrows the territory node options (and re-inits its Select2).
        $('#filterCountryScope').off('change.territory-cascade').on('change.territory-cascade', function () {
            populateTerritoryOptions(api, $(this).val() || []);
            initSingleSelect2($('#filterTerritory'));
        });
        // Reflect any country scope restored from a saved view onto the territory options right away.
        populateTerritoryOptions(api, $('#filterCountryScope').val() || []);
        initSingleSelect2($('#filterTerritory'));

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterStatus').val() || [],
                accountType: $('#filterAccountType').val() || [],
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

    // Presentation only: maps a published account-status code to a badge tone.
    // MOD-0048 remains the source of truth for which codes exist; unknown codes render neutral.
    const statusBadgeClass = (status) => ({
        active: 'bg-label-success',
        draft: 'bg-label-secondary',
        inactive: 'bg-label-secondary',
        suspended: 'bg-label-warning',
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
                        const res = await fetch(`${apiUrl}/api/crm/accounts/bulk`, {
                            method: 'DELETE',
                            credentials: 'include',
                            headers: getAuthHeaders(true),
                            body: JSON.stringify(ids)
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
        quickView: ({ id }) => {
            if (id) window.location.href = `/CRM/Accounts/Details/${id}`;
        },
        edit: ({ id }) => {
            if (id) window.location.href = `/CRM/Accounts/Edit/${id}`;
        },
        delete: ({ row }) => {
            if (!row?.id) return;
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const res = await fetch(`${apiUrl}/api/crm/accounts/${row.id}`, {
                        method: 'DELETE',
                        credentials: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!res.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted');
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, { entityName: row.accountName, type: 'danger', confirmButtonText: L.Delete });
        }
    };

    const bindEvents = () => {
        // Quick View delegation is handled by DitenDataTable, equivalent to closest('.js-quick-view').
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[Accounts] window.API.crm (or window.ApiBaseUrl) is required.'); return; }
        syncL10n();
        await loadDefaultView();
        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'data-bs-toggle': 'tooltip' },
                action: () => window.showToast?.(L.ComingSoon, 'warning')
            },
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
                        console.error('[Accounts SaveView] Failed to save default view.', error);
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
                // TRUE server-side processing. The CRM list endpoint is paged (Response<PagedResult<T>>): every page,
                // search and sort is resolved on the backend so all 43K accounts are reachable (was: one wide 500-row
                // page filtered/paged in the browser, which hid ~42.9K rows).
                url: apiUrl + '/api/crm/accounts',
                type: 'GET',
                xhrFields: { withCredentials: true },
                // Map the DataTables request → CRM list query string.
                data: function (d) {
                    lastDrawToken = d.draw;
                    const length = d.length > 0 ? d.length : 25;
                    const params = {
                        page: Math.floor((d.start || 0) / length) + 1,
                        pageSize: length,
                        search: d?.search?.value ? d.search.value : ''
                    };
                    // Inline-filter chips pushed to the backend (server-side). Status + Account Type are plain stored
                    // Account fields, sent as comma-separated codes (multi-select ⇒ backend IN predicate). The
                    // Country-Scope and Territory-Node chips are MOD-0151 territory-coverage projections, not stored
                    // Account fields: the backend resolves them to the set of current-coverage account ids (both
                    // lifecycle gates at now) and ANDs that onto the query. The Country-Scope chip carries the owning
                    // model's `country` scope code; the Territory-Node chip carries the node id (both comma-separated).
                    const statusVals = normalizeArray(appliedFilters.status);
                    const typeVals = normalizeArray(appliedFilters.accountType);
                    const countryScopeVals = normalizeArray(appliedFilters.countryScope);
                    const nodeVals = normalizeArray(appliedFilters.territoryNode);
                    if (statusVals.length) params.status = statusVals.join(',');
                    if (typeVals.length) params.accountType = typeVals.join(',');
                    if (countryScopeVals.length) params.countryScope = countryScopeVals.join(',');
                    if (nodeVals.length) params.territoryNodeId = nodeVals.join(',');
                    // Only accountCode/accountName are backend-sortable (each backed by a {TenantId, field} index).
                    // Their columns are the ONLY orderable ones (see columnDefs), so order[0] can only be one of them;
                    // anything else is ignored and the backend falls back to AccountName asc.
                    if (Array.isArray(d.order) && d.order.length) {
                        const col = d.columns?.[d.order[0].column];
                        const sortName = col && (col.data || col.name);
                        if (sortName === 'accountCode' || sortName === 'accountName') {
                            params.sortBy = sortName;
                            params.sortDir = d.order[0].dir === 'desc' ? 'desc' : 'asc';
                        }
                    }
                    return params;
                },
                // Reshape the Response<PagedResult> envelope into the {draw,recordsTotal,recordsFiltered,data} contract
                // DataTables server-side expects. Runs in jQuery BEFORE DataTables reads the counts, so it is the only
                // reliable place to remap them (a dataSrc function runs too late for recordsTotal/recordsFiltered).
                dataFilter: function (raw) {
                    let json;
                    try { json = JSON.parse(raw); } catch (e) { json = {}; }
                    const paged = json?.data ?? json?.Data ?? {};
                    const items = paged.items ?? paged.Items ?? [];
                    const filtered = paged.total ?? paged.Total ?? items.length;             // recordsFiltered
                    const totalAll = paged.unfilteredTotal ?? paged.UnfilteredTotal ?? filtered; // recordsTotal
                    return JSON.stringify({
                        draw: lastDrawToken,
                        recordsTotal: totalAll,
                        recordsFiltered: filtered,
                        data: items
                    });
                },
                dataSrc: 'data'
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                // True DataTables server-side processing (paging/search/sort resolved by the CRM backend).
                serverSide: true,
                processing: true,
                // Default sort stays AccountName ascending (column 3), matching the backend default and the repo's
                // prior AccountName ordering. accountCode(2) + accountName(3) are the only backend-sortable columns.
                order: [[3, 'asc']],
                colReorder: { columns: ':gt(1):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'accountCode', name: 'accountCode' },
                    { data: 'accountName', name: 'accountName' },
                    { data: 'accountType', name: 'accountType' },
                    { data: 'accountCategory', name: 'accountCategory' },
                    { data: 'status', name: 'status' },
                    { data: 'territoryCountryScope', name: 'countryScope' },
                    { data: 'territoryNodeName', name: 'territoryNode' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    // Server-side: only accountCode(2) + accountName(3) are backend-sortable (indexed). accountType(4),
                    // accountCategory(5), status(6) and the computed territory columns(7,8) are NOT sortable on the
                    // backend, so they are non-orderable here — no misleading sort arrow, and no unindexed 32MB sort.
                    { targets: [4, 5, 6, 7, 8], orderable: false },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    {
                        // Logo thumbnail + account name (mirrors the Contacts list avatar). Non-display types return the
                        // plain name so search/sort/EXPORT stay text-only — the logo never enters a client-side export.
                        targets: 3,
                        render: (data, type, full) => {
                            const name = data ?? '';
                            if (type !== 'display') return name;
                            const logo = full && full.logoDataUri;
                            const thumb = logo
                                ? `<img src="${logo}" class="rounded border flex-shrink-0" style="width:32px;height:32px;object-fit:contain;background:var(--bs-body-bg);" alt="">`
                                : `<span class="rounded border d-inline-flex align-items-center justify-content-center text-muted flex-shrink-0" style="width:32px;height:32px;"><i class="icon-base bx bx-buildings"></i></span>`;
                            return `<div class="d-flex align-items-center gap-2">${thumb}<span class="fw-medium text-heading">${name}</span></div>`;
                        }
                    },
                    {
                        targets: 4,
                        render: (data) => data ? `<span class="badge bg-label-info">${data}</span>` : ''
                    },
                    {
                        targets: 6,
                        render: (data, type) =>
                            type === 'display' && data
                                ? `<span class="badge ${statusBadgeClass(data)}">${data}</span>`
                                : (data ?? '')
                    },
                    {
                        // Current territory country scope. Plain text for search/sort/export; em dash when unassigned.
                        targets: 7,
                        render: (data, type) => {
                            const text = normalizeString(data);
                            if (type !== 'display') return text;
                            return text ? text : '<span class="text-muted">—</span>';
                        }
                    },
                    {
                        // Current territory node(s). Display shows a neutral chip; non-display returns plain text so
                        // search/sort/export stay text-only. Unassigned rows render an em dash.
                        targets: 8,
                        render: (data, type) => {
                            const text = normalizeString(data);
                            if (type !== 'display') return text;
                            return text
                                ? `<span class="badge bg-label-secondary">${text}</span>`
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
                                {
                                    key: 'quickView',
                                    className: 'js-quick-view me-1',
                                    icon: 'bx bx-show',
                                    attrs: { 'data-id': full.id, 'title': L.QuickView }
                                },
                                {
                                    key: 'edit',
                                    className: 'js-edit-item',
                                    icon: 'bx bx-edit',
                                    text: L.Edit,
                                    attrs: { 'data-id': full.id, 'data-json': rowJson }
                                },
                                {
                                    key: 'delete',
                                    className: 'text-danger',
                                    icon: 'bx bx-trash',
                                    text: L.Delete,
                                    attrs: { 'data-json': rowJson }
                                }
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew,
                    { href: '/CRM/Accounts/Create' },
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5, 6, 7, 8], colvisColumns: [2, 3, 4, 5, 6, 7, 8] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/CRM/Accounts/Create';
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

document.addEventListener('DOMContentLoaded', () => AccountsList.init());
