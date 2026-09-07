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
    let initialPagePrefetch = null;

    const endpoint = '/MasterDataManagement/GlobalProducts/api';
    const tableEl = document.querySelector('.datatables-globalproducts');
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'GlobalProducts' };
    const saveViewColumnIndexes = [1, 2, 3];
    const totalColumnCount = 5;
    const baseOrder = [[1, 'asc']];
    const initialPageSize = 10;
    const L = window.L10n || {};
    const permissionHost = document.querySelector('[data-can-create]');
    const canCreate = permissionHost?.getAttribute('data-can-create') === 'true';
    const lifecycleRequests = new Set();
    const detailById = new Map();
    const actionOrder = ['DETAILS', 'EDIT', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT'];
    let editorMode = 'create';
    let editorId = '';
    let editorVersion = null;
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const emptyFilters = () => ({ lifecycleStatus: '' });

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const normalizeString = (value) => typeof value === 'string' ? value.trim() : '';
    const normalizeFilters = (filters) => ({ lifecycleStatus: normalizeString(filters?.lifecycleStatus) });
    const syncSingleFilterState = ($filter) => {
        const hasValue = Boolean(normalizeString($filter.val()));
        $filter.next('.select2-container').find('.select2-selection__rendered')
            .toggleClass('text-body fw-semibold', hasValue)
            .toggleClass('text-secondary', !hasValue);
    };
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
        const $filter = $('#filterLifecycleStatus');
        $filter.val(appliedFilters.lifecycleStatus).trigger('change.select2');
        syncSingleFilterState($filter);
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
        if (!window.jQuery || !$.fn.select2) return;
        const $filter = $('#filterLifecycleStatus');
        if ($filter.length) {
            if ($filter.hasClass('select2-hidden-accessible')) $filter.select2('destroy');

            const clampDropdown = () => {
                requestAnimationFrame(() => {
                    const dropdown = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
                    if (!dropdown) return;
                    const rect = dropdown.getBoundingClientRect();
                    const padding = 8;
                    let deltaX = 0;
                    let deltaY = 0;
                    if (rect.right > window.innerWidth - padding) deltaX -= rect.right - (window.innerWidth - padding);
                    if (rect.left < padding) deltaX += padding - rect.left;
                    if (rect.bottom > window.innerHeight - padding) deltaY -= rect.bottom - (window.innerHeight - padding);
                    if (rect.top < padding) deltaY += padding - rect.top;
                    if (!deltaX && !deltaY) return;
                    const computedStyle = window.getComputedStyle(dropdown);
                    const baseLeft = parseFloat(computedStyle.left) || rect.left + window.scrollX;
                    const baseTop = parseFloat(computedStyle.top) || rect.top + window.scrollY;
                    if (deltaX) dropdown.style.left = `${baseLeft + deltaX}px`;
                    if (deltaY) dropdown.style.top = `${baseTop + deltaY}px`;
                    dropdown.style.transform = 'none';
                });
            };

            $filter.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-single',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $filter.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                allowClear: true
            });
            $filter.on('select2:open', clampDropdown);
            $filter.off('change.globalProductsFilterState')
                .on('change.globalProductsFilterState', () => syncSingleFilterState($filter));
            requestAnimationFrame(() => syncSingleFilterState($filter));
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

    const fetchPage = async (query, signal) => {
        const response = await fetch(`${endpoint}?${query}`, {
            credentials: 'same-origin',
            headers: getAuthHeaders(),
            signal
        });
        if (response.status === 401) handleUnauthorized();
        if (!response.ok) throw new Error(await getErrorMessage(response));
        return unwrapData(await response.json());
    };
    const settlePageRequest = (request) => request.then(
        (page) => ({ page, error: null }),
        (error) => ({ page: null, error })
    );
    const startInitialPagePrefetch = () => {
        const query = new URLSearchParams({
            pageNumber: '1',
            pageSize: String(initialPageSize)
        }).toString();
        const controller = new AbortController();
        return {
            query,
            controller,
            result: settlePageRequest(fetchPage(query, controller.signal))
        };
    };
    const consumePage = async (query) => {
        const prefetched = initialPagePrefetch;
        initialPagePrefetch = null;

        let result;
        if (prefetched?.query === query) {
            result = await prefetched.result;
        } else {
            prefetched?.controller.abort();
            result = await settlePageRequest(fetchPage(query));
        }

        if (result.error) throw result.error;
        return result.page;
    };

    const fetchDetail = async (id) => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
            credentials: 'same-origin', headers: getAuthHeaders()
        });
        if (response.status === 401) handleUnauthorized();
        if (!response.ok) throw new Error(await getErrorMessage(response));
        return unwrapData(await response.json());
    };
    const readAvailableActions = (detail) => {
        const raw = detail?.availableActions ?? detail?.AvailableActions;
        if (!Array.isArray(raw) || !raw.length) throw new Error(L.ErrorGateway);
        const actions = raw.map((value) => typeof value === 'string' ? value.trim().toUpperCase() : '');
        if (actions.some((value) => !actionOrder.includes(value)) || new Set(actions).size !== actions.length
            || actions[0] !== 'DETAILS'
            || actions.some((value, index) => index > 0 && actionOrder.indexOf(value) <= actionOrder.indexOf(actions[index - 1]))) {
            throw new Error(L.ErrorGateway);
        }
        return actions;
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
    const lifecycleToken = () => document.querySelector(
        '#globalProductLifecycleToken input[name="__RequestVerificationToken"]')?.value || '';
    const setLifecycleBusy = (button, busy) => {
        button?.classList.toggle('disabled', busy);
        button?.setAttribute('aria-disabled', busy ? 'true' : 'false');
    };
    const scalarLength = (value) => Array.from(value).length;
    const hasControlCharacter = (value) => /[\u0000-\u001F\u007F-\u009F]/u.test(value);
    const mutationDefinition = (action) => ({
        SUBMIT: { path: 'submit', method: 'POST', success: L.SubmitPendingSuccess },
        WITHDRAW_APPROVAL: { path: 'withdraw', method: 'POST', success: L.WithdrawSuccess },
        REQUEST_CORRECTION: { path: 'correction-requests', method: 'POST', success: L.CorrectionRequestedSuccess },
        REQUEST_RETIREMENT: { path: 'retirement-requests', method: 'POST', success: L.RetirementRequestedSuccess },
        EDIT: { path: '', method: 'PUT', success: L.UpdateSuccess }
    }[action]);
    const validMutationEnvelope = (payload, responseStatus, action, expectedId) => {
        const successful = payload?.isSuccessful ?? payload?.IsSuccessful;
        const status = Number(payload?.statusCode ?? payload?.StatusCode);
        const data = payload?.data ?? payload?.Data;
        if (successful !== true || status !== responseStatus || !data) return false;

        const id = data?.globalProductId ?? data?.GlobalProductId ?? data?.id ?? data?.Id;
        const operationId = data?.operationId ?? data?.OperationId;
        const checkpoint = data?.checkpoint ?? data?.Checkpoint;
        const lifecycleStatus = data?.lifecycleStatus ?? data?.LifecycleStatus;
        const version = Number(data?.version ?? data?.Version);
        const productVersion = Number(data?.productVersion ?? data?.ProductVersion);
        const idMatches = typeof id === 'string'
            && id.toLowerCase() === String(expectedId).toLowerCase();
        const hasOperation = typeof operationId === 'string' && operationId.length > 0;
        const hasCheckpoint = typeof checkpoint === 'string' && checkpoint.length > 0;
        const hasLifecycle = typeof lifecycleStatus === 'string' && lifecycleStatus.length > 0
            || Number.isInteger(Number(lifecycleStatus)) && Number(lifecycleStatus) > 0;

        if (action === 'SUBMIT') return idMatches && hasOperation && hasCheckpoint;
        if (action === 'EDIT') return idMatches && hasLifecycle && Number.isInteger(version) && version >= 0;
        if (action === 'WITHDRAW_APPROVAL') return idMatches && hasLifecycle && Number.isInteger(version) && version >= 0;
        if (action === 'REQUEST_CORRECTION' || action === 'REQUEST_RETIREMENT') {
            return idMatches && hasOperation && hasCheckpoint
                && Number.isInteger(productVersion) && productVersion >= 0
                && (responseStatus === 200 && checkpoint === 'Completed'
                    || responseStatus === 202 && checkpoint === 'AwaitingDecision');
        }
        return false;
    };
    const mutationReadbackIsCoherent = (beforeVersion, detail, action, fields, responseStatus, payload) => {
        const version = Number(detail?.version ?? detail?.Version);
        const state = lifecycleCode(detail?.lifecycleStatus ?? detail?.LifecycleStatus);
        const actions = readAvailableActions(detail);
        if (!Number.isInteger(version) || version <= beforeVersion) return false;
        if (action === 'EDIT') {
            return state === 1
                && normalizeString(detail?.globalProductName ?? detail?.GlobalProductName)
                    === normalizeString(fields?.GlobalProductName);
        }
        if (action === 'SUBMIT') return state === 2 && !actions.includes('SUBMIT');
        if (action === 'WITHDRAW_APPROVAL') return state === 1 && !actions.includes('WITHDRAW_APPROVAL');
        if (action !== 'REQUEST_CORRECTION' && action !== 'REQUEST_RETIREMENT') return false;

        const data = payload?.data ?? payload?.Data;
        const productVersion = Number(data?.productVersion ?? data?.ProductVersion);
        if (!Number.isInteger(productVersion) || version !== productVersion) return false;
        if (responseStatus === 202) {
            return state === 3 && !actions.includes('REQUEST_CORRECTION')
                && !actions.includes('REQUEST_RETIREMENT');
        }
        if (action === 'REQUEST_CORRECTION') {
            return state === 3 && actions.includes('REQUEST_CORRECTION');
        }
        return state === 4 || state === 3 && actions.includes('REQUEST_RETIREMENT');
    };
    const mutationReadbackProvesCommitted = (beforeVersion, detail, action, fields) => {
        if (action === 'REQUEST_CORRECTION' || action === 'REQUEST_RETIREMENT') return false;
        return mutationReadbackIsCoherent(beforeVersion, detail, action, fields, 0, null);
    };
    const postLifecycle = async (id, action, fields, button, expectedVersionOverride = null) => {
        const definition = mutationDefinition(action);
        const requestKey = `${id}:${action}`;
        if (!definition) return;
        if (lifecycleRequests.has(requestKey)) return;
        lifecycleRequests.add(requestKey);
        setLifecycleBusy(button, true);
        try {
            const detail = await fetchDetail(id);
            if (!readAvailableActions(detail).includes(action)) {
                dt?.ajax.reload(null, false);
                throw new Error(L.LifecycleStateChanged);
            }
            const expectedVersion = Number(detail.version ?? detail.Version);
            if (!Number.isInteger(expectedVersion) || expectedVersion < 0) throw new Error(L.ErrorConflict);
            if (Number.isInteger(expectedVersionOverride) && expectedVersion !== expectedVersionOverride)
                throw new Error(L.LifecycleStateChanged);

            const body = new FormData();
            body.set('ExpectedVersion', String(expectedVersion));
            Object.entries(fields || {}).forEach(([key, value]) => body.set(key, value));
            const token = lifecycleToken();
            body.set('__RequestVerificationToken', token);
            const suffix = definition.path ? `/${definition.path}` : '';
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}${suffix}`, {
                method: definition.method,
                credentials: 'same-origin',
                headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' },
                body
            });
            if (response.status === 401) handleUnauthorized();
            let payload = null;
            let responseError = null;
            if (!response.ok) responseError = new Error(await getErrorMessage(response));
            else {
                try { payload = await response.json(); }
                catch (error) { responseError = new Error(L.ErrorGateway); }
            }

            // A proxy/upstream contract error can follow an already-committed mutation. Always
            // reconcile the visible row and fresh server-owned actions before reporting it.
            dt?.ajax.reload(null, false);
            const refreshedDetail = await fetchDetail(id);
            detailById.set(String(id).toLowerCase(), refreshedDetail);
            const envelopeValid = !responseError
                && validMutationEnvelope(payload, response.status, action, id);
            const coherent = envelopeValid
                && mutationReadbackIsCoherent(
                    expectedVersion, refreshedDetail, action, fields, response.status, payload);
            const reconciledCommit = !envelopeValid
                && (response.ok || [502, 503, 504].includes(response.status))
                && mutationReadbackProvesCommitted(expectedVersion, refreshedDetail, action, fields);
            if (reconciledCommit) {
                window.showToast?.(definition.success, 'success');
                return true;
            }
            if (responseError) throw responseError;
            if (!envelopeValid || !coherent) throw new Error(L.ErrorGateway);
            window.showToast?.(definition.success, 'success');
            return true;
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            return false;
        } finally {
            lifecycleRequests.delete(requestKey);
            setLifecycleBusy(button, false);
        }
    };
    const requestLifecycle = (id, action, button) => {
        if (!id) return;
        if (action === 'SUBMIT') {
            window.showConfirm?.(L.SubmitConfirmation, () => postLifecycle(id, action, {}, button), {
                type: 'warning', confirmButtonText: L.SubmitIdentity
            });
            return;
        }
        if (action === 'WITHDRAW_APPROVAL') {
            window.showConfirm?.(L.WithdrawConfirmation, () => postLifecycle(id, action, {}, button), {
                type: 'warning', confirmButtonText: L.WithdrawApproval
            });
            return;
        }
        if (action === 'REQUEST_CORRECTION') {
            window.showConfirm?.(L.CorrectionConfirmation, (input) => {
                const name = normalizeString(input);
                if (!name || scalarLength(name) > 200) return;
                return postLifecycle(id, action, { GlobalProductName: name }, button);
            }, {
                type: 'primary', showInput: true, inputType: 'text', inputRequired: true,
                inputLabel: L.CorrectionNameLabel, inputAttributes: { maxlength: 400 },
                confirmButtonText: L.RequestCorrection
            });
            return;
        }
        window.showConfirm?.(L.RetirementRequestConfirmation, (input) => {
            const reason = normalizeString(input);
            if (!reason || scalarLength(reason) > 2000) {
                window.showToast?.(L.RetirementRequestReasonRequired, 'error');
                return;
            }
            if (hasControlCharacter(reason)) {
                window.showToast?.(L.RetirementRequestReasonInvalid, 'error');
                return;
            }
            return postLifecycle(id, action, { Reason: reason }, button);
        }, {
            type: 'warning', showInput: true, inputRequired: true,
            inputLabel: L.RetirementRequestReasonLabel,
            inputAttributes: { maxlength: 4000 }, confirmButtonText: L.RequestRetirement
        });
    };

    const renderActions = (row) => {
        const id = row.id || row.Id;
        return `<div class="dropdown"><button type="button" class="btn btn-icon dropdown-toggle hide-arrow js-global-product-actions-toggle" data-id="${escapeHtml(id)}" aria-expanded="false" title="${escapeHtml(L.Actions)}" aria-label="${escapeHtml(L.Actions)}"><i class="bx bx-dots-vertical-rounded icon-md"></i></button><div class="dropdown-menu dropdown-menu-end m-0 js-global-product-actions-menu"></div></div>`;
    };

    const actionPresentation = {
        DETAILS: ['js-quick-view', 'bx bx-show', () => L.ViewDetails],
        EDIT: ['js-edit-draft', 'bx bx-edit', () => L.EditDraft],
        SUBMIT: ['js-lifecycle-action', 'bx bx-send', () => L.SubmitIdentity],
        WITHDRAW_APPROVAL: ['js-lifecycle-action', 'bx bx-undo', () => L.WithdrawApproval],
        REQUEST_CORRECTION: ['js-lifecycle-action', 'bx bx-edit-alt', () => L.RequestCorrection],
        REQUEST_RETIREMENT: ['js-lifecycle-action', 'bx bx-archive', () => L.RequestRetirement]
    };
    const renderFreshActionMenu = (menu, id, actions) => {
        menu.innerHTML = actions.map((code) => {
            const [className, icon, label] = actionPresentation[code];
            return `<a href="javascript:void(0);" class="dropdown-item dt-action-item ${className}" data-id="${escapeHtml(id)}" data-action="${code}"><i class="${icon} dt-action-icon"></i>${escapeHtml(label())}</a>`;
        }).join('');
    };
    const loadActionMenu = async (toggle) => {
        const id = toggle?.dataset.id;
        const menu = toggle?.parentElement?.querySelector('.js-global-product-actions-menu');
        if (!id || !menu || toggle.classList.contains('disabled')) return;
        toggle.classList.add('disabled');
        try {
            const detail = await fetchDetail(id);
            const actions = readAvailableActions(detail);
            detailById.set(String(id).toLowerCase(), detail);
            renderFreshActionMenu(menu, id, actions);
            bootstrap.Dropdown.getOrCreateInstance(toggle).show();
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorOccurred, 'error');
        } finally {
            toggle.classList.remove('disabled');
        }
    };

    const openCreate = () => {
        editorMode = 'create'; editorId = ''; editorVersion = null;
        const form = document.getElementById('formGlobalProduct');
        form?.reset();
        form?.classList.remove('was-validated');
        document.getElementById('formGlobalProductAlert')?.classList.add('d-none');
        document.getElementById('offcanvasCreateEditLabel').textContent = L.FormTitleCreate;
        document.getElementById('btnSaveGlobalProduct').textContent = L.Save;
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        setTimeout(() => document.getElementById('globalProductName')?.focus(), 150);
    };
    const openEdit = async (id, button) => {
        setLifecycleBusy(button, true);
        try {
            const detail = await fetchDetail(id);
            if (!readAvailableActions(detail).includes('EDIT')) throw new Error(L.LifecycleStateChanged);
            editorMode = 'edit'; editorId = id;
            editorVersion = Number(detail.version ?? detail.Version);
            if (!Number.isInteger(editorVersion) || editorVersion < 0) throw new Error(L.ErrorConflict);
            const form = document.getElementById('formGlobalProduct');
            form?.classList.remove('was-validated');
            document.getElementById('globalProductName').value = detail.globalProductName ?? detail.GlobalProductName ?? '';
            document.getElementById('offcanvasCreateEditLabel').textContent = L.FormTitleEdit;
            document.getElementById('btnSaveGlobalProduct').textContent = L.UpdateDraft;
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorOccurred, 'error');
        } finally { setLifecycleBusy(button, false); }
    };
    const submitEditor = () => {
        const form = document.getElementById('formGlobalProduct');
        const name = normalizeString(document.getElementById('globalProductName')?.value);
        if (!form || !name || scalarLength(name) > 200 || !form.checkValidity()) {
            form?.classList.add('was-validated');
            window.showToast?.(L.GlobalProductNameRequired, 'error');
            return;
        }

        if (editorMode === 'edit') {
            window.showConfirm?.(L.UpdateConfirmation, async () => {
                const button = document.getElementById('btnSaveGlobalProduct');
                if (button) button.disabled = true;
                try {
                    const saved = await postLifecycle(editorId, 'EDIT', { GlobalProductName: name }, button, editorVersion);
                    if (saved) bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                } finally { if (button) button.disabled = false; }
            }, { entityName: name, type: 'primary', confirmButtonText: L.UpdateDraft });
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
        // Keep saved-view restoration authoritative, but overlap its Gateway trip with the
        // first default page. The prefetched payload is consumed only when the final query
        // is identical; a saved search/filter always performs its own exact request.
        initialPagePrefetch = startInitialPagePrefetch();
        const savedState = await loadDefaultView();
        if (savedState) appliedFilters = savedState.filters;

        const extraButtons = {
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

        const buttons = window.DtDefaults.exportButtons(
            canCreate ? L.AddNew : null,
            {},
            extraButtons,
            { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes });
        const exportCollection = buttons.find((button) => button?.extend === 'collection');
        exportCollection?.buttons?.forEach((button) => {
            if (!['print', 'csv', 'excel', 'pdf', 'copy'].includes(button?.extend)) return;
            button.text = `${button.text}<small class="d-block text-muted">${escapeHtml(L.CurrentPageOnly)}</small>`;
            button.titleAttr = L.CurrentPageOnly;
        });

        const config = window.DtDefaults.create({
            processing: true,
            serverSide: true,
            deferRender: true,
            stateSave: false,
            pageLength: initialPageSize,
            order: savedState?.order || baseOrder,
            search: { search: savedState?.search || '' },
            colReorder: { columns: ':gt(0):not(:last-child)' },
            ajax: (data, callback) => {
                consumePage(buildQuery(data))
                    .then((page) => {
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
            buttons,
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
        document.getElementById('btnSaveGlobalProduct')?.addEventListener('click', submitEditor);
        document.addEventListener('click', (event) => {
            const toggle = event.target.closest('.js-global-product-actions-toggle');
            if (toggle?.closest('.datatables-globalproducts')) {
                event.preventDefault(); event.stopPropagation();
                loadActionMenu(toggle);
                return;
            }
            const quickViewAction = event.target.closest('.js-quick-view');
            const action = quickViewAction || event.target.closest('.js-edit-draft, .js-lifecycle-action');
            if (!action || !action.closest('.datatables-globalproducts') || action.classList.contains('disabled')) return;
            event.preventDefault();
            const id = action.dataset.id;
            const code = action.dataset.action;
            if (code === 'DETAILS') {
                const detail = detailById.get(String(id).toLowerCase());
                if (detail) renderDetail(detail, id);
                else window.showToast?.(L.LifecycleStateChanged, 'warning');
            } else if (code === 'EDIT') openEdit(id, action);
            else requestLifecycle(id, code, action);
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
