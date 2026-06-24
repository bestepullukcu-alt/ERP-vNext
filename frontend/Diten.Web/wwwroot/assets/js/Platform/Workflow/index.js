'use strict';

// MOD-0023 Batch 08 — Workflow admin console controller.
// Drives the tabbed Index page: Definitions, Instances, Tasks, SLA Rules, Escalations and the
// read-only Transition Gate test panel. All data access is via window.WorkflowApi (gateway proxy).
(function () {
    const api = window.WorkflowApi;
    const L = window.WorkflowL10n || {};
    const t = (key, fallback) => (L[key] != null ? L[key] : (fallback != null ? fallback : key));

    // ---- small DOM/util helpers -------------------------------------------------
    const el = (id) => document.getElementById(id);
    const show = (node) => node && node.classList.remove('d-none');
    const hide = (node) => node && node.classList.add('d-none');
    const val = (id) => (el(id)?.value ?? '').trim();
    const checked = (id) => !!el(id)?.checked;

    const escapeHtml = (value) => {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    };

    const shortId = (value) => {
        if (!value) return '<span class="text-muted">—</span>';
        const s = String(value);
        return `<code class="text-body" title="${escapeHtml(s)}">${escapeHtml(s.length > 8 ? s.slice(0, 8) + '…' : s)}</code>`;
    };

    const fmtDate = (value) => {
        if (!value) return '<span class="text-muted">—</span>';
        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return escapeHtml(value);
        const datePart = d.toLocaleDateString('en-US', {
            month: 'short',
            day: '2-digit',
            year: '2-digit'
        });
        const timePart = d.toLocaleTimeString('en-US', {
            hour: '2-digit',
            minute: '2-digit',
            hour12: true
        });
        return `<span class="d-inline-flex flex-column lh-sm"><span>${escapeHtml(datePart)}</span><span>${escapeHtml(timePart)}</span></span>`;
    };

    const yesNo = (b) => (b ? t('Yes', 'Yes') : t('No', 'No'));

    const STATUS_TONE = {
        approved: 'success', published: 'success', active: 'success', completed: 'success',
        pending: 'warning', pendingapproval: 'warning', waitingevidence: 'warning', inprogress: 'info',
        draft: 'secondary', rejected: 'danger', cancelled: 'danger', timedout: 'danger', failed: 'danger',
        escalated: 'warning', immutable: 'success', allowed: 'success', blocked: 'danger', notapplicable: 'secondary'
    };
    const statusBadge = (status) => {
        if (status === null || status === undefined || status === '') return '<span class="text-muted">—</span>';
        const tone = STATUS_TONE[String(status).toLowerCase().replace(/[^a-z]/g, '')] || 'secondary';
        return `<span class="badge bg-label-${tone}">${escapeHtml(status)}</span>`;
    };

    // ---- definition lifecycle gates --------------------------------------------
    // Status flow: Draft → Reviewed → Published → Superseded → Archived. The backend enforces these
    // same gates (Start needs an active published version; Publish is blocked once superseded/archived),
    // so the row actions disable accordingly to surface the order instead of letting the call 409.
    const lc = (status) => String(status || '').toLowerCase();
    const isPublished = (status) => lc(status) === 'published';
    const everPublished = (status) => ['published', 'superseded', 'archived'].includes(lc(status));
    const canPublish = (status) => ['draft', 'reviewed', 'published'].includes(lc(status));
    // Renders an action as a non-clickable, greyed dropdown item with a reason tooltip.
    const disableAction = (action, reason) => Object.assign({}, action, {
        className: [action.className, 'disabled'].filter(Boolean).join(' '),
        attrs: Object.assign({}, action.attrs, { 'aria-disabled': 'true', tabindex: '-1', title: reason })
    });
    const gateAction = (action, enabled, reasonKey, reasonFallback) =>
        enabled ? action : disableAction(action, t(reasonKey, reasonFallback));

    // MOD-0013 / Golden Compact toast surface. reasonCode + correlationId are surfaced in the message.
    const notify = (kind, message, debug) => {
        const text = [message, debug ? `(${debug})` : ''].filter(Boolean).join(' ');
        const type = kind === 'error' || kind === 'danger'
            ? 'error'
            : (kind === 'warning' || kind === 'info' ? kind : 'success');

        if (typeof window.showToast === 'function') {
            window.showToast(text, type);
            return;
        }

        console[type === 'error' ? 'error' : 'log'](text);
    };

    const debugSuffix = (res) => {
        const parts = [];
        if (res.reasonCode) parts.push(`${t('ReasonCode', 'Reason')}: ${res.reasonCode}`);
        if (res.correlationId) parts.push(`${t('CorrelationId', 'Correlation')}: ${res.correlationId}`);
        return parts.join(' · ');
    };

    // Maps non-success responses to a friendly, debug-friendly message.
    const failureMessage = (res) => {
        if (res.status === 403) return t('PermissionMissing', 'Permission missing or not assigned.');
        if (res.status === 0) return t('NetworkError', 'Cannot reach the workflow gateway.');
        if (res.status === 503) return t('GatewayUnavailable', 'Workflow gateway proxy unavailable.');
        if (res.status === 404) return res.message || t('NotFound', 'Not found.');
        return res.message || t('RequestFailed', 'Request failed.');
    };

    const setFormError = (boxId, res) => {
        const box = el(boxId);
        if (!box) return;
        const msg = failureMessage(res);
        const dbg = debugSuffix(res);
        box.innerHTML = `${escapeHtml(msg)}${dbg ? ` <span class="text-muted small d-block mt-1">${escapeHtml(dbg)}</span>` : ''}`;
        show(box);
    };
    const clearFormError = (boxId) => hide(el(boxId));

    // Renders the loading/error/empty/data state machine for a list area.
    const renderState = (prefix, { loading, error, empty }) => {
        const map = {
            loading: el(`wf-${prefix}-loading`),
            error: el(`wf-${prefix}-error`),
            empty: el(`wf-${prefix}-empty`),
            table: el(`wf-${prefix}-table`)
        };
        Object.values(map).forEach(hide);
        if (loading) show(map.loading);
        else if (error) show(map.error);
        else if (empty) show(map.empty);
        else show(map.table);
    };

    const asArray = (data) => (Array.isArray(data) ? data : (Array.isArray(data?.items) ? data.items : []));
    const setText = (id, value) => {
        const node = el(id);
        if (node) node.textContent = value || '—';
    };
    const setHtml = (id, value) => {
        const node = el(id);
        if (node) node.innerHTML = value || '<span class="text-muted">—</span>';
    };

    // ---- principal lookup (Users/Positions) — shared by the Start Instance modal -----------------
    const unwrapItems = (payload) => {
        if (Array.isArray(payload)) return payload;
        if (Array.isArray(payload?.data)) return payload.data;
        if (Array.isArray(payload?.data?.items)) return payload.data.items;
        if (Array.isArray(payload?.items)) return payload.items;
        return [];
    };
    const getJson = async (url) => {
        const response = await fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' });
        if (!response.ok) return [];
        return unwrapItems(await response.json());
    };
    const userLabel = (user) => {
        const name = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
        return name || user.fullName || user.displayName || user.email || user.userName || user.id;
    };
    let startPrincipalType = 'user';
    let startCandidatesInited = false;

    // =====================================================================
    // Definitions
    // =====================================================================
    let definitionsCache = [];
    let definitionsDt = null;
    let activeDefinitionId = null;
    let activeDefinitionCode = '';
    let activeInstanceIds = null;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-workflow-definitions');
    const dtL = () => window.L10n || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5];
    const totalColumnCount = 7;
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { status: [] };

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilters = (filters) => ({ status: normalizeArray(filters?.status) });
    const emptyFilters = () => ({ status: [] });
    const hasFilterValue = (v) => normalizeArray(v).length > 0;
    const matchesStatusFilter = (selected, status) => {
        const values = normalizeArray(selected);
        return !values.length || values.includes(normalizeString(status));
    };
    const getAppliedFilterCount = () => hasFilterValue(appliedFilters.status) ? 1 : 0;

    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const normalized = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((index, position) => {
                if (typeof colVis[index] === 'boolean') normalized[index] = colVis[index];
                else if (typeof colVis[position] === 'boolean') normalized[index] = colVis[position];
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((index) => {
                if (typeof colVis[index] === 'boolean') normalized[index] = colVis[index];
            });
        }
        return Object.keys(normalized).length ? normalized : null;
    };
    const defaultColVis = () => saveViewColumnIndexes.reduce((acc, index) => {
        acc[index] = true;
        return acc;
    }, {});
    const captureColVis = (tableApi) => {
        const result = {};
        saveViewColumnIndexes.forEach((index) => {
            try { result[index] = !!tableApi.column(index).visible(); } catch (_e) { }
        });
        return result;
    };
    const applyColVis = (tableApi, colVis) => {
        const normalized = normalizeColVis(colVis);
        if (!normalized) return;
        saveViewColumnIndexes.forEach((index) => {
            if (typeof normalized[index] === 'boolean') {
                try { tableApi.column(index).visible(normalized[index], false); } catch (_e) { }
            }
        });
    };
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const normalized = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
        return normalized.length === totalColumnCount && new Set(normalized).size === totalColumnCount ? normalized : null;
    };
    const captureColOrder = (tableApi) => {
        try { return normalizeColOrder(tableApi?.colReorder?.order?.()); } catch (_e) { return null; }
    };
    const applyColOrder = (tableApi, order) => {
        const normalized = normalizeColOrder(order);
        if (normalized && typeof tableApi?.colReorder?.order === 'function') {
            tableApi.colReorder.order(normalized, true);
        }
    };
    const getSearchVal = (tableApi) => {
        try { return tableApi.table().container().querySelector('.dt-search input')?.value || ''; } catch (_e) { return ''; }
    };
    const syncSearchInput = (tableApi, value) => {
        try {
            const input = tableApi.table().container().querySelector('.dt-search input');
            if (input) input.value = value || '';
        } catch (_e) { }
    };
    const getCurrentView = (tableApi) => ({
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(tableApi) || tableApi.search()),
        colVis: captureColVis(tableApi),
        columnOrder: captureColOrder(tableApi),
        order: tableApi.order()
    });
    const serializeView = (view) => JSON.stringify({
        filters: { status: normalizeArray(view?.filters?.status) },
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getSavedViewId = (savedView) => savedView?.id || savedView?.Id || savedView?._id || null;
    const getSavedViewName = (savedView) => normalizeString(savedView?.viewName || savedView?.ViewName || '');
    const isSavedViewDefault = (savedView) => savedView?.isDefault === true || savedView?.IsDefault === true;
    const unwrapViewResponse = (response) => response?.data || response?.Data || response;
    const getSavedViewDef = (savedView) => {
        const raw = savedView?.viewDefinition ?? savedView?.ViewDefinition ?? {};
        if (typeof raw === 'string') {
            try { return JSON.parse(raw); } catch (_e) { return {}; }
        }
        return raw || {};
    };
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || view || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const mapSavedViewToState = (savedView) => normalizeViewState(getSavedViewDef(savedView));
    const getResetBaselineState = () => normalizeViewState({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
        order: baseOrder
    });
    const isDirtyComparedToDefault = (tableApi) => {
        const baseline = defaultViewState || getResetBaselineState();
        return serializeView(getCurrentView(tableApi)) !== serializeView(baseline);
    };
    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;
        const personalizationClient = window.personalizationClient;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews('Platform', 'WorkflowDefinitions');
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[WorkflowDefinitions SaveView] Failed to load saved views.', error);
            return null;
        }
    };
    const saveDefaultView = async (view) => {
        const personalizationClient = window.personalizationClient;
        if (!personalizationClient?.saveView) return null;
        const normalizedView = normalizeViewState(view);
        const l10n = dtL();
        const payload = {
            moduleKey: 'Platform',
            pageKey: 'WorkflowDefinitions',
            viewName: getSavedViewName(defaultViewRecord) || l10n.SaveView || 'Default',
            viewDefinition: normalizedView,
            isDefault: true,
            visibility: 'private'
        };
        const existingId = getSavedViewId(defaultViewRecord);
        const savedResponse = existingId
            ? await personalizationClient.updateView(existingId, payload)
            : await personalizationClient.saveView(payload);
        defaultViewRecord = unwrapViewResponse(savedResponse) || Object.assign({}, defaultViewRecord || {}, payload);
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
    const registerDefinitionFilters = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.workflowFilterBound === '1') return;
        dtTableEl.dataset.workflowFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _searchData, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || definitionsDt?.row(dataIndex)?.data?.() || null;
            return row ? matchesStatusFilter(appliedFilters.status, row.status) : true;
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
        const l10n = dtL();
        const placeholder = normalizeString($select.data('placeholder')) || '';
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || []).map((item) => normalizeString(item.text)).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + (l10n.Reset || '') + '" title="' + (l10n.Reset || '') + '">&times;</span>');
            $clearBtn.on('mousedown', (event) => {
                event.preventDefault();
                event.stopPropagation();
                $select.val(null).trigger('change');
            });
            $actions.append($clearBtn);
        }
    };
    const populateStatusOptions = (items) => {
        const select = document.getElementById('filterStatus');
        if (!select) return;
        const current = normalizeArray($(select).val());
        const statuses = Array.from(new Set((items || []).map((item) => normalizeString(item.status)).filter(Boolean)))
            .sort((a, b) => a.localeCompare(b));
        select.innerHTML = '';
        statuses.forEach((status) => {
            const option = document.createElement('option');
            option.value = status;
            option.textContent = status;
            select.appendChild(option);
        });
        $(select).val(current.filter((value) => statuses.includes(value))).trigger('change');
    };
    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        $('#filterStatus').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: $(document.body),
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
    const syncFilterControls = (filters) => {
        $('#filterStatus').val(normalizeArray(filters?.status)).trigger('change');
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
        try { tableApi.columns.adjust(); } catch (_e) { }
        try { tableApi.responsive?.recalc?.(); } catch (_e) { }
        tableApi.draw(false);
        window.DtDefaults?.updateVisualState?.(tableApi, getAppliedFilterCount());
    };
    const setupDefinitionFilters = (tableApi) => {
        initSelect2Filters();
        applySavedTableState(tableApi, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = { status: $('#filterStatus').val() || [] };
            tableApi.draw();
            window.DtDefaults?.updateVisualState?.(tableApi, getAppliedFilterCount());
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
    const definitionAjaxDataSrc = (json) => {
        const payload = json?.data?.data || json?.data || json;
        definitionsCache = Array.isArray(payload?.items) ? payload.items : (Array.isArray(payload) ? payload : []);
        populateStatusOptions(definitionsCache);
        return definitionsCache;
    };
    const setDetailsLoading = () => {
        show(el('wf-details-loading'));
        hide(el('wf-details-error'));
        hide(el('wf-details-content'));
    };
    const renderDetailsError = (res) => {
        hide(el('wf-details-loading'));
        hide(el('wf-details-content'));
        const error = el('wf-details-error');
        if (error) {
            error.textContent = failureMessage(res);
            show(error);
        }
    };
    const renderDefinitionDetails = (data) => {
        hide(el('wf-details-loading'));
        hide(el('wf-details-error'));
        setText('wf-details-title', data.templateCode || data.name);
        setText('wf-details-subtitle', data.name || data.id);
        setText('wf-details-code', data.templateCode);
        setText('wf-details-name', data.name);
        setHtml('wf-details-status', statusBadge(data.status));
        setHtml('wf-details-active-version', shortId(data.activePublishedVersionId));
        setHtml('wf-details-current-version', shortId(data.currentVersionId));
        setHtml('wf-details-created-at', fmtDate(data.createdAt));
        setText('wf-details-description', data.description || '—');
        show(el('wf-details-content'));
    };
    const openDefinitionDetails = async (id, row) => {
        if (!id) return;
        const offcanvasEl = el('wf-details-offcanvas');
        if (!offcanvasEl) return;
        setDetailsLoading();
        setText('wf-details-title', row?.templateCode || '—');
        setText('wf-details-subtitle', row?.name || '');
        bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl).show();

        const res = await api.getDefinition(id);
        if (!res.ok) {
            renderDetailsError(res);
            return;
        }
        renderDefinitionDetails(res.data || row || {});
    };
    const setActiveDefinitionContext = (row) => {
        activeDefinitionId = row?.id || null;
        activeDefinitionCode = row?.templateCode || '';
        activeInstanceIds = null;
    };
    const setModalContextTitle = (id, code) => {
        const title = el(id);
        if (title) title.textContent = code ? `· ${code}` : '';
    };
    const openInstancesModal = (row) => {
        setActiveDefinitionContext(row);
        setModalContextTitle('wf-inst-title-code', activeDefinitionCode);
        new bootstrap.Modal(el('wf-instances-modal')).show();
        loadInstances();
    };
    const openTasksModal = (row) => {
        setActiveDefinitionContext(row);
        setModalContextTitle('wf-task-title-code', activeDefinitionCode);
        new bootstrap.Modal(el('wf-tasks-modal')).show();
        loadTasks();
    };
    const openSlaModal = (row) => {
        setActiveDefinitionContext(row);
        setModalContextTitle('wf-sla-title-code', activeDefinitionCode);
        if (el('wf-sla-filter-templateid')) el('wf-sla-filter-templateid').value = activeDefinitionId || '';
        if (el('wf-sla-templateid')) el('wf-sla-templateid').value = activeDefinitionId || '';
        new bootstrap.Modal(el('wf-sla-modal')).show();
        loadSlaRules();
    };
    const loadDefinitions = async () => {
        if (!definitionsDt) {
            await initDefinitionsDataTable();
            return;
        }
        definitionsDt.ajax.reload(null, false);
    };
    const initDefinitionsDataTable = async () => {
        if (!dtTableEl || definitionsDt) return;
        if (!window.DitenDataTable || !window.DtDefaults) {
            console.error('[WorkflowDefinitions] DataTable shared helpers are required.');
            return;
        }
        await loadDefaultView();
        registerDefinitionFilters();
        const l10n = dtL();
        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: l10n.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (l10n.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: l10n.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (_event, tableApi) {
                    const targetApi = tableApi || definitionsDt;
                    if (!targetApi) return;
                    try {
                        await saveDefaultView(getCurrentView(targetApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(l10n.RecordSaved || l10n.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[WorkflowDefinitions SaveView] Failed to save default view.', error);
                        window.showToast?.(l10n.ErrorOccurred || '', 'error');
                    }
                }
            }
        };
        definitionsDt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: '/Platform/Workflow/api/definitions',
                type: 'GET',
                dataSrc: definitionAjaxDataSrc
            },
            actions: {
                onRowAction: {
                    detail: ({ id, row }) => {
                        openDefinitionDetails(id, row);
                    },
                    versions: ({ id }) => {
                        if (id) window.location.href = `/Platform/Workflow/Definitions/${encodeURIComponent(id)}/Versions`;
                    },
                    designer: ({ id }) => {
                        if (id) window.location.href = `/Platform/Workflow/Definitions/${encodeURIComponent(id)}/Designer`;
                    },
                    visualDesigner: ({ id }) => {
                        if (id) window.location.href = `/Platform/Workflow/Definitions/${encodeURIComponent(id)}/VisualDesigner`;
                    },
                    publish: ({ row }) => {
                        if (row?.id) openPublishModal(row.id, row.templateCode);
                    },
                    start: ({ row }) => {
                        if (row?.id) openStartModal(row.id, row.templateCode);
                    },
                    instances: ({ id }) => {
                        if (id) window.location.href = `/Platform/Workflow/Definitions/${encodeURIComponent(id)}/Instances`;
                    },
                    tasks: ({ id }) => {
                        if (id) window.location.href = `/Platform/Workflow/Definitions/${encodeURIComponent(id)}/Tasks`;
                    },
                    slaRules: ({ id }) => {
                        if (id) window.location.href = `/Platform/Workflow/Definitions/${encodeURIComponent(id)}/SlaRules`;
                    }
                }
            },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                order: baseOrder,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'templateCode', name: 'templateCode' },
                    { data: 'name', name: 'name' },
                    { data: 'status', name: 'status' },
                    { data: 'createdAt', name: 'createdAt' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${escapeHtml(data)}">` },
                    {
                        targets: 2,
                        render: (data, type, row) => type === 'display'
                            ? `<a href="#" class="fw-medium text-heading wf-definition-detail-link" data-id="${escapeHtml(row.id)}">${escapeHtml(data || '')}</a>`
                            : (data || '')
                    },
                    { targets: 4, render: (data, type) => type === 'display' ? statusBadge(data) : (data || '') },
                    { targets: 5, render: (data, type) => type === 'display' ? fmtDate(data) : (data || '') },
                    {
                        targets: -1,
                        title: l10n.Actions || t('Actions', 'Actions'),
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (_data, _type, row) => {
                            const status = row.status;
                            return window.DitenDataTable.renderActions([
                                {
                                    key: 'detail',
                                    className: 'btn-text-secondary',
                                    icon: 'bx bx-show',
                                    attrs: { 'data-id': row.id }
                                },
                                gateAction({
                                    key: 'publish',
                                    text: t('Publish', 'Publish'),
                                    icon: 'bx bx-cloud-upload',
                                    attrs: { 'data-id': row.id }
                                }, canPublish(status), 'PublishNotAllowedFromStatus', 'This definition can no longer be published.'),
                                gateAction({
                                    key: 'start',
                                    text: t('StartInstance', 'Start Instance'),
                                    icon: 'bx bx-play-circle',
                                    attrs: { 'data-id': row.id }
                                }, isPublished(status), 'PublishRequiredFirst', 'Publish the definition first.'),
                                {
                                    key: 'versions',
                                    text: t('TabVersions', 'Versions'),
                                    icon: 'bx bx-history',
                                    attrs: { 'data-id': row.id }
                                },
                                {
                                    key: 'designer',
                                    text: t('Designer', 'Designer'),
                                    icon: 'bx bx-edit',
                                    attrs: { 'data-id': row.id }
                                },
                                {
                                    key: 'visualDesigner',
                                    text: t('VisualDesigner', 'Visual Designer'),
                                    icon: 'bx bx-sitemap',
                                    attrs: { 'data-id': row.id }
                                },
                                gateAction({
                                    key: 'instances',
                                    text: t('TabInstances', 'Instances'),
                                    icon: 'bx bx-list-ul',
                                    attrs: { 'data-id': row.id }
                                }, everPublished(status), 'PublishRequiredFirst', 'Publish the definition first.'),
                                gateAction({
                                    key: 'tasks',
                                    text: t('TabTasks', 'Tasks'),
                                    icon: 'bx bx-task',
                                    attrs: { 'data-id': row.id }
                                }, everPublished(status), 'PublishRequiredFirst', 'Publish the definition first.'),
                                gateAction({
                                    key: 'slaRules',
                                    text: t('TabSlaRules', 'SLA Rules'),
                                    icon: 'bx bx-time-five',
                                    attrs: { 'data-id': row.id }
                                }, everPublished(status), 'PublishRequiredFirst', 'Publish the definition first.')
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    t('CreateDefinition', 'Create Definition'),
                    {},
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5], colvisColumns: [2, 3, 4, 5] }
                ),
                initComplete: function () {
                    const tableApi = this.api();
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupDefinitionFilters(tableApi);
                    document.querySelector('.add-new')?.addEventListener('click', (event) => {
                        event.preventDefault();
                        openCreateDefinition();
                    });
                    setTimeout(() => { saveFilterArmed = true; }, 0);
                },
                drawCallback: function () {
                    window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount());
                }
            }
        });
        definitionsDt.on('column-visibility.dt', function () {
            window.DtDefaults?.updateVisualState?.(definitionsDt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(definitionsDt));
        });
        definitionsDt.on('search.dt order.dt column-reorder.dt columns-reordered.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(definitionsDt));
        });
    };

    const submitCreateDefinition = async () => {
        clearFormError('wf-create-error');
        const payload = { templateCode: val('wf-create-code'), name: val('wf-create-name'), description: val('wf-create-description') || null };
        if (!payload.templateCode) { setFormError('wf-create-error', { message: t('TemplateCodeRequired', 'Template Code is required.') }); return; }
        if (!payload.name) { setFormError('wf-create-error', { message: t('NameRequired', 'Name is required.') }); return; }
        const btn = el('wf-create-submit'); btn.disabled = true;
        const res = await api.createDefinition(payload);
        btn.disabled = false;
        if (!res.ok) { setFormError('wf-create-error', res); return; }
        bootstrap.Offcanvas.getInstance(el('wf-create-offcanvas'))?.hide();
        el('wf-create-form').reset();
        notify('success', t('DefinitionCreated', 'Workflow definition created.'), res.correlationId);
        loadDefinitions();
    };

    const openCreateDefinition = () => {
        clearFormError('wf-create-error');
        el('wf-create-form')?.reset();
        bootstrap.Offcanvas.getOrCreateInstance(el('wf-create-offcanvas')).show();
    };

    const openPublishModal = (id, code) => {
        clearFormError('wf-publish-error'); hide(el('wf-publish-result'));
        el('wf-publish-form').reset();
        el('wf-publish-definitionid').value = id;
        el('wf-publish-title-code').textContent = code || '';
        bootstrap.Offcanvas.getOrCreateInstance(el('wf-publish-offcanvas')).show();
    };

    const submitPublish = async () => {
        clearFormError('wf-publish-error');
        const id = val('wf-publish-definitionid');
        const expectedRaw = val('wf-publish-expected');
        const payload = {
            definitionJson: el('wf-publish-json').value,
            schemaVersion: val('wf-publish-schema'),
            expressionVersion: val('wf-publish-expression'),
            expectedTemplateVersion: expectedRaw ? Number(expectedRaw) : null,
            expectedRowVersion: val('wf-publish-rowversion') || null,
            publishReason: val('wf-publish-reason') || null
        };
        if (!payload.definitionJson.trim()) { setFormError('wf-publish-error', { message: t('DefinitionJsonRequired', 'Definition JSON is required.') }); return; }
        if (!payload.schemaVersion) { setFormError('wf-publish-error', { message: t('SchemaVersionRequired', 'Schema Version is required.') }); return; }
        if (!payload.expressionVersion) { setFormError('wf-publish-error', { message: t('ExpressionVersionRequired', 'Expression Version is required.') }); return; }
        const btn = el('wf-publish-submit'); btn.disabled = true;
        const res = await api.publishDefinition(id, payload);
        btn.disabled = false;
        if (!res.ok) { setFormError('wf-publish-error', res); return; }
        const d = res.data || {};
        el('wf-publish-result').innerHTML = `
            <div class="alert alert-success mb-0">
                <div class="fw-medium mb-1">${escapeHtml(t('PublishSucceeded', 'Published successfully.'))}</div>
                <div>${escapeHtml(t('VersionNumber', 'Version Number'))}: <strong>${escapeHtml(d.versionNumber)}</strong></div>
                <div>${escapeHtml(t('Immutable', 'Immutable'))}: ${statusBadge(d.isImmutable ? 'Immutable' : 'Draft')}</div>
                <div>${escapeHtml(t('TemplateVersionId', 'Template Version Id'))}: ${shortId(d.templateVersionId)}</div>
                <div class="small text-muted mt-1">${escapeHtml(debugSuffix(res))}</div>
            </div>`;
        show(el('wf-publish-result'));
        notify('success', t('PublishSucceeded', 'Published successfully.'), res.correlationId);
        loadDefinitions();
    };

    // Candidate Principal Ids — Users/Positions toggle + Select2 (mirrors the Visual Designer).
    // Options are a single pool of `user:{id}` / `position:{id}`; the toggle filters the dropdown
    // while chosen chips of either kind persist. The backend resolves both prefixes.
    const initStartCandidates = async () => {
        if (startCandidatesInited) return;
        startCandidatesInited = true;
        const selectNode = el('wf-start-candidates');
        if (!selectNode) return;
        try {
            const [users, positions] = await Promise.all([
                getJson('/Platform/Workflow/lookup/users'),
                getJson('/Platform/Workflow/lookup/positions')
            ]);
            const userOptions = users.filter((u) => u?.id).map((u) => ({ id: `user:${u.id}`, text: userLabel(u) }));
            const positionOptions = positions.filter((p) => p?.id)
                .map((p) => ({ id: `position:${p.id}`, text: `${p.code || ''} ${p.name || p.id}`.trim() }));
            [...userOptions, ...positionOptions].forEach((opt) => {
                const o = document.createElement('option');
                o.value = opt.id;
                o.textContent = opt.text;
                selectNode.appendChild(o);
            });
        } catch (_e) {
            notify('warning', t('RequestFailed', 'Request failed.'));
        }

        if (window.jQuery && window.jQuery.fn?.select2) {
            const $ = window.jQuery;
            $(selectNode).select2({
                width: '100%',
                dropdownParent: $('#wf-start-modal'),
                placeholder: t('CandidatePrincipalIds', 'Candidate Principal IDs'),
                closeOnSelect: false,
                matcher: (params, data) => {
                    if (!data || !data.id) return data;
                    if (!String(data.id).startsWith(`${startPrincipalType}:`)) return null;
                    const term = (params.term || '').trim().toLowerCase();
                    if (!term) return data;
                    return (data.text || '').toLowerCase().indexOf(term) > -1 ? data : null;
                }
            });
            $('input[name="wf-start-candidates-type"]').on('change', function () {
                startPrincipalType = this.value === 'position' ? 'position' : 'user';
            });
        }
    };

    const readStartCandidates = () => {
        const node = el('wf-start-candidates');
        if (!node) return [];
        if (window.jQuery && window.jQuery.fn?.select2) return window.jQuery(node).val() || [];
        return Array.from(node.selectedOptions || []).map((o) => o.value).filter(Boolean);
    };

    const openStartModal = (templateId, code) => {
        clearFormError('wf-start-error');
        el('wf-start-form').reset();
        el('wf-start-templateid').value = templateId || '';
        el('wf-start-idem').value = api.newGuid();
        el('wf-start-title-code').textContent = code || '';
        initStartCandidates();
        startPrincipalType = 'user';
        if (el('wf-start-candidates-type-user')) el('wf-start-candidates-type-user').checked = true;
        if (window.jQuery && el('wf-start-candidates')) window.jQuery('#wf-start-candidates').val(null).trigger('change');
        new bootstrap.Modal(el('wf-start-modal')).show();
    };

    const submitStartInstance = async () => {
        clearFormError('wf-start-error');
        const candidates = readStartCandidates();
        const due = val('wf-start-dueat');
        const payload = {
            templateId: val('wf-start-templateid') || null,
            templateCode: val('wf-start-templatecode') || null,
            objectType: val('wf-start-objecttype'),
            objectId: val('wf-start-objectid'),
            objectRef: val('wf-start-objectref') || null,
            candidatePrincipalIds: candidates,
            reasonCode: val('wf-start-reason') || null,
            idempotencyKey: val('wf-start-idem') || null,
            commentRequired: checked('wf-start-commentrequired'),
            evidenceRequired: checked('wf-start-evidencerequired'),
            dueAt: due ? new Date(due).toISOString() : null
        };
        if (!payload.templateId && !payload.templateCode) { setFormError('wf-start-error', { message: t('TemplateRequired', 'TemplateId or TemplateCode is required.') }); return; }
        if (!payload.objectType) { setFormError('wf-start-error', { message: t('ObjectTypeRequired', 'Object Type is required.') }); return; }
        if (!payload.objectId) { setFormError('wf-start-error', { message: t('ObjectIdRequired', 'Object Id is required.') }); return; }
        if (!candidates.length) { setFormError('wf-start-error', { message: t('CandidatesRequired', 'At least one Candidate Principal Id is required.') }); return; }
        const btn = el('wf-start-submit'); btn.disabled = true;
        const res = await api.startInstance(payload);
        btn.disabled = false;
        if (!res.ok) { setFormError('wf-start-error', res); return; }
        bootstrap.Modal.getInstance(el('wf-start-modal'))?.hide();
        notify('success', t('InstanceStarted', 'Workflow instance started.'), res.correlationId);
        if (el('wf-instances-modal')?.classList.contains('show')) loadInstances();
    };

    // =====================================================================
    // Instances
    // =====================================================================
    const loadInstances = async () => {
        renderState('inst', { loading: true });
        const res = await api.listInstances();
        if (!res.ok) { el('wf-inst-error').textContent = failureMessage(res); renderState('inst', { error: true }); return; }
        const allItems = asArray(res.data);
        const items = activeDefinitionId
            ? allItems.filter((i) => String(i.templateId || '').toLowerCase() === String(activeDefinitionId).toLowerCase())
            : allItems;
        activeInstanceIds = new Set(items.map((i) => String(i.id || '').toLowerCase()).filter(Boolean));
        if (!items.length) { renderState('inst', { empty: true }); return; }
        el('wf-inst-rows').innerHTML = items.map((i) => `
            <tr>
                <td>${shortId(i.id)}</td>
                <td>${shortId(i.templateId)}</td>
                <td>${escapeHtml(i.objectRef || '—')}</td>
                <td>${statusBadge(i.status)}</td>
                <td>${escapeHtml(i.currentStage || '—')} / ${escapeHtml(i.currentStep || '—')}</td>
                <td>${fmtDate(i.startedAt)}</td>
                <td>${fmtDate(i.dueAt)}</td>
                <td>${fmtDate(i.completedAt)}</td>
            </tr>`).join('');
        renderState('inst', {});
    };

    // =====================================================================
    // Tasks + task actions
    // =====================================================================
    const TERMINAL_TASK = new Set(['approved', 'rejected', 'cancelled', 'completed', 'delegated']);
    const isTerminalTask = (status) => TERMINAL_TASK.has(String(status || '').toLowerCase());

    const loadTasks = async () => {
        renderState('task', { loading: true });
        if (activeDefinitionId && !activeInstanceIds) {
            const instancesRes = await api.listInstances();
            if (instancesRes.ok) {
                activeInstanceIds = new Set(asArray(instancesRes.data)
                    .filter((i) => String(i.templateId || '').toLowerCase() === String(activeDefinitionId).toLowerCase())
                    .map((i) => String(i.id || '').toLowerCase())
                    .filter(Boolean));
            } else {
                activeInstanceIds = new Set();
            }
        }
        const res = await api.listTasks();
        if (!res.ok) { el('wf-task-error').textContent = failureMessage(res); renderState('task', { error: true }); return; }
        const allItems = asArray(res.data);
        const items = activeDefinitionId && activeInstanceIds
            ? allItems.filter((task) => activeInstanceIds.has(String(task.workflowInstanceId || '').toLowerCase()))
            : allItems;
        if (!items.length) { renderState('task', { empty: true }); return; }
        el('wf-task-rows').innerHTML = items.map((task) => {
            const terminal = isTerminalTask(task.status);
            const disabled = terminal ? 'disabled' : '';
            const btn = (action, icon, label) =>
                `<button type="button" class="btn btn-sm btn-icon btn-text-secondary wf-task-action" data-action="${action}" data-id="${escapeHtml(task.id)}" ${disabled} title="${escapeHtml(label)}"><i class="icon-base bx ${icon}"></i></button>`;
            return `
            <tr>
                <td>${shortId(task.id)}</td>
                <td>${shortId(task.workflowInstanceId)}</td>
                <td>${statusBadge(task.status)}</td>
                <td>${escapeHtml(task.stageCode || '—')} / ${escapeHtml(task.stepCode || '—')}</td>
                <td>${fmtDate(task.dueAt)}</td>
                <td>${escapeHtml(task.actionedBy || '—')}</td>
                <td>${fmtDate(task.completedAt)}</td>
                <td class="text-end text-nowrap">
                    ${btn('approve', 'bx-check', t('Approve', 'Approve'))}
                    ${btn('reject', 'bx-x', t('Reject', 'Reject'))}
                    ${btn('delegate', 'bx-share', t('Delegate', 'Delegate'))}
                    ${btn('request-info', 'bx-help-circle', t('RequestInfo', 'Request Info'))}
                    ${btn('cancel', 'bx-block', t('Cancel', 'Cancel'))}
                </td>
            </tr>`;
        }).join('');
        renderState('task', {});
    };

    const TASK_ACTION_META = {
        approve: { title: 'Approve', fn: (id, p) => api.approveTask(id, p), evidence: true, comment: true },
        reject: { title: 'Reject', fn: (id, p) => api.rejectTask(id, p), evidence: true, comment: true },
        delegate: { title: 'Delegate', fn: (id, p) => api.delegateTask(id, p), delegate: true, comment: true },
        'request-info': { title: 'RequestInfo', fn: (id, p) => api.requestInfoTask(id, p), target: true, evidence: true, comment: true },
        cancel: { title: 'Cancel', fn: (id, p) => api.cancelTask(id, p), comment: true }
    };
    let activeTaskAction = null;
    let activeTaskId = null;

    const openTaskActionModal = (action, taskId) => {
        const meta = TASK_ACTION_META[action];
        if (!meta) return;
        activeTaskAction = action; activeTaskId = taskId;
        clearFormError('wf-taskaction-error');
        el('wf-taskaction-form').reset();
        el('wf-taskaction-idem').value = api.newGuid();
        el('wf-taskaction-title').textContent = `${t(meta.title, meta.title)} · ${String(taskId).slice(0, 8)}…`;
        el('wf-taskaction-row-delegate').classList.toggle('d-none', !meta.delegate);
        el('wf-taskaction-row-target').classList.toggle('d-none', !meta.target);
        el('wf-taskaction-row-evidence').classList.toggle('d-none', !meta.evidence);
        new bootstrap.Modal(el('wf-taskaction-modal')).show();
    };

    const submitTaskAction = async () => {
        const meta = TASK_ACTION_META[activeTaskAction];
        if (!meta) return;
        clearFormError('wf-taskaction-error');
        const payload = {
            actorId: val('wf-taskaction-actorid'),
            reasonCode: val('wf-taskaction-reason'),
            idempotencyKey: val('wf-taskaction-idem'),
            comment: val('wf-taskaction-comment') || null
        };
        if (meta.evidence) payload.evidenceRef = val('wf-taskaction-evidence') || null;
        if (meta.delegate) payload.delegatePrincipalId = val('wf-taskaction-delegate');
        if (meta.target) payload.targetPrincipalId = val('wf-taskaction-target') || null;
        if (!payload.actorId) { setFormError('wf-taskaction-error', { message: t('ActorIdRequired', 'Actor Id is required.') }); return; }
        if (!payload.reasonCode) { setFormError('wf-taskaction-error', { message: t('ReasonCodeRequired', 'Reason Code is required.') }); return; }
        if (!payload.idempotencyKey) { setFormError('wf-taskaction-error', { message: t('IdempotencyKeyRequired', 'Idempotency Key is required.') }); return; }
        if (meta.delegate && !payload.delegatePrincipalId) { setFormError('wf-taskaction-error', { message: t('DelegatePrincipalRequired', 'Delegate Principal Id is required.') }); return; }
        const btn = el('wf-taskaction-submit'); btn.disabled = true;
        const res = await meta.fn(activeTaskId, payload);
        btn.disabled = false;
        if (!res.ok) { setFormError('wf-taskaction-error', res); return; }
        bootstrap.Modal.getInstance(el('wf-taskaction-modal'))?.hide();
        if (res.data?.isIdempotent) {
            notify('warning', t('IdempotentResponse', 'Already processed — idempotent response returned.'), res.correlationId);
        } else {
            notify('success', t('TaskActionDone', 'Task action completed.'), res.correlationId);
        }
        loadTasks();
    };

    // =====================================================================
    // SLA rules
    // =====================================================================
    const loadSlaRules = async () => {
        renderState('sla', { loading: true });
        const templateId = val('wf-sla-filter-templateid') || undefined;
        const res = await api.listSlaRules(templateId);
        if (!res.ok) { el('wf-sla-error').textContent = failureMessage(res); renderState('sla', { error: true }); return; }
        const items = asArray(res.data);
        if (!items.length) { renderState('sla', { empty: true }); return; }
        el('wf-sla-rows').innerHTML = items.map((r) => `
            <tr>
                <td>${shortId(r.templateId)}</td>
                <td>${escapeHtml(r.stageCode)} / ${escapeHtml(r.stepCode)}</td>
                <td class="text-center">${escapeHtml(r.dueInMinutes)}</td>
                <td class="text-center">${escapeHtml(r.escalateAfterMinutes)}</td>
                <td class="text-center">${r.timeoutAfterMinutes != null ? escapeHtml(r.timeoutAfterMinutes) : '—'}</td>
                <td>${escapeHtml((r.escalationPrincipalIds || []).join(', '))}</td>
                <td>${statusBadge(r.isActive ? 'Active' : 'Passive')}</td>
            </tr>`).join('');
        renderState('sla', {});
    };

    const submitSlaRule = async () => {
        clearFormError('wf-sla-form-error');
        const principals = val('wf-sla-principals').split(',').map((s) => s.trim()).filter(Boolean);
        const timeoutRaw = val('wf-sla-timeout');
        const due = Number(val('wf-sla-due'));
        const escalate = Number(val('wf-sla-escalate'));
        const timeout = timeoutRaw ? Number(timeoutRaw) : null;
        if (!val('wf-sla-templateid')) { setFormError('wf-sla-form-error', { message: t('TemplateRequired', 'TemplateId is required.') }); return; }
        if (!val('wf-sla-stage') || !val('wf-sla-step')) { setFormError('wf-sla-form-error', { message: t('StageStepRequired', 'Stage Code and Step Code are required.') }); return; }
        if (!(due > 0)) { setFormError('wf-sla-form-error', { message: t('DueMinutesPositive', 'Due In Minutes must be greater than 0.') }); return; }
        if (!(escalate >= due)) { setFormError('wf-sla-form-error', { message: t('EscalateGteDue', 'Escalate After Minutes must be ≥ Due In Minutes.') }); return; }
        if (timeout != null && !(timeout >= escalate)) { setFormError('wf-sla-form-error', { message: t('TimeoutGteEscalate', 'Timeout After Minutes must be ≥ Escalate After Minutes.') }); return; }
        if (!principals.length) { setFormError('wf-sla-form-error', { message: t('EscalationPrincipalsRequired', 'Escalation Principal Ids are required.') }); return; }
        const payload = {
            templateId: val('wf-sla-templateid'), stageCode: val('wf-sla-stage'), stepCode: val('wf-sla-step'),
            dueInMinutes: due, escalateAfterMinutes: escalate, timeoutAfterMinutes: timeout, escalationPrincipalIds: principals
        };
        const btn = el('wf-sla-submit'); btn.disabled = true;
        const res = await api.createSlaRule(payload);
        btn.disabled = false;
        if (!res.ok) { setFormError('wf-sla-form-error', res); return; }
        el('wf-sla-form').reset();
        notify('success', t('SlaRuleCreated', 'SLA rule created.'), res.correlationId);
        loadSlaRules();
    };

    // =====================================================================
    // Escalation runner
    // =====================================================================
    const runEscalations = async () => {
        clearFormError('wf-esc-error');
        const now = val('wf-esc-now');
        const maxRaw = val('wf-esc-max');
        const payload = {
            nowUtc: now ? new Date(now).toISOString() : null,
            maxItems: maxRaw ? Number(maxRaw) : null,
            idempotencyKey: val('wf-esc-idem') || null
        };
        const btn = el('wf-esc-run'); btn.disabled = true;
        const res = await api.runEscalations(payload);
        btn.disabled = false;
        if (!res.ok) { setFormError('wf-esc-error', res); return; }
        const d = res.data || {};
        el('wf-esc-summary').innerHTML = `
            <div class="col"><div class="card border shadow-none"><div class="card-body text-center py-3">
                <div class="h4 mb-0">${escapeHtml(d.evaluatedCount ?? 0)}</div><small class="text-muted">${escapeHtml(t('EvaluatedCount', 'Evaluated'))}</small></div></div></div>
            <div class="col"><div class="card border shadow-none"><div class="card-body text-center py-3">
                <div class="h4 mb-0 text-warning">${escapeHtml(d.escalatedCount ?? 0)}</div><small class="text-muted">${escapeHtml(t('EscalatedCount', 'Escalated'))}</small></div></div></div>
            <div class="col"><div class="card border shadow-none"><div class="card-body text-center py-3">
                <div class="h4 mb-0 text-danger">${escapeHtml(d.timedOutCount ?? 0)}</div><small class="text-muted">${escapeHtml(t('TimedOutCount', 'Timed Out'))}</small></div></div></div>
            <div class="col"><div class="card border shadow-none"><div class="card-body text-center py-3">
                <div class="h4 mb-0 text-muted">${escapeHtml(d.skippedCount ?? 0)}</div><small class="text-muted">${escapeHtml(t('SkippedCount', 'Skipped'))}</small></div></div></div>`;
        show(el('wf-esc-summary'));
        const results = Array.isArray(d.results) ? d.results : [];
        el('wf-esc-rows').innerHTML = results.length
            ? results.map((r) => `
                <tr>
                    <td>${shortId(r.workflowInstanceId)}</td>
                    <td>${shortId(r.approvalTaskId)}</td>
                    <td>${escapeHtml(r.action)}</td>
                    <td>${statusBadge(r.previousTaskStatus)} → ${statusBadge(r.newTaskStatus)}</td>
                    <td>${escapeHtml(r.reasonCode || '—')}</td>
                    <td class="text-center">${yesNo(r.isIdempotent)}</td>
                </tr>`).join('')
            : `<tr><td colspan="6" class="text-center text-muted py-3">${escapeHtml(t('NoResults', 'No results.'))}</td></tr>`;
        show(el('wf-esc-results-card'));
        notify('success', t('EscalationRunComplete', 'Escalation run complete.'), res.correlationId);
    };

    document.addEventListener('DOMContentLoaded', () => {
        // Definitions
        el('wf-def-refresh')?.addEventListener('click', loadDefinitions);
        el('wf-create-open')?.addEventListener('click', openCreateDefinition);
        el('wf-create-submit')?.addEventListener('click', submitCreateDefinition);
        el('wf-publish-submit')?.addEventListener('click', submitPublish);
        el('wf-start-submit')?.addEventListener('click', submitStartInstance);
        dtTableEl?.addEventListener('click', (event) => {
            const link = event.target.closest('.wf-definition-detail-link');
            if (!link) return;
            event.preventDefault();
            const id = link.dataset.id;
            const row = id ? definitionsCache.find((item) => String(item.id) === String(id)) : null;
            openDefinitionDetails(id, row);
        });

        // Instances
        el('wf-inst-refresh')?.addEventListener('click', loadInstances);

        // Tasks
        el('wf-task-refresh')?.addEventListener('click', loadTasks);
        el('wf-task-rows')?.addEventListener('click', (ev) => {
            const b = ev.target.closest('.wf-task-action');
            if (b && !b.disabled) openTaskActionModal(b.dataset.action, b.dataset.id);
        });
        el('wf-taskaction-submit')?.addEventListener('click', submitTaskAction);
        el('wf-taskaction-regen')?.addEventListener('click', () => { el('wf-taskaction-idem').value = api.newGuid(); });

        // SLA
        el('wf-sla-refresh')?.addEventListener('click', loadSlaRules);
        el('wf-sla-submit')?.addEventListener('click', submitSlaRule);

        // Escalations
        el('wf-open-escalations')?.addEventListener('click', () => { window.location.href = '/Platform/Workflow/Escalations'; });
        el('wf-esc-run')?.addEventListener('click', runEscalations);
        el('wf-esc-regen')?.addEventListener('click', () => { el('wf-esc-idem').value = api.newGuid(); });

        el('wf-start-regen')?.addEventListener('click', () => { el('wf-start-idem').value = api.newGuid(); });

        // Initial load of the Definitions table
        loadDefinitions();
    });
})();
