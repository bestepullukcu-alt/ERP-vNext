'use strict';

/*
 * MOD-0022 E5W1 — the ONE signature dialog (pack §11.1–§11.4, §12 UI rules; mockup v2 "MODAL · İMZA PENCERESİ", v2.app.js:276-299,
 * 396-425). Markup: Views/Shared/ESignature/_ESignatureDialog.cshtml. Vocabulary: window.ESignL10n (_ESignatureL10n.cshtml).
 *
 *   ESign.open({ module, subjectType, subjectId, actionCode?, meaningCode?, requestId?, context? })
 *     → Promise<{ signatureId, signatureNumber } | null>
 *
 * The HOST GIVES ONLY IDS. Everything the signer sees comes from the server (GET /api/v1/esign/policy/effective): the subject's
 * display + summary (the owner module's provider), the allowed meanings, the policy (required, fixed meaning, visual mode,
 * re-auth every time / window), the signer's own sample and the signer's login / name. `context` is an object, or a function of
 * the server's SignableSubject (subject.fields) → object, so a host that knows only the subject id (WorkCenter: the task) takes the
 * rest of its binding context from the server's answer instead of inventing it.
 *
 * Flow (pack §11.3): subject → meaning → user (read-only) + password (or, inside an open KS-3 window, the user name re-typed — CT-D7)
 * → visual appearance (None / Optional / SavedAccepted / DrawRequired, §12 disabled reasons) → AuthService esign/reauth (or
 * esign/ticket) → Platform POST signatures → success. Locked (423) and session time-out (401) are their own phases.
 *
 * SECRETS (ESIGN-BUNDLE x-rules 8, pack §16 E5): the password is read from the input once, sent once, and the input is emptied in
 * a `finally` after EVERY attempt and on close. Nothing is written to localStorage / sessionStorage / the URL / the console; the
 * single-use ticket lives only in the local variable of the submit call. What is remembered between dialogs on this page is the
 * window's time stamps (no secret), so the window line can be shown.
 */
(function (global) {
    const API = '/ESignature/api';
    const AUTH = '/ESignature/auth';
    const doc = global.document;
    const lang = ((doc.documentElement.getAttribute('lang') || 'tr').slice(0, 2) || 'tr').toLowerCase();

    // ── vocabulary ──────────────────────────────────────────────────────────────────────────────────────────────
    function t(key, ...args) {
        const all = global.ESignL10n || {};
        let text = all[key];
        if (!text) {
            console.warn(`[L10N WARNING] Missing localization key: ${key}`);
            text = key;
        }
        return args.reduce((acc, value, i) => acc.split(`{${i}}`).join(value == null ? '' : String(value)), text);
    }

    /** The error sentence of an ESIGN_* code (ESign.Errors.*), or the generic one. */
    function message(code) {
        const key = code ? `ESign.Errors.${code}` : null;
        return key && (global.ESignL10n || {})[key] ? t(key) : t('ESign.Dialog.Err.Generic');
    }

    // ── time: the signer's own clock with its offset (pack §9 "UTC+03:00"; storage stays UTC) ─────────────────────────
    function offsetLabel(date) {
        const minutes = -date.getTimezoneOffset();
        const sign = minutes >= 0 ? '+' : '-';
        const abs = Math.abs(minutes);
        return `UTC${sign}${String(Math.floor(abs / 60)).padStart(2, '0')}:${String(abs % 60).padStart(2, '0')}`;
    }
    function formatTime(utc, withOffset) {
        if (!utc) return '—';
        const d = new Date(utc);
        if (Number.isNaN(d.getTime())) return '—';
        const text = d.toLocaleString(doc.documentElement.getAttribute('lang') || undefined, {
            year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit'
        });
        return withOffset ? `${text} ${offsetLabel(d)}` : text;
    }
    const hhmm = (utc) => {
        const d = new Date(utc);
        return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
    };

    // ── HTTP (same-origin proxy ESignatureController; envelopes per ESIGN-BUNDLE x-implementation-notes) ───────────────
    async function call(method, url, body) {
        let res;
        try {
            res = await fetch(url, {
                method,
                credentials: 'same-origin',
                headers: { 'Accept': 'application/json', 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                body: body === undefined ? undefined : JSON.stringify(body)
            });
        } catch (_) {
            return { ok: false, status: 0, json: null };
        }
        let json = null;
        try { json = await res.json(); } catch (_) { json = null; }
        return { ok: res.ok, status: res.status, json };
    }
    const dataOf = (r) => (r.json && typeof r.json === 'object' && 'data' in r.json ? r.json.data : r.json);
    const codeOf = (r) => (r.json && (r.json.reason_code || r.json.reasonCode
        || (Array.isArray(r.json.errorCodes) && r.json.errorCodes[0] && r.json.errorCodes[0].code))) || null;
    const paramsOf = (r) => (r.json && Array.isArray(r.json.errorCodes) && r.json.errorCodes[0] && r.json.errorCodes[0].params) || {};
    const newKey = () => 'esign-' + (global.crypto && global.crypto.randomUUID ? global.crypto.randomUUID() : String(Date.now()) + Math.random().toString(16).slice(2));

    // ── state ───────────────────────────────────────────────────────────────────────────────────────────────────
    /** KS-3 password window of THIS page: time stamps only (never the password, never a ticket). */
    let passwordWindow = null; // { verifiedAtUtc, expiresAtUtc }
    let session = null;
    let modal = null;
    let pad = null;

    const $ = (id) => doc.getElementById(id);
    const root = () => $('esignDialog');

    function windowOpen() {
        return !!(passwordWindow && passwordWindow.expiresAtUtc && Date.now() < new Date(passwordWindow.expiresAtUtc).getTime());
    }

    function setPhase(phase) {
        const el = root();
        el.querySelectorAll('[data-esign-phase]').forEach((b) => b.classList.toggle('d-none', b.getAttribute('data-esign-phase') !== phase));
        el.querySelectorAll('[data-esign-for]').forEach((b) => b.classList.toggle('d-none', !b.getAttribute('data-esign-for').split(' ').includes(phase)));
        if (session) session.phase = phase;
    }

    /** Empties the password input — after EVERY attempt, on every phase change and on close (pack §16 E5). */
    function clearPassword() {
        const input = $('esignPassword');
        if (input) input.value = '';
    }

    function fieldError(inputId, errorId, text) {
        const input = $(inputId);
        const error = $(errorId);
        if (input) input.classList.toggle('is-invalid', !!text);
        if (error) error.textContent = text || '';
    }
    function formError(text) {
        const el = $('esignFormError');
        el.textContent = text || '';
        el.classList.toggle('d-none', !text);
    }
    function appearanceError(text) {
        const el = $('esignAppearanceError');
        el.textContent = text || '';
        el.classList.toggle('d-none', !text);
        const surface = root().querySelector('#esignDialogPad [data-esign-pad-surface]');
        if (surface) surface.classList.toggle('is-invalid', !!text && session && session.appearance === 'draw');
    }

    // ── open / close ───────────────────────────────────────────────────────────────────────────────────────────
    function open(options) {
        const o = options || {};
        if (!o.module || !o.subjectType || !o.subjectId) {
            return Promise.reject(new Error('ESign.open needs module, subjectType and subjectId.'));
        }
        if (!root()) {
            return Promise.reject(new Error('The e-signature dialog partial (_ESignatureDialog) is not on this page.'));
        }
        if (session) finish(null);
        return new Promise((resolve) => {
            session = { options: o, resolve, phase: 'loading', policy: null, appearance: null, result: null, busy: false };
            wire();
            reset();
            setPhase('loading');
            modal = modal || global.bootstrap.Modal.getOrCreateInstance(root());
            modal.show();
            load();
        });
    }

    function finish(value) {
        if (!session) return;
        const s = session;
        session = null;
        clearPassword();
        if (pad) pad.clear();
        if (modal) modal.hide();
        s.resolve(value);
    }

    function close() {
        if (!session || session.busy) return;
        finish(session.phase === 'success' && session.result ? session.result : null);
    }

    function reset() {
        clearPassword();
        formError('');
        appearanceError('');
        fieldError('esignPassword', 'esignPasswordError', '');
        fieldError('esignRetypeUser', 'esignRetypeError', '');
        fieldError('esignMeaning', 'esignMeaningError', '');
        $('esignRetypeUser').value = '';
        $('esignNote').value = '';
        if (pad) pad.clear();
        busy(false);
    }

    function busy(on) {
        if (session) session.busy = on;
        const btn = $('esignSubmit');
        if (!btn) return;
        btn.disabled = !!on;
        btn.innerHTML = on
            ? `<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>${escapeHtml(t('ESign.Dialog.Signing'))}`
            : `<i class="bx bx-pen me-1"></i>${escapeHtml(t('ESign.Dialog.Sign'))}`;
    }

    function escapeHtml(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
    }

    let wired = false;
    function wire() {
        if (wired) return;
        wired = true;
        const el = root();
        el.querySelectorAll('[data-esign-close]').forEach((b) => b.addEventListener('click', close));
        $('esignDialogForm').addEventListener('submit', (e) => { e.preventDefault(); submit(); });
        $('esignMeaning').addEventListener('change', () => { fieldError('esignMeaning', 'esignMeaningError', ''); describeMeaning(); });
        $('esignPassword').addEventListener('input', () => fieldError('esignPassword', 'esignPasswordError', ''));
        $('esignRetypeUser').addEventListener('input', () => fieldError('esignRetypeUser', 'esignRetypeError', ''));
        el.addEventListener('hidden.bs.modal', () => { if (session) finish(session.phase === 'success' ? session.result : null); });
        pad = global.ESignPad.attach($('esignDialogPad'));
        pad.onChange(() => appearanceError(''));
        const relogin = $('esignRelogin');
        relogin.addEventListener('click', () => {
            relogin.setAttribute('href', '/account/login?returnUrl=' + encodeURIComponent(global.location.pathname + global.location.search));
        });
    }

    // ── load + render (everything from the server) ─────────────────────────────────────────────────────────────────
    async function load() {
        const s = session;
        const o = s.options;
        const q = new URLSearchParams({ module: o.module, subjectType: o.subjectType, subjectId: o.subjectId });
        if (o.actionCode) q.set('actionCode', o.actionCode);
        const r = await call('GET', `${API}/policy/effective?${q.toString()}`);
        if (session !== s) return;
        if (r.status === 401) { setPhase('timeout'); return; }
        if (!r.ok) {
            const text = r.status === 403 ? t('ESign.Dialog.Err.Forbidden') : message(codeOf(r));
            finish(null);
            showError(text);
            return;
        }
        s.policy = dataOf(r) || {};
        render();
    }

    function showError(text) {
        if (global.DitenModal && global.DitenModal.error) {
            global.DitenModal.error({ title: t('ESign.Dialog.Err.Title'), message: text, confirmButtonText: t('ESign.Dialog.Close') });
        } else if (global.showToast) {
            global.showToast(text, 'error');
        }
    }

    function meaningName(m) {
        const names = (m && m.names) || {};
        return names[lang] || names.en || (m && m.code) || '';
    }

    function render() {
        const p = session.policy;
        const o = session.options;
        const subject = p.subject || {};
        const moduleKey = `ESign.Module.${String(o.module).replace(/[^a-z0-9]/gi, '').toLowerCase()}`;
        $('esignModule').textContent = (global.ESignL10n || {})[moduleKey] ? t(moduleKey) : o.module;
        $('esignSubjectTitle').textContent = subject.display || o.subjectId;
        $('esignSummary').innerHTML = (subject.summary || []).map((line) =>
            `<dt class="col-5 text-muted fw-normal">${escapeHtml(line.label)}</dt><dd class="col-7 mb-1">${escapeHtml(line.value)}</dd>`).join('');

        const signer = p.signer || {};
        $('esignUserName').value = [signer.login, signer.name].filter(Boolean).join(' · ');
        if (pad) pad.setName(signer.name || signer.login || '');

        // meanings: the policy's allowed list; a fixed policy meaning locks the select (V5); the host may only pre-select
        const select = $('esignMeaning');
        const meanings = (p.allowedMeanings || []).filter((m) => m.isActive !== false);
        select.innerHTML = `<option value="">${escapeHtml(t('ESign.Dialog.MeaningSelect'))}</option>`
            + meanings.map((m) => `<option value="${escapeHtml(m.code)}">${escapeHtml(meaningName(m))}</option>`).join('');
        const preferred = p.meaningCode || o.meaningCode;
        select.value = meanings.some((m) => m.code === preferred) ? preferred : (meanings.length === 1 ? meanings[0].code : '');
        select.disabled = !!(p.meaningCode && meanings.some((m) => m.code === p.meaningCode));
        describeMeaning();

        $('esignVisualMode').textContent = t('ESign.Dialog.AppearancePolicy', t(`ESign.Dialog.VisualMode.${p.visualMode || 'optional'}`));
        session.appearance = defaultAppearance();
        renderAppearance();
        renderCredential();
        setPhase('form');
        global.setTimeout(() => {
            const focus = !$('esignPasswordGroup').classList.contains('d-none') ? $('esignPassword') : $('esignRetypeUser');
            if (focus) focus.focus();
            if (pad) pad.redraw();
        }, 200);
    }

    function describeMeaning() {
        const code = $('esignMeaning').value;
        const m = ((session && session.policy && session.policy.allowedMeanings) || []).find((x) => x.code === code);
        $('esignMeaningDesc').textContent = (m && m.description) || '';
    }

    /** §12 UI rules: the three appearance options and why one is off (mockup v2.app.js:403-410). */
    function appearanceOptions() {
        const p = session.policy;
        const mode = p.visualMode || 'optional';
        const sample = p.mySample && p.mySample.status === 'active' ? p.mySample : null;
        const savedReason = mode === 'drawRequired' ? t('ESign.Dialog.Dis.DrawRequired')
            : mode === 'none' ? t('ESign.Dialog.Dis.None')
                : !sample ? (p.myPendingSample ? t('ESign.Dialog.Dis.Pending') : t('ESign.Dialog.Dis.NoSample')) : null;
        return [
            { key: 'saved', label: t('ESign.Dialog.App.Saved'), off: savedReason, sub: sample ? t('ESign.Dialog.App.SavedSub', sample.sampleNumber || '') : '', sample },
            { key: 'draw', label: t('ESign.Dialog.App.Draw'), off: mode === 'none' ? t('ESign.Dialog.Dis.None') : null, sub: t('ESign.Dialog.App.DrawSub') },
            { key: 'name', label: t('ESign.Dialog.App.Name'), off: (mode === 'drawRequired' || mode === 'savedAccepted') ? t('ESign.Dialog.Dis.VisualRequired') : null, sub: t('ESign.Dialog.App.NameSub') }
        ];
    }

    function defaultAppearance() {
        const opts = appearanceOptions();
        const on = (k) => opts.find((x) => x.key === k && !x.off);
        const mode = session.policy.visualMode || 'optional';
        if (mode === 'drawRequired') return 'draw';
        if (on('saved')) return 'saved';
        if (mode === 'savedAccepted') return 'draw';
        return on('name') ? 'name' : (on('draw') ? 'draw' : 'name');
    }

    function renderAppearance() {
        const host = $('esignAppearanceOptions');
        host.innerHTML = appearanceOptions().map((x) => {
            const active = session.appearance === x.key;
            const thumb = x.key === 'saved' && x.sample
                ? `<span class="border rounded bg-white p-1 ms-auto"><img alt="" src="${API}/samples/${encodeURIComponent(x.sample.id)}/image" style="max-height:32px;max-width:100px"></span>` : '';
            return `<button type="button" role="radio" aria-checked="${active}" data-esign-appearance="${x.key}"
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 ${active ? 'active-subtle border-primary' : ''}" ${x.off ? 'disabled aria-disabled="true"' : ''}>
                <i class="bx ${active ? 'bx-radio-circle-marked text-primary' : 'bx-radio-circle text-muted'} fs-5"></i>
                <span class="d-grid text-start"><span class="fw-medium">${escapeHtml(x.label)}</span>
                <small class="${x.off ? 'text-warning' : 'text-muted'}">${escapeHtml(x.off || x.sub)}</small></span>${thumb}</button>`;
        }).join('');
        host.querySelectorAll('[data-esign-appearance]').forEach((b) => b.addEventListener('click', () => {
            if (b.disabled) return;
            session.appearance = b.getAttribute('data-esign-appearance');
            appearanceError('');
            renderAppearance();
            renderCredential();
        }));
        $('esignPadWrap').classList.toggle('d-none', session.appearance !== 'draw');
        if (session.appearance === 'draw' && pad) global.setTimeout(() => pad.redraw(), 50);
    }

    /** Password, or (window open, not "every signature", not the saved sample — V3) the window line + the re-typed user name. */
    function needsPassword() {
        const p = session.policy;
        return !!p.reauthEvery || !windowOpen() || session.appearance === 'saved';
    }

    function renderCredential() {
        const pw = needsPassword();
        $('esignPasswordGroup').classList.toggle('d-none', !pw);
        $('esignWindowGroup').classList.toggle('d-none', pw);
        if (!pw) {
            const left = Math.max(1, Math.ceil((new Date(passwordWindow.expiresAtUtc).getTime() - Date.now()) / 60000));
            $('esignWindowInfo').textContent = t('ESign.Dialog.WindowInfo', hhmm(passwordWindow.verifiedAtUtc), session.policy.reauthWindowMinutes || left, left);
        } else {
            clearPassword();
        }
    }

    // ── submit ─────────────────────────────────────────────────────────────────────────────────────────────────
    async function submit() {
        const s = session;
        if (!s || s.phase !== 'form' || s.busy) return;
        const p = s.policy;
        const o = s.options;
        formError('');
        const meaning = $('esignMeaning').value;
        const usePassword = needsPassword();
        let invalid = false;
        if (!meaning) { fieldError('esignMeaning', 'esignMeaningError', t('ESign.Dialog.Err.Meaning')); invalid = true; }
        if (usePassword && !$('esignPassword').value) { fieldError('esignPassword', 'esignPasswordError', t('ESign.Dialog.Err.Password')); invalid = true; }
        if (!usePassword && !$('esignRetypeUser').value.trim()) { fieldError('esignRetypeUser', 'esignRetypeError', t('ESign.Dialog.Err.UserName')); invalid = true; }
        if (s.appearance === 'draw' && (!pad || pad.isEmpty())) { appearanceError(t('ESign.Dialog.Err.Draw')); invalid = true; }
        const sample = p.mySample && p.mySample.status === 'active' ? p.mySample : null;
        if (s.appearance === 'saved' && !sample) { appearanceError(t('ESign.Dialog.Err.NoSample')); invalid = true; }
        if (invalid) return;

        busy(true);
        // 1 · the ticket (AuthService). The password leaves the input exactly once and the input is emptied whatever happens.
        let auth;
        try {
            auth = usePassword
                ? await call('POST', `${AUTH}/reauth`, {
                    password: $('esignPassword').value,
                    intent: 'sign',
                    openWindowMinutes: p.reauthEvery ? null : (p.reauthWindowMinutes || null)
                })
                : await call('POST', `${AUTH}/ticket`, { intent: 'sign', userName: $('esignRetypeUser').value.trim() });
        } finally {
            clearPassword();
        }
        if (session !== s) return;
        if (!auth.ok) {
            busy(false);
            onAuthFailure(auth, usePassword);
            return;
        }
        const issued = dataOf(auth) || {};
        if (issued.kind === 'password' && issued.windowExpiresAtUtc) {
            passwordWindow = { verifiedAtUtc: issued.passwordVerifiedAtUtc, expiresAtUtc: issued.windowExpiresAtUtc };
        }

        // 2 · the signature (Platform). The ticket is used once and dropped with this call.
        let drawn = null;
        const appearance = s.appearance === 'saved' ? { type: 'sample', sampleId: sample.id }
            : s.appearance === 'draw' ? { type: 'drawn', drawing: (drawn = { pngBase64: pad.png(), strokes: pad.strokes() }) }
                : { type: 'none' };
        const context = typeof o.context === 'function' ? (o.context(p.subject || {}) || {}) : (o.context || null);
        const signed = await call('POST', `${API}/signatures`, {
            ticket: issued.ticket,
            subject: { module: o.module, subjectType: o.subjectType, subjectId: o.subjectId },
            actionCode: o.actionCode || null,
            meaningCode: meaning,
            note: $('esignNote').value.trim() || null,
            appearance,
            requestId: o.requestId || null,
            context,
            idempotencyKey: newKey()
        });
        if (session !== s) return;
        busy(false);
        if (signed.status === 401) { setPhase('timeout'); return; }
        if (!signed.ok) {
            const code = codeOf(signed);
            if (code === 'ESIGN_REAUTH_REQUIRED' || code === 'ESIGN_TICKET_INVALID') {
                passwordWindow = null;
                renderCredential();
            }
            if (code === 'ESIGN_APPEARANCE_NOT_ALLOWED' || code === 'ESIGN_DRAWING_EMPTY' || code === 'ESIGN_SAMPLE_NOT_ACTIVE') {
                appearanceError(message(code));
            } else {
                formError(signed.status === 403 && !code ? t('ESign.Dialog.Err.Forbidden') : message(code));
            }
            return;
        }
        success(dataOf(signed) || {}, drawn, sample);
    }

    function onAuthFailure(r, usedPassword) {
        const code = codeOf(r);
        if (r.status === 423 || code === 'ESIGN_ACCOUNT_LOCKED') {
            const end = paramsOf(r).lockoutEndUtc;
            const minutes = end ? Math.max(1, Math.ceil((new Date(end).getTime() - Date.now()) / 60000)) : '—';
            $('esignLockedText').textContent = t('ESign.Dialog.Locked.Text', minutes);
            passwordWindow = null;
            setPhase('locked');
            return;
        }
        if (code === 'ESIGN_REAUTH_FAILED') {
            const left = paramsOf(r).remainingAttempts;
            fieldError('esignPassword', 'esignPasswordError', left != null ? t('ESign.Dialog.Err.WrongPassword', left) : message(code));
            $('esignPassword').focus();
            return;
        }
        if (r.status === 401 && !code) { setPhase('timeout'); return; }
        if (!usedPassword && (code === 'ESIGN_REAUTH_WINDOW_EXPIRED' || code === 'ESIGN_INTENT_REQUIRES_PASSWORD')) {
            passwordWindow = null;
            renderCredential();
            fieldError('esignPassword', 'esignPasswordError', message(code));
            return;
        }
        formError(message(code));
    }

    function success(record, drawn, sample) {
        const s = session;
        s.result = { signatureId: record.id, signatureNumber: record.signatureNumber };
        const signer = record.signer || (s.policy.signer || {});
        const type = (record.appearance && record.appearance.type) || 'none';
        const img = $('esignResultImage');
        const src = type === 'drawn' && drawn ? `data:image/png;base64,${drawn.pngBase64}`
            : type === 'sample' && sample ? `${API}/samples/${encodeURIComponent(sample.id)}/image` : null;
        img.classList.toggle('d-none', !src);
        if (src) img.setAttribute('src', src); else img.removeAttribute('src');
        $('esignResultNoVisual').classList.toggle('d-none', !!src);
        const at = formatTime(record.signedAtUtc, true);
        const meaningText = record.meaningText || record.meaningCode || '';
        $('esignResultSigner').textContent = signer.name || signer.login || '';
        $('esignResultLine').textContent = `${meaningText} · ${at}`;
        $('esignResNumber').textContent = record.signatureNumber || record.id || '';
        $('esignResAt').textContent = at;
        $('esignResSigner').textContent = signer.login ? `${signer.name || ''} (${signer.login})` : (signer.name || '');
        $('esignResMeaning').textContent = meaningText;
        $('esignResAppearance').textContent = t(`ESign.Dialog.AppType.${type}`);
        $('esignResRecord').textContent = record.subjectDisplay || (record.subject && record.subject.subjectId) || s.options.subjectId;
        $('esignResHash').textContent = record.subjectHash ? `sha256:${record.subjectHash}` : '—';
        $('esignResImageHash').textContent = record.appearance && record.appearance.imageHash ? `sha256:${record.appearance.imageHash}` : '—';
        setPhase('success');
    }

    // ── zoom (shared with İmzam) ───────────────────────────────────────────────────────────────────────────────
    function zoom(d) {
        const el = $('esignZoom');
        if (!el) return;
        const set = (k, v) => { const x = el.querySelector(`[data-esign-zoom="${k}"]`); if (x) x.textContent = v || ''; };
        const img = el.querySelector('[data-esign-zoom="img"]');
        const name = el.querySelector('[data-esign-zoom="name"]');
        img.classList.toggle('d-none', !d.img);
        if (d.img) img.setAttribute('src', d.img); else img.removeAttribute('src');
        name.classList.toggle('d-none', !!d.img);
        name.textContent = d.img ? '' : (d.signer || '');
        set('sub', d.sub);
        set('signer', d.login ? `${d.signer || ''} (${d.login})` : d.signer);
        set('type', d.type);
        set('at', d.at);
        set('storage', d.storage);
        set('hash', d.hash || '—');
        el.querySelectorAll('[data-esign-zoom-row="storage"], [data-esign-zoom="storage"]').forEach((x) => x.classList.toggle('d-none', !d.storage));
        global.bootstrap.Modal.getOrCreateInstance(el).show();
    }

    global.ESign = Object.freeze({ open, message, t, formatTime, zoom, call, dataOf, codeOf, paramsOf, newKey, escapeHtml });
})(window);
