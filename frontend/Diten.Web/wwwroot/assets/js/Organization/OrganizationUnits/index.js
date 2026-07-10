'use strict';

// MOD-0288 — Organization Units (platform-admin). Backend list endpoints return a plain array
// (Response<IReadOnlyList<T>>), so the DataTable is client-side (serverSide:false): a single ajax fetch
// loads the whole list, with paging/sort/search handled in the browser. Phase 2: create/edit/details live
// on full-page routes (/OrganizationUnits/Create|Edit/{id}|Details/{id}); the list only navigates there.
// Archive + delete stay as AJAX row actions. A Table/Tree toggle renders the same loaded list either as the
// DataTable or as an indented parent→child hierarchy.
const OrganizationUnitsList = (function () {
    let dt;
    let L = {};
    const dtTableEl = document.querySelector('.datatables-organization-units');
    const endpoint = '/OrganizationUnits/api';
    const legalEntitiesEndpoint = '/OrganizationUnits/api/legal-entities';
    const createUrl = '/OrganizationUnits/Create';
    const editUrl = '/OrganizationUnits/Edit';
    const detailsUrl = '/OrganizationUnits/Details';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5];
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5];
    const totalColumnCount = 7; // control(0) + code/name/legalEntity/parent/isArchived(1-5) + action(6)
    const baseOrder = [[1, 'asc']];
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Organization', pageKey: 'OrganizationUnits' };
    let appliedFilters = { archived: '' };
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    // Loaded reference data + id→label maps used by the table renderers and the tree.
    let orgUnitsData = [];
    let legalEntitiesData = [];
    const orgUnitMap = {};
    const legalEntityMap = {};

    const loadL10n = () => {
        const node = document.getElementById('organization-units-l10n');
        if (!node) return;
        try {
            const raw = JSON.parse(node.textContent || '{}');
            const toPascal = (key) => key.charAt(0).toUpperCase() + key.slice(1);
            Object.keys(raw).forEach((key) => { L[toPascal(key)] = raw[key]; });
        } catch (error) {
            console.error('[OrganizationUnits] L10n payload could not be parsed.', error);
        }
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const archivedBadge = (value) => value
        ? `<span class="badge bg-label-secondary">${escapeHtml(L.StatusArchived || 'Archived')}</span>`
        : `<span class="badge bg-label-success">${escapeHtml(L.StatusActive || 'Active')}</span>`;

    const legalEntityLabel = (entity) => {
        const id = entity.legalEntityId || entity.id || entity.Id || entity.LegalEntityId;
        const name = entity.displayName || entity.DisplayName || entity.legalName || entity.LegalName || '';
        const code = entity.code || entity.Code || '';
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

    const fetchOrgUnits = () => fetch(`${endpoint}`, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList);

    // Legal-entity lookup (referenceable-only) is best-effort: a failure (e.g. MDM offline) resolves to [] so the
    // table still renders; the select just has no options to choose (no manual-GUID fallback).
    const fetchLegalEntities = () => fetch(legalEntitiesEndpoint, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then((payload) => unwrapList(payload))
        .catch(() => []);

    const rebuildMaps = () => {
        Object.keys(orgUnitMap).forEach((k) => delete orgUnitMap[k]);
        Object.keys(legalEntityMap).forEach((k) => delete legalEntityMap[k]);
        orgUnitsData.forEach((u) => { orgUnitMap[u.id || u.Id] = u.name || u.Name || ''; });
        legalEntitiesData.forEach((e) => { const { id, text } = legalEntityLabel(e); if (id) legalEntityMap[id] = text; });
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
            console.error('[OrganizationUnits SaveView] Failed to load saved views.', error);
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
            console.error('[OrganizationUnits] DataTable element or DtDefaults not found.');
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
                    console.error('[OrganizationUnits SaveView] Failed to save default view.', error);
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
                Promise.all([fetchOrgUnits(), fetchLegalEntities()])
                    .then(([units, legalEntities]) => {
                        orgUnitsData = units || [];
                        legalEntitiesData = legalEntities || [];
                        rebuildMaps();
                        renderTree();
                        callback({ data: applyClientFilter(orgUnitsData) });
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
                    data: 'legalEntityId', name: 'legalEntity',
                    render: (value) => escapeHtml(legalEntityMap[value] || value || '-')
                },
                {
                    data: 'parentOrganizationUnitId', name: 'parent',
                    render: (value) => escapeHtml(value ? (orgUnitMap[value] || value) : '-')
                },
                { data: 'isArchived', name: 'isArchived', render: (value) => archivedBadge(value) },
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

    // ─── Table/Tree toggle ───────────────────────────────────────────────────
    const setView = (view) => {
        const tableView = document.getElementById('orgUnitsTableView');
        const treeView = document.getElementById('orgUnitsTreeView');
        const btnFlat = document.getElementById('btnViewFlat');
        const btnTree = document.getElementById('btnViewTree');
        const isTree = view === 'tree';

        tableView?.classList.toggle('d-none', isTree);
        treeView?.classList.toggle('d-none', !isTree);

        // Clean segmented control: both buttons are btn-outline-primary; the active one is filled
        // via Bootstrap's .active state, so the group joins without the outline/solid border clash.
        btnFlat?.classList.toggle('active', !isTree);
        btnTree?.classList.toggle('active', isTree);
    };

    const bindViewToggle = () => {
        document.getElementById('btnViewFlat')?.addEventListener('click', () => setView('flat'));
        document.getElementById('btnViewTree')?.addEventListener('click', () => setView('tree'));
    };

    // ─── Tree view — reusable DitenTree component ────────────────────────────
    // The same loaded list is rendered as an expand/collapse hierarchy with row actions and
    // drag-to-reparent. Node identity/parent come from the org-unit fields; drag moves reuse the
    // Update endpoint (fetch full entity → change parent → PUT) so enterprise fields survive.
    let orgTree = null;

    const ouId = (n) => n.id ?? n.Id;
    const ouParent = (n) => n.parentOrganizationUnitId ?? n.ParentOrganizationUnitId ?? null;
    const ouLegalEntity = (n) => n.legalEntityId ?? n.LegalEntityId ?? null;
    const ouArchived = (n) => Boolean(n.isArchived ?? n.IsArchived);

    // Level-based node glyphs (HQ / division / department / team → deeper).
    const TREE_ICONS = [
        '<path d="M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18Z"/><path d="M10 6h4M10 10h4M10 14h4"/>',
        '<rect x="3" y="7" width="18" height="14" rx="2"/><path d="M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>',
        '<circle cx="9" cy="8" r="3"/><path d="M15 11a3 3 0 1 0 0-6M3 20a6 6 0 0 1 12 0M15.5 14A6 6 0 0 1 21 20"/>',
        '<circle cx="12" cy="12" r="3"/><path d="M12 2v7M12 15v7M2 12h7M15 12h7"/>'
    ];
    const ACT_ICONS = {
        addChild: '<path d="M12 5v14M5 12h14"/>',
        edit: '<path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z"/>',
        details: '<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/>',
        archive: '<rect x="3" y="4" width="18" height="4" rx="1"/><path d="M5 8v11a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8M10 12h4"/>'
    };

    const goTo = (base, id) => { if (id) window.location.href = `${base}/${encodeURIComponent(id)}`; };
    const addChild = (parentId) => { window.location.href = `${createUrl}?parentId=${encodeURIComponent(parentId)}`; };

    // Map a full org-unit DTO back onto the Update request shape, changing only the parent.
    const buildReparentPayload = (d, newParentId) => ({
        code: d.code ?? d.Code ?? '',
        name: d.name ?? d.Name ?? '',
        legalEntityId: d.legalEntityId ?? d.LegalEntityId ?? null,
        parentOrganizationUnitId: newParentId,
        orgUnitType: d.orgUnitType ?? d.OrgUnitType ?? 'Department',
        managerPositionId: d.managerPositionId ?? d.ManagerPositionId ?? null,
        description: d.description ?? d.Description ?? null,
        status: d.status ?? d.Status ?? 'Active',
        effectiveFrom: d.effectiveFrom ?? d.EffectiveFrom ?? null,
        effectiveTo: d.effectiveTo ?? d.EffectiveTo ?? null
    });

    // Backend rule: parent must be the same Legal Entity, not archived, and must not create a cycle
    // (the component already blocks own-subtree drops). Return true or a localized reason to show.
    const canReparent = (dragNode, targetNode) => {
        if (ouArchived(dragNode) || (targetNode && ouArchived(targetNode))) return L.MoveArchivedBlocked || 'blocked';
        if (targetNode && ouLegalEntity(dragNode) !== ouLegalEntity(targetNode)) return L.MoveSameLegalEntityOnly || 'blocked';
        return true;
    };

    const reparent = async (dragNode, targetNode) => {
        const id = ouId(dragNode);
        const newParentId = targetNode ? ouId(targetNode) : null;
        try {
            const full = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { headers: getAuthHeaders() })
                .then((r) => r.ok ? r.json() : Promise.reject(r))
                .then((p) => p?.data ?? p?.Data ?? {});
            const res = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
                body: JSON.stringify(buildReparentPayload(full, newParentId))
            });
            if (!res.ok) {
                let msg = L.ErrorOccurred || '';
                try { const j = await res.json(); const e = (j.errors || j.Errors || []); if (e.length) msg = e.join(' '); } catch { /* non-JSON */ }
                window.showToast?.(msg, 'error');
                dt?.ajax.reload(null, false); // snap the tree back to server truth
                return;
            }
            reloadWithSuccessToast('MoveSuccess', full.name || full.Name || '');
        } catch (error) {
            console.error('[OrganizationUnits] Reparent failed.', error);
            window.showToast?.(L.ErrorOccurred || '', 'error');
            dt?.ajax.reload(null, false);
        }
    };

    const treeConfig = () => ({
        data: orgUnitsData,
        idField: 'id',
        parentField: 'parentOrganizationUnitId',
        expandDepth: 1,
        addLabel: L.AddNew || '',
        onAdd: () => { window.location.href = createUrl; },
        label: (node, level) => ({
            title: node.name ?? node.Name ?? '',
            code: node.code ?? node.Code ?? '',
            subtitle: legalEntityMap[ouLegalEntity(node)] || '',
            statusHtml: archivedBadge(ouArchived(node)),
            icon: TREE_ICONS[Math.min(level, TREE_ICONS.length - 1)],
            iconLevel: level
        }),
        actions: [
            { key: 'addChild', icon: ACT_ICONS.addChild, title: L.AddChild || '', variant: 'primary', visible: (n) => !ouArchived(n), handler: (n) => addChild(ouId(n)) },
            { key: 'details', icon: ACT_ICONS.details, title: L.ViewDetails || '', handler: (n) => goTo(detailsUrl, ouId(n)) },
            { key: 'edit', icon: ACT_ICONS.edit, title: L.Edit || '', visible: (n) => !ouArchived(n), handler: (n) => goTo(editUrl, ouId(n)) },
            { key: 'archive', icon: ACT_ICONS.archive, title: L.Archive || '', variant: 'danger', visible: (n) => !ouArchived(n), handler: (n) => rowActionHandlers.archive({ row: n, id: ouId(n) }) }
        ],
        drag: {
            enabled: true,
            canDrag: (n) => !ouArchived(n),
            canDrop: canReparent,
            onDrop: reparent,
            onReject: (reason) => {
                const msg = reason === 'cycle' ? (L.MoveCycleBlocked || '') : (typeof reason === 'string' ? reason : '');
                if (msg) window.showToast?.(msg, 'warning');
            }
        },
        l10n: {
            expandAll: L.ExpandAll, collapseAll: L.CollapseAll,
            searchPlaceholder: L.TreeSearchPlaceholder || L.Search,
            empty: L.TreeEmpty, emptyHint: ''
        }
    });

    const renderTree = () => {
        const host = document.getElementById('orgUnitsTree');
        if (!host || !window.DitenTree) return;
        if (orgTree) orgTree.setData(orgUnitsData);
        else orgTree = window.DitenTree.create('#orgUnitsTree', treeConfig());
    };

    // After a full-page create/edit/details save redirects back here, surface the toast it stashed.
    const flushToast = () => {
        try {
            const msg = sessionStorage.getItem('ou-toast');
            if (msg) { sessionStorage.removeItem('ou-toast'); window.showToast?.(msg, 'success'); }
        } catch { /* ignore */ }
    };

    const init = async () => {
        loadL10n();
        flushToast();
        bindAddNew();
        bindViewToggle();
        await loadDefaultView();
        initDataTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => OrganizationUnitsList.init());
