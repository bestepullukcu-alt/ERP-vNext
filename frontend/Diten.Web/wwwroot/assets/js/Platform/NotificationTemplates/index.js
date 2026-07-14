/**
 * Notification Templates - Platform Admin DataTables Index Script (MOD-0027-FU02).
 * Proxy-profile: browser JS only calls same-origin /Platform/NotificationTemplates/api endpoints.
 * Archive-only lifecycle: no delete/bulk-delete actions (backend exposes none).
 */
'use strict';

const NotificationTemplatesList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-notificationtemplates');
    const apiBase = '/Platform/NotificationTemplates/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'NotificationTemplates' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6];
    const totalColumnCount = 8;
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5, 6];
    const baseOrder = [[1, 'asc']];
    let appliedFilters = { scope: 'platform', tenant: '', status: '', locale: '', channel: '' };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const emptyFilters = () => ({ scope: 'platform', tenant: '', status: '', locale: '', channel: '' });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        const scope = normalizeString(source.scope) === 'tenant' ? 'tenant' : 'platform';
        return {
            scope: scope,
            tenant: normalizeString(source.tenant),
            status: normalizeString(source.status),
            locale: normalizeString(source.locale),
            channel: normalizeString(source.channel)
        };
    };
    const hasFilterValue = (v) => normalizeString(v).length > 0;

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
            console.error('[NotificationTemplates SaveView] Failed to load saved views.', error);
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

    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $body = $(document.body);
        $('#filterScope, #filterTenant, #filterStatus, #filterLocale, #filterChannel').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: this.id === 'filterTenant' ? 0 : Infinity,
                width: 'element',
                allowClear: this.id !== 'filterScope'
            });
        });
        $('#filterScope').on('change', syncTenantChipVisibility);
    };
    const syncTenantChipVisibility = () => {
        const scope = document.getElementById('filterScope')?.value || 'platform';
        document.getElementById('filterTenantChip')?.classList.toggle('d-none', scope !== 'tenant');
    };

    const syncFilterControls = (values) => {
        $('#filterScope').val(values.scope || 'platform').trigger('change');
        $('#filterTenant').val(values.tenant || '').trigger('change');
        $('#filterStatus').val(values.status || '').trigger('change');
        $('#filterLocale').val(values.locale || '').trigger('change');
        $('#filterChannel').val(values.channel || '').trigger('change');
        syncTenantChipVisibility();
    };
    const getAppliedFilterCount = () => {
        let count = [appliedFilters.status, appliedFilters.locale, appliedFilters.channel].filter(hasFilterValue).length;
        if (appliedFilters.scope === 'tenant') count += 1;
        return count;
    };

    const buildDataUrl = () => {
        const params = new URLSearchParams();
        if (appliedFilters.status) params.set('status', appliedFilters.status);
        if (appliedFilters.locale) params.set('locale', appliedFilters.locale);
        if (appliedFilters.channel) params.set('channel', appliedFilters.channel);
        params.set('pageSize', '100');
        if (appliedFilters.scope === 'tenant') {
            if (!appliedFilters.tenant) return null;
            return `${apiBase}/tenant/${appliedFilters.tenant}/templates?${params.toString()}`;
        }
        return `${apiBase}/templates?${params.toString()}`;
    };
    const reloadData = (api) => {
        const url = buildDataUrl();
        if (!url) {
            window.showToast?.(L.SelectTenantFirst, 'warning');
            return false;
        }
        try { api.ajax.url(url).load(null, false); } catch (e) { console.error('[NotificationTemplates] Reload failed.', e); }
        return true;
    };

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
        reloadData(api);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };

    const unwrapLookup = (payload) => payload?.data || payload?.Data || [];
    const fillLookupSelect = (selectId, options, keepShowAll = true) => {
        const select = document.getElementById(selectId);
        if (!select) return;
        const showAllText = L.ShowAll || '';
        select.innerHTML = '';
        if (keepShowAll) {
            const showAll = document.createElement('option');
            showAll.value = '';
            showAll.textContent = showAllText;
            select.appendChild(showAll);
        }
        options.forEach((item) => {
            if (!item?.value) return;
            const opt = document.createElement('option');
            opt.value = item.value;
            opt.textContent = item.name || item.code || item.value;
            select.appendChild(opt);
        });
    };
    const fetchLookup = async (key) => {
        const res = await fetch(`${apiBase}/lookups/${encodeURIComponent(key)}`, { credentials: 'same-origin' });
        if (!res.ok) throw new Error(`Lookup '${key}' failed (${res.status}).`);
        return unwrapLookup(await res.json());
    };
    const loadLookupOptions = async () => {
        try {
            const [statuses, locales, channels] = await Promise.all([
                fetchLookup('notification-template-statuses'),
                fetchLookup('locales'),
                fetchLookup('notification-channels')
            ]);
            fillLookupSelect('filterStatus', statuses);
            fillLookupSelect('filterLocale', locales);
            fillLookupSelect('filterChannel', channels);
        } catch (error) {
            // Controlled degraded state: lookups unavailable -> dropdowns stay empty, no hardcoded fallback.
            console.error('[NotificationTemplates Lookup] Failed.', error);
        }
        try {
            const res = await fetch(`${apiBase}/tenants?page=1&pageSize=100`, { credentials: 'same-origin' });
            if (res.ok) {
                const payload = await res.json();
                const raw = payload?.data?.items || payload?.data || payload?.Data || [];
                const tenants = (Array.isArray(raw) ? raw : []).map((t) => ({
                    value: t.id || t.Id,
                    name: t.displayName || t.DisplayName || t.name || t.Name || t.id || t.Id
                }));
                fillLookupSelect('filterTenant', tenants, false);
            }
        } catch (error) {
            console.error('[NotificationTemplates Tenants] Failed.', error);
        }
    };

    const setupFilters = async (api) => {
        await loadLookupOptions();
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = normalizeFilters({
                scope: document.getElementById('filterScope')?.value,
                tenant: document.getElementById('filterTenant')?.value,
                status: document.getElementById('filterStatus')?.value,
                locale: document.getElementById('filterLocale')?.value,
                channel: document.getElementById('filterChannel')?.value
            });
            if (!reloadData(api)) return;
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
        'Draft': { title: L.StatusDraft || 'Draft', class: 'bg-label-warning' },
        'Active': { title: L.StatusActive || L.Active, class: 'bg-label-success' },
        'Archived': { title: L.StatusArchived || 'Archived', class: 'bg-label-secondary' }
    });
    const formatDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? String(v).slice(0, 10) : d.toLocaleDateString(window.CurrentLanguage || undefined);
    };
    const rowScopeSuffix = (row) => (row?.tenantId ? `?tenantId=${row.tenantId}` : '');

    const rowActionHandlers = {
        quickView: ({ row }) => {
            if (row?.id) window.location.href = `/Platform/NotificationTemplates/Details/${row.id}${rowScopeSuffix(row)}`;
        },
        edit: ({ row }) => {
            if (row?.id) window.location.href = `/Platform/NotificationTemplates/Edit/${row.id}${rowScopeSuffix(row)}`;
        },
        archive: ({ row }) => {
            if (!row?.id || row.status === 'Archived') return;
            window.showConfirm?.(L.ArchiveConfirm, async () => {
                try {
                    const res = await fetch(`${apiBase}/templates/${row.id}/archive`, {
                        method: 'POST',
                        credentials: 'same-origin'
                    });
                    if (!res.ok) throw new Error('Archive failed.');
                    dt.ajax.reload(null, false);
                    window.showToast?.(L.TemplateArchived, 'success');
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, { entityName: row.templateKey, type: 'danger', confirmButtonText: L.Archive });
        }
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
                        console.error('[NotificationTemplates SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: buildDataUrl() || `${apiBase}/templates?pageSize=100`,
                type: 'GET'
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'templateKey', name: 'templateKey' },
                    { data: 'locale', name: 'locale' },
                    { data: 'channel', name: 'channel' },
                    { data: 'status', name: 'status' },
                    { data: 'semanticVersion', name: 'semanticVersion' },
                    { data: 'updatedAt', name: 'updatedAt' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    {
                        targets: 4,
                        render: (data, type) => {
                            const status = getStatusMap()[data] || { title: data || L.Unknown, class: 'bg-label-primary' };
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
                            const actions = [
                                {
                                    key: 'quickView',
                                    className: 'js-quick-view me-1',
                                    icon: 'bx bx-show',
                                    attrs: { 'data-id': full.id, 'data-json': rowJson, 'title': L.QuickView }
                                },
                                {
                                    key: 'edit',
                                    className: 'js-edit-item',
                                    icon: 'bx bx-edit',
                                    text: L.Edit,
                                    attrs: { 'data-id': full.id, 'data-json': rowJson }
                                }
                            ];
                            if (full.status !== 'Archived') {
                                actions.push({
                                    key: 'archive',
                                    className: 'text-danger',
                                    icon: 'bx bx-archive',
                                    text: L.Archive,
                                    attrs: { 'data-json': rowJson }
                                });
                            }
                            return window.DitenDataTable.renderActions(actions);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew,
                    { href: '/Platform/NotificationTemplates/Create' },
                    extraButtons,
                    { exportColumns: [1, 2, 3, 4, 5, 6], colvisColumns: [1, 2, 3, 4, 5, 6] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        const suffix = appliedFilters.scope === 'tenant' && appliedFilters.tenant
                            ? `?tenantId=${appliedFilters.tenant}`
                            : '';
                        window.location.href = `/Platform/NotificationTemplates/Create${suffix}`;
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
            initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => NotificationTemplatesList.init());
