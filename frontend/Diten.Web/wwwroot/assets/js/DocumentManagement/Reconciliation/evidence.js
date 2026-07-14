/**
 * MOD-0028-FU10 — Provisioning Evidence DataTable (Compact v2, same-origin MVC proxy).
 * List + IT/QA sign-off only (mark permissions applied / QA verify). No hard delete, no bulk, no metadata edit
 * (evidence upsert/edit is out of scope for FU10). Save View + inline filter + ColReorder per standard.
 */
'use strict';

const EvidenceList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const ctx = window.ReconciliationContext || {};
    const baselineReleaseId = ctx.baselineReleaseId || '';
    const canManage = ctx.canManage === true;
    const dtTableEl = document.querySelector('.datatables-evidence');
    const endpoint = '/DocumentManagement/Reconciliation/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'DocumentManagement', pageKey: 'ReconciliationEvidence' };
    const filterCollapseId = 'inlineFilterCollapse';
    const filterHostId = 'inlineFilterHost';

    // control(0) + 9 data + action(10). Reorderable data columns: 1..9.
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9];
    const totalColumnCount = 11;
    const baseOrder = [[9, 'desc']];
    let appliedFilters = { platformProvider: [], provisioningStatus: [], deviationStatus: [] };
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
    const emptyFilters = () => ({ platformProvider: [], provisioningStatus: [], deviationStatus: [] });
    const normalizeFilters = (f) => ({
        platformProvider: normalizeArray(f?.platformProvider),
        provisioningStatus: normalizeArray(f?.provisioningStatus),
        deviationStatus: normalizeArray(f?.deviationStatus)
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
            console.error('[Evidence SaveView] Failed to load saved views.', error);
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
            return matchesMultiFilter(appliedFilters.platformProvider, row.platformProvider)
                && matchesMultiFilter(appliedFilters.provisioningStatus, row.provisioningStatus)
                && matchesMultiFilter(appliedFilters.deviationStatus, row.deviationStatus);
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
        $('#filterPlatformProvider, #filterProvisioningStatus, #filterDeviationStatus').each(function () {
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
        $('#filterPlatformProvider').val(normalizeArray(values.platformProvider)).trigger('change');
        $('#filterProvisioningStatus').val(normalizeArray(values.provisioningStatus)).trigger('change');
        $('#filterDeviationStatus').val(normalizeArray(values.deviationStatus)).trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.platformProvider, appliedFilters.provisioningStatus, appliedFilters.deviationStatus].filter(hasFilterValue).length;

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
                platformProvider: $('#filterPlatformProvider').val() || [],
                provisioningStatus: $('#filterProvisioningStatus').val() || [],
                deviationStatus: $('#filterDeviationStatus').val() || []
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
    const providerLabel = (v) => {
        const s = upper(v);
        if (s === 'INHOUSE') return t('ProviderInHouse');
        if (s === 'GOOGLEDRIVE') return t('ProviderGoogleDrive');
        return v || t('Unknown');
    };
    const provStatusClass = (v) => ({ CREATED: 'success', EXISTINGMATCHED: 'success', PENDING: 'secondary', FAILED: 'danger', SKIPPED: 'warning', DEVIATED: 'warning' }[upper(v)] || 'secondary');
    const provStatusBadge = (v) => `<span class="badge bg-label-${provStatusClass(v)}">${escapeHtml(t('Prov' + (v || '')) || v)}</span>`;
    const devStatusClass = (v) => ({ NONE: 'secondary', OPEN: 'warning', CLOSED: 'success', ACCEPTED: 'info' }[upper(v)] || 'secondary');
    const devStatusBadge = (v) => `<span class="badge bg-label-${devStatusClass(v)}">${escapeHtml(t('EvidenceDev' + (v || 'None')) || v)}</span>`;
    const boolBadge = (v) => v
        ? `<span class="badge bg-label-success">${t('Yes')}</span>`
        : `<span class="badge bg-label-secondary">${t('No')}</span>`;
    const pathCell = (v) => v
        ? `<small class="text-truncate d-inline-block" style="max-width:240px" title="${escapeHtml(v)}">${escapeHtml(v)}</small>`
        : t('NotAvailable');
    const dateCell = (v) => (v ? String(v).slice(0, 10) : t('NotAvailable'));
    const lastReadBack = (row) => row.updatedAt || row.createdOnPlatformAt || null;

    // ── Sign-off actions ──
    const signOff = (row, action, confirmKey, doneKey) => {
        if (!row?.id) return;
        window.showConfirm?.(t(confirmKey), async () => {
            const body = new FormData();
            body.append('__RequestVerificationToken', token());
            try {
                const res = await fetch(`${endpoint}/evidence/${row.id}/${action}`, { method: 'POST', body });
                const payload = await res.json().catch(() => ({}));
                if (!res.ok || payload?.isSuccessful === false) {
                    if (window.DitenUnauthorized?.handle(res, payload)) return;
                    const msg = Array.isArray(payload?.errors) && payload.errors.length ? payload.errors[0] : t('ErrorOccurred');
                    window.showToast?.(msg, 'error');
                    return;
                }
                dt.ajax.reload(() => window.showToast?.(t(doneKey), 'success'), false);
            } catch (e) {
                console.error('[Evidence] Sign-off failed.', e);
                window.showToast?.(t('ErrorOccurred'), 'error');
            }
        }, { type: 'warning', confirmButtonText: t('Confirm') });
    };

    const rowActionHandlers = {
        permissionsApplied: ({ row }) => signOff(row, 'permissions-applied', 'PermissionsAppliedConfirm', 'PermissionsAppliedDone'),
        qaVerify: ({ row }) => signOff(row, 'qa-verify', 'QaVerifyConfirm', 'QaVerifiedDone')
    };

    const buildRowActions = (full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        const actions = [];
        if (canManage && full.permissionsApplied !== true) {
            actions.push({ key: 'permissionsApplied', icon: 'bx bx-key', text: L.MarkPermissionsApplied, attrs: { 'data-id': full.id, 'data-json': rowJson } });
        }
        if (canManage && full.qaVerified !== true) {
            actions.push({ key: 'qaVerify', icon: 'bx bx-badge-check', text: L.MarkQaVerified, attrs: { 'data-id': full.id, 'data-json': rowJson } });
        }
        if (!actions.length) return '<span class="text-muted">—</span>';
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
                        console.error('[Evidence SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: `${endpoint}/evidence/${baselineReleaseId}`,
                type: 'GET',
                headers: getAuthHeaders(),
                dataSrc: (json) => window.DitenDataTable.unwrapResponseData(json)
            },
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' }, // control(0) & action(last) fixed; no checkbox column (no bulk on this read+signoff table)
            columns: [
                { data: 'id', name: 'control' },
                { data: 'registerFolderId', name: 'registerFolderId' },
                { data: 'fullPath', name: 'fullPath' },
                { data: 'platformProvider', name: 'platformProvider' },
                { data: 'platformFolderId', name: 'platformFolderId' },
                { data: 'provisioningStatus', name: 'provisioningStatus' },
                { data: 'permissionsApplied', name: 'permissionsApplied' },
                { data: 'qaVerified', name: 'qaVerified' },
                { data: 'deviationStatus', name: 'deviationStatus' },
                { data: 'updatedAt', name: 'lastReadBackAt' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => escapeHtml(data || t('NotAvailable')) },
                { targets: 2, render: (data) => pathCell(data) },
                { targets: 3, render: (data) => escapeHtml(providerLabel(data)) },
                { targets: 4, render: (data) => escapeHtml(data || t('NotAvailable')) },
                { targets: 5, render: (data) => provStatusBadge(data) },
                { targets: 6, render: (data) => boolBadge(data === true) },
                { targets: 7, render: (data) => boolBadge(data === true) },
                { targets: 8, render: (data) => devStatusBadge(data) },
                { targets: 9, render: (data, type, full) => dateCell(lastReadBack(full)) },
                { targets: -1, title: L.RowActions, searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => buildRowActions(full) }
            ],
            buttons: window.DtDefaults.exportButtons(
                null,
                null,
                extraButtons,
                { exportColumns: [1, 2, 3, 4, 5, 6, 7, 8, 9], colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8, 9] }
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

document.addEventListener('DOMContentLoaded', () => EvidenceList.init());
