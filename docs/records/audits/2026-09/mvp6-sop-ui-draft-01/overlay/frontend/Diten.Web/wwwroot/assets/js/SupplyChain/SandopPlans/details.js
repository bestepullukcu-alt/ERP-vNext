'use strict';

/**
 * MOD-0190 S&OP — plan workspace (pack §23.4–§23.8; golden-reference slim, bounded DataTables v2 profile).
 * DRAFT overlay — not built, not runtime-verified, not writer-complete.
 *
 * Bound operations only (pack §23.3), each through the same-origin adapter /SupplyChain/SandopPlans/api/{id}*:
 * getSandopPlan, listSandopSnapshots, captureSandopSnapshot, listSandopSignOffs, recordSandopSignOff. The browser never
 * calls the Gateway, never holds a token and never sends tenant/legal-entity scope.
 *
 * The UI never changes plan status; it shows the status the server returns (pack §23.7). Actions are gated by key AND
 * status: capture in Draft/InReview; sign-off in InReview with at least one listed snapshot. After every 201 the
 * workspace reloads from the server; a response body is never shown as the current state (a replayed 201 carries the
 * same body as the first one, README A7).
 *
 * Unknown, foreign-scope and soft-deleted plans all give 404 UNKNOWN_SANDOP_PLAN → one identical safe-not-found surface;
 * the workspace content is removed and late responses never re-expose it (SU-13).
 */
const SandopPlanWorkspace = (function () {
    const host = document.getElementById('sandop-plan-details');
    const planId = host?.dataset.sandopPlanId || '';
    const base = `/SupplyChain/SandopPlans/api/${encodeURIComponent(planId)}`;
    const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

    // Published vocabulary (sandop-capacity.openapi.yaml; SandopPlanModels.cs:22).
    const STATUS_KEY = Object.freeze({
        Draft: 'StatusDraft', InReview: 'StatusInReview', Approved: 'StatusApproved', Rejected: 'StatusRejected', Archived: 'StatusArchived'
    });
    const STATUS_CSS = Object.freeze({ Draft: 'secondary', InReview: 'warning', Approved: 'success', Rejected: 'danger', Archived: 'dark' });
    const ROLES = Object.freeze(['DemandPlanning', 'SupplyPlanning', 'Finance', 'Operations', 'Executive']);
    const ROLE_KEY = Object.freeze({
        DemandPlanning: 'RoleDemandPlanning', SupplyPlanning: 'RoleSupplyPlanning', Finance: 'RoleFinance',
        Operations: 'RoleOperations', Executive: 'RoleExecutive'
    });
    const DECISIONS = Object.freeze(['Approved', 'Rejected']);
    const DECISION_KEY = Object.freeze({ Approved: 'DecisionApproved', Rejected: 'DecisionRejected' });
    // Pack §23.7 (display only; §13 on the server stays authoritative).
    const CAPTURE_STATUSES = new Set(['Draft', 'InReview']);
    const SIGN_OFF_STATUSES = new Set(['InReview']);
    const COMMENT_MAX = 2000; // RecordSignOffRequest.comment maxLength
    // All 14 published S&OP codes (pack §23.8), localized — never raw text on screen.
    const ERROR_KEY = Object.freeze({
        INVALID_REQUEST: 'ErrInvalidRequest',
        INVALID_CORRELATION_ID: 'ErrInvalidCorrelationId',
        UNAUTHENTICATED: 'ErrUnauthenticated',
        FORBIDDEN: 'ErrForbidden',
        UNKNOWN_SANDOP_PLAN: 'ErrUnknownSandopPlan',
        SANDOP_PLAN_ALREADY_EXISTS: 'ErrSandopPlanAlreadyExists',
        SANDOP_PLAN_STATE_CONFLICT: 'ErrSandopPlanStateConflict',
        SANDOP_SIGN_OFF_STATE_CONFLICT: 'ErrSandopSignOffStateConflict',
        SIGN_OFF_ALREADY_RECORDED: 'ErrSignOffAlreadyRecorded',
        IDEMPOTENCY_KEY_REUSED: 'ErrIdempotencyKeyReused',
        INVALID_DEMAND_REFERENCE: 'ErrInvalidDemandReference',
        INVALID_SNAPSHOT_REFERENCE: 'ErrInvalidSnapshotReference',
        DEPENDENCY_UNAVAILABLE: 'ErrDependencyUnavailable',
        COMMIT_RESULT_UNRESOLVED: 'ErrCommitResultUnresolved'
    });
    const STATUS_FALLBACK_CODE = Object.freeze({
        400: 'INVALID_REQUEST', 401: 'UNAUTHENTICATED', 403: 'FORBIDDEN', 404: 'UNKNOWN_SANDOP_PLAN',
        409: 'INVALID_REQUEST', 422: 'INVALID_REQUEST'
    });
    const RELOAD_CODES = new Set(['SANDOP_PLAN_STATE_CONFLICT', 'SANDOP_SIGN_OFF_STATE_CONFLICT', 'SIGN_OFF_ALREADY_RECORDED']);

    let L = window.L10n || {};
    const permissions = readPermissions();
    let plan = null;
    let snapshots = [];
    let signOffs = [];
    let generation = 0;      // load version: a late response of an older load is ignored
    let closed = false;      // safe-not-found or page denial: nothing is rendered again
    let dtSnapshots = null;
    let dtSignOffs = null;
    const loaded = { summary: false, snapshots: false, signoffs: false };

    // Mutation intents: { key, bodyText, blocked, blockedFailure }
    const intents = { capture: null, signOff: null };
    const pending = { capture: false, signOff: false };

    // ─── Basics ──────────────────────────────────────────────────────────────
    function readPermissions() {
        const fallback = { canCapture: false, canSignOff: false };
        const island = document.getElementById('sandop-plan-permissions');
        if (!island) return fallback;
        try {
            const parsed = JSON.parse(island.textContent || '{}');
            return { canCapture: parsed.canCapture === true, canSignOff: parsed.canSignOff === true };
        } catch (error) {
            console.error('[S&OP] Permission payload could not be parsed.', error);
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
    const escapeHtml = (value) => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    const ltr = (value) => `<bdi dir="ltr">${escapeHtml(value)}</bdi>`;
    const notProvided = () => `<span class="text-muted">${escapeHtml(t('NotProvided'))}</span>`;
    const copyButton = (value) => `<button type="button" class="btn btn-sm btn-icon btn-text-secondary js-copy-value" `
        + `data-copy-value="${escapeHtml(value)}" aria-label="${escapeHtml(t('Copy'))}"><i class="bx bx-copy" aria-hidden="true"></i></button>`;
    const copyable = (value) => (isText(value) && value
        ? `<span class="d-inline-flex align-items-center gap-1"><bdi dir="ltr" class="font-monospace small text-break">${escapeHtml(value)}</bdi>${copyButton(value)}</span>`
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
    const statusLabel = (status) => (isText(status) && STATUS_KEY[status] ? t(STATUS_KEY[status]) : '');
    const roleLabel = (role) => (isText(role) && ROLE_KEY[role] ? t(ROLE_KEY[role]) : '');
    const decisionLabel = (decision) => (isText(decision) && DECISION_KEY[decision] ? t(DECISION_KEY[decision]) : '');

    // ─── Failure envelope → localized message + support reference ────────────
    const readFailure = async (response, mutation) => {
        let body = null;
        try { body = await response.json(); } catch (_) { body = null; }
        const envelopeCode = isText(body?.error?.code) ? body.error.code : '';
        const unavailable = mutation ? 'COMMIT_RESULT_UNRESOLVED' : 'DEPENDENCY_UNAVAILABLE';
        const code = ERROR_KEY[envelopeCode] ? envelopeCode
            : (STATUS_FALLBACK_CODE[response.status] || (response.status >= 500 ? unavailable : 'INVALID_REQUEST'));
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

    // ─── Section states: skeleton / content / error are distinct (VIEW-001 §3.1) ──
    const SECTION = Object.freeze({
        summary: { skeleton: 'summary-skeleton', content: 'summary-content', error: 'summary-error', errorKey: 'SummaryErrorState' },
        snapshots: { skeleton: 'snapshots-skeleton', content: 'snapshots-table-host', error: 'snapshots-error', errorKey: 'SnapshotsErrorState' },
        signoffs: { skeleton: 'signoffs-skeleton', content: 'signoffs-table-host', error: 'signoffs-error', errorKey: 'SignOffsErrorState' }
    });
    const setSectionState = (name, state, failure) => {
        const ids = SECTION[name];
        document.getElementById(ids.skeleton)?.classList.toggle('d-none', state !== 'skeleton');
        document.getElementById(ids.content)?.classList.toggle('d-none', state !== 'content');
        const error = document.getElementById(ids.error);
        error?.classList.toggle('d-none', state !== 'error');
        if (state === 'error' && error) {
            const message = error.querySelector('.js-section-error-message');
            if (message) message.textContent = failure?.code ? failureText(failure) : t(ids.errorKey);
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

    // Safe-not-found (404 UNKNOWN_SANDOP_PLAN) and page denial (403): remove the workspace; later responses are ignored.
    const closeWorkspace = (failure) => {
        closed = true;
        plan = null;
        snapshots = [];
        signOffs = [];
        const workspace = document.getElementById('planWorkspace');
        if (workspace) {
            workspace.hidden = true;
            workspace.inert = true;
            workspace.setAttribute('aria-hidden', 'true');
        }
        dtSnapshots?.clear().draw();
        dtSignOffs?.clear().draw();
        ['offcanvasCaptureSnapshot', 'offcanvasRecordSignOff'].forEach((id) => {
            const el = document.getElementById(id);
            if (el) window.bootstrap?.Offcanvas.getInstance(el)?.hide();
        });
        document.getElementById('btnOpenCaptureSnapshot')?.remove();
        document.getElementById('btnOpenRecordSignOff')?.remove();
        const title = document.getElementById('planTitle');
        if (title) title.textContent = t('PlanDetailsTitle');
        showAlert('planAlert', failure);
        document.getElementById('planAlert')?.focus();
    };

    // ─── Loading ─────────────────────────────────────────────────────────────
    const getJson = async (url) => {
        try {
            const response = await fetch(url, { credentials: 'same-origin', headers: traceHeaders() });
            if (!response.ok) return { failure: await readFailure(response, false) };
            let body = null;
            try { body = await response.json(); } catch (_) { body = null; }
            return { body, reference: response.headers.get('X-Correlation-Id') || '' };
        } catch (error) {
            console.error('[S&OP] Read request failed.', error);
            return { failure: networkFailure(false) };
        }
    };

    // Returns true when the failure closed the workspace or started the session surface.
    const pageLevelFailure = (failure) => {
        if (failure.status === 401) { handleUnauthorized(); return true; }
        if (failure.status === 404 || failure.status === 403) { closeWorkspace(failure); return true; }
        return false;
    };

    const isPlan = (body) => isObject(body) && isText(body.sandopPlanId) && isText(body.name) && isText(body.status)
        && isText(body.horizonStart) && isText(body.horizonEnd) && isText(body.demandPlanId) && isText(body.demandPlanVersion);
    const isItems = (body) => isObject(body) && Array.isArray(body.items) && body.items.every(isObject);

    const loadPlan = async (gen) => {
        const result = await getJson(base);
        if (gen !== generation || closed) return;
        if (result.failure) {
            if (!pageLevelFailure(result.failure)) setSectionState('summary', 'error', result.failure);
            return;
        }
        if (!isPlan(result.body)) {
            // Malformed envelope → error, never an empty or partial summary.
            setSectionState('summary', 'error', { status: 200, code: 'DEPENDENCY_UNAVAILABLE', reference: result.reference });
            return;
        }
        plan = result.body;
        loaded.summary = true;
        renderSummary();
        setSectionState('summary', 'content');
    };

    const loadList = async (gen, section, path, assign) => {
        const result = await getJson(`${base}/${path}`);
        if (gen !== generation || closed) return;
        if (result.failure) {
            if (!pageLevelFailure(result.failure)) setSectionState(section, 'error', result.failure);
            return;
        }
        if (!isItems(result.body)) {
            setSectionState(section, 'error', { status: 200, code: 'DEPENDENCY_UNAVAILABLE', reference: result.reference });
            return;
        }
        assign(result.body.items);
        loaded[section] = true;
        setSectionState(section, 'content');
    };

    const loadAll = async () => {
        if (closed) return;
        const gen = ++generation;
        Object.keys(SECTION).forEach((name) => { if (!loaded[name]) setSectionState(name, 'skeleton'); });
        await Promise.all([
            loadPlan(gen),
            loadList(gen, 'snapshots', 'snapshots', (items) => { snapshots = items; renderSnapshots(); }),
            loadList(gen, 'signoffs', 'sign-offs', (items) => { signOffs = items; renderSignOffs(); })
        ]);
        if (gen === generation && !closed) {
            renderSnapshots(); // the current-snapshot marker depends on the plan, which may arrive after the list
            syncActions();
        }
    };

    // ─── Rendering ───────────────────────────────────────────────────────────
    const setHtml = (id, html) => { const el = document.getElementById(id); if (el) el.innerHTML = html; };

    const renderSummary = () => {
        if (!plan) return;
        const title = document.getElementById('planTitle');
        if (title) title.textContent = plan.name;
        setHtml('summaryPlanId', copyable(plan.sandopPlanId));
        const label = statusLabel(plan.status);
        setHtml('summaryStatus', label
            ? `<span class="badge bg-label-${STATUS_CSS[plan.status]}">${escapeHtml(label)}</span>`
            : notProvided());
        setHtml('summaryHorizon', `${ltr(plan.horizonStart)} – ${ltr(plan.horizonEnd)}`);
        setHtml('summaryDemand', `${ltr(plan.demandPlanId)} / ${ltr(plan.demandPlanVersion)}`);
        setHtml('summaryCurrentSnapshot', copyable(isText(plan.currentSnapshotId) ? plan.currentSnapshotId : ''));
        setHtml('summaryCreatedAt', isText(plan.createdAt) ? ltr(plan.createdAt) : notProvided());
    };

    // Text cell: display escapes and isolates; sort/search use the raw wire text.
    const textCell = (isLtr) => (value, type) => {
        if (type !== 'display') return isText(value) ? value : '';
        if (!isText(value) || value === '') return notProvided();
        return isLtr ? ltr(value) : escapeHtml(value);
    };

    const snapshotColumns = () => [
        { data: null, defaultContent: '', className: 'control', orderable: false, searchable: false },
        { data: 'capturedAt', render: textCell(true) },
        {
            data: 'snapshotId',
            render: (value, type) => {
                if (type !== 'display') return isText(value) ? value : '';
                const current = isText(value) && plan && plan.currentSnapshotId === value
                    ? ` <span class="badge bg-label-primary ms-1">${escapeHtml(t('CurrentMarker'))}</span>` : '';
                return `${copyable(value)}${current}`;
            }
        },
        {
            data: 'provenance', orderable: false,
            render: (value, type) => {
                const ok = isObject(value) && isText(value.demandPlanId) && isText(value.demandPlanVersion);
                if (type !== 'display') return ok ? `${value.demandPlanId} ${value.demandPlanVersion}` : '';
                return ok ? `${ltr(value.demandPlanId)} / ${ltr(value.demandPlanVersion)}` : notProvided();
            }
        },
        {
            data: 'provenance', orderable: false,
            render: (value, type) => {
                const checksum = isObject(value) && isText(value.sourceChecksum) ? value.sourceChecksum : '';
                return type !== 'display' ? checksum : copyable(checksum);
            }
        },
        {
            data: 'provenance',
            render: (value, type) => {
                const at = isObject(value) && isText(value.sourceCapturedAt) ? value.sourceCapturedAt : '';
                if (type !== 'display') return at;
                return at ? ltr(at) : notProvided();
            }
        },
        {
            data: 'supplyInputRefs', className: 'text-end',
            render: (value, type) => {
                const count = Array.isArray(value) ? value.length : null;
                if (type !== 'display') return count ?? -1;
                return count === null ? notProvided() : ltr(String(count));
            }
        }
    ];

    const signOffColumns = () => [
        { data: null, defaultContent: '', className: 'control', orderable: false, searchable: false },
        { data: 'decidedAt', render: textCell(true) },
        { data: 'snapshotId', render: (value, type) => (type === 'display' ? copyable(value) : (isText(value) ? value : '')) },
        {
            data: 'role',
            render: (value, type) => {
                const label = roleLabel(value);
                if (type !== 'display') return label || (isText(value) ? value : '');
                return label ? escapeHtml(label) : notProvided();
            }
        },
        {
            data: 'decision',
            render: (value, type) => {
                const label = decisionLabel(value);
                if (type !== 'display') return label;
                return label
                    ? `<span class="badge bg-label-${value === 'Approved' ? 'success' : 'danger'}">${escapeHtml(label)}</span>`
                    : notProvided();
            }
        },
        { data: 'comment', orderable: false, render: textCell(false) },
        { data: 'decidedBy', render: (value, type) => (type === 'display' ? copyable(value) : (isText(value) ? value : '')) }
    ];

    // Bounded client-side profile (pack §23.5): no server paging/search, no filter, column visibility, saved view,
    // export or quick view; client search/sort/paging over the returned set only.
    const createTable = (selector, columns, emptyKey) => {
        const el = document.querySelector(selector);
        if (!el || typeof DataTable === 'undefined' || !window.DtDefaults) return null;
        return new DataTable(el, window.DtDefaults.create({
            serverSide: false,
            stateSave: false,
            processing: false,
            data: [],
            order: [[1, 'desc']],
            responsive: { details: { type: 'column', target: 0 } },
            columns,
            buttons: [],
            language: { emptyTable: t(emptyKey), zeroRecords: t(emptyKey) }
        }));
    };

    const renderSnapshots = () => {
        if (!dtSnapshots) dtSnapshots = createTable('#dt-sandop-snapshots', snapshotColumns(), 'EmptySnapshots');
        dtSnapshots?.clear().rows.add(snapshots).draw();
    };
    const renderSignOffs = () => {
        if (!dtSignOffs) dtSignOffs = createTable('#dt-sandop-signoffs', signOffColumns(), 'EmptySignOffs');
        dtSignOffs?.clear().rows.add(signOffs).draw();
    };

    // ─── State-gated actions (pack §23.7) ────────────────────────────────────
    const canCaptureNow = () => permissions.canCapture && !closed && plan !== null && CAPTURE_STATUSES.has(plan.status);
    const canSignOffNow = () => permissions.canSignOff && !closed && plan !== null && SIGN_OFF_STATUSES.has(plan.status)
        && snapshots.some((s) => isText(s.snapshotId) && UUID_PATTERN.test(s.snapshotId));
    const syncActions = () => {
        document.getElementById('btnOpenCaptureSnapshot')?.classList.toggle('d-none', !canCaptureNow());
        document.getElementById('btnOpenRecordSignOff')?.classList.toggle('d-none', !canSignOffNow());
    };

    // Shared mutation outcome handling (pack §23.8).
    const handleMutationFailure = (failure, intent, alertId, panelId) => {
        if (failure.status === 401) { handleUnauthorized(); return; }
        if (failure.status === 404) { offcanvas(panelId)?.hide(); closeWorkspace(failure); return; }
        if (failure.status === 403) {
            // Action denial: close, remove the control, localized denial.
            offcanvas(panelId)?.hide();
            document.getElementById(panelId === 'offcanvasCaptureSnapshot' ? 'btnOpenCaptureSnapshot' : 'btnOpenRecordSignOff')?.remove();
            toastFailure(failure);
            return;
        }
        if (RELOAD_CODES.has(failure.code)) {
            // Stale state, or the first decision already recorded: show the message and reload plan and lists.
            offcanvas(panelId)?.hide();
            toastFailure(failure);
            void loadAll();
            return;
        }
        if (failure.code === 'IDEMPOTENCY_KEY_REUSED') {
            // Stop retry: a new user intent (an edited payload) is required.
            intent.blocked = true;
            intent.blockedFailure = failure;
        }
        // 400/422: inputs kept. 503/network: the same key and identical body on the explicit retry.
        showAlert(alertId, failure);
    };

    const postIntent = async (kind, path, formId, alertId, panelId, submitId, bodyText, onCreated) => {
        if (pending[kind]) { window.showToast?.(t('RequestPending'), 'warning'); return; }
        if (!intents[kind] || intents[kind].bodyText !== bodyText) {
            intents[kind] = { key: uuid(), bodyText, blocked: false, blockedFailure: null };
        } else if (intents[kind].blocked) {
            showAlert(alertId, intents[kind].blockedFailure);
            return;
        }
        const intent = intents[kind];
        const submit = document.getElementById(submitId);
        pending[kind] = true;
        if (submit) submit.disabled = true;
        try {
            const response = await fetch(`${base}/${path}`, {
                method: 'POST', credentials: 'same-origin', headers: mutationHeaders(intent.key, formId), body: intent.bodyText
            });
            if (response.status === 201) {
                intents[kind] = null;
                offcanvas(panelId)?.hide();
                onCreated();
                void loadAll(); // the server state is shown, never the response body
                return;
            }
            handleMutationFailure(await readFailure(response, true), intent, alertId, panelId);
        } catch (error) {
            console.error('[S&OP] Mutation request failed.', error);
            showAlert(alertId, networkFailure(true));
        } finally {
            pending[kind] = false;
            if (submit) submit.disabled = false;
        }
    };

    // ─── Capture snapshot ────────────────────────────────────────────────────
    const valueOf = (id) => document.getElementById(id)?.value ?? '';
    const supplyRows = () => Array.from(document.querySelectorAll('#captureSupplyRefs .sandop-supply-ref-row'));
    let rowSequence = 0;

    const addSupplyRow = () => {
        const template = document.getElementById('supplyRefRowTemplate');
        const list = document.getElementById('captureSupplyRefs');
        if (!template || !list) return;
        const row = template.content.firstElementChild.cloneNode(true);
        rowSequence += 1;
        [['.js-ref-source', '.js-label-source', 'source'], ['.js-ref-resource-id', '.js-label-resource-id', 'resource-id'],
            ['.js-ref-resource-version', '.js-label-resource-version', 'resource-version']].forEach(([inputSelector, labelSelector, suffix]) => {
            const input = row.querySelector(inputSelector);
            const label = row.querySelector(labelSelector);
            if (input && label) {
                input.id = `supplyRef${rowSequence}-${suffix}`;
                label.htmlFor = input.id;
            }
        });
        list.appendChild(row);
        row.querySelector('.js-ref-source')?.focus();
    };

    const buildCaptureBody = () => JSON.stringify({
        demandPlanId: valueOf('captureDemandPlanId'),
        demandPlanVersion: valueOf('captureDemandPlanVersion'),
        sourceCapturedAt: valueOf('captureSourceCapturedAt'),
        sourceChecksum: valueOf('captureSourceChecksum'),
        // Always sent; [] when there is no row (pack §23.6, SU-07). Order kept as shown.
        supplyInputRefs: supplyRows().map((row) => ({
            source: row.querySelector('.js-ref-source')?.value ?? '',
            resourceId: row.querySelector('.js-ref-resource-id')?.value ?? '',
            resourceVersion: row.querySelector('.js-ref-resource-version')?.value ?? ''
        }))
    });

    // Mirrors CaptureSandopSnapshotRequest only: 4 required, each repeater row all three (SandopPlanModels.cs:17-19).
    const captureCheck = () => {
        if (['captureDemandPlanId', 'captureDemandPlanVersion', 'captureSourceCapturedAt', 'captureSourceChecksum']
            .some((id) => valueOf(id) === '')) return t('RequiredMissing');
        const incomplete = supplyRows().some((row) => ['.js-ref-source', '.js-ref-resource-id', '.js-ref-resource-version']
            .some((selector) => (row.querySelector(selector)?.value ?? '') === ''));
        return incomplete ? t('RowIncomplete') : '';
    };

    const openCapture = () => {
        if (!canCaptureNow()) return;
        hideAlert('formCaptureSnapshotAlert');
        // Convenience defaults only when a field is empty (README A5); every value stays editable and is sent as shown.
        const demandId = document.getElementById('captureDemandPlanId');
        const demandVersion = document.getElementById('captureDemandPlanVersion');
        const capturedAt = document.getElementById('captureSourceCapturedAt');
        if (demandId && demandId.value === '') demandId.value = plan.demandPlanId;
        if (demandVersion && demandVersion.value === '') demandVersion.value = plan.demandPlanVersion;
        if (capturedAt && capturedAt.value === '') capturedAt.value = new Date().toISOString();
        offcanvas('offcanvasCaptureSnapshot')?.show();
    };

    const submitCapture = () => {
        if (!canCaptureNow()) return;
        const problem = captureCheck();
        if (problem) { showAlert('formCaptureSnapshotAlert', problem); return; }
        hideAlert('formCaptureSnapshotAlert');
        void postIntent('capture', 'snapshots', 'formCaptureSnapshot', 'formCaptureSnapshotAlert', 'offcanvasCaptureSnapshot',
            'btnSubmitCaptureSnapshot', buildCaptureBody(), () => {
                window.showToast?.(t('SnapshotCaptured'), 'success');
                supplyRows().forEach((row) => row.remove());
                ['captureSourceCapturedAt', 'captureSourceChecksum'].forEach((id) => {
                    const el = document.getElementById(id);
                    if (el) el.value = '';
                });
            });
    };

    // ─── Record sign-off ─────────────────────────────────────────────────────
    const fillSelect = (select, options, selected) => {
        if (!select) return;
        select.replaceChildren();
        const placeholder = document.createElement('option');
        placeholder.value = '';
        placeholder.textContent = t('SelectOption');
        select.appendChild(placeholder);
        options.forEach(({ value, text }) => {
            const option = document.createElement('option');
            option.value = value;
            option.textContent = text;
            select.appendChild(option);
        });
        select.value = selected && options.some((o) => o.value === selected) ? selected : '';
    };

    const openSignOff = () => {
        if (!canSignOffNow()) return;
        hideAlert('formRecordSignOffAlert');
        const snapshotSelect = document.getElementById('signOffSnapshotId');
        const previous = snapshotSelect?.value || '';
        // Snapshots of THIS plan only, from the loaded listSandopSnapshots set (pack §23.6).
        const options = snapshots.filter((s) => isText(s.snapshotId) && UUID_PATTERN.test(s.snapshotId)).map((s) => ({
            value: s.snapshotId,
            text: s.snapshotId === plan.currentSnapshotId ? `${s.snapshotId} (${t('CurrentMarker')})` : s.snapshotId
        }));
        fillSelect(snapshotSelect, options, previous || plan.currentSnapshotId);
        const role = document.getElementById('signOffRole');
        fillSelect(role, ROLES.map((r) => ({ value: r, text: roleLabel(r) })), role?.value);
        const decision = document.getElementById('signOffDecision');
        fillSelect(decision, DECISIONS.map((d) => ({ value: d, text: decisionLabel(d) })), decision?.value);
        offcanvas('offcanvasRecordSignOff')?.show();
    };

    const buildSignOffBody = () => {
        const body = {
            snapshotId: valueOf('signOffSnapshotId'),
            role: valueOf('signOffRole'),
            decision: valueOf('signOffDecision')
        };
        const comment = valueOf('signOffComment');
        if (comment !== '') body.comment = comment; // optional; omitted when empty, never trimmed or truncated
        return JSON.stringify(body);
    };

    const signOffCheck = () => {
        if (['signOffSnapshotId', 'signOffRole', 'signOffDecision'].some((id) => valueOf(id) === '')) return t('RequiredMissing');
        if (valueOf('signOffComment').length > COMMENT_MAX) return t('CommentTooLong');
        return '';
    };

    const submitSignOff = () => postIntent('signOff', 'sign-offs', 'formRecordSignOff', 'formRecordSignOffAlert',
        'offcanvasRecordSignOff', 'btnSubmitRecordSignOff', buildSignOffBody(), () => {
            window.showToast?.(t('SignOffRecorded'), 'success');
            const comment = document.getElementById('signOffComment');
            if (comment) comment.value = '';
        });

    const requestSignOff = () => {
        if (!canSignOffNow()) return;
        if (pending.signOff) { window.showToast?.(t('RequestPending'), 'warning'); return; }
        const problem = signOffCheck();
        if (problem) { showAlert('formRecordSignOffAlert', problem); return; }
        hideAlert('formRecordSignOffAlert');
        const decision = valueOf('signOffDecision');
        // Final confirmation through the shared premium wrapper only (MOD-0013 showConfirm(title, callback, options));
        // no native dialog and no direct SweetAlert call. Title and subtext are resx text only (SU-09).
        window.showConfirm?.(t('RecordSignOffTitle'), () => { void submitSignOff(); }, {
            subtext: `${roleLabel(valueOf('signOffRole'))} · ${decisionLabel(decision)}. ${t('ConfirmSignOff')}`,
            type: decision === 'Rejected' ? 'danger' : undefined,
            confirmButtonText: t('RecordSignOff')
        });
    };

    // ─── Events ──────────────────────────────────────────────────────────────
    const bindEvents = () => {
        document.getElementById('btnOpenCaptureSnapshot')?.addEventListener('click', openCapture);
        document.getElementById('btnSubmitCaptureSnapshot')?.addEventListener('click', submitCapture);
        document.getElementById('btnAddSupplyRef')?.addEventListener('click', addSupplyRow);
        document.getElementById('captureSupplyRefs')?.addEventListener('click', (event) => {
            const remove = event.target.closest('.js-remove-supply-ref');
            if (!remove) return;
            event.preventDefault();
            remove.closest('.sandop-supply-ref-row')?.remove();
            document.getElementById('btnAddSupplyRef')?.focus();
        });
        document.getElementById('btnOpenRecordSignOff')?.addEventListener('click', openSignOff);
        document.getElementById('btnSubmitRecordSignOff')?.addEventListener('click', requestSignOff);
        ['formCaptureSnapshot', 'formRecordSignOff'].forEach((id) => {
            document.getElementById(id)?.addEventListener('submit', (event) => event.preventDefault());
        });

        document.querySelectorAll('#planWorkspace .js-retry').forEach((button) => {
            button.addEventListener('click', () => { void loadAll(); });
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
                console.error('[S&OP] Copy failed.', error);
                window.showToast?.(t('ErrorOccurred'), 'error');
            }
        });

        // Hidden surfaces are inert and not keyboard-reachable; focus returns to the opener (pack §23.9).
        [['offcanvasCaptureSnapshot', 'btnOpenCaptureSnapshot', 'captureDemandPlanId'],
            ['offcanvasRecordSignOff', 'btnOpenRecordSignOff', 'signOffSnapshotId']].forEach(([id, opener, first]) => {
            const el = document.getElementById(id);
            if (!el) return;
            el.inert = true;
            el.addEventListener('show.bs.offcanvas', () => { el.inert = false; });
            el.addEventListener('shown.bs.offcanvas', () => document.getElementById(first)?.focus());
            el.addEventListener('hidden.bs.offcanvas', () => {
                el.inert = true;
                document.getElementById(opener)?.focus();
            });
        });
    };

    return {
        init: function () {
            if (!host || !UUID_PATTERN.test(planId)) return;
            syncL10n();
            bindEvents();
            void loadAll();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => SandopPlanWorkspace.init());
