'use strict';

/*
 * MOD-0022 E5W2 — İmza talepleri (pack §11.3; mockup v2 tab 2). Page: Views/ESignature/Requests.cshtml.
 *
 * THE LIST IS A COMPONENT (BL-440): DitenDataTable.createList, SERVER mode. /ESignature/list/requests is the proxy of
 * listSignatureRequests (scope mine — the requests nominated to me) that only translates the page wire (start / length →
 * page / pageSize); the filters are the operation's own (module, status, request date from / to). The contract has no
 * sort and no free-text search, so the list offers neither (the server's order: request time, newest first).
 *
 * ROW ACTIONS (absent, not disabled, without the key — UAS-001 §6; the server checks again):
 *   Open the record's signatures → Verification (esign.signatures.read)
 *   Reddet  → red reasoned confirm (≥ 10) → POST requests/{id}/reject        (esign.requests.reject, waiting only)
 *   İmzala  → the shared dialog with the request's ids, action and meaning  (esign.signatures.sign, waiting only)
 */
(function (global) {
    const E = global.ESign;
    const P = global.ESignPages;
    const t = E.t;
    const esc = E.escapeHtml;
    const tableEl = document.querySelector('.datatables-esign-requests');
    let list = null;
    let meanings = [];

    const statusClass = { waiting: 'bg-label-warning', signed: 'bg-label-success', rejected: 'bg-label-danger', expired: 'bg-label-secondary', cancelled: 'bg-label-secondary' };

    const filterFields = [
        { id: 'filterModule', key: 'module', kind: 'single' },
        { id: 'filterStatus', key: 'status', kind: 'single' },
        { id: 'filterFrom', key: 'fromUtc', kind: 'text' },
        { id: 'filterTo', key: 'toUtc', kind: 'text' }
    ];

    const isToday = (utc) => {
        const d = new Date(utc);
        const n = new Date();
        return !Number.isNaN(d.getTime()) && d.getFullYear() === n.getFullYear() && d.getMonth() === n.getMonth() && d.getDate() === n.getDate();
    };

    function onResponse(json) {
        const page = (json && json.data) || {};
        const el = document.getElementById('esignRequestsCount');
        if (el) el.textContent = t('ESign.Requests.Count', page.total || 0, page.waitingCount || 0);
    }

    const reload = () => list?.dt?.ajax?.reload(null, false);

    async function reject({ row }) {
        if (!row?.id) return;
        const reason = await P.reason({
            title: t('ESign.Requests.Reject.Title'),
            subtext: t('ESign.Requests.Reject.Sub', row.requestNumber || '', row.subjectDisplay || ''),
            confirmText: t('ESign.Requests.Reject'),
            type: 'danger'
        });
        if (!reason) return;
        const r = await E.call('POST', `${P.API}/requests/${encodeURIComponent(row.id)}/reject`, { reason, idempotencyKey: E.newKey() });
        if (!r.ok) { P.failed(r); return; }
        P.toast(t('ESign.Requests.Rejected', row.requestNumber || ''));
        reload();
    }

    async function sign({ row }) {
        if (!row?.id || !row.subject) return;
        const signed = await E.open({
            module: row.subject.module,
            subjectType: row.subject.subjectType,
            subjectId: row.subject.subjectId,
            actionCode: row.actionCode || undefined,
            meaningCode: row.meaningCode || undefined,
            requestId: row.id
        });
        if (signed) reload();
    }

    function openRecord({ row }) {
        if (!row?.subject) return;
        const q = new URLSearchParams({ module: row.subject.module, type: row.subject.subjectType, id: row.subject.subjectId });
        global.location.href = `/ESignature/Verification?${q.toString()}`;
    }

    function renderActions(row) {
        const waiting = row.status === 'waiting';
        const attrs = (text) => ({ 'data-id': row.id, 'data-json': JSON.stringify(row), title: text, 'aria-label': text });
        return global.DitenDataTable.renderActions([
            { key: 'sign', visible: waiting && P.can('esign.signatures.sign'), className: 'text-primary', icon: 'bx bx-pen', text: t('ESign.Requests.Sign'), attrs: attrs(t('ESign.Requests.Sign')) },
            { key: 'reject', visible: waiting && P.can('esign.requests.reject'), className: 'text-danger', icon: 'bx bx-x-circle', text: t('ESign.Requests.Reject'), attrs: attrs(t('ESign.Requests.Reject')) },
            { key: 'openRecord', visible: P.can('esign.signatures.read'), icon: 'bx bx-link-external', text: t('ESign.Requests.OpenRecord'), attrs: attrs(t('ESign.Requests.OpenRecord')) }
        ]);
    }

    async function init() {
        if (!tableEl) return;
        meanings = await P.meanings();
        list = await global.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: '/ESignature/list/requests', type: 'GET' },
            onResponse,
            actions: { onRowAction: { sign, reject, openRecord } },
            toolbar: { addNewText: '', colvisColumns: [1, 2, 3, 4, 5, 6, 7] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields },
            savedView: {
                moduleKey: 'ESignature',
                pageKey: 'Requests',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7],
                baseOrder: []
            },
            config: {
                ordering: false,
                searching: false,
                order: [],
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'subject.module', name: 'module' },
                    { data: 'subjectDisplay', name: 'record' },
                    { data: 'meaningCode', name: 'meaning' },
                    { data: 'requestedBy.name', name: 'requestedBy', defaultContent: '' },
                    { data: 'requestedAtUtc', name: 'requestedAt' },
                    { data: 'dueAtUtc', name: 'due' },
                    { data: 'status', name: 'status' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (d) => `<span class="badge bg-label-secondary">${esc(P.moduleLabel(d))}</span>` },
                    {
                        targets: 2,
                        render: (d, type, row) => `<div class="fw-medium text-heading">${esc(d || '')}</div>`
                            + `<small class="text-muted font-monospace">${esc(row.requestNumber || '')} · ${esc(row.subject?.subjectId || '')}</small>`
                    },
                    { targets: 3, render: (d) => esc(P.meaningName(meanings, d)) },
                    { targets: 4, render: (d) => esc(d || '') },
                    { targets: 5, render: (d) => `<small>${esc(P.at(d))}</small>` },
                    {
                        targets: 6,
                        render: (d, type, row) => `<small>${esc(P.at(d))}</small>`
                            + (row.status === 'waiting' && isToday(d) ? `<div><span class="badge bg-label-danger mt-1"><i class="bx bx-time-five me-1"></i>${esc(t('ESign.Requests.Today'))}</span></div>` : '')
                    },
                    {
                        targets: 7,
                        render: (d, type, row) => `<span class="badge ${statusClass[d] || 'bg-label-secondary'}">${esc(t(`ESign.Requests.Status.${d}`))}</span>`
                            + (d === 'rejected' && row.rejectReason ? `<div class="small text-muted mt-1">${esc(t('ESign.Requests.RejectReason', row.rejectReason))}</div>` : '')
                    },
                    { targets: -1, title: t('ESign.Common.Actions'), searchable: false, orderable: false, className: 'cell-fit all', render: (d, type, row) => renderActions(row) }
                ]
            }
        });
    }

    init();
})(window);
