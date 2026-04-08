/**
 * Countries DataTables Page Script
 * Diten ERP vNext - MDM/Countries
 */
'use strict';

const CountriesList = (function () {
    let dt;
    const dtTableEl = document.querySelector('.datatables-countries');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MDM', pageKey: 'Countries' };
    let L = window.L10n || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6];
    const totalColumnCount = 8;
    let saveFilterArmed = false;
    const baseOrder = [[2, 'desc']];
    let appliedFilters = { status: '' };
    let defaultViewRecord = null;
    let defaultViewState = null;
    const isAuthHandledError = (error) => error?.authHandled === true || error?.code === 'auth-refresh-in-progress';

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
            return;
        }
        L = L || {};
    };

    const getTenantId = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (e) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    };

    const getAuthHeaders = () => {
        const token = getCookie('access_token');
        return {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };
    };

    const getStatusMap = () => ({
        'true': { title: L.Active, class: 'bg-label-success' },
        'false': { title: L.Passive, class: 'bg-label-secondary' }
    });

    const tryParseRowJson = (element) => {
        if (!element) return null;
        const raw = element.getAttribute('data-json');
        if (!raw) return null;
        try {
            return JSON.parse(raw.replace(/&#39;/g, "'"));
        } catch (err) {
            console.error('[Countries QuickView] Could not parse row data', err);
            return null;
        }
    };

    const populateOffcanvas = (data) => {
        if (!data) return;
        document.getElementById('oc-title').innerText = data.name || '-';
        document.getElementById('oc-subtitle').innerText = data.iso2Code || '-';
        const status = getStatusMap()[String(data.isActive)] || { title: L.Unknown || String(data.isActive), class: 'bg-label-primary' };
        const statusEl = document.getElementById('oc-status');
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';

        document.getElementById('oc-countryName').innerText = data.name || '-';
        document.getElementById('oc-iso2').innerText = data.iso2Code || '-';
        document.getElementById('oc-iso3').innerText = data.iso3Code || '-';
        document.getElementById('oc-phone').innerText = data.phoneCode || '-';

        document.getElementById('oc-btn-edit').href = `/Countries/Edit/${data.id}`;
    };

    // ─── Save View Helpers ───────────────────────────────────────────────────────

    const normalizeSavedString = (value) => typeof value === 'string' ? value.trim() : '';

    const getSavedViewFlag = (savedView, camelKey, pascalKey) => {
        if (!savedView || typeof savedView !== 'object') return undefined;
        if (typeof savedView[camelKey] !== 'undefined') return savedView[camelKey];
        if (typeof savedView[pascalKey] !== 'undefined') return savedView[pascalKey];
        return undefined;
    };

    const getSavedViewDefinition = (savedView) => {
        if (!savedView || typeof savedView !== 'object') return {};
        const rawDefinition =
            savedView.viewDefinition ??
            savedView.ViewDefinition ??
            savedView.viewDefinitionJson ??
            savedView.ViewDefinitionJson ??
            {};
        if (rawDefinition && typeof rawDefinition === 'object') return rawDefinition;
        if (typeof rawDefinition === 'string') {
            try {
                const parsed = JSON.parse(rawDefinition);
                return parsed && typeof parsed === 'object' ? parsed : {};
            } catch (e) { return {}; }
        }
        return {};
    };

    const getSavedViewId = (savedView) =>
        normalizeSavedString(
            getSavedViewFlag(savedView, 'id', 'Id') ||
            getSavedViewFlag(savedView, '_id', '_id'));

    const isSavedViewDefault = (savedView) => {
        const value = getSavedViewFlag(savedView, 'isDefault', 'IsDefault');
        return value === true;
    };

    const getSavedViewName = (savedView) =>
        normalizeSavedString(getSavedViewFlag(savedView, 'viewName', 'ViewName'));

    const createDefaultColumnVisibility = () =>
        saveViewColumnIndexes.reduce((acc, columnIndex) => { acc[columnIndex] = true; return acc; }, {});

    const normalizeColumnVisibility = (colVis) => {
        if (!colVis) return null;
        const normalized = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((columnIndex, position) => {
                if (typeof colVis[columnIndex] === 'boolean') { normalized[columnIndex] = colVis[columnIndex]; return; }
                if (typeof colVis[position] === 'boolean') { normalized[columnIndex] = colVis[position]; }
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((columnIndex) => {
                const rawValue = colVis[columnIndex];
                if (typeof rawValue === 'boolean') normalized[columnIndex] = rawValue;
            });
        }
        return Object.keys(normalized).length ? normalized : null;
    };

    const areColumnVisibilitiesEqual = (left, right) => {
        const normalizedLeft = normalizeColumnVisibility(left);
        const normalizedRight = normalizeColumnVisibility(right);
        if (!normalizedLeft && !normalizedRight) return true;
        if (!normalizedLeft || !normalizedRight) return false;
        return saveViewColumnIndexes.every((columnIndex) => {
            const leftValue = typeof normalizedLeft[columnIndex] === 'boolean' ? normalizedLeft[columnIndex] : true;
            const rightValue = typeof normalizedRight[columnIndex] === 'boolean' ? normalizedRight[columnIndex] : true;
            return leftValue === rightValue;
        });
    };

    const captureColumnVisibility = (api) => {
        const colVis = {};
        saveViewColumnIndexes.forEach((columnIndex) => {
            try { colVis[columnIndex] = !!api.column(columnIndex).visible(); } catch (e) { }
        });
        return Object.keys(colVis).length ? colVis : null;
    };

    const applyColumnVisibility = (api, colVis) => {
        const normalized = normalizeColumnVisibility(colVis);
        if (!normalized) return;
        saveViewColumnIndexes.forEach((columnIndex) => {
            const shouldBeVisible = normalized[columnIndex];
            if (typeof shouldBeVisible !== 'boolean') return;
            try { api.column(columnIndex).visible(shouldBeVisible, false); } catch (e) { }
        });
    };

    const normalizeColumnOrder = (columnOrder) => {
        if (!Array.isArray(columnOrder) || columnOrder.length !== totalColumnCount) return null;
        const normalized = columnOrder
            .map((index) => Number(index))
            .filter((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount);
        if (normalized.length !== totalColumnCount) return null;
        if (new Set(normalized).size !== totalColumnCount) return null;
        return normalized;
    };

    const areColumnOrdersEqual = (left, right) => {
        const normalizedLeft = normalizeColumnOrder(left) || Array.from({ length: totalColumnCount }, (_, index) => index);
        const normalizedRight = normalizeColumnOrder(right) || Array.from({ length: totalColumnCount }, (_, index) => index);
        return normalizedLeft.every((value, index) => value === normalizedRight[index]);
    };

    const captureColumnOrder = (api) => {
        try {
            const order = api?.colReorder?.order?.();
            return normalizeColumnOrder(order);
        } catch (e) { return null; }
    };

    const applyColumnOrder = (api, columnOrder) => {
        const normalized = normalizeColumnOrder(columnOrder);
        if (!normalized || typeof api?.colReorder?.order !== 'function') return;
        try { api.colReorder.order(normalized, true); } catch (e) { }
    };

    const getSearchInputValue = (api) => {
        try {
            const container = api.table().container();
            const input = container?.querySelector('.dt-search input');
            return typeof input?.value === 'string' ? input.value : '';
        } catch (e) { return ''; }
    };

    const syncSearchInput = (api, searchValue) => {
        try {
            const container = api.table().container();
            const input = container?.querySelector('.dt-search input');
            if (input) input.value = searchValue || '';
        } catch (e) { }
    };

    const applyFilterValues = (api, values) => {
        const status = values?.status || '';
        api.column('isActive:name').search(status);
    };

    const syncFilterControls = (values) => {
        const status = normalizeSavedString(values?.status);
        $('#filterStatus').val(status).trigger('change');
    };

    const mapSavedViewToState = (savedView) => {
        const definition = getSavedViewDefinition(savedView);
        return {
            status: normalizeSavedString(definition.status),
            search: normalizeSavedString(definition.search),
            colVis: normalizeColumnVisibility(definition.colVis),
            columnOrder: normalizeColumnOrder(definition.columnOrder),
            order: Array.isArray(definition.order) ? definition.order : null
        };
    };

    const getCurrentView = (api) => {
        const status = appliedFilters?.status || '';
        const inputSearch = getSearchInputValue(api);
        const search = typeof inputSearch === 'string' && inputSearch.length >= 0
            ? inputSearch
            : (typeof api?.search === 'function' ? (api.search() || '') : '');
        let colVis = null;
        try { colVis = captureColumnVisibility(api); } catch (e) { colVis = null; }
        let order = null;
        try { order = api?.order?.() || null; } catch (e) { order = null; }
        return { status, search, colVis, columnOrder: captureColumnOrder(api), order };
    };

    const applySavedTableState = (api, view, options) => {
        const state = view || {};
        const fallbackOrder = Array.isArray(options?.fallbackOrder) ? options.fallbackOrder : baseOrder;
        const fallbackColVis = options?.resetColumns === true ? createDefaultColumnVisibility() : null;
        const fallbackColumnOrder = options?.resetColumnOrder === true
            ? Array.from({ length: totalColumnCount }, (_, index) => index)
            : null;
        const colVisToApply = state.colVis || fallbackColVis;
        const columnOrderToApply = state.columnOrder || fallbackColumnOrder;

        if (typeof state.search === 'string') {
            try { api.search(state.search); } catch (e) { }
            syncSearchInput(api, state.search);
        } else if (options?.clearSearch === true) {
            try { api.search(''); } catch (e) { }
            syncSearchInput(api, '');
        }

        if (columnOrderToApply) applyColumnOrder(api, columnOrderToApply);
        if (colVisToApply) applyColumnVisibility(api, colVisToApply);

        if (Array.isArray(state.order)) {
            try { api.order(state.order); } catch (e) { }
        } else if (fallbackOrder) {
            try { api.order(fallbackOrder); } catch (e) { }
        }

        appliedFilters = { status: state.status || '' };
        syncFilterControls(appliedFilters);
        applyFilterValues(api, appliedFilters);

        try { api.columns.adjust(); } catch (e) { }
        api.draw(false);

        setTimeout(() => {
            syncFilterControls(appliedFilters);
            syncSearchInput(api, typeof state.search === 'string' ? state.search : '');
            window.DtDefaults.updateVisualState(api, getAppliedFilterCount(api));
        }, 0);
    };

    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            defaultViewRecord = Array.isArray(views)
                ? (views.find(isSavedViewDefault) || views[0] || null)
                : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (isAuthHandledError(error)) return null;
            console.error('[Countries SaveView] Failed to load saved views', error);
            defaultViewRecord = null;
            defaultViewState = null;
            return null;
        }
    };

    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView || !personalizationClient?.updateView) {
            throw new Error('Personalization client is unavailable.');
        }
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || '').trim(),
            viewDefinition: view || {},
            isDefault: true,
            visibility: 'private'
        };
        const existingViewId = getSavedViewId(defaultViewRecord);
        const savedView = existingViewId
            ? await personalizationClient.updateView(existingViewId, payload)
            : await personalizationClient.saveView(payload);
        defaultViewRecord = savedView;
        defaultViewState = mapSavedViewToState(savedView);
        return defaultViewState;
    };

    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const isDirtyComparedToDefault = (api) => {
        const def = defaultViewState || null;
        const cur = getCurrentView(api);
        const curHasHiddenCols = !!saveViewColumnIndexes.find((columnIndex) => cur.colVis?.[columnIndex] === false);
        const ref = def || {
            status: '',
            search: '',
            colVis: createDefaultColumnVisibility(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
            order: baseOrder
        };
        const colVisEqual = areColumnVisibilitiesEqual(ref.colVis, cur.colVis);
        const columnOrderEqual = areColumnOrdersEqual(ref.columnOrder, cur.columnOrder);
        const refOrder = Array.isArray(ref.order) ? ref.order : null;
        const curOrder = Array.isArray(cur.order) ? cur.order : null;
        const orderEqual = Array.isArray(refOrder) && Array.isArray(curOrder) && refOrder.length === curOrder.length
            ? refOrder.every((o, i) => String(o?.[0]) === String(curOrder[i]?.[0]) && String(o?.[1]) === String(curOrder[i]?.[1]))
            : refOrder === curOrder;
        if (!def) {
            return [cur.status].filter(Boolean).length > 0 ||
                !!cur.search ||
                curHasHiddenCols ||
                !columnOrderEqual ||
                !orderEqual;
        }
        return (String(cur.status || '') !== String(ref.status || '')) ||
            (String(cur.search || '') !== String(ref.search || '')) ||
            !colVisEqual ||
            !columnOrderEqual ||
            !orderEqual;
    };

    const getAppliedFilterCount = (api) => {
        try {
            const statusSearch = api.column('isActive:name').search() || '';
            return [statusSearch].filter(v => typeof v === 'string' ? v.trim() : !!v).length;
        } catch (e) {
            return [appliedFilters?.status].filter(Boolean).length;
        }
    };

    const syncPendingTableUiState = (api) => {
        const inputSearch = getSearchInputValue(api);
        const appliedSearch = typeof api?.search === 'function' ? (api.search() || '') : '';
        if (inputSearch !== appliedSearch) {
            try { api.search(inputSearch); } catch (e) { }
        }
    };

    // ─── Layout ──────────────────────────────────────────────────────────────────

    const mountInlineFilter = () => {
        if (!dtTableEl) return;
        const host = document.getElementById(filterHostId);
        if (!host) return;
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-6');
            return;
        }
        const dtContainer = dtTableEl.closest('.dt-container') || dtTableEl.closest('.dataTables_wrapper') || dtTableEl.parentElement;
        if (dtContainer) {
            dtContainer.insertAdjacentElement('beforeend', host);
            host.classList.add('px-6');
        }
    };

    const bindInlineFilterToggle = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const el = document.getElementById(filterCollapseId);
        if (!btn || !el) return;
        if (btn.dataset.inlineFilterBound) return;
        btn.dataset.inlineFilterBound = '1';
        el.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        el.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(el, { toggle: false });
            if (el.classList.contains('show')) instance.hide(); else instance.show();
        });
    };

    // ─── DataTable Init ──────────────────────────────────────────────────────────

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView();

        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'data-bs-toggle': 'tooltip' },
                action: function () { window.showToast?.(L.ComingSoon, 'warning'); }
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false' }
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        syncPendingTableUiState(tableApi);
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || 'RecordSaved', 'success');
                    } catch (error) {
                        if (isAuthHandledError(error)) return;
                        console.error('[Countries SaveView] Failed to save default view', error);
                        window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/countries',
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            stateSave: false, // data-dt-standard="v2": custom personalizationClient handles persistence
            colReorder: {
                columns: ':gt(1):not(:last-child)' // control(0) + checkbox(1) + action(last) sabit kalır
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'name', name: 'name' },
                { data: 'iso2Code', name: 'iso2Code' },
                { data: 'iso3Code', name: 'iso3Code' },
                { data: 'phoneCode', name: 'phoneCode' },
                { data: 'isActive', name: 'isActive' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                {
                    targets: 0,
                    className: 'control',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 2,
                    render: () => ''
                },
                {
                    targets: 1,
                    orderable: false,
                    searchable: false,
                    responsivePriority: 3,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">`
                },
                {
                    targets: 2,
                    responsivePriority: 1,
                    render: (data, type, full) => `<span class="fw-medium text-heading">${data}</span>`
                },
                {
                    targets: -2,
                    render: (data, type) => {
                        const status = getStatusMap()[String(data)] || { title: L.Unknown || String(data), class: 'bg-label-primary' };
                        if (type === 'display') return `<span class="badge ${status.class}" text-capitalized>${status.title}</span>`;
                        return status.title || '';
                    }
                },
                {
                    targets: -1,
                    title: L.Actions,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit',
                    render: (data, type, full) =>
                        `<div class="d-flex align-items-center">
                            <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
                            <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
                            <div class="dropdown-menu dropdown-menu-end m-0">
                                <a href="/Countries/Details/${full.id}" class="dropdown-item">${L.ViewDetails}</a>
                                <a href="javascript:void(0);" class="dropdown-item js-quick-view" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview" data-json='${JSON.stringify(full).replace(/'/g, "&#39;")}'>${L.QuickView}</a>
                                <a href="/Countries/Edit/${full.id}" class="dropdown-item">${L.Edit}</a>
                            </div>
                        </div>`
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.AddNewCountries,
                { onclick: "window.location.href='/Countries/Create'" },
                extraButtons,
                {
                    exportColumns: [2, 3, 4, 5, 6],
                    colvisColumns: [2, 3, 4, 5, 6]
                }
            ),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterToggle();
                setupFilters(this.api());
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                const api = this.api();
                const filterCount = getAppliedFilterCount(api);
                window.DtDefaults.updateVisualState(api, filterCount);
            }
        }));

        dt.on('column-visibility.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount(dt));
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('search.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('order.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount(dt));
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    // ─── Filters ─────────────────────────────────────────────────────────────────

    const setupFilters = (api) => {
        if ($.fn.select2) {
            $('#filterStatus').select2({
                dropdownParent: $('#inlineFilterCollapse'),
                minimumResultsForSearch: -1,
                selectionCssClass: 'form-select form-select-sm',
                allowClear: true
            });
        }

        const defaultView = defaultViewState;
        if (defaultView) {
            applySavedTableState(api, defaultView, { fallbackOrder: baseOrder });
        } else {
            appliedFilters = { status: '' };
            syncFilterControls(appliedFilters);
        }

        window.DtDefaults.updateVisualState(api, getAppliedFilterCount(api));
        setSaveFilterVisible(false);

        const applyBtn = document.getElementById('btnFilterApply');
        const resetBtn = document.getElementById('btnFilterReset');

        if (applyBtn && !applyBtn.dataset.bound) {
            applyBtn.dataset.bound = '1';
            applyBtn.addEventListener('click', () => {
                const status = document.getElementById('filterStatus')?.value || '';
                appliedFilters = { status };
                applyFilterValues(api, appliedFilters);
                api.draw();
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount(api));
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
                const el = document.getElementById(filterCollapseId);
                if (el) bootstrap.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
            });
        }

        if (resetBtn && !resetBtn.dataset.bound) {
            resetBtn.dataset.bound = '1';
            resetBtn.addEventListener('click', (e) => {
                e.preventDefault();
                const def = defaultViewState;
                const hasSavedDefault = !!def;
                const isDirty = hasSavedDefault ? isDirtyComparedToDefault(api) : false;

                if (hasSavedDefault && isDirty) {
                    applySavedTableState(api, def, { fallbackOrder: baseOrder, resetColumnOrder: !def?.columnOrder });
                } else {
                    applySavedTableState(api, { status: '', search: '' }, {
                        fallbackOrder: baseOrder,
                        clearSearch: true,
                        resetColumns: true,
                        resetColumnOrder: true
                    });
                }

                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            });
        }
    };

    // ─── Selection & Events ──────────────────────────────────────────────────────

    const getSelectedIds = () => {
        const ids = [];
        dtTableEl.querySelectorAll('.dt-checkboxes:checked').forEach(cb => ids.push(cb.value));
        return ids;
    };

    const updateBulkBar = () => {
        const ids = getSelectedIds();
        const bar = document.getElementById('bulkActionBar');
        const countEl = document.getElementById('bulkSelectedCount');
        if (!bar || !countEl) return;
        if (ids.length > 0) { bar.classList.remove('d-none'); countEl.textContent = ids.length; }
        else { bar.classList.add('d-none'); countEl.textContent = '0'; }
        const headerCb = dtTableEl?.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) {
            const total = dtTableEl.querySelectorAll('tbody .dt-checkboxes').length;
            headerCb.checked = ids.length > 0 && ids.length === total;
            headerCb.indeterminate = ids.length > 0 && ids.length < total;
        }
    };

    const clearSelection = () => {
        dtTableEl?.querySelectorAll('.dt-checkboxes:checked').forEach(cb => {
            cb.checked = false;
            cb.closest('tr')?.classList.remove('selected');
        });
        const headerCb = dtTableEl?.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) { headerCb.checked = false; headerCb.indeterminate = false; }
        updateBulkBar();
    };

    const reloadTableAndToastSuccess = (messageKey) => {
        clearSelection();
        dt.ajax.reload(() => {
            window.showToast?.(L[messageKey] || messageKey, 'success');
        }, false);
    };

    const handleEvents = () => {
        if (!dtTableEl) return;
        dtTableEl.addEventListener('click', (e) => {
            const deleteBtn = e.target.closest('.delete-record');
            if (deleteBtn) {
                let tr = deleteBtn.closest('tr');
                if (tr.classList.contains('child')) tr = tr.previousElementSibling;
                const data = dt.row(tr).data();
                if (window.showConfirm) {
                    window.showConfirm(data.name, () => {
                        fetch(`${apiUrl}/api/countries/${data.id}`, {
                            method: 'DELETE',
                            headers: getAuthHeaders()
                        }).then(res => {
                            if (res.ok) reloadTableAndToastSuccess('RecordDeleted');
                            else window.showToast?.(L.ErrorOccurred, 'error');
                        }).catch(() => window.showToast?.(L.ErrorOccurred, 'error'));
                    }, data.name);
                }
            }
            const quickViewBtn = e.target.closest('.js-quick-view');
            if (quickViewBtn) populateOffcanvas(tryParseRowJson(quickViewBtn));
        });

        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            const tr = $(this).closest('tr');
            if (this.checked) tr.addClass('selected'); else tr.removeClass('selected');
            updateBulkBar();
        });

        $(dtTableEl).on('change', '.dt-checkboxes-select-all', function () {
            const isChecked = this.checked;
            dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach(cb => {
                cb.checked = isChecked;
                const tr = cb.closest('tr');
                if (isChecked) tr?.classList.add('selected'); else tr?.classList.remove('selected');
            });
            updateBulkBar();
        });

        document.getElementById('btnClearSelection')?.addEventListener('click', () => clearSelection());

        document.getElementById('btnBulkDelete')?.addEventListener('click', () => {
            const ids = getSelectedIds();
            if (!ids.length) return;
            const msg = (L.BulkDeleteConfirm || '').replace('{0}', ids.length);
            if (window.showConfirm) {
                window.showConfirm(msg, () => {
                    fetch(`${apiUrl}/api/countries/bulk`, {
                        method: 'DELETE',
                        headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                        body: JSON.stringify({ ids })
                    }).then(res => {
                        if (res.ok) return res.json();
                        throw new Error('Bulk delete failed');
                    }).then(() => {
                        reloadTableAndToastSuccess('BulkDeleteSuccess');
                    }).catch(() => window.showToast?.(L.ErrorOccurred, 'error'));
                }, `${ids.length} records`);
            }
        });
    };

    return { init: () => { initDataTable(); handleEvents(); } };
})();

document.addEventListener('DOMContentLoaded', () => CountriesList.init());
