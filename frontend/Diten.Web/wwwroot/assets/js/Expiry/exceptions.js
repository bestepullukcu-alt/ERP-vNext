/**
 * MOD-0176 F5 — FEFO İstisna Kutusu (pack §11.1; mockup "inbox"). DitenDataTable.createList, SERVER mode, door /Expiry/list/exceptions
 * (listExceptions, newest first). The tabs set the hidden filter "tab" (all · active · late · done — the door turns it into status / late)
 * and show the server's totals. A row opens /Expiry/Exceptions/Details/{id}. No rule here: late / dueSoon are the server's flags.
 */
'use strict';

(function () {
    const ui = window.ExpiryUi;
    const { t, esc } = ui;
    const tableEl = document.querySelector('.datatables-expiry-exceptions');
    const filterFields = [
        { id: 'filterTab', key: 'tab', kind: 'single' },
        { id: 'filterType', key: 'type', kind: 'single' },
        { id: 'filterStatus', key: 'status', kind: 'single' },
        { id: 'filterWarehouse', key: 'warehouseId', kind: 'single' },
        { id: 'filterAssigned', key: 'assignedToMe', kind: 'single' }
    ];
    const detailUrl = (id) => `/Expiry/Exceptions/Details/${encodeURIComponent(id)}`;

    const loadCounts = async () => {
        await Promise.all(['all', 'active', 'late', 'done'].map(async (tab) => {
            const reply = await ui.get(`/Expiry/list/exceptions?start=0&length=1&tab=${tab}`);
            const badge = document.querySelector(`[data-ex-count="${tab}"]`);
            if (!badge) return;
            const total = reply.ok ? Number(reply.json?.data?.total ?? 0) : null;
            badge.textContent = total === null ? '—' : ui.qty(total);
            badge.classList.toggle('bg-label-danger', tab === 'late' && total > 0);
            badge.classList.toggle('bg-label-secondary', !(tab === 'late' && total > 0));
        }));
    };

    const selectTab = (tab) => {
        document.querySelectorAll('[data-ex-tab]').forEach((b) => {
            const on = b.getAttribute('data-ex-tab') === tab;
            b.classList.toggle('active', on);
            b.classList.toggle('shadow-sm', !on);
            b.style.backgroundColor = on ? '' : 'var(--bs-paper-bg)';
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        ui.byId('filterTab').value = tab;
        ui.byId('btnFilterApply')?.click();
    };

    const onListError = (xhr) => {
        let reply = { status: xhr?.status || 0, json: null };
        try { reply.json = JSON.parse(xhr?.responseText || 'null'); } catch (error) { reply.json = null; }
        ui.showError(ui.byId('ex-list-error'), reply);
    };

    const init = async () => {
        if (!tableEl) return;
        await ui.loadWarehouses(ui.byId('filterWarehouse'));
        document.querySelectorAll('[data-ex-tab]').forEach((b) => b.addEventListener('click', () => selectTab(b.getAttribute('data-ex-tab'))));
        await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: '/Expiry/list/exceptions', type: 'GET', error: onListError },
            onResponse: (json) => {
                ui.hideError(ui.byId('ex-list-error'));
                ui.byId('ex-labels-unavailable')?.classList.toggle('d-none', json?.labelsAvailable !== false);
            },
            toolbar: { addNewText: '', colvisColumns: [3, 5, 7] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields },
            savedView: {
                moduleKey: 'Expiry',
                pageKey: 'Exceptions',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8],
                baseOrder: []
            },
            config: {
                ordering: false,
                searching: true,
                pageLength: 25,
                order: [],
                language: { emptyTable: t('Expiry.Inbox.Empty'), zeroRecords: t('Expiry.Inbox.Empty') },
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'exceptionId', name: 'control' },
                    { data: 'number', name: 'number' },
                    { data: 'type', name: 'type' },
                    { data: 'itemLabel', name: 'product' },
                    { data: 'lot', name: 'lot' },
                    { data: 'openedAtUtc', name: 'opened' },
                    { data: 'dueAtUtc', name: 'sla' },
                    { data: 'assignee', name: 'assignee' },
                    { data: 'status', name: 'status' },
                    { data: 'exceptionId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => `<a class="fw-medium font-monospace" href="${detailUrl(full.exceptionId)}">${esc(data)}</a>` },
                    { targets: 2, render: (data) => ui.typeBadge(data) },
                    { targets: 3, render: (data, type, full) => ui.productCell(full) },
                    { targets: 4, render: (data, type, full) => `<div class="d-flex flex-column">${ui.lotLink({ lotId: full.lot?.lotId, lotNumber: full.lot?.lotNumber })}`
                        + `<small class="text-muted">${esc(full.warehouseLabel || '—')}</small></div>` },
                    { targets: 5, render: (data) => `<small dir="ltr">${esc(ui.dateTime(data))}</small>` },
                    { targets: 6, render: (data, type, full) => ui.slaCell(full) },
                    { targets: 7, render: (data) => (data ? esc(ui.person(data)) : `<span class="text-muted">${esc(t('Expiry.Inbox.Unassigned'))}</span>`) },
                    { targets: 8, render: (data) => ui.caseStatusBadge(data) },
                    {
                        targets: -1, title: t('Expiry.Col.Actions'), searchable: false, orderable: false, className: 'cell-fit all',
                        render: (data) => `<a class="btn btn-sm btn-icon btn-text-secondary" href="${detailUrl(data)}" title="${esc(t('Expiry.Action.OpenDetail'))}" aria-label="${esc(t('Expiry.Action.OpenDetail'))}"><i class="bx bx-chevron-right"></i></a>`
                    }
                ]
            }
        });
        await loadCounts();
    };

    document.addEventListener('DOMContentLoaded', init);
})();
