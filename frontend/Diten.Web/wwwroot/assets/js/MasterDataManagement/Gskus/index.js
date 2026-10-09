/**
 * MOD-0290 GSKU Register — tenant-shell Golden Slim lifecycle surface.
 * Browser traffic is restricted to the same-origin MVC proxy.
 */
'use strict';

const GskusList = (function () {
    /*
     * FIX3 B17 — the register stands on the list component (DitenDataTable.createList, server mode): the factory owns
     * the table, the inline filter, Save View (same moduleKey/pageKey as before, so saved views still load), the
     * toolbar and the server wire (start/length/search/orderBy/orderDir/lifecycleStatus → the MVC proxy → MDM). This
     * file keeps only the register's own business: renderers, the server-owned action menu, quick view, and the
     * create / edit / correction / retirement / submit / withdraw flows.
     */
    let list = null;
    let dt;
    let editorId = '';
    let editorGskuVersion = null;
    let editorRevisionVersion = null;
    let editorMode = 'create';
    const lifecycleRequests = new Set();
    const endpoint = '/MasterDataManagement/Gskus/api';
    const tableEl = document.querySelector('.datatables-gskus');
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'Gskus' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5];
    const baseOrder = [[1, 'asc']];
    // The product picker pages through MDM (create-options: pageSize 1–50); it never holds the whole catalogue.
    const productPickerPageSize = 20;
    const productPickerDelayMs = 300;
    // The create form's options read needs the UoMs and only to know a product exists; the picker pages the rest.
    const createOptionsProbeSize = 1;
    const L = window.L10n || {};
    const permissionHost = document.querySelector('[data-can-create]');
    const canCreate = permissionHost?.getAttribute('data-can-create') === 'true';
    const actionOrder = ['DETAILS', 'EDIT', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT', 'REPEAT_REQUEST'];
    const supportedActions = new Set(['DETAILS', 'EDIT', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT', 'REPEAT_REQUEST']);
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const normalizeSearch = (value) => typeof value === 'string' ? value.trim().slice(0, 100) : '';
    const unwrapData = (payload) => payload?.data || payload?.Data || {};
    const valueOf = (source, camel, pascal) => source?.[camel] ?? source?.[pascal];
    const readVersion = (source, camel, pascal) => {
        const value = valueOf(source, camel, pascal);
        return typeof value === 'number' && Number.isSafeInteger(value) && value >= 0 ? value : NaN;
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
    const handleUnauthorized = () => {
        window.DtDefaults?.handleUnauthorized?.();
        const error = new Error('auth-refresh-in-progress');
        error.authHandled = true;
        throw error;
    };
    /*
     * FIX3 (B18 companion): the proxy marks a refusal whose MDM code has no sentence yet with `unmapped: true` (the
     * reader still gets the status's general sentence). That is a missing mapping, so the console says so — with the
     * status only; the body is never logged.
     */
    const warnIfUnmapped = (payload, status) => {
        if (payload?.unmapped === true) console.warn('GSKU: a refusal without a sentence mapping', status);
    };
    const statusMessage = (status) => ({
        400: L.ErrorValidation,
        401: L.ErrorUnauthorized,
        403: L.ErrorForbidden,
        404: L.ErrorNotFound,
        409: L.ErrorConflict,
        503: L.ErrorServiceUnavailable,
        504: L.ErrorTimeout
    })[status] || L.ErrorGateway;
    const failureSentence = (payload, status) => {
        warnIfUnmapped(payload, status);
        const errors = payload?.errors || payload?.Errors || [];
        if (Array.isArray(errors) && errors.find(Boolean)) return errors.find(Boolean);
        return statusMessage(status);
    };
    const getErrorMessage = async (response) => {
        let payload = {};
        try { payload = await response.json(); } catch (error) { }
        return failureSentence(payload, response.status);
    };
    // The list's own failed read (jQuery, through the factory): the same sentence and the same unmapped warning.
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

    const setText = (elementId, fieldValue) => {
        const element = document.getElementById(elementId);
        if (!element) throw new Error(L.ErrorGateway);
        element.textContent = fieldValue === null || fieldValue === undefined || fieldValue === ''
            ? L.NotAvailable
            : String(fieldValue);
    };
    const formatDate = (dateValue) => dateValue
        ? new Intl.DateTimeFormat(document.documentElement.lang || undefined, {
            dateStyle: 'medium', timeStyle: 'short'
        }).format(new Date(dateValue))
        : L.NotAvailable;
    const fetchDetail = async (id) => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
            credentials: 'same-origin', headers: getAuthHeaders()
        });
        if (response.status === 401) handleUnauthorized();
        if (!response.ok) throw new Error(await getErrorMessage(response));
        const detail = unwrapData(await response.json());
        if (response.status !== 200 || String(valueOf(detail, 'id', 'Id')).toLowerCase() !== String(id).toLowerCase()
            || !Number.isSafeInteger(readVersion(detail, 'gskuVersion', 'GskuVersion'))
            || !Number.isSafeInteger(readVersion(detail, 'revisionVersion', 'RevisionVersion'))) {
            throw new Error(L.ErrorGateway);
        }
        return detail;
    };
    const renderDetail = (detail, expectedId) => {
        const detailId = valueOf(detail, 'id', 'Id');
        const detailVersion = readVersion(detail, 'gskuVersion', 'GskuVersion');
        const revisionVersion = readVersion(detail, 'revisionVersion', 'RevisionVersion');
        const detailState = lifecycleCode(valueOf(detail, 'lifecycleStatus', 'LifecycleStatus'));
        const offcanvas = document.getElementById('offcanvasDetailsPreview');
        if (!detailId || String(detailId).toLowerCase() !== String(expectedId).toLowerCase()
            || !Number.isInteger(detailVersion) || detailVersion < 0
            || !Number.isInteger(revisionVersion) || revisionVersion < 0
            || detailState === 0 || !offcanvas) {
            throw new Error(L.ErrorGateway);
        }
        const code = valueOf(detail, 'canonicalCode', 'CanonicalCode');
        const productCode = valueOf(detail, 'globalProductCanonicalCode', 'GlobalProductCanonicalCode');
        const productName = valueOf(detail, 'globalProductName', 'GlobalProductName');
        const quantity = valueOf(detail, 'packQuantity', 'PackQuantity');
        const uom = valueOf(detail, 'packUomCode', 'PackUomCode');
        setText('oc-title', code);
        setText('oc-subtitle', valueOf(detail, 'revisionIdentifier', 'RevisionIdentifier'));
        setText('oc-id', detailId);
        setText('oc-code', code);
        setText('oc-global-product', `${productCode || ''}${productCode && productName ? ' — ' : ''}${productName || ''}`);
        setText('oc-revision', valueOf(detail, 'revisionIdentifier', 'RevisionIdentifier'));
        setText('oc-pack', `${quantity ?? ''}${quantity !== null && quantity !== undefined && uom ? ' ' : ''}${uom || ''}`);
        setText('oc-version', detailVersion);
        setText('oc-created-at', formatDate(valueOf(detail, 'createdAt', 'CreatedAt')));
        setText('oc-updated-at', formatDate(valueOf(detail, 'updatedAt', 'UpdatedAt')));
        // GSKU FIX1 (B2 b): the approval was granted and could not be applied — say why, in the reader's language.
        const notApplied = valueOf(detail, 'identityNotAppliedReason', 'IdentityNotAppliedReason');
        // FIX2 item 1: where the latest submit stands is said first — each state with what happens next.
        const submitState = valueOf(detail, 'identitySubmitState', 'IdentitySubmitState');
        const submitStateText = {
            SUBMIT_IN_FLIGHT: L.IdentityStateInFlight,
            RESUBMIT_REQUIRED: L.IdentityStateResubmitRequired,
            // FIX4 item 11: only the submitter's repeat settles it — anyone else is told so.
            RECONCILIATION_REQUIRED: readAvailableActions(detail).includes('SUBMIT')
                ? L.IdentityStateReconciliationRequired : L.IdentityStateReconciliationOtherUser,
            SUBMISSION_ENDED: L.IdentityStateSubmissionEnded
        }[submitState] || '';
        const notAppliedText = submitStateText || (!notApplied ? ''
            : notApplied === 'FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED' ? L.IdentityNotAppliedParent
            : notApplied === 'REFERENCE_DATA_CONTRACT_CONFLICT' || notApplied === 'REFERENCE_NOT_SELECTABLE'
                ? L.IdentityNotAppliedReference
            : notApplied === 'FIRST_GSKU_IDENTITY_SELF_APPROVAL_IGNORED' ? L.IdentityNotAppliedSelfApproval
            // FIX3 E1: the approval was closed without a decision — the draft is open again.
            : notApplied === 'FIRST_GSKU_IDENTITY_CLOSED_WITHOUT_DECISION' ? L.IdentityNotAppliedClosedWithoutDecision
            : L.IdentityNotAppliedGeneric)
            // FIX3 (CT decision 1): approved, and a unit retired afterwards — said as information.
            || (valueOf(detail, 'identityApprovalNote', 'IdentityApprovalNote')
                === 'FIRST_GSKU_IDENTITY_REFERENCE_RETIRED_AFTER_APPROVAL' ? L.IdentityApprovalNoteRetiredAfterApproval : '')
            // FIX4 item 1 (b): a correction / retirement request stuck in manual reconciliation — its requester can send
            // it again; everyone else is told who can.
            || (valueOf(detail, 'lifecycleRequestState', 'LifecycleRequestState') === 'STUCK'
                ? (readAvailableActions(detail).includes('REPEAT_REQUEST') ? L.LifecycleRequestStuckRequester
                    : L.LifecycleRequestStuckOther) : '');
        setText('oc-identity-not-applied', notAppliedText);
        document.getElementById('oc-identity-not-applied-row')?.classList.toggle('d-none', !notAppliedText);
        const statusElement = document.getElementById('oc-status');
        if (!statusElement) throw new Error(L.ErrorGateway);
        statusElement.outerHTML = renderLifecycle(valueOf(detail, 'lifecycleStatus', 'LifecycleStatus'))
            .replace('<span ', '<span id="oc-status" ');
        bootstrap.Offcanvas.getOrCreateInstance(offcanvas).show();
        return detailState;
    };
    const populateDetails = async (id) => {
        try {
            const detail = await fetchDetail(id);
            renderDetail(detail, id);
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
        }
    };

    const readAvailableActions = (detail) => {
        const raw = detail?.availableActions ?? detail?.AvailableActions;
        if (!Array.isArray(raw) || !raw.length) throw new Error(L.ErrorGateway);
        const actions = raw.map((value) => typeof value === 'string' ? value : '');
        if (actions.some((value) => !actionOrder.includes(value))
            || new Set(actions).size !== actions.length
            || actions[0] !== 'DETAILS'
            || actions.some((value, index) => index > 0
                && actionOrder.indexOf(value) <= actionOrder.indexOf(actions[index - 1]))) {
            throw new Error(L.ErrorGateway);
        }
        return actions;
    };
    const renderActions = (row) => {
        const id = valueOf(row, 'id', 'Id');
        return `<div class="dropdown"><button type="button" class="btn btn-icon dropdown-toggle hide-arrow js-gsku-actions-toggle" data-id="${escapeHtml(id)}" aria-expanded="false" title="${escapeHtml(L.Actions)}" aria-label="${escapeHtml(L.Actions)}"><i class="bx bx-dots-vertical-rounded icon-md"></i></button><div class="dropdown-menu dropdown-menu-end m-0 js-gsku-actions-menu"></div></div>`;
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
        REQUEST_CORRECTION: { key: 'requestCorrection', className: 'js-request-correction', icon: 'bx bx-revision', label: () => L.RequestCorrection },
        REQUEST_RETIREMENT: { key: 'requestRetirement', className: 'js-request-retirement', icon: 'bx bx-archive', label: () => L.RequestRetirement },
        // FIX4 item 1 (b): the requester's same request again, for a request stuck in manual reconciliation.
        REPEAT_REQUEST: { key: 'repeatRequest', className: 'js-repeat-request', icon: 'bx bx-refresh', label: () => L.RepeatRequest }
    };
    const renderFreshActionMenu = (menu, id, actions) => {
        menu.innerHTML = actions.filter((action) => supportedActions.has(action)).map((action) => {
            const { key, className, icon, label } = actionPresentation[action];
            return `<a href="javascript:void(0);" class="dropdown-item dt-action-item ${className}" data-row-action="${key}" data-id="${escapeHtml(id)}" data-action="${action}"><i class="${icon} dt-action-icon"></i>${escapeHtml(label())}</a>`;
        }).join('');
    };
    // The register's row-action handlers, by the keys above. A busy item (disabled) is never run twice.
    const rowAction = (run) => ({ id, trigger }) => {
        if (!id || trigger?.classList.contains('disabled')) return;
        run(String(id), trigger);
    };
    const rowActionHandlers = () => ({
        quickView: rowAction((id) => populateDetails(id)),
        editDraft: rowAction((id, trigger) => openEdit(id, trigger)),
        submitIdentity: rowAction((id, trigger) => requestLifecycle(id, 'SUBMIT', trigger)),
        withdrawApproval: rowAction((id, trigger) => requestLifecycle(id, 'WITHDRAW_APPROVAL', trigger)),
        requestCorrection: rowAction((id, trigger) => openCorrection(id, trigger)),
        requestRetirement: rowAction((id, trigger) => requestRetirement(id, trigger)),
        repeatRequest: rowAction((id, trigger) => repeatLifecycleRequest(id, trigger))
    });
    const loadActionMenu = async (toggle) => {
        const id = toggle?.dataset.id;
        const menu = toggle?.parentElement?.querySelector('.js-gsku-actions-menu');
        if (!id || !menu || toggle.classList.contains('disabled')) return;
        toggle.classList.add('disabled');
        try {
            const actions = readAvailableActions(await fetchDetail(id));
            renderFreshActionMenu(menu, id, actions);
            if (!menu.children.length) throw new Error(L.ErrorGateway);
            bootstrap.Dropdown.getOrCreateInstance(toggle).show();
        } catch (error) {
            menu.replaceChildren();
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
        } finally {
            toggle.classList.remove('disabled');
        }
    };

    /*
     * FIX3 B17 — the Global Product picker searches and pages through MDM instead of holding one fetched list (MDM's
     * default page of 20 used to be every product a maker could ever choose). The BL-531 transport contract:
     *  - select2 4.0.13 needs the transport to RETURN a request object (it reads `'status' in request` on failure and
     *    calls `abort()` when the reader types again) — without it the dropdown sits on "searching" forever;
     *  - a late answer (an older request, or an aborted one) is dropped by a sequence counter;
     *  - a failed read says a SENTENCE in the reader's language (errorLoading), never "no results";
     *  - MDM's `hasMore` is select2's `pagination.more` (the next page loads on scroll).
     * minimumInputLength 0: opening the picker asks for the first page only; typing narrows it (debounced 300 ms).
     */
    const createProductPickerOptions = ({ fetchPage, words = {}, pageSize = productPickerPageSize } = {}) => {
        let sequence = 0;
        let failure = '';
        const failureText = (result) => (typeof words.failure === 'function' ? words.failure(result) : '') || words.error || '';
        return {
            minimumInputLength: 0,
            ajax: {
                delay: productPickerDelayMs,
                transport: (params, success, fail) => {
                    const mine = ++sequence;
                    const request = { aborted: false, abort() { request.aborted = true; } };
                    const live = () => !request.aborted && mine === sequence;
                    const term = normalizeSearch(String(params?.data?.term ?? ''));
                    const page = Math.max(1, Math.floor(Number(params?.data?.page) || 1));
                    Promise.resolve()
                        .then(() => fetchPage({ term, page, pageSize }))
                        .then((result) => {
                            if (!live()) return;
                            if (!result || result.ok !== true) {
                                failure = failureText(result || { ok: false, status: 0 });
                                fail(result);
                                return;
                            }
                            failure = '';
                            success({
                                results: (Array.isArray(result.products) ? result.products : []).map((item) => ({
                                    id: String(valueOf(item, 'id', 'Id')),
                                    text: `${valueOf(item, 'canonicalCode', 'CanonicalCode')} — ${valueOf(item, 'globalProductName', 'GlobalProductName')}`
                                })),
                                pagination: { more: result.hasMore === true }
                            });
                        })
                        .catch((error) => {
                            if (!live()) return;
                            failure = failureText({ ok: false, status: 0, authHandled: !!error?.authHandled });
                            fail(error);
                        });
                    return request;
                }
            },
            language: {
                errorLoading: () => failure || words.error || '',
                noResults: () => words.noResults || '',
                searching: () => words.searching || '',
                loadingMore: () => words.loadingMore || ''
            }
        };
    };
    const fetchProductPage = async ({ term, page, pageSize }) => {
        const query = new URLSearchParams({ pageNumber: String(page), pageSize: String(pageSize) });
        if (term) query.set('search', term);
        const response = await fetch(`${endpoint}/create-options?${query.toString()}`, {
            credentials: 'same-origin', headers: getAuthHeaders()
        });
        if (response.status === 401) handleUnauthorized();
        if (!response.ok) return { ok: false, status: response.status, message: await getErrorMessage(response) };
        const data = unwrapData(await response.json());
        const products = data.globalProducts || data.GlobalProducts;
        if (!Array.isArray(products)) return { ok: false, status: 502, message: L.ErrorGateway };
        return { ok: true, products, hasMore: valueOf(data, 'hasMore', 'HasMore') === true };
    };
    const productPickerWords = () => ({
        error: L.ProductPickerLoadFailed,
        failure: (result) => result?.message || L.ProductPickerLoadFailed,
        noResults: L.ProductPickerNoResults,
        searching: L.ProductPickerSearching,
        loadingMore: L.ProductPickerLoadingMore
    });
    const initializeCreateSelects = () => {
        const $product = $('#globalProductId');
        if ($product.length && !$product.hasClass('select2-hidden-accessible')) {
            $product.select2(Object.assign({
                dropdownParent: $('#offcanvasCreateEdit'),
                width: '100%',
                allowClear: true,
                placeholder: $product.data('placeholder') || ''
            }, createProductPickerOptions({ fetchPage: fetchProductPage, words: productPickerWords() })));
        }
        const $uom = $('#packUomCode');
        if ($uom.length && !$uom.hasClass('select2-hidden-accessible')) {
            $uom.select2({
                dropdownParent: $('#offcanvasCreateEdit'),
                width: '100%',
                allowClear: true
            });
        }
    };
    const replaceOptions = (select, options) => {
        select.replaceChildren(new Option('', ''));
        options.forEach((option) => select.add(option));
        $(select).val(null).trigger('change');
    };
    // The UoMs (a short provider list) still come with the options read; the create read asks MDM for ONE product
    // only — enough to know there is something to choose — and the picker pages the products itself.
    const loadCreateOptions = async (detail = null, mode = 'create') => {
        const button = document.getElementById('btnSaveGsku');
        const globalProduct = document.getElementById('globalProductId');
        const uom = document.getElementById('packUomCode');
        if (!globalProduct || !uom) return false;
        if (button) button.disabled = true;
        globalProduct.disabled = true;
        uom.disabled = true;
        try {
            const optionTarget = detail
                ? `${endpoint}/${encodeURIComponent(editorId)}/${mode === 'edit' ? 'edit-options' : 'correction-options'}`
                : `${endpoint}/create-options?${new URLSearchParams({ pageNumber: '1', pageSize: String(createOptionsProbeSize) })}`;
            const response = await fetch(optionTarget, {
                credentials: 'same-origin', headers: getAuthHeaders()
            });
            if (response.status === 401) handleUnauthorized();
            if (!response.ok) throw new Error(await getErrorMessage(response));
            const data = unwrapData(await response.json());
            const products = data.globalProducts || data.GlobalProducts || [];
            const uoms = data.uoms || data.Uoms || [];
            if (!Array.isArray(uoms) || !uoms.length || !detail && !products.length) throw new Error(L.OptionsEmpty);
            if (detail && (String(valueOf(data, 'gskuId', 'GskuId')).toLowerCase() !== editorId.toLowerCase()
                || valueOf(data, 'gskuVersion', 'GskuVersion') !== editorGskuVersion
                || valueOf(data, 'revisionVersion', 'RevisionVersion') !== editorRevisionVersion)) {
                throw new Error(L.LifecycleStateChanged);
            }
            if (!detail) replaceOptions(globalProduct, []);
            replaceOptions(uom, [...uoms]
                .sort((left, right) => Number(valueOf(left, 'sortOrder', 'SortOrder')) - Number(valueOf(right, 'sortOrder', 'SortOrder')))
                .map((item) => {
                    const option = new Option(
                        valueOf(item, 'displayText', 'DisplayText'),
                        valueOf(item, 'code', 'Code'));
                    option.dataset.maximumDecimalPrecision = String(
                        valueOf(item, 'maximumDecimalPrecision', 'MaximumDecimalPrecision'));
                    return option;
                }));
            globalProduct.disabled = !!detail;
            uom.disabled = false;
            if (button) button.disabled = false;
            return true;
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            return false;
        }
    };
    const openCreate = async () => {
        const form = document.getElementById('formGsku');
        if (!form) return;
        editorId = '';
        editorGskuVersion = null;
        editorRevisionVersion = null;
        editorMode = 'create';
        form.reset();
        form.classList.remove('was-validated');
        document.getElementById('offcanvasCreateEditLabel').textContent = L.FormTitleCreate;
        document.getElementById('btnSaveGskuText').textContent = L.Save;
        initializeCreateSelects();
        document.getElementById('globalProductId').disabled = false;
        $('#globalProductId, #packUomCode').val(null).trigger('change');
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        await loadCreateOptions();
    };
    const setBusy = (button, busy) => {
        if (button) button.disabled = busy;
        button?.classList.toggle('disabled', busy);
        button?.setAttribute('aria-disabled', busy ? 'true' : 'false');
    };
    const openExistingEditor = async (id, actionButton, mode) => {
        setBusy(actionButton, true);
        try {
            const detail = await fetchDetail(id);
            const requiredAction = mode === 'correction' ? 'REQUEST_CORRECTION' : 'EDIT';
            if (!readAvailableActions(detail).includes(requiredAction)) throw new Error(L.LifecycleStateChanged);
            const detailId = valueOf(detail, 'id', 'Id');
            const gskuVersion = readVersion(detail, 'gskuVersion', 'GskuVersion');
            const revisionVersion = readVersion(detail, 'revisionVersion', 'RevisionVersion');
            if (!detailId || String(detailId).toLowerCase() !== String(id).toLowerCase()
                || !Number.isInteger(gskuVersion) || gskuVersion < 0
                || !Number.isInteger(revisionVersion) || revisionVersion < 0) {
                throw new Error(L.ErrorConflict);
            }

            editorId = String(id);
            editorGskuVersion = gskuVersion;
            editorRevisionVersion = revisionVersion;
            editorMode = mode;
            const form = document.getElementById('formGsku');
            form?.classList.remove('was-validated');
            initializeCreateSelects();
            const globalProduct = document.getElementById('globalProductId');
            const uom = document.getElementById('packUomCode');
            if (!form || !globalProduct || !uom) throw new Error(L.ErrorGateway);
            const productId = valueOf(detail, 'globalProductId', 'GlobalProductId');
            const productCode = valueOf(detail, 'globalProductCanonicalCode', 'GlobalProductCanonicalCode');
            const productName = valueOf(detail, 'globalProductName', 'GlobalProductName');
            const uomCode = valueOf(detail, 'packUomCode', 'PackUomCode');
            document.getElementById('packQuantity').value = String(valueOf(detail, 'packQuantity', 'PackQuantity'));
            document.getElementById('offcanvasCreateEditLabel').textContent = mode === 'correction'
                ? L.FormTitleCorrection : L.FormTitleEdit;
            document.getElementById('btnSaveGskuText').textContent = mode === 'correction'
                ? L.RequestCorrection : L.UpdateDraft;
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
            if (!await loadCreateOptions(detail, mode)) {
                editorId = '';
                editorGskuVersion = null;
                editorRevisionVersion = null;
                editorMode = 'create';
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                return;
            }
            globalProduct.replaceChildren(new Option(
                    `${productCode || ''}${productCode && productName ? ' — ' : ''}${productName || ''}`,
                    productId));
            if (![...uom.options].some((option) => option.value === String(uomCode))) {
                throw new Error(L.ErrorConflict);
            }
            $(globalProduct).val(String(productId)).trigger('change');
            globalProduct.disabled = true;
            $(uom).val(String(uomCode)).trigger('change');
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
        } finally {
            setBusy(actionButton, false);
        }
    };
    const openEdit = (id, actionButton) => openExistingEditor(id, actionButton, 'edit');
    const openCorrection = (id, actionButton) => openExistingEditor(id, actionButton, 'correction');
    const validateQuantity = (quantity, selectedUom) => {
        const match = String(quantity).match(/^\d+(?:\.(\d+))?$/);
        if (!match || Number(quantity) <= 0) return L.PackQuantityRequired;
        const precision = Number(selectedUom?.dataset.maximumDecimalPrecision);
        if (!Number.isInteger(precision) || (match[1]?.length || 0) > precision) {
            return L.PackQuantityPrecision.replace('{0}', String(Number.isInteger(precision) ? precision : 0));
        }
        return '';
    };
    const submitEditor = () => {
        const form = document.getElementById('formGsku');
        const globalProductId = $('#globalProductId').val();
        const packUomCode = $('#packUomCode').val();
        const quantity = document.getElementById('packQuantity')?.value || '';
        if (!form || !globalProductId || !packUomCode || !form.checkValidity()) {
            form?.classList.add('was-validated');
            window.showToast?.(!globalProductId ? L.GlobalProductRequired : (!packUomCode ? L.PackUomRequired : L.PackQuantityRequired), 'error');
            return;
        }
        const quantityError = validateQuantity(quantity, document.querySelector('#packUomCode option:checked'));
        if (quantityError) {
            window.showToast?.(quantityError, 'error');
            return;
        }

        const entityName = $('#globalProductId option:selected').text() || String(globalProductId);
        const confirmation = editorMode === 'correction' ? L.CorrectionConfirmation
            : editorId ? L.UpdateConfirmation : L.CreateConfirmation;
        const confirmButtonText = editorMode === 'correction' ? L.RequestCorrection
            : editorId ? L.UpdateDraft : L.Save;
        window.showConfirm?.(confirmation, async () => {
            const button = document.getElementById('btnSaveGsku');
            if (button) button.disabled = true;
            try {
                if (editorMode === 'correction') {
                    const fresh = await fetchDetail(editorId);
                    const freshGskuVersion = readVersion(fresh, 'gskuVersion', 'GskuVersion');
                    const freshRevisionVersion = readVersion(fresh, 'revisionVersion', 'RevisionVersion');
                    if (!readAvailableActions(fresh).includes('REQUEST_CORRECTION')
                        || freshGskuVersion !== editorGskuVersion
                        || freshRevisionVersion !== editorRevisionVersion) {
                        throw new Error(L.LifecycleStateChanged);
                    }
                }
                const body = new FormData();
                body.set('PackQuantity', quantity);
                body.set('PackUomCode', String(packUomCode));
                if (editorMode === 'correction') body.set('ExpectedGskuVersion', String(editorGskuVersion));
                else if (editorId) body.set('ExpectedVersion', String(editorGskuVersion));
                else {
                    body.set('GlobalProductId', String(globalProductId));
                    body.set('FormAttemptToken', document.getElementById('formAttemptToken')?.value || '');
                }
                const antiForgeryToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                body.set('__RequestVerificationToken', antiForgeryToken);
                const target = editorMode === 'correction'
                    ? `${endpoint}/${encodeURIComponent(editorId)}/correction-requests`
                    : editorId ? `${endpoint}/${encodeURIComponent(editorId)}` : endpoint;
                const response = await fetch(target, {
                    method: editorMode === 'edit' ? 'PUT' : 'POST',
                    credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': antiForgeryToken, 'X-Requested-With': 'XMLHttpRequest' },
                    body
                });
                if (response.status === 401) handleUnauthorized();
                if (editorMode === 'correction' && response.status !== 200 && response.status !== 202
                    || editorMode === 'edit' && response.status !== 200
                    || !editorId && response.status !== 201 && response.status !== 202) {
                    throw new Error(await getErrorMessage(response));
                }
                const payload = await response.json();
                const draft = unwrapData(payload);
                if (!editorId && (response.status === 202 || payload?.success === false)) {
                    window.showToast?.(L.CreateReconciliationPending, 'warning');
                    return;
                }
                if (editorMode === 'correction') {
                    const successful = payload?.isSuccessful ?? payload?.IsSuccessful;
                    const responseGskuId = valueOf(draft, 'gskuId', 'GskuId');
                    const checkpoint = valueOf(draft, 'checkpoint', 'Checkpoint');
                    const responseVersion = readVersion(draft, 'gskuVersion', 'GskuVersion');
                    const operationId = valueOf(draft, 'operationId', 'OperationId');
                    if (successful !== true
                        || String(operationId) === '00000000-0000-0000-0000-000000000000'
                        || String(responseGskuId).toLowerCase() !== editorId.toLowerCase()
                        || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(String(operationId))
                        || !Number.isInteger(responseVersion) || responseVersion <= editorGskuVersion
                        || response.status === 202 && checkpoint !== 'AwaitingDecision'
                        || response.status === 200 && checkpoint !== 'Completed') {
                        throw new Error(L.ErrorGateway);
                    }
                    const refreshed = await fetchDetail(editorId);
                    const refreshedVersion = readVersion(refreshed, 'gskuVersion', 'GskuVersion');
                    const refreshedRevisionVersion = readVersion(refreshed, 'revisionVersion', 'RevisionVersion');
                    const refreshedActions = readAvailableActions(refreshed);
                    if (!Number.isInteger(refreshedRevisionVersion)
                        || response.status === 202 && (refreshedVersion !== responseVersion
                            || refreshedRevisionVersion !== editorRevisionVersion
                            || refreshedActions.includes('REQUEST_CORRECTION'))
                        || response.status === 200 && (refreshedVersion <= editorGskuVersion
                            || String(valueOf(refreshed, 'packUomCode', 'PackUomCode')) !== String(packUomCode)
                            || Number(valueOf(refreshed, 'packQuantity', 'PackQuantity')) !== Number(quantity))) {
                        throw new Error(L.LifecycleStateChanged);
                    }
                } else if (editorId) {
                    const successful = payload?.isSuccessful ?? payload?.IsSuccessful;
                    const responseGskuId = valueOf(draft, 'gskuId', 'GskuId');
                    const responseVersion = readVersion(draft, 'version', 'Version');
                    if (successful !== true
                        || String(responseGskuId).toLowerCase() !== editorId.toLowerCase()
                        || !Number.isInteger(responseVersion)
                        || responseVersion <= editorGskuVersion) {
                        throw new Error(L.ErrorGateway);
                    }
                    const refreshed = await fetchDetail(editorId);
                    if (readVersion(refreshed, 'gskuVersion', 'GskuVersion') !== responseVersion
                        || lifecycleCode(valueOf(refreshed, 'lifecycleStatus', 'LifecycleStatus')) !== 1
                        || Number(valueOf(refreshed, 'packQuantity', 'PackQuantity')) !== Number(quantity)
                        || valueOf(refreshed, 'packUomCode', 'PackUomCode') !== packUomCode) {
                        throw new Error(L.LifecycleStateChanged);
                    }
                }

                const nextToken = payload?.formAttemptToken || payload?.FormAttemptToken;
                const tokenInput = document.getElementById('formAttemptToken');
                if (tokenInput && nextToken) tokenInput.value = nextToken;
                const code = valueOf(draft, 'canonicalCode', 'CanonicalCode') || L.NotAvailable;
                const revision = valueOf(draft, 'revisionIdentifier', 'RevisionIdentifier') || L.NotAvailable;
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                form.reset();
                $('#globalProductId, #packUomCode').val(null).trigger('change');
                dt?.ajax.reload(null, false);
                window.showToast?.(editorMode === 'correction'
                    ? response.status === 202 ? L.CorrectionPending : L.CorrectionCompleted
                    : editorId ? L.UpdateSuccess : L.CreateSuccessWithIdentifiers
                    .replace('{0}', code)
                    .replace('{1}', revision),
                    editorMode === 'correction' && response.status === 202 ? 'warning' : 'success');
                editorId = '';
                editorGskuVersion = null;
                editorRevisionVersion = null;
                editorMode = 'create';
            } catch (error) {
                if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            } finally {
                if (button) button.disabled = false;
            }
        }, { entityName, type: 'primary', confirmButtonText });
    };

    const requestLifecycle = (id, action, actionButton) => {
        if (!id || !['SUBMIT', 'WITHDRAW_APPROVAL'].includes(action)) return;
        const confirmation = action === 'SUBMIT' ? L.SubmitConfirmation : L.WithdrawConfirmation;
        const confirmButtonText = action === 'SUBMIT' ? L.SubmitIdentity : L.WithdrawApproval;
        window.showConfirm?.(confirmation, async () => {
            const requestKey = `${id}:${action}`;
            if (lifecycleRequests.has(requestKey)) return;
            lifecycleRequests.add(requestKey);
            setBusy(actionButton, true);
            try {
                const detail = await fetchDetail(id);
                if (!readAvailableActions(detail).includes(action)) throw new Error(L.LifecycleStateChanged);
                const gskuVersion = readVersion(detail, 'gskuVersion', 'GskuVersion');
                if (!Number.isInteger(gskuVersion) || gskuVersion < 0) throw new Error(L.ErrorConflict);
                const body = new FormData();
                const suffix = action === 'SUBMIT' ? 'submit' : 'identity-approval/withdraw';
                // FIX3 E2: a submit in reconciliation is repeated as the SAME submit — at the version it was made at
                // (the GSKU's version moved when the pair went pending), so that MDM reads the engine again for it.
                const submittedAt = readVersion(detail, 'identitySubmitVersion', 'IdentitySubmitVersion');
                if (action === 'SUBMIT') body.set('ExpectedVersion', String(
                    valueOf(detail, 'identitySubmitState', 'IdentitySubmitState') === 'RECONCILIATION_REQUIRED'
                        && Number.isInteger(submittedAt) && submittedAt >= 0 ? submittedAt : gskuVersion));
                else {
                    body.set('ExpectedGskuVersion', String(gskuVersion));
                    body.set('ReasonCode', 'REQUESTER_WITHDRAWAL');
                    body.set('Comment', '');
                }
                const token = document.querySelector('#gskuLifecycleToken input[name="__RequestVerificationToken"]')?.value || '';
                body.set('__RequestVerificationToken', token);
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${suffix}`, {
                    method: 'POST', credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' }, body
                });
                if (response.status === 401) handleUnauthorized();
                if (!response.ok) throw new Error(await getErrorMessage(response));
                const payload = await response.json();
                const successful = payload?.isSuccessful ?? payload?.IsSuccessful ?? payload?.success;
                const statusCode = Number(payload?.statusCode ?? payload?.StatusCode ?? response.status);
                if (successful !== true || statusCode !== response.status) throw new Error(L.ErrorGateway);
                const result = unwrapData(payload);
                if (String(valueOf(result, 'gskuId', 'GskuId')).toLowerCase() !== String(id).toLowerCase())
                    throw new Error(L.ErrorGateway);
                const refreshed = await fetchDetail(id);
                const refreshedState = lifecycleCode(valueOf(refreshed, 'lifecycleStatus', 'LifecycleStatus'));
                const refreshedActions = readAvailableActions(refreshed);
                if (action === 'WITHDRAW_APPROVAL'
                    && (readVersion(result, 'gskuVersion', 'GskuVersion') !== readVersion(refreshed, 'gskuVersion', 'GskuVersion')
                        || readVersion(result, 'revisionVersion', 'RevisionVersion') !== readVersion(refreshed, 'revisionVersion', 'RevisionVersion')))
                    throw new Error(L.LifecycleStateChanged);
                if (action === 'SUBMIT' && (refreshedState !== 2 || refreshedActions.includes('SUBMIT'))
                    || action === 'WITHDRAW_APPROVAL' && response.status === 200
                        && (refreshedState !== 1 || refreshedActions.includes('WITHDRAW_APPROVAL'))) {
                    throw new Error(L.LifecycleStateChanged);
                }
                dt?.ajax.reload(null, false);
                window.showToast?.(
                    action === 'SUBMIT' ? L.SubmitPendingSuccess
                        : response.status === 202 ? L.WithdrawPending : L.WithdrawSuccess,
                    response.status === 202 && action === 'WITHDRAW_APPROVAL' ? 'warning' : 'success');
            } catch (error) {
                if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            } finally {
                lifecycleRequests.delete(requestKey);
                setBusy(actionButton, false);
            }
        }, { type: 'warning', confirmButtonText });
    };

    /*
     * FIX4 item 1 (b): a correction or retirement request stuck in manual reconciliation holds the GSKU. Its requester
     * sends the SAME request again — the version and facts MDM keeps for it (so the proxy derives the same operation) —
     * and MDM reads the engine again: decided → applied; closed or unusable → the request ends, the GSKU is free;
     * still open → it waits. The answer is said in words; the register reloads either way.
     */
    // FIX5 item 4: the repeated quantity is written as the server derives it — no exponent (1e-7), no grouping; the
    // proxy drops trailing zeros on its side, so "2.5" here and "2.50" in the original form are the same request.
    const canonicalQuantity = (value) => typeof value === 'number'
        ? value.toLocaleString('en-US', { useGrouping: false, maximumFractionDigits: 20 })
        : String(value ?? '').trim();
    const repeatLifecycleRequest = (id, actionButton) => {
        if (!id) return;
        window.showConfirm?.(L.RepeatRequestConfirmation, async () => {
            const requestKey = `${id}:REPEAT_REQUEST`;
            if (lifecycleRequests.has(requestKey)) return;
            lifecycleRequests.add(requestKey);
            setBusy(actionButton, true);
            try {
                const detail = await fetchDetail(id);
                const pending = valueOf(detail, 'pendingLifecycleRequest', 'PendingLifecycleRequest');
                const kind = valueOf(pending, 'kind', 'Kind');
                const version = readVersion(pending, 'expectedGskuVersion', 'ExpectedGskuVersion');
                if (!readAvailableActions(detail).includes('REPEAT_REQUEST') || !pending
                    || !['CORRECTION', 'RETIREMENT'].includes(kind) || !Number.isInteger(version)) {
                    throw new Error(L.LifecycleStateChanged);
                }
                const token = document.querySelector(
                    '#gskuLifecycleToken input[name="__RequestVerificationToken"]')?.value || '';
                const body = new FormData();
                body.set('ExpectedGskuVersion', String(version));
                if (kind === 'CORRECTION') {
                    body.set('PackQuantity', canonicalQuantity(valueOf(pending, 'packQuantity', 'PackQuantity')));
                    body.set('PackUomCode', String(valueOf(pending, 'packUomCode', 'PackUomCode')));
                } else {
                    body.set('RequestReason', String(valueOf(pending, 'requestReason', 'RequestReason')));
                }
                body.set('__RequestVerificationToken', token);
                const suffix = kind === 'CORRECTION' ? 'correction-requests' : 'retirement-requests';
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${suffix}`, {
                    method: 'POST', credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' },
                    body
                });
                if (response.status === 401) handleUnauthorized();
                if (response.status !== 200 && response.status !== 202) {
                    throw new Error(await getErrorMessage(response));
                }
                window.showToast?.(response.status === 202 ? L.RepeatRequestWaiting : L.RepeatRequestSettled, 'success');
            } catch (error) {
                window.showToast?.(error?.message || L.ErrorGateway, 'error');
            } finally {
                lifecycleRequests.delete(requestKey);
                setBusy(actionButton, false);
                dt?.ajax.reload(null, false);
            }
        }, { entityName: L.RepeatRequest, type: 'primary', confirmButtonText: L.RepeatRequest });
    };

    const requestRetirement = (id, actionButton) => {
        if (!id) return;
        window.showConfirm?.(L.RetirementRequestConfirmation, async (input) => {
            const requestReason = String(input ?? '').trim();
            if (!requestReason || requestReason.length > 128) {
                window.showToast?.(L.RetirementRequestReasonRequired, 'error');
                return;
            }
            if (/[\u0000-\u001F\u007F-\u009F]/u.test(requestReason)) {
                window.showToast?.(L.RetirementRequestReasonInvalid, 'error');
                return;
            }
            const requestKey = `${id}:REQUEST_RETIREMENT`;
            if (lifecycleRequests.has(requestKey)) return;
            lifecycleRequests.add(requestKey);
            setBusy(actionButton, true);
            try {
                const detail = await fetchDetail(id);
                if (!readAvailableActions(detail).includes('REQUEST_RETIREMENT')) {
                    throw new Error(L.LifecycleStateChanged);
                }
                const gskuVersion = readVersion(detail, 'gskuVersion', 'GskuVersion');
                const revisionVersion = readVersion(detail, 'revisionVersion', 'RevisionVersion');
                if (!Number.isInteger(gskuVersion) || gskuVersion < 0
                    || !Number.isInteger(revisionVersion) || revisionVersion < 0) {
                    throw new Error(L.ErrorConflict);
                }
                const token = document.querySelector(
                    '#gskuLifecycleToken input[name="__RequestVerificationToken"]')?.value || '';
                const body = new FormData();
                body.set('ExpectedGskuVersion', String(gskuVersion));
                body.set('RequestReason', requestReason);
                body.set('__RequestVerificationToken', token);
                const response = await fetch(
                    `${endpoint}/${encodeURIComponent(id)}/retirement-requests`, {
                        method: 'POST', credentials: 'same-origin',
                        headers: {
                            'RequestVerificationToken': token,
                            'X-Requested-With': 'XMLHttpRequest'
                        },
                        body
                    });
                if (response.status === 401) handleUnauthorized();
                if (response.status !== 200 && response.status !== 202) {
                    throw new Error(await getErrorMessage(response));
                }
                const payload = await response.json();
                const data = unwrapData(payload);
                const successful = payload?.isSuccessful ?? payload?.IsSuccessful;
                const statusCode = Number(payload?.statusCode ?? payload?.StatusCode);
                const responseId = valueOf(data, 'gskuId', 'GskuId');
                const responseVersion = readVersion(data, 'gskuVersion', 'GskuVersion');
                const operationId = valueOf(data, 'operationId', 'OperationId');
                const checkpoint = valueOf(data, 'checkpoint', 'Checkpoint');
                if (successful !== true || statusCode !== response.status
                    || String(operationId) === '00000000-0000-0000-0000-000000000000'
                    || String(responseId).toLowerCase() !== String(id).toLowerCase()
                    || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(String(operationId))
                    || !Number.isInteger(responseVersion) || responseVersion <= gskuVersion
                    || response.status === 202 && checkpoint !== 'AwaitingDecision'
                    || response.status === 200 && checkpoint !== 'Completed') {
                    throw new Error(L.ErrorGateway);
                }
                const refreshed = await fetchDetail(id);
                const refreshedState = lifecycleCode(valueOf(refreshed, 'lifecycleStatus', 'LifecycleStatus'));
                const refreshedGskuVersion = readVersion(refreshed, 'gskuVersion', 'GskuVersion');
                const refreshedRevisionVersion = readVersion(refreshed, 'revisionVersion', 'RevisionVersion');
                const refreshedActions = readAvailableActions(refreshed);
                if (refreshedGskuVersion !== responseVersion
                    || response.status === 202 && (refreshedState !== 3
                        || refreshedRevisionVersion !== revisionVersion
                        || refreshedActions.includes('REQUEST_CORRECTION')
                        || refreshedActions.includes('REQUEST_RETIREMENT'))
                    || response.status === 200 && (refreshedState !== 4
                        || refreshedRevisionVersion <= revisionVersion)) {
                    throw new Error(L.LifecycleStateChanged);
                }
                dt?.ajax.reload(null, false);
                window.showToast?.(L.RetirementRequestedSuccess,
                    response.status === 202 ? 'warning' : 'success');
            } catch (error) {
                if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            } finally {
                lifecycleRequests.delete(requestKey);
                setBusy(actionButton, false);
            }
        }, {
            type: 'warning', showInput: true, inputType: 'text', inputRequired: true,
            inputLabel: L.RetirementRequestReasonLabel,
            inputAttributes: { maxlength: 128 }, confirmButtonText: L.RequestRetirement
        });
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
        ajax: { url: endpoint, type: 'GET', headers: getAuthHeaders(), error: onListError },
        // The quick view and every row action stay the register's own (fresh server-owned actions per row).
        actions: { onRowAction: rowActionHandlers() },
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
            fields: [{ id: 'filterLifecycleStatus', key: 'lifecycleStatus', kind: 'single' }]
        },
        // Same keys as before the migration: every saved GSKU view still loads.
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
            // EK item 9: chained by the list component after its own initComplete.
            initComplete: function () { labelExportsCurrentPageOnly(this.api()); },
            // `data` names are MDM's orderBy whitelist (canonicalCode, packQuantity, lifecycleStatus). The Global
            // Product and the revision live on other documents: not sortable, so never sent as a sort key.
            columns: [
                { data: 'id', name: 'control' },
                { data: 'canonicalCode', name: 'canonicalCode' },
                { data: 'globalProductName', name: 'globalProductName' },
                { data: 'revisionIdentifier', name: 'revisionIdentifier' },
                { data: 'packQuantity', name: 'packQuantity' },
                { data: 'lifecycleStatus', name: 'lifecycleStatus' },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                {
                    targets: 2,
                    orderable: false,
                    render: (data, type, row) => {
                        const code = row.globalProductCanonicalCode || row.GlobalProductCanonicalCode || '';
                        const name = data || row.GlobalProductName || '';
                        return escapeHtml(`${code}${code && name ? ' — ' : ''}${name}`);
                    }
                },
                { targets: 3, orderable: false, render: escapeHtml },
                {
                    targets: 4,
                    render: (data, type, row) => escapeHtml(`${data} ${row.packUomCode || row.PackUomCode || ''}`.trim())
                },
                { targets: 5, render: renderLifecycle },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => renderActions(row)
                }
            ]
        }
    });

    const initDataTable = async () => {
        if (!tableEl || !window.DitenDataTable?.createList) return;
        list = await window.DitenDataTable.createList(buildListOptions());
        dt = list.dt;
    };

    const bindEvents = () => {
        document.getElementById('btnSaveGsku')?.addEventListener('click', submitEditor);
        // The menu itself is read fresh from MDM when it opens; its items are dispatched by the list component.
        document.addEventListener('click', (event) => {
            const menuToggle = event.target.closest('.js-gsku-actions-toggle');
            if (!menuToggle || !(menuToggle.closest('.datatables-gskus') || menuToggle.closest('.modal.dtr-bs-modal'))) return;
            event.preventDefault();
            loadActionMenu(menuToggle);
        });
    };

    return {
        init: async () => {
            bindEvents();
            await initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => GskusList.init());
