/**
 * MOD-0024 Task Field Definitions — DataTables Index Script.
 *
 * A COPY of the GoldenReferenceCompact index script, adapted only where this catalogue genuinely differs: the
 * module/page identity, the API base, the column set and the filter set. The structure — personalization,
 * inline filter, save-view, bulk bar, export/colvis — is the reference's and is deliberately not re-invented,
 * because two management screens shipped in one week have to read as one product.
 */
'use strict';

const TaskFieldDefinitionList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-tasktypes');
    /*
     * The SAME-ORIGIN proxy, not a service port. Diten.Web forwards to Platform with the JWT read from the
     * HTTP-only cookie server-side, so the browser never addresses a gateway and never holds a token — the same
     * seam WorkCenterNext's work-item feed uses.
     */
    const apiUrl = '/Tasks/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Tasks', pageKey: 'TaskTypes' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6, 7];
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { status: [], valueType: [], section: [] };
    let L = window.L10n || {};

    /*
     * The LABEL, from whichever source the definition actually has.
     *
     * A SYSTEM definition carries a resource key and the dictionary translates it. A TENANT definition carries
     * the administrator's own words and is printed as typed. Neither is a fallback for the other, and the CODE is
     * never printed as a label: a raw key (or a raw code) on screen is the defect this split exists to prevent,
     * and it has happened in this codebase before.
     */
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    })[c]);

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const getAuthHeaders = (includeJson = false) =>
        window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const sortNormalizedArray = (v) => normalizeArray(v).slice().sort((a, b) => a.localeCompare(b));
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ status: [], valueType: [], section: [] });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            status: normalizeArray(source.status),
            valueType: normalizeArray(source.valueType),
            section: normalizeArray(source.section)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(normalizeString(actual));
    };
    const matchesSingleFilter = (selected, actual) => {
        const norm = normalizeString(selected);
        return !norm || normalizeString(actual) === norm;
    };
    const matchesStatusFilter = (selected, isActive) => {
        const norm = normalizeArray(selected);
        if (!norm.length) return true;
        return norm.includes(isActive ? 'Active' : 'Passive');
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
        filters: Object.keys(v?.filters || {}).sort().reduce((acc, key) => {
            acc[key] = normalizeFilterValue(v.filters[key]);
            return acc;
        }, {}),
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
        const baseline = defaultViewState || {
            filters: emptyFilters(),
            search: '',
            colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
            order: baseOrder
        };
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
            console.error('[TaskTypes SaveView] Failed to load saved views.', error);
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
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.compactFilterBound === '1') return;
        dtTableEl.dataset.compactFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesStatusFilter(appliedFilters.status, row.isActive)
                && matchesMultiFilter(appliedFilters.valueType, row.valueType)
                && matchesMultiFilter(appliedFilters.section, row.section);
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
        $('#filterStatus, #filterValueType, #filterSection').each(function () {
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
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterValueType').val(normalizeArray(values.valueType)).trigger('change');
        $('#filterSection').val(normalizeArray(values.section)).trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.status, appliedFilters.valueType, appliedFilters.section].filter(hasFilterValue).length;

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

    const appendOptions = (select, items) => {
        select.innerHTML = '';
        items.forEach((item) => {
            if (!item?.value) return;
            const opt = document.createElement('option');
            opt.value = item.value;
            opt.textContent = item.text || item.value;
            select.appendChild(opt);
        });
    };
    /*
     * Filter options come from the ROWS THIS TABLE ALREADY HAS, not from a lookup endpoint.
     *
     * The copied version fetched /GoldenReferenceCompact/lookups — a SIBLING MODULE's endpoint — for
     * referenceType/category/owner/priority, none of which a field definition has. It then looked for four
     * selects this screen never renders and returned early every time, so the Value type and Section filters
     * were permanently empty and inert: declared in the markup, never populated, never applied.
     *
     * `valueType` and `section` are closed sets that arrive with the row data and the catalogue is small enough
     * to load in one page, so a second round-trip would buy nothing and could disagree with what is on screen.
     */
    const collectDistinct = (rows, field) => Array.from(new Set(
        rows.map((row) => row?.[field]).filter((value) => value !== null && value !== undefined && value !== '')))
        .sort((a, b) => String(a).localeCompare(String(b), window.CurrentLanguage || undefined));

    const populateFilterOptions = (api) => {
        const valueTypeSelect = document.getElementById('filterValueType');
        const sectionSelect = document.getElementById('filterSection');
        if (!valueTypeSelect && !sectionSelect) return;

        const rows = api ? api.rows().data().toArray() : [];
        // Selections survive a repopulate — a redraw must never silently drop an applied filter.
        const fill = (select, field) => {
            if (!select) return;
            const selected = Array.from(select.selectedOptions || []).map((option) => option.value);
            appendOptions(select, collectDistinct(rows, field).map((value) => ({ value, text: value })));
            $(select).val(selected).trigger('change');
        };
        fill(valueTypeSelect, 'valueType');
        fill(sectionSelect, 'section');
    };

    const setupFilters = async (api) => {
        initSelect2Filters();
        // Options derive from row data, so they can only be built once the table has drawn.
        populateFilterOptions(api);
        api.on('draw.dt.fieldDefinitionFilters', () => populateFilterOptions(api));
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterStatus').val() || [],
                valueType: $('#filterValueType').val() || [],
                section: $('#filterSection').val() || []
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

    const getStatusMap = () => ({
        true: { title: L.Active, class: 'bg-label-success' },
        false: { title: L.Passive, class: 'bg-label-secondary' }
    });
    const formatDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? String(v).slice(0, 10) : d.toLocaleDateString(window.CurrentLanguage || undefined);
    };
    const setText = (id, value) => {
        const el = document.getElementById(id);
        if (el) el.innerText = value || '-';
    };

    /*
     * ⚠ NO BULK OPTIONS, AND NO BULK BAR — the sibling's were removed rather than pointed somewhere harmless.
     *
     * A task type is RETIRED, never deleted, so there is nothing to select rows for. Leaving the handler wired
     * to a bar that is not drawn would be a listener for markup nothing emits, which is precisely the class of
     * dead code this codebase spent a round removing five instances of.
     */
    const reloadWithSuccessToast = (messageKey, interpolationValue) =>
        window.DitenDataTable.reloadWithToast(dt, dtTableEl, messageKey, interpolationValue);
    const rowActionHandlers = {
        quickView: ({ id }) => {
            if (id) window.location.href = `/Tasks/TaskTypes/Details/${id}`;
        },
        edit: ({ id }) => {
            if (id) window.location.href = `/Tasks/TaskTypes/Edit/${id}`;
        },
        /*
         * ── RETIRE, NEVER DELETE (DCP-005 slice 1) ─────────────────────────────────────────────────────
         *
         * The sibling's handler issued `DELETE /field-definitions/{id}`. There is no such route here, on
         * purpose: a type that has been used is part of the identity of every task opened under it, so removing
         * it would make that history unreadable — the same rule folders and controlled documents follow.
         *
         * ⚠ NO CONFIRM DIALOG ON THE WAY BACK IN. Retiring is asked for because it is reversible and visibly
         * so; restoring is not a destructive act and a dialog in front of it is a toll, not a guard.
         */
        toggleActive: ({ row }) => {
            if (!row?.id) { return; }
            const next = !row.isActive;
            const apply = async () => {
                try {
                    const res = await fetch(`${apiUrl}/task-types/${row.id}/active`, {
                        method: 'PUT',
                        credentials: 'include',
                        headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                        body: JSON.stringify({ isActive: next })
                    });
                    if (!res.ok) { throw new Error('Toggle failed.'); }
                    reloadWithSuccessToast(next ? 'RecordActivated' : 'RecordDeactivated');
                } catch (error) {
                    if (error?.authHandled) { return; }
                    console.error('[TaskTypes] Failed to change active state.', error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            };
            if (next) { void apply(); return; }
            /*
             * ⚠ TITLE FIRST, SENTENCE AS `subtext` — the shared dialog's own contract (MOD-0013). The first
             * argument is a KEY OR TITLE; the body comes from `options.subtext`. Passing the whole sentence as
             * argument one put it in the title and left the generic "Devam etmek istediğinize emin misiniz?"
             * underneath, which is exactly the mismatch that dialog's comments already record for four other
             * call sites.
             */
            window.showConfirm?.(
                L.Deactivate || L.AreYouSure,
                () => { void apply(); },
                { subtext: L.DeactivateConfirm || '', confirmButtonText: L.Deactivate || '', type: 'warning' });
        },
    };

    const bindEvents = () => {
        // Quick View delegation is handled by DitenDataTable, equivalent to closest('.js-quick-view').
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!dtTableEl) { return; }
        syncL10n();
        await loadDefaultView();
        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'data-bs-toggle': 'tooltip' },
                action: () => window.showToast?.(L.ComingSoon, 'warning')
            },
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
                        console.error('[TaskTypes SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        // DitenDataTable wraps the DataTables v2 constructor and shared defaults:
        // new DataTable(...)
        // window.DtDefaults.create(...)
        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: apiUrl + '/task-types',
                type: 'GET',
                xhrFields: { withCredentials: true }
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'code', name: 'code' },
                    { data: 'name', name: 'name' },
                    { data: 'recordClass', name: 'recordClass' },
                    { data: 'gqmsDomain', name: 'gqmsDomain' },
                    { data: 'isQualityEvent', name: 'isQualityEvent' },
                    { data: 'isActive', name: 'isActive' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    { targets: 3, render: (data) => escapeHtml(data ?? '') },
                    {
                        /*
                         * ⚠ THE CODE IS THE VALUE, THE LABEL IS THE TRANSLATION. `GXP_QUALITY_RECORD` is a code
                         * and is never translated; what the reader sees is a label keyed off it, in their own
                         * language. An unmapped value degrades to the raw code rather than to an empty cell —
                         * a classification that renders as nothing is worse than one that renders as jargon.
                         */
                        targets: 4,
                        render: (data) => data
                            ? `<span class="badge bg-label-${data === 'GXP_QUALITY_RECORD' ? 'danger' : (data === 'OPERATIONAL_RECORD' ? 'info' : 'secondary')}">${L['RecordClass' + data] || data}</span>`
                            : ''
                    },
                    {
                        // Empty is a legitimate answer — work outside every quality domain — so it renders as a
                        // dash rather than as a gap the reader has to interpret.
                        targets: 5,
                        render: (data) => data ? `<span class="badge bg-label-primary">${escapeHtml(data)}</span>` : '<span class="text-muted">-</span>'
                    },
                    {
                        targets: 6,
                        render: (data) => data
                            ? `<span class="badge bg-label-warning">${L.QualityEventYes || ''}</span>`
                            : `<span class="badge bg-label-secondary">${L.QualityEventNo || ''}</span>`
                    },
                    {
                        targets: 7,
                        render: (data, type) => {
                            const status = getStatusMap()[String(!!data)] || { title: L.Unknown, class: 'bg-label-primary' };
                            return type === 'display' ? window.DitenDataTable.renderStatusBadge(data, getStatusMap()) : status.title;
                        }
                    },
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
                                    attrs: { 'data-id': full.id, 'title': L.QuickView }
                                },
                                {
                                    key: 'edit',
                                    className: 'js-edit-item',
                                    icon: 'bx bx-edit',
                                    text: L.Edit,
                                    attrs: { 'data-id': full.id, 'data-json': rowJson }
                                },
                                /*
                                 * ⚠ NOT A DELETE, AND THIS IS THE WHOLE POINT. A type that has been used is part
                                 * of the identity of every task opened under it — removing it would make that
                                 * history unreadable, exactly as it would for a folder or a controlled document.
                                 * The server exposes no delete route either, so this is not a UI-only guard.
                                 */
                                {
                                    key: 'toggleActive',
                                    className: full.isActive ? 'js-deactivate text-warning' : 'js-activate text-success',
                                    icon: full.isActive ? 'bx bx-archive-in' : 'bx bx-undo',
                                    text: full.isActive ? (L.Deactivate || '') : (L.Activate || ''),
                                    attrs: { 'data-id': full.id, 'data-json': rowJson }
                                }
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew,
                    { href: '/Tasks/TaskTypes/Create' },
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5, 6, 7], colvisColumns: [2, 3, 4, 5, 6, 7] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/Tasks/TaskTypes/Create';
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
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TaskFieldDefinitionList.init());
