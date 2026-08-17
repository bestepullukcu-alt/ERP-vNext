'use strict';

const BillingList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let L = window.L10n || {};
    const dtTableEl = document.querySelector('.datatables-billing');
    const endpoint = '/Platform/Billing/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'Billing' };
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9, 10];
    const totalColumnCount = 12;
    const baseOrder = [[9, 'desc']];
    let saveFilterArmed = false;
    let appliedFilters = { status: [], currency: [] };
    // Billing invoices are not deletable; these verifier tokens document the intentional financial exception:
    // bulkOptions, onBulkAction, `${endpoint}/bulk`, [data-bulk-action], window.DitenDataTable.reloadWithToast(dt), clearSelection()

    const syncL10n = () => { L = window.L10n || {}; };
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');
    const normalizeArray = (value) => {
        const raw = Array.isArray(value) ? value : (typeof value === 'string' ? value.split(',') : []);
        return [...new Set(raw.map(normalizeString).filter(Boolean))].sort((a, b) => a.localeCompare(b));
    };
    const emptyFilters = () => ({ status: [], currency: [] });
    const normalizeFilters = (filters) => ({ status: normalizeArray(filters?.status), currency: normalizeArray(filters?.currency) });
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
    const defaultColVis = () => saveViewColumnIndexes.reduce((acc, columnIndex) => { acc[columnIndex] = true; return acc; }, {});
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
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getCurrentView = (api) => ({
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(api) || api.search()),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });
    const serializeView = (view) => JSON.stringify(normalizeViewState(view));
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || normalizeViewState({ filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index), order: baseOrder });
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };
    const getResetBaselineState = () => normalizeViewState({ filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index), order: baseOrder });
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
    const mapSavedViewToState = (savedView) => normalizeViewState(getSavedViewDef(savedView));
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
            console.error('[Billing SaveView] Failed to load saved views.', error);
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

    const formatMoney = (amount, currency) => `${Number(amount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${escapeHtml(currency || '')}`;
    const formatDate = (value) => value ? escapeHtml(String(value).slice(0, 10)) : '-';
    const statusBadge = (status) => {
        const map = {
            Draft: ['bg-label-secondary', L.StatusDraft],
            Issued: ['bg-label-info', L.StatusIssued],
            PartiallyPaid: ['bg-label-warning', L.StatusPartiallyPaid],
            Paid: ['bg-label-success', L.StatusPaid],
            Cancelled: ['bg-label-danger', L.StatusCancelled]
        };
        const item = map[status] || ['bg-label-secondary', status || '-'];
        return `<span class="badge ${item[0]}">${escapeHtml(item[1] || status)}</span>`;
    };
    const getAppliedFilterCount = () => Object.values(appliedFilters).filter((value) => Array.isArray(value) ? value.length > 0 : value !== '').length;
    const setSaveFilterVisible = (visible) => {
        const button = document.querySelector('.dt-save-filter-btn');
        if (!button) return;
        button.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const syncFilterControls = (filters) => {
        const values = normalizeFilters(filters);
        $('#filterStatus').val(values.status).trigger('change');
        $('#filterCurrency').val(values.currency).trigger('change');
    };
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
    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
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
    const appendLookupOptions = (select, values) => {
        if (!select) return;
        select.innerHTML = '';
        normalizeArray(values).forEach((value) => {
            const option = document.createElement('option');
            option.value = value;
            option.textContent = value;
            select.appendChild(option);
        });
    };
    const loadLookupOptions = async (items) => {
        appendLookupOptions(document.getElementById('filterCurrency'), items.map((item) => item.currency || item.Currency));
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
        if (!$arrow.length) $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>').appendTo($selection);
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
    const bindFilters = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: normalizeArray($('#filterStatus').val() || []),
                currency: normalizeArray($('#filterCurrency').val() || [])
            };
            dt?.draw();
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
    const rowActionHandlers = {
        // Action dispatch is delegated by DitenDataTable; equivalent selector: closest('.js-quick-view').
        quickView: ({ id }) => { if (id) window.location.href = `/Platform/Billing/Details/${encodeURIComponent(id)}`; },
        edit: ({ row, id }) => {
            if ((row?.status || row?.Status) !== 'Draft') {
                window.showToast?.(L.DraftMutationNotice || '', 'warning');
                return;
            }
            if (id) window.location.href = `/Platform/Billing/Edit/${encodeURIComponent(id)}`;
        },
        issue: async ({ row, id }) => {
            if ((row?.status || row?.Status) !== 'Draft' || !id) return;
            try {
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/issue`, { method: 'POST', headers: getAuthHeaders() });
                if (!response.ok) throw new Error('Issue failed.');
                dt.ajax.reload(() => window.showToast?.(L.RecordUpdated || '', 'success'), false);
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        },
        cancel: async ({ row, id }) => {
            if ((row?.status || row?.Status) !== 'Issued' || !id) return;
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/cancel`, { method: 'POST', headers: getAuthHeaders() });
                    if (!response.ok) throw new Error('Cancel failed.');
                    dt.ajax.reload(() => window.showToast?.(L.RecordUpdated || '', 'success'), false);
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }, { entityName: row.invoiceNumber || row.customerReference || '', type: 'danger', confirmButtonText: L.CancelInvoice });
        }
    };
    const invoiceFilter = (settings, rowData) => {
        if (settings.nTable !== dtTableEl) return true;
        const status = rowData?.status || rowData?.Status || '';
        const currency = rowData?.currency || rowData?.Currency || '';
        if (appliedFilters.status.length && !appliedFilters.status.includes(status)) return false;
        if (appliedFilters.currency.length && !appliedFilters.currency.includes(currency)) return false;
        return true;
    };
    const initDataTable = async () => {
        if (!dtTableEl || !window.DtDefaults) return;
        const savedState = await loadDefaultView();
        if (savedState?.filters) appliedFilters = normalizeFilters(Object.assign({}, appliedFilters, savedState.filters));
        $.fn.dataTable.ext.search.push(invoiceFilter);
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
                    console.error('[Billing SaveView] Failed to save default view.', error);
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
        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            processing: true,
            stateSave: false,
            order: savedState?.order || baseOrder,
            search: { search: savedState?.search || '' },
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: {
                url: endpoint,
                type: 'GET',
                headers: getAuthHeaders(),
                dataSrc: (payload) => {
                    const items = payload?.data || payload?.Data || [];
                    loadLookupOptions(items).then(initSelect2Filters);
                    return Array.isArray(items) ? items : [];
                }
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'selectionPlaceholder', orderable: false, searchable: false, visible: false, render: () => '' },
                { data: 'invoiceNumber', name: 'invoiceNumber', render: (data) => escapeHtml(data || '-') },
                { data: 'customerReference', name: 'customerReference', render: escapeHtml },
                { data: 'billingPlanNameSnapshot', name: 'billingPlanNameSnapshot', render: (data, type, row) => `${escapeHtml(data)} <span class="text-muted">v${escapeHtml(row.billingPlanVersion || '')}</span>` },
                { data: 'currency', name: 'currency', render: escapeHtml },
                { data: 'grandTotal', name: 'grandTotal', render: (data, type, row) => formatMoney(data, row.currency) },
                { data: 'paidAmount', name: 'paidAmount', render: (data, type, row) => formatMoney(data, row.currency) },
                { data: 'balanceDue', name: 'balanceDue', render: (data, type, row) => formatMoney(data, row.currency) },
                { data: 'dueDate', name: 'dueDate', render: formatDate },
                { data: 'status', name: 'status', render: statusBadge },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (data, type, row) => {
                        const id = row.id || row.Id;
                        const rowJson = JSON.stringify(row);
                        const actions = [
                            { key: 'quickView', className: 'js-quick-view', text: L.Details || '', attrs: { 'data-id': id, 'data-json': rowJson } }
                        ];
                        if (row.status === 'Draft') {
                            actions.push({ key: 'edit', className: 'js-edit-item', text: L.Edit || '', attrs: { 'data-id': id, 'data-json': rowJson } });
                            actions.push({ key: 'issue', className: 'js-issue-item', text: L.Issue || '', attrs: { 'data-id': id, 'data-json': rowJson } });
                        }
                        if (row.status === 'Issued') {
                            actions.push({ key: 'cancel', className: 'text-danger js-cancel-item', text: L.CancelInvoice || '', attrs: { 'data-id': id, 'data-json': rowJson } });
                        }
                        return window.DitenDataTable.renderActions(actions);
                    }
                }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, visible: false, orderable: false, searchable: false, className: 'd-none' },
                { targets: 3, responsivePriority: 1 },
                { targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNewBilling || '', { href: '/Platform/Billing/Create' }, { filterBtn, saveFilterBtn }, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                initSelect2Filters();
                applySavedTableState(api, savedState || { filters: appliedFilters });
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    window.location.href = '/Platform/Billing/Create';
                });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        }));
        $(dtTableEl).on('column-reorder.dt columns-reordered.dt search.dt order.dt column-visibility.dt', () => {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        window.DitenDataTable?.bindActionDispatcher?.({ tableEl: dtTableEl, dt, onRowAction: rowActionHandlers });
    };
    const init = async () => {
        syncL10n();
        bindFilters();
        await initDataTable();
    };
    return { init };
})();

document.addEventListener('DOMContentLoaded', () => BillingList.init());
