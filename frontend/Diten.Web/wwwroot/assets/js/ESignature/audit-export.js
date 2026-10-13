'use strict';

/*
 * MOD-0022 E5W2 — Denetim dışa aktarımı (pack §11.3; mockup v2 tab 4). Page: Views/ESignature/AuditExport.cshtml.
 *
 * THE LIST IS A COMPONENT (BL-440): createList, SERVER mode on /ESignature/list/signatures — the proxy of searchSignatures
 * (rows outside the caller's company scope are excluded by the server) translating only the page wire. The filter is the
 * operation's: from / to (whole UTC days), module, signer, meaning, company.
 *
 * PDF / CSV: POST /ESignature/api/exports { format, filter, idempotencyKey } → the file (server PDF, CT-D11; CSV UTF-8 BOM ';')
 * through the proxy's binary branch → blob → download with the server's file name; the export number (X-Export-Number) is
 * told, and the export log (listExports — who, when, how many, the filter, the SHA-256 of the bytes sent) is reloaded.
 * Every export is written by the server whether or not the browser keeps the file (KS-6).
 */
(function (global) {
    const E = global.ESign;
    const P = global.ESignPages;
    const t = E.t;
    const esc = E.escapeHtml;
    const $ = (id) => document.getElementById(id);
    const tableEl = document.querySelector('.datatables-esign-export');
    let list = null;
    let companies = [];
    let meanings = [];
    let rows = [];
    const signers = new Map();

    const filterFields = [
        { id: 'filterFrom', key: 'fromUtc', kind: 'text' },
        { id: 'filterTo', key: 'toUtc', kind: 'text' },
        { id: 'filterModule', key: 'module', kind: 'single' },
        { id: 'filterSigner', key: 'signerUserId', kind: 'single' },
        { id: 'filterMeaning', key: 'meaningCode', kind: 'single' },
        { id: 'filterCompany', key: 'legalEntityId', kind: 'single' }
    ];

    const companyName = (id) => (id ? ((companies.find((c) => c.id === id) || {}).name || '') : '');
    const isoDay = (d) => d.toISOString().slice(0, 10);

    /** The export filter: the applied list filter; an open range defaults to the last 30 days (the operation needs both ends). */
    function exportFilter() {
        const f = (list && list.appliedFilters) || {};
        const to = f.toUtc || isoDay(new Date());
        const from = f.fromUtc || isoDay(new Date(Date.now() - 29 * 86400000));
        return {
            fromUtc: `${from}T00:00:00Z`,
            toUtc: `${to}T23:59:59.999Z`,
            module: f.module || null,
            signerUserId: f.signerUserId || null,
            meaningCode: f.meaningCode || null,
            legalEntityId: f.legalEntityId || null
        };
    }

    function filterText() {
        const f = exportFilter();
        const parts = [`${f.fromUtc.slice(0, 10)} – ${f.toUtc.slice(0, 10)} UTC`];
        parts.push(f.module ? P.moduleLabel(f.module) : t('ESign.Common.AllModules'));
        if (f.signerUserId) parts.push(signers.get(f.signerUserId) || f.signerUserId);
        if (f.meaningCode) parts.push(P.meaningName(meanings, f.meaningCode));
        parts.push(f.legalEntityId ? companyName(f.legalEntityId) : t('ESign.Export.AllCompanies'));
        return parts.join(' · ');
    }

    function onResponse(json) {
        const page = (json && json.data) || {};
        rows = page.items || [];
        $('esignExportCount').textContent = t('ESign.Export.Count', page.total || 0);
        let added = false;
        rows.forEach((row) => {
            const s = (row.signature && row.signature.signer) || {};
            if (s.userId && !signers.has(s.userId)) { signers.set(s.userId, s.login ? `${s.name || ''} (${s.login})` : (s.name || s.userId)); added = true; }
        });
        if (added) {
            const select = $('filterSigner');
            const current = select.value;
            P.fill(select, [...signers.entries()].map(([value, text]) => ({ value, text })), t('ESign.Export.AllSigners'));
            select.value = current;
        }
        renderPdfPreview();
    }

    function renderPdfPreview() {
        $('esignExportPdfFilter').textContent = filterText();
        $('esignExportPdfCount').textContent = t('ESign.Export.Count', rows.length);
        $('esignExportPdfBlocks').innerHTML = rows.map((row) => {
            const s = row.signature || {};
            const a = s.appearance || {};
            const img = a.type === 'sample' && a.sampleId ? `${P.API}/samples/${encodeURIComponent(a.sampleId)}/image` : null;
            return `<div class="col-12 col-md-6"><div class="border rounded p-3 h-100">
                <div class="border-bottom pb-2 mb-2 d-grid" style="min-height:56px;place-items:center">${img ? `<img alt="" src="${esc(img)}" style="max-height:48px;max-width:100%">` : `<small class="text-muted">${esc(t(a.type === 'drawn' ? 'ESign.Verify.DrawnNoImage' : 'ESign.Export.NoVisual'))}</small>`}</div>
                <div class="fw-medium text-heading">${esc((s.signer && s.signer.name) || '')}</div>
                <small class="d-block">${esc(s.meaningText || s.meaningCode || '')} · ${esc(P.at(s.signedAtUtc))}</small>
                <small class="d-block text-muted font-monospace">${esc(s.signatureNumber || '')} · ${esc(s.subjectDisplay || '')}</small>
                <div class="mt-1">${P.verifyBadge(row.verificationStatus)} <small class="text-muted">${esc(t(`ESign.Dialog.AppType.${a.type || 'none'}`))}</small></div>
            </div></div>`;
        }).join('') || `<div class="col-12 text-center text-muted">${esc(t('ESign.Export.Empty'))}</div>`;
    }

    function setView(view) {
        document.querySelectorAll('[data-esign-view]').forEach((b) => b.classList.toggle('active', b.getAttribute('data-esign-view') === view));
        $('esignExportTableView').classList.toggle('d-none', view !== 'table');
        $('esignExportPdfView').classList.toggle('d-none', view !== 'pdf');
        if (view === 'pdf') renderPdfPreview();
    }

    const fileNameOf = (disposition) => {
        const m = /filename\*?=(?:UTF-8'')?"?([^";]+)/i.exec(disposition || '');
        if (!m) return null;
        try { return decodeURIComponent(m[1]); } catch (_) { return m[1]; }
    };

    async function exportAs(format) {
        const button = $(format === 'pdf' ? 'esignExportPdf' : 'esignExportCsv');
        button.disabled = true;
        try {
            let res;
            try {
                res = await fetch(`${P.API}/exports`, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                    body: JSON.stringify({ format, filter: exportFilter(), idempotencyKey: E.newKey() })
                });
            } catch (_) {
                P.failed({ ok: false, status: 0, json: null });
                return;
            }
            const type = res.headers.get('content-type') || '';
            if (!res.ok || type.includes('application/json')) {
                let json = null;
                try { json = await res.json(); } catch (_) { json = null; }
                P.failed({ ok: false, status: res.status, json });
                return;
            }
            const blob = await res.blob();
            const number = res.headers.get('X-Export-Number') || '';
            const name = fileNameOf(res.headers.get('content-disposition')) || `${number || 'esign-export'}.${format}`;
            const url = URL.createObjectURL(blob);
            try {
                const a = document.createElement('a');
                a.href = url;
                a.download = name;
                document.body.appendChild(a);
                a.click();
                a.remove();
            } finally {
                setTimeout(() => URL.revokeObjectURL(url), 1000);
            }
            P.toast(t('ESign.Export.Done', number, format.toUpperCase()));
            loadLog();
        } finally {
            button.disabled = false;
        }
    }

    async function loadLog() {
        const body = $('esignExportLog').querySelector('tbody');
        const r = await E.call('GET', `${P.API}/exports?page=1&pageSize=20`);
        const items = r.ok ? ((E.dataOf(r) || {}).items || []) : [];
        body.innerHTML = items.length
            ? items.map((l) => `<tr>
                <td class="font-monospace small">${esc(l.exportNumber)}</td>
                <td class="small">${esc(P.at(l.atUtc))}</td>
                <td class="small">${esc((l.by && l.by.name) || '')}</td>
                <td><span class="badge ${l.format === 'pdf' ? 'bg-label-danger' : 'bg-label-success'}">${esc(String(l.format || '').toUpperCase())}</span></td>
                <td class="small">${esc(t('ESign.Export.Log.Records', l.recordCount))}</td>
                <td class="small text-muted">${esc(l.filterText || '')}</td>
                <td class="small font-monospace text-muted" title="${esc(l.fileHash || '')}">${esc(l.fileHash ? `sha256:${l.fileHash.slice(0, 12)}…` : '')}</td>
            </tr>`).join('')
            : `<tr><td colspan="7" class="text-center text-muted small">${esc(t('ESign.Export.Log.Empty'))}</td></tr>`;
    }

    async function init() {
        if (!tableEl) return;
        [companies, meanings] = await Promise.all([P.companies(), P.meanings()]);
        document.querySelectorAll('[data-esign-view]').forEach((b) => b.addEventListener('click', () => setView(b.getAttribute('data-esign-view'))));
        $('esignExportPdf').addEventListener('click', () => exportAs('pdf'));
        $('esignExportCsv').addEventListener('click', () => exportAs('csv'));
        loadLog();

        list = await global.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: '/ESignature/list/signatures', type: 'GET' },
            onResponse,
            toolbar: { addNewText: '', colvisColumns: [1, 2, 3, 4, 5, 6, 7] },
            filters: {
                hostId: 'inlineFilterHost',
                collapseId: 'inlineFilterCollapse',
                fields: filterFields,
                loadOptions: async () => {
                    P.fill($('filterMeaning'), meanings.map((m) => ({ value: m.code, text: P.meaningName(meanings, m.code) })), t('ESign.Export.AllMeanings'));
                    P.fill($('filterCompany'), companies.map((c) => ({ value: c.id, text: c.name })), t('ESign.Export.AllCompanies'));
                }
            },
            savedView: {
                moduleKey: 'ESignature',
                pageKey: 'AuditExport',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7],
                baseOrder: []
            },
            config: {
                ordering: false,
                searching: false,
                order: [],
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* the export is the page's own */ }
                },
                columns: [
                    { data: 'signature.id', name: 'control' },
                    { data: 'signature.signedAtUtc', name: 'at' },
                    { data: 'signature.signatureNumber', name: 'number' },
                    { data: 'signature.signer', name: 'signer' },
                    { data: 'signature.meaningText', name: 'meaning', defaultContent: '' },
                    { data: 'signature.subjectDisplay', name: 'record', defaultContent: '' },
                    { data: 'signature.legalEntityId', name: 'company', defaultContent: '' },
                    { data: 'verificationStatus', name: 'verification' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (d) => `<small>${esc(P.at(d))}</small>` },
                    {
                        targets: 2,
                        render: (d, type, row) => `<a class="font-monospace small" href="/ESignature/Verification?signature=${encodeURIComponent(row.signature.id)}">${esc(d || '')}</a>`
                    },
                    { targets: 3, render: (d) => `<div class="fw-medium text-heading">${esc((d && d.name) || '')}</div><small class="text-muted">${esc((d && d.login) || '')}</small>` },
                    { targets: 4, render: (d, type, row) => esc(d || row.signature.meaningCode || '') },
                    {
                        targets: 5,
                        render: (d, type, row) => `<div>${esc(P.moduleLabel(row.signature.subject && row.signature.subject.module))}</div><small class="text-muted">${esc(d || '')}</small>`
                    },
                    { targets: 6, render: (d) => esc(companyName(d)) },
                    { targets: 7, render: (d) => P.verifyBadge(d) }
                ]
            }
        });
    }

    init();
})(window);
