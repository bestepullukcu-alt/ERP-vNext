'use strict';

const ShipmentList = (function () {
    const endpoint = '/SupplyChain/Shipments/api';
    const tableElement = document.querySelector('.datatables-shipments');
    const L = window.L10n || {};
    let table;

    const uuid = () => crypto.randomUUID();
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest', 'X-Correlation-Id': uuid() });
    const escapeHtml = (value) => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    const formatDate = (value) => value ? new Intl.DateTimeFormat(document.documentElement.lang || 'en', {
        dateStyle: 'medium', timeStyle: 'short'
    }).format(new Date(value)) : L.unavailable;
    const badge = (status) => {
        const css = ({ Draft: 'secondary', Planned: 'info', Dispatched: 'primary', InTransit: 'warning',
            Delivered: 'success', Exception: 'danger', Closed: 'dark', Cancelled: 'secondary' })[status] || 'secondary';
        // Q371: the wire value stays the contract's English name; only the label is localized (all seven languages).
        const label = (L.statuses || {})[status] || status || L.unknown;
        return `<span class="badge bg-label-${css}">${escapeHtml(label)}</span>`;
    };
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
        alert.classList.remove('d-none'); tableElement?.closest('.card')?.classList.add('d-none');
    };
    const clearLoadFailure = () => {
        document.getElementById('shipmentsLoadError')?.classList.add('d-none'); tableElement?.closest('.card')?.classList.remove('d-none');
    };
    const notifyFailure = async (response) => {
        if (response.status === 401) { window.DtDefaults?.handleUnauthorized?.(); return; }
        if (response.status === 403) { showDenied(); return; }
        let body = null;
        try { body = await response.json(); } catch (_) { }
        const code = body?.error?.code || '';
        showLoadFailure(code === 'INTERNAL_ERROR' ? L.internalError : L.listUnavailable,
            body?.error?.correlationId || response.headers.get('X-Correlation-Id'));
    };
    const buildUrl = (request) => {
        const params = new URLSearchParams({
            page: String(Math.floor(request.start / request.length) + 1),
            pageSize: String(Math.min(request.length, 200))
        });
        const status = document.getElementById('filterStatus')?.value || '';
        // Stored source document ids are trimmed, so padded input would match nothing.
        const source = (document.getElementById('filterSourceDocumentId')?.value || '').trim();
        if (status) params.set('status', status);
        if (source) params.set('sourceDocumentId', source);
        return `${endpoint}?${params.toString()}`;
    };
    const load = async (request, callback) => {
        try {
            const response = await fetch(buildUrl(request), { credentials: 'same-origin', headers: getAuthHeaders() });
            if (!response.ok) { await notifyFailure(response); callback({ draw: request.draw, recordsTotal: 0, recordsFiltered: 0, data: [] }); return; }
            const body = await response.json();
            const items = Array.isArray(body?.items) ? body.items : [];
            const total = Number(body?.total || 0);
            clearLoadFailure();
            callback({ draw: request.draw, recordsTotal: total, recordsFiltered: total, data: items });
        } catch (error) {
            console.error('[Shipments] List request failed.', error);
            showLoadFailure(L.listUnavailable);
            callback({ draw: request.draw, recordsTotal: 0, recordsFiltered: 0, data: [] });
        }
    };
    const init = () => {
        if (!tableElement || typeof DataTable === 'undefined') return;
        const toolbar = window.DtDefaults.exportButtons('', {}, {}, { exportColumns: [1, 2, 3, 4, 5], colvisColumns: [1, 2, 3, 4, 5] })
            .filter((feature) => !feature.buttons?.some((button) => String(button.className || '').includes('dt-export-collection-btn')));
        table = new DataTable(tableElement, window.DtDefaults.create({
            processing: true, serverSide: true, stateSave: false, searching: false, ajax: load,
            order: [[4, 'desc']], responsive: { details: { type: 'column', target: 0 } },
            columns: [
                { data: null, defaultContent: '', className: 'control', orderable: false },
                { data: 'shipmentNumber', render: (value) => escapeHtml(value) },
                { data: 'sourceDocumentId', render: (value) => escapeHtml(value) },
                { data: 'status', render: badge },
                { data: 'plannedShipAt', render: formatDate },
                { data: 'plannedDeliverAt', render: formatDate },
                { data: 'shipmentId', orderable: false, searchable: false, render: (id) => `<a class="btn btn-sm btn-icon btn-label-primary" href="/SupplyChain/Shipments/Details/${encodeURIComponent(id)}" aria-label="${escapeHtml(L.details)}"><i class="bx bx-show" aria-hidden="true"></i></a>` }
            ], buttons: toolbar, language: { emptyTable: L.empty, zeroRecords: L.empty }
        }));
        document.getElementById('btnFilterApply')?.addEventListener('click', () => table.ajax.reload());
        document.getElementById('btnFilterReset')?.addEventListener('click', () => {
            document.getElementById('filterStatus').value = '';
            document.getElementById('filterSourceDocumentId').value = '';
            table.ajax.reload();
        });
    };
    document.addEventListener('DOMContentLoaded', init);
    return { init };
})();
