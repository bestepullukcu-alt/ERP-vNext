/**
 * WP-VW-W4 · the potential thresholds — W3E-E4R2-FIX-c (U-5, K-STD): the CHANGE HISTORY, the page's second table.
 *
 * data_mode: server — the CRM pages the append-only history (newest first). The factory allows ONE createList per page
 * (the thresholds list is it), so this second table stands on the factory's own table builder (createCrudTable →
 * DtDefaults: the shared card, the standard toolbar — page length · the Action menu · column visibility —, the info line
 * and the paging) and carries the server mode with the factory's OWN wire pieces, never a copy of them:
 *   the request  — DitenDataTable.toServerQuery (start · length · draw; no search, no sort: the history has neither),
 *   the answer   — DitenDataTable.toDataTablesResponse ({ items, total, filteredTotal } → DataTables, the draw of the request),
 *   the export   — DitenDataTable.toExportQuery + the proxy's /history/export (CSV / Excel = EVERY change, BL-452),
 *   print / PDF  — the same export's CSV through DitenDataTable.parseCsv (the controlled copy holds every change).
 * Read only: no selection, no actions, no filter bar, no Save View. The thresholds list's save reloads it.
 *
 * window.PotentialThresholdsHistory.create({ tableEl, endpoint, cells, onNames(displayNames) }) → { reload(), table }
 */
'use strict';

window.PotentialThresholdsHistory = (function () {
    function create(options) {
        const o = options || {};
        const cells = o.cells || {};
        const D = window.DitenDataTable;
        // 0 = the responsive control · date 1 · person 2 · target 3 · field 4 · change 5
        const DATA_COLUMNS = [1, 2, 3, 4, 5];
        const EXPORT_KEYS = ['at', 'by', 'productId', 'field', 'newValue'];
        let lastDraw = 0;
        let dt = null;

        // jQuery has appended the query to the GET url when dataFilter runs: the answer keeps ITS request's draw
        const drawOf = (settings, fallback) => {
            const m = /[?&]draw=(\d+)/.exec(String((settings && settings.url) || ''));
            return m ? Number(m[1]) : fallback;
        };

        const exportUrl = (format) => {
            const keys = [];
            if (dt) {
                dt.columns(':visible').indexes().toArray().forEach((i) => {
                    const key = dt.column(i).dataSrc();
                    if (EXPORT_KEYS.indexOf(key) > -1 && keys.indexOf(key) === -1) { keys.push(key); }
                });
            }
            return o.endpoint + '/thresholds/history/export?' + D.toExportQuery({}, [], {}, format, keys);
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
            link.download = named ? decodeURIComponent(named[1]) : 'potential-threshold-history.' + (format === 'xlsx' ? 'xlsx' : 'csv');
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

        dt = D.createCrudTable({
            tableEl: o.tableEl,
            dataMode: 'server',
            ajax: {
                url: o.endpoint + '/thresholds/history',
                type: 'GET',
                data: (request) => { lastDraw = Number(request && request.draw) || lastDraw; return D.toServerQuery(request, [], {}); },
                dataSrc: 'data',
                dataFilter: function (raw) {
                    let json;
                    try { json = typeof raw === 'string' ? JSON.parse(raw) : raw; } catch (e) { return raw; }
                    if (typeof o.onNames === 'function') { o.onNames((json && json.data && json.data.displayNames) || {}); }
                    return JSON.stringify(D.toDataTablesResponse(json, drawOf(this, lastDraw)));
                }
            },
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
                // the empty / no-match line is the shared DataTables wording (dt-defaults' _DataTableL10n), as on every list
                columns: [
                    { data: 'at', name: 'control' },
                    { data: 'at', name: 'at' },
                    { data: 'by', name: 'by' },
                    { data: 'productId', name: 'productId' },
                    { data: 'field', name: 'field' },
                    { data: 'newValue', name: 'newValue' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 'pt-nowrap all', render: (d, type) => (type === 'display' ? cells.esc(cells.when(d)) : d) },
                    { targets: 2, responsivePriority: 4, render: (d, type) => (type === 'display' ? cells.person(d) : d) },
                    { targets: 3, responsivePriority: 3, render: (d, type) => (type === 'display' ? cells.target(d) : d) },
                    { targets: 4, responsivePriority: 2, render: (d, type) => cells.esc(cells.field(d)) },
                    { targets: 5, className: 'all', render: (d, type, full) => (type === 'display' ? cells.change(full.oldValue, full.newValue) : full.oldValue + ' → ' + full.newValue) }
                ]
            }
        });

        return {
            reload: () => { if (dt) { dt.ajax.reload(null, false); } },
            get table() { return dt; }
        };
    }

    return { create: create };
})();
