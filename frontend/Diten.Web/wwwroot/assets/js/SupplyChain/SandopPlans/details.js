'use strict';

/**
 * MOD-0190 S&OP — plan workspace (UI-SCOPE-0190 §3; G6). Header from getSandopPlan; snapshots and sign-offs as
 * client-side tables (listSandopSnapshots / listSandopSignOffs); Capture snapshot (Draft/InReview) and Record sign-off
 * (InReview, needs a snapshot). Status is server-owned and shown as returned — the page never changes it (O-03), and
 * five approvals do not advance the plan (pack §16).
 *
 * Every call is scoped by ?legalEntityId= from the page URL; a missing or malformed scope fails closed before any
 * request (the MOD-0183 Details lesson, 2026-10-18). One Idempotency-Key per opened offcanvas (Q403): kept across edits,
 * failures and retries until it closes; a reused-key answer blocks that form.
 */
(function () {
    const endpoint = '/SupplyChain/SandopPlans/api';
    const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
    const ERROR_KEY = Object.freeze({
        INVALID_REQUEST: 'ErrInvalidRequest',
        INVALID_CORRELATION_ID: 'ErrInvalidCorrelationId',
        UNAUTHENTICATED: 'ErrUnauthenticated',
        FORBIDDEN: 'ErrForbidden',
        UNKNOWN_SANDOP_PLAN: 'ErrSandopPlanNotFound',
        SANDOP_PLAN_ALREADY_EXISTS: 'ErrSandopPlanAlreadyExists',
        SANDOP_PLAN_STATE_CONFLICT: 'ErrSandopPlanStateConflict',
        SANDOP_SIGN_OFF_STATE_CONFLICT: 'ErrSandopSignOffStateConflict',
        SIGN_OFF_ALREADY_RECORDED: 'ErrSignOffAlreadyRecorded',
        IDEMPOTENCY_KEY_REUSED: 'ErrIdempotencyKeyReused',
        INVALID_DEMAND_REFERENCE: 'ErrInvalidDemandReference',
        INVALID_SNAPSHOT_REFERENCE: 'ErrInvalidSnapshotReference',
        DEPENDENCY_UNAVAILABLE: 'ErrDependencyUnavailable',
        COMMIT_RESULT_UNRESOLVED: 'ErrCommitResultUnresolved',
        INTERNAL_ERROR: 'ErrInternalError'
    });
    const STATUS_FALLBACK_CODE = Object.freeze({
        400: 'INVALID_REQUEST', 401: 'UNAUTHENTICATED', 403: 'FORBIDDEN', 404: 'UNKNOWN_SANDOP_PLAN', 503: 'DEPENDENCY_UNAVAILABLE'
    });
    // The 409s whose resx text says the plan was reloaded, and the one whose rule says the sign-offs reload (pack §23.8).
    const RELOAD_CODES = new Set(['SANDOP_PLAN_STATE_CONFLICT', 'SANDOP_SIGN_OFF_STATE_CONFLICT', 'SIGN_OFF_ALREADY_RECORDED']);
    const STATUS_KEY = Object.freeze({
        Draft: 'StatusDraft', InReview: 'StatusInReview', Approved: 'StatusApproved', Rejected: 'StatusRejected', Archived: 'StatusArchived'
    });
    const STATUS_CSS = Object.freeze({ Draft: 'secondary', InReview: 'warning', Approved: 'success', Rejected: 'danger', Archived: 'dark' });
    const ROLE_KEY = Object.freeze({
        DemandPlanning: 'RoleDemandPlanning', SupplyPlanning: 'RoleSupplyPlanning', Finance: 'RoleFinance',
        Operations: 'RoleOperations', Executive: 'RoleExecutive'
    });
    const DECISION_KEY = Object.freeze({ Approved: 'DecisionApproved', Rejected: 'DecisionRejected' });
    const CAPTURE_STATUSES = new Set(['Draft', 'InReview']);   // pack §13 line 169
    const SIGN_OFF_STATUSES = new Set(['InReview']);           // pack §13 line 170

    const host = document.getElementById('sandop-plan-details');
    const planId = host?.dataset.planId || '';
    const legalEntityId = host?.dataset.legalEntityId || '';
    const scoped = (path) => `${path}?legalEntityId=${encodeURIComponent(legalEntityId)}`;

    let L = window.L10n || {};
    const t = (key) => L[key] || '';
    const uuid = () => crypto.randomUUID();
    const isText = (value) => typeof value === 'string';
    const permissions = (() => {
        try {
            const parsed = JSON.parse(document.getElementById('sandop-plans-permissions')?.textContent || '{}');
            return { canCapture: parsed.canCapture === true, canSignOff: parsed.canSignOff === true };
        } catch (_) { return { canCapture: false, canSignOff: false }; }
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
    const setCell = (id, node) => document.getElementById(id)?.replaceChildren(node);

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
    const closeOffcanvases = () => { offcanvas('offcanvasCapture')?.hide(); offcanvas('offcanvasSignOff')?.hide(); };
    // Not found (also a foreign plan — no leak): nothing of the plan stays on screen, and no action is reachable.
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

    // ─── Load (versioned: a late answer never overwrites a newer one) ────────
    let loadVersion = 0;
    let plan = null;
    let snapshots = [];
    let signOffs = [];

    const fetchJson = (path) => fetch(scoped(path), { credentials: 'same-origin', headers: traceHeaders() });
    const readItems = async (response) => {
        let body = null;
        try { body = await response.json(); } catch (_) { body = null; }
        return Array.isArray(body?.items) ? body.items.filter((item) => item && typeof item === 'object') : null;
    };

    const load = async () => {
        const version = ++loadVersion;
        let planResponse;
        try {
            planResponse = await fetchJson(`${endpoint}/${planId}`);
        } catch (_) {
            if (version !== loadVersion) return;
            showLoadFailure({ status: 0, code: 'DEPENDENCY_UNAVAILABLE', reference: '' });
            return;
        }
        if (version !== loadVersion) return;
        if (planResponse.status === 401) { handleUnauthorized(); return; }
        if (planResponse.status === 403) { renderDenied(); return; }
        if (!planResponse.ok) {
            const failure = await readFailure(planResponse);
            if (version !== loadVersion) return;
            if (failure.code === 'UNKNOWN_SANDOP_PLAN' || planResponse.status === 404) { renderSafeNotFound(failure); return; }
            showLoadFailure(failure);
            return;
        }
        let body = null;
        try { body = await planResponse.json(); } catch (_) { body = null; }
        if (version !== loadVersion) return;
        if (!body || body.sandopPlanId !== planId) {
            showLoadFailure({ status: 502, code: 'INTERNAL_ERROR', reference: '' });
            return;
        }
        plan = body;
        hideAlert('workspaceAlert');
        renderPlan();
        hideSkeleton();
        setVisible(document.getElementById('workspaceSurface'), true);
        await Promise.all([loadSnapshots(version), loadSignOffs(version)]);
        if (version === loadVersion) renderActions();
    };

    const loadSnapshots = async (version) => {
        try {
            const response = await fetchJson(`${endpoint}/${planId}/snapshots`);
            if (version !== loadVersion) return;
            if (response.status === 401) { handleUnauthorized(); return; }
            const items = response.ok ? await readItems(response) : null;
            if (version !== loadVersion) return;
            if (items === null) {
                snapshots = [];
                showAlert('snapshotsError', response.ok ? t('ListErrorState') : await readFailure(response));
                renderSnapshots(false);
                return;
            }
            hideAlert('snapshotsError');
            snapshots = items.slice().sort((a, b) => String(b.capturedAt || '').localeCompare(String(a.capturedAt || '')));
            renderSnapshots(true);
        } catch (_) {
            if (version !== loadVersion) return;
            snapshots = [];
            showAlert('snapshotsError', t('ListErrorState'));
            renderSnapshots(false);
        }
    };

    const loadSignOffs = async (version) => {
        try {
            const response = await fetchJson(`${endpoint}/${planId}/sign-offs`);
            if (version !== loadVersion) return;
            if (response.status === 401) { handleUnauthorized(); return; }
            const items = response.ok ? await readItems(response) : null;
            if (version !== loadVersion) return;
            if (items === null) {
                signOffs = [];
                showAlert('signOffsError', response.ok ? t('ListErrorState') : await readFailure(response));
                renderSignOffs(false);
                return;
            }
            hideAlert('signOffsError');
            signOffs = items.slice().sort((a, b) => String(b.decidedAt || '').localeCompare(String(a.decidedAt || '')));
            renderSignOffs(true);
        } catch (_) {
            if (version !== loadVersion) return;
            signOffs = [];
            showAlert('signOffsError', t('ListErrorState'));
            renderSignOffs(false);
        }
    };

    // ─── Render ──────────────────────────────────────────────────────────────
    const statusNode = (status) => (isText(status) && STATUS_KEY[status] ? badge(t(STATUS_KEY[status]), STATUS_CSS[status]) : textOrMissing(status));
    const renderPlan = () => {
        document.getElementById('planName').textContent = isText(plan.name) && plan.name !== '' ? plan.name : t('WorkspaceTitle');
        setCell('detailPlanId', ltr(plan.sandopPlanId, 'font-monospace small'));
        setCell('detailStatus', statusNode(plan.status));
        setCell('detailHorizonStart', textOrMissing(plan.horizonStart));
        setCell('detailHorizonEnd', textOrMissing(plan.horizonEnd));
        setCell('detailDemandPlanId', textOrMissing(plan.demandPlanId));
        setCell('detailDemandPlanVersion', textOrMissing(plan.demandPlanVersion));
        setCell('detailCurrentSnapshot', textOrMissing(plan.currentSnapshotId, 'font-monospace small'));
        setCell('detailCreated', dateTime(plan.createdAt));
    };

    // Workspace tables — the bounded DataTables v2 profile of pack §23.5 (G8 M1): DtDefaults.create(), client-side only,
    // no saved state, search/sort/paging over the returned set. DataTables renders cell strings as HTML, so every server
    // value is escaped here; the sort value of a date is its ISO text, so ordering never depends on the display locale.
    const esc = (value) => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    const missingHtml = () => `<span class="text-muted">${esc(t('NotProvided'))}</span>`;
    const ltrHtml = (text, className) => (isText(text) && text !== ''
        ? `<bdi dir="ltr"${className ? ` class="${className}"` : ''}>${esc(text)}</bdi>` : missingHtml());
    const badgeHtml = (text, css) => `<span class="badge bg-label-${css}">${esc(text)}</span>`;
    const dateHtml = (text) => {
        if (!isText(text) || text === '') return missingHtml();
        const parsed = new Date(text);
        return Number.isNaN(parsed.getTime()) ? ltrHtml(text)
            : `<bdi dir="auto" title="${esc(text)}">${esc(parsed.toLocaleString(document.documentElement.lang || undefined))}</bdi>`;
    };
    const dateColumn = (pick) => ({ data: null, render: (_d, type, row) => (type === 'display' ? dateHtml(pick(row)) : String(pick(row) || '')) });
    const tables = {};
    const fillTable = (id, columns, order, rows) => {
        if (!tables[id]) {
            // Pack §23.5: no export, column visibility or saved view — an explicit layout skips DtDefaults' toolbar entirely
            // (G8 NL-2: an empty button group still left a phantom gap). The page's empty text is set AFTER create(),
            // which otherwise replaces it with the shared DtEmptyTable (G8 NL-1).
            const config = window.DtDefaults.create({
                data: [], columns, order, stateSave: false, serverSide: false,
                layout: {
                    topStart: { features: [{ pageLength: { menu: [10, 25, 50, 100], text: '_MENU_' } }] },
                    topEnd: { features: [{ search: { placeholder: t('Search'), text: '_INPUT_' } }] },
                    bottomStart: { features: ['info'] },
                    bottomEnd: { features: [{ paging: { firstLast: false } }] }
                }
            });
            config.language.emptyTable = t('EmptyState');
            config.language.searchPlaceholder = t('Search');
            tables[id] = new DataTable(document.getElementById(id), config);
        }
        tables[id].clear();
        tables[id].rows.add(rows);
        tables[id].draw();
    };
    // A list that failed shows its error instead of a table — never an empty table (pack §23.5).
    const showList = (hostId, visible) => document.getElementById(hostId)?.classList.toggle('d-none', !visible);

    const renderSnapshots = (loaded) => {
        showList('snapshotsTableHost', loaded);
        if (!loaded) return;
        const provenanceOf = (row) => (row.provenance && typeof row.provenance === 'object' ? row.provenance : {});
        fillTable('dt-sandop-snapshots', [
            dateColumn((row) => row.capturedAt),
            { data: null, render: (_d, _t, row) => `${ltrHtml(provenanceOf(row).demandPlanId)} · ${ltrHtml(provenanceOf(row).demandPlanVersion)}` },
            dateColumn((row) => provenanceOf(row).sourceCapturedAt),
            { data: null, render: (_d, _t, row) => ltrHtml(provenanceOf(row).sourceChecksum, 'font-monospace small text-break') },
            { data: null, render: (_d, type, row) => {
                const count = Array.isArray(row.supplyInputRefs) ? row.supplyInputRefs.length : 0;
                return type === 'display' ? ltrHtml(String(count)) : count;
            } },
            { data: null, orderable: false, render: (_d, _t, row) => (row.snapshotId === plan?.currentSnapshotId ? badgeHtml(t('CurrentMarker'), 'primary') : '') }
        ], [[0, 'desc']], snapshots);
    };

    const renderSignOffs = (loaded) => {
        showList('signOffsTableHost', loaded);
        if (!loaded) return;
        fillTable('dt-sandop-signoffs', [
            { data: null, render: (_d, _t, row) => ltrHtml(row.snapshotId, 'font-monospace small') },
            { data: null, render: (_d, _t, row) => (isText(row.role) && ROLE_KEY[row.role] ? esc(t(ROLE_KEY[row.role])) : ltrHtml(row.role)) },
            { data: null, render: (_d, _t, row) => (isText(row.decision) && DECISION_KEY[row.decision]
                ? badgeHtml(t(DECISION_KEY[row.decision]), row.decision === 'Approved' ? 'success' : 'danger') : ltrHtml(row.decision)) },
            { data: null, render: (_d, _t, row) => (isText(row.comment) && row.comment !== '' ? esc(row.comment) : missingHtml()) },
            // decidedBy is a UUID; a person's name needs a user lookup that is not in scope (UI-SCOPE-0190 §9.5).
            { data: null, render: (_d, _t, row) => ltrHtml(row.decidedBy, 'font-monospace small') },
            dateColumn((row) => row.decidedAt)
        ], [[5, 'desc']], signOffs);
    };

    // Display decision only: the service re-checks state (409) and permission (403).
    const renderActions = () => {
        const status = plan?.status;
        const capture = document.getElementById('btnCaptureSnapshot');
        if (capture) capture.hidden = !(permissions.canCapture && CAPTURE_STATUSES.has(status));
        const signOff = document.getElementById('btnRecordSignOff');
        if (signOff) {
            const allowed = permissions.canSignOff && SIGN_OFF_STATUSES.has(status);
            signOff.hidden = !allowed;
            signOff.disabled = snapshots.length === 0;
            signOff.title = snapshots.length === 0 ? t('SignOffNeedsSnapshot') : '';
        }
    };

    // ─── Mutation intents ────────────────────────────────────────────────────
    const intents = { capture: null, signOff: null };   // { key, blocked } per opened offcanvas
    const pending = { capture: false, signOff: false };

    const send = async (kind, path, body, alertId, submitId, successKey) => {
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
                intents[kind] = null;
                offcanvas(kind === 'capture' ? 'offcanvasCapture' : 'offcanvasSignOff')?.hide();
                window.showToast?.(t(successKey), 'success');
                await load();
                return;
            }
            const failure = await readFailure(response);
            // The plan is gone (or never was the caller's): the workspace content goes with it (pack §23.8).
            if (response.status === 404) { intents[kind] = null; renderSafeNotFound(failure); return; }
            // Denied action: close it and take the button away, with the localized denial (pack §23.8).
            if (response.status === 403) {
                intents[kind] = null;
                offcanvas(kind === 'capture' ? 'offcanvasCapture' : 'offcanvasSignOff')?.hide();
                const button = document.getElementById(kind === 'capture' ? 'btnCaptureSnapshot' : 'btnRecordSignOff');
                if (button) button.hidden = true;
                showAlert('workspaceAlert', failure);
                return;
            }
            if (failure.code === 'IDEMPOTENCY_KEY_REUSED') intent.blocked = true;
            showAlert(alertId, failure);
            if (RELOAD_CODES.has(failure.code) || failure.code === 'INVALID_SNAPSHOT_REFERENCE') await load();
        } catch (_) {
            // Outcome unknown: the same intent (same key, same body) may be retried.
            showAlert(alertId, { status: 0, code: 'DEPENDENCY_UNAVAILABLE', reference: '' });
        } finally {
            pending[kind] = false;
            submit.disabled = Boolean(intents[kind]?.blocked);
        }
    };

    // Capture snapshot — CaptureSandopSnapshotRequest. The demand reference starts from the plan's; values sent as typed.
    const CAPTURE_FIELDS = ['captureDemandPlanId', 'captureDemandPlanVersion', 'captureSourceCapturedAt', 'captureSourceChecksum'];
    const addRefRow = () => {
        const template = document.getElementById('supplyInputRefTemplate');
        const row = template.content.firstElementChild.cloneNode(true);
        row.querySelector('.js-ref-remove').addEventListener('click', () => row.remove());
        document.getElementById('supplyInputRefs').appendChild(row);
        row.querySelector('.js-ref-source')?.focus({ preventScroll: true });
    };
    const openCapture = () => {
        hideAlert('formCaptureAlert');
        CAPTURE_FIELDS.forEach((id) => { const el = document.getElementById(id); if (el) el.value = ''; setInvalid(el, ''); });
        document.getElementById('captureDemandPlanId').value = isText(plan?.demandPlanId) ? plan.demandPlanId : '';
        document.getElementById('captureDemandPlanVersion').value = isText(plan?.demandPlanVersion) ? plan.demandPlanVersion : '';
        document.getElementById('supplyInputRefs').replaceChildren();
        intents.capture = { key: uuid(), blocked: false };
        document.getElementById('btnSubmitCapture').disabled = false;
        offcanvas('offcanvasCapture')?.show();
    };
    // Mirrors the contract only: 4 scalars required; each reference row needs all three values. No trim.
    const submitCapture = () => {
        hideAlert('formCaptureAlert');
        let valid = true;
        CAPTURE_FIELDS.forEach((id) => {
            const el = document.getElementById(id);
            const missing = value(id) === '';
            setInvalid(el, missing ? t('FieldRequired') : '');
            if (missing) valid = false;
        });
        const refs = [];
        document.querySelectorAll('#supplyInputRefs .js-ref-row').forEach((row) => {
            const [source, resourceId, resourceVersion] = ['.js-ref-source', '.js-ref-resource-id', '.js-ref-resource-version'].map((selector) => {
                const input = row.querySelector(selector);
                const missing = String(input.value) === '';
                input.classList.toggle('is-invalid', missing);
                input.setAttribute('aria-invalid', missing ? 'true' : 'false');
                if (missing) valid = false;
                return String(input.value);
            });
            refs.push({ source, resourceId, resourceVersion });
        });
        if (!valid) { showAlert('formCaptureAlert', t('FieldRequired')); return; }
        const body = JSON.stringify({
            demandPlanId: value('captureDemandPlanId'),
            demandPlanVersion: value('captureDemandPlanVersion'),
            sourceCapturedAt: value('captureSourceCapturedAt'),
            sourceChecksum: value('captureSourceChecksum'),
            supplyInputRefs: refs
        });
        void send('capture', `${endpoint}/${planId}/snapshots`, body, 'formCaptureAlert', 'btnSubmitCapture', 'SnapshotCaptured');
    };

    // Record sign-off — RecordSignOffRequest. A decision is final (append-only), so it asks once more.
    const fillSnapshotSelect = () => {
        const select = document.getElementById('signOffSnapshot');
        select.replaceChildren();
        snapshots.forEach((snapshot) => {
            const option = document.createElement('option');
            option.value = snapshot.snapshotId;
            const captured = new Date(snapshot.capturedAt);
            const when = Number.isNaN(captured.getTime()) ? String(snapshot.capturedAt || '') : captured.toLocaleString(document.documentElement.lang || undefined);
            option.textContent = `${when} · ${snapshot.snapshotId}${snapshot.snapshotId === plan?.currentSnapshotId ? ` (${t('CurrentMarker')})` : ''}`;
            option.selected = snapshot.snapshotId === plan?.currentSnapshotId;
            select.appendChild(option);
        });
    };
    const openSignOff = () => {
        if (snapshots.length === 0) { window.showToast?.(t('SignOffNeedsSnapshot'), 'warning'); return; }
        hideAlert('formSignOffAlert');
        fillSnapshotSelect();
        ['signOffRole', 'signOffDecision', 'signOffComment'].forEach((id) => { const el = document.getElementById(id); el.value = ''; setInvalid(el, ''); });
        intents.signOff = { key: uuid(), blocked: false };
        document.getElementById('btnSubmitSignOff').disabled = false;
        offcanvas('offcanvasSignOff')?.show();
    };
    const submitSignOff = () => {
        hideAlert('formSignOffAlert');
        let valid = true;
        ['signOffSnapshot', 'signOffRole', 'signOffDecision'].forEach((id) => {
            const missing = value(id) === '';
            setInvalid(document.getElementById(id), missing ? t('FieldRequired') : '');
            if (missing) valid = false;
        });
        // Contract mirror, no tightening (pack §23.6): comment maxLength 2000.
        const commentInput = document.getElementById('signOffComment');
        const tooLong = value('signOffComment').length > 2000;
        setInvalid(commentInput, tooLong ? t('ValidationCommentLength') : '');
        if (tooLong) valid = false;
        if (!valid) return;
        const payload = { snapshotId: value('signOffSnapshot'), role: value('signOffRole'), decision: value('signOffDecision') };
        const comment = value('signOffComment');
        if (comment !== '') payload.comment = comment;
        const body = JSON.stringify(payload);
        const execute = () => { void send('signOff', `${endpoint}/${planId}/sign-offs`, body, 'formSignOffAlert', 'btnSubmitSignOff', 'SignOffRecorded'); };
        window.showConfirm?.(t('SignOffFinalNote'), execute, { confirmButtonText: t('RecordSignOff') });
    };

    // ─── Init ────────────────────────────────────────────────────────────────
    const init = () => {
        L = window.L10n || L;
        if (!UUID_PATTERN.test(planId)) { renderSafeNotFound({ status: 404, code: 'UNKNOWN_SANDOP_PLAN', reference: '' }); return; }
        if (!UUID_PATTERN.test(legalEntityId)) { hideSkeleton(); document.getElementById('planName').textContent = t('WorkspaceTitle'); showAlert('workspaceAlert', t('LegalEntityRequired')); return; }
        document.getElementById('btnCaptureSnapshot')?.addEventListener('click', openCapture);
        document.getElementById('btnAddSupplyInputRef')?.addEventListener('click', addRefRow);
        document.getElementById('btnSubmitCapture')?.addEventListener('click', submitCapture);
        document.getElementById('btnRecordSignOff')?.addEventListener('click', openSignOff);
        document.getElementById('btnSubmitSignOff')?.addEventListener('click', submitSignOff);
        document.getElementById('offcanvasCapture')?.addEventListener('hidden.bs.offcanvas', () => { if (!pending.capture) intents.capture = null; });
        document.getElementById('offcanvasSignOff')?.addEventListener('hidden.bs.offcanvas', () => { if (!pending.signOff) intents.signOff = null; });
        void load();
    };

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
