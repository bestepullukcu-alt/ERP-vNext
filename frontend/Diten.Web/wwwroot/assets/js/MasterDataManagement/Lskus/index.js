/* MOD-0290 LSKU Golden Slim — same-origin MVC proxy only. */
const LskusList = (() => {
    'use strict';

    const endpoint = '/MasterDataManagement/Lskus/api';
    const tableEl = document.querySelector('.datatables-lskus');
    const L = window.L10n || {};
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'Lskus' };
    const baseOrder = [[0, 'asc']];
    const dataColumnCount = 5;
    const initialColumnOrder = Array.from({ length: dataColumnCount + 1 }, (_, index) => index);
    const permissionHost = document.querySelector('[data-can-create]');
    const canCreate = permissionHost?.getAttribute('data-can-create') === 'true';
    const canSubmit = permissionHost?.getAttribute('data-can-submit') === 'true';
    const canRetire = permissionHost?.getAttribute('data-can-retire') === 'true';
    const lifecycleRequests = new Set();

    let dt = null;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const valueOf = (value, camelName, pascalName) => value?.[camelName] ?? value?.[pascalName];
    const unwrapData = value => value?.data ?? value?.Data ?? value;
    const escapeHtml = value => String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
    const normalizeRetirementReason = value => typeof value === 'string' ? value.trim() : '';

    const emptyFilters = () => ({ search: '' });
    const defaultColVis = () => Array.from({ length: dataColumnCount + 1 }, () => true);
    const normalizeView = view => ({
        filters: { ...emptyFilters(), ...(view?.filters || {}) },
        search: view?.search || '',
        colVis: Array.isArray(view?.colVis) ? view.colVis : defaultColVis(),
        columnOrder: Array.isArray(view?.columnOrder) ? view.columnOrder : [...initialColumnOrder],
        order: Array.isArray(view?.order) && view.order.length ? view.order : baseOrder
    });
    const getResetBaselineState = () => normalizeView({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: dataColumnCount + 1 }, (_, index) => index),
        order: baseOrder
    });
    const serializeView = view => JSON.stringify(normalizeView(view));
    const getCurrentView = api => normalizeView({
        filters: { search: document.getElementById('lskuSearch')?.value || '' },
        search: api.search(),
        colVis: api.columns().visible().toArray(),
        columnOrder: api.colReorder?.order?.() || [...initialColumnOrder],
        order: api.order()
    });
    const isDirtyComparedToDefault = api =>
        serializeView(getCurrentView(api)) !== serializeView(defaultViewState || getResetBaselineState());
    const setSaveFilterVisible = visible => {
        document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const applySavedTableState = (api, view) => {
        const normalized = normalizeView(view);
        const searchInput = document.getElementById('lskuSearch');
        if (searchInput) searchInput.value = normalized.filters.search || normalized.search;
        api.search(normalized.search);
        normalized.colVis.forEach((visible, index) => api.column(index).visible(visible, false));
        if (api.colReorder?.order) api.colReorder.order(normalized.columnOrder, true);
        api.order(normalized.order);
    };

    const parseSavedConfiguration = record => {
        const raw = record?.viewDefinition ?? record?.ViewDefinition;
        if (!raw) return null;
        try { return normalizeView(typeof raw === 'string' ? JSON.parse(raw) : raw); }
        catch { return null; }
    };
    const getSavedViewId = record => record?.id || record?.Id || record?._id || null;
    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return null;
        try {
            const response = await personalizationClient.getViews(
                personalizationContext.moduleKey,
                personalizationContext.pageKey);
            const records = Array.isArray(response) ? response : (response?.data || response?.Data || []);
            defaultViewRecord = records.find(item => item?.isDefault || item?.IsDefault) || records[0] || null;
            defaultViewState = parseSavedConfiguration(defaultViewRecord);
            return defaultViewState;
        } catch {
            defaultViewRecord = null;
            defaultViewState = null;
            return null;
        }
    };
    const saveDefaultView = async view => {
        if (!personalizationClient?.saveView) return null;
        const normalized = normalizeView(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (defaultViewRecord?.viewName || defaultViewRecord?.ViewName || L.SaveView || 'Default'),
            isDefault: true,
            viewDefinition: normalized
        };
        const id = getSavedViewId(defaultViewRecord);
        const response = id && personalizationClient.updateView
            ? await personalizationClient.updateView(id, payload)
            : await personalizationClient.saveView(payload);
        defaultViewRecord = response?.data || response?.Data || response || payload;
        defaultViewState = normalized;
        setSaveFilterVisible(false);
        return defaultViewRecord;
    };

    const getErrorMessage = async response => {
        const payload = await response.json().catch(() => null);
        return payload?.errors?.[0] || payload?.Errors?.[0] || L.ErrorGateway || 'Request failed.';
    };
    const formatDate = value => value
        ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
        : (L.Unknown || '-');

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
    const renderLifecycle = value => {
        const item = lifecycleItem(value);
        return item
            ? `<span class="badge ${item.class}">${escapeHtml(item.title)}</span>`
            : `<span class="badge bg-label-secondary">${escapeHtml(L.Unknown)}</span>`;
    };

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };

    const loadCreateOptions = async () => {
        const response = await fetch(`${endpoint}/create-options`, {
            credentials: 'same-origin',
            headers: getAuthHeaders()
        });
        if (!response.ok) throw new Error(await getErrorMessage(response));
        const options = unwrapData(await response.json());
        const gskus = options?.gskus || options?.Gskus || [];
        const markets = options?.markets || options?.Markets || [];
        const gskuSelect = document.getElementById('gskuId');
        const marketSelect = document.getElementById('marketCode');
        gskuSelect.innerHTML = '<option value=""></option>';
        marketSelect.innerHTML = '<option value=""></option>';
        gskus.forEach(item => gskuSelect.add(new Option(
            `${valueOf(item, 'canonicalCode', 'CanonicalCode')} — ${valueOf(item, 'globalProductName', 'GlobalProductName')}`,
            valueOf(item, 'id', 'Id'))));
        markets.forEach(item => marketSelect.add(new Option(
            `${valueOf(item, 'code', 'Code')} — ${valueOf(item, 'displayText', 'DisplayText')}`,
            valueOf(item, 'code', 'Code'))));
    };

    const openCreate = async () => {
        const form = document.getElementById('formLsku');
        if (!form) return;
        form.reset();
        form.classList.remove('was-validated');
        document.getElementById('requiredProgress').textContent = '0/2';
        await loadCreateOptions();
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
    };

    const submitCreate = async () => {
        const form = document.getElementById('formLsku');
        const gskuId = document.getElementById('gskuId')?.value || '';
        const marketCode = document.getElementById('marketCode')?.value || '';
        if (!form || !gskuId || !marketCode || !form.checkValidity()) {
            form?.classList.add('was-validated');
            return;
        }
        const antiForgeryToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const body = new FormData();
        body.set('GskuId', gskuId);
        body.set('MarketCode', marketCode);
        body.set('FormAttemptToken', document.getElementById('formAttemptToken')?.value || '');
        body.set('__RequestVerificationToken', antiForgeryToken);
        const response = await fetch(endpoint, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { ...getAuthHeaders(), RequestVerificationToken: antiForgeryToken },
            body
        });
        const payload = await response.json().catch(() => null);
        if (response.status === 202 || payload?.success === false && response.status === 202) {
            window.showToast?.(L.CreateReconciliationPending || L.Pending, 'warning');
            return;
        }
        if (response.status !== 201) throw new Error(payload?.errors?.[0] || L.ErrorGateway);
        const nextToken = payload?.formAttemptToken || payload?.FormAttemptToken;
        if (nextToken) document.getElementById('formAttemptToken').value = nextToken;
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
        dt?.ajax.reload(null, false);
        window.showToast?.(L.CreateSuccess || L.Success, 'success');
    };

    const setDetailValue = (id, value) => {
        const element = document.getElementById(id);
        if (element) element.textContent = value ?? L.Unknown ?? '-';
    };
    const fetchDetail = async id => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
            credentials: 'same-origin',
            headers: getAuthHeaders()
        });
        if (response.status === 401) handleUnauthorized();
        if (!response.ok) throw new Error(response.status === 404 ? L.ErrorNotFound : await getErrorMessage(response));
        return unwrapData(await response.json());
    };

    const handleUnauthorized = () => {
        window.DtDefaults?.handleUnauthorized?.();
        const error = new Error('auth-refresh-in-progress');
        error.authHandled = true;
        throw error;
    };
    const validateDetail = (detail, expectedId) => {
        const detailId = String(valueOf(detail, 'id', 'Id') || '').toLowerCase();
        if (!detailId || detailId !== String(expectedId || '').toLowerCase()) throw new Error(L.ErrorGateway);
        const detailState = lifecycleCode(valueOf(detail, 'lifecycleStatus', 'LifecycleStatus'));
        const version = Number(valueOf(detail, 'version', 'Version'));
        if (detailState === null || !Number.isInteger(version) || version < 0) throw new Error(L.ErrorGateway);
        return { state: detailState, version };
    };
    const renderDetail = (detail, expectedId) => {
        const verified = validateDetail(detail, expectedId);
        setDetailValue('oc-title', valueOf(detail, 'canonicalCode', 'CanonicalCode'));
        setDetailValue('oc-code', valueOf(detail, 'canonicalCode', 'CanonicalCode'));
        setDetailValue('oc-gsku-code', valueOf(detail, 'gskuCanonicalCode', 'GskuCanonicalCode'));
        setDetailValue('oc-market', valueOf(detail, 'marketCode', 'MarketCode'));
        setDetailValue('oc-version', verified.version);
        setDetailValue('oc-created-at', formatDate(valueOf(detail, 'createdAt', 'CreatedAt')));
        setDetailValue('oc-updated-at', formatDate(valueOf(detail, 'updatedAt', 'UpdatedAt')));
        const statusElement = document.getElementById('oc-status');
        if (!statusElement) throw new Error(L.ErrorGateway);
        statusElement.outerHTML = renderLifecycle(valueOf(detail, 'lifecycleStatus', 'LifecycleStatus'))
            .replace('<span ', '<span id="oc-status" ');
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
        return verified.state;
    };
    const populateDetails = async id => {
        const loading = document.getElementById('lskuDetailLoading');
        const error = document.getElementById('lskuDetailError');
        loading?.classList.remove('d-none');
        error?.classList.add('d-none');
        try {
            renderDetail(await fetchDetail(id), id);
        } catch (exception) {
            if (exception?.authHandled) return;
            if (error) {
                error.textContent = exception.message || L.ErrorGateway;
                error.classList.remove('d-none');
            }
        } finally {
            loading?.classList.add('d-none');
            if (!error || error.classList.contains('d-none')) return;
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
        }
    };

    const lifecycleToken = () => document.querySelector(
        '#lskuLifecycleToken input[name="__RequestVerificationToken"]')?.value || '';
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
            const state = verified.state;
            const expectedState = action === 'submit' ? 1 : 3;
            if (state !== expectedState) {
                dt?.ajax.reload(null, false);
                throw new Error(L.LifecycleStateChanged);
            }
            const expectedVersion = verified.version;

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
        } catch (exception) {
            if (!exception?.authHandled) window.showToast?.(exception.message || L.ErrorGateway, 'error');
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
            const reason = normalizeRetirementReason(input);
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
            inputAttributes: { maxlength: '128', rows: '3' },
            confirmButtonText: L.RetireIdentity
        });
    };
    const renderActions = row => {
        const id = valueOf(row, 'id', 'Id');
        const actions = [{
            key: 'details', className: 'js-quick-view', text: L.QuickView, icon: 'bx bx-show',
            attrs: { 'data-id': id, title: L.ViewDetails }
        }];
        const state = lifecycleCode(valueOf(row, 'lifecycleStatus', 'LifecycleStatus'));
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

    const buildQuery = data => new URLSearchParams({
        pageNumber: String(Math.floor(data.start / data.length) + 1),
        pageSize: String(data.length),
        search: data.search.value || ''
    });

    const initDataTable = async () => {
        if (!tableEl || !window.DtDefaults) return;
        const savedState = await loadDefaultView();
        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn',
                attr: {
                    title: L.Filter,
                    'aria-label': L.Filter,
                    'aria-controls': 'inlineFilterCollapse'
                },
                action: toggleInlineFilter
            },
            saveFilterBtn: {
                text: L.SaveView || 'Save View',
                className: 'btn btn-label-primary dt-save-filter-btn d-none',
                attr: { title: L.SaveView, 'aria-label': L.SaveView },
                action: async (event, api) => {
                    await saveDefaultView(getCurrentView(api || dt));
                    setSaveFilterVisible(false);
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
            colReorder: { columns: ':not(:last-child)' },
            ajax: (data, callback) => fetch(`${endpoint}?${buildQuery(data)}`, {
                credentials: 'same-origin',
                headers: getAuthHeaders()
            }).then(async response => {
                if (!response.ok) throw new Error(await getErrorMessage(response));
                return response.json();
            }).then(payload => {
                const page = unwrapData(payload);
                const count = page?.totalCount || page?.TotalCount || 0;
                callback({ data: page?.items || page?.Items || [], recordsTotal: count, recordsFiltered: count });
            }).catch(exception => {
                window.showToast?.(exception.message || L.ErrorGateway, 'error');
                callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
            }),
            columns: [
                { data: 'canonicalCode' },
                { data: 'gskuCanonicalCode' },
                { data: 'marketCode' },
                { data: 'lifecycleStatus' },
                { data: 'version' },
                { data: null }
            ],
            columnDefs: [
                { targets: 0, render: data => `<span class="fw-medium">${escapeHtml(data)}</span>` },
                { targets: [1, 2, 4], render: escapeHtml },
                { targets: 3, render: renderLifecycle },
                {
                    targets: -1,
                    title: L.Actions,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => renderActions(row)
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                canCreate ? L.AddNew : null,
                {},
                extraButtons,
                { exportColumns: [0, 1, 2, 3, 4], colvisColumns: [0, 1, 2, 3, 4] }),
            initComplete: function () {
                const api = this.api();
                applySavedTableState(api, savedState || getResetBaselineState());
                document.querySelector('.dt-export-collection-btn')?.setAttribute('title', L.Export);
                document.querySelector('.buttons-colvis')?.setAttribute('title', L.ColumnVisibility);
                document.querySelector('.add-new')?.addEventListener('click', event => {
                    event.preventDefault();
                    openCreate().catch(exception => window.showToast?.(exception.message || L.ErrorGateway, 'error'));
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
        document.getElementById('btnSaveLsku')?.addEventListener('click', () =>
            submitCreate().catch(exception => window.showToast?.(exception.message || L.ErrorGateway, 'error')));
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            if (!dt) return;
            dt.search(document.getElementById('lskuSearch')?.value || '').draw();
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', event => {
            event.preventDefault();
            if (!dt) return;
            applySavedTableState(dt, getResetBaselineState());
            dt.draw();
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        document.addEventListener('change', event => {
            if (!event.target.matches('#gskuId, #marketCode')) return;
            const completed = ['gskuId', 'marketCode'].filter(id => document.getElementById(id)?.value).length;
            document.getElementById('requiredProgress').textContent = `${completed}/2`;
        });
        document.addEventListener('click', event => {
            const quickView = event.target.closest('.js-quick-view');
            const lifecycleAction = event.target.closest('.js-submit-identity, .js-retire-identity');
            const action = quickView || lifecycleAction;
            if (!action || !action.closest('.datatables-lskus') || action.classList.contains('disabled')) return;
            event.preventDefault();
            if (action.classList.contains('js-submit-identity')) requestLifecycle(action.dataset.id, 'submit', action);
            else if (action.classList.contains('js-retire-identity')) requestLifecycle(action.dataset.id, 'retire', action);
            else populateDetails(action.dataset.id);
        });
    };

    return { init: async () => { bindEvents(); await initDataTable(); } };
})();

document.addEventListener('DOMContentLoaded', () => LskusList.init());
