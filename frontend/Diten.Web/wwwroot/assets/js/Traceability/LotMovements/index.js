/**
 * MOD-0174 P3 — Lot Hareket Geçmişi (pack §11.3): the FU01 S-2 history wire (/LotMovements/api → INVENTORY-BUNDLE
 * getTransactions?lotId=, the FU01 adapter) for ONE lot. The lot comes from the selector (or the address). A serial
 * chip (from Seri Gezgini) names the serial and highlights its last movement — the contract's movement row carries no
 * serial numbers, so rows are not filtered by serial. Row → the receipt modal (VIEWRECEIPT), "Stok Defteri'nde aç"
 * (RECEIPTINLEDGER) — action codes of the pack §14 table, drawn per TraceUi.can.
 */
'use strict';

const LotMovementsList = (function () {
    let list = null;
    const ui = window.TraceUi;
    const t = ui.t;
    const esc = ui.esc;
    const tableEl = document.querySelector('.datatables-lotmovements');
    const byId = (id) => document.getElementById(id);
    const context = byId('lotmovements-context');
    const lotSelect = byId('lotMovementsLot');
    const state = { lotId: context?.dataset.lotId || '', serial: context?.dataset.serial || '', lastTransactionId: null };

    const filterFields = [
        { id: 'filterFrom', key: 'from', kind: 'text' },
        { id: 'filterTo', key: 'to', kind: 'text' }
    ];

    const initLotPicker = async () => {
        if (!lotSelect || !window.jQuery?.fn?.select2) return;
        $(lotSelect).select2({
            width: '100%', placeholder: lotSelect.dataset.placeholder || '',
            language: { searching: () => t('Searching'), noResults: () => t('NoResults') },
            ajax: { url: '/LotMovements/lookups/lots', dataType: 'json', delay: 250, data: (p) => ({ q: p.term || '' }),
                processResults: (d) => ({ results: (d?.items || []).map((o) => ({ id: o.value, text: o.text })) }) }
        });
        if (state.lotId) {
            const reply = await ui.getJson(`/LotMovements/lookups/lot?${new URLSearchParams({ lotId: state.lotId })}`);
            lotSelect.add(new Option(reply.json?.lotNumber || state.lotId, state.lotId, true, true));
            $(lotSelect).trigger('change.select2');
        }
        $(lotSelect).on('change', () => {
            state.lotId = lotSelect.value;
            state.serial = '';
            renderChip();
            list?.dt?.ajax?.reload();
        });
    };

    const renderChip = async () => {
        const chip = byId('lotmovements-serial-chip');
        if (!chip) return;
        state.lastTransactionId = null;
        if (!state.serial || !state.lotId) { chip.classList.add('d-none'); return; }
        chip.textContent = t('Chip.Serial', state.serial);
        chip.title = t('Chip.SerialLast', state.serial);
        chip.classList.remove('d-none');
        const reply = await ui.getJson(`/LotMovements/lookups/serial?${new URLSearchParams({ lotId: state.lotId, serial: state.serial })}`);
        state.lastTransactionId = reply.json?.lastTransactionId || null;
        list?.dt?.rows?.().invalidate?.().draw?.(false);
    };

    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        const box = byId('lotmovements-list-error');
        if (box) { box.textContent = message || t('Error.ServiceUnavailable'); box.classList.remove('d-none'); }
    };

    const fill = (name, text) => { const n = document.querySelector(`#modalLotReceipt [data-receipt="${name}"]`); if (n) n.textContent = text || '—'; };
    const openReceipt = (row) => {
        fill('number', row.transactionNumber || row.id);
        fill('type', t('Movement.' + row.movementType));
        fill('quantity', `${ui.qty(row.quantity)} ${row.uomId || ''}`.trim());
        fill('product', [row.productLabel || row.itemId, row.skuLabel].filter(Boolean).join(' · '));
        fill('lot', lotSelect?.selectedOptions?.[0]?.text || row.lotId);
        fill('status', row.toStockStatus ? t('StockStatus.' + row.toStockStatus) : (row.fromStockStatus ? t('StockStatus.' + row.fromStockStatus) : ''));
        fill('to', [row.toWarehouseLabel || row.toWarehouseId, row.toLocationLabel || row.toLocationId].filter(Boolean).join(' / '));
        fill('document', row.sourceDocumentId);
        fill('date', ui.dateTime(row.transactionDate));
        const ledger = document.querySelector('#modalLotReceipt [data-receipt="ledger"]');
        if (ledger) ledger.href = `/StockMovements?${new URLSearchParams({ lotId: row.lotId || state.lotId })}`;
        const modal = document.getElementById('modalLotReceipt');
        if (modal && window.bootstrap) window.bootstrap.Modal.getOrCreateInstance(modal).show();
    };

    const rowActionHandlers = { VIEWRECEIPT: ({ row }) => row && openReceipt(row) };

    const renderAction = (full) => window.DitenDataTable.renderActions([
        { key: 'VIEWRECEIPT', visible: ui.can('VIEWRECEIPT'), icon: 'bx bx-receipt', text: t('Action.VIEWRECEIPT'),
          attrs: { 'data-id': full.id, 'data-json': JSON.stringify(full), title: t('Action.VIEWRECEIPT'), 'aria-label': t('Action.VIEWRECEIPT') } }
    ]);

    const signed = (row) => {
        const outbound = ['out', 'Outbound'].includes(row.direction) || /ISSUE|LOSS|SCRAP|WRITE_OFF|SUPPLIER_RETURN/.test(row.movementType || '');
        return `<span class="${outbound ? 'text-danger' : 'text-success'}" dir="ltr">${outbound ? '−' : '+'}${esc(ui.qty(row.quantity))} ${esc(row.uomId || '')}</span>`;
    };

    const initDataTable = async () => {
        if (!tableEl) return;
        list = await window.DitenDataTable.createList({
            tableEl: tableEl,
            dataMode: 'server',
            ajax: {
                url: '/LotMovements/api', type: 'GET', error: onListError,
                // The lot travels with every page request (the selector is the page's, not a list filter).
                beforeSend: function (xhr, settings) { settings.url += (settings.url.includes('?') ? '&' : '?') + new URLSearchParams({ lotId: state.lotId || '' }); }
            },
            onResponse: () => byId('lotmovements-list-error')?.classList.add('d-none'),
            actions: { onRowAction: rowActionHandlers },
            toolbar: { addNewText: '', colvisColumns: [2, 3, 4, 5, 6, 7] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: async () => { await initLotPicker(); renderChip(); } },
            savedView: { moduleKey: 'Traceability', pageKey: 'LotMovements', saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7], defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7], baseOrder: [[7, 'desc']] },
            config: {
                ordering: false,
                searching: false,
                initComplete: function () { try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ } },
                createdRow: (row, data) => {
                    if (state.lastTransactionId && data.id === state.lastTransactionId) row.classList.add('table-info');
                    if (!ui.can('VIEWRECEIPT')) return;
                    row.style.cursor = 'pointer';
                    row.addEventListener('click', (event) => { if (!event.target.closest('a, button, .dropdown-menu, td.control')) openReceipt(data); });
                },
                columns: [
                    { data: 'id', name: 'control' }, { data: 'transactionNumber', name: 'transactionNumber' }, { data: 'movementType', name: 'movementType' },
                    { data: 'quantity', name: 'quantity' }, { data: 'toLocationLabel', name: 'to' }, { data: 'toStockStatus', name: 'status' },
                    { data: 'sourceDocumentId', name: 'source' }, { data: 'transactionDate', name: 'transactionDate' }, { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => `<span class="fw-medium font-monospace">${esc(data || full.id || '')}</span>` },
                    { targets: 2, render: (data, type, full) => esc(t('Movement.' + data)) + (full.isAutomatic ? ` <span class="badge bg-label-info ms-1">${esc(t('Badge.Automatic'))}</span>` : '') },
                    { targets: 3, render: (data, type, full) => signed(full) },
                    { targets: 4, render: (data, type, full) => esc([full.toWarehouseLabel || full.toWarehouseId, full.toLocationLabel || full.toLocationId].filter(Boolean).join(' / ')) },
                    { targets: 5, render: (data, type, full) => esc(full.toStockStatus ? t('StockStatus.' + full.toStockStatus) : (full.fromStockStatus ? t('StockStatus.' + full.fromStockStatus) : '')) },
                    { targets: 6, render: (data) => esc(data || '') },
                    { targets: 7, render: (data) => esc(ui.dateTime(data)) },
                    { targets: -1, title: t('Col.Actions'), orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
                ]
            }
        });
    };

    return { init: initDataTable };
})();

document.addEventListener('DOMContentLoaded', () => LotMovementsList.init());
