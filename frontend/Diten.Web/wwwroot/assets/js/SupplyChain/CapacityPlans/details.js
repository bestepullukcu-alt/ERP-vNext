'use strict';

/**
 * MOD-0192 Capacity — plan workspace (UI-SCOPE-0192 §3; G6). Plan header (getCapacityPlan); Create scenario and Open
 * scenario by ID (no scenario list, F192-LIST); the scenario panel (getCapacityScenario) with Evaluate; the evaluation
 * panel (getCapacityEvaluation) with the bottleneck table and a manual Refresh — no polling (F192-POLL).
 *
 * The opened scenario and evaluation ride the page URL (?scenarioId=&evaluationId=), so a reload or a shared link shows
 * the same view (CP-22). Measures are decimal STRINGS, shown exactly as returned and never parsed to a number. Every call
 * is scoped by ?legalEntityId=; a missing or malformed scope fails closed before any request. One Idempotency-Key per
 * opened offcanvas (Q403); a reused-key answer blocks that form.
 */
(function () {
    const endpoint = '/SupplyChain/CapacityPlans/api';
    const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
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
    const STATUS_KEY = Object.freeze({
        Draft: 'StatusDraft', Evaluating: 'StatusEvaluating', Ready: 'StatusReady', Approved: 'StatusApproved',
        Archived: 'StatusArchived', Evaluated: 'StatusEvaluated', Accepted: 'StatusAccepted', Running: 'StatusRunning',
        Completed: 'StatusCompleted', Failed: 'StatusFailed'
    });
    const STATUS_CSS = Object.freeze({
        Draft: 'secondary', Evaluating: 'warning', Ready: 'info', Approved: 'success', Archived: 'dark', Evaluated: 'info',
        Accepted: 'secondary', Running: 'warning', Completed: 'success', Failed: 'danger'
    });
    const MODE_KEY = Object.freeze({ Finite: 'ModeFinite', Infinite: 'ModeInfinite' });
    const OPEN_EVALUATION = new Set(['Accepted', 'Running']);   // Refresh is offered only while the outcome is open

    const host = document.getElementById('capacity-plan-details');
    const planId = host?.dataset.planId || '';
    const legalEntityId = host?.dataset.legalEntityId || '';
    const scoped = (path) => `${path}?legalEntityId=${encodeURIComponent(legalEntityId)}`;

    let L = window.L10n || {};
    const t = (key) => L[key] || '';
    const uuid = () => crypto.randomUUID();
    const isText = (value) => typeof value === 'string';
    const permissions = (() => {
        try {
            const parsed = JSON.parse(document.getElementById('capacity-plans-permissions')?.textContent || '{}');
            return { canCreateScenario: parsed.canCreateScenario === true, canEvaluate: parsed.canEvaluate === true };
        } catch (_) { return { canCreateScenario: false, canEvaluate: false }; }
    })();
    const antiForgeryToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const traceHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest', 'X-Correlation-Id': uuid() });
    const mutationHeaders = (key) => Object.assign(traceHeaders(), {
        'Content-Type': 'application/json', 'Idempotency-Key': key, 'RequestVerificationToken': antiForgeryToken()
    });
    const handleUnauthorized = () => { window.DtDefaults?.handleUnauthorized?.(); };
    const value = (id) => String(document.getElementById(id)?.value ?? '');
    const offcanvas = (id) => { const el = document.getElementById(id); return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null; };

    // ─── Rendering helpers (textContent only; no server text as HTML) ────────
    const ltr = (text, className) => {
        const el = document.createElement('bdi');
        el.dir = 'ltr';
        if (className) el.className = className;
        el.textContent = text;
        return el;
    };
    const notProvided = () => { const el = document.createElement('span'); el.className = 'text-muted'; el.textContent = t('NotProvided'); return el; };
    const textOrMissing = (text, className) => (isText(text) && text !== '' ? ltr(text, className) : notProvided());
    const dateTime = (text) => {
        if (!isText(text) || text === '') return notProvided();
        const parsed = new Date(text);
        if (Number.isNaN(parsed.getTime())) return ltr(text);
        // A localized date-time follows its own script direction (ar is RTL); forcing LTR scrambles it.
        const el = document.createElement('bdi');
        el.dir = 'auto';
        el.textContent = parsed.toLocaleString(document.documentElement.lang || undefined);
        el.title = text;
        return el;
    };
    const badge = (text, css) => { const el = document.createElement('span'); el.className = `badge bg-label-${css}`; el.textContent = text; return el; };
    const statusNode = (status) => (isText(status) && STATUS_KEY[status] ? badge(t(STATUS_KEY[status]), STATUS_CSS[status]) : textOrMissing(status));
    const setCell = (id, node) => document.getElementById(id)?.replaceChildren(node);
    const cell = (node, className) => { const td = document.createElement('td'); if (className) td.className = className; td.appendChild(node); return td; };
    // A decimal string exactly as the service returned it — never converted to a number, rounded or locale-grouped.
    const decimal = (text) => (isText(text) && text !== '' ? ltr(text, 'font-monospace') : notProvided());

    // ─── Failure envelope → localized message + support reference ────────────
    const readFailure = async (response, notFoundCode) => {
        let body = null;
        try { body = await response.json(); } catch (_) { body = null; }
        const envelopeCode = isText(body?.error?.code) ? body.error.code : '';
        const fallback = { 400: 'INVALID_REQUEST', 401: 'UNAUTHENTICATED', 403: 'FORBIDDEN', 404: notFoundCode, 503: 'DEPENDENCY_UNAVAILABLE' };
        const code = ERROR_KEY[envelopeCode] ? envelopeCode
            : (fallback[response.status] || (response.status >= 500 ? 'INTERNAL_ERROR' : 'INVALID_REQUEST'));
        const reference = isText(body?.error?.correlationId) ? body.error.correlationId : (response.headers.get('X-Correlation-Id') || '');
        return { status: response.status, code, reference: UUID_PATTERN.test(reference) ? reference : '' };
    };
    const failureText = (failure) => t(ERROR_KEY[failure.code] || 'ErrInternalError');
    const networkFailure = () => ({ status: 0, code: 'DEPENDENCY_UNAVAILABLE', reference: '' });
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
            reference.append(label, ltr(failureOrText.reference, 'font-monospace'));
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
    const setInvalid = (element, message) => {
        if (!element) return;
        element.classList.toggle('is-invalid', Boolean(message));
        element.setAttribute('aria-invalid', message ? 'true' : 'false');
        const feedback = element.id ? document.querySelector(`.invalid-feedback[data-for="${element.id}"]`) : null;
        if (feedback) feedback.textContent = message || '';
    };

    // ─── Surfaces ────────────────────────────────────────────────────────────
    const setVisible = (element, visible) => {
        if (!element) return;
        element.hidden = !visible;
        element.toggleAttribute('inert', !visible);
        if (visible) element.removeAttribute('aria-hidden'); else element.setAttribute('aria-hidden', 'true');
    };
    const hideSkeleton = () => { const s = document.getElementById('workspaceSkeleton'); if (s) s.style.display = 'none'; };
    const closeOffcanvases = () => { offcanvas('offcanvasScenario')?.hide(); offcanvas('offcanvasEvaluate')?.hide(); };
    // Not found (also a foreign plan — no leak): nothing of a plan stays on screen, and no action is reachable.
    const renderSafeNotFound = (failure) => {
        hideSkeleton();
        closeOffcanvases();
        setVisible(document.getElementById('workspaceSurface'), false);
        document.getElementById('planName').textContent = t('WorkspaceTitle');
        showAlert('workspaceAlert', failure);
    };
    // A 403 on the read is the denied card inside the shell, never an error toast (MODULE-RECIPE 3.6) — and nothing of the
    // page above it: no "Loading…" title, breadcrumb or Back (UAS-001 §1; G8 M2).
    const renderDenied = () => {
        hideSkeleton();
        closeOffcanvases();
        setVisible(document.getElementById('workspaceHeader'), false);
        setVisible(document.getElementById('workspaceSurface'), false);
        hideAlert('workspaceAlert');
        document.getElementById('workspaceDenied').hidden = false;
    };

    // Any other first-load failure: the skeleton and the "Loading…" title both go, the error takes their place (G8 M2).
    const showLoadFailure = (failure) => {
        hideSkeleton();
        document.getElementById('planName').textContent = t('WorkspaceTitle');
        showAlert('workspaceAlert', failure);
    };

    // ─── State in the URL (reload and shared links show the same view) ───────
    // A present but malformed ID is ignored with a localized message and sends no request (pack §23.4; G8 L5d).
    const urlState = (() => {
        const params = new URLSearchParams(window.location.search);
        let invalid = false;
        const pick = (key) => {
            const v = params.get(key);
            if (v === null || v === '') return '';
            if (UUID_PATTERN.test(v)) return v;
            invalid = true;
            return '';
        };
        return { scenarioId: pick('scenarioId'), evaluationId: pick('evaluationId'), invalid };
    })();
    const writeUrl = () => {
        const params = new URLSearchParams({ legalEntityId });
        if (scenario?.scenarioId) params.set('scenarioId', scenario.scenarioId);
        if (evaluation?.evaluationId) params.set('evaluationId', evaluation.evaluationId);
        window.history.replaceState(null, '', `${window.location.pathname}?${params.toString()}`);
    };

    // ─── Loads (each versioned: a late answer never overwrites a newer one) ──
    let planVersion = 0;
    let scenarioVersion = 0;
    let evaluationVersion = 0;
    let plan = null;
    let scenario = null;
    let evaluation = null;
    const get = (path) => fetch(scoped(path), { credentials: 'same-origin', headers: traceHeaders() });
    const readJson = async (response) => { try { return await response.json(); } catch (_) { return null; } };

    const loadPlan = async () => {
        const version = ++planVersion;
        let response;
        try { response = await get(`${endpoint}/${planId}`); }
        catch (_) { if (version !== planVersion) return; showLoadFailure(networkFailure()); return; }
        if (version !== planVersion) return;
        if (response.status === 401) { handleUnauthorized(); return; }
        if (response.status === 403) { renderDenied(); return; }
        if (!response.ok) {
            const failure = await readFailure(response, 'UNKNOWN_CAPACITY_PLAN');
            if (version !== planVersion) return;
            if (response.status === 404) { renderSafeNotFound(failure); return; }
            showLoadFailure(failure);
            return;
        }
        const body = await readJson(response);
        if (version !== planVersion) return;
        if (!body || body.capacityPlanId !== planId) { showLoadFailure({ status: 502, code: 'INTERNAL_ERROR', reference: '' }); return; }
        plan = body;
        if (urlState.invalid) showAlert('workspaceAlert', t('InvalidLinkId')); else hideAlert('workspaceAlert');
        renderPlan();
        hideSkeleton();
        setVisible(document.getElementById('workspaceSurface'), true);
        if (urlState.scenarioId) await loadScenario(urlState.scenarioId);
        if (urlState.evaluationId) await loadEvaluation(urlState.evaluationId);
    };

    const loadScenario = async (scenarioId) => {
        const version = ++scenarioVersion;
        const section = document.getElementById('scenarioSection');
        let response;
        try { response = await get(`${endpoint}/${planId}/scenarios/${scenarioId}`); }
        catch (_) { if (version !== scenarioVersion) return; showScenarioFailure(networkFailure()); return; }
        if (version !== scenarioVersion) return;
        if (response.status === 401) { handleUnauthorized(); return; }
        if (!response.ok) {
            const failure = await readFailure(response, 'UNKNOWN_CAPACITY_SCENARIO');
            if (version !== scenarioVersion) return;
            showScenarioFailure(failure);
            return;
        }
        const body = await readJson(response);
        if (version !== scenarioVersion) return;
        if (!body || body.scenarioId !== scenarioId) { showScenarioFailure({ status: 502, code: 'INTERNAL_ERROR', reference: '' }); return; }
        scenario = body;
        hideAlert('scenarioAlert');
        renderScenario();
        section.hidden = false;
        writeUrl();
    };
    // A scenario that cannot be shown leaves nothing of a previous one behind (no stale Evaluate target).
    const showScenarioFailure = (failure) => {
        scenario = null;
        document.getElementById('scenarioSection').hidden = false;
        document.getElementById('scenarioName').textContent = '';
        ['scenarioId', 'scenarioStatus'].forEach((id) => document.getElementById(id)?.replaceChildren());
        document.getElementById('constraintRefsBody').replaceChildren();
        document.getElementById('adjustmentsBody').replaceChildren();
        const evaluate = document.getElementById('btnEvaluate');
        if (evaluate) evaluate.hidden = true;
        showAlert('scenarioAlert', failure);
        writeUrl();
    };

    const loadEvaluation = async (evaluationId) => {
        const version = ++evaluationVersion;
        const refresh = document.getElementById('btnRefreshEvaluation');
        if (refresh) refresh.disabled = true;
        let response;
        try { response = await get(`${endpoint}/${planId}/evaluations/${evaluationId}`); }
        catch (_) { if (version === evaluationVersion) { showEvaluationFailure(networkFailure()); if (refresh) refresh.disabled = false; } return; }
        if (version !== evaluationVersion) return;
        if (refresh) refresh.disabled = false;
        if (response.status === 401) { handleUnauthorized(); return; }
        if (!response.ok) {
            const failure = await readFailure(response, 'UNKNOWN_CAPACITY_EVALUATION');
            if (version !== evaluationVersion) return;
            showEvaluationFailure(failure);
            return;
        }
        const body = await readJson(response);
        if (version !== evaluationVersion) return;
        if (!body || body.evaluationId !== evaluationId) { showEvaluationFailure({ status: 502, code: 'INTERNAL_ERROR', reference: '' }); return; }
        evaluation = body;
        hideAlert('evaluationAlert');
        renderEvaluation();
        writeUrl();
    };
    // An evaluation that cannot be shown leaves nothing of a previous one behind (pack §23.8: panel content removed; G8 L5c).
    const showEvaluationFailure = (failure) => {
        evaluation = null;
        document.getElementById('evaluationSection').hidden = false;
        ['evaluationId', 'evaluationStatus', 'evaluationMode', 'evaluationSubmitted', 'evaluationCompleted']
            .forEach((id) => document.getElementById(id)?.replaceChildren());
        document.getElementById('btnRefreshEvaluation').hidden = true;
        document.getElementById('evaluationPending').classList.add('d-none');
        document.getElementById('bottlenecksHost').classList.add('d-none');
        showAlert('evaluationAlert', failure);
        renderEvaluateAction();
        writeUrl();
    };

    // ─── Render ──────────────────────────────────────────────────────────────
    const renderPlan = () => {
        const provenance = plan.provenance && typeof plan.provenance === 'object' ? plan.provenance : {};
        document.getElementById('planName').textContent = isText(plan.name) && plan.name !== '' ? plan.name : t('WorkspaceTitle');
        setCell('detailPlanId', ltr(plan.capacityPlanId, 'font-monospace small'));
        setCell('detailStatus', statusNode(plan.status));
        setCell('detailHorizonStart', textOrMissing(plan.horizonStart));
        setCell('detailHorizonEnd', textOrMissing(plan.horizonEnd));
        setCell('detailDemandPlanId', textOrMissing(provenance.demandPlanId));
        setCell('detailDemandPlanVersion', textOrMissing(provenance.demandPlanVersion));
        setCell('detailSourceCapturedAt', dateTime(provenance.sourceCapturedAt));
        setCell('detailSourceChecksum', textOrMissing(provenance.sourceChecksum, 'font-monospace small text-break'));
        const source = document.createElement('span');
        source.append(textOrMissing(provenance.sourceContract), document.createTextNode(' · '), textOrMissing(provenance.sourceContractVersion));
        setCell('detailProvenance', source);
        setCell('detailCreated', dateTime(plan.createdAt));
    };

    const renderScenario = () => {
        document.getElementById('scenarioName').textContent = isText(scenario.name) ? scenario.name : '';
        setCell('scenarioId', ltr(scenario.scenarioId, 'font-monospace small'));
        setCell('scenarioStatus', statusNode(scenario.status));
        const refs = Array.isArray(scenario.constraintRefs) ? scenario.constraintRefs.filter((r) => r && typeof r === 'object') : [];
        const refsBody = document.getElementById('constraintRefsBody');
        refsBody.replaceChildren(...refs.map((r) => {
            const row = document.createElement('tr');
            row.append(cell(textOrMissing(r.constraintId)), cell(textOrMissing(r.source)), cell(textOrMissing(r.sourceVersion)));
            return row;
        }));
        document.getElementById('constraintRefsEmpty').classList.toggle('d-none', refs.length > 0);
        document.getElementById('constraintRefsHost').classList.toggle('d-none', refs.length === 0);
        const adjustments = Array.isArray(scenario.adjustments) ? scenario.adjustments.filter((a) => a && typeof a === 'object') : [];
        const adjBody = document.getElementById('adjustmentsBody');
        adjBody.replaceChildren(...adjustments.map((a) => {
            const row = document.createElement('tr');
            row.append(cell(textOrMissing(a.resourceRef)), cell(textOrMissing(a.period)), cell(decimal(a.availableCapacityDelta), 'text-end'), cell(textOrMissing(a.uomId)));
            return row;
        }));
        document.getElementById('adjustmentsEmpty').classList.toggle('d-none', adjustments.length > 0);
        document.getElementById('adjustmentsHost').classList.toggle('d-none', adjustments.length === 0);
        const evaluate = document.getElementById('btnEvaluate');
        renderEvaluateAction();
    };

    const renderEvaluation = () => {
        document.getElementById('evaluationSection').hidden = false;
        setCell('evaluationId', ltr(evaluation.evaluationId, 'font-monospace small'));
        setCell('evaluationStatus', statusNode(evaluation.status));
        setCell('evaluationMode', isText(evaluation.evaluationMode) && MODE_KEY[evaluation.evaluationMode]
            ? document.createTextNode(t(MODE_KEY[evaluation.evaluationMode])) : textOrMissing(evaluation.evaluationMode));
        setCell('evaluationSubmitted', dateTime(evaluation.submittedAt));
        setCell('evaluationCompleted', dateTime(evaluation.completedAt));
        const open = OPEN_EVALUATION.has(evaluation.status);
        document.getElementById('btnRefreshEvaluation').hidden = !open;
        document.getElementById('evaluationPending').classList.toggle('d-none', !open);
        const bottlenecks = Array.isArray(evaluation.bottlenecks) ? evaluation.bottlenecks.filter((b) => b && typeof b === 'object') : [];
        // An open evaluation has no result yet: no table and no "no bottlenecks" claim until it has completed.
        document.getElementById('bottlenecksHost').classList.toggle('d-none', open);
        if (!open) fillBottlenecks(bottlenecks);
        renderEvaluateAction();
    };

    // Evaluate is offered for a loaded scenario unless an evaluation of it is shown as Accepted/Running (pack §23.7; G8 L5a),
    // or the service said one is already active (EVALUATION_ALREADY_ACTIVE: no second submit).
    let evaluationActiveFor = '';
    const renderEvaluateAction = () => {
        const evaluate = document.getElementById('btnEvaluate');
        if (!evaluate) return;
        const open = evaluation && evaluation.scenarioId === scenario?.scenarioId && OPEN_EVALUATION.has(evaluation.status);
        evaluate.hidden = !permissions.canEvaluate || !scenario || open || evaluationActiveFor === scenario.scenarioId;
    };

    // Bottlenecks — the bounded DataTables v2 profile of pack §23.5 (G8 M1): DtDefaults.create(), client-side, no saved state.
    // DataTables renders cell strings as HTML, so every value is escaped; decimals stay the returned strings (sorted as text).
    const esc = (value) => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    const ltrHtml = (text, className) => (isText(text) && text !== ''
        ? `<bdi dir="ltr"${className ? ` class="${className}"` : ''}>${esc(text)}</bdi>`
        : `<span class="text-muted">${esc(t('NotProvided'))}</span>`);
    let bottleneckTable = null;
    const fillBottlenecks = (rows) => {
        if (!bottleneckTable) {
            const text = (key, className) => ({ data: null, className, render: (_d, _t, row) => ltrHtml(row[key], className === 'text-end' ? 'font-monospace' : '') });
            bottleneckTable = new DataTable(document.getElementById('dt-capacity-bottlenecks'), window.DtDefaults.create({
                // No buttons: pack §23.5 allows no export, column visibility or saved view.
                data: [], order: [[1, 'asc']], stateSave: false, serverSide: false, language: { emptyTable: t('NoBottlenecks') }, buttons: [{ buttons: [] }],
                columns: [text('resourceRef'), text('period'), text('requiredCapacity', 'text-end'), text('availableCapacity', 'text-end'),
                    text('shortfall', 'text-end'), text('uomId')]
            }));
        }
        bottleneckTable.clear();
        bottleneckTable.rows.add(rows);
        bottleneckTable.draw();
    };

    // ─── Repeaters ───────────────────────────────────────────────────────────
    const addRow = (templateId, hostId, focusSelector) => {
        const row = document.getElementById(templateId).content.firstElementChild.cloneNode(true);
        row.querySelector('.js-row-remove').addEventListener('click', () => row.remove());
        document.getElementById(hostId).appendChild(row);
        row.querySelector(focusSelector)?.focus({ preventScroll: true });
    };
    // Each row needs all its values (contract); an empty cell marks itself. Values sent as typed — no trim.
    const readRows = (hostSelector, selectors, keys) => {
        let valid = true;
        const rows = [...document.querySelectorAll(hostSelector)].map((row) => {
            const item = {};
            selectors.forEach((selector, i) => {
                const input = row.querySelector(selector);
                const missing = String(input.value) === '';
                input.classList.toggle('is-invalid', missing);
                input.setAttribute('aria-invalid', missing ? 'true' : 'false');
                if (missing) valid = false;
                item[keys[i]] = String(input.value);
            });
            return item;
        });
        return { valid, rows };
    };

    // ─── Mutation intents ────────────────────────────────────────────────────
    const intents = { scenario: null, evaluate: null };   // { key, blocked } per opened offcanvas
    const pending = { scenario: false, evaluate: false };

    const send = async (kind, path, body, alertId, submitId, onSuccess) => {
        const intent = intents[kind];
        if (!intent || pending[kind]) { if (pending[kind]) window.showToast?.(t('RequestPending'), 'warning'); return; }
        if (intent.blocked) { showAlert(alertId, t('ErrIdempotencyKeyReused')); return; }
        const submit = document.getElementById(submitId);
        pending[kind] = true;
        submit.disabled = true;
        try {
            const response = await fetch(scoped(path), { method: 'POST', credentials: 'same-origin', headers: mutationHeaders(intent.key), body });
            if (response.status === 401) { handleUnauthorized(); return; }
            if (response.ok) {
                const result = await readJson(response);
                intents[kind] = null;
                offcanvas(kind === 'scenario' ? 'offcanvasScenario' : 'offcanvasEvaluate')?.hide();
                await onSuccess(result);
                return;
            }
            const failure = await readFailure(response, kind === 'scenario' ? 'UNKNOWN_CAPACITY_PLAN' : 'UNKNOWN_CAPACITY_SCENARIO');
            const panel = kind === 'scenario' ? 'offcanvasScenario' : 'offcanvasEvaluate';
            // Denied action: close it and take the button away, with the localized denial (pack §23.8).
            if (response.status === 403) {
                intents[kind] = null;
                offcanvas(panel)?.hide();
                const button = document.getElementById(kind === 'scenario' ? 'btnCreateScenario' : 'btnEvaluate');
                if (button) button.hidden = true;
                showAlert('workspaceAlert', failure);
                return;
            }
            // The target is gone (or never was the caller's): its content goes with it (pack §23.8).
            if (response.status === 404) {
                intents[kind] = null;
                offcanvas(panel)?.hide();
                if (kind === 'scenario') renderSafeNotFound(failure); else showScenarioFailure(failure);
                return;
            }
            if (failure.code === 'IDEMPOTENCY_KEY_REUSED') intent.blocked = true;
            // One active evaluation per scenario: no second submit from this page (pack §23.8).
            if (failure.code === 'EVALUATION_ALREADY_ACTIVE') { intent.blocked = true; evaluationActiveFor = scenario?.scenarioId || ''; renderEvaluateAction(); }
            showAlert(alertId, failure);
            // Stale state: the message says so, and the plan reloads (pack §23.8; G8 L5b).
            if (failure.code === 'CAPACITY_PLAN_STATE_CONFLICT') await loadPlan();
        } catch (_) {
            // Outcome unknown: the same intent (same key, same body) may be retried.
            showAlert(alertId, networkFailure());
        } finally {
            pending[kind] = false;
            submit.disabled = Boolean(intents[kind]?.blocked);
        }
    };

    // Create scenario — CreateCapacityScenarioRequest.
    const openScenarioForm = () => {
        hideAlert('formScenarioAlert');
        const name = document.getElementById('scenarioNameInput');
        name.value = '';
        setInvalid(name, '');
        document.getElementById('constraintRefRows').replaceChildren();
        document.getElementById('adjustmentRows').replaceChildren();
        intents.scenario = { key: uuid(), blocked: false };
        document.getElementById('btnSubmitScenario').disabled = false;
        offcanvas('offcanvasScenario')?.show();
    };
    const submitScenario = () => {
        hideAlert('formScenarioAlert');
        const nameInput = document.getElementById('scenarioNameInput');
        const name = value('scenarioNameInput');
        setInvalid(nameInput, name === '' ? t('FieldRequired') : '');
        const constraints = readRows('#constraintRefRows .js-constraint-row', ['.js-constraint-id', '.js-constraint-source', '.js-constraint-version'], ['constraintId', 'source', 'sourceVersion']);
        const adjustments = readRows('#adjustmentRows .js-adjustment-row', ['.js-adj-resource', '.js-adj-period', '.js-adj-delta', '.js-adj-uom'], ['resourceRef', 'period', 'availableCapacityDelta', 'uomId']);
        if (name === '' || !constraints.valid || !adjustments.valid) { showAlert('formScenarioAlert', t('FieldRequired')); return; }
        // Contract mirror, no tightening (pack §23.6): availableCapacityDelta is a decimal string ^-?\d+(\.\d+)?$.
        const DECIMAL = /^-?\d+(\.\d+)?$/;
        let decimalsValid = true;
        document.querySelectorAll('#adjustmentRows .js-adj-delta').forEach((input) => {
            const bad = !DECIMAL.test(String(input.value));
            input.classList.toggle('is-invalid', bad);
            input.setAttribute('aria-invalid', bad ? 'true' : 'false');
            if (bad) decimalsValid = false;
        });
        if (!decimalsValid) { showAlert('formScenarioAlert', t('DecimalHelp')); return; }
        const body = JSON.stringify({ name, constraintRefs: constraints.rows, adjustments: adjustments.rows });
        void send('scenario', `${endpoint}/${planId}/scenarios`, body, 'formScenarioAlert', 'btnSubmitScenario', async (created) => {
            window.showToast?.(t('ScenarioCreated'), 'success');
            evaluation = null;
            document.getElementById('evaluationSection').hidden = true;
            if (isText(created?.scenarioId) && UUID_PATTERN.test(created.scenarioId)) await loadScenario(created.scenarioId);
        });
    };

    // Evaluate — EvaluateCapacityScenarioRequest. 202: the evaluation runs in the service; Refresh reads it back.
    const openEvaluateForm = () => {
        if (!scenario) return;
        hideAlert('formEvaluateAlert');
        const mode = document.getElementById('evaluationModeInput');
        mode.value = '';
        setInvalid(mode, '');
        document.getElementById('resourceRefRows').replaceChildren();
        addRow('resourceRefTemplate', 'resourceRefRows', '.js-resource-ref');
        intents.evaluate = { key: uuid(), blocked: false };
        document.getElementById('btnSubmitEvaluate').disabled = false;
        offcanvas('offcanvasEvaluate')?.show();
    };
    const submitEvaluate = () => {
        hideAlert('formEvaluateAlert');
        const modeInput = document.getElementById('evaluationModeInput');
        const mode = value('evaluationModeInput');
        setInvalid(modeInput, mode === '' ? t('FieldRequired') : '');
        const resources = readRows('#resourceRefRows .js-resource-row', ['.js-resource-ref'], ['ref']);
        // resourceRefs has minItems 1 (contract): an empty list is refused here, as the service would.
        if (mode === '' || !resources.valid || resources.rows.length === 0) { showAlert('formEvaluateAlert', t('FieldRequired')); return; }
        const body = JSON.stringify({ evaluationMode: mode, resourceRefs: resources.rows.map((r) => r.ref) });
        void send('evaluate', `${endpoint}/${planId}/scenarios/${scenario.scenarioId}/evaluations`, body, 'formEvaluateAlert', 'btnSubmitEvaluate', async (accepted) => {
            window.showToast?.(t('EvaluationSubmitted'), 'success');
            if (isText(accepted?.evaluationId) && UUID_PATTERN.test(accepted.evaluationId)) await loadEvaluation(accepted.evaluationId);
        });
    };

    // Open scenario by ID: a non-UUID is refused without a request; the service answers the rest (safe not-found).
    const openScenario = (event) => {
        event.preventDefault();
        const input = document.getElementById('openScenarioId');
        const id = value('openScenarioId');
        if (!UUID_PATTERN.test(id)) { setInvalid(input, t('ScenarioIdHelp')); return; }
        setInvalid(input, '');
        evaluation = null;
        document.getElementById('evaluationSection').hidden = true;
        void loadScenario(id);
    };

    // ─── Init ────────────────────────────────────────────────────────────────
    const init = () => {
        L = window.L10n || L;
        if (!UUID_PATTERN.test(planId)) { renderSafeNotFound({ status: 404, code: 'UNKNOWN_CAPACITY_PLAN', reference: '' }); return; }
        if (!UUID_PATTERN.test(legalEntityId)) { hideSkeleton(); document.getElementById('planName').textContent = t('WorkspaceTitle'); showAlert('workspaceAlert', t('LegalEntityRequired')); return; }
        document.getElementById('formOpenScenario')?.addEventListener('submit', openScenario);
        document.getElementById('btnCreateScenario')?.addEventListener('click', openScenarioForm);
        document.getElementById('btnAddConstraintRef')?.addEventListener('click', () => addRow('constraintRefTemplate', 'constraintRefRows', '.js-constraint-id'));
        document.getElementById('btnAddAdjustment')?.addEventListener('click', () => addRow('adjustmentTemplate', 'adjustmentRows', '.js-adj-resource'));
        document.getElementById('btnSubmitScenario')?.addEventListener('click', submitScenario);
        document.getElementById('btnEvaluate')?.addEventListener('click', openEvaluateForm);
        document.getElementById('btnAddResourceRef')?.addEventListener('click', () => addRow('resourceRefTemplate', 'resourceRefRows', '.js-resource-ref'));
        document.getElementById('btnSubmitEvaluate')?.addEventListener('click', submitEvaluate);
        document.getElementById('btnRefreshEvaluation')?.addEventListener('click', () => { if (evaluation) void loadEvaluation(evaluation.evaluationId); });
        document.getElementById('offcanvasScenario')?.addEventListener('hidden.bs.offcanvas', () => { if (!pending.scenario) intents.scenario = null; });
        document.getElementById('offcanvasEvaluate')?.addEventListener('hidden.bs.offcanvas', () => { if (!pending.evaluate) intents.evaluate = null; });
        void loadPlan();
    };

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
