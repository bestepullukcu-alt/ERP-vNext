'use strict';

/*
 * MOD-0022 E5W2 — İmza örneği onayları (pack §11.3, CT-D5; mockup v2 tab 8). Page: Views/ESignature/Settings/SampleApprovals.cshtml.
 *
 * THE LIST IS A COMPONENT (BL-440): createList, CLIENT mode on GET /ESignature/api/samples/pending (listPendingSamples).
 *
 *   Onayla → confirm → POST samples/{id}/approve           (esign.samples.approve)
 *   Reddet → red reasoned confirm (≥ 10) → POST samples/{id}/reject { reason }   (esign.samples.reject)
 *   own sample → no button, "Başka bir yönetici onaylar" — ownSample() decides; DISPLAY ONLY: the server refuses a
 *   self-decision with 403 ESIGN_SELF_APPROVAL_FORBIDDEN whatever this page draws (E3).
 * The decision log = GET samples/decisions (who decided, whose sample, approved / rejected, the reason).
 */
(function (global) {
    const E = global.ESign;
    const P = global.ESignPages;
    const t = E.t;
    const esc = E.escapeHtml;
    const $ = (id) => document.getElementById(id);
    const tableEl = document.querySelector('.datatables-esign-approvals');
    const me = String((global.CurrentUser && global.CurrentUser.id) || '').toLowerCase();
    let list = null;

    const filterFields = [{ id: 'filterKind', key: 'kind', kind: 'multi' }];

    /** The caller's own sample: never decided here (CT-D5 — another administrator decides). */
    const ownSample = (row) => !!me && String((row.owner && row.owner.userId) || '').toLowerCase() === me;

    const imageUrl = (row) => `${P.API}/samples/${encodeURIComponent(row.id)}/image`;
    const reload = () => { list?.dt?.ajax?.reload(null, false); loadLog(); };

    async function approve({ row }) {
        if (!row?.id || ownSample(row)) return;
        const ok = await P.confirm({
            title: t('ESign.Approvals.Approve.Title', (row.owner && row.owner.name) || ''),
            subtext: t('ESign.Approvals.Approve.Sub', row.sampleNumber || ''),
            confirmText: t('ESign.Approvals.Approve'),
            type: 'success'
        });
        if (!ok) return;
        const r = await E.call('POST', `${P.API}/samples/${encodeURIComponent(row.id)}/approve`);
        if (!r.ok) { P.failed(r); return; }
        P.toast(t('ESign.Approvals.Approved', row.sampleNumber || ''));
        reload();
    }

    async function reject({ row }) {
        if (!row?.id || ownSample(row)) return;
        const reason = await P.reason({
            title: t('ESign.Approvals.Reject.Title', (row.owner && row.owner.name) || ''),
            subtext: t('ESign.Approvals.Reject.Sub', row.sampleNumber || ''),
            confirmText: t('ESign.Approvals.Reject'),
            type: 'danger'
        });
        if (!reason) return;
        const r = await E.call('POST', `${P.API}/samples/${encodeURIComponent(row.id)}/reject`, { reason, idempotencyKey: E.newKey() });
        if (!r.ok) { P.failed(r); return; }
        P.toast(t('ESign.Approvals.Rejected', row.sampleNumber || ''));
        reload();
    }

    function zoom(row) {
        E.zoom({
            img: imageUrl(row), signer: (row.owner && row.owner.name) || '', login: (row.owner && row.owner.login) || '',
            type: t('ESign.Zoom.Sample', t(`ESign.Mine.Kind.${row.kind || 'drawn'}`)), at: P.at(row.createdAtUtc),
            storage: t(`ESign.Mine.Format.${row.format || 'png'}`), hash: row.imageHash ? `sha256:${row.imageHash}` : '—', sub: row.sampleNumber || row.id
        });
    }

    function renderDecision(row) {
        if (ownSample(row)) {
            return `<span class="small text-muted text-nowrap" data-esign-own-sample><i class="bx bx-lock-alt me-1"></i>${esc(t('ESign.Approvals.OtherAdmin'))}</span>`;
        }
        const attrs = (text) => ({ 'data-id': row.id, 'data-json': JSON.stringify(row), title: text, 'aria-label': text });
        return global.DitenDataTable.renderActions([
            { key: 'approve', visible: P.can('esign.samples.approve'), className: 'text-success', icon: 'bx bx-check', text: t('ESign.Approvals.Approve'), attrs: attrs(t('ESign.Approvals.Approve')) },
            { key: 'reject', visible: P.can('esign.samples.reject'), className: 'text-danger', icon: 'bx bx-x', text: t('ESign.Approvals.Reject'), attrs: attrs(t('ESign.Approvals.Reject')) }
        ]);
    }

    async function loadLog() {
        const r = await E.call('GET', `${P.API}/samples/decisions`);
        const items = r.ok ? (E.dataOf(r) || []) : [];
        $('esignApprovalsLog').innerHTML = items.length
            ? items.map((l) => `<div class="border-bottom pb-2 mb-2">
                <div class="d-flex flex-wrap gap-2 small"><span class="fw-medium">${esc(P.at(l.atUtc))}</span><span class="text-muted">${esc(l.by || '')}</span></div>
                <div class="d-flex flex-wrap gap-2 align-items-center mt-1"><span class="badge ${l.decision === 'approved' ? 'bg-label-success' : 'bg-label-danger'}">${esc(t(`ESign.Approvals.Decision.${l.decision}`))}</span>
                <span class="small">${esc(l.owner || '')}</span><span class="small font-monospace text-muted">${esc(l.sampleNumber || '')}</span></div>
                ${l.reason ? `<small class="d-block text-muted mt-1">${esc(t('ESign.Common.ReasonQuoted', l.reason))}</small>` : ''}</div>`).join('')
            : `<p class="text-muted small mb-0">${esc(t('ESign.Approvals.LogEmpty'))}</p>`;
    }

    async function init() {
        if (!tableEl) return;
        loadLog();
        list = await global.DitenDataTable.createList({
            tableEl,
            dataMode: 'client',
            ajax: { url: `${P.API}/samples/pending`, type: 'GET' },
            actions: { onRowAction: { approve, reject } },
            toolbar: { addNewText: '', colvisColumns: [2, 3, 4] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields },
            savedView: {
                moduleKey: 'ESignature',
                pageKey: 'SampleApprovals',
                saveViewColumnIndexes: [1, 2, 3, 4],
                defaultVisibleColumnIndexes: [1, 2, 3, 4],
                baseOrder: [[2, 'asc']]
            },
            config: {
                order: [[2, 'asc']],
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'owner', name: 'user' },
                    { data: 'createdAtUtc', name: 'addedAt' },
                    { data: 'id', name: 'image' },
                    { data: 'kind', name: 'how' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    {
                        targets: 1,
                        render: (d, type, row) => (type === 'display'
                            ? `<div class="fw-medium text-heading">${esc((d && d.name) || '')}</div><small class="text-muted">${esc((d && d.login) || '')} · <span class="font-monospace">${esc(row.sampleNumber || '')}</span></small>`
                            : `${(d && d.name) || ''} ${row.sampleNumber || ''}`)
                    },
                    { targets: 2, render: (d, type) => (type === 'display' ? `<small>${esc(P.at(d))}</small>` : d) },
                    {
                        targets: 3,
                        orderable: false,
                        searchable: false,
                        render: (d, type, row) => (type === 'display'
                            ? `<button type="button" class="border rounded bg-white p-1" data-esign-zoom-sample="${esc(row.id)}" title="${esc(t('ESign.Zoom.Enlarge'))}"><img alt="" src="${esc(imageUrl(row))}" style="max-height:36px;max-width:120px"></button>` : '')
                    },
                    { targets: 4, render: (d, type, row) => `<div>${esc(t(`ESign.Mine.Kind.${d || 'drawn'}`))}</div><small class="text-muted">${esc(t(`ESign.Mine.Format.${row.format || 'png'}`))}</small>` },
                    { targets: -1, title: t('ESign.Common.Actions'), searchable: false, orderable: false, className: 'cell-fit all', render: (d, type, row) => renderDecision(row) }
                ]
            }
        });
        tableEl.addEventListener('click', (e) => {
            const z = e.target.closest('[data-esign-zoom-sample]');
            if (!z) return;
            const row = list.dt.rows().data().toArray().find((x) => x.id === z.getAttribute('data-esign-zoom-sample'));
            if (row) zoom(row);
        });
    }

    init();
})(window);
