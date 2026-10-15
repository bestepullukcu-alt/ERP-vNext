/**
 * MOD-0177 R6 — Vakalar (pack §8.10, §11.1, mockup "isCases"). DitenDataTable.createList, SERVER mode, door /Recall/list/cases — the RECALL
 * 1.5.0 union read: real cases and drill runs in ONE list in the server's order. Filters: type, case status, drill status, class, product,
 * opened from (forwarded as they are; the server applies them — a class filter leaves the drill rows out). Status chips = the server's case
 * counts. R7a: the number of a real case links to its Vaka Detayı; drill rows link nowhere until R7b (no dead link).
 */
'use strict';

(function () {
    if (typeof document === 'undefined' || !window.RecallUi || !window.RecallCasesView) return;
    const ui = window.RecallUi;
    const { t, esc } = ui;
    const tableEl = document.querySelector('.datatables-recall-cases');
    const fmt = { t, qty: ui.qty, date: ui.date, duration: ui.duration };
    let list = null;

    const filterFields = [
        { id: 'filterType', key: 'type', kind: 'single' },
        { id: 'filterStatus', key: 'status', kind: 'single' },
        { id: 'filterDrillStatus', key: 'drillStatus', kind: 'single' },
        { id: 'filterClass', key: 'class', kind: 'single' },
        { id: 'filterProduct', key: 'skuId', kind: 'single' },
        { id: 'filterOpenedFrom', key: 'openedFrom', kind: 'text' }
    ];

    const loadProducts = async () => {
        const reply = await ui.get('/Recall/lookups/products');
        const select = ui.byId('filterProduct');
        (reply.ok ? reply.json?.items || [] : []).forEach((p) => select?.add(new Option(p.text || p.value, p.value)));
    };

    const onResponse = (json) => {
        ui.hideError(ui.byId('rc-list-error'));
        ui.byId('rc-labels-unavailable')?.classList.toggle('d-none', json?.labelsAvailable !== false);
        Object.entries(json?.statusCounts || {}).forEach(([status, n]) => {
            const chip = document.querySelector(`[data-status-count="${status}"]`);
            if (chip) chip.textContent = String(n);
        });
    };
    const onListError = (xhr) => {
        const reply = { status: xhr?.status || 0, json: null };
        try { reply.json = JSON.parse(xhr?.responseText || 'null'); } catch (error) { reply.json = null; }
        ui.showError(ui.byId('rc-list-error'), reply);
    };

    const cell = (row) => window.RecallCasesView.cells(row, fmt);
    const productCell = (c) => `<div class="d-flex flex-column"><span class="fw-medium text-heading">${esc(c.product)}</span><small class="text-muted font-monospace">${esc(c.lot)}</small></div>`;
    const status = (row) => (row.rowKind === 'Drill' ? ui.drillStatusBadge(row.status) : ui.statusBadge(row.status));

    const init = async () => {
        if (!tableEl) return;
        document.getElementById('rc-status-chips')?.addEventListener('click', (event) => {
            const chip = event.target.closest('[data-status-chip]');
            if (!chip) return;
            const select = ui.byId('filterStatus');
            select.value = chip.dataset.statusChip;
            document.getElementById('btnFilterApply')?.click();
        });
        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: '/Recall/list/cases', type: 'GET', error: onListError },
            onResponse,
            toolbar: { addNewText: '', colvisColumns: [2, 4, 5, 6, 7, 8] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadProducts },
            savedView: {
                moduleKey: 'Recall',
                pageKey: 'Cases',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8, 9],
                baseOrder: []
            },
            config: {
                ordering: false,
                searching: true,
                pageLength: 10,
                order: [],
                language: { emptyTable: t('Recall.Cases.Empty'), zeroRecords: t('Recall.Cases.Empty') },
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'number', name: 'control' },
                    { data: 'number', name: 'number' },
                    { data: 'rowKind', name: 'type' },
                    { data: 'item', name: 'product' },
                    { data: 'class', name: 'class' },
                    { data: 'level', name: 'level' },
                    { data: 'openedAtUtc', name: 'opened' },
                    { data: 'owner', name: 'owner' },
                    { data: 'progress', name: 'progress' },
                    { data: 'status', name: 'status' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => (cell(full).clickable
                        ? `<a class="font-monospace fw-medium" href="${esc(cell(full).href)}">${esc(cell(full).number)}</a>`
                        : `<span class="font-monospace">${esc(cell(full).number)}</span>`) },
                    { targets: 2, render: (data, type, full) => ui.typeBadge(full.rowKind) },
                    { targets: 3, render: (data, type, full) => productCell(cell(full)) },
                    { targets: 4, render: (data, type, full) => (full.rowKind === 'Drill'
                        ? `<span class="text-muted" title="${esc(t('Recall.Drill.NoClass'))}">${esc(window.RecallCasesView.DASH)}</span>`
                        : ui.classBadge(full.class)) },
                    { targets: 5, render: (data, type, full) => esc(cell(full).level) },
                    { targets: 6, render: (data, type, full) => esc(cell(full).opened) },
                    { targets: 7, render: (data, type, full) => esc(cell(full).owner) },
                    { targets: 8, render: (data, type, full) => `<small>${esc(cell(full).progress)}</small>` },
                    { targets: 9, render: (data, type, full) => status(full) }
                ]
            }
        });
    };

    document.addEventListener('DOMContentLoaded', init);
})();
