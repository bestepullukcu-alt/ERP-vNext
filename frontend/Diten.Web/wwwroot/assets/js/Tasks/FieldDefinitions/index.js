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

    const dtTableEl = document.querySelector('.datatables-taskfielddefinitions');
    /*
     * The SAME-ORIGIN proxy, not a service port. Diten.Web forwards to Platform with the JWT read from the
     * HTTP-only cookie server-side, so the browser never addresses a gateway and never holds a token — the same
     * seam WorkCenterNext's work-item feed uses.
     */
    const apiUrl = '/Tasks/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Tasks', pageKey: 'TaskFieldDefinitions' };
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
    const renderLabel = (row) => {
        if (!row) { return ''; }
        if (row.labelText) { return escapeHtml(row.labelText); }
        if (row.labelResourceKey) { return escapeHtml(L[row.labelResourceKey] || row.labelResourceKey); }
        return '';
    };

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
            console.error('[GoldenReferenceCompact SaveView] Failed to load saved views.', error);
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
                && matchesMultiFilter(appliedFilters.referenceType, row.referenceType)
                && matchesMultiFilter(appliedFilters.category, row.category)
                && matchesMultiFilter(appliedFilters.owner, row.owner)
                && matchesSingleFilter(appliedFilters.priority, row.priority);
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
        $('#filterStatus, #filterReferenceType, #filterCategory, #filterOwner').each(function () {
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
        $('#filterPriority').each(function () {
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
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterReferenceType').val(normalizeArray(values.referenceType)).trigger('change');
        $('#filterCategory').val(normalizeArray(values.category)).trigger('change');
        $('#filterOwner').val(normalizeArray(values.owner)).trigger('change');
        $('#filterPriority').val(values.priority || '').trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.status, appliedFilters.referenceType, appliedFilters.category, appliedFilters.owner, appliedFilters.priority].filter(hasFilterValue).length;

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
    const loadLookupOptions = async () => {
        const refSelect = document.getElementById('filterReferenceType');
        const categorySelect = document.getElementById('filterCategory');
        const ownerSelect = document.getElementById('filterOwner');
        const prioritySelect = document.getElementById('filterPriority');
        if (!refSelect || !categorySelect || !ownerSelect || !prioritySelect) return;
        try {
            const res = await fetch('/GoldenReferenceCompact/lookups', {
                method: 'GET',
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            if (!res.ok) return;
            const data = await res.json();
            appendOptions(refSelect, Array.isArray(data?.referenceTypes) ? data.referenceTypes : []);
            appendOptions(categorySelect, Array.isArray(data?.categories) ? data.categories : []);
            appendOptions(ownerSelect, Array.isArray(data?.owners) ? data.owners : []);
            prioritySelect.innerHTML = '';
            const showAll = document.createElement('option');
            showAll.value = '';
            showAll.textContent = L.ShowAll || '';
            prioritySelect.appendChild(showAll);
            (Array.isArray(data?.priorities) ? data.priorities : []).forEach((item) => {
                if (item?.value == null) return;
                const opt = document.createElement('option');
                opt.value = String(item.value);
                opt.textContent = item.text || `${L.LevelPrefix || ''} ${item.value}`.trim();
                prioritySelect.appendChild(opt);
            });
        } catch (error) {
            console.error('[GoldenReferenceCompact Lookup] Failed.', error);
        }
    };

    const setupFilters = async (api) => {
        await loadLookupOptions();
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterStatus').val() || [],
                referenceType: $('#filterReferenceType').val() || [],
                category: $('#filterCategory').val() || [],
                owner: $('#filterOwner').val() || [],
                priority: document.getElementById('filterPriority')?.value || ''
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
    const getReferenceTypeMap = () => ({
        'Standard': L.ReferenceTypeStandard || 'Standard',
        'Custom': L.ReferenceTypeCustom || 'Custom',
        'Pro': L.ReferenceTypePro || 'Pro'
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

    const closeResponsiveModal = () => {
        const modalEl = document.querySelector('.modal.dtr-bs-modal.show');
        if (!modalEl) return false;
        const modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide();
        else modalEl.querySelector('[data-bs-dismiss="modal"], .btn-close')?.click();
        return true;
    };
    const populateDetailsOffcanvas = (data) => {
        if (!data) return;
        setText('oc-title', data.name);
        setText('oc-subtitle', data.code);
        setText('oc-code', data.code);
        setText('oc-name', data.name);
        setText('oc-type', getReferenceTypeMap()[data.referenceType] || data.referenceType);
        setText('oc-category', data.category);
        setText('oc-group-key', data.groupKey);
        setText('oc-source-system', data.sourceSystem);
        setText('oc-owner', data.owner);
        setText('oc-version', data.version);
        setText('oc-effective-date', formatDate(data.effectiveDate));
        setText('oc-expiration-date', formatDate(data.expirationDate));
        setText('oc-priority', data.priority != null ? String(data.priority) : '-');
        setText('oc-desc', data.description);
        const statusEl = document.getElementById('oc-status');
        const status = getStatusMap()[String(!!data.isActive)] || { title: L.Unknown, class: 'bg-label-primary' };
        if (statusEl) {
            statusEl.className = `badge ${status.class}`;
            statusEl.innerText = status.title || '-';
        }
        const priorityDot = document.getElementById('oc-priority-dot');
        if (priorityDot) {
            const priority = Number(data.priority || 0);
            priorityDot.className = 'backbone-priority-dot';
            if (priority >= 70) priorityDot.classList.add('is-high');
            else if (priority > 0) priorityDot.classList.add('is-medium');
        }
        const editBtn = document.getElementById('oc-btn-edit');
        if (editBtn) editBtn.href = `/Tasks/FieldDefinitions/Edit/${data.id}`;
    };

    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        bulkActionSelector: '[data-bulk-action]',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all',
        onBulkAction: {
            delete: async ({ ids }) => {
                if (!ids.length) return;
                const confirmText = (L.BulkDeleteConfirm || '').replace('{0}', ids.length);
                window.showConfirm?.(confirmText, async () => {
                    try {
                        const res = await fetch(`${apiUrl}/field-definitions/bulk`, {
                            method: 'DELETE',
                            credentials: 'include',
                            headers: getAuthHeaders(true),
                            body: JSON.stringify(ids)
                        });
                        if (!res.ok) throw new Error('Bulk delete failed.');
                        reloadWithSuccessToast('BulkDeleteSuccess', String(ids.length));
                    } catch (error) {
                        console.error(error);
                        window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }, { entityName: String(ids.length), type: 'danger', confirmButtonText: L.Delete });
            }
        }
    };
    const reloadWithSuccessToast = (messageKey, interpolationValue) =>
        window.DitenDataTable.reloadWithToast(dt, dtTableEl, messageKey, interpolationValue, bulkOptions);
    const rowActionHandlers = {
        quickView: ({ id }) => {
            if (id) window.location.href = `/Tasks/FieldDefinitions/Details/${id}`;
        },
        edit: ({ id }) => {
            if (id) window.location.href = `/Tasks/FieldDefinitions/Edit/${id}`;
        },
        delete: ({ row }) => {
            if (!row?.id) return;
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const res = await fetch(`${apiUrl}/field-definitions/${row.id}`, {
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
            }, { entityName: row.name, type: 'danger', confirmButtonText: L.Delete });
        }
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
                        console.error('[GoldenReferenceCompact SaveView] Failed to save default view.', error);
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
            bulk: bulkOptions,
            ajax: {
                url: apiUrl + '/field-definitions',
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
                    // The LABEL column reads from both sources — see renderLabel for why neither is a fallback.
                    { data: null, name: 'label' },
                    { data: 'valueType', name: 'valueType' },
                    { data: 'section', name: 'section' },
                    { data: 'isRequired', name: 'isRequired' },
                    { data: 'isActive', name: 'isActive' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    { targets: 3, render: (data, type, full) => renderLabel(full) },
                    {
                        targets: 4,
                        render: (data) => data ? `<span class="badge bg-label-info">${L['ValueType' + data] || data}</span>` : ''
                    },
                    {
                        targets: 6,
                        render: (data) => data
                            ? `<span class="badge bg-label-warning">${L.Required || ''}</span>`
                            : `<span class="badge bg-label-secondary">${L.Optional || ''}</span>`
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
                    { href: '/Tasks/FieldDefinitions/Create' },
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5, 6, 7], colvisColumns: [2, 3, 4, 5, 6, 7] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/Tasks/FieldDefinitions/Create';
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
