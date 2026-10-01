/**
 * MOD-0280-FU01 T2b (U4) — Timesheet approvals: the approver's queue.
 *
 * Server-mode list (Golden Reference factory). A row OPENS the read-only week (/TimeEntry/Approvals/{weekId}), where
 * the week is approved or returned one by one. The only bulk act is APPROVE, and only for weeks that carry no mark
 * (11-hour day, timer cut at midnight, outside working hours, holiday): a marked week cannot even be ticked, the
 * selection is filtered again before it is sent, and the web tier re-reads the queue and refuses a marked week a
 * third time. Every decision travels the Task Center's own action path (work-items → MOD-0023). No edit exists (D6).
 */
'use strict';

const TimeApprovalsList = (function () {
    let list = null;

    const endpoint = '/TimeEntry/Approvals/api';
    const dtTableEl = document.querySelector('.datatables-timeapprovals');
    const L = () => window.L10n || {};
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const fmt = (text, ...values) => values.reduce((out, value, i) => out.split(`{${i}}`).join(String(value)), String(text || ''));

    const readJson = async (res) => {
        try { const text = await res.text(); return text ? JSON.parse(text) : null; } catch (error) { return null; }
    };

    const formatMinutes = (minutes) => {
        const total = Math.max(0, Number(minutes) || 0);
        return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
    };

    /** The marks a bulk approval never skips over. Exported for the tests: the page and the web tier agree on them. */
    const marksOf = (row) => {
        const marks = [];
        if ((row.flaggedDates || []).length) marks.push({ key: 'MarkFlagged', cls: 'bg-label-warning' });
        if ((row.autoClosedDates || []).length) marks.push({ key: 'MarkAutoClosed', cls: 'bg-label-warning' });
        if ((Number(row.outsideWorkingMinutes) || 0) > 0) marks.push({ key: 'MarkOutsideHours', cls: 'bg-label-info' });
        if ((row.holidayDates || []).length) marks.push({ key: 'MarkHoliday', cls: 'bg-label-info' });
        return marks;
    };
    const isMarked = (row) => marksOf(row).length > 0;

    const currentRows = () => list?.dt?.rows?.().data?.().toArray?.() || [];

    /** The selection as it may be sent: unmarked, still decidable weeks only (rows = the list's own rows). */
    const approvableIds = (ids, rows = currentRows()) => {
        const byId = new Map(rows.map((row) => [String(row.weekId), row]));
        return ids.map(String).filter((id) => {
            const row = byId.get(id);
            return row && row.approvalTaskId && !isMarked(row);
        });
    };

    const openWeek = (weekId) => {
        if (weekId) window.location.href = `/TimeEntry/Approvals/${encodeURIComponent(weekId)}`;
    };

    const bulkApprove = ({ ids, rows }) => {
        const sendable = approvableIds(ids || [], rows || currentRows());
        if (!sendable.length) {
            window.showToast?.(L().BulkNothingSelected, 'warning');
            return;
        }

        window.showConfirm?.(fmt(L().BulkApproveConfirm, sendable.length), async () => {
            try {
                const res = await fetch(`${endpoint}/bulk`, {
                    method: 'POST', credentials: 'same-origin', headers: getAuthHeaders(true),
                    body: JSON.stringify({ weekIds: sendable })
                });
                const json = await readJson(res);
                if (!res.ok) {
                    window.showToast?.(window.TimeEntryCore?.failureMessage({ status: res.status, reasonCode: json?.reason_code }, (k) => L()[k] || k) || L().ErrorOccurred, 'error');
                    return;
                }
                const result = json?.data || {};
                window.showToast?.(fmt(L().BulkApproveDone, (result.approved || []).length, (result.skipped || []).length),
                    (result.skipped || []).length ? 'warning' : 'success');
                list?.reload();
            } catch (error) {
                window.showToast?.(L().ErrorOccurred, 'error');
            }
        }, { type: 'primary', confirmButtonText: L().BulkApprove });
    };

    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        bulkActionSelector: '[data-bulk-action]',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all',
        onBulkAction: { approve: bulkApprove }
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            bulk: bulkOptions,
            ajax: { url: endpoint, type: 'GET', headers: getAuthHeaders(), xhrFields: { withCredentials: true } },
            actions: { onRowAction: { quickView: ({ row }) => openWeek(row?.weekId) } },
            toolbar: { addNewText: '', exportColumns: [], colvisColumns: [2, 3, 4, 5, 6] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: [] },
            savedView: { moduleKey: 'TimeEntry', pageKey: 'Approvals', saveViewColumnIndexes: [2, 3, 4, 5, 6], defaultVisibleColumnIndexes: [2, 3, 4, 5, 6], baseOrder: [[6, 'asc']] },
            config: {
                columns: [
                    { data: 'weekId', name: 'control' },
                    { data: 'weekId', name: 'checkbox' },
                    { data: 'displayName', name: 'displayName' },
                    { data: 'weekKey', name: 'weekKey' },
                    { data: 'totalMinutes', name: 'totalMinutes' },
                    { data: 'flaggedDates', name: 'marks' },
                    { data: 'submittedAtUtc', name: 'submittedAtUtc' },
                    { data: 'weekId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    {
                        targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit',
                        // A marked week cannot be ticked at all: it is decided one by one, on its own page.
                        render: (data, type, full) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${esc(data)}"${isMarked(full) || !full.approvalTaskId ? ' disabled data-marked="true"' : ''}>`
                    },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading">${esc(data || '—')}</span>` },
                    {
                        targets: 3,
                        render: (data, type, full) => `${esc(data)}${full.isCorrection ? ` <span class="badge bg-label-warning">${esc(fmt(L().CorrectionBadge, Math.max(1, (full.revisionNumber || 1) - 1)))}</span>` : ''}`
                    },
                    { targets: 4, render: (data, type) => type === 'display' ? esc(formatMinutes(data)) : data },
                    {
                        targets: 5, orderable: false,
                        render: (data, type, full) => {
                            const marks = marksOf(full);
                            if (type !== 'display') return marks.map((m) => L()[m.key]).join(', ');
                            return marks.length
                                ? marks.map((m) => `<span class="badge ${m.cls} me-1 time-entry-mark-badge">${esc(L()[m.key])}</span>`).join('')
                                : `<span class="text-muted">${esc(L().NoMarks)}</span>`;
                        }
                    },
                    {
                        targets: 6,
                        render: (data, type) => {
                            if (type !== 'display' || !data) return data || '';
                            try { return esc(new Intl.DateTimeFormat(document.documentElement.lang || 'en', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(data))); } catch (e) { return esc(data); }
                        }
                    },
                    {
                        targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all',
                        render: (data, type, full) => window.DitenDataTable.renderActions([
                            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', text: L().QuickView, attrs: { 'data-json': JSON.stringify(full).replace(/'/g, '&#39;'), 'title': L().QuickView } }
                        ])
                    }
                ],
                // The whole row opens the week — except the controls that live in it.
                initComplete: function () {
                    this.api().on('click', 'tbody tr', function (event) {
                        if (event.target.closest('input,a,button,.dropdown-menu,td.control,.dt-checkboxes-cell')) return;
                        const row = list?.dt?.row(this).data();
                        openWeek(row?.weekId);
                    });
                }
            }
        });
    };

    return {
        init: () => { initDataTable(); },
        // Seams for the tests (the page itself never calls these from outside).
        marksOf, isMarked, approvableIds, bulkApprove, list: () => list
    };
})();

window.TimeApprovalsList = TimeApprovalsList;
document.addEventListener('DOMContentLoaded', () => TimeApprovalsList.init());
