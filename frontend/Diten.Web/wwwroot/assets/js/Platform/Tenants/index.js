/**
 * Tenant Core DataTables Page Script
 * Diten ERP vNext - Platform/Tenants
 */
'use strict';

const TenantsList = (function () {
    let dt;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let appliedFilters = { status: [], region: [], tenantType: [] };
    let L = window.L10n || {};

    const dtTableEl = document.querySelector('.datatables-tenants');
    const apiUrl = window.API?.platform || window.ApiBaseUrl || 'http://localhost:5000';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'Tenants' };
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9, 10];
    const totalColumnCount = 12;
    const baseOrder = [[10, 'desc']];
    const filterCollapseId = 'inlineFilterCollapse';

    const syncL10n = () => { L = window.L10n || {}; };
    const isAuthHandledError = (error) => error?.authHandled === true || error?.code === 'auth-refresh-in-progress';

    const getAuthHeaders = () => ({});

    const escapeHtml = (value) => {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    };

    const normalizeString = (value) => typeof value === 'string' ? value.trim() : '';
    const normalizeArray = (value) => Array.isArray(value) ? value.map(String).filter(Boolean).sort() : [];
    const unwrap = (payload) => payload?.data ?? payload ?? null;

    const formatDate = (value) => {
        if (!value) return '-';
        try { return new Date(value).toLocaleString(); } catch (error) { return value; }
    };

    const statusBadge = (status) => {
        const map = {
            Active: 'bg-label-success',
            Provisioning: 'bg-label-info',
            Suspended: 'bg-label-warning',
            Deactivated: 'bg-label-danger'
        };
        return `<span class="badge ${map[status] || 'bg-label-secondary'}">${escapeHtml(status || L.Unknown || '-')}</span>`;
    };

    const updateKpis = (stats) => {
        if (!stats) return;
        document.getElementById('kpi-total').innerText = String(stats.total || 0);
        document.getElementById('kpi-active').innerText = String(stats.active || 0);
        document.getElementById('kpi-provisioning').innerText = String(stats.provisioning || 0);
        document.getElementById('kpi-suspended').innerText = String(stats.suspended || 0);
    };

    const loadStats = async () => {
        try {
            const response = await fetch(`${apiUrl}/api/admin/tenants/stats`, {
                credentials: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) return;
            updateKpis(unwrap(await response.json()));
        } catch (error) {
            // KPI cards are secondary; table load errors are handled separately.
        }
    };

    const loadTenantDetail = async (id) => {
        const response = await fetch(`${apiUrl}/api/admin/tenants/${encodeURIComponent(id)}`, {
            credentials: 'include',
            headers: getAuthHeaders()
        });

        if (response.status === 401 || response.status === 403) {
            window.DtDefaults?.handleUnauthorized?.();
            const authError = new Error('auth-refresh-in-progress');
            authError.authHandled = true;
            throw authError;
        }

        if (!response.ok) {
            throw new Error(await response.text());
        }

        return unwrap(await response.json());
    };

    const changeLifecycle = async (tenantId, action, reason) => {
        const response = await fetch(`${apiUrl}/api/admin/tenants/${encodeURIComponent(tenantId)}/${action}`, {
            method: 'POST',
            credentials: 'include',
            headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify({ reason: reason || '' })
        });

        if (response.status === 401 || response.status === 403) {
            window.DtDefaults?.handleUnauthorized?.();
            const authError = new Error('auth-refresh-in-progress');
            authError.authHandled = true;
            throw authError;
        }

        if (!response.ok) {
            throw new Error(await response.text());
        }

        return unwrap(await response.json());
    };

    const getSelectedIds = () =>
        Array.from(dtTableEl.querySelectorAll('tbody .dt-checkboxes:checked')).map((checkbox) => checkbox.value);

    const clearSelection = () => {
        dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach((checkbox) => {
            checkbox.checked = false;
            checkbox.closest('tr')?.classList.remove('selected');
        });

        const header = dtTableEl.querySelector('.dt-checkboxes-select-all');
        if (header) {
            header.checked = false;
            header.indeterminate = false;
        }

        updateBulkBar();
    };

    const updateBulkBar = () => {
        const selectedIds = getSelectedIds();
        const bulkBar = document.getElementById('bulkActionBar');
        const countEl = document.getElementById('bulkSelectedCount');
        if (countEl) countEl.innerText = String(selectedIds.length);
        bulkBar?.classList.toggle('d-none', selectedIds.length === 0);

        const checkboxes = Array.from(dtTableEl.querySelectorAll('tbody .dt-checkboxes'));
        const header = dtTableEl.querySelector('.dt-checkboxes-select-all');
        if (header) {
            header.checked = checkboxes.length > 0 && selectedIds.length === checkboxes.length;
            header.indeterminate = selectedIds.length > 0 && selectedIds.length < checkboxes.length;
        }
    };

    const createDefaultColumnVisibility = () =>
        saveViewColumnIndexes.reduce((acc, index) => {
            acc[index] = true;
            return acc;
        }, {});

    const normalizeColumnVisibility = (colVis) => {
        if (!colVis) return null;
        const normalized = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((index, position) => {
                if (typeof colVis[index] === 'boolean') normalized[index] = colVis[index];
                else if (typeof colVis[position] === 'boolean') normalized[index] = colVis[position];
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((index) => {
                if (typeof colVis[index] === 'boolean') normalized[index] = colVis[index];
            });
        }

        return Object.keys(normalized).length ? normalized : null;
    };

    const captureColumnVisibility = (api) => {
        const colVis = {};
        saveViewColumnIndexes.forEach((index) => {
            try { colVis[index] = !!api.column(index).visible(); } catch (error) { }
        });
        return colVis;
    };

    const applyColumnVisibility = (api, colVis) => {
        const normalized = normalizeColumnVisibility(colVis);
        if (!normalized) return;
        saveViewColumnIndexes.forEach((index) => {
            if (typeof normalized[index] === 'boolean') {
                try { api.column(index).visible(normalized[index], false); } catch (error) { }
            }
        });
    };

    const normalizeColumnOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const normalized = order.map(Number).filter((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount);
        return normalized.length === totalColumnCount && new Set(normalized).size === totalColumnCount ? normalized : null;
    };

    const captureColumnOrder = (api) => {
        try { return normalizeColumnOrder(api?.colReorder?.order?.()); } catch (error) { return null; }
    };

    const applyColumnOrder = (api, order) => {
        const normalized = normalizeColumnOrder(order);
        if (!normalized || typeof api?.colReorder?.order !== 'function') return;
        try { api.colReorder.order(normalized, true); } catch (error) { }
    };

    const getSearchInputValue = (api) => {
        try { return normalizeString(api.table().container()?.querySelector('.dt-search input')?.value || ''); }
        catch (error) { return ''; }
    };

    const syncSearchInput = (api, value) => {
        try {
            const input = api.table().container()?.querySelector('.dt-search input');
            if (input) input.value = value || '';
        } catch (error) { }
    };

    const getSavedViewDefinition = (savedView) => {
        const raw = savedView?.viewDefinition ?? savedView?.ViewDefinition ?? savedView?.viewDefinitionJson ?? savedView?.ViewDefinitionJson ?? {};
        if (raw && typeof raw === 'object') return raw;
        if (typeof raw === 'string') {
            try { return JSON.parse(raw) || {}; } catch (error) { return {}; }
        }
        return {};
    };

    const mapSavedViewToState = (savedView) => {
        const definition = getSavedViewDefinition(savedView);
        return {
            status: normalizeArray(definition.status),
            region: normalizeArray(definition.region),
            tenantType: normalizeArray(definition.tenantType),
            search: normalizeString(definition.search),
            colVis: normalizeColumnVisibility(definition.colVis),
            columnOrder: normalizeColumnOrder(definition.columnOrder),
            order: Array.isArray(definition.order) ? definition.order : null
        };
    };

    const getCurrentView = (api) => ({
        status: normalizeArray(appliedFilters.status),
        region: normalizeArray(appliedFilters.region),
        tenantType: normalizeArray(appliedFilters.tenantType),
        search: getSearchInputValue(api),
        colVis: captureColumnVisibility(api),
        columnOrder: captureColumnOrder(api),
        order: typeof api.order === 'function' ? api.order() : baseOrder
    });

    const serializeView = (view) => JSON.stringify({
        status: normalizeArray(view?.status),
        region: normalizeArray(view?.region),
        tenantType: normalizeArray(view?.tenantType),
        search: normalizeString(view?.search),
        colVis: normalizeColumnVisibility(view?.colVis) || createDefaultColumnVisibility(),
        columnOrder: normalizeColumnOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });

    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || []);
            defaultViewRecord = items.find((view) => view.isDefault || view.IsDefault) || null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
        } catch (error) {
            if (!isAuthHandledError(error)) {
                console.warn('[Tenants] Default view could not be loaded.', error);
            }
        }
    };

    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: 'Default',
            isDefault: true,
            viewDefinition: view
        };

        if (defaultViewRecord) {
            const id = defaultViewRecord.id || defaultViewRecord.Id;
            defaultViewRecord = await personalizationClient.updateView(id, Object.assign({}, defaultViewRecord, payload));
        } else {
            defaultViewRecord = await personalizationClient.saveView(payload);
        }

        defaultViewState = view;
        return defaultViewRecord;
    };

    const setSaveFilterVisible = (visible) => {
        const button = document.querySelector('.dt-save-filter-btn');
        if (!button) return;
        button.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            status: [],
            region: [],
            tenantType: [],
            search: '',
            colVis: createDefaultColumnVisibility(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
            order: baseOrder
        };

        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };

    const applyFilterValues = (api, filters) => {
        const statusRegex = normalizeArray(filters.status).join('|');
        const regionRegex = normalizeArray(filters.region).join('|');
        const tenantTypeRegex = normalizeArray(filters.tenantType).join('|');

        api.column('status:name').search(statusRegex, true, false);
        api.column('region:name').search(regionRegex, true, false);
        api.column('tenantType:name').search(tenantTypeRegex, true, false);
    };

    const syncFilterControls = (filters) => {
        $('#filterStatus').val(normalizeArray(filters.status)).trigger('change');
        $('#filterRegion').val(normalizeArray(filters.region)).trigger('change');
        $('#filterTenantType').val(normalizeArray(filters.tenantType)).trigger('change');
    };

    const getStagedFilters = () => ({
        status: $('#filterStatus').val() || [],
        region: $('#filterRegion').val() || [],
        tenantType: $('#filterTenantType').val() || []
    });

    const getAppliedFilterCount = () =>
        normalizeArray(appliedFilters.status).length +
        normalizeArray(appliedFilters.region).length +
        normalizeArray(appliedFilters.tenantType).length;

    const applySavedTableState = (api, state, options) => {
        if (!api || !state) return;
        appliedFilters = {
            status: normalizeArray(state.status),
            region: normalizeArray(state.region),
            tenantType: normalizeArray(state.tenantType)
        };
        syncFilterControls(appliedFilters);
        applyFilterValues(api, appliedFilters);

        api.search(typeof state.search === 'string' ? state.search : '');
        syncSearchInput(api, state.search || '');
        applyColumnOrder(api, state.columnOrder || (options?.resetColumnOrder ? Array.from({ length: totalColumnCount }, (_, index) => index) : null));
        applyColumnVisibility(api, state.colVis || (options?.resetColumns ? createDefaultColumnVisibility() : null));
        api.order(Array.isArray(state.order) ? state.order : baseOrder);
        api.draw(false);
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        if (!host) return;
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-6');
        }
    };

    const bindInlineFilterToggle = () => {
        const button = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById(filterCollapseId);
        if (!button || !collapseEl || button.dataset.inlineFilterBound) return;
        button.dataset.inlineFilterBound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => button.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => button.setAttribute('aria-expanded', 'false'));
        button.addEventListener('click', (event) => {
            event.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false });
            if (collapseEl.classList.contains('show')) instance.hide(); else instance.show();
        });
    };

    const initSelect2Filters = () => {
        $('#inlineFilterHost select.select2').each(function () {
            const $select = $(this);
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                minimumResultsForSearch: Infinity,
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                closeOnSelect: false
            });
        });
    };

    const populateOffcanvas = (data) => {
        if (!data) return;
        document.getElementById('oc-title').innerText = data.displayName || data.name || '-';
        document.getElementById('oc-subtitle').innerText = `${data.code || '-'} / ${data.slug || '-'}`;
        document.getElementById('oc-status').outerHTML = statusBadge(data.status).replace('<span', '<span id="oc-status"');
        document.getElementById('oc-provisioning').innerText = data.provisioningStatus || '-';
        document.getElementById('oc-btn-details').href = `/Platform/Tenants/Details/${encodeURIComponent(data.id)}`;

        const rows = [
            [L.Details || 'Details', data.id],
            [L.Domain || 'Domain', data.domain],
            [L.TenantType || 'Tenant Type', data.tenantType],
            [L.Country || 'Country', data.country],
            [L.DefaultLanguage || 'Default Language', data.defaultLanguage],
            [L.DefaultTimezone || 'Default Timezone', data.defaultTimezone],
            [L.DefaultCurrency || 'Default Currency', data.defaultCurrency],
            [L.Created || 'Created', formatDate(data.createdAt)]
        ];

        document.getElementById('oc-details-list').innerHTML = rows.map(([label, value]) =>
            `<dt class="col-5 fw-medium text-heading mb-2">${escapeHtml(label)}</dt><dd class="col-7 mb-2 text-break">${escapeHtml(value || '-')}</dd>`
        ).join('');
    };

    const reloadTableAndToastSuccess = (message) => {
        clearSelection();
        dt.ajax.reload(() => {
            loadStats();
            window.showToast?.(message, 'success');
        }, false);
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView();

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: {
                    title: L.Filter,
                    'aria-label': L.Filter,
                    'aria-controls': filterCollapseId,
                    'aria-expanded': 'false',
                    'data-bs-toggle': 'tooltip'
                }
            },
            saveFilterBtn: {
                text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L.SaveView || '')}</span>`,
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'aria-label': L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (event, api) {
                    try {
                        await saveDefaultView(getCurrentView(api || dt));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || 'RecordSaved', 'success');
                    } catch (error) {
                        if (isAuthHandledError(error)) return;
                        window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                    }
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: `${apiUrl}/api/admin/tenants`,
                type: 'GET',
                headers: getAuthHeaders(),
                data: function () {
                    return { page: 1, pageSize: 200, sort: '-createdAt' };
                },
                dataSrc: function (json) {
                    const data = unwrap(json) || {};
                    loadStats();
                    return Array.isArray(data.items) ? data.items : [];
                }
            },
            stateSave: false,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            order: baseOrder,
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'displayName', name: 'displayName' },
                { data: 'code', name: 'code' },
                { data: 'domain', name: 'domain' },
                { data: 'tenantType', name: 'tenantType' },
                { data: 'country', name: 'country' },
                { data: 'region', name: 'region' },
                { data: 'provisioningStatus', name: 'provisioningStatus' },
                { data: 'status', name: 'status' },
                { data: 'createdAt', name: 'createdAt' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, render: () => '' },
                {
                    targets: 1,
                    searchable: false,
                    orderable: false,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${escapeHtml(data)}">`
                },
                {
                    targets: 2,
                    render: (data, type, full) => `<div><span class="fw-medium text-heading">${escapeHtml(full.displayName || full.name)}</span><br><small class="text-muted">${escapeHtml(full.id)}</small></div>`
                },
                {
                    targets: 3,
                    render: (data, type, full) => `<div><span class="fw-medium text-primary">${escapeHtml(full.code)}</span><br><small class="text-muted">${escapeHtml(full.slug || '-')}</small></div>`
                },
                {
                    targets: 7,
                    render: (data, type, full) => `<div><span class="fw-medium">${escapeHtml(full.region || '-')}</span><br><small class="text-muted">${escapeHtml(full.environment || '-')}</small></div>`
                },
                { targets: 9, render: (data) => statusBadge(data) },
                { targets: 10, render: (data, type, full) => `<div><span>${escapeHtml(formatDate(data))}</span><br><small class="text-muted">${escapeHtml(full.createdBy || 'system')}</small></div>` },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit text-end',
                    render: (data, type, full) => {
                        const suspendDisabled = full.status === 'Suspended' || full.status === 'Deactivated' ? 'disabled' : '';
                        const reactivateDisabled = full.status === 'Active' || full.status === 'Deactivated' ? 'disabled' : '';
                        return `<div class="d-flex align-items-center justify-content-end">
                            <a href="javascript:void(0);" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown" aria-label="${escapeHtml(L.Actions || '')}"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
                            <div class="dropdown-menu dropdown-menu-end m-0">
                                <a href="javascript:void(0);" class="dropdown-item js-quick-view" data-id="${escapeHtml(full.id)}" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview">${escapeHtml(L.QuickView || '')}</a>
                                <a href="/Platform/Tenants/Details/${encodeURIComponent(full.id)}" class="dropdown-item">${escapeHtml(L.ViewDetails || '')}</a>
                                <a href="javascript:void(0);" class="dropdown-item js-tenant-suspend ${suspendDisabled}" data-id="${escapeHtml(full.id)}" data-name="${escapeHtml(full.displayName || full.name)}">${escapeHtml(L.Suspend || '')}</a>
                                <a href="javascript:void(0);" class="dropdown-item js-tenant-reactivate ${reactivateDisabled}" data-id="${escapeHtml(full.id)}" data-name="${escapeHtml(full.displayName || full.name)}">${escapeHtml(L.Reactivate || '')}</a>
                            </div>
                        </div>`;
                    }
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.AddNewTenants,
                { onclick: "location.href='/Platform/Tenants/Create'" },
                extraButtons,
                { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes }
            ),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                bindInlineFilterToggle();
                initSelect2Filters();
                if (defaultViewState) {
                    applySavedTableState(api, defaultViewState);
                }
                setTimeout(() => { saveFilterArmed = true; }, 0);
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            },
            drawCallback: function () {
                updateBulkBar();
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        }));

        dt.on('search.dt order.dt column-visibility.dt column-reorder.dt columns-reordered.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const bindEvents = () => {
        document.addEventListener('click', async (event) => {
            const quickView = event.target.closest('.js-quick-view');
            if (quickView) {
                const id = quickView.getAttribute('data-id');
                try {
                    populateOffcanvas(await loadTenantDetail(id));
                } catch (error) {
                    if (!isAuthHandledError(error)) window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                }
                return;
            }

            const suspend = event.target.closest('.js-tenant-suspend:not(.disabled)');
            if (suspend) {
                const id = suspend.getAttribute('data-id');
                const name = suspend.getAttribute('data-name');
                window.showConfirm?.('AreYouSure', async (reason) => {
                    try {
                        await changeLifecycle(id, 'suspend', reason);
                        reloadTableAndToastSuccess(L.TenantSuspended || 'Tenant suspended.');
                    } catch (error) {
                        if (!isAuthHandledError(error)) window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                    }
                }, {
                    entityName: name,
                    type: 'warning',
                    confirmButtonText: L.Suspend,
                    showInput: true,
                    inputPlaceholder: L.SuspendReason
                });
                return;
            }

            const reactivate = event.target.closest('.js-tenant-reactivate:not(.disabled)');
            if (reactivate) {
                const id = reactivate.getAttribute('data-id');
                const name = reactivate.getAttribute('data-name');
                window.showConfirm?.('AreYouSure', async () => {
                    try {
                        await changeLifecycle(id, 'reactivate', '');
                        reloadTableAndToastSuccess(L.TenantReactivated || 'Tenant reactivated.');
                    } catch (error) {
                        if (!isAuthHandledError(error)) window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                    }
                }, { entityName: name, type: 'success', confirmButtonText: L.Reactivate });
            }
        });

        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            this.closest('tr')?.classList.toggle('selected', this.checked);
            updateBulkBar();
        });

        $(dtTableEl).on('change', '.dt-checkboxes-select-all', function () {
            const checked = this.checked;
            dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach((checkbox) => {
                checkbox.checked = checked;
                checkbox.closest('tr')?.classList.toggle('selected', checked);
            });
            updateBulkBar();
        });

        document.getElementById('btnClearSelection')?.addEventListener('click', clearSelection);

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = getStagedFilters();
            applyFilterValues(dt, appliedFilters);
            dt.draw(false);
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
            bootstrap.Collapse.getInstance(document.getElementById(filterCollapseId))?.hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            appliedFilters = defaultViewState
                ? {
                    status: normalizeArray(defaultViewState.status),
                    region: normalizeArray(defaultViewState.region),
                    tenantType: normalizeArray(defaultViewState.tenantType)
                }
                : { status: [], region: [], tenantType: [] };
            syncFilterControls(appliedFilters);
            applyFilterValues(dt, appliedFilters);
            dt.draw(false);
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    return {
        init: () => {
            syncL10n();
            initDataTable();
            bindEvents();
            loadStats();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TenantsList.init());
