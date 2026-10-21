/**
 * WP-VW-W6 · W6-WEB-a — B: the pre-orders on the Golden Compact list standard (mockup W6 B).
 *
 * THE LIST IS THE SHARED COMPONENT (data_mode: server): Save View, column state, the inline filter, paging, the skeleton,
 * the export and the quick view's open / close come from DitenDataTable.createList. The factory sends start / length /
 * draw + each applied filter by its key to the same-origin proxy, which turns it into the CRM's query (newest first: NO
 * column sorts — an order or a total across currencies is never made, AC-UI-2). Each amount names its currency; the local
 * equivalent column (off by default) shows the FX snapshot's amount only where there is one.
 *
 * THE QUICK VIEW reads the order (lines, totals, timeline) and, for sales operations (record-depot-response) on a
 * transmitted order, records the depot's answer: delivered, or partial with the delivered boxes per line + a note (D9).
 * "Onayımı bekleyenler" carries the caller's count. Every rule is PreOrdersCore's; every word window.L10n (7 languages).
 */
'use strict';

const PreOrdersList = (function () {
    let list = null;
    let quickToken = 0;
    let quickOrder = null;

    const dtTableEl = document.querySelector('.datatables-preorders');
    const root = document.getElementById('po-root');
    const endpoint = '/Commercial/PreOrders/api';
    const C = window.PreOrdersCore;
    const U = window.PreOrdersUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const flag = (name) => !!root && root.getAttribute('data-' + name) === 'true';
    const $ = window.jQuery;
    const esc = (s) => (U ? U.esc(s) : String(s || ''));
    const valueOf = (id) => { const n = byId(id); return n ? String(n.value || '') : ''; };
    const onChange = (id, fn) => { const n = byId(id); if (!n) { return; } if ($) { $(n).on('change', fn); } else { n.addEventListener('change', fn); } };
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    // control 0 · code 1 · date 2 · pharmacy 3 · depot 4 · rep 5 · lines 6 · total 7 · local 8 · effective 9 · status 10 · rule 11 · actions 12
    const DATA_COLUMNS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
    const defaultVisible = () => DATA_COLUMNS.filter((c) => c !== 8 && (flag('sees-team') || c !== 5));
    const EXPORT_COLUMNS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    // ── the cells ────────────────────────────────────────────────────────────────────────────────────────────
    const totalCell = (row) => (row.priceMode === 'priced'
        ? '<span class="text-heading text-nowrap">' + esc(U.money(row.gross, row.currency)) + '</span>'
        : '<span class="text-muted text-nowrap"><i class="bx bx-package me-1"></i>' + esc(L().QuantityModeShort) + '</span>');
    const localCell = (row) => (row.localGross != null && row.localCurrencyCode && row.localCurrencyCode !== row.currency
        ? '<span class="text-muted text-nowrap">≈ ' + esc(U.money(row.localGross, row.localCurrencyCode)) + '</span>' : '<span class="text-muted">—</span>');
    const pharmacyCell = (row) => '<div class="d-flex flex-column"><span class="text-heading">' + U.bidi(row.pharmacyName || '') + '</span>'
        + (row.pharmacyCity ? '<small class="text-muted">' + U.bidi(row.pharmacyCity) + '</small>' : '') + '</div>';
    const statusCell = (row) => '<div class="d-flex flex-column gap-1 align-items-start">' + U.statusBadge(row.status) + U.depotResponseBadge(row.depotResponseStatus)
        + (row.awaitingMe ? '<small class="text-warning text-nowrap"><i class="bx bx-user-check me-1"></i>' + esc(L().AwaitingYou) + '</small>' : '') + '</div>';
    const ruleCell = (row) => (row.outcome === 'soft' || row.outcome === 'hard'
        ? '<i class="bx ' + C.outcomeOf(row.outcome).icon + ' text-' + C.outcomeOf(row.outcome).tone + ' fs-5" title="' + esc(window.CommercialRules ? window.CommercialRules.outcome(row.outcome) : '') + '" aria-label="' + esc(window.CommercialRules ? window.CommercialRules.outcome(row.outcome) : '') + '"></i>' : '');

    // ── the filter ───────────────────────────────────────────────────────────────────────────────────────────
    const reps = {};
    const countries = {};
    const filterFields = [
        { id: 'filterPeriod', key: 'period', kind: 'single' },
        { id: 'po-from', key: 'from', kind: 'text' },
        { id: 'po-to', key: 'to', kind: 'text' },
        { id: 'filterStatus', key: 'status', kind: 'single' },
        { id: 'filterRep', key: 'rep', kind: 'single' },
        { id: 'filterPharmacy', key: 'pharmacy', kind: 'single' },
        { id: 'filterDepot', key: 'depot', kind: 'single' },
        { id: 'filterSku', key: 'sku', kind: 'single' },
        { id: 'filterCountry', key: 'country', kind: 'single' },
        { id: 'filterAwaiting', key: 'awaiting', kind: 'single' }
    ].filter((f) => !!byId(f.id));

    let awaitingCount = 0;
    const awaitingText = () => U.T('AwaitingOption', C.num(awaitingCount, U.culture(), 0));
    const loadLookupOptions = async ({ fillSelect }) => {
        fillSelect(byId('filterPeriod'), [{ value: 'last7', text: L().PeriodLast7 }, { value: 'last30', text: L().PeriodLast30 }, { value: 'month', text: L().PeriodMonth }, { value: 'custom', text: L().PeriodCustom }],
            { showAllText: L().PeriodAll });
        fillSelect(byId('filterStatus'), C.STATUS_CODES.map((s) => ({ value: s, text: L()[C.STATUSES[s].key] || s })), { showAllText: L().StatusAll });
        if (byId('filterRep')) { fillSelect(byId('filterRep'), [], { showAllText: L().RepAll }); }
        ['filterPharmacy', 'filterDepot', 'filterSku'].forEach((id) => fillSelect(byId(id), [], { showAllText: L().AllOption }));
        fillSelect(byId('filterCountry'), [], { showAllText: L().CountryAll });
        if (byId('filterAwaiting')) { fillSelect(byId('filterAwaiting'), [{ value: 'true', text: awaitingText() }], { showAllText: L().AwaitingAll }); }
    };

    /** A select whose options come from the rows the list has shown (a value the options lack is kept as its own option). */
    const refill = (id, map, allKey) => {
        const el = byId(id);
        if (!el) { return; }
        const current = $ ? $(el).val() : el.value;
        el.innerHTML = '';
        const add = (value, text) => { const o = document.createElement('option'); o.value = value; o.textContent = text; el.appendChild(o); };
        add('', L()[allKey] || '');
        Object.keys(map).sort((a, b) => String(map[a]).localeCompare(String(map[b]), U.culture())).forEach((k) => add(k, map[k]));
        if (current && !map[current]) { add(current, current); }
        if ($) { $(el).val(current || '').trigger('change.select2'); } else { el.value = current || ''; }
    };

    const onListResponse = (json) => {
        const items = (json && json.data && Array.isArray(json.data.items)) ? json.data.items : [];
        let repsAdded = false;
        let countriesAdded = false;
        items.forEach((row) => {
            if (row && row.repUserId && !reps[row.repUserId]) { reps[row.repUserId] = row.repName || row.repUserId; repsAdded = true; }
            if (row && row.countryCode && !countries[row.countryCode]) { countries[row.countryCode] = row.countryCode; countriesAdded = true; }
        });
        if (repsAdded) { refill('filterRep', reps, 'RepAll'); }
        if (countriesAdded) { refill('filterCountry', countries, 'CountryAll'); }
    };

    /** Pharmacy / depot / product: search pickers over the CRM accounts / the product master (typed text, 20 rows). */
    const enhanceSearchPickers = () => {
        if (!$ || !$.fn.select2) { return; }
        document.querySelectorAll('select[data-po-search]').forEach((el) => {
            const $s = $(el);
            const source = el.getAttribute('data-po-search');
            if ($s.hasClass('select2-hidden-accessible')) { $s.select2('destroy'); }
            $s.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                allowClear: true,
                width: 'element',
                minimumInputLength: 2,
                language: { inputTooShort: () => L().SearchHint || '', noResults: () => L().NoMatch || '', searching: () => L().Searching || '' },
                ajax: {
                    delay: 250,
                    transport: (params, success, failure) => U.request('GET', endpoint + '/lookups/' + source + '?q=' + encodeURIComponent((params.data && params.data.term) || '')).then((r) => (r.ok ? success(r.body) : failure())),
                    processResults: (body) => ({ results: (((body || {}).data || {}).items || []).map((o) => ({ id: o.id, text: o.text + (o.sub ? ' · ' + o.sub : '') })) })
                }
            });
        });
    };

    const syncRange = () => {
        const custom = valueOf('filterPeriod') === 'custom';
        ['filterRangeFrom', 'filterRangeTo'].forEach((id) => byId(id)?.classList.toggle('d-none', !custom));
        if (!custom) {
            ['po-from', 'po-to'].forEach((id) => { const n = byId(id); if (n && n.value) { if (n._flatpickr) { n._flatpickr.clear(); } n.value = ''; } });
        }
    };
    const enhanceRange = () => {
        const altFormat = L().DateFormat || 'Y-m-d';
        ['po-from', 'po-to'].forEach((id) => {
            const n = byId(id);
            if (!n || typeof n.flatpickr !== 'function' || n._flatpickr) { return; }
            n.flatpickr({ monthSelectorType: 'static', dateFormat: 'Y-m-d', altInput: true, altFormat: altFormat, allowInput: true });
            window.DitenDateField?.guardAgainstSilentMisparse?.(n._flatpickr, 'altFormat');
            const icon = n.closest ? n.closest('.diten-field')?.querySelector('.diten-field-icon') : null;
            icon?.addEventListener('click', () => n._flatpickr?.open());
        });
    };
    /** A custom range the proxy would refuse is never sent: the capture-phase guard runs before the factory's Apply. */
    const guardApply = (event) => {
        const period = valueOf('filterPeriod');
        if (period !== 'custom') { syncRange(); }
        const bad = C.customRangeError(period, valueOf('po-from'), valueOf('po-to'));
        ['po-from', 'po-to'].forEach((id) => { const n = byId(id); n?.classList.toggle('is-invalid', !!bad); n?._flatpickr?.altInput?.classList.toggle('is-invalid', !!bad); });
        if (!bad) { return; }
        event.preventDefault();
        event.stopImmediatePropagation();
        U.toast(L()[bad] || '', 'warning');
    };

    const loadAwaiting = () => {
        if (!flag('can-approve')) { return; }
        fetch(endpoint + '/awaiting-count', { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() })
            .then((r) => r.json().then((b) => ({ ok: r.ok, status: r.status, body: b })).catch(() => ({ ok: false, status: r.status, body: null })))
            .catch(() => ({ ok: false, status: 0, body: null }))
            .then((r) => {
            const d = U.data(r);
            awaitingCount = d && Number.isFinite(Number(d.count)) ? Number(d.count) : 0;
            const option = document.querySelector('#filterAwaiting option[value="true"]');
            if (option) { option.textContent = awaitingText(); if ($) { $('#filterAwaiting').trigger('change.select2'); } }
        });
    };

    // ── the empty state ──────────────────────────────────────────────────────────────────────────────────────
    function drawEmptyState() {
        const api = this && this.api ? this.api() : null;
        if (!api || api.rows({ page: 'current' }).count() > 0) { return; }
        const cell = dtTableEl.querySelector('tbody td.dt-empty');
        if (cell) { cell.innerHTML = '<div class="po-empty"><i class="bx bx-cart-alt"></i><strong>' + esc(L().EmptyTitle) + '</strong><span class="text-muted">' + esc(L().EmptyText) + '</span></div>'; }
    }

    // ── the quick view ───────────────────────────────────────────────────────────────────────────────────────
    const quickHtml = (o) => '<div class="px-4 py-3 border-bottom">' + U.facts(o) + '</div>'
        + '<div class="px-4 py-3 border-bottom">' + U.linesTable(o) + '<div class="mt-2">' + U.totals(o) + '</div></div>'
        + '<div class="px-4 py-3"><h6 class="mb-2">' + esc(L().TimelineTitle) + '</h6>' + U.timelineHtml(o) + '</div>';

    const depotForm = (o) => {
        const form = byId('po-depot-form');
        const save = byId('po-dr-save');
        const show = !!form && !!(o && o.can && o.can.recordDepotResponse) && o.status === 'transmitted';
        form?.classList.toggle('d-none', !show);
        save?.classList.toggle('d-none', !show);
        if (!show) { return; }
        const current = (o.depotResponse && o.depotResponse.status) || '';
        ['delivered', 'partial'].forEach((s) => { const r = byId('po-dr-' + s); if (r) { r.checked = s === current; } });
        byId('po-dr-note').value = (o.depotResponse && o.depotResponse.note) || '';
        byId('po-dr-alert').classList.add('d-none');
        const partial = new Map(((o.depotResponse && o.depotResponse.partialLines) || []).map((p) => [p.lineNo, p.deliveredQuantity]));
        byId('po-dr-lines').innerHTML = (o.lines || []).map((l) => '<div class="d-flex align-items-center gap-2 mb-2"><label class="flex-grow-1 small" for="po-dr-q-' + l.lineNo + '">' + U.bidi((l.sku || {}).name || '') + ' <span class="text-muted">' + esc(U.T('OfQuantity', C.num(l.quantity, U.culture(), 0))) + '</span></label>'
            + '<div class="diten-field po-dr-qty"><i class="bx bx-package diten-field-icon" aria-hidden="true"></i><input type="number" min="0" max="' + Number(l.quantity) + '" class="form-control form-control-sm" id="po-dr-q-' + l.lineNo + '" data-line="' + l.lineNo + '" value="' + esc(partial.has(l.lineNo) ? partial.get(l.lineNo) : '') + '" /></div></div>').join('');
        byId('po-dr-lines').classList.toggle('d-none', current !== 'partial');
    };

    const saveDepotResponse = async () => {
        const o = quickOrder;
        if (!o) { return; }
        const status = (document.querySelector('input[name="po-dr-status"]:checked') || {}).value || '';
        const alert = byId('po-dr-alert');
        const partialLines = status === 'partial'
            ? Array.from(document.querySelectorAll('#po-dr-lines input[data-line]')).map((n) => ({ lineNo: Number(n.getAttribute('data-line')), deliveredQuantity: n.value === '' ? null : Number(n.value) }))
            : [];
        if (!status || partialLines.some((p) => p.deliveredQuantity == null || p.deliveredQuantity < 0)) {
            alert.textContent = window.CommercialRules?.error('pre_order_depot_response_invalid') || L().Err_generic;
            alert.classList.remove('d-none');
            return;
        }
        const btn = byId('po-dr-save');
        btn.disabled = true;
        const r = await U.request('POST', endpoint + '/orders/' + encodeURIComponent(o.id) + '/depot-response', { status: status, partialLines: partialLines, note: byId('po-dr-note').value.trim() || null, expectedVersion: o.version });
        btn.disabled = false;
        if (!r.ok) { alert.textContent = U.errorText(r); alert.classList.remove('d-none'); return; }
        U.toast(L().DepotResponseSaved, 'success');
        bootstrap.Offcanvas.getOrCreateInstance(byId('offcanvasDetailsPreview')).hide();
        list?.dt?.ajax?.reload(null, false);
    };

    const renderQuickView = (row, o) => {
        const sub = byId('oc-subtitle');
        if (sub) { sub.innerHTML = U.bidi(row.code || '') + ' · ' + U.bidi(row.pharmacyName || ''); }
        const body = byId('oc-details-list');
        if (body) { body.innerHTML = o ? quickHtml(o) : '<div class="px-4 py-3">' + U.statusBadge(row.status) + '</div>'; }
        const doc = byId('oc-btn-document');
        const approve = byId('oc-btn-approve');
        const open = byId('oc-btn-edit');
        if (doc) { doc.setAttribute('href', '/Commercial/PreOrders/Document/' + encodeURIComponent(row.id)); doc.classList.toggle('d-none', !o); }
        if (approve) { approve.setAttribute('href', '/Commercial/PreOrders/Approve/' + encodeURIComponent(row.id)); approve.classList.toggle('d-none', !(o && o.can && o.can.decide)); }
        if (open) {
            const edit = o && o.can && o.can.edit;
            open.textContent = edit ? L().OpenEdit : L().OpenDetails;
            open.setAttribute('href', (edit ? '/Commercial/PreOrders/Edit/' : '/Commercial/PreOrders/Details/') + encodeURIComponent(row.id));
            open.classList.toggle('d-none', !o);
        }
        depotForm(o);
    };

    /** The quick view draws the row at once, then the order when it arrives. */
    const populateQuickView = (row) => {
        if (!row) { return; }
        const token = ++quickToken;
        quickOrder = null;
        renderQuickView(row, null);
        U.request('GET', endpoint + '/orders/' + encodeURIComponent(row.id)).then((res) => {
            const o = U.data(res);
            if (token !== quickToken) { return; }
            if (o) { quickOrder = o; renderQuickView(row, o); } else { const body = byId('oc-details-list'); if (body) { body.innerHTML = '<div class="alert alert-warning m-4">' + esc(U.errorText(res)) + '</div>'; } }
        });
    };

    const initDataTable = async () => {
        if (!dtTableEl || !C || !U || !window.DitenDataTable) return;

        onChange('filterPeriod', syncRange);
        byId('btnFilterApply')?.addEventListener('click', guardApply, true);
        enhanceRange();
        document.querySelectorAll('input[name="po-dr-status"]').forEach((r) => r.addEventListener('change', () => byId('po-dr-lines')?.classList.toggle('d-none', r.value !== 'partial' || !r.checked)));
        byId('po-dr-save')?.addEventListener('click', saveDepotResponse);

        const canCreate = flag('can-create');
        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            ajax: { url: endpoint + '/list', type: 'GET' },
            // BL-452: CSV/Excel = every matching order with the visible columns and the filters, not the page on screen
            export: { mode: 'server', url: endpoint + '/list/export', fileName: 'pre-orders' },
            onResponse: onListResponse,
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateQuickView, actionKey: 'quickView' },
            toolbar: {
                addNewText: canCreate ? L().NewPreOrder : '',
                onAddNew: canCreate ? () => { window.location.href = '/Commercial/PreOrders/Create'; } : undefined,
                exportColumns: EXPORT_COLUMNS,
                colvisColumns: DATA_COLUMNS,
                exportPermitted: flag('can-export')
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Commercial', pageKey: 'PreOrders', saveViewColumnIndexes: DATA_COLUMNS, defaultVisibleColumnIndexes: defaultVisible(), baseOrder: [] },
            config: {
                order: [],
                searching: true,
                drawCallback: drawEmptyState,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'code', name: 'code' },
                    { data: 'createdAt', name: 'createdAt' },
                    { data: 'pharmacyName', name: 'pharmacyName' },
                    { data: 'depotName', name: 'depotName' },
                    { data: 'repName', name: 'repName' },
                    { data: 'lineCount', name: 'lineCount' },
                    { data: 'gross', name: 'gross' },
                    { data: 'localGross', name: 'localGross' },
                    { data: 'effectiveDiscountPct', name: 'effectiveDiscountPct' },
                    { data: 'status', name: 'status' },
                    { data: 'outcome', name: 'outcome' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    // newest first by the CRM; a column sort would order amounts of different currencies together (AC-UI-2)
                    { targets: '_all', orderable: false },
                    { targets: 0, className: 'control', searchable: false, responsivePriority: 1, render: () => '' },
                    { targets: 1, className: 'all', render: (d, type, full) => (type === 'display' ? '<span class="fw-medium text-heading text-nowrap">' + U.bidi(full.code || '') + '</span>' : (full.code || '')) },
                    { targets: 2, responsivePriority: 3, render: (d, type, full) => (type === 'display' ? '<span class="text-nowrap">' + esc(U.day(full.createdAt)) + '</span>' : (full.createdAt || '')) },
                    { targets: 3, responsivePriority: 2, render: (d, type, full) => (type === 'display' ? pharmacyCell(full) : (full.pharmacyName || '')) },
                    { targets: 4, responsivePriority: 6, render: (d, type, full) => (type === 'display' ? U.bidi(full.depotName || '—') : (full.depotName || '')) },
                    { targets: 5, responsivePriority: 7, render: (d, type, full) => (type === 'display' ? U.bidi(full.repName || '') : (full.repName || '')) },
                    { targets: 6, responsivePriority: 8, render: (d, type, full) => (type === 'display' ? esc(C.num(full.lineCount, U.culture(), 0)) : full.lineCount) },
                    { targets: 7, responsivePriority: 5, render: (d, type, full) => (type === 'display' ? totalCell(full) : (full.priceMode === 'priced' ? U.money(full.gross, full.currency) : L().QuantityModeShort)) },
                    { targets: 8, render: (d, type, full) => (type === 'display' ? localCell(full) : (full.localGross != null ? U.money(full.localGross, full.localCurrencyCode) : '')) },
                    { targets: 9, render: (d, type, full) => (full.effectiveDiscountPct == null ? (type === 'display' ? '<span class="text-muted">—</span>' : '') : esc(U.pct(full.effectiveDiscountPct))) },
                    { targets: 10, responsivePriority: 4, render: (d, type, full) => (type === 'display' ? statusCell(full) : (L()[C.statusOf(full.status).key] || full.status || '')) },
                    { targets: 11, render: (d, type, full) => (type === 'display' ? ruleCell(full) : (full.outcome && full.outcome !== 'ok' && window.CommercialRules ? window.CommercialRules.outcome(full.outcome) : '')) },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        className: 'cell-fit all',
                        render: (d, type, full) => window.DitenDataTable.renderActions([
                            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-id': full.id, title: L().QuickView, 'aria-label': L().QuickView } }
                        ])
                    }
                ]
            }
        });
        enhanceSearchPickers();
        loadAwaiting();
    };

    return {
        init: function () {
            initDataTable();
        },
        // for the tests: the quick view of one order as the drawer draws it
        quickHtml: quickHtml
    };
})();

document.addEventListener('DOMContentLoaded', () => PreOrdersList.init());
