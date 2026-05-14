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
    const endpoint = '/Platform/Tenants/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'Tenants' };
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9, 10];
    const defaultVisibleColumnIndexes = [2, 3, 4, 9, 10];
    const totalColumnCount = 12;
    const baseOrder = [[2, 'asc']];
    const filterCollapseId = 'inlineFilterCollapse';

    const syncL10n = () => { L = window.L10n || {}; };
    const isAuthHandledError = (error) => error?.authHandled === true || error?.code === 'auth-refresh-in-progress';

    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

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

    const firstValue = (source, keys) => {
        for (const key of keys) {
            const value = source?.[key];
            if (value !== undefined && value !== null && value !== '') return value;
        }
        return null;
    };

    const formatBytes = (value) => {
        const bytes = Number(value);
        if (!Number.isFinite(bytes) || bytes < 0) return null;
        const units = ['B', 'KB', 'MB', 'GB', 'TB'];
        let size = bytes;
        let unitIndex = 0;
        while (size >= 1024 && unitIndex < units.length - 1) {
            size /= 1024;
            unitIndex += 1;
        }
        return `${size >= 10 || unitIndex === 0 ? size.toFixed(0) : size.toFixed(1)} ${units[unitIndex]}`;
    };

    const formatStorage = (tenant) => {
        const usedGb = firstValue(tenant, ['storageUsedGb', 'storageUsedGB']);
        const quotaGb = firstValue(tenant, ['storageQuotaGb', 'storageQuotaGB']);
        const usedBytes = firstValue(tenant, ['storageUsedBytes', 'usedStorageBytes', 'storageBytesUsed']);
        const quotaBytes = firstValue(tenant, ['storageQuotaBytes', 'storageLimitBytes', 'quotaBytes']);
        const percent = firstValue(tenant, ['storageUsagePercent', 'storagePercent', 'quotaUsagePercent']);
        const usedNumber = Number(usedGb);
        const quotaNumber = Number(quotaGb);
        const calculatedPercent = Number.isFinite(Number(percent))
            ? Number(percent)
            : (Number.isFinite(usedNumber) && Number.isFinite(quotaNumber) && quotaNumber > 0 ? (usedNumber / quotaNumber) * 100 : 0);
        const safePercent = Math.max(0, Math.min(100, calculatedPercent));
        const usedText = Number.isFinite(usedNumber)
            ? `${usedNumber.toFixed(1)} GB`
            : (formatBytes(usedBytes) || firstValue(tenant, ['storageUsed', 'usedStorage', 'storageUsedText']));
        const quotaText = Number.isFinite(quotaNumber)
            ? `${quotaNumber.toFixed(0)} GB`
            : (formatBytes(quotaBytes) || firstValue(tenant, ['storageQuota', 'storageLimit', 'storageQuotaText']));

        if (!usedText && !quotaText && !Number.isFinite(calculatedPercent)) return '<span class="text-muted">-</span>';

        return `<div class="tenant-storage-cell">
            <div class="d-flex align-items-center gap-2 mb-1">
                <span class="fw-medium">${escapeHtml(usedText || '0 GB')}</span>
                <span class="text-muted">/</span>
                <span>${escapeHtml(quotaText || '-')}</span>
                <small class="text-muted ms-auto">${escapeHtml(`${safePercent.toFixed(0)}%`)}</small>
            </div>
            <div class="progress" style="height: 6px; min-width: 140px;">
                <div class="progress-bar ${calculatedPercent > 100 ? 'bg-danger' : 'bg-primary'}" role="progressbar" style="width: ${safePercent}%;" aria-valuenow="${safePercent.toFixed(0)}" aria-valuemin="0" aria-valuemax="100"></div>
            </div>
        </div>`;
    };

    const statValue = (source, keys) => {
        for (const key of keys) {
            if (Object.prototype.hasOwnProperty.call(source || {}, key)) return source[key] ?? 0;
        }
        return null;
    };

    const updateKpisFromItems = (items) => {
        if (!Array.isArray(items)) return;
        document.getElementById('kpi-total').innerText = String(items.length);
        document.getElementById('kpi-active').innerText = String(items.filter((item) => item.status === 'Active').length);
        document.getElementById('kpi-trial').innerText = String(items.filter((item) => item.tenantType === 'Trial' || item.tenantType === 2).length);
        document.getElementById('kpi-suspended').innerText = String(items.filter((item) => item.status === 'Suspended').length);
        document.getElementById('kpi-over-quota').innerText = String(items.filter((item) => item.isOverQuota || item.overQuota || item.storageOverQuota).length);
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

    const tenantTypeBadge = (tenantType) => {
        const map = {
            Customer: 'bg-label-primary',
            Demo: 'bg-label-info',
            Internal: 'bg-label-dark',
            Trial: 'bg-label-warning',
            Paid: 'bg-label-success'
        };
        return `<span class="badge ${map[tenantType] || 'bg-label-secondary'}">${escapeHtml(tenantType || L.Unknown || '-')}</span>`;
    };

    const updateKpis = (stats) => {
        if (!stats) return;
        document.getElementById('kpi-total').innerText = String(stats.total || 0);
        document.getElementById('kpi-active').innerText = String(stats.active || 0);
        const trial = statValue(stats, ['trial', 'trialTenants']);
        if (trial !== null) document.getElementById('kpi-trial').innerText = String(trial);
        document.getElementById('kpi-suspended').innerText = String(stats.suspended || 0);
        const overQuota = statValue(stats, ['overQuota', 'overQuotaTenants']);
        if (overQuota !== null) document.getElementById('kpi-over-quota').innerText = String(overQuota);
    };

    const loadStats = async () => {
        try {
            const response = await fetch(`${endpoint}/stats`, {
                credentials: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) return;
            updateKpis(unwrap(await response.json()));
        } catch (error) {
            // KPI cards are secondary; table load errors are handled separately.
        }
    };

    const changeLifecycle = async (tenantId, action, reason) => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(tenantId)}/${action}`, {
            method: 'POST',
            credentials: 'include',
            headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify({ reason: reason || '' })
        });

        if (response.status === 401) {
            window.DtDefaults?.handleUnauthorized?.();
            const authError = new Error('auth-refresh-in-progress');
            authError.authHandled = true;
            throw authError;
        }

        if (response.status === 403) {
            throw new Error(L.PermissionDenied || 'Permission denied.');
        }

        if (!response.ok) {
            throw new Error(await response.text());
        }

        return unwrap(await response.json());
    };

    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        clearSelectionSelector: '#btnClearSelection',
        bulkActionSelector: '[data-bulk-action]',
        onBulkAction: {
            delete: ({ ids }) => {
                window.showConfirm?.(L.BulkDeleteConfirm || L.AreYouSure || '', async () => {
                    try {
                        const response = await fetch(`${endpoint}/bulk`, {
                            method: 'DELETE',
                            credentials: 'include',
                            headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                            body: JSON.stringify({ ids })
                        });
                        if (!response.ok) throw new Error('Bulk delete failed.');
                        reloadWithSuccessToast('BulkDeleteSuccess');
                    } catch (error) {
                        if (!isAuthHandledError(error)) window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                    }
                }, { type: 'danger', confirmButtonText: L.BulkDelete });
            }
        }
    };

    const getSelectedIds = () => window.DitenDataTable?.getSelectedIds?.(dtTableEl, bulkOptions.checkboxSelector) || [];
    const clearSelection = () => window.DitenDataTable?.clearSelection?.(dtTableEl, bulkOptions);

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

    const defaultColVis = () =>
        saveViewColumnIndexes.reduce((acc, index) => {
            acc[index] = defaultVisibleColumnIndexes.includes(index);
            return acc;
        }, {});

    const emptyFilters = () => ({ status: [], region: [], tenantType: [] });

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
        colVis: normalizeColumnVisibility(view?.colVis) || defaultColVis(),
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
            viewName: (defaultViewRecord?.viewName || defaultViewRecord?.ViewName || L.SaveView || 'Default').trim(),
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
            filters: emptyFilters(),
            search: '',
            colVis: defaultColVis(),
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
        applyColumnVisibility(api, state.colVis || (options?.resetColumns ? defaultColVis() : null));
        api.order(Array.isArray(state.order) ? state.order : baseOrder);
        api.draw(false);
    };

    const getResetBaselineState = () => ({
        status: [],
        region: [],
        tenantType: [],
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
        order: baseOrder
    });

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        if (!host) return;
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
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

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue, bulkOptions);
        loadStats();
    };

    const rowActionHandlers = {
        delete: ({ row, id }) => {
            const rowId = id || row?.id;
            if (!rowId) return;
            const entityName = row?.displayName || row?.name || '';
            window.showConfirm?.(L.AreYouSure || '', async () => {
                try {
                    const response = await fetch(`${endpoint}/${encodeURIComponent(rowId)}`, {
                        method: 'DELETE',
                        credentials: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted', entityName);
                } catch (error) {
                    if (!isAuthHandledError(error)) window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                }
            }, { entityName, type: 'danger', confirmButtonText: L.Delete });
        },
        edit: ({ id }) => {
            if (id) window.location.href = `/Platform/Tenants/Edit/${encodeURIComponent(id)}`;
        },
        details: ({ id }) => {
            if (id) window.location.href = `/Platform/Tenants/Details/${encodeURIComponent(id)}`;
        }
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
                url: endpoint,
                type: 'GET',
                headers: getAuthHeaders(),
                data: function () {
                    return { page: 1, pageSize: 200, sort: '-createdAt' };
                },
                dataSrc: function (json) {
                    const data = unwrap(json) || {};
                    const items = Array.isArray(data.items) ? data.items : [];
                    updateKpisFromItems(items);
                    loadStats();
                    return items;
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
                { data: null, name: 'storage' },
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
                    render: (data, type, full) => `<span class="fw-medium text-heading">${escapeHtml(full.displayName || full.name || '-')}</span>`
                },
                {
                    targets: 3,
                    render: (data, type, full) => `<div><span class="fw-medium text-primary">${escapeHtml(full.code)}</span><br><small class="text-muted">${escapeHtml(full.slug || '-')}</small></div>`
                },
                {
                    targets: 7,
                    render: (data, type, full) => `<div><span class="fw-medium">${escapeHtml(full.region || '-')}</span><br><small class="text-muted">${escapeHtml(full.environment || '-')}</small></div>`
                },
                { targets: 5, render: (data) => tenantTypeBadge(data) },
                { targets: [5, 6, 7, 8], visible: false },
                { targets: 9, render: (data) => statusBadge(data) },
                { targets: 10, render: (data, type, full) => formatStorage(full) },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit text-end',
                    render: (data, type, full) => {
                        const suspendDisabled = full.status === 'Suspended' || full.status === 'Deactivated' ? 'disabled' : '';
                        const reactivateDisabled = full.status === 'Active' || full.status === 'Deactivated' ? 'disabled' : '';
                        const rowJson = JSON.stringify(full);
                        return window.DitenDataTable.renderActions([
                            {
                                key: 'delete',
                                className: 'text-danger me-1',
                                icon: 'bx bx-trash',
                                attrs: { 'data-id': full.id, 'data-json': rowJson }
                            },
                            {
                                key: 'edit',
                                icon: 'bx bx-edit',
                                text: L.Edit || '',
                                attrs: { 'data-id': full.id, 'data-json': rowJson }
                            },
                            {
                                key: 'details',
                                icon: 'bx bx-show',
                                text: L.ViewDetails || '',
                                attrs: { 'data-id': full.id, 'data-json': rowJson }
                            },
                            {
                                key: 'suspend',
                                className: `js-tenant-suspend ${suspendDisabled}`,
                                text: L.Suspend || '',
                                attrs: { 'data-id': full.id, 'data-name': full.displayName || full.name }
                            },
                            {
                                key: 'reactivate',
                                className: `js-tenant-reactivate ${reactivateDisabled}`,
                                text: L.Reactivate || '',
                                attrs: { 'data-id': full.id, 'data-name': full.displayName || full.name }
                            }
                        ]);
                    }
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.AddNewTenants,
                { href: '/Platform/Tenants/Create' },
                extraButtons,
                { exportColumns: defaultVisibleColumnIndexes, colvisColumns: saveViewColumnIndexes }
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
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    window.location.href = '/Platform/Tenants/Create';
                });
                window.DitenDataTable?.bindActionDispatcher?.({
                    tableEl: dtTableEl,
                    dt,
                    onRowAction: rowActionHandlers
                });
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
            const suspend = event.target.closest('.js-tenant-suspend:not(.disabled)');
            if (suspend) {
                const id = suspend.getAttribute('data-id');
                const name = suspend.getAttribute('data-name');
                window.showConfirm?.('AreYouSure', async (reason) => {
                    try {
                        await changeLifecycle(id, 'suspend', reason);
                        reloadWithSuccessToast('TenantSuspended');
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
                        reloadWithSuccessToast('TenantReactivated');
                    } catch (error) {
                        if (!isAuthHandledError(error)) window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                    }
                }, { entityName: name, type: 'success', confirmButtonText: L.Reactivate });
            }
        });

        window.DitenDataTable?.bindBulkSelection?.(dtTableEl, dt, bulkOptions);

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = getStagedFilters();
            applyFilterValues(dt, appliedFilters);
            dt.draw(false);
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
            bootstrap.Collapse.getInstance(document.getElementById(filterCollapseId))?.hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            applySavedTableState(dt, getResetBaselineState());
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    return {
        init: () => {
            syncL10n();
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TenantsList.init());
