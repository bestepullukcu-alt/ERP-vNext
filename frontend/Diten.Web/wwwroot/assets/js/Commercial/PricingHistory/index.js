/**
 * WP-VW-W6 · W6-WEB-b — screen D · the change history (AC-HIST-1): who · when · what · each field's old → new, read only,
 * on the Golden Compact list standard (server mode, newest first by the CRM). The kinds the caller's keys do not cover are
 * not in the CRM's answer at all.
 */
'use strict';

const PricingHistoryList = (function () {
    const dtTableEl = document.querySelector('.datatables-pricinghistory');
    const U = window.PreOrdersUi;
    const K = window.PricingCore;
    const PU = window.PricingUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const esc = (s) => U.esc(s);
    const endpoint = PU.api + '/history';

    // control 0 · when 1 · who 2 · what 3 · changes 4 · actions 5
    const DATA_COLUMNS = [1, 2, 3, 4];
    const filterFields = [
        { id: 'filterType', key: 'entityType', kind: 'single' },
        { id: 'pr-from', key: 'from', kind: 'text' },
        { id: 'pr-to', key: 'to', kind: 'text' }
    ];

    const loadLookupOptions = async ({ fillSelect }) => {
        fillSelect(byId('filterType'), K.HISTORY_TYPES.map((t) => ({ value: t, text: L()['Type_' + t.replace(/-/g, '_')] || t })), { showAllText: L().TypeAll });
    };

    function drawEmptyState() {
        const api = this && this.api ? this.api() : null;
        if (!api || api.rows({ page: 'current' }).count() > 0) return;
        const cell = dtTableEl.querySelector('tbody td.dt-empty');
        if (cell) { cell.innerHTML = '<div class="pr-empty"><i class="bx bx-history"></i><strong>' + esc(L().EmptyTitle) + '</strong><span class="text-muted">' + esc(L().EmptyText) + '</span></div>'; }
    }

    const populateQuickView = (row) => {
        if (!row) return;
        const c = PU.historyCells(row);
        byId('oc-subtitle').innerHTML = c.when;
        byId('oc-details-list').innerHTML = '<div class="px-4 py-3">' + c.what + '<div class="small text-muted mt-1">' + c.who + '</div><div class="mt-3">' + c.changes + '</div></div>';
    };

    const enhanceRange = () => ['pr-from', 'pr-to'].forEach((id) => {
        const n = byId(id);
        if (n && typeof n.flatpickr === 'function' && !n._flatpickr) n.flatpickr({ dateFormat: 'Y-m-d', altInput: true, altFormat: L().DateFormat || 'Y-m-d', allowInput: true });
    });

    const init = async () => {
        if (!dtTableEl || !U || !K || !PU || !window.DitenDataTable) return;
        enhanceRange();
        await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            ajax: { url: endpoint, type: 'GET' },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateQuickView, actionKey: 'quickView' },
            // MOD-0156 §14 has no export key for the history: no file entries (Copy stays)
            toolbar: { addNewText: '', exportColumns: DATA_COLUMNS, colvisColumns: DATA_COLUMNS, exportPermitted: false },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Commercial', pageKey: 'PricingHistory', saveViewColumnIndexes: DATA_COLUMNS, defaultVisibleColumnIndexes: DATA_COLUMNS, baseOrder: [] },
            config: {
                order: [],
                searching: false,
                drawCallback: drawEmptyState,
                columns: [{ data: 'id', name: 'control' }, { data: 'at', name: 'at' }, { data: 'byUserId', name: 'byUserId' }, { data: 'entityType', name: 'entityType' }, { data: 'changes', name: 'changes' }, { data: 'id', name: 'action' }],
                columnDefs: [
                    { targets: '_all', orderable: false },
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 'all', render: (d, type, full) => (type === 'display' ? '<span class="text-nowrap">' + PU.historyCells(full).when + '</span>' : (full.at || '')) },
                    { targets: 2, render: (d, type, full) => (type === 'display' ? PU.historyCells(full).who : (full.byUserId || '')) },
                    { targets: 3, render: (d, type, full) => (type === 'display' ? PU.historyCells(full).what : (full.entityType || '')) },
                    { targets: 4, render: (d, type, full) => (type === 'display' ? PU.historyCells(full).changes : K.historyRow(full).changes.map((f) => f.field + ': ' + f.old + ' → ' + f.new).join('; ')) },
                    {
                        targets: -1, title: L().Actions, searchable: false, className: 'cell-fit all',
                        render: (d, type, full) => window.DitenDataTable.renderActions([
                            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-id': full.id, title: L().QuickView, 'aria-label': L().QuickView } }
                        ])
                    }
                ]
            }
        });
    };

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', () => PricingHistoryList.init());
