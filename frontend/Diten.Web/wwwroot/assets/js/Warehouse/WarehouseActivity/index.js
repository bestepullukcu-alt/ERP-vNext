/**
 * MOD-0178 DL-7 — Etkinlik İzi (WAREHOUSEACTIVITY) — DataTables index script.
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, filters, paging and export come from DitenDataTable.createList. This file writes only
 * the screen's own business: the source / event words, the read-only QuickView (the row's data) and the link to the record's own screen.
 *
 * SERVER MODE, PROXY PROFILE (pack §11.5): '/Warehouse/WarehouseActivity/api' translates the factory's wire for listWarehouseActivity; the browser never
 * calls the inventory service (5067), /api/warehouse or the Gateway. The range is mandatory at the service and at most 31 days (§8.23): an empty
 * range is the last 7 days (the proxy fills it); a wrong one comes back as the proxy's own sentence.
 *
 * READ-ONLY: no write. A link to a record's screen is drawn only when the reader holds that screen's key.
 */
'use strict';

const WarehouseActivityList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-warehouseactivity');
    const L = () => window.L10n || {};
    const W = (key) => (L().Words || {})[key] || '';
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const can = (key) => window.Permissions?.has?.(key) === true;
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const endpoint = '/Warehouse/WarehouseActivity/api';

    const fmtDate = (value) => {
        if (!value) return '';
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString(window.CurrentLanguage || undefined);
    };

    const sourceWord = (source) => W(`Warehouse.Activity.Source.${source}`) || source || '';
    const eventWord = (row) => W(row.eventKey) || row.kind || '';
    const actorText = (row) => (row.system ? W('SystemActor') : row.actor ? (row.actor.name || W('UnknownUser')) : '');

    /** The record's own screen, or null when the reader lacks its key. */
    const linkOf = (row) => {
        switch (row.source) {
            case 'PutawayTask': return can('warehouse.putaway-tasks.read') ? '/Warehouse/PutawayTasks' : null;
            case 'PickTask': return can('warehouse.pick-tasks.read') ? '/Warehouse/PickTasks' : null;
            case 'Outbound': return row.recordId && can('warehouse.outbound-shipments.read') ? `/Warehouse/OutboundShipments/Details/${encodeURIComponent(row.recordId)}` : null;
            case 'Movement': return can('inventory.stock.read') ? '/StockMovements' : null;
            default: return null;
        }
    };

    const recordCell = (row) => {
        const number = `<span class="font-monospace">${esc(row.recordNumber || '')}</span>`;
        const link = linkOf(row);
        return link ? `<a href="${esc(link)}" data-wh-record-link>${number}</a>` : number;
    };

    // ─── Filters ─────────────────────────────────────────────────────────────
    const filterFields = [
        { id: 'filterFrom', key: 'from', kind: 'text' },
        { id: 'filterTo', key: 'to', kind: 'text' },
        { id: 'filterSource', key: 'kind', kind: 'text' },
        { id: 'filterWarehouse', key: 'warehouseId', kind: 'text' }
    ];

    const pickerNotice = (available) => {
        const notice = byId('activity-picker-unavailable');
        if (!notice) return;
        notice.textContent = available ? '' : W('Warehouse.Error.ServiceUnavailable');
        notice.classList.toggle('d-none', !!available);
    };

    const loadLookupOptions = async () => {
        const el = byId('filterWarehouse');
        if (!el || !window.jQuery?.fn?.select2) return;
        const $el = window.jQuery(el);
        $el.select2({
            placeholder: $el.data('placeholder') || '',
            allowClear: true,
            width: 'element',
            dropdownParent: window.jQuery(document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            selectionCssClass: 'form-select form-select-sm',
            language: { searching: () => L().Searching || '', noResults: () => L().NoResults || '' },
            ajax: {
                url: '/Warehouse/WarehouseActivity/warehouses',
                dataType: 'json',
                delay: 250,
                data: (params) => ({ q: params.term || '' }),
                processResults: (data) => {
                    pickerNotice(data?.available !== false);
                    return { results: (data?.items || []).map((item) => ({ id: item.value, text: item.text })) };
                },
                error: (xhr) => { if (xhr?.statusText !== 'abort') pickerNotice(false); }
            }
        });
    };

    const onResponse = (json) => {
        const box = byId('activity-list-error');
        box?.classList.add('d-none');
        if (box && json && json.namesAvailable === false) {
            box.textContent = W('NamesUnavailable');
            box.classList.remove('d-none', 'alert-danger');
            box.classList.add('alert-warning');
        }
    };

    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        const box = byId('activity-list-error');
        if (box) {
            box.textContent = message || W('Warehouse.Error.ServiceUnavailable');
            box.classList.remove('d-none', 'alert-warning');
            box.classList.add('alert-danger');
        }
    };

    // ─── Row menu: the read-only QuickView and the link to the record ────────
    const renderAction = (full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        const items = [{ key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-json': rowJson, title: L().QuickView, 'aria-label': L().QuickView } }];
        if (linkOf(full)) items.push({ key: 'open', icon: 'bx bx-link-external', text: W('ActionOpen'), attrs: { 'data-json': rowJson, title: W('ActionOpen'), 'aria-label': W('ActionOpen') } });
        return window.DitenDataTable.renderActions(items);
    };

    const rowActionHandlers = {
        open: ({ row }) => {
            const link = row && linkOf(row);
            if (link) window.location.assign(link);
        }
    };

    // ─── QuickView ───────────────────────────────────────────────────────────
    const setText = (id, value) => { const el = byId(id); if (el) el.textContent = value == null || value === '' ? '-' : String(value); };

    const populateDetailsOffcanvas = (row) => {
        setText('offcanvasDetailsPreviewLabel', eventWord(row));
        setText('oc-subtitle', [sourceWord(row.source), row.recordNumber].filter(Boolean).join(' · '));
        setText('oc-time', fmtDate(row.at));
        setText('oc-source', sourceWord(row.source));
        setText('oc-event', eventWord(row));
        setText('oc-record', row.recordNumber);
        setText('oc-seq', row.seq);
        setText('oc-warehouse', row.warehouse);
        setText('oc-actor', actorText(row));
        const host = byId('oc-data');
        if (host) {
            // the row's own data: technical keys as written by the service (a value, never a sentence), shown as code
            host.innerHTML = Object.entries(row.data || {})
                .map(([key, value]) => `<dt class="col-5 text-muted fw-normal"><code>${esc(key)}</code></dt><dd class="col-7 mb-0 text-break">${esc(value)}</dd>`).join('');
        }
        const link = byId('oc-record-link');
        const target = linkOf(row);
        if (link) {
            link.classList.toggle('d-none', !target);
            link.setAttribute('href', target || '#');
        }
    };

    const initDataTable = async () => {
        if (!tableEl) return;
        const base = 1; // control
        const columns = [
            { data: 'id', name: 'control' },
            { data: 'at', name: 'time' },
            { data: 'source', name: 'source' },
            { data: 'eventKey', name: 'event' },
            { data: 'recordNumber', name: 'record' },
            { data: 'warehouse', name: 'warehouse' },
            { data: 'actor', name: 'actor' },
            { data: 'id', name: 'action' }
        ];
        const visible = Array.from({ length: 6 }, (_, i) => base + i);
        const columnDefs = [
            { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
            { targets: base, render: (data) => `<span class="text-nowrap">${esc(fmtDate(data))}</span>` },
            { targets: base + 1, render: (data) => `<span class="badge bg-label-secondary">${esc(sourceWord(data))}</span>` },
            { targets: base + 2, render: (data, type, full) => esc(eventWord(full)) },
            { targets: base + 3, render: (data, type, full) => recordCell(full) },
            { targets: base + 4, render: (data) => esc(data) },
            { targets: base + 5, render: (data, type, full) => esc(actorText(full)) },
            { targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
        ];

        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: endpoint, type: 'GET', headers: getAuthHeaders(), error: onListError },
            onResponse,
            export: { mode: 'server', url: '/Warehouse/WarehouseActivity/export', fileName: 'warehouse-activity' },
            actions: { onRowAction: rowActionHandlers },
            // Read-only list (§8.23): no "Yeni", no bulk action.
            toolbar: { addNewText: '', colvisColumns: visible },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Warehouse', pageKey: 'WarehouseActivity', saveViewColumnIndexes: visible, defaultVisibleColumnIndexes: visible, baseOrder: [] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            config: { ordering: false, searching: false, columns, columnDefs }
        });
    };

    return {
        init: function () {
            initDataTable();
        },
        // the pure part, read by the Node tests (no DOM)
        linkOf
    };
})();

window.WarehouseActivityList = WarehouseActivityList;
document.addEventListener('DOMContentLoaded', () => WarehouseActivityList.init());
