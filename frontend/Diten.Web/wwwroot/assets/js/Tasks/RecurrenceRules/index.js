/**
 * MOD-0024 Phase 4 — Recurring Task Rules, DataTables index script (BL-052).
 *
 * The shape is the sibling field-definition screen's: personalization, inline filter, save-view, export/colvis.
 * Two management screens in one module have to read as one product, so the structure is copied rather than
 * re-invented.
 *
 * TWO deliberate differences, both because the SERVER differs:
 *   · No bulk bar and no checkbox column — MOD-0024 exposes no bulk-delete for rules. A bulk button wired to an
 *     endpoint that does not exist is a button that fails.
 *   · The frequency column reads "every N periods" from two fields, because that is the sentence a person wrote
 *     when they defined the rule; the raw enum alone answers a different question.
 */
'use strict';

const TaskRecurrenceRuleList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-taskrecurrencerules');
    // The SAME-ORIGIN proxy, not a service port: Diten.Web forwards with the JWT read from the HTTP-only cookie.
    const apiUrl = '/Tasks/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Tasks', pageKey: 'TaskRecurrenceRules' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5, 6, 7];
    const baseOrder = [[1, 'asc']];
    let appliedFilters = { status: [], frequency: [], assignment: [] };
    let L = window.L10n || {};

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
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ status: [], frequency: [], assignment: [] });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            status: normalizeArray(source.status),
            frequency: normalizeArray(source.frequency),
            assignment: normalizeArray(source.assignment)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(normalizeString(actual));
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
            console.error('[RecurrenceRules SaveView] Failed to load saved views.', error);
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
                && matchesMultiFilter(appliedFilters.frequency, row.frequency)
                && matchesMultiFilter(appliedFilters.assignment, row.assignmentTarget);
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
        $('#filterStatus, #filterFrequency, #filterAssignment').each(function () {
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
        $('#filterFrequency').val(normalizeArray(values.frequency)).trigger('change');
        $('#filterAssignment').val(normalizeArray(values.assignment)).trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.status, appliedFilters.frequency, appliedFilters.assignment].filter(hasFilterValue).length;

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
     * Filter options come from the ROWS THIS TABLE ALREADY HAS, not from a lookup endpoint. Frequency and
     * assignment target are closed sets that arrive with the row data, so a second round-trip would buy nothing
     * and could disagree with what is on screen.
     */
    const collectDistinct = (rows, field) => Array.from(new Set(
        rows.map((row) => row?.[field]).filter((value) => value !== null && value !== undefined && value !== '')))
        .sort((a, b) => String(a).localeCompare(String(b), window.CurrentLanguage || undefined));

    const populateFilterOptions = (api) => {
        const frequencySelect = document.getElementById('filterFrequency');
        const assignmentSelect = document.getElementById('filterAssignment');
        if (!frequencySelect && !assignmentSelect) return;

        const rows = api ? api.rows().data().toArray() : [];
        // Selections survive a repopulate — a redraw must never silently drop an applied filter.
        const fill = (select, field, label) => {
            if (!select) return;
            const selected = Array.from(select.selectedOptions || []).map((option) => option.value);
            appendOptions(select, collectDistinct(rows, field).map((value) => ({ value, text: label(value) })));
            $(select).val(selected).trigger('change');
        };
        fill(frequencySelect, 'frequency', (value) => L['Frequency' + value] || value);
        fill(assignmentSelect, 'assignmentTarget', (value) => assignmentLabel(value));
    };

    const setupFilters = async (api) => {
        initSelect2Filters();
        populateFilterOptions(api);
        api.on('draw.dt.recurrenceRuleFilters', () => populateFilterOptions(api));
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterStatus').val() || [],
                frequency: $('#filterFrequency').val() || [],
                assignment: $('#filterAssignment').val() || []
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

    /*
     * "Every two weeks", not "Weekly / 2".
     *
     * The interval is only meaningful beside its frequency — printing the enum alone answers a question nobody
     * asked and hides the one thing the person who wrote the rule actually chose. Interval 1 says nothing extra,
     * so it is left off rather than rendered as a redundant "1".
     */
    const frequencyLabel = (row) => {
        const name = L['Frequency' + (row?.frequency || '')] || row?.frequency || '';
        const interval = Number(row?.interval || 1);
        return interval > 1 ? `${escapeHtml(name)} · ${escapeHtml(L.Interval || '')} ${interval}` : escapeHtml(name);
    };

    /*
     * WHO the generated work goes to. "SelfAssigned" is deliberately not mapped: no rule can carry it (the form
     * refuses it and so does the server), so a row showing it would be data nobody can explain — better it read
     * as the raw value and be noticed than be given a friendly name that hides it.
     */
    const assignmentLabel = (target) => {
        if (target === 'Person') return L.AssignmentPerson || target;
        if (target === 'PositionPool') return L.AssignmentPool || target;
        return target || '';
    };

    const formatDate = (v) => {
        if (!v) return '';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? String(v).slice(0, 10) : d.toLocaleDateString(window.CurrentLanguage || undefined);
    };

    const reloadWithSuccessToast = (messageKey, interpolationValue) =>
        window.DitenDataTable.reloadWithToast(dt, dtTableEl, messageKey, interpolationValue);

    const rowActionHandlers = {
        quickView: ({ id }) => {
            if (id) window.location.href = `/Tasks/RecurrenceRules/Details/${id}`;
        },
        edit: ({ id }) => {
            if (id) window.location.href = `/Tasks/RecurrenceRules/Edit/${id}`;
        },
        delete: ({ row }) => {
            if (!row?.id) return;
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const res = await fetch(`${apiUrl}/recurrence-rules/${row.id}`, {
                        method: 'DELETE',
                        credentials: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!res.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted');
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
                // The rule's NAME, which is what the person recognises — a GUID in a confirm dialog tells them
                // nothing about what they are about to retire.
            }, { entityName: row.name, type: 'danger', confirmButtonText: L.Delete });
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
                        console.error('[RecurrenceRules SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: apiUrl + '/recurrence-rules',
                type: 'GET',
                xhrFields: { withCredentials: true }
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'name', name: 'name' },
                    { data: 'frequency', name: 'frequency' },
                    { data: 'startsAt', name: 'startsAt' },
                    { data: 'endsAt', name: 'endsAt' },
                    { data: 'assignmentTarget', name: 'assignmentTarget' },
                    { data: 'lastGeneratedAt', name: 'lastGeneratedAt' },
                    { data: 'isActive', name: 'isActive' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                    { targets: 2, render: (data, type, full) => type === 'display' ? frequencyLabel(full) : data },
                    { targets: 3, render: (data, type) => type === 'display' ? formatDate(data) : (data || '') },
                    {
                        targets: 4,
                        // An empty end date is an ANSWER — the rule is open-ended — so it is SAID, not left blank
                        // for the reader to interpret as missing data.
                        render: (data, type) => type === 'display'
                            ? (data ? formatDate(data) : `<span class="text-muted">${escapeHtml(L.OpenEnded || '')}</span>`)
                            : (data || '')
                    },
                    { targets: 5, render: (data, type) => type === 'display' ? escapeHtml(assignmentLabel(data)) : (data || '') },
                    {
                        targets: 6,
                        // "Why did nothing appear this month?" is answered here, so a rule that has never fired
                        // says so rather than showing an empty cell that reads like a bug.
                        render: (data, type) => type === 'display'
                            ? (data ? formatDate(data) : `<span class="text-muted">${escapeHtml(L.NeverGenerated || '')}</span>`)
                            : (data || '')
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
                    { href: '/Tasks/RecurrenceRules/Create' },
                    extraButtons,
                    { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/Tasks/RecurrenceRules/Create';
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

document.addEventListener('DOMContentLoaded', () => TaskRecurrenceRuleList.init());
