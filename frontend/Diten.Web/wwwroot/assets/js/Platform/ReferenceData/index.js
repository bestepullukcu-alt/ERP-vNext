'use strict';

const ReferenceDataList = (function () {
    let dt = null;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;

    const root = document.getElementById('reference-data-page');
    const api = window.ReferenceDataApi;
    const permissions = window.ReferenceDataPermissions || {
        can: () => true,
        hideUnless: () => true,
        guard: () => true
    };
    const dtTableEl = document.querySelector('.datatables-reference-data');
    const endpoint = '/Platform/ReferenceData/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'ReferenceData' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6, 7];
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { status: [], scopeType: [] };
    let L = window.L10n || {};

    const loadingEl = document.getElementById('rd-state-loading');
    const errorEl = document.getElementById('rd-state-error');
    const emptyEl = document.getElementById('rd-state-empty');
    const createSetModalEl = document.getElementById('rd-create-set-modal');
    const createSetForm = document.getElementById('rd-create-set-form');
    const createSetCodeInput = document.getElementById('rd-new-set-code');
    const createSetNameInput = document.getElementById('rd-new-set-name');
    const createSetScopeSelect = document.getElementById('rd-new-set-scope');
    const submitCreateSetBtn = document.getElementById('rd-submit-create-set');
    const createSetModal = (window.bootstrap?.Modal && createSetModalEl) ? new window.bootstrap.Modal(createSetModalEl) : null;

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const getAuthHeaders = (includeJson = false) =>
        window.DitenDataTable?.getAuthHeaders?.(includeJson) || (includeJson ? { 'Content-Type': 'application/json' } : {});

    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');
    const normalizeArray = (value) => {
        if (Array.isArray(value)) return Array.from(new Set(value.map((item) => normalizeString(String(item))).filter(Boolean)));
        const normalized = normalizeString(value);
        return normalized ? [normalized] : [];
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ status: [], scopeType: [] });
    const normalizeFilters = (filters) => ({
        status: normalizeArray(filters?.status),
        scopeType: normalizeArray(filters?.scopeType)
    });
    const hasFilterValue = (value) => Array.isArray(value) ? normalizeArray(value).length > 0 : normalizeString(value).length > 0;
    const get = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];
    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    const show = (el, visible) => el && el.classList.toggle('d-none', !visible);
    const text = (value, fallback) => {
        const normalized = normalizeString(value || '');
        return normalized.length ? normalized : (fallback || '');
    };

    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const result = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((index, position) => {
                if (typeof colVis[index] === 'boolean') result[index] = colVis[index];
                else if (typeof colVis[position] === 'boolean') result[index] = colVis[position];
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((index) => {
                if (typeof colVis[index] === 'boolean') result[index] = colVis[index];
            });
        }
        return Object.keys(result).length ? result : null;
    };
    const captureColVis = (tableApi) => {
        const result = {};
        saveViewColumnIndexes.forEach((index) => {
            try { result[index] = !!tableApi.column(index).visible(); } catch (_error) { }
        });
        return result;
    };
    const defaultColVis = () => saveViewColumnIndexes.reduce((acc, index) => {
        acc[index] = defaultVisibleColumnIndexes.includes(index);
        return acc;
    }, {});
    const applyColVis = (tableApi, colVis) => {
        const normalized = normalizeColVis(colVis);
        if (!normalized) return;
        saveViewColumnIndexes.forEach((index) => {
            if (typeof normalized[index] === 'boolean') {
                try { tableApi.column(index).visible(normalized[index], false); } catch (_error) { }
            }
        });
    };
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const normalized = order.map(Number).filter((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount);
        return normalized.length === totalColumnCount && new Set(normalized).size === totalColumnCount ? normalized : null;
    };
    const captureColOrder = (tableApi) => {
        try { return normalizeColOrder(tableApi?.colReorder?.order?.()); } catch (_error) { return null; }
    };
    const applyColOrder = (tableApi, order) => {
        const normalized = normalizeColOrder(order);
        if (normalized && typeof tableApi?.colReorder?.order === 'function') tableApi.colReorder.order(normalized, true);
    };
    const getSearchVal = (tableApi) => {
        try { return tableApi.table().container().querySelector('.dt-search input')?.value || ''; } catch (_error) { return ''; }
    };
    const syncSearchInput = (tableApi, value) => {
        try {
            const input = tableApi.table().container().querySelector('.dt-search input');
            if (input) input.value = value || '';
        } catch (_error) { }
    };
    const getCurrentView = (tableApi) => ({
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(tableApi) || tableApi.search()),
        colVis: captureColVis(tableApi),
        columnOrder: captureColOrder(tableApi),
        order: tableApi.order()
    });
    const serializeView = (view) => JSON.stringify({
        filters: Object.keys(view?.filters || {}).sort().reduce((acc, key) => {
            acc[key] = normalizeFilterValue(view.filters[key]);
            return acc;
        }, {}),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getSavedViewId = (savedView) => savedView?.id || savedView?.Id || savedView?._id || null;
    const getSavedViewName = (savedView) => savedView?.viewName || savedView?.ViewName || '';
    const isSavedViewDefault = (savedView) => savedView?.isDefault === true || savedView?.IsDefault === true;
    const unwrapViewResponse = (response) => response?.data || response?.Data || response;
    const getSavedViewDef = (savedView) => {
        const raw = savedView?.viewDefinition ?? savedView?.ViewDefinition ?? {};
        if (typeof raw === 'string') {
            try { return JSON.parse(raw); } catch (_error) { return {}; }
        }
        return raw || {};
    };
    const mapSavedViewToState = (savedView) => {
        const definition = getSavedViewDef(savedView);
        return {
            filters: normalizeFilters(definition.filters || definition),
            search: normalizeString(definition.search),
            colVis: normalizeColVis(definition.colVis),
            columnOrder: normalizeColOrder(definition.columnOrder),
            order: Array.isArray(definition.order) ? definition.order : null
        };
    };
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || view || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getResetBaselineState = () => normalizeViewState({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
        order: baseOrder
    });
    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (tableApi) => {
        const baseline = defaultViewState || {
            filters: emptyFilters(),
            search: '',
            colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
            order: baseOrder
        };
        return serializeView(getCurrentView(tableApi)) !== serializeView(baseline);
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
            console.error('[ReferenceData SaveView] Failed to load saved views.', error);
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

    const surfaceTarget = (area, setCode, setId) => `/Platform/ReferenceData/${area}/${encodeURIComponent(setCode)}?setId=${encodeURIComponent(setId)}`;
    const navigate = (url) => {
        if (typeof window.__rdNavigate === 'function') {
            window.__rdNavigate(url);
            return;
        }
        window.location.href = url;
    };
    const statusBadge = (status, type) => {
        const normalized = normalizeString(status).toLowerCase();
        const label = status || L.Unknown || '-';
        if (type !== 'display') return label;
        const css = normalized === 'active' ? 'bg-label-success' : normalized === 'draft' ? 'bg-label-warning' : 'bg-label-secondary';
        return `<span class="badge ${css}">${escapeHtml(label)}</span>`;
    };
    const shortId = (value) => {
        const normalized = normalizeString(value);
        if (!normalized) return '';
        const head = normalized.split('-')[0];
        return head.length > 8 ? head.slice(0, 8) : head;
    };
    const governanceLabelKeys = {
        notconfigured: 'GovernanceNotConfigured',
        draft: 'GovernanceDraft',
        submitted: 'GovernanceSubmitted',
        inreview: 'GovernanceInReview',
        approved: 'GovernanceApproved',
        rejected: 'GovernanceRejected'
    };
    const humanize = (value) => normalizeString(value).replace(/_/g, ' ').replace(/([a-z0-9])([A-Z])/g, '$1 $2').trim();
    const governanceLabel = (raw) => {
        const key = governanceLabelKeys[normalizeString(raw).toLowerCase()];
        return (key && L[key]) || humanize(raw) || raw;
    };
    const governanceBadge = (status, type) => {
        const raw = normalizeString(status) || 'NotConfigured';
        const label = governanceLabel(raw);
        if (type !== 'display') return label;
        const normalized = raw.toLowerCase();
        let css = 'bg-label-secondary';
        if (normalized === 'approved') css = 'bg-label-success';
        else if (normalized === 'submitted' || normalized === 'pending' || normalized === 'inreview') css = 'bg-label-warning';
        else if (normalized === 'rejected') css = 'bg-label-danger';
        return `<span class="badge ${css}">${escapeHtml(label)}</span>`;
    };
    const matchesMultiFilter = (selected, actual) => {
        const normalized = normalizeArray(selected);
        return !normalized.length || normalized.includes(normalizeString(actual));
    };
    const registerTableFilters = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.referenceFilterBound === '1') return;
        dtTableEl.dataset.referenceFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _searchData, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesMultiFilter(appliedFilters.status, get(row, 'status', 'Status'))
                && matchesMultiFilter(appliedFilters.scopeType, get(row, 'scopeType', 'ScopeType'));
        });
    };

    const getAppliedFilterCount = () =>
        [appliedFilters.status, appliedFilters.scopeType].filter(hasFilterValue).length;

    const resolveOpenRoute = async (setId) => {
        const setPayload = await api.getSet(setId);
        const draftId = setPayload?.activeDraftVersionId || setPayload?.ActiveDraftVersionId || null;
        const publishedId = setPayload?.publishedVersionId || setPayload?.PublishedVersionId || null;

        if (!draftId) return `/Platform/ReferenceData/Sets/${setId}`;

        const draftValuesPayload = await api.getVersionValues(draftId).catch(() => ({ items: [] }));
        const draftValuesCount = (draftValuesPayload?.items || draftValuesPayload?.Items || []).length;
        if (draftValuesCount > 0) {
            return `/Platform/ReferenceData/Sets/${setId}/DraftWizard?mode=resume&versionId=${encodeURIComponent(draftId)}`;
        }

        if (publishedId) {
            const publishedValuesPayload = await api.getVersionValues(publishedId).catch(() => ({ items: [] }));
            const publishedValuesCount = (publishedValuesPayload?.items || publishedValuesPayload?.Items || []).length;
            if (publishedValuesCount > 0) return `/Platform/ReferenceData/Sets/${setId}`;
        }

        return `/Platform/ReferenceData/Sets/${setId}/DraftWizard?mode=resume&versionId=${encodeURIComponent(draftId)}`;
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
        const selectedTexts = ($select.select2('data') || []).map((item) => normalizeString(item.text)).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + (L.Reset || '') + '" title="' + (L.Reset || '') + '">&times;</span>');
            $clearBtn.on('mousedown', (event) => { event.preventDefault(); event.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clearBtn);
        }
    };
    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $body = $(document.body);
        $('#filterStatus, #filterScopeType').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $select.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                closeOnSelect: false
            });
            $select.on('change.select2-summary', function () { syncMultiSelectSummary($select); });
            requestAnimationFrame(() => syncMultiSelectSummary($select));
        });
    };
    const syncFilterControls = (values) => {
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterScopeType').val(normalizeArray(values.scopeType)).trigger('change');
    };
    const applySavedTableState = (tableApi, view) => {
        if (!tableApi || !view) return;
        const state = normalizeViewState(view);
        appliedFilters = state.filters;
        syncFilterControls(appliedFilters);
        applyColOrder(tableApi, state.columnOrder);
        applyColVis(tableApi, state.colVis);
        tableApi.search(state.search);
        syncSearchInput(tableApi, state.search);
        tableApi.order(state.order);
        try { tableApi.columns.adjust(); } catch (_error) { }
        try { tableApi.responsive?.recalc?.(); } catch (_error) { }
        tableApi.draw(false);
        window.DtDefaults?.updateVisualState?.(tableApi, getAppliedFilterCount());
    };
    const setupFilters = (tableApi) => {
        initSelect2Filters();
        applySavedTableState(tableApi, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterStatus').val() || [],
                scopeType: $('#filterScopeType').val() || []
            };
            tableApi.draw();
            window.DtDefaults.updateVisualState(tableApi, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(tableApi));
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            applySavedTableState(tableApi, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(tableApi));
        });
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

    const readCreatePayload = () => {
        const setCode = text(document.getElementById('rd-new-set-code')?.value).toUpperCase();
        const name = text(document.getElementById('rd-new-set-name')?.value);
        const scopeType = text(document.getElementById('rd-new-set-scope')?.value, 'Global');
        const status = text(document.getElementById('rd-new-set-status')?.value, 'Draft');
        const description = text(document.getElementById('rd-new-set-description')?.value) || null;
        return { set_code: setCode, name, scope_type: scopeType, description, status };
    };
    const setFieldValidity = (el, valid) => el && el.classList.toggle('is-invalid', !valid);
    const validateCreateForm = () => {
        const code = text(createSetCodeInput?.value).toUpperCase();
        const name = text(createSetNameInput?.value);
        const codeValid = /^[A-Z0-9_]+$/.test(code);
        const nameValid = name.length > 0;
        setFieldValidity(createSetCodeInput, codeValid);
        setFieldValidity(createSetNameInput, nameValid);
        if (!codeValid) createSetCodeInput?.focus();
        else if (!nameValid) createSetNameInput?.focus();
        return codeValid && nameValid;
    };
    const bindCreateFormBehavior = () => {
        createSetCodeInput?.addEventListener('input', () => {
            const pos = createSetCodeInput.selectionStart;
            createSetCodeInput.value = createSetCodeInput.value.toUpperCase();
            try { createSetCodeInput.setSelectionRange(pos, pos); } catch (_error) { }
            if (createSetCodeInput.classList.contains('is-invalid')) setFieldValidity(createSetCodeInput, true);
        });
        createSetNameInput?.addEventListener('input', () => {
            if (createSetNameInput.classList.contains('is-invalid')) setFieldValidity(createSetNameInput, true);
        });
    };
    const openCreateSet = () => {
        if (!permissions.guard('canCreateSet', (message) => window.showToast?.(message, 'warning'))) return;
        createSetForm?.reset();
        setFieldValidity(createSetCodeInput, true);
        setFieldValidity(createSetNameInput, true);
        createSetModal?.show();
    };
    const submitCreateSet = async () => {
        if (!permissions.guard('canCreateSet', (message) => window.showToast?.(message, 'warning'))) return;
        if (!validateCreateForm()) return;
        try {
            const payload = readCreatePayload();

            submitCreateSetBtn.disabled = true;
            await api.createSet(payload);
            createSetModal?.hide();
            window.showToast?.(L.RecordSaved, 'success');
            dt?.ajax?.reload?.(null, false);
        } catch (error) {
            if (error?.isHandled) return;
            console.error(error);
            show(errorEl, true);
            if (errorEl) errorEl.textContent = `${L.ErrorState}: ${error.message}`;
        } finally {
            submitCreateSetBtn.disabled = false;
        }
    };

    const buildAjaxData = (data) => {
        data.page = 1;
        data.page_size = 100;
        data.sort = '-createdAt';
    };
    const unwrapSets = (payload) => {
        const data = payload?.data || payload?.Data || payload || {};
        return data.items || data.Items || [];
    };
    const rowActionHandlers = {
        quickView: async ({ row }) => {
            const setId = get(row, 'setId', 'SetId');
            if (!setId) return;
            try {
                navigate(await resolveOpenRoute(setId));
            } catch (error) {
                if (error?.isHandled) return;
                show(errorEl, true);
                if (errorEl) errorEl.textContent = `${L.ErrorState}: ${error?.message || 'request_failed'}`;
            }
        },
        hierarchy: ({ row }) => {
            const setId = get(row, 'setId', 'SetId');
            const setCode = get(row, 'setCode', 'SetCode');
            if (setId && setCode) navigate(surfaceTarget('Hierarchy', setCode, setId));
        },
        attributes: ({ row }) => {
            const setId = get(row, 'setId', 'SetId');
            const setCode = get(row, 'setCode', 'SetCode');
            if (setId && setCode) navigate(surfaceTarget('Attributes', setCode, setId));
        },
        mappings: ({ row }) => {
            const setId = get(row, 'setId', 'SetId');
            const setCode = get(row, 'setCode', 'SetCode');
            if (setId && setCode) navigate(surfaceTarget('Mappings', setCode, setId));
        },
        usage: ({ row }) => {
            const setId = get(row, 'setId', 'SetId');
            const setCode = get(row, 'setCode', 'SetCode');
            if (setId && setCode) navigate(surfaceTarget('Usage', setCode, setId));
        }
    };

    const renderRowActionLink = (action, rowJson) =>
        `<a href="javascript:void(0);" class="dropdown-item ${action.className || ''}" data-row-action="${action.key}" data-json="${rowJson}">
            <i class="${action.icon} me-2"></i>${escapeHtml(action.text)}
        </a>`;

    const renderReferenceActions = (row) => {
        const rowJson = escapeHtml(JSON.stringify(row));
        const actions = [
            { key: 'hierarchy', text: L.HierarchyTitle, icon: 'bx bx-git-branch' },
            { key: 'attributes', text: L.AttributesTitle, icon: 'bx bx-slider-alt' },
            { key: 'mappings', text: L.MappingsTitle, icon: 'bx bx-transfer-alt' },
            { key: 'usage', text: L.UsageTitle, icon: 'bx bx-link' }
        ];

        return `<div class="d-inline-flex align-items-center justify-content-end gap-2">
            <button type="button"
                    class="btn btn-sm btn-label-primary js-quick-view"
                    data-row-action="quickView"
                    data-json="${rowJson}"
                    title="${escapeHtml(L.Open)}"
                    aria-label="${escapeHtml(L.Open)}">
                <i class="bx bx-show me-1"></i>${escapeHtml(L.Open)}
            </button>
            <div class="dropdown">
                <button type="button"
                        class="btn btn-sm btn-label-secondary dropdown-toggle"
                        data-bs-toggle="dropdown"
                        aria-expanded="false"
                        title="${escapeHtml(L.Actions)}"
                        aria-label="${escapeHtml(L.Actions)}">
                    <i class="bx bx-cog me-1"></i>${escapeHtml(L.Actions)}
                </button>
                <div class="dropdown-menu dropdown-menu-end">
                    ${actions.map((action) => renderRowActionLink(action, rowJson)).join('')}
                </div>
            </div>
        </div>`;
    };

    const bindEvents = () => {
        document.addEventListener('click', (event) => {
            const quickViewTrigger = event.target.closest('.js-quick-view');
            if (!quickViewTrigger) return;
        });

        submitCreateSetBtn?.addEventListener('click', submitCreateSet);
        bindCreateFormBehavior();
    };

    const populateScopeTypeSelect = (select, options) => {
        if (!select || !Array.isArray(options) || !options.length) return;
        const previous = Array.from(select.selectedOptions || []).map((option) => option.value);
        select.innerHTML = '';
        options.forEach((option) => {
            const value = normalizeString(get(option, 'value', 'Value'));
            if (!value) return;
            const opt = document.createElement('option');
            opt.value = value;
            opt.textContent = normalizeString(get(option, 'name', 'Name')) || value;
            if (previous.includes(value)) opt.selected = true;
            select.appendChild(opt);
        });
    };
    const loadScopeTypes = async () => {
        try {
            const data = await api.getScopeTypes();
            const list = Array.isArray(data) ? data : (data?.items || data?.Items || []);
            if (!Array.isArray(list) || !list.length) return;
            populateScopeTypeSelect(document.getElementById('filterScopeType'), list);
            populateScopeTypeSelect(createSetScopeSelect, list);
        } catch (error) {
            if (error?.isHandled) return;
            console.warn('[ReferenceData] Failed to load scope types; using static fallback options.', error);
        }
    };

    const initDataTable = async () => {
        if (!root || !dtTableEl || !api) return;
        syncL10n();
        await loadDefaultView();
        await loadScopeTypes();

        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'aria-label': L.Import, 'data-bs-toggle': 'tooltip' },
                action: () => navigate('/Platform/ReferenceData/ImportPreview')
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-label': L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'aria-label': L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (_event, tableApi) {
                    const currentTable = tableApi || dt;
                    if (!currentTable) return;
                    try {
                        await saveDefaultView(getCurrentView(currentTable));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || L.SaveView, 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[ReferenceData SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }
            }
        };

        const bulkOptions = {
            bulkBarSelector: '#bulkActionBar',
            bulkCountSelector: '#bulkSelectedCount',
            checkboxSelector: '.dt-checkboxes',
            clearSelectionSelector: '#btnClearSelection',
            selectAllSelector: '.dt-checkboxes-select-all',
            onBulkAction: {}
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            bulk: bulkOptions,
            ajax: {
                url: `${endpoint}/sets`,
                type: 'GET',
                data: buildAjaxData,
                dataSrc: unwrapSets,
                headers: getAuthHeaders()
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                order: baseOrder,
                columns: [
                    { data: null, name: 'control', title: '' },
                    { data: null, name: 'checkbox', title: '' },
                    { data: null, name: 'setCode', title: L.SetCode },
                    { data: null, name: 'name', title: L.SetName },
                    { data: null, name: 'scopeType', title: L.ScopeType },
                    { data: null, name: 'status', title: L.Status },
                    { data: null, name: 'version', title: L.Version },
                    { data: null, name: 'governance', title: L.Governance },
                    { data: null, name: 'action', title: L.Actions }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    {
                        targets: 1,
                        orderable: false,
                        searchable: false,
                        responsivePriority: 3,
                        className: 'dt-checkboxes-cell cell-fit',
                        render: (_data, _type, row) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${escapeHtml(get(row, 'setId', 'SetId') || '')}">`
                    },
                    {
                        targets: 2,
                        render: (_data, type, row) => {
                            const value = get(row, 'setCode', 'SetCode') || '-';
                            return type === 'display' ? `<span class="fw-medium text-heading">${escapeHtml(value)}</span>` : value;
                        }
                    },
                    {
                        targets: 3,
                        render: (_data, type, row) => {
                            const value = get(row, 'name', 'Name') || '-';
                            return type === 'display' ? escapeHtml(value) : value;
                        }
                    },
                    {
                        targets: 4,
                        render: (_data, type, row) => {
                            const value = get(row, 'scopeType', 'ScopeType') || '-';
                            return type === 'display' ? `<span class="badge bg-label-info">${escapeHtml(value)}</span>` : value;
                        }
                    },
                    {
                        targets: 5,
                        render: (_data, type, row) => statusBadge(get(row, 'status', 'Status'), type)
                    },
                    {
                        targets: 6,
                        render: (_data, type, row) => {
                            const versionId = get(row, 'publishedVersionId', 'PublishedVersionId')
                                || get(row, 'activeDraftVersionId', 'ActiveDraftVersionId');
                            const short = shortId(versionId);
                            if (!short) return type === 'display' ? '-' : '';
                            return type === 'display' ? `<span class="fw-medium" title="${escapeHtml(versionId)}">${escapeHtml(short)}</span>` : short;
                        }
                    },
                    {
                        targets: 7,
                        render: (_data, type, row) => {
                            const status = get(row, 'governanceStatus', 'GovernanceStatus')
                                || get(row, 'businessReferenceDataGovernanceState', 'BusinessReferenceDataGovernanceState')
                                || 'NotConfigured';
                            return governanceBadge(status, type);
                        }
                    },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (_data, _type, row) => renderReferenceActions(row)
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddSet,
                    { href: '#', title: L.AddSet, 'aria-label': L.AddSet },
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5, 6, 7], colvisColumns: [2, 3, 4, 5, 6, 7] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupFilters(this.api());
                    const addNew = document.querySelector('.add-new');
                    permissions.hideUnless(addNew, 'canCreateSet');
                    addNew?.addEventListener('click', (event) => {
                        event.preventDefault();
                        openCreateSet();
                    });
                    setTimeout(() => { saveFilterArmed = true; }, 0);
                },
                drawCallback: function () {
                    const tableApi = this.api();
                    const count = tableApi.rows({ filter: 'applied' }).count();
                    show(loadingEl, false);
                    show(emptyEl, count === 0);
                    window.DtDefaults.updateVisualState(tableApi, getAppliedFilterCount());
                },
                preDrawCallback: function () {
                    show(loadingEl, true);
                    show(errorEl, false);
                    return true;
                }
            }
        });

        dt.on('xhr.dt', function (_event, _settings, json, xhr) {
            show(loadingEl, false);
            if (xhr?.status && xhr.status >= 400) {
                show(errorEl, true);
                if (errorEl) errorEl.textContent = L.ErrorState || '';
            }
        });
        dt.on('error.dt', function () {
            show(loadingEl, false);
            show(errorEl, true);
            if (errorEl) errorEl.textContent = L.ErrorState || '';
        });
        dt.on('column-visibility.dt column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        dt.on('search.dt order.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    return {
        init: function () {
            if (!root) return;
            registerTableFilters();
            bindEvents();
            initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => ReferenceDataList.init());
