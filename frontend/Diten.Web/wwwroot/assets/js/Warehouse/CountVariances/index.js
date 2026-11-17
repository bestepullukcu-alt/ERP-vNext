/**
 * MOD-0181 DL-4 — Fark İzi (COUNTVARIANCES, Variance Trace) — DataTables index script.
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, filters and paging come from DitenDataTable.createList. This file writes the rows
 * ("Sayım" rows carry a count line, "Elle" rows a manual ledger movement — pack §2.4), the ledger link, the QuickView and the decision form.
 *
 * Decisions only on "Sayım" rows; "Elle" rows are read-only and show "kaynak beyanı uyuşmuyor" when the service flags it. The choices are gated by
 * the line's state, the reader's keys and four eyes: "Onayla" is withheld from the line's counter / the count's submitter (selfApproval) — the
 * service still answers 403 COUNT_SELF_APPROVAL_FORBIDDEN, the screen rule never replaces the server's. "manual" and "all" need a warehouse and a
 * window of ≤ 31 days — the service refuses otherwise and the list shows its message.
 */
'use strict';

const CountVariancesList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-countvariances');
    const root = '/Warehouse/CountVariances';
    const endpoint = '/Warehouse/CountVariances/api';
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
    const token = () => document.querySelector('#formDecision input[name="__RequestVerificationToken"]')?.value || '';
    const offcanvas = (id) => (byId(id) ? window.bootstrap?.Offcanvas.getOrCreateInstance(byId(id)) : null);
    const format = (template, ...values) => values.reduce((text, value, i) => text.replace(`{${i}}`, value ?? ''), template || '');
    const dateText = (value) => (value ? new Date(value).toLocaleString(document.documentElement.lang || undefined) : '-');
    const showAlert = (id, text) => { const box = byId(id); if (box) { box.textContent = text || ''; box.classList.toggle('d-none', !text); } };

    const post = async (url, body) => {
        const headers = Object.assign({}, getAuthHeaders(true), { RequestVerificationToken: token(), 'Content-Type': 'application/json' });
        const res = await fetch(url, { method: 'POST', credentials: 'same-origin', headers, body: JSON.stringify(body) });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const failure = (answer) => answer?.json?.error?.message || W('CycleCount.Error.Generic');
    const reload = () => list?.dt?.ajax?.reload(null, false);

    // ─── cells ───────────────────────────────────────────────────────────────
    const isCount = (row) => row.source === 'Count' && !!row.line;
    const statusTone = { NoVariance: 'success', AwaitingApproval: 'warning', Approved: 'primary', Rejected: 'secondary', Recounted: 'info', Counted: 'secondary', Open: 'secondary' };
    const adjustmentTone = { Pending: 'warning', Posted: 'success', Refused: 'danger', IntegrityError: 'danger' };

    const ledgerLink = (number) => (number
        ? `<a href="/StockMovements?search=${encodeURIComponent(number)}" class="font-monospace" title="${esc(W('OpenLedger'))}">${esc(number)}</a>` : '');

    const cell = {
        count: (row) => (isCount(row) ? `<span class="fw-medium font-monospace">${esc(row.countNumber)}</span>` : ledgerLink(row.movement?.transactionNumber)),
        source: (row) => `<span class="badge bg-label-${isCount(row) ? 'primary' : 'secondary'}">${esc(W(isCount(row) ? 'CycleCount.Source.Count' : 'CycleCount.Source.Manual'))}</span>`,
        // DL-4 FIX-02: names and lot NUMBERS, never ids — what the proxy could not name is said in words
        bin: (row) => esc((isCount(row) ? row.line.location : row.movement?.location) || W('NameUnknown')),
        product: (row) => esc((isCount(row) ? row.line.sku : row.movement?.sku) || W('NameUnknown')),
        lot: (row) => {
            const source = isCount(row) ? row.line : row.movement;
            return source?.lotId ? esc(source.lotNumber || W('LotUnknown')) : '-';
        },
        expected: (row) => esc(isCount(row) ? (row.line.expectedAtCount ?? '–') : '–'),
        counted: (row) => esc(isCount(row) ? (row.line.countedQuantity ?? '–') : '–'),
        variance: (row) => {
            const value = isCount(row) ? row.line.varianceQuantity : (row.movement ? `${row.movement.movementType === 'COUNT_LOSS' ? '-' : '+'}${row.movement.quantity}` : null);
            if (value === null || value === undefined) return '–';
            const tone = String(value).startsWith('-') ? 'text-danger' : (Number(value) === 0 ? 'text-muted' : 'text-success');
            return `<span class="${tone} fw-medium">${esc(value)}</span>`;
        },
        flags: (row) => {
            const flags = [];
            if (isCount(row)) {
                if (row.line.movementDuringCount) flags.push(W('Flag.MovementDuringCount'));
                if (row.line.snapshotDrift) flags.push(W('Flag.SnapshotDrift'));
                if (row.line.origin === 'Found') flags.push(W('Flag.Found'));
                if (row.line.origin === 'Recount') flags.push(W('Flag.Recount'));
            } else if (row.movement?.sourceMismatch) {
                flags.push(W('Flag.SourceMismatch'));
            }
            return flags.map((f) => `<span class="badge bg-label-warning me-1">${esc(f)}</span>`).join('') || '';
        },
        status: (row) => (isCount(row)
            ? `<span class="badge bg-label-${statusTone[row.line.status] || 'secondary'}">${esc(W(`CycleCount.LineStatus.${row.line.status}`))}</span>`
            : `<span class="badge bg-label-dark">${esc(W('ReadOnlyManual'))}</span>`),
        adjustment: (row) => {
            const adj = isCount(row) ? row.line.adjustment : null;
            if (!adj) return isCount(row) ? '–' : ledgerLink(row.movement?.transactionNumber);
            const badge = `<span class="badge bg-label-${adjustmentTone[adj.status] || 'secondary'}">${esc(W(`CycleCount.Adjustment.${adj.status}`))}</span>`;
            return `${badge}${adj.transactionNumber ? ` ${ledgerLink(adj.transactionNumber)}` : ''}`;
        }
    };

    // ─── which decisions this row allows (state + keys + four eyes) ──────────
    const choicesFor = (row) => {
        if (!isCount(row)) return [];
        const line = row.line;
        const adj = line.adjustment;
        const out = [];
        if (line.status === 'AwaitingApproval') {
            if (can('cycle-count.variances.approve') && !row.selfApproval) out.push('approve');
            if (can('cycle-count.variances.reject')) out.push('reject');
            if (can('cycle-count.variances.recount') && row.countEtag && row.countStatus === 'Submitted') out.push('recount');
        }
        if (line.status === 'Approved' && adj?.status === 'Refused' && can('cycle-count.variances.post')) out.push('retry');
        if (line.status === 'Approved' && adj?.status === 'IntegrityError' && can('cycle-count.variances.resolve')) out.push('accept-holder', 'reject-holder');
        return out;
    };

    // ─── QuickView ───────────────────────────────────────────────────────────
    let current = null;

    const field = (label, value) => `<div class="col-6"><small class="text-muted d-block">${esc(label)}</small><span>${value}</span></div>`;

    // a "Sayım" row is read again when its QuickView opens: the decision then works on the line's current state and ETag
    const fresh = async (row) => {
        if (!isCount(row)) return row;
        const res = await fetch(`${root}/variance/${encodeURIComponent(row.line.lineId)}`, { credentials: 'same-origin', headers: getAuthHeaders() });
        if (!res.ok) return row;
        try { return await res.json(); } catch (error) { return row; }
    };

    const populateDetailsOffcanvas = async (listed) => {
        const row = await fresh(listed);
        current = row;
        byId('offcanvasDetailsPreviewLabel').textContent = isCount(row) ? (row.countNumber || '-') : (row.movement?.transactionNumber || '-');
        byId('oc-subtitle').innerHTML = `${cell.source(row)} ${cell.status(row)}`;
        const parts = [
            field(W('ColBin'), cell.bin(row)), field(W('ColProduct'), cell.product(row)), field(W('ColLot'), cell.lot(row)),
            field(W('ColVariance'), cell.variance(row))
        ];
        if (isCount(row)) {
            const line = row.line;
            parts.push(field(W('ColExpectedAtCount'), cell.expected(row)), field(W('ColCounted'), cell.counted(row)),
                field(W('CountedAt'), esc(dateText(line.countedAt))), field(W('ColAdjustment'), cell.adjustment(row)));
            if (line.decision) {
                parts.push(field(W('FieldDecision'), esc(`${W(`CycleCount.Decision.${line.decision.kind}`)} · ${W(`CycleCount.Decision.Source.${line.decision.source}`)}`)),
                    field(W('DecidedAt'), esc(dateText(line.decision.at))));
                if (line.decision.reason) parts.push(`<div class="col-12"><small class="text-muted d-block">${esc(W('FieldReason'))}</small>${esc(line.decision.reason)}</div>`);
            }
            if (line.adjustment?.refusalCode) parts.push(`<div class="col-12"><small class="text-muted d-block">${esc(W('RefusalCode'))}</small><span class="font-monospace">${esc(line.adjustment.refusalCode)}</span></div>`);
            if (line.integrityResolution) parts.push(field(W('IntegrityResolution'), esc(`${W(`CycleCount.Integrity.${line.integrityResolution.kind}`)} · ${dateText(line.integrityResolution.at)}`)));
        } else {
            const m = row.movement || {};
            parts.push(field(W('MovementType'), esc(W(`MovementType.${m.movementType}`) || m.movementType)), field(W('SourceDeclared'), esc(`${m.sourceModule || ''} / ${m.sourceType || ''}`)),
                field(W('PostedAt'), esc(dateText(m.createdAt))), field(W('OpenLedger'), ledgerLink(m.transactionNumber)));
        }
        byId('oc-fields').innerHTML = parts.join('');
        const notes = [];
        if (!isCount(row)) notes.push(W('ManualReadOnlyNote'));
        if (!isCount(row) && row.movement?.sourceMismatch) notes.push(W('Flag.SourceMismatchNote'));
        if (isCount(row) && row.selfApproval && row.line.status === 'AwaitingApproval') notes.push(W('SelfApprovalNote'));
        showAlert('oc-note', notes.join(' '));
        byId('oc-btn-decide')?.classList.toggle('d-none', choicesFor(row).length === 0);
    };

    // ─── decision form ───────────────────────────────────────────────────────
    let decisionKey = null;
    const reasoned = new Set(['reject', 'recount', 'accept-holder', 'reject-holder']);

    const openDecision = (row) => {
        const choices = choicesFor(row);
        if (!choices.length) return;
        current = row;
        decisionKey = newKey();
        showAlert('formDecisionAlert', '');
        byId('decisionSummary').textContent = format(W('DecisionSummary'), row.countNumber, row.line.location || W('NameUnknown'), row.line.sku || W('NameUnknown'),
            row.line.varianceQuantity ?? '');
        document.querySelectorAll('#decisionChoices [data-decision]').forEach((el) => {
            const allowed = choices.includes(el.getAttribute('data-decision'));
            el.classList.toggle('d-none', !allowed);
            const input = el.querySelector('input');
            input.checked = false;
            input.disabled = !allowed;
        });
        byId('decisionNone')?.classList.toggle('d-none', choices.length > 0);
        byId('decisionReason').value = '';
        byId('decisionReasonBlock')?.classList.add('d-none');
        offcanvas('offcanvasDetailsPreview')?.hide();
        offcanvas('offcanvasCreateEdit')?.show();
    };

    const onChoice = () => {
        const choice = document.querySelector('#decisionChoices input[name="decision"]:checked')?.value;
        byId('decisionReasonBlock')?.classList.toggle('d-none', !reasoned.has(choice));
    };

    const submitDecision = async () => {
        if (!current) return;
        const choice = document.querySelector('#decisionChoices input[name="decision"]:checked')?.value;
        if (!choice) { showAlert('formDecisionAlert', W('ValidationDecision')); return; }
        const reason = byId('decisionReason').value.trim();
        if (reasoned.has(choice) && !reason) { showAlert('formDecisionAlert', W('CycleCount.Error.COUNT_REASON_REQUIRED')); return; }
        const lineId = encodeURIComponent(current.line.lineId);
        const base = { idempotencyKey: decisionKey, ifMatch: current.line.etag };
        const button = byId('btnSaveDecision');
        if (button) button.disabled = true;
        let answer;
        switch (choice) {
            case 'approve': answer = await post(`${root}/approve/${lineId}`, base); break;
            case 'reject': answer = await post(`${root}/reject/${lineId}`, Object.assign(base, { reason })); break;
            case 'retry': answer = await post(`${root}/retry/${lineId}`, base); break;
            case 'accept-holder': answer = await post(`${root}/resolve/${lineId}`, Object.assign(base, { decision: 'AcceptHolder', reason })); break;
            case 'reject-holder': answer = await post(`${root}/resolve/${lineId}`, Object.assign(base, { decision: 'Reject', reason })); break;
            case 'recount':
                answer = await post(`${root}/recount/${encodeURIComponent(current.line.countId)}`,
                    { idempotencyKey: decisionKey, ifMatch: current.countEtag, lineIds: [current.line.lineId], reason });
                break;
            default: answer = null;
        }
        if (button) button.disabled = false;
        if (!answer?.ok) {
            showAlert('formDecisionAlert', failure(answer));
            if (answer && (answer.status === 409 || answer.status === 412)) decisionKey = newKey();
            return;
        }
        offcanvas('offcanvasCreateEdit')?.hide();
        const created = choice === 'recount' && answer.json?.number ? ` ${format(W('RecountCreated'), answer.json.number)}` : '';
        window.showToast?.(`${answer.json?.message || ''}${created}`, 'success');
        reload();
    };

    // ─── filters ─────────────────────────────────────────────────────────────
    const filterFields = [
        { id: 'filterSource', key: 'source', kind: 'single' },
        { id: 'filterWarehouse', key: 'warehouseId', kind: 'text' },
        { id: 'filterFrom', key: 'from', kind: 'text' },
        { id: 'filterTo', key: 'to', kind: 'text' },
        { id: 'filterStatus', key: 'status', kind: 'multi' },
        { id: 'filterLot', key: 'lotId', kind: 'text' }
    ];

    const loadLookupOptions = async () => {
        const el = byId('filterWarehouse');
        if (!el || !window.jQuery?.fn?.select2) return;
        window.jQuery(el).select2({
            placeholder: window.jQuery(el).data('placeholder') || '', allowClear: true, width: 'element',
            dropdownParent: window.jQuery(document.body), dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm',
            ajax: { url: `${root}/warehouses`, dataType: 'json', delay: 250, processResults: (answer) => ({ results: (answer?.items || []).map((i) => ({ id: i.value, text: i.text })) }) }
        });
        const hint = () => byId('cv-window-hint')?.classList.toggle('text-warning', (byId('filterSource')?.value || 'count') !== 'count');
        window.jQuery(byId('filterSource')).on('change', hint);
        hint();
    };

    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        showAlert('cv-list-error', message || W('CycleCount.Error.ServiceUnavailable'));
    };

    const renderAction = (full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        return window.DitenDataTable.renderActions([
            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-json': rowJson, title: L().QuickView, 'aria-label': L().QuickView } },
            { key: 'decide', visible: choicesFor(full).length > 0, icon: 'bx bx-check-shield', text: W('CycleCount.Action.Decide'), attrs: { 'data-json': rowJson } }
        ]);
    };

    const initDataTable = async () => {
        if (!tableEl) return;
        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            export: { mode: 'server', url: '/Warehouse/CountVariances/export', fileName: 'count-variances' },
            ajax: { url: endpoint, type: 'GET', error: onListError },
            onResponse: () => byId('cv-list-error')?.classList.add('d-none'),
            actions: { onRowAction: { decide: ({ row }) => row && openDecision(row) } },
            toolbar: { addNewText: '', colvisColumns: [3, 4, 5, 6, 7, 8, 9, 10, 11] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Warehouse', pageKey: 'CountVariances', saveViewColumnIndexes: [3, 4, 5, 6, 7, 8, 9, 10, 11], defaultVisibleColumnIndexes: [3, 4, 5, 6, 7, 8, 9, 10, 11], baseOrder: [] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            config: {
                ordering: false,
                searching: false,
                columns: [
                    { data: null, name: 'control' },
                    { data: null, name: 'count' },
                    { data: null, name: 'source' },
                    { data: null, name: 'bin' },
                    { data: null, name: 'product' },
                    { data: null, name: 'lot' },
                    { data: null, name: 'expected' },
                    { data: null, name: 'counted' },
                    { data: null, name: 'variance' },
                    { data: null, name: 'flags' },
                    { data: null, name: 'status' },
                    { data: null, name: 'adjustment' },
                    { data: null, name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (d, t, full) => cell.count(full) },
                    { targets: 2, render: (d, t, full) => cell.source(full) },
                    { targets: 3, render: (d, t, full) => cell.bin(full) },
                    { targets: 4, render: (d, t, full) => cell.product(full) },
                    { targets: 5, render: (d, t, full) => cell.lot(full) },
                    { targets: 6, render: (d, t, full) => cell.expected(full) },
                    { targets: 7, render: (d, t, full) => cell.counted(full) },
                    { targets: 8, render: (d, t, full) => cell.variance(full) },
                    { targets: 9, render: (d, t, full) => cell.flags(full) },
                    { targets: 10, render: (d, t, full) => cell.status(full) },
                    { targets: 11, render: (d, t, full) => cell.adjustment(full) },
                    { targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all', render: (d, t, full) => renderAction(full) }
                ]
            }
        });
    };

    const bindEvents = () => {
        byId('oc-btn-decide')?.addEventListener('click', () => current && openDecision(current));
        document.querySelectorAll('#decisionChoices input[name="decision"]').forEach((el) => el.addEventListener('change', onChoice));
        byId('btnSaveDecision')?.addEventListener('click', submitDecision);
    };

    return { init: () => { initDataTable(); bindEvents(); } };
})();

document.addEventListener('DOMContentLoaded', () => CountVariancesList.init());
