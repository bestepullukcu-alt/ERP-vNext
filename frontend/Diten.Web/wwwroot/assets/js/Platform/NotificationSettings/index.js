/**
 * Notification Settings - Platform Admin DataTables Index Script (MOD-0027-FU02).
 * Proxy-profile: browser JS only calls same-origin /Platform/NotificationSettings/api endpoints.
 * Lists tenant-scoped messaging settings; delete is per-tenant (no bulk endpoint, no bulk bar).
 */
'use strict';

const NotificationSettingsList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    let tenantNameMap = {};

    const dtTableEl = document.querySelector('.datatables-notificationsettings');
    const apiBase = '/Platform/NotificationSettings/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'NotificationSettings' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6];
    const totalColumnCount = 8;
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5, 6];
    const baseOrder = [[1, 'asc']];
    let appliedFilters = { provider: [], enabled: '' };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const emptyFilters = () => ({ provider: [], enabled: '' });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            provider: normalizeArray(source.provider),
            enabled: normalizeString(source.enabled)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(normalizeString(actual));
    };
    const matchesEnabledFilter = (selected, isEnabled) => {
        const norm = normalizeString(selected);
        if (!norm) return true;
        return norm === (isEnabled ? 'Active' : 'Passive');
    };

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
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
        return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
    };
    const captureColOrder = (api) => { try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (e) { return null; } };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = defaultVisibleColumnIndexes.includes(ci); return a; }, {});
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
    const syncSearchInput = (api, v) => { try { const el = api.table().container().querySelector('.dt-search input'); if (el) el.value = v || ''; } catch (e) { } };
    const getCurrentView = (api) => ({
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(api) || api.search()),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });
    const serializeView = (v) => JSON.stringify({
        filters: normalizeFilters(v?.filters),
        search: normalizeString(v?.search),
        colVis: normalizeColVis(v?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(v?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });

    const getSavedViewId = (sv) => sv?.id || sv?.Id || sv?._id || null;
    const getSavedViewName = (sv) => sv?.viewName || sv?.ViewName || '';
    const isSavedViewDefault = (sv) => sv?.isDefault === true || sv?.IsDefault === true;
    const unwrapViewResponse = (response) => response?.data || response?.Data || response;
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
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
        order: baseOrder
    });
    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || getResetBaselineState();
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };

    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[NotificationSettings SaveView] Failed to load saved views.', error);
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
        const savedResponse = existingId
            ? await personalizationClient.updateView(existingId, payload)
            : await personalizationClient.saveView(payload);
        const savedRecord = unwrapViewResponse(savedResponse);
        defaultViewRecord = savedRecord && typeof savedRecord === 'object'
            ? savedRecord
            : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };

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
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
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
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.settingsFilterBound === '1') return;
        dtTableEl.dataset.settingsFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesMultiFilter(appliedFilters.provider, row.providerCode)
                && matchesEnabledFilter(appliedFilters.enabled, row.isEnabled);
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
        $('#filterProvider').each(function () {
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
        $('#filterEnabled').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                allowClear: true
            });
        });
    };

    const syncFilterControls = (values) => {
        $('#filterProvider').val(normalizeArray(values.provider)).trigger('change');
        $('#filterEnabled').val(values.enabled || '').trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.provider, appliedFilters.enabled].filter(hasFilterValue).length;

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

    const unwrapLookup = (payload) => payload?.data || payload?.Data || [];
    const loadLookupOptions = async () => {
        const providerSelect = document.getElementById('filterProvider');
        if (!providerSelect) return;
        try {
            const res = await fetch(`${apiBase}/lookups/messaging-providers`, { credentials: 'same-origin' });
            if (!res.ok) return;
            const options = unwrapLookup(await res.json());
            providerSelect.innerHTML = '';
            options.forEach((item) => {
                if (!item?.value) return;
                const opt = document.createElement('option');
                opt.value = item.value;
                opt.textContent = item.name || item.code || item.value;
                providerSelect.appendChild(opt);
            });
        } catch (error) {
            console.error('[NotificationSettings Lookup] Failed.', error);
        }
        try {
            const res = await fetch(`${apiBase}/tenants?page=1&pageSize=100`, { credentials: 'same-origin' });
            if (res.ok) {
                const payload = await res.json();
                const raw = payload?.data?.items || payload?.data || payload?.Data || [];
                tenantNameMap = {};
                (Array.isArray(raw) ? raw : []).forEach((t) => {
                    const id = t.id || t.Id;
                    if (id) tenantNameMap[id] = t.displayName || t.DisplayName || t.name || t.Name || id;
                });
            }
        } catch (error) {
            console.error('[NotificationSettings Tenants] Failed.', error);
        }
    };

    const setupFilters = async (api) => {
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = normalizeFilters({
                provider: $('#filterProvider').val() || [],
                enabled: document.getElementById('filterEnabled')?.value || ''
            });
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

    const getStatusMap = () => ({
        true: { title: L.Active, class: 'bg-label-success' },
        false: { title: L.Passive, class: 'bg-label-secondary' }
    });
    const formatDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? String(v).slice(0, 10) : d.toLocaleDateString(window.CurrentLanguage || undefined);
    };
    const tenantLabel = (tenantId) => tenantNameMap[tenantId] || tenantId || '-';

    const rowActionHandlers = {
        quickView: ({ row }) => {
            if (row?.tenantId) window.location.href = `/Platform/NotificationSettings/Details/${row.tenantId}`;
        },
        edit: ({ row }) => {
            if (row?.tenantId) window.location.href = `/Platform/NotificationSettings/Edit/${row.tenantId}`;
        },
        delete: ({ row }) => {
            if (!row?.tenantId) return;
            window.showConfirm?.(L.DeleteConfirm, async () => {
                try {
                    const res = await fetch(`${apiBase}/${row.tenantId}`, {
                        method: 'DELETE',
                        credentials: 'same-origin'
                    });
                    if (!res.ok) throw new Error('Delete failed.');
                    dt.ajax.reload(null, false);
                    window.showToast?.(L.RecordDeleted, 'success');
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, { entityName: tenantLabel(row.tenantId), type: 'danger', confirmButtonText: L.Delete });
        }
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView();
        await loadLookupOptions();
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
                        console.error('[NotificationSettings SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: `${apiBase}?page=1&pageSize=100`,
                type: 'GET'
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'tenantId', name: 'tenantId' },
                    { data: 'providerCode', name: 'providerCode' },
                    { data: 'senderEmail', name: 'senderEmail' },
                    { data: 'isEnabled', name: 'isEnabled' },
                    { data: 'fallbackPolicy', name: 'fallbackPolicy' },
                    { data: 'updatedAt', name: 'updatedAt' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${tenantLabel(data)}</span>` },
                    { targets: 2, render: (data) => data ? `<span class="badge bg-label-info">${data}</span>` : '' },
                    {
                        targets: 4,
                        render: (data, type) => {
                            const status = getStatusMap()[String(!!data)] || { title: L.Unknown, class: 'bg-label-primary' };
                            return type === 'display' ? `<span class="badge ${status.class}">${status.title}</span>` : status.title;
                        }
                    },
                    { targets: 6, render: (data) => formatDate(data) },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, "&#39;");
                            return window.DitenDataTable.renderActions([
                                {
                                    key: 'quickView',
                                    className: 'js-quick-view me-1',
                                    icon: 'bx bx-show',
                                    attrs: { 'data-id': full.tenantId, 'data-json': rowJson, 'title': L.QuickView }
                                },
                                {
                                    key: 'edit',
                                    className: 'js-edit-item',
                                    icon: 'bx bx-edit',
                                    text: L.Edit,
                                    attrs: { 'data-id': full.tenantId, 'data-json': rowJson }
                                },
                                {
                                    key: 'delete',
                                    className: 'text-danger',
                                    icon: 'bx bx-trash',
                                    text: L.Delete,
                                    attrs: { 'data-json': rowJson }
                                }
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew,
                    { href: '/Platform/NotificationSettings/Create' },
                    extraButtons,
                    { exportColumns: [1, 2, 3, 4, 5, 6], colvisColumns: [1, 2, 3, 4, 5, 6] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/Platform/NotificationSettings/Create';
                    });
                    setTimeout(() => { saveFilterArmed = true; }, 0);
                },
                drawCallback: function () {
                    window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                }
            }
        });
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
            registerTableFilters();
            initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => NotificationSettingsList.init());
