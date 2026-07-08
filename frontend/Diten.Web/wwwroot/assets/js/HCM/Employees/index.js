/**
 * MOD-0251 Employee Registry DataTables Page Script
 * Diten ERP vNext - HCM/Employees
 */
'use strict';

window.HcmEmployeeRegistry = (function () {
    let dt;
    let L = window.L10n || {};
    let appliedFilters = { employeeStatus: '', workerType: '', employmentType: '', legalEntityId: '' };
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const page = document.getElementById('hcm-employee-registry-page');
    const dtTableEl = document.querySelector('.datatables-employees');
    const endpoint = '/HCM/Employees/api';
    const canSearch = page?.getAttribute('data-can-search') === 'true';
    const canView = page?.getAttribute('data-can-view') === 'true';
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'HCM', pageKey: 'Employees' };
    const totalColumnCount = 12;
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9, 10];
    const baseOrder = [[10, 'desc']];
    const bulkOptions = {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
        }
    };

    const getFilterValues = () => ({
        employeeStatus: document.getElementById('filterEmployeeStatus')?.value || '',
        workerType: document.getElementById('filterWorkerType')?.value || '',
        employmentType: document.getElementById('filterEmploymentType')?.value || '',
        legalEntityId: document.getElementById('filterLegalEntityId')?.value?.trim() || ''
    });

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter(Boolean).length;
    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || { 'X-Requested-With': 'XMLHttpRequest' };

    const emptyFilters = () => ({ employeeStatus: '', workerType: '', employmentType: '', legalEntityId: '' });
    const defaultColVis = () => Array.from({ length: totalColumnCount }, (_, index) => index < totalColumnCount);
    const defaultColumnOrder = () => Array.from({ length: totalColumnCount }, (_, index) => index);
    const normalizeString = (value) => (value == null ? '' : String(value).trim());
    const normalizeFilters = (filters) => Object.assign(emptyFilters(), filters || {});
    const normalizeColVis = (value) => Array.isArray(value) && value.length === totalColumnCount
        ? value.map(Boolean)
        : defaultColVis();
    const normalizeColumnOrder = (value) => Array.isArray(value) && value.length === totalColumnCount
        ? value.map(Number)
        : defaultColumnOrder();
    const normalizeOrder = (value) => Array.isArray(value) && value.length ? value : baseOrder;
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || view),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis),
        columnOrder: normalizeColumnOrder(view?.columnOrder),
        order: normalizeOrder(view?.order)
    });
    const serializeView = (view) => JSON.stringify(normalizeViewState(view));
    const getSavedViewId = (record) => record?.id || record?.Id || record?._id || null;
    const getSavedViewName = (record) => record?.viewName || record?.ViewName || '';
    const isSavedViewDefault = (record) => record?.isDefault === true || record?.IsDefault === true;
    const unwrapViewResponse = (response) => response?.data || response?.Data || response;
    const getSavedViewDefinition = (record) => {
        const raw = record?.viewDefinition ?? record?.ViewDefinition ?? {};
        if (typeof raw === 'string') {
            try { return JSON.parse(raw); } catch (error) { return {}; }
        }
        return raw || {};
    };
    const mapSavedViewToState = (record) => normalizeViewState(getSavedViewDefinition(record));
    const getResetBaselineState = () => normalizeViewState({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
        order: baseOrder
    });
    const captureColVis = (api) => saveViewColumnIndexes.map(index => {
        try { return api.column(index).visible(); } catch (error) { return true; }
    }).reduce((result, visible, offset) => {
        result[saveViewColumnIndexes[offset]] = visible;
        return result;
    }, defaultColVis());
    const captureColumnOrder = (api) => {
        try {
            return api.colReorder?.order?.() || defaultColumnOrder();
        } catch (error) {
            return defaultColumnOrder();
        }
    };
    const getCurrentView = (api) => ({
        filters: appliedFilters,
        search: normalizeString(api?.search?.()),
        colVis: captureColVis(api),
        columnOrder: captureColumnOrder(api),
        order: api?.order?.() || baseOrder
    });
    const setSaveFilterVisible = (visible) => {
        const button = document.querySelector('.dt-save-filter-btn');
        if (!button) return;
        button.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || getResetBaselineState();
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
            console.error('[EmployeeMaster SaveView] Failed to load saved views.', error);
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
    const syncFilterControls = (filters) => {
        const values = normalizeFilters(filters);
        document.getElementById('filterEmployeeStatus').value = values.employeeStatus;
        document.getElementById('filterWorkerType').value = values.workerType;
        document.getElementById('filterEmploymentType').value = values.employmentType;
        document.getElementById('filterLegalEntityId').value = values.legalEntityId;
        if (window.jQuery?.fn?.select2) {
            $('#inlineFilterHost select.select2').trigger('change');
        }
    };
    const applySavedTableState = (api, view) => {
        if (!api || !view) return;
        const state = normalizeViewState(view);
        appliedFilters = state.filters;
        syncFilterControls(appliedFilters);
        try { api.colReorder?.order?.(state.columnOrder, true); } catch (error) { }
        state.colVis.forEach((visible, index) => {
            try { api.column(index).visible(visible, false); } catch (error) { }
        });
        api.search(state.search);
        api.order(state.order);
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };

    const setError = (message) => {
        const error = document.getElementById('hcm-registry-error');
        if (!error) return;
        error.textContent = message || '';
        error.classList.toggle('d-none', !message);
    };

    const classifyStatus = (status) => {
        if (status === 401) return L.ForbiddenState || 'Authentication required.';
        if (status === 403) return L.ForbiddenState || 'Permission denied.';
        if (status >= 500) return L.DependencyError || 'Dependency unavailable.';
        return L.ErrorOccurred || 'Request failed.';
    };

    const mapSortColumn = (columnName) => {
        const map = {
            employeeNumber: 'employeeNumber',
            displayName: 'displayName',
            workerType: 'workerType',
            employmentType: 'employmentType',
            employeeStatus: 'employeeStatus',
            sensitivityLevel: 'sensitivityLevel',
            legalEntityDisplayName: 'legalEntity',
            hireDate: 'hireDate',
            updatedAt: 'updatedAt'
        };
        return map[columnName] || 'updatedAt';
    };

    const buildQueryParams = (data) => {
        const pageSize = Number(data.length) > 0 ? Number(data.length) : 20;
        const pageNumber = Number(data.start) >= 0 ? Math.floor(Number(data.start) / pageSize) + 1 : 1;
        const order = Array.isArray(data.order) ? data.order[0] : null;
        const orderedColumn = order ? data.columns?.[order.column] : null;
        const searchTerm = data.search?.value || '';

        return {
            search: searchTerm,
            employeeStatus: appliedFilters.employeeStatus,
            workerType: appliedFilters.workerType,
            employmentType: appliedFilters.employmentType,
            legalEntityId: appliedFilters.legalEntityId,
            page: pageNumber,
            pageSize,
            sortBy: mapSortColumn(orderedColumn?.name),
            sortDirection: order?.dir || 'desc'
        };
    };

    const toQueryString = (parameters) => {
        const pairs = [];
        Object.entries(parameters).forEach(([key, value]) => {
            if (value === null || value === undefined || value === '') return;
            pairs.push(`${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`);
        });
        return pairs.length ? `?${pairs.join('&')}` : '';
    };

    const normalizeResponse = (json, draw) => {
        const envelope = json?.data || json?.Data || {};
        const items = envelope.items || envelope.Items || [];
        const total = envelope.totalCount ?? envelope.TotalCount ?? items.length;
        return {
            draw,
            recordsTotal: total,
            recordsFiltered: total,
            data: items
        };
    };

    const mountInlineFilter = () => {
        if (!dtTableEl) return;
        const host = document.getElementById(filterHostId);
        if (!host) return;

        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;

        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };

    const toggleInlineFilter = () => {
        const el = document.getElementById(filterCollapseId);
        const btn = document.querySelector('.dt-filter-btn');
        if (!el) return;

        if (window.bootstrap?.Collapse) {
            const collapse = window.bootstrap.Collapse.getOrCreateInstance(el, { toggle: false });
            el.classList.contains('show') ? collapse.hide() : collapse.show();
        } else {
            el.classList.toggle('show');
        }

        btn?.setAttribute('aria-expanded', el.classList.contains('show') ? 'true' : 'false');
    };

    const setupFilters = (api) => {
        if (window.jQuery?.fn?.select2) {
            $('#inlineFilterHost select.select2').each(function () {
                const $select = $(this);
                if ($select.data('select2')) return;
                $select.select2({
                    dropdownParent: $(document.body),
                    dropdownCssClass: 'dt-inline-filter-dropdown',
                    minimumResultsForSearch: Infinity,
                    selectionCssClass: 'form-select form-select-sm',
                    width: 'element',
                    allowClear: true
                });
            });
        }

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = getFilterValues();
            api?.ajax?.reload();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            applySavedTableState(api, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });
    };

    const renderStatusBadge = (value) => {
        const key = String(value || '').toLowerCase();
        const map = {
            active: { title: L.StatusActive || 'Active', class: 'bg-label-success' },
            draft: { title: L.StatusDraft || 'Draft', class: 'bg-label-warning' },
            inactive: { title: L.StatusInactive || 'Inactive', class: 'bg-label-secondary' }
        };
        const status = map[key] || { title: value || L.Unknown || '', class: 'bg-label-primary' };
        return `<span class="badge ${status.class}">${status.title}</span>`;
    };

    const renderActions = (row) => {
        const actions = row?.actions || row?.Actions || {};
        if (!canView || actions.canView !== true) {
            return `<span class="text-muted small">${L.NoViewPermission || ''}</span>`;
        }

        return window.DitenDataTable?.renderActions?.([{
            key: 'view',
            icon: 'bx bx-show',
            className: 'text-primary',
            attrs: {
                href: `/HCM/Employees/${row.employeeId || row.EmployeeId}`,
                title: L.ViewDetails || ''
            }
        }]) || '';
    };

    const initDataTable = async () => {
        if (!dtTableEl || !window.DtDefaults) return;
        if (!canSearch) {
            document.querySelector('.card-datatable')?.classList.add('d-none');
            return;
        }

        syncL10n();
        await loadDefaultView();
        const extraButtons = {
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
                action: async function (event, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[EmployeeMaster SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        const config = window.DtDefaults.create({
            stateSave: false,
            processing: true,
            serverSide: true,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: async function (data, callback) {
                setError('');
                try {
                    const response = await fetch(`${endpoint}${toQueryString(buildQueryParams(data))}`, {
                        method: 'GET',
                        headers: getAuthHeaders()
                    });
                    const text = await response.text();
                    const json = text ? JSON.parse(text) : {};
                    if (!response.ok) {
                        const message = classifyStatus(response.status);
                        setError(message);
                        callback({ draw: data.draw, recordsTotal: 0, recordsFiltered: 0, data: [] });
                        return;
                    }

                    callback(normalizeResponse(json, data.draw));
                } catch (error) {
                    console.error('[EmployeeMaster] Registry search failed.', error);
                    setError(L.DependencyError || L.ErrorOccurred || '');
                    callback({ draw: data.draw, recordsTotal: 0, recordsFiltered: 0, data: [] });
                }
            },
            columns: [
                { data: 'employeeId', name: 'control' },
                { data: 'employeeId', name: 'checkbox' },
                { data: 'employeeNumber', name: 'employeeNumber' },
                { data: 'displayName', name: 'displayName' },
                { data: 'workerType', name: 'workerType' },
                { data: 'employmentType', name: 'employmentType' },
                { data: 'employeeStatus', name: 'employeeStatus' },
                { data: 'sensitivityLevel', name: 'sensitivityLevel' },
                { data: 'legalEntityDisplayName', name: 'legalEntityDisplayName' },
                { data: 'hireDate', name: 'hireDate' },
                { data: 'updatedAt', name: 'updatedAt' },
                { data: 'employeeId', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                { targets: 2, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                { targets: 6, render: (data, type) => type === 'display' ? renderStatusBadge(data) : data },
                { targets: 9, render: (data) => data || '' },
                { targets: 10, render: (data) => data ? new Date(data).toLocaleString() : '' },
                {
                    targets: -1,
                    title: L.Actions,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all',
                    render: (data, type, full) => renderActions(full)
                }
            ],
            buttons: [
                extraButtons.filterBtn,
                extraButtons.saveFilterBtn
            ],
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                setupFilters(api);
                applySavedTableState(api, defaultViewState || { filters: appliedFilters });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                const api = this.api();
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            },
            language: {
                emptyTable: getAppliedFilterCount() > 0 ? L.FilteredEmptyState : L.EmptyState,
                zeroRecords: getAppliedFilterCount() > 0 ? L.FilteredEmptyState : L.EmptyState,
                processing: L.Loading
            }
        });

        dt = new DataTable(dtTableEl, config);
        window.DitenDataTable?.bindBulkSelection?.(dtTableEl, dt, bulkOptions);
        window.DitenDataTable?.bindActionDispatcher?.({
            tableEl: dtTableEl,
            dt,
            onRowAction: {
                view: ({ row }) => {
                    if (!row?.employeeId) return;
                    window.location.href = `/HCM/Employees/${row.employeeId}`;
                }
            }
        });
    };

    document.addEventListener('DOMContentLoaded', initDataTable);

    return {
        _test: {
            buildQueryParams,
            classifyStatus,
            normalizeResponse,
            getFilterValues,
            getAuthHeaders,
            getResetBaselineState,
            mapSortColumn,
            normalizeViewState,
            toQueryString
        }
    };
})();
