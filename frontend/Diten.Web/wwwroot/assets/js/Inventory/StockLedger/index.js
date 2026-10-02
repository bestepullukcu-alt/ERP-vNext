/**
 * MOD-0173-FU01 S-1 Stock list — DataTables index script (UI-1, UI-2).
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, filters and the inline bar come from
 * DitenDataTable.createList. This file writes only what is the stock list's own: columns, renderers, the KPI strip
 * and its filters' sources. UI-2 adds two navigations for a user who may post: the toolbar "New stock entry"
 * (→ /StockLedger/Create, KD-2: an inbound movement, never a balance write) and the row "Movement" (→ S-3 with the
 * row as the source).
 *
 * SERVER MODE, PROXY PROFILE (pack §8, ASSUMPTION-UI-01): the factory sends start/length/draw + the applied filters
 * to the same-origin adapter '/StockLedger/api', which translates them for INVENTORY-BUNDLE /balance and answers
 * { data: { items, total, filteredTotal }, summary, labelsAvailable }. No column is orderable and there is no search
 * box: the contract has neither (BG-04, ASSUMPTION-UI-02). The browser never calls the inventory service or the Gateway.
 *
 * QUANTITIES ARE DECIMAL STRINGS (pack §8, UI-13): they are formatted with Intl.NumberFormat from the string and
 * never added, compared or parsed into a float here. The KPI numbers are summed server-side in decimal.
 */
'use strict';

const StockLedgerList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-stockledger');
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    // Display decision only (UAS-001 §6: a user who may not post sees no post action at all); MOD-0173 decides.
    const canCreate = () => window.Permissions?.has?.('inventory.movements.create') === true;
    const esc = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const numberFormat = new Intl.NumberFormat(document.documentElement.lang || undefined, { maximumFractionDigits: 6 });
    const formatQuantity = (value) => {
        if (value === null || value === undefined || value === '') return '';
        try { return numberFormat.format(String(value)); } catch (error) { return String(value); }
    };

    const statusClass = {
        AVAILABLE: 'bg-label-success',
        QUALITY_INSPECTION: 'bg-label-info',
        QUARANTINE: 'bg-label-warning',
        BLOCKED: 'bg-label-danger',
        IN_TRANSIT: 'bg-label-primary',
        DAMAGED: 'bg-label-danger',
        EXPIRED: 'bg-label-secondary'
    };
    const statusBadge = (status) => status
        ? `<span class="badge ${statusClass[status] || 'bg-label-secondary'}">${esc(L()['Status' + status] || status)}</span>`
        : '';
    const skuLevelText = (level) => (level && L()['SkuLevel' + level]) || level || '';

    // Product / SKU are free-text searched on the server (W-UI-01 Model A selectors behind ProductPickerSource),
    // so they are `text` fields for the factory (it must not re-initialise them as static Select2 chips).
    const filterFields = [
        { id: 'filterItemId', key: 'itemId', kind: 'text' },
        { id: 'filterSku', key: 'sku', kind: 'text' },
        { id: 'filterWarehouseId', key: 'warehouseId', kind: 'single' },
        { id: 'filterLocationId', key: 'locationId', kind: 'single' },
        { id: 'filterLotId', key: 'lotId', kind: 'text' },
        { id: 'filterStockStatus', key: 'stockStatus', kind: 'single' }
    ];

    const pickerNotice = (available) => {
        const notice = byId('stockledger-picker-unavailable');
        if (!notice) return;
        notice.textContent = available ? '' : (L().PickerUnavailable || '');
        notice.classList.toggle('d-none', !!available);
    };

    const initSearchPicker = (selectId, kind) => {
        const el = byId(selectId);
        if (!el || !window.jQuery?.fn?.select2) return;
        const $el = $(el);
        $el.select2({
            dropdownParent: $(document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            selectionCssClass: 'form-select form-select-sm',
            placeholder: $el.data('placeholder') || '',
            allowClear: true,
            width: 'element',
            language: { searching: () => L().Searching || '', noResults: () => L().NoResults || '' },
            ajax: {
                url: `/StockMovements/lookups/${kind}`,
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

    const loadLocations = async (warehouseId, fillSelect) => {
        const select = byId('filterLocationId');
        if (!select) return;
        if (!warehouseId) { fillSelect(select, [], { showAllText: L().ShowAll || '' }); return; }
        try {
            const res = await fetch(`/StockMovements/lookups/locations?warehouseId=${encodeURIComponent(warehouseId)}`, { credentials: 'same-origin' });
            const data = res.ok ? await res.json() : null;
            pickerNotice(!!data && data.available !== false);
            fillSelect(select, data?.items || [], { showAllText: L().ShowAll || '' });
        } catch (error) {
            pickerNotice(false);
        }
    };

    // MOD-0173-FU02 (K-10): "View in stock list" on a location opens /StockLedger?locationId=… — that location becomes
    // the APPLIED filter, through the same Apply button a reader would press (the S-2 itemId/lotId pattern), so the
    // Save View dirty-state stays the factory's. Runs after the factory has initialised the filter bar.
    const applyFiltersFromUrl = () => {
        const locationId = new URLSearchParams(window.location.search).get('locationId');
        if (!locationId) return;
        window.setTimeout(() => {
            const select = byId('filterLocationId');
            if (select && !Array.from(select.options).some((o) => o.value === locationId)) select.add(new Option(locationId, locationId, true, true));
            $(select).val(locationId).trigger('change');
            byId('btnFilterApply')?.click();
        }, 0);
    };

    const loadLookupOptions = async ({ fillSelect }) => {
        initSearchPicker('filterItemId', 'products');
        initSearchPicker('filterSku', 'skus');
        const warehouse = byId('filterWarehouseId');
        if (!warehouse) { applyFiltersFromUrl(); return; }
        try {
            const res = await fetch('/StockMovements/lookups/warehouses', { credentials: 'same-origin' });
            const data = res.ok ? await res.json() : null;
            pickerNotice(!!data && data.available !== false);
            fillSelect(warehouse, data?.items || [], { showAllText: L().ShowAll || '' });
        } catch (error) {
            pickerNotice(false);
        }
        $(warehouse).on('change.stockledger', () => loadLocations(warehouse.value, fillSelect));
        applyFiltersFromUrl();
    };

    const writeKpis = (json) => {
        const summary = json?.summary || null;
        document.querySelectorAll('#stockledger-kpis [data-kpi]').forEach((el) => {
            const value = summary ? summary[el.getAttribute('data-kpi')] : null;
            el.textContent = summary ? `${formatQuantity(value)} ${summary.baseUomId || ''}`.trim() : '—';
        });
        const hint = byId('stockledger-kpi-hint');
        if (hint) {
            hint.textContent = summary ? '' : (json?.summaryMessage || L().KpiSelectProduct || '');
            hint.classList.toggle('d-none', !!summary);
        }
    };

    const onResponse = (json) => {
        byId('stockledger-list-error')?.classList.add('d-none');
        byId('stockledger-labels-unavailable')?.classList.toggle('d-none', json?.labelsAvailable !== false);
        writeKpis(json);
    };

    // The list's own failure surface: a sentence in the reader's language (the adapter already localized it), never
    // a technical toast (status codes, English refusal text) — UAS-001 §5/§6.
    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        const box = byId('stockledger-list-error');
        if (box) {
            box.textContent = message || L().ErrServiceUnavailable || '';
            box.classList.remove('d-none');
        }
        // A failed FIRST load never reaches initComplete, where the export menu is removed: remove it here too.
        try {
            const api = window.jQuery(tableEl).DataTable();
            api.buttons('.dt-export-collection-btn').remove();
        } catch (error) { /* table not built yet */ }
    };

    const rowActionHandlers = {
        // UI-11: History = the movement history of this item (and lot) — S-2 filtered.
        history: ({ row }) => {
            if (!row?.itemId) return;
            const query = new URLSearchParams({ itemId: row.itemId });
            if (row.lotId) query.set('lotId', row.lotId);
            window.location.href = `/StockMovements?${query.toString()}`;
        },
        // UI-11 (UI-2): "Movement" opens S-3 as a location transfer with THIS row as the source (mockup); the reader
        // may change the type (count loss, scrap, warehouse transfer…) and the row stays the source.
        movement: ({ row }) => {
            if (!row?.itemId || !row.skuId || !canCreate()) return;
            const query = new URLSearchParams({
                type: 'LOCATION_TRANSFER',
                itemId: row.itemId,
                itemText: row.productLabel || row.itemId,
                sku: `${row.skuLevel}:${row.skuId}`,
                skuText: row.skuLabel || row.skuId,
                source: [row.warehouseId || '', row.locationId || '', row.stockStatus || '', row.lotId || ''].join('|')
            });
            window.location.href = `/StockMovements/Create?${query.toString()}`;
        }
    };

    const initDataTable = async () => {
        if (!tableEl) return;

        list = await window.DitenDataTable.createList({
            tableEl: tableEl,
            dataMode: 'server',
            ajax: { url: '/StockLedger/api', type: 'GET', error: onListError },
            onResponse: onResponse,
            actions: { onRowAction: rowActionHandlers },
            // KD-2 (UI-2): the list's "New" is the stock ENTRY page — an inbound movement, never a balance write — and
            // only for a user who may post (absent otherwise, UAS-001 §6).
            toolbar: {
                addNewText: canCreate() ? (L().NewStockEntry || '') : '',
                onAddNew: () => { window.location.href = '/StockLedger/Create'; },
                colvisColumns: [2, 3, 4, 6, 8, 9, 10, 11]
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: {
                moduleKey: 'Inventory',
                pageKey: 'StockLedger',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
                baseOrder: []
            },
            config: {
                ordering: false,
                searching: false,
                // No export until the export gate (pack §2/§20): the factory's Action menu is removed for this list.
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'productLabel', name: 'productSku' },
                    { data: 'warehouseId', name: 'warehouseLocation' },
                    { data: 'lotId', name: 'lot' },
                    { data: 'stockStatus', name: 'stockStatus' },
                    { data: 'onHand', name: 'onHand' },
                    { data: 'reserved', name: 'reserved' },
                    { data: 'available', name: 'available' },
                    { data: 'qualityInspection', name: 'qualityInspection' },
                    { data: 'blocked', name: 'blocked' },
                    { data: 'inTransit', name: 'inTransit' },
                    { data: 'baseUomId', name: 'unit' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: '_all', orderable: false },
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 2, render: () => '' },
                    {
                        targets: 1,
                        render: (data, type, full) => {
                            const product = full.productLabel || full.itemId;
                            const sku = full.skuLabel || full.skuId;
                            return `<div class="d-flex flex-column"><span class="fw-medium text-heading">${esc(product)}</span>`
                                + `<small class="text-muted">${esc(sku)} · ${esc(skuLevelText(full.skuLevel))}</small></div>`;
                        }
                    },
                    {
                        targets: 2,
                        render: (data, type, full) => `<div class="d-flex flex-column"><span>${esc(full.warehouseLabel || full.warehouseId || '')}</span>`
                            + `<small class="text-muted">${esc(full.locationLabel || full.locationId || '')}</small></div>`
                    },
                    // W-UI-02: until MOD-0174 registers lots, every lot is an "unregistered lot (pre-MOD-0174)".
                    { targets: 3, render: (data) => data ? `${esc(data)} <span class="badge bg-label-secondary ms-1" title="${esc(L().LotUnregisteredHint)}">${esc(L().LotUnregisteredBadge)}</span>` : '' },
                    { targets: 4, render: (data) => statusBadge(data) },
                    { targets: [5, 6, 7, 8, 9, 10], className: 'text-end font-monospace', render: (data) => esc(formatQuantity(data)) },
                    { targets: 11, render: (data) => esc(data || '') },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => window.DitenDataTable.renderActions([
                            { key: 'movement', visible: canCreate(), icon: 'bx bx-transfer-alt', text: L().RowMovement, attrs: { 'data-id': full.id, 'data-json': JSON.stringify(full), title: L().RowMovement, 'aria-label': L().RowMovement } },
                            { key: 'history', icon: 'bx bx-history', text: L().History, attrs: { 'data-id': full.id, 'data-json': JSON.stringify(full), title: L().History, 'aria-label': L().History } }
                        ])
                    }
                ]
            }
        });
    };

    return {
        init: function () {
            initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => StockLedgerList.init());
