/**
 * WP-VW-W4 (W4-WEB-b, §2.2) — the potential thresholds page. Every rule lives in thresholds-core.js (pure, tested in
 * Node); every text comes from window.PotentialThresholdsL10n (7 languages) or from the CRM / MDM (people, products).
 * Dates / numbers in the APPLICATION language through VisitPlanningFormat; every data name isolated (bidi) for RTL.
 * Calls go to the same-origin proxy /CRM/PotentialThresholds/api/*.
 *   The table: the tenant default first, then the product rows ("Az < X · Orta · Çok > Y", source, last change).
 *   The drawer: a product (new row) or the row's values, the band preview, the field checks; Save → the confirm (the
 *   changed values) → PUT. The history below (append-only, newest first).
 */
(function (window, document) {
    'use strict';

    const C = window.PotentialThresholdsCore;
    const F = window.VisitPlanningFormat;
    const L = window.PotentialThresholdsL10n || {};
    const root = document.getElementById('pt-root');
    if (!C || !F || !root) { return; }

    const base = root.getAttribute('data-api-base') || '/CRM/PotentialThresholds/api';
    const canManage = root.getAttribute('data-can-manage') === 'true';
    const canProducts = root.getAttribute('data-can-read-products') === 'true';
    const me = root.getAttribute('data-me') || '';

    const el = id => document.getElementById(id);
    const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const T = (key, ...args) => fmt(L[key] || '', ...args);
    const show = (id, on) => { const n = el(id); if (n) { n.classList.toggle('d-none', !on); } };
    const toast = (msg, type) => { if (window.showToast) { window.showToast(msg, type || 'success'); } };
    const modal = id => (window.bootstrap && el(id) ? window.bootstrap.Modal.getOrCreateInstance(el(id)) : null);
    const drawer = id => (window.bootstrap && el(id) ? window.bootstrap.Offcanvas.getOrCreateInstance(el(id)) : null);
    const num = n => (n == null || n === '' ? '—' : Number.isFinite(Number(n)) ? F.number(n, 0) : String(n));
    const when = iso => (iso ? F.dateTime(iso) : '—');

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
    const data = r => (r && r.ok && r.body ? (r.body.data !== undefined ? r.body.data : r.body) : null);
    const errText = r => L[C.errorKey(r)] || L.Err_generic || '';

    const state = { dto: null, rows: [], products: [], names: {}, people: {}, history: [], edit: null, pending: null, tried: false };

    const productName = id => state.names[id] || String(id || '').slice(0, 8);
    const personName = id => (!id ? '—' : id === me ? (L.YouLabel || id) : (state.people[id] || id));
    const rowName = row => (row.key === C.DEFAULT ? esc(L.TenantDefault) : F.bidi(productName(row.productId)));
    const sourceChip = src => '<span class="pt-pill pt-tone-' + (src === 'product' ? 'primary' : src === 'default' ? 'secondary' : 'muted') + '">' + esc(L['Source_' + src] || src) + '</span>';

    // ── the table ────────────────────────────────────────────────────────────────────────────────────────────
    function load() {
        return call('GET', '/thresholds').then(r => {
            const d = data(r);
            if (!d) { el('pt-load-error-text').textContent = errText(r); show('pt-load-error', true); return; }
            show('pt-load-error', false);
            state.dto = d;
            renderRows();
        });
    }

    function renderRows() {
        state.rows = C.rowsOf(state.dto, state.names);
        const body = el('pt-rows');
        if (!state.rows.length) { body.innerHTML = '<tr><td colspan="7" class="pt-empty">' + esc(L.NoProducts) + '</td></tr>'; return; }
        body.innerHTML = state.rows.map(row => '<tr' + (row.key === C.DEFAULT ? ' class="pt-default"' : '') + '>'
            + '<td><span class="pt-strong">' + rowName(row) + '</span>' + (row.key === C.DEFAULT ? ' <i class="bx bx-lock-alt pt-mu" aria-hidden="true"></i>' : '') + '</td>'
            + '<td><span class="pt-band pt-band-low">&lt; ' + esc(num(row.lowBelow)) + '</span></td>'
            + '<td><span class="pt-band pt-band-medium">' + esc(num(row.lowBelow)) + '–' + esc(num(row.highAbove)) + '</span></td>'
            + '<td><span class="pt-band pt-band-high">&gt; ' + esc(num(row.highAbove)) + '</span></td>'
            + '<td>' + sourceChip(row.source) + '</td>'
            + '<td class="pt-sub">' + (row.updatedAt ? esc(when(row.updatedAt)) + ' · ' + F.bidi(personName(row.updatedBy)) : esc(L.Never)) + '</td>'
            + '<td class="pt-actions">' + (canManage ? '<button type="button" class="pt-btn pt-btn-o pt-btn-sm" data-edit="' + esc(row.key) + '"><i class="bx bx-edit-alt"></i> ' + esc(L.EditButton) + '</button>' : '') + '</td>'
            + '</tr>').join('');
    }

    function loadProducts() {
        if (!canProducts) { return Promise.resolve(); }
        return call('GET', '/lookups/products').then(r => {
            const d = data(r);
            state.products = Array.isArray(d) ? d : [];
            state.products.forEach(p => { if (p && p.id) { state.names[p.id] = p.name || p.code || p.id; } });
            if (state.dto) { renderRows(); }
            if (!Array.isArray(d)) { state.productsError = errText(r); }
        });
    }

    // ── the history ──────────────────────────────────────────────────────────────────────────────────────────
    function loadHistory() {
        return call('GET', '/thresholds/history?page=1&pageSize=100').then(r => {
            const d = data(r);
            if (d && d.displayNames) { Object.keys(d.displayNames).forEach(k => { state.people[k] = d.displayNames[k]; }); }
            state.history = C.historyRows(d && d.items, state.names);
            renderHistory();
            if (state.dto) { renderRows(); } // the CRM's names also label the rows' "last change"
        });
    }

    function renderHistory() {
        const body = el('pt-history-rows');
        if (!state.history.length) { body.innerHTML = '<tr><td colspan="5" class="pt-empty">' + esc(L.NoHistory) + '</td></tr>'; return; }
        body.innerHTML = state.history.map(h => '<tr>'
            + '<td class="pt-nowrap">' + esc(when(h.at)) + '</td>'
            + '<td>' + F.bidi(personName(h.by)) + '</td>'
            + '<td>' + (h.productId ? F.bidi(productName(h.productId)) : esc(L.HistoryDefault)) + '</td>'
            + '<td>' + esc(L['Field_' + h.field] || h.field) + '</td>'
            + '<td><span class="pt-old">' + esc(num(h.oldValue)) + '</span> <i class="bx bx-right-arrow-alt pt-flip" aria-hidden="true"></i> <span class="pt-strong">' + esc(num(h.newValue)) + '</span></td>'
            + '</tr>').join('');
    }

    // ── the drawer ───────────────────────────────────────────────────────────────────────────────────────────
    function openEdit(key) {
        const row = key ? state.rows.find(r => r.key === key) : null;
        state.edit = row ? Object.assign({}, row) : { key: null, productId: null, lowBelow: null, highAbove: null, version: 0, source: null };
        if (!row) {
            // a new product row starts from the tenant default (what the product has today)
            const def = state.rows.find(r => r.key === C.DEFAULT);
            if (def) { state.edit.lowBelow = def.lowBelow; state.edit.highAbove = def.highAbove; state.edit.source = 'default'; }
        }
        state.tried = false;
        el('pt-edit-title').innerHTML = row ? fmt(esc(L.EditTitle), rowName(row)) : esc(L.NewTitle);
        show('pt-product-field', !row);
        if (!row) { renderProductOptions(); }
        el('pt-low').value = state.edit.lowBelow == null ? '' : String(state.edit.lowBelow);
        el('pt-high').value = state.edit.highAbove == null ? '' : String(state.edit.highAbove);
        el('pt-try').value = '';
        show('pt-edit-error', false);
        renderEdit();
        const d = drawer('pt-edit-drawer'); if (d) { d.show(); }
    }

    function renderProductOptions() {
        const taken = {};
        state.rows.forEach(r => { if (r.productId) { taken[r.productId] = true; } });
        const free = state.products.filter(p => p && p.id && !taken[p.id]);
        el('pt-product').innerHTML = '<option value="">' + esc(state.productsError || L.ProductPlaceholder) + '</option>'
            + free.map(p => '<option value="' + esc(p.id) + '">' + esc(p.name || p.code || p.id) + (p.code && p.name ? ' · ' + esc(p.code) : '') + '</option>').join('');
        el('pt-product').value = '';
    }

    function check() {
        const v = C.validate(el('pt-low').value, el('pt-high').value);
        const productMissing = !state.edit.key && !el('pt-product').value;
        return { v, productMissing };
    }

    function renderEdit() {
        const { v, productMissing } = check();
        const err = (id, key) => { const n = el(id); n.textContent = key ? (L[key] || '') : ''; n.classList.toggle('d-none', !(key && state.tried)); };
        err('pt-err-low', v.errors.low);
        err('pt-err-high', v.errors.high);
        err('pt-err-product', productMissing ? 'ErrProduct' : null);
        const low = v.ok ? v.low : null;
        const high = v.ok ? v.high : null;
        el('pt-bands').innerHTML = v.ok
            ? '<span class="pt-band pt-band-low">' + esc(T('BandLow', num(low))) + '</span>'
              + '<span class="pt-band pt-band-medium">' + esc(T('BandMedium', num(low), num(high))) + '</span>'
              + '<span class="pt-band pt-band-high">' + esc(T('BandHigh', num(high))) + '</span>'
            : '<span class="pt-sub">—</span>';
        const tryValue = el('pt-try').value;
        const cls = v.ok && tryValue !== '' ? C.classOf(tryValue, { lowBelow: low, highAbove: high }) : null;
        el('pt-try-class').innerHTML = cls ? '<span class="pt-band pt-band-' + cls + '">' + esc(L['Class_' + cls]) + '</span>' : '';
    }

    function save() {
        state.tried = true;
        renderEdit();
        const { v, productMissing } = check();
        if (!v.ok || productMissing) { return; }
        const key = state.edit.key || el('pt-product').value;
        const before = state.edit.key ? state.edit : Object.assign({}, state.edit, { key: key });
        const rows = C.diff(before, v.low, v.high);
        if (!rows.length) { toast(L.NothingChanged, 'info'); return; }
        state.pending = { key: key, low: v.low, high: v.high, version: state.edit.key ? (state.edit.version || 0) : 0 }; // new row: 0 = nobody created it meanwhile
        el('pt-save-text').textContent = key === C.DEFAULT ? (L.SaveTextDefault || '') : T('SaveText', productName(key));
        el('pt-save-diff').innerHTML = rows.map(r => '<div class="pt-diff-row"><span>' + esc(L['Field_' + r.field] || r.field) + '</span>'
            + (r.field === 'own'
                ? '<span class="pt-strong">' + esc(L.OwnRow) + '</span>'
                : '<span><span class="pt-old">' + esc(num(r.oldValue)) + '</span> <i class="bx bx-right-arrow-alt pt-flip" aria-hidden="true"></i> <span class="pt-strong">' + esc(num(r.newValue)) + '</span></span>')
            + '</div>').join('');
        const m = modal('pt-save-modal'); if (m) { m.show(); }
    }

    function confirmSave() {
        const p = state.pending;
        if (!p) { return; }
        const button = el('pt-save-confirm');
        button.disabled = true;
        return call('PUT', '/thresholds/' + encodeURIComponent(p.key), C.putBody(p.low, p.high, p.version)).then(r => {
            button.disabled = false;
            const m = modal('pt-save-modal'); if (m) { m.hide(); }
            if (!r.ok) { const n = el('pt-edit-error'); n.textContent = errText(r); show('pt-edit-error', true); return; }
            state.pending = null;
            const d = drawer('pt-edit-drawer'); if (d) { d.hide(); }
            toast(L.Saved);
            return Promise.all([load(), loadHistory()]);
        });
    }

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────
    root.addEventListener('click', e => {
        const t = e.target && e.target.closest ? e.target.closest('[data-edit]') : null;
        if (t && canManage) { openEdit(t.getAttribute('data-edit')); }
    });
    const on = (id, ev, fn) => { const n = el(id); if (n) { n.addEventListener(ev, fn); } };
    on('pt-add', 'click', () => { if (canManage) { openEdit(null); } });
    on('pt-low', 'input', renderEdit);
    on('pt-high', 'input', renderEdit);
    on('pt-try', 'input', renderEdit);
    on('pt-product', 'change', renderEdit);
    on('pt-save', 'click', () => { if (canManage) { save(); } });
    on('pt-save-confirm', 'click', () => { if (canManage) { confirmSave(); } });
    on('pt-retry', 'click', () => { load(); loadHistory(); });

    Promise.all([load(), loadProducts()]).then(loadHistory);
}(window, document));
