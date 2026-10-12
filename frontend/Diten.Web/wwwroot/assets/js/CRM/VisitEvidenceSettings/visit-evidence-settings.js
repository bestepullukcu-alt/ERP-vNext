/**
 * WP-VW-W3 (W3-WEB-d) — the visit evidence settings page (mockup "W3 Yazim Ekranlari" C1–C4). Every rule lives in
 * evidence-core.js (pure, tested in Node); every text comes from window.VisitEvidenceSettingsL10n (7 languages) or from
 * the CRM (rep names, reasons). Dates / numbers in the APPLICATION language through VisitPlanningFormat; every data name
 * isolated (bidi) for RTL. Calls go to the same-origin proxy /CRM/VisitEvidenceSettings/api/*.
 *   Settings tab: the scope tree (the tenant default only this round), the 8-field form in the mockup's groups, the
 *   preview ("the rep in this scope will see"), the save confirm (changed rows, "applies to N reps"), the effective-rules
 *   lookup and the change history.
 *   Exceptions tab: the list (status / phase chips with counts), the new-exception drawer (create → send for approval)
 *   and the detail drawer (timeline; submit / withdraw / approve / return / cancel / end early by the CRM abilities).
 */
(function (window, document) {
    'use strict';

    const C = window.VisitEvidenceCore;
    const F = window.VisitPlanningFormat;
    const L = window.VisitEvidenceSettingsL10n || {};
    const root = document.getElementById('ve-root');
    if (!C || !F || !root) { return; }

    const base = root.getAttribute('data-api-base') || '/CRM/VisitEvidenceSettings/api';
    const perms = {
        managePolicy: root.getAttribute('data-can-manage-policy') === 'true',
        read: root.getAttribute('data-can-read-exceptions') === 'true',
        manage: root.getAttribute('data-can-manage-exceptions') === 'true'
    };
    const me = root.getAttribute('data-me') || '';

    // ── tiny helpers ─────────────────────────────────────────────────────────────────────────────────────────
    const el = id => document.getElementById(id);
    const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const T = (key, ...args) => fmt(L[key] || '', ...args);
    const show = (id, on) => { const n = el(id); if (n) { n.classList.toggle('d-none', !on); } };
    const setText = (id, t) => { const n = el(id); if (n) { n.textContent = t; } };
    const toast = (msg, type) => { if (window.showToast) { window.showToast(msg, type || 'success'); } };
    const modal = id => (window.bootstrap && el(id) ? window.bootstrap.Modal.getOrCreateInstance(el(id)) : null);
    const drawer = id => (window.bootstrap && el(id) ? window.bootstrap.Offcanvas.getOrCreateInstance(el(id)) : null);
    const when = iso => (iso ? F.dateTime(iso) : '—');
    const day = iso => (iso ? F.dayMonth(iso + 'T12:00:00Z') + ' ' + String(iso).slice(0, 4) : '—'); // "10 Eki 2026" (the mockup)
    const initials = name => String(name || '').split(/\s+/).filter(Boolean).map(w => w[0]).join('').slice(0, 2).toUpperCase() || '·';

    const call = (method, path, body) => fetch(base + path, {
        method: method,
        credentials: 'same-origin',
        headers: Object.assign({ Accept: 'application/json' }, body ? { 'Content-Type': 'application/json' } : {}),
        body: body ? JSON.stringify(body) : undefined
    }).then(r => r.text().then(t => {
        let b = null;
        try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; }
        return { ok: r.ok, status: r.status, body: b };
    })).catch(() => ({ ok: false, status: 0, body: null }));
    const get = path => call('GET', path);
    const data = r => (r && r.ok && r.body ? r.body.data : null);
    const errText = r => L[C.errorKey(r)] || L.Err_generic || '';

    // ── state ────────────────────────────────────────────────────────────────────────────────────────────────
    const state = {
        policy: null, original: null, draft: null, reps: [], repNames: {}, history: null,
        tab: 'settings', pane: 'form', look: '', effective: null,
        list: null, filter: 'all', current: null, close: null, closeTried: false,
        nx: null, nxTried: false, nxFields: null
    };

    const nameOf = id => {
        if (!id) { return '—'; }
        if (me && String(id).toLowerCase() === me.toLowerCase()) { return L.YouLabel || ''; }
        if (state.repNames[id]) { return state.repNames[id]; }
        if (/^workflow:/.test(id)) { return L.WorkflowActor || ''; }
        return String(id).length > 12 ? String(id).slice(0, 8) + '…' : String(id);
    };

    const valueText = (field, value) => { const v = C.valueKey(field, value); return T(v.key, ...v.args); };

    // ── the policy ───────────────────────────────────────────────────────────────────────────────────────────
    function loadPolicy() {
        return get('/policy').then(r => {
            const p = data(r);
            if (!p) { const n = el('ve-load-error'); n.textContent = errText(r); show('ve-load-error', true); return; }
            show('ve-load-error', false);
            state.policy = p;
            state.original = C.draftOf(p);
            state.draft = C.draftOf(p);
            renderScope();
            renderForm();
            renderPreview();
            renderDirty();
            if (state.pane === 'history') { loadHistory(); }
        });
    }

    function renderScope() {
        const p = state.policy || {};
        const reps = typeof p.repCount === 'number' ? T('Reps', p.repCount) : '';
        setText('ve-node-meta', [L.AllFields, reps].filter(Boolean).join(' · '));
        setText('ve-scope-meta', [reps, L.AllFields].filter(Boolean).join(' · '));
        const last = p.isDefault ? (L.DefaultPolicy || '') : nameOf(p.updatedBy) + ' · ' + when(p.updatedAt);
        setText('ve-node-last', last);
        setText('ve-last-by', last);
        setText('ve-last-initials', p.isDefault ? '·' : initials(nameOf(p.updatedBy)));
    }

    function fieldHtml(field) {
        const d = state.draft;
        const kind = C.KIND[field];
        const durOff = field === 'shortVisitSeconds' && !d.durationConsistency;
        const dis = !perms.managePolicy || durOff;
        const errs = C.fieldErrors(d);
        const err = errs[field];
        const descKey = field === 'locationMode' ? 'D_locationMode_' + d.locationMode : 'D_' + field;
        const desc = L[descKey] || '';
        let control = '';
        if (kind === 'seg') {
            control = '<div class="ve-seg' + (dis ? ' ve-dis' : '') + '" role="radiogroup" aria-label="' + esc(L['F_' + field]) + '">'
                + C.OPTIONS[field].map(v => '<button type="button" role="radio" class="ve-seg-btn' + (d[field] === v ? ' active' : '') + '" aria-checked="' + (d[field] === v) + '" data-field="' + field + '" data-opt="' + v + '"' + (dis ? ' disabled' : '') + '>' + esc(L['Opt_' + field + '_' + v]) + '</button>').join('')
                + '</div>';
        } else if (kind === 'radio') {
            control = '<div class="ve-radios' + (dis ? ' ve-dis' : '') + '" role="radiogroup" aria-label="' + esc(L['F_' + field]) + '">'
                + C.OPTIONS[field].map(v => '<label class="ve-radio' + (d[field] === v ? ' active' : '') + '"><input type="radio" name="ve-' + field + '" data-field="' + field + '" data-opt="' + v + '"' + (d[field] === v ? ' checked' : '') + (dis ? ' disabled' : '') + '> ' + esc(L['Opt_' + field + '_' + v]) + '</label>').join('')
                + '</div>';
        } else if (kind === 'switch') {
            return '<div class="ve-field-row"><div class="ve-row ve-between ve-top"><span class="ve-col"><span class="ve-flabel">' + esc(L['F_' + field]) + '</span>' + (desc ? '<span class="ve-sub">' + esc(desc) + '</span>' : '') + '</span>'
                + '<button type="button" role="switch" class="ve-switch' + (d[field] ? ' on' : '') + '" aria-checked="' + !!d[field] + '" aria-label="' + esc(L['F_' + field]) + '" data-switch="' + field + '"' + (dis ? ' disabled' : '') + '><span></span></button></div></div>';
        } else {
            const range = C.RANGES[field];
            control = '<div class="ve-num' + (durOff ? ' ve-dis' : '') + '"><input type="number" inputmode="numeric" class="ve-input' + (err ? ' ve-invalid' : '') + '" data-num="' + field + '" min="' + range.min + '" max="' + range.max + '" value="' + esc(d[field]) + '"' + (dis ? ' disabled' : '') + ' aria-label="' + esc(L['F_' + field]) + '"><span class="ve-unit">' + esc(range.unit === 'm' ? L.UnitM : L.UnitS) + '</span></div>'
                + (err ? '<span class="ve-err"><i class="bx bx-error-circle"></i> ' + esc(T('RangeError', F.isolate(err.min), F.isolate(err.max), range.unit === 'm' ? L.UnitM : L.UnitS)) + '</span>'
                    : '<span class="ve-hint">' + esc(L['R_' + field] || '') + '</span>');
        }
        return '<div class="ve-field-row"><span class="ve-col"><span class="ve-flabel">' + esc(L['F_' + field]) + '</span>' + (desc ? '<span class="ve-sub">' + esc(desc) + '</span>' : '') + '</span>' + control + '</div>';
    }

    function renderForm() {
        if (!state.draft) { return; }
        el('ve-groups').innerHTML = C.GROUPS.map(g => '<div class="ve-card"><div class="ve-card-head ve-start"><span class="ve-card-title ve-row ve-lg"><i class="bx ' + g.icon + ' ve-ic"></i> ' + esc(L['G_' + g.key]) + '</span></div>'
            + '<div class="ve-fields">' + g.fields.map(fieldHtml).join('') + '</div></div>').join('');
    }

    function renderPreview() {
        if (!state.draft) { return; }
        const pv = C.preview(state.draft);
        setText('ve-pv-photo', '· ' + (L[pv.photoKey] || ''));
        const chip = el('ve-pv-loc-chip');
        chip.className = 've-pill ve-tone-' + pv.locationTone;
        chip.textContent = L[pv.locationKey] || '';
        setText('ve-pv-loc-text', T(pv.locationTextKey, ...pv.locationArgs));
        show('ve-pv-sign', pv.sign);
        el('ve-pv-start').classList.toggle('ve-faded', pv.startDisabled);
        el('ve-pv-lines').innerHTML = pv.lines.map(l => '<li><i class="bx ' + l.icon + ' ve-tone-text-' + l.tone + '"></i><span>' + esc(T(l.key, ...l.args.map(a => F.isolate(a)))) + '</span></li>').join('');
    }

    function renderDirty() {
        const changed = state.draft ? C.changes(state.original, state.draft) : [];
        const errs = state.draft ? C.fieldErrors(state.draft) : {};
        const dirty = perms.managePolicy && changed.length > 0;
        show('ve-save-bar', perms.managePolicy); // read-only: no Save / Cancel at all (the mockup)
        show('ve-unsaved', dirty);
        const save = el('ve-save'), revert = el('ve-revert');
        if (save) { save.disabled = !dirty || Object.keys(errs).length > 0; }
        if (revert) { revert.disabled = !dirty; }
    }

    function changed() { renderForm(); renderPreview(); renderDirty(); }

    function askSave() {
        const rows = C.changes(state.original, state.draft);
        if (!rows.length || Object.keys(C.fieldErrors(state.draft)).length) { return; }
        setText('ve-save-text', T('SaveText', F.isolate((state.policy && state.policy.repCount) || 0)));
        el('ve-save-diff').innerHTML = rows.map(r => '<div class="ve-diff-row"><span>' + esc(L['F_' + r.field]) + '</span><span class="ve-row"><s class="ve-mu">' + esc(valueText(r.field, r.from)) + '</s> <i class="bx bx-right-arrow-alt ve-flip"></i> <strong>' + esc(valueText(r.field, r.to)) + '</strong></span></div>').join('');
        const m = modal('ve-save-modal'); if (m) { m.show(); }
    }

    function doSave() {
        const btn = el('ve-save-confirm'); if (btn) { btn.disabled = true; }
        call('PUT', '/policy', C.putBody(state.draft, state.policy && state.policy.version)).then(r => {
            if (btn) { btn.disabled = false; }
            const m = modal('ve-save-modal'); if (m) { m.hide(); }
            if (!r.ok) {
                setText('ve-save-error-text', errText(r));
                show('ve-save-error', true);
                return;
            }
            show('ve-save-error', false);
            toast(T('Saved', (state.policy && state.policy.repCount) || 0));
            state.history = null;
            loadPolicy();
        });
    }

    // ── C1 — the effective rules of a rep ────────────────────────────────────────────────────────────────────
    let repTimer = null;
    function loadReps(q, selectId, selected) {
        return get('/policy/reps' + (q ? '?q=' + encodeURIComponent(q) : '')).then(r => {
            const reps = data(r) || [];
            reps.forEach(x => { state.repNames[x.resourceId] = x.displayName; });
            if (!q) { state.reps = reps; }
            const s = el(selectId);
            if (s) {
                s.innerHTML = '<option value="">' + esc(L.LookPlaceholder || '') + '</option>'
                    + reps.map(x => '<option value="' + esc(x.resourceId) + '"' + (x.resourceId === selected ? ' selected' : '') + '>' + esc(F.isolate(x.displayName)) + '</option>').join('');
            }
            return reps;
        });
    }

    function openLook(resourceId) {
        state.look = resourceId;
        if (!resourceId) { closeLook(); return; }
        get('/policy/effective?resourceId=' + encodeURIComponent(resourceId)).then(r => {
            const dto = data(r);
            if (!dto || state.look !== resourceId) { return; }
            state.effective = dto;
            const name = state.repNames[resourceId] || resourceId;
            el('ve-eff-name').innerHTML = F.bidi(name);
            setText('ve-eff-initials', initials(name));
            const x = dto.activeException;
            const banner = el('ve-eff-exception');
            banner.innerHTML = x ? '<i class="bx bx-time-five"></i> ' + esc(T('ActiveException', (x.rules || []).map(k => L['XRule_' + k] || k).join(', '), day(x.endDate))) : '';
            show('ve-eff-exception', !!x);
            el('ve-eff-rows').innerHTML = C.effectiveRows(dto).map(row => {
                const fromException = row.source === 'exception';
                return '<tr><td>' + esc(L['F_' + row.field]) + '</td><td>' + esc(valueText(row.field, row.value)) + '</td><td><span class="ve-pill ' + (fromException ? 've-tone-warning' : 've-tone-secondary') + '">'
                    + esc(fromException ? T('SourceExceptionUntil', x ? day(x.endDate) : '') : L.SourceTenant) + '</span></td></tr>';
            }).join('');
            show('ve-effective', true);
            show('ve-scope', false);
            el('ve-node-tenant').classList.remove('active'); // the mockup: the tree row is not selected while a rep is looked up
        });
    }

    function closeLook() {
        state.look = ''; state.effective = null;
        const s = el('ve-look'); if (s) { s.value = ''; }
        show('ve-effective', false);
        show('ve-scope', true);
        el('ve-node-tenant').classList.add('active');
    }

    // ── C3 — the change history ──────────────────────────────────────────────────────────────────────────────
    function loadHistory() {
        get('/policy/history?page=1&pageSize=100').then(r => {
            const h = data(r);
            state.history = h;
            const rows = C.historyRows(h ? h.items : []);
            el('ve-history-rows').innerHTML = rows.length
                ? rows.map(x => '<tr><td>' + esc(when(x.at)) + '</td><td>' + F.bidi(nameOf(x.by)) + '</td><td>' + esc(x.field ? L['F_' + x.field] || x.field : '—') + '</td><td>'
                    + (x.kind === 'created' ? '<span class="ve-mu">—</span> <i class="bx bx-right-arrow-alt ve-flip"></i> <strong>' + esc(L.HistCreated) + '</strong>'
                        : '<span class="ve-mu">' + esc(valueText(x.field, C.typed(x.field, x.from))) + '</span> <i class="bx bx-right-arrow-alt ve-flip"></i> <strong>' + esc(valueText(x.field, C.typed(x.field, x.to))) + '</strong>') + '</td></tr>').join('')
                : '<tr><td colspan="4" class="ve-empty">' + esc(L.NoHistory) + '</td></tr>';
        });
    }

    function setPane(pane) {
        state.pane = pane;
        document.querySelectorAll('.ve-pane-tab').forEach(b => { const on = b.getAttribute('data-pane') === pane; b.classList.toggle('active', on); b.setAttribute('aria-selected', String(on)); });
        show('ve-pane-form', pane === 'form');
        show('ve-pane-history', pane === 'history');
        if (pane === 'history' && !state.history) { loadHistory(); }
    }

    // ── C4 — the exceptions ──────────────────────────────────────────────────────────────────────────────────
    function setTab(tab) {
        state.tab = tab;
        document.querySelectorAll('.ve-tab').forEach(b => { const on = b.getAttribute('data-tab') === tab; b.classList.toggle('active', on); b.setAttribute('aria-selected', String(on)); });
        show('ve-pane-settings', tab === 'settings');
        show('ve-pane-exceptions', tab === 'exceptions');
        show('ve-new-exception', tab === 'exceptions' && perms.manage);
    }

    function loadExceptions() {
        if (!perms.read) { return Promise.resolve(); }
        return get('/exceptions').then(r => {
            const list = data(r);
            if (!list) { return; }
            state.list = list;
            (list.items || []).forEach(x => { if (x.resourceId && x.repDisplayName) { state.repNames[x.resourceId] = x.repDisplayName; } });
            const badge = el('ve-tab-badge');
            if (badge) { const n = C.tabBadge(list); badge.textContent = F.isolate(n); badge.classList.toggle('d-none', n === 0); }
            renderExceptions();
        });
    }

    const chipHtml = x => { const c = C.chipOf(x); return '<span class="ve-pill ve-tone-' + c.tone + '">' + esc(L[c.key] || '') + '</span>'; };
    const rulesHtml = x => (x.rules || []).map(k => '<span class="ve-pill ve-tone-warning">' + esc(L['XRule_' + k] || k) + '</span>').join('');

    function renderExceptions() {
        const list = state.list || { items: [] };
        el('ve-x-filters').innerHTML = C.FILTERS.map(f => '<button type="button" class="ve-chip' + (state.filter === f.key ? ' active' : '') + '" data-xfilter="' + f.key + '">' + esc(L['XFilter_' + f.key] || f.key) + ' <span>' + F.isolate(C.filterCount(list, f.key)) + '</span></button>').join('');
        const rows = C.filterExceptions(list.items, state.filter);
        el('ve-x-rows').innerHTML = rows.length
            ? rows.map(x => '<tr class="ve-click" data-xopen="' + esc(x.id) + '">'
                + '<td><div class="ve-row"><span class="ve-avatar ve-avatar-sm">' + esc(initials(x.repDisplayName)) + '</span><span class="ve-strong">' + F.bidi(x.repDisplayName || x.resourceId) + '</span></div></td>'
                + '<td><div class="ve-col ve-gap-xs ve-start">' + rulesHtml(x) + '</div></td>'
                + '<td><div class="ve-col"><span>' + esc(day(x.startDate)) + ' – ' + esc(day(x.endDate)) + '</span><span class="ve-sub">' + esc(T('XDays', F.isolate(x.days))) + '</span></div></td>'
                + '<td><span class="ve-reason">' + F.bidi(x.reason || '') + '</span></td>'
                + '<td><div class="ve-col ve-sub"><span>' + esc(T('XCreatedBy', nameOf(x.createdBy))) + '</span><span>' + esc(T('XApprovedBy', x.approvedBy ? nameOf(x.approvedBy) : '—')) + '</span></div></td>'
                + '<td>' + chipHtml(x) + '</td>'
                + '<td><button type="button" class="ve-x" data-xopen="' + esc(x.id) + '" aria-label="' + esc(L.OpenRow || '') + '"><i class="bx bx-show"></i></button></td></tr>').join('')
            : '<tr><td colspan="7" class="ve-empty">' + esc(L.XEmpty) + '</td></tr>';
    }

    function openException(id) {
        state.close = null; state.closeTried = false;
        get('/exceptions/' + encodeURIComponent(id)).then(r => {
            const x = data(r);
            if (!x) { toast(errText(r), 'error'); return; }
            state.current = x;
            renderDetail();
            const d = drawer('ve-x-drawer'); if (d) { d.show(); }
        });
    }

    function renderDetail(errorText) {
        const x = state.current;
        if (!x) { return; }
        const a = C.actionsOf(x, perms, me);
        const tl = C.timeline(x);
        el('ve-x-body').innerHTML = '<div class="ve-row ve-between ve-top"><div class="ve-row"><span class="ve-avatar">' + esc(initials(x.repDisplayName)) + '</span><div class="ve-col"><span class="ve-name">' + F.bidi(x.repDisplayName || x.resourceId) + '</span></div></div>' + chipHtml(x) + '</div>'
            + '<div class="ve-col ve-gap-xs"><span class="ve-cap">' + esc(L.XDetailRules) + '</span><div class="ve-row ve-wrap">' + rulesHtml(x) + '</div></div>'
            + '<div class="ve-col ve-gap-xs"><span class="ve-cap">' + esc(L.XDetailRange) + '</span><span>' + esc(day(x.startDate)) + ' – ' + esc(day(x.endDate)) + ' · ' + esc(T('XDays', F.isolate(x.days))) + '</span></div>'
            + '<div class="ve-col ve-gap-xs"><span class="ve-cap">' + esc(L.XDetailReason) + '</span><span>' + F.bidi(x.reason || '') + '</span></div>'
            + '<ul class="ve-timeline">' + tl.map(e => '<li><span class="ve-dot ve-dot-' + e.key + '"></span><div class="ve-col"><span class="ve-strong">' + esc(L[e.key] || '') + '</span><span class="ve-sub">' + (e.by ? F.bidi(nameOf(e.by)) + ' · ' : '') + esc(when(e.at)) + '</span>' + (e.note ? '<span class="ve-note">' + F.bidi(e.note) + '</span>' : '') + '</div></li>').join('') + '</ul>'
            + (x.status === 'in_review' ? '<div class="ve-alert ve-alert-info"><i class="bx bx-task"></i> ' + esc(L.XInReviewNote) + '</div>' : '')
            + (a.sod ? '<div class="ve-alert ve-alert-warning" id="ve-x-sod"><i class="bx bx-user-x"></i> ' + esc(L.XSod) + '</div>' : '')
            + (state.close ? '<div class="ve-field"><label class="ve-label" for="ve-x-reason">' + esc(L[state.close === 'return' ? 'XReturnComment' : 'XCloseReason']) + ' <span class="ve-req">*</span></label>'
                + '<textarea class="ve-input ve-textarea" id="ve-x-reason" rows="3"></textarea>'
                + '<span class="ve-err' + (state.closeTried ? '' : ' d-none') + '" id="ve-x-reason-err">' + esc(L.XErrWhy) + '</span></div>' : '')
            + (errorText ? '<div class="ve-alert ve-alert-danger" role="alert">' + esc(errorText) + '</div>' : '');

        const btn = (action, label, cls, disabled) => '<button type="button" class="ve-btn ' + cls + '" data-xact="' + action + '"' + (disabled ? ' disabled aria-disabled="true"' : '') + '>' + esc(L[label] || '') + '</button>';
        const buttons = [btn('close', 'CloseButton', 've-btn-s')];
        if (state.close) {
            buttons.push(btn('back', 'CancelButton', 've-btn-s'));
            buttons.push(btn('confirm-' + state.close, state.close === 'return' ? 'XBtnConfirmReturn' : state.close === 'cancel' ? 'XBtnConfirmCancel' : 'XBtnConfirmEndEarly', state.close === 'return' ? 've-btn-w' : 've-btn-d'));
        } else {
            if (a.withdraw) { buttons.push(btn('withdraw', 'XBtnWithdraw', 've-btn-s')); }
            if (a.submit) { buttons.push(btn('submit', 'XBtnSubmit', 've-btn-p')); }
            if (a.cancel) { buttons.push(btn('cancel', 'XBtnCancel', 've-btn-od')); }
            if (a.endEarly) { buttons.push(btn('end-early', 'XBtnEndEarly', 've-btn-od')); }
            if (a.decide) { buttons.push(btn('return', 'XBtnReturn', 've-btn-w')); }
            if (a.decide || a.sod) { buttons.push(btn('approve', 'XBtnApprove', 've-btn-ok', a.approveDisabled)); }
        }
        el('ve-x-foot').innerHTML = buttons.join('');
    }

    function exceptionAction(action) {
        const x = state.current; if (!x) { return; }
        const path = '/exceptions/' + encodeURIComponent(x.id);
        const done = (r, toastKey) => {
            if (!r.ok) { renderDetail(errText(r)); return; }
            state.current = data(r) || x;
            state.close = null; state.closeTried = false;
            toast(L[toastKey] || '');
            renderDetail();
            loadExceptions();
        };
        if (action === 'close') { const d = drawer('ve-x-drawer'); if (d) { d.hide(); } return; }
        if (action === 'back') { state.close = null; state.closeTried = false; renderDetail(); return; }
        if (action === 'cancel' || action === 'end-early' || action === 'return') { state.close = action; state.closeTried = false; renderDetail(); return; }
        if (action === 'submit') { call('POST', path + '/submit', {}).then(r => done(r, 'XSubmitted')); return; }
        if (action === 'withdraw') { call('POST', path + '/withdraw', {}).then(r => done(r, 'XWithdrawn')); return; }
        if (action === 'approve') {
            if (C.actionsOf(x, perms, me).approveDisabled) { return; }
            call('POST', path + '/decision', { decision: 'approve', comment: null }).then(r => done(r, 'XApprovedToast'));
            return;
        }
        if (action.indexOf('confirm-') === 0) {
            const kind = action.slice('confirm-'.length);
            const reason = (el('ve-x-reason') ? el('ve-x-reason').value : '').trim();
            if (!reason) { state.closeTried = true; show('ve-x-reason-err', true); return; }
            if (kind === 'return') { call('POST', path + '/decision', { decision: 'return', comment: reason }).then(r => done(r, 'XReturnedToast')); return; }
            call('POST', path + (kind === 'cancel' ? '/cancel' : '/end-early'), { reason: reason }).then(r => done(r, kind === 'cancel' ? 'XCancelledToast' : 'XEndedToast'));
        }
    }

    // ── the new exception ────────────────────────────────────────────────────────────────────────────────────
    function openNew() {
        state.nx = { resourceId: '', rules: [], startDate: C.todayUtc(Date.now()), endDate: '', reason: '' };
        state.nxTried = false; state.nxFields = null;
        el('ve-nx-from').value = state.nx.startDate;
        el('ve-nx-from').min = state.nx.startDate;
        el('ve-nx-to').value = '';
        el('ve-nx-to').min = state.nx.startDate;
        el('ve-nx-why').value = '';
        el('ve-nx-rep-search').value = '';
        show('ve-nx-error', false);
        loadReps('', 've-nx-rep', '');
        renderNew();
        const d = drawer('ve-new-drawer'); if (d) { d.show(); }
    }

    function renderNew() {
        const f = state.nx; if (!f) { return; }
        el('ve-nx-rules').innerHTML = C.RULES.map(rule => {
            const st = C.ruleState(rule, f.resourceId ? state.nxFields : null);
            const on = f.rules.indexOf(rule) > -1;
            const note = st.reason === 'XRuleNoRep' ? L.XRuleNoRep
                : st.reason === 'XRuleAlready' ? T('XRuleAlready', valueText(C.RULE_FIELD[rule].field, C.RULE_FIELD[rule].value), L[st.source === 'exception' ? 'SourceException' : 'SourceTenant'])
                    : T('XRuleNow', valueText(st.field, st.value), L[st.source === 'exception' ? 'SourceException' : 'SourceTenant']);
            return '<label class="ve-check' + (on ? ' active' : '') + (st.enabled ? '' : ' ve-dis') + '"><input type="checkbox" data-rule="' + rule + '"' + (on ? ' checked' : '') + (st.enabled ? '' : ' disabled') + '>'
                + '<span class="ve-col"><span>' + esc(L['XRule_' + rule]) + '</span><span class="ve-sub">' + esc(note) + '</span></span></label>';
        }).join('');
        const days = C.daysInclusive(f.startDate, f.endDate);
        const counter = el('ve-nx-days');
        counter.textContent = days ? T('XDayCounter', F.isolate(days), F.isolate(C.MAX_DAYS)) : T('XDayCounterEmpty', F.isolate(C.MAX_DAYS));
        counter.classList.toggle('ve-err', days > C.MAX_DAYS);
        const count = el('ve-nx-why-count');
        count.textContent = F.ratio(f.reason.length, C.MAX_REASON);
        count.classList.toggle('ve-err', f.reason.length > C.MAX_REASON);
        const errs = state.nxTried ? C.exceptionErrors(f, C.todayUtc(Date.now())) : {};
        ['rep', 'rules', 'from', 'to', 'why'].forEach(k => {
            const n = el('ve-nx-err-' + k);
            if (!n) { return; }
            n.textContent = errs[k] ? T(errs[k].key, ...errs[k].args.map(a => F.isolate(a))) : '';
            n.classList.toggle('d-none', !errs[k]);
        });
    }

    function pickRep(id) {
        state.nx.resourceId = id; state.nx.rules = []; state.nxFields = null;
        renderNew();
        if (!id) { return; }
        get('/policy/effective?resourceId=' + encodeURIComponent(id)).then(r => {
            const dto = data(r);
            if (!dto || !state.nx || state.nx.resourceId !== id) { return; }
            state.nxFields = dto.fields || [];
            renderNew();
        });
    }

    /** "Send for approval": the draft is written (POST), then sent (POST submit); a refused send leaves the draft and
     *  opens it with the reason. */
    function submitNew() {
        const f = state.nx; if (!f) { return; }
        state.nxTried = true;
        renderNew();
        if (Object.keys(C.exceptionErrors(f, C.todayUtc(Date.now()))).length) { return; }
        const btn = el('ve-nx-submit'); btn.disabled = true;
        show('ve-nx-error', false);
        call('POST', '/exceptions', { resourceId: f.resourceId, rules: f.rules, startDate: f.startDate, endDate: f.endDate, reason: f.reason.trim() }).then(r => {
            const created = data(r);
            if (!r.ok || !created) {
                btn.disabled = false;
                const n = el('ve-nx-error'); n.textContent = errText(r); show('ve-nx-error', true);
                return null;
            }
            return call('POST', '/exceptions/' + encodeURIComponent(created.id) + '/submit', {}).then(s => {
                btn.disabled = false;
                const d = drawer('ve-new-drawer'); if (d) { d.hide(); }
                state.nx = null;
                state.filter = 'all';
                state.current = data(s) || created;
                loadExceptions();
                if (s.ok) { toast(L.XSubmitted || ''); renderDetail(); } else { renderDetail(T('XCreatedNotSent', errText(s))); }
                const x = drawer('ve-x-drawer'); if (x) { x.show(); }
            });
        });
    }

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────
    const on = (id, ev, fn) => { const n = el(id); if (n) { n.addEventListener(ev, fn); } };

    root.addEventListener('click', e => {
        const tab = e.target.closest('[data-tab]');
        if (tab) { setTab(tab.getAttribute('data-tab')); return; }
        const pane = e.target.closest('[data-pane]');
        if (pane) { setPane(pane.getAttribute('data-pane')); return; }
        const opt = e.target.closest('button[data-opt]');
        if (opt && !opt.disabled && perms.managePolicy) { state.draft[opt.getAttribute('data-field')] = opt.getAttribute('data-opt'); changed(); return; }
        const sw = e.target.closest('[data-switch]');
        if (sw && !sw.disabled && perms.managePolicy) { const f = sw.getAttribute('data-switch'); state.draft[f] = !state.draft[f]; changed(); return; }
        const xf = e.target.closest('[data-xfilter]');
        if (xf) { state.filter = xf.getAttribute('data-xfilter'); renderExceptions(); return; }
        const open = e.target.closest('[data-xopen]');
        if (open) { openException(open.getAttribute('data-xopen')); }
    });
    root.addEventListener('change', e => {
        const radio = e.target.closest && e.target.closest('input[data-opt]');
        if (radio && perms.managePolicy) { state.draft[radio.getAttribute('data-field')] = radio.getAttribute('data-opt'); changed(); }
    });
    root.addEventListener('input', e => {
        const num = e.target.closest && e.target.closest('[data-num]');
        if (num && perms.managePolicy) {
            const f = num.getAttribute('data-num');
            state.draft[f] = num.value;
            renderPreview(); renderDirty();
            const errs = C.fieldErrors(state.draft);
            num.classList.toggle('ve-invalid', !!errs[f]);
        }
    });
    root.addEventListener('focusout', e => { if (e.target.closest && e.target.closest('[data-num]')) { renderForm(); } });

    on('ve-save', 'click', askSave);
    on('ve-revert', 'click', () => { state.draft = C.draftOf(state.original); show('ve-save-error', false); changed(); });
    on('ve-save-confirm', 'click', doSave);
    on('ve-save-retry', 'click', askSave);
    on('ve-look', 'change', e => openLook(e.target.value));
    on('ve-eff-close', 'click', closeLook);
    on('ve-node-tenant', 'click', closeLook);
    on('ve-look-search', 'input', e => { clearTimeout(repTimer); const q = e.target.value.trim(); repTimer = setTimeout(() => loadReps(q, 've-look', state.look), 250); });
    on('ve-new-exception', 'click', openNew);
    on('ve-nx-rep', 'change', e => pickRep(e.target.value));
    on('ve-nx-rep-search', 'input', e => { clearTimeout(repTimer); const q = e.target.value.trim(); repTimer = setTimeout(() => loadReps(q, 've-nx-rep', state.nx ? state.nx.resourceId : ''), 250); });
    on('ve-nx-from', 'change', e => { state.nx.startDate = e.target.value; el('ve-nx-to').min = e.target.value; renderNew(); });
    on('ve-nx-to', 'change', e => { state.nx.endDate = e.target.value; renderNew(); });
    on('ve-nx-why', 'input', e => { state.nx.reason = e.target.value; renderNew(); });
    on('ve-nx-submit', 'click', submitNew);
    const nxRules = el('ve-nx-rules');
    if (nxRules) {
        nxRules.addEventListener('change', e => {
            const cb = e.target.closest('[data-rule]');
            if (!cb || !state.nx) { return; }
            const rule = cb.getAttribute('data-rule');
            state.nx.rules = cb.checked ? state.nx.rules.concat([rule]) : state.nx.rules.filter(x => x !== rule);
            renderNew();
        });
    }
    const xFoot = el('ve-x-foot');
    if (xFoot) {
        xFoot.addEventListener('click', e => {
            const b = e.target.closest('[data-xact]');
            if (b && !b.disabled) { exceptionAction(b.getAttribute('data-xact')); }
        });
    }

    setTab('settings');
    loadReps('', 've-look', '');
    loadPolicy();
    loadExceptions();
}(window, document));
