/**
 * MOD-0174 P3 — Seri Gezgini (pack §11.3): DitenDataTable.createList, server mode, '/Serials/api' (TRACE listSerials),
 * 15 rows per page. Filters: serial (search box), product, lot (dependent on the product), status. Row actions:
 * SERIALLOT (Lot Detayı) and SERIALMOVES (Hareket Geçmişi) — action codes of the pack §14 table, drawn per TraceUi.can.
 */
'use strict';

const SerialsList = (function () {
    let list = null;
    const ui = window.TraceUi;
    const t = ui.t;
    const esc = ui.esc;
    const tableEl = document.querySelector('.datatables-serials');
    const byId = (id) => document.getElementById(id);
    const contextLot = byId('serials-context')?.dataset.lotId || '';

    const filterFields = [
        { id: 'filterItemId', key: 'itemId', kind: 'text' },
        { id: 'filterLotId', key: 'lotId', kind: 'single' },
        { id: 'filterStatus', key: 'status', kind: 'single' }
    ];

    const loadLots = async () => {
        const select = byId('filterLotId');
        if (!select) return;
        const keep = select.value || contextLot;
        Array.from(select.options).slice(1).forEach((o) => o.remove());
        const itemId = byId('filterItemId')?.value;
        if (itemId) {
            const reply = await ui.getJson(`/Serials/lookups/lots?${new URLSearchParams({ itemId })}`);
            (reply.json?.items || []).forEach((o) => select.add(new Option(o.text, o.value)));
        }
        if (keep && !Array.from(select.options).some((o) => o.value === keep)) select.add(new Option(keep, keep));
        if (window.jQuery) $(select).val(keep || '').trigger('change.select2');
    };

    const initPickers = async () => {
        const el = byId('filterItemId');
        if (el && window.jQuery?.fn?.select2) {
            $(el).select2({
                dropdownParent: $(document.body), dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm',
                placeholder: $(el).data('placeholder') || '', allowClear: true, width: 'element',
                language: { searching: () => t('Searching'), noResults: () => t('NoResults') },
                ajax: { url: '/Serials/lookups/products', dataType: 'json', delay: 250, data: (p) => ({ q: p.term || '' }),
                    processResults: (d) => ({ results: (d?.items || []).map((o) => ({ id: o.value, text: o.text })) }) }
            });
            $(el).on('change', loadLots);
        }
        await loadLots();
        // Opened from Lot Detayı "Seri Gezgini'nde tümü": the lot filter is APPLIED, through the same Apply a reader presses.
        if (contextLot) window.setTimeout(() => byId('btnFilterApply')?.click(), 0);
    };

    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        const box = byId('serials-list-error');
        if (box) { box.textContent = message || t('Error.ServiceUnavailable'); box.classList.remove('d-none'); }
    };

    const go = (url) => { window.location.href = url; };
    const rowActionHandlers = {
        SERIALLOT: ({ row }) => row?.lotId && go(ui.lotUrl(row.lotId)),
        SERIALMOVES: ({ row }) => row?.lotId && go(`/LotMovements?${new URLSearchParams({ lotId: row.lotId, serial: row.serialNumber })}`)
    };

    const renderAction = (full) => {
        const attrs = (text) => ({ 'data-id': full.id, 'data-json': JSON.stringify(full), title: text, 'aria-label': text });
        return window.DitenDataTable.renderActions([
            { key: 'SERIALLOT', visible: ui.can('SERIALLOT') && !!full.lotId, icon: 'bx bx-purchase-tag-alt', text: t('Action.SERIALLOT'), attrs: attrs(t('Action.SERIALLOT')) },
            { key: 'SERIALMOVES', visible: ui.can('SERIALMOVES') && !!full.lotId, icon: 'bx bx-history', text: t('Action.SERIALMOVES'), attrs: attrs(t('Action.SERIALMOVES')) }
        ]);
    };

    const initDataTable = async () => {
        if (!tableEl) return;
        list = await window.DitenDataTable.createList({
            tableEl: tableEl,
            dataMode: 'server',
            ajax: { url: '/Serials/api', type: 'GET', error: onListError },
            onResponse: () => byId('serials-list-error')?.classList.add('d-none'),
            actions: { onRowAction: rowActionHandlers },
            toolbar: { addNewText: '', colvisColumns: [2, 3, 4, 5, 6] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: initPickers },
            savedView: { moduleKey: 'Traceability', pageKey: 'Serials', saveViewColumnIndexes: [1, 2, 3, 4, 5, 6], defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6], baseOrder: [[1, 'asc']] },
            config: {
                ordering: false,
                searching: true,
                pageLength: 15,
                lengthMenu: [15, 30, 60],
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                    // Pack §11 "15 rows per page": the shared layout's length menu is fixed at 10/25/50/100 and would show
                    // an empty selector for 15 (E4) — this page's own menu, drawn in the selector the layout created.
                    const length = this.api().table().container().querySelector('.dt-length select');
                    if (length) {
                        length.innerHTML = [15, 30, 60].map((n) => `<option value="${n}">${n}</option>`).join('');
                        length.value = String(this.api().page.len());
                    }
                },
                columns: [
                    { data: 'id', name: 'control' }, { data: 'serialNumber', name: 'serialNumber' }, { data: 'product', name: 'product' },
                    { data: 'lotNumber', name: 'lotNumber' }, { data: 'status', name: 'status' }, { data: 'location', name: 'location' },
                    { data: 'lastMovementAt', name: 'lastMovementAt' }, { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading font-monospace">${esc(data || '')}</span>` },
                    { targets: 2, render: (data, type, full) => esc(data || full.itemId || '') },
                    { targets: 3, render: (data) => `<span class="font-monospace">${esc(data || '')}</span>` },
                    { targets: 4, render: (data) => ui.serialBadge(data) },
                    { targets: 5, render: (data, type, full) => esc([full.warehouse, full.location].filter(Boolean).join(' / ')) },
                    { targets: 6, render: (data) => esc(ui.dateTime(data)) },
                    { targets: -1, title: t('Col.Actions'), searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
                ]
            }
        });
    };

    return { init: initDataTable };
})();

document.addEventListener('DOMContentLoaded', () => SerialsList.init());
