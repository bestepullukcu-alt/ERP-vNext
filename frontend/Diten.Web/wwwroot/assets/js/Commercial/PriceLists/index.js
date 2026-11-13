/**
 * WP-VW-W6 · W6-WEB-b — screen D · the price lists on the Golden Compact list standard (server mode: the CRM pages them;
 * no column order). The badge is the CRM's (Draft / In review / Planned / In force / Past — the phase read on the day).
 * The quick view shows the list's head, its approval and its numbers; the details page has the items and the difference.
 */
'use strict';

const PriceListsList = (function () {
    let list = null;
    let quickToken = 0;
    const dtTableEl = document.querySelector('.datatables-pricelists');
    const root = document.getElementById('pr-root');
    const U = window.PreOrdersUi;
    const K = window.PricingCore;
    const PU = window.PricingUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const flag = (n) => !!root && root.getAttribute('data-' + n) === 'true';
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const esc = (s) => U.esc(s);
    const endpoint = PU.api + '/price-lists';

    // control 0 · name 1 · company 2 · country 3 · currency 4 · kind 5 · validity 6 · status 7 · items 8 · actions 9
    const DATA_COLUMNS = [1, 2, 3, 4, 5, 6, 7, 8];
    const filterFields = [
        { id: 'filterLegalEntity', key: 'legalEntity', kind: 'single' },
        { id: 'filterCountry', key: 'country', kind: 'single' },
        { id: 'filterCurrency', key: 'currency', kind: 'single' },
        { id: 'filterKind', key: 'kind', kind: 'single' },
        { id: 'filterStatus', key: 'status', kind: 'single' }
    ];
    const seen = { country: {}, currency: {} };

    const loadLookupOptions = async ({ fillSelect }) => {
        const companies = await PU.loadCompanies();
        fillSelect(byId('filterLegalEntity'), companies.map((c) => ({ value: c.id, text: c.text + (c.sub ? ' · ' + c.sub : '') })), { showAllText: L().CompanyAll });
        fillSelect(byId('filterCountry'), [], { showAllText: L().CountryAll });
        fillSelect(byId('filterCurrency'), [], { showAllText: L().CurrencyAll });
        fillSelect(byId('filterKind'), K.KINDS.map((k) => ({ value: k, text: PU.kind(k) })), { showAllText: L().KindAll });
        fillSelect(byId('filterStatus'), Object.keys(K.BADGES).map((b) => ({ value: b, text: L()[K.BADGES[b].key] || b })), { showAllText: L().StatusAll });
    };

    const refill = (id, map, allKey) => {
        const el = byId(id);
        if (!el) return;
        const current = window.jQuery ? window.jQuery(el).val() : el.value;
        el.innerHTML = '<option value="">' + esc(L()[allKey] || '') + '</option>' + Object.keys(map).sort().map((k) => '<option value="' + esc(k) + '">' + esc(k) + '</option>').join('');
        if (current && !map[current]) { el.insertAdjacentHTML('beforeend', '<option value="' + esc(current) + '">' + esc(current) + '</option>'); }
        if (window.jQuery) { window.jQuery(el).val(current || '').trigger('change.select2'); } else { el.value = current || ''; }
    };
    const onListResponse = (json) => {
        const items = (json && json.data && Array.isArray(json.data.items)) ? json.data.items : [];
        let c = false, m = false;
        items.forEach((r) => {
            if (r.countryCode && !seen.country[r.countryCode]) { seen.country[r.countryCode] = 1; c = true; }
            if (r.currency && !seen.currency[r.currency]) { seen.currency[r.currency] = 1; m = true; }
        });
        if (c) refill('filterCountry', seen.country, 'CountryAll');
        if (m) refill('filterCurrency', seen.currency, 'CurrencyAll');
    };

    function drawEmptyState() {
        const api = this && this.api ? this.api() : null;
        if (!api || api.rows({ page: 'current' }).count() > 0) return;
        const cell = dtTableEl.querySelector('tbody td.dt-empty');
        if (cell) { cell.innerHTML = '<div class="pr-empty"><i class="bx bx-purchase-tag-alt"></i><strong>' + esc(L().EmptyTitle) + '</strong><span class="text-muted">' + esc(L().EmptyText) + '</span></div>'; }
    }

    const quickHtml = (l) => '<div class="px-4 py-3 border-bottom"><div class="d-flex flex-wrap gap-2 mb-2">' + PU.badge(l.badge) + '<span class="badge bg-label-primary">' + esc(PU.kind(l.kind)) + '</span></div>'
        + '<dl class="row g-2 mb-0">'
        + [['ColCompany', l.legalEntityName || l.legalEntityCode], ['ColCountry', l.countryCode], ['ColCurrency', l.currency], ['ColValidity', PU.validity(l.validFrom, l.effectiveValidTo || l.validTo)],
            ['ColItems', U.T('ItemsCount', l.itemCount || 0)], ['ColVersion', 'v' + (l.listVersion || 1)]]
            .filter((r) => r[1]).map((r) => '<div class="col-6"><dt class="small text-muted fw-normal">' + esc(L()[r[0]] || '') + '</dt><dd class="mb-0 text-heading">' + U.bidi(r[1]) + '</dd></div>').join('')
        + '</dl></div><div class="px-4 py-3"><h6 class="mb-2">' + esc(L().ApprovalTitle) + '</h6>' + PU.approvalHtml(l) + '</div>';

    const populateQuickView = (row) => {
        if (!row) return;
        const token = ++quickToken;
        byId('oc-subtitle').innerHTML = U.bidi(row.name || '');
        byId('oc-details-list').innerHTML = quickHtml(row);
        byId('oc-btn-edit').setAttribute('href', '/Commercial/Pricing/PriceLists/Details/' + encodeURIComponent(row.id));
        fetch(endpoint + '/' + encodeURIComponent(row.id), { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() })
            .then((r) => (r.ok ? r.json() : null)).catch(() => null)
            .then((b) => { const d = b && b.data; if (d && token === quickToken) { byId('oc-details-list').innerHTML = quickHtml(d); } });
    };

    const init = async () => {
        if (!dtTableEl || !U || !K || !PU || !window.DitenDataTable) return;
        const canCreate = flag('can-create');
        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            ajax: { url: endpoint, type: 'GET' },
            export: { mode: 'server', url: endpoint + '/export', fileName: 'price-lists' },
            onResponse: onListResponse,
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateQuickView, actionKey: 'quickView' },
            toolbar: {
                addNewText: canCreate ? L().NewList : '',
                onAddNew: canCreate ? () => { window.location.href = '/Commercial/Pricing/PriceLists/Create'; } : undefined,
                exportColumns: DATA_COLUMNS, colvisColumns: DATA_COLUMNS, exportPermitted: flag('can-export')
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Commercial', pageKey: 'PriceLists', saveViewColumnIndexes: DATA_COLUMNS, defaultVisibleColumnIndexes: DATA_COLUMNS, baseOrder: [] },
            config: {
                order: [],
                searching: true,
                drawCallback: drawEmptyState,
                columns: [
                    { data: 'id', name: 'control' }, { data: 'name', name: 'name' }, { data: 'legalEntityName', name: 'legalEntityName' },
                    { data: 'countryCode', name: 'countryCode' }, { data: 'currency', name: 'currency' }, { data: 'kind', name: 'kind' },
                    { data: 'validFrom', name: 'validFrom' }, { data: 'badge', name: 'badge' }, { data: 'itemCount', name: 'itemCount' }, { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: '_all', orderable: false },
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 'all', render: (d, type, full) => (type === 'display' ? '<a class="fw-medium text-heading" href="/Commercial/Pricing/PriceLists/Details/' + encodeURIComponent(full.id) + '">' + U.bidi(full.name || '') + '</a><small class="d-block text-muted">v' + esc(full.listVersion || 1) + '</small>' : (full.name || '')) },
                    { targets: 2, responsivePriority: 3, render: (d, type, full) => (type === 'display' ? U.bidi(full.legalEntityName || full.legalEntityCode || '') : (full.legalEntityName || '')) },
                    { targets: 3, render: (d, type, full) => esc(full.countryCode || '') },
                    { targets: 4, render: (d, type, full) => esc(full.currency || '') },
                    { targets: 5, render: (d, type, full) => esc(PU.kind(full.kind)) },
                    { targets: 6, render: (d, type, full) => esc(PU.validity(full.validFrom, full.effectiveValidTo || full.validTo)) },
                    { targets: 7, responsivePriority: 2, render: (d, type, full) => (type === 'display' ? PU.badge(full.badge) : (L()[K.badgeOf(full.badge).key] || full.badge || '')) },
                    { targets: 8, render: (d, type, full) => esc(U.T('ItemsCount', full.itemCount || 0)) },
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

    return { init: init, quickHtml: quickHtml };
})();

document.addEventListener('DOMContentLoaded', () => PriceListsList.init());
