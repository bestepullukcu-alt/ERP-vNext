/**
 * MOD-0028-FU10 — Deviations DataTable (Compact v2, same-origin MVC proxy).
 * Read + Resolve/Accept/Details only. No hard delete, no bulk. Save View + inline filter + ColReorder per standard.
 */
'use strict';

const DeviationsList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const ctx = window.ReconciliationContext || {};
    const baselineReleaseId = ctx.baselineReleaseId || '';
    const canManage = ctx.canManage === true;
    const dtTableEl = document.querySelector('.datatables-deviations');
    const endpoint = '/DocumentManagement/Reconciliation/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'DocumentManagement', pageKey: 'ReconciliationDeviations' };
    const filterCollapseId = 'inlineFilterCollapse';
    const filterHostId = 'inlineFilterHost';

    // control(0) + 8 data + action(9). Reorderable data columns: 1..8.
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8];
    const totalColumnCount = 10;
    const baseOrder = [[8, 'desc']];
    let appliedFilters = { deviationType: [], severity: [], status: [] };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };
    const t = (key) => L[key] || key;
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || { 'X-Requested-With': 'XMLHttpRequest' };
    const upper = (v) => String(v || '').toUpperCase();
    const escapeHtml = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const emptyFilters = () => ({ deviationType: [], severity: [], status: [] });
    const normalizeFilters = (f) => ({
        deviationType: normalizeArray(f?.deviationType),
        severity: normalizeArray(f?.severity),
        status: normalizeArray(f?.status)
    });
    const hasFilterValue = (v) => normalizeArray(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected).map((s) => s.toUpperCase());
        return !norm.length || norm.includes(upper(actual));
    };

    // ── Save View plumbing ──
    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const n = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((ci, pos) => {
                if (typeof colVis[ci] === 'boolean') n[ci] = colVis[ci];
                else if (typeof colVis[pos] === 'boolean') n[ci] = colVis[pos];
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((ci) => { if (typeof colVis[ci] === 'boolean') n[ci] = colVis[ci]; });
        }
        return Object.keys(n).length ? n : null;
    };
    const captureColVis = (api) => {
        const r = {};
        saveViewColumnIndexes.forEach((ci) => { try { r[ci] = !!api.column(ci).visible(); } catch (e) { } });
        return r;
    };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = true; return a; }, {});
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
        return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
    };
    const captureColOrder = (api) => { try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (e) { return null; } };
    const applyColOrder = (api, order) => {
        const n = normalizeColOrder(order);
        if (n && typeof api?.colReorder?.order === 'function') api.colReorder.order(n, true);
    };
    const applyColVis = (api, colVis) => {
        const n = normalizeColVis(colVis);
        if (!n) return;
        saveViewColumnIndexes.forEach((ci) => { if (typeof n[ci] === 'boolean') { try { api.column(ci).visible(n[ci], false); } catch (e) { } } });
    };
    const getSearchVal = (api) => { try { return api.table().container().querySelector('.dt-search input')?.value || ''; } catch (e) { return ''; } };
    const syncSearchInput = (api, v) => { try { const e2 = api.table().container().querySelector('.dt-search input'); if (e2) e2.value = v || ''; } catch (e) { } };
    const getCurrentView = (api) => ({
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(api) || api.search()),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });
    const serializeView = (v) => JSON.stringify({
        filters: Object.keys(v?.filters || {}).sort().reduce((acc, key) => { acc[key] = normalizeArray(v.filters[key]); return acc; }, {}),
        search: normalizeString(v?.search),
        colVis: normalizeColVis(v?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(v?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });
    const getSavedViewId = (sv) => sv?.id || sv?.Id || sv?._id || null;
    const getSavedViewName = (sv) => sv?.viewName || sv?.ViewName || '';
    const isSavedViewDefault = (sv) => sv?.isDefault === true || sv?.IsDefault === true;
    const unwrapViewResponse = (r) => r?.data || r?.Data || r;
    const getSavedViewDef = (sv) => {
        const raw = sv?.viewDefinition ?? sv?.ViewDefinition ?? {};
        if (typeof raw === 'string') { try { return JSON.parse(raw); } catch (e) { return {}; } }
        return raw || {};
    };
    const mapSavedViewToState = (sv) => {
        const d = getSavedViewDef(sv);
        return {
            filters: normalizeFilters(d.filters || d),
            search: normalizeString(d.search),
            colVis: normalizeColVis(d.colVis),
            columnOrder: normalizeColOrder(d.columnOrder),
            order: Array.isArray(d.order) ? d.order : null
        };
    };
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || view || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getResetBaselineState = () => normalizeViewState({
        filters: emptyFilters(), search: '', colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder
    });
    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            filters: emptyFilters(), search: '', colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder
        };
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };
    const loadDefaultView = async () => {
        defaultViewRecord = null; defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[Deviations SaveView] Failed to load saved views.', error);
            return null;
        }
    };
    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const normalizedView = normalizeViewState(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Default').trim(),
            viewDefinition: normalizedView,
            isDefault: true,
            visibility: 'private'
        };
        const existingId = getSavedViewId(defaultViewRecord);
        const saved = existingId
            ? await personalizationClient.updateView(existingId, payload)
            : await personalizationClient.saveView(payload);
        const rec = unwrapViewResponse(saved);
        defaultViewRecord = rec && typeof rec === 'object' ? rec : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };

    // ── Inline filter ──
    const mountInlineFilter = () => {
        const host = document.getElementById(filterHostId);
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };
    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById(filterCollapseId);
        if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };
    const bindInlineFilterA11y = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById(filterCollapseId);
        if (!btn || !collapseEl || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };
    const registerTableFilters = () => {
        if (!dtTableEl || !window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl.dataset.compactFilterBound === '1') return;
        dtTableEl.dataset.compactFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesMultiFilter(appliedFilters.deviationType, row.deviationType)
                && matchesMultiFilter(appliedFilters.severity, row.severity)
                && matchesMultiFilter(appliedFilters.status, row.status);
        });
    };
    const syncMultiSelectSummary = ($select) => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;
        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) { $summary = $('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = $('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }
        const placeholder = normalizeString($select.data('placeholder')) || '';
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || []).map((i) => normalizeString(i.text)).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + (L.Reset || '') + '" title="' + (L.Reset || '') + '">&times;</span>');
            $clearBtn.on('mousedown', (e) => { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clearBtn);
        }
    };
    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $body = $(document.body);
        $('#filterDeviationType, #filterSeverity, #filterStatus').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                closeOnSelect: false
            });
            $s.on('change.select2-summary', function () { syncMultiSelectSummary($s); });
            requestAnimationFrame(() => syncMultiSelectSummary($s));
        });
    };
    const syncFilterControls = (values) => {
        $('#filterDeviationType').val(normalizeArray(values.deviationType)).trigger('change');
        $('#filterSeverity').val(normalizeArray(values.severity)).trigger('change');
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.deviationType, appliedFilters.severity, appliedFilters.status].filter(hasFilterValue).length;

    const applySavedTableState = (api, view) => {
        if (!api || !view) return;
        const s = normalizeViewState(view);
        appliedFilters = s.filters;
        syncFilterControls(appliedFilters);
        applyColOrder(api, s.columnOrder);
        applyColVis(api, s.colVis);
        api.search(s.search);
        syncSearchInput(api, s.search);
        api.order(s.order);
        try { api.columns.adjust(); } catch (e) { }
        try { api.responsive?.recalc?.(); } catch (e) { }
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };
    const setupFilters = (api) => {
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                deviationType: $('#filterDeviationType').val() || [],
                severity: $('#filterSeverity').val() || [],
                status: $('#filterStatus').val() || []
            };
            api.draw();
            window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            applySavedTableState(api, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });
    };

    // ── Renderers ──
    const typeLabel = (v) => t(v) || v || t('NotAvailable');
    const severityClass = (v) => ({ CRITICAL: 'danger', MAJOR: 'warning', WARNING: 'warning', INFO: 'info' }[upper(v)] || 'secondary');
    const severityBadge = (v) => `<span class="badge bg-label-${severityClass(v)}">${escapeHtml(t('Severity' + (v || '')) || v)}</span>`;
    const statusClass = (v) => ({ OPEN: 'warning', ACCEPTED: 'info', RESOLVED: 'success', CLOSED: 'secondary' }[upper(v)] || 'secondary');
    const statusBadge = (v) => `<span class="badge bg-label-${statusClass(v)}">${escapeHtml(t('DeviationStatus' + (v || '')) || v)}</span>`;
    const pathCell = (v) => v
        ? `<small class="text-truncate d-inline-block" style="max-width:220px" title="${escapeHtml(v)}">${escapeHtml(v)}</small>`
        : t('NotAvailable');
    const dateCell = (v) => (v ? String(v).slice(0, 10) : t('NotAvailable'));

    const openDetails = (row) => {
        const body = document.getElementById('deviationDetailsBody');
        if (!body || !row) return;
        const fields = [
            ['DeviationType', typeLabel(row.deviationType)],
            ['Severity', severityBadge(row.severity)],
            ['Status', statusBadge(row.status)],
            ['RegisterFolderId', escapeHtml(row.registerFolderId || t('NotAvailable'))],
            ['ExpectedFullPath', escapeHtml(row.expectedFullPath || t('NotAvailable'))],
            ['ActualFullPath', escapeHtml(row.actualFullPath || t('NotAvailable'))],
            ['Description', escapeHtml(row.description || t('NotAvailable'))],
            ['DetectedAt', escapeHtml(row.detectedAt ? String(row.detectedAt).slice(0, 19).replace('T', ' ') : t('NotAvailable'))],
            ['CommentLabel', escapeHtml(row.resolutionComment || t('NotAvailable'))]
        ];
        body.innerHTML = fields.map(([k, v]) => `<dt class="col-sm-5">${t(k)}</dt><dd class="col-sm-7">${v}</dd>`).join('');
        window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('deviationDetailsModal')).show();
    };

    const openCommentModal = (action, row) => {
        if (!row?.id) return;
        document.getElementById('commentDeviationId').value = row.id;
        document.getElementById('commentAction').value = action;
        document.getElementById('commentText').value = '';
        const titleEl = document.getElementById('commentModalTitle');
        if (titleEl) titleEl.textContent = action === 'accept' ? t('Accept') : t('Resolve');
        window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('commentModal')).show();
    };

    const submitComment = async () => {
        const id = document.getElementById('commentDeviationId')?.value;
        const action = document.getElementById('commentAction')?.value;
        const comment = document.getElementById('commentText')?.value || '';
        if (!id || !action) return;
        const body = new FormData();
        body.append('__RequestVerificationToken', token());
        body.append('comment', comment);
        try {
            const res = await fetch(`${endpoint}/deviations/${id}/${action}`, { method: 'POST', body });
            const payload = await res.json().catch(() => ({}));
            if (!res.ok || payload?.isSuccessful === false) {
                if (window.DitenUnauthorized?.handle(res, payload)) return;
                const msg = Array.isArray(payload?.errors) && payload.errors.length ? payload.errors[0] : t('ErrorOccurred');
                window.showToast?.(msg, 'error');
                return;
            }
            window.bootstrap?.Modal.getInstance(document.getElementById('commentModal'))?.hide();
            dt.ajax.reload(() => {
                window.showToast?.(action === 'accept' ? t('DeviationAccepted') : t('DeviationResolved'), 'success');
            }, false);
        } catch (e) {
            console.error('[Deviations] Comment submit failed.', e);
            window.showToast?.(t('ErrorOccurred'), 'error');
        }
    };

    const rowActionHandlers = {
        details: ({ row }) => openDetails(row),
        resolve: ({ row }) => openCommentModal('resolve', row),
        accept: ({ row }) => openCommentModal('accept', row)
    };

    const buildRowActions = (full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        const actions = [
            { key: 'details', className: 'me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, 'data-json': rowJson, title: L.ViewDetails } }
        ];
        const isOpen = upper(full.status) === 'OPEN';
        if (canManage && isOpen) {
            actions.push({ key: 'resolve', icon: 'bx bx-check', text: L.Resolve, attrs: { 'data-id': full.id, 'data-json': rowJson } });
            actions.push({ key: 'accept', icon: 'bx bx-check-shield', text: L.Accept, attrs: { 'data-id': full.id, 'data-json': rowJson } });
        }
        return window.DitenDataTable.renderActions(actions);
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView();

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || L.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[Deviations SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: `${endpoint}/deviations/${baselineReleaseId}`,
                type: 'GET',
                headers: getAuthHeaders(),
                dataSrc: (json) => window.DitenDataTable.unwrapResponseData(json)
            },
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' }, // control(0) & action(last) fixed; no checkbox column (no bulk on this read+signoff table)
            columns: [
                { data: 'id', name: 'control' },
                { data: 'deviationType', name: 'deviationType' },
                { data: 'severity', name: 'severity' },
                { data: 'status', name: 'status' },
                { data: 'registerFolderId', name: 'registerFolderId' },
                { data: 'expectedFullPath', name: 'expectedFullPath' },
                { data: 'actualFullPath', name: 'actualFullPath' },
                { data: 'description', name: 'description' },
                { data: 'detectedAt', name: 'detectedAt' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => `<span class="fw-medium">${escapeHtml(typeLabel(data))}</span>` },
                { targets: 2, render: (data) => severityBadge(data) },
                { targets: 3, render: (data) => statusBadge(data) },
                { targets: 4, render: (data) => escapeHtml(data || t('NotAvailable')) },
                { targets: 5, render: (data) => pathCell(data) },
                { targets: 6, render: (data) => pathCell(data) },
                { targets: 7, render: (data) => pathCell(data) },
                { targets: 8, render: (data) => dateCell(data) },
                { targets: -1, title: L.RowActions, searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => buildRowActions(full) }
            ],
            buttons: window.DtDefaults.exportButtons(
                null,
                null,
                extraButtons,
                { exportColumns: [1, 2, 3, 4, 5, 6, 7, 8], colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8] }
            ),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterA11y();
                setupFilters(this.api());
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () { window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount()); }
        }));

        window.DitenDataTable.bindActionDispatcher({ tableEl: dtTableEl, dt: dt, onRowAction: rowActionHandlers });
        document.getElementById('btnCommentConfirm')?.addEventListener('click', () => void submitComment());

        dt.on('column-visibility.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        dt.on('search.dt order.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        dt.on('column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    return {
        init: function () {
            if (!dtTableEl) return;
            registerTableFilters();
            void initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => DeviationsList.init());
