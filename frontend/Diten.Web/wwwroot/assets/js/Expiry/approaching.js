/**
 * MOD-0176 F4 — Son Kullanma Yaklaşanlar (pack §11.3). THE LIST IS A COMPONENT: DitenDataTable.createList, SERVER mode, door
 * /Expiry/list/approaching (listApproaching: the service's order — expiry ascending, the FEFO order; severity from the company
 * horizons). Kullanılabilir and Bekleyen are separate columns (D-11). Row actions by action code (ExpiryUi.can).
 */
'use strict';

(function () {
    const ui = window.ExpiryUi;
    const { t, esc, qty } = ui;
    const tableEl = document.querySelector('.datatables-expiry-approaching');

    const filterFields = [
        { id: 'filterWarehouse', key: 'warehouseId', kind: 'single' },
        { id: 'filterSeverity', key: 'severity', kind: 'single' },
        { id: 'filterHorizon', key: 'horizonDays', kind: 'single' },
        { id: 'filterHeld', key: 'includeHeld', kind: 'single' }
    ];

    const exportHref = () => {
        const params = new URLSearchParams();
        filterFields.forEach((f) => { const v = ui.byId(f.id)?.value; if (v) params.set(f.key, v); });
        const a = ui.byId('ex-export');
        if (a) a.href = `/Expiry/export/approaching${params.toString() ? '?' + params : ''}`;
    };

    const onResponse = (json) => {
        ui.hideError(ui.byId('ex-list-error'));
        ui.byId('ex-labels-unavailable')?.classList.toggle('d-none', json?.labelsAvailable !== false);
        const total = json?.data?.total ?? 0;
        const horizon = ui.byId('filterHorizon')?.value;
        const count = ui.byId('ex-count');
        if (count) count.textContent = horizon ? t('Expiry.Approaching.Count', qty(total), horizon) : t('Expiry.Kpi.Lots', qty(total));
        exportHref();
    };

    const onListError = (xhr) => {
        let reply = { status: xhr?.status || 0, json: null };
        try { reply.json = JSON.parse(xhr?.responseText || 'null'); } catch (error) { reply.json = null; }
        ui.showError(ui.byId('ex-list-error'), reply);
    };

    const daysCell = (row) => {
        const tone = row.severity === 'Critical' ? 'text-warning' : row.severity === 'Approaching' ? 'text-info' : 'text-heading';
        return `<span class="fw-medium ${tone}" dir="ltr">${esc(qty(row.daysToExpiry))}</span>`;
    };

    const init = async () => {
        if (!tableEl) return;
        // pre-fill from the panel's card links (?horizonDays=… / ?warehouseId=…)
        const query = new URLSearchParams(window.location.search);
        await ui.loadWarehouses(ui.byId('filterWarehouse'));
        ['warehouseId:filterWarehouse', 'horizonDays:filterHorizon', 'severity:filterSeverity'].forEach((pair) => {
            const [key, id] = pair.split(':');
            const value = query.get(key);
            const select = ui.byId(id);
            if (value && select && !Array.from(select.options).some((o) => o.value === value)) select.add(new Option(t('Expiry.Common.Days', value), value));
            if (value && select) select.value = value;
        });
        await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: '/Expiry/list/approaching', type: 'GET', error: onListError },
            onResponse,
            toolbar: { addNewText: '', colvisColumns: [2, 4, 5, 7, 8, 9] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields },
            savedView: {
                moduleKey: 'Expiry',
                pageKey: 'Approaching',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
                baseOrder: []
            },
            config: {
                ordering: false,
                searching: true,
                pageLength: 25,
                order: [],
                language: { emptyTable: t('Expiry.Approaching.Empty'), zeroRecords: t('Expiry.Approaching.Empty') },
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'lotId', name: 'control' },
                    { data: 'itemLabel', name: 'product' },
                    { data: 'item', name: 'skuLevel' },
                    { data: 'lotNumber', name: 'lot' },
                    { data: 'warehouseLabel', name: 'warehouse' },
                    { data: 'expiryDate', name: 'expiry' },
                    { data: 'daysToExpiry', name: 'days' },
                    { data: 'remainingShelfLifePct', name: 'shelfLife' },
                    { data: 'onHand', name: 'available' },
                    { data: 'onHand', name: 'held' },
                    { data: 'severity', name: 'status' },
                    { data: 'lotId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => ui.productCell(full) },
                    { targets: 2, render: (data) => `<span class="badge bg-label-secondary">${esc(data?.skuLevel || '')}</span>` },
                    { targets: 3, render: (data, type, full) => ui.lotLink(full) },
                    { targets: 4, render: (data) => esc(data || '') },
                    { targets: 5, render: (data) => esc(ui.date(data)) },
                    { targets: 6, className: 'text-end', render: (data, type, full) => daysCell(full) },
                    { targets: 7, render: (data, type, full) => ui.shelfBar(full) },
                    { targets: 8, className: 'text-end', render: (data) => ui.amount(data?.available, data) },
                    { targets: 9, className: 'text-end', render: (data) => Number(data?.held) ? ui.amount(data.held, data) : '<span class="text-muted">—</span>' },
                    { targets: 10, render: (data, type, full) => ui.statusCell(full) },
                    { targets: -1, title: t('Expiry.Col.Actions'), searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => ui.rowActions(full, '/Quarantine') }
                ]
            }
        });
    };

    document.addEventListener('DOMContentLoaded', init);
})();
