/**
 * MOD-0178 DL-7 — İstisna Kutusu (WAREHOUSEEXCEPTIONS) — DataTables index script.
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, filters, paging and export come from DitenDataTable.createList. This file writes only
 * the screen's own business: the kind badge, the one-line summary of each kind, the read-only QuickView (details + the Staging difference) and the
 * link to the record's own screen.
 *
 * SERVER MODE, PROXY PROFILE (pack §11.5): '/Warehouse/WarehouseExceptions/api' translates the factory's wire for listWarehouseExceptions; the browser
 * never calls the inventory service (5067), /api/warehouse or the Gateway.
 *
 * READ-ONLY (pack §8.23): the records are derived by the service; there is no acknowledge / resolve in v1 (FU-0178-S3-ACT). A link to a record's
 * screen is drawn only when the reader holds that screen's key (a reader without it gets the record number as text — no page that would refuse).
 */
'use strict';

const WarehouseExceptionsList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-warehouseexceptions');
    const L = () => window.L10n || {};
    const W = (key) => (L().Words || {})[key] || '';
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const can = (key) => window.Permissions?.has?.(key) === true;
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const endpoint = '/Warehouse/WarehouseExceptions/api';

    const fmtDate = (value) => {
        if (!value) return '';
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString(window.CurrentLanguage || undefined);
    };
    const format = (template, ...values) => values.reduce((text, value, i) => text.split(`{${i}}`).join(value ?? ''), template || '');

    const PostingKinds = ['PostingRefused', 'PostingPendingLong', 'PostingIntegrityError'];
    const KindBadge = {
        PostingRefused: 'bg-label-danger', PostingPendingLong: 'bg-label-warning', PostingIntegrityError: 'bg-label-danger', DispatchNotSeen: 'bg-label-warning',
        ReservationReleasePending: 'bg-label-info', ShortDispatchUnresolved: 'bg-label-danger', AllocationOrphanSuspected: 'bg-label-danger',
        ReservedLotUnderRecall: 'bg-label-danger', AllocationStuck: 'bg-label-warning'
    };
    const kindBadge = (kind) => `<span class="badge ${KindBadge[kind] || 'bg-label-secondary'}">${esc(W(`Warehouse.Exception.Kind.${kind}`) || kind)}</span>`;

    /** The record's own screen, or null when the reader lacks its key (pack §8.23 "Satırdan ilgili ekrana bağlantı"). */
    const linkOf = (row) => {
        if (PostingKinds.includes(row.kind)) {
            if (row.taskKind === 'Pick') return can('warehouse.pick-tasks.read') ? '/Warehouse/PickTasks' : null;
            return can('warehouse.putaway-tasks.read') ? '/Warehouse/PutawayTasks' : null;
        }
        return row.outboundId && can('warehouse.outbound-shipments.read') ? `/Warehouse/OutboundShipments/Details/${encodeURIComponent(row.outboundId)}` : null;
    };

    /** The kind's one-line summary (the list's detail column). */
    const summary = (row) => {
        switch (row.kind) {
            case 'PostingRefused':
            case 'PostingPendingLong':
            case 'PostingIntegrityError':
                return format(W('SummaryPosting'), row.seq, row.quantity);
            case 'DispatchNotSeen': return format(W('SummaryReady'), fmtDate(row.readyAt));
            case 'AllocationStuck': return format(W('SummaryStuck'), fmtDate(row.createdAt));
            case 'ReservationReleasePending': return format(W('SummaryRelease'), row.allocSeq);
            case 'AllocationOrphanSuspected': return format(W('SummaryOrphan'), row.pending);
            case 'ReservedLotUnderRecall': return format(W('SummaryRecall'), row.allocSeq, row.lot || W('NoLot'));
            case 'ShortDispatchUnresolved':
                return row.deferredBy
                    ? format(W('SummaryDeferred'), W(`Warehouse.Exception.DeferredBy.${row.deferredBy}`))
                    : format(W('SummaryShort'), row.sku, row.expected, row.issued);
            default: return '';
        }
    };

    const codeText = (code) => (code ? (W(`Warehouse.Error.${code}`) || code) : '');

    const recordCell = (row) => {
        const kindWord = PostingKinds.includes(row.kind) ? W(`Warehouse.Exception.TaskKind.${row.taskKind || 'Putaway'}`) : '';
        const number = `<span class="font-monospace">${esc(row.record)}</span>`;
        const link = linkOf(row);
        const head = link ? `<a href="${esc(link)}" data-wh-record-link>${number}</a>` : number;
        return kindWord ? `${head}<small class="d-block text-muted">${esc(kindWord)}</small>` : head;
    };

    // ─── Filters ─────────────────────────────────────────────────────────────
    const filterFields = [
        { id: 'filterKind', key: 'kind', kind: 'text' },
        { id: 'filterWarehouse', key: 'warehouseId', kind: 'text' }
    ];

    const pickerNotice = (available) => {
        const notice = byId('exceptions-picker-unavailable');
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
                url: '/Warehouse/WarehouseExceptions/warehouses',
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
        const box = byId('exceptions-list-error');
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
        const box = byId('exceptions-list-error');
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

    const detailPairs = (row) => {
        const pairs = [];
        const add = (word, value) => { if (value != null && value !== '') pairs.push([W(word), value]); };
        if (PostingKinds.includes(row.kind)) {
            add('FieldSeq', row.seq);
            add('FieldQuantity', row.quantity);
        }
        if (row.outboundStatus) add('FieldOutboundStatus', W(`Warehouse.Outbound.Status.${row.outboundStatus}`) || row.outboundStatus);
        add('FieldAllocSeq', row.allocSeq);
        if (row.kind !== 'ShortDispatchUnresolved') add('FieldLot', row.lot);
        add('FieldReadyAt', fmtDate(row.readyAt));
        add('FieldCreatedAt', fmtDate(row.createdAt));
        add('FieldPendingReleases', row.pending);
        if (row.reservationKeys?.length) add('FieldReservationKeys', row.reservationKeys.join(', '));
        if (row.kind === 'ShortDispatchUnresolved') {
            add('FieldSku', row.sku);
            add('FieldReason', row.reason ? (W(`Warehouse.Exception.Reason.${row.reason}`) || row.reason) : '');
            add('FieldExpected', row.expected);
            add('FieldIssued', row.issued);
            if (row.deferredBy) add('FieldDeferredBy', W(`Warehouse.Exception.DeferredBy.${row.deferredBy}`));
        }
        return pairs;
    };

    const fillDetails = (row) => {
        const host = byId('oc-details');
        if (!host) return;
        host.innerHTML = detailPairs(row)
            .map(([word, value]) => `<dt class="col-5 text-muted fw-normal">${esc(word)}</dt><dd class="col-7 mb-0">${esc(value)}</dd>`).join('');
    };

    const fillDifference = (row) => {
        const section = byId('oc-difference-section');
        if (!section) return;
        const isShort = row.kind === 'ShortDispatchUnresolved';
        section.classList.toggle('d-none', !isShort);
        if (!isShort) return;
        const deferred = byId('oc-deferred');
        const table = byId('oc-difference-table');
        const rows = byId('oc-difference-rows');
        if (row.deferredBy || !Array.isArray(row.differences)) {
            if (deferred) { deferred.textContent = format(W('SummaryDeferred'), W(`Warehouse.Exception.DeferredBy.${row.deferredBy}`)); deferred.classList.remove('d-none'); }
            table?.classList.add('d-none');
            if (rows) rows.innerHTML = '';
            return;
        }
        deferred?.classList.add('d-none');
        table?.classList.remove('d-none');
        if (rows) {
            rows.innerHTML = row.differences.map((d) => `<tr><td>${esc(d.lotId || W('NoLot'))}</td><td class="text-end">${esc(d.onHand)}</td>`
                + `<td class="text-end">${esc(d.bound)}</td><td class="text-end fw-medium">${esc(d.difference)}</td></tr>`).join('');
        }
    };

    const populateDetailsOffcanvas = (row) => {
        setText('offcanvasDetailsPreviewLabel', W(`Warehouse.Exception.Kind.${row.kind}`) || row.kind);
        setText('oc-subtitle', row.record);
        const kindEl = byId('oc-kind');
        if (kindEl) { kindEl.className = `badge ${KindBadge[row.kind] || 'bg-label-secondary'}`; kindEl.textContent = W(`Warehouse.Exception.Kind.${row.kind}`) || row.kind; }
        setText('oc-status', W(`Warehouse.Exception.Status.${row.status || 'Open'}`));
        setText('oc-hint', W(`Warehouse.Exception.Hint.${row.kind}`));
        setText('oc-record', row.record);
        setText('oc-warehouse', row.warehouse);
        setText('oc-opened', fmtDate(row.openedAt));
        setText('oc-code', codeText(row.code));
        fillDetails(row);
        fillDifference(row);
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
            { data: 'kind', name: 'kind' },
            { data: 'record', name: 'record' },
            { data: 'id', name: 'detail' },
            { data: 'warehouse', name: 'warehouse' },
            { data: 'code', name: 'code' },
            { data: 'openedAt', name: 'opened' },
            { data: 'id', name: 'action' }
        ];
        const visible = Array.from({ length: 6 }, (_, i) => base + i);
        const columnDefs = [
            { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
            { targets: base, render: (data) => kindBadge(data) },
            { targets: base + 1, render: (data, type, full) => recordCell(full) },
            { targets: base + 2, render: (data, type, full) => `<small>${esc(summary(full))}</small>` },
            { targets: base + 3, render: (data) => esc(data) },
            { targets: base + 4, render: (data) => `<small>${esc(codeText(data))}</small>` },
            { targets: base + 5, render: (data) => esc(fmtDate(data)) },
            { targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
        ];

        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: endpoint, type: 'GET', headers: getAuthHeaders(), error: onListError },
            onResponse,
            export: { mode: 'server', url: '/Warehouse/WarehouseExceptions/export', fileName: 'warehouse-exceptions' },
            actions: { onRowAction: rowActionHandlers },
            // Read-only list (§8.23): no "Yeni", no bulk action.
            toolbar: { addNewText: '', colvisColumns: visible },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Warehouse', pageKey: 'WarehouseExceptions', saveViewColumnIndexes: visible, defaultVisibleColumnIndexes: visible, baseOrder: [] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            config: { ordering: false, searching: false, columns, columnDefs }
        });
    };

    return {
        init: function () {
            initDataTable();
        },
        // the pure parts, read by the Node tests (no DOM)
        summary,
        linkOf
    };
})();

window.WarehouseExceptionsList = WarehouseExceptionsList;
document.addEventListener('DOMContentLoaded', () => WarehouseExceptionsList.init());
