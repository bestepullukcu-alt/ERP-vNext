'use strict';

/**
 * MOD-0187 Claims — tenant list page (pack §32, golden-reference slim, bounded DataTables v2 profile).
 * DRAFT overlay — not built, not runtime-verified, not writer-complete.
 *
 * Bound operations only (pack §32.3): queryClaims (list/filter/reload), createClaim (create offcanvas),
 * transitionClaim (row action) and the Shipment resolve projection. Every request goes to the same-origin adapter
 * /SupplyChain/Claims/api*; the browser never calls the Gateway, never holds a token and never sends tenant/LE scope.
 *
 * Out of scope and absent on purpose: checkbox and bulk (CU-SCR-01), edit and delete (CU-SCR-02), import and export (CU-SCR-03),
 * server paging/search/sort (CU-SCR-04), multi-select status (CU-SCR-05), by-ID claim GET and Details page (§32.3).
 *
 * Payload rule (pack §32.6/§32.8): values are sent exactly as typed — no trim, no uppercase, no number conversion.
 * One pending request per intent; a retry after network/500/503 re-sends the same Idempotency-Key with identical body
 * text; an edited payload is a new intent with a new key.
 */
const ClaimsList = (function () {
    const endpoint = '/SupplyChain/Claims/api';
    const tableEl = document.querySelector('.datatables-claims');
    const personalizationClient = window.personalizationClient;
    // ASSUMPTION A6: module/page codes of the §33 manifest; the integration owner confirms the personalization codes.
    const personalizationContext = { moduleKey: 'claims-management', pageKey: 'CLAIMS' };
    const filterCollapseId = 'inlineFilterCollapse';
    const colvisColumns = [1, 2, 3, 4, 5];
    const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

    // ClaimLifecycle.cs:4-7 (display only; the backend decides).
    const TRANSITIONS = Object.freeze({
        Open: ['Investigating', 'Withdrawn'],
        Investigating: ['Approved', 'Rejected'],
        Approved: ['Settled'],
        Rejected: ['Closed'],
        Settled: ['Closed'],
        Closed: [],
        Withdrawn: []
    });
    // ClaimModels.cs:45 target map, expressed as the page permission flags (each also needs shipments.read).
    const TARGET_PERMISSION = Object.freeze({
        Investigating: 'canInvestigate', Withdrawn: 'canInvestigate',
        Approved: 'canDecide', Rejected: 'canDecide', Closed: 'canDecide',
        Settled: 'canSettle'
    });
    const TARGET_ACTION_KEY = Object.freeze({
        Investigating: 'ActionInvestigate', Withdrawn: 'ActionWithdraw', Approved: 'ActionApprove',
        Rejected: 'ActionReject', Settled: 'ActionSettle', Closed: 'ActionClose'
    });
    const DANGEROUS_TARGETS = new Set(['Withdrawn', 'Rejected']);
    const CLAIM_STATUS_KEY = Object.freeze({
        Open: 'StatusOpen', Investigating: 'StatusInvestigating', Approved: 'StatusApproved', Rejected: 'StatusRejected',
        Settled: 'StatusSettled', Closed: 'StatusClosed', Withdrawn: 'StatusWithdrawn'
    });
    const CLAIM_STATUS_CSS = Object.freeze({
        Open: 'info', Investigating: 'warning', Approved: 'success', Rejected: 'danger',
        Settled: 'primary', Closed: 'dark', Withdrawn: 'secondary'
    });
    const SHIPMENT_STATUS_KEY = Object.freeze({
        Draft: 'ShipmentStatusDraft', Planned: 'ShipmentStatusPlanned', Dispatched: 'ShipmentStatusDispatched',
        InTransit: 'ShipmentStatusInTransit', Delivered: 'ShipmentStatusDelivered', Exception: 'ShipmentStatusException',
        Closed: 'ShipmentStatusClosed', Cancelled: 'ShipmentStatusCancelled'
    });
    // ClaimLifecycle.cs:10 (display note + disabled submit; server 422 CLAIM_SHIPMENT_INELIGIBLE stays authoritative).
    const ELIGIBLE_SHIPMENT_STATUSES = new Set(['Dispatched', 'InTransit', 'Delivered', 'Exception', 'Closed']);
    // Annex D187-05 — all 18 codes, localized (pack §32.8/§32.9).
    const ERROR_KEY = Object.freeze({
        INVALID_REQUEST: 'ErrInvalidRequest',
        UNAUTHENTICATED: 'ErrUnauthenticated',
        FORBIDDEN: 'ErrForbidden',
        CLAIM_NOT_FOUND: 'ErrClaimNotFound',
        UNSUPPORTED_MEDIA_TYPE: 'ErrUnsupportedMediaType',
        CLAIM_SHIPMENT_INELIGIBLE: 'ErrClaimShipmentIneligible',
        CLAIM_CARRIER_MISMATCH: 'ErrClaimCarrierMismatch',
        CLAIM_AMOUNT_INVALID: 'ErrClaimAmountInvalid',
        CLAIM_APPROVAL_AMOUNT_INVALID: 'ErrClaimApprovalAmountInvalid',
        CLAIM_APPROVED_AMOUNT_NOT_ALLOWED: 'ErrClaimApprovedAmountNotAllowed',
        INVALID_CLAIM_TRANSITION: 'ErrInvalidClaimTransition',
        CLAIM_CORRELATION_MISMATCH: 'ErrClaimCorrelationMismatch',
        IDEMPOTENCY_KEY_REUSED: 'ErrIdempotencyKeyReused',
        CLAIM_REFERENCE_INVALID: 'ErrClaimReferenceInvalid',
        CLAIM_REFERENCE_INCOMPLETE: 'ErrClaimReferenceIncomplete',
        CLAIM_REFERENCE_UNAVAILABLE: 'ErrClaimReferenceUnavailable',
        CLAIM_STORAGE_UNAVAILABLE: 'ErrClaimStorageUnavailable',
        INTERNAL_ERROR: 'ErrInternalError'
    });
    // Status → code when an envelope carries no known code (never raw text on screen).
    const STATUS_FALLBACK_CODE = Object.freeze({
        400: 'INVALID_REQUEST', 401: 'UNAUTHENTICATED', 403: 'FORBIDDEN', 404: 'CLAIM_NOT_FOUND',
        415: 'UNSUPPORTED_MEDIA_TYPE', 422: 'INVALID_REQUEST', 409: 'INVALID_REQUEST',
        502: 'CLAIM_REFERENCE_INVALID', 503: 'CLAIM_STORAGE_UNAVAILABLE'
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
    let createIntent = null;           // { key, bodyText, blocked }
    let createPending = false;
    let resolveSequence = 0;
    let resolvedShipment = null;       // { shipmentId (exact typed text), shipmentNumber, status, carrierId }

    // Transition intent and state.
    let transitionContext = null;      // { claimId, shipmentId, claimNumber, fromStatus }
    let transitionIntent = null;       // { claimId, key, bodyText, blocked }
    let transitionPending = false;

    // ─── Basics ──────────────────────────────────────────────────────────────
    function readPermissions() {
        const island = document.getElementById('claims-permissions');
        const fallback = { canCreate: false, canInvestigate: false, canDecide: false, canSettle: false };
        if (!island) return fallback;
        try {
            const parsed = JSON.parse(island.textContent || '{}');
            return {
                canCreate: parsed.canCreate === true,
                canInvestigate: parsed.canInvestigate === true,
                canDecide: parsed.canDecide === true,
                canSettle: parsed.canSettle === true
            };
        } catch (error) {
            console.error('[Claims] Permission payload could not be parsed.', error);
            return fallback;
        }
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
    const claimStatusLabel = (status) => (isText(status) && CLAIM_STATUS_KEY[status] ? t(CLAIM_STATUS_KEY[status]) : '');
    const shipmentStatusLabel = (status) =>
        (isText(status) && SHIPMENT_STATUS_KEY[status] ? t(SHIPMENT_STATUS_KEY[status]) : t('NotProvided'));
    const targetLabel = (target) => (target === 'Settled' ? t('TargetSettledLabel') : claimStatusLabel(target));
    const offcanvas = (id) => {
        const el = document.getElementById(id);
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    // Exact decimal text → lexical sort key (ordering only; the displayed/sent text is never converted).
    const amountSortKey = (value) => {
        if (!isText(value)) return '';
        const match = /^(\d+)(?:\.(\d+))?$/.exec(value);
        if (!match) return `~${value}`;
        return `${match[1].replace(/^0+(?=\d)/, '').padStart(160, '0')}.${(match[2] || '').padEnd(80, '0')}`;
    };

    // Local "now" with explicit offset, e.g. 2026-09-26T16:40:12+03:00 (G-DATETIME: plain text input for now).
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
    const failureText = (failure) => t(ERROR_KEY[failure.code] || 'ErrInternalError');
    const retryable = (failure) => failure.status === 0 || failure.status === 500 || failure.status === 503;

    // Toast/confirm text is resx text plus values that are shape-checked first (UUID reference, decimal amount), so it
    // is safe whether the shared wrapper renders text or HTML (README NOT-VERIFIED N7).
    const safeReference = (value) => (isText(value) && UUID_PATTERN.test(value) ? value : '');
    const safeDecimal = (value) => (isText(value) && /^-?\d+(\.\d+)?$/.test(value) ? value : '');
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
        if (typeof failureOrText !== 'string' && failureOrText.reference) {
            const reference = document.createElement('div');
            reference.className = 'small mt-1';
            const label = document.createElement('span');
            label.textContent = `${t('SupportReference')}: `;
            const value = document.createElement('bdi');
            value.dir = 'ltr';
            value.className = 'font-monospace';
            value.textContent = failureOrText.reference;
            const copy = document.createElement('button');
            copy.type = 'button';
            copy.className = 'btn btn-sm btn-icon btn-text-secondary js-copy-value';
            copy.dataset.copyValue = failureOrText.reference;
            copy.setAttribute('aria-label', t('Copy'));
            copy.innerHTML = '<i class="bx bx-copy" aria-hidden="true"></i>';
            reference.append(label, value, copy);
            alert.appendChild(reference);
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
        document.getElementById('claims-table-host')?.classList.toggle('d-none', state !== 'table');
        const errorHost = document.getElementById('claims-error-state');
        errorHost?.classList.toggle('d-none', state !== 'error');
        if (state === 'error' && failure) {
            const message = document.getElementById('claims-error-message');
            if (message) message.textContent = failure.code ? failureText(failure) : t('ListErrorState');
            const referenceRow = document.getElementById('claims-error-reference');
            const referenceValue = document.getElementById('claims-error-reference-value');
            const referenceCopy = document.getElementById('claims-error-reference-copy');
            if (referenceRow && referenceValue) {
                referenceValue.textContent = failure.reference || '';
                referenceRow.classList.toggle('d-none', !failure.reference);
                if (referenceCopy) referenceCopy.dataset.copyValue = failure.reference || '';
            }
        }
    };

    const isListEnvelope = (body) => body !== null && typeof body === 'object' && Array.isArray(body.items)
        && body.items.every((item) => item !== null && typeof item === 'object' && !Array.isArray(item));

    const renderTotal = (total) => {
        const el = document.getElementById('claims-total');
        if (!el) return;
        el.textContent = `${t('TotalLabel')}: ${Number.isInteger(total) ? total : t('NotProvided')}`;
    };

    // DataTables ajax function (serverSide:false): one request per load, client paging/sort/search over the set.
    const loadClaims = async (_data, callback) => {
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
            body.items.forEach((item) => { if (isText(item.claimId) && item.claimId) rowsById.set(item.claimId, item); });
            loadedOnce = true;
            setListState('table');
            renderTotal(body.total);
            callback({ data: body.items });
        } catch (error) {
            console.error('[Claims] List request failed.', error);
            setListState('error', networkFailure('CLAIM_STORAGE_UNAVAILABLE'));
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
        if (!isText(row?.claimId) || !row.claimId) return ''; // no action without claimId (pack §32.5)
        const id = escapeHtml(row.claimId);
        const quickView = `<button type="button" class="btn btn-sm btn-icon btn-text-secondary rounded-pill js-quick-view" `
            + `data-claim-id="${id}" title="${escapeHtml(t('QuickView'))}" aria-label="${escapeHtml(t('QuickView'))}">`
            + '<i class="bx bx-show" aria-hidden="true"></i></button>';
        const targets = isText(row.shipmentId) ? allowedTargets(row) : [];
        if (!targets.length) return `<div class="d-inline-flex align-items-center gap-1">${quickView}</div>`;
        const items = targets.map((target) => `<button type="button" class="dropdown-item js-transition${DANGEROUS_TARGETS.has(target) ? ' text-danger' : ''}" `
            + `data-claim-id="${id}" data-target-status="${escapeHtml(target)}">${escapeHtml(t(TARGET_ACTION_KEY[target]))}</button>`).join('');
        return `<div class="d-inline-flex align-items-center gap-1">${quickView}`
            + '<div class="dropdown">'
            + `<button type="button" class="btn btn-sm btn-icon btn-text-secondary rounded-pill dropdown-toggle hide-arrow" data-bs-toggle="dropdown" aria-expanded="false" aria-label="${escapeHtml(t('ActionsHeader'))}">`
            + '<i class="bx bx-dots-vertical-rounded" aria-hidden="true"></i></button>'
            + `<div class="dropdown-menu dropdown-menu-end">${items}</div></div></div>`;
    };

    const renderStatus = (status, type) => {
        const label = claimStatusLabel(status);
        if (type !== 'display') return label || (isText(status) ? status : '');
        if (!label) return notProvided();
        return `<span class="badge bg-label-${CLAIM_STATUS_CSS[status] || 'secondary'}">${escapeHtml(label)}</span>`;
    };

    const renderCopyable = (value, type) => {
        if (!isText(value) || !value) return type === 'display' ? notProvided() : '';
        if (type !== 'display') return value;
        return `<span class="d-inline-flex align-items-center gap-1">${ltr(value, 'font-monospace')}`
            + `<button type="button" class="btn btn-sm btn-icon btn-text-secondary js-copy-value" data-copy-value="${escapeHtml(value)}" aria-label="${escapeHtml(t('Copy'))}">`
            + '<i class="bx bx-copy" aria-hidden="true"></i></button></span>';
    };

    // ─── QuickView (row summary only; no request, no Edit) ───────────────────
    const openQuickView = (claimId) => {
        const row = rowsById.get(claimId);
        if (!row) return;
        const setText = (id, value) => {
            const el = document.getElementById(id);
            if (el) el.textContent = isText(value) && value !== '' ? value : t('NotProvided');
        };
        setText('oc-claim-number', row.claimNumber);
        setText('oc-claim-id', row.claimId);
        setText('oc-shipment-id', row.shipmentId);
        setText('oc-claimed-amount', row.claimedAmount);
        setText('oc-currency', row.currency);
        const subtitle = document.getElementById('oc-subtitle');
        if (subtitle) subtitle.textContent = claimStatusLabel(row.status) || t('NotProvided');
        const status = document.getElementById('oc-status');
        if (status) status.innerHTML = renderStatus(row.status, 'display');
        const copy = document.getElementById('oc-claim-id-copy');
        if (copy) copy.dataset.copyValue = row.claimId;
        offcanvas('offcanvasDetailsPreview')?.show();
    };

    // ─── Create ──────────────────────────────────────────────────────────────
    const evidenceList = () => document.getElementById('claimEvidenceList');

    const addEvidenceRow = () => {
        const template = document.getElementById('claimEvidenceRowTemplate');
        const list = evidenceList();
        if (!template || !list) return;
        list.appendChild(template.content.cloneNode(true));
        const rows = list.querySelectorAll('.claim-evidence-row');
        const addedInput = rows[rows.length - 1]?.querySelector('.claim-evidence-input');
        if (addedInput) {
            addedInput.id = `claimEvidence_${uuid()}`;
            addedInput.focus();
        }
    };

    const clearResolvedShipment = () => {
        resolveSequence += 1; // any in-flight resolve becomes stale (CU-13)
        resolvedShipment = null;
        document.getElementById('claimShipmentResolved')?.classList.add('d-none');
        const number = document.getElementById('claimShipmentNumber');
        const status = document.getElementById('claimShipmentStatus');
        if (number) number.textContent = '';
        if (status) status.textContent = '';
        const link = document.getElementById('claimLinkCarrier');
        if (link) { link.checked = false; link.disabled = true; }
        document.getElementById('claimIneligibleNote')?.classList.add('d-none');
        const save = document.getElementById('btnSaveClaim');
        if (save) save.disabled = true;
    };

    const openCreate = () => {
        if (!permissions.canCreate) return;
        const form = document.getElementById('formClaim');
        form?.reset();
        const list = evidenceList();
        if (list) list.replaceChildren();
        hideAlert('formClaimAlert');
        clearResolvedShipment();
        createIntent = null;
        offcanvas('offcanvasCreateEdit')?.show();
    };

    const resolveShipment = async () => {
        const input = document.getElementById('claimShipmentId');
        if (!input) return;
        hideAlert('formClaimAlert');
        clearResolvedShipment();
        const requested = input.value;
        if (!UUID_PATTERN.test(requested)) {
            // The adapter route needs a UUID segment; nothing else is checked here.
            showAlert('formClaimAlert', { status: 400, code: 'INVALID_REQUEST', reference: '' });
            return;
        }
        const sequence = resolveSequence;
        const isCurrent = () => sequence === resolveSequence && input.value === requested;
        try {
            const response = await fetch(`${endpoint}/shipments/${encodeURIComponent(requested)}`,
                { credentials: 'same-origin', headers: traceHeaders() });
            if (!isCurrent()) return; // late response for a previous UUID never populates
            if (!response.ok) {
                const failure = await readFailure(response);
                if (!isCurrent()) return;
                if (response.status === 401) { handleUnauthorized(); return; }
                if (response.status === 403) { closeCreateAsForbidden(failure); return; }
                showAlert('formClaimAlert', failure); // 404: one safe text; no shipment data kept in the DOM
                return;
            }
            let body = null;
            try { body = await response.json(); } catch (_) { body = null; }
            if (!isCurrent()) return;
            if (!body || typeof body !== 'object') {
                showAlert('formClaimAlert', { status: 502, code: 'CLAIM_REFERENCE_INVALID', reference: response.headers.get('X-Correlation-Id') || '' });
                return;
            }
            resolvedShipment = {
                shipmentId: requested,
                shipmentNumber: isText(body.shipmentNumber) ? body.shipmentNumber : '',
                status: isText(body.status) ? body.status : '',
                carrierId: isText(body.carrierId) && body.carrierId ? body.carrierId : null
            };
            const number = document.getElementById('claimShipmentNumber');
            const status = document.getElementById('claimShipmentStatus');
            if (number) number.textContent = resolvedShipment.shipmentNumber || t('NotProvided');
            if (status) status.textContent = shipmentStatusLabel(resolvedShipment.status);
            document.getElementById('claimShipmentResolved')?.classList.remove('d-none');
            const link = document.getElementById('claimLinkCarrier');
            if (link) link.disabled = resolvedShipment.carrierId === null;
            const eligible = ELIGIBLE_SHIPMENT_STATUSES.has(resolvedShipment.status);
            document.getElementById('claimIneligibleNote')?.classList.toggle('d-none', eligible);
            const save = document.getElementById('btnSaveClaim');
            if (save) save.disabled = !eligible;
        } catch (error) {
            if (!isCurrent()) return;
            console.error('[Claims] Shipment resolve failed.', error);
            showAlert('formClaimAlert', networkFailure('CLAIM_REFERENCE_UNAVAILABLE'));
        }
    };

    const closeCreateAsForbidden = (failure) => {
        offcanvas('offcanvasCreateEdit')?.hide();
        document.querySelectorAll('.add-new').forEach((button) => { button.disabled = true; button.classList.add('disabled'); });
        toastFailure(failure);
    };

    // Body built in contract order; every value exactly as typed (CU-09, CU-11, CU-12).
    const buildCreateBody = () => {
        const body = { shipmentId: document.getElementById('claimShipmentId')?.value ?? '' };
        const link = document.getElementById('claimLinkCarrier');
        if (link?.checked && resolvedShipment?.carrierId) body.carrierId = resolvedShipment.carrierId;
        body.reasonCode = document.getElementById('claimReasonCode')?.value ?? '';
        body.claimedAmount = document.getElementById('claimClaimedAmount')?.value ?? '';
        body.currency = document.getElementById('claimCurrency')?.value ?? '';
        const evidence = Array.from(evidenceList()?.querySelectorAll('.claim-evidence-input') || []).map((input) => input.value);
        if (evidence.length) body.evidenceReferenceIds = evidence; // omitted when empty; order/duplicates/empty kept
        return JSON.stringify(body);
    };

    const submitCreate = async () => {
        if (createPending) { window.showToast?.(t('RequestPending'), 'warning'); return; }
        const input = document.getElementById('claimShipmentId');
        if (!resolvedShipment || !input || resolvedShipment.shipmentId !== input.value) {
            showAlert('formClaimAlert', t('ResolveFirst'));
            return;
        }
        if (!ELIGIBLE_SHIPMENT_STATUSES.has(resolvedShipment.status)) return;
        hideAlert('formClaimAlert');
        const bodyText = buildCreateBody();
        if (!createIntent || createIntent.bodyText !== bodyText) {
            createIntent = { key: uuid(), bodyText, blocked: false };
        } else if (createIntent.blocked) {
            showAlert('formClaimAlert', createIntent.blockedFailure);
            return;
        }
        const intent = createIntent;
        const save = document.getElementById('btnSaveClaim');
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
                window.showToast?.(t(result?.idempotentReplay === true ? 'ClaimCreateReplayed' : 'ClaimCreated'), 'success');
                reloadList();
                return;
            }
            const failure = await readFailure(response);
            handleCreateFailure(failure, intent);
        } catch (error) {
            console.error('[Claims] Create request failed.', error);
            // Unknown outcome: keep the same key and body for the explicit retry.
            showAlert('formClaimAlert', networkFailure('CLAIM_STORAGE_UNAVAILABLE'));
        } finally {
            createPending = false;
            if (save) save.disabled = !(resolvedShipment && ELIGIBLE_SHIPMENT_STATUSES.has(resolvedShipment.status));
        }
    };

    const handleCreateFailure = (failure, intent) => {
        switch (failure.status) {
            case 401:
                handleUnauthorized();
                return;
            case 403:
                closeCreateAsForbidden(failure);
                return;
            case 404:
                offcanvas('offcanvasCreateEdit')?.hide();
                toastFailure(failure);
                reloadList();
                return;
            case 409:
                // IDEMPOTENCY_KEY_REUSED / CLAIM_CORRELATION_MISMATCH: stop; no automatic new key.
                intent.blocked = true;
                intent.blockedFailure = failure;
                showAlert('formClaimAlert', failure);
                return;
            default:
                // 400/415/422: inputs kept. 500/502/503: same key + identical body on the explicit retry.
                if (!retryable(failure) && failure.status !== 422 && failure.status !== 400) createIntent = null;
                showAlert('formClaimAlert', failure);
        }
    };

    // ─── Transition ──────────────────────────────────────────────────────────
    const openTransition = (claimId, target) => {
        const row = rowsById.get(claimId);
        if (!row || !isText(row.shipmentId)) return;
        const targets = allowedTargets(row);
        if (!targets.includes(target)) return;
        transitionContext = { claimId, shipmentId: row.shipmentId, claimNumber: row.claimNumber, fromStatus: row.status };
        transitionIntent = null;
        hideAlert('formClaimTransitionAlert');
        const number = document.getElementById('transitionClaimNumber');
        if (number) number.textContent = isText(row.claimNumber) ? row.claimNumber : t('NotProvided');
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
        if (occurredAt) occurredAt.value = nowWithOffset();
        ['transitionApprovedAmount', 'transitionResolutionCode', 'transitionNote'].forEach((id) => {
            const el = document.getElementById(id);
            if (el) el.value = '';
        });
        syncTransitionTarget();
        const el = document.getElementById('offcanvasClaimTransition');
        if (el) el.inert = false;
        offcanvas('offcanvasClaimTransition')?.show();
    };

    const syncTransitionTarget = () => {
        const target = document.getElementById('transitionTarget')?.value || '';
        document.getElementById('transitionApprovedAmountGroup')?.classList.toggle('d-none', target !== 'Approved');
        document.getElementById('transitionSettleNote')?.classList.toggle('d-none', target !== 'Settled');
    };

    // approvedAmount only for Approved; optional texts omitted when empty (ASSUMPTION A9); occurredAt exact text.
    const buildTransitionBody = () => {
        const target = document.getElementById('transitionTarget')?.value || '';
        const body = { targetStatus: target, occurredAt: document.getElementById('transitionOccurredAt')?.value ?? '' };
        const resolution = document.getElementById('transitionResolutionCode')?.value ?? '';
        if (resolution !== '') body.resolutionCode = resolution;
        if (target === 'Approved') body.approvedAmount = document.getElementById('transitionApprovedAmount')?.value ?? '';
        const note = document.getElementById('transitionNote')?.value ?? '';
        if (note !== '') body.note = note;
        return { target, bodyText: JSON.stringify(body) };
    };

    const closeTransition = () => {
        offcanvas('offcanvasClaimTransition')?.hide();
        transitionContext = null;
    };

    const requestTransition = () => {
        if (!transitionContext) return;
        if (transitionPending) { window.showToast?.(t('RequestPending'), 'warning'); return; }
        const { target } = buildTransitionBody();
        const subtext = target === 'Settled' ? `${t('ConfirmTransition')} ${t('SettledNoPaymentNote')}` : t('ConfirmTransition');
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
        hideAlert('formClaimTransitionAlert');
        const { target, bodyText } = buildTransitionBody();
        if (!transitionIntent || transitionIntent.claimId !== context.claimId || transitionIntent.bodyText !== bodyText) {
            transitionIntent = { claimId: context.claimId, key: uuid(), bodyText, blocked: false };
        } else if (transitionIntent.blocked) {
            showAlert('formClaimTransitionAlert', transitionIntent.blockedFailure);
            return;
        }
        const intent = transitionIntent;
        const submit = document.getElementById('btnSubmitTransition');
        transitionPending = true;
        if (submit) submit.disabled = true;
        const url = `${endpoint}/${encodeURIComponent(context.claimId)}/transition?shipmentId=${encodeURIComponent(context.shipmentId)}`;
        try {
            const response = await fetch(url, {
                method: 'POST', credentials: 'same-origin', headers: mutationHeaders(intent.key), body: intent.bodyText
            });
            if (response.ok) {
                let result = null;
                try { result = await response.json(); } catch (_) { result = null; }
                transitionIntent = null;
                closeTransition();
                let message = t(result?.idempotentReplay === true ? 'TransitionReplayed' : 'TransitionCompleted');
                const approved = target === 'Approved' ? safeDecimal(result?.approvedAmount) : '';
                if (approved) message = `${message} ${t('ApprovedAmountResult')}: ${approved}`;
                window.showToast?.(message, 'success');
                reloadList(); // a replay snapshot is never shown as current state
                return;
            }
            const failure = await readFailure(response);
            handleTransitionFailure(failure, intent);
        } catch (error) {
            console.error('[Claims] Transition request failed.', error);
            showAlert('formClaimTransitionAlert', networkFailure('CLAIM_STORAGE_UNAVAILABLE'));
        } finally {
            transitionPending = false;
            if (submit) submit.disabled = false;
        }
    };

    const handleTransitionFailure = (failure, intent) => {
        switch (failure.status) {
            case 401:
                handleUnauthorized();
                return;
            case 403:
            case 404:
                // Close; hidden surface inert; one localized text + support reference; reload.
                closeTransition();
                toastFailure(failure);
                reloadList();
                return;
            case 409:
                intent.blocked = true;
                intent.blockedFailure = failure;
                showAlert('formClaimTransitionAlert', failure);
                return;
            case 422:
                if (failure.code === 'INVALID_CLAIM_TRANSITION') {
                    closeTransition();
                    toastFailure(failure);
                    reloadList();
                    return;
                }
                showAlert('formClaimTransitionAlert', failure);
                return;
            default:
                showAlert('formClaimTransitionAlert', failure);
        }
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
            if (!error?.authHandled) console.error('[Claims SaveView] Failed to load saved views.', error);
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
            console.error('[Claims SaveView] Failed to save the view.', error);
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
        const buttons = window.DtDefaults.exportButtons(permissions.canCreate ? t('AddNewClaims') : '', {}, extraButtons,
            { exportColumns: [], colvisColumns })
            .filter((feature) => !feature?.buttons?.some?.((button) => String(button?.className || '').includes('dt-export-collection-btn')));

        dt = new DataTable(tableEl, window.DtDefaults.create({
            serverSide: false,
            stateSave: false,
            processing: false,
            ajax: loadClaims,
            order: [[1, 'desc']],
            colReorder: { columns: ':gt(0):not(:last-child)' },
            responsive: { details: { type: 'column', target: 0 } },
            columns: [
                { data: null, defaultContent: '', className: 'control', orderable: false, searchable: false },
                { data: 'claimNumber', render: (value, type) => (type === 'display' ? (isText(value) && value ? ltr(value) : notProvided()) : (isText(value) ? value : '')) },
                { data: 'shipmentId', render: renderCopyable },
                { data: 'status', render: renderStatus },
                {
                    data: 'claimedAmount', className: 'text-end',
                    render: (value, type) => {
                        if (type === 'sort' || type === 'type') return amountSortKey(value);
                        if (type !== 'display') return isText(value) ? value : '';
                        return isText(value) ? ltr(value) : notProvided(); // exact wire text, no grouping/rounding
                    }
                },
                { data: 'currency', render: (value, type) => (type === 'display' ? (isText(value) ? ltr(value) : notProvided()) : (isText(value) ? value : '')) },
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
                setTimeout(() => { saveViewArmed = true; }, 0);
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
        document.getElementById('btnClaimsRetry')?.addEventListener('click', () => {
            if (!dt) { void initDataTable(); return; }
            setListState(loadedOnce ? 'table' : 'skeleton');
            reloadList();
        });

        tableEl?.addEventListener('click', (event) => {
            const quick = event.target.closest('.js-quick-view');
            if (quick) { event.preventDefault(); openQuickView(quick.dataset.claimId); return; }
            const transition = event.target.closest('.js-transition');
            if (transition) { event.preventDefault(); openTransition(transition.dataset.claimId, transition.dataset.targetStatus); }
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
                console.error('[Claims] Copy failed.', error);
                window.showToast?.(t('ErrorOccurred'), 'error');
            }
        });

        document.getElementById('btnResolveShipment')?.addEventListener('click', () => { void resolveShipment(); });
        document.getElementById('claimShipmentId')?.addEventListener('input', () => {
            if (resolvedShipment || !document.getElementById('claimShipmentResolved')?.classList.contains('d-none')) clearResolvedShipment();
            else resolveSequence += 1;
        });
        document.getElementById('btnAddEvidence')?.addEventListener('click', addEvidenceRow);
        evidenceList()?.addEventListener('click', (event) => {
            const remove = event.target.closest('.js-remove-evidence');
            if (!remove) return;
            event.preventDefault();
            remove.closest('.claim-evidence-row')?.remove();
            document.getElementById('btnAddEvidence')?.focus();
        });
        document.getElementById('btnSaveClaim')?.addEventListener('click', () => { void submitCreate(); });

        document.getElementById('transitionTarget')?.addEventListener('change', syncTransitionTarget);
        document.getElementById('btnSubmitTransition')?.addEventListener('click', requestTransition);

        // Hidden surfaces are inert and not keyboard-reachable; late responses never re-open them (CU-15).
        ['offcanvasCreateEdit', 'offcanvasDetailsPreview', 'offcanvasClaimTransition'].forEach((id) => {
            const el = document.getElementById(id);
            if (!el) return;
            el.inert = true;
            el.addEventListener('show.bs.offcanvas', () => { el.inert = false; });
            el.addEventListener('hidden.bs.offcanvas', () => {
                el.inert = true;
                if (id === 'offcanvasClaimTransition') transitionContext = null;
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

document.addEventListener('DOMContentLoaded', () => ClaimsList.init());
