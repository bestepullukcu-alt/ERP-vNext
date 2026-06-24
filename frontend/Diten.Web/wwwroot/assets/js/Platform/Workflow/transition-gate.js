'use strict';

// MOD-0023 - Workflow Transition Gate standalone page.
// The test panel is a golden slim offcanvas; read-only evaluation results render into a golden
// compact DataTable with export / colvis / colreorder / inline filter / save view.
(function () {
    const api = window.WorkflowApi;
    const L = window.WorkflowL10n || {};
    const t = (key, fallback) => (L[key] != null ? L[key] : (fallback != null ? fallback : key));

    const el = (id) => document.getElementById(id);
    const val = (id) => (el(id)?.value ?? '').trim();
    const show = (node) => node && node.classList.remove('d-none');
    const hide = (node) => node && node.classList.add('d-none');
    const asArray = (data) => (Array.isArray(data) ? data : (Array.isArray(data?.items) ? data.items : []));

    const escapeHtml = (value) => {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    };
    const shortId = (value) => {
        if (!value) return '<span class="text-muted">&mdash;</span>';
        const s = String(value);
        return `<code class="text-body" title="${escapeHtml(s)}">${escapeHtml(s.length > 8 ? s.slice(0, 8) + '...' : s)}</code>`;
    };
    const STATUS_TONE = {
        allowed: 'success',
        blocked: 'danger',
        notapplicable: 'secondary',
        active: 'success',
        completed: 'success',
        pending: 'warning',
        waitingapproval: 'warning',
        waitingevidence: 'warning',
        noworkflow: 'secondary',
        notterminalapproved: 'danger',
        failed: 'danger',
        rejected: 'danger',
        cancelled: 'danger'
    };
    const DECISION_MAP = {
        0: 'Allowed',
        1: 'Blocked',
        2: 'NotApplicable',
        allowed: 'Allowed',
        blocked: 'Blocked',
        notapplicable: 'NotApplicable',
        notApplicable: 'NotApplicable'
    };
    const GATE_STATUS_MAP = {
        0: 'NoWorkflow',
        1: 'PendingApproval',
        2: 'WaitingEvidence',
        3: 'Approved',
        4: 'Rejected',
        5: 'Cancelled',
        6: 'NotTerminalApproved',
        noworkflow: 'NoWorkflow',
        noWorkflow: 'NoWorkflow',
        pendingapproval: 'PendingApproval',
        pendingApproval: 'PendingApproval',
        waitingevidence: 'WaitingEvidence',
        waitingEvidence: 'WaitingEvidence',
        approved: 'Approved',
        rejected: 'Rejected',
        cancelled: 'Cancelled',
        notterminalapproved: 'NotTerminalApproved',
        notTerminalApproved: 'NotTerminalApproved'
    };
    const normalizeEnumLabel = (value, map) => {
        if (value === null || value === undefined || value === '') return '';
        const raw = String(value);
        const compact = raw.toLowerCase().replace(/[^a-z0-9]/g, '');
        return map[raw] || map[Number(raw)] || map[compact] || raw;
    };
    const formatDecision = (value) => normalizeEnumLabel(value, DECISION_MAP);
    const formatGateStatus = (value) => normalizeEnumLabel(value, GATE_STATUS_MAP);
    const statusBadge = (status) => {
        if (status === null || status === undefined || status === '') return '<span class="text-muted">&mdash;</span>';
        const tone = STATUS_TONE[String(status).toLowerCase().replace(/[^a-z]/g, '')] || 'secondary';
        return `<span class="badge bg-label-${tone}">${escapeHtml(status)}</span>`;
    };
    const notify = (kind, message) => {
        const type = kind === 'error' || kind === 'danger' ? 'error' : (kind === 'warning' || kind === 'info' ? kind : 'success');
        if (typeof window.showToast === 'function') { window.showToast(message, type); return; }
        console[type === 'error' ? 'error' : 'log'](message);
    };
    const failureMessage = (res) => {
        if (res.status === 403) return t('PermissionMissing', 'Permission missing or not assigned.');
        if (res.status === 0) return t('NetworkError', 'Cannot reach the workflow gateway.');
        if (res.status === 503) return t('GatewayUnavailable', 'Workflow gateway proxy unavailable.');
        if (res.status === 404) return res.message || t('NotFound', 'Not found.');
        return res.message || t('RequestFailed', 'Request failed.');
    };
    const debugSuffix = (res) => {
        const parts = [];
        if (res.reasonCode) parts.push(`${t('ReasonCode', 'Reason')}: ${res.reasonCode}`);
        if (res.correlationId) parts.push(`${t('CorrelationId', 'Correlation')}: ${res.correlationId}`);
        return parts.join(' - ');
    };
    const unwrapItems = (payload) => {
        if (Array.isArray(payload)) return payload;
        if (Array.isArray(payload?.data)) return payload.data;
        if (Array.isArray(payload?.data?.items)) return payload.data.items;
        if (Array.isArray(payload?.items)) return payload.items;
        return [];
    };
    const getJson = async (url) => {
        const response = await fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
        if (!response.ok) return [];
        return unwrapItems(await response.json());
    };
    const userLabel = (user) => {
        const name = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
        return name || user.fullName || user.displayName || user.email || user.userName || user.id;
    };
    const setBoxError = (id, message) => {
        const box = el(id);
        if (!box) return;
        box.textContent = message;
        show(box);
    };
    const clearBox = (id) => hide(el(id));
    const setStatus = (kind, message) => {
        const box = el('wf-gate-status');
        if (!box) return;
        box.className = `alert alert-${kind === 'error' ? 'danger' : (kind === 'warning' ? 'warning' : 'success')}`;
        box.textContent = message;
        box.classList.remove('d-none');
    };

    // =====================================================================
    // DataTable + Save View state
    // =====================================================================
    let gateDt = null;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-workflow-transition-gate');
    const dtL = () => window.L10n || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8];
    const totalColumnCount = 9;
    const baseOrder = [[1, 'asc']];
    let appliedFilters = { status: [] };

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilters = (filters) => ({ status: normalizeArray(filters?.status) });
    const emptyFilters = () => ({ status: [] });
    const matchesStatusFilter = (selected, status) => {
        const values = normalizeArray(selected);
        return !values.length || values.includes(normalizeString(status));
    };
    const getAppliedFilterCount = () => normalizeArray(appliedFilters.status).length ? 1 : 0;

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
            const views = await personalizationClient.getViews('Platform', 'WorkflowTransitionGate');
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[WorkflowTransitionGate SaveView] Failed to load saved views.', error);
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
            pageKey: 'WorkflowTransitionGate',
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
    // Inline filter (Status = Decision)
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
            const row = rowData || gateDt?.row(dataIndex)?.data?.() || null;
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
    // Evaluate form Select2 controls
    // =====================================================================
    const transitionOptions = [
        { id: 'submit', text: 'Submit' },
        { id: 'approve', text: 'Approve' },
        { id: 'reject', text: 'Reject' },
        { id: 'cancel', text: 'Cancel' },
        { id: 'publish', text: 'Publish' },
        { id: 'archive', text: 'Archive' }
    ];
    const targetStateOptions = [
        { id: 'Submitted', text: 'Submitted' },
        { id: 'Approved', text: 'Approved' },
        { id: 'Rejected', text: 'Rejected' },
        { id: 'Cancelled', text: 'Cancelled' },
        { id: 'Published', text: 'Published' },
        { id: 'Archived', text: 'Archived' },
        { id: 'Draft', text: 'Draft' },
        { id: 'Reviewed', text: 'Reviewed' }
    ];
    let gateSelectsInited = false;
    let gateSelectsInitPromise = null;

    const fillSelect = (selectNode, options) => {
        if (!selectNode) return;
        const current = selectNode.value;
        selectNode.innerHTML = '<option value=""></option>';
        options.forEach((option) => {
            const node = document.createElement('option');
            node.value = option.id;
            node.textContent = option.text;
            selectNode.appendChild(node);
        });
        if (current && options.some((option) => option.id === current)) selectNode.value = current;
    };
    const initSingleSelect2 = (selector) => {
        if (!window.jQuery?.fn?.select2) return;
        const $select = window.jQuery(selector);
        if (!$select.length) return;
        if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
        $select.select2({
            width: '100%',
            dropdownParent: window.jQuery('#wf-gate-offcanvas'),
            placeholder: $select.data('placeholder') || '',
            allowClear: true
        });
    };
    const initGateSelects = async () => {
        if (gateSelectsInited) return;
        if (gateSelectsInitPromise) return gateSelectsInitPromise;

        gateSelectsInitPromise = (async () => {
            fillSelect(el('wf-gate-transition'), transitionOptions);
            fillSelect(el('wf-gate-target'), targetStateOptions);

            try {
                const users = await getJson('/Platform/Workflow/lookup/users');
                const userOptions = users
                    .filter((user) => user?.id)
                    .map((user) => ({ id: String(user.id), text: userLabel(user) }));
                fillSelect(el('wf-gate-actorid'), userOptions);
            } catch (_e) {
                notify('warning', t('RequestFailed', 'Request failed.'));
            }

            initSingleSelect2('#wf-gate-transition');
            initSingleSelect2('#wf-gate-target');
            initSingleSelect2('#wf-gate-actorid');
            gateSelectsInited = true;
        })();

        return gateSelectsInitPromise;
    };

    // =====================================================================
    // Offcanvas + evaluation
    // =====================================================================
    const openEvaluate = async () => {
        clearBox('wf-gate-error');
        await initGateSelects();
        bootstrap.Offcanvas.getOrCreateInstance(el('wf-gate-offcanvas')).show();
    };
    const validatePayload = (payload) => {
        for (const [field, label] of [
            ['objectType', 'ObjectType'],
            ['objectId', 'ObjectId'],
            ['objectRef', 'ObjectRef'],
            ['requestedTransition', 'RequestedTransition'],
            ['requestedTargetState', 'RequestedTargetState'],
            ['actorId', 'ActorId']
        ]) {
            if (!payload[field]) return `${t(label, label)} ${t('IsRequired', 'is required.')}`;
        }
        return '';
    };
    const evaluateTransition = async () => {
        clearBox('wf-gate-error');
        const payload = {
            objectType: val('wf-gate-objecttype'),
            objectId: val('wf-gate-objectid'),
            objectRef: val('wf-gate-objectref'),
            requestedTransition: val('wf-gate-transition'),
            requestedTargetState: val('wf-gate-target'),
            actorId: val('wf-gate-actorid'),
            reasonCode: val('wf-gate-reason') || null
        };
        const validationMessage = validatePayload(payload);
        if (validationMessage) { setBoxError('wf-gate-error', validationMessage); return; }

        const btn = el('wf-gate-eval');
        if (btn) btn.disabled = true;
        const res = await api.evaluateTransition(payload);
        if (btn) btn.disabled = false;
        if (!res.ok) { setBoxError('wf-gate-error', failureMessage(res)); return; }

        const d = res.data || {};
        const decision = formatDecision(d.decision);
        const gateStatus = formatGateStatus(d.gateStatus);
        const row = {
            status: decision,
            decision,
            objectRef: payload.objectRef,
            requestedTransition: payload.requestedTransition,
            requestedTargetState: payload.requestedTargetState,
            gateStatus,
            blockingReasonCode: d.blockingReasonCode || '',
            blockingMessage: d.blockingMessage || '',
            workflowInstanceId: d.workflowInstanceId || '',
            activeTaskId: d.activeTaskId || '',
            correlationId: res.correlationId || ''
        };

        if (gateDt) {
            gateDt.row.add(row);
            gateDt.draw(false);
            populateStatusOptions(gateDt.rows().data().toArray());
        }
        bootstrap.Offcanvas.getInstance(el('wf-gate-offcanvas'))?.hide();
        setStatus('success', [t('Evaluate', 'Evaluate'), debugSuffix(res)].filter(Boolean).join(' - '));
        notify('success', t('Evaluate', 'Evaluate'));
    };

    // =====================================================================
    // DataTable
    // =====================================================================
    const initDataTable = async () => {
        if (!dtTableEl || gateDt) return;
        if (!window.DataTable || !window.DtDefaults) {
            console.error('[WorkflowTransitionGate] DataTable shared helpers are required.');
            return;
        }
        await loadDefaultView();
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
                    const targetApi = tableApi || gateDt;
                    if (!targetApi) return;
                    try {
                        await saveDefaultView(getCurrentView(targetApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(l10n.RecordSaved || l10n.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[WorkflowTransitionGate SaveView] Failed to save default view.', error);
                        window.showToast?.(l10n.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        gateDt = new DataTable(dtTableEl, window.DtDefaults.create({
            data: [],
            stateSave: false,
            colReorder: { columns: ':gt(0)' },
            order: baseOrder,
            columns: [
                { data: 'decision', name: 'control' },
                { data: 'decision', name: 'decision' },
                { data: 'objectRef', name: 'objectRef' },
                { data: 'requestedTransition', name: 'requestedTransition' },
                { data: 'requestedTargetState', name: 'requestedTargetState' },
                { data: 'gateStatus', name: 'gateStatus' },
                { data: 'blockingReasonCode', name: 'blockingReasonCode' },
                { data: 'workflowInstanceId', name: 'workflowInstanceId' },
                { data: 'activeTaskId', name: 'activeTaskId' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, className: 'fw-medium', render: (data, type) => type === 'display' ? statusBadge(data) : (data || '') },
                { targets: 2, render: (data) => escapeHtml(data || '-') },
                { targets: 3, render: (data) => escapeHtml(data || '-') },
                { targets: 4, render: (data) => escapeHtml(data || '-') },
                { targets: 5, render: (data, type) => type === 'display' ? statusBadge(formatGateStatus(data)) : formatGateStatus(data) },
                {
                    targets: 6,
                    render: (data, type, row) => {
                        const reason = data || '-';
                        const message = row.blockingMessage || '';
                        return type === 'display' && message
                            ? `<span title="${escapeHtml(message)}">${escapeHtml(reason)}</span>`
                            : escapeHtml(reason);
                    }
                },
                { targets: 7, render: (data, type) => type === 'display' ? shortId(data) : (data || '') },
                { targets: 8, render: (data, type) => type === 'display' ? shortId(data) : (data || '') }
            ],
            buttons: window.DtDefaults.exportButtons(
                t('Evaluate', 'Evaluate'),
                {},
                extraButtons,
                { exportColumns: [1, 2, 3, 4, 5, 6, 7, 8], colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8] }
            ),
            initComplete: function () {
                const tableApi = this.api();
                mountInlineFilter();
                bindInlineFilterA11y();
                setupFilters(tableApi);
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    openEvaluate();
                });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount());
            }
        }));

        gateDt.on('column-visibility.dt', function () {
            window.DtDefaults?.updateVisualState?.(gateDt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(gateDt));
        });
        gateDt.on('search.dt order.dt column-reorder.dt columns-reordered.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(gateDt));
        });
    };

    document.addEventListener('DOMContentLoaded', () => {
        initDataTable();
        initGateSelects();
        el('wf-gate-eval')?.addEventListener('click', evaluateTransition);
    });
})();
