'use strict';

/**
 * MOD-0192 Capacity — plan workspace (pack §23.4–§23.8; golden-reference slim, bounded DataTables v2 profile).
 * DRAFT overlay — not built, not runtime-verified, not writer-complete.
 *
 * Bound operations only (pack §23.3), each through the same-origin adapter /SupplyChain/CapacityPlans/api/{id}*:
 * getCapacityPlan, createCapacityScenario, getCapacityScenario, evaluateCapacityScenario, getCapacityEvaluation.
 * There is no list request of any kind (F192-LIST). The browser never calls the Gateway, never holds a token and never
 * sends tenant/legal-entity scope.
 *
 * The UI never changes a status; it shows what the server returns. After a 201/202 the workspace reads the created
 * resource back with one GET; a response body is never shown as the current state (README A7).
 *
 * Evaluation status changes only on a manual Refresh click: each click issues exactly one getCapacityEvaluation.
 * There is no timer, polling loop or automatic retry anywhere in this file (F192-POLL).
 *
 * Scenario and evaluation IDs live in memory for this page only; nothing is written to browser storage, the URL or a
 * cookie. A reload keeps the plan (route) and loses the rest — README FINDING F-Q79-05.
 *
 * 404 handling is per resource (pack §23.8): UNKNOWN_CAPACITY_PLAN removes the whole workspace; UNKNOWN_CAPACITY_SCENARIO
 * and UNKNOWN_CAPACITY_EVALUATION remove only that panel's content. Late responses of an older load are ignored.
 */
const CapacityPlanWorkspace = (function () {
    const host = document.getElementById('capacity-plan-details');
    const planId = host?.dataset.capacityPlanId || '';
    const base = `/SupplyChain/CapacityPlans/api/${encodeURIComponent(planId)}`;
    const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
    const NIL_UUID = '00000000-0000-0000-0000-000000000000';
    const DECIMAL_PATTERN = /^-?\d+(\.\d+)?$/; // contract Decimal string form (pack §23.6)

    // Published vocabulary (sandop-capacity.openapi.yaml), display only.
    const PLAN_STATUS_KEY = Object.freeze({
        Draft: 'PlanStatusDraft', Evaluating: 'PlanStatusEvaluating', Ready: 'PlanStatusReady',
        Approved: 'PlanStatusApproved', Archived: 'PlanStatusArchived'
    });
    const PLAN_STATUS_CSS = Object.freeze({ Draft: 'secondary', Evaluating: 'warning', Ready: 'info', Approved: 'success', Archived: 'dark' });
    const SCENARIO_STATUS_KEY = Object.freeze({
        Draft: 'ScenarioStatusDraft', Evaluating: 'ScenarioStatusEvaluating', Evaluated: 'ScenarioStatusEvaluated', Archived: 'ScenarioStatusArchived'
    });
    const SCENARIO_STATUS_CSS = Object.freeze({ Draft: 'secondary', Evaluating: 'warning', Evaluated: 'success', Archived: 'dark' });
    const EVALUATION_STATUS_KEY = Object.freeze({
        Accepted: 'EvaluationStatusAccepted', Running: 'EvaluationStatusRunning',
        Completed: 'EvaluationStatusCompleted', Failed: 'EvaluationStatusFailed'
    });
    const EVALUATION_STATUS_CSS = Object.freeze({ Accepted: 'info', Running: 'warning', Completed: 'success', Failed: 'danger' });
    const EVALUATION_MODES = Object.freeze(['Finite', 'Infinite']);
    const EVALUATION_MODE_KEY = Object.freeze({ Finite: 'EvaluationModeFinite', Infinite: 'EvaluationModeInfinite' });
    const ACTIVE_EVALUATION = new Set(['Accepted', 'Running']); // pack §23.7: Refresh shown, Evaluate withheld
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
    const STATUS_FALLBACK_CODE = Object.freeze({
        400: 'INVALID_REQUEST', 401: 'UNAUTHENTICATED', 403: 'FORBIDDEN', 409: 'INVALID_REQUEST', 422: 'INVALID_REQUEST'
    });

    let L = window.L10n || {};
    const permissions = readPermissions();
    let plan = null;
    let scenario = null;     // the scenario shown in the scenario panel
    let evaluation = null;   // the evaluation shown in the evaluation panel
    let closed = false;      // plan safe-not-found or page denial: nothing is rendered again
    let dtBottlenecks = null;
    // Load versions per panel: a late response of an older load is ignored.
    const generation = { plan: 0, scenario: 0, evaluation: 0 };
    // In-memory only (never browser storage): scenarioId → evaluationId submitted in this page session.
    const sessionEvaluations = new Map();
    // Scenarios whose evaluate intent got EVALUATION_ALREADY_ACTIVE: no second submit in this session (pack §23.8).
    const evaluationBlocked = new Set();
    let evaluationPendingFor = '';

    // Mutation intents: { key, target, bodyText, blocked, blockedFailure } — reused only for the same target and body.
    const intents = { scenario: null, evaluate: null };
    const pending = { scenario: false, evaluate: false };

    // ─── Basics ──────────────────────────────────────────────────────────────
    function readPermissions() {
        const fallback = { canCreateScenario: false, canEvaluate: false };
        const island = document.getElementById('capacity-plan-permissions');
        if (!island) return fallback;
        try {
            const parsed = JSON.parse(island.textContent || '{}');
            return { canCreateScenario: parsed.canCreateScenario === true, canEvaluate: parsed.canEvaluate === true };
        } catch (error) {
            console.error('[Capacity] Permission payload could not be parsed.', error);
            return fallback;
        }
    }

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };
    const t = (key) => L[key] || '';
    const uuid = () => crypto.randomUUID();
    const isText = (value) => typeof value === 'string';
    const isObject = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);
    const isId = (value) => isText(value) && UUID_PATTERN.test(value) && value !== NIL_UUID;
    const escapeHtml = (value) => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    const ltr = (value) => `<bdi dir="ltr">${escapeHtml(value)}</bdi>`;
    const notProvided = () => `<span class="text-muted">${escapeHtml(t('NotProvided'))}</span>`;
    const copyButton = (value) => `<button type="button" class="btn btn-sm btn-icon btn-text-secondary js-copy-value" `
        + `data-copy-value="${escapeHtml(value)}" aria-label="${escapeHtml(t('Copy'))}"><i class="bx bx-copy" aria-hidden="true"></i></button>`;
    const copyable = (value) => (isText(value) && value
        ? `<span class="d-inline-flex align-items-center gap-1"><bdi dir="ltr" class="font-monospace small text-break">${escapeHtml(value)}</bdi>${copyButton(value)}</span>`
        : notProvided());
    const badge = (keys, css, value) => (isText(value) && keys[value]
        ? `<span class="badge bg-label-${css[value]}">${escapeHtml(t(keys[value]))}</span>`
        : notProvided());
    const traceHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest', 'X-Correlation-Id': uuid() });
    const antiForgeryToken = (formId) =>
        document.querySelector(`#${formId} input[name="__RequestVerificationToken"]`)?.value || '';
    const mutationHeaders = (key, formId) => Object.assign(traceHeaders(), {
        'Content-Type': 'application/json',
        'Idempotency-Key': key,
        'RequestVerificationToken': antiForgeryToken(formId)
    });
    const offcanvas = (id) => {
        const el = document.getElementById(id);
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };
    const setHtml = (id, html) => { const el = document.getElementById(id); if (el) el.innerHTML = html; };

    // ─── Failure envelope → localized message + support reference ────────────
    // notFoundCode: the published code a code-less 404 means for this resource.
    const readFailure = async (response, mutation, notFoundCode) => {
        let body = null;
        try { body = await response.json(); } catch (_) { body = null; }
        const envelopeCode = isText(body?.error?.code) ? body.error.code : '';
        const unavailable = mutation ? 'COMMIT_RESULT_UNRESOLVED' : 'DEPENDENCY_UNAVAILABLE';
        const fallback = response.status === 404 ? notFoundCode : STATUS_FALLBACK_CODE[response.status];
        const code = ERROR_KEY[envelopeCode] ? envelopeCode
            : (fallback || (response.status >= 500 ? unavailable : 'INVALID_REQUEST'));
        const reference = isText(body?.error?.correlationId) ? body.error.correlationId
            : (response.headers.get('X-Correlation-Id') || '');
        return { status: response.status, code, reference };
    };
    const networkFailure = (mutation) => ({ status: 0, code: mutation ? 'COMMIT_RESULT_UNRESOLVED' : 'DEPENDENCY_UNAVAILABLE', reference: '' });
    const failureText = (failure) => t(ERROR_KEY[failure.code] || 'ErrInvalidRequest');
    const safeReference = (value) => (isText(value) && UUID_PATTERN.test(value) ? value : '');

    const renderMessage = (container, text, reference) => {
        container.replaceChildren();
        const message = document.createElement('div');
        message.textContent = text;
        container.appendChild(message);
        if (reference) {
            const row = document.createElement('div');
            row.className = 'small mt-1';
            const label = document.createElement('span');
            label.textContent = `${t('SupportReference')}: `;
            const value = document.createElement('bdi');
            value.dir = 'ltr';
            value.className = 'font-monospace';
            value.textContent = reference;
            row.append(label, value);
            row.insertAdjacentHTML('beforeend', copyButton(reference));
            container.appendChild(row);
        }
    };
    const showAlert = (alertId, failureOrText) => {
        const alert = document.getElementById(alertId);
        if (!alert) return;
        const isFailure = typeof failureOrText !== 'string';
        renderMessage(alert, isFailure ? failureText(failureOrText) : failureOrText, isFailure ? safeReference(failureOrText.reference) : '');
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

    // ─── Section states: empty / skeleton / content / error are distinct (VIEW-001 §3.1) ──
    const SECTION = Object.freeze({
        summary: { empty: '', skeleton: 'summary-skeleton', content: 'summary-content', error: 'summary-error', errorKey: 'SummaryErrorState' },
        scenario: { empty: 'scenario-empty', skeleton: 'scenario-skeleton', content: 'scenario-content', error: 'scenario-error', errorKey: 'ScenarioErrorState' },
        evaluation: { empty: 'evaluation-empty', skeleton: 'evaluation-skeleton', content: 'evaluation-content', error: 'evaluation-error', errorKey: 'EvaluationErrorState' }
    });
    const setSectionState = (name, state, failure, retryable = true) => {
        const ids = SECTION[name];
        if (ids.empty) document.getElementById(ids.empty)?.classList.toggle('d-none', state !== 'empty');
        document.getElementById(ids.skeleton)?.classList.toggle('d-none', state !== 'skeleton');
        document.getElementById(ids.content)?.classList.toggle('d-none', state !== 'content');
        const error = document.getElementById(ids.error);
        error?.classList.toggle('d-none', state !== 'error');
        if (state === 'error' && error) {
            const message = error.querySelector('.js-section-error-message');
            if (message) message.textContent = failure?.code ? failureText(failure) : t(ids.errorKey);
            error.querySelector('.js-retry')?.classList.toggle('d-none', !retryable);
            const referenceRow = error.querySelector('.js-section-error-reference');
            const reference = safeReference(failure?.reference);
            if (referenceRow) {
                referenceRow.replaceChildren();
                if (reference) {
                    const label = document.createElement('span');
                    label.textContent = `${t('SupportReference')}: `;
                    const value = document.createElement('bdi');
                    value.dir = 'ltr';
                    value.className = 'font-monospace';
                    value.textContent = reference;
                    referenceRow.append(label, value);
                    referenceRow.insertAdjacentHTML('beforeend', copyButton(reference));
                }
                referenceRow.classList.toggle('d-none', !reference);
            }
        }
    };

    // Plan safe-not-found (404 UNKNOWN_CAPACITY_PLAN) and page denial (403): remove the workspace; later responses are
    // ignored and nothing is rendered again.
    const closeWorkspace = (failure) => {
        closed = true;
        plan = null;
        scenario = null;
        evaluation = null;
        sessionEvaluations.clear();
        const workspace = document.getElementById('planWorkspace');
        if (workspace) {
            workspace.hidden = true;
            workspace.inert = true;
            workspace.setAttribute('aria-hidden', 'true');
        }
        dtBottlenecks?.clear().draw();
        ['offcanvasCreateScenario', 'offcanvasEvaluate'].forEach((id) => {
            const el = document.getElementById(id);
            if (el) window.bootstrap?.Offcanvas.getInstance(el)?.hide();
        });
        ['btnOpenCreateScenario', 'btnOpenEvaluate', 'btnRefreshEvaluation'].forEach((id) => document.getElementById(id)?.remove());
        const title = document.getElementById('planTitle');
        if (title) title.textContent = t('PlanDetailsTitle');
        showAlert('planAlert', failure);
        document.getElementById('planAlert')?.focus();
    };

    const getJson = async (url, notFoundCode) => {
        try {
            const response = await fetch(url, { credentials: 'same-origin', headers: traceHeaders() });
            if (!response.ok) return { failure: await readFailure(response, false, notFoundCode) };
            let body = null;
            try { body = await response.json(); } catch (_) { body = null; }
            return { body, reference: response.headers.get('X-Correlation-Id') || '' };
        } catch (error) {
            console.error('[Capacity] Read request failed.', error);
            return { failure: networkFailure(false) };
        }
    };

    // Returns true when the failure was handled at page level (session surface, plan safe-not-found, page denial).
    const pageLevelFailure = (failure) => {
        if (failure.status === 401) { handleUnauthorized(); return true; }
        if (failure.status === 403 || failure.code === 'UNKNOWN_CAPACITY_PLAN') { closeWorkspace(failure); return true; }
        return false;
    };

    // ─── Shape checks (a malformed envelope is an error state, never a partial render) ──
    const isPlan = (body) => isObject(body) && isId(body.capacityPlanId) && body.capacityPlanId.toLowerCase() === planId.toLowerCase()
        && isText(body.name) && isText(body.status) && isText(body.horizonStart) && isText(body.horizonEnd)
        && isObject(body.provenance);
    const isRowSet = (value, fields) => Array.isArray(value) && value.every((row) => isObject(row) && fields.every((f) => isText(row[f])));
    const isScenario = (body, scenarioId) => isObject(body) && isId(body.scenarioId)
        && body.scenarioId.toLowerCase() === scenarioId.toLowerCase() && isText(body.name) && isText(body.status)
        && isRowSet(body.constraintRefs, ['constraintId', 'source', 'sourceVersion'])
        && isRowSet(body.adjustments, ['resourceRef', 'period', 'availableCapacityDelta', 'uomId']);
    const isEvaluation = (body, evaluationId) => isObject(body) && isId(body.evaluationId)
        && body.evaluationId.toLowerCase() === evaluationId.toLowerCase() && isId(body.scenarioId) && isText(body.status)
        && isText(body.evaluationMode)
        && isRowSet(body.bottlenecks, ['resourceRef', 'period', 'requiredCapacity', 'availableCapacity', 'shortfall', 'uomId']);
    const malformed = (reference) => ({ status: 200, code: 'DEPENDENCY_UNAVAILABLE', reference });

    // ─── Plan ────────────────────────────────────────────────────────────────
    const loadPlan = async () => {
        if (closed) return;
        const gen = ++generation.plan;
        if (!plan) setSectionState('summary', 'skeleton');
        const result = await getJson(base, 'UNKNOWN_CAPACITY_PLAN');
        if (gen !== generation.plan || closed) return;
        if (result.failure) {
            if (!pageLevelFailure(result.failure)) setSectionState('summary', 'error', result.failure);
            syncActions();
            return;
        }
        if (!isPlan(result.body)) {
            setSectionState('summary', 'error', malformed(result.reference));
            syncActions();
            return;
        }
        plan = result.body;
        renderSummary();
        setSectionState('summary', 'content');
        syncActions();
    };

    const renderSummary = () => {
        if (!plan) return;
        const title = document.getElementById('planTitle');
        if (title) title.textContent = plan.name;
        setHtml('summaryPlanId', copyable(plan.capacityPlanId));
        const name = document.getElementById('summaryPlanName');
        if (name) name.textContent = plan.name;
        setHtml('summaryStatus', badge(PLAN_STATUS_KEY, PLAN_STATUS_CSS, plan.status));
        setHtml('summaryHorizon', `${ltr(plan.horizonStart)} – ${ltr(plan.horizonEnd)}`);
        const p = plan.provenance;
        setHtml('summaryProvenance', isText(p.demandPlanId) && isText(p.demandPlanVersion)
            ? `${ltr(p.demandPlanId)} / ${ltr(p.demandPlanVersion)}` : notProvided());
        setHtml('summarySourceChecksum', copyable(isText(p.sourceChecksum) ? p.sourceChecksum : ''));
        setHtml('summarySourceCapturedAt', isText(p.sourceCapturedAt) ? ltr(p.sourceCapturedAt) : notProvided());
        setHtml('summaryCreatedAt', isText(plan.createdAt) ? ltr(plan.createdAt) : notProvided());
    };

    // ─── Scenario ────────────────────────────────────────────────────────────
    // Removes the scenario panel content (scenario safe-not-found or a new scenario being opened).
    const clearScenario = () => {
        scenario = null;
        ['#scenarioConstraintRefs tbody', '#scenarioAdjustments tbody'].forEach((s) => document.querySelector(s)?.replaceChildren());
        ['scenarioIdValue', 'scenarioNameValue', 'scenarioStatusValue', 'scenarioCreatedAtValue'].forEach((id) => setHtml(id, ''));
    };

    const loadScenario = async (scenarioId) => {
        if (closed || !isId(scenarioId)) return;
        const gen = ++generation.scenario;
        if (!scenario || scenario.scenarioId.toLowerCase() !== scenarioId.toLowerCase()) {
            clearScenario();
            clearEvaluation();
            setSectionState('evaluation', 'empty');
        }
        setSectionState('scenario', 'skeleton');
        syncActions();
        const result = await getJson(`${base}/scenarios/${encodeURIComponent(scenarioId)}`, 'UNKNOWN_CAPACITY_SCENARIO');
        if (gen !== generation.scenario || closed) return;
        if (result.failure) {
            if (pageLevelFailure(result.failure)) return;
            if (result.failure.code === 'UNKNOWN_CAPACITY_SCENARIO') {
                // Safe-not-found for this panel only: content removed, no retry of the same unknown ID.
                clearScenario();
                sessionEvaluations.delete(scenarioId.toLowerCase());
                setSectionState('scenario', 'error', result.failure, false);
            } else {
                setSectionState('scenario', 'error', result.failure);
            }
            syncActions();
            return;
        }
        if (!isScenario(result.body, scenarioId)) {
            setSectionState('scenario', 'error', malformed(result.reference));
            syncActions();
            return;
        }
        scenario = result.body;
        renderScenario();
        setSectionState('scenario', 'content');
        syncActions();
        // The evaluation this page submitted for the scenario, if any, is opened once (one GET, not polling).
        const known = sessionEvaluations.get(scenario.scenarioId.toLowerCase());
        if (known && (!evaluation || evaluation.evaluationId.toLowerCase() !== known)) void loadEvaluation(known);
    };
    let lastScenarioId = '';

    const cellRow = (values, decimalIndexes) => {
        const tr = document.createElement('tr');
        values.forEach((value, index) => {
            const td = document.createElement('td');
            const bdi = document.createElement('bdi');
            bdi.dir = 'ltr';
            bdi.textContent = value; // wire text exactly as returned (decimals are strings; never parsed)
            if (decimalIndexes.includes(index)) td.className = 'text-end font-monospace';
            td.appendChild(bdi);
            tr.appendChild(td);
        });
        return tr;
    };
    const noneRow = (span) => {
        const tr = document.createElement('tr');
        const td = document.createElement('td');
        td.colSpan = span;
        td.className = 'text-muted';
        td.textContent = t('NoneListed');
        tr.appendChild(td);
        return tr;
    };

    const renderScenario = () => {
        if (!scenario) return;
        setHtml('scenarioIdValue', copyable(scenario.scenarioId));
        const name = document.getElementById('scenarioNameValue');
        if (name) name.textContent = scenario.name;
        setHtml('scenarioStatusValue', badge(SCENARIO_STATUS_KEY, SCENARIO_STATUS_CSS, scenario.status));
        setHtml('scenarioCreatedAtValue', isText(scenario.createdAt) ? ltr(scenario.createdAt) : notProvided());
        const refs = document.querySelector('#scenarioConstraintRefs tbody');
        if (refs) {
            refs.replaceChildren(...(scenario.constraintRefs.length
                ? scenario.constraintRefs.map((r) => cellRow([r.constraintId, r.source, r.sourceVersion], []))
                : [noneRow(3)]));
        }
        const adjustments = document.querySelector('#scenarioAdjustments tbody');
        if (adjustments) {
            adjustments.replaceChildren(...(scenario.adjustments.length
                ? scenario.adjustments.map((a) => cellRow([a.resourceRef, a.period, a.availableCapacityDelta, a.uomId], [2]))
                : [noneRow(4)]));
        }
    };

    const openScenarioById = (event) => {
        event?.preventDefault();
        const input = document.getElementById('openScenarioId');
        const error = document.getElementById('openScenarioIdError');
        const value = input?.value ?? '';
        if (!isId(value)) {
            // Malformed or nil UUID: no request.
            error?.classList.remove('d-none');
            input?.setAttribute('aria-invalid', 'true');
            input?.focus();
            return;
        }
        error?.classList.add('d-none');
        input?.removeAttribute('aria-invalid');
        lastScenarioId = value;
        void loadScenario(value);
    };

    // ─── Evaluation ──────────────────────────────────────────────────────────
    const clearEvaluation = () => {
        evaluation = null;
        ++generation.evaluation; // a late evaluation response of the previous scenario is ignored
        dtBottlenecks?.clear().draw();
        document.getElementById('bottlenecks-host')?.classList.add('d-none');
        const refresh = document.getElementById('btnRefreshEvaluation');
        if (refresh) refresh.disabled = false;
        ['evaluationIdValue', 'evaluationScenarioIdValue', 'evaluationModeValue', 'evaluationStatusValue',
            'evaluationSubmittedAtValue', 'evaluationCompletedAtValue'].forEach((id) => setHtml(id, ''));
    };
    let lastEvaluationId = '';

    // One call = exactly one getCapacityEvaluation. Called on the 202 read-back, on opening a scenario that has a
    // session evaluation, and on each Refresh/Retry click — never from a timer.
    const loadEvaluation = async (evaluationId) => {
        if (closed || !isId(evaluationId)) return;
        const gen = ++generation.evaluation;
        lastEvaluationId = evaluationId;
        const refresh = document.getElementById('btnRefreshEvaluation');
        if (refresh) refresh.disabled = true;
        if (!evaluation || evaluation.evaluationId.toLowerCase() !== evaluationId.toLowerCase()) setSectionState('evaluation', 'skeleton');
        const result = await getJson(`${base}/evaluations/${encodeURIComponent(evaluationId)}`, 'UNKNOWN_CAPACITY_EVALUATION');
        if (gen !== generation.evaluation || closed) return;
        if (refresh) refresh.disabled = false;
        if (result.failure) {
            if (pageLevelFailure(result.failure)) return;
            if (result.failure.code === 'UNKNOWN_CAPACITY_EVALUATION') {
                // Safe-not-found for this panel only.
                clearEvaluation();
                for (const [key, value] of sessionEvaluations) if (value === evaluationId.toLowerCase()) sessionEvaluations.delete(key);
                setSectionState('evaluation', 'error', result.failure, false);
            } else {
                setSectionState('evaluation', 'error', result.failure);
            }
            syncActions();
            return;
        }
        if (!isEvaluation(result.body, evaluationId)) {
            setSectionState('evaluation', 'error', malformed(result.reference));
            syncActions();
            return;
        }
        evaluation = result.body;
        renderEvaluation();
        setSectionState('evaluation', 'content');
        syncActions();
    };

    // Text cell for the bounded table: display escapes and isolates LTR; sort/search use the raw wire text.
    const textCell = (value, type) => {
        if (type !== 'display') return isText(value) ? value : '';
        return isText(value) && value !== '' ? ltr(value) : notProvided();
    };
    const decimalCell = (value, type) => {
        if (type !== 'display') return isText(value) ? value : '';
        return isText(value) && value !== '' ? `<bdi dir="ltr" class="font-monospace">${escapeHtml(value)}</bdi>` : notProvided();
    };
    const bottleneckColumns = () => [
        { data: null, defaultContent: '', className: 'control', orderable: false, searchable: false },
        { data: 'resourceRef', render: textCell },
        { data: 'period', render: textCell },
        { data: 'requiredCapacity', className: 'text-end', orderable: false, render: decimalCell },
        { data: 'availableCapacity', className: 'text-end', orderable: false, render: decimalCell },
        { data: 'shortfall', className: 'text-end', orderable: false, render: decimalCell },
        { data: 'uomId', render: textCell }
    ];

    // Bounded client-side profile (pack §23.5): no server paging/search, no filter, column visibility, saved view,
    // export or quick view; client search/sort/paging over the returned evaluation only. Decimal columns are not
    // sortable because their wire strings are never parsed into numbers (README A10).
    const createTable = () => {
        const el = document.querySelector('#dt-capacity-bottlenecks');
        if (!el || typeof DataTable === 'undefined' || !window.DtDefaults) return null;
        return new DataTable(el, window.DtDefaults.create({
            serverSide: false,
            stateSave: false,
            processing: false,
            data: [],
            order: [[1, 'asc']],
            responsive: { details: { type: 'column', target: 0 } },
            columns: bottleneckColumns(),
            buttons: [],
            language: { emptyTable: t('EmptyBottlenecks'), zeroRecords: t('EmptyBottlenecks') }
        }));
    };

    const renderEvaluation = () => {
        if (!evaluation) return;
        setHtml('evaluationIdValue', copyable(evaluation.evaluationId));
        setHtml('evaluationScenarioIdValue', copyable(evaluation.scenarioId));
        const modeKey = EVALUATION_MODE_KEY[evaluation.evaluationMode];
        setHtml('evaluationModeValue', modeKey ? escapeHtml(t(modeKey)) : notProvided());
        setHtml('evaluationStatusValue', badge(EVALUATION_STATUS_KEY, EVALUATION_STATUS_CSS, evaluation.status));
        setHtml('evaluationSubmittedAtValue', isText(evaluation.submittedAt) ? ltr(evaluation.submittedAt) : notProvided());
        setHtml('evaluationCompletedAtValue', isText(evaluation.completedAt) ? ltr(evaluation.completedAt) : notProvided());
        const completed = evaluation.status === 'Completed';
        document.getElementById('bottlenecks-host')?.classList.toggle('d-none', !completed);
        if (!dtBottlenecks) dtBottlenecks = createTable();
        dtBottlenecks?.clear().rows.add(completed ? evaluation.bottlenecks : []).draw();
    };

    // ─── State-gated actions (pack §23.7) ────────────────────────────────────
    const evaluationShownActive = () => evaluation !== null && scenario !== null
        && evaluation.scenarioId.toLowerCase() === scenario.scenarioId.toLowerCase() && ACTIVE_EVALUATION.has(evaluation.status);
    const canCreateScenarioNow = () => permissions.canCreateScenario && !closed && plan !== null;
    // An evaluation this page submitted for the scenario withholds Evaluate until it is shown as Completed or Failed.
    const sessionEvaluationOpen = () => {
        const known = scenario ? sessionEvaluations.get(scenario.scenarioId.toLowerCase()) : undefined;
        if (!known) return false;
        return !(evaluation !== null && evaluation.evaluationId.toLowerCase() === known && !ACTIVE_EVALUATION.has(evaluation.status));
    };
    const canEvaluateNow = () => permissions.canEvaluate && !closed && plan !== null && scenario !== null
        && !evaluationShownActive() && !sessionEvaluationOpen() && !evaluationBlocked.has(scenario.scenarioId.toLowerCase())
        && evaluationPendingFor !== scenario.scenarioId.toLowerCase();
    const canRefreshNow = () => !closed && evaluation !== null && ACTIVE_EVALUATION.has(evaluation.status);
    const syncActions = () => {
        document.getElementById('btnOpenCreateScenario')?.classList.toggle('d-none', !canCreateScenarioNow());
        document.getElementById('btnOpenEvaluate')?.classList.toggle('d-none', !canEvaluateNow());
        document.getElementById('btnRefreshEvaluation')?.classList.toggle('d-none', !canRefreshNow());
    };

    // ─── Mutations: per-intent Idempotency-Key (pack §23.8) ─────────────────
    // A retry after a network failure or 503 re-sends the same key with identical body text to the same target; an
    // edited payload or another target is a new intent with a new key. IDEMPOTENCY_KEY_REUSED blocks the intent.
    const postIntent = async (options) => {
        const { kind, url, formId, alertId, panelId, submitId, bodyText, okStatus, idField, notFoundCode, onAccepted, onFailure } = options;
        if (pending[kind]) { window.showToast?.(t('RequestPending'), 'warning'); return; }
        const current = intents[kind];
        if (!current || current.target !== url || current.bodyText !== bodyText) {
            intents[kind] = { key: uuid(), target: url, bodyText, blocked: false, blockedFailure: null };
        } else if (current.blocked) {
            showAlert(alertId, current.blockedFailure);
            return;
        }
        const intent = intents[kind];
        const submit = document.getElementById(submitId);
        pending[kind] = true;
        if (submit) submit.disabled = true;
        syncActions();
        try {
            const response = await fetch(url, {
                method: 'POST', credentials: 'same-origin', headers: mutationHeaders(intent.key, formId), body: intent.bodyText
            });
            if (closed) return;
            if (response.status === okStatus) {
                let accepted = null;
                try { accepted = await response.json(); } catch (_) { accepted = null; }
                const id = isObject(accepted) && isId(accepted[idField]) ? accepted[idField] : '';
                if (!id) {
                    // Malformed success envelope: never shown as done; the explicit retry replays the same key.
                    showAlert(alertId, { status: 503, code: 'COMMIT_RESULT_UNRESOLVED', reference: response.headers.get('X-Correlation-Id') || '' });
                    return;
                }
                intents[kind] = null;
                offcanvas(panelId)?.hide();
                onAccepted(id);
                return;
            }
            const failure = await readFailure(response, true, notFoundCode);
            if (closed) return;
            if (failure.status === 401) { handleUnauthorized(); return; }
            if (failure.code === 'UNKNOWN_CAPACITY_PLAN') { offcanvas(panelId)?.hide(); closeWorkspace(failure); return; }
            if (failure.status === 403) {
                // Action denial: close, remove the control, localized denial.
                offcanvas(panelId)?.hide();
                document.getElementById(kind === 'scenario' ? 'btnOpenCreateScenario' : 'btnOpenEvaluate')?.remove();
                toastFailure(failure);
                return;
            }
            if (onFailure(failure, intent)) return;
            if (failure.code === 'IDEMPOTENCY_KEY_REUSED') {
                // Stop retry: a new user intent (an edited payload) is required.
                intent.blocked = true;
                intent.blockedFailure = failure;
            }
            // 400/409/422: inputs kept. 503/network: the same key and identical body on the explicit retry.
            showAlert(alertId, failure);
        } catch (error) {
            console.error('[Capacity] Mutation request failed.', error);
            showAlert(alertId, networkFailure(true));
        } finally {
            pending[kind] = false;
            if (submit) submit.disabled = false;
            syncActions();
        }
    };

    // ─── Repeater rows (templates; labels bound to generated IDs) ────────────
    const valueOf = (id) => document.getElementById(id)?.value ?? '';
    let rowSequence = 0;
    const addRow = (templateId, listId, fields, prefix) => {
        const template = document.getElementById(templateId);
        const list = document.getElementById(listId);
        if (!template || !list) return null;
        const row = template.content.firstElementChild.cloneNode(true);
        rowSequence += 1;
        fields.forEach(([inputSelector, labelSelector, suffix]) => {
            const input = row.querySelector(inputSelector);
            const label = row.querySelector(labelSelector);
            if (input && label) {
                input.id = `${prefix}${rowSequence}-${suffix}`;
                label.htmlFor = input.id;
            }
        });
        list.appendChild(row);
        row.querySelector('input')?.focus();
        return row;
    };
    const CONSTRAINT_FIELDS = [['.js-constraint-id', '.js-label-constraint-id', 'constraint-id'],
        ['.js-constraint-source', '.js-label-constraint-source', 'source'], ['.js-constraint-version', '.js-label-constraint-version', 'source-version']];
    const ADJUSTMENT_FIELDS = [['.js-resource-ref', '.js-label-resource-ref', 'resource-ref'], ['.js-period', '.js-label-period', 'period'],
        ['.js-delta', '.js-label-delta', 'delta'], ['.js-uom', '.js-label-uom', 'uom']];
    const RESOURCE_FIELDS = [['.js-resource-ref', '.js-label-resource-ref', 'resource-ref']];
    const rowsOf = (listId, rowClass) => Array.from(document.querySelectorAll(`#${listId} .${rowClass}`));
    const cell = (row, selector) => row.querySelector(selector)?.value ?? '';

    // ─── Create scenario ─────────────────────────────────────────────────────
    const constraintRows = () => rowsOf('scenarioConstraintRefRows', 'capacity-constraint-ref-row');
    const adjustmentRows = () => rowsOf('scenarioAdjustmentRows', 'capacity-adjustment-row');

    // Both arrays are always sent ([] when there is no row); rows keep the order shown; every value is sent as typed and
    // availableCapacityDelta stays a JSON string (pack §23.6, never a float).
    const buildScenarioBody = () => JSON.stringify({
        name: valueOf('scenarioName'),
        constraintRefs: constraintRows().map((row) => ({
            constraintId: cell(row, '.js-constraint-id'),
            source: cell(row, '.js-constraint-source'),
            sourceVersion: cell(row, '.js-constraint-version')
        })),
        adjustments: adjustmentRows().map((row) => ({
            resourceRef: cell(row, '.js-resource-ref'),
            period: cell(row, '.js-period'),
            availableCapacityDelta: cell(row, '.js-delta'),
            uomId: cell(row, '.js-uom')
        }))
    });

    // Mirrors CreateCapacityScenarioRequest only (CapacityPlanModels.cs:15-21; CreateCapacityScenarioValidator.cs:8-11).
    const scenarioCheck = () => {
        if (valueOf('scenarioName') === '') return t('RequiredMissing');
        const incomplete = constraintRows().some((row) => CONSTRAINT_FIELDS.some(([s]) => cell(row, s) === ''))
            || adjustmentRows().some((row) => ADJUSTMENT_FIELDS.some(([s]) => cell(row, s) === ''));
        if (incomplete) return t('RowIncomplete');
        let invalid = '';
        adjustmentRows().forEach((row) => {
            const input = row.querySelector('.js-delta');
            const ok = DECIMAL_PATTERN.test(input?.value ?? '');
            if (input) {
                if (ok) input.removeAttribute('aria-invalid');
                else input.setAttribute('aria-invalid', 'true');
            }
            if (!ok) invalid = t('DecimalInvalid');
        });
        return invalid;
    };

    const openCreateScenario = () => {
        if (!canCreateScenarioNow()) return;
        hideAlert('formCreateScenarioAlert');
        offcanvas('offcanvasCreateScenario')?.show();
    };

    const resetScenarioForm = () => {
        const name = document.getElementById('scenarioName');
        if (name) name.value = '';
        [...constraintRows(), ...adjustmentRows()].forEach((row) => row.remove());
    };

    const submitCreateScenario = () => {
        if (!canCreateScenarioNow()) return;
        const problem = scenarioCheck();
        if (problem) { showAlert('formCreateScenarioAlert', problem); return; }
        hideAlert('formCreateScenarioAlert');
        void postIntent({
            kind: 'scenario',
            url: `${base}/scenarios`,
            formId: 'formCreateScenario',
            alertId: 'formCreateScenarioAlert',
            panelId: 'offcanvasCreateScenario',
            submitId: 'btnSubmitCreateScenario',
            bodyText: buildScenarioBody(),
            okStatus: 201,
            idField: 'scenarioId',
            notFoundCode: 'UNKNOWN_CAPACITY_PLAN',
            onAccepted: (scenarioId) => {
                window.showToast?.(t('ScenarioCreated'), 'success');
                resetScenarioForm();
                const input = document.getElementById('openScenarioId');
                if (input) input.value = scenarioId;
                lastScenarioId = scenarioId;
                void loadScenario(scenarioId); // the scenario panel opens with the server state
            },
            onFailure: (failure) => {
                if (failure.code === 'CAPACITY_PLAN_STATE_CONFLICT') {
                    // Stale plan state: message kept in the form, the plan reloads (pack §23.8).
                    showAlert('formCreateScenarioAlert', failure);
                    void loadPlan();
                    return true;
                }
                return false;
            }
        });
    };

    // ─── Evaluate ────────────────────────────────────────────────────────────
    const resourceRows = () => rowsOf('evaluateResourceRefRows', 'capacity-resource-ref-row');

    const fillModes = () => {
        const select = document.getElementById('evaluationMode');
        if (!select) return;
        const selected = select.value;
        select.replaceChildren();
        const placeholder = document.createElement('option');
        placeholder.value = '';
        placeholder.textContent = t('SelectOption');
        select.appendChild(placeholder);
        EVALUATION_MODES.forEach((mode) => {
            const option = document.createElement('option');
            option.value = mode;
            option.textContent = t(EVALUATION_MODE_KEY[mode]);
            select.appendChild(option);
        });
        select.value = EVALUATION_MODES.includes(selected) ? selected : '';
    };

    const buildEvaluateBody = () => JSON.stringify({
        evaluationMode: valueOf('evaluationMode'),
        resourceRefs: resourceRows().map((row) => cell(row, '.js-resource-ref')) // order kept as shown
    });

    // Mirrors EvaluateCapacityScenarioRequest only (EvaluateCapacityScenarioValidator.cs:10-11).
    const evaluateCheck = () => {
        if (!EVALUATION_MODES.includes(valueOf('evaluationMode'))) return t('RequiredMissing');
        const rows = resourceRows();
        if (rows.length === 0 || rows.some((row) => cell(row, '.js-resource-ref') === '')) return t('ResourceRefsMissing');
        return '';
    };

    const openEvaluate = () => {
        if (!canEvaluateNow()) return;
        hideAlert('formEvaluateAlert');
        fillModes();
        const heading = document.getElementById('evaluateScenarioName');
        if (heading) heading.textContent = scenario.name;
        if (resourceRows().length === 0) addRow('resourceRefRowTemplate', 'evaluateResourceRefRows', RESOURCE_FIELDS, 'resourceRef');
        offcanvas('offcanvasEvaluate')?.show();
    };

    const submitEvaluate = () => {
        if (!canEvaluateNow()) return;
        const problem = evaluateCheck();
        if (problem) { showAlert('formEvaluateAlert', problem); return; }
        hideAlert('formEvaluateAlert');
        const scenarioId = scenario.scenarioId;
        const scenarioKey = scenarioId.toLowerCase();
        evaluationPendingFor = scenarioKey;
        void postIntent({
            kind: 'evaluate',
            url: `${base}/scenarios/${encodeURIComponent(scenarioId)}/evaluations`,
            formId: 'formEvaluate',
            alertId: 'formEvaluateAlert',
            panelId: 'offcanvasEvaluate',
            submitId: 'btnSubmitEvaluate',
            bodyText: buildEvaluateBody(),
            okStatus: 202,
            idField: 'evaluationId',
            notFoundCode: 'UNKNOWN_CAPACITY_SCENARIO',
            onAccepted: (evaluationId) => {
                sessionEvaluations.set(scenarioKey, evaluationId.toLowerCase());
                window.showToast?.(t('EvaluationSubmitted'), 'success');
                void loadEvaluation(evaluationId); // the evaluation panel opens with the server state
            },
            onFailure: (failure, intent) => {
                if (failure.code === 'EVALUATION_ALREADY_ACTIVE') {
                    // No second submit for this scenario in this session (pack §23.8).
                    evaluationBlocked.add(scenarioKey);
                    intent.blocked = true;
                    intent.blockedFailure = failure;
                    offcanvas('offcanvasEvaluate')?.hide();
                    toastFailure(failure);
                    return true;
                }
                if (failure.code === 'UNKNOWN_CAPACITY_SCENARIO') {
                    // Scenario safe-not-found: that panel's content is removed; the plan stays.
                    offcanvas('offcanvasEvaluate')?.hide();
                    ++generation.scenario;
                    clearScenario();
                    clearEvaluation();
                    setSectionState('evaluation', 'empty');
                    setSectionState('scenario', 'error', failure, false);
                    return true;
                }
                return false;
            }
        }).finally(() => { evaluationPendingFor = ''; syncActions(); });
    };

    // ─── Events ──────────────────────────────────────────────────────────────
    const bindRemove = (listId, rowClass, addButtonId) => {
        document.getElementById(listId)?.addEventListener('click', (event) => {
            const remove = event.target.closest('.js-remove-row');
            if (!remove) return;
            event.preventDefault();
            remove.closest(`.${rowClass}`)?.remove();
            document.getElementById(addButtonId)?.focus();
        });
    };

    const bindEvents = () => {
        document.getElementById('formOpenScenario')?.addEventListener('submit', openScenarioById);
        document.getElementById('openScenarioId')?.addEventListener('input', () => {
            document.getElementById('openScenarioIdError')?.classList.add('d-none');
            document.getElementById('openScenarioId')?.removeAttribute('aria-invalid');
        });

        document.getElementById('btnOpenCreateScenario')?.addEventListener('click', openCreateScenario);
        document.getElementById('btnSubmitCreateScenario')?.addEventListener('click', submitCreateScenario);
        document.getElementById('btnAddConstraintRef')?.addEventListener('click', () =>
            addRow('constraintRefRowTemplate', 'scenarioConstraintRefRows', CONSTRAINT_FIELDS, 'constraintRef'));
        document.getElementById('btnAddAdjustment')?.addEventListener('click', () =>
            addRow('adjustmentRowTemplate', 'scenarioAdjustmentRows', ADJUSTMENT_FIELDS, 'adjustment'));
        bindRemove('scenarioConstraintRefRows', 'capacity-constraint-ref-row', 'btnAddConstraintRef');
        bindRemove('scenarioAdjustmentRows', 'capacity-adjustment-row', 'btnAddAdjustment');

        document.getElementById('btnOpenEvaluate')?.addEventListener('click', openEvaluate);
        document.getElementById('btnSubmitEvaluate')?.addEventListener('click', submitEvaluate);
        document.getElementById('btnAddResourceRef')?.addEventListener('click', () =>
            addRow('resourceRefRowTemplate', 'evaluateResourceRefRows', RESOURCE_FIELDS, 'resourceRef'));
        bindRemove('evaluateResourceRefRows', 'capacity-resource-ref-row', 'btnAddResourceRef');

        ['formCreateScenario', 'formEvaluate'].forEach((id) => {
            document.getElementById(id)?.addEventListener('submit', (event) => event.preventDefault());
        });

        // Manual Refresh: each click is exactly one getCapacityEvaluation (F192-POLL). Disabled while it is pending.
        document.getElementById('btnRefreshEvaluation')?.addEventListener('click', (event) => {
            if (event.currentTarget.disabled || !canRefreshNow()) return;
            void loadEvaluation(evaluation.evaluationId);
        });

        document.querySelectorAll('#planWorkspace .js-retry').forEach((button) => {
            button.addEventListener('click', () => {
                const target = button.dataset.retry;
                if (target === 'summary') void loadPlan();
                else if (target === 'scenario' && lastScenarioId) void loadScenario(lastScenarioId);
                else if (target === 'evaluation' && lastEvaluationId) void loadEvaluation(lastEvaluationId);
            });
        });

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

        // Hidden surfaces are inert and not keyboard-reachable; focus returns to the opener (pack §23.9).
        [['offcanvasCreateScenario', 'btnOpenCreateScenario', 'scenarioName'],
            ['offcanvasEvaluate', 'btnOpenEvaluate', 'evaluationMode']].forEach(([id, opener, first]) => {
            const el = document.getElementById(id);
            if (!el) return;
            el.inert = true;
            el.addEventListener('show.bs.offcanvas', () => { el.inert = false; });
            el.addEventListener('shown.bs.offcanvas', () => document.getElementById(first)?.focus());
            el.addEventListener('hidden.bs.offcanvas', () => {
                el.inert = true;
                const back = document.getElementById(opener);
                if (back && !back.classList.contains('d-none')) back.focus();
                else document.getElementById('scenarioTitle')?.focus();
            });
        });
    };

    return {
        init: function () {
            if (!host || !isId(planId)) return;
            syncL10n();
            bindEvents();
            fillModes();
            setSectionState('scenario', 'empty');
            setSectionState('evaluation', 'empty');
            syncActions();
            void loadPlan();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => CapacityPlanWorkspace.init());
