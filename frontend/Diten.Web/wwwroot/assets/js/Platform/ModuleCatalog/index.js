'use strict';

const ModuleCatalogList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let L = window.L10n || {};
    const dtTableEl = document.querySelector('.datatables-module-catalog');
    const endpoint = '/Platform/ModuleCatalog/api';
    const lookupEndpoint = '/Platform/ModuleCatalog/api/lookups/module-catalog';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'ModuleCatalog' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    const totalColumnCount = 12;
    const baseOrder = [[1, 'asc']];
    let saveFilterArmed = false;
    let appliedFilters = { domain: [], service: [], status: [], isTenantAssignable: '', isCoreModule: '' };

    const syncL10n = () => { L = window.L10n || {}; };
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');
    const normalizeArray = (value) => {
        const raw = Array.isArray(value) ? value : (typeof value === 'string' ? value.split(',') : []);
        return [...new Set(raw.map(normalizeString).filter(Boolean))].sort((a, b) => a.localeCompare(b));
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ domain: [], service: [], status: [], isTenantAssignable: '', isCoreModule: '' });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            domain: normalizeArray(source.domain),
            service: normalizeArray(source.service),
            status: normalizeArray(source.status),
            isTenantAssignable: normalizeString(source.isTenantAssignable),
            isCoreModule: normalizeString(source.isCoreModule)
        };
    };
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const normalized = order.map(Number).filter((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount);
        return normalized.length === totalColumnCount && new Set(normalized).size === totalColumnCount ? normalized : null;
    };
    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const normalized = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((columnIndex, position) => {
                if (typeof colVis[columnIndex] === 'boolean') normalized[columnIndex] = colVis[columnIndex];
                else if (typeof colVis[position] === 'boolean') normalized[columnIndex] = colVis[position];
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((columnIndex) => {
                if (typeof colVis[columnIndex] === 'boolean') normalized[columnIndex] = colVis[columnIndex];
            });
        }
        return Object.keys(normalized).length ? normalized : null;
    };
    const defaultColVis = () => {
        const result = {};
        saveViewColumnIndexes.forEach((columnIndex) => {
            const visibleIndexes = [1, 2, 3, 4, 5, 6];
            result[columnIndex] = visibleIndexes.includes(columnIndex);
        });
        return result;
    };
    const captureColVis = (api) => {
        const result = {};
        saveViewColumnIndexes.forEach((columnIndex) => {
            try { result[columnIndex] = !!api.column(columnIndex).visible(); } catch (error) { }
        });
        return result;
    };
    const captureColOrder = (api) => {
        try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (error) { return null; }
    };
    const applyColVis = (api, colVis) => {
        const normalized = normalizeColVis(colVis);
        if (!normalized) return;
        saveViewColumnIndexes.forEach((columnIndex) => {
            if (typeof normalized[columnIndex] === 'boolean') {
                try { api.column(columnIndex).visible(normalized[columnIndex], false); } catch (error) { }
            }
        });
    };
    const applyColOrder = (api, order) => {
        const normalized = normalizeColOrder(order);
        if (normalized && typeof api?.colReorder?.order === 'function') api.colReorder.order(normalized, true);
    };
    const getSearchVal = (api) => {
        try { return api.table().container().querySelector('.dt-search input')?.value || ''; } catch (error) { return ''; }
    };
    const syncSearchInput = (api, value) => {
        try {
            const input = api.table().container().querySelector('.dt-search input');
            if (input) input.value = value || '';
        } catch (error) { }
    };
    const getCurrentView = (api) => ({
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(api) || api.search()),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });
    const serializeView = (view) => JSON.stringify({
        filters: Object.keys(view?.filters || {}).sort().reduce((acc, key) => {
            acc[key] = normalizeFilterValue(view.filters[key]);
            return acc;
        }, {}),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getSavedViewId = (savedView) => savedView?.id || savedView?.Id || savedView?._id || null;
    const getSavedViewName = (savedView) => savedView?.viewName || savedView?.ViewName || '';
    const isSavedViewDefault = (savedView) => savedView?.isDefault === true || savedView?.IsDefault === true;
    const unwrapViewResponse = (response) => response?.data || response?.Data || response;
    const getSavedViewDef = (savedView) => {
        const raw = savedView?.viewDefinition ?? savedView?.ViewDefinition ?? {};
        if (typeof raw === 'string') {
            try { return JSON.parse(raw); } catch (error) { return {}; }
        }
        return raw || {};
    };
    const mapSavedViewToState = (savedView) => {
        const definition = getSavedViewDef(savedView);
        return {
            filters: normalizeFilters(definition.filters),
            search: normalizeString(definition.search),
            colVis: normalizeColVis(definition.colVis),
            columnOrder: normalizeColOrder(definition.columnOrder),
            order: Array.isArray(definition.order) ? definition.order : null
        };
    };
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            filters: emptyFilters(),
            search: '',
            colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
            order: baseOrder
        };
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };
    const getResetBaselineState = () => normalizeViewState({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
        order: baseOrder
    });
    const applySavedTableState = (api, state) => {
        if (!api || !state) return;
        const normalizedState = normalizeViewState(state);
        appliedFilters = normalizedState.filters;
        syncFilterControls(appliedFilters);
        applyColOrder(api, normalizedState.columnOrder);
        applyColVis(api, normalizedState.colVis);
        api.search(normalizedState.search);
        syncSearchInput(api, normalizedState.search);
        api.order(normalizedState.order);
        try { api.columns.adjust(); } catch (error) { }
        try { api.responsive?.recalc?.(); } catch (error) { }
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
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
            console.error('[ModuleCatalog SaveView] Failed to load saved views.', error);
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
        if (savedRecord && typeof savedRecord === 'object') {
            defaultViewRecord = savedRecord;
        } else {
            defaultViewRecord = Object.assign({}, defaultViewRecord || {}, payload);
        }
        defaultViewState = normalizedView;
        return defaultViewState;
    };

    const statusBadge = (status) => {
        const map = {
            Draft: ['bg-label-secondary', L.StatusDraft],
            Active: ['bg-label-success', L.StatusActive],
            Inactive: ['bg-label-warning', L.StatusInactive],
            Deprecated: ['bg-label-danger', L.StatusDeprecated]
        };
        const item = map[status] || ['bg-label-secondary', status || '-'];
        return `<span class="badge ${item[0]}">${escapeHtml(item[1] || status)}</span>`;
    };

    const originBadge = (origin) => {
        if (origin === 'SelfRegistered') {
            return `<span class="badge bg-label-info">${escapeHtml(L.OriginAuto || 'Auto')}</span>`;
        }
        if (origin === 'Manual') {
            return `<span class="badge bg-label-secondary">${escapeHtml(L.OriginManual || 'Manual')}</span>`;
        }
        return '';
    };

    const boolBadge = (value, isCore = false) => {
        if (isCore && value) {
            return `<i class="bx bx-lock-alt text-warning fs-4" title="${escapeHtml(L.Core || '')}"></i>`;
        }
        return value
            ? `<i class="bx bx-check text-success fs-4"></i>`
            : `<i class="bx bx-x text-danger fs-4"></i>`;
    };

    // FEAT-CATALOG-BASELINE-BADGE — baseline (entitlement-free) marker; empty for regular modules.
    const isBaselineRow = (row) => !!(row && (row.isBaseline || row.IsBaseline));
    const baselineBadge = (row) => isBaselineRow(row)
        ? ` <span class="badge bg-label-info ms-1" title="${escapeHtml(L.BaselineHint || '')}">${escapeHtml(L.Baseline || 'Baseline')}</span>`
        : '';

    const domainBadge = (domain) => {
        const value = normalizeString(domain);
        return value
            ? escapeHtml(value)
            : `<span class="text-muted fst-italic">${escapeHtml(L.Unknown || '')}</span>`;
    };

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue);
        loadStats();
    };

    const updateKpis = (stats) => {
        if (!stats) return;
        const totalEl = document.getElementById('kpi-total');
        const activeEl = document.getElementById('kpi-active');
        const betaEl = document.getElementById('kpi-beta');
        const previewEl = document.getElementById('kpi-preview');

        if (totalEl) totalEl.innerText = String(stats.total ?? 0);
        if (activeEl) activeEl.innerText = String(stats.active ?? 0);
        if (betaEl) betaEl.innerText = String(stats.beta ?? 0);
        if (previewEl) previewEl.innerText = String(stats.preview ?? 0);
    };

    const loadStats = async () => {
        try {
            const response = await fetch(`${endpoint}/stats`, { headers: getAuthHeaders() });
            if (!response.ok) return;
            const payload = await response.json();
            updateKpis(payload.data || payload.Data);
        } catch (error) {
            console.warn('[ModuleCatalog KPIs] Failed to load stats.', error);
        }
    };

    const rowActionHandlers = {
        quickView: ({ id }) => {
            if (id) window.location.href = `/Platform/ModuleCatalog/Details/${encodeURIComponent(id)}`;
        },
        edit: ({ id }) => {
            if (id) window.location.href = `/Platform/ModuleCatalog/Edit/${encodeURIComponent(id)}`;
        },
        activate: async ({ id, row }) => {
            try {
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/activate`, { method: 'POST', headers: getAuthHeaders() });
                if (!response.ok) throw new Error('Activation failed.');
                reloadWithSuccessToast('RecordUpdated', row?.displayName || row?.moduleCode || '');
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        },
        deactivate: async ({ id, row }) => {
            try {
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/deactivate`, { method: 'POST', headers: getAuthHeaders() });
                if (!response.ok) throw new Error('Deactivation failed.');
                reloadWithSuccessToast('RecordUpdated', row?.displayName || row?.moduleCode || '');
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        },
        delete: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (!rowId) return;
            const entityName = row?.displayName || row?.moduleCode || row?.ModuleCode || '';
            const isCore = row?.isCoreModule || row?.IsCoreModule;
            if (isCore) {
                window.showToast?.(L.CoreModuleCannotBeDeleted || '', 'warning');
                return;
            }
            if ((row?.origin || row?.Origin) === 'SelfRegistered') {
                window.showToast?.(L.SelfRegisteredDeleteHint || '', 'warning');
                return;
            }
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const response = await fetch(`${endpoint}/${encodeURIComponent(rowId)}`, { method: 'DELETE', headers: getAuthHeaders() });
                    if (!response.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted', entityName);
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }, { entityName, type: 'danger', confirmButtonText: L.Delete });
        }
    };

    const buildQuery = (data) => {
        const params = new URLSearchParams();
        params.set('page', String(Math.floor((data.start || 0) / (data.length || 20)) + 1));
        params.set('pageSize', String(data.length || 20));
        const order = Array.isArray(data.order) ? data.order[0] : null;
        const orderColumn = order ? data.columns?.[order.column] : null;
        const sortName = orderColumn?.name || orderColumn?.data || 'sortOrder';
        params.set('sort', `${sortName}:${order?.dir || 'asc'}`);
        const search = data.search?.value || '';
        if (search) params.set('search', search);
        Object.entries(appliedFilters).forEach(([key, value]) => {
            if (Array.isArray(value)) {
                if (value.length) params.set(key, value.join(','));
            } else if (value !== '') {
                params.set(key, value);
            }
        });
        return params.toString();
    };

    const initDataTable = async () => {
        if (!dtTableEl || !window.DtDefaults) {
            console.error('[ModuleCatalog] DataTable element or DtDefaults not found.');
            return;
        }
        
        const savedState = await loadDefaultView();
        if (savedState?.filters) {
            appliedFilters = normalizeFilters(Object.assign({}, appliedFilters, savedState.filters));
        }

        const addNewAttr = { href: '/Platform/ModuleCatalog/Create' };
        const saveFilterBtn = {
            text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L.SaveView || '')}</span>`,
            className: 'btn btn-label-primary dt-save-filter-btn d-none',
            attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
            action: async function (event, api) {
                const tableApi = api || dt;
                if (!tableApi) return;
                try {
                    await saveDefaultView(getCurrentView(tableApi));
                    setSaveFilterVisible(false);
                    window.showToast?.(L.RecordSaved || L.SaveView || '', 'success');
                } catch (error) {
                    if (error?.authHandled) return;
                    console.error('[ModuleCatalog SaveView] Failed to save default view.', error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }
        };
        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
            action: () => toggleInlineFilter()
        };

        const dtConfig = window.DtDefaults.create({
            processing: true,
            serverSide: true,
            stateSave: false,
            order: savedState?.order || baseOrder,
            search: { search: savedState?.search || '' },
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: function (data, callback) {
                fetch(`${endpoint}?${buildQuery(data)}`, { headers: getAuthHeaders() })
                    .then((response) => response.ok ? response.json() : Promise.reject(response))
                    .then((payload) => {
                        const result = payload.data || payload.Data || {};
                        callback({
                            data: result.items || result.Items || [],
                            recordsTotal: result.totalCount || result.TotalCount || 0,
                            recordsFiltered: result.totalCount || result.TotalCount || 0
                        });
                    })
                    .catch(() => {
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                        callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'moduleCode', name: 'moduleCode', render: (data) => `<span class="fw-medium font-monospace text-primary">${escapeHtml(data)}</span>` },
                { data: 'moduleName', name: 'moduleName', render: (data, type, row) => type === 'display' ? `${escapeHtml(data)}${baselineBadge(row)}` : data },
                {
                    data: 'status',
                    name: 'status',
                    render: (data, type, row) => {
                        if (type !== 'display') return data;
                        const origin = originBadge(row.origin || row.Origin);
                        return origin
                            ? `<span class="d-inline-flex align-items-center gap-1">${statusBadge(data)}${origin}</span>`
                            : statusBadge(data);
                    }
                },
                { data: 'domain', name: 'domain', render: domainBadge },
                { data: 'isTenantAssignable', name: 'isTenantAssignable', render: (data, type, row) => isBaselineRow(row) ? `<span class="text-muted" title="${escapeHtml(L.BaselineAutoHint || '')}">${escapeHtml(L.BaselineAuto || 'Baseline (auto)')}</span>` : boolBadge(data) },
                { data: 'isCoreModule', name: 'isCoreModule', render: (data) => boolBadge(data, true) },
                { data: 'displayName', name: 'displayName', visible: false, render: escapeHtml },
                { data: 'service', name: 'service', visible: false, render: escapeHtml },
                { data: 'moduleVersion', name: 'moduleVersion', visible: false, render: escapeHtml },
                { data: 'sortOrder', name: 'sortOrder', visible: false, render: (data) => escapeHtml(data ?? 0) },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (data, type, row) => {
                        const id = row.id || row.Id;
                        const status = row.status || row.Status;
                        const isCore = row.isCoreModule || row.IsCoreModule;
                        const isSelfRegistered = (row.origin || row.Origin) === 'SelfRegistered';
                        const rowJson = JSON.stringify(row);
                        
                        const actions = [
                            {
                                key: 'quickView',
                                className: 'js-quick-view',
                                text: L.Details || '',
                                icon: 'bx bx-show',
                                attrs: { 'data-id': id, 'data-json': rowJson }
                            },
                            {
                                key: 'edit',
                                className: 'js-edit-item',
                                text: L.Edit || '',
                                attrs: { 'data-id': id, 'data-json': rowJson }
                            }
                        ];

                        if (status === 'Draft' || status === 'Inactive') {
                            actions.push({
                                key: 'activate',
                                className: 'text-success',
                                text: L.Activate || '',
                                attrs: { 'data-id': id, 'data-json': rowJson }
                            });
                        } else if (status === 'Active' && !isBaselineRow(row)) {
                            // FIX-BASELINE-NO-DEACTIVATE — baseline modules reach every tenant automatically; hide
                            // Deactivate (the backend also refuses it with 409). Delete is already disabled (self-reg).
                            actions.push({
                                key: 'deactivate',
                                className: 'text-warning',
                                text: L.Deactivate || '',
                                attrs: { 'data-id': id, 'data-json': rowJson }
                            });
                        }

                        const deleteDisabled = isCore || isSelfRegistered;
                        const deleteTitle = isSelfRegistered
                            ? (L.SelfRegisteredDeleteHint || 'Self-registered; manage via the module manifest')
                            : (isCore ? (L.CoreModuleCannotBeDeleted || '') : '');
                        actions.push({
                            key: 'delete',
                            className: deleteDisabled ? 'text-muted' : 'text-danger',
                            text: L.Delete || '',
                            attrs: {
                                'data-id': id,
                                'data-json': rowJson,
                                'title': deleteTitle,
                                'style': deleteDisabled ? 'pointer-events: none; opacity: 0.5;' : ''
                            }
                        });

                        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
                    }
                }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 2, responsivePriority: 1 },
                { targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNewModuleCatalog || '', addNewAttr, { filterBtn, saveFilterBtn }, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                loadLookupOptions()
                    .then(() => {
                        initSelect2Filters();
                        applySavedTableState(api, savedState || { filters: appliedFilters });
                        window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
                    })
                    .catch((error) => {
                        console.error('[ModuleCatalog Filters] Failed to initialize lookup filters.', error);
                        initSelect2Filters();
                        applySavedTableState(api, savedState || { filters: appliedFilters });
                        window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
                    });
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    window.location.href = '/Platform/ModuleCatalog/Create';
                });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                loadStats();
            }
        });

        if (window.L10n?.Showing) {
            dtConfig.language = dtConfig.language || {};
            dtConfig.language.info = `${window.L10n.Showing} _START_ - _END_ / _TOTAL_`;
        }

        dt = new DataTable(dtTableEl, dtConfig);

        $(dtTableEl).on('column-reorder.dt columns-reordered.dt search.dt order.dt column-visibility.dt', () => {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        window.DitenDataTable?.bindActionDispatcher?.({
            tableEl: dtTableEl,
            dt,
            onRowAction: rowActionHandlers
        });
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;

        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };

    const unwrapLookupRows = (payload) => {
        if (Array.isArray(payload)) return payload;
        if (Array.isArray(payload?.data)) return payload.data;
        if (Array.isArray(payload?.Data)) return payload.Data;
        return [];
    };

    const normalizeLookupOption = (item) => {
        if (typeof item === 'string') {
            return { value: normalizeString(item), text: normalizeString(item) };
        }
        const value = item?.value ?? item?.Value ?? item?.code ?? item?.Code ?? item?.id ?? item?.Id ?? '';
        const text = item?.name ?? item?.Name ?? item?.text ?? item?.Text ?? value;
        return {
            value: normalizeString(String(value || '')),
            text: normalizeString(String(text || value || ''))
        };
    };

    const appendLookupOptions = (select, options) => {
        if (!select) return;
        select.innerHTML = '';
        const normalized = Array.isArray(options)
            ? options.map(normalizeLookupOption).filter((option) => option.value)
            : [];
        normalized
            .sort((a, b) => a.text.localeCompare(b.text))
            .forEach((item) => {
                const option = document.createElement('option');
                option.value = item.value;
                option.textContent = item.text || item.value;
                select.appendChild(option);
            });
    };

    const fetchLookupOptions = async (lookupName) => {
        const response = await fetch(`${lookupEndpoint}/${encodeURIComponent(lookupName)}`, { headers: getAuthHeaders() });
        if (!response.ok) return [];
        const payload = await response.json();
        return unwrapLookupRows(payload);
    };

    const loadLookupOptions = async () => {
        const domainSelect = document.getElementById('filterDomain');
        const serviceSelect = document.getElementById('filterService');
        if (!domainSelect || !serviceSelect) return;
        const [domains, services] = await Promise.all([
            fetchLookupOptions('domains'),
            fetchLookupOptions('services')
        ]);
        appendLookupOptions(domainSelect, domains);
        appendLookupOptions(serviceSelect, services);
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
        if (!$summary.length) $summary = $('<span class="dt-inline-filter-multi__summary"></span>').prependTo($selection);
        if (!$actions.length) $actions = $('<span class="dt-inline-filter-multi__actions"></span>').appendTo($selection);
        if (!$count.length) $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>').appendTo($actions);
        if (!$arrow.length) $arrow = $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>').appendTo($selection);
        const placeholder = normalizeString($select.data('placeholder')) || '';
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || []).map((item) => normalizeString(item.text)).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + (L.Reset || '') + '" title="' + (L.Reset || '') + '">&times;</span>');
            $clearBtn.on('mousedown', (event) => {
                event.preventDefault();
                event.stopPropagation();
                $select.val(null).trigger('change');
            });
            $actions.append($clearBtn);
        }
    };

    const initSelect2Filters = () => {
        if (!window.jQuery?.fn?.select2) return;
        $('#inlineFilterHost select.select2').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            const isMultiple = $select.prop('multiple');
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: isMultiple ? 'dt-inline-filter-multi' : undefined,
                minimumResultsForSearch: Infinity,
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                placeholder: $select.data('placeholder') || '',
                closeOnSelect: !isMultiple,
                allowClear: !isMultiple
            });
            if (isMultiple) {
                $select.off('change.select2-summary').on('change.select2-summary', function () { syncMultiSelectSummary($select); });
                requestAnimationFrame(() => syncMultiSelectSummary($select));
            }
        });
    };

    const setSaveFilterVisible = (visible) => {
        const button = document.querySelector('.dt-save-filter-btn');
        if (!button) return;
        button.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const syncFilterControls = (filters) => {
        const values = normalizeFilters(filters);
        $('#filterDomain').val(values.domain).trigger('change');
        $('#filterService').val(values.service).trigger('change');
        $('#filterStatus').val(values.status).trigger('change');
        [
            ['filterAssignable', 'isTenantAssignable'],
            ['filterCore', 'isCoreModule']
        ].forEach(([id, key]) => {
            const element = document.getElementById(id);
            if (!element) return;
            element.value = values[key] || '';
            if (window.jQuery?.fn?.select2) $(element).val(values[key] || '').trigger('change');
        });
    };

    const bindFilters = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                domain: normalizeArray($('#filterDomain').val() || []),
                service: normalizeArray($('#filterService').val() || []),
                status: normalizeArray($('#filterStatus').val() || []),
                isTenantAssignable: document.getElementById('filterAssignable')?.value || '',
                isCoreModule: document.getElementById('filterCore')?.value || ''
            };
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed && dt) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            if (dt) applySavedTableState(dt, getResetBaselineState());
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed && dt) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter((value) => Array.isArray(value) ? value.length > 0 : value !== '').length;

    const bindActions = () => {
    };

    const init = async () => {
        syncL10n();
        bindFilters();
        bindActions();
        loadStats();
        await initDataTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ModuleCatalogList.init());
