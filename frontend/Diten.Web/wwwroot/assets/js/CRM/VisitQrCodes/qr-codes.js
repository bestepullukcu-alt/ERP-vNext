/**
 * WP-VW-W3E (W3E-WEB-a, Q5) — the institution QR codes page: filters (region / type / city / QR status / name), the
 * server-paged list, "↳ parent" + "parent code applies", the selection (kept across pages, ≤ 200) → bulk generate (only
 * those without a code; the CRM skips the active ones) and the label print, the result strip, the empty state. The list is
 * the CRM's; this page never holds more than one page of institutions.
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('vq-list-root');
    if (!root || !window.VisitQrCore) { return; }
    const C = window.VisitQrCore;
    const L = window.VisitQrL10n || {};
    const api = root.getAttribute('data-api-base') || '/CRM/VisitQrCodes/api';
    const printBase = root.getAttribute('data-print-base') || '/CRM/VisitQrCodes';
    const canManage = root.getAttribute('data-can-manage') === 'true';
    const canReadAccounts = root.getAttribute('data-can-read-accounts') === 'true';
    const PAGE_SIZE = 25;

    const el = id => document.getElementById(id);
    const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => String(t == null ? '' : t).replace(/\{(\d+)\}/g, (m, i) => (a[+i] == null ? '' : String(a[+i])));
    const show = (id, on) => { const n = el(id); if (n) { n.classList.toggle('d-none', !on); } };
    const lang = () => ((document.documentElement && document.documentElement.lang) || 'tr').trim() || 'tr';
    const dateOnly = iso => {
        const d = new Date(iso || '');
        if (isNaN(d)) { return '—'; }
        try { return new Intl.DateTimeFormat(/^en(-|$)/i.test(lang()) ? 'en-GB' : lang(), { day: 'numeric', month: 'short', year: 'numeric' }).format(d); } catch (e) { return d.toISOString().slice(0, 10); }
    };
    const time = iso => { const d = new Date(iso); return String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0'); };
    const when = iso => {
        if (!iso) { return '—'; }
        const k = C.whenKey(iso, Date.now(), -new Date().getTimezoneOffset());
        return k ? fmt(L[k], time(iso)) : dateOnly(iso);
    };

    const call = (method, url, body) => window.fetch(url, {
        method: method,
        credentials: 'same-origin',
        headers: Object.assign({ Accept: 'application/json' }, body ? { 'Content-Type': 'application/json' } : {}),
        body: body ? JSON.stringify(body) : undefined
    }).then(r => r.text().then(t => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: r.ok, status: r.status, body: b }; }))
        .catch(() => ({ ok: false, status: 0, body: null }));

    const state = { filters: {}, page: 1, total: 0, rows: [], lookups: { types: {}, cities: {} }, selected: new Set(), meta: {}, seq: 0 };

    function fail(text) { el('vq-list-error-text').textContent = text || ''; show('vq-list-error', !!text); }

    function fillSelect(id, options) {
        const s = el(id);
        if (!s) { return; }
        options.forEach(o => { const opt = document.createElement('option'); opt.value = o.value; opt.textContent = o.text; s.appendChild(opt); });
    }

    function lookups() {
        call('GET', api + '/lookups').then(r => {
            const d = r.ok && r.body && r.body.data ? r.body.data : { accountTypes: [], cities: [] };
            (d.accountTypes || []).forEach(o => { state.lookups.types[o.value] = o.text; });
            (d.cities || []).forEach(o => { state.lookups.cities[o.value] = o.text; });
            fillSelect('vq-f-type', d.accountTypes || []);
            fillSelect('vq-f-city', d.cities || []);
            if (state.rows.length) { renderRows(); }
        });
        if (canReadAccounts && el('vq-f-region')) {
            call('GET', '/CRM/Accounts/territory-lookups').then(r => {
                const nodes = r.ok && r.body && Array.isArray(r.body.nodes) ? r.body.nodes : [];
                fillSelect('vq-f-region', nodes.map(n => ({ value: n.id, text: n.name })));
            });
        }
    }

    function load() {
        const seq = ++state.seq;
        fail('');
        call('GET', api + '/accounts?' + C.listQuery(state.filters, state.page, PAGE_SIZE)).then(r => {
            if (seq !== state.seq) { return; }
            if (!r.ok || !r.body || !r.body.data) { el('vq-rows').innerHTML = ''; fail(L[C.errorKey(r)] || L.LoadFailed || ''); return; }
            const d = r.body.data;
            state.rows = Array.isArray(d.items) ? d.items : [];
            state.total = d.total || 0;
            renderRows();
        });
    }

    function rowHtml(item) {
        const m = C.listRow(item, state.lookups);
        state.meta[m.id] = { name: m.name, parent: m.parent };
        const sel = state.selected.has(m.id);
        const check = canManage
            ? '<input type="checkbox" data-select="' + esc(m.id) + '"' + (sel ? ' checked' : '') + ' aria-label="' + esc(fmt(L.SelectRow, m.name)) + '" />'
            : '<span></span>';
        const print = canManage && m.printable
            ? '<a class="vq-icon-btn" href="' + esc(C.printUrl(printBase, m.id)) + '" title="' + esc(L.RowPrint) + '" aria-label="' + esc(L.RowPrint) + '"><i class="bx bx-printer"></i></a>'
            : '<span class="vq-icon-btn" aria-hidden="true" style="visibility:hidden"><i class="bx bx-printer"></i></span>';
        return '<div class="vq-row' + (sel ? ' vq-selected' : '') + '" role="row">' + check
            + '<div style="min-width:0"><a class="vq-row-name" href="/CRM/Accounts/Details/' + esc(m.id) + '"><bdi>' + esc(m.name) + '</bdi></a>'
            + (m.parent ? '<div class="vq-row-parent">↳ <bdi>' + esc(m.parent) + '</bdi></div>' : '') + '</div>'
            + '<span>' + esc(m.typeLabel) + '</span><span>' + esc(m.cityLabel) + '</span>'
            + '<div class="vq-status-cell"><span class="vq-chip vq-chip-' + m.status + '">' + esc(L[m.chipKey]) + '</span>'
            + (m.inherits ? '<span class="vq-inherit">' + esc(L.InheritsParent) + '</span>' : '') + '</div>'
            + '<span>' + (m.version == null ? '—' : esc(fmt(L.VersionShort, m.version))) + '</span>'
            + '<span>' + esc(m.lastScannedAt ? when(m.lastScannedAt) : '—') + '</span>'
            + '<span>' + (m.recent == null ? '—' : esc(m.recent)) + '</span>'
            + '<div class="vq-row-actions">' + print
            + '<a class="vq-icon-btn" href="/CRM/Accounts/Details/' + esc(m.id) + '" title="' + esc(L.RowOpen) + '" aria-label="' + esc(L.RowOpen) + '"><i class="bx bx-dots-vertical-rounded"></i></a></div>'
            + '</div>';
    }

    function renderRows() {
        el('vq-rows').innerHTML = state.rows.map(rowHtml).join('');
        const empty = state.rows.length === 0;
        show('vq-empty', empty);
        show('vq-clear', empty && C.hasFilters(state.filters));
        const from = empty ? 0 : (state.page - 1) * PAGE_SIZE + 1;
        const to = (state.page - 1) * PAGE_SIZE + state.rows.length;
        show('vq-pager', state.total > PAGE_SIZE);
        el('vq-page-info').textContent = fmt(L.PageInfo, from, to, state.total);
        el('vq-prev').disabled = state.page <= 1;
        el('vq-next').disabled = to >= state.total;
        syncSelection();
    }

    function syncSelection() {
        const n = state.selected.size;
        const has = canManage && n > 0;
        show('vq-selbar', has);
        show('vq-count', !has);
        el('vq-count').textContent = fmt(L.CountLabel, state.total);
        if (el('vq-selcount')) { el('vq-selcount').textContent = fmt(L.SelectedLabel, n); }
        const all = el('vq-select-all');
        if (all) {
            const pageIds = state.rows.map(r => r.accountId);
            all.checked = pageIds.length > 0 && pageIds.every(id => state.selected.has(id));
        }
    }

    function selectedIds() { return Array.from(state.selected); }

    function rememberMeta() {
        try {
            const meta = {};
            selectedIds().forEach(id => { if (state.meta[id]) { meta[id] = state.meta[id]; } });
            window.sessionStorage.setItem('vq-print-meta', JSON.stringify(meta));
        } catch (e) { /* the print page then names the labels from the print answer */ }
    }

    function printSelected(e) {
        if (e) { e.preventDefault(); }
        if (!canManage || state.selected.size === 0) { return; }
        rememberMeta();
        window.location.assign(C.labelsUrl(printBase, selectedIds()));
    }

    function bulkGenerate() {
        if (!canManage || state.selected.size === 0) { return; }
        const button = el('vq-bulk-generate');
        button.disabled = true;
        call('POST', api + '/bulk-issue', { accountIds: selectedIds() }).then(r => {
            button.disabled = false;
            if (!r.ok || !r.body || !r.body.data) { fail(L[C.errorKey(r)] || L.Err_generic || ''); return; }
            const args = C.bulkResultArgs(r.body.data);
            el('vq-result-text').textContent = fmt(L.BulkResult, args[0], args[1]);
            show('vq-result', true);
            load();
        });
    }

    let searchTimer = null;
    function setFilter(key, value) {
        state.filters[key] = value;
        state.page = 1;
        load();
    }

    [['vq-f-region', 'territoryId'], ['vq-f-type', 'accountType'], ['vq-f-city', 'city'], ['vq-f-status', 'status']].forEach(([id, key]) => {
        const s = el(id);
        if (s) { s.addEventListener('change', () => setFilter(key, s.value)); }
    });
    el('vq-f-search').addEventListener('input', e => {
        window.clearTimeout(searchTimer);
        const v = e.target.value;
        searchTimer = window.setTimeout(() => setFilter('search', v), 300);
    });
    el('vq-clear').addEventListener('click', () => {
        state.filters = {}; state.page = 1;
        ['vq-f-region', 'vq-f-type', 'vq-f-city', 'vq-f-status', 'vq-f-search'].forEach(id => { const n = el(id); if (n) { n.value = ''; } });
        load();
    });
    el('vq-prev').addEventListener('click', () => { if (state.page > 1) { state.page--; load(); } });
    el('vq-next').addEventListener('click', () => { state.page++; load(); });
    el('vq-rows').addEventListener('change', e => {
        const id = e.target.getAttribute && e.target.getAttribute('data-select');
        if (!id) { return; }
        const on = C.select(state.selected, id, e.target.checked);
        if (e.target.checked && !on) { e.target.checked = false; fail(L.BulkLimit || ''); }
        const row = e.target.closest('.vq-row');
        if (row) { row.classList.toggle('vq-selected', on); }
        syncSelection();
    });
    if (el('vq-select-all')) {
        el('vq-select-all').addEventListener('change', e => {
            C.toggleAll(state.selected, state.rows.map(r => r.accountId), e.target.checked);
            renderRows();
        });
    }
    if (el('vq-bulk-generate')) { el('vq-bulk-generate').addEventListener('click', bulkGenerate); }
    if (el('vq-bulk-print')) { el('vq-bulk-print').addEventListener('click', printSelected); }
    el('vq-result-print').addEventListener('click', printSelected);
    el('vq-result-close').addEventListener('click', () => show('vq-result', false));

    lookups();
    load();
}(window, document));
