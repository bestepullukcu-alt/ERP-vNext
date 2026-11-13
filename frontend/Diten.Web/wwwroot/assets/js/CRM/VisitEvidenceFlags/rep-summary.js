/**
 * WP-VW-W3E · the visit evidence flags — W3E-FLAGS-STD (K-STD): M3, the REP SUMMARY, the page's second table.
 *
 * data_mode: client — the summary read's rep set is bounded (my team, or the tenant's reps with read-all) and comes back
 * whole: no page size is ever in the URL, paging / sorting happen in the browser. The table stands on the shared layer
 * (DitenDataTable.createCrudTable → DtDefaults) with the Golden Compact look (W3E-E4R2-FIX-a, U-6): the shared table card
 * (_ListShell), the standard toolbar (page length · the Action menu with CSV / Excel · column visibility), the info line
 * and the paging — the factory allows ONE createList per page, so this second table uses the factory's own table builder
 * and DtDefaults' own toolbar, never a copy of either. It carries no filter bar and no Save View of its own: it FOLLOWS the
 * visits list's applied filter state (index.js hands it the summary query), so the one Save View of the page keeps it.
 * The same read answers the summary card (onSummary) — one request for the card and the table.
 *
 * window.VisitEvidenceFlagsRepSummary.create({ tableEl, endpoint, onSummary(data) → reps, onRep(resourceId), cells }) →
 *   { sync(query) }  — a new query → one new read (the same query twice reads once).
 */
'use strict';

window.VisitEvidenceFlagsRepSummary = (function () {
    function create(options) {
        const o = options || {};
        const cells = o.cells || {};
        const L = () => window.L10n || {};
        let dt = null;
        let lastQuery = null;
        // rep 1 · started 2 · flagged 3 · rate 4 · top flag 5 (0 = the responsive control, 6 = the actions)
        const DATA_COLUMNS = [1, 2, 3, 4, 5];

        const build = (url) => window.DitenDataTable.createCrudTable({
            tableEl: o.tableEl,
            dataMode: 'client',
            ajax: { url: url, type: 'GET', dataSrc: (json) => o.onSummary((json && json.data) || {}) },
            actions: { onRowAction: { filterRep: ({ row }) => { if (row && row.resourceId) { o.onRep(row.resourceId); } } } },
            config: {
                stateSave: false,
                // no search box of its own: the one filter state is the visits list's (the rep filter included)
                searching: false,
                order: [[3, 'desc']],
                buttons: window.DtDefaults.exportButtons('', {}, {}, { exportColumns: DATA_COLUMNS, colvisColumns: DATA_COLUMNS }),
                language: { emptyTable: cells.esc(L().RepsEmpty), zeroRecords: cells.esc(L().RepsEmpty) },
                columns: [
                    { data: 'resourceId', name: 'control' },
                    { data: 'repName', name: 'repName' },
                    { data: 'started', name: 'started' },
                    { data: 'flagged', name: 'flagged' },
                    { data: 'resourceId', name: 'rate' },
                    { data: 'topFlag', name: 'topFlag' },
                    { data: 'resourceId', name: 'action' }
                ],
                columnDefs: [
                    // E2-1 — what stays on a narrow screen: the rep + the actions always ('all'), then the rate and the top flag
                    { targets: 0, className: 'control', orderable: false, searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 'all', render: (d, type, full) => (type === 'display' ? cells.rep(full.repName, full.resourceId) : (full.repName || full.resourceId)) },
                    { targets: 2, responsivePriority: 5, render: (d, type) => (type === 'display' ? cells.num(d) : d) },
                    { targets: 3, responsivePriority: 4, render: (d, type) => (type === 'display' ? cells.num(d) : d) },
                    { targets: 4, responsivePriority: 2, render: (d, type, full) => (type === 'display' ? cells.rate(full.started, full.flagged) : cells.rateValue(full.started, full.flagged)) },
                    { targets: 5, orderable: false, responsivePriority: 3, render: (d, type) => (d ? (type === 'display' ? cells.topFlag(d) : (L()['Flag_' + d] || d)) : '—') },
                    {
                        targets: -1, orderable: false, searchable: false, className: 'cell-fit all',
                        render: (d, type, full) => window.DitenDataTable.renderActions([
                            { key: 'filterRep', icon: 'bx bx-chevron-right vf-flip', attrs: { 'data-id': full.resourceId, title: L().TabVisits || '', 'aria-label': L().TabVisits || '' } }
                        ])
                    }
                ]
            }
        });

        // M3 — a click anywhere on a rep row filters the visits to that rep (the action button keeps its own handler)
        o.tableEl?.addEventListener('click', (e) => {
            if (e.target.closest('[data-row-action]')) { return; }
            const tr = e.target.closest('tbody tr');
            const row = tr && dt ? dt.row(tr).data() : null;
            if (row && row.resourceId) { o.onRep(row.resourceId); }
        });

        return {
            sync: function (query) {
                if (!o.tableEl || query == null || query === lastQuery) { return false; }
                lastQuery = query;
                const url = o.endpoint + '/summary' + query;
                if (!dt) { dt = build(url); } else { dt.ajax.url(url).load(); }
                return true;
            },
            get table() { return dt; }
        };
    }

    return { create: create };
})();
