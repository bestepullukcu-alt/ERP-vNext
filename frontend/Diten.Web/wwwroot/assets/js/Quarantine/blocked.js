/**
 * MOD-0175 Q5 — Bloke Stok (pack §11.3). DitenDataTable.createList, SERVER mode, door /Quarantine/list/blocked (listBlocked).
 * Row actions (pack §14 codes): DISPOSE → the Disposition page; UNBLOCK → an Unblock DECISION (reason ≥ 10 → createDecision →
 * MOD-0022 signature → submitDecision; the stock moves Bloke → Kullanılabilir only after approval, §21.3 #3) — shown disabled with
 * the mockup tooltip until the review is complete (V14); COMPLETEREVIEW → review-complete (If-Match). Toolbar BLOCKSTOCK →
 * the "Stoğu bloke et" modal (blockStock, §21.3 #9). A row with a decision in flight offers no new decision.
 */
'use strict';

/**
 * WP-MVP1-FIX-QUI1 — the "Stoğu bloke et" quantity rules (no DOM; the Node test runs the page against THIS file):
 *   quantityAfterRowChange — choosing a stock row NEVER writes a quantity (WP-MVP1-FIX-LIVE4, FU-0175-BLOCK-QTY: the row's 80 prefilled
 *                            the field and a typed "10" became "8010"); what the user typed stays, an empty field stays empty;
 *   quantityAll            — the whole row's amount, written ONLY by the "Tümü" button;
 *   quantityError          — none, ≤ 0 or above the row's amount is refused before anything is sent.
 */
var QuarantineBlockedRules = (function () {
    const quantityAfterRowChange = (current) => String(current ?? '').trim();
    const quantityAll = (row) => (row ? String(row.onHand ?? '') : '');
    const quantityError = (value, row) => {
        const quantity = Number(value);
        return !row || !(quantity > 0) || quantity > Number(row.onHand) ? 'Quarantine.BlockStock.QtyInvalid' : null;
    };
    return { quantityAfterRowChange, quantityAll, quantityError };
})();

if (typeof window !== 'undefined') window.QuarantineBlockedRules = QuarantineBlockedRules;

(function () {
    if (typeof window === 'undefined' || !window.QuarantineUi) return;
    const ui = window.QuarantineUi;
    const { t, esc, qty, date } = ui;
    const tableEl = document.querySelector('.datatables-quarantine-blocked');
    const lang = (document.documentElement.lang || 'tr').slice(0, 2);
    let list = null;
    let reasons = [];

    const filterFields = [
        { id: 'filterReason', key: 'blockReasonCode', kind: 'single' },
        { id: 'filterPending', key: 'pendingOnly', kind: 'single' },
        // WP-MVP1-FIX-QUI1: empty = Açık (the door's default status=Open), Closed / SampleRetained, All = every record
        { id: 'filterStatus', key: 'status', kind: 'single' }
    ];

    const reasonName = (code) => {
        const r = reasons.find((x) => x.code === code);
        return r ? (r.names?.[lang] || r.names?.en || r.code) : code;
    };
    const loadReasons = async () => {
        const reply = await ui.get(ui.api('block-reasons'));
        reasons = reply.ok && Array.isArray(reply.json) ? reply.json : [];
        const filter = ui.byId('filterReason');
        reasons.forEach((r) => filter?.add(new Option(reasonName(r.code), r.code)));
        const select = ui.byId('qr-block-reason-code');
        reasons.filter((r) => r.isActive).forEach((r) => select?.add(new Option(reasonName(r.code), r.code)));
    };

    const reload = () => list?.dt?.ajax?.reload(null, false);

    const onResponse = (json) => {
        ui.hideError(ui.byId('qr-list-error'));
        ui.byId('qr-labels-unavailable')?.classList.toggle('d-none', json?.labelsAvailable !== false);
        // WP-MVP1-FIX-QUI1: the tab's badge counts OPEN records only. The door names the status this page shows; another
        // status's total is not the badge's, so the open count is read on its own (the door's default is Open).
        if (json?.status === 'Open') {
            ui.setCount('BLOCKEDSTOCK', json?.data?.total ?? 0);
        } else {
            ui.get('/Quarantine/list/blocked?start=0&length=1').then((reply) => { if (reply.ok) ui.setCount('BLOCKEDSTOCK', reply.json?.data?.total ?? ''); });
        }
    };
    const onListError = (xhr) => {
        let reply = { status: xhr?.status || 0, json: null };
        try { reply.json = JSON.parse(xhr?.responseText || 'null'); } catch (error) { reply.json = null; }
        ui.showError(ui.byId('qr-list-error'), reply);
    };

    // ── Blokeyi Kaldır: the decision, its signature, the approval ────────────────────────────────────────────────
    const unblock = (row) => {
        const template = ui.byId('qr-unblock-template');
        const body = template.content.firstElementChild.cloneNode(true);
        body.querySelector('[data-ub-subject]').textContent = [row.itemLabel, row.lot?.lotNumber, `${qty(row.quantity)} ${ui.uom(row)}`].filter(Boolean).join(' · ');
        const key = ui.newKey();
        window.showConfirm?.(t('Quarantine.Unblock.Title'), async (reason) => {
            const draft = await ui.write('POST', 'decisions', { kind: 'Unblock', targetId: row.blockedId, reason: (reason || '').trim(), idempotencyKey: key });
            if (!draft.ok) {
                window.DitenModal?.error({ title: t('Quarantine.Unblock.Title'), message: ui.errorText(draft) });
                return;
            }
            const sent = await ui.signAndSubmit(draft.json);
            if (sent.cancelled) {
                ui.toast(t('Quarantine.Sign.Cancelled'), 'warning');
                reload();
                return;
            }
            if (!sent.ok) {
                window.DitenModal?.error({ title: t('Quarantine.Unblock.Title'), message: ui.errorText(sent.reply) });
                reload();
                return;
            }
            await ui.announceSent(sent.decision);
            reload();
        }, {
            type: 'warning',
            // WP-MVP1-FIX-QUI1: the body is MARKUP (the view's constant template; the subject went in through textContent, so the
            // serialized form keeps it escaped) — the dialog's markup door is subtextHtml; `subtext` is text and showed the tags.
            subtextHtml: body.outerHTML,
            confirmButtonText: t('Quarantine.Unblock.Confirm'),
            cancelButtonText: t('Quarantine.Common.Cancel'),
            showInput: true,
            inputType: 'textarea',
            inputLabel: t('Quarantine.Field.Reason'),
            inputAttributes: { maxlength: 1000 },
            inputValidator: (value) => ((value || '').trim().length < 10 ? t('Quarantine.Common.ReasonMin') : null)
        });
    };

    // ── İncelemeyi tamamla (V14 opens Unblock) ───────────────────────────────────────────────────────────────────
    const completeReview = (row) => {
        window.showConfirm?.(t('Quarantine.CompleteReview.Title'), async (reason) => {
            const reply = await ui.write('POST', `blocked/${encodeURIComponent(row.blockedId)}/review-complete`, { reason: (reason || '').trim() }, `W/"${row.version}"`);
            if (!reply.ok) {
                window.DitenModal?.error({ title: t('Quarantine.CompleteReview.Title'), message: ui.errorText(reply) });
                return;
            }
            ui.toast(t('Quarantine.CompleteReview.Done'), 'success');
            reload();
        }, {
            type: 'info',
            subtext: [row.itemLabel, row.lot?.lotNumber, row.blockedNumber].filter(Boolean).join(' · '), // text: the dialog escapes it (FIX-QUI1)
            confirmButtonText: t('Quarantine.Action.CompleteReview'),
            cancelButtonText: t('Quarantine.Common.Cancel'),
            showInput: true,
            inputType: 'textarea',
            inputLabel: t('Quarantine.Field.Reason'),
            inputAttributes: { maxlength: 1000 },
            inputValidator: (value) => ((value || '').trim().length < 10 ? t('Quarantine.Common.ReasonMin') : null)
        });
    };

    // ── Stoğu bloke et (blockStock — no workflow, the safety direction) ──────────────────────────────────────────
    let stockRows = [];
    let blockKey = null;
    const blockModal = () => ui.byId('qr-block-modal');
    const openBlock = () => {
        const modal = blockModal();
        if (!modal) return;
        const form = ui.byId('qr-block-form');
        form.reset();
        ui.clearFieldErrors(form);
        ui.hideError(ui.byId('qr-block-error'));
        blockKey = ui.newKey();
        stockRows = [];
        const rowSelect = ui.byId('qr-block-row');
        rowSelect.innerHTML = '<option value=""></option>';
        rowSelect.disabled = true;
        ui.byId('qr-block-uom').textContent = '';
        ui.byId('qr-block-available').textContent = '';
        if (window.jQuery?.fn?.select2) window.jQuery('#qr-block-lot').val(null).trigger('change');
        window.bootstrap.Modal.getOrCreateInstance(modal).show();
    };
    const initBlock = () => {
        const modal = blockModal();
        if (!modal) return;
        ui.counter(ui.byId('qr-block-reason'));
        const lotSelect = ui.byId('qr-block-lot');
        if (window.jQuery?.fn?.select2) {
            window.jQuery(lotSelect).select2({
                dropdownParent: window.jQuery(modal),
                placeholder: lotSelect.dataset.placeholder || '',
                allowClear: true,
                width: '100%',
                ajax: {
                    url: '/Quarantine/lookups/lots',
                    dataType: 'json',
                    delay: 250,
                    data: (params) => ({ q: params.term || '' }),
                    processResults: (data) => ({ results: (data?.items || []).map((i) => ({ id: i.value, text: i.text || i.value })) })
                }
            }).on('change', () => loadStock(lotSelect.value));
        } else {
            lotSelect.addEventListener('change', () => loadStock(lotSelect.value));
        }
        ui.byId('qr-block-row').addEventListener('change', (event) => {
            const row = event.target.value === '' ? undefined : stockRows[Number(event.target.value)];
            ui.byId('qr-block-uom').textContent = row?.baseUomId || '';
            // WP-MVP1-FIX-QUI1: the typed quantity stays (an empty field takes the row's amount as a suggestion); the row's amount
            // is shown under the field, and a quantity above it is named at once (the submit refuses it as well).
            const field = ui.byId('qr-block-qty');
            field.value = QuarantineBlockedRules.quantityAfterRowChange(field.value, row);
            ui.byId('qr-block-available').textContent = row ? t('Quarantine.BlockStock.Available', qty(row.onHand), row.baseUomId || '') : '';
            ui.byId('qr-block-all').disabled = !row;
            ui.fieldError(ui.byId('qr-block-form'), 'qty', row && field.value !== '' && QuarantineBlockedRules.quantityError(field.value, row) ? t('Quarantine.BlockStock.QtyInvalid') : '');
        });
        // WP-MVP1-FIX-LIVE4 (FU-0175-BLOCK-QTY): the whole row only on purpose — "Tümü" writes the row's amount, a click away, never by default
        ui.byId('qr-block-all').addEventListener('click', () => {
            const value = ui.byId('qr-block-row').value;
            const row = value === '' ? undefined : stockRows[Number(value)];
            const field = ui.byId('qr-block-qty');
            field.value = QuarantineBlockedRules.quantityAll(row);
            ui.fieldError(ui.byId('qr-block-form'), 'qty', '');
            field.focus?.();
        });
        ui.byId('qr-block-form').addEventListener('submit', submitBlock);
    };
    const loadStock = async (lotId) => {
        const rowSelect = ui.byId('qr-block-row');
        rowSelect.innerHTML = '<option value=""></option>';
        rowSelect.disabled = true;
        ui.byId('qr-block-norows').classList.add('d-none');
        ui.byId('qr-block-available').textContent = '';
        if (!lotId) return;
        const reply = await ui.get(`/Quarantine/lookups/stock?${new URLSearchParams({ lotId })}`);
        stockRows = reply.ok ? (reply.json?.items || []) : [];
        stockRows.forEach((r, i) => rowSelect.add(new Option(`${r.warehouse || r.warehouseId} · ${r.location || r.locationId} — ${qty(r.onHand)} ${r.baseUomId || ''}`, String(i))));
        rowSelect.disabled = stockRows.length === 0;
        ui.byId('qr-block-norows').classList.toggle('d-none', stockRows.length > 0);
    };
    const submitBlock = async (event) => {
        event.preventDefault();
        const form = ui.byId('qr-block-form');
        ui.clearFieldErrors(form);
        const rowValue = ui.byId('qr-block-row').value;
        const row = rowValue === '' ? undefined : stockRows[Number(rowValue)];
        const quantity = Number(ui.byId('qr-block-qty').value);
        const code = ui.byId('qr-block-reason-code').value;
        const reason = ui.byId('qr-block-reason').value.trim();
        let invalid = false;
        if (QuarantineBlockedRules.quantityError(ui.byId('qr-block-qty').value, row)) { ui.fieldError(form, 'qty', t('Quarantine.BlockStock.QtyInvalid')); invalid = true; }
        if (!code) { ui.fieldError(form, 'blockReason', t('Quarantine.Decide.Err.BlockReason')); invalid = true; }
        if (reason.length < 10) { ui.fieldError(form, 'reason', t('Quarantine.Common.ReasonMin')); invalid = true; }
        if (invalid) return;
        const button = ui.byId('qr-block-submit');
        button.disabled = true;
        const reply = await ui.write('POST', 'blocked', {
            buckets: [{ itemId: row.itemId, skuId: row.skuId, skuLevel: row.skuLevel, warehouseId: row.warehouseId, locationId: row.locationId, quantity, lotId: row.lotId }],
            blockReasonCode: code,
            note: ui.byId('qr-block-note').value.trim() || undefined,
            reason,
            idempotencyKey: blockKey
        });
        button.disabled = false;
        if (!reply.ok) {
            ui.showError(ui.byId('qr-block-error'), reply);
            return;
        }
        window.bootstrap.Modal.getOrCreateInstance(blockModal()).hide();
        ui.toast(t('Quarantine.BlockStock.Done', (reply.json || []).map((b) => b.blockedNumber).join(', ')), 'success');
        reload();
    };

    // ── the list ────────────────────────────────────────────────────────────────────────────────────────────────
    const go = (url) => { window.location.href = url; };
    const rowActionHandlers = {
        DISPOSE: ({ row }) => row?.blockedId && go(`/Quarantine/Blocked/Disposition/${encodeURIComponent(row.blockedId)}`),
        UNBLOCK: ({ row }) => row?.blockedId && row.reviewed && !row.activeDecision && unblock(row),
        COMPLETEREVIEW: ({ row }) => row?.blockedId && completeReview(row)
    };

    const pendingCell = (row) => {
        // WP-MVP1-FIX-QUI1: a record that is no longer Open (Closed / SampleRetained) says so — no pending state, no actions
        if (row.status && row.status !== 'Open') {
            return `<span class="badge bg-label-secondary" data-qr-blocked-status="${esc(row.status)}">${esc(t('Quarantine.Blocked.Status.' + row.status))}</span>`;
        }
        if (row.activeDecision) {
            return `<span class="d-flex align-items-center gap-1 text-warning"><i class="bx bx-time-five"></i>${esc(t('Quarantine.Blocked.ActivePending', row.activeDecision.decisionNumber))}</span>`;
        }
        if (row.pendingDisposition) return `<span class="d-flex align-items-center gap-1 text-danger"><i class="bx bx-error-circle"></i>${esc(t('Quarantine.Blocked.Pending.Yes'))}</span>`;
        return row.reviewed
            ? `<span class="d-flex align-items-center gap-1 text-success"><i class="bx bx-check-circle"></i>${esc(t('Quarantine.Blocked.Pending.NoReviewed'))}</span>`
            : `<span class="d-flex align-items-center gap-1 text-primary"><i class="bx bx-loader-circle"></i>${esc(t('Quarantine.Blocked.Pending.NoReviewing'))}</span>`;
    };
    const renderAction = (full) => {
        if (!full.blockedId) return '';
        const attrs = (text) => ({ 'data-id': full.blockedId, 'data-json': JSON.stringify(full), title: text, 'aria-label': text });
        const open = full.status === 'Open' && !full.activeDecision;
        const allowed = full.allowedActions || [];
        const unblockText = full.reviewed ? t('Quarantine.Action.Unblock') : t('Quarantine.Blocked.UnblockDisabled');
        return window.DitenDataTable.renderActions([
            { key: 'DISPOSE', visible: ui.can('DISPOSE') && open && allowed.some((a) => a.startsWith('Dispose:')), icon: 'bx bx-transfer', text: t('Quarantine.Action.Dispose'), attrs: attrs(t('Quarantine.Action.Dispose')) },
            { key: 'UNBLOCK', visible: ui.can('UNBLOCK') && open, className: full.reviewed ? '' : 'disabled text-muted', icon: 'bx bx-lock-open', text: unblockText, attrs: attrs(unblockText) },
            { key: 'COMPLETEREVIEW', visible: ui.can('COMPLETEREVIEW') && open && !full.reviewed, icon: 'bx bx-check-double', text: t('Quarantine.Action.CompleteReview'), attrs: attrs(t('Quarantine.Action.CompleteReview')) }
        ]);
    };

    const init = async () => {
        if (!tableEl) return;
        ui.loadCounts('BLOCKEDSTOCK');
        initBlock();
        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: '/Quarantine/list/blocked', type: 'GET', error: onListError },
            onResponse,
            actions: { onRowAction: rowActionHandlers },
            toolbar: {
                addNewText: ui.can('BLOCKSTOCK') && blockModal() ? t('Quarantine.Action.BlockStock') : '',
                onAddNew: openBlock,
                colvisColumns: [2, 3, 4, 5, 6, 7]
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadReasons },
            savedView: {
                moduleKey: 'Quarantine',
                pageKey: 'Blocked',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7],
                baseOrder: []
            },
            config: {
                ordering: false,
                searching: true,
                pageLength: 10,
                order: [],
                language: { emptyTable: t('Quarantine.Blocked.Empty'), zeroRecords: t('Quarantine.Blocked.Empty') },
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'blockedId', name: 'control' },
                    { data: 'itemLabel', name: 'product' },
                    { data: 'lot', name: 'lot' },
                    { data: 'warehouseLabel', name: 'where' },
                    { data: 'quantity', name: 'quantity' },
                    { data: 'blockReason', name: 'blockReason' },
                    { data: 'blockedAtUtc', name: 'blockedAt' },
                    { data: 'pendingDisposition', name: 'pending' },
                    { data: 'blockedId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => ui.productCell(full, full.blockedNumber || t('Quarantine.Origin.' + full.origin)) },
                    { targets: 2, render: (data, type, full) => `<div class="d-flex flex-column">${ui.lotLink(data)}<small class="text-muted">${esc(t('Quarantine.Origin.' + full.origin))}</small></div>` },
                    { targets: 3, render: (data, type, full) => ui.whereCell(full) },
                    { targets: 4, className: 'text-end', render: (data, type, full) => `<span dir="ltr">${esc(qty(data))} ${esc(ui.uom(full))}</span>` },
                    { targets: 5, render: (data, type, full) => `<div class="d-flex flex-column"><span class="badge bg-label-danger align-self-start">${esc(data?.name || reasonName(data?.code) || '—')}</span><small class="text-muted">${esc(full.note || '')}</small></div>` },
                    { targets: 6, render: (data) => esc(date(data)) },
                    { targets: 7, render: (data, type, full) => pendingCell(full) },
                    { targets: -1, title: t('Quarantine.Col.Actions'), searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
                ]
            }
        });
        // The WorkCenter task of a blocked-stock decision links here with ?q={BLK no} (DisplayContext, Q5): the list opens on it.
        const q = new URLSearchParams(window.location.search).get('q');
        if (q && list?.dt) list.dt.search(q).draw();
    };

    document.addEventListener('DOMContentLoaded', init);
})();
