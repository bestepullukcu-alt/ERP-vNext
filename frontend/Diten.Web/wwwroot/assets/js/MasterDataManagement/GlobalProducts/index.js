/**
 * MOD-0290 Global Product Register — tenant-shell Golden Slim list.
 * Browser traffic is restricted to the same-origin MVC proxy at /GlobalProducts/api.
 */
'use strict';

const GlobalProductsList = (function () {
    /*
     * WP-MDM-GP-LIST-01 (BL-440) — the register stands on the list component (DitenDataTable.createList, server mode),
     * as the GSKU register does since FIX3 B17: the factory owns the table, the inline filter, Save View (same
     * moduleKey/pageKey as before, so saved views still load), the toolbar and the server wire
     * (start/length/search/orderBy/orderDir/lifecycleStatus → the MVC proxy → MDM). This file keeps only the register's
     * own business: renderers, the server-owned action menu, quick view, the first-page prefetch, and the
     * create / edit / submit / withdraw / correction / retirement flows.
     */
    let list = null;
    let dt;
    let initialPagePrefetch = null;

    const endpoint = '/MasterDataManagement/GlobalProducts/api';
    const tableEl = document.querySelector('.datatables-globalproducts');
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'GlobalProducts' };
    // MOD-0290-FU05: + the Brand column (4), last before the actions, so a saved view's indexes keep their meaning.
    const saveViewColumnIndexes = [1, 2, 3, 4];
    // The register's order before server sorting existed: MDM sorted by name (the code column's arrow was a label only).
    const baseOrder = [[2, 'asc']];
    const initialPageSize = 10;
    const L = window.L10n || {};
    const permissionHost = document.querySelector('[data-can-create]');
    const canCreate = permissionHost?.getAttribute('data-can-create') === 'true';
    const lifecycleRequests = new Set();
    const detailById = new Map();
    // MDM's order (GetGlobalProductByIdHandler.BuildAvailableActions); MOD-0290-FU05: CHANGE_BRAND comes last.
    const actionOrder = ['DETAILS', 'EDIT', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT', 'CHANGE_BRAND'];
    let editorMode = 'create';
    let editorId = '';
    let editorVersion = null;
    let editorBrandId = '';
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const normalizeString = (value) => typeof value === 'string' ? value.trim() : '';
    // MOD-0290-FU05 (Karar b): MDM reads the brand flat — brandId, brandCode, brandName (all null without a brand).
    const brandOf = (record) => ({
        id: record?.brandId ?? record?.BrandId ?? null,
        code: record?.brandCode ?? record?.BrandCode ?? '',
        name: record?.brandName ?? record?.BrandName ?? ''
    });
    // "CODE · Name"; "—" without a brand; Unknown when the brand is linked but MDM could not read it.
    const brandLabel = (brand) => brand?.code ? `${brand.code} · ${brand.name || ''}`.trim() : (brand?.id ? L.Unknown : '—');
    const brandOptionLabel = (option) => `${option?.brandCode ?? option?.BrandCode ?? ''} · ${option?.brandName ?? option?.BrandName ?? ''}`;

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

    // A failed page read says a sentence in the reader's language (the proxy answers one); 401 is the shared refresh.
    const onListError = function (xhr) {
        const status = Number(xhr?.status) || 0;
        if (status === 401) {
            if (!this?._retried) window.DtDefaults?.handleUnauthorized?.(this);
            return;
        }
        if (xhr?.statusText === 'abort') return;
        let payload = null;
        try { payload = xhr?.responseJSON ?? JSON.parse(xhr?.responseText || 'null'); } catch (error) { payload = null; }
        const errors = payload?.errors || payload?.Errors || [];
        const sentence = Array.isArray(errors) ? errors.find((value) => typeof value === 'string' && value) : '';
        window.showToast?.(sentence || L.ErrorGateway, 'error');
    };

    /*
     * The first-page prefetch (kept from the hand-written list): the default first page is asked for while the saved
     * view is still loading, and handed to the list component's FIRST request only when that request is exactly the
     * same query (no saved search, filter or order) — otherwise it is aborted and the component asks for its own page.
     * The component sends its requests through jQuery; jQuery asks `settings.xhr()` for the request object, so this
     * page answers that one request from the prefetch. The factory and the shared files are not changed.
     */
    const defaultFirstPageQuery = () => new URLSearchParams({
        start: '0',
        length: String(initialPageSize),
        orderBy: 'globalProductName',
        orderDir: 'asc'
    });
    // The component adds `draw` (and jQuery may add a cache buster `_`): neither changes which rows are asked for.
    const comparableQuery = (url) => {
        const parsed = new URL(url, window.location.origin);
        parsed.searchParams.delete('draw');
        parsed.searchParams.delete('_');
        const pairs = Array.from(parsed.searchParams.entries()).sort(([a, x], [b, y]) => a === b ? (x < y ? -1 : 1) : (a < b ? -1 : 1));
        return `${parsed.pathname}?${new URLSearchParams(pairs).toString()}`;
    };
    const startInitialPagePrefetch = () => {
        const url = `${endpoint}?${defaultFirstPageQuery().toString()}`;
        const controller = new AbortController();
        const result = fetch(url, { credentials: 'same-origin', headers: getAuthHeaders(), signal: controller.signal })
            .then(async (response) => ({
                status: response.status,
                statusText: response.statusText,
                contentType: response.headers.get('content-type') || 'application/json',
                text: await response.text()
            }))
            .catch(() => null);
        return { query: comparableQuery(url), controller, result };
    };
    // A request object jQuery can drive (open, setRequestHeader, send, abort, the on* handlers) answered from the prefetch.
    const prefetchedRequest = (prefetched) => {
        const request = {
            readyState: 0, status: 0, statusText: '', responseText: '', response: '', responseType: '',
            onload: null, onerror: null, onabort: null, ontimeout: null, onreadystatechange: null,
            headers: '',
            open() { request.readyState = 1; },
            setRequestHeader() { },
            getAllResponseHeaders() { return request.headers; },
            abort() { prefetched.controller.abort(); request.onabort?.(); },
            send() {
                prefetched.result.then((answer) => {
                    if (!answer) { request.onerror?.(); return; }
                    request.readyState = 4;
                    request.status = answer.status;
                    request.statusText = answer.statusText;
                    request.responseText = answer.text;
                    request.response = answer.text;
                    request.headers = `content-type: ${answer.contentType}\r\n`;
                    request.onload?.();
                });
            }
        };
        return request;
    };
    function listRequest() {
        const prefetched = initialPagePrefetch;
        initialPagePrefetch = null;
        if (prefetched && comparableQuery(this.url) === prefetched.query) return prefetchedRequest(prefetched);
        prefetched?.controller.abort();
        return new window.XMLHttpRequest();
    }

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
        const brandElement = document.getElementById('oc-brand');
        if (!brandElement) throw new Error(L.ErrorGateway);
        brandElement.textContent = brandLabel(brandOf(detail));
        const status = detail.lifecycleStatus || detail.LifecycleStatus;
        const statusElement = document.getElementById('oc-status');
        if (!statusElement) throw new Error(L.ErrorGateway);
        statusElement.outerHTML = renderLifecycle(status).replace('<span ', '<span id="oc-status" ');
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
        EDIT: { path: '', method: 'PUT', success: L.UpdateSuccess },
        CHANGE_BRAND: { path: 'brand', method: 'POST', success: L.ChangeBrandSuccess }
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
        if (action === 'CHANGE_BRAND') return idMatches && hasLifecycle && Number.isInteger(version) && version >= 0;
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
        // MOD-0290-FU05: the brand the panel sent ('' = none) is the brand the fresh detail reads.
        const brandMatches = !fields || !Object.prototype.hasOwnProperty.call(fields, 'BrandId')
            || String(fields.BrandId || '').toLowerCase() === String(detail?.brandId ?? detail?.BrandId ?? '').toLowerCase();
        if (action === 'EDIT') {
            return state === 1 && brandMatches
                && normalizeString(detail?.globalProductName ?? detail?.GlobalProductName)
                    === normalizeString(fields?.GlobalProductName);
        }
        if (action === 'CHANGE_BRAND') return state === 3 && brandMatches;
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

    /*
     * Each server action is a menu item the list component dispatches (`data-row-action` → actions.onRowAction): the
     * same delegation inside the table and inside the responsive row modal. Only actions MDM returned for this row,
     * read fresh when the menu opens, are ever drawn.
     */
    const actionPresentation = {
        DETAILS: { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', label: () => L.ViewDetails },
        EDIT: { key: 'editDraft', className: 'js-edit-draft', icon: 'bx bx-edit', label: () => L.EditDraft },
        SUBMIT: { key: 'submitIdentity', className: 'js-lifecycle-action', icon: 'bx bx-send', label: () => L.SubmitIdentity },
        WITHDRAW_APPROVAL: { key: 'withdrawApproval', className: 'js-lifecycle-action', icon: 'bx bx-undo', label: () => L.WithdrawApproval },
        REQUEST_CORRECTION: { key: 'requestCorrection', className: 'js-lifecycle-action', icon: 'bx bx-edit-alt', label: () => L.RequestCorrection },
        REQUEST_RETIREMENT: { key: 'requestRetirement', className: 'js-lifecycle-action', icon: 'bx bx-archive', label: () => L.RequestRetirement },
        CHANGE_BRAND: { key: 'changeBrand', className: 'js-lifecycle-action', icon: 'bx bx-purchase-tag', label: () => L.ChangeBrand }
    };
    const renderFreshActionMenu = (menu, id, actions) => {
        menu.innerHTML = actions.map((code) => {
            const { key, className, icon, label } = actionPresentation[code];
            return `<a href="javascript:void(0);" class="dropdown-item dt-action-item ${className}" data-row-action="${key}" data-id="${escapeHtml(id)}" data-action="${code}"><i class="${icon} dt-action-icon"></i>${escapeHtml(label())}</a>`;
        }).join('');
    };
    // The quick view shows the detail read fresh when the menu opened; the list component opens and closes it.
    const showDetails = (id, trigger) => {
        const detail = detailById.get(String(id).toLowerCase());
        if (!detail) {
            window.showToast?.(L.LifecycleStateChanged, 'warning');
            return;
        }
        try {
            list?.showQuickView({ id, detail }, trigger);
        } catch (error) {
            window.showToast?.(error.message || L.ErrorGateway, 'error');
        }
    };
    // The register's row-action handlers, by the keys above. A busy item (disabled) is never run twice.
    const rowAction = (run) => ({ id, trigger }) => {
        if (!id || trigger?.classList.contains('disabled')) return;
        run(String(id), trigger);
    };
    const rowActionHandlers = () => ({
        quickView: rowAction((id, trigger) => showDetails(id, trigger)),
        editDraft: rowAction((id, trigger) => openEdit(id, trigger)),
        submitIdentity: rowAction((id, trigger) => requestLifecycle(id, 'SUBMIT', trigger)),
        withdrawApproval: rowAction((id, trigger) => requestLifecycle(id, 'WITHDRAW_APPROVAL', trigger)),
        requestCorrection: rowAction((id, trigger) => requestLifecycle(id, 'REQUEST_CORRECTION', trigger)),
        requestRetirement: rowAction((id, trigger) => requestLifecycle(id, 'REQUEST_RETIREMENT', trigger)),
        changeBrand: rowAction((id, trigger) => openChangeBrand(id, trigger))
    });
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
            menu.replaceChildren();
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorOccurred, 'error');
        } finally {
            toggle.classList.remove('disabled');
        }
    };

    /*
     * MOD-0290-FU05 — the brand picker: only MDM's brand-options answer (linkable brands) is drawn, after one "No brand"
     * choice on the first page; the field is always sent ('' = no brand).
     */
    const brandPageSize = 20;
    const brandSelect = () => document.getElementById('globalProductBrand');
    const selectedBrandId = () => normalizeString(brandSelect()?.value);
    const brandPickerResults = (payload, page) => {
        const data = unwrapData(payload);
        const items = Array.isArray(data.items ?? data.Items) ? (data.items ?? data.Items) : [];
        const total = Number(data.totalCount ?? data.TotalCount ?? 0);
        const results = items
            .filter((option) => option?.id ?? option?.Id)
            .map((option) => ({ id: String(option.id ?? option.Id), text: brandOptionLabel(option) }));
        return {
            results: page === 1 ? [{ id: '', text: L.BrandNone }].concat(results) : results,
            pagination: { more: page * brandPageSize < total }
        };
    };
    const setBrandSelection = (brand) => {
        const select = brandSelect();
        if (!select) return;
        Array.from(select.options).filter((option) => option.value !== '').forEach((option) => option.remove());
        if (brand?.id) select.appendChild(new Option(brandLabel(brand), String(brand.id), true, true));
        select.value = brand?.id ? String(brand.id) : '';
        window.jQuery?.(select).trigger('change');
    };
    const initBrandPicker = () => {
        const select = brandSelect();
        const $ = window.jQuery;
        if (!select || !$?.fn?.select2) return;
        const $select = $(select);
        if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
        $select.select2({
            dropdownParent: $('#offcanvasCreateEdit'),
            width: '100%',
            allowClear: true,
            placeholder: L.BrandNone,
            ajax: {
                url: `${endpoint}/brand-options`,
                dataType: 'json',
                delay: 250,
                headers: getAuthHeaders(),
                data: (params) => ({ search: normalizeString(params.term), pageNumber: params.page || 1, pageSize: brandPageSize }),
                processResults: (payload, params) => brandPickerResults(payload, params.page || 1)
            }
        });
    };
    // The filter's options: the same linkable brands, EVERY page of them (MDM pages by at most 100) — never a silent cap.
    // The loop is bounded twice: by the pages MDM's count needs, and by brandFilterMaxPages (5 000 brands). The chip is
    // NOT shown — never a partial or duplicated list — when the reader may not read brand-options (403), a page cannot be
    // read, the count is not a number, the count needs more than the cap, or the pages do not add up to the count (a
    // server that ignored pageNumber would repeat one page).
    const brandFilterPageSize = 100;
    const brandFilterMaxPages = 50;
    const loadBrandFilterOptions = async ({ fillSelect }) => {
        const select = document.getElementById('filterBrand');
        if (!select) return;
        const hide = () => select.closest('.filter-chip')?.classList.add('d-none');
        const byId = new Map();
        let total = 0;
        let maxPages = 1;
        for (let pageNumber = 1; pageNumber <= maxPages; pageNumber += 1) {
            const query = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(brandFilterPageSize) });
            const response = await fetch(`${endpoint}/brand-options?${query}`, { credentials: 'same-origin', headers: getAuthHeaders() });
            if (!response.ok) return hide();
            const data = unwrapData(await response.json());
            const page = Array.isArray(data.items ?? data.Items) ? (data.items ?? data.Items) : [];
            if (pageNumber === 1) {
                total = Number(data.totalCount ?? data.TotalCount);
                if (!Number.isFinite(total) || total < 0) return hide();
                maxPages = Math.ceil(total / brandFilterPageSize);
                if (maxPages > brandFilterMaxPages) return hide();
            }
            page.forEach((option) => {
                const id = option?.id ?? option?.Id;
                if (id) byId.set(String(id).toLowerCase(), { value: id, text: brandOptionLabel(option) });
            });
            if (page.length < brandFilterPageSize) break;
        }
        if (byId.size !== total) return hide();
        fillSelect(select, Array.from(byId.values()), { showAllText: L.ShowAll });
    };
    // The panel's three faces: create (name + brand), draft edit (name + brand), approved product's brand (brand only).
    const showEditorFields = (nameVisible) => {
        document.getElementById('globalProductNameField')?.classList.toggle('d-none', !nameVisible);
    };

    const openCreate = () => {
        editorMode = 'create'; editorId = ''; editorVersion = null; editorBrandId = '';
        const form = document.getElementById('formGlobalProduct');
        form?.reset();
        showEditorFields(true);
        setBrandSelection(null);
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
            showEditorFields(true);
            document.getElementById('globalProductName').value = detail.globalProductName ?? detail.GlobalProductName ?? '';
            const brand = brandOf(detail);
            editorBrandId = brand.id ? String(brand.id).toLowerCase() : '';
            setBrandSelection(brand);
            document.getElementById('offcanvasCreateEditLabel').textContent = L.FormTitleEdit;
            document.getElementById('btnSaveGlobalProduct').textContent = L.UpdateDraft;
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorOccurred, 'error');
        } finally { setLifecycleBusy(button, false); }
    };
    // MOD-0290-FU05 slice 2: only when MDM offers CHANGE_BRAND for this product (approved, no operation, update-brand).
    const openChangeBrand = async (id, button) => {
        setLifecycleBusy(button, true);
        try {
            const detail = await fetchDetail(id);
            if (!readAvailableActions(detail).includes('CHANGE_BRAND')) throw new Error(L.LifecycleStateChanged);
            editorMode = 'brand'; editorId = id;
            editorVersion = Number(detail.version ?? detail.Version);
            if (!Number.isInteger(editorVersion) || editorVersion < 0) throw new Error(L.ErrorConflict);
            const form = document.getElementById('formGlobalProduct');
            form?.classList.remove('was-validated');
            document.getElementById('globalProductName').value = detail.globalProductName ?? detail.GlobalProductName ?? '';
            showEditorFields(false);
            const brand = brandOf(detail);
            editorBrandId = brand.id ? String(brand.id).toLowerCase() : '';
            setBrandSelection(brand);
            document.getElementById('offcanvasCreateEditLabel').textContent = L.ChangeBrand;
            document.getElementById('btnSaveGlobalProduct').textContent = L.ChangeBrand;
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorOccurred, 'error');
        } finally { setLifecycleBusy(button, false); }
    };
    const submitEditor = () => {
        const form = document.getElementById('formGlobalProduct');
        if (editorMode === 'brand') {
            const brandId = selectedBrandId();
            if (brandId.toLowerCase() === editorBrandId) {
                window.showToast?.(L.BrandUnchanged, 'warning');
                return;
            }
            window.showConfirm?.(L.ChangeBrandConfirmation, async () => {
                const button = document.getElementById('btnSaveGlobalProduct');
                if (button) button.disabled = true;
                try {
                    const saved = await postLifecycle(editorId, 'CHANGE_BRAND', { BrandId: brandId }, button, editorVersion);
                    if (saved) bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                } finally { if (button) button.disabled = false; }
            }, { type: 'warning', confirmButtonText: L.ChangeBrand });
            return;
        }
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
                    const saved = await postLifecycle(editorId, 'EDIT', { GlobalProductName: name, BrandId: selectedBrandId() }, button, editorVersion);
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
                body.set('BrandId', selectedBrandId());
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

    /*
     * EK item 9 (FIX5) — no silent partial export in a GxP list: until BL-452 gives this list a server export (every
     * matching row), each Print / CSV / Excel / PDF / Copy entry says plainly that it writes only the page on screen.
     * The list component chains the page's `initComplete`, so this is done without touching the shared files; the
     * entries are found by their Buttons class or the export icon dt-defaults gives them (read, not changed).
     */
    const exportEntrySelector = '.dt-controlled-copy, .buttons-csv, .buttons-excel, .buttons-copy, .buttons-print, .buttons-pdf';
    const exportEntryIcon = '.bx-printer, .bx-file, .bxs-file-export, .bxs-file-pdf, .bx-copy';
    const labelExportsCurrentPageOnly = (api) => {
        const nodes = api?.buttons?.()?.nodes?.();
        Array.from(nodes || []).forEach((node) => {
            if (!node?.matches || node.matches('.dt-server-export')) return;
            if (!node.matches(exportEntrySelector) && !node.querySelector(exportEntryIcon)) return;
            if (node.querySelector('.dt-current-page-only')) return;
            const note = document.createElement('small');
            note.className = 'd-block text-muted dt-current-page-only';
            note.textContent = L.CurrentPageOnly || '';
            node.appendChild(note);
            node.setAttribute('title', L.CurrentPageOnly || '');
        });
    };

    // The createList options of the register — one place, so the wire and the saved-view keys are measurable.
    const buildListOptions = () => ({
        tableEl,
        dataMode: 'server',
        ajax: { url: endpoint, type: 'GET', headers: getAuthHeaders(), error: onListError, xhr: listRequest },
        // The action menu and every row action stay the register's own (fresh server-owned actions per row).
        actions: { onRowAction: rowActionHandlers() },
        quickView: {
            offcanvasId: 'offcanvasDetailsPreview',
            populate: (row) => renderDetail(row.detail, row.id)
        },
        toolbar: {
            addNewText: canCreate ? L.AddNew : '',
            onAddNew: () => openCreate(),
            exportColumns: saveViewColumnIndexes,
            colvisColumns: saveViewColumnIndexes
        },
        filters: {
            hostId: 'inlineFilterHost',
            collapseId: 'inlineFilterCollapse',
            applyBtn: 'btnFilterApply',
            resetBtn: 'btnFilterReset',
            fields: [
                { id: 'filterLifecycleStatus', key: 'lifecycleStatus', kind: 'single' },
                // MOD-0290-FU05: one brand (the proxy passes only a GUID on as brandId).
                { id: 'filterBrand', key: 'brandId', kind: 'single' }
            ],
            loadOptions: loadBrandFilterOptions
        },
        // Same keys as before the migration: every saved Global Product view still loads.
        savedView: {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            saveViewColumnIndexes,
            defaultVisibleColumnIndexes: saveViewColumnIndexes,
            baseOrder
        },
        config: {
            pageLength: initialPageSize,
            deferRender: true,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            // EK item 9: chained by the list component after its own initComplete.
            initComplete: function () { labelExportsCurrentPageOnly(this.api()); },
            // `data` names are MDM's orderBy whitelist (globalProductName, canonicalCode, lifecycleStatus).
            columns: [
                { data: 'id', name: 'control' },
                { data: 'canonicalCode', name: 'canonicalCode' },
                { data: 'globalProductName', name: 'globalProductName' },
                { data: 'lifecycleStatus', name: 'lifecycleStatus' },
                // MOD-0290-FU05: not in MDM's orderBy whitelist — never sortable.
                { data: 'brandCode', name: 'brand' },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { targets: 2, render: escapeHtml },
                { targets: 3, render: renderLifecycle },
                { targets: 4, orderable: false, render: (data, type, row) => escapeHtml(brandLabel(brandOf(row))) },
                {
                    targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => renderActions(row)
                }
            ]
        }
    });

    const initDataTable = async () => {
        if (!tableEl || !window.DitenDataTable?.createList) return;
        // The default first page is asked for now, while the component loads the saved view (it is used only if the
        // component's first request is that very page).
        initialPagePrefetch = startInitialPagePrefetch();
        list = await window.DitenDataTable.createList(buildListOptions());
        dt = list.dt;
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
        document.getElementById('btnSaveGlobalProduct')?.addEventListener('click', submitEditor);
        // The menu itself is read fresh from MDM when it opens; its items are dispatched by the list component.
        document.addEventListener('click', (event) => {
            const toggle = event.target.closest('.js-global-product-actions-toggle');
            if (!toggle || !(toggle.closest('.datatables-globalproducts') || toggle.closest('.modal.dtr-bs-modal'))) return;
            event.preventDefault();
            event.stopPropagation();
            loadActionMenu(toggle);
        });
    };

    return {
        init: async () => {
            installSelectorInfrastructure();
            bindEvents();
            initBrandPicker();
            await initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => GlobalProductsList.init());
