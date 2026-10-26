/**
 * MOD-0175 Q5 — the shared helpers of the quarantine pages (pack §11.2). No quarantine rule lives here: the server decides
 * (QUARANTINE-BUNDLE 1.0.0). This file holds the vocabulary reader, formatting, the same-origin calls (/Quarantine/api/** —
 * antiforgery + Idempotency-Key + If-Match), the status badges, the tab counts, and the ONE sign-and-send step shared by Karar
 * Ver, Disposition, Blokeyi Kaldır and Karar Gönder:
 *   decision.signatureRequired → ESign.open({ module: 'quarantine', subjectType: 'quarantine.decision', subjectId, actionCode })
 *   (MOD-0022 E5W1 dialog, CT-D17 — 0175 draws no password field, KQ-G, and keeps only the signature id) → submitDecision.
 * A cancelled signature sends nothing. Buttons are shown by action code (can) — never by a permission key.
 */
'use strict';

window.QuarantineUi = (function () {
    const actions = (() => {
        try { return JSON.parse(document.getElementById('quarantine-actions')?.textContent || '{}'); } catch (error) { return {}; }
    })();
    const L = () => window.QuarantineL10n || {};
    const t = (key, ...args) => String(L()[key] || '').replace(/\{(\d)\}/g, (m, i) => `${args[i] ?? ''}`);
    const has = (key) => !!L()[key];
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const lang = document.documentElement.lang || undefined;
    const numberFormat = new Intl.NumberFormat(lang, { maximumFractionDigits: 6 });
    const qty = (value) => {
        const n = Number(value);
        return Number.isFinite(n) ? numberFormat.format(n) : String(value ?? '');
    };
    const date = (wire) => {
        if (!wire) return '';
        const d = new Date(wire);
        return Number.isNaN(d.getTime()) ? String(wire) : d.toLocaleDateString(lang, { year: 'numeric', month: '2-digit', day: '2-digit' });
    };
    const dateTime = (wire) => {
        if (!wire) return '';
        const d = new Date(wire);
        return Number.isNaN(d.getTime()) ? String(wire) : d.toLocaleString(lang, { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
    };
    const daysUntil = (wire) => {
        if (!wire) return null;
        const today = new Date(new Date().toISOString().slice(0, 10) + 'T00:00:00Z');
        const day = new Date(String(wire).slice(0, 10) + 'T00:00:00Z');
        return Math.round((day - today) / 86400000);
    };
    const newKey = () => 'q5-' + (window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(16).slice(2)}`).replace(/[^A-Za-z0-9-]/g, '');
    const antiforgery = () => document.querySelector('#qr-antiforgery input[name="__RequestVerificationToken"]')?.value || '';
    const byId = (id) => document.getElementById(id);

    // ── calls ───────────────────────────────────────────────────────────────────────────────────────────────────
    const call = async (method, url, body, options) => {
        const headers = { Accept: 'application/json' };
        if (method !== 'GET') {
            headers['Content-Type'] = 'application/json';
            headers.RequestVerificationToken = antiforgery();
        }
        if (options?.idempotencyKey) headers['Idempotency-Key'] = options.idempotencyKey;
        if (options?.ifMatch) headers['If-Match'] = options.ifMatch;
        try {
            const res = await fetch(url, { method, credentials: 'same-origin', headers, body: body === undefined ? undefined : JSON.stringify(body) });
            const json = await res.json().catch(() => null);
            return { ok: res.ok, status: res.status, json, etag: res.headers.get('ETag') };
        } catch (error) {
            return { ok: false, status: 0, json: null, etag: null };
        }
    };
    const get = (url) => call('GET', url);
    const api = (path) => `/Quarantine/api/${path.replace(/^\//, '')}`;
    /** A write of QUARANTINE-BUNDLE: the Idempotency-Key header equals the body's idempotencyKey (contract x-rules 1). */
    const write = (method, path, body, ifMatch) => {
        const key = body?.idempotencyKey || newKey();
        const payload = body ? Object.assign({}, body, { idempotencyKey: key }) : body;
        return call(method, api(path), payload, { idempotencyKey: key, ifMatch });
    };
    /** A write the contract gives no Idempotency-Key (saveSettings, block reasons): the body goes as it is. */
    const send = (method, path, body, ifMatch) => call(method, api(path), body, { ifMatch });
    const codeOf = (reply) => reply?.json?.error?.code || null;
    const errorText = (reply) => {
        const code = codeOf(reply);
        if (code && has(`Quarantine.Errors.${code}`)) return t(`Quarantine.Errors.${code}`);
        if (reply?.status === 0 || reply?.status >= 500) return t('Quarantine.Errors.SERVICE_UNAVAILABLE');
        if (reply?.status === 401 || reply?.status === 403) return t('Quarantine.Errors.FORBIDDEN');
        return t('Quarantine.Errors.GENERIC');
    };
    const showError = (box, reply) => {
        if (!box) return;
        box.textContent = typeof reply === 'string' ? reply : errorText(reply);
        box.classList.remove('d-none');
        box.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    };
    const hideError = (box) => box?.classList.add('d-none');
    const toast = (message, type) => window.showToast?.(message, type || 'success');

    // ── badges (mockup colours: Karantina amber, Kalite Kontrol cyan, Bloke red, Kullanılabilir green) ─────────────
    const STOCK = { QUARANTINE: 'bg-label-warning', QUALITY_INSPECTION: 'bg-label-info', BLOCKED: 'bg-label-danger', AVAILABLE: 'bg-label-success' };
    const stockBadge = (status) => status ? `<span class="badge ${STOCK[status] || 'bg-label-secondary'}">${esc(t('Quarantine.Status.' + status) || status)}</span>` : '';
    const PENDING = ['Submitted', 'PendingApproval', 'Approved', 'ApplyFailed', 'StartFailed', 'Draft'];
    /** Durum column: Bekliyor / İncelemede / Onay bekliyor / Karar Verildi (§11.3; "Onay bekliyor" = a decision in flight, §21.3 #1). */
    const holdState = (hold) => {
        if (hold?.status === 'Decided') return 'Decided';
        const state = hold?.activeDecision?.state;
        if (state && state !== 'Draft' && PENDING.includes(state)) return 'Pending';
        return hold?.reviewState === 'InReview' ? 'InReview' : 'Waiting';
    };
    const HOLD = { Waiting: 'bg-label-secondary', InReview: 'bg-label-primary', Pending: 'bg-label-warning', Decided: 'bg-label-success' };
    const holdBadge = (hold) => {
        const s = holdState(hold);
        return `<span class="badge ${HOLD[s]}">${esc(t('Quarantine.HoldState.' + s))}</span>`;
    };
    const DECISION = {
        Draft: 'bg-label-secondary', Submitted: 'bg-label-info', PendingApproval: 'bg-label-warning', Approved: 'bg-label-info',
        Applied: 'bg-label-success', Rejected: 'bg-label-danger', Cancelled: 'bg-label-secondary', TimedOut: 'bg-label-secondary',
        Withdrawn: 'bg-label-secondary', StartFailed: 'bg-label-danger', ApplyFailed: 'bg-label-danger', FourEyesViolation: 'bg-label-danger'
    };
    const decisionBadge = (state) => state ? `<span class="badge ${DECISION[state] || 'bg-label-secondary'}">${esc(t('Quarantine.Workflow.' + state) || state)}</span>` : '';
    const lotUrl = (lotId) => `/Lots/Details?${new URLSearchParams({ lotId })}`;
    const lotLink = (lot) => {
        if (!lot?.lotId) return '<span class="text-muted">—</span>';
        const text = esc(lot.lotNumber || lot.lotId);
        return /^[0-9a-f-]{36}$/i.test(lot.lotId) ? `<a class="font-monospace" href="${lotUrl(lot.lotId)}">${text}</a>` : `<span class="font-monospace">${text}</span>`;
    };
    const productCell = (row, sub) => `<div class="d-flex flex-column"><span class="fw-medium text-heading">${esc(row.itemLabel || row.item?.itemId || '')}</span>`
        + `<small class="text-muted">${esc([row.skuLabel, sub].filter(Boolean).join(' · '))}</small></div>`;
    const whereCell = (row) => `<div class="d-flex flex-column"><span>${esc(row.warehouseLabel || '')}</span><small class="text-muted">${esc(row.locationLabel || '')}</small></div>`;
    const uom = (row) => row?.baseUom?.code || row?.baseUom?.id || '';

    // ── tab counts (the strip of _Header.cshtml; the shown list writes its own count from its response) ───────────
    const COUNT_DOORS = { QUARANTINEQUEUE: '/Quarantine/list/holds', BLOCKEDSTOCK: '/Quarantine/list/blocked', RETESTQUEUE: '/Quarantine/list/retest' };
    const setCount = (code, value) => {
        const chip = document.querySelector(`[data-qr-count="${code}"]`);
        if (chip) chip.textContent = value == null ? '' : String(value);
    };
    const loadCounts = (except) => {
        Object.entries(COUNT_DOORS).filter(([code]) => code !== except && document.querySelector(`[data-qr-count="${code}"]`)).forEach(async ([code, url]) => {
            const reply = await get(`${url}?start=0&length=1`);
            if (reply.ok) setCount(code, reply.json?.data?.total ?? '');
        });
    };

    // ── reason counter ("n / en az 10") ─────────────────────────────────────────────────────────────────────────
    const counter = (textarea) => {
        const out = document.querySelector(`[data-counter="${textarea.id}"]`);
        const update = () => { if (out) out.textContent = t('Quarantine.Common.ReasonCounter', textarea.value.trim().length); };
        textarea.addEventListener('input', update);
        update();
    };
    const fieldError = (scope, name, text) => {
        const el = scope.querySelector(`[data-err="${name}"]`);
        if (el) el.textContent = text || '';
    };
    const clearFieldErrors = (scope) => scope.querySelectorAll('[data-err]').forEach((el) => { el.textContent = ''; });

    // ── the decision to be signed (step 2 summary — the SERVER's draft, never the form's copy) ────────────────────
    const summaryHtml = (decision, record) => {
        const unit = record ? uom(record) : (decision.baseUom || '');
        const rows = [];
        rows.push(`<div class="d-flex flex-wrap justify-content-between gap-2 mb-2"><span class="fw-medium">${esc(t('Quarantine.DecisionKind.' + decision.kind))}</span>`
            + `<span class="font-monospace text-muted">${esc(decision.decisionNumber || '')}</span></div>`);
        (decision.lines || []).forEach((line) => {
            rows.push(`<div class="d-flex flex-wrap align-items-center gap-2 mb-1"><span>${esc(t('Quarantine.LineDecision.' + line.decision))}</span>`
                + `<span class="fw-medium" dir="ltr">${esc(qty(line.quantity))} ${esc(unit)}</span><i class="bx bx-right-arrow-alt text-muted"></i>${stockBadge(line.toStockStatus)}</div>`);
        });
        if (decision.disposition) {
            rows.push(`<div class="mb-1">${esc(t('Quarantine.Disposition.' + decision.disposition.kind))} · <span class="fw-medium" dir="ltr">${esc(qty(decision.disposition.quantity))} ${esc(unit)}</span></div>`);
        }
        if (decision.kind === 'LotRelease') {
            rows.push(`<div class="mb-1">${esc(t('Quarantine.Submit.LotRelease', decision.lotNumber || decision.lotId || ''))}</div>`);
        }
        (decision.plannedMovements || []).forEach((m) => {
            rows.push(`<div class="small text-muted mb-1"><i class="bx bx-transfer-alt me-1"></i>${esc(t('Quarantine.Sign.Movement'))}: ${esc(t('Quarantine.MovementType.' + m.movementType) || m.movementType)}`
                + ` · <span dir="ltr">${esc(qty(m.quantity))} ${esc(unit)}</span>`
                + (m.fromStockStatus || m.toStockStatus ? ` · ${stockBadge(m.fromStockStatus)} <i class="bx bx-right-arrow-alt"></i> ${stockBadge(m.toStockStatus)}` : '') + '</div>');
        });
        if (decision.blockReasonCode) rows.push(`<div class="small mb-1">${esc(t('Quarantine.Field.BlockReason'))}: <span class="font-monospace">${esc(decision.blockReasonCode)}</span></div>`);
        if (decision.newRetestDate) rows.push(`<div class="small mb-1">${esc(t('Quarantine.Decide.Field.NewRetest'))}: ${esc(date(decision.newRetestDate))}</div>`);
        if (decision.reason) rows.push(`<div class="small text-muted mt-2">${esc(t('Quarantine.Sign.ReasonLine', decision.reason))}</div>`);
        return rows.join('');
    };

    /**
     * Does the policy ask a signature for this decision? The draft carries no signatureRequired yet (the contract fills it at
     * submit), so the MOD-0022 effective policy of the decision's own action is read — the same read the dialog makes
     * (GET /ESignature/api/policy/effective, read only). An unreadable policy counts as REQUIRED (the dialog then decides).
     */
    const signatureRequired = async (decision) => {
        if (typeof decision.signatureRequired === 'boolean') return decision.signatureRequired;
        const query = new URLSearchParams({ module: 'quarantine', subjectType: 'quarantine.decision', subjectId: decision.decisionId });
        if (decision.esignActionCode) query.set('actionCode', decision.esignActionCode);
        const reply = await get(`/ESignature/api/policy/effective?${query}`);
        const policy = reply.json?.data ?? reply.json;
        decision.signatureRequired = !(reply.ok && policy && policy.required === false);
        return decision.signatureRequired;
    };

    /** Step 2 of the two-step pages: shows the server's draft and whether the policy asks for a signature. */
    const showSignStep = async (decision, record) => {
        byId('qr-summary').innerHTML = summaryHtml(decision, record);
        byId('qr-sign-step').classList.remove('d-none');
        const required = await signatureRequired(decision);
        byId('qr-step-skipped')?.classList.toggle('d-none', required);
        const info = byId('qr-sign-info-text');
        if (info) info.textContent = required ? t('Quarantine.Sign.Info') : t('Quarantine.Sign.NotRequired');
        document.querySelectorAll('[data-qr-step]').forEach((step) => {
            const on = step.getAttribute('data-qr-step') === '2';
            step.classList.toggle('text-muted', !on);
            const dot = step.querySelector('[data-qr-step-dot]');
            dot?.classList.toggle('bg-primary', on);
            dot?.classList.toggle('bg-label-secondary', !on);
        });
        return required;
    };

    /**
     * Sign (when the server says so) and send. Returns { ok, decision } | { ok: false, cancelled } | { ok: false, reply }.
     * The signature id is the ONLY thing 0175 keeps of the signature (KQ-B). A cancelled dialog sends nothing.
     */
    const signAndSubmit = async (decision) => {
        let signatureId;
        if (await signatureRequired(decision)) {
            if (!window.ESign?.open) return { ok: false, reply: { status: 503, json: { error: { code: 'ESIGN_UNAVAILABLE' } } } };
            const signature = await window.ESign.open({
                module: 'quarantine',
                subjectType: 'quarantine.decision',
                subjectId: decision.decisionId,
                actionCode: decision.esignActionCode || undefined
            });
            if (!signature?.signatureId) return { ok: false, cancelled: true };
            signatureId = signature.signatureId;
        }
        const body = signatureId ? { signatureId } : {};
        const reply = await write('POST', `decisions/${encodeURIComponent(decision.decisionId)}/submit`, body);
        return reply.ok ? { ok: true, decision: reply.json } : { ok: false, reply };
    };

    /** "Onaya gönderildi" (202 PendingApproval): the stock moves only after the approval (§21.3 #1). */
    const announceSent = async (decision, then) => {
        toast(t('Quarantine.Sent.Toast'), 'success');
        await window.DitenModal?.success({ title: t('Quarantine.Sent.Title'), message: t('Quarantine.Sent.Text', decision?.decisionNumber || '') });
        if (then) window.location.href = then;
    };

    /**
     * D-KAR-1 (owner decision A, 2026-10-19; was FU-0175-Q5-02's "short id · unknown user"): a UserRef is shown by the ONE person rule of
     * MVP-1 — the shared StockMovementPeople rule (people.js, no copy): the name, else the localized "unknown user"; the id is never shown as
     * text, only as the hover title of an unnamed person. person = the text, personHtml = the escaped markup with that title.
     */
    const personRow = (ref) => ({ createdById: ref?.userId || '', createdByName: ref?.name || '' });
    const person = (ref) => window.StockMovementPeople.text(personRow(ref), t('Quarantine.Common.UnknownUser'));
    const personHtml = (ref) => window.StockMovementPeople.html(personRow(ref), t('Quarantine.Common.UnknownUser'));

    /** WP-0174-ITEM: the SKU level in the reader's language (Item = a material item that is its own SKU). */
    const levelLabel = (level) => (level ? t('Quarantine.Level.' + level) || level : '');

    return {
        t, has, esc, qty, date, dateTime, daysUntil, newKey, byId, person, personHtml, levelLabel,
        can: (code) => actions[code] === true,
        get, api, write, send, codeOf, errorText, showError, hideError, toast,
        stockBadge, holdState, holdBadge, decisionBadge, lotUrl, lotLink, productCell, whereCell, uom,
        setCount, loadCounts, counter, fieldError, clearFieldErrors,
        summaryHtml, signatureRequired, showSignStep, signAndSubmit, announceSent
    };
})();
