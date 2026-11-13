/**
 * WP-VW-W3E · the visit evidence settings — W3E-E4R2-FIX-b (K-STD): C4, the TIME-BOXED EXCEPTIONS on the Golden Compact
 * list standard (frontend-datatable-template.md). The standard wins over the mockup; the mockup ("W3 Yazim Ekranlari" C4)
 * gives the content: the columns (rep · rules · range · reason · people · status) and the chip words.
 *
 * data_mode: client — the CRM does not page the exceptions (a bounded set: GET api/exceptions comes back whole), so paging,
 * sorting and the search happen in the browser. The filter (status · phase — the former chips) is ALSO the proxy's query:
 * every read sends the applied status / phase as ?status= / ?phase= (the CRM's own filter), and the same values filter the
 * rows on the table (the factory's client hook), so a saved view is right from the first draw. An Apply / Reset re-reads.
 *
 * The page script (visit-evidence-settings.js) keeps the exception's quick view (the timeline + the Workflow actions) and
 * the "New exception" drawer; this file asks for them through window.VisitEvidenceSettingsPage and offers
 * window.VisitEvidenceExceptionsList.reload() for a change made there. Every word comes from window.L10n (7 languages);
 * every data name is isolated (bidi) for RTL. No selection, no bulk bar (HasSelection = false: every action is a
 * single-record Workflow step).
 */
'use strict';

(function () {
    const dtTableEl = document.querySelector('.datatables-visitevidenceexceptions');
    const root = document.getElementById('ve-root');
    const endpoint = '/CRM/VisitEvidenceSettings/api';
    const C = window.VisitEvidenceCore;
    const F = window.VisitPlanningFormat;
    const L = () => window.L10n || {};
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const byId = (id) => document.getElementById(id);
    const page = () => window.VisitEvidenceSettingsPage || {};
    const canManage = !!root && root.getAttribute('data-can-manage-exceptions') === 'true';
    const me = (root && root.getAttribute('data-me')) || '';

    // control 0 · rep 1 · rules 2 · range 3 · reason 4 · people 5 · status 6 · actions 7
    const DATA_COLUMNS = [1, 2, 3, 4, 5, 6];

    // ── tiny helpers ─────────────────────────────────────────────────────────────────────────────────────────
    const esc = (s) => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const T = (key, ...args) => fmt(L()[key] || '', ...args);
    const day = (iso) => (iso ? F.dayMonth(iso + 'T12:00:00Z') + ' ' + String(iso).slice(0, 4) : '—'); // "10 Eki 2026" (the mockup)
    const initials = (name) => String(name || '').split(/\s+/).filter(Boolean).map((w) => w[0]).join('').slice(0, 2).toUpperCase() || '·';

    // the names the reads carry (CRM displayNames + the reps' names), so "Oluşturan / Onaylayan" read as people
    const people = {};
    const repNames = {};
    const nameOf = (id) => C.personLabel(id, people, repNames, me, L());
    const repName = (x) => x.repDisplayName || x.resourceId || '';
    const statusText = (x) => L()[C.chipOf(x).key] || '';
    const rulesText = (x) => (x.rules || []).map((k) => L()['XRule_' + k] || k).join(', ');
    const rangeText = (x) => day(x.startDate) + ' – ' + day(x.endDate);
    const peopleText = (x) => T('XCreatedBy', nameOf(x.createdBy)) + ' · ' + T('XApprovedBy', x.approvedBy ? nameOf(x.approvedBy) : '—');

    // the cells (the former hand-written table's content, on the standard table)
    const chipHtml = (x) => { const c = C.chipOf(x); return '<span class="ve-pill ve-tone-' + c.tone + '">' + esc(L()[c.key] || '') + '</span>'; };
    const cells = {
        rep: (x) => '<div class="ve-row"><span class="ve-avatar ve-avatar-sm">' + esc(initials(x.repDisplayName)) + '</span><span class="ve-strong">' + F.bidi(repName(x)) + '</span></div>',
        rules: (x) => '<div class="ve-col ve-gap-xs ve-start">' + (x.rules || []).map((k) => '<span class="ve-pill ve-tone-warning">' + esc(L()['XRule_' + k] || k) + '</span>').join('') + '</div>',
        range: (x) => '<div class="ve-col"><span>' + esc(rangeText(x)) + '</span><span class="ve-sub">' + esc(T('XDays', F.isolate(x.days))) + '</span></div>',
        reason: (x) => '<span class="ve-reason">' + F.bidi(x.reason || '') + '</span>',
        people: (x) => '<div class="ve-col ve-sub"><span>' + esc(T('XCreatedBy', nameOf(x.createdBy))) + '</span><span>' + esc(T('XApprovedBy', x.approvedBy ? nameOf(x.approvedBy) : '—')) + '</span></div>'
    };

    // ── the filter: the former chips' two axes; the same values are the proxy's query ────────────────────────
    const filterFields = [
        { id: 'filterStatus', key: 'status', kind: 'single', matches: (row, value) => !value || row.status === value },
        { id: 'filterPhase', key: 'phase', kind: 'single', matches: (row, value) => !value || row.phase === value }
    ];
    let list = null;
    /** The read's query: the APPLIED status / phase (the CRM's own filter, GET api/exceptions?status&phase). */
    const listQuery = () => {
        const applied = (list && list.appliedFilters) || {};
        const query = {};
        if (applied.status) { query.status = applied.status; }
        if (applied.phase) { query.phase = applied.phase; }
        return query;
    };
    const dataSrc = (json) => {
        const d = (json && json.data) || {};
        if (C.mergeNames) { C.mergeNames(people, d.displayNames); }
        const items = Array.isArray(d.items) ? d.items : [];
        items.forEach((x) => { if (x.resourceId && x.repDisplayName) { repNames[x.resourceId] = x.repDisplayName; } });
        return items;
    };
    const reload = () => { try { list?.dt?.ajax?.reload(null, false); } catch (e) { /* not drawn yet: the first read is on its way */ } };

    // ── the empty state on the standard table ──────────────────────────────────────────────────────────────
    const emptyHtml = () => '<div class="ve-empty-state"><i class="bx bx-time-five"></i><span>' + esc(L().XEmpty) + '</span></div>';
    function drawEmptyState() {
        const api = this && this.api ? this.api() : null;
        if (!api || api.rows({ page: 'current' }).count() > 0) { return; }
        const cell = dtTableEl.querySelector('tbody td.dt-empty');
        if (cell) { cell.innerHTML = emptyHtml(); }
    }

    // ── the quick view: the page's exception detail (timeline + the Workflow actions) ───────────────────────
    const populateQuickView = (row) => {
        if (!row || !row.id || typeof page().openException !== 'function') { return; }
        page().openException(row.id, { row: row });
    };

    const initDataTable = async () => {
        if (!dtTableEl || !C || !F || !window.DitenDataTable) return;

        // an Apply / a Reset re-reads with the new query (the factory has applied the filters by then)
        ['btnFilterApply', 'btnFilterReset'].forEach((id) => byId(id)?.addEventListener('click', () => setTimeout(reload, 0)));
        // a table laid out while its tab was hidden is re-measured when the tab shows (columns + responsive)
        document.addEventListener('diten:tab-change', (event) => {
            if (!event.detail || event.detail.key !== 'exceptions') { return; }
            try { list?.dt?.columns?.adjust?.(); list?.dt?.responsive?.recalc?.(); } catch (e) { /* a table not drawn yet has nothing to measure */ }
        });

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'client',
            ajax: { url: endpoint + '/exceptions', type: 'GET', headers: getAuthHeaders(), data: listQuery, dataSrc: dataSrc },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateQuickView, actionKey: 'quickView' },
            // "New exception" — the list's standard add-new action, only with the manage key (the drawer + Workflow stay the page's)
            toolbar: {
                addNewText: canManage ? (L().NewException || '') : '',
                onAddNew: () => { if (typeof page().openNew === 'function') { page().openNew(); } },
                exportColumns: DATA_COLUMNS,
                colvisColumns: DATA_COLUMNS
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields },
            savedView: { moduleKey: 'CRM', pageKey: 'VisitEvidenceExceptions', saveViewColumnIndexes: DATA_COLUMNS, defaultVisibleColumnIndexes: DATA_COLUMNS, baseOrder: [[3, 'desc']] },
            config: {
                order: [[3, 'desc']],
                drawCallback: drawEmptyState,
                language: { emptyTable: esc(L().XEmpty), zeroRecords: esc(L().XEmpty) },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'repDisplayName', name: 'rep' },
                    { data: 'rules', name: 'rules' },
                    { data: 'startDate', name: 'range' },
                    { data: 'reason', name: 'reason' },
                    { data: 'createdBy', name: 'people' },
                    { data: 'status', name: 'status' },
                    { data: 'id', name: 'action' }
                ],
                // display = the chips / the avatar; every other use (sort, search, CSV / Excel) = the plain words in the reader's language
                columnDefs: [
                    { targets: 0, className: 'control', orderable: false, searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 'all', render: (d, type, x) => (type === 'display' ? cells.rep(x) : repName(x)) },
                    { targets: 2, orderable: false, responsivePriority: 4, render: (d, type, x) => (type === 'display' ? cells.rules(x) : rulesText(x)) },
                    { targets: 3, responsivePriority: 3, render: (d, type, x) => (type === 'display' ? cells.range(x) : type === 'sort' || type === 'type' ? (x.startDate || '') : rangeText(x)) },
                    { targets: 4, orderable: false, responsivePriority: 6, render: (d, type, x) => (type === 'display' ? cells.reason(x) : (x.reason || '')) },
                    { targets: 5, orderable: false, responsivePriority: 5, render: (d, type, x) => (type === 'display' ? cells.people(x) : peopleText(x)) },
                    { targets: 6, responsivePriority: 2, render: (d, type, x) => (type === 'display' ? chipHtml(x) : statusText(x)) },
                    {
                        targets: -1, title: L().Actions, orderable: false, searchable: false, className: 'cell-fit all',
                        render: (d, type, x) => window.DitenDataTable.renderActions([
                            { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': x.id, title: L().QuickView, 'aria-label': L().QuickView } }
                        ])
                    }
                ]
            }
        });
    };

    window.VisitEvidenceExceptionsList = { reload: reload, query: listQuery };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => { void initDataTable(); });
    } else {
        void initDataTable();
    }
})();
