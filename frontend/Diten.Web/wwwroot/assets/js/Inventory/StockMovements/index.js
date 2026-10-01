/**
 * MOD-0173-FU01 S-2 Movement history — DataTables index script (UI-1).
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, filters and the inline bar come from
 * DitenDataTable.createList. This file writes only the history's own business: columns, renderers, the filter
 * sources and the ONE state-changing action, Reversal.
 *
 * SERVER MODE, PROXY PROFILE (pack §8, ASSUMPTION-UI-01): '/StockMovements/api' translates paging for
 * INVENTORY-BUNDLE /transactions. No column is orderable, no search box (BG-04/BG-09). The browser never calls
 * the inventory service or the Gateway.
 *
 * REVERSAL (UI-08/UI-09, parent ASSUMPTION-0173-05): a NEW immutable REVERSAL movement, never an edit.
 *   - offered only to a user holding inventory.movements.create (display decision — the service decides),
 *   - only on a manual movement (sourceModule MOD-0173) that is not itself a reversal and not reversed on this page,
 *   - an automatic movement shows a passive button: it is reversed in its source module,
 *   - the confirmation stays open on a refusal and says why in the reader's language (ALREADY_REVERSED,
 *     INSUFFICIENT_STOCK, service unavailable…); the list reloads only on success,
 *   - one Idempotency-Key per opened confirmation: a double submit of the same dialog is a replay, never a second
 *     transaction; a NEW dialog on an already reversed row gets ALREADY_REVERSED from the service.
 */
'use strict';

const StockMovementsList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-stockmovements');
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const can = (key) => window.Permissions?.has?.(key) === true;
    const canReverse = () => can('inventory.movements.create');
    const esc = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const numberFormat = new Intl.NumberFormat(document.documentElement.lang || undefined, { maximumFractionDigits: 6 });
    const dateFormat = new Intl.DateTimeFormat(document.documentElement.lang || undefined, { dateStyle: 'short', timeStyle: 'short' });
    const formatQuantity = (value) => {
        if (value === null || value === undefined || value === '') return '';
        try { return numberFormat.format(String(value)); } catch (error) { return String(value); }
    };
    // UTC from the contract → the reader's time zone for display (pack §8).
    const formatDate = (value) => {
        if (!value) return '';
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? String(value) : dateFormat.format(date);
    };
    const signOf = { in: '+', out: '−', transfer: '⇄', reversal: '↺' };
    const movementText = (type) => (type && L()['Movement' + type]) || type || '';
    const statusText = (status) => (status && L()['Status' + status]) || status || '';
    const skuLevelText = (level) => (level && L()['SkuLevel' + level]) || level || '';

    const filterFields = [
        { id: 'filterItemId', key: 'itemId', kind: 'text' },
        { id: 'filterLotId', key: 'lotId', kind: 'text' },
        { id: 'filterWarehouseId', key: 'warehouseId', kind: 'single' },
        { id: 'filterLocationId', key: 'locationId', kind: 'single' },
        { id: 'filterFrom', key: 'from', kind: 'text' },
        { id: 'filterTo', key: 'to', kind: 'text' }
    ];

    const pickerNotice = (available) => {
        const notice = byId('stockmovements-picker-unavailable');
        if (!notice) return;
        notice.textContent = available ? '' : (L().PickerUnavailable || '');
        notice.classList.toggle('d-none', !!available);
    };

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
            language: { searching: () => L().Searching || '', noResults: () => L().NoResults || '' },
            ajax: {
                url: '/StockMovements/lookups/products',
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

    // UI-11: S-1 "History" opens this page as /StockMovements?itemId=…&lotId=… — those become the APPLIED filters.
    // Runs after the factory has initialised the filter bar (a task after loadOptions resolves), through the same
    // Apply button a reader would press, so the Save View dirty-state stays the factory's.
    const applyFiltersFromUrl = () => {
        const params = new URLSearchParams(window.location.search);
        const itemId = params.get('itemId');
        const lotId = params.get('lotId');
        if (!itemId && !lotId) return;
        window.setTimeout(() => {
            if (itemId) {
                const select = byId('filterItemId');
                if (select && !Array.from(select.options).some((o) => o.value === itemId)) select.add(new Option(itemId, itemId, true, true));
                $(select).val(itemId).trigger('change');
            }
            if (lotId && byId('filterLotId')) byId('filterLotId').value = lotId;
            byId('btnFilterApply')?.click();
        }, 0);
    };

    const loadLookupOptions = async ({ fillSelect }) => {
        initProductPicker();
        const warehouse = byId('filterWarehouseId');
        if (warehouse) {
            try {
                const res = await fetch('/StockMovements/lookups/warehouses', { credentials: 'same-origin' });
                const data = res.ok ? await res.json() : null;
                pickerNotice(!!data && data.available !== false);
                fillSelect(warehouse, data?.items || [], { showAllText: L().ShowAll || '' });
            } catch (error) {
                pickerNotice(false);
            }
            $(warehouse).on('change.stockmovements', () => loadLocations(warehouse.value, fillSelect));
        }
        applyFiltersFromUrl();
    };

    const onResponse = (json) => {
        byId('stockmovements-list-error')?.classList.add('d-none');
        byId('stockmovements-labels-unavailable')?.classList.toggle('d-none', json?.labelsAvailable !== false);
    };

    // The list's own failure surface: the adapter's localized sentence, never a technical toast (UAS-001 §5/§6).
    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        const box = byId('stockmovements-list-error');
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

    const newKey = () => (window.crypto?.randomUUID
        ? window.crypto.randomUUID()
        : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
            const r = window.crypto.getRandomValues(new Uint8Array(1))[0] % 16;
            return (c === 'x' ? r : ((r & 0x3) | 0x8)).toString(16);
        }));

    const antiforgeryToken = () =>
        document.querySelector('#stockmovements-antiforgery input[name="__RequestVerificationToken"]')?.value || '';

    const postReversal = async (row, idempotencyKey) => {
        try {
            const res = await fetch(`/StockMovements/reverse/${encodeURIComponent(row.id)}`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': antiforgeryToken() },
                body: JSON.stringify({
                    idempotencyKey: idempotencyKey,
                    itemId: row.itemId,
                    skuId: row.skuId,
                    skuLevel: row.skuLevel,
                    quantity: row.quantity,
                    uomId: row.uomId
                })
            });
            const json = await res.json().catch(() => null);
            return res.ok && json?.success
                ? { ok: true, message: json.message }
                : { ok: false, message: json?.error?.message || L().ErrServiceUnavailable || '' };
        } catch (error) {
            return { ok: false, message: L().ErrServiceUnavailable || '' };
        }
    };

    // The SHARED confirm (MOD-0013 window.showConfirm — one dialog implementation, product-wide). Its callback runs
    // after the dialog has closed, but a refused reversal must keep the dialog OPEN with the reason (pack §13), so
    // the confirm button is taken over in `didOpen`: post, and either close + reload, or stay with the sentence.
    const reverse = ({ row }) => {
        if (!row?.id || !row.canReverse || !canReverse()) return;
        const idempotencyKey = newKey();
        const sentence = String(L().ReversalConfirmText || '').replace('{0}', row.transactionNumber || row.id);
        let busy = false;

        window.showConfirm?.(L().ReversalConfirmTitle || '', null, {
            type: 'danger',
            subtext: esc(sentence),
            entityName: `${esc(movementText(row.movementType))} · ${esc(formatQuantity(row.quantity))} ${esc(row.uomId || '')}`,
            confirmButtonText: L().ReversalConfirmButton || '',
            cancelButtonText: L().Cancel || '',
            didOpen: (popup, swal) => {
                const confirm = popup.querySelector('.swal2-confirm');
                confirm?.addEventListener('click', async (event) => {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    // A double click (or a held Enter) submits once: the same dialog, the same key, one request.
                    if (busy) return;
                    busy = true;
                    swal.resetValidationMessage();
                    swal.showLoading(confirm);
                    const outcome = await postReversal(row, idempotencyKey);
                    if (outcome.ok) {
                        swal.close();
                        list.reload(outcome.message || L().RecordSaved);
                        return;
                    }
                    swal.hideLoading();
                    swal.showValidationMessage(esc(outcome.message));
                    busy = false;
                }, { capture: true });
            }
        });
    };

    const rowActionHandlers = { reversal: reverse };

    const renderAction = (full) => {
        if (!canReverse()) return '';
        if (full.isAutomatic) {
            // Passive, with the reason: an automatic movement is reversed in the module that posted it (UI-08).
            return `<span class="d-inline-block" tabindex="0" title="${esc(L().ReversedInSource)}" aria-label="${esc(L().ReversedInSource)}">`
                + `<a class="btn btn-icon disabled" aria-disabled="true"><i class="bx bx-undo icon-md"></i></a></span>`;
        }
        return window.DitenDataTable.renderActions([
            {
                key: 'reversal',
                visible: !!full.canReverse,
                className: 'text-danger',
                icon: 'bx bx-undo',
                text: L().Reversal,
                attrs: { 'data-id': full.id, 'data-json': JSON.stringify(full), title: L().Reversal, 'aria-label': L().Reversal }
            }
        ]);
    };

    const initDataTable = async () => {
        if (!tableEl) return;

        list = await window.DitenDataTable.createList({
            tableEl: tableEl,
            dataMode: 'server',
            ajax: { url: '/StockMovements/api', type: 'GET', error: onListError },
            onResponse: onResponse,
            actions: { onRowAction: rowActionHandlers },
            // No "New movement" in UI-1: the movement form arrives with UI-2 (no dead link, no disabled stand-in).
            toolbar: { addNewText: '', colvisColumns: [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: {
                moduleKey: 'Inventory',
                pageKey: 'StockMovements',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
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
                    { data: 'transactionNumber', name: 'transactionNumber' },
                    { data: 'movementType', name: 'movementType' },
                    { data: 'movementReason', name: 'reason' },
                    { data: 'productLabel', name: 'productSku' },
                    { data: 'quantity', name: 'quantity' },
                    { data: 'baseQuantity', name: 'baseQuantity' },
                    { data: 'toWarehouseId', name: 'to' },
                    { data: 'lotId', name: 'lot' },
                    { data: 'toStockStatus', name: 'statusChange' },
                    { data: 'sourceModule', name: 'source' },
                    { data: 'transactionDate', name: 'transactionDate' },
                    { data: 'postingDate', name: 'postingDate' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: '_all', orderable: false },
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 2, render: () => '' },
                    {
                        targets: 1,
                        render: (data, type, full) => `<span class="fw-medium text-heading">${esc(data || '')}</span>`
                            + (full.reversedOnPage ? ` <span class="badge bg-label-secondary ms-1">${esc(L().ReversedBadge)}</span>` : '')
                    },
                    {
                        targets: 2,
                        render: (data, type, full) => `<span>${esc(movementText(data))}</span>`
                            + (full.isAutomatic ? ` <span class="badge bg-label-info ms-1" title="${esc(L().ReversedInSource)}">${esc(L().Automatic)}</span>` : '')
                    },
                    { targets: 3, render: (data) => esc(data || '') },
                    {
                        targets: 4,
                        render: (data, type, full) => `<div class="d-flex flex-column"><span class="fw-medium text-heading">${esc(full.productLabel || full.itemId || '')}</span>`
                            + `<small class="text-muted">${esc(full.skuLabel || full.skuId || '')} · ${esc(skuLevelText(full.skuLevel))}</small></div>`
                    },
                    {
                        targets: 5,
                        className: 'text-end font-monospace',
                        render: (data, type, full) => `${esc(signOf[full.direction] || '')} ${esc(formatQuantity(data))} ${esc(full.uomId || '')}`.trim()
                    },
                    {
                        targets: 6,
                        className: 'text-end font-monospace',
                        render: (data, type, full) => `${esc(formatQuantity(data))} ${esc(full.baseUomId || '')}`.trim()
                    },
                    {
                        targets: 7,
                        render: (data, type, full) => `<div class="d-flex flex-column"><span>${esc(full.toWarehouseLabel || full.toWarehouseId || '')}</span>`
                            + `<small class="text-muted">${esc(full.toLocationLabel || full.toLocationId || '')}</small></div>`
                    },
                    { targets: 8, render: (data) => esc(data || '') },
                    {
                        targets: 9,
                        // "from → to" only when the movement names both and they differ (an outbound has no "to").
                        render: (data, type, full) => full.fromStockStatus && full.toStockStatus && full.fromStockStatus !== full.toStockStatus
                            ? `${esc(statusText(full.fromStockStatus))} → ${esc(statusText(full.toStockStatus))}`
                            : esc(statusText(full.toStockStatus || full.fromStockStatus))
                    },
                    {
                        targets: 10,
                        render: (data, type, full) => `<div class="d-flex flex-column"><span>${esc(full.sourceModule || '')}</span>`
                            + `<small class="text-muted">${esc([full.sourceDocumentId, full.sourceLineId].filter(Boolean).join(' / '))}</small></div>`
                    },
                    { targets: [11, 12], render: (data) => esc(formatDate(data)) },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => renderAction(full)
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

document.addEventListener('DOMContentLoaded', () => StockMovementsList.init());
