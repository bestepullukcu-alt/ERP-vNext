/**
 * MOD-0176 F4 — Süresi Dolmuş Stok (pack §11.1). DitenDataTable.createList, SERVER mode, door /Expiry/list/expired (listExpired: most
 * expired first). "Dolduğundan beri" (D-5). No decision here — "Karantinaya al" opens MOD-0175's blocked-stock page (a link).
 */
'use strict';

(function () {
    const ui = window.ExpiryUi;
    const { t, esc, qty } = ui;
    const tableEl = document.querySelector('.datatables-expiry-expired');
    const filterFields = [{ id: 'filterWarehouse', key: 'warehouseId', kind: 'single' }];

    const onResponse = (json) => {
        ui.hideError(ui.byId('ex-list-error'));
        ui.byId('ex-labels-unavailable')?.classList.toggle('d-none', json?.labelsAvailable !== false);
        const warehouse = ui.byId('filterWarehouse')?.value;
        const a = ui.byId('ex-export');
        if (a) a.href = `/Expiry/export/expired${warehouse ? `?warehouseId=${encodeURIComponent(warehouse)}` : ''}`;
    };

    const onListError = (xhr) => {
        let reply = { status: xhr?.status || 0, json: null };
        try { reply.json = JSON.parse(xhr?.responseText || 'null'); } catch (error) { reply.json = null; }
        ui.showError(ui.byId('ex-list-error'), reply);
    };

    const init = async () => {
        if (!tableEl) return;
        const query = new URLSearchParams(window.location.search);
        await ui.loadWarehouses(ui.byId('filterWarehouse'));
        if (query.get('warehouseId')) ui.byId('filterWarehouse').value = query.get('warehouseId');
        await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: '/Expiry/list/expired', type: 'GET', error: onListError },
            onResponse,
            toolbar: { addNewText: '', colvisColumns: [3, 4, 6, 7, 8, 9] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields },
            savedView: {
                moduleKey: 'Expiry',
                pageKey: 'Expired',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9],
                baseOrder: []
            },
            config: {
                ordering: false,
                searching: false,
                pageLength: 25,
                order: [],
                language: { emptyTable: t('Expiry.Expired.Empty'), zeroRecords: t('Expiry.Expired.Empty') },
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'lotId', name: 'control' },
                    { data: 'itemLabel', name: 'product' },
                    { data: 'lotNumber', name: 'lot' },
                    { data: 'warehouseLabel', name: 'warehouse' },
                    { data: 'expiryDate', name: 'expiry' },
                    { data: 'daysSinceExpiry', name: 'since' },
                    { data: 'onHand', name: 'available' },
                    { data: 'onHand', name: 'held' },
                    { data: 'severity', name: 'status' },
                    { data: 'openExceptionId', name: 'exception' },
                    { data: 'lotId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => ui.productCell(full) },
                    { targets: 2, render: (data, type, full) => ui.lotLink(full) },
                    { targets: 3, render: (data) => esc(data || '') },
                    { targets: 4, render: (data) => esc(ui.date(data)) },
                    { targets: 5, className: 'text-end', render: (data) => `<span class="fw-medium text-danger" dir="ltr">${esc(t('Expiry.Expired.SinceDays', qty(data)))}</span>` },
                    { targets: 6, className: 'text-end', render: (data) => ui.amount(data?.available, data) },
                    { targets: 7, className: 'text-end', render: (data) => Number(data?.held) ? ui.amount(data.held, data) : '<span class="text-muted">—</span>' },
                    { targets: 8, render: (data, type, full) => ui.statusCell(full) },
                    { targets: 9, render: (data) => !data ? '<span class="text-muted">—</span>'
                        // O-F4-4 (F5): the badge opens the case where the reader may read cases
                        : ui.can('EXPIRY_EXCEPTIONS_VIEW') ? `<a class="badge bg-label-danger" href="/Expiry/Exceptions/Details/${encodeURIComponent(data)}"><i class="bx bx-error-alt me-1"></i>${esc(t('Expiry.Col.Exception'))}</a>`
                        : `<span class="badge bg-label-danger"><i class="bx bx-error-alt me-1"></i>${esc(t('Expiry.Col.Exception'))}</span>` },
                    { targets: -1, title: t('Expiry.Col.Actions'), searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => ui.rowActions(full, '/Quarantine/Blocked') }
                ]
            }
        });
    };

    document.addEventListener('DOMContentLoaded', init);
})();
