/**
 * MOD-0174 P3 — Lot Gezgini (pack §11.3). THE LIST IS A COMPONENT (BL-440): DitenDataTable.createList, server mode,
 * proxy profile '/Lots/api' (TRACE listLots, includeStock=true). This file writes the columns, renderers, filters and
 * the row actions only. Every button is an ACTION CODE of the pack §14 table, drawn only when TraceUi.can(code).
 * No selection, no bulk, no import / export (pack §11.2).
 */
'use strict';

const LotsList = (function () {
    let list = null;
    const ui = window.TraceUi;
    const t = ui.t;
    const esc = ui.esc;
    const tableEl = document.querySelector('.datatables-lots');
    const byId = (id) => document.getElementById(id);

    const filterFields = [
        { id: 'filterItemId', key: 'itemId', kind: 'text' },
        { id: 'filterStatus', key: 'status', kind: 'single' },
        { id: 'filterExpiryBefore', key: 'expiryBefore', kind: 'text' },
        { id: 'filterWarn', key: 'warn', kind: 'single' }
    ];

    const initProductPicker = () => {
        const el = byId('filterItemId');
        if (!el || !window.jQuery?.fn?.select2) return;
        const $el = $(el);
        $el.select2({
            dropdownParent: $(document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            selectionCssClass: 'form-select form-select-sm',
            placeholder: $el.data('placeholder') || '',
            allowClear: true,
            width: 'element',
            language: { searching: () => t('Searching'), noResults: () => t('NoResults') },
            ajax: {
                url: '/Lots/lookups/products',
                dataType: 'json',
                delay: 250,
                data: (params) => ({ q: params.term || '' }),
                processResults: (data) => ({ results: (data?.items || []).map((item) => ({ id: item.value, text: item.text })) })
            }
        });
    };

    const onResponse = (json) => {
        byId('lots-list-error')?.classList.add('d-none');
        byId('lots-labels-unavailable')?.classList.toggle('d-none', json?.labelsAvailable !== false);
    };

    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        const box = byId('lots-list-error');
        if (box) {
            box.textContent = message || t('Error.ServiceUnavailable');
            box.classList.remove('d-none');
        }
    };

    const reload = () => list?.dt?.ajax?.reload(null, false);

    const go = (url) => { window.location.href = url; };
    const rowActionHandlers = {
        VIEWLOT: ({ row }) => row?.id && go(ui.lotUrl(row.id)),
        EDITLOT: ({ row }) => row?.id && go(`/Lots/Edit?${new URLSearchParams({ lotId: row.id })}`),
        EDITRELEASEDLOT: ({ row }) => row?.id && go(`/Lots/Edit?${new URLSearchParams({ lotId: row.id })}`),
        CHANGELOTSTATUS: ({ row }) => row?.id && window.LotStatus?.open(row.id, { onDone: reload }),
        LOTMOVES: ({ row }) => row?.id && go(`/LotMovements?${new URLSearchParams({ lotId: row.id })}`),
        LOTTRACEOPEN: ({ row }) => row?.id && go(`/LotTrace?${new URLSearchParams({ lotId: row.id })}`)
    };

    const renderAction = (full) => {
        const attrs = (text) => ({ 'data-id': full.id, 'data-json': JSON.stringify(full), title: text, 'aria-label': text });
        // K-C: a Draft lot is edited with EDITLOT (trace.lots.update), every other status with EDITRELEASEDLOT.
        const edit = full.status === 'Draft' ? 'EDITLOT' : 'EDITRELEASEDLOT';
        return window.DitenDataTable.renderActions([
            { key: 'VIEWLOT', visible: ui.can('VIEWLOT'), icon: 'bx bx-show', text: t('Action.VIEWLOT'), attrs: attrs(t('Action.VIEWLOT')) },
            { key: edit, visible: ui.can(edit), icon: 'bx bx-edit', text: t('Action.' + edit), attrs: attrs(t('Action.' + edit)) },
            // Expired is the system's: no person-made transition leaves it (K-E) — the dialog would offer nothing.
            { key: 'CHANGELOTSTATUS', visible: ui.can('CHANGELOTSTATUS') && full.status !== 'Expired', icon: 'bx bx-transfer', text: t('Action.CHANGELOTSTATUS'), attrs: attrs(t('Action.CHANGELOTSTATUS')) },
            { key: 'LOTMOVES', visible: ui.can('LOTMOVES'), icon: 'bx bx-history', text: t('Action.LOTMOVES'), attrs: attrs(t('Action.LOTMOVES')) },
            { key: 'LOTTRACEOPEN', visible: ui.can('LOTTRACEOPEN'), icon: 'bx bx-git-branch', text: t('Action.LOTTRACEOPEN'), attrs: attrs(t('Action.LOTTRACEOPEN')) }
        ]);
    };

    const initDataTable = async () => {
        if (!tableEl) return;
        list = await window.DitenDataTable.createList({
            tableEl: tableEl,
            dataMode: 'server',
            ajax: { url: '/Lots/api', type: 'GET', error: onListError },
            onResponse: onResponse,
            actions: { onRowAction: rowActionHandlers },
            toolbar: {
                addNewText: ui.can('NEWLOT') ? t('Action.NEWLOT') : '',
                onAddNew: () => go('/Lots/Create'),
                colvisColumns: [2, 3, 4, 5, 6, 7, 8, 9, 10, 11]
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: async () => initProductPicker() },
            savedView: {
                moduleKey: 'Traceability',
                pageKey: 'Lots',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
                baseOrder: [[1, 'asc']]
            },
            config: {
                ordering: true,
                searching: true,
                pageLength: 10,
                order: [[1, 'asc']],
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                createdRow: (row, data) => {
                    // Row click = Lot Detayı (VIEWLOT) for a user who may see it.
                    if (!ui.can('VIEWLOT')) return;
                    row.style.cursor = 'pointer';
                    row.addEventListener('click', (event) => {
                        if (event.target.closest('a, button, .dropdown-menu, td.control')) return;
                        go(ui.lotUrl(data.id));
                    });
                },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'lotNumber', name: 'lotNumber' },
                    { data: 'product', name: 'product' },
                    { data: 'manufacturerLot', name: 'manufacturerLot' },
                    { data: 'supplierLot', name: 'supplierLot' },
                    { data: 'manufacturingDate', name: 'manufacturingDate' },
                    { data: 'expiryDate', name: 'expiryDate' },
                    { data: 'retestDate', name: 'retestDate' },
                    { data: 'country', name: 'country' },
                    { data: 'status', name: 'status' },
                    { data: 'coaReference', name: 'coaReference' },
                    { data: 'onHand', name: 'onHand' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading font-monospace">${esc(data || '')}</span>` },
                    { targets: 2, orderable: false, render: (data, type, full) => esc(data || full.itemId || '') },
                    { targets: [3, 4, 10], orderable: false, render: (data) => esc(data || '') },
                    { targets: 5, orderable: false, render: (data) => esc(ui.date(data)) },
                    { targets: 6, render: (data, type, full) => ui.expiryCell(data, full.expiryState) },
                    { targets: 7, orderable: false, render: (data, type, full) => ui.retestCell(data, full.retestDue) },
                    { targets: 8, orderable: false, render: (data) => esc(data || '') },
                    { targets: 9, orderable: false, render: (data) => ui.statusBadge(data) },
                    // K-A: the ledger's per-lot position (stockSummary), never summed here.
                    { targets: 11, orderable: false, className: 'text-end', render: (data, type, full) => data == null ? '' : `<span dir="ltr">${esc(ui.qty(data))} ${esc(full.baseUomId || '')}</span>` },
                    { targets: -1, title: t('Col.Actions'), searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
                ]
            }
        });
    };

    return { init: initDataTable };
})();

document.addEventListener('DOMContentLoaded', () => LotsList.init());
