'use strict';

// Positions (platform-admin). Backend list endpoints return a plain array
// (Response<IReadOnlyList<T>>), so the DataTable is client-side (serverSide:false): a single ajax
// fetch loads the whole list, with paging/sort/search handled in the browser. Phase 3: create/edit/details
// live on full-page routes (/Positions/Create|Edit/{id}|Details/{id}); the list only navigates there.
// Archive + delete stay as AJAX row actions. Org-unit + reports-to names are resolved client-side from the
// loaded lists for the table renderers.
const PositionsList = (function () {
    let dt;
    let L = {};
    const dtTableEl = document.querySelector('.datatables-positions');
    const endpoint = '/Positions/api';
    const orgUnitsEndpoint = '/Positions/api/org-units';
    const createUrl = '/Positions/Create';
    const editUrl = '/Positions/Edit';
    const detailsUrl = '/Positions/Details';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6];
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5, 6];
    const totalColumnCount = 8; // control(0) + code/name/orgUnit/reportsTo/isArchived/occupancy(1-6) + action(7)
    const baseOrder = [[1, 'asc']];
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Organization', pageKey: 'Positions' };
    let appliedFilters = { archived: '' };
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    // Loaded reference data + id→label maps used by the table renderers.
    let positionsData = [];
    let orgUnitsData = [];
    const orgUnitMap = {};
    const positionMap = {};

    const loadL10n = () => {
        const node = document.getElementById('positions-l10n');
        if (!node) return;
        try {
            const raw = JSON.parse(node.textContent || '{}');
            const toPascal = (key) => key.charAt(0).toUpperCase() + key.slice(1);
            Object.keys(raw).forEach((key) => { L[toPascal(key)] = raw[key]; });
        } catch (error) {
            console.error('[Positions] L10n payload could not be parsed.', error);
        }
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const archivedBadge = (value) => value
        ? `<span class="badge bg-label-secondary">${escapeHtml(L.StatusArchived || 'Archived')}</span>`
        : `<span class="badge bg-label-success">${escapeHtml(L.StatusActive || 'Active')}</span>`;

    const occupancyBadge = (isVacant, activeAssignmentCount) => isVacant
        ? `<span class="badge bg-label-secondary">${escapeHtml(L.Vacant || 'Vacant')}</span>`
        : `<span class="badge bg-label-success">${escapeHtml(`${L.Occupied || 'Occupied'} (${activeAssignmentCount ?? 0})`)}</span>`;

    const orgUnitLabel = (unit) => {
        const id = unit.id || unit.Id;
        const name = unit.name || unit.Name || '';
        const code = unit.code || unit.Code || '';
        return { id, text: code ? `${code} — ${name}` : name };
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
        archive: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (!rowId) return;
            const entityName = row?.name || row?.Name || row?.code || row?.Code || '';
            window.showConfirm?.(L.ArchiveConfirm || L.AreYouSure, async () => {
                try {
                    const response = await fetch(`${endpoint}/${encodeURIComponent(rowId)}/archive`, { method: 'POST', headers: getAuthHeaders() });
                    if (!response.ok) throw new Error('Archive failed.');
                    reloadWithSuccessToast('RecordArchived', entityName);
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }, { entityName, type: 'warning', confirmButtonText: L.Archive });
        },
        delete: ({ row, id }) => {
            const rowId = id || row?.id || row?.Id;
            if (!rowId) return;
            const entityName = row?.name || row?.Name || row?.code || row?.Code || '';
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

    const fetchPositions = () => fetch(`${endpoint}`, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList);

    // Org-units lookup feeds the OrgUnit column renderer. Platform org-units is always reachable, but a
    // failure resolves to [] so the table still renders.
    const fetchOrgUnits = () => fetch(orgUnitsEndpoint, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList)
        .catch(() => []);

    const rebuildMaps = () => {
        Object.keys(orgUnitMap).forEach((k) => delete orgUnitMap[k]);
        Object.keys(positionMap).forEach((k) => delete positionMap[k]);
        orgUnitsData.forEach((u) => { const { id, text } = orgUnitLabel(u); if (id) orgUnitMap[id] = text; });
        positionsData.forEach((p) => { positionMap[p.id || p.Id] = p.name || p.Name || ''; });
    };

    const applyClientFilter = (rows) => {
        if (appliedFilters.archived === '') return rows;
        const wantArchived = appliedFilters.archived === 'true';
        return rows.filter((r) => Boolean(r.isArchived ?? r.IsArchived) === wantArchived);
    };

    // ─── Save View: normalization + state capture / (de)serialization ────────
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v).trim()));
    const emptyFilters = () => ({ archived: '' });
    const normalizeFilters = (f) => ({ archived: normalizeString((f || {}).archived) });
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
        filters: { archived: normalizeString((v?.filters || {}).archived) },
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
            console.error('[Positions SaveView] Failed to load saved views.', error);
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
    const syncFilterControls = (values) => {
        const el = document.getElementById('filterArchived');
        if (el) { el.value = values.archived || ''; if (window.jQuery?.fn?.select2) $(el).val(values.archived || '').trigger('change'); }
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
        dt?.ajax.reload(() => { window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount()); }, false);
    };
    const setupFilters = (api) => {
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = { archived: document.getElementById('filterArchived')?.value || '' };
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
            console.error('[Positions] DataTable element or DtDefaults not found.');
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
                    console.error('[Positions SaveView] Failed to save default view.', error);
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
                Promise.all([fetchPositions(), fetchOrgUnits()])
                    .then(([positions, orgUnits]) => {
                        positionsData = positions || [];
                        orgUnitsData = orgUnits || [];
                        rebuildMaps();
                        callback({ data: applyClientFilter(positionsData) });
                    })
                    .catch(() => {
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                        callback({ data: [] });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'code', name: 'code', render: (value) => `<span class="fw-medium font-monospace text-primary">${escapeHtml(value)}</span>` },
                { data: 'name', name: 'name', render: escapeHtml },
                {
                    data: 'organizationUnitId', name: 'orgUnit',
                    render: (value) => escapeHtml(orgUnitMap[value] || value || '-')
                },
                {
                    data: 'reportsToPositionId', name: 'reportsTo',
                    render: (value) => escapeHtml(value ? (positionMap[value] || value) : '-')
                },
                { data: 'isArchived', name: 'isArchived', render: (value) => archivedBadge(value) },
                {
                    data: 'isVacant', name: 'occupancy', orderable: false,
                    render: (value, type, row) => occupancyBadge(
                        Boolean(row.isVacant ?? row.IsVacant),
                        row.activeAssignmentCount ?? row.ActiveAssignmentCount ?? 0
                    )
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
                        const isArchived = Boolean(row.isArchived ?? row.IsArchived);

                        const actions = [
                            { key: 'details', icon: 'bx bx-show', text: L.ViewDetails || '', attrs: { 'data-id': id, 'data-json': rowJson } },
                            { key: 'edit', icon: 'bx bx-edit', className: 'js-edit-item', text: L.Edit || '', attrs: { 'data-id': id, 'data-json': rowJson } }
                        ];
                        if (!isArchived) {
                            actions.push({ key: 'archive', icon: 'bx bx-archive-in', className: 'text-warning', text: L.Archive || '', attrs: { 'data-id': id, 'data-json': rowJson } });
                        }
                        actions.push({ key: 'delete', icon: 'bx bx-trash', className: 'text-danger', text: L.Delete || '', attrs: { 'data-id': id, 'data-json': rowJson } });

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

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;

        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById('inlineFilterCollapse');
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

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter((value) => value !== '').length;

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
            const msg = sessionStorage.getItem('p-toast');
            if (msg) { sessionStorage.removeItem('p-toast'); window.showToast?.(msg, 'success'); }
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

document.addEventListener('DOMContentLoaded', () => PositionsList.init());
