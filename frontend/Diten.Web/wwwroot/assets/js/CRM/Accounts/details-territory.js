/**
 * MOD-0151 — Account 360 Details: Territory coverage history table.
 *  • Read-only DataTable v2 via the shared Crm360Section factory (inline filter, Save View, colReorder, export/colvis).
 *  • Shows the CURRENT (active, effective-now) assignment(s) AND every historical one; a "current" badge flags the
 *    effective rows and closed rows render muted.
 *  • No management actions: assignments are created / ended from MOD-0151 Territory Management, never from this page.
 *
 * Rows come from the server-rendered #account-territory-payload projection, so the section needs no extra endpoint.
 */
'use strict';

(function () {
    const tableEl = document.getElementById('dt-account-territory');
    const payloadEl = document.getElementById('account-territory-payload');
    if (!tableEl || !payloadEl) return;

    let ctx = {};
    try {
        ctx = JSON.parse(payloadEl.textContent || '{}');
    } catch (error) {
        console.error('[AccountDetails] Territory payload could not be parsed.', error);
        return;
    }

    const rows = Array.isArray(ctx.rows) ? ctx.rows : [];

    let L = window.L10n || {};
    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const esc = (v) => (v === null || v === undefined || v === ''
        ? ''
        : String(v).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])));
    const dash = (v) => (v === null || v === undefined || String(v).trim() === '' ? '-' : esc(v));

    const statusBadgeClass = (status) => ({
        active: 'bg-label-success',
        pending: 'bg-label-warning',
        inactive: 'bg-label-secondary',
        ended: 'bg-label-secondary',
        superseded: 'bg-label-secondary'
    }[String(status || '').toLowerCase()] || 'bg-label-secondary');

    const renderRow = (row) => {
        const closed = !row.isCurrent;
        const status = row.status ? `<span class="badge ${statusBadgeClass(row.status)}">${esc(row.status)}</span>` : '-';
        const currentBadge = row.isCurrent
            ? `<span class="badge bg-label-primary ms-1">${esc(L.CurrentBadge || L.Active || 'Current')}</span>`
            : '';

        return `<tr class="${closed ? 'text-muted' : ''}">
            <td></td>
            <td><span class="fw-medium text-heading">${dash(row.territoryNodeName)}</span>${currentBadge}</td>
            <td>${dash(row.territoryNodeCode)}</td>
            <td>${dash(row.assignmentSource)}</td>
            <td data-order="${esc(row.status)}">${status}</td>
            <td class="text-nowrap" data-order="${esc(row.effectiveFrom || '')}">${dash(row.effectiveFrom)}</td>
            <td class="text-nowrap" data-order="${esc(row.effectiveTo || '')}">${dash(row.effectiveTo)}</td>
        </tr>`;
    };

    const init = async () => {
        syncL10n();

        if (!window.Crm360Section?.create) {
            console.warn('[AccountDetails] Crm360Section factory unavailable; territory table not initialised.');
            return;
        }

        const section = await window.Crm360Section.create({
            tableEl,
            bodyEl: document.getElementById('accountTerritoryBody'),
            rows,
            renderRow,
            totalColumnCount: 7,
            saveViewColumns: [1, 2, 3, 4, 5, 6],
            baseOrder: [[5, 'desc']],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, responsivePriority: 1 }
            ],
            pageKey: 'AccountTerritory',
            filters: [
                { selectId: 'filterTerritoryStatus', pick: (r) => r.status },
                { selectId: 'filterTerritorySource', pick: (r) => r.assignmentSource }
            ],
            filterHostId: 'inlineFilterHostTerritory',
            filterCollapseId: 'inlineFilterCollapseTerritory',
            applyButtonId: 'btnTerritoryFilterApply',
            resetButtonId: 'btnTerritoryFilterReset',
            skeletonSelector: '#territory-skeleton-loader',
            addNewText: null,
            onAddNew: null,
            rowActions: null,
            l10n: () => L
        });
        if (!section) console.warn('[AccountDetails] Territory table could not be initialised.');
    };

    void init();
})();
