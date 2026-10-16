/**
 * WP-VW-W4 (W4-WEB-b, §2.2) — the potential thresholds page. Every rule lives in thresholds-core.js (pure, tested in
 * Node); every text comes from window.L10n (the standard payload _IndexL10n.cshtml, 7 languages) or from the CRM / MDM
 * (people, products). Dates / numbers in the APPLICATION language through VisitPlanningFormat; every data name isolated
 * (bidi) for RTL. Calls go to the same-origin proxy /CRM/PotentialThresholds/api/*.
 *
 * W3E-E4R2-FIX-c (U-5, K-STD, K-TABS) — THE THRESHOLDS ARE THE SHARED LIST COMPONENT: DitenDataTable.createList in CLIENT
 * mode (the CRM answers the tenant default + every product row in one read; the browser pages, sorts, searches the product
 * name and filters the source). The tenant default stays the first row. The page action "add a product" is the toolbar's
 * Add New. The row actions: quick view + Edit. The edit drawer, the band preview, the field checks and the save confirm are
 * AS THEY WERE (Save → the confirm with the changed values → PUT with the row's version); a save reloads the list through
 * the standard reload-with-toast lifecycle and the change history (history.js, the second table, server mode).
 */
'use strict';

const PotentialThresholdsList = (function () {
    let list = null;
    let history = null;

    const dtTableEl = document.querySelector('.datatables-potentialthresholds');
    const historyTableEl = document.querySelector('.datatables-pt-history');
    const root = document.getElementById('pt-root');
    const endpoint = '/CRM/PotentialThresholds/api';
    const C = window.PotentialThresholdsCore;
    const F = window.VisitPlanningFormat;
    const L = window.L10n || {};
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const canManage = !!root && root.getAttribute('data-can-manage') === 'true';
    const canAdd = !!root && root.getAttribute('data-can-add') === 'true';
    const canProducts = !!root && root.getAttribute('data-can-read-products') === 'true';
    const me = (root && root.getAttribute('data-me')) || '';
    // 0 = the responsive control · product 1 · low 2 · medium 3 · high 4 · source 5 · last change 6 · actions 7
    const DATA_COLUMNS = [1, 2, 3, 4, 5, 6];
    const SOURCES = ['product', 'default', 'built_in'];

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

    const call = (method, path, body) => fetch(endpoint + path, {
        method: method,
        credentials: 'same-origin',
        headers: Object.assign({ Accept: 'application/json' }, getAuthHeaders(), body ? { 'Content-Type': 'application/json' } : {}),
        body: body ? JSON.stringify(body) : undefined
    }).then(r => r.text().then(t => {
        let b = null;
        try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; }
        return { ok: r.ok, status: r.status, body: b };
    })).catch(() => ({ ok: false, status: 0, body: null }));
    const data = r => (r && r.ok && r.body ? (r.body.data !== undefined ? r.body.data : r.body) : null);
    const errText = r => L[C.errorKey(r)] || L.Err_generic || '';

    const state = { dto: null, rows: [], products: [], names: {}, people: {}, edit: null, pending: null, tried: false, quick: null };

    const productName = id => state.names[id] || String(id || '').slice(0, 8);
    const personName = id => (!id ? '—' : id === me ? (L.YouLabel || id) : (state.people[id] || '—'));
    const rowText = row => (row.key === C.DEFAULT ? L.TenantDefault : productName(row.productId));
    const rowName = row => (row.key === C.DEFAULT ? esc(L.TenantDefault) : F.bidi(productName(row.productId)));
    const sourceChip = src => '<span class="pt-pill pt-tone-' + (src === 'product' ? 'primary' : src === 'default' ? 'secondary' : 'muted') + '">' + esc(L['Source_' + src] || src) + '</span>';
    const lastChange = row => (row.updatedAt ? esc(when(row.updatedAt)) + ' · ' + F.bidi(personName(row.updatedBy)) : esc(L.Never));
    const bandsOf = row => '<span class="pt-band pt-band-low">' + esc(T('BandLow', num(row.lowBelow))) + '</span>'
        + '<span class="pt-band pt-band-medium">' + esc(C.isolateRange(T('BandMedium', num(row.lowBelow), num(row.highAbove)))) + '</span>'
        + '<span class="pt-band pt-band-high">' + esc(T('BandHigh', num(row.highAbove))) + '</span>';

    // ── the table's load failure keeps the page's own notice + retry ───────────────────────────────────────────
    function showLoadError(r) {
        const n = el('pt-load-error-text');
        if (n) { n.textContent = errText(r); }
        show('pt-load-error', true);
    }

    function loadProducts() {
        if (!canProducts) { return Promise.resolve(); }
        return fetch(endpoint + '/lookups/products', { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() })
            .then(res => res.text().then(t => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: res.ok, status: res.status, body: b }; }))
            .catch(() => ({ ok: false, status: 0, body: null }))
            .then(r => {
            const d = data(r);
            state.products = Array.isArray(d) ? d : [];
            state.products.forEach(p => { if (p && p.id) { state.names[p.id] = p.name || p.code || p.id; } });
            if (!Array.isArray(d)) { state.productsError = errText(r); }
        });
    }

    // the history's people (the CRM resolves the names) also label the rows' "last change"
    function rememberNames(names) {
        let added = false;
        Object.keys(names || {}).forEach(k => { if (state.people[k] !== names[k]) { state.people[k] = names[k]; added = true; } });
        if (added && list && list.dt) { list.dt.rows().invalidate('data').draw(false); }
    }

    // ── the quick view ────────────────────────────────────────────────────────────────────────────────────────
    function populateQuickView(row) {
        state.quick = row.key;
        const title = el('oc-title');
        if (title) { title.textContent = rowText(row); title.setAttribute('dir', 'auto'); }
        const sub = el('oc-subtitle');
        if (sub) { sub.textContent = row.key === C.DEFAULT ? '' : (state.products.find(p => p.id === row.productId) || {}).code || ''; }
        const bands = el('oc-bands'); if (bands) { bands.innerHTML = bandsOf(row); }
        const src = el('oc-source'); if (src) { src.innerHTML = sourceChip(row.source); }
        const last = el('oc-last'); if (last) { last.innerHTML = lastChange(row); }
    }

    // ── the drawer (as it was) ────────────────────────────────────────────────────────────────────────────────
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
        el('pt-bands').innerHTML = v.ok ? bandsOf({ lowBelow: low, highAbove: high }) : '<span class="pt-sub">—</span>';
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
            // the standard reload-with-toast lifecycle: the list reloads and says "saved"; the history follows
            list.reload('Saved');
            if (history) { history.reload(); }
        });
    }

    // ── the list ─────────────────────────────────────────────────────────────────────────────────────────────
    const filterFields = [
        { id: 'filterSource', key: 'source', kind: 'multi', matches: (row, values) => !values || !values.length || values.indexOf(row.source) > -1 }
    ];

    const loadLookupOptions = async ({ fillSelect }) => {
        fillSelect(el('filterSource'), SOURCES.map(s => ({ value: s, text: L['Source_' + s] || s })));
    };

    const rowActionHandlers = {
        edit: ({ id }) => { if (canManage && id) { openEdit(id); } }
    };

    const initDataTable = async () => {
        if (!dtTableEl || !C || !F) return;
        await loadProducts();

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'client',
            ajax: {
                url: endpoint + '/thresholds',
                type: 'GET',
                // the CRM's { default, products } → the rows: the tenant default first, then the products by name
                dataSrc: (json) => {
                    show('pt-load-error', false);
                    state.dto = json && json.data !== undefined ? json.data : json;
                    state.rows = C.rowsOf(state.dto, state.names);
                    return state.rows;
                },
                error: (xhr) => {
                    let body = null;
                    try { body = xhr && xhr.responseText ? JSON.parse(xhr.responseText) : null; } catch (e) { body = null; }
                    showLoadError({ ok: false, status: xhr ? xhr.status : 0, body: body });
                }
            },
            actions: { onRowAction: rowActionHandlers },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateQuickView, actionKey: 'quickView' },
            toolbar: {
                addNewText: canAdd ? L.AddProduct : '',
                onAddNew: () => { if (canAdd) { openEdit(null); } },
                exportColumns: DATA_COLUMNS,
                colvisColumns: DATA_COLUMNS
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'CRM', pageKey: 'PotentialThresholds', saveViewColumnIndexes: DATA_COLUMNS, defaultVisibleColumnIndexes: DATA_COLUMNS, baseOrder: [[1, 'asc']] },
            config: {
                // the tenant default stays the FIRST row whatever the reader sorts by (the control column's sort value: 0 for it,
                // 1 for a product); the products by name unless the reader sorts otherwise
                orderFixed: { pre: [[0, 'asc']] },
                order: [[1, 'asc']],
                createdRow: (tr, row) => { if (row && row.key === C.DEFAULT) { tr.classList.add('pt-default'); } },
                columns: [
                    { data: 'key', name: 'control' },
                    { data: 'key', name: 'product' },
                    { data: 'lowBelow', name: 'lowBelow' },
                    { data: 'key', name: 'medium' },
                    { data: 'highAbove', name: 'highAbove' },
                    { data: 'source', name: 'source' },
                    { data: 'updatedAt', name: 'updatedAt' },
                    { data: 'key', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', orderable: false, searchable: false, responsivePriority: 1, render: (d, type, row) => (type === 'sort' ? (row.key === C.DEFAULT ? 0 : 1) : '') },
                    {
                        targets: 1,
                        render: (d, type, row) => (type === 'display'
                            ? '<span class="pt-strong">' + rowName(row) + '</span>' + (row.key === C.DEFAULT ? ' <i class="bx bx-lock-alt pt-mu" aria-hidden="true"></i>' : '')
                            : rowText(row))
                    },
                    { targets: 2, responsivePriority: 2, render: (d, type) => (type === 'display' ? '<span class="pt-band pt-band-low">&lt; ' + esc(num(d)) + '</span>' : d) },
                    { targets: 3, orderable: false, responsivePriority: 4, render: (d, type, row) => (type === 'display' ? '<span class="pt-band pt-band-medium">' + esc(C.isolateRange(num(row.lowBelow) + '–' + num(row.highAbove))) + '</span>' : num(row.lowBelow) + '–' + num(row.highAbove)) },
                    { targets: 4, responsivePriority: 3, render: (d, type) => (type === 'display' ? '<span class="pt-band pt-band-high">&gt; ' + esc(num(d)) + '</span>' : d) },
                    { targets: 5, responsivePriority: 5, render: (d, type) => (type === 'display' ? sourceChip(d) : (L['Source_' + d] || d)) },
                    { targets: 6, responsivePriority: 6, render: (d, type, row) => (type === 'display' ? '<span class="pt-sub">' + lastChange(row) + '</span>' : (d || '')) },
                    {
                        targets: -1,
                        title: L.Actions,
                        orderable: false,
                        searchable: false,
                        className: 'cell-fit all',
                        render: (d, type, row) => window.DitenDataTable.renderActions([
                            { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': row.key, title: L.QuickView, 'aria-label': L.QuickView } },
                            { key: 'edit', icon: 'bx bx-edit-alt', text: L.EditButton, visible: canManage, attrs: { 'data-id': row.key } }
                        ])
                    }
                ]
            }
        });

        // the second table AFTER the primary list (the shared placeholder the list removes is its own)
        history = window.PotentialThresholdsHistory?.create({
            tableEl: historyTableEl,
            endpoint: endpoint,
            onNames: rememberNames,
            cells: {
                esc: esc,
                when: when,
                person: id => F.bidi(personName(id)),
                target: id => (id ? F.bidi(productName(id)) : esc(L.HistoryDefault)),
                field: f => L['Field_' + f] || f,
                change: (oldValue, newValue) => '<span class="pt-old">' + esc(num(oldValue)) + '</span> <i class="bx bx-right-arrow-alt pt-flip" aria-hidden="true"></i> <span class="pt-strong">' + esc(num(newValue)) + '</span>'
            }
        });
    };

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────
    const on = (id, ev, fn) => { const n = el(id); if (n) { n.addEventListener(ev, fn); } };
    const wire = () => {
        on('pt-low', 'input', renderEdit);
        on('pt-high', 'input', renderEdit);
        on('pt-try', 'input', renderEdit);
        on('pt-product', 'change', renderEdit);
        on('pt-save', 'click', () => { if (canManage) { save(); } });
        on('pt-save-confirm', 'click', () => { if (canManage) { confirmSave(); } });
        on('pt-retry', 'click', () => { if (list) { list.dt.ajax.reload(null, false); } if (history) { history.reload(); } });
        // the quick view's Edit opens the edit drawer for the same row
        on('oc-btn-edit', 'click', () => {
            if (!canManage || !state.quick) { return; }
            const qv = drawer('offcanvasDetailsPreview'); if (qv) { qv.hide(); }
            openEdit(state.quick);
        });
        // a table drawn while its tab was hidden measures itself when the tab shows (K-TABS)
        document.addEventListener('diten:tab-change', (event) => {
            const key = event.detail && event.detail.key;
            const dt = key === 'history' ? history?.table : key === 'thresholds' ? list?.dt : null;
            try { dt?.columns?.adjust?.(); dt?.responsive?.recalc?.(); } catch (e) { /* a table not drawn yet has nothing to measure */ }
        });
    };

    return {
        init: function () {
            wire();
            initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => PotentialThresholdsList.init());
