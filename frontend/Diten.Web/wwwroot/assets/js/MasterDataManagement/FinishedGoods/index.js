/**
 * MOD-0290 Finished Good Draft Foundation — tenant-shell Golden Slim list.
 * Browser traffic is restricted to the same-origin MVC proxy.
 */
'use strict';

const FinishedGoodsList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    const endpoint = '/MasterDataManagement/FinishedGoods/api';
    const tableEl = document.querySelector('.datatables-finishedgoods');
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'FinishedGoods' };
    const saveViewColumnIndexes = [1, 2, 3, 4];
    const totalColumnCount = 6;
    const baseOrder = [[1, 'asc']];
    const L = window.L10n || {};
    const permissionHost = document.querySelector('[data-can-create]');
    const canCreate = permissionHost?.getAttribute('data-can-create') === 'true';
    const canSubmit = permissionHost?.getAttribute('data-can-submit') === 'true';
    const canRetire = permissionHost?.getAttribute('data-can-retire') === 'true';
    const lifecycleRequests = new Set();
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const emptyFilters = () => ({ codeSearch: '' });
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const normalizeSearch = (value) => typeof value === 'string' ? value.trim().slice(0, 200) : '';
    const unwrapData = (payload) => payload?.data || payload?.Data || {};
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const normalized = order.map(Number).filter((index) =>
            Number.isInteger(index) && index >= 0 && index < totalColumnCount);
        return normalized.length === totalColumnCount && new Set(normalized).size === totalColumnCount
            ? normalized
            : null;
    };
    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const normalized = {};
        saveViewColumnIndexes.forEach((index, position) => {
            const value = Array.isArray(colVis) ? (colVis[index] ?? colVis[position]) : colVis[index];
            if (typeof value === 'boolean') normalized[index] = value;
        });
        return Object.keys(normalized).length ? normalized : null;
    };
    const defaultColVis = () => saveViewColumnIndexes.reduce((state, index) => {
        state[index] = true;
        return state;
    }, {});
    const normalizeView = (view) => ({
        filters: emptyFilters(),
        search: normalizeSearch(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder)
            || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const captureColVis = (api) => saveViewColumnIndexes.reduce((state, index) => {
        state[index] = !!api.column(index).visible();
        return state;
    }, {});
    const captureColOrder = (api) => {
        try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (error) { return null; }
    };
    const getSearchValue = (api) => {
        try { return normalizeSearch(api.search()); } catch (error) { return ''; }
    };
    const getCurrentView = (api) => normalizeView({
        search: getSearchValue(api),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });
    const serializeView = (view) => JSON.stringify(normalizeView(view));
    const getSavedViewDefinition = (view) => {
        const definition = view?.viewDefinition ?? view?.ViewDefinition ?? {};
        if (typeof definition !== 'string') return definition;
        try { return JSON.parse(definition); } catch (error) { return {}; }
    };
    const getSavedViewId = (view) => view?.id || view?.Id || view?._id || null;
    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return null;
        try {
            const response = await personalizationClient.getViews(
                personalizationContext.moduleKey,
                personalizationContext.pageKey);
            const items = Array.isArray(response) ? response : (response?.data || response?.Data || []);
            defaultViewRecord = items.find((item) => item?.isDefault === true || item?.IsDefault === true)
                || items[0]
                || null;
            defaultViewState = defaultViewRecord ? normalizeView(getSavedViewDefinition(defaultViewRecord)) : null;
            return defaultViewState;
        } catch (error) {
            if (!error?.authHandled) console.error('[FinishedGoods SaveView] Load failed.', error);
            return null;
        }
    };
    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const normalized = normalizeView(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (defaultViewRecord?.viewName || defaultViewRecord?.ViewName || L.SaveView || 'Default').trim(),
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
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
        order: baseOrder
    });
    const applySavedTableState = (api, view) => {
        const normalized = normalizeView(view || {});
        if (typeof api?.colReorder?.order === 'function') api.colReorder.order(normalized.columnOrder, true);
        saveViewColumnIndexes.forEach((index) => api.column(index).visible(normalized.colVis[index], false));
        api.search(normalized.search);
        api.order(normalized.order);
        const input = document.getElementById('filterCanonicalCode');
        if (input) input.value = normalized.search;
    };
    const getResetBaselineState = () => normalizeView({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
        order: baseOrder
    });

    const lifecycleMap = () => ({
        Draft: { code: 1, title: L.LifecycleDraft, class: 'bg-label-secondary' },
        1: { code: 1, title: L.LifecycleDraft, class: 'bg-label-secondary' },
        PendingIdentityApproval: { code: 2, title: L.LifecyclePendingIdentityApproval, class: 'bg-label-warning' },
        2: { code: 2, title: L.LifecyclePendingIdentityApproval, class: 'bg-label-warning' },
        IdentityApproved: { code: 3, title: L.LifecycleIdentityApproved, class: 'bg-label-success' },
        3: { code: 3, title: L.LifecycleIdentityApproved, class: 'bg-label-success' },
        Retired: { code: 4, title: L.LifecycleRetired, class: 'bg-label-danger' },
        4: { code: 4, title: L.LifecycleRetired, class: 'bg-label-danger' }
    });
    const lifecycleItem = value => lifecycleMap()[value] || null;
    const lifecycleCode = value => lifecycleItem(value)?.code ?? null;
    const renderLifecycle = (value) => {
        const item = lifecycleItem(value) || { title: L.Unknown, class: 'bg-label-secondary' };
        return `<span class="badge ${item.class}">${escapeHtml(item.title)}</span>`;
    };
    const handleUnauthorized = () => {
        window.DtDefaults?.handleUnauthorized?.();
        const error = new Error('auth-refresh-in-progress');
        error.authHandled = true;
        throw error;
    };
    const getErrorMessage = async (response) => {
        let payload = {};
        try { payload = await response.json(); } catch (error) { }
        const errors = payload?.errors || payload?.Errors || [];
        const raw = Array.isArray(errors) ? errors.find(Boolean) : '';
        if (raw === 'GSKU_NOT_REFERENCEABLE') return L.GskuNotReferenceable;
        return ({
            400: L.ErrorValidation,
            401: L.ErrorUnauthorized,
            403: L.ErrorForbidden,
            404: L.ErrorNotFound,
            409: L.ErrorConflict,
            500: L.ErrorGateway,
            502: L.ErrorGateway,
            503: L.ErrorServiceUnavailable,
            504: L.ErrorTimeout
        })[response.status] || L.ErrorGateway;
    };
    const buildQuery = (data) => {
        const pageSize = Math.max(1, Math.min(Number(data.length) || 20, 100));
        const pageNumber = Math.max(1, Math.min(
            Math.floor((Number(data.start) || 0) / pageSize) + 1,
            1000000));
        const query = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(pageSize) });
        const search = normalizeSearch(data.search?.value);
        if (search) query.set('search', search);
        return query.toString();
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const button = document.querySelector('.dt-filter-btn');
        const toolbar = button?.closest('.dt-layout-row') || button?.closest('.row');
        if (host && toolbar) {
            toolbar.insertAdjacentElement('afterend', host);
            host.classList.add('px-3');
        }
    };
    const toggleFilter = () => {
        const collapse = document.getElementById('inlineFilterCollapse');
        if (collapse) bootstrap.Collapse.getOrCreateInstance(collapse, { toggle: false }).toggle();
    };
    const bindFilter = () => {
        const collapse = document.getElementById('inlineFilterCollapse');
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            dt?.search(normalizeSearch(document.getElementById('filterCanonicalCode')?.value)).draw();
            if (saveFilterArmed && dt) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            if (collapse) bootstrap.Collapse.getOrCreateInstance(collapse, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            const input = document.getElementById('filterCanonicalCode');
            if (input) input.value = '';
            if (dt) {
                applySavedTableState(dt, getResetBaselineState());
                dt.draw();
                setSaveFilterVisible(isDirtyComparedToDefault(dt));
            }
        });
    };

    const fetchDetail = async id => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
            credentials: 'same-origin', headers: getAuthHeaders()
        });
        if (response.status === 401) handleUnauthorized();
        if (!response.ok) throw new Error(await getErrorMessage(response));
        return unwrapData(await response.json());
    };
    const valueOf = (source, camel, pascal) => source?.[camel] ?? source?.[pascal];
    const validateDetail = (detail, expectedId) => {
        const detailId = String(valueOf(detail, 'id', 'Id') || '').toLowerCase();
        const state = lifecycleCode(valueOf(detail, 'lifecycleStatus', 'LifecycleStatus'));
        const version = Number(valueOf(detail, 'version', 'Version'));
        if (!detailId || detailId !== String(expectedId || '').toLowerCase()
            || state === null || !Number.isInteger(version) || version < 0) {
            throw new Error(L.ErrorGateway);
        }
        return { state, version };
    };
    const renderDetail = (detail, expectedId) => {
        const verified = validateDetail(detail, expectedId);
        const setText = (elementId, fieldValue) => {
            const element = document.getElementById(elementId);
            if (!element) throw new Error(L.ErrorGateway);
            element.textContent = fieldValue === null || fieldValue === undefined || fieldValue === ''
                ? L.NotAvailable : String(fieldValue);
        };
        const code = valueOf(detail, 'canonicalCode', 'CanonicalCode');
        setText('oc-title', code);
        setText('oc-subtitle', valueOf(detail, 'gskuCanonicalCode', 'GskuCanonicalCode'));
        setText('oc-id', valueOf(detail, 'id', 'Id'));
        setText('oc-code', code);
        setText('oc-gsku-code', valueOf(detail, 'gskuCanonicalCode', 'GskuCanonicalCode'));
        setText('oc-version', verified.version);
        const formatDate = dateValue => dateValue
            ? new Intl.DateTimeFormat(document.documentElement.lang || undefined, {
                dateStyle: 'medium', timeStyle: 'short'
            }).format(new Date(dateValue))
            : L.NotAvailable;
        setText('oc-created-at', formatDate(valueOf(detail, 'createdAt', 'CreatedAt')));
        setText('oc-updated-at', formatDate(valueOf(detail, 'updatedAt', 'UpdatedAt')));
        const statusElement = document.getElementById('oc-status');
        if (!statusElement) throw new Error(L.ErrorGateway);
        statusElement.outerHTML = renderLifecycle(valueOf(detail, 'lifecycleStatus', 'LifecycleStatus'))
            .replace('<span ', '<span id="oc-status" ');
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
        return verified.state;
    };
    const populateDetails = async id => {
        try { renderDetail(await fetchDetail(id), id); }
        catch (error) { if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error'); }
    };

    const lifecycleToken = () => document.querySelector(
        '#finishedGoodLifecycleToken input[name="__RequestVerificationToken"]')?.value || '';
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
            const verified = validateDetail(detail, id);
            const expectedState = action === 'submit' ? 1 : 3;
            if (verified.state !== expectedState) {
                dt?.ajax.reload(null, false);
                throw new Error(L.LifecycleStateChanged);
            }

            const body = new FormData();
            body.set('ExpectedVersion', String(verified.version));
            if (action === 'retire') body.set('ReasonCode', reasonCode);
            const token = lifecycleToken();
            body.set('__RequestVerificationToken', token);
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${action}`, {
                method: 'POST', credentials: 'same-origin',
                headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' }, body
            });
            if (response.status === 401) handleUnauthorized();
            if (!response.ok) throw new Error(await getErrorMessage(response));
            const payload = await response.json().catch(() => null);
            const responseStatus = Number(payload?.statusCode ?? payload?.StatusCode);
            if (payload?.isSuccessful !== true && payload?.IsSuccessful !== true) throw new Error(L.ErrorGateway);
            if (responseStatus !== response.status
                || (action === 'retire' && response.status !== 200)
                || (action === 'submit' && response.status !== 200 && response.status !== 202)) {
                throw new Error(L.ErrorGateway);
            }

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
        window.showConfirm?.(L.RetireConfirmation, input => {
            const reason = normalizeSearch(input);
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
            type: 'warning', showInput: true, inputRequired: true,
            inputLabel: L.RetirementReasonLabel,
            inputAttributes: { maxlength: '128', rows: '3' },
            confirmButtonText: L.RetireIdentity
        });
    };
    const renderActions = row => {
        const id = row.id || row.Id;
        const actions = [{
            key: 'details', className: 'js-quick-view', text: L.QuickView, icon: 'bx bx-show',
            attrs: { 'data-id': id, title: L.QuickView }
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

    const initGskuSelector = () => {
        const $selector = $('#gskuId');
        if (!$selector.length || $selector.hasClass('select2-hidden-accessible')) return;
        $selector.select2({
            dropdownParent: $('#offcanvasCreateEdit'),
            width: '100%',
            allowClear: true,
            minimumInputLength: 0,
            ajax: {
                delay: 250,
                transport: (params, success, failure) => {
                    const search = normalizeSearch(params.data?.term || '');
                    const pageNumber = Math.max(1, Math.min(Number(params.data?.page) || 1, 1000000));
                    const query = new URLSearchParams({
                        pageNumber: String(pageNumber),
                        pageSize: '20'
                    });
                    if (search) query.set('search', search);
                    const controller = new AbortController();
                    fetch(`${endpoint}/gsku-selector?${query}`, {
                        credentials: 'same-origin',
                        headers: getAuthHeaders(),
                        signal: controller.signal
                    })
                        .then(async (response) => {
                            if (response.status === 401) handleUnauthorized();
                            if (!response.ok) throw new Error(await getErrorMessage(response));
                            return response.json();
                        })
                        .then(success)
                        .catch((error) => {
                            if (error.name !== 'AbortError') failure(error);
                        });
                    return { abort: () => controller.abort() };
                },
                processResults: (payload, params) => {
                    const page = unwrapData(payload);
                    const items = page.items || page.Items || [];
                    const pageNumber = Number(params.page) || 1;
                    const pageSize = Number(page.pageSize || page.PageSize) || 20;
                    const totalCount = Number(page.totalCount || page.TotalCount) || 0;
                    return {
                        results: items.map((item) => ({
                            id: item.id || item.Id,
                            text: item.gskuCanonicalCode || item.GskuCanonicalCode
                        })),
                        pagination: { more: pageNumber * pageSize < totalCount }
                    };
                }
            }
        });
    };

    const openCreate = () => {
        const form = document.getElementById('formFinishedGood');
        form?.reset();
        form?.classList.remove('was-validated');
        $('#gskuId').val(null).trigger('change');
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        initGskuSelector();
    };
    const submitCreate = () => {
        const form = document.getElementById('formFinishedGood');
        const gskuId = $('#gskuId').val();
        if (!form || !gskuId || !form.checkValidity()) {
            form?.classList.add('was-validated');
            window.showToast?.(L.GskuRequired, 'error');
            return;
        }

        const selectedText = $('#gskuId option:selected').text() || String(gskuId);
        window.showConfirm?.(L.CreateConfirmation, async () => {
            const button = document.getElementById('btnSaveFinishedGood');
            if (button) button.disabled = true;
            try {
                const body = new FormData();
                body.set('GskuId', String(gskuId));
                const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                body.set('__RequestVerificationToken', token);
                const response = await fetch(endpoint, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' },
                    body
                });
                if (response.status === 401) handleUnauthorized();
                if (response.status !== 201 && response.status !== 202) {
                    throw new Error(await getErrorMessage(response));
                }
                const draft = unwrapData(await response.json());
                const code = draft.canonicalCode || draft.CanonicalCode || '';
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                dt?.ajax.reload(null, false);
                if (response.status === 202) {
                    const message = code
                        ? (L.CreatePendingWithCode || '').replace('{0}', code)
                        : L.CreatePending;
                    window.showToast?.(message, 'warning');
                } else {
                    window.showToast?.((L.CreateSuccessWithCode || '').replace('{0}', code), 'success');
                }
            } catch (error) {
                if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            } finally {
                if (button) button.disabled = false;
            }
        }, { entityName: selectedText, type: 'primary', confirmButtonText: L.Save });
    };

    const initDataTable = async () => {
        if (!tableEl || !window.DtDefaults) return;
        const savedState = await loadDefaultView();
        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn',
                attr: { title: L.Filter, 'aria-label': L.Filter, 'aria-controls': 'inlineFilterCollapse' },
                action: toggleFilter
            },
            saveFilterBtn: {
                text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L.SaveView)}</span>`,
                className: 'btn btn-label-primary dt-save-filter-btn d-none',
                attr: { title: L.SaveView, 'aria-label': L.SaveView },
                action: async (event, api) => {
                    try {
                        await saveDefaultView(getCurrentView(api || dt));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved, 'success');
                    } catch (error) {
                        if (!error?.authHandled) window.showToast?.(L.ErrorGateway, 'error');
                    }
                }
            }
        };
        const config = window.DtDefaults.create({
            processing: true,
            serverSide: true,
            stateSave: false,
            pageLength: 20,
            order: savedState?.order || baseOrder,
            search: { search: savedState?.search || '' },
            colReorder: { columns: ':gt(0):not(:last-child)' },
            ajax: (data, callback) => {
                fetch(`${endpoint}?${buildQuery(data)}`, { credentials: 'same-origin', headers: getAuthHeaders() })
                    .then(async (response) => {
                        if (response.status === 401) handleUnauthorized();
                        if (!response.ok) throw new Error(await getErrorMessage(response));
                        return response.json();
                    })
                    .then((payload) => {
                        const page = unwrapData(payload);
                        const count = page.totalCount || page.TotalCount || 0;
                        callback({ data: page.items || page.Items || [], recordsTotal: count, recordsFiltered: count });
                    })
                    .catch((error) => {
                        if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
                        callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'canonicalCode', name: 'canonicalCode' },
                { data: 'gskuCanonicalCode', name: 'gskuCanonicalCode' },
                { data: 'lifecycleStatus', name: 'lifecycleStatus' },
                { data: 'createdAt', name: 'createdAt' },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { targets: 2, render: escapeHtml },
                { targets: 3, render: renderLifecycle },
                {
                    targets: 4,
                    render: (data) => data
                        ? escapeHtml(new Intl.DateTimeFormat(document.documentElement.lang || undefined, {
                            dateStyle: 'medium', timeStyle: 'short'
                        }).format(new Date(data)))
                        : escapeHtml(L.NotAvailable)
                },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => renderActions(row)
                }
            ],
            buttons: window.DtDefaults.exportButtons(canCreate ? L.AddNew : null, {}, extraButtons, {
                exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                applySavedTableState(api, savedState || {});
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    openCreate();
                });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () { window.DtDefaults.updateVisualState(this.api(), 0); }
        });
        dt = new DataTable(tableEl, config);
        $(tableEl).on('column-reorder.dt columns-reordered.dt search.dt order.dt column-visibility.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const bindEvents = () => {
        bindFilter();
        document.getElementById('btnSaveFinishedGood')?.addEventListener('click', submitCreate);
        document.addEventListener('click', (event) => {
            const quickView = event.target.closest('.js-quick-view');
            const lifecycleAction = event.target.closest('.js-submit-identity, .js-retire-identity');
            const action = quickView || lifecycleAction;
            if (!action || !action.closest('.datatables-finishedgoods') || action.classList.contains('disabled')) return;
            event.preventDefault();
            if (action.classList.contains('js-submit-identity')) requestLifecycle(action.dataset.id, 'submit', action);
            else if (action.classList.contains('js-retire-identity')) requestLifecycle(action.dataset.id, 'retire', action);
            else populateDetails(action.dataset.id);
        });
    };

    return {
        init: async () => {
            bindEvents();
            await initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => FinishedGoodsList.init());
