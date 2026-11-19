/**
 * MOD-0178 DL-6 — Outbound Sevkiyatlar (OUTBOUNDSHIPMENTS) — DataTables index script (Golden Reference Compact: create / edit / details are full
 * MVC pages).
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, filters, search, paging and export come from DitenDataTable.createList. This file
 * writes only the screen's own business: columns, the status badge and the §8.18 status menu (OutboundRules, rules.js).
 *
 * SERVER MODE, PROXY PROFILE (pack §11.5): '/Warehouse/OutboundShipments/api' translates the factory's wire for searchOutboundShipments; the
 * browser never calls the inventory service (5067), /api/warehouse or the Gateway.
 *
 * THE MENU (AC-34): the row's dropdown is exactly the status's transitions (Packed → "Sevke hazır", "İptal"); a key the reader lacks hides the
 * entry; an Item outbound's "Sevke hazır" is disabled with its reason (§8.24, AC-33 (vi)). "Paketle" opens the Packing page; the other entries open
 * the Details page on their dialog (#Release, #Cancel, #SyncDispatch) — one place for each write.
 */
'use strict';

const OutboundShipmentsList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-outboundshipments');
    const rules = window.OutboundRules;
    const L = () => window.L10n || {};
    const W = (key) => (L().Words || {})[key] || '';
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const can = (key) => window.Permissions?.has?.(key) === true;
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const endpoint = '/Warehouse/OutboundShipments/api';
    const fmtDate = (value) => {
        if (!value) return '';
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString(window.CurrentLanguage || undefined);
    };

    const filterFields = [
        { id: 'filterStatus', key: 'status', kind: 'multi' },
        { id: 'filterWarehouse', key: 'warehouseId', kind: 'text' }
    ];

    const pickerNotice = (available) => {
        const notice = byId('outbound-picker-unavailable');
        if (!notice) return;
        notice.textContent = available ? '' : W('Warehouse.Error.ServiceUnavailable');
        notice.classList.toggle('d-none', !!available);
    };

    const loadLookupOptions = async () => {
        const el = byId('filterWarehouse');
        if (!el || !window.jQuery?.fn?.select2) return;
        window.jQuery(el).select2({
            placeholder: window.jQuery(el).data('placeholder') || '',
            allowClear: true,
            dropdownParent: window.jQuery(document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            selectionCssClass: 'form-select form-select-sm',
            width: 'element',
            language: { searching: () => L().Searching || '', noResults: () => L().NoResults || '' },
            ajax: {
                url: '/Warehouse/OutboundShipments/warehouses',
                headers: getAuthHeaders(),
                dataType: 'json',
                delay: 250,
                processResults: (data) => {
                    pickerNotice(data?.available !== false);
                    return { results: (data?.items || []).map((item) => ({ id: item.value, text: item.text })) };
                },
                error: (xhr) => { if (xhr?.statusText !== 'abort') pickerNotice(false); }
            }
        });
    };

    const onResponse = () => byId('outbound-list-error')?.classList.add('d-none');

    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        const box = byId('outbound-list-error');
        if (box) { box.textContent = message || W('Warehouse.Error.ServiceUnavailable'); box.classList.remove('d-none'); }
    };

    const ActionIcon = { Pack: 'bx bx-package', Release: 'bx bx-send', SyncDispatch: 'bx bx-refresh', Cancel: 'bx bx-x-circle' };

    const menuEntry = (state, row) => {
        const word = W(rules.ActionWord[state.action]);
        if (state.enabled) {
            return {
                key: state.action, className: state.dangerous ? 'text-danger' : '', icon: ActionIcon[state.action], text: word,
                attrs: { 'data-url': rules.actionUrl(row, state.action), title: word, 'aria-label': word }
            };
        }
        const reason = W(rules.reasonWord(state.reasonCode));
        return {
            key: `disabled-${state.action}`, className: 'disabled text-muted', icon: ActionIcon[state.action], text: reason ? `${word} — ${reason}` : word,
            attrs: { 'aria-disabled': 'true', title: reason, 'data-disabled-reason': state.reasonCode || '' }
        };
    };

    const renderAction = (full) => window.DitenDataTable.renderActions([
        { key: 'quickView', className: 'js-details', icon: 'bx bx-show', attrs: { 'data-url': `/Warehouse/OutboundShipments/Details/${encodeURIComponent(full.id)}`, title: L().ViewDetails, 'aria-label': L().ViewDetails } },
        ...rules.menu(full, can).map((state) => menuEntry(state, full))
    ]);

    const go = ({ event, id }) => {
        const url = event?.target?.closest?.('[data-url]')?.getAttribute('data-url');
        if (url) window.location.href = url;
        else if (id) window.location.href = `/Warehouse/OutboundShipments/Details/${encodeURIComponent(id)}`;
    };

    const rowActionHandlers = { quickView: go, Pack: go, Release: go, SyncDispatch: go, Cancel: go };

    const initDataTable = async () => {
        if (!tableEl) return;
        const columns = [
            { data: 'id', name: 'control' },
            { data: 'number', name: 'number' },
            { data: 'status', name: 'status' },
            { data: 'warehouse', name: 'warehouse' },
            { data: 'orderRef', name: 'orderRef' },
            { data: 'shipTo', name: 'shipTo' },
            { data: 'lines', name: 'lines' },
            { data: 'createdAtUtc', name: 'created' },
            { data: 'id', name: 'action' }
        ];
        const visible = [1, 2, 3, 4, 5, 6, 7];
        const columnDefs = [
            { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
            { targets: 1, render: (data) => `<span class="fw-medium text-heading font-monospace">${esc(data)}</span>` },
            {
                targets: 2, render: (data, type, full) => {
                    const b = rules.statusBadge(data);
                    const item = full.itemLevel ? ` <span class="badge bg-label-info">${esc(W('ItemLevelBadge'))}</span>` : '';
                    return `<span class="badge ${b.cls}">${esc(W(b.word))}</span>${item}`;
                }
            },
            { targets: 3, render: (data) => esc(data) },
            { targets: 4, render: (data) => esc(data) },
            { targets: 5, render: (data) => esc(data) },
            { targets: 6, render: (data) => esc(data) },
            { targets: 7, render: (data) => `<small>${esc(fmtDate(data))}</small>` },
            { targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
        ];

        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: endpoint, type: 'GET', error: onListError },
            onResponse,
            export: { mode: 'server', url: '/Warehouse/OutboundShipments/export', fileName: 'outbound-shipments' },
            actions: { onRowAction: rowActionHandlers },
            // "Yeni outbound" only for a holder of warehouse.outbound-shipments.create (absent otherwise — UAS-001 §6).
            toolbar: {
                addNewText: can(rules.Keys.Create) ? (L().AddNew || '') : '', addNewAttr: { href: '/Warehouse/OutboundShipments/Create' },
                onAddNew: () => { window.location.href = '/Warehouse/OutboundShipments/Create'; }, colvisColumns: visible
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Warehouse', pageKey: 'OutboundShipments', saveViewColumnIndexes: visible, defaultVisibleColumnIndexes: visible, baseOrder: [] },
            config: { ordering: false, searching: true, columns, columnDefs }
        });
    };

    return { init: function () { initDataTable(); } };
})();

document.addEventListener('DOMContentLoaded', () => OutboundShipmentsList.init());
