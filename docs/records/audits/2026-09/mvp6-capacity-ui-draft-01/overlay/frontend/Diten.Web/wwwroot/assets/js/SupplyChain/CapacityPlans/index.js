'use strict';

/**
 * MOD-0192 Capacity — entry page (pack §23.4, §23.6, §23.8; golden-reference slim). DRAFT overlay — not built, not
 * runtime-verified, not writer-complete.
 *
 * This page issues NO list request: the published contract has no plan list operation (F192-LIST).
 * "Open plan by ID" validates the UUID shape (non-nil) client-side and only navigates; a malformed value sends nothing.
 * "Create plan" posts createCapacityPlan through the same-origin adapter /SupplyChain/CapacityPlans/api; the browser never
 * calls the Gateway, never holds a token and never sends tenant/legal-entity scope.
 *
 * Payload rule (pack §23.6/§23.8): values are sent exactly as typed — no trim. One pending request per intent; a retry
 * after a network failure or 503 re-sends the same Idempotency-Key with identical body text; an edited payload is a new
 * intent with a new key. A replayed 201 is indistinguishable from the first 201 and is shown as completed (README A7).
 */
const CapacityPlansEntry = (function () {
    const endpoint = '/SupplyChain/CapacityPlans/api';
    const detailsBase = '/SupplyChain/CapacityPlans/Details/';
    const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
    const NIL_UUID = '00000000-0000-0000-0000-000000000000';
    const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
    // CreateCapacityPlanRequest mandatory fields, in schema order (pack §23.6; form_field_count 7).
    const CREATE_FIELDS = Object.freeze([
        ['name', 'planName'],
        ['horizonStart', 'planHorizonStart'],
        ['horizonEnd', 'planHorizonEnd'],
        ['demandPlanId', 'planDemandPlanId'],
        ['demandPlanVersion', 'planDemandPlanVersion'],
        ['sourceCapturedAt', 'planSourceCapturedAt'],
        ['sourceChecksum', 'planSourceChecksum']
    ]);
    // All 16 published Capacity codes (pack §23.8), localized — never raw text on screen.
    const ERROR_KEY = Object.freeze({
        INVALID_REQUEST: 'ErrInvalidRequest',
        INVALID_CORRELATION_ID: 'ErrInvalidCorrelationId',
        UNAUTHENTICATED: 'ErrUnauthenticated',
        FORBIDDEN: 'ErrForbidden',
        UNKNOWN_CAPACITY_PLAN: 'ErrUnknownCapacityPlan',
        UNKNOWN_CAPACITY_SCENARIO: 'ErrUnknownCapacityScenario',
        UNKNOWN_CAPACITY_EVALUATION: 'ErrUnknownCapacityEvaluation',
        CAPACITY_PLAN_ALREADY_EXISTS: 'ErrCapacityPlanAlreadyExists',
        CAPACITY_SCENARIO_NAME_CONFLICT: 'ErrCapacityScenarioNameConflict',
        CAPACITY_PLAN_STATE_CONFLICT: 'ErrCapacityPlanStateConflict',
        EVALUATION_ALREADY_ACTIVE: 'ErrEvaluationAlreadyActive',
        IDEMPOTENCY_KEY_REUSED: 'ErrIdempotencyKeyReused',
        INVALID_DEMAND_REFERENCE: 'ErrInvalidDemandReference',
        INVALID_CONSTRAINT_REFERENCE: 'ErrInvalidConstraintReference',
        DEPENDENCY_UNAVAILABLE: 'ErrDependencyUnavailable',
        COMMIT_RESULT_UNRESOLVED: 'ErrCommitResultUnresolved'
    });
    // Status → published code when an envelope carries no known code (create is a mutation: 5xx → unresolved).
    const STATUS_FALLBACK_CODE = Object.freeze({
        400: 'INVALID_REQUEST', 401: 'UNAUTHENTICATED', 403: 'FORBIDDEN', 404: 'UNKNOWN_CAPACITY_PLAN',
        409: 'INVALID_REQUEST', 422: 'INVALID_REQUEST', 503: 'COMMIT_RESULT_UNRESOLVED'
    });

    let L = window.L10n || {};
    const permissions = readPermissions();
    let createIntent = null;   // { key, bodyText, blocked, blockedFailure }
    let createPending = false;

    // ─── Basics ──────────────────────────────────────────────────────────────
    function readPermissions() {
        const island = document.getElementById('capacity-plans-permissions');
        if (!island) return { canCreate: false };
        try {
            return { canCreate: JSON.parse(island.textContent || '{}').canCreate === true };
        } catch (error) {
            console.error('[Capacity] Permission payload could not be parsed.', error);
            return { canCreate: false };
        }
    }

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };
    const t = (key) => L[key] || '';
    const uuid = () => crypto.randomUUID();
    const isText = (value) => typeof value === 'string';
    const valueOf = (id) => document.getElementById(id)?.value ?? '';
    const antiForgeryToken = () =>
        document.querySelector('#formCreatePlan input[name="__RequestVerificationToken"]')?.value || '';
    const mutationHeaders = (key) => ({
        'X-Requested-With': 'XMLHttpRequest',
        'X-Correlation-Id': uuid(),
        'Content-Type': 'application/json',
        'Idempotency-Key': key,
        'RequestVerificationToken': antiForgeryToken()
    });
    const offcanvas = (id) => {
        const el = document.getElementById(id);
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    // ─── Failure envelope → localized message + support reference ────────────
    const readFailure = async (response) => {
        let body = null;
        try { body = await response.json(); } catch (_) { body = null; }
        const envelopeCode = isText(body?.error?.code) ? body.error.code : '';
        const code = ERROR_KEY[envelopeCode] ? envelopeCode
            : (STATUS_FALLBACK_CODE[response.status] || (response.status >= 500 ? 'COMMIT_RESULT_UNRESOLVED' : 'INVALID_REQUEST'));
        const reference = isText(body?.error?.correlationId) ? body.error.correlationId
            : (response.headers.get('X-Correlation-Id') || '');
        return { status: response.status, code, reference };
    };
    const failureText = (failure) => t(ERROR_KEY[failure.code] || 'ErrInvalidRequest');
    const safeReference = (value) => (isText(value) && UUID_PATTERN.test(value) ? value : '');

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
    const toastFailure = (failure) => {
        const reference = safeReference(failure.reference);
        window.showToast?.(`${failureText(failure)}${reference ? ` ${t('SupportReference')}: ${reference}` : ''}`,
            failure.status === 0 || failure.status >= 500 ? 'error' : 'warning');
    };
    const handleUnauthorized = () => { window.DtDefaults?.handleUnauthorized?.(); };

    // ─── Open plan by ID (navigation only; SU-02) ────────────────────────────
    const openPlan = (event) => {
        event?.preventDefault();
        const input = document.getElementById('openPlanId');
        const error = document.getElementById('openPlanIdError');
        const value = input?.value ?? '';
        if (!UUID_PATTERN.test(value) || value === NIL_UUID) {
            // Malformed or nil UUID: no request and no navigation (the backend rejects a nil route ID).
            error?.classList.remove('d-none');
            input?.setAttribute('aria-invalid', 'true');
            input?.focus();
            return;
        }
        error?.classList.add('d-none');
        input?.removeAttribute('aria-invalid');
        window.location.assign(detailsBase + encodeURIComponent(value));
    };

    // ─── Create plan ─────────────────────────────────────────────────────────
    const updateProgress = () => {
        const progress = document.getElementById('createRequiredProgress');
        if (!progress) return;
        const filled = CREATE_FIELDS.filter(([, id]) => valueOf(id) !== '').length;
        progress.textContent = `${t('RequiredFilled')}: ${filled}/${CREATE_FIELDS.length}`;
    };

    const buildCreateBody = () => {
        const body = {};
        CREATE_FIELDS.forEach(([name, id]) => { body[name] = valueOf(id); });
        return JSON.stringify(body);
    };

    // Mirrors the published schema only: mandatory (present and non-empty) and horizonEnd ≥ horizonStart. No trim,
    // no length limit and no date-time reformatting; the server stays authoritative
    // (CreateCapacityPlanValidator.cs:7-13 in BC-SOURCE).
    const clientCheck = () => {
        if (CREATE_FIELDS.some(([, id]) => valueOf(id) === '')) return t('RequiredMissing');
        const start = valueOf('planHorizonStart');
        const end = valueOf('planHorizonEnd');
        if (DATE_PATTERN.test(start) && DATE_PATTERN.test(end) && end < start) return t('HorizonOrderInvalid');
        return '';
    };

    const submitCreate = async () => {
        if (createPending) { window.showToast?.(t('RequestPending'), 'warning'); return; }
        const problem = clientCheck();
        if (problem) { showAlert('formCreatePlanAlert', problem); return; }
        hideAlert('formCreatePlanAlert');
        const bodyText = buildCreateBody();
        if (!createIntent || createIntent.bodyText !== bodyText) {
            createIntent = { key: uuid(), bodyText, blocked: false, blockedFailure: null };
        } else if (createIntent.blocked) {
            showAlert('formCreatePlanAlert', createIntent.blockedFailure);
            return;
        }
        const intent = createIntent;
        const submit = document.getElementById('btnSubmitCreatePlan');
        createPending = true;
        if (submit) submit.disabled = true;
        try {
            const response = await fetch(endpoint, {
                method: 'POST', credentials: 'same-origin', headers: mutationHeaders(intent.key), body: intent.bodyText
            });
            if (response.status === 201) {
                let created = null;
                try { created = await response.json(); } catch (_) { created = null; }
                const id = isText(created?.capacityPlanId) && UUID_PATTERN.test(created.capacityPlanId) ? created.capacityPlanId : '';
                if (!id) {
                    // Malformed success envelope: never shown as done; the explicit retry replays the same key.
                    showAlert('formCreatePlanAlert', { status: 503, code: 'COMMIT_RESULT_UNRESOLVED', reference: response.headers.get('X-Correlation-Id') || '' });
                    return;
                }
                createIntent = null;
                window.showToast?.(t('PlanCreated'), 'success');
                window.location.assign(detailsBase + encodeURIComponent(id));
                return;
            }
            handleCreateFailure(await readFailure(response), intent);
        } catch (error) {
            console.error('[Capacity] Create request failed.', error);
            // Unknown outcome: same key and identical body text on the explicit retry.
            showAlert('formCreatePlanAlert', { status: 0, code: 'COMMIT_RESULT_UNRESOLVED', reference: '' });
        } finally {
            createPending = false;
            if (submit) submit.disabled = false;
        }
    };

    const handleCreateFailure = (failure, intent) => {
        switch (failure.status) {
            case 401:
                handleUnauthorized();
                return;
            case 403:
                // Action denial: close, remove the CTA, localized denial (pack §23.8 FORBIDDEN).
                offcanvas('offcanvasCreatePlan')?.hide();
                document.getElementById('btnOpenCreatePlan')?.remove();
                toastFailure(failure);
                return;
            default:
                if (failure.code === 'IDEMPOTENCY_KEY_REUSED') {
                    // Stop retry: a new user intent (an edited payload) is required.
                    intent.blocked = true;
                    intent.blockedFailure = failure;
                }
                // 400/409/422: inputs kept. 503/network: same key + identical body on the explicit retry.
                showAlert('formCreatePlanAlert', failure);
        }
    };

    // ─── Events ──────────────────────────────────────────────────────────────
    const bindEvents = () => {
        document.getElementById('formOpenPlan')?.addEventListener('submit', openPlan);
        document.getElementById('openPlanId')?.addEventListener('input', () => {
            document.getElementById('openPlanIdError')?.classList.add('d-none');
            document.getElementById('openPlanId')?.removeAttribute('aria-invalid');
        });

        if (permissions.canCreate) {
            document.getElementById('btnOpenCreatePlan')?.addEventListener('click', () => {
                hideAlert('formCreatePlanAlert');
                updateProgress();
                offcanvas('offcanvasCreatePlan')?.show();
            });
            document.querySelectorAll('#formCreatePlan .js-create-required').forEach((input) => {
                input.addEventListener('input', updateProgress);
                input.addEventListener('change', updateProgress);
            });
            document.getElementById('btnSubmitCreatePlan')?.addEventListener('click', () => { void submitCreate(); });
            document.getElementById('formCreatePlan')?.addEventListener('submit', (event) => { event.preventDefault(); void submitCreate(); });
        }

        document.addEventListener('click', async (event) => {
            const copy = event.target.closest('.js-copy-value');
            if (!copy) return;
            event.preventDefault();
            const value = copy.dataset.copyValue || '';
            if (!value) return;
            try {
                await navigator.clipboard.writeText(value);
                window.showToast?.(t('CopyDone'), 'success');
            } catch (error) {
                console.error('[Capacity] Copy failed.', error);
                window.showToast?.(t('ErrorOccurred'), 'error');
            }
        });

        // Hidden surfaces are inert and not keyboard-reachable (pack §23.9 keyboard/Escape).
        const panel = document.getElementById('offcanvasCreatePlan');
        if (panel) {
            panel.inert = true;
            panel.addEventListener('show.bs.offcanvas', () => { panel.inert = false; });
            panel.addEventListener('shown.bs.offcanvas', () => document.getElementById('planName')?.focus());
            panel.addEventListener('hidden.bs.offcanvas', () => {
                panel.inert = true;
                document.getElementById('btnOpenCreatePlan')?.focus();
            });
        }
    };

    return {
        init: function () {
            syncL10n();
            bindEvents();
            updateProgress();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => CapacityPlansEntry.init());
