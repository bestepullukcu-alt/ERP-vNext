/**
 * MOD-0181 DL-5 — Kanıt Paketi (COUNTEVIDENCE, Evidence Pack) — DataTables index script.
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, the filter, paging and the server export come from DitenDataTable.createList. This file
 * writes the rows (one insert-only export each: count, format, the file's and the requested language, who / when, status, SHA-256, size), the QuickView
 * with its download link and the export form (a Submitted / Closed count + PDF | CSV). The file is drawn in THIS page's language — the proxy sends it
 * as Accept-Language (pack §4.5); a zh / ar PDF comes back in English with a note.
 */
'use strict';

const CountEvidenceExportsList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-countevidenceexports');
    const root = '/Warehouse/CountEvidenceExports';
    const endpoint = '/Warehouse/CountEvidenceExports/api';
    const L = () => window.L10n || {};
    const W = (key) => (L().Words || {})[key] || '';
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const can = (key) => window.Permissions?.has?.(key) === true;
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const newKey = () => (window.crypto?.randomUUID ? window.crypto.randomUUID() : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    }));
    const token = () => document.querySelector('#formExport input[name="__RequestVerificationToken"]')?.value || '';
    const offcanvas = (id) => (byId(id) ? window.bootstrap?.Offcanvas.getOrCreateInstance(byId(id)) : null);
    const dateText = (value) => (value ? new Date(value).toLocaleString(document.documentElement.lang || undefined) : '-');
    const showAlert = (id, text) => { const box = byId(id); if (box) { box.textContent = text || ''; box.classList.toggle('d-none', !text); } };
    const failure = (answer) => answer?.json?.error?.message || W('CycleCount.Error.Generic');
    const reload = () => list?.dt?.ajax?.reload(null, false);
    const fileUrl = (row) => `${root}/file/${encodeURIComponent(row.exportId)}`;
    const size = (bytes) => (bytes >= 1048576 ? `${(bytes / 1048576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

    // ─── cells ───────────────────────────────────────────────────────────────
    const language = (row) => {
        const drawn = W(`CountEvidence.Language.${row.language}`) || row.language;
        return row.requestedLanguage && row.requestedLanguage !== row.language
            ? `${esc(drawn)} <small class="text-muted d-block">${esc(W('RequestedLanguageShort'))}: ${esc(W(`CountEvidence.Language.${row.requestedLanguage}`) || row.requestedLanguage)}</small>`
            : esc(drawn);
    };

    const cell = {
        count: (row) => `<span class="fw-medium font-monospace">${esc(row.countNumber)}</span>`,
        format: (row) => `<span class="badge bg-label-${row.format === 'Pdf' ? 'danger' : 'success'}">${esc(W(`CountEvidence.Format.${row.format}`))}</span>`,
        by: (row) => esc(row.requestedByName || row.requestedBy || '-'),
        at: (row) => esc(dateText(row.requestedAt)),
        status: (row) => `<span class="badge bg-label-${row.status === 'Ready' ? 'success' : 'danger'}">${esc(W(`CountEvidence.Status.${row.status}`))}</span>`,
        sha: (row) => `<span class="font-monospace small" title="${esc(row.sha256)}">${esc((row.sha256 || '').slice(0, 12))}…</span>`,
        size: (row) => esc(size(row.sizeBytes || 0))
    };

    // ─── QuickView ───────────────────────────────────────────────────────────
    const field = (label, value) => `<div class="col-12"><small class="text-muted d-block">${esc(label)}</small><span class="text-break">${value}</span></div>`;

    const populateDetailsOffcanvas = (row) => {
        byId('offcanvasDetailsPreviewLabel').textContent = row.countNumber || '-';
        byId('oc-subtitle').innerHTML = `${cell.format(row)} ${cell.status(row)}`;
        byId('oc-fields').innerHTML = [
            field(W('ColLanguage'), language(row)), field(W('ColRequestedBy'), cell.by(row)), field(W('ColRequestedAt'), cell.at(row)),
            field(W('ColFileName'), esc(row.fileName)), field(W('ColSha256'), `<span class="font-monospace small">${esc(row.sha256)}</span>`),
            field(W('ColSize'), cell.size(row))
        ].join('');
        showAlert('oc-note', row.requestedLanguage && row.requestedLanguage !== row.language ? W('FallbackNote') : '');
        const download = byId('oc-btn-download');
        if (download) download.href = fileUrl(row);
    };

    // ─── export form ─────────────────────────────────────────────────────────
    let exportKey = null;

    const initCountPicker = () => {
        const el = byId('exportCount');
        if (!el || !window.jQuery?.fn?.select2 || window.jQuery(el).hasClass('select2-hidden-accessible')) return;
        window.jQuery(el).select2({
            dropdownParent: window.jQuery('#offcanvasCreateEdit'), width: '100%', allowClear: true, placeholder: window.jQuery(el).data('placeholder') || '',
            language: { searching: () => L().Searching || '', noResults: () => L().NoResults || '' },
            ajax: {
                url: `${root}/counts`, dataType: 'json', delay: 250, data: (params) => ({ q: params.term || '' }),
                processResults: (answer) => {
                    byId('exportCountUnavailable')?.classList.toggle('d-none', answer?.available !== false);
                    return { results: (answer?.items || []).map((i) => ({ id: i.value, text: `${i.text} · ${W(`CycleCount.Status.${i.status}`) || i.status}` })) };
                }
            }
        });
    };

    const openExport = () => {
        if (!can('cycle-count.evidence.export')) return;
        exportKey = newKey();
        showAlert('formExportAlert', '');
        offcanvas('offcanvasCreateEdit')?.show();
        window.setTimeout(() => {
            initCountPicker();
            window.jQuery?.('#exportCount').val(null).trigger('change');
            byId('exportFormatPdf').checked = true;
        }, 50);
    };

    const submitExport = async () => {
        showAlert('formExportAlert', '');
        const countId = byId('exportCount')?.value || '';
        const format = document.querySelector('input[name="exportFormat"]:checked')?.value || 'Pdf';
        if (!countId) { showAlert('formExportAlert', W('ValidationCount')); return; }
        const button = byId('btnSaveExport');
        if (button) button.disabled = true;
        let answer;
        try {
            const res = await fetch(`${root}/create`, {
                method: 'POST', credentials: 'same-origin',
                headers: Object.assign({}, getAuthHeaders(true), { RequestVerificationToken: token(), 'Content-Type': 'application/json' }),
                body: JSON.stringify({ idempotencyKey: exportKey, countId, format })
            });
            let json = null;
            try { json = await res.json(); } catch (error) { json = null; }
            answer = { ok: res.ok, status: res.status, json };
        } finally {
            if (button) button.disabled = false;
        }

        if (!answer.ok) {
            showAlert('formExportAlert', failure(answer));
            if (answer.status === 400 || answer.status === 409) exportKey = newKey();
            return;
        }

        offcanvas('offcanvasCreateEdit')?.hide();
        window.showToast?.(answer.json?.message || '', 'success');
        reload();
    };

    // ─── filter and list ─────────────────────────────────────────────────────
    const filterFields = [{ id: 'filterCount', key: 'countId', kind: 'single' }];

    const loadLookupOptions = async () => {
        const el = byId('filterCount');
        if (!el || !window.jQuery?.fn?.select2) return;
        // the count filter lists the counts this reader may open; without counts.read it stays empty (the export log is still listed)
        const res = await fetch(`${root}/counts`, { credentials: 'same-origin', headers: getAuthHeaders() }).catch(() => null);
        const answer = res && res.ok ? await res.json().catch(() => null) : null;
        (answer?.items || []).forEach((i) => el.appendChild(new Option(i.text, i.value)));
        window.jQuery(el).select2({
            placeholder: window.jQuery(el).data('placeholder') || '', allowClear: true, width: 'element',
            dropdownParent: window.jQuery(document.body), dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm'
        });
    };

    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        showAlert('ce-list-error', message || W('CycleCount.Error.ServiceUnavailable'));
    };

    const renderAction = (full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        return window.DitenDataTable.renderActions([
            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-json': rowJson, title: L().QuickView, 'aria-label': L().QuickView } },
            { key: 'download', visible: full.status === 'Ready', icon: 'bx bx-download', text: W('CountEvidence.Action.Download'), attrs: { 'data-json': rowJson } }
        ]);
    };

    const initDataTable = async () => {
        if (!tableEl) return;
        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            export: { mode: 'server', url: '/Warehouse/CountEvidenceExports/export', fileName: 'count-evidence-exports' },
            ajax: { url: endpoint, type: 'GET', error: onListError },
            onResponse: () => byId('ce-list-error')?.classList.add('d-none'),
            actions: { onRowAction: { download: ({ row }) => { if (row?.exportId) window.location.href = fileUrl(row); } } },
            toolbar: { addNewText: can('cycle-count.evidence.export') ? (L().AddNew || '') : '', onAddNew: openExport, colvisColumns: [2, 3, 4, 5, 6, 7, 8] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Warehouse', pageKey: 'CountEvidenceExports', saveViewColumnIndexes: [2, 3, 4, 5, 6, 7, 8], defaultVisibleColumnIndexes: [2, 3, 4, 5, 6, 7, 8], baseOrder: [] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            config: {
                ordering: false,
                searching: false,
                columns: [
                    { data: null, name: 'control' },
                    { data: 'countNumber', name: 'count' },
                    { data: 'format', name: 'format' },
                    { data: 'language', name: 'language' },
                    { data: 'requestedBy', name: 'requestedBy' },
                    { data: 'requestedAt', name: 'requestedAt' },
                    { data: 'status', name: 'status' },
                    { data: 'sha256', name: 'sha256' },
                    { data: 'sizeBytes', name: 'size' },
                    { data: null, name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (d, t, full) => cell.count(full) },
                    { targets: 2, render: (d, t, full) => cell.format(full) },
                    { targets: 3, render: (d, t, full) => language(full) },
                    { targets: 4, render: (d, t, full) => cell.by(full) },
                    { targets: 5, render: (d, t, full) => cell.at(full) },
                    { targets: 6, render: (d, t, full) => cell.status(full) },
                    { targets: 7, render: (d, t, full) => cell.sha(full) },
                    { targets: 8, render: (d, t, full) => cell.size(full) },
                    { targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all', render: (d, t, full) => renderAction(full) }
                ]
            }
        });
    };

    const bindEvents = () => {
        byId('btnSaveExport')?.addEventListener('click', submitExport);
    };

    return { init: () => { initDataTable(); bindEvents(); } };
})();

document.addEventListener('DOMContentLoaded', () => CountEvidenceExportsList.init());
