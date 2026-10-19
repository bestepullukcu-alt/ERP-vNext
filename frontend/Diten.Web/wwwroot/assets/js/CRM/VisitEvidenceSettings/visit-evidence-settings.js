/**
 * WP-VW-W3 (W3-WEB-d) — the visit evidence settings page (mockup "W3 Yazim Ekranlari" C1–C4). Every rule lives in
 * evidence-core.js (pure, tested in Node); every text comes from window.VisitEvidenceSettingsL10n (7 languages) or from
 * the CRM (rep names, reasons). Dates / numbers in the APPLICATION language through VisitPlanningFormat; every data name
 * isolated (bidi) for RTL. Calls go to the same-origin proxy /CRM/VisitEvidenceSettings/api/*.
 *   Settings tab: the scope tree (the tenant default only this round), the 8-field form in the mockup's groups, the
 *   preview ("the rep in this scope will see"), the save confirm (changed rows, "applies to N reps"), the effective-rules
 *   lookup and the change history.
 *   Exceptions tab: the new-exception drawer (create → send for approval) and the detail in the list's quick view
 *   (timeline; submit / withdraw / approve / return / cancel / end early by the CRM abilities).
 *   W3E-E4R2-FIX-b — the exceptions LIST is the Golden Compact DataTable (index.js, client mode); this script keeps the
 *   full list for the tab's corner count, the reps pane and the "?exception=" link, and hands index.js its quick view and
 *   its "New exception" action through window.VisitEvidenceSettingsPage. The tabs are the shared tab card (diten-tabs.js).
 *   W3E-E4R2-FIX-d (U-7) — the settings tab's inner panes (form · history · reps) are a second shared tab card; the change
 *   history and the rep roster are Golden Compact DataTables in server mode (settings-tables.js), built when their pane is
 *   first shown, measured again on every diten:tab-change. The rep cells stay this page's rule (C.repRows, one row at a
 *   time, over the exception list the page already reads — no read per rep).
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
        manage: root.getAttribute('data-can-manage-exceptions') === 'true',
        qrCodes: root.getAttribute('data-can-read-qr-codes') === 'true' // W3E — the "manage institution QR codes" link
    };
    const qrCodesUrl = root.getAttribute('data-qr-codes-url') || '/CRM/VisitQrCodes';
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
        policy: null, original: null, draft: null, reps: [], repNames: {}, people: {}, tables: { history: null, reps: null },
        tab: 'settings', pane: 'form', look: '', effective: null,
        list: null, current: null, close: null, closeTried: false,
        nx: null, nxTried: false, nxFields: null,
        // WP-VW-W3L — the scope tree (GET scopes), the names of the scopes (MDM legal entities, MOD-0048 business units), the
        // selected scope ("tenant" | "<type>:<ref>"), the selected layer's form ({ id, type, ref, version, isNew, original,
        // ov } — ov[field] null = from above), every layer's overrides (the reps' cells), a picked scope not yet saved, the
        // history's scope filter
        tree: C.scopeTree(null, {}, L), names: {}, options: { 'legal-entity': [], 'business-unit': [] }, optionsRead: {},
        sel: 'tenant', layer: null, layerOv: {}, layerDetail: {}, pending: null, addType: null, historyFilter: 'tenant'
    };
    const BASE_LABELS = L;

    /** WP-VW-W3L — a scope's name (never the code where a list knows the name). */
    const scopeLabel = (type, ref) => C.scopeName(type, ref, state.names, BASE_LABELS);
    /** WP-VW-W3L — a field source as words: "Kiracı" / "Tüzel kişilik: <name>" / "İş birimi: <name>" / the exception. */
    const sourceText = (source, until) => {
        const s = C.sourceOf(source, state.names, BASE_LABELS);
        return s.type === 'exception' ? T('SourceExceptionUntil', until || '') : T(s.key, ...s.args.map(a => F.isolate(a)));
    };
    /** The short source of the exception window's rule notes ("· Kiracı", "· Tüzel kişilik: X", "· İstisna"). */
    const sourceShort = source => (C.parseSource(source).type === 'exception' ? L.SourceException : sourceText(source));
    const isLayer = () => !!state.layer;
    const layerBase = () => C.draftOf(state.policy);
    const nodeOf = id => state.tree.find(n => n.id === id) || (state.pending && state.pending.id === id ? state.pending : null);

    // WP-VW-W3-FIX (F5) — CRM's displayNames first (every read carries them), a short id when unresolved.
    const nameOf = id => C.personLabel(id, state.people, state.repNames, me, L);
    const absorb = dto => { if (dto && dto.displayNames) { C.mergeNames(state.people, dto.displayNames); } return dto; };

    const valueText = (field, value) => { const v = C.valueKey(field, value); return T(v.key, ...v.args); };

    // ── the policy ───────────────────────────────────────────────────────────────────────────────────────────
    function loadPolicy() {
        return get('/policy').then(r => {
            const p = absorb(data(r));
            if (!p) { const n = el('ve-load-error'); n.textContent = errText(r); show('ve-load-error', true); return; }
            show('ve-load-error', false);
            state.policy = p;
            if (isLayer()) {
                // a layer is selected: its form keeps its overrides, the inherited values follow the new tenant policy
                state.draft = C.layerEffective(p, state.layer.ov);
            } else {
                state.original = C.draftOf(p);
                state.draft = C.draftOf(p);
            }
            renderScope();
            renderForm();
            renderPreview();
            renderDirty();
            refreshTables();
        });
    }

    // ── WP-VW-W3L (C1) — the scope tree ──────────────────────────────────────────────────────────────────────
    /** The two lists that name the scopes and feed "add": MDM legal entities, the published business units. A list that
     *  cannot be read names its layers by their code and offers nothing to add (never a guessed name). */
    function loadOptions() {
        const one = (type, path) => get(path).then(r => {
            const d = data(r);
            state.optionsRead[type] = !!(d && d.available);
            state.options[type] = d && Array.isArray(d.items) ? d.items : [];
        });
        return Promise.all([one('legal-entity', '/scope-options/legal-entities'), one('business-unit', '/scope-options/business-units')]).then(() => {
            state.names = C.scopeNames(state.options['legal-entity'], state.options['business-unit']);
            state.tree = C.scopeTree(state.list0, state.names, BASE_LABELS);
            renderTree(); renderScope(); renderForm(); renderHistoryFilter();
            if (state.effective) { renderEffective(state.effective); }
            refreshTables();
        });
    }

    /** GET scopes (the tenant + every active layer: overrides, last change, reps) and each layer's overrides (the reps'
     *  cells and the forms) — one read per layer, never one per rep. */
    function loadScopes() {
        return get('/policy/scopes').then(r => {
            const list = absorb(data(r));
            state.list0 = list;
            state.tree = C.scopeTree(list, state.names, BASE_LABELS);
            const layers = state.tree.filter(n => n.type !== 'tenant');
            return Promise.all(layers.map(n => get('/policy/scopes/' + n.type + '/' + encodeURIComponent(n.ref)).then(x => {
                const detail = absorb(data(x));
                if (detail) { state.layerDetail[n.id] = detail; state.layerOv[n.id] = C.layerOverrides(detail); }
            }))).then(() => {
                Object.keys(state.layerOv).forEach(id => { if (!layers.some(n => n.id === id)) { delete state.layerOv[id]; delete state.layerDetail[id]; } });
                if (state.pending && layers.some(n => n.id === state.pending.id)) { state.pending = null; }
                renderTree(); renderHistoryFilter();
                if (state.sel !== 'tenant' && !nodeOf(state.sel)) { selectScope('tenant'); } else { renderScope(); }
                refreshTables();
            });
        });
    }

    function nodeHtml(n) {
        const m = C.scopeMeta(n);
        const on = n.id === state.sel && !state.look;
        const last = n.isNew ? L.NewScopeBadge : n.updatedAt ? nameOf(n.updatedBy) + ' · ' + when(n.updatedAt) : '';
        return '<button type="button" class="ve-node ve-node-layer' + (on ? ' active' : '') + '" data-scope="' + esc(n.id) + '" aria-pressed="' + on + '">'
            + '<span class="ve-node-icon ve-tone-' + C.SCOPE_TONE[n.type] + '"><i class="bx ' + C.SCOPE_ICON[n.type] + '"></i></span>'
            + '<span class="ve-node-body"><span class="ve-node-name">' + F.bidi(n.name) + '</span>'
            + '<span class="ve-node-meta' + (n.overridden ? ' ve-node-diff' : '') + '">' + esc(n.isNew ? L.NoDiff : T(m.fields.key, ...m.fields.args.map(a => F.isolate(a)))) + (n.isNew ? '' : ' · ' + esc(T(m.reps.key, ...m.reps.args.map(a => F.isolate(a))))) + '</span>'
            + (last ? '<span class="ve-node-last">' + esc(last) + '</span>' : '') + '</span></button>';
    }

    /** The layers under the tenant: the legal entities, then the business units (a business unit is not under a legal
     *  entity — the CRM takes it from the rep's territory assignment, K-L4), a picked scope not yet saved, and with the
     *  manage key "Add legal entity" / "Add business unit". */
    function renderTree() {
        const host = el('ve-tree-layers');
        const tenant = el('ve-node-tenant');
        if (tenant) { tenant.classList.toggle('active', state.sel === 'tenant' && !state.look); }
        if (!host) { return; }
        const group = type => {
            const rows = state.tree.filter(n => n.type === type).concat(state.pending && state.pending.type === type ? [state.pending] : []);
            return rows.map(nodeHtml).join('')
                + (perms.managePolicy ? '<button type="button" class="ve-add" data-add="' + type + '"><i class="bx bx-plus-circle"></i> ' + esc(L[type === 'legal-entity' ? 'AddLegalEntity' : 'AddBusinessUnit']) + '</button>' : '');
        };
        host.innerHTML = group('legal-entity') + group('business-unit');
    }

    /** Selects a scope: the tenant (its policy form) or a layer (its overrides; a field it does not override comes from
     *  above). An unsaved change of the scope left is dropped (the mockup: the form is the selected scope's). */
    function selectScope(id) {
        state.sel = id || 'tenant';
        if (state.pending && state.pending.id !== state.sel) { state.pending = null; } // a picked scope left unsaved is dropped
        if (state.look) { state.look = ''; state.effective = null; const s = el('ve-look'); if (s) { s.value = ''; } show('ve-effective', false); show('ve-scope', true); }
        show('ve-save-error', false);
        if (state.sel === 'tenant') {
            state.layer = null;
            state.original = C.draftOf(state.policy);
            state.draft = C.draftOf(state.policy);
        } else {
            const n = nodeOf(state.sel);
            if (!n) { selectScope('tenant'); return; }
            const ov = n.isNew ? C.layerOverrides(null) : (state.layerOv[n.id] || C.layerOverrides(state.layerDetail[n.id]));
            const detail = state.layerDetail[n.id] || {};
            state.layer = { id: n.id, type: n.type, ref: n.ref, version: n.isNew ? 0 : (detail.version || n.version || 0), isNew: !!n.isNew, original: Object.assign({}, ov), ov: Object.assign({}, ov) };
            state.draft = C.layerEffective(state.policy, state.layer.ov);
        }
        state.historyFilter = state.sel;
        renderTree(); renderScope(); renderForm(); renderPreview(); renderDirty(); renderHistoryFilter();
        if (state.tables.history) { state.tables.history.reload(); }
    }

    function renderScope() {
        const p = state.policy || {};
        const reps = typeof p.repCount === 'number' ? T('Reps', p.repCount) : '';
        setText('ve-node-meta', [L.AllFields, reps].filter(Boolean).join(' · '));
        const tenantLast = p.isDefault ? (L.DefaultPolicy || '') : nameOf(p.updatedBy) + ' · ' + when(p.updatedAt);
        setText('ve-node-last', tenantLast);
        const n = state.sel === 'tenant' ? null : nodeOf(state.sel);
        const type = n ? n.type : 'tenant';
        // the header: the scope's icon, name and type
        const icon = el('ve-scope-icon');
        if (icon) { icon.className = 've-scope-icon ve-tone-' + C.SCOPE_TONE[type]; icon.innerHTML = '<i class="bx ' + C.SCOPE_ICON[type] + '"></i>'; }
        const name = el('ve-scope-name');
        if (name) { name.innerHTML = F.bidi(n ? n.name : L.TenantDefault || ''); }
        const pill = el('ve-scope-type');
        if (pill) { pill.className = 've-pill ve-tone-' + C.SCOPE_TONE[type]; pill.textContent = L[type === 'legal-entity' ? 'TypeLegalEntity' : type === 'business-unit' ? 'TypeBusinessUnit' : 'TypeTenant'] || ''; }
        if (!n) {
            setText('ve-scope-meta', [reps, L.AllFields].filter(Boolean).join(' · '));
            setText('ve-last-by', tenantLast);
            setText('ve-last-initials', p.isDefault ? '·' : initials(nameOf(p.updatedBy)));
        } else {
            const m = C.scopeMeta(Object.assign({}, n, { overridden: state.layer ? C.overriddenCount(state.layer.ov) : n.overridden }));
            setText('ve-scope-meta', n.isNew ? L.NewScopeBadge : T(m.reps.key, ...m.reps.args) + ' · ' + T(m.fields.key, ...m.fields.args));
            const last = n.isNew ? L.NewScopeBadge : nameOf(n.updatedBy) + ' · ' + when(n.updatedAt);
            setText('ve-last-by', last);
            setText('ve-last-initials', n.isNew ? '·' : initials(nameOf(n.updatedBy)));
        }
        show('ve-scope-remove', !!n && !n.isNew && perms.managePolicy);
        // C2 — the inheritance note of a layer's form
        const note = !n ? '' : n.isNew ? L.NewScopeNote
            : (state.layer && C.overriddenCount(state.layer.original) ? L.InheritNote : L.InheritNoneNote) + (type === 'business-unit' ? ' ' + L.InheritBuNote : '');
        setText('ve-inherit-text', note);
        show('ve-inherit-note', !!note);
    }

    /** C2 — under a layer's field: "from above (Kiracı): value" + "Change in this scope", or "Changed in this scope" + the
     *  value above + "Back to the setting above" (only with the manage key). */
    function layerRow(field) {
        if (!isLayer()) { return ''; }
        const base = layerBase();
        const own = state.layer.ov[field] !== null && state.layer.ov[field] !== undefined;
        const above = valueText(field, base[field]);
        if (!own) {
            return '<div class="ve-inh"><span class="ve-mu ve-row"><i class="bx bx-git-merge" aria-hidden="true"></i> ' + esc(T('InheritedFrom', L.SourceTenant, above)) + '</span>'
                + (perms.managePolicy ? '<button type="button" class="ve-link" data-take="' + field + '"><i class="bx bx-edit-alt"></i> ' + esc(L.ChangeHere) + '</button>' : '') + '</div>';
        }
        return '<div class="ve-inh"><span class="ve-pill ve-tone-primary">' + esc(L.OverriddenHere) + '</span><span class="ve-mu">' + esc(T('UpperValue', above)) + '</span>'
            + (perms.managePolicy ? '<button type="button" class="ve-link" data-drop="' + field + '"><i class="bx bx-undo"></i> ' + esc(L.BackToUpperLink) + '</button>' : '') + '</div>';
    }

    /** A layer's field the layer does not override is shown faded and cannot be edited until "Change in this scope". */
    const inheritedField = field => isLayer() && (state.layer.ov[field] === null || state.layer.ov[field] === undefined);

    /** One field's new value: the tenant draft, or the selected layer's override (its effective values follow). */
    function setField(field, value) {
        if (isLayer()) {
            state.layer.ov[field] = value;
            state.draft = C.layerEffective(state.policy, state.layer.ov);
        } else {
            state.draft[field] = value;
        }
    }

    function fieldHtml(field) {
        const d = state.draft;
        const kind = C.KIND[field];
        const durOff = field === 'shortVisitSeconds' && !d.durationConsistency;
        // WP-VW-W3L — a layer's field it does not override is the scope above's: faded, not editable here
        const inh = inheritedField(field);
        const dis = !perms.managePolicy || durOff || inh;
        const errs = isLayer() ? C.layerFieldErrors(state.layer.ov, layerBase()) : C.fieldErrors(d);
        const rowCls = 've-field-row' + (inh ? ' ve-inherited' : '');
        const inhRow = layerRow(field);
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
            return '<div class="' + rowCls + '"><div class="ve-row ve-between ve-top"><span class="ve-col"><span class="ve-flabel">' + esc(L['F_' + field]) + '</span>' + (desc ? '<span class="ve-sub">' + esc(desc) + '</span>' : '') + '</span>'
                + '<button type="button" role="switch" class="ve-switch' + (d[field] ? ' on' : '') + '" aria-checked="' + !!d[field] + '" aria-label="' + esc(L['F_' + field]) + '" data-switch="' + field + '"' + (dis ? ' disabled' : '') + '><span></span></button></div>' + inhRow + '</div>';
        } else {
            const range = C.RANGES[field];
            control = '<div class="ve-num' + (durOff ? ' ve-dis' : '') + '"><input type="number" inputmode="numeric" class="ve-input' + (err ? ' ve-invalid' : '') + '" data-num="' + field + '" min="' + range.min + '" max="' + range.max + '" value="' + esc(d[field]) + '"' + (dis ? ' disabled' : '') + ' aria-label="' + esc(L['F_' + field]) + '"><span class="ve-unit">' + esc(range.unit === 'm' ? L.UnitM : L.UnitS) + '</span></div>'
                + (err ? '<span class="ve-err"><i class="bx bx-error-circle"></i> ' + esc(T('RangeError', F.isolate(err.min), F.isolate(err.max), range.unit === 'm' ? L.UnitM : L.UnitS)) + '</span>'
                    : '<span class="ve-hint">' + esc(L['R_' + field] || '') + '</span>');
        }
        return '<div class="' + rowCls + '"><span class="ve-col"><span class="ve-flabel">' + esc(L['F_' + field]) + '</span>' + (desc ? '<span class="ve-sub">' + esc(desc) + '</span>' : '') + '</span>' + control + inhRow + '</div>';
    }

    function renderForm() {
        if (!state.draft) { return; }
        el('ve-groups').innerHTML = C.GROUPS.map(g => '<div class="ve-card"><div class="ve-card-head ve-start"><span class="ve-card-title ve-row ve-lg"><i class="bx ' + g.icon + ' ve-ic"></i> ' + esc(L['G_' + g.key]) + '</span></div>'
            + '<div class="ve-fields">' + g.fields.map(fieldHtml).join('') + '</div></div>').join('');
        renderExtra();
    }

    /**
     * WP-VW-W3E (W3E-WEB-b, E1) — "Additional evidence" (in place of the former "coming soon" card): the institution QR code
     * as three choice cards with their effect sentence + the "manage institution QR codes" link (only with a QR key), the
     * device verification as a segment whose "block" is disabled with "soon" (W3E-ATTEST).
     */
    function renderExtra() {
        const host = el('ve-extra');
        if (!host || !state.draft) { return; }
        const d = state.draft;
        const dis = !perms.managePolicy;
        // WP-VW-W3L — in a layer each of the two fields is inherited or overridden on its own
        const qrDis = dis || inheritedField('qrMode');
        const deviceDis = dis || inheritedField('antiFraudMode');
        const qr = C.EXTRA.qrMode.options.map(v => {
            const on = d.qrMode === v;
            return '<button type="button" role="radio" class="ve-choice' + (on ? ' active' : '') + '" aria-checked="' + on + '" data-field="qrMode" data-opt="' + v + '"' + (qrDis ? ' disabled' : '') + '>'
                + '<span class="ve-choice-head"><span class="ve-choice-dot" aria-hidden="true"></span><span class="ve-strong">' + esc(L['Opt_qrMode_' + v]) + '</span></span>'
                + '<span class="ve-sub">' + esc(L['D_qrMode_' + v]) + '</span></button>';
        }).join('');
        const device = C.EXTRA.antiFraudMode.options.map(v => {
            const soon = C.EXTRA.antiFraudMode.soon.indexOf(v) > -1;
            const on = d.antiFraudMode === v && !soon;
            return '<button type="button" role="radio" class="ve-seg-btn' + (on ? ' active' : '') + (soon ? ' ve-soon-opt' : '') + '" aria-checked="' + on + '"'
                + (soon ? ' disabled aria-disabled="true" data-soon="' + v + '"' : ' data-field="antiFraudMode" data-opt="' + v + '"' + (deviceDis ? ' disabled' : ''))
                + '>' + esc(L['Opt_antiFraudMode_' + v]) + (soon ? ' <span class="ve-pill ve-tone-secondary ve-pill-xs">' + esc(L.SoonBadge) + '</span>' : '') + '</button>';
        }).join('');
        host.innerHTML = '<div class="ve-card ve-extra"><div class="ve-card-head ve-start"><span class="ve-card-title ve-row ve-lg"><i class="bx bx-qr-scan ve-ic"></i> ' + esc(L.G_extra) + '</span></div>'
            + '<div class="ve-fields">'
            + '<div class="ve-field-row ve-col ve-gap-s' + (inheritedField('qrMode') ? ' ve-inherited' : '') + '"><span class="ve-cap">' + esc(L.QrCaption) + '</span><span class="ve-sub">' + esc(L.QrIntro) + '</span>'
            + '<div class="ve-choices" role="radiogroup" aria-label="' + esc(L.F_qrMode) + '">' + qr + '</div>'
            + (perms.qrCodes ? '<a class="ve-link" href="' + esc(qrCodesUrl) + '" id="ve-qr-manage">' + esc(L.ManageQrLink) + '</a>' : '') + layerRow('qrMode') + '</div>'
            + '<div class="ve-field-row ve-col ve-gap-s' + (inheritedField('antiFraudMode') ? ' ve-inherited' : '') + '"><span class="ve-cap">' + esc(L.DeviceCaption) + '</span><span class="ve-sub">' + esc(L.DeviceIntro) + '</span>'
            + '<div class="ve-seg' + (deviceDis ? ' ve-dis' : '') + '" role="radiogroup" aria-label="' + esc(L.F_antiFraudMode) + '">' + device + '</div>' + layerRow('antiFraudMode') + '</div>'
            + '</div></div>';
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
        // W3E-FIX (G-5) — the QR row (off → no row) and the device line on the web (antiFraudMode = flag)
        show('ve-pv-qr', !!pv.qr);
        if (pv.qr) { const q = el('ve-pv-qr-mode'); q.className = 've-pill ve-tone-' + pv.qr.tone; q.textContent = L[pv.qr.modeKey] || ''; }
        show('ve-pv-device', pv.deviceWeb);
        el('ve-pv-start').classList.toggle('ve-faded', pv.startDisabled);
        el('ve-pv-lines').innerHTML = pv.lines.map(l => '<li><i class="bx ' + l.icon + ' ve-tone-text-' + l.tone + '"></i><span>' + esc(T(l.key, ...l.args.map(a => F.isolate(a)))) + '</span></li>').join('');
    }

    /** The rows of the save confirm: the tenant's changed fields, or a layer's ({ from / to: { value, inherited } }). */
    const pendingChanges = () => (!state.draft ? [] : isLayer() ? C.layerChanges(state.layer.original, state.layer.ov, layerBase()) : C.changes(state.original, state.draft));
    const pendingErrors = () => (!state.draft ? {} : isLayer() ? C.layerFieldErrors(state.layer.ov, layerBase()) : C.fieldErrors(state.draft));
    /** A layer that overrides nothing cannot be saved (the CRM refuses it: such a scope is removed instead). */
    const layerEmpty = () => isLayer() && C.overriddenCount(state.layer.ov) === 0;
    /** How many reps a save reaches: the tenant's roster, a layer's own count (a scope not saved yet: none counted). */
    const repsReached = () => (isLayer() ? ((nodeOf(state.layer.id) || {}).repCount || 0) : (state.policy && state.policy.repCount) || 0);

    function renderDirty() {
        const changed = pendingChanges();
        const errs = pendingErrors();
        const empty = layerEmpty();
        const dirty = perms.managePolicy && (changed.length > 0 || (isLayer() && state.layer.isNew && !empty));
        show('ve-save-bar', perms.managePolicy); // read-only: no Save / Cancel at all (the mockup)
        show('ve-unsaved', dirty);
        setText('ve-layer-empty-text', L.LayerEmpty || '');
        show('ve-layer-empty', perms.managePolicy && empty);
        const save = el('ve-save'), revert = el('ve-revert');
        if (save) { save.disabled = !dirty || empty || Object.keys(errs).length > 0; }
        if (revert) { revert.disabled = !dirty; }
    }

    function changed() { renderForm(); renderPreview(); renderDirty(); renderScope(); }

    /** One side of a changed layer field in the confirm: its value, or "From above: X" / "Back to the setting above (X)". */
    const sideText = (field, side, to) => (side && side.inherited
        ? T(to ? 'DiffBackToUpper' : 'DiffInherited', valueText(field, side.value))
        : valueText(field, side && side.value !== undefined ? side.value : side));

    function askSave() {
        const rows = pendingChanges();
        if (layerEmpty() || (!rows.length && !(isLayer() && state.layer.isNew)) || Object.keys(pendingErrors()).length) { return; }
        setText('ve-save-text', isLayer() && state.layer.isNew ? L.SaveTextNew || '' : T('SaveText', F.isolate(repsReached())));
        el('ve-save-diff').innerHTML = rows.map(r => '<div class="ve-diff-row"><span>' + esc(L['F_' + r.field]) + '</span><span class="ve-row"><s class="ve-mu">'
            + esc(isLayer() ? sideText(r.field, r.from, false) : valueText(r.field, r.from)) + '</s> <i class="bx bx-right-arrow-alt ve-flip"></i> <strong>'
            + esc(isLayer() ? sideText(r.field, r.to, true) : valueText(r.field, r.to)) + '</strong></span></div>').join('');
        const m = modal('ve-save-modal'); if (m) { m.show(); }
    }

    /** The scope's proxy path (a legal entity id, a business unit code — escaped). */
    const scopePath = layer => '/policy/scopes/' + layer.type + '/' + encodeURIComponent(layer.ref);

    function doSave() {
        if (!perms.managePolicy) { return; } // read-only: the confirm is never shown, and never answered either
        const btn = el('ve-save-confirm'); if (btn) { btn.disabled = true; }
        const layer = state.layer;
        const reached = repsReached();
        const write = layer
            ? call('PUT', scopePath(layer), C.layerPutBody(layer.ov, layer.version)) // EVERY field: an inherited one as null
            : call('PUT', '/policy', C.putBody(state.draft, state.policy && state.policy.version));
        write.then(r => {
            if (btn) { btn.disabled = false; }
            const m = modal('ve-save-modal'); if (m) { m.hide(); }
            if (!r.ok) {
                setText('ve-save-error-text', errText(r));
                show('ve-save-error', true);
                return;
            }
            show('ve-save-error', false);
            toast(layer && layer.isNew ? L.ScopeCreated || '' : T('Saved', reached));
            if (!layer) { loadPolicy(); loadScopes(); return; } // → refreshTables: the history shows the new change, the reps their new values
            const saved = data(r);
            if (saved) { state.layerDetail[layer.id] = saved; state.layerOv[layer.id] = C.layerOverrides(saved); }
            loadScopes().then(() => selectScope(layer.id));
        });
    }

    // ── WP-VW-W3L (C2) — "Remove the scope" ─────────────────────────────────────────────────────────────────
    function askRemove() {
        const n = state.layer && nodeOf(state.layer.id);
        if (!n || n.isNew || !perms.managePolicy) { return; }
        setText('ve-rm-text', T('RemoveText', F.isolate(n.repCount || 0)));
        show('ve-rm-error', false);
        const m = modal('ve-rm-modal'); if (m) { m.show(); }
    }

    /** Archived at its version (never deleted; the history keeps it): its reps fall back to the scope above at once. */
    function doRemove() {
        const layer = state.layer;
        if (!layer || layer.isNew || !perms.managePolicy) { return; }
        const btn = el('ve-rm-confirm'); if (btn) { btn.disabled = true; }
        call('DELETE', scopePath(layer) + '?expectedVersion=' + encodeURIComponent(layer.version)).then(r => {
            if (btn) { btn.disabled = false; }
            if (!r.ok) { const n = el('ve-rm-error'); if (n) { n.textContent = errText(r); } show('ve-rm-error', true); return; }
            const m = modal('ve-rm-modal'); if (m) { m.hide(); }
            toast(L.Removed || '');
            delete state.layerOv[layer.id]; delete state.layerDetail[layer.id];
            state.sel = 'tenant';
            loadScopes().then(() => selectScope('tenant'));
        });
    }

    // ── WP-VW-W3L (C1) — "Add legal entity" / "Add business unit" ───────────────────────────────────────────
    /** The pick opens an EMPTY scope on the page (every field from above); nothing is written until its first save. */
    function openAdd(type) {
        if (!perms.managePolicy || C.LAYER_TYPES.indexOf(type) < 0) { return; }
        state.addType = type;
        const options = C.addOptions(type, state.options[type], state.tree.concat(state.pending ? [state.pending] : []));
        setText('ve-add-title', L[type === 'legal-entity' ? 'AddTitleLegalEntity' : 'AddTitleBusinessUnit'] || '');
        setText('ve-add-label', L[type === 'legal-entity' ? 'TypeLegalEntity' : 'TypeBusinessUnit'] || '');
        const pick = el('ve-add-pick');
        if (pick) {
            pick.innerHTML = '<option value="">' + esc(L.AddPlaceholder || '') + '</option>'
                + options.map(o => '<option value="' + esc(o.ref) + '">' + esc(F.isolate(o.name)) + '</option>').join('');
            pick.value = '';
            pick.disabled = !options.length;
        }
        const none = el('ve-add-none');
        if (none) { none.textContent = options.length ? '' : (state.optionsRead[type] ? L.AddNone : L.AddUnavailable) || ''; }
        show('ve-add-none', !options.length);
        const ok = el('ve-add-confirm'); if (ok) { ok.disabled = true; }
        const m = modal('ve-add-modal'); if (m) { m.show(); }
    }

    function confirmAdd() {
        const type = state.addType;
        const pick = el('ve-add-pick');
        const ref = pick ? pick.value : '';
        if (!type || !ref || !perms.managePolicy) { return; }
        const option = C.addOptions(type, state.options[type], state.tree).find(o => o.ref === C.normRef(type, ref));
        if (!option) { return; }
        state.pending = { id: option.id, type: type, ref: option.ref, name: option.name, overridden: 0, repCount: 0, repCountNote: null, version: 0, isNew: true, depth: 1 };
        const m = modal('ve-add-modal'); if (m) { m.hide(); }
        selectScope(option.id);
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
            renderEffective(dto);
            show('ve-effective', true);
            show('ve-scope', false);
            renderTree(); // the mockup: no tree row is selected while a rep is looked up
        });
    }

    /**
     * C1 — a rep's effective rules: every field's value and WHERE it comes from, by name (Kiracı / Tüzel kişilik: <name> /
     * İş birimi: <name> / the exception) — WP-VW-W3L; the rep's chain (legal entity › business unit) and why a layer was
     * skipped ("no position assignment — the legal entity layer was skipped", "more than one business unit").
     */
    function renderEffective(dto) {
        const resourceId = dto.resourceId || state.look;
        const name = state.repNames[resourceId] || resourceId;
        el('ve-eff-name').innerHTML = F.bidi(name);
        setText('ve-eff-initials', initials(name));
        const x = dto.activeException;
        const banner = el('ve-eff-exception');
        banner.innerHTML = x ? '<i class="bx bx-time-five"></i> ' + esc(T('ActiveException', (x.rules || []).map(k => L['XRule_' + k] || k).join(', '), day(x.endDate))) : '';
        show('ve-eff-exception', !!x);
        el('ve-eff-rows').innerHTML = C.effectiveRows(dto).map(row => {
            const src = C.sourceOf(row.source, state.names, BASE_LABELS);
            const isNew = C.NEW_FIELDS.indexOf(row.field) > -1; // W3E — the two new rows carry the mockup's "new" badge
            return '<tr' + (isNew ? ' class="ve-row-new"' : '') + '><td>' + esc(L['F_' + row.field]) + (isNew ? ' <span class="ve-pill ve-tone-primary ve-pill-xs">' + esc(L.NewBadge) + '</span>' : '') + '</td><td>' + esc(valueText(row.field, row.value)) + '</td><td><span class="ve-pill ve-tone-' + src.tone + '">'
                + esc(sourceText(row.source, x ? day(x.endDate) : '')) + '</span></td></tr>';
        }).join('');
        const chain = dto.scopeChain || {};
        const links = [];
        if (chain.legalEntity) { links.push('<span class="ve-pill ve-tone-info">' + F.bidi(scopeLabel('legal-entity', chain.legalEntity)) + '</span>'); }
        if (chain.businessUnit) { links.push('<span class="ve-pill ve-tone-primary">' + F.bidi(scopeLabel('business-unit', chain.businessUnit)) + '</span>'); }
        el('ve-eff-chain').innerHTML = links.join(' <i class="bx bx-chevron-right ve-flip" aria-hidden="true"></i> ');
        const notes = C.chainNotes(chain);
        el('ve-eff-notes').innerHTML = notes.map(k => '<span class="ve-sub ve-row"><i class="bx bx-info-circle" aria-hidden="true"></i> ' + esc(L[k] || '') + '</span>').join('');
        show('ve-eff-chain-box', links.length > 0 || notes.length > 0);
    }

    function closeLook() {
        state.look = ''; state.effective = null;
        const s = el('ve-look'); if (s) { s.value = ''; }
        show('ve-effective', false);
        show('ve-scope', true);
        renderTree();
    }

    // ── C3 — the change history ──────────────────────────────────────────────────────────────────────────────
    /** C3 — one history change, its "old → new" cell (a creation: "— → Settings created"). */
    function historyChange(item) {
        const x = C.historyRows([item])[0];
        const arrow = ' <i class="bx bx-right-arrow-alt ve-flip" aria-hidden="true"></i> ';
        // WP-VW-W3L — a layer's creation / removal, and a side that is "from above"
        if (x.kind === 'created' || x.kind === 'archived') {
            const key = x.kind === 'archived' ? 'HistArchived' : x.scope.type === 'tenant' ? 'HistCreated' : 'HistScopeCreated';
            return '<span class="ve-mu">—</span>' + arrow + '<strong>' + esc(L[key]) + '</strong>';
        }
        const side = (raw, inherited) => (inherited ? L.HistInherited : valueText(x.field, C.typed(x.field, raw)));
        return '<span class="ve-mu">' + esc(side(x.from, x.fromInherited)) + '</span>' + arrow + '<strong>' + esc(side(x.to, x.toInherited)) + '</strong>';
    }

    /** A scope as a chip (its tone, its NAME). */
    const scopeChip = (type, ref) => '<span class="ve-pill ve-tone-' + C.SCOPE_TONE[type] + '">' + F.bidi(type === 'tenant' ? L.SourceTenant : scopeLabel(type, ref)) + '</span>';

    /** C3 — the change history: the Golden Compact DataTable (server mode), built once, when its pane first shows; its scope
     *  filter rides on every read (WP-VW-W3L). */
    function buildHistory() {
        const tableEl = el('dt-ve-history');
        if (state.tables.history || !tableEl || !window.VisitEvidenceSettingsTables) { return state.tables.history; }
        state.tables.history = window.VisitEvidenceSettingsTables.history({
            tableEl: tableEl,
            endpoint: base,
            onNames: names => C.mergeNames(state.people, names),
            query: () => C.historyQuery(state.historyFilter),
            cells: {
                esc: esc,
                when: when,
                person: id => F.bidi(nameOf(id)),
                field: f => (f ? L['F_' + f] || f : '—'),
                change: historyChange,
                scope: (row, type) => { const s = C.historyScope(row); return type === 'display' ? scopeChip(s.type, s.ref) : (s.type === 'tenant' ? L.SourceTenant : scopeLabel(s.type, s.ref)); }
            }
        });
        return state.tables.history;
    }

    /** C3 — the history's scope filter: every scope, the tenant, each layer (by name); it follows the selected scope. */
    function renderHistoryFilter() {
        const s = el('ve-history-scope');
        if (!s) { return; }
        const nodes = state.tree.filter(n => n.type !== 'tenant');
        const known = state.historyFilter === '' || state.historyFilter === 'tenant' || nodes.some(n => n.id === state.historyFilter);
        if (!known) { state.historyFilter = ''; }
        s.innerHTML = '<option value="">' + esc(L.HistAllScopes || '') + '</option>'
            + '<option value="tenant"' + (state.historyFilter === 'tenant' ? ' selected' : '') + '>' + esc(L.TenantDefault || '') + '</option>'
            + nodes.map(n => '<option value="' + esc(n.id) + '"' + (state.historyFilter === n.id ? ' selected' : '') + '>' + esc(F.isolate(n.name)) + '</option>').join('');
        s.value = state.historyFilter;
    }

    /**
     * WP-VW-W3E (W3E-WEB-b, E3) — one rep's effective location / photo / QR / device value: the tenant policy relaxed by the
     * rep's approved exception active today ("· exception"), from the exception list the page already reads (no read per
     * rep). W3E-E4R2-FIX-d — the rule of C.repRows, one roster row at a time (the roster page is the CRM's).
     */
    function repCell(rep, field, type) {
        if (!state.policy) { return type === 'display' ? '<span class="ve-mu">—</span>' : ''; }
        // WP-VW-W3L — the rep's layers (the roster row's chain) over the tenant policy, then the exception
        const c = C.repRows([rep], state.policy, state.list ? state.list.items : [], state.layerOv)[0].cells[field];
        const text = valueText(field, c.value);
        if (type !== 'display') { return text; }
        return esc(text) + (c.fromException ? ' <span class="ve-mu">· ' + esc(L.SourceExceptionShort) + '</span>' : '');
    }

    /** WP-VW-W3L — the layers that apply to the rep today (legal entity, business unit — by name), the tenant when none;
     *  a skipped layer's reason as the chip's note. */
    function repScope(rep, type) {
        const applied = C.appliedLayers(rep.scopeChain, state.tree);
        const notes = C.chainNotes(rep.scopeChain).map(k => L[k] || '').filter(Boolean);
        if (type !== 'display') { return applied.length ? applied.map(n => n.name).join(' › ') : L.SourceTenant; }
        const chips = applied.length ? applied.map(n => scopeChip(n.type, n.ref)).join(' ') : scopeChip('tenant', null);
        return '<span class="ve-row ve-wrap">' + chips + (notes.length ? ' <i class="bx bx-info-circle ve-mu" title="' + esc(notes.join(' · ')) + '" aria-label="' + esc(notes.join(' · ')) + '"></i>' : '') + '</span>';
    }

    function buildReps() {
        const tableEl = el('dt-ve-reps');
        if (state.tables.reps || !tableEl || !window.VisitEvidenceSettingsTables) { return state.tables.reps; }
        state.tables.reps = window.VisitEvidenceSettingsTables.reps({
            tableEl: tableEl,
            endpoint: base,
            cells: {
                isNew: f => C.NEW_FIELDS.indexOf(f) > -1,
                rep: r => { const name = r.displayName || r.resourceId; state.repNames[r.resourceId] = name; return '<div class="ve-row"><span class="ve-avatar ve-avatar-sm">' + esc(initials(name)) + '</span><span class="ve-strong">' + F.bidi(name) + '</span></div>'; },
                scope: repScope,
                value: repCell
            }
        });
        return state.tables.reps;
    }

    /** The policy / the exception list changed: a table already built reads its page again (never one not yet shown). */
    function refreshTables() {
        if (state.tables.history) { state.tables.history.reload(); }
        if (state.tables.reps) { state.tables.reps.redraw(); }
    }

    /** A table drawn while its pane was hidden measures itself when the pane shows (K-TABS). */
    function adjust(table) {
        const dt = table && table.table;
        try { if (dt && dt.columns) { dt.columns.adjust(); } if (dt && dt.responsive) { dt.responsive.recalc(); } } catch (e) { /* not drawn yet: nothing to measure */ }
    }

    /** K-TABS — the inner tab card selected a pane (diten-tabs.js has shown it already). */
    function setPane(pane) {
        state.pane = pane;
        if (pane === 'history') { adjust(buildHistory()); }
        if (pane === 'reps') { adjust(buildReps()); }
    }

    // ── C4 — the exceptions ──────────────────────────────────────────────────────────────────────────────────
    /** K-TABS — selects a tab of the shared tab card from code (the "?exception=" link). */
    function setTab(tab) {
        state.tab = tab;
        if (window.DitenTabs) { window.DitenTabs.activate(el('ve-tab-' + tab)); }
    }

    /** The tab's corner count (in review + active today — the CRM's counts over every exception): the shared tab card's
     *  own badge (_TabCard.cshtml draws the same span for a Count), written here because the count is the list read's. */
    const BADGE_CLASS = 'badge rounded-pill bg-danger wcn-tab-count position-absolute top-0 start-100 translate-middle';
    function renderBadge(n) {
        const tab = el('ve-tab-exceptions');
        if (!tab) { return; }
        let badge = tab.querySelector('.wcn-tab-count');
        if (!n) { if (badge) { badge.remove(); } return; }
        if (!badge) { badge = document.createElement('span'); badge.className = BADGE_CLASS; tab.appendChild(badge); }
        badge.textContent = F.isolate(n);
    }

    /** The full list (every exception: the reps pane, the corner count, the "?exception=" link); the Golden Compact list
     *  (index.js) reads its own, filtered by the reader — a change re-reads both. */
    function loadExceptions() {
        if (!perms.read) { return Promise.resolve(); }
        const table = window.VisitEvidenceExceptionsList;
        if (table && typeof table.reload === 'function') { table.reload(); }
        return get('/exceptions').then(r => {
            const list = absorb(data(r));
            if (!list) { return; }
            state.list = list;
            (list.items || []).forEach(x => { if (x.resourceId && x.repDisplayName) { state.repNames[x.resourceId] = x.repDisplayName; } });
            renderBadge(C.tabBadge(list));
            if (state.tables.reps) { state.tables.reps.redraw(); }
            openLinkedException();
        });
    }

    /** WP-VW-W3E (W3E-WEB-b, M2) — "?exception=<id>" (the evidence flags drawer's link) opens that exception once. */
    let linkedOpened = false;
    function openLinkedException() {
        if (linkedOpened) { return; }
        linkedOpened = true;
        const id = new URLSearchParams((window.location && window.location.search) || '').get('exception');
        if (id && /^[0-9a-fA-F-]{36}$/.test(id)) { setTab('exceptions'); openException(id, { show: true }); }
    }

    const chipHtml = x => { const c = C.chipOf(x); return '<span class="ve-pill ve-tone-' + c.tone + '">' + esc(L[c.key] || '') + '</span>'; };
    const rulesHtml = x => (x.rules || []).map(k => '<span class="ve-pill ve-tone-warning">' + esc(L['XRule_' + k] || k) + '</span>').join('');

    /** The quick view (#offcanvasDetailsPreview, the list's standard offcanvas): drawn at once from the list row when
     *  there is one, then from the exception's own read. The list's quick view action opens the offcanvas itself; the
     *  "?exception=" link and a just-sent new exception ask for { show: true }. */
    let detailToken = 0;
    function openException(id, options) {
        const o = options || {};
        const token = ++detailToken;
        state.close = null; state.closeTried = false;
        if (o.row && o.row.id === id) { state.current = absorb(o.row); renderDetail(); }
        if (o.show) { const d = drawer('offcanvasDetailsPreview'); if (d) { d.show(); } }
        return get('/exceptions/' + encodeURIComponent(id)).then(r => {
            const x = absorb(data(r));
            if (token !== detailToken) { return; }
            if (!x) { toast(errText(r), 'error'); return; }
            state.current = x;
            renderDetail();
        });
    }

    function renderDetail(errorText) {
        const x = state.current;
        if (!x) { return; }
        const a = C.actionsOf(x, perms, me);
        const tl = C.timeline(x);
        const sub = el('oc-subtitle');
        if (sub) { sub.innerHTML = F.bidi(x.repDisplayName || x.resourceId) + ' · ' + esc(day(x.startDate)) + ' – ' + esc(day(x.endDate)); }
        el('oc-details-list').innerHTML = '<div class="ve-row ve-between ve-top"><div class="ve-row"><span class="ve-avatar">' + esc(initials(x.repDisplayName)) + '</span><div class="ve-col"><span class="ve-name">' + F.bidi(x.repDisplayName || x.resourceId) + '</span></div></div>' + chipHtml(x) + '</div>'
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

        const btn = (action, label, cls, disabled, mark) => '<button type="button" class="ve-btn ' + cls + '" data-xact="' + action + '"' + (mark ? ' data-dt-primary="' + mark + '"' : '') + (disabled ? ' disabled aria-disabled="true"' : '') + '>' + esc(L[label] || '') + '</button>';
        const buttons = [btn('close', 'CloseButton', 've-btn-s')];
        if (state.close) {
            buttons.push(btn('back', 'CancelButton', 've-btn-s'));
            // UXH-001 §3 — with the reason field shown, this confirm is the panel's primary: a return / cancel / early end
            // cannot be taken back, so Ctrl / ⌘ + Enter only (chord)
            buttons.push(btn('confirm-' + state.close, state.close === 'return' ? 'XBtnConfirmReturn' : state.close === 'cancel' ? 'XBtnConfirmCancel' : 'XBtnConfirmEndEarly', state.close === 'return' ? 've-btn-w' : 've-btn-d', false, 'chord'));
        } else {
            if (a.withdraw) { buttons.push(btn('withdraw', 'XBtnWithdraw', 've-btn-s')); }
            if (a.submit) { buttons.push(btn('submit', 'XBtnSubmit', 've-btn-p')); }
            if (a.cancel) { buttons.push(btn('cancel', 'XBtnCancel', 've-btn-od')); }
            if (a.endEarly) { buttons.push(btn('end-early', 'XBtnEndEarly', 've-btn-od')); }
            if (a.decide) { buttons.push(btn('return', 'XBtnReturn', 've-btn-w')); }
            if (a.decide || a.sod) { buttons.push(btn('approve', 'XBtnApprove', 've-btn-ok', a.approveDisabled)); }
        }
        el('oc-actions').innerHTML = buttons.join('');
        // the shared Enter module tells the reader about a chord mark when the panel opens; this one is drawn after that
        if (window.DitenEnterPrimary && typeof window.DitenEnterPrimary.decorate === 'function') { window.DitenEnterPrimary.decorate(el('oc-actions')); }
    }

    function exceptionAction(action) {
        const x = state.current; if (!x) { return; }
        const path = '/exceptions/' + encodeURIComponent(x.id);
        const done = (r, toastKey) => {
            if (!r.ok) { renderDetail(errText(r)); return; }
            state.current = absorb(data(r)) || x;
            state.close = null; state.closeTried = false;
            toast(L[toastKey] || '');
            renderDetail();
            loadExceptions();
        };
        if (action === 'close') { const d = drawer('offcanvasDetailsPreview'); if (d) { d.hide(); } return; }
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
                : st.reason === 'XRuleQrNotRequired' ? L.XRuleQrNotRequired
                // WP-VW-W3L — where the rep's value comes from, by name (a layer: "Tüzel kişilik: <name>" / "İş birimi: <name>")
                : st.reason === 'XRuleAlready' ? T('XRuleAlready', valueText(C.RULE_FIELD[rule].field, C.RULE_FIELD[rule].value), sourceShort(st.source))
                    : T('XRuleNow', valueText(st.field, st.value), sourceShort(st.source));
            const isNew = C.NEW_RULES.indexOf(rule) > -1; // W3E (E2) — "relax the QR requirement", with its "new" badge + description
            return '<label class="ve-check' + (on ? ' active' : '') + (st.enabled ? '' : ' ve-dis') + '"><input type="checkbox" data-rule="' + rule + '"' + (on ? ' checked' : '') + (st.enabled ? '' : ' disabled') + '>'
                + '<span class="ve-col"><span>' + esc(L['XRule_' + rule]) + (isNew ? ' <span class="ve-pill ve-tone-primary ve-pill-xs">' + esc(L.NewBadge) + '</span>' : '') + '</span>'
                + (isNew ? '<span class="ve-sub">' + esc(L.XRuleDesc_qr_optional) + '</span>' : '') + '<span class="ve-sub">' + esc(note) + '</span></span></label>';
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
                state.current = absorb(data(s)) || created;
                loadExceptions();
                if (s.ok) { toast(L.XSubmitted || ''); renderDetail(); } else { renderDetail(T('XCreatedNotSent', errText(s))); }
                const x = drawer('offcanvasDetailsPreview'); if (x) { x.show(); }
            });
        });
    }

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────
    const on = (id, ev, fn) => { const n = el(id); if (n) { n.addEventListener(ev, fn); } };

    root.addEventListener('click', e => {
        const opt = e.target.closest('button[data-opt]');
        // W3E — an "additional evidence" value is taken only when it is one of the field's wire values (never "block")
        if (opt && !opt.disabled && perms.managePolicy && !inheritedField(opt.getAttribute('data-field')) && (C.KIND[opt.getAttribute('data-field')] !== 'extra' || C.canSelect(opt.getAttribute('data-field'), opt.getAttribute('data-opt')))) { setField(opt.getAttribute('data-field'), opt.getAttribute('data-opt')); changed(); return; }
        const sw = e.target.closest('[data-switch]');
        if (sw && !sw.disabled && perms.managePolicy && !inheritedField(sw.getAttribute('data-switch'))) { const f = sw.getAttribute('data-switch'); setField(f, !state.draft[f]); changed(); return; }
        // WP-VW-W3L — the tree (a scope; "add"), a layer field's "Change in this scope" (takes the value it inherits) and
        // "Back to the setting above" (the field is left empty: from above again)
        const node = e.target.closest('[data-scope]');
        if (node) { selectScope(node.getAttribute('data-scope')); return; }
        const add = e.target.closest('[data-add]');
        if (add && perms.managePolicy) { openAdd(add.getAttribute('data-add')); return; }
        const take = e.target.closest('[data-take]');
        if (take && isLayer() && perms.managePolicy) { const f = take.getAttribute('data-take'); setField(f, state.draft[f]); changed(); return; }
        const drop = e.target.closest('[data-drop]');
        if (drop && isLayer() && perms.managePolicy) { setField(drop.getAttribute('data-drop'), null); changed(); }
    });
    // K-TABS — the shared tab cards say which tab / pane shows: the outer (settings · exceptions), the inner (form · history
    // · reps); coming back to the settings tab measures the pane's table again (it was laid out hidden)
    document.addEventListener('diten:tab-change', e => {
        const key = e.detail && e.detail.key;
        if (key === 'settings' || key === 'exceptions') {
            state.tab = key;
            if (key === 'settings') { adjust(state.pane === 'history' ? state.tables.history : state.pane === 'reps' ? state.tables.reps : null); }
            return;
        }
        if (key === 'form' || key === 'history' || key === 'reps') { setPane(key); }
    });
    root.addEventListener('change', e => {
        const radio = e.target.closest && e.target.closest('input[data-opt]');
        if (radio && perms.managePolicy && !inheritedField(radio.getAttribute('data-field'))) { setField(radio.getAttribute('data-field'), radio.getAttribute('data-opt')); changed(); }
    });
    root.addEventListener('input', e => {
        const num = e.target.closest && e.target.closest('[data-num]');
        if (num && perms.managePolicy && !inheritedField(num.getAttribute('data-num'))) {
            const f = num.getAttribute('data-num');
            setField(f, num.value);
            renderPreview(); renderDirty();
            const errs = pendingErrors();
            num.classList.toggle('ve-invalid', !!errs[f]);
        }
    });
    root.addEventListener('focusout', e => { if (e.target.closest && e.target.closest('[data-num]')) { renderForm(); } });

    on('ve-save', 'click', askSave);
    on('ve-revert', 'click', () => {
        if (isLayer()) { state.layer.ov = Object.assign({}, state.layer.original); state.draft = C.layerEffective(state.policy, state.layer.ov); }
        else { state.draft = C.draftOf(state.original); }
        show('ve-save-error', false); changed();
    });
    on('ve-save-confirm', 'click', doSave);
    on('ve-save-retry', 'click', askSave);
    on('ve-look', 'change', e => openLook(e.target.value));
    on('ve-eff-close', 'click', closeLook);
    on('ve-node-tenant', 'click', () => { if (state.look) { closeLook(); } if (state.sel !== 'tenant') { selectScope('tenant'); } });
    // WP-VW-W3L — remove a layer, add one, the history's scope filter
    on('ve-scope-remove', 'click', askRemove);
    on('ve-rm-confirm', 'click', doRemove);
    on('ve-add-pick', 'change', e => { const ok = el('ve-add-confirm'); if (ok) { ok.disabled = !e.target.value; } });
    on('ve-add-confirm', 'click', confirmAdd);
    on('ve-history-scope', 'change', e => { state.historyFilter = e.target.value; if (state.tables.history) { state.tables.history.reload(); } });
    on('ve-look-search', 'input', e => { clearTimeout(repTimer); const q = e.target.value.trim(); repTimer = setTimeout(() => loadReps(q, 've-look', state.look), 250); });
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
    const xFoot = el('oc-actions');
    if (xFoot) {
        xFoot.addEventListener('click', e => {
            const b = e.target.closest('[data-xact]');
            if (b && !b.disabled) { exceptionAction(b.getAttribute('data-xact')); }
        });
    }

    /** W3E-E4R2-FIX-b — what the exceptions list (index.js) asks of this page: its "New exception" action (only offered
     *  with the manage key) and its quick view. */
    window.VisitEvidenceSettingsPage = {
        openNew: () => { if (perms.manage) { openNew(); } },
        openException: (id, options) => openException(id, options)
    };

    loadReps('', 've-look', '');
    loadPolicy();
    loadExceptions();
    renderTree();
    renderHistoryFilter();
    loadScopes().then(loadOptions); // WP-VW-W3L — the layers, then their names (the tree first shows their codes)
}(window, document));
