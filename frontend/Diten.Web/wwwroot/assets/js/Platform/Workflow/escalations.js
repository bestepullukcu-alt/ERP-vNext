'use strict';

// MOD-0023 — Workflow Escalation Runner (standalone page).
// Run Escalations is a global operation (POST /escalations/run), not a per-definition list, so the
// results table is client-side (fed from the run response). Run parameters live in a golden slim
// offcanvas; results render into a golden compact DataTable (export / colvis / colreorder / inline
// filter by action / save view) with summary cards above it.
(function () {
    const api = window.WorkflowApi;
    const L = window.WorkflowL10n || {};
    const t = (key, fallback) => (L[key] != null ? L[key] : (fallback != null ? fallback : key));

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

    const STATUS_TONE = {
        approved: 'success', completed: 'success', active: 'success',
        escalated: 'warning', waitingapproval: 'warning', waitingevidence: 'warning',
        timedout: 'danger', cancelled: 'danger', rejected: 'danger', failed: 'danger'
    };
    const statusBadge = (status) => {
        if (status === null || status === undefined || status === '') return '<span class="text-muted">—</span>';
        const tone = STATUS_TONE[String(status).toLowerCase().replace(/[^a-z]/g, '')] || 'secondary';
        return `<span class="badge bg-label-${tone}">${escapeHtml(status)}</span>`;
    };
    const yesNo = (b) => (b
        ? `<span class="badge bg-label-info">${escapeHtml(t('Yes', 'Yes'))}</span>`
        : `<span class="badge bg-label-secondary">${escapeHtml(t('No', 'No'))}</span>`);

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

    // =====================================================================
    // DataTable + Save View state
    // =====================================================================
    let escDt = null;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-workflow-escalations');
    const dtL = () => window.L10n || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6];
    const totalColumnCount = 7;
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
            const views = await personalizationClient.getViews('Platform', 'WorkflowEscalations');
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[WorkflowEscalations SaveView] Failed to load saved views.', error);
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
            pageKey: 'WorkflowEscalations',
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
    // Inline filter (Status = Action: Escalate / Timeout)
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
            const row = rowData || escDt?.row(dataIndex)?.data?.() || null;
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
    // Run Escalations offcanvas + result rendering
    // =====================================================================
    const setBoxError = (id, msg) => { const b = el(id); if (b) { b.textContent = msg; show(b); } };
    const clearBox = (id) => hide(el(id));
    const setStatus = (kind, message) => {
        const box = el('wf-esc-status');
        if (!box) return;
        box.className = `alert alert-${kind === 'error' ? 'danger' : (kind === 'warning' ? 'warning' : 'success')}`;
        box.textContent = message;
        box.classList.remove('d-none');
    };

    const openRun = () => {
        clearBox('wf-esc-error');
        el('wf-esc-form').reset();
        el('wf-esc-idem').value = api.newGuid();
        bootstrap.Offcanvas.getOrCreateInstance(el('wf-esc-offcanvas')).show();
    };

    const summaryCard = (value, labelKey, labelFallback, tone) => `
        <div class="col"><div class="card border shadow-none"><div class="card-body text-center py-3">
            <div class="h4 mb-0 ${tone}">${escapeHtml(value ?? 0)}</div>
            <small class="text-muted">${escapeHtml(t(labelKey, labelFallback))}</small>
        </div></div></div>`;

    const renderSummary = (d) => {
        const box = el('wf-esc-summary');
        if (!box) return;
        box.innerHTML =
            summaryCard(d.evaluatedCount, 'EvaluatedCount', 'Evaluated', '') +
            summaryCard(d.escalatedCount, 'EscalatedCount', 'Escalated', 'text-warning') +
            summaryCard(d.timedOutCount, 'TimedOutCount', 'Timed Out', 'text-danger') +
            summaryCard(d.skippedCount, 'SkippedCount', 'Skipped', 'text-muted');
        show(box);
    };

    const runEscalations = async () => {
        clearBox('wf-esc-error');
        const now = val('wf-esc-now');
        const maxRaw = val('wf-esc-max');
        const payload = {
            nowUtc: now ? new Date(now).toISOString() : null,
            maxItems: maxRaw ? Number(maxRaw) : null,
            idempotencyKey: val('wf-esc-idem') || null
        };
        const btn = el('wf-esc-run');
        if (btn) btn.disabled = true;
        const res = await api.runEscalations(payload);
        if (btn) btn.disabled = false;
        if (!res.ok) { setBoxError('wf-esc-error', failureMessage(res)); return; }

        bootstrap.Offcanvas.getInstance(el('wf-esc-offcanvas'))?.hide();
        const d = res.data || {};
        renderSummary(d);

        const results = (Array.isArray(d.results) ? d.results : []).map((r) => Object.assign({}, r, { status: r.action }));
        if (escDt) {
            escDt.clear();
            escDt.rows.add(results);
            escDt.draw(false);
        }
        populateStatusOptions(results);
        setStatus('success', t('EscalationRunComplete', 'Escalation run complete.'));
        notify('success', t('EscalationRunComplete', 'Escalation run complete.'));
    };

    // =====================================================================
    // DataTable (client-side; rows are populated from each run)
    // =====================================================================
    const initDataTable = async () => {
        if (!dtTableEl || escDt) return;
        if (!window.DataTable || !window.DtDefaults) {
            console.error('[WorkflowEscalations] DataTable shared helpers are required.');
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
                    const targetApi = tableApi || escDt;
                    if (!targetApi) return;
                    try {
                        await saveDefaultView(getCurrentView(targetApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(l10n.RecordSaved || l10n.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[WorkflowEscalations SaveView] Failed to save default view.', error);
                        window.showToast?.(l10n.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        escDt = new DataTable(dtTableEl, window.DtDefaults.create({
            data: [],
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            order: baseOrder,
            columns: [
                { data: 'workflowInstanceId', name: 'control' },
                { data: 'workflowInstanceId', name: 'workflowInstanceId' },
                { data: 'approvalTaskId', name: 'approvalTaskId' },
                { data: 'action', name: 'action' },
                { data: 'previousTaskStatus', name: 'transition' },
                { data: 'reasonCode', name: 'reasonCode' },
                { data: 'isIdempotent', name: 'isIdempotent', className: 'text-center' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, className: 'fw-medium', render: (data, type) => type === 'display' ? shortId(data) : (data || '') },
                { targets: 2, render: (data, type) => type === 'display' ? shortId(data) : (data || '') },
                { targets: 3, render: (data) => escapeHtml(data || '—') },
                {
                    targets: 4,
                    render: (data, type, row) => type === 'display'
                        ? `${statusBadge(row.previousTaskStatus)} <i class="bx bx-right-arrow-alt"></i> ${statusBadge(row.newTaskStatus)}`
                        : (data || '')
                },
                { targets: 5, render: (data) => escapeHtml(data || '—') },
                { targets: 6, className: 'text-center', render: (data, type) => type === 'display' ? yesNo(!!data) : (data ? '1' : '0') }
            ],
            buttons: window.DtDefaults.exportButtons(
                t('RunEscalations', 'Run Escalations'),
                {},
                extraButtons,
                { exportColumns: [1, 2, 3, 4, 5, 6], colvisColumns: [1, 2, 3, 4, 5, 6] }
            ),
            initComplete: function () {
                const tableApi = this.api();
                mountInlineFilter();
                bindInlineFilterA11y();
                setupFilters(tableApi);
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    openRun();
                });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount());
            }
        }));

        escDt.on('column-visibility.dt', function () {
            window.DtDefaults?.updateVisualState?.(escDt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(escDt));
        });
        escDt.on('search.dt order.dt column-reorder.dt columns-reordered.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(escDt));
        });
    };

    document.addEventListener('DOMContentLoaded', () => {
        initDataTable();
        el('wf-esc-run')?.addEventListener('click', runEscalations);
        el('wf-esc-regen')?.addEventListener('click', () => { el('wf-esc-idem').value = api.newGuid(); });
    });
})();
