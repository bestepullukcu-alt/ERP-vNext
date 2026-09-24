/**
 * Golden Reference Compact — DataTables Index Script
 * Large-field reference pattern: create/edit/details use full MVC pages.
 *
 * THE LIST IS A COMPONENT (BL-440 package 2): Save View, column state, filters, the inline bar and the
 * responsive-modal return come from DitenDataTable.createList. This file writes only what is Compact's own:
 * columns and renderers, its badge maps, its lookups and its endpoints. Before the factory: 685 lines.
 *
 * THE SERVER-MODE REFERENCE (BL-440 package 3, data_mode: server). Paging, search, sort and the filters below run on
 * the service: the factory sends start/length/search/orderBy/orderDir + each applied filter by its key, and reads
 * back { items, total, filteredTotal }. So the filter fields carry no `matches` — the browser never filters a row.
 * (Golden Slim is the client-mode reference: a bounded set, filtered in the browser.)
 */
'use strict';

const GoldenReferenceCompactList = (function () {
    let list = null;

    const dtTableEl = document.querySelector('.datatables-goldenreferencecompact');
    const apiUrl = window.API?.deven;
    const L = () => window.L10n || {};
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const byId = (id) => document.getElementById(id);

    const getStatusMap = () => ({
        true: { title: L().Active, class: 'bg-label-success' },
        false: { title: L().Passive, class: 'bg-label-secondary' }
    });
    const getReferenceTypeMap = () => ({
        'Standard': L().ReferenceTypeStandard || 'Standard',
        'Custom': L().ReferenceTypeCustom || 'Custom',
        'Pro': L().ReferenceTypePro || 'Pro'
    });

    const filterFields = [
        { id: 'filterStatus', key: 'status', kind: 'multi' },
        { id: 'filterReferenceType', key: 'referenceType', kind: 'multi' },
        { id: 'filterCategory', key: 'category', kind: 'multi' },
        { id: 'filterOwner', key: 'owner', kind: 'multi' },
        { id: 'filterPriority', key: 'priority', kind: 'single' }
    ];

    const loadLookupOptions = async ({ fillSelect }) => {
        const selects = ['filterReferenceType', 'filterCategory', 'filterOwner', 'filterPriority'].map(byId);
        if (selects.some((el) => !el)) return;
        const res = await fetch('/GoldenReferenceCompact/lookups', { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() });
        if (!res.ok) return;
        const data = await res.json();
        fillSelect(selects[0], data?.referenceTypes);
        fillSelect(selects[1], data?.categories);
        fillSelect(selects[2], data?.owners);
        fillSelect(selects[3], (data?.priorities || []).map((item) => ({
            value: item.value,
            text: item.text || `${L().LevelPrefix || ''} ${item.value}`.trim()
        })), { showAllText: L().ShowAll || '' });
    };

    const deleteRow = ({ row }) => {
        if (!row?.id) return;
        window.showConfirm?.(L().AreYouSure, async () => {
            try {
                const res = await fetch(`${apiUrl}/api/golden-reference-compact/${row.id}`, { method: 'DELETE', credentials: 'include', headers: getAuthHeaders() });
                if (!res.ok) throw new Error('Delete failed.');
                list.reload('RecordDeleted');
            } catch (error) {
                console.error(error);
                window.showToast?.(L().ErrorOccurred, 'error');
            }
        }, { entityName: row.name, type: 'danger', confirmButtonText: L().Delete });
    };

    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        bulkActionSelector: '[data-bulk-action]',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all',
        onBulkAction: {
            delete: ({ ids }) => {
                if (!ids.length) return;
                const confirmText = (L().BulkDeleteConfirm || '').replace('{0}', ids.length);
                window.showConfirm?.(confirmText, async () => {
                    try {
                        const res = await fetch(`${apiUrl}/api/golden-reference-compact/bulk`, { method: 'DELETE', credentials: 'include', headers: getAuthHeaders(true), body: JSON.stringify(ids) });
                        if (!res.ok) throw new Error('Bulk delete failed.');
                        list.reload('BulkDeleteSuccess', String(ids.length));
                    } catch (error) {
                        console.error(error);
                        window.showToast?.(L().ErrorOccurred, 'error');
                    }
                }, { entityName: String(ids.length), type: 'danger', confirmButtonText: L().Delete });
            }
        }
    };

    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = `/GoldenReferenceCompact/Details/${id}`; },
        edit: ({ id }) => { if (id) window.location.href = `/GoldenReferenceCompact/Edit/${id}`; },
        delete: deleteRow
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[GoldenReferenceCompact] window.API.deven is required.'); return; }

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            bulk: bulkOptions,
            ajax: { url: apiUrl + '/api/golden-reference-compact', type: 'GET', xhrFields: { withCredentials: true } },
            actions: { onRowAction: rowActionHandlers },
            toolbar: { addNewText: L().AddNew, addNewAttr: { href: '/GoldenReferenceCompact/Create' }, onAddNew: () => { window.location.href = '/GoldenReferenceCompact/Create'; }, exportColumns: [2, 3, 4, 5, 6, 7, 8, 9], colvisColumns: [2, 3, 4, 5, 6, 7, 8, 9] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'DevEnablement', pageKey: 'GoldenReferenceCompact', saveViewColumnIndexes: [2, 3, 4, 5, 6, 7, 8, 9], defaultVisibleColumnIndexes: [2, 3, 4, 5, 6, 7, 8, 9], baseOrder: [[2, 'asc']] },
            config: {
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'code', name: 'code' },
                    { data: 'name', name: 'name' },
                    { data: 'referenceType', name: 'referenceType' },
                    { data: 'category', name: 'category' },
                    { data: 'owner', name: 'owner' },
                    { data: 'version', name: 'version' },
                    { data: 'priority', name: 'priority' },
                    { data: 'isActive', name: 'isActive' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    { targets: 4, render: (data) => data ? `<span class="badge bg-label-info">${getReferenceTypeMap()[data] || data}</span>` : '' },
                    {
                        targets: 9,
                        render: (data, type) => type === 'display'
                            ? window.DitenDataTable.renderStatusBadge(data, getStatusMap())
                            : (getStatusMap()[String(!!data)] || { title: L().Unknown }).title
                    },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, "&#39;");
                            return window.DitenDataTable.renderActions([
                                { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, 'title': L().QuickView } },
                                { key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L().Edit, attrs: { 'data-id': full.id, 'data-json': rowJson } },
                                { key: 'delete', className: 'text-danger', icon: 'bx bx-trash', text: L().Delete, attrs: { 'data-json': rowJson } }
                            ]);
                        }
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

document.addEventListener('DOMContentLoaded', () => GoldenReferenceCompactList.init());
