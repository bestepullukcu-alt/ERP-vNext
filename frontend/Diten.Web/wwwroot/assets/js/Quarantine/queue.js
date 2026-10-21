/**
 * MOD-0175 Q5 — Karantina Kuyruğu (pack §11.3). THE LIST IS A COMPONENT (BL-440): DitenDataTable.createList, SERVER mode,
 * door /Quarantine/list/holds (listHolds: page / pageSize / q + the filters below; the service's order — open first, then the
 * most overdue — so no column sorts). KPI cards come from the same response (kpis), never from the rows in hand. Late rows are
 * highlighted (mockup), "SKT ≤ 90 gün" carries the warning icon. Row actions are pack §14 action codes: Karar Ver (DECIDE),
 * Detay (VIEWHOLD) — drawn only when QuarantineUi.can(code).
 */
'use strict';

(function () {
    const ui = window.QuarantineUi;
    const { t, esc, qty, date } = ui;
    const tableEl = document.querySelector('.datatables-quarantine-holds');
    let list = null;
    // WP-MVP1-FU-CLEAN1 (FU-0175-UI-MANUALHOLD-01): "Stoğu karantinaya al" reloads the list after a hold (manual-hold.js)
    window.QuarantineQueue = { reload: () => list?.dt?.ajax?.reload(null, false) };

    const filterFields = [
        { id: 'filterWarehouse', key: 'warehouseId', kind: 'single' },
        { id: 'filterStockStatus', key: 'stockStatus', kind: 'single' },
        { id: 'filterSource', key: 'source', kind: 'single' },
        { id: 'filterLate', key: 'lateOnly', kind: 'single' }
    ];

    const loadWarehouses = async () => {
        const select = ui.byId('filterWarehouse');
        const reply = await ui.get('/Quarantine/lookups/warehouses');
        if (!select || !reply.ok) return;
        (reply.json?.items || []).forEach((o) => select.add(new Option(o.text || o.value, o.value)));
    };

    const kpis = (k) => {
        if (!k) return;
        const set = (name, html) => { const el = document.querySelector(`[data-kpi="${name}"]`); if (el) el.innerHTML = html; };
        set('pending', esc(qty(k.openCount)));
        set('inReview', esc(t('Quarantine.Queue.Kpi.InReview', k.inReviewCount)));
        set('quantity', (k.quantityByUom || []).length
            ? k.quantityByUom.map((u) => `<span class="fw-medium" dir="ltr">${esc(qty(u.quantity))} <small class="text-muted">${esc(u.uomCode)}</small></span>`).join('<span class="text-muted">·</span>')
            : '<span class="text-muted">—</span>');
        set('late', esc(qty(k.lateCount)));
        set('expirySoon', esc(t('Quarantine.Queue.Kpi.ExpirySoon', k.expirySoonCount)));
    };

    const onResponse = (json) => {
        ui.hideError(ui.byId('qr-list-error'));
        ui.byId('qr-labels-unavailable')?.classList.toggle('d-none', json?.labelsAvailable !== false);
        kpis(json?.kpis);
        const total = json?.data?.total ?? 0;
        ui.setCount('QUARANTINEQUEUE', total);
        const footer = ui.byId('qr-footer');
        if (footer) footer.textContent = t('Quarantine.Queue.Footer', total);
    };

    const onListError = (xhr) => {
        let reply = { status: xhr?.status || 0, json: null };
        try { reply.json = JSON.parse(xhr?.responseText || 'null'); } catch (error) { reply.json = null; }
        ui.showError(ui.byId('qr-list-error'), reply);
    };

    const go = (url) => { window.location.href = url; };
    const rowActionHandlers = {
        DECIDE: ({ row }) => row?.holdId && go(`/Quarantine/Decide/${encodeURIComponent(row.holdId)}`),
        VIEWHOLD: ({ row }) => row?.holdId && go(`/Quarantine/Details/${encodeURIComponent(row.holdId)}`)
    };

    const renderAction = (full) => {
        const attrs = (text) => ({ 'data-id': full.holdId, 'data-json': JSON.stringify(full), title: text, 'aria-label': text });
        const open = full.status !== 'Decided' && !full.activeDecision;
        return window.DitenDataTable.renderActions([
            { key: 'DECIDE', visible: ui.can('DECIDE') && open, icon: 'bx bx-check-shield', text: t('Quarantine.Action.Decide'), attrs: attrs(t('Quarantine.Action.Decide')) },
            { key: 'VIEWHOLD', visible: ui.can('VIEWHOLD'), icon: 'bx bx-show', text: t('Quarantine.Action.Detail'), attrs: attrs(t('Quarantine.Action.Detail')) }
        ]);
    };

    const expiryCell = (lot) => {
        if (!lot?.expiryDate) return '<span class="text-muted">—</span>';
        const days = ui.daysUntil(lot.expiryDate);
        const soon = days != null && days <= 90;
        return `<div class="d-flex flex-column"><span>${esc(date(lot.expiryDate))}${soon ? ' <i class="bx bx-error-circle text-danger"></i>' : ''}</span>`
            + (soon ? `<small class="text-danger">${esc(days < 0 ? t('Quarantine.Common.DaysPast', -days) : t('Quarantine.Common.DaysLeft', days))}</small>` : '') + '</div>';
    };
    const waitCell = (row) => `<div class="d-flex flex-column"><span class="fw-medium ${row.late ? 'text-warning' : ''}">${esc(t('Quarantine.Common.Days', row.waitDays))}</span>`
        + `<small class="text-muted">${esc(t('Quarantine.Common.Target', row.targetDays))}</small></div>`;
    const sourceCell = (row) => `<div class="d-flex flex-column"><span>${esc(t('Quarantine.Source.' + row.source) || row.source)}</span>`
        + `<small class="text-muted font-monospace">${esc(row.sourceDocument?.number || row.sourceDocument?.text || '')}</small></div>`;

    const init = async () => {
        if (!tableEl) return;
        ui.loadCounts('QUARANTINEQUEUE');
        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: '/Quarantine/list/holds', type: 'GET', error: onListError },
            onResponse,
            actions: { onRowAction: rowActionHandlers },
            // FU-0175-UI-MANUALHOLD-01: the button only with quarantine.holds.create (QUARANTINELOT) AND the modal the view drew for that key
            toolbar: {
                addNewText: ui.can('QUARANTINELOT') && ui.byId('qr-manual-modal') ? t('Quarantine.ManualHold.Action') : '',
                onAddNew: () => window.QuarantineManualHold?.open(),
                colvisColumns: [2, 3, 4, 5, 6, 7, 8, 9]
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadWarehouses },
            savedView: {
                moduleKey: 'Quarantine',
                pageKey: 'Queue',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
                baseOrder: []
            },
            config: {
                ordering: false,
                searching: true,
                pageLength: 10,
                order: [],
                language: { emptyTable: t('Quarantine.Queue.Empty'), zeroRecords: t('Quarantine.Queue.Empty') },
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                createdRow: (row, data) => {
                    if (data.late && data.status !== 'Decided') row.style.backgroundColor = '#fff9ee';
                    if (!ui.can('VIEWHOLD')) return;
                    row.style.cursor = 'pointer';
                    row.addEventListener('click', (event) => {
                        if (event.target.closest('a, button, .dropdown-menu, td.control')) return;
                        go(`/Quarantine/Details/${encodeURIComponent(data.holdId)}`);
                    });
                },
                columns: [
                    { data: 'holdId', name: 'control' },
                    { data: 'itemLabel', name: 'product' },
                    { data: 'lot', name: 'lot' },
                    { data: 'lot', name: 'expiry' },
                    { data: 'warehouseLabel', name: 'where' },
                    { data: 'stockStatus', name: 'stockStatus' },
                    { data: 'quantity', name: 'quantity' },
                    { data: 'enteredAtUtc', name: 'enteredAt' },
                    { data: 'waitDays', name: 'wait' },
                    { data: 'source', name: 'source' },
                    { data: 'status', name: 'status' },
                    { data: 'holdId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => ui.productCell(full, full.holdNumber) },
                    { targets: 2, render: (data) => ui.lotLink(data) },
                    { targets: 3, render: (data) => expiryCell(data) },
                    { targets: 4, render: (data, type, full) => ui.whereCell(full) },
                    { targets: 5, render: (data) => ui.stockBadge(data) },
                    { targets: 6, className: 'text-end', render: (data, type, full) => `<span dir="ltr">${esc(qty(data))} ${esc(ui.uom(full))}</span>` },
                    { targets: 7, render: (data) => esc(date(data)) },
                    { targets: 8, render: (data, type, full) => waitCell(full) },
                    { targets: 9, render: (data, type, full) => sourceCell(full) },
                    { targets: 10, render: (data, type, full) => ui.holdBadge(full) },
                    { targets: -1, title: t('Quarantine.Col.Actions'), searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
                ]
            }
        });
    };

    document.addEventListener('DOMContentLoaded', init);
})();
