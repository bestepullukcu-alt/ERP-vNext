/**
 * Business Reference Data - Usage Dependencies DataTables Index Script
 * Set-scoped consumer registry. Create/Edit use full MVC route pages (compact pattern).
 */
'use strict';

const ReferenceDataUsageList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const root = document.getElementById('rd-usage-page');
    const dtTableEl = document.querySelector('.datatables-usage');
    const setCode = root?.dataset.setCode || '';
    const api = window.ReferenceDataApi;
    const permissions = window.ReferenceDataPermissions || {
        can: () => true,
        apply: (el, _c, ok) => { if (el) el.disabled = ok === false; return ok !== false; },
        guard: () => true,
        isBlocked: () => false,
        setGlobalBlock: () => { },
        isRetiredSet: (s) => String(s?.status || s?.Status || '').toLowerCase() === 'retired',
        retiredSetReason: 'This reference data set is retired. Changes are disabled.'
    };
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'BusinessReferenceDataUsage' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6, 7];
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { resolution: [], criticality: [] };
    let L = window.L10n || {};

    let draftStateKnown = false;
    let activeDraftAvailable = true;

    const statusEl = document.getElementById('rd-usage-status');

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
    const normalizeLower = (v) => normalizeString(v).toLowerCase();
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ resolution: [], criticality: [] });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return { resolution: normalizeArray(source.resolution), criticality: normalizeArray(source.criticality) };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected).map((x) => x.toLowerCase());
        return !norm.length || norm.includes(normalizeLower(actual));
    };

    const setStatus = (message, level) => {
        if (!statusEl) return;
        if (!message) {
            statusEl.className = 'alert alert-info d-none mb-3';
            statusEl.textContent = '';
            return;
        }
        const css = level === 'error' ? 'danger' : level === 'success' ? 'success' : 'info';
        statusEl.className = `alert alert-${css} mb-3`;
        statusEl.textContent = message;
    };

    // ---- Save View helpers ------------------------------------------------
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
    const captureColVis = (apiRef) => {
        const r = {};
        saveViewColumnIndexes.forEach((ci) => { try { r[ci] = !!apiRef.column(ci).visible(); } catch (e) { } });
        return r;
    };
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
        return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
    };
    const captureColOrder = (apiRef) => { try { return normalizeColOrder(apiRef?.colReorder?.order?.()); } catch (e) { return null; } };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = defaultVisibleColumnIndexes.includes(ci); return a; }, {});
    const applyColOrder = (apiRef, order) => {
        const n = normalizeColOrder(order);
        if (n && typeof apiRef?.colReorder?.order === 'function') apiRef.colReorder.order(n, true);
    };
    const applyColVis = (apiRef, colVis) => {
        const n = normalizeColVis(colVis);
        if (!n) return;
        saveViewColumnIndexes.forEach((ci) => { if (typeof n[ci] === 'boolean') { try { apiRef.column(ci).visible(n[ci], false); } catch (e) { } } });
    };
    const getSearchVal = (apiRef) => { try { return apiRef.table().container().querySelector('.dt-search input')?.value || ''; } catch (e) { return ''; } };
    const syncSearchInput = (apiRef, v) => { try { const el = apiRef.table().container().querySelector('.dt-search input'); if (el) el.value = v || ''; } catch (e) { } };
    const getCurrentView = (apiRef) => ({
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(apiRef) || apiRef.search()),
        colVis: captureColVis(apiRef),
        columnOrder: captureColOrder(apiRef),
        order: apiRef.order()
    });
    const serializeView = (v) => JSON.stringify({
        filters: Object.keys(v?.filters || {}).sort().reduce((acc, key) => { acc[key] = normalizeFilterValue(v.filters[key]); return acc; }, {}),
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
        filters: emptyFilters(), search: '', colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder
    });
    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (apiRef) => {
        const baseline = defaultViewState || {
            filters: emptyFilters(), search: '', colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder
        };
        return serializeView(getCurrentView(apiRef)) !== serializeView(baseline);
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
            console.error('[Usage SaveView] Failed to load saved views.', error);
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
        defaultViewRecord = savedRecord && typeof savedRecord === 'object' ? savedRecord : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };

    // ---- Inline filter ----------------------------------------------------
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
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.usageFilterBound === '1') return;
        dtTableEl.dataset.usageFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesMultiFilter(appliedFilters.resolution, row.resolutionMode || row.ResolutionMode)
                && matchesMultiFilter(appliedFilters.criticality, row.criticality || row.Criticality);
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
        $('#filterResolution, #filterCriticality').each(function () {
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
        $('#filterResolution').val(normalizeArray(values.resolution)).trigger('change');
        $('#filterCriticality').val(normalizeArray(values.criticality)).trigger('change');
    };
    const getAppliedFilterCount = () => [appliedFilters.resolution, appliedFilters.criticality].filter(hasFilterValue).length;

    const applySavedTableState = (apiRef, view) => {
        if (!apiRef || !view) return;
        const s = normalizeViewState(view);
        appliedFilters = s.filters;
        syncFilterControls(appliedFilters);
        applyColOrder(apiRef, s.columnOrder);
        applyColVis(apiRef, s.colVis);
        apiRef.search(s.search);
        syncSearchInput(apiRef, s.search);
        apiRef.order(s.order);
        try { apiRef.columns.adjust(); } catch (e) { }
        try { apiRef.responsive?.recalc?.(); } catch (e) { }
        apiRef.draw(false);
        window.DtDefaults?.updateVisualState?.(apiRef, getAppliedFilterCount());
    };
    const setupFilters = (apiRef) => {
        initSelect2Filters();
        applySavedTableState(apiRef, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                resolution: $('#filterResolution').val() || [],
                criticality: $('#filterCriticality').val() || []
            };
            apiRef.draw();
            window.DtDefaults.updateVisualState(apiRef, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(apiRef));
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            applySavedTableState(apiRef, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(apiRef));
        });
    };

    // ---- Renderers --------------------------------------------------------
    const getCriticalityMap = () => ({
        critical: { title: L.UsageImpactCritical || 'Critical', class: 'bg-label-danger' },
        high: { title: L.UsageCriticalityHigh || 'High', class: 'bg-label-warning' },
        medium: { title: L.UsageCriticalityMedium || 'Medium', class: 'bg-label-info' },
        low: { title: L.UsageCriticalityLow || 'Low', class: 'bg-label-secondary' }
    });
    const getResolutionMap = () => ({
        latest: L.UsageResolutionLatest || 'Latest',
        pinned: L.UsageResolutionPinned || 'Pinned',
        'as-of': L.UsageResolutionAsOf || 'As Of'
    });
    const formatDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? String(v).slice(0, 10) : d.toLocaleString(window.CurrentLanguage || undefined);
    };
    const scopeText = (full) => {
        const t = full.scopeType || full.ScopeType;
        const k = full.scopeKey || full.ScopeKey;
        return [t, k].filter(Boolean).join(' / ') || '-';
    };
    const applySummary = (summary) => {
        const s = summary || {};
        const set = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = String(val ?? 0); };
        set('rd-usage-total', s.totalRegistrations ?? s.TotalRegistrations ?? 0);
        set('rd-usage-critical', s.criticalRegistrations ?? s.CriticalRegistrations ?? 0);
        set('rd-usage-high', s.highRegistrations ?? s.HighRegistrations ?? 0);
        set('rd-usage-medium', s.mediumRegistrations ?? s.MediumRegistrations ?? 0);
        set('rd-usage-low', s.lowRegistrations ?? s.LowRegistrations ?? 0);
    };

    const usageRoute = (suffix) => `/Platform/ReferenceData/Usage/${encodeURIComponent(setCode)}${suffix}`;
    const canMutate = () => permissions.can('canRegisterUsage') && (!draftStateKnown || activeDraftAvailable) && !(typeof permissions.isBlocked === 'function' && permissions.isBlocked());

    // ---- Bulk / row actions ----------------------------------------------
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
                if (!permissions.guard('canRegisterUsage', (m) => window.showToast?.(m, 'error'))) return;
                const confirmText = (L.BulkDeleteConfirm || '').replace('{0}', ids.length);
                window.showConfirm?.(confirmText, async () => {
                    try {
                        await api.bulkDeleteUsageRegistrations(ids);
                        reloadWithSuccessToast('BulkDeleteSuccess', String(ids.length));
                    } catch (error) {
                        if (error?.isHandled) return;
                        console.error(error);
                        window.showToast?.(error?.message || L.ErrorOccurred, 'error');
                    }
                }, { entityName: String(ids.length), type: 'danger', confirmButtonText: L.Delete });
            }
        }
    };
    const reloadWithSuccessToast = (messageKey, interpolationValue) =>
        window.DitenDataTable.reloadWithToast(dt, dtTableEl, messageKey, interpolationValue, bulkOptions);

    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = usageRoute(`/Details/${id}`); },
        edit: ({ id }) => { if (id) window.location.href = usageRoute(`/Edit/${id}`); },
        delete: ({ row }) => {
            const targetId = row?.id || row?.usageRegistrationId || row?.UsageRegistrationId;
            if (!targetId) return;
            if (!permissions.guard('canRegisterUsage', (m) => window.showToast?.(m, 'error'))) return;
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    await api.deleteUsageRegistration(targetId);
                    reloadWithSuccessToast('RecordDeleted');
                } catch (error) {
                    if (error?.isHandled) return;
                    console.error(error);
                    window.showToast?.(error?.message || L.ErrorOccurred, 'error');
                }
            }, { entityName: row.consumerName || row.ConsumerName, type: 'danger', confirmButtonText: L.Delete });
        }
    };

    // ---- Set context guard (retired / active draft) -----------------------
    const resolveSetContext = async () => {
        if (!setCode || typeof api.getSets !== 'function' || typeof api.getSet !== 'function') {
            draftStateKnown = false; activeDraftAvailable = true; return;
        }
        try {
            const data = await api.getSets(`?search=${encodeURIComponent(setCode)}&status=&scope_type=&page=1&page_size=100&sort=-createdAt`);
            const items = data?.items || data?.Items || [];
            const candidate = items.find((x) => normalizeLower(x.setCode || x.SetCode) === normalizeLower(setCode));
            if (!candidate) {
                draftStateKnown = true; activeDraftAvailable = false;
                setStatus((L.SetNotFoundDescription || 'Set not found:') + ' ' + setCode, 'error');
                return;
            }
            const setId = candidate.setId || candidate.SetId;
            const detail = await api.getSet(setId);
            const retired = typeof permissions.isRetiredSet === 'function'
                ? permissions.isRetiredSet(detail)
                : normalizeLower(detail?.status || detail?.Status) === 'retired';
            if (typeof permissions.setGlobalBlock === 'function') {
                permissions.setGlobalBlock(retired, permissions.retiredSetReason);
            }
            draftStateKnown = true;
            activeDraftAvailable = !retired && !!(detail.activeDraftVersionId || detail.ActiveDraftVersionId);
            if (retired) setStatus(permissions.retiredSetReason, 'info');
            else if (!activeDraftAvailable) setStatus(L.NoDraftReason || 'An active draft version is required.', 'info');
        } catch (error) {
            if (error?.isHandled) return;
            console.warn('[Usage] Failed to resolve set context.', error);
        }
    };

    const applyAddNewState = () => {
        const addBtn = document.querySelector('.add-new');
        if (!addBtn) return;
        const allowed = canMutate();
        addBtn.classList.toggle('disabled', !allowed);
        if (!allowed) addBtn.setAttribute('aria-disabled', 'true'); else addBtn.removeAttribute('aria-disabled');
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!window.DitenDataTable || !window.DtDefaults) { console.error('[Usage] DitenDataTable/DtDefaults required.'); return; }
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
                action: async function (e, apiRef) {
                    const tableApi = apiRef || dt;
                    if (!tableApi) return;
                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || L.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[Usage SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            bulk: bulkOptions,
            ajax: {
                url: `/Platform/ReferenceData/api/usage-registrations?set_code=${encodeURIComponent(setCode)}`,
                type: 'GET',
                xhrFields: { withCredentials: true },
                dataSrc: function (json) {
                    const payload = json?.data ?? json ?? {};
                    applySummary(payload.impactSummary || payload.ImpactSummary || {});
                    return payload.items || payload.Items || [];
                }
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                columns: [
                    { data: 'usageRegistrationId', name: 'control' },
                    { data: 'usageRegistrationId', name: 'checkbox' },
                    { data: 'consumerModule', name: 'consumerModule' },
                    { data: 'consumerName', name: 'consumerName' },
                    { data: 'resolutionMode', name: 'resolutionMode' },
                    { data: 'scopeKey', name: 'scope' },
                    { data: 'criticality', name: 'criticality' },
                    { data: 'lastResolvedAt', name: 'lastResolvedAt' },
                    { data: 'usageRegistrationId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    { targets: 4, render: (data) => data ? `<span class="badge bg-label-primary">${getResolutionMap()[String(data).toLowerCase()] || data}</span>` : '' },
                    { targets: 5, render: (data, type, full) => scopeText(full) },
                    {
                        targets: 6,
                        render: (data, type) => {
                            const map = getCriticalityMap();
                            const entry = map[String(data || '').toLowerCase()] || { title: data || L.Unknown, class: 'bg-label-primary' };
                            return type === 'display' ? `<span class="badge ${entry.class}">${entry.title}</span>` : entry.title;
                        }
                    },
                    { targets: 7, render: (data) => formatDate(data) },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
                            const actions = [
                                { key: 'quickView', className: 'js-quick-view', text: L.QuickView, attrs: { 'data-id': full.usageRegistrationId } }
                            ];
                            if (canMutate()) {
                                actions.unshift({ key: 'delete', className: 'text-danger me-1', icon: 'bx bx-trash', attrs: { 'data-json': rowJson } });
                                actions.push({ key: 'edit', className: 'js-edit-item', text: L.Edit, attrs: { 'data-id': full.usageRegistrationId, 'data-json': rowJson } });
                            }
                            return window.DitenDataTable.renderActions(actions);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew,
                    { href: usageRoute('/Create') },
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5, 6, 7], colvisColumns: [2, 3, 4, 5, 6, 7] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        if (!canMutate()) { window.showToast?.(L.NoDraftReason || permissions.retiredSetReason, 'warning'); return; }
                        window.location.href = usageRoute('/Create');
                    });
                    applyAddNewState();
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
        init: async function () {
            if (!root) return;
            registerTableFilters();
            await resolveSetContext();
            await initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => ReferenceDataUsageList.init());
