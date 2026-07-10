'use strict';

// Position Assignments (platform-admin). Backend list endpoint returns a plain array
// (Response<IReadOnlyList<T>>), so the DataTable is client-side (serverSide:false): a single ajax fetch
// loads the whole list, with paging/sort/search handled in the browser. Phase 4: create/edit/details live
// on full-page routes (/PositionAssignments/Create|Edit/{id}|Details/{id}); the list only navigates there.
// Delete stays as an AJAX row action (there is NO archive endpoint). Position + user names are resolved
// client-side from the loaded lookups for the table renderers.
const PositionAssignmentsList = (function () {
    let dt;
    let L = {};
    const dtTableEl = document.querySelector('.datatables-position-assignments');
    const endpoint = '/PositionAssignments/api';
    const positionsEndpoint = '/PositionAssignments/api/positions';
    const usersEndpoint = '/PositionAssignments/api/users';
    const createUrl = '/PositionAssignments/Create';
    const editUrl = '/PositionAssignments/Edit';
    const detailsUrl = '/PositionAssignments/Details';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5];
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5];
    const totalColumnCount = 7; // control(0) + position/user/effectiveFrom/effectiveTo/status(1-5) + action(6)
    const baseOrder = [[1, 'asc']];
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Organization', pageKey: 'PositionAssignments' };
    let appliedFilters = { status: '' };
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    // Loaded reference data + id→label maps used by the table renderers.
    let assignmentsData = [];
    let positionsData = [];
    let usersData = [];
    const positionMap = {};
    const userMap = {};

    const loadL10n = () => {
        const node = document.getElementById('position-assignments-l10n');
        if (!node) return;
        try {
            const raw = JSON.parse(node.textContent || '{}');
            const toPascal = (key) => key.charAt(0).toUpperCase() + key.slice(1);
            Object.keys(raw).forEach((key) => { L[toPascal(key)] = raw[key]; });
        } catch (error) {
            console.error('[PositionAssignments] L10n payload could not be parsed.', error);
        }
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');
    const toDateString = (value) => (value ? String(value).slice(0, 10) : '');

    // Derived status → colored badge. Uppercase-compare the server-computed derivedStatus.
    const statusBadge = (value) => {
        switch (String(value ?? '').toUpperCase()) {
            case 'PLANNED':
                return `<span class="badge bg-label-info">${escapeHtml(L.StatusPlanned || 'Planned')}</span>`;
            case 'ACTIVE':
                return `<span class="badge bg-label-success">${escapeHtml(L.StatusActive || 'Active')}</span>`;
            case 'ENDED':
                return `<span class="badge bg-label-secondary">${escapeHtml(L.StatusEnded || 'Ended')}</span>`;
            default:
                return `<span class="badge bg-label-secondary">${escapeHtml(value || '-')}</span>`;
        }
    };

    const positionLabel = (position) => {
        const id = position.id || position.Id;
        const code = position.code || position.Code || '';
        const name = position.name || position.Name || '';
        return { id, text: code ? `${code} — ${name}` : name };
    };

    const userLabel = (user) => {
        const id = user.id || user.Id;
        const email = user.email || user.Email || '';
        const first = normalizeString(user.firstName || user.FirstName);
        const last = normalizeString(user.lastName || user.LastName);
        const fullName = `${first} ${last}`.trim();
        return { id, text: fullName ? `${fullName} (${email})` : email };
    };

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue);
    };

    const rowActionHandlers = {
        details: ({ id, row }) => {
            const rid = id || row?.id || row?.Id;
            if (rid) window.location.href = `${detailsUrl}/${encodeURIComponent(rid)}`;
        },
        edit: ({ id, row }) => {
            const rid = id || row?.id || row?.Id;
            if (rid) window.location.href = `${editUrl}/${encodeURIComponent(rid)}`;
        },
        delete: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (!rowId) return;
            const entityName = (row && positionMap[row.positionId || row.PositionId]) || '';
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const response = await fetch(`${endpoint}/${encodeURIComponent(rowId)}`, { method: 'DELETE', headers: getAuthHeaders() });
                    if (!response.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted', entityName);
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }, { entityName, type: 'danger', confirmButtonText: L.Delete });
        }
    };

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };

    const fetchAssignments = () => fetch(`${endpoint}`, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList);

    // Position lookup is best-effort: a failure resolves to [] so the table still renders.
    const fetchPositions = () => fetch(positionsEndpoint, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList)
        .catch(() => []);

    // User lookup returns a PaginatedResult ({ items, totalCount, ... }), NOT the Response<T> envelope.
    const fetchUsers = () => fetch(usersEndpoint, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then((payload) => payload?.items || payload?.Items || [])
        .catch(() => []);

    const rebuildMaps = () => {
        Object.keys(positionMap).forEach((k) => delete positionMap[k]);
        Object.keys(userMap).forEach((k) => delete userMap[k]);
        positionsData.forEach((p) => { const { id, text } = positionLabel(p); if (id) positionMap[id] = text; });
        usersData.forEach((u) => { const { id, text } = userLabel(u); if (id) userMap[id] = text; });
    };

    // Client-side Status filter on the server-computed derivedStatus.
    const applyClientFilter = (rows) => {
        const s = normalizeString(appliedFilters.status).toUpperCase();
        if (!s) return rows;
        return rows.filter((r) => String(r.derivedStatus ?? r.DerivedStatus ?? '').toUpperCase() === s);
    };

    // ─── Save View: normalization + state capture / (de)serialization ────────
    const emptyFilters = () => ({ status: '' });
    const normalizeFilters = (f) => ({ status: normalizeString((f || {}).status) });
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
    const captureColVis = (api) => { const r = {}; saveViewColumnIndexes.forEach((ci) => { try { r[ci] = !!api.column(ci).visible(); } catch (e) { } }); return r; };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = defaultVisibleColumnIndexes.includes(ci); return a; }, {});
    const applyColVis = (api, colVis) => {
        const n = normalizeColVis(colVis);
        if (!n) return;
        saveViewColumnIndexes.forEach((ci) => { if (typeof n[ci] === 'boolean') { try { api.column(ci).visible(n[ci], false); } catch (e) { } } });
    };
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
        return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
    };
    const captureColOrder = (api) => { try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (e) { return null; } };
    const applyColOrder = (api, order) => { const n = normalizeColOrder(order); if (n && typeof api?.colReorder?.order === 'function') api.colReorder.order(n, true); };
    const identityColOrder = () => Array.from({ length: totalColumnCount }, (_, i) => i);

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
        filters: { status: normalizeString((v?.filters || {}).status) },
        search: normalizeString(v?.search),
        colVis: normalizeColVis(v?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(v?.columnOrder) || identityColOrder(),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || view || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || identityColOrder(),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getResetBaselineState = () => normalizeViewState({ filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: identityColOrder(), order: baseOrder });

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
        return { filters: normalizeFilters(d.filters || d), search: normalizeString(d.search), colVis: normalizeColVis(d.colVis), columnOrder: normalizeColOrder(d.columnOrder), order: Array.isArray(d.order) ? d.order : null };
    };
    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || { filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: identityColOrder(), order: baseOrder };
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
            console.error('[PositionAssignments SaveView] Failed to load saved views.', error);
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
        const savedResponse = existingId ? await personalizationClient.updateView(existingId, payload) : await personalizationClient.saveView(payload);
        const savedRecord = unwrapViewResponse(savedResponse);
        defaultViewRecord = savedRecord && typeof savedRecord === 'object' ? savedRecord : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
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
    const initSelect2Filters = () => {
        if (!window.jQuery?.fn?.select2) return;
        $('#inlineFilterHost select.select2').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                minimumResultsForSearch: Infinity,
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                placeholder: $select.data('placeholder') || '',
                closeOnSelect: true,
                allowClear: true
            });
        });
    };
    const syncFilterControls = (values) => {
        const el = document.getElementById('filterStatus');
        if (el) { el.value = values.status || ''; if (window.jQuery?.fn?.select2) $(el).val(values.status || '').trigger('change'); }
    };
    const getAppliedFilterCount = () => [appliedFilters.status].filter(hasFilterValue).length;
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
        dt?.ajax.reload(() => { window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount()); }, false);
    };
    const setupFilters = (api) => {
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = { status: document.getElementById('filterStatus')?.value || '' };
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
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

    const initDataTable = () => {
        if (!dtTableEl || !window.DtDefaults) {
            console.error('[PositionAssignments] DataTable element or DtDefaults not found.');
            return;
        }

        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
            action: () => toggleInlineFilter()
        };
        const saveFilterBtn = {
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
                    console.error('[PositionAssignments SaveView] Failed to save default view.', error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }
        };

        const dtConfig = window.DtDefaults.create({
            processing: true,
            serverSide: false,
            stateSave: false,
            order: baseOrder,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: function (data, callback) {
                Promise.all([fetchAssignments(), fetchPositions(), fetchUsers()])
                    .then(([assignments, positions, users]) => {
                        assignmentsData = assignments || [];
                        positionsData = positions || [];
                        usersData = users || [];
                        rebuildMaps();
                        callback({ data: applyClientFilter(assignmentsData) });
                    })
                    .catch(() => {
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                        callback({ data: [] });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                {
                    data: 'positionId', name: 'position',
                    render: (value) => escapeHtml(positionMap[value] || value || '-')
                },
                {
                    data: 'userId', name: 'user',
                    render: (value) => escapeHtml(userMap[value] || value || '-')
                },
                { data: 'effectiveFrom', name: 'effectiveFrom', render: (value) => escapeHtml(toDateString(value) || '-') },
                { data: 'effectiveTo', name: 'effectiveTo', render: (value) => escapeHtml(value ? toDateString(value) : '-') },
                {
                    data: 'derivedStatus', name: 'status', orderable: false,
                    render: (value) => statusBadge(value)
                },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (value, type, row) => {
                        const id = row.id || row.Id;
                        const rowJson = JSON.stringify(row);

                        const actions = [
                            { key: 'details', icon: 'bx bx-show', text: L.ViewDetails || '', attrs: { 'data-id': id, 'data-json': rowJson } },
                            { key: 'edit', icon: 'bx bx-edit', className: 'js-edit-item', text: L.Edit || '', attrs: { 'data-id': id, 'data-json': rowJson } },
                            { key: 'delete', icon: 'bx bx-trash', className: 'text-danger', text: L.Delete || '', attrs: { 'data-id': id, 'data-json': rowJson } }
                        ];

                        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
                    }
                }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 2, responsivePriority: 1 },
                { targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNew || '', {}, { filterBtn, saveFilterBtn }, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                setupFilters(api);
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        });

        if (L.Showing) {
            dtConfig.language = dtConfig.language || {};
            dtConfig.language.info = `${L.Showing} _START_ - _END_ / _TOTAL_`;
        }

        dt = new DataTable(dtTableEl, dtConfig);

        window.DitenDataTable?.bindActionDispatcher?.({
            tableEl: dtTableEl,
            dt,
            onRowAction: rowActionHandlers
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

    // ─── Add New → full-page create route ────────────────────────────────────
    const bindAddNew = () => {
        // Delegated: the Add New button is rendered by DataTables into the toolbar, so bind at document level.
        document.addEventListener('click', (event) => {
            if (event.target.closest('.add-new')) {
                event.preventDefault();
                window.location.href = createUrl;
            }
        });
    };

    // After a full-page create/edit/details save redirects back here, surface the toast it stashed.
    const flushToast = () => {
        try {
            const msg = sessionStorage.getItem('a-toast');
            if (msg) { sessionStorage.removeItem('a-toast'); window.showToast?.(msg, 'success'); }
        } catch { /* ignore */ }
    };

    const init = async () => {
        loadL10n();
        flushToast();
        bindAddNew();
        await loadDefaultView();
        initDataTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => PositionAssignmentsList.init());
