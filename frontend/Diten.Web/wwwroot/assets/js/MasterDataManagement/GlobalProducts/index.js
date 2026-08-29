/**
 * MOD-0290 Global Product Register — tenant-shell Golden Slim list.
 * Browser traffic is restricted to the same-origin MVC proxy at /GlobalProducts/api.
 */
'use strict';

const GlobalProductsList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    let appliedFilters = { lifecycleStatus: '' };

    const endpoint = '/MasterDataManagement/GlobalProducts/api';
    const tableEl = document.querySelector('.datatables-globalproducts');
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'GlobalProducts' };
    const saveViewColumnIndexes = [1, 2, 3];
    const totalColumnCount = 5;
    const baseOrder = [[1, 'asc']];
    const L = window.L10n || {};
    const permissionHost = document.querySelector('[data-can-create]');
    const canCreate = permissionHost?.getAttribute('data-can-create') === 'true';
    const canSubmit = permissionHost?.getAttribute('data-can-submit') === 'true';
    const canRetire = permissionHost?.getAttribute('data-can-retire') === 'true';
    const lifecycleRequests = new Set();
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const emptyFilters = () => ({ lifecycleStatus: '' });

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const normalizeString = (value) => typeof value === 'string' ? value.trim() : '';
    const normalizeFilters = (filters) => ({ lifecycleStatus: normalizeString(filters?.lifecycleStatus) });
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const result = order.map(Number).filter((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount);
        return result.length === totalColumnCount && new Set(result).size === totalColumnCount ? result : null;
    };
    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const result = {};
        saveViewColumnIndexes.forEach((index, position) => {
            const value = Array.isArray(colVis) ? (colVis[index] ?? colVis[position]) : colVis[index];
            if (typeof value === 'boolean') result[index] = value;
        });
        return Object.keys(result).length ? result : null;
    };
    const defaultColVis = () => saveViewColumnIndexes.reduce((result, index) => {
        result[index] = true;
        return result;
    }, {});
    const captureColVis = (api) => saveViewColumnIndexes.reduce((result, index) => {
        try { result[index] = !!api.column(index).visible(); } catch (error) { result[index] = true; }
        return result;
    }, {});
    const captureColOrder = (api) => {
        try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (error) { return null; }
    };
    const getSearchValue = (api) => {
        try { return api.table().container().querySelector('.dt-search input')?.value || api.search() || ''; }
        catch (error) { return ''; }
    };
    const getCurrentView = (api) => ({
        filters: normalizeFilters(appliedFilters),
        search: normalizeString(getSearchValue(api)),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });
    const normalizeView = (view) => ({
        filters: normalizeFilters(view?.filters),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const serializeView = (view) => JSON.stringify(normalizeView(view));
    const getSavedViewId = (view) => view?.id || view?.Id || view?._id || null;
    const getSavedViewName = (view) => view?.viewName || view?.ViewName || '';
    const getSavedViewDefinition = (view) => {
        const definition = view?.viewDefinition ?? view?.ViewDefinition ?? {};
        if (typeof definition !== 'string') return definition;
        try { return JSON.parse(definition); } catch (error) { return {}; }
    };
    const isSavedViewDefault = (view) => view?.isDefault === true || view?.IsDefault === true;

    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const response = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(response) ? response : (response?.data || response?.Data || []);
            defaultViewRecord = items.find(isSavedViewDefault) || items[0] || null;
            defaultViewState = defaultViewRecord ? normalizeView(getSavedViewDefinition(defaultViewRecord)) : null;
            return defaultViewState;
        } catch (error) {
            if (!error?.authHandled) console.error('[GlobalProducts SaveView] Load failed.', error);
            return null;
        }
    };

    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const normalized = normalizeView(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Default').trim(),
            viewDefinition: normalized,
            isDefault: true,
            visibility: 'private'
        };
        const id = getSavedViewId(defaultViewRecord);
        const response = id
            ? await personalizationClient.updateView(id, payload)
            : await personalizationClient.saveView(payload);
        defaultViewRecord = response?.data || response?.Data || response || payload;
        defaultViewState = normalized;
        return normalized;
    };

    const setSaveFilterVisible = (visible) => {
        document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => serializeView(getCurrentView(api)) !== serializeView(defaultViewState || {
        filters: { lifecycleStatus: '' }, search: '', colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index), order: baseOrder
    });
    const applyColumnState = (api, state) => {
        const columnOrder = normalizeColOrder(state?.columnOrder);
        if (columnOrder && typeof api?.colReorder?.order === 'function') api.colReorder.order(columnOrder, true);
        const colVis = normalizeColVis(state?.colVis);
        if (colVis) saveViewColumnIndexes.forEach((index) => api.column(index).visible(colVis[index], false));
    };
    const applySavedTableState = (api, state) => {
        const normalized = normalizeView(state || {});
        appliedFilters = normalized.filters;
        $('#filterLifecycleStatus').val(appliedFilters.lifecycleStatus).trigger('change.select2');
        applyColumnState(api, normalized);
        api.search(normalized.search);
        api.order(normalized.order);
    };
    const getResetBaselineState = () => normalizeView({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
        order: baseOrder
    });

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterButton = document.querySelector('.dt-filter-btn');
        const toolbar = filterButton?.closest('.dt-layout-row') || filterButton?.closest('.row');
        if (host && toolbar) {
            toolbar.insertAdjacentElement('afterend', host);
            host.classList.add('px-3');
        }
    };
    const initFilter = () => {
        const $filter = $('#filterLifecycleStatus');
        if ($filter.length && !$filter.hasClass('select2-hidden-accessible')) {
            $filter.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                minimumResultsForSearch: 8,
                width: 'element'
            });
        }
    };
    const getAppliedFilterCount = () => appliedFilters.lifecycleStatus ? 1 : 0;
    const toggleInlineFilter = () => {
        const element = document.getElementById('inlineFilterCollapse');
        if (element) bootstrap.Collapse.getOrCreateInstance(element, { toggle: false }).toggle();
    };
    const bindFilterEvents = () => {
        const collapse = document.getElementById('inlineFilterCollapse');
        collapse?.addEventListener('shown.bs.collapse', () => document.querySelector('.dt-filter-btn')?.setAttribute('aria-expanded', 'true'));
        collapse?.addEventListener('hidden.bs.collapse', () => document.querySelector('.dt-filter-btn')?.setAttribute('aria-expanded', 'false'));
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = normalizeFilters({ lifecycleStatus: $('#filterLifecycleStatus').val() });
            dt?.ajax.reload();
            bootstrap.Collapse.getOrCreateInstance(collapse, { toggle: false }).hide();
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            if (dt) {
                applySavedTableState(dt, getResetBaselineState());
                dt.ajax.reload();
                setSaveFilterVisible(isDirtyComparedToDefault(dt));
            }
        });
    };

    const lifecycleMap = () => ({
        Draft: { title: L.LifecycleDraft, class: 'bg-label-secondary' },
        1: { title: L.LifecycleDraft, class: 'bg-label-secondary' },
        PendingIdentityApproval: { title: L.LifecyclePendingIdentityApproval, class: 'bg-label-warning' },
        2: { title: L.LifecyclePendingIdentityApproval, class: 'bg-label-warning' },
        IdentityApproved: { title: L.LifecycleIdentityApproved, class: 'bg-label-success' },
        3: { title: L.LifecycleIdentityApproved, class: 'bg-label-success' },
        Retired: { title: L.LifecycleRetired, class: 'bg-label-danger' },
        4: { title: L.LifecycleRetired, class: 'bg-label-danger' }
    });
    const renderLifecycle = (value) => {
        const item = lifecycleMap()[value] || { title: value || L.Unknown, class: 'bg-label-secondary' };
        return `<span class="badge ${item.class}">${escapeHtml(item.title)}</span>`;
    };
    const lifecycleCode = (value) => ({
        Draft: 1, 1: 1,
        PendingIdentityApproval: 2, 2: 2,
        IdentityApproved: 3, 3: 3,
        Retired: 4, 4: 4
    })[value] || 0;
    const unwrapData = (payload) => payload?.data || payload?.Data || {};
    const getErrorMessage = async (response) => {
        let payload = {};
        try { payload = await response.json(); } catch (error) { }
        const errors = payload?.errors || payload?.Errors || [];
        const raw = Array.isArray(errors) ? errors.find(Boolean) : '';
        const domainMessages = {
            GLOBAL_PRODUCT_NAME_DUPLICATE: L.ErrorDuplicateName,
            CODE_RESERVATION_REQUIRED: L.ErrorReservationRequired
        };
        if (raw) return domainMessages[raw] || raw;
        return ({
            400: L.ErrorValidation,
            401: L.ErrorUnauthorized,
            403: L.ErrorForbidden,
            404: L.ErrorNotFound,
            409: L.ErrorConflict,
            503: L.ErrorServiceUnavailable,
            504: L.ErrorTimeout
        })[response.status] || L.ErrorGateway;
    };
    const handleUnauthorized = () => {
        window.DtDefaults?.handleUnauthorized?.();
        const error = new Error('auth-refresh-in-progress');
        error.authHandled = true;
        throw error;
    };

    const buildQuery = (data) => {
        const pageSize = Math.max(10, Math.min(Number(data.length) || 20, 100));
        const pageNumber = Math.floor((Number(data.start) || 0) / pageSize) + 1;
        const query = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(pageSize) });
        const search = normalizeString(data.search?.value);
        if (search) query.set('search', search);
        if (appliedFilters.lifecycleStatus) query.set('lifecycleStatus', appliedFilters.lifecycleStatus);
        return query.toString();
    };

    const fetchDetail = async (id) => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
            credentials: 'same-origin', headers: getAuthHeaders()
        });
        if (response.status === 401) handleUnauthorized();
        if (!response.ok) throw new Error(await getErrorMessage(response));
        return unwrapData(await response.json());
    };
    const renderDetail = (detail, expectedId) => {
        const detailId = detail?.id || detail?.Id;
        const detailVersion = Number(detail?.version ?? detail?.Version);
        const detailState = lifecycleCode(detail?.lifecycleStatus ?? detail?.LifecycleStatus);
        const offcanvas = document.getElementById('offcanvasDetailsPreview');
        if (!detailId || String(detailId).toLowerCase() !== String(expectedId).toLowerCase()
            || !Number.isInteger(detailVersion) || detailVersion < 0 || detailState === 0 || !offcanvas) {
            throw new Error(L.ErrorGateway);
        }
        const setText = (elementId, value) => {
            const element = document.getElementById(elementId);
            if (!element) throw new Error(L.ErrorGateway);
            element.textContent = value === null || value === undefined || value === '' ? L.NotAvailable : String(value);
        };
        setText('oc-title', detail.globalProductName || detail.GlobalProductName);
        setText('oc-subtitle', detail.canonicalCode || detail.CanonicalCode);
        setText('oc-id', detailId);
        setText('oc-code', detail.canonicalCode || detail.CanonicalCode);
        setText('oc-name', detail.globalProductName || detail.GlobalProductName);
        setText('oc-version', String(detail.version ?? detail.Version ?? ''));
        const formatDate = (value) => value ? new Intl.DateTimeFormat(document.documentElement.lang || undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : L.NotAvailable;
        setText('oc-created-at', formatDate(detail.createdAt || detail.CreatedAt));
        setText('oc-updated-at', formatDate(detail.updatedAt || detail.UpdatedAt));
        const status = detail.lifecycleStatus || detail.LifecycleStatus;
        const statusElement = document.getElementById('oc-status');
        if (!statusElement) throw new Error(L.ErrorGateway);
        statusElement.outerHTML = renderLifecycle(status).replace('<span ', '<span id="oc-status" ');
        bootstrap.Offcanvas.getOrCreateInstance(offcanvas).show();
        return detailState;
    };
    const populateDetails = async (id) => {
        try {
            const detail = await fetchDetail(id);
            renderDetail(detail, id);
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorOccurred, 'error');
        }
    };

    const lifecycleToken = () => document.querySelector(
        '#globalProductLifecycleToken input[name="__RequestVerificationToken"]')?.value || '';
    const setLifecycleBusy = (button, busy) => {
        button?.classList.toggle('disabled', busy);
        button?.setAttribute('aria-disabled', busy ? 'true' : 'false');
    };
    const postLifecycle = async (id, action, reasonCode, button) => {
        const requestKey = `${id}:${action}`;
        if (lifecycleRequests.has(requestKey)) return;
        lifecycleRequests.add(requestKey);
        setLifecycleBusy(button, true);
        try {
            const detail = await fetchDetail(id);
            const state = lifecycleCode(detail.lifecycleStatus ?? detail.LifecycleStatus);
            const expectedState = action === 'submit' ? 1 : 3;
            if (state !== expectedState) {
                dt?.ajax.reload(null, false);
                throw new Error(L.LifecycleStateChanged);
            }
            const expectedVersion = Number(detail.version ?? detail.Version);
            if (!Number.isInteger(expectedVersion) || expectedVersion < 0) throw new Error(L.ErrorConflict);

            const body = new FormData();
            body.set('ExpectedVersion', String(expectedVersion));
            if (action === 'retire') body.set('ReasonCode', reasonCode);
            const token = lifecycleToken();
            body.set('__RequestVerificationToken', token);
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${action}`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' },
                body
            });
            if (response.status === 401) handleUnauthorized();
            if (!response.ok) throw new Error(await getErrorMessage(response));

            dt?.ajax.reload(null, false);
            const refreshedDetail = await fetchDetail(id);
            const refreshedState = renderDetail(refreshedDetail, id);
            if (refreshedState !== (action === 'submit' ? 2 : 4)) throw new Error(L.LifecycleStateChanged);
            window.showToast?.(action === 'submit' ? L.SubmitPendingSuccess : L.RetireSuccess, 'success');
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
        } finally {
            lifecycleRequests.delete(requestKey);
            setLifecycleBusy(button, false);
        }
    };
    const requestLifecycle = (id, action, button) => {
        if (!id || (action === 'submit' && !canSubmit) || (action === 'retire' && !canRetire)) return;
        if (action === 'submit') {
            window.showConfirm?.(L.SubmitConfirmation, () => postLifecycle(id, action, '', button), {
                type: 'warning', confirmButtonText: L.SubmitIdentity
            });
            return;
        }
        window.showConfirm?.(L.RetireConfirmation, (input) => {
            const reason = normalizeString(input);
            if (!reason) {
                window.showToast?.(L.RetirementReasonRequired, 'error');
                return;
            }
            if (reason.length > 128) {
                window.showToast?.(L.RetirementReasonTooLong, 'error');
                return;
            }
            return postLifecycle(id, action, reason, button);
        }, {
            type: 'warning',
            showInput: true,
            inputRequired: true,
            inputLabel: L.RetirementReasonLabel,
            inputAttributes: { maxlength: 128 },
            confirmButtonText: L.RetireIdentity
        });
    };

    const renderActions = (row) => {
        const id = row.id || row.Id;
        const actions = [{
            key: 'details', className: 'js-quick-view', text: L.ViewDetails, icon: 'bx bx-show',
            attrs: { 'data-id': id, title: L.ViewDetails }
        }];
        const state = lifecycleCode(row.lifecycleStatus ?? row.LifecycleStatus);
        if (state === 1 && canSubmit) actions.push({
            key: 'submit', className: 'js-submit-identity', text: L.SubmitIdentity, icon: 'bx bx-send',
            attrs: { 'data-id': id, title: L.SubmitIdentity }
        });
        if (state === 3 && canRetire) actions.push({
            key: 'retire', className: 'js-retire-identity', text: L.RetireIdentity, icon: 'bx bx-archive',
            attrs: { 'data-id': id, title: L.RetireIdentity }
        });
        return window.DitenDataTable.renderActions(actions);
    };

    const openCreate = () => {
        const form = document.getElementById('formGlobalProduct');
        form?.reset();
        form?.classList.remove('was-validated');
        document.getElementById('formGlobalProductAlert')?.classList.add('d-none');
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        setTimeout(() => document.getElementById('globalProductName')?.focus(), 150);
    };
    const submitCreate = () => {
        const form = document.getElementById('formGlobalProduct');
        const name = normalizeString(document.getElementById('globalProductName')?.value);
        if (!form || !name || !form.checkValidity()) {
            form?.classList.add('was-validated');
            window.showToast?.(L.GlobalProductNameRequired, 'error');
            return;
        }

        window.showConfirm?.(L.CreateConfirmation, async () => {
            const button = document.getElementById('btnSaveGlobalProduct');
            if (button) button.disabled = true;
            try {
                const body = new FormData(form);
                body.set('GlobalProductName', name);
                const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                const response = await fetch(endpoint, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' },
                    body
                });
                if (response.status === 401) handleUnauthorized();
                if (!response.ok) throw new Error(await getErrorMessage(response));
                const payload = unwrapData(await response.json());
                const code = payload.canonicalCode || payload.CanonicalCode || '';
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                dt?.ajax.reload(null, false);
                window.showToast?.((L.CreateSuccessWithCode || L.RecordCreated).replace('{0}', code), 'success');
            } catch (error) {
                if (!error?.authHandled) window.showToast?.(error.message || L.ErrorOccurred, 'error');
            } finally {
                if (button) button.disabled = false;
            }
        }, { entityName: name, type: 'primary', confirmButtonText: L.Save });
    };

    const initDataTable = async () => {
        if (!tableEl || !window.DtDefaults) return;
        const savedState = await loadDefaultView();
        if (savedState) appliedFilters = savedState.filters;

        const extraButtons = {
            importBtn: { text: '<i class="icon-base bx bx-import icon-sm"></i>', className: 'btn btn-icon btn-label-secondary', attr: { title: L.Import, 'aria-label': L.Import, 'data-bs-toggle': 'tooltip' }, action: () => window.showToast?.(L.ComingSoon, 'warning') },
            filterBtn: { text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr: { title: L.Filter, 'aria-label': L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' }, action: toggleInlineFilter },
            saveFilterBtn: {
                text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L.SaveView)}</span>`,
                className: 'btn btn-label-primary dt-save-filter-btn d-none',
                attr: { title: L.SaveView, 'aria-label': L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async (event, api) => {
                    try {
                        await saveDefaultView(getCurrentView(api || dt));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved, 'success');
                    } catch (error) {
                        if (!error?.authHandled) window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }
            }
        };

        const config = window.DtDefaults.create({
            processing: true,
            serverSide: true,
            stateSave: false,
            order: savedState?.order || baseOrder,
            search: { search: savedState?.search || '' },
            colReorder: { columns: ':gt(0):not(:last-child)' },
            ajax: (data, callback) => {
                fetch(`${endpoint}?${buildQuery(data)}`, { credentials: 'same-origin', headers: getAuthHeaders() })
                    .then((response) => {
                        if (response.status === 401) handleUnauthorized();
                        if (!response.ok) return getErrorMessage(response).then((message) => Promise.reject(new Error(message)));
                        return response.json();
                    })
                    .then((payload) => {
                        const page = unwrapData(payload);
                        callback({ data: page.items || page.Items || [], recordsTotal: page.totalCount || page.TotalCount || 0, recordsFiltered: page.totalCount || page.TotalCount || 0 });
                    })
                    .catch((error) => {
                        if (!error?.authHandled) window.showToast?.(error.message || L.ErrorOccurred, 'error');
                        callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'canonicalCode', name: 'canonicalCode' },
                { data: 'globalProductName', name: 'globalProductName' },
                { data: 'lifecycleStatus', name: 'lifecycleStatus' },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { targets: 2, render: escapeHtml },
                { targets: 3, render: renderLifecycle },
                {
                    targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => renderActions(row)
                }
            ],
            buttons: window.DtDefaults.exportButtons(canCreate ? L.AddNew : null, {}, extraButtons, { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                initFilter();
                applySavedTableState(api, savedState || { filters: appliedFilters });
                document.querySelector('.add-new')?.addEventListener('click', (event) => { event.preventDefault(); openCreate(); });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () { window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount()); }
        });

        dt = new DataTable(tableEl, config);
        $(tableEl).on('column-reorder.dt columns-reordered.dt search.dt order.dt column-visibility.dt', () => {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const installSelectorInfrastructure = () => {
        window.DitenSelectors = window.DitenSelectors || {};
        window.DitenSelectors.globalProducts = {
            search: async (search, pageNumber = 1, pageSize = 20) => {
                const query = new URLSearchParams({ search: normalizeString(search), pageNumber: String(pageNumber), pageSize: String(pageSize) });
                const response = await fetch(`${endpoint}/selector?${query}`, { credentials: 'same-origin', headers: getAuthHeaders() });
                if (!response.ok) throw new Error(await getErrorMessage(response));
                return unwrapData(await response.json());
            }
        };
    };

    const bindEvents = () => {
        bindFilterEvents();
        document.getElementById('btnSaveGlobalProduct')?.addEventListener('click', submitCreate);
        document.addEventListener('click', (event) => {
            const quickViewAction = event.target.closest('.js-quick-view');
            const lifecycleAction = event.target.closest('.js-submit-identity, .js-retire-identity');
            const action = quickViewAction || lifecycleAction;
            if (!action || !action.closest('.datatables-globalproducts') || action.classList.contains('disabled')) return;
            event.preventDefault();
            if (action.classList.contains('js-submit-identity')) requestLifecycle(action.dataset.id, 'submit', action);
            else if (action.classList.contains('js-retire-identity')) requestLifecycle(action.dataset.id, 'retire', action);
            else populateDetails(action.dataset.id);
        });
    };

    return {
        init: async () => {
            installSelectorInfrastructure();
            bindEvents();
            await initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => GlobalProductsList.init());
