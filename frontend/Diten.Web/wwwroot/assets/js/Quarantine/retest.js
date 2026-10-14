/**
 * MOD-0175 Q5 — Yeniden Test Kuyruğu (pack §11.3, KQ-H). DitenDataTable.createList, SERVER mode, door /Quarantine/list/retest
 * (listRetest — the whole horizon; the door pages and searches it). State chip: Yaklaşıyor / Tarihi geçti · kullanıma kapalı /
 * Karantinada. Row actions: QUARANTINELOT → "Karantinaya Al" (quarantineStock mode Retest: every AVAILABLE bucket of the lot →
 * QUARANTINE now, no workflow — the safety direction; the contract asks a reason ≥ 10, so the confirmation asks it);
 * VIEWHOLD → "{KRN} →" the hold the lot already has.
 */
'use strict';

(function () {
    const ui = window.QuarantineUi;
    const { t, esc, qty, date } = ui;
    const tableEl = document.querySelector('.datatables-quarantine-retest');
    let list = null;

    const reload = () => list?.dt?.ajax?.reload(null, false);
    const onResponse = (json) => {
        ui.hideError(ui.byId('qr-list-error'));
        ui.byId('qr-labels-unavailable')?.classList.toggle('d-none', json?.labelsAvailable !== false);
        ui.setCount('RETESTQUEUE', json?.total ?? json?.data?.total ?? 0);
    };
    const onListError = (xhr) => {
        let reply = { status: xhr?.status || 0, json: null };
        try { reply.json = JSON.parse(xhr?.responseText || 'null'); } catch (error) { reply.json = null; }
        ui.showError(ui.byId('qr-list-error'), reply);
    };

    const quarantine = (row) => {
        const amount = `${qty(row.availableQuantity)} ${ui.uom(row)}`;
        const key = ui.newKey();
        window.showConfirm?.(t('Quarantine.Retest.Confirm.Title'), async (reason) => {
            const reply = await ui.write('POST', 'holds', { mode: 'Retest', lotId: row.lotId, reason: (reason || '').trim(), idempotencyKey: key });
            if (!reply.ok) {
                window.DitenModal?.error({ title: t('Quarantine.Retest.Confirm.Title'), message: ui.errorText(reply) });
                reload();
                return;
            }
            const numbers = (reply.json || []).map((h) => h.holdNumber).join(', ');
            ui.toast(t('Quarantine.Retest.Done', row.lotNumber || row.lotId, numbers), 'success');
            reload();
            ui.loadCounts('RETESTQUEUE');
        }, {
            type: 'warning',
            subtext: t('Quarantine.Retest.Confirm.Text', row.lotNumber || row.lotId, amount),
            confirmButtonText: t('Quarantine.Action.Quarantine'),
            cancelButtonText: t('Quarantine.Common.Cancel'),
            showInput: true,
            inputType: 'textarea',
            inputLabel: t('Quarantine.Field.Reason'),
            inputPlaceholder: t('Quarantine.Retest.Confirm.DefaultReason', date(row.retestDate)),
            inputAttributes: { maxlength: 1000 },
            inputValidator: (value) => ((value || '').trim().length < 10 ? t('Quarantine.Common.ReasonMin') : null)
        });
    };

    const go = (url) => { window.location.href = url; };
    const rowActionHandlers = {
        QUARANTINELOT: ({ row }) => row?.lotId && row.state !== 'Quarantined' && quarantine(row),
        VIEWHOLD: ({ row }) => row?.holdIds?.[0] && go(`/Quarantine/Details/${encodeURIComponent(row.holdIds[0])}`)
    };

    const STATE = { Upcoming: ['bg-label-info', 'bx-time'], DueClosed: ['bg-label-danger', 'bx-lock-alt'], Quarantined: ['bg-label-warning', 'bx-shield-quarter'] };
    const stateCell = (row) => {
        const [cls, icon] = STATE[row.state] || ['bg-label-secondary', 'bx-help-circle'];
        return `<span class="badge ${cls} d-inline-flex align-items-center gap-1"><i class="bx ${icon}"></i>${esc(t('Quarantine.Retest.State.' + row.state))}</span>`;
    };
    const dateCell = (row) => {
        const d = row.daysRelative;
        const rel = d < 0 ? t('Quarantine.Common.DaysPast', -d) : t('Quarantine.Common.DaysLeft', d);
        return `<div class="d-flex flex-column"><span>${esc(date(row.retestDate))}</span><small class="${d < 0 ? 'text-danger' : 'text-muted'}">${esc(rel)}</small></div>`;
    };
    const renderAction = (full) => {
        const attrs = (text) => ({ 'data-id': full.lotId, 'data-json': JSON.stringify(full), title: text, 'aria-label': text });
        const quarantined = full.state === 'Quarantined' || (full.holdIds || []).length > 0;
        return window.DitenDataTable.renderActions([
            { key: 'QUARANTINELOT', visible: ui.can('QUARANTINELOT') && !quarantined, icon: 'bx bx-shield-quarter', text: t('Quarantine.Action.Quarantine'), attrs: attrs(t('Quarantine.Action.Quarantine')) },
            { key: 'VIEWHOLD', visible: ui.can('VIEWHOLD') && quarantined, icon: 'bx bx-right-arrow-alt', text: t('Quarantine.Retest.Open'), attrs: attrs(t('Quarantine.Retest.Open')) }
        ]);
    };

    const init = async () => {
        if (!tableEl) return;
        ui.loadCounts('RETESTQUEUE');
        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: '/Quarantine/list/retest', type: 'GET', error: onListError },
            onResponse,
            actions: { onRowAction: rowActionHandlers },
            toolbar: { addNewText: '', colvisColumns: [2, 3, 4, 5] },
            savedView: {
                moduleKey: 'Quarantine',
                pageKey: 'Retest',
                saveViewColumnIndexes: [1, 2, 3, 4, 5],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5],
                baseOrder: []
            },
            config: {
                ordering: false,
                searching: true,
                pageLength: 10,
                order: [],
                language: { emptyTable: t('Quarantine.Retest.Empty'), zeroRecords: t('Quarantine.Retest.Empty') },
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'lotId', name: 'control' },
                    { data: 'itemLabel', name: 'product' },
                    { data: 'lotNumber', name: 'lot' },
                    { data: 'retestDate', name: 'retestDate' },
                    { data: 'availableQuantity', name: 'onHand' },
                    { data: 'state', name: 'state' },
                    { data: 'lotId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => ui.productCell(full, null) },
                    { targets: 2, render: (data, type, full) => ui.lotLink({ lotId: full.lotId, lotNumber: full.lotNumber }) },
                    { targets: 3, render: (data, type, full) => dateCell(full) },
                    { targets: 4, className: 'text-end', render: (data, type, full) => `<span dir="ltr">${esc(qty(data))} ${esc(ui.uom(full))}</span>` },
                    { targets: 5, render: (data, type, full) => stateCell(full) },
                    { targets: -1, title: t('Quarantine.Col.Actions'), searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
                ]
            }
        });
    };

    document.addEventListener('DOMContentLoaded', init);
})();
