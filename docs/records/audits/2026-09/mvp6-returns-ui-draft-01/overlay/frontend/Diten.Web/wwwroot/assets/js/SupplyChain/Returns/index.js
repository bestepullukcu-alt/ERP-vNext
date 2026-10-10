'use strict';

/**
 * MOD-0186 Returns — tenant list page (pack §32, golden-reference slim, bounded DataTables v2 profile).
 * DRAFT overlay — not built, not runtime-verified, not writer-complete.
 *
 * Bound operations only (pack §32.3): queryReturns (list/filter/reload), createReturn (create offcanvas),
 * transitionReturn (row action) and the Shipment resolve projection. Every request goes to the same-origin adapter
 * /SupplyChain/Returns/api*; the browser never calls the Gateway, never holds a token and never sends tenant/LE scope
 * (the adapter fills the scope headers from the signed session server-side).
 *
 * Out of scope and absent on purpose: checkbox and bulk (RU-SCR-01), edit and delete (RU-SCR-02), import and export
 * (RU-SCR-03), server paging/search/sort (RU-SCR-04), multi-select status (RU-SCR-05), by-ID return GET and Details page,
 * reason/disposition catalogue, entitlement-balance display, Inventory or Warehouse call (§32.3).
 *
 * Payload rule (pack §32.6/§32.8): values are sent exactly as typed — no trim, no number conversion; quantities are
 * JSON strings. One pending request per intent; a retry after network/500/503 re-sends the same Idempotency-Key with
 * identical body text; an edited payload is a new intent with a new key. There is no timer or polling in this file.
 */
const ReturnsList = (function () {
    const endpoint = '/SupplyChain/Returns/api';
    const tableEl = document.querySelector('.datatables-returns');
    const personalizationClient = window.personalizationClient;
    // ASSUMPTION A6: module/page codes of the §33 manifest; the integration owner confirms the personalization codes.
    const personalizationContext = { moduleKey: 'reverse-logistics', pageKey: 'RETURNS' };
    const filterCollapseId = 'inlineFilterCollapse';
    const colvisColumns = [1, 2, 3];
    const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

    // ReturnLifecycle.cs:4-9 (display only; the backend decides). InTransit→Cancelled does not exist (annex correction).
    const TRANSITIONS = Object.freeze({
        Requested: ['Authorized', 'Rejected'],
        Authorized: ['InTransit', 'Cancelled'],
        InTransit: ['Received'],
        Received: ['Dispositioned'],
        Dispositioned: ['Closed'],
        Rejected: [],
        Cancelled: [],
        Closed: []
    });
    // ReturnPermissions.ForTarget, expressed as the page permission flags (each also needs .transition + shipments.read).
    const TARGET_PERMISSION = Object.freeze({
        Authorized: 'canAuthorize', Rejected: 'canAuthorize', InTransit: 'canTransit', Cancelled: 'canCancel',
        Received: 'canReceive', Dispositioned: 'canDisposition', Closed: 'canClose'
    });
    const TARGET_ACTION_KEY = Object.freeze({
        Authorized: 'ActionAuthorize', Rejected: 'ActionReject', InTransit: 'ActionMarkInTransit',
        Cancelled: 'ActionCancelReturn', Received: 'ActionReceive', Dispositioned: 'ActionDisposition', Closed: 'ActionClose'
    });
    // Manifest §33: Reject and Cancel Return are the dangerous actions.
    const DANGEROUS_TARGETS = new Set(['Rejected', 'Cancelled']);
    // Optional inventoryTransactionReferenceId is offered on Received and Dispositioned only (pack §32.7).
    const INVENTORY_REFERENCE_TARGETS = new Set(['Received', 'Dispositioned']);
    const RETURN_STATUS_KEY = Object.freeze({
        Requested: 'StatusRequested', Authorized: 'StatusAuthorized', Rejected: 'StatusRejected', InTransit: 'StatusInTransit',
        Received: 'StatusReceived', Dispositioned: 'StatusDispositioned', Closed: 'StatusClosed', Cancelled: 'StatusCancelled'
    });
    const RETURN_STATUS_CSS = Object.freeze({
        Requested: 'info', Authorized: 'primary', Rejected: 'danger', InTransit: 'warning',
        Received: 'success', Dispositioned: 'success', Closed: 'dark', Cancelled: 'secondary'
    });
    const SHIPMENT_STATUS_KEY = Object.freeze({
        Draft: 'ShipmentStatusDraft', Planned: 'ShipmentStatusPlanned', Dispatched: 'ShipmentStatusDispatched',
        InTransit: 'ShipmentStatusInTransit', Delivered: 'ShipmentStatusDelivered', Exception: 'ShipmentStatusException',
        Closed: 'ShipmentStatusClosed', Cancelled: 'ShipmentStatusCancelled'
    });
    // ReturnRepository.cs:57 (display note + disabled submit; server 422 SHIPMENT_NOT_RETURNABLE stays authoritative).
    const ELIGIBLE_SHIPMENT_STATUSES = new Set(['Delivered', 'Closed']);
    // All 21 published Returns codes (shipment-bundle.openapi.yaml /returns operations), localized (pack §32.8/§32.9).
    const ERROR_KEY = Object.freeze({
        INVALID_REQUEST: 'ErrInvalidRequest',
        RETURN_NOT_FOUND: 'ErrReturnNotFound',
        SHIPMENT_NOT_FOUND: 'ErrShipmentNotFound',
        SHIPMENT_LINE_NOT_FOUND: 'ErrShipmentLineNotFound',
        INVALID_RETURN_TRANSITION: 'ErrInvalidReturnTransition',
        DISPOSITION_REQUIRED: 'ErrDispositionRequired',
        INVALID_RETURN_QUANTITY: 'ErrInvalidReturnQuantity',
        RETURN_QUANTITY_EXCEEDED: 'ErrReturnQuantityExceeded',
        RETURN_UOM_MISMATCH: 'ErrReturnUomMismatch',
        DUPLICATE_RETURN_LINE: 'ErrDuplicateReturnLine',
        SHIPMENT_NOT_RETURNABLE: 'ErrShipmentNotReturnable',
        CORRELATION_ROOT_MISMATCH: 'ErrCorrelationRootMismatch',
        IDEMPOTENCY_KEY_REUSED: 'ErrIdempotencyKeyReused',
        RETURN_SOURCE_CHANGED: 'ErrReturnSourceChanged',
        RETURN_SHIPMENT_ROOT_INVALID: 'ErrReturnShipmentRootInvalid',
        DEPENDENCY_RESPONSE_INVALID: 'ErrDependencyResponseInvalid',
        RETURN_SHIPMENT_ROOT_UNAVAILABLE: 'ErrReturnShipmentRootUnavailable',
        REFERENCE_STATE_UNAVAILABLE: 'ErrReferenceStateUnavailable',
        DEPENDENCY_UNAVAILABLE: 'ErrDependencyUnavailable',
        PERSISTENCE_UNAVAILABLE: 'ErrPersistenceUnavailable',
        INTERNAL_ERROR: 'ErrInternalError'
    });
    // INVALID_REQUEST covers 400 schema, 401 auth, 403 context and 415 media (annex D186-05); the text follows the status.
    const INVALID_REQUEST_NOTICE = Object.freeze({ 401: 'NoticeSessionEnded', 403: 'NoticeForbidden', 415: 'NoticeUnsupportedMedia' });
    // Status → code when an envelope carries no known code (never raw text on screen).
    const STATUS_FALLBACK_CODE = Object.freeze({
        400: 'INVALID_REQUEST', 401: 'INVALID_REQUEST', 403: 'INVALID_REQUEST', 404: 'RETURN_NOT_FOUND',
        409: 'INVALID_REQUEST', 415: 'INVALID_REQUEST', 422: 'INVALID_REQUEST', 500: 'INTERNAL_ERROR',
        502: 'DEPENDENCY_RESPONSE_INVALID', 503: 'PERSISTENCE_UNAVAILABLE'
    });

    let L = window.L10n || {};
    let dt = null;
    let loadedOnce = false;
    let saveViewArmed = false;
    let defaultViewRecord = null;
    let appliedFilters = { status: '', shipmentId: '' };
    const rowsById = new Map();
    const permissions = readPermissions();

    // Create intent and resolve state.
    let createIntent = null;           // { key, bodyText, blocked, blockedFailure }
    let createPending = false;
    let resolveSequence = 0;
    let resolvedShipment = null;       // { shipmentId (exact typed text), shipmentNumber, status, lines }

    // Transition intent and state.
    let transitionContext = null;      // { returnId, shipmentId, rmaNumber, fromStatus }
    let transitionIntent = null;       // { returnId, key, bodyText, blocked, blockedFailure }
    let transitionPending = false;

    // ─── Basics ──────────────────────────────────────────────────────────────
    function readPermissions() {
        const island = document.getElementById('returns-permissions');
        const names = ['canCreate', 'canAuthorize', 'canTransit', 'canCancel', 'canReceive', 'canDisposition', 'canClose'];
        const result = {};
        names.forEach((name) => { result[name] = false; });
        if (!island) return result;
        try {
            const parsed = JSON.parse(island.textContent || '{}');
            names.forEach((name) => { result[name] = parsed[name] === true; });
        } catch (error) {
            console.error('[Returns] Permission payload could not be parsed.', error);
        }
        return result;
    }

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };
    const t = (key) => L[key] || '';
    const uuid = () => crypto.randomUUID();
    const escapeHtml = (value) => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    const isText = (value) => typeof value === 'string';
    const ltr = (value, extraClass = '') =>
        `<bdi dir="ltr"${extraClass ? ` class="${extraClass}"` : ''}>${escapeHtml(value)}</bdi>`;
    const notProvided = () => `<span class="text-muted">${escapeHtml(t('NotProvided'))}</span>`;
    const antiForgeryToken = () =>
        document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const traceHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest', 'X-Correlation-Id': uuid() });
    const mutationHeaders = (key) => Object.assign(traceHeaders(), {
        'Content-Type': 'application/json',
        'Idempotency-Key': key,
        'RequestVerificationToken': antiForgeryToken()
    });
    const returnStatusLabel = (status) => (isText(status) && RETURN_STATUS_KEY[status] ? t(RETURN_STATUS_KEY[status]) : '');
    const shipmentStatusLabel = (status) =>
        (isText(status) && SHIPMENT_STATUS_KEY[status] ? t(SHIPMENT_STATUS_KEY[status]) : t('NotProvided'));
    // "Received (manual assertion)" on the transition surfaces; the status column keeps "Received" (README A8).
    const targetLabel = (target) => (target === 'Received' ? t('TargetReceivedLabel') : returnStatusLabel(target));
    const offcanvas = (id) => {
        const el = document.getElementById(id);
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    // Local "now" with explicit offset, e.g. 2026-09-26T16:40:12+03:00 (G-DATETIME: plain text input; README F7).
    const nowWithOffset = () => {
        const d = new Date();
        const pad = (n) => String(n).padStart(2, '0');
        const offset = -d.getTimezoneOffset();
        const sign = offset >= 0 ? '+' : '-';
        const abs = Math.abs(offset);
        return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:`
            + `${pad(d.getMinutes())}:${pad(d.getSeconds())}${sign}${pad(Math.floor(abs / 60))}:${pad(abs % 60)}`;
    };

    // ─── Failure envelope → localized message + support reference ────────────
    const readFailure = async (response) => {
        let body = null;
        try { body = await response.json(); } catch (_) { body = null; }
        const envelopeCode = isText(body?.error?.code) ? body.error.code : '';
        const code = ERROR_KEY[envelopeCode] ? envelopeCode
            : (STATUS_FALLBACK_CODE[response.status] || (response.status >= 500 ? 'INTERNAL_ERROR' : 'INVALID_REQUEST'));
        const headerReference = response.headers.get('X-Correlation-Id');
        const reference = isText(body?.error?.correlationId) ? body.error.correlationId : (headerReference || '');
        return { status: response.status, code, reference };
    };
    const networkFailure = (code) => ({ status: 0, code, reference: '' });
    const failureText = (failure) => {
        if (failure.code === 'INVALID_REQUEST' && INVALID_REQUEST_NOTICE[failure.status]) return t(INVALID_REQUEST_NOTICE[failure.status]);
        return t(ERROR_KEY[failure.code] || 'ErrInternalError');
    };
    const retryable = (failure) => failure.status === 0 || failure.status === 500 || failure.status === 503;

    // Toast/confirm text is resx text plus a shape-checked UUID reference only, so it is safe whether the shared wrapper
    // renders text or HTML (README NOT-VERIFIED).
    const safeReference = (value) => (isText(value) && UUID_PATTERN.test(value) ? value : '');
    const toastFailure = (failure) => {
        const ref = safeReference(failure.reference);
        const reference = ref ? ` ${t('SupportReference')}: ${ref}` : '';
        window.showToast?.(`${failureText(failure)}${reference}`, failure.status >= 500 || failure.status === 0 ? 'error' : 'warning');
    };

    const showAlert = (alertId, failureOrText) => {
        const alert = document.getElementById(alertId);
        if (!alert) return;
        alert.replaceChildren();
        const message = document.createElement('div');
        message.textContent = typeof failureOrText === 'string' ? failureOrText : failureText(failureOrText);
        alert.appendChild(message);
        const reference = typeof failureOrText === 'string' ? '' : safeReference(failureOrText.reference);
        if (reference) {
            const row = document.createElement('div');
            row.className = 'small mt-1';
            const label = document.createElement('span');
            label.textContent = `${t('SupportReference')}: `;
            const value = document.createElement('bdi');
            value.dir = 'ltr';
            value.className = 'font-monospace';
            value.textContent = reference;
            const copy = document.createElement('button');
            copy.type = 'button';
            copy.className = 'btn btn-sm btn-icon btn-text-secondary js-copy-value';
            copy.dataset.copyValue = reference;
            copy.setAttribute('aria-label', t('Copy'));
            const icon = document.createElement('i');
            icon.className = 'bx bx-copy';
            icon.setAttribute('aria-hidden', 'true');
            copy.appendChild(icon);
            row.append(label, value, copy);
            alert.appendChild(row);
        }
        alert.classList.remove('d-none');
    };
    const hideAlert = (alertId) => {
        const alert = document.getElementById(alertId);
        if (!alert) return;
        alert.replaceChildren();
        alert.classList.add('d-none');
    };

    const handleUnauthorized = () => { window.DtDefaults?.handleUnauthorized?.(); };

    // ─── List: three distinct states (skeleton / table incl. empty / error) ──
    const setListState = (state, failure) => {
        document.getElementById('skeleton-loader')?.classList.toggle('d-none', state !== 'skeleton');
        document.getElementById('returns-table-host')?.classList.toggle('d-none', state !== 'table');
        const errorHost = document.getElementById('returns-error-state');
        errorHost?.classList.toggle('d-none', state !== 'error');
        if (state === 'error' && failure) {
            const message = document.getElementById('returns-error-message');
            if (message) message.textContent = failure.code ? failureText(failure) : t('ListErrorState');
            const referenceRow = document.getElementById('returns-error-reference');
            const referenceValue = document.getElementById('returns-error-reference-value');
            const referenceCopy = document.getElementById('returns-error-reference-copy');
            const reference = safeReference(failure.reference);
            if (referenceRow && referenceValue) {
                referenceValue.textContent = reference;
                referenceRow.classList.toggle('d-none', !reference);
                if (referenceCopy) referenceCopy.dataset.copyValue = reference;
            }
        }
    };

    const isListEnvelope = (body) => body !== null && typeof body === 'object' && Array.isArray(body.items)
        && body.items.every((item) => item !== null && typeof item === 'object' && !Array.isArray(item));

    const renderTotal = (total) => {
        const el = document.getElementById('returns-total');
        if (!el) return;
        el.textContent = `${t('TotalLabel')}: ${Number.isInteger(total) ? total : t('NotProvided')}`;
    };

    // DataTables ajax function (serverSide:false): one request per load, client paging/sort/search over the set.
    const loadReturns = async (_data, callback) => {
        const params = new URLSearchParams();
        if (appliedFilters.shipmentId !== '') params.set('shipmentId', appliedFilters.shipmentId);
        if (appliedFilters.status !== '') params.set('status', appliedFilters.status);
        const url = params.toString() ? `${endpoint}?${params.toString()}` : endpoint;
        rowsById.clear();
        try {
            const response = await fetch(url, { credentials: 'same-origin', headers: traceHeaders() });
            if (!response.ok) {
                const failure = await readFailure(response);
                if (response.status === 401) handleUnauthorized();
                setListState('error', failure);
                callback({ data: [] });
                return;
            }
            let body = null;
            try { body = await response.json(); } catch (_) { body = null; }
            if (!isListEnvelope(body)) {
                // A malformed envelope is an error, never an empty list (pack §32.5).
                setListState('error', { status: response.status, code: 'INTERNAL_ERROR', reference: response.headers.get('X-Correlation-Id') || '' });
                callback({ data: [] });
                return;
            }
            body.items.forEach((item) => { if (isText(item.returnId) && item.returnId) rowsById.set(item.returnId, item); });
            loadedOnce = true;
            setListState('table');
            renderTotal(body.total);
            callback({ data: body.items });
        } catch (error) {
            console.error('[Returns] List request failed.', error);
            setListState('error', networkFailure('PERSISTENCE_UNAVAILABLE'));
            callback({ data: [] });
        }
    };

    const reloadList = () => {
        if (!dt) return;
        if (!loadedOnce) setListState('skeleton');
        dt.ajax.reload(null, false);
    };

    // ─── Row rendering ───────────────────────────────────────────────────────
    const allowedTargets = (row) => {
        const targets = TRANSITIONS[row?.status] || [];
        return targets.filter((target) => permissions[TARGET_PERMISSION[target]] === true);
    };

    const renderActions = (row) => {
        if (!isText(row?.returnId) || !row.returnId) return ''; // no action without returnId (pack §32.5)
        const id = escapeHtml(row.returnId);
        const quickView = `<button type="button" class="btn btn-sm btn-icon btn-text-secondary rounded-pill js-quick-view" `
            + `data-return-id="${id}" title="${escapeHtml(t('QuickView'))}" aria-label="${escapeHtml(t('QuickView'))}">`
            + '<i class="bx bx-show" aria-hidden="true"></i></button>';
        const targets = isText(row.shipmentId) && UUID_PATTERN.test(row.shipmentId) ? allowedTargets(row) : [];
        if (!targets.length) return `<div class="d-inline-flex align-items-center gap-1">${quickView}</div>`;
        const items = targets.map((target) => `<button type="button" class="dropdown-item js-transition${DANGEROUS_TARGETS.has(target) ? ' text-danger' : ''}" `
            + `data-return-id="${id}" data-target-status="${escapeHtml(target)}">${escapeHtml(t(TARGET_ACTION_KEY[target]))}</button>`).join('');
        return `<div class="d-inline-flex align-items-center gap-1">${quickView}`
            + '<div class="dropdown">'
            + `<button type="button" class="btn btn-sm btn-icon btn-text-secondary rounded-pill dropdown-toggle hide-arrow" data-bs-toggle="dropdown" aria-expanded="false" aria-label="${escapeHtml(t('ActionsHeader'))}">`
            + '<i class="bx bx-dots-vertical-rounded" aria-hidden="true"></i></button>'
            + `<div class="dropdown-menu dropdown-menu-end">${items}</div></div></div>`;
    };

    const renderStatus = (status, type) => {
        const label = returnStatusLabel(status);
        if (type !== 'display') return label || (isText(status) ? status : '');
        if (!label) return notProvided();
        return `<span class="badge bg-label-${RETURN_STATUS_CSS[status] || 'secondary'}">${escapeHtml(label)}</span>`;
    };

    const renderCopyable = (value, type) => {
        if (!isText(value) || !value) return type === 'display' ? notProvided() : '';
        if (type !== 'display') return value;
        return `<span class="d-inline-flex align-items-center gap-1">${ltr(value, 'font-monospace')}`
            + `<button type="button" class="btn btn-sm btn-icon btn-text-secondary js-copy-value" data-copy-value="${escapeHtml(value)}" aria-label="${escapeHtml(t('Copy'))}">`
            + '<i class="bx bx-copy" aria-hidden="true"></i></button></span>';
    };

    // ─── QuickView (row summary only; no request, no Edit) ───────────────────
    const openQuickView = (returnId) => {
        const row = rowsById.get(returnId);
        if (!row) return;
        const setText = (id, value) => {
            const el = document.getElementById(id);
            if (el) el.textContent = isText(value) && value !== '' ? value : t('NotProvided');
        };
        setText('oc-rma-number', row.rmaNumber);
        setText('oc-return-id', row.returnId);
        setText('oc-shipment-id', row.shipmentId);
        const subtitle = document.getElementById('oc-subtitle');
        if (subtitle) subtitle.textContent = returnStatusLabel(row.status) || t('NotProvided');
        const status = document.getElementById('oc-status');
        if (status) status.innerHTML = renderStatus(row.status, 'display');
        const copy = document.getElementById('oc-return-id-copy');
        if (copy) copy.dataset.copyValue = row.returnId;
        offcanvas('offcanvasDetailsPreview')?.show();
    };

    // ─── Create ──────────────────────────────────────────────────────────────
    const evidenceList = () => document.getElementById('returnEvidenceList');
    const lineRows = () => Array.from(document.querySelectorAll('#returnLinesBody .return-line-row'));

    const addEvidenceRow = () => {
        const template = document.getElementById('returnEvidenceRowTemplate');
        const list = evidenceList();
        if (!template || !list) return;
        list.appendChild(template.content.cloneNode(true));
        const rows = list.querySelectorAll('.return-evidence-row');
        const addedInput = rows[rows.length - 1]?.querySelector('.return-evidence-input');
        if (addedInput) {
            addedInput.id = `returnEvidence_${uuid()}`;
            addedInput.focus();
        }
    };

    // Removes every trace of the looked-up Shipment from the DOM (pack §32.6: 404 or a new UUID leaves nothing behind).
    const clearResolvedShipment = () => {
        resolveSequence += 1; // any in-flight resolve becomes stale (RU-10)
        resolvedShipment = null;
        document.getElementById('returnShipmentResolved')?.classList.add('d-none');
        const number = document.getElementById('returnShipmentNumber');
        const status = document.getElementById('returnShipmentStatus');
        if (number) number.textContent = '';
        if (status) status.textContent = '';
        document.getElementById('returnLinesBody')?.replaceChildren();
        document.getElementById('returnLinesTable')?.classList.add('d-none');
        document.getElementById('returnNoLines')?.classList.add('d-none');
        document.getElementById('returnIneligibleNote')?.classList.add('d-none');
        const save = document.getElementById('btnSaveReturn');
        if (save) save.disabled = true;
    };

    // One row per resolved source line; values written with textContent; unique ids and accessible names per row.
    const renderLines = (lines) => {
        const body = document.getElementById('returnLinesBody');
        const template = document.getElementById('returnLineRowTemplate');
        if (!body || !template) return;
        body.replaceChildren();
        lines.forEach((line, index) => {
            const fragment = template.content.cloneNode(true);
            const row = fragment.querySelector('.return-line-row');
            const usable = isText(line.lineNumber) && isText(line.uomId);
            row.dataset.lineNumber = usable ? line.lineNumber : '';
            row.dataset.uomId = usable ? line.uomId : '';
            const labelText = `${t('LineNumber')} ${isText(line.lineNumber) ? line.lineNumber : t('NotProvided')}`;
            row.querySelector('.js-line-number').textContent = isText(line.lineNumber) ? line.lineNumber : t('NotProvided');
            row.querySelector('.js-line-shipped').textContent = isText(line.quantity) ? line.quantity : t('NotProvided');
            row.querySelector('.js-line-uom').textContent = isText(line.uomId) ? line.uomId : t('NotProvided');
            const select = row.querySelector('.js-line-select');
            const quantity = row.querySelector('.js-line-quantity');
            select.id = `returnLineSelect_${index}_${uuid()}`;
            select.setAttribute('aria-label', `${t('SelectLine')} — ${labelText}`);
            select.disabled = !usable;
            quantity.id = `returnLineQuantity_${index}_${uuid()}`;
            quantity.setAttribute('aria-label', `${t('ReturnQuantity')} — ${labelText}`);
            body.appendChild(fragment);
        });
        document.getElementById('returnLinesTable')?.classList.toggle('d-none', lines.length === 0);
        document.getElementById('returnNoLines')?.classList.toggle('d-none', lines.length !== 0);
    };

    const openCreate = () => {
        if (!permissions.canCreate) return;
        const form = document.getElementById('formReturn');
        form?.reset();
        const list = evidenceList();
        if (list) list.replaceChildren();
        hideAlert('formReturnAlert');
        clearResolvedShipment();
        createIntent = null;
        offcanvas('offcanvasCreateEdit')?.show();
    };

    const closeCreateAsForbidden = (failure) => {
        offcanvas('offcanvasCreateEdit')?.hide();
        document.querySelectorAll('.add-new').forEach((button) => { button.disabled = true; button.classList.add('disabled'); });
        toastFailure(failure);
    };

    const resolveShipment = async () => {
        const input = document.getElementById('returnShipmentId');
        if (!input) return;
        hideAlert('formReturnAlert');
        clearResolvedShipment();
        const requested = input.value;
        if (!UUID_PATTERN.test(requested)) {
            // The adapter route needs a UUID segment; nothing else is checked here.
            showAlert('formReturnAlert', { status: 400, code: 'INVALID_REQUEST', reference: '' });
            return;
        }
        const sequence = resolveSequence;
        const isCurrent = () => sequence === resolveSequence && input.value === requested;
        try {
            const response = await fetch(`${endpoint}/shipments/${encodeURIComponent(requested)}`,
                { credentials: 'same-origin', headers: traceHeaders() });
            if (!isCurrent()) return; // a late response for a previous UUID never populates
            if (!response.ok) {
                const failure = await readFailure(response);
                if (!isCurrent()) return;
                if (response.status === 401) { handleUnauthorized(); return; }
                if (response.status === 403) { closeCreateAsForbidden(failure); return; }
                showAlert('formReturnAlert', failure); // 404: one safe text; no shipment data kept in the DOM
                return;
            }
            let body = null;
            try { body = await response.json(); } catch (_) { body = null; }
            if (!isCurrent()) return;
            if (!body || typeof body !== 'object' || !Array.isArray(body.lines)
                || !body.lines.every((line) => line !== null && typeof line === 'object')) {
                showAlert('formReturnAlert', { status: 502, code: 'DEPENDENCY_RESPONSE_INVALID', reference: response.headers.get('X-Correlation-Id') || '' });
                return;
            }
            resolvedShipment = {
                shipmentId: requested,
                shipmentNumber: isText(body.shipmentNumber) ? body.shipmentNumber : '',
                status: isText(body.status) ? body.status : '',
                lines: body.lines
            };
            const number = document.getElementById('returnShipmentNumber');
            const status = document.getElementById('returnShipmentStatus');
            if (number) number.textContent = resolvedShipment.shipmentNumber || t('NotProvided');
            if (status) status.textContent = shipmentStatusLabel(resolvedShipment.status);
            document.getElementById('returnShipmentResolved')?.classList.remove('d-none');
            renderLines(resolvedShipment.lines);
            const eligible = ELIGIBLE_SHIPMENT_STATUSES.has(resolvedShipment.status);
            document.getElementById('returnIneligibleNote')?.classList.toggle('d-none', eligible);
            const save = document.getElementById('btnSaveReturn');
            if (save) save.disabled = !eligible;
        } catch (error) {
            if (!isCurrent()) return;
            console.error('[Returns] Shipment resolve failed.', error);
            showAlert('formReturnAlert', networkFailure('DEPENDENCY_UNAVAILABLE'));
        }
    };

    const selectedLines = () => lineRows()
        .filter((row) => row.querySelector('.js-line-select')?.checked === true && row.dataset.lineNumber !== '')
        .map((row) => ({
            shipmentLineNumber: row.dataset.lineNumber,
            quantity: row.querySelector('.js-line-quantity')?.value ?? '', // JSON string exactly as typed
            uomId: row.dataset.uomId
        }));

    // Body in contract order (CreateReturnCommand); every value exactly as typed (RU-08, RU-09).
    const buildCreateBody = () => {
        const body = {
            shipmentId: document.getElementById('returnShipmentId')?.value ?? '',
            reasonCode: document.getElementById('returnReasonCode')?.value ?? '',
            lines: selectedLines()
        };
        const evidence = Array.from(evidenceList()?.querySelectorAll('.return-evidence-input') || []).map((input) => input.value);
        if (evidence.length) body.evidenceReferenceIds = evidence; // omitted when empty; order/duplicates/empty kept
        return JSON.stringify(body);
    };

    const submitCreate = async () => {
        if (createPending) { window.showToast?.(t('RequestPending'), 'warning'); return; }
        const input = document.getElementById('returnShipmentId');
        // lines has minItems 1 (CreateReturnCommand): a return needs a looked-up shipment and one selected line.
        if (!resolvedShipment || !input || resolvedShipment.shipmentId !== input.value || selectedLines().length === 0) {
            showAlert('formReturnAlert', t('ResolveFirst'));
            return;
        }
        if (!ELIGIBLE_SHIPMENT_STATUSES.has(resolvedShipment.status)) return;
        hideAlert('formReturnAlert');
        const bodyText = buildCreateBody();
        if (!createIntent || createIntent.bodyText !== bodyText) {
            createIntent = { key: uuid(), bodyText, blocked: false, blockedFailure: null };
        } else if (createIntent.blocked) {
            showAlert('formReturnAlert', createIntent.blockedFailure);
            return;
        }
        const intent = createIntent;
        const save = document.getElementById('btnSaveReturn');
        createPending = true;
        if (save) save.disabled = true;
        try {
            const response = await fetch(endpoint, {
                method: 'POST', credentials: 'same-origin', headers: mutationHeaders(intent.key), body: intent.bodyText
            });
            if (response.ok) {
                let result = null;
                try { result = await response.json(); } catch (_) { result = null; }
                createIntent = null;
                offcanvas('offcanvasCreateEdit')?.hide();
                window.showToast?.(t(result?.idempotentReplay === true ? 'ReturnCreateReplayed' : 'ReturnCreated'), 'success');
                reloadList(); // a replay snapshot is never shown as current state
                return;
            }
            handleCreateFailure(await readFailure(response), intent);
        } catch (error) {
            console.error('[Returns] Create request failed.', error);
            // Unknown outcome: keep the same key and body for the explicit retry.
            showAlert('formReturnAlert', networkFailure('PERSISTENCE_UNAVAILABLE'));
        } finally {
            createPending = false;
            if (save) save.disabled = !(resolvedShipment && ELIGIBLE_SHIPMENT_STATUSES.has(resolvedShipment.status));
        }
    };

    const handleCreateFailure = (failure, intent) => {
        if (failure.status === 401) { handleUnauthorized(); return; }
        if (failure.status === 403) { closeCreateAsForbidden(failure); return; }
        if (failure.status === 404 && failure.code !== 'SHIPMENT_LINE_NOT_FOUND') {
            // Safe-not-found: close, no shipment data left, one localized text + support reference, reload.
            clearResolvedShipment();
            offcanvas('offcanvasCreateEdit')?.hide();
            toastFailure(failure);
            reloadList();
            return;
        }
        if (failure.code === 'IDEMPOTENCY_KEY_REUSED' || failure.code === 'CORRELATION_ROOT_MISMATCH') {
            // Stop; no automatic new key. A new user intent (an edited payload) is required.
            intent.blocked = true;
            intent.blockedFailure = failure;
        }
        // 400/415/422/409 RETURN_SOURCE_CHANGED/404 line: inputs kept. 500/503: same key + identical body on the
        // explicit retry (retryable). 502: shown once; there is no automatic retry anywhere.
        showAlert('formReturnAlert', failure);
    };

    // ─── Transition ──────────────────────────────────────────────────────────
    const openTransition = (returnId, target) => {
        const row = rowsById.get(returnId);
        if (!row || !isText(row.shipmentId) || !UUID_PATTERN.test(row.shipmentId)) return;
        const targets = allowedTargets(row);
        if (!targets.includes(target)) return;
        transitionContext = { returnId, shipmentId: row.shipmentId, rmaNumber: row.rmaNumber, fromStatus: row.status };
        transitionIntent = null;
        hideAlert('formReturnTransitionAlert');
        const number = document.getElementById('transitionRmaNumber');
        if (number) number.textContent = isText(row.rmaNumber) ? row.rmaNumber : t('NotProvided');
        const select = document.getElementById('transitionTarget');
        if (select) {
            select.replaceChildren(...targets.map((value) => {
                const option = document.createElement('option');
                option.value = value;
                option.textContent = targetLabel(value);
                option.selected = value === target;
                return option;
            }));
        }
        const occurredAt = document.getElementById('transitionOccurredAt');
        if (occurredAt) occurredAt.value = nowWithOffset(); // default now; the exact text is kept for retries
        ['transitionDispositionCode', 'transitionInventoryReference'].forEach((id) => {
            const el = document.getElementById(id);
            if (el) el.value = '';
        });
        syncTransitionTarget();
        const el = document.getElementById('offcanvasReturnTransition');
        if (el) el.inert = false;
        offcanvas('offcanvasReturnTransition')?.show();
    };

    const syncTransitionTarget = () => {
        const target = document.getElementById('transitionTarget')?.value || '';
        document.getElementById('transitionDispositionGroup')?.classList.toggle('d-none', target !== 'Dispositioned');
        document.getElementById('transitionInventoryGroup')?.classList.toggle('d-none', !INVENTORY_REFERENCE_TARGETS.has(target));
        document.getElementById('transitionReceivedNote')?.classList.toggle('d-none', target !== 'Received');
    };

    // TransitionReturnCommand: targetStatus + occurredAt (exact text); dispositionCode only for Dispositioned (sent as
    // typed, whitespace kept); inventoryTransactionReferenceId only for Received/Dispositioned and omitted when empty
    // (README A9). Optional fields not listed for a target are omitted (pack §32.7).
    const buildTransitionBody = () => {
        const target = document.getElementById('transitionTarget')?.value || '';
        const body = { targetStatus: target, occurredAt: document.getElementById('transitionOccurredAt')?.value ?? '' };
        if (INVENTORY_REFERENCE_TARGETS.has(target)) {
            const reference = document.getElementById('transitionInventoryReference')?.value ?? '';
            if (reference !== '') body.inventoryTransactionReferenceId = reference;
        }
        if (target === 'Dispositioned') body.dispositionCode = document.getElementById('transitionDispositionCode')?.value ?? '';
        return { target, bodyText: JSON.stringify(body) };
    };

    const closeTransition = () => {
        offcanvas('offcanvasReturnTransition')?.hide();
        transitionContext = null;
    };

    const requestTransition = () => {
        if (!transitionContext) return;
        if (transitionPending) { window.showToast?.(t('RequestPending'), 'warning'); return; }
        const { target } = buildTransitionBody();
        // Mirror of the annex rule only: Dispositioned needs dispositionCode length ≥ 1 (no trim; whitespace accepted).
        if (target === 'Dispositioned' && (document.getElementById('transitionDispositionCode')?.value ?? '') === '') {
            showAlert('formReturnTransitionAlert', t('ErrDispositionRequired'));
            return;
        }
        const subtext = target === 'Received' ? `${t('ConfirmTransition')} ${t('ReceivedManualNote')}` : t('ConfirmTransition');
        // Shared premium wrapper only (MOD-0013 _GlobalConfirmation: showConfirm(title, callback, options));
        // no native dialog and no direct SweetAlert call. Title and subtext are resx text only.
        window.showConfirm?.(targetLabel(target), () => { void submitTransition(); }, {
            subtext,
            type: DANGEROUS_TARGETS.has(target) ? 'danger' : undefined,
            confirmButtonText: t('SubmitTransition')
        });
    };

    const submitTransition = async () => {
        const context = transitionContext;
        if (!context || transitionPending) return;
        hideAlert('formReturnTransitionAlert');
        const { bodyText } = buildTransitionBody();
        if (!transitionIntent || transitionIntent.returnId !== context.returnId || transitionIntent.bodyText !== bodyText) {
            transitionIntent = { returnId: context.returnId, key: uuid(), bodyText, blocked: false, blockedFailure: null };
        } else if (transitionIntent.blocked) {
            showAlert('formReturnTransitionAlert', transitionIntent.blockedFailure);
            return;
        }
        const intent = transitionIntent;
        const submit = document.getElementById('btnSubmitTransition');
        transitionPending = true;
        if (submit) submit.disabled = true;
        const url = `${endpoint}/${encodeURIComponent(context.returnId)}/transition?shipmentId=${encodeURIComponent(context.shipmentId)}`;
        try {
            const response = await fetch(url, {
                method: 'POST', credentials: 'same-origin', headers: mutationHeaders(intent.key), body: intent.bodyText
            });
            if (response.ok) {
                let result = null;
                try { result = await response.json(); } catch (_) { result = null; }
                transitionIntent = null;
                closeTransition();
                window.showToast?.(t(result?.idempotentReplay === true ? 'TransitionReplayed' : 'TransitionCompleted'), 'success');
                reloadList(); // a replay snapshot is never shown as current state
                return;
            }
            handleTransitionFailure(await readFailure(response), intent);
        } catch (error) {
            console.error('[Returns] Transition request failed.', error);
            showAlert('formReturnTransitionAlert', networkFailure('PERSISTENCE_UNAVAILABLE'));
        } finally {
            transitionPending = false;
            if (submit) submit.disabled = false;
        }
    };

    const handleTransitionFailure = (failure, intent) => {
        if (failure.status === 401) { handleUnauthorized(); return; }
        if (failure.status === 403 || failure.status === 404) {
            // Close; hidden surface inert; one localized text + support reference; reload (RU-11).
            closeTransition();
            toastFailure(failure);
            reloadList();
            return;
        }
        if (failure.code === 'INVALID_RETURN_TRANSITION') {
            // Stale state: conflict text + support reference + reload (RU-17).
            closeTransition();
            toastFailure(failure);
            reloadList();
            return;
        }
        if (failure.code === 'IDEMPOTENCY_KEY_REUSED' || failure.code === 'CORRELATION_ROOT_MISMATCH') {
            intent.blocked = true;
            intent.blockedFailure = failure;
        }
        // DISPOSITION_REQUIRED/400/415: inputs kept. 500/503: same key + identical body on the explicit retry.
        showAlert('formReturnTransitionAlert', failure);
    };

    // ─── Filters ─────────────────────────────────────────────────────────────
    const getAppliedFilterCount = () => [appliedFilters.status, appliedFilters.shipmentId].filter((v) => v !== '').length;

    const setupFilterSelect = () => {
        if (!window.jQuery?.fn?.select2) return;
        const $s = window.jQuery('#filterStatus');
        if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
        $s.select2({
            dropdownParent: window.jQuery('body'),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            selectionCssClass: 'form-select form-select-sm',
            placeholder: $s.data('placeholder') || '',
            minimumResultsForSearch: Infinity,
            width: 'element'
        });
    };

    const syncFilterControls = () => {
        const status = document.getElementById('filterStatus');
        const shipment = document.getElementById('filterShipmentId');
        if (status) {
            status.value = appliedFilters.status;
            if (window.jQuery) window.jQuery(status).trigger('change');
        }
        if (shipment) shipment.value = appliedFilters.shipmentId;
    };

    const applyFilters = () => {
        appliedFilters = {
            status: document.getElementById('filterStatus')?.value || '',
            shipmentId: document.getElementById('filterShipmentId')?.value ?? '' // as typed; backend validates
        };
        if (dt) window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
        markViewDirty();
        reloadList();
    };

    const resetFilters = () => {
        appliedFilters = { status: '', shipmentId: '' };
        syncFilterControls();
        if (dt) window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
        markViewDirty();
        reloadList();
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row')
            || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-3');
        }
        const collapseEl = document.getElementById(filterCollapseId);
        if (filterBtn && collapseEl && !filterBtn.dataset.bound) {
            filterBtn.dataset.bound = '1';
            collapseEl.addEventListener('shown.bs.collapse', () => filterBtn.setAttribute('aria-expanded', 'true'));
            collapseEl.addEventListener('hidden.bs.collapse', () => filterBtn.setAttribute('aria-expanded', 'false'));
        }
    };

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById(filterCollapseId);
        if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };

    // ─── Save View (shared personalizationClient only; no browser storage) ───
    const currentView = () => ({
        columnVisibility: colvisColumns.map((index) => dt.column(index).visible()),
        order: dt.order(),
        search: dt.search(),
        filters: Object.assign({}, appliedFilters)
    });

    const setSaveViewVisible = (visible) => {
        document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible);
    };

    const markViewDirty = () => { if (saveViewArmed) setSaveViewVisible(true); };

    const viewDefinition = (record) => record?.viewDefinition || record?.ViewDefinition || null;
    const viewId = (record) => record?.id || record?.Id || record?.viewId || record?.ViewId || null;

    const loadDefaultView = async () => {
        defaultViewRecord = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items)
                ? (items.find((v) => v?.isDefault === true || v?.IsDefault === true) || items[0] || null) : null;
            const definition = viewDefinition(defaultViewRecord);
            return typeof definition === 'string' ? JSON.parse(definition) : definition;
        } catch (error) {
            if (!error?.authHandled) console.error('[Returns SaveView] Failed to load saved views.', error);
            return null;
        }
    };

    const applyView = (view) => {
        if (!dt || !view || typeof view !== 'object') return;
        if (Array.isArray(view.columnVisibility)) {
            colvisColumns.forEach((index, position) => {
                if (typeof view.columnVisibility[position] === 'boolean') dt.column(index).visible(view.columnVisibility[position], false);
            });
        }
        const filters = view.filters && typeof view.filters === 'object' ? view.filters : {};
        appliedFilters = {
            status: isText(filters.status) ? filters.status : '',
            shipmentId: isText(filters.shipmentId) ? filters.shipmentId : ''
        };
        syncFilterControls();
        if (Array.isArray(view.order)) dt.order(view.order);
        if (isText(view.search)) dt.search(view.search);
        try { dt.columns.adjust(); dt.responsive?.recalc?.(); } catch (_) { /* layout only */ }
        window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
    };

    const saveView = async () => {
        if (!dt || !personalizationClient?.saveView) return;
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: t('SaveView'),
            viewDefinition: currentView(),
            isDefault: true,
            visibility: 'private'
        };
        try {
            const existingId = viewId(defaultViewRecord);
            const saved = existingId
                ? await personalizationClient.updateView(existingId, payload)
                : await personalizationClient.saveView(payload);
            defaultViewRecord = saved && typeof saved === 'object' ? (saved.data || saved.Data || saved) : payload;
            setSaveViewVisible(false);
            window.showToast?.(t('RecordSaved'), 'success');
        } catch (error) {
            if (error?.authHandled) return;
            console.error('[Returns SaveView] Failed to save the view.', error);
            window.showToast?.(t('ErrorOccurred'), 'error');
        }
    };

    // ─── DataTable ───────────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!tableEl || typeof DataTable === 'undefined' || !window.DtDefaults) return;
        syncL10n();
        setListState('skeleton');
        const savedView = await loadDefaultView();
        if (savedView?.filters) {
            appliedFilters = {
                status: isText(savedView.filters.status) ? savedView.filters.status : '',
                shipmentId: isText(savedView.filters.shipmentId) ? savedView.filters.shipmentId : ''
            };
        }

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm" aria-hidden="true"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: t('Filter'), 'aria-label': t('Filter'), 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            saveFilterBtn: {
                text: `<i class="icon-base bx bx-save icon-sm" aria-hidden="true"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(t('SaveView'))}</span>`,
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: t('SaveView'), 'data-bs-toggle': 'tooltip' },
                action: () => { void saveView(); }
            }
        };
        // Toolbar: Add (only with create + shipments.read), filter, Save View, column visibility. No export and no import.
        const buttons = window.DtDefaults.exportButtons(permissions.canCreate ? t('AddNewReturns') : '', {}, extraButtons,
            { exportColumns: [], colvisColumns })
            .filter((feature) => !feature?.buttons?.some?.((button) => String(button?.className || '').includes('dt-export-collection-btn')));

        dt = new DataTable(tableEl, window.DtDefaults.create({
            serverSide: false,
            stateSave: false,
            processing: false,
            ajax: loadReturns,
            order: [[1, 'desc']],
            colReorder: { columns: ':gt(0):not(:last-child)' },
            responsive: { details: { type: 'column', target: 0 } },
            columns: [
                { data: null, defaultContent: '', className: 'control', orderable: false, searchable: false },
                { data: 'rmaNumber', render: (value, type) => (type === 'display' ? (isText(value) && value ? ltr(value) : notProvided()) : (isText(value) ? value : '')) },
                { data: 'shipmentId', render: renderCopyable },
                { data: 'status', render: renderStatus },
                { data: null, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (_v, type, row) => (type === 'display' ? renderActions(row) : '') }
            ],
            buttons,
            language: { emptyTable: t('EmptyState'), zeroRecords: t('EmptyState') },
            initComplete: function () {
                mountInlineFilter();
                setupFilterSelect();
                syncFilterControls();
                if (savedView) applyView(savedView);
                if (!permissions.canCreate) document.querySelectorAll('.add-new').forEach((el) => el.remove());
                document.querySelector('.add-new')?.addEventListener('click', (event) => { event.preventDefault(); openCreate(); });
                window.DtDefaults.updateVisualState?.(this.api(), getAppliedFilterCount());
                // Armed only after the saved view is applied; events raised while applying it do not mark the view dirty.
                saveViewArmed = true;
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState?.(this.api(), getAppliedFilterCount());
            }
        }));

        dt.on('column-visibility.dt order.dt search.dt column-reorder.dt columns-reordered.dt', () => {
            window.DtDefaults.updateVisualState?.(dt, getAppliedFilterCount());
            markViewDirty();
        });
    };

    // ─── Events ──────────────────────────────────────────────────────────────
    const bindEvents = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', applyFilters);
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => { event.preventDefault(); resetFilters(); });
        document.getElementById('btnReturnsRetry')?.addEventListener('click', () => {
            if (!dt) { void initDataTable(); return; }
            setListState(loadedOnce ? 'table' : 'skeleton');
            reloadList();
        });

        tableEl?.addEventListener('click', (event) => {
            const quick = event.target.closest('.js-quick-view');
            if (quick) { event.preventDefault(); openQuickView(quick.dataset.returnId); return; }
            const transition = event.target.closest('.js-transition');
            if (transition) { event.preventDefault(); openTransition(transition.dataset.returnId, transition.dataset.targetStatus); }
        });

        document.addEventListener('click', async (event) => {
            const copy = event.target.closest('.js-copy-value');
            if (!copy) return;
            event.preventDefault();
            const source = copy.dataset.copySource ? document.getElementById(copy.dataset.copySource) : null;
            const value = copy.dataset.copyValue || source?.textContent || '';
            if (!value) return;
            try {
                await navigator.clipboard.writeText(value);
                window.showToast?.(t('CopyDone'), 'success');
            } catch (error) {
                console.error('[Returns] Copy failed.', error);
                window.showToast?.(t('ErrorOccurred'), 'error');
            }
        });

        document.getElementById('btnResolveShipment')?.addEventListener('click', () => { void resolveShipment(); });
        document.getElementById('returnShipmentId')?.addEventListener('input', () => {
            if (resolvedShipment || !document.getElementById('returnShipmentResolved')?.classList.contains('d-none')) clearResolvedShipment();
            else resolveSequence += 1;
        });
        document.getElementById('returnLinesBody')?.addEventListener('change', (event) => {
            const select = event.target.closest('.js-line-select');
            if (!select) return;
            const quantity = select.closest('.return-line-row')?.querySelector('.js-line-quantity');
            if (quantity) {
                quantity.disabled = !select.checked;
                if (select.checked) quantity.focus();
            }
        });
        document.getElementById('btnAddEvidence')?.addEventListener('click', addEvidenceRow);
        evidenceList()?.addEventListener('click', (event) => {
            const remove = event.target.closest('.js-remove-evidence');
            if (!remove) return;
            event.preventDefault();
            remove.closest('.return-evidence-row')?.remove();
            document.getElementById('btnAddEvidence')?.focus();
        });
        document.getElementById('btnSaveReturn')?.addEventListener('click', () => { void submitCreate(); });

        document.getElementById('transitionTarget')?.addEventListener('change', syncTransitionTarget);
        document.getElementById('btnSubmitTransition')?.addEventListener('click', requestTransition);
        ['formReturn', 'formReturnTransition'].forEach((id) => {
            document.getElementById(id)?.addEventListener('submit', (event) => event.preventDefault());
        });

        // Hidden surfaces are inert and not keyboard-reachable; late responses never re-open them (pack §32.8).
        ['offcanvasCreateEdit', 'offcanvasDetailsPreview', 'offcanvasReturnTransition'].forEach((id) => {
            const el = document.getElementById(id);
            if (!el) return;
            el.inert = true;
            el.addEventListener('show.bs.offcanvas', () => { el.inert = false; });
            el.addEventListener('hidden.bs.offcanvas', () => {
                el.inert = true;
                if (id === 'offcanvasReturnTransition') transitionContext = null;
                if (id === 'offcanvasCreateEdit') clearResolvedShipment();
            });
        });
    };

    return {
        init: function () {
            syncL10n();
            bindEvents();
            void initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => ReturnsList.init());
