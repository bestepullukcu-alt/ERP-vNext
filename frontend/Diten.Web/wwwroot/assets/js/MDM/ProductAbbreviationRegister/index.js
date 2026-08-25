'use strict';

const ProductAbbreviationRegisterList = (function () {
    let dt;
    let selectedProductId = '';
    let selectedProductText = '';
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    const endpoint = '/MDM/ProductAbbreviationRegister/api';
    const root = document.getElementById('product-abbreviation-register');
    const tableEl = document.getElementById('dt-product-abbreviation-register');
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'ProductAbbreviationRegister' };
    const dataColumnIndexes = [2, 3, 4, 5, 6];
    const totalColumnCount = 8;
    const baseOrder = [[3, 'asc']];
    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        selectedCountSelector: '#bulkSelectedCount',
        clearSelectionSelector: '#btnClearSelection',
        checkboxSelector: '.dt-checkboxes'
    };
    const L = window.L10n || {};
    const permissions = {
        request: root?.dataset.canRequest === 'true',
        correct: root?.dataset.canCorrect === 'true',
        retire: root?.dataset.canRetire === 'true',
        audit: root?.dataset.canAudit === 'true'
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const normalizeString = (value) => typeof value === 'string' ? value.trim() : '';
    const emptyFilters = () => ({ globalProductId: '', globalProductText: '' });
    const defaultColVis = () => dataColumnIndexes.map(() => true);
    const unwrapData = (payload) => payload?.data ?? payload?.Data ?? null;
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const antiForgeryToken = () => document.querySelector('#formProductAbbreviationRequest input[name="__RequestVerificationToken"]')?.value || '';

    const normalizeView = (view) => ({
        filters: {
            globalProductId: normalizeString(view?.filters?.globalProductId),
            globalProductText: normalizeString(view?.filters?.globalProductText)
        },
        search: normalizeString(view?.search),
        colVis: Array.isArray(view?.colVis) ? view.colVis.map(Boolean) : defaultColVis(),
        columnOrder: Array.isArray(view?.columnOrder) && view.columnOrder.length === totalColumnCount
            ? view.columnOrder.map(Number)
            : Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getSavedDefinition = (record) => {
        const value = record?.viewDefinition ?? record?.ViewDefinition ?? {};
        if (typeof value === 'object') return value;
        try { return JSON.parse(value); } catch (error) { return {}; }
    };
    const getSavedId = (record) => record?.id ?? record?.Id ?? '';
    const getSavedName = (record) => normalizeString(record?.viewName ?? record?.ViewName);
    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return null;
        try {
            const response = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const views = response?.data ?? response?.Data ?? response ?? [];
            defaultViewRecord = views.find((view) => (view.isDefault ?? view.IsDefault) === true) || views[0] || null;
            defaultViewState = defaultViewRecord ? normalizeView(getSavedDefinition(defaultViewRecord)) : null;
            return defaultViewState;
        } catch (error) {
            if (!error?.authHandled) console.error('[ProductAbbreviationRegister SaveView] Load failed.', error);
            return null;
        }
    };
    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const normalized = normalizeView(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedName(defaultViewRecord) || L.SaveView || 'Default').trim(),
            viewDefinition: normalized,
            isDefault: true,
            visibility: 'private'
        };
        const id = getSavedId(defaultViewRecord);
        const response = id
            ? await personalizationClient.updateView(id, payload)
            : await personalizationClient.saveView(payload);
        defaultViewRecord = response?.data ?? response?.Data ?? response ?? payload;
        defaultViewState = normalized;
        return normalized;
    };
    const captureColumnVisibility = (api) => dataColumnIndexes.map((index) => !!api.column(index).visible());
    const captureColumnOrder = (api) => {
        try { return api.colReorder.order(); } catch (error) { return Array.from({ length: totalColumnCount }, (_, index) => index); }
    };
    const captureSearch = (api) => normalizeString(
        api.table().container().querySelector('.dt-search input')?.value ?? api.search()
    );
    const getCurrentView = (api) => normalizeView({
        filters: { globalProductId: selectedProductId, globalProductText: selectedProductText },
        search: captureSearch(api),
        colVis: captureColumnVisibility(api),
        columnOrder: captureColumnOrder(api),
        order: api.order()
    });
    const serializeView = (view) => JSON.stringify(normalizeView(view));
    const getResetBaselineState = () => normalizeView({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
        order: baseOrder
    });
    const isDirtyComparedToDefault = (api) => serializeView(getCurrentView(api)) !== serializeView(defaultViewState || getResetBaselineState());
    const setSaveFilterVisible = (visible) => {
        document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const syncProductSelect = (selector, productId, text) => {
        const select = document.querySelector(selector);
        if (!select) return;
        if (productId && !Array.from(select.options).some((option) => option.value === productId))
            select.add(new Option(text, productId, true, true));
        $(select).val(productId || null).trigger('change.select2');
    };
    const syncSearch = (api, value) => {
        const search = normalizeString(value);
        api.search(search);
        const input = api.table().container().querySelector('.dt-search input');
        if (input) input.value = search;
    };
    const applySavedTableState = (api, state) => {
        const normalized = normalizeView(state || {});
        selectedProductId = normalized.filters.globalProductId;
        selectedProductText = normalized.filters.globalProductText;
        syncProductSelect('#filterGlobalProduct', selectedProductId, selectedProductText);
        syncSearch(api, normalized.search);
        dataColumnIndexes.forEach((index, position) => api.column(index).visible(normalized.colVis[position], false));
        if (typeof api.colReorder?.order === 'function') api.colReorder.order(normalized.columnOrder, true);
        api.order(normalized.order);
        return normalized;
    };
    const redrawAppliedTableState = (api, state) => {
        const normalized = applySavedTableState(api, state);
        api.draw(false);
        syncSearch(api, normalized.search);
        return normalized;
    };
    const reloadAppliedTableState = (api, state) => {
        const normalized = applySavedTableState(api, state);
        api.ajax.reload(() => syncSearch(api, normalized.search), false);
        return normalized;
    };

    const getErrorMessage = async (response) => {
        let payload = {};
        try { payload = await response.json(); } catch (error) { /* bounded status mapping below */ }
        const errors = payload?.errors ?? payload?.Errors ?? [];
        const code = Array.isArray(errors) ? errors.find(Boolean) : '';
        const stable = {
            ABBREVIATION_GRAMMAR_INVALID: L.ErrorValidation,
            ABBREVIATION_PERMISSION_DENIED: L.ErrorForbidden,
            ABBREVIATION_CANCEL_NOT_REQUEST_OWNER: L.ErrorForbidden,
            ABBREVIATION_MAKER_CHECKER_VIOLATION: L.ErrorForbidden,
            CONCURRENCY_CONFLICT: L.ErrorConflict
        };
        return stable[code] || ({ 400: L.ErrorValidation, 403: L.ErrorForbidden, 404: L.ErrorNotFound, 409: L.ErrorConflict })[response.status] || L.ErrorGateway;
    };
    const handleUnauthorized = () => {
        window.DtDefaults?.handleUnauthorized?.();
        const error = new Error('auth-refresh-in-progress');
        error.authHandled = true;
        throw error;
    };

    const initSelector = (selector, dropdownParent) => {
        const $selector = $(selector);
        if (!$selector.length || $selector.hasClass('select2-hidden-accessible')) return;
        $selector.select2({
            dropdownParent: $(dropdownParent || document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            width: 'element',
            allowClear: true,
            minimumInputLength: 0,
            placeholder: $selector.data('placeholder'),
            ajax: {
                delay: 250,
                transport: (params, success, failure) => {
                    const query = new URLSearchParams({
                        pageNumber: String(params.data.page || 1),
                        pageSize: '20',
                        search: normalizeString(params.data.term)
                    });
                    fetch(`${endpoint}/global-products/selector?${query}`, { credentials: 'same-origin', headers: getAuthHeaders() })
                        .then((response) => {
                            if (response.status === 401) handleUnauthorized();
                            if (!response.ok) throw new Error(L.SelectorLoadError);
                            return response.json();
                        })
                        .then(success)
                        .catch(failure);
                },
                processResults: (payload) => {
                    const page = unwrapData(payload) || {};
                    const items = page.items ?? page.Items ?? [];
                    return {
                        results: items.map((item) => ({
                            id: item.id ?? item.Id,
                            text: `${item.canonicalCode ?? item.CanonicalCode} — ${item.globalProductName ?? item.GlobalProductName}`
                        })),
                        pagination: { more: ((page.pageNumber ?? page.PageNumber) * (page.pageSize ?? page.PageSize)) < (page.totalCount ?? page.TotalCount) }
                    };
                }
            }
        });
    };

    const lifecycleMap = () => ({
        REQUESTED: { text: L.LifecycleRequested, css: 'bg-label-warning' }, 0: { text: L.LifecycleRequested, css: 'bg-label-warning' },
        ACTIVE: { text: L.LifecycleActive, css: 'bg-label-success' }, 1: { text: L.LifecycleActive, css: 'bg-label-success' },
        REJECTED: { text: L.LifecycleRejected, css: 'bg-label-danger' }, 2: { text: L.LifecycleRejected, css: 'bg-label-danger' },
        CANCELLED: { text: L.LifecycleCancelled, css: 'bg-label-secondary' }, 3: { text: L.LifecycleCancelled, css: 'bg-label-secondary' },
        RETIRED: { text: L.LifecycleRetired, css: 'bg-label-dark' }, 4: { text: L.LifecycleRetired, css: 'bg-label-dark' }
    });
    const renderLifecycle = (value) => {
        const item = lifecycleMap()[value] || { text: L.Unknown, css: 'bg-label-secondary' };
        return `<span class="badge ${item.css}">${escapeHtml(item.text)}</span>`;
    };
    const lifecycleText = (value) => value === null || value === undefined
        ? L.Unknown
        : (lifecycleMap()[value]?.text || L.Unknown);

    const loadEvidence = async (entryId) => {
        if (!permissions.audit) return;
        const section = document.getElementById('auditEvidenceSection');
        const loading = document.getElementById('auditEvidenceLoading');
        const empty = document.getElementById('auditEvidenceEmpty');
        const list = document.getElementById('auditEvidenceList');
        section?.classList.remove('d-none');
        loading?.classList.remove('d-none');
        empty?.classList.add('d-none');
        if (list) list.innerHTML = '';
        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(entryId)}/evidence`, { credentials: 'same-origin', headers: getAuthHeaders() });
            if (response.status === 401) handleUnauthorized();
            if (!response.ok) throw new Error(await getErrorMessage(response));
            const evidence = unwrapData(await response.json()) || {};
            const history = evidence.history ?? evidence.History ?? [];
            loading?.classList.add('d-none');
            empty?.classList.toggle('d-none', history.length !== 0);
            if (list) list.innerHTML = history.map((item) => {
                const before = item.beforeStatus ?? item.BeforeStatus;
                const after = item.afterStatus ?? item.AfterStatus;
                return `<div class="border rounded p-3">
                    <div class="fw-medium">${escapeHtml(item.eventType ?? item.EventType)}</div>
                    <div class="small text-muted mb-2">${escapeHtml(item.occurredAtUtc ?? item.OccurredAtUtc)}</div>
                    <dl class="row small mb-0">
                        <dt class="col-5">${escapeHtml(L.AuditBefore)}</dt><dd class="col-7">${escapeHtml(lifecycleText(before))}</dd>
                        <dt class="col-5">${escapeHtml(L.AuditAfter)}</dt><dd class="col-7">${escapeHtml(lifecycleText(after))}</dd>
                        <dt class="col-5">${escapeHtml(L.AuditActor)}</dt><dd class="col-7 text-break">${escapeHtml(item.canonicalHumanSubjectId ?? item.CanonicalHumanSubjectId)}</dd>
                        <dt class="col-5">${escapeHtml(L.AuditCorrelation)}</dt><dd class="col-7 text-break">${escapeHtml(item.correlationId ?? item.CorrelationId)}</dd>
                        <dt class="col-5">${escapeHtml(L.AuditIdempotency)}</dt><dd class="col-7 text-break">${escapeHtml(item.idempotencyKey ?? item.IdempotencyKey)}</dd>
                        <dt class="col-5">${escapeHtml(L.AuditEvidenceHash)}</dt><dd class="col-7 text-break">${escapeHtml(item.evidenceHash ?? item.EvidenceHash)}</dd>
                    </dl>
                    ${item.reason ?? item.Reason ? `<div class="small mt-2">${escapeHtml(item.reason ?? item.Reason)}</div>` : ''}
                </div>`;
            }).join('');
        } catch (error) {
            loading?.classList.add('d-none');
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
        }
    };
    const showDetails = (row) => {
        document.getElementById('oc-subtitle').textContent = row.abbreviation;
        document.getElementById('oc-global-product').textContent = row.globalProductDisplay;
        document.getElementById('oc-abbreviation').textContent = row.abbreviation;
        document.getElementById('oc-version').textContent = String(row.version);
        document.getElementById('oc-retirement-pending').textContent = row.retirementPending ? L.Yes : L.No;
        const status = document.getElementById('oc-status');
        if (status) status.outerHTML = renderLifecycle(row.lifecycleStatus).replace('<span ', '<span id="oc-status" ');
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
        loadEvidence(row.id);
    };

    const requestRetirement = (row) => {
        window.showConfirm?.(L.AreYouSure, async (reason) => {
            const response = await fetch(`${endpoint}/${encodeURIComponent(row.id)}/retirement-requests`, {
                method: 'POST', credentials: 'same-origin',
                headers: { ...getAuthHeaders(), 'Content-Type': 'application/json', RequestVerificationToken: antiForgeryToken() },
                body: JSON.stringify({ expectedVersion: row.version, reason })
            });
            if (!response.ok) throw new Error(await getErrorMessage(response));
            dt.ajax.reload(null, false);
        }, { type: 'warning', showInput: true, inputRequired: true, inputLabel: L.AreYouSure, confirmButtonText: L.Apply });
    };

    const renderActions = (row) => {
        const actions = [{ key: 'details', className: 'js-quick-view', text: L.ViewDetails, icon: 'bx bx-show', attrs: { 'data-id': row.id } }];
        if (permissions.retire && (row.lifecycleStatus === 'ACTIVE' || row.lifecycleStatus === 1) && !row.retirementPending)
            actions.push({ key: 'retire', className: 'js-request-retirement', text: L.RequestRetirement, icon: 'bx bx-archive', attrs: { 'data-id': row.id } });
        return window.DitenDataTable.renderActions(actions);
    };

    const initDataTable = async () => {
        if (!tableEl || !window.DtDefaults) return;
        const saved = await loadDefaultView();
        if (saved) {
            selectedProductId = saved.filters.globalProductId;
            selectedProductText = saved.filters.globalProductText;
        }
        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L.Filter, 'aria-label': L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false' },
            action: () => bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse'), { toggle: false }).toggle()
        };
        const saveFilterBtn = {
            text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L.SaveView)}</span>`,
            className: 'btn btn-label-primary dt-save-filter-btn d-none',
            attr: { title: L.SaveView, 'aria-label': L.SaveView },
            action: async (event, api) => {
                try {
                    await saveDefaultView(getCurrentView(api || dt));
                    setSaveFilterVisible(false);
                } catch (error) {
                    setSaveFilterVisible(true);
                    window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                }
            }
        };
        const config = window.DtDefaults.create({
            processing: true,
            serverSide: false,
            stateSave: false,
            order: saved?.order || baseOrder,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: (request, callback) => {
                if (!selectedProductId) {
                    callback({ data: [] });
                    return;
                }
                fetch(`${endpoint}/by-global-product/${encodeURIComponent(selectedProductId)}`, { credentials: 'same-origin', headers: getAuthHeaders() })
                    .then((response) => {
                        if (response.status === 401) handleUnauthorized();
                        if (response.status === 404) return null;
                        if (!response.ok) return getErrorMessage(response).then((message) => Promise.reject(new Error(message)));
                        return response.json();
                    })
                    .then((payload) => {
                        const entry = payload ? unwrapData(payload) : null;
                        callback({ data: entry ? [{
                            id: entry.id ?? entry.Id,
                            globalProductId: entry.globalProductId ?? entry.GlobalProductId,
                            globalProductDisplay: selectedProductText,
                            abbreviation: entry.abbreviation ?? entry.Abbreviation,
                            lifecycleStatus: entry.lifecycleStatus ?? entry.LifecycleStatus,
                            version: entry.version ?? entry.Version,
                            retirementPending: entry.retirementPending ?? entry.RetirementPending
                        }] : [] });
                    })
                    .catch((error) => {
                        if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
                        callback({ data: [] });
                    });
            },
            columns: [
                { data: 'id', name: 'control' }, { data: 'id', name: 'checkbox' },
                { data: 'globalProductDisplay', name: 'globalProduct' }, { data: 'abbreviation', name: 'abbreviation' },
                { data: 'lifecycleStatus', name: 'lifecycleStatus' }, { data: 'version', name: 'version' },
                { data: 'retirementPending', name: 'retirementPending' }, { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, className: 'dt-checkboxes-cell cell-fit', searchable: false, orderable: false, responsivePriority: 3, render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${escapeHtml(data)}">` },
                { targets: 4, render: renderLifecycle },
                { targets: 6, render: (value) => value ? L.Yes : L.No },
                { targets: -1, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3', render: (data, type, row) => renderActions(row) }
            ],
            buttons: window.DtDefaults.exportButtons(permissions.request ? L.AddNew : null, {}, { filterBtn, saveFilterBtn }, { exportColumns: dataColumnIndexes, colvisColumns: dataColumnIndexes }),
            initComplete: function () {
                const toolbar = document.querySelector('.dt-filter-btn')?.closest('.dt-layout-row') || document.querySelector('.dt-filter-btn')?.closest('.row');
                const host = document.getElementById('inlineFilterHost');
                if (toolbar && host) { toolbar.insertAdjacentElement('afterend', host); host.classList.add('px-3'); }
                initSelector('#filterGlobalProduct', document.body);
                initSelector('#requestGlobalProduct', document.getElementById('offcanvasCreateEdit'));
                redrawAppliedTableState(this.api(), saved || getResetBaselineState());
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    document.getElementById('formProductAbbreviationRequest')?.reset();
                    bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
                });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () { window.DtDefaults.updateVisualState(this.api(), selectedProductId ? 1 : 0); }
        });
        dt = new DataTable(tableEl, config);
        window.DitenDataTable.bindBulkSelection(tableEl, dt, bulkOptions);
        window.DitenDataTable.bindActionDispatcher({
            tableEl,
            dt,
            onRowAction: {
                details: ({ row }) => showDetails(row),
                retire: ({ row }) => requestRetirement(row)
            }
        });
        $(tableEl).on('column-reorder.dt columns-reordered.dt search.dt order.dt column-visibility.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const bindEvents = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            selectedProductId = normalizeString($('#filterGlobalProduct').val());
            selectedProductText = normalizeString($('#filterGlobalProduct option:selected').text());
            dt?.ajax.reload();
            bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse'), { toggle: false }).hide();
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            selectedProductId = '';
            selectedProductText = '';
            if (dt) reloadAppliedTableState(dt, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        document.getElementById('btnRequestAbbreviation')?.addEventListener('click', () => {
            const form = document.getElementById('formProductAbbreviationRequest');
            if (!form?.checkValidity()) { form?.classList.add('was-validated'); return; }
            const productId = normalizeString($('#requestGlobalProduct').val());
            const productText = normalizeString($('#requestGlobalProduct option:selected').text());
            const abbreviation = normalizeString(document.getElementById('requestAbbreviation')?.value);
            window.showConfirm?.(L.RequestConfirmation, async () => {
                const body = new FormData(form);
                body.set('GlobalProductId', productId);
                body.set('Abbreviation', abbreviation);
                const response = await fetch(`${endpoint}/requests`, {
                    method: 'POST', credentials: 'same-origin',
                    headers: { ...getAuthHeaders(), RequestVerificationToken: antiForgeryToken() }, body
                });
                if (response.status === 401) handleUnauthorized();
                if (!response.ok) throw new Error(await getErrorMessage(response));
                selectedProductId = productId;
                selectedProductText = productText;
                syncProductSelect('#filterGlobalProduct', productId, productText);
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                dt?.ajax.reload(null, false);
                window.showToast?.(L.RequestSuccess, 'success');
            }, { entityName: abbreviation, type: 'primary', confirmButtonText: L.Apply });
        });
    };

    return { init: async () => { bindEvents(); await initDataTable(); } };
})();

document.addEventListener('DOMContentLoaded', () => ProductAbbreviationRegisterList.init());
