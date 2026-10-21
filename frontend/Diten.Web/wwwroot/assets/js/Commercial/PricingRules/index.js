/**
 * WP-VW-W6 · W6-WEB-b — screen D · the MF / discount rules on the Golden Compact list standard (server mode). Each pair
 * is the rule's soft · hard; the MF names the CRM's own ratio of each term (the DTO's ratioPct — not computed here).
 */
'use strict';

const PricingRulesList = (function () {
    let quickToken = 0;
    const dtTableEl = document.querySelector('.datatables-pricingrules');
    const root = document.getElementById('pr-root');
    const U = window.PreOrdersUi;
    const K = window.PricingCore;
    const PU = window.PricingUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const esc = (s) => U.esc(s);
    const endpoint = PU.api + '/rules';

    // control 0 · product 1 · discount 2 · mf 3 · combined 4 · quantity 5 · validity 6 · status 7 · version 8 · actions 9
    const DATA_COLUMNS = [1, 2, 3, 4, 5, 6, 7, 8];
    const filterFields = [
        { id: 'filterLegalEntity', key: 'legalEntity', kind: 'single' },
        { id: 'filterSku', key: 'sku', kind: 'single' },
        { id: 'filterStatus', key: 'status', kind: 'single' }
    ];

    const loadLookupOptions = async ({ fillSelect }) => {
        const companies = await PU.loadCompanies();
        fillSelect(byId('filterLegalEntity'), companies.map((c) => ({ value: c.id, text: c.text + (c.sub ? ' · ' + c.sub : '') })), { showAllText: L().CompanyAll });
        fillSelect(byId('filterSku'), [], { showAllText: L().AllOption });
        fillSelect(byId('filterStatus'), Object.keys(K.RULE_STATUSES).map((s) => ({ value: s, text: L()[K.RULE_STATUSES[s].key] || s })), { showAllText: L().StatusAll });
    };

    const productCell = (r) => '<a class="fw-medium text-heading" href="/Commercial/Pricing/Rules/Details/' + encodeURIComponent(r.id) + '">' + U.bidi(r.skuName || r.skuCode || '') + '</a><small class="d-block text-muted">' + U.bidi(r.legalEntityName || '') + '</small>';

    function drawEmptyState() {
        const api = this && this.api ? this.api() : null;
        if (!api || api.rows({ page: 'current' }).count() > 0) return;
        const cell = dtTableEl.querySelector('tbody td.dt-empty');
        if (cell) { cell.innerHTML = '<div class="pr-empty"><i class="bx bx-slider-alt"></i><strong>' + esc(L().EmptyTitle) + '</strong><span class="text-muted">' + esc(L().EmptyText) + '</span></div>'; }
    }

    const quickHtml = (r) => {
        const lim = PU.limitsHtml(r);
        return '<div class="px-4 py-3 border-bottom"><div class="mb-2">' + PU.ruleStatus(r.status) + '</div><dl class="row g-2 mb-0">'
            + [['ColMaxDiscount', lim.discount], ['ColMaxMf', lim.mf], ['ColMaxCombined', lim.combined], ['ColQuantity', lim.quantity]]
                .map((x) => '<div class="col-6"><dt class="small text-muted fw-normal">' + esc(L()[x[0]]) + '</dt><dd class="mb-0">' + x[1] + '</dd></div>').join('') + '</dl></div>'
            + '<div class="px-4 py-3"><h6 class="mb-2">' + esc(L().ApprovalTitle) + '</h6>' + PU.approvalHtml(r) + '</div>';
    };

    const populateQuickView = (row) => {
        if (!row) return;
        const token = ++quickToken;
        byId('oc-subtitle').innerHTML = U.bidi(row.skuName || row.skuCode || '');
        byId('oc-details-list').innerHTML = quickHtml(row);
        byId('oc-btn-edit').setAttribute('href', '/Commercial/Pricing/Rules/Details/' + encodeURIComponent(row.id));
        fetch(endpoint + '/' + encodeURIComponent(row.id), { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() })
            .then((r) => (r.ok ? r.json() : null)).catch(() => null)
            .then((b) => { const d = b && b.data && b.data.rule; if (d && token === quickToken) { byId('oc-details-list').innerHTML = quickHtml(d); } });
    };

    const enhanceSkuFilter = () => {
        const el = byId('filterSku');
        if (!el || !window.jQuery || !window.jQuery.fn.select2) return;
        if (window.jQuery(el).hasClass('select2-hidden-accessible')) window.jQuery(el).select2('destroy');
        el.setAttribute('data-placeholder', L().ProductLabel || '');
        PU.skuPicker(el);
    };

    const init = async () => {
        if (!dtTableEl || !U || !K || !PU || !window.DitenDataTable) return;
        const canCreate = !!root && root.getAttribute('data-can-create') === 'true';
        await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            ajax: { url: endpoint, type: 'GET' },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateQuickView, actionKey: 'quickView' },
            toolbar: {
                addNewText: canCreate ? L().NewRule : '',
                onAddNew: canCreate ? () => { window.location.href = '/Commercial/Pricing/Rules/Create'; } : undefined,
                // MOD-0156 §14 has no export key for the rules: no file entries (Copy stays)
                exportColumns: DATA_COLUMNS, colvisColumns: DATA_COLUMNS, exportPermitted: false
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Commercial', pageKey: 'PricingRules', saveViewColumnIndexes: DATA_COLUMNS, defaultVisibleColumnIndexes: DATA_COLUMNS, baseOrder: [] },
            config: {
                order: [],
                searching: true,
                drawCallback: drawEmptyState,
                columns: [
                    { data: 'id', name: 'control' }, { data: 'skuName', name: 'skuName' }, { data: 'maxDiscountPct', name: 'maxDiscountPct' }, { data: 'maxMf', name: 'maxMf' },
                    { data: 'maxCombinedPct', name: 'maxCombinedPct' }, { data: 'quantity', name: 'quantity' }, { data: 'validFrom', name: 'validFrom' },
                    { data: 'status', name: 'status' }, { data: 'ruleVersion', name: 'ruleVersion' }, { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: '_all', orderable: false },
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 'all', render: (d, type, full) => (type === 'display' ? productCell(full) : (full.skuName || full.skuCode || '')) },
                    { targets: 2, render: (d, type, full) => PU.limitsHtml(full).discount },
                    { targets: 3, render: (d, type, full) => PU.limitsHtml(full).mf },
                    { targets: 4, render: (d, type, full) => PU.limitsHtml(full).combined },
                    { targets: 5, render: (d, type, full) => PU.limitsHtml(full).quantity },
                    { targets: 6, render: (d, type, full) => esc(full.validFrom || full.validTo ? PU.validity(full.validFrom, full.validTo) : L().AlwaysValid) },
                    { targets: 7, responsivePriority: 2, render: (d, type, full) => (type === 'display' ? PU.ruleStatus(full.status) : (L()[K.ruleStatusOf(full.status).key] || full.status || '')) },
                    { targets: 8, render: (d, type, full) => esc('v' + (full.ruleVersion || 1)) },
                    {
                        targets: -1, title: L().Actions, searchable: false, className: 'cell-fit all',
                        render: (d, type, full) => window.DitenDataTable.renderActions([
                            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-id': full.id, title: L().QuickView, 'aria-label': L().QuickView } }
                        ])
                    }
                ]
            }
        });
        enhanceSkuFilter();
    };

    return { init: init, quickHtml: quickHtml };
})();

document.addEventListener('DOMContentLoaded', () => PricingRulesList.init());
