'use strict';

// MOD-0023 — Workflow Tasks list (golden compact DataTable v2).
// Standalone page: lists the approval tasks of one definition's instances, with inline Status filter,
// Save View personalization, ColVis, ColReorder, and the per-row task actions
// (approve / reject / delegate / request-info / cancel) via a shared action modal.
// The list endpoint returns all tenant tasks, so rows are filtered to this definition's instances.
(function () {
    const api = window.WorkflowApi;
    const L = window.WorkflowL10n || {};
    const t = (key, fallback) => (L[key] != null ? L[key] : (fallback != null ? fallback : key));
    const definitionId = window.WorkflowDefinitionId;

    const el = (id) => document.getElementById(id);
    const val = (id) => (el(id)?.value ?? '').trim();
    const show = (node) => node && node.classList.remove('d-none');
    const hide = (node) => node && node.classList.add('d-none');

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
        const datePart = d.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: '2-digit' });
        const timePart = d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: true });
        return `<span class="d-inline-flex flex-column lh-sm"><span>${escapeHtml(datePart)}</span><span>${escapeHtml(timePart)}</span></span>`;
    };

    const STATUS_TONE = {
        waitingapproval: 'warning', waitingevidence: 'warning', pending: 'warning', escalated: 'warning',
        approved: 'success', completed: 'success', delegated: 'info',
        rejected: 'danger', cancelled: 'danger', timedout: 'danger', failed: 'danger'
    };
    const statusBadge = (status) => {
        if (status === null || status === undefined || status === '') return '<span class="text-muted">—</span>';
        const tone = STATUS_TONE[String(status).toLowerCase().replace(/[^a-z]/g, '')] || 'secondary';
        return `<span class="badge bg-label-${tone}">${escapeHtml(status)}</span>`;
    };

    const TERMINAL_TASK = new Set(['approved', 'rejected', 'cancelled', 'completed', 'delegated', 'timedout']);
    const isTerminalTask = (status) => TERMINAL_TASK.has(String(status || '').toLowerCase().replace(/[^a-z]/g, ''));

    const notify = (kind, message) => {
        const type = kind === 'error' || kind === 'danger' ? 'error' : (kind === 'warning' || kind === 'info' ? kind : 'success');
        if (typeof window.showToast === 'function') { window.showToast(message, type); return; }
        console[type === 'error' ? 'error' : 'log'](message);
    };
    const failureMessage = (res) => {
        if (res.status === 403) return t('PermissionMissing', 'Permission missing or not assigned.');
        if (res.status === 0) return t('NetworkError', 'Cannot reach the workflow gateway.');
        if (res.status === 503) return t('GatewayUnavailable', 'Workflow gateway proxy unavailable.');
        return res.message || t('RequestFailed', 'Request failed.');
    };
    const asArray = (data) => (Array.isArray(data) ? data : (Array.isArray(data?.items) ? data.items : []));

    // =====================================================================
    // DataTable + Save View state
    // =====================================================================
    let tasksDt = null;
    let activeInstanceIds = null;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-workflow-tasks');
    const dtL = () => window.L10n || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const baseOrder = [[5, 'asc']];
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
    const defaultColVis = () => saveViewColumnIndexes.reduce((acc, index) => { acc[index] = true; return acc; }, {});
    const captureColVis = (tableApi) => {
        const result = {};
        saveViewColumnIndexes.forEach((index) => { try { result[index] = !!tableApi.column(index).visible(); } catch (_e) { } });
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
        if (typeof raw === 'string') { try { return JSON.parse(raw); } catch (_e) { return {}; } }
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
        filters: emptyFilters(), search: '', colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder
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
            const views = await personalizationClient.getViews('Platform', 'WorkflowTasks');
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[WorkflowTasks SaveView] Failed to load saved views.', error);
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
            pageKey: 'WorkflowTasks',
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

    // =====================================================================
    // Inline filter (Status)
    // =====================================================================
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
    const registerFilters = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.workflowFilterBound === '1') return;
        dtTableEl.dataset.workflowFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _searchData, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || tasksDt?.row(dataIndex)?.data?.() || null;
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
            $clearBtn.on('mousedown', (event) => { event.preventDefault(); event.stopPropagation(); $select.val(null).trigger('change'); });
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
    const setupFilters = (tableApi) => {
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

    // =====================================================================
    // Task action modal (approve / reject / delegate / request-info / cancel)
    // =====================================================================
    const setBoxError = (id, msg) => { const b = el(id); if (b) { b.textContent = msg; show(b); } };
    const clearBox = (id) => hide(el(id));

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
        if (!meta || !taskId) return;
        activeTaskAction = action;
        activeTaskId = taskId;
        clearBox('wf-taskaction-error');
        el('wf-taskaction-form').reset();
        el('wf-taskaction-idem').value = api.newGuid();
        el('wf-taskaction-title').textContent = `${t(meta.title, meta.title)} · ${String(taskId).slice(0, 8)}…`;
        el('wf-taskaction-row-delegate').classList.toggle('d-none', !meta.delegate);
        el('wf-taskaction-row-target').classList.toggle('d-none', !meta.target);
        el('wf-taskaction-row-evidence').classList.toggle('d-none', !meta.evidence);
        bootstrap.Offcanvas.getOrCreateInstance(el('wf-taskaction-offcanvas')).show();
    };

    const submitTaskAction = async () => {
        const meta = TASK_ACTION_META[activeTaskAction];
        if (!meta) return;
        clearBox('wf-taskaction-error');
        const payload = {
            actorId: val('wf-taskaction-actorid'),
            reasonCode: val('wf-taskaction-reason'),
            idempotencyKey: val('wf-taskaction-idem'),
            comment: val('wf-taskaction-comment') || null
        };
        if (meta.evidence) payload.evidenceRef = val('wf-taskaction-evidence') || null;
        if (meta.delegate) payload.delegatePrincipalId = val('wf-taskaction-delegate');
        if (meta.target) payload.targetPrincipalId = val('wf-taskaction-target') || null;
        if (!payload.actorId) { setBoxError('wf-taskaction-error', t('ActorIdRequired', 'Actor Id is required.')); return; }
        if (!payload.reasonCode) { setBoxError('wf-taskaction-error', t('ReasonCodeRequired', 'Reason Code is required.')); return; }
        if (!payload.idempotencyKey) { setBoxError('wf-taskaction-error', t('IdempotencyKeyRequired', 'Idempotency Key is required.')); return; }
        if (meta.delegate && !payload.delegatePrincipalId) { setBoxError('wf-taskaction-error', t('DelegatePrincipalRequired', 'Delegate Principal Id is required.')); return; }
        const btn = el('wf-taskaction-submit');
        if (btn) btn.disabled = true;
        const res = await meta.fn(activeTaskId, payload);
        if (btn) btn.disabled = false;
        if (!res.ok) { setBoxError('wf-taskaction-error', failureMessage(res)); return; }
        bootstrap.Offcanvas.getInstance(el('wf-taskaction-offcanvas'))?.hide();
        if (res.data?.isIdempotent) {
            notify('warning', t('IdempotentResponse', 'Already processed — idempotent response returned.'));
        } else {
            notify('success', t('TaskActionDone', 'Task action completed.'));
        }
        tasksDt?.ajax.reload(null, false);
    };

    // =====================================================================
    // DataTable
    // =====================================================================
    const loadInstanceIds = async () => {
        if (!definitionId) { activeInstanceIds = null; return; }
        const res = await api.listInstances();
        activeInstanceIds = res.ok
            ? new Set(asArray(res.data)
                .filter((i) => String(i.templateId || '').toLowerCase() === String(definitionId).toLowerCase())
                .map((i) => String(i.id || '').toLowerCase())
                .filter(Boolean))
            : new Set();
    };

    const tasksAjaxDataSrc = (json) => {
        const payload = json?.data?.data || json?.data || json;
        const all = Array.isArray(payload) ? payload : (Array.isArray(payload?.items) ? payload.items : []);
        const items = activeInstanceIds
            ? all.filter((task) => activeInstanceIds.has(String(task.workflowInstanceId || '').toLowerCase()))
            : all;
        populateStatusOptions(items);
        return items;
    };

    // Renders a task action; disabled (greyed, non-clickable) when the task is terminal.
    const taskAction = (cfg, terminal) => terminal
        ? Object.assign({}, cfg, {
            className: [cfg.className, 'disabled'].filter(Boolean).join(' '),
            attrs: Object.assign({}, cfg.attrs, { 'aria-disabled': 'true', tabindex: '-1' })
        })
        : cfg;

    const initDataTable = async () => {
        if (!dtTableEl || tasksDt) return;
        if (!window.DitenDataTable || !window.DtDefaults) {
            console.error('[WorkflowTasks] DataTable shared helpers are required.');
            return;
        }
        await loadDefaultView();
        await loadInstanceIds();
        registerFilters();
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
                    const targetApi = tableApi || tasksDt;
                    if (!targetApi) return;
                    try {
                        await saveDefaultView(getCurrentView(targetApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(l10n.RecordSaved || l10n.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[WorkflowTasks SaveView] Failed to save default view.', error);
                        window.showToast?.(l10n.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        tasksDt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: '/Platform/Workflow/api/tasks',
                type: 'GET',
                dataSrc: tasksAjaxDataSrc
            },
            actions: {
                onRowAction: {
                    approve: ({ id }) => openTaskActionModal('approve', id),
                    reject: ({ id }) => openTaskActionModal('reject', id),
                    delegate: ({ id }) => openTaskActionModal('delegate', id),
                    'request-info': ({ id }) => openTaskActionModal('request-info', id),
                    cancel: ({ id }) => openTaskActionModal('cancel', id)
                }
            },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                order: baseOrder,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'taskId' },
                    { data: 'workflowInstanceId', name: 'workflowInstanceId' },
                    { data: 'status', name: 'status' },
                    { data: 'stageCode', name: 'stageCode' },
                    { data: 'dueAt', name: 'dueAt' },
                    { data: 'actionedBy', name: 'actionedBy' },
                    { data: 'completedAt', name: 'completedAt' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, className: 'fw-medium', render: (data, type) => type === 'display' ? shortId(data) : (data || '') },
                    { targets: 2, render: (data, type) => type === 'display' ? shortId(data) : (data || '') },
                    { targets: 3, render: (data, type) => type === 'display' ? statusBadge(data) : (data || '') },
                    {
                        targets: 4,
                        render: (data, type, row) => type === 'display'
                            ? `${escapeHtml(row.stageCode || '—')} / ${escapeHtml(row.stepCode || '—')}`
                            : (data || '')
                    },
                    { targets: 5, render: (data, type) => type === 'display' ? fmtDate(data) : (data || '') },
                    { targets: 6, render: (data) => escapeHtml(data || '—') },
                    { targets: 7, render: (data, type) => type === 'display' ? fmtDate(data) : (data || '') },
                    {
                        targets: -1,
                        title: l10n.Actions || t('Actions', 'Actions'),
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (_data, _type, row) => {
                            const terminal = isTerminalTask(row.status);
                            return window.DitenDataTable.renderActions([
                                taskAction({ key: 'approve', className: 'btn-text-secondary', icon: 'bx bx-check', attrs: { 'data-id': row.id, title: t('Approve', 'Approve') } }, terminal),
                                taskAction({ key: 'reject', text: t('Reject', 'Reject'), icon: 'bx bx-x', attrs: { 'data-id': row.id } }, terminal),
                                taskAction({ key: 'delegate', text: t('Delegate', 'Delegate'), icon: 'bx bx-share', attrs: { 'data-id': row.id } }, terminal),
                                taskAction({ key: 'request-info', text: t('RequestInfo', 'Request Info'), icon: 'bx bx-help-circle', attrs: { 'data-id': row.id } }, terminal),
                                taskAction({ key: 'cancel', text: t('Cancel', 'Cancel'), icon: 'bx bx-block', attrs: { 'data-id': row.id } }, terminal)
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    null,
                    {},
                    extraButtons,
                    { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] }
                ),
                initComplete: function () {
                    const tableApi = this.api();
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupFilters(tableApi);
                    setTimeout(() => { saveFilterArmed = true; }, 0);
                },
                drawCallback: function () {
                    window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount());
                }
            }
        });

        tasksDt.on('column-visibility.dt', function () {
            window.DtDefaults?.updateVisualState?.(tasksDt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(tasksDt));
        });
        tasksDt.on('search.dt order.dt column-reorder.dt columns-reordered.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(tasksDt));
        });
    };

    const loadMeta = async () => {
        if (!api || !definitionId) return;
        const res = await api.getDefinition(definitionId);
        if (!res.ok) { notify('error', failureMessage(res)); return; }
        const d = res.data || {};
        if (el('wf-meta-code')) el('wf-meta-code').textContent = d.templateCode || '-';
        if (el('wf-meta-name')) el('wf-meta-name').textContent = d.name || '-';
    };

    document.addEventListener('DOMContentLoaded', () => {
        loadMeta();
        initDataTable();
        el('wf-taskaction-submit')?.addEventListener('click', submitTaskAction);
        el('wf-taskaction-regen')?.addEventListener('click', () => { el('wf-taskaction-idem').value = api.newGuid(); });
    });
})();
