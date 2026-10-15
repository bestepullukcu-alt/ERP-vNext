/**
 * WP-VW-W3E · Q5 the institution QR codes — W3E-FIX (U-1): the Golden Compact list standard (data_mode: server).
 *
 * THE LIST IS THE SHARED COMPONENT: Save View, column state, the inline filter, paging, search and the bulk bar come from
 * DitenDataTable.createList. This file writes only what is Q5's own: the columns and their renderers (the mockup's content:
 * institution + ↳ parent, type, city, the QR status chip, version, last scan, last 30 days), the lookups, the row actions
 * (print, go to the institution, quick view) and the two bulk actions (generate for the selected — behind the showConfirm
 * standard, the result as the standard toast; print the selected → the label sheet). The rules of a row are VisitQrCore's
 * (qr-core.js, Node-tested), the same the card uses.
 *
 * SERVER MODE: the factory sends start/length/search/draw + each applied filter by its key to the same-origin proxy, which
 * turns it into the CRM list's own page/pageSize query (the CRM orders by institution name, so no column is sortable and
 * no orderBy is ever sent). No ext.search, no serverSide, no ajax.data here — the factory owns them.
 */
'use strict';

const VisitQrCodesList = (function () {
    let list = null;

    const dtTableEl = document.querySelector('.datatables-visitqrcodes');
    const flags = document.getElementById('vq-list-root');
    const endpoint = '/CRM/VisitQrCodes/api';
    const printBase = '/CRM/VisitQrCodes';
    const canManage = !!flags && flags.getAttribute('data-can-manage') === 'true';
    const canReadAccounts = !!flags && flags.getAttribute('data-can-read-accounts') === 'true';
    const C = window.VisitQrCore;
    const L = () => window.L10n || {};
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const byId = (id) => document.getElementById(id);
    const esc = (s) => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => String(t == null ? '' : t).replace(/\{(\d+)\}/g, (m, i) => (a[+i] == null ? '' : String(a[+i])));
    const lookups = { types: {}, cities: {}, typeOptions: [], cityOptions: [], regions: [] };
    const DATA_COLUMNS = [2, 3, 4, 5, 6, 7, 8];

    const CHIP = {
        active: { key: 'ChipActive', badge: 'bg-label-success' },
        revoked: { key: 'ChipRevoked', badge: 'bg-label-warning' },
        none: { key: 'ChipNone', badge: 'bg-label-secondary' }
    };

    const lang = () => ((document.documentElement && document.documentElement.lang) || 'tr').trim() || 'tr';
    const time = (iso) => { const d = new Date(iso); return String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0'); };
    const dateOnly = (iso) => {
        const d = new Date(iso || '');
        if (isNaN(d)) { return '—'; }
        try { return new Intl.DateTimeFormat(/^en(-|$)/i.test(lang()) ? 'en-GB' : lang(), { day: 'numeric', month: 'short', year: 'numeric' }).format(d); } catch (e) { return d.toISOString().slice(0, 10); }
    };
    const when = (iso) => {
        if (!iso) { return '—'; }
        const k = C.whenKey(iso, Date.now(), -new Date().getTimezoneOffset());
        return k ? fmt(L()[k], time(iso)) : dateOnly(iso);
    };
    const rowOf = (item) => C.listRow(item, lookups);

    // ── the lookups: the type / city words in the reader's language (BRD), the regions (territory nodes) ──
    const loadLookups = async () => {
        try {
            const res = await fetch(endpoint + '/lookups', { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() });
            const body = res.ok ? await res.json() : null;
            const d = body?.data || {};
            lookups.typeOptions = Array.isArray(d.accountTypes) ? d.accountTypes : [];
            lookups.cityOptions = Array.isArray(d.cities) ? d.cities : [];
            lookups.typeOptions.forEach((o) => { lookups.types[o.value] = o.text; });
            lookups.cityOptions.forEach((o) => { lookups.cities[o.value] = o.text; });
        } catch (error) {
            console.error('[VisitQrCodes] lookups failed.', error);
        }
        if (canReadAccounts) {
            try {
                const res = await fetch('/CRM/Accounts/territory-lookups', { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() });
                const body = res.ok ? await res.json() : null;
                lookups.regions = (Array.isArray(body?.nodes) ? body.nodes : []).map((n) => ({ value: n.id, text: n.name }));
            } catch (error) {
                console.error('[VisitQrCodes] regions failed.', error);
            }
        }
    };

    const filterFields = [
        { id: 'filterRegion', key: 'territoryId', kind: 'single' },
        { id: 'filterType', key: 'accountType', kind: 'single' },
        { id: 'filterCity', key: 'city', kind: 'single' },
        { id: 'filterStatus', key: 'status', kind: 'single' }
    ].filter((f) => f.id !== 'filterRegion' || canReadAccounts);

    const loadLookupOptions = async ({ fillSelect }) => {
        const showAll = { showAllText: L().ShowAll || '' };
        if (byId('filterRegion')) { fillSelect(byId('filterRegion'), lookups.regions, showAll); }
        fillSelect(byId('filterType'), lookups.typeOptions, showAll);
        fillSelect(byId('filterCity'), lookups.cityOptions, showAll);
    };

    // ── the bulk actions (manage only) ──
    const rememberMeta = (ids) => {
        try {
            const meta = {};
            const rows = list?.dt?.rows?.().data?.().toArray?.() || [];
            rows.filter((r) => ids.indexOf(String(r.accountId)) > -1).forEach((r) => { meta[r.accountId] = { name: r.accountName, parent: r.parentAccountName || null }; });
            window.sessionStorage.setItem('vq-print-meta', JSON.stringify(meta));
        } catch (e) { /* the print page then names the labels from the print answer */ }
    };

    const errorText = async (res) => {
        let body = null;
        try { body = await res.json(); } catch (e) { body = null; }
        const k = C.errorKey({ status: res.status, body: body });
        const map = { Err_permission: 'ErrPermission', Err_visit_qr_request_invalid: 'ErrRequestInvalid', Err_visit_qr_signing_unavailable: 'ErrSigningUnavailable' };
        return L()[map[k]] || L().ErrGeneric || L().ErrorOccurred || '';
    };

    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        bulkActionSelector: '[data-bulk-action]',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all',
        onBulkAction: {
            // "Generate for the selected (those without a code)": the CRM skips the ones with an active code
            issue: ({ ids }) => {
                if (!ids.length) return;
                if (ids.length > C.MAX_BULK) { window.showToast?.(L().BulkLimit, 'warning'); return; }
                window.showConfirm?.(fmt(L().BulkGenerateConfirm, ids.length), async () => {
                    try {
                        const res = await fetch(`${endpoint}/accounts/bulk`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders(true), body: JSON.stringify({ accountIds: ids }) });
                        if (!res.ok) { window.showToast?.(await errorText(res), 'error'); return; }
                        const body = await res.json();
                        const args = C.bulkResultArgs(body?.data);
                        // the standard reload-with-toast lifecycle: the result sentence is the toast it raises
                        window.L10n = Object.assign({}, window.L10n || {}, { BulkIssueResult: fmt(L().BulkResult, args[0], args[1]) });
                        list.reload('BulkIssueResult');
                    } catch (error) {
                        console.error(error);
                        window.showToast?.(L().ErrorOccurred, 'error');
                    }
                }, { entityName: String(ids.length), confirmButtonText: L().BulkGenerate });
            },
            // "Print the selected (label sheet)": the print page asks the codes only when its Print is pressed
            print: ({ ids }) => {
                if (!ids.length) return;
                if (ids.length > C.MAX_BULK) { window.showToast?.(L().BulkLimit, 'warning'); return; }
                rememberMeta(ids);
                window.location.assign(C.labelsUrl(printBase, ids));
            }
        }
    };

    // ── the row actions ──
    const rowActionHandlers = {
        print: ({ id }) => { if (id && canManage) { window.location.assign(C.printUrl(printBase, id)); } },
        open: ({ id }) => { if (id) { window.location.assign('/CRM/Accounts/Details/' + encodeURIComponent(id)); } }
    };

    const setText = (id, text) => { const n = byId(id); if (n) { n.textContent = text == null || text === '' ? '—' : String(text); n.setAttribute('dir', 'auto'); } };
    const populateQuickView = (item) => {
        const m = rowOf(item);
        setText('oc-title', m.name);
        setText('oc-subtitle', m.parent ? '↳ ' + m.parent : (item.accountCode || ''));
        const status = byId('oc-status');
        if (status) { status.className = 'badge ' + CHIP[m.status].badge; status.textContent = L()[CHIP[m.status].key] || m.status; }
        setText('oc-type', m.typeLabel);
        setText('oc-city', m.cityLabel);
        setText('oc-version', m.version == null ? '—' : fmt(L().VersionShort, m.version));
        setText('oc-last-scan', when(m.lastScannedAt));
        setText('oc-recent', m.recent == null ? '—' : m.recent);
        const inherits = byId('oc-inherits');
        if (inherits) {
            inherits.classList.toggle('d-none', !m.inherits);
            inherits.innerHTML = m.inherits ? fmt(esc(L().InheritedInfo), '<bdi>' + esc(item.inheritedFrom?.accountName || '') + '</bdi>') : '';
        }
        const print = byId('oc-btn-print');
        if (print) { print.classList.toggle('d-none', !(canManage && m.printable)); print.setAttribute('href', C.printUrl(printBase, m.id)); }
        const open = byId('oc-btn-edit');
        if (open) { open.setAttribute('href', '/CRM/Accounts/Details/' + encodeURIComponent(m.id)); }
    };

    const initDataTable = async () => {
        if (!dtTableEl || !C) return;
        await loadLookups();

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            bulk: canManage ? bulkOptions : undefined,
            ajax: { url: endpoint + '/accounts', type: 'GET' },
            // BL-452: CSV/Excel = every matching institution with the visible columns, the filters and the search.
            export: { mode: 'server', url: endpoint + '/accounts/export', fileName: 'institution-qr-codes' },
            actions: { onRowAction: rowActionHandlers },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateQuickView, actionKey: 'quickView' },
            toolbar: { addNewText: '', exportColumns: DATA_COLUMNS, colvisColumns: DATA_COLUMNS },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'CRM', pageKey: 'VisitQrCodes', saveViewColumnIndexes: DATA_COLUMNS, defaultVisibleColumnIndexes: DATA_COLUMNS, baseOrder: [] },
            config: {
                order: [],
                columns: [
                    { data: 'accountId', name: 'control' },
                    { data: 'accountId', name: 'checkbox' },
                    { data: 'accountName', name: 'accountName' },
                    { data: 'accountType', name: 'accountType' },
                    { data: 'cityRef', name: 'cityRef' },
                    { data: 'status', name: 'status' },
                    { data: 'version', name: 'version' },
                    { data: 'lastScannedAt', name: 'lastScannedAt' },
                    { data: 'recentScanCount', name: 'recentScanCount' },
                    { data: 'accountId', name: 'action' }
                ],
                columnDefs: [
                    // the CRM orders by institution name: no column sorts (the factory then never sends orderBy)
                    { targets: '_all', orderable: false },
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 2, render: () => '' },
                    // the selection is a manage right: without it the column is not shown at all
                    { targets: 1, searchable: false, visible: canManage, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => (canManage ? `<input type="checkbox" class="dt-checkboxes form-check-input" value="${esc(data)}">` : '') },
                    {
                        targets: 2,
                        render: (data, type, full) => {
                            const m = rowOf(full);
                            if (type !== 'display') return m.name;
                            return '<a class="fw-medium text-heading" href="/CRM/Accounts/Details/' + esc(encodeURIComponent(m.id)) + '"><bdi>' + esc(m.name) + '</bdi></a>'
                                + (m.parent ? '<div class="small text-muted">↳ <bdi>' + esc(m.parent) + '</bdi></div>' : '');
                        }
                    },
                    { targets: 3, render: (data, type, full) => esc(rowOf(full).typeLabel) },
                    { targets: 4, render: (data, type, full) => '<bdi>' + esc(rowOf(full).cityLabel) + '</bdi>' },
                    {
                        targets: 5,
                        render: (data, type, full) => {
                            const m = rowOf(full);
                            const chip = CHIP[m.status];
                            if (type !== 'display') return L()[chip.key] || m.status;
                            return '<span class="badge ' + chip.badge + '">' + esc(L()[chip.key]) + '</span>'
                                + (m.inherits ? '<div class="small text-muted mt-1">' + esc(L().InheritsParent) + '</div>' : '');
                        }
                    },
                    { targets: 6, render: (data, type, full) => { const m = rowOf(full); return m.version == null ? '—' : esc(fmt(L().VersionShort, m.version)); } },
                    { targets: 7, render: (data, type, full) => esc(when(rowOf(full).lastScannedAt)) },
                    { targets: 8, render: (data, type, full) => { const m = rowOf(full); return m.recent == null ? '—' : esc(m.recent); } },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const m = rowOf(full);
                            return window.DitenDataTable.renderActions([
                                { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': m.id, title: L().QuickView, 'aria-label': L().QuickView } },
                                { key: 'print', icon: 'bx bx-printer', text: L().RowPrint, visible: canManage && m.printable, attrs: { 'data-id': m.id } },
                                { key: 'open', icon: 'bx bx-right-arrow-alt', text: L().RowOpen, attrs: { 'data-id': m.id } }
                            ]);
                        }
                    }
                ]
            }
        });
    };

    return {
        init: function () {
            initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => VisitQrCodesList.init());
