/**
 * WP-VW-W6 · W6-WEB-b — screen D · the interim FX rates (D14) on the Golden Compact list standard (server mode). "New rate"
 * for the create key; the quick view's button opens the correction form for the update key (the CRM writes the change
 * into the history; an order's own snapshot is never touched).
 */
'use strict';

const FxRatesList = (function () {
    const dtTableEl = document.querySelector('.datatables-fxrates');
    const root = document.getElementById('pr-root');
    const U = window.PreOrdersUi;
    const PU = window.PricingUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const flag = (n) => !!root && root.getAttribute('data-' + n) === 'true';
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const esc = (s) => U.esc(s);
    const endpoint = PU.api + '/fx-rates';
    let companies = [];

    // control 0 · company 1 · pair 2 · day 3 · rate 4 · source 5 · updated 6 · actions 7
    const DATA_COLUMNS = [1, 2, 3, 4, 5, 6];
    const filterFields = [
        { id: 'filterLegalEntity', key: 'legalEntity', kind: 'single' },
        { id: 'filterFromCurrency', key: 'fromCurrency', kind: 'single' },
        { id: 'filterToCurrency', key: 'toCurrency', kind: 'single' },
        { id: 'pr-from', key: 'from', kind: 'text' },
        { id: 'pr-to', key: 'to', kind: 'text' }
    ];
    const seen = { from: {}, to: {} };

    const loadLookupOptions = async ({ fillSelect }) => {
        companies = await PU.loadCompanies();
        fillSelect(byId('filterLegalEntity'), companies.map((c) => ({ value: c.id, text: c.text + (c.sub ? ' · ' + c.sub : '') })), { showAllText: L().CompanyAll });
        fillSelect(byId('filterFromCurrency'), [], { showAllText: L().CurrencyAll });
        fillSelect(byId('filterToCurrency'), [], { showAllText: L().CurrencyAll });
    };

    const refill = (id, map) => {
        const el = byId(id);
        if (!el) return;
        const current = el.value;
        el.innerHTML = '<option value="">' + esc(L().CurrencyAll) + '</option>' + Object.keys(map).sort().map((k) => '<option value="' + esc(k) + '">' + esc(k) + '</option>').join('');
        el.value = current || '';
        if (window.jQuery) window.jQuery(el).trigger('change.select2');
    };
    const onListResponse = (json) => {
        const items = (json && json.data && Array.isArray(json.data.items)) ? json.data.items : [];
        let a = false, b = false;
        items.forEach((r) => {
            if (r.fromCurrency && !seen.from[r.fromCurrency]) { seen.from[r.fromCurrency] = 1; a = true; }
            if (r.toCurrency && !seen.to[r.toCurrency]) { seen.to[r.toCurrency] = 1; b = true; }
        });
        if (a) refill('filterFromCurrency', seen.from);
        if (b) refill('filterToCurrency', seen.to);
    };

    function drawEmptyState() {
        const api = this && this.api ? this.api() : null;
        if (!api || api.rows({ page: 'current' }).count() > 0) return;
        const cell = dtTableEl.querySelector('tbody td.dt-empty');
        if (cell) { cell.innerHTML = '<div class="pr-empty"><i class="bx bx-transfer-alt"></i><strong>' + esc(L().EmptyTitle) + '</strong><span class="text-muted">' + esc(L().EmptyText) + '</span></div>'; }
    }

    const pair = (r) => esc((r.fromCurrency || '') + ' → ' + (r.toCurrency || ''));
    const rate = (r) => esc(window.PreOrdersCore.num(r.rate, U.culture(), 6));
    /** G-3 (W6-FIX-d) — the last writer by NAME (the CRM resolves it); no name → "—", never the user id. */
    const changedBy = (r) => U.bidi((r.updatedBy ? r.updatedByName : r.createdByName) || '—');

    const populateQuickView = (row) => {
        if (!row) return;
        byId('oc-subtitle').innerHTML = pair(row) + ' · ' + esc(U.day(row.rateDate));
        byId('oc-details-list').innerHTML = '<div class="px-4 py-3"><dl class="row g-2 mb-0">'
            + [['ColCompany', U.bidi(PU.companyName(companies, row.legalEntityId) || row.legalEntityId)], ['ColPair', pair(row)], ['ColRateDate', esc(U.day(row.rateDate))],
                ['ColRate', rate(row)], ['ColSource', U.bidi(row.source || '')], ['ColUpdated', esc(U.moment(row.updatedAt || row.createdAt)) + ' · ' + changedBy(row)]]
                .map((x) => '<div class="col-6"><dt class="small text-muted fw-normal">' + esc(L()[x[0]]) + '</dt><dd class="mb-0 text-heading">' + x[1] + '</dd></div>').join('') + '</dl></div>';
        const edit = byId('oc-btn-edit');
        edit.setAttribute('href', '/Commercial/Pricing/FxRates/Edit/' + encodeURIComponent(row.id));
        edit.classList.toggle('d-none', !flag('can-update'));
    };

    const enhanceRange = () => ['pr-from', 'pr-to'].forEach((id) => {
        const n = byId(id);
        if (n && typeof n.flatpickr === 'function' && !n._flatpickr) n.flatpickr({ dateFormat: 'Y-m-d', altInput: true, altFormat: L().DateFormat || 'Y-m-d', allowInput: true });
    });

    const init = async () => {
        if (!dtTableEl || !U || !PU || !window.DitenDataTable) return;
        enhanceRange();
        companies = await fetch(PU.api + '/lookups/legal-entities', { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() })
            .then((r) => (r.ok ? r.json() : null)).then((b) => ((b && b.data && b.data.items) || [])).catch(() => []);
        const canCreate = flag('can-create');
        await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            ajax: { url: endpoint, type: 'GET' },
            onResponse: onListResponse,
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateQuickView, actionKey: 'quickView' },
            toolbar: {
                addNewText: canCreate ? L().NewRate : '',
                onAddNew: canCreate ? () => { window.location.href = '/Commercial/Pricing/FxRates/Create'; } : undefined,
                // MOD-0156 §14 has no export key for the rates: no file entries (Copy stays)
                exportColumns: DATA_COLUMNS, colvisColumns: DATA_COLUMNS, exportPermitted: false
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Commercial', pageKey: 'FxRates', saveViewColumnIndexes: DATA_COLUMNS, defaultVisibleColumnIndexes: DATA_COLUMNS, baseOrder: [] },
            config: {
                order: [],
                searching: false,
                drawCallback: drawEmptyState,
                columns: [{ data: 'id', name: 'control' }, { data: 'legalEntityId', name: 'legalEntityId' }, { data: 'fromCurrency', name: 'pair' }, { data: 'rateDate', name: 'rateDate' },
                    { data: 'rate', name: 'rate' }, { data: 'source', name: 'source' }, { data: 'updatedAt', name: 'updatedAt' }, { data: 'id', name: 'action' }],
                columnDefs: [
                    { targets: '_all', orderable: false },
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 'all', render: (d, type, full) => U.bidi(PU.companyName(companies, full.legalEntityId) || full.legalEntityId || '') },
                    { targets: 2, responsivePriority: 2, render: (d, type, full) => pair(full) },
                    { targets: 3, render: (d, type, full) => esc(U.day(full.rateDate)) },
                    { targets: 4, render: (d, type, full) => rate(full) },
                    { targets: 5, render: (d, type, full) => U.bidi(full.source || '') },
                    { targets: 6, render: (d, type, full) => '<small>' + esc(U.moment(full.updatedAt || full.createdAt)) + '</small><small class="d-block text-muted">' + changedBy(full) + '</small>' },
                    {
                        targets: -1, title: L().Actions, searchable: false, className: 'cell-fit all',
                        render: (d, type, full) => window.DitenDataTable.renderActions([
                            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-id': full.id, title: L().QuickView, 'aria-label': L().QuickView } }
                        ])
                    }
                ]
            }
        });
    };

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', () => FxRatesList.init());
