/**
 * MOD-0173-FU02 L-2 Location list — DataTables index script (LOC2).
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, filters, search, sort and the inline bar come from
 * DitenDataTable.createList. This file writes only the list's own business: columns, renderers, the parent picker
 * and the row actions.
 *
 * SERVER MODE, PROXY PROFILE (pack §11): '/Locations/api' translates the factory's wire for LOCATION v1.1
 * searchLocations. Search and sort ARE offered (code, name, type, status — the operation supports them). The browser
 * never calls the inventory service, /api/locations or the Gateway.
 *
 * ROW ACTIONS (pack §11/§14, by permission — absent, not disabled, when not allowed):
 *   Edit · Add child · Activate / Deactivate / Archive  (inventory.locations.manage — the shared form.js)
 *   History    (inventory.locations.read — the L-1 detail of the node, where the history lives)
 *   View in stock list  (inventory.stock.read — FU01 S-1 filtered by the location, K-10)
 * No bulk selection, no import, no export (pack §11). No quantity anywhere (K-10).
 */
'use strict';

const LocationsList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-locations');
    const ui = window.LocationUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const esc = (value) => ui.esc(value);
    const canManage = () => ui.can('inventory.locations.manage') && !!byId('formLocation');
    const canSeeStock = () => ui.can('inventory.stock.read');

    const filterFields = [
        { id: 'filterType', key: 'type', kind: 'multi' },
        { id: 'filterStatus', key: 'status', kind: 'multi' },
        { id: 'filterStockable', key: 'isStockable', kind: 'single' },
        // searched on the server (the company's own locations), so a `text` field for the factory
        { id: 'filterParent', key: 'parentId', kind: 'text' }
    ];

    const pickerNotice = (available) => {
        const notice = byId('locations-picker-unavailable');
        if (!notice) return;
        notice.textContent = available ? '' : (L().ErrServiceUnavailable || '');
        notice.classList.toggle('d-none', !!available);
    };

    const initParentPicker = () => {
        const el = byId('filterParent');
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
                url: '/Locations/parents',
                dataType: 'json',
                delay: 250,
                traditional: true,
                // Any status: the list may show the children of an inactive or archived parent.
                data: (params) => ({ q: params.term || '', type: ['Site', 'Warehouse', 'StorageLocation', 'Zone'], activeOnly: false }),
                processResults: (data) => {
                    pickerNotice(data?.available !== false);
                    return { results: (data?.items || []).map((item) => ({ id: item.value, text: item.text })) };
                },
                error: (xhr) => { if (xhr?.statusText !== 'abort') pickerNotice(false); }
            }
        });
    };

    // L-1 hands over "too many children" as /Locations?parentId=…&parentLabel=… — the APPLIED parent filter.
    // Pressed through the same Apply button a reader would press, so the Save View dirty-state stays the factory's.
    const applyFiltersFromUrl = () => {
        const params = new URLSearchParams(window.location.search);
        const parentId = params.get('parentId');
        if (!parentId) return;
        window.setTimeout(() => {
            const select = byId('filterParent');
            if (select && !Array.from(select.options).some((o) => o.value === parentId)) {
                select.add(new Option(params.get('parentLabel') || parentId, parentId, true, true));
            }
            if (window.jQuery) $(select).val(parentId).trigger('change');
            byId('btnFilterApply')?.click();
        }, 0);
    };

    const loadLookupOptions = async () => {
        initParentPicker();
        applyFiltersFromUrl();
    };

    const onResponse = () => byId('locations-list-error')?.classList.add('d-none');

    // The list's own failure surface: the adapter's localized sentence, never a technical toast (UAS-001 §5/§6).
    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        const box = byId('locations-list-error');
        if (box) {
            box.textContent = message || L().ErrServiceUnavailable || '';
            box.classList.remove('d-none');
        }
        try {
            const api = window.jQuery(tableEl).DataTable();
            api.buttons('.dt-export-collection-btn').remove();
        } catch (error) { /* table not built yet */ }
    };

    const reload = () => list?.dt?.ajax?.reload(null, false);

    const rowActionHandlers = {
        edit: ({ row }) => row?.id && window.LocationForm?.open({ mode: 'edit', id: row.id, onSaved: reload }),
        addChild: ({ row }) => row?.id && window.LocationForm?.open({ mode: 'child', parent: { id: row.id, type: row.type, code: row.code, name: row.name }, onSaved: reload }),
        activate: ({ row }) => row?.id && window.LocationForm?.changeStatus(row, 'Active', { onDone: reload }),
        deactivate: ({ row }) => row?.id && window.LocationForm?.changeStatus(row, 'Inactive', { onDone: reload }),
        archive: ({ row }) => row?.id && window.LocationForm?.changeStatus(row, 'Archived', { onDone: reload }),
        locationHistory: ({ row }) => { if (row?.id) window.location.href = `/LocationTree?${new URLSearchParams({ locationId: row.id }).toString()}#history`; },
        viewStock: ({ row }) => { if (row?.id) window.location.href = ui.stockListUrl(row.id); }
    };

    const renderAction = (full) => {
        const manage = canManage();
        const archived = full.status === 'Archived';
        const attrs = (text) => ({ 'data-id': full.id, 'data-json': JSON.stringify(full), title: text, 'aria-label': text });
        return window.DitenDataTable.renderActions([
            // An archived location takes no change at all (K-04): no edit, no child, no status action.
            { key: 'edit', visible: manage && !archived, icon: 'bx bx-edit', text: L().ActionEdit, attrs: attrs(L().ActionEdit) },
            { key: 'addChild', visible: manage && !archived && full.type !== 'Bin', icon: 'bx bx-plus', text: L().ActionAddChild, attrs: attrs(L().ActionAddChild) },
            { key: 'activate', visible: manage && full.status === 'Inactive', icon: 'bx bx-play-circle', text: L().ActionActivate, attrs: attrs(L().ActionActivate) },
            { key: 'deactivate', visible: manage && full.status === 'Active', icon: 'bx bx-pause-circle', text: L().ActionDeactivate, attrs: attrs(L().ActionDeactivate) },
            { key: 'archive', visible: manage && !archived, className: 'text-danger', icon: 'bx bx-archive', text: L().ActionArchive, attrs: attrs(L().ActionArchive) },
            { key: 'locationHistory', icon: 'bx bx-history', text: L().ActionHistory, attrs: attrs(L().ActionHistory) },
            { key: 'viewStock', visible: canSeeStock(), icon: 'bx bx-package', text: L().ActionViewStock, attrs: attrs(L().ActionViewStock) }
        ]);
    };

    const initDataTable = async () => {
        if (!tableEl) return;

        list = await window.DitenDataTable.createList({
            tableEl: tableEl,
            dataMode: 'server',
            ajax: { url: '/Locations/api', type: 'GET', error: onListError },
            onResponse: onResponse,
            actions: { onRowAction: rowActionHandlers },
            // "New location" only for a user who may create (absent otherwise — UAS-001 §6).
            toolbar: {
                addNewText: canManage() ? (L().AddNewLocation || '') : '',
                onAddNew: () => window.LocationForm?.open({ mode: 'create', onSaved: reload }),
                colvisColumns: [2, 3, 4, 5, 6, 7]
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: {
                moduleKey: 'Inventory',
                pageKey: 'Locations',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7],
                baseOrder: [[1, 'asc']]
            },
            config: {
                ordering: true,
                searching: true,
                order: [[1, 'asc']],
                // No export until the export gate (pack §11): the factory's Action menu is removed for this list.
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'code', name: 'code' },
                    { data: 'name', name: 'name' },
                    { data: 'type', name: 'type' },
                    { data: 'parentPath', name: 'parentPath' },
                    { data: 'status', name: 'status' },
                    { data: 'isStockable', name: 'isStockable' },
                    { data: 'operatingMode', name: 'operatingMode' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading font-monospace">${esc(data || '')}</span>` },
                    { targets: 2, render: (data) => esc(data || '') },
                    { targets: 3, render: (data) => ui.typeBadge(data) },
                    { targets: 4, orderable: false, render: (data) => `<small class="text-muted">${esc(data || '')}</small>` },
                    { targets: 5, render: (data) => ui.statusBadge(data) },
                    {
                        targets: 6,
                        orderable: false,
                        // Only a Bin / StorageLocation can hold stock (LOC1-04); for the others the column is blank.
                        render: (data, type, full) => (full.type === 'Bin' || full.type === 'StorageLocation')
                            ? esc(data ? L().StockableYes : L().StockableNo)
                            : ''
                    },
                    { targets: 7, orderable: false, render: (data) => esc(ui.modeText(data)) },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        orderable: false,
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

document.addEventListener('DOMContentLoaded', () => LocationsList.init());
