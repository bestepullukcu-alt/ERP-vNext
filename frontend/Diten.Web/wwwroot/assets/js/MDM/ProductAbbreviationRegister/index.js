'use strict';

const ProductAbbreviationRegisterList = (function () {
    /*
     * WP-MDM-ABB-LIST-01 (BL-440) — the register stands on the list component (DitenDataTable.createList, SERVER mode):
     * the factory owns the table, the inline filter, Save View (same moduleKey/pageKey as before, so saved views still
     * load), the toolbar and the server wire (start/length/search/orderBy/orderDir/globalProductId → the MVC proxy →
     * MDM). This file keeps only the register's own business: the product picker, renderers, the action menu, quick view
     * with its audit evidence, and the request / correction / retirement / cancel flows with their FIX1–FIX4 sentences.
     * No selection column and no bulk action: an abbreviation is never changed in bulk (GxP, HasSelection = false).
     */
    let list = null;
    let productHasOpenEntry = false;
    const endpoint = '/MDM/ProductAbbreviationRegister/api';
    const root = document.getElementById('product-abbreviation-register');
    const tableEl = document.getElementById('dt-product-abbreviation-register');
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'ProductAbbreviationRegister' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5];
    const baseOrder = [[2, 'asc']];
    const L = window.L10n || {};
    const permissions = {
        selectProduct: root?.dataset.canSelectProduct === 'true',
        request: root?.dataset.canRequest === 'true',
        correct: root?.dataset.canCorrect === 'true',
        retire: root?.dataset.canRetire === 'true',
        audit: root?.dataset.canAudit === 'true',
        cancel: root?.dataset.canCancel === 'true',
        decide: root?.dataset.canDecide === 'true'
    };
    const taskCenterUrl = root?.dataset.taskCenterUrl || '/WorkCenterNext';
    /*
     * FIX1 S4 — EVERY code the register can answer, mapped to its own sentence (seven languages, key parity is
     * tested). A refusal the reader cannot act on by reloading must not say "reload": the code taken, the value
     * unchanged and the closed request each say what to do instead.
     */
    const ERROR_KEYS = Object.freeze({
        ABBREVIATION_ACTOR_NOT_DIRECT_TENANT_HUMAN: 'ErrorForbidden',
        ABBREVIATION_ACTOR_UNAUTHENTICATED: 'ErrorForbidden',
        ABBREVIATION_TENANT_CONTEXT_UNTRUSTED: 'ErrorForbidden',
        ABBREVIATION_PERMISSION_DENIED: 'ErrorForbidden',
        ABBREVIATION_CANCEL_NOT_REQUEST_OWNER: 'ErrorCancelNotOwner',
        ABBREVIATION_MAKER_CHECKER_VIOLATION: 'ErrorMakerChecker',
        ABBREVIATION_GRAMMAR_INVALID: 'ErrorValidation',
        ABBREVIATION_ALREADY_ALLOCATED: 'ErrorAlreadyAllocated',
        ABBREVIATION_ALLOCATION_FAILED: 'ErrorAlreadyAllocated',
        ABBREVIATION_CORRECTION_VALUE_UNCHANGED: 'ErrorCorrectionUnchanged',
        ABBREVIATION_REQUEST_ALREADY_CLOSED: 'ErrorRequestClosed',
        ABBREVIATION_PRODUCT_ALREADY_HAS_ENTRY: 'ErrorProductHasEntry',
        ACTIVE_ABBREVIATION_CONFLICT: 'ErrorProductHasEntry',
        ABBREVIATION_CORRECTION_PENDING: 'ErrorCorrectionPending',
        ABBREVIATION_RETIREMENT_PENDING: 'ErrorRetirementPending',
        ABBREVIATION_NOT_PENDING: 'ErrorNotPending',
        ABBREVIATION_TRANSITION_FORBIDDEN: 'ErrorNotPending',
        ABBREVIATION_CORRECTION_FORMER_NOT_ACTIVE: 'ErrorFormerNotActive',
        ABBREVIATION_CORRECTION_REQUIRES_ACTIVE: 'ErrorCorrectionRequiresActive',
        ABBREVIATION_RETIREMENT_REQUEST_NOT_FOUND: 'ErrorRetirementRequestNotFound',
        ABBREVIATION_IDEMPOTENCY_KEY_REUSED: 'ErrorKeyReused',
        ABBREVIATION_IDEMPOTENCY_CONFLICT: 'ErrorKeyReused',
        ABBREVIATION_NOT_FOUND: 'ErrorNotFound',
        GLOBAL_PRODUCT_NOT_FOUND: 'ErrorProductNotFound',
        ABBREVIATION_PERSISTENCE_UNAVAILABLE: 'ErrorPersistenceUnavailable',
        // FIX4 item 6 — the server cannot run the transaction every register write needs: the same "try again later",
        // not a gateway failure.
        PRODUCT_ABBREVIATION_TRANSACTIONS_UNAVAILABLE: 'ErrorPersistenceUnavailable',
        ABBREVIATION_EVIDENCE_RECONCILIATION_REQUIRED: 'ErrorReconciliation',
        ABBREVIATION_REGISTER_RECONCILIATION_REQUIRED: 'ErrorReconciliation',
        ABBREVIATION_REGISTER_CONFLICT: 'ErrorConflict',
        ABBREVIATION_CORRECTION_CONFLICT: 'ErrorConflict',
        ABBREVIATION_FORMER_EXPECTED_VERSION_REQUIRED: 'ErrorConflict',
        ABBREVIATION_WRITE_FAILED: 'ErrorConflict',
        ABBREVIATION_LEDGER_INVARIANT_VIOLATION: 'ErrorGateway',
        CONCURRENCY_CONFLICT: 'ErrorConflict',
        ABBREVIATION_ALREADY_DECIDED_BY_ANOTHER: 'ErrorDecidedByAnother',
        ABBREVIATION_IDEMPOTENCY_KEY_INVALID: 'ErrorKeyInvalid',
        TENANT_CONTEXT_REQUIRED: 'ErrorScopeUnavailable',
        PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID: 'ErrorScopeUnavailable'
    });

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const normalizeString = (value) => typeof value === 'string' ? value.trim() : '';
    const unwrapData = (payload) => payload?.data ?? payload?.Data ?? null;
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const antiForgeryToken = () => root?.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const canRequest = () => permissions.request && permissions.selectProduct;
    const isStatus = (row, name, number) => row.lifecycleStatus === name || row.lifecycleStatus === number;
    // FIX1 B3 — one pending change per code: no correction or retirement offered while either one waits.
    const canActOnActive = (row, permission) => permission && isStatus(row, 'ACTIVE', 1)
        && !row.retirementPending && !row.correctionPending;
    const requireSuccess = async (response) => {
        if (response.status === 401) handleUnauthorized();
        if (!response.ok) throw new Error(await getErrorMessage(response));
        const payload = await response.json();
        if (response.status === 202 || (payload?.isSuccessful ?? payload?.IsSuccessful) !== true) {
            const code = (payload?.errors ?? payload?.Errors ?? []).find?.(Boolean);
            throw new Error(L[ERROR_KEYS[code]] || (response.status === 202 ? L.ErrorReconciliation : L.ErrorGateway));
        }
        return payload;
    };

    // ── The product filter: a paged MDM picker that writes the list component's two filter fields ─────────────
    const productIdField = () => document.getElementById('filterGlobalProductId');
    const productTextField = () => document.getElementById('filterGlobalProductText');
    const appliedProductId = () => normalizeString(list?.appliedFilters?.globalProductId);
    const appliedProductText = () => normalizeString(list?.appliedFilters?.globalProductText) || appliedProductId();
    const syncProductSelect = (selector, productId, text) => {
        const select = document.querySelector(selector);
        if (!select) return;
        if (productId && !Array.from(select.options).some((option) => option.value === productId))
            select.add(new Option(text || productId, productId, true, true));
        $(select).val(productId || null).trigger('change.select2');
    };
    // The picker → the staged filter (Apply makes it the applied one); the factory writing a saved / reset filter →
    // the picker's display. Each direction writes the other side without re-entering it.
    const bindProductFilterFields = () => {
        $('#filterGlobalProduct').on('change', function () {
            const idField = productIdField();
            const textField = productTextField();
            if (idField) idField.value = normalizeString($(this).val());
            if (textField) textField.value = idField?.value ? normalizeString($(this).find('option:selected').text()) : '';
        });
        // The factory writes a filter back with jQuery (.val().trigger('change')): a jQuery handler hears it.
        $(productIdField()).on('change', () => {
            syncProductSelect('#filterGlobalProduct', normalizeString(productIdField()?.value), normalizeString(productTextField()?.value));
        });
    };
    // A product chosen outside the filter bar (a deep link, a request just made): staged, then applied by the bar's
    // own Apply — one path for every applied filter.
    const applyProductFilter = (productId, productText) => {
        const idField = productIdField();
        const textField = productTextField();
        if (!idField || !textField) return;
        textField.value = productText || productId;
        idField.value = productId;
        syncProductSelect('#filterGlobalProduct', productId, textField.value);
        document.getElementById('btnFilterApply')?.click();
    };

    const getErrorMessage = async (response) => {
        let payload = {};
        try { payload = await response.json(); } catch (error) { /* bounded status mapping below */ }
        const errors = payload?.errors ?? payload?.Errors ?? [];
        const code = payload?.errorCode ?? (Array.isArray(errors) ? errors.find(Boolean) : '');
        return L[ERROR_KEYS[code]] || ({ 400: L.ErrorValidation, 403: L.ErrorForbidden, 404: L.ErrorNotFound, 409: L.ErrorConflict })[response.status] || L.ErrorGateway;
    };
    const handleUnauthorized = () => {
        window.DtDefaults?.handleUnauthorized?.();
        const error = new Error('auth-refresh-in-progress');
        error.authHandled = true;
        throw error;
    };

    const initSelector = (selector, dropdownParent) => {
        const $selector = $(selector);
        if (!permissions.selectProduct) { $selector.prop('disabled', true); return; }
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
        document.getElementById('oc-global-product').textContent = appliedProductText();
        document.getElementById('oc-abbreviation').textContent = row.abbreviation;
        document.getElementById('oc-version').textContent = String(row.version);
        document.getElementById('oc-retirement-pending').textContent = row.retirementPending ? L.Yes : L.No;
        const status = document.getElementById('oc-status');
        if (status) status.outerHTML = renderLifecycle(row.lifecycleStatus).replace('<span ', '<span id="oc-status" ');
        loadEvidence(row.id);
    };

    const requestRetirement = (row) => {
        if (!canActOnActive(row, permissions.retire)) return;
        window.showConfirm?.(L.AreYouSure, async (reason) => {
          try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(row.id)}/retirement-requests`, {
                method: 'POST', credentials: 'same-origin',
                headers: { ...getAuthHeaders(), 'Content-Type': 'application/json', RequestVerificationToken: antiForgeryToken() },
                body: JSON.stringify({ expectedVersion: row.version, reason })
            });
            await requireSuccess(response);
            list?.reload('RequestSuccess');
          } catch (error) { if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error'); }
        }, { type: 'warning', showInput: true, inputRequired: true, inputLabel: L.Reason, inputValidationMessage: L.ErrorValidation,
            inputValidator: (value) => !normalizeString(value) || value.length > 512 ? L.ErrorValidation : null,
            confirmButtonText: L.Apply });
    };

    const requestCorrection = (row) => {
        if (!canActOnActive(row, permissions.correct)) return;
        window.showConfirm?.(L.RequestCorrection, (replacement) => {
            const replacementAbbreviation = normalizeString(replacement);
            window.showConfirm?.(L.RequestCorrection, async (reason) => {
                try {
                    const response = await fetch(`${endpoint}/${encodeURIComponent(row.id)}/corrections`, {
                        method: 'POST', credentials: 'same-origin',
                        headers: { ...getAuthHeaders(), 'Content-Type': 'application/json', RequestVerificationToken: antiForgeryToken() },
                        body: JSON.stringify({ expectedVersion: row.version, replacementAbbreviation, reason })
                    });
                    await requireSuccess(response);
                    list?.reload('RequestSuccess');
                } catch (error) { if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error'); }
            }, { type: 'warning', entityName: replacementAbbreviation, showInput: true, inputRequired: true,
                inputLabel: L.Reason, inputValidationMessage: L.ErrorValidation,
                inputValidator: (value) => !normalizeString(value) || value.length > 512 ? L.ErrorValidation : null,
                confirmButtonText: L.Apply });
        }, { type: 'primary', showInput: true, inputType: 'text', inputLabel: L.ReplacementAbbreviation,
            inputAttributes: { maxlength: 3 }, inputRequired: true, inputValidationMessage: L.ErrorValidation,
            inputValidator: (value) => /^[A-Za-z]{3}$/.test(normalizeString(value)) ? null : L.ErrorValidation,
            confirmButtonText: L.Apply });
    };

    const cancelRequest = (row) => {
        if (!(permissions.cancel && row.requestedByCurrentUser && isStatus(row, 'REQUESTED', 0))) return;
        window.showConfirm?.(L.CancelRequest, async () => {
            try {
                const response = await fetch(`${endpoint}/${encodeURIComponent(row.id)}/cancel`, {
                    method: 'PATCH', credentials: 'same-origin',
                    headers: { ...getAuthHeaders(), 'Content-Type': 'application/json', RequestVerificationToken: antiForgeryToken() },
                    body: JSON.stringify({ expectedVersion: row.version })
                });
                await requireSuccess(response);
                list?.reload('RequestSuccess');
            } catch (error) { if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error'); }
        }, { type: 'warning', entityName: row.abbreviation, confirmButtonText: L.Apply });
    };

    // FIX1 B2 — a decision is taken in the Task Center (Pack:922); the register page only leads there.
    const waitsForDecisionBySomeoneElse = (row) => permissions.decide
        && (isStatus(row, 'REQUESTED', 0) && !row.requestedByCurrentUser
            || row.retirementPending && !row.retirementRequestedByCurrentUser);

    const renderActions = (row) => {
        const actions = [{ key: 'quickView', className: 'js-quick-view', text: L.ViewDetails, icon: 'bx bx-show', attrs: { 'data-id': row.id } }];
        if (permissions.cancel && row.requestedByCurrentUser && isStatus(row, 'REQUESTED', 0))
            actions.push({ key: 'cancel', className: 'js-cancel-request', text: L.CancelRequest, icon: 'bx bx-x-circle', attrs: { 'data-id': row.id } });
        if (waitsForDecisionBySomeoneElse(row))
            actions.push({ key: 'taskCenter', className: 'js-open-task-center', text: L.OpenInTaskCenter, icon: 'bx bx-task', attrs: { 'data-id': row.id } });
        if (canActOnActive(row, permissions.correct))
            actions.push({ key: 'correct', className: 'js-request-correction', text: L.RequestCorrection, icon: 'bx bx-revision', attrs: { 'data-id': row.id } });
        if (canActOnActive(row, permissions.retire))
            actions.push({ key: 'retire', className: 'js-request-retirement', text: L.RequestRetirement, icon: 'bx bx-archive', attrs: { 'data-id': row.id } });
        return window.DitenDataTable.renderActions(actions);
    };

    // The createList options of the register — one place, so the wire and the saved-view keys are measurable.
    const buildListOptions = () => ({
        tableEl,
        dataMode: 'server',
        ajax: { url: `${endpoint}/list`, type: 'GET', headers: getAuthHeaders() },
        // FIX1 S2 — a product with an active or pending code is offered no new request: read from every answer.
        onResponse: (json) => {
            const items = unwrapData(json)?.items ?? [];
            productHasOpenEntry = items.some((row) => isStatus(row, 'ACTIVE', 1) || isStatus(row, 'REQUESTED', 0));
        },
        quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: showDetails, actionKey: 'quickView' },
        actions: {
            onRowAction: {
                cancel: ({ row }) => cancelRequest(row),
                taskCenter: () => { window.location.assign(taskCenterUrl); },
                correct: ({ row }) => requestCorrection(row),
                retire: ({ row }) => requestRetirement(row)
            }
        },
        toolbar: {
            addNewText: canRequest() ? L.AddNew : '',
            onAddNew: () => {
                if (!canRequest()) return;
                document.getElementById('formProductAbbreviationRequest')?.reset();
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
            },
            exportColumns: saveViewColumnIndexes,
            colvisColumns: saveViewColumnIndexes
        },
        filters: {
            hostId: 'inlineFilterHost',
            collapseId: 'inlineFilterCollapse',
            applyBtn: 'btnFilterApply',
            resetBtn: 'btnFilterReset',
            // The text before the id: when the factory writes a saved filter back, the id's change finds its name.
            fields: [
                { id: 'filterGlobalProductText', key: 'globalProductText', kind: 'text' },
                { id: 'filterGlobalProductId', key: 'globalProductId', kind: 'text' }
            ],
            loadOptions: async () => {
                initSelector('#filterGlobalProduct', document.body);
                bindProductFilterFields();
            }
        },
        // Same keys as before the migration: every saved register view still loads.
        savedView: {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            saveViewColumnIndexes,
            defaultVisibleColumnIndexes: saveViewColumnIndexes,
            baseOrder
        },
        config: {
            colReorder: { columns: ':gt(0):not(:last-child)' },
            // `data` names are the proxy's orderBy whitelist (abbreviation, lifecycleStatus, version, retirementPending).
            // The Global Product is the filter itself (one product per list): not sortable, never sent as a sort key.
            columns: [
                { data: 'id', name: 'control' },
                { data: 'globalProductId', name: 'globalProduct' },
                { data: 'abbreviation', name: 'abbreviation' },
                { data: 'lifecycleStatus', name: 'lifecycleStatus' },
                { data: 'version', name: 'version' },
                { data: 'retirementPending', name: 'retirementPending' },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, orderable: false, render: () => escapeHtml(appliedProductText()) },
                { targets: 2, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { targets: 3, render: renderLifecycle },
                { targets: 5, render: (value) => value ? L.Yes : L.No },
                { targets: -1, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3', render: (data, type, row) => renderActions(row) }
            ],
            initComplete: function () {
                initSelector('#requestGlobalProduct', document.getElementById('offcanvasCreateEdit'));
            },
            drawCallback: function () {
                document.querySelector('.add-new')?.classList.toggle('d-none', productHasOpenEntry);
            }
        }
    });

    const initDataTable = async () => {
        if (!tableEl || !window.DitenDataTable?.createList) return;
        list = await window.DitenDataTable.createList(buildListOptions());
        // A link from another page names the product: it becomes the applied filter, as if chosen in the bar.
        const linkedProduct = new URLSearchParams(window.location.search).get('globalProductId');
        if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(linkedProduct || ''))
            applyProductFilter(linkedProduct, linkedProduct);
    };

    const bindEvents = () => {
        document.getElementById('btnRequestAbbreviation')?.addEventListener('click', () => {
            if (!canRequest()) return;
            const form = document.getElementById('formProductAbbreviationRequest');
            if (!form?.checkValidity()) { form?.classList.add('was-validated'); return; }
            const productId = normalizeString($('#requestGlobalProduct').val());
            const productText = normalizeString($('#requestGlobalProduct option:selected').text());
            const abbreviation = normalizeString(document.getElementById('requestAbbreviation')?.value);
            if (productId && productId === appliedProductId() && productHasOpenEntry) {
                window.showToast?.(L.ErrorProductHasEntry, 'error');
                return;
            }
            window.showConfirm?.(L.RequestConfirmation, async () => {
              try {
                const body = new FormData(form);
                body.set('GlobalProductId', productId);
                body.set('Abbreviation', abbreviation);
                const response = await fetch(`${endpoint}/requests`, {
                    method: 'POST', credentials: 'same-origin',
                    headers: { ...getAuthHeaders(), RequestVerificationToken: antiForgeryToken() }, body
                });
                await requireSuccess(response);
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                window.showToast?.(L.RequestSuccess, 'success');
                // The list follows the product the request was made for: its own filter, applied.
                applyProductFilter(productId, productText);
              } catch (error) { if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error'); }
            }, { entityName: abbreviation, type: 'primary', confirmButtonText: L.Apply });
        });
    };

    return {
        init: async () => { bindEvents(); await initDataTable(); },
        // The options handed to the list component — read by the page's own tests.
        buildListOptions
    };
})();

document.addEventListener('DOMContentLoaded', () => ProductAbbreviationRegisterList.init());
