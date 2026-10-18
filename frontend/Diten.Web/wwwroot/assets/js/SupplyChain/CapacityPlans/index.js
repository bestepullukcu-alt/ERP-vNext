'use strict';

/**
 * MOD-0192 Capacity — entry page (UI-SCOPE-0192 §3, option A; G6). Create plan (offcanvas) + Open plan by ID. No list
 * of any kind: the contract has none (F192-LIST, O-01).
 *
 * Every request goes to the same-origin adapter /SupplyChain/CapacityPlans/api*; the browser never calls the Gateway,
 * never holds a token and never sends scope headers — the company rides ?legalEntityId= and the adapter turns it into
 * the scope header. Values are sent exactly as typed (no trim). One Idempotency-Key per opened create form (Q403,
 * MODULE-RECIPE 3.1): kept across edits, failures and retries until the form closes.
 */
(function () {
    const endpoint = '/SupplyChain/CapacityPlans';
    const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
    // CapacityContractError.cs + the adapter's own codes, localized (UI-SCOPE-0192 §5).
    const ERROR_KEY = Object.freeze({
        INVALID_REQUEST: 'ErrInvalidRequest',
        INVALID_CORRELATION_ID: 'ErrInvalidCorrelationId',
        UNAUTHENTICATED: 'ErrUnauthenticated',
        FORBIDDEN: 'ErrForbidden',
        UNKNOWN_CAPACITY_PLAN: 'ErrCapacityPlanNotFound',
        UNKNOWN_CAPACITY_SCENARIO: 'ErrCapacityScenarioNotFound',
        UNKNOWN_CAPACITY_EVALUATION: 'ErrCapacityEvaluationNotFound',
        CAPACITY_PLAN_ALREADY_EXISTS: 'ErrCapacityPlanAlreadyExists',
        CAPACITY_PLAN_STATE_CONFLICT: 'ErrCapacityPlanStateConflict',
        CAPACITY_SCENARIO_NAME_CONFLICT: 'ErrCapacityScenarioNameConflict',
        EVALUATION_ALREADY_ACTIVE: 'ErrEvaluationAlreadyActive',
        IDEMPOTENCY_KEY_REUSED: 'ErrIdempotencyKeyReused',
        INVALID_DEMAND_REFERENCE: 'ErrInvalidDemandReference',
        INVALID_CONSTRAINT_REFERENCE: 'ErrInvalidConstraintReference',
        DEPENDENCY_UNAVAILABLE: 'ErrDependencyUnavailable',
        COMMIT_RESULT_UNRESOLVED: 'ErrCommitResultUnresolved',
        INTERNAL_ERROR: 'ErrInternalError'
    });
    const STATUS_FALLBACK_CODE = Object.freeze({
        400: 'INVALID_REQUEST', 401: 'UNAUTHENTICATED', 403: 'FORBIDDEN', 404: 'UNKNOWN_CAPACITY_PLAN', 503: 'DEPENDENCY_UNAVAILABLE'
    });

    let L = window.L10n || {};
    const t = (key) => L[key] || '';
    const uuid = () => crypto.randomUUID();
    const isText = (value) => typeof value === 'string';
    const permissions = (() => {
        try {
            const parsed = JSON.parse(document.getElementById('capacity-plans-permissions')?.textContent || '{}');
            return { canCreate: parsed.canCreate === true };
        } catch (_) { return { canCreate: false }; }
    })();
    const antiForgeryToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const traceHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest', 'X-Correlation-Id': uuid() });
    const mutationHeaders = (key) => Object.assign(traceHeaders(), {
        'Content-Type': 'application/json', 'Idempotency-Key': key, 'RequestVerificationToken': antiForgeryToken()
    });
    const withScope = (url, scope) => `${url}${url.includes('?') ? '&' : '?'}legalEntityId=${encodeURIComponent(scope)}`;
    const handleUnauthorized = () => { window.DtDefaults?.handleUnauthorized?.(); };
    const value = (id) => String(document.getElementById(id)?.value ?? '');

    // ─── Failure envelope → localized message + support reference ────────────
    const readFailure = async (response) => {
        let body = null;
        try { body = await response.json(); } catch (_) { body = null; }
        const envelopeCode = isText(body?.error?.code) ? body.error.code : '';
        const code = ERROR_KEY[envelopeCode] ? envelopeCode
            : (STATUS_FALLBACK_CODE[response.status] || (response.status >= 500 ? 'INTERNAL_ERROR' : 'INVALID_REQUEST'));
        const reference = isText(body?.error?.correlationId) ? body.error.correlationId : (response.headers.get('X-Correlation-Id') || '');
        return { status: response.status, code, reference: UUID_PATTERN.test(reference) ? reference : '' };
    };
    const failureText = (failure) => t(ERROR_KEY[failure.code] || 'ErrInternalError');
    // An unknown outcome: the same intent may be retried with the same key (pack §13).
    const outcomeOpen = (failure) => failure.status === 0 || failure.status >= 500 || failure.code === 'COMMIT_RESULT_UNRESOLVED';

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
            const ref = document.createElement('bdi');
            ref.dir = 'ltr';
            ref.className = 'font-monospace';
            ref.textContent = failureOrText.reference;
            reference.append(label, ref);
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
    const setInvalid = (id, message) => {
        const input = document.getElementById(id);
        const feedback = document.querySelector(`.invalid-feedback[data-for="${id}"]`);
        input?.classList.toggle('is-invalid', Boolean(message));
        input?.setAttribute('aria-invalid', message ? 'true' : 'false');
        if (feedback) feedback.textContent = message || '';
    };

    // ─── Company scope ───────────────────────────────────────────────────────
    let legalEntities = [];
    const fillLegalEntitySelect = (element, selected) => {
        if (!element) return;
        element.replaceChildren();
        legalEntities.forEach((item) => {
            const option = document.createElement('option');
            option.value = item.id;
            option.textContent = item.name;
            option.selected = item.id === selected;
            element.appendChild(option);
        });
    };
    const loadLegalEntities = async () => {
        try {
            const response = await fetch('/SupplyChain/api/legal-entities', { credentials: 'same-origin', headers: traceHeaders() });
            if (response.status === 401) { handleUnauthorized(); return false; }
            if (!response.ok) { showAlert('entryAlert', t('LegalEntityUnavailable')); return false; }
            let payload = null;
            try { payload = await response.json(); } catch (_) { payload = null; }
            const rows = Array.isArray(payload) ? payload : (payload?.data ?? payload?.items ?? []);
            legalEntities = rows.map((row) => ({
                id: String(row.legalEntityId ?? row.LegalEntityId ?? ''),
                name: String(row.displayName ?? row.DisplayName ?? row.legalName ?? row.LegalName ?? row.code ?? '')
            })).filter((row) => UUID_PATTERN.test(row.id));
            // An empty answer is not an outage: the tenant genuinely has none.
            if (legalEntities.length === 0) { showAlert('entryAlert', t('LegalEntityNone')); return false; }
            fillLegalEntitySelect(document.getElementById('scopeLegalEntity'), legalEntities[0].id);
            return true;
        } catch (_) {
            showAlert('entryAlert', t('LegalEntityUnavailable'));
            return false;
        }
    };
    const scope = () => value('scopeLegalEntity');
    const workspaceUrl = (planId, legalEntityId) =>
        `${endpoint}/Details/${encodeURIComponent(planId)}?legalEntityId=${encodeURIComponent(legalEntityId)}`;

    // ─── Open plan by ID ─────────────────────────────────────────────────────
    // The workspace itself answers not-found safely (no leak for a foreign plan); this only refuses a non-UUID.
    const openPlan = (event) => {
        event.preventDefault();
        const planId = value('openPlanId');
        if (!UUID_PATTERN.test(planId)) { setInvalid('openPlanId', t('PlanIdHelp')); return; }
        if (!UUID_PATTERN.test(scope())) { showAlert('entryAlert', t('LegalEntityRequired')); return; }
        setInvalid('openPlanId', '');
        window.location.assign(workspaceUrl(planId, scope()));
    };

    // ─── Create plan ─────────────────────────────────────────────────────────
    const CREATE_FIELDS = ['planName', 'planHorizonStart', 'planHorizonEnd', 'planDemandPlanId', 'planDemandPlanVersion',
        'planSourceCapturedAt', 'planSourceChecksum'];
    let createIntent = null;   // { key, blocked } — minted when the form opens, dropped when it closes
    let createPending = false;

    const resetCreateForm = () => {
        CREATE_FIELDS.forEach((id) => { const el = document.getElementById(id); if (el) el.value = ''; setInvalid(id, ''); });
        hideAlert('formPlanAlert');
    };
    const openCreate = () => {
        resetCreateForm();
        fillLegalEntitySelect(document.getElementById('formLegalEntity'), scope());
        createIntent = { key: uuid(), blocked: false };
        document.getElementById('btnSavePlan').disabled = false;
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
    };
    // Mirrors the contract only: all seven required, horizonEnd ≥ horizonStart (CreateCapacityPlanRequest). No trim.
    const validateCreate = () => {
        let valid = true;
        CREATE_FIELDS.forEach((id) => {
            const missing = value(id) === '';
            setInvalid(id, missing ? t('FieldRequired') : '');
            if (missing) valid = false;
        });
        const start = value('planHorizonStart');
        const end = value('planHorizonEnd');
        if (start && end && end < start) { setInvalid('planHorizonEnd', t('ValidationHorizonOrder')); valid = false; }
        return valid;
    };
    const submitCreate = async () => {
        if (!createIntent || createPending) { if (createPending) window.showToast?.(t('RequestPending'), 'warning'); return; }
        if (createIntent.blocked) { showAlert('formPlanAlert', t('ErrIdempotencyKeyReused')); return; }
        hideAlert('formPlanAlert');
        const legalEntityId = value('formLegalEntity');
        if (!UUID_PATTERN.test(legalEntityId)) { showAlert('formPlanAlert', t('LegalEntityRequired')); return; }
        if (!validateCreate()) return;
        const body = JSON.stringify({
            name: value('planName'),
            horizonStart: value('planHorizonStart'),
            horizonEnd: value('planHorizonEnd'),
            demandPlanId: value('planDemandPlanId'),
            demandPlanVersion: value('planDemandPlanVersion'),
            sourceCapturedAt: value('planSourceCapturedAt'),
            sourceChecksum: value('planSourceChecksum')
        });
        const save = document.getElementById('btnSavePlan');
        createPending = true;
        save.disabled = true;
        try {
            const response = await fetch(withScope(`${endpoint}/api`, legalEntityId), {
                method: 'POST', credentials: 'same-origin', headers: mutationHeaders(createIntent.key), body
            });
            if (response.status === 401) { handleUnauthorized(); return; }
            if (response.status === 201 || response.status === 200) {
                let created = null;
                try { created = await response.json(); } catch (_) { created = null; }
                const planId = isText(created?.capacityPlanId) ? created.capacityPlanId : '';
                window.showToast?.(t('PlanCreated'), 'success');
                if (UUID_PATTERN.test(planId)) { window.location.assign(workspaceUrl(planId, legalEntityId)); return; }
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                return;
            }
            const failure = await readFailure(response);
            if (failure.code === 'IDEMPOTENCY_KEY_REUSED') createIntent.blocked = true;
            showAlert('formPlanAlert', failure);
        } catch (_) {
            showAlert('formPlanAlert', { status: 0, code: 'DEPENDENCY_UNAVAILABLE', reference: '' });
        } finally {
            createPending = false;
            save.disabled = Boolean(createIntent?.blocked);
        }
    };

    // ─── Init ────────────────────────────────────────────────────────────────
    const reveal = () => {
        const skeleton = document.getElementById('entrySkeleton');
        if (skeleton) skeleton.style.display = 'none';
        const surface = document.getElementById('entrySurface');
        if (surface) { surface.hidden = false; surface.removeAttribute('inert'); surface.removeAttribute('aria-hidden'); }
    };
    const init = async () => {
        L = window.L10n || L;
        document.getElementById('formOpenPlan')?.addEventListener('submit', openPlan);
        const ready = await loadLegalEntities();
        reveal();
        if (!ready) { document.getElementById('btnOpenPlan')?.setAttribute('disabled', ''); return; }
        if (permissions.canCreate) {
            const create = document.getElementById('btnCreatePlan');
            if (create) { create.disabled = false; create.addEventListener('click', openCreate); }
            document.getElementById('btnSavePlan')?.addEventListener('click', () => { void submitCreate(); });
            document.getElementById('offcanvasCreateEdit')?.addEventListener('hidden.bs.offcanvas', () => {
                if (!createPending) createIntent = null;
                create?.focus({ preventScroll: true });
            });
        }
    };

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => { void init(); });
    else void init();
})();
