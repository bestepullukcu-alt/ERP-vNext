'use strict';

// MOD-0183 r9 §24.3 (DL-4) Golden Compact list: DitenDataTable.createList in SERVER mode (data_mode: server in the pack, the
// <table> marker and here alike, SB-9). The adapter answers { data: { items, total, filteredTotal } } with names resolved
// (§24.4); no cell shows a GUID except the shipment number until the SHP follow-up, and customerRef never reaches the page.
const ShipmentList = (function () {
    const endpoint = '/SupplyChain/Shipments/api';
    const tableElement = document.getElementById('shipmentsTable');
    const L = window.L10n || {};
    let list;
    // R-2 (SHIPMENT-BUNDLE 3.2.0): a REQUIRED scope on every call, not an optional filter — the shared company selector chose
    // it and Index.cshtml draws no list until one is chosen. Turning it into the downstream header is the adapter's job.
    const company = () => document.getElementById('shipmentsListSurface')?.dataset.legalEntityId || '';

    // The shared list headers (X-Requested-With and friends) on the factory's page requests.
    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || { 'X-Requested-With': 'XMLHttpRequest' };
    const escapeHtml = (value) => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    const formatDate = (value) => value ? new Intl.DateTimeFormat(document.documentElement.lang || 'en', {
        dateStyle: 'medium', timeStyle: 'short'
    }).format(new Date(value)) : L.unavailable;
    const badge = (status) => {
        const css = ({ Draft: 'secondary', Planned: 'info', Dispatched: 'primary', InTransit: 'warning',
            Delivered: 'success', Exception: 'danger', Closed: 'dark', Cancelled: 'secondary' })[status] || 'secondary';
        // Q371 / SB-13: the wire value stays the contract's English name; only the label is localized (all seven languages).
        const label = (L.statuses || {})[status] || status || L.unknown;
        return `<span class="badge bg-label-${css}">${escapeHtml(label)}</span>`;
    };
    // §24.4 / SB-10: a failed or denied name read shows "name unavailable"; for warehouse and location the reference follows
    // small and LTR; for the customer only the text, never an id. A deleted account is "not found". An old record (no
    // customerRef) shows its shipToReference as stored, marked "old record" (SB-11a).
    const reference = (value) => value ? ` <small class="text-muted" dir="ltr">${escapeHtml(value)}</small>` : '';
    const customerCell = (cell) => {
        switch (cell?.state) {
            case 'resolved': return `${escapeHtml(cell.name || L.nameUnavailable)}${cell.code ? ` <small class="text-muted">${escapeHtml(cell.code)}</small>` : ''}`;
            case 'oldRecord': return `<span class="badge bg-label-secondary me-1">${escapeHtml(L.oldRecord)}</span>${escapeHtml(cell.reference || '')}`;
            case 'notFound': return `<span class="text-muted">${escapeHtml(L.nameNotFound)}</span>`;
            default: return `<span class="text-muted">${escapeHtml(L.nameUnavailable)}</span>`;
        }
    };
    const placeCell = (cell) => cell?.state === 'resolved' ? escapeHtml(cell.label)
        : `<span class="text-muted">${escapeHtml(L.nameUnavailable)}</span>${reference(cell?.reference)}`;

    // UAS-001: a 403 from the list API replaces the whole list surface with the access-denied card, never an empty table.
    const showDenied = () => {
        const surface = document.getElementById('shipmentsListSurface'); const denied = document.getElementById('shipmentsListDenied');
        if (!surface || !denied) return;
        surface.hidden = true; surface.setAttribute('inert', ''); denied.hidden = false;
    };
    // A failed load is its own state: an alert in place of the table, so it never reads as "no shipments".
    const showLoadFailure = (message, correlation) => {
        const alert = document.getElementById('shipmentsLoadError'); if (!alert) return;
        alert.textContent = `${message}${correlation ? ` ${L.supportReference}: ${correlation}` : ''}`;
        alert.classList.remove('d-none');
    };
    const onListError = (xhr) => {
        if (xhr?.status === 401) { window.DtDefaults?.handleUnauthorized?.(); return; }
        if (xhr?.status === 403) { showDenied(); return; }
        let body = null;
        try { body = JSON.parse(xhr?.responseText || '{}'); } catch (_) { body = null; }
        const code = body?.error?.code || '';
        showLoadFailure(code === 'INTERNAL_ERROR' ? L.internalError : L.listUnavailable,
            body?.error?.correlationId || xhr?.getResponseHeader?.('X-Correlation-Id'));
    };
    const detailsUrl = (id) => `/SupplyChain/Shipments/Details/${encodeURIComponent(id)}?legalEntityId=${encodeURIComponent(company())}`;
    const filterFields = [
        { id: 'filterStatus', key: 'status', kind: 'single' },
        { id: 'filterSourceDocumentId', key: 'sourceDocumentId', kind: 'single' }
    ];

    const init = async () => {
        if (!tableElement || !window.DitenDataTable?.createList) return;
        list = await window.DitenDataTable.createList({
            tableEl: tableElement,
            dataMode: 'server',
            ajax: {
                url: endpoint, type: 'GET', error: onListError, headers: getAuthHeaders(),
                // The company travels with every page request (the selector is the page's scope, not a list filter).
                beforeSend: function (xhr, settings) {
                    settings.url += (settings.url.includes('?') ? '&' : '?') + new URLSearchParams({ legalEntityId: company() });
                    xhr.setRequestHeader('X-Correlation-Id', crypto.randomUUID());
                }
            },
            // BL-452 (owner decision 2026-11-05): CSV / Excel are every matching row, written by the adapter, not the page.
            export: { mode: 'server', url: `${endpoint}/export?legalEntityId=${encodeURIComponent(company())}`, fileName: 'shipments' },
            onResponse: () => document.getElementById('shipmentsLoadError')?.classList.add('d-none'),
            actions: { onRowAction: { quickView: ({ id }) => { if (id) window.location.href = detailsUrl(id); } } },
            toolbar: { addNewText: '', exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields },
            savedView: { moduleKey: 'SupplyChain', pageKey: 'Shipments', saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7], defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7], baseOrder: [[6, 'desc']] },
            config: {
                language: { emptyTable: L.empty, zeroRecords: L.empty },
                columns: [
                    { data: 'shipmentId', name: 'control' },
                    { data: 'shipmentNumber', name: 'shipmentNumber' },
                    { data: 'sourceDocumentId', name: 'sourceDocumentId' },
                    { data: 'customer', name: 'customer' },
                    { data: 'warehouse', name: 'warehouse' },
                    { data: 'status', name: 'status' },
                    { data: 'plannedShipAt', name: 'plannedShipAt' },
                    { data: 'plannedDeliverAt', name: 'plannedDeliverAt' },
                    { data: 'shipmentId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => type === 'display' ? `<a class="fw-medium text-heading" href="${detailsUrl(full.shipmentId)}">${escapeHtml(data)}</a>` : (data ?? '') },
                    { targets: 2, render: (data, type) => type === 'display' ? escapeHtml(data) : (data ?? '') },
                    // Names are not stored on the shipment, so neither column is sortable or searchable (§24.3).
                    { targets: 3, orderable: false, searchable: false, render: (data, type) => type === 'display' ? customerCell(data) : '' },
                    { targets: 4, orderable: false, searchable: false, render: (data, type) => type === 'display' ? placeCell(data) : '' },
                    { targets: 5, render: (data, type) => type === 'display' ? badge(data) : (data ?? '') },
                    { targets: [6, 7], render: (data, type) => type === 'display' ? escapeHtml(formatDate(data)) : (data ?? '') },
                    {
                        targets: -1, title: L.actions || '', searchable: false, orderable: false, className: 'cell-fit all',
                        render: (data, type, full) => window.DitenDataTable.renderActions([{ key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-id': full.shipmentId, title: L.details, 'aria-label': L.details } }])
                    }
                ]
            }
        });
    };
    document.addEventListener('DOMContentLoaded', () => { void init(); });
    return { init, customerCell, placeCell };
})();
