/**
 * WP-VW-W3E · the visit evidence settings — W3E-E4R2-FIX-d (U-7, K-STD): the settings tab's CHANGE HISTORY and REP ROSTER,
 * the page's second and third tables.
 *
 * data_mode: server — the CRM pages both (the history newest first; the roster by name, then id). The factory allows ONE
 * createList per page (the exceptions list is it, index.js), so these tables stand on the factory's own table builder
 * (createCrudTable → DtDefaults: the shared card, the standard toolbar — page length · search · the Action menu · column
 * visibility —, the info line and the paging) and carry the server mode with the factory's OWN wire pieces (FIX-c's
 * method, FU-DT-MULTI-LIST), never a copy of them:
 *   the request  — DitenDataTable.toServerQuery (start · length · search · draw; no sort: both keep the CRM's order),
 *   the answer   — DitenDataTable.toDataTablesResponse ({ items, total, filteredTotal } → DataTables, the draw of the request),
 *   the export   — the history only: DitenDataTable.toExportQuery + the proxy's /policy/history/export (EVERY change,
 *                  BL-452); print / PDF read the same CSV through DitenDataTable.parseCsv.
 * The rep roster has no file export (its cells are the page's rule over the policy + the exception list: a file would need
 * a server twin of that rule — K13), only the column choice. Read only: no selection, no actions, no filter bar, no Save View.
 *
 * FU-DT-SKELETON-ID — every DtDefaults table removes the FIRST shaped placeholder of the document when it is ready; these
 * tables sit before the exceptions list, so each one removes ITS OWN placeholder when it is built (it is built when its
 * pane is first shown) and the shared rule is left with the exceptions list's own.
 *
 * window.VisitEvidenceSettingsTables.history({ tableEl, endpoint, cells, onNames }) → { reload(), table }
 * window.VisitEvidenceSettingsTables.reps({ tableEl, endpoint, cells })              → { reload(), redraw(), table }
 */
'use strict';

window.VisitEvidenceSettingsTables = (function () {
    // jQuery has appended the query to the GET url when dataFilter runs: the answer keeps ITS request's draw
    const drawOf = (settings, fallback) => {
        const m = /[?&]draw=(\d+)/.exec(String((settings && settings.url) || ''));
        return m ? Number(m[1]) : fallback;
    };

    /** FU-DT-SKELETON-ID — the table's own shaped placeholder goes when the table is built (never another list's). */
    const dropOwnSkeleton = (tableEl) => {
        const card = tableEl && tableEl.closest ? tableEl.closest('.card') : null;
        const own = card ? card.querySelector('[data-table-skeleton]') : null;
        if (own) { own.remove(); }
    };

    /** The server-mode ajax of a second table: the factory's request / answer pieces, the list contract of the proxy. */
    const serverAjax = (url, onJson) => {
        const D = window.DitenDataTable;
        let lastDraw = 0;
        return {
            url: url,
            type: 'GET',
            headers: D.getAuthHeaders(),
            data: (request) => { lastDraw = Number(request && request.draw) || lastDraw; return D.toServerQuery(request, [], {}); },
            dataSrc: 'data',
            dataFilter: function (raw) {
                let json;
                try { json = typeof raw === 'string' ? JSON.parse(raw) : raw; } catch (e) { return raw; }
                if (typeof onJson === 'function') { onJson(json); }
                return JSON.stringify(D.toDataTablesResponse(json, drawOf(this, lastDraw)));
            }
        };
    };

    function history(options) {
        const o = options || {};
        const cells = o.cells || {};
        const D = window.DitenDataTable;
        // 0 = the responsive control · date 1 · person 2 · field 3 · change 4
        const DATA_COLUMNS = [1, 2, 3, 4];
        const EXPORT_KEYS = ['at', 'by', 'field', 'newValue'];
        let dt = null;

        const exportUrl = (format) => {
            const keys = [];
            if (dt) {
                dt.columns(':visible').indexes().toArray().forEach((i) => {
                    const key = dt.column(i).dataSrc();
                    if (EXPORT_KEYS.indexOf(key) > -1 && keys.indexOf(key) === -1) { keys.push(key); }
                });
            }
            return o.endpoint + '/policy/history/export?' + D.toExportQuery({}, [], {}, format, keys);
        };

        const fetchExport = async (url) => {
            const headers = Object.assign({}, D.getAuthHeaders());
            if (window.CurrentLanguage) { headers['Accept-Language'] = window.CurrentLanguage; }
            const response = await fetch(url, { method: 'GET', credentials: 'include', headers: headers });
            if (response.status === 413) { window.showToast?.('ExportTooLarge', 'warning'); return null; }
            if (response.status === 403) { window.showToast?.('AccessDenied', 'error'); return null; }
            if (!response.ok) { window.showToast?.('ErrorOccurred', 'error'); return null; }
            return response;
        };

        const exportToServer = async (format) => {
            const response = await fetchExport(exportUrl(format));
            if (!response) { return; }
            const blob = await response.blob();
            const header = response.headers.get('Content-Disposition') || '';
            const named = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header);
            const link = document.createElement('a');
            link.href = URL.createObjectURL(blob);
            link.download = named ? decodeURIComponent(named[1]) : 'visit-evidence-policy-history.' + (format === 'xlsx' ? 'xlsx' : 'csv');
            link.classList.add('d-none');
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(link.href), 60000);
        };

        const controlledCopyRows = async () => {
            const response = await fetchExport(exportUrl('csv'));
            if (!response) { return null; }
            const rows = D.parseCsv(await response.text());
            return { header: rows[0] || [], body: rows.slice(1) };
        };

        dropOwnSkeleton(o.tableEl);
        dt = D.createCrudTable({
            tableEl: o.tableEl,
            dataMode: 'server',
            ajax: serverAjax(o.endpoint + '/policy/history', (json) => {
                if (typeof o.onNames === 'function') { o.onNames((json && json.data && json.data.displayNames) || {}); }
            }),
            config: {
                serverSide: true,
                processing: true,
                stateSave: false,
                // the history has no search and no sort of its own (newest first, the CRM's order)
                searching: false,
                ordering: false,
                order: [],
                buttons: window.DtDefaults.exportButtons('', {}, {}, {
                    exportColumns: DATA_COLUMNS,
                    colvisColumns: DATA_COLUMNS,
                    serverExport: (format) => exportToServer(format),
                    controlledCopy: { rows: () => controlledCopyRows(), meta: () => ({ filters: [], search: '' }) }
                }),
                columns: [
                    { data: 'at', name: 'control' },
                    { data: 'at', name: 'at' },
                    { data: 'by', name: 'by' },
                    { data: 'field', name: 'field' },
                    { data: 'newValue', name: 'newValue' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 've-nowrap all', render: (d, type) => (type === 'display' ? cells.esc(cells.when(d)) : d) },
                    { targets: 2, responsivePriority: 3, render: (d, type) => (type === 'display' ? cells.person(d) : d) },
                    { targets: 3, responsivePriority: 2, render: (d) => cells.esc(cells.field(d)) },
                    { targets: 4, className: 'all', render: (d, type, full) => (type === 'display' ? cells.change(full) : full.oldValue + ' → ' + full.newValue) }
                ]
            }
        });

        return {
            reload: () => { if (dt) { dt.ajax.reload(null, false); } },
            get table() { return dt; }
        };
    }

    function reps(options) {
        const o = options || {};
        const cells = o.cells || {};
        const D = window.DitenDataTable;
        // 0 = the responsive control · rep 1 · location 2 · photo 3 · QR 4 · device 5
        const DATA_COLUMNS = [1, 2, 3, 4, 5];
        const FIELDS = ['locationMode', 'photoMode', 'qrMode', 'antiFraudMode'];
        let dt = null;

        dropOwnSkeleton(o.tableEl);
        dt = D.createCrudTable({
            tableEl: o.tableEl,
            dataMode: 'server',
            ajax: serverAjax(o.endpoint + '/policy/reps/roster'),
            config: {
                serverSide: true,
                processing: true,
                stateSave: false,
                // the search box asks the CRM (q: the name or the resource id); the order is the CRM's (name, then id)
                searching: true,
                searchDelay: 300,
                ordering: false,
                order: [],
                // no file of its own (the cells are this page's rule) — the column choice only
                buttons: window.DtDefaults.exportButtons('', {}, {}, {
                    exportColumns: DATA_COLUMNS,
                    colvisColumns: DATA_COLUMNS,
                    exportPermitted: false
                }),
                columns: [{ data: 'resourceId', name: 'control' }, { data: 'displayName', name: 'displayName' }]
                    .concat(FIELDS.map((f) => ({ data: 'resourceId', name: f }))),
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 'all', render: (d, type, full) => (type === 'display' ? cells.rep(full) : d) }
                ].concat(FIELDS.map((f, i) => ({
                    targets: 2 + i,
                    className: cells.isNew && cells.isNew(f) ? 've-col-new' : '',
                    responsivePriority: 2 + i,
                    render: (d, type, full) => cells.value(full, f, type)
                })))
            }
        });

        return {
            reload: () => { if (dt) { dt.ajax.reload(null, false); } },
            // the policy / the exception list changed: the same page, its cells again (one CRM read, never per rep)
            redraw: () => { if (dt) { dt.ajax.reload(null, false); } },
            get table() { return dt; }
        };
    }

    return { history: history, reps: reps };
})();
