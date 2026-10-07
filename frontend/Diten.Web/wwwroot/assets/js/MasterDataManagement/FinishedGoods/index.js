/**
 * MOD-0290 Finished Good Draft Foundation — tenant-shell Golden Slim list.
 * Browser traffic is restricted to the same-origin MVC proxy.
 */
'use strict';

const FinishedGoodsList = (function () {
    /*
     * FG item 6 — the register stands on the list component (DitenDataTable.createList, server mode): the factory owns
     * the table, the inline filter, Save View (same moduleKey/pageKey as before, so saved views still load), the
     * toolbar and the server wire. This file keeps only the register's own business: renderers, the quick view, the
     * refusal sentences and the create flow.
     */
    let list = null;
    let dt;
    const endpoint = '/MasterDataManagement/FinishedGoods/api';
    const tableEl = document.querySelector('.datatables-finishedgoods');
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'FinishedGoods' };
    const saveViewColumnIndexes = [1, 2, 3, 4];
    const baseOrder = [[1, 'asc']];
    // MDM's list query (GetFinishedGoodsQuery) takes pageNumber 1–1 000 000, pageSize 1–100 and search ≤ 200.
    const maxPageSize = 100;
    const maxPageNumber = 1000000;
    const L = window.L10n || {};
    const canCreate = document.querySelector('[data-can-create]')?.getAttribute('data-can-create') === 'true';
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const normalizeSearch = (value) => typeof value === 'string' ? value.trim().slice(0, 200) : '';
    const unwrapData = (payload) => payload?.data || payload?.Data || {};
    const valueOf = (source, camel, pascal) => source?.[camel] ?? source?.[pascal];

    /*
     * Lifecycle — never the raw value: an unknown state reads "Unknown". m9: the audited Draft cancellation
     * (cancellationDisposition 1 PendingAudit / 2 Cancelled, MDM's FinishedGoodCancellationDisposition) takes
     * precedence over the stored Draft badge.
     */
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
    const cancellationMap = () => ({
        PendingAudit: { title: L.CancellationPendingAudit, class: 'bg-label-warning' },
        1: { title: L.CancellationPendingAudit, class: 'bg-label-warning' },
        Cancelled: { title: L.LifecycleCancelled, class: 'bg-label-dark' },
        2: { title: L.LifecycleCancelled, class: 'bg-label-dark' }
    });
    const lifecyclePresentation = (value, disposition) =>
        cancellationMap()[disposition]
        || lifecycleMap()[value]
        || { title: L.Unknown, class: 'bg-label-secondary' };
    const renderLifecycle = (value, row) => {
        const item = lifecyclePresentation(value, valueOf(row, 'cancellationDisposition', 'CancellationDisposition'));
        return `<span class="badge ${item.class}">${escapeHtml(item.title || L.Unknown)}</span>`;
    };

    /*
     * Item 6 — MDM refusal code → sentence key. The screen never shows a raw code: a code that is not here reads as
     * its HTTP status's sentence. Codes read from MDM's Finished Good create / cancel / read paths (the handlers, the
     * Finished Good repository, the GSKU child admission, the code reservation and the legal-entity scope guard).
     */
    const refusalKeys = Object.freeze({
        // The selected GSKU.
        GSKU_NOT_REFERENCEABLE: 'GskuNotReferenceable',
        GSKU_NOT_FOUND: 'GskuNotReferenceable',
        GSKU_ID_REQUIRED: 'GskuRequired',
        PARENT_NOT_IDENTITY_APPROVED: 'RefusalGskuNotApproved',
        GSKU_RETIREMENT_IN_PROGRESS: 'RefusalGskuLifecycleBusy',
        GSKU_LIFECYCLE_OPERATION_ACTIVE: 'RefusalGskuLifecycleBusy',
        GSKU_CHILD_ADMISSION_LIMIT_REACHED: 'RefusalAdmissionCapacity',
        // CreateFinishedGoodDraftHandler.AdmissionStatus names the capacity refusal this way; the repository answers
        // GSKU_CHILD_ADMISSION_LIMIT_REACHED. Both read the same.
        GSKU_CHILD_ADMISSION_CAPACITY_EXCEEDED: 'RefusalAdmissionCapacity',
        GSKU_CHILD_ADMISSION_CONFLICT: 'RefusalConcurrencyConflict',
        GSKU_CHILD_ADMISSION_REJECTED: 'RefusalConcurrencyConflict',
        GSKU_CHILD_ADMISSION_CONTRACT_INVALID: 'ErrorValidation',
        GSKU_CHILD_ADMISSION_REQUIRED: 'RefusalWriteFailed',
        GSKU_CHILD_BINDING_NOT_DURABLE: 'RefusalReconciliationRequired',
        // The form attempt (one attempt = one MDM creation key).
        IDEMPOTENCY_KEY_CONFLICT: 'RefusalIdempotencyKeyConflict',
        RESERVATION_IDEMPOTENCY_KEY_TOMBSTONED: 'RefusalIdempotencyKeyConflict',
        CREATION_COMMAND_TOMBSTONED: 'RefusalIdempotencyKeyConflict',
        IDEMPOTENCY_KEY_INVALID: 'RefusalIdempotencyKeyInvalid',
        FINISHED_GOOD_FIELD_FORBIDDEN: 'ErrorValidation',
        UNKNOWN_WRITE_FIELD_FORBIDDEN: 'ErrorValidation',
        // The Draft cancellation.
        FINISHED_GOOD_DRAFT_CANCELLED: 'RefusalDraftCancelled',
        FINISHED_GOOD_DRAFT_ALREADY_CANCELLED: 'RefusalDraftAlreadyCancelled',
        FINISHED_GOOD_CREATION_NOT_SETTLED: 'RefusalCreationNotSettled',
        FINISHED_GOOD_NOT_A_DRAFT: 'RefusalNotADraft',
        FINISHED_GOOD_DRAFT_CANCEL_CONFLICT: 'RefusalConcurrencyConflict',
        FINISHED_GOOD_DRAFT_CANCEL_FAILED: 'RefusalWriteFailed',
        FINISHED_GOOD_DRAFT_CANCEL_FORBIDDEN: 'ErrorForbidden',
        FINISHED_GOOD_DRAFT_CANCEL_INVALID: 'ErrorValidation',
        FINISHED_GOOD_DRAFT_CANCEL_CONTRACT_INVALID: 'ErrorValidation',
        AUDIT_INTENT_CAPACITY_REACHED: 'RefusalAuditBusy',
        AUDIT_INTENT_CONTRACT_INVALID: 'ErrorValidation',
        CONCURRENCY_CONFLICT: 'RefusalConcurrencyConflict',
        // The code and the write.
        FINISHED_GOOD_CODE_BURN_REFUSED: 'RefusalCodeBurnRefused',
        CODE_RESERVATION_REQUIRED: 'RefusalCodeNotIssued',
        CODE_RESERVATION_BURNED: 'RefusalCodeNotIssued',
        CODE_RESERVATION_ALREADY_TERMINAL: 'RefusalCodeNotIssued',
        CODE_RESERVATION_MISMATCH: 'RefusalCodeNotIssued',
        IDENTITY_RESERVATION_CONFLICT: 'RefusalCodeNotIssued',
        FINISHED_GOOD_WRITE_FAILED: 'RefusalWriteFailed',
        FINISHED_GOOD_DUPLICATE_CONFLICT: 'RefusalWriteFailed',
        FINISHED_GOOD_CONTRACT_INVALID: 'RefusalWriteFailed',
        FINISHED_GOOD_WRITE_OUTCOME_AMBIGUOUS: 'RefusalReconciliationRequired',
        FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED: 'RefusalReconciliationRequired',
        FINISHED_GOOD_BINDING_INVARIANT_VIOLATION: 'RefusalBindingInvariant',
        FINISHED_GOOD_NOT_FOUND: 'ErrorNotFound',
        // The store and the legal-entity scope.
        PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE: 'RefusalStoreUnavailable',
        // FG FIX2 K4: a standalone database cannot run the burn's transaction — a retry cannot succeed, an administrator
        // must repair it (its own sentence; RefusalStoreUnavailable says "try again").
        CODE_RESERVATION_BURN_TRANSACTION_UNAVAILABLE: 'RefusalBurnTransactionUnavailable',
        PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID: 'RefusalScopeUnavailable',
        LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE: 'RefusalScopeUnavailable',
        LEGAL_ENTITY_SCOPE_PROVIDER_TIMEOUT: 'RefusalScopeUnavailable',
        LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID: 'RefusalScopeUnavailable',
        LEGAL_ENTITY_SCOPE_FORBIDDEN: 'ErrorForbidden',
        LEGAL_ENTITY_SCOPE_REQUEST_INVALID: 'ErrorValidation',
        LEGAL_ENTITY_SCOPE_UNAUTHENTICATED: 'ErrorUnauthorized',
        TRUSTED_SUBJECT_REQUIRED: 'ErrorUnauthorized',
        TENANT_CONTEXT_REQUIRED: 'ErrorUnauthorized'
    });
    // FG FIX2 item 4 / K3: MDM adds this marker to a refusal the SAME key can still finish (the proxy then keeps the
    // form attempt). The marker itself is never shown; whatever the first code, the sentence is RefusalRetrySameForm
    // (send the same form again, no reload — a reload draws a new key). Without the marker nothing changes.
    const retrySameKeyMarker = 'FINISHED_GOOD_CREATION_RETRY_SAME_KEY';
    const isCode = (value) => /^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+$/.test(value);
    const statusMessage = (status) => ({
        400: L.ErrorValidation,
        401: L.ErrorUnauthorized,
        403: L.ErrorForbidden,
        404: L.ErrorNotFound,
        409: L.ErrorConflict,
        500: L.ErrorGateway,
        502: L.ErrorGateway,
        503: L.ErrorGateway,
        504: L.ErrorTimeout
    })[status] || L.ErrorGateway;
    // A code → its sentence (or the status's, when unmapped — said on the console by status only); a sentence the
    // server already wrote (the proxy's own: ErrorInvalidFormAttempt, ErrorTimeout, GskuRequired…) is kept.
    const failureSentence = (payload, status, fallback) => {
        const errors = payload?.errors || payload?.Errors || [];
        if (Array.isArray(errors) && errors.some((error) => typeof error === 'string' && error.trim() === retrySameKeyMarker)) {
            return L.RefusalRetrySameForm || fallback || statusMessage(status);
        }
        const first = Array.isArray(errors)
            ? errors.find((error) => typeof error === 'string' && error.trim() && error.trim() !== retrySameKeyMarker)
            : '';
        if (!first) return fallback || statusMessage(status);
        const value = first.trim();
        if (!isCode(value)) return value;
        const key = refusalKeys[value];
        if (!key) console.warn('FinishedGoods: a refusal without a sentence mapping', status);
        return (key && L[key]) || fallback || statusMessage(status);
    };
    const getErrorMessage = async (response) => {
        let payload = {};
        try { payload = await response.json(); } catch (error) { }
        return failureSentence(payload, response.status);
    };
    // FG FIX1 part B item 5: the next form attempt comes only from the server's answer — a new one after 201 or a
    // definite refusal (so "try again" sends a new key), the same one after 202 / 5xx / a timeout / a refusal MDM
    // marks as resumable (a replay). FG FIX2 K1c: the proxy binds the attempt to its GSKU, so the same attempt sent for
    // another GSKU comes back as a new one.
    const adoptFormAttempt = (payload) => {
        const nextToken = payload?.formAttemptToken || payload?.FormAttemptToken;
        const tokenInput = document.getElementById('formAttemptToken');
        if (nextToken && tokenInput) tokenInput.value = nextToken;
    };
    const handleUnauthorized = () => {
        window.DtDefaults?.handleUnauthorized?.();
        const error = new Error('auth-refresh-in-progress');
        error.authHandled = true;
        throw error;
    };
    // The list's own failed read (jQuery, through the factory): the same sentence, never a code.
    const readXhrPayload = (xhr) => {
        if (xhr?.responseJSON && typeof xhr.responseJSON === 'object') return xhr.responseJSON;
        try { return JSON.parse(xhr?.responseText || '{}'); } catch (error) { return {}; }
    };
    const onListError = function (xhr) {
        const status = Number(xhr?.status) || 0;
        if (status === 401) {
            if (!this?._retried) window.DtDefaults?.handleUnauthorized?.(this);
            return;
        }
        if (xhr?.statusText === 'abort') return;
        window.showToast?.(failureSentence(readXhrPayload(xhr), status), 'error');
    };

    /*
     * The wire. The factory writes start · length · search · orderBy/orderDir · draw; the proxy forwards the query
     * as it is and MDM reads only pageNumber · pageSize · search. So the page query is rewritten before it leaves
     * (jQuery has already appended it to the URL when beforeSend runs): start/length → pageNumber/pageSize, search
     * kept, draw kept (the factory reads its draw back from this URL), every sort key dropped — MDM always returns
     * canonical code ascending and honours no other order.
     */
    const toMdmPageUrl = (url) => {
        const text = String(url || '');
        const at = text.indexOf('?');
        const base = at === -1 ? text : text.slice(0, at);
        const source = new URLSearchParams(at === -1 ? '' : text.slice(at + 1));
        // Already MDM's shape (DtDefaults' 401 retry sends the same settings again): left as it is.
        if (source.has('pageNumber') && !source.has('start')) return text;
        const requested = Number(source.get('length'));
        const pageSize = requested > 0 ? Math.min(Math.floor(requested), maxPageSize) : 20;
        const start = Math.max(0, Number(source.get('start')) || 0);
        const pageNumber = Math.min(Math.floor(start / pageSize) + 1, maxPageNumber);
        const query = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(pageSize) });
        const search = normalizeSearch(source.get('search') || '');
        if (search) query.set('search', search);
        if (source.has('draw')) query.set('draw', source.get('draw'));
        if (source.has('_')) query.set('_', source.get('_'));
        return `${base}?${query.toString()}`;
    };
    // MDM's page `{ items, pageNumber, pageSize, totalCount }` gains the component's counts (one count: the rows
    // matching the search). Called by the factory with the parsed envelope before it reads the counts.
    const adaptPage = (json) => {
        const page = json?.data || json?.Data;
        if (!page || typeof page !== 'object') return;
        const count = Number(page.totalCount ?? page.TotalCount);
        if (!Number.isFinite(count)) return;
        if (page.total === undefined) page.total = count;
        if (page.filteredTotal === undefined) page.filteredTotal = count;
    };
    /*
     * BL-440 trap: a saved view may carry a sort MDM cannot honour (it has no orderBy). No column is sortable, and a
     * saved order other than the base order is put back to it on load (no request: MDM's order is the same either
     * way); the next Save View writes the base order.
     */
    const isBaseOrder = (order) => {
        const pairs = Array.isArray(order) && Array.isArray(order[0]) ? order : (Array.isArray(order) && order.length ? [order] : []);
        return pairs.length === 1 && Number(pairs[0][0]) === baseOrder[0][0]
            && String(pairs[0][1]).toLowerCase() === baseOrder[0][1];
    };
    const clearUnservableOrder = (api) => {
        try {
            if (typeof api?.order === 'function' && !isBaseOrder(api.order())) api.order(baseOrder);
        } catch (error) { }
    };

    /*
     * No silent partial export in a GxP list: until BL-452 gives this list a server export (every matching row), each
     * Print / CSV / Excel / PDF / Copy entry says plainly that it writes only the page on screen. The list component
     * chains the page's `initComplete`, so this is done without touching the shared files.
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

    const formatDate = (dateValue) => dateValue
        ? new Intl.DateTimeFormat(document.documentElement.lang || undefined, {
            dateStyle: 'medium', timeStyle: 'short'
        }).format(new Date(dateValue))
        : L.NotAvailable;

    const populateDetails = async (id) => {
        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            if (response.status === 401) handleUnauthorized();
            if (!response.ok) throw new Error(await getErrorMessage(response));
            const detail = unwrapData(await response.json());
            const value = (camel, pascal) => valueOf(detail, camel, pascal);
            const setText = (elementId, fieldValue) => {
                const element = document.getElementById(elementId);
                if (element) element.textContent = fieldValue === null || fieldValue === undefined || fieldValue === ''
                    ? L.NotAvailable
                    : String(fieldValue);
            };
            const code = value('canonicalCode', 'CanonicalCode');
            setText('oc-title', code);
            setText('oc-subtitle', value('gskuCanonicalCode', 'GskuCanonicalCode'));
            setText('oc-id', value('id', 'Id'));
            setText('oc-code', code);
            setText('oc-gsku-code', value('gskuCanonicalCode', 'GskuCanonicalCode'));
            setText('oc-version', value('version', 'Version'));
            setText('oc-created-at', formatDate(value('createdAt', 'CreatedAt')));
            setText('oc-updated-at', formatDate(value('updatedAt', 'UpdatedAt')));
            // m9: the badge shows the cancellation disposition ahead of the stored Draft (classes only, no styles).
            const statusElement = document.getElementById('oc-status');
            if (statusElement) {
                const item = lifecyclePresentation(
                    value('lifecycleStatus', 'LifecycleStatus'),
                    value('cancellationDisposition', 'CancellationDisposition'));
                statusElement.className = `badge ${item.class}`;
                statusElement.textContent = item.title || L.Unknown;
            }
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
        }
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
                    const pageNumber = Math.max(1, Math.min(Number(params.data?.page) || 1, maxPageNumber));
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
                // FG FIX1 item 4: the same attempt until MDM answers 201 — a repeat is a replay, not a second draft.
                body.set('FormAttemptToken', document.getElementById('formAttemptToken')?.value || '');
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
                    let failure = {};
                    try { failure = await response.json(); } catch (error) { }
                    adoptFormAttempt(failure);
                    throw new Error(failureSentence(failure, response.status));
                }
                const payload = await response.json();
                adoptFormAttempt(payload);
                const draft = unwrapData(payload);
                const code = draft.canonicalCode || draft.CanonicalCode || '';
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                dt?.ajax.reload(null, false);
                if (response.status === 202) {
                    // FG FIX2 K1: MDM's 202 reconciliation (success false, FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED
                    // + its cause) is a warning in its own sentence — send the same form again; the attempt was kept.
                    const unfinished = payload?.success === false || payload?.Success === false;
                    const message = code
                        ? (L.CreatePendingWithCode || '').replace('{0}', code)
                        : (unfinished ? failureSentence(payload, response.status, L.CreatePending) : L.CreatePending);
                    window.showToast?.(message || L.CreatePending, 'warning');
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

    const renderRowActions = (row) => window.DitenDataTable.renderActions([{
        key: 'quickView',
        className: 'js-quick-view',
        text: L.QuickView,
        icon: 'bx bx-show',
        attrs: { 'data-id': valueOf(row, 'id', 'Id'), title: L.QuickView }
    }]);

    // The createList options of the register — one place, so the wire and the saved-view keys are measurable.
    const buildListOptions = () => ({
        tableEl,
        dataMode: 'server',
        ajax: {
            url: endpoint,
            type: 'GET',
            headers: getAuthHeaders(),
            error: onListError,
            beforeSend: function (xhr, settings) { settings.url = toMdmPageUrl(settings.url); }
        },
        onResponse: adaptPage,
        // The quick view stays the register's own (a fresh detail read per row); the factory dispatches it.
        actions: { onRowAction: { quickView: (ctx) => { if (ctx?.id) populateDetails(String(ctx.id)); } } },
        toolbar: {
            addNewText: canCreate ? L.AddNew : '',
            onAddNew: () => openCreate(),
            exportColumns: saveViewColumnIndexes,
            colvisColumns: saveViewColumnIndexes
        },
        // The code search is the list search (MDM matches the Finished Good or the GSKU canonical code by prefix).
        filters: {
            hostId: 'inlineFilterHost',
            collapseId: 'inlineFilterCollapse',
            applyBtn: 'btnFilterApply',
            resetBtn: 'btnFilterReset',
            fields: []
        },
        // Same keys as before the migration: every saved Finished Good view still loads.
        savedView: {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            saveViewColumnIndexes,
            defaultVisibleColumnIndexes: saveViewColumnIndexes,
            baseOrder
        },
        config: {
            pageLength: 20,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            // Chained by the list component after its own initComplete (which has applied the saved view).
            initComplete: function () {
                const api = this.api();
                clearUnservableOrder(api);
                bindCodeSearch(api);
                labelExportsCurrentPageOnly(api);
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'canonicalCode', name: 'canonicalCode' },
                { data: 'gskuCanonicalCode', name: 'gskuCanonicalCode' },
                { data: 'lifecycleStatus', name: 'lifecycleStatus' },
                { data: 'createdAt', name: 'createdAt' },
                { data: null, name: 'action' }
            ],
            // MDM has no orderBy: no column is sortable, so no sort key is ever sent or offered.
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, orderable: false, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { targets: 2, orderable: false, render: escapeHtml },
                { targets: 3, orderable: false, render: (data, type, row) => renderLifecycle(data, row) },
                { targets: 4, orderable: false, render: (data) => escapeHtml(formatDate(data)) },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => renderRowActions(row)
                }
            ]
        }
    });

    /*
     * The inline filter's code box is the list search. Apply: the box is written into the table search in the
     * CAPTURE phase on the document — before the factory's own Apply handler on the button redraws and recomputes
     * Save View. Reset: the box is cleared (the factory restores the baseline, search included).
     */
    const bindCodeSearch = (api) => {
        const input = document.getElementById('filterCanonicalCode');
        if (input) input.value = normalizeSearch(api.search());
        document.addEventListener('click', (event) => {
            if (!event.target?.closest?.('#btnFilterApply')) return;
            api.search(normalizeSearch(document.getElementById('filterCanonicalCode')?.value));
        }, true);
        document.getElementById('btnFilterReset')?.addEventListener('click', () => {
            const box = document.getElementById('filterCanonicalCode');
            if (box) box.value = '';
        });
    };

    const initDataTable = async () => {
        if (!tableEl || !window.DitenDataTable?.createList) return;
        list = await window.DitenDataTable.createList(buildListOptions());
        dt = list.dt;
    };

    const bindEvents = () => {
        document.getElementById('btnSaveFinishedGood')?.addEventListener('click', submitCreate);
    };

    return {
        init: async () => {
            bindEvents();
            await initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => FinishedGoodsList.init());
