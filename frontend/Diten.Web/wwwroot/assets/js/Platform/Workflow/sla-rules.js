'use strict';

// MOD-0023 — Workflow SLA Rules list (golden compact DataTable v2) + golden slim create offcanvas.
// Standalone page: lists the SLA/escalation rules of one definition (server-filtered by templateId),
// with inline Status filter, Save View personalization, ColVis, ColReorder, and an "Add SLA Rule"
// toolbar button that opens a slim create offcanvas.
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

    const STATUS_TONE = { active: 'success', passive: 'secondary' };
    const statusBadge = (status) => {
        if (status === null || status === undefined || status === '') return '<span class="text-muted">—</span>';
        const tone = STATUS_TONE[String(status).toLowerCase().replace(/[^a-z]/g, '')] || 'secondary';
        return `<span class="badge bg-label-${tone}">${escapeHtml(status)}</span>`;
    };
    const num = (value) => (value === null || value === undefined || value === '' ? '<span class="text-muted">—</span>' : escapeHtml(value));

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
    const normalizeCsvPrincipals = (raw) => {
        const seen = new Set();
        const out = [];
        (raw || '').split(',').forEach((part) => {
            const v = part.trim();
            if (v && !seen.has(v)) { seen.add(v); out.push(v); }
        });
        return out;
    };

    // ---- principal lookup (Users/Positions) — for the Escalation Principal Ids picker -------------
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
    let slaPrincipalType = 'user';
    let slaPrincipalsInited = false;

    // Escalation principals — Users/Positions toggle + Select2 (mirrors the Visual Designer). The
    // toggle filters the dropdown; chosen chips of either kind persist. Backend resolves both prefixes.
    const initSlaPrincipals = async () => {
        if (slaPrincipalsInited) return;
        slaPrincipalsInited = true;
        const selectNode = el('wf-sla-principals');
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
                dropdownParent: $('#wf-sla-offcanvas'),
                placeholder: t('EscalationPrincipalIds', 'Escalation Principal IDs'),
                closeOnSelect: false,
                matcher: (params, data) => {
                    if (!data || !data.id) return data;
                    if (!String(data.id).startsWith(`${slaPrincipalType}:`)) return null;
                    const term = (params.term || '').trim().toLowerCase();
                    if (!term) return data;
                    return (data.text || '').toLowerCase().indexOf(term) > -1 ? data : null;
                }
            });
            $('input[name="wf-sla-principals-type"]').on('change', function () {
                slaPrincipalType = this.value === 'position' ? 'position' : 'user';
            });
        }
    };

    const readSlaPrincipals = () => {
        const node = el('wf-sla-principals');
        if (!node) return [];
        if (window.jQuery && window.jQuery.fn?.select2) return window.jQuery(node).val() || [];
        return Array.from(node.selectedOptions || []).map((o) => o.value).filter(Boolean);
    };

    // =====================================================================
    // DataTable + Save View state
    // =====================================================================
    let slaDt = null;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-workflow-slarules');
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
            const views = await personalizationClient.getViews('Platform', 'WorkflowSlaRules');
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[WorkflowSlaRules SaveView] Failed to load saved views.', error);
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
            pageKey: 'WorkflowSlaRules',
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
    // Inline filter (Status = Active / Passive)
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
            const row = rowData || slaDt?.row(dataIndex)?.data?.() || null;
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
    // Create offcanvas (New SLA Rule)
    // =====================================================================
    const setBoxError = (id, msg) => { const b = el(id); if (b) { b.textContent = msg; show(b); } };
    const clearBox = (id) => hide(el(id));

    const openCreate = () => {
        clearBox('wf-sla-form-error');
        el('wf-sla-form').reset();
        el('wf-sla-templateid').value = definitionId || '';
        initSlaPrincipals();
        slaPrincipalType = 'user';
        if (el('wf-sla-principals-type-user')) el('wf-sla-principals-type-user').checked = true;
        if (window.jQuery && el('wf-sla-principals')) window.jQuery('#wf-sla-principals').val(null).trigger('change');
        bootstrap.Offcanvas.getOrCreateInstance(el('wf-sla-offcanvas')).show();
    };

    const submitCreate = async () => {
        clearBox('wf-sla-form-error');
        const principals = readSlaPrincipals();
        const timeoutRaw = val('wf-sla-timeout');
        const due = Number(val('wf-sla-due'));
        const escalate = Number(val('wf-sla-escalate'));
        const timeout = timeoutRaw ? Number(timeoutRaw) : null;
        if (!val('wf-sla-templateid')) { setBoxError('wf-sla-form-error', t('TemplateRequired', 'TemplateId is required.')); return; }
        if (!val('wf-sla-stage') || !val('wf-sla-step')) { setBoxError('wf-sla-form-error', t('StageStepRequired', 'Stage Code and Step Code are required.')); return; }
        if (!(due > 0)) { setBoxError('wf-sla-form-error', t('DueMinutesPositive', 'Due In Minutes must be greater than 0.')); return; }
        if (!(escalate >= due)) { setBoxError('wf-sla-form-error', t('EscalateGteDue', 'Escalate After Minutes must be ≥ Due In Minutes.')); return; }
        if (timeout != null && !(timeout >= escalate)) { setBoxError('wf-sla-form-error', t('TimeoutGteEscalate', 'Timeout After Minutes must be ≥ Escalate After Minutes.')); return; }
        if (!principals.length) { setBoxError('wf-sla-form-error', t('EscalationPrincipalsRequired', 'Escalation Principal Ids are required.')); return; }

        const payload = {
            templateId: val('wf-sla-templateid'),
            stageCode: val('wf-sla-stage'),
            stepCode: val('wf-sla-step'),
            dueInMinutes: due,
            escalateAfterMinutes: escalate,
            timeoutAfterMinutes: timeout,
            escalationPrincipalIds: principals
        };
        const btn = el('wf-sla-submit');
        if (btn) btn.disabled = true;
        const res = await api.createSlaRule(payload);
        if (btn) btn.disabled = false;
        if (!res.ok) { setBoxError('wf-sla-form-error', failureMessage(res)); return; }
        bootstrap.Offcanvas.getInstance(el('wf-sla-offcanvas'))?.hide();
        el('wf-sla-form').reset();
        notify('success', t('SlaRuleCreated', 'SLA rule created.'));
        slaDt?.ajax.reload(null, false);
    };

    // =====================================================================
    // DataTable
    // =====================================================================
    const activeLabel = () => (window.L10n && window.L10n.Active) || 'Active';
    const passiveLabel = () => (window.L10n && window.L10n.Passive) || 'Passive';
    const slaAjaxDataSrc = (json) => {
        const payload = json?.data?.data || json?.data || json;
        const items = Array.isArray(payload) ? payload : (Array.isArray(payload?.items) ? payload.items : []);
        // Synthesize a Status label from IsActive so the standard status filter machinery works.
        items.forEach((rule) => { rule.status = rule.isActive ? activeLabel() : passiveLabel(); });
        populateStatusOptions(items);
        return items;
    };

    const initDataTable = async () => {
        if (!dtTableEl || slaDt) return;
        if (!window.DitenDataTable || !window.DtDefaults) {
            console.error('[WorkflowSlaRules] DataTable shared helpers are required.');
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
                    const targetApi = tableApi || slaDt;
                    if (!targetApi) return;
                    try {
                        await saveDefaultView(getCurrentView(targetApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(l10n.RecordSaved || l10n.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[WorkflowSlaRules SaveView] Failed to save default view.', error);
                        window.showToast?.(l10n.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        slaDt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: `/Platform/Workflow/api/sla-rules?templateId=${encodeURIComponent(definitionId)}`,
                type: 'GET',
                dataSrc: slaAjaxDataSrc
            },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                order: baseOrder,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'stageCode', name: 'stageCode' },
                    { data: 'dueInMinutes', name: 'dueInMinutes', className: 'text-center' },
                    { data: 'escalateAfterMinutes', name: 'escalateAfterMinutes', className: 'text-center' },
                    { data: 'timeoutAfterMinutes', name: 'timeoutAfterMinutes', className: 'text-center' },
                    { data: 'escalationPrincipalIds', name: 'escalationPrincipalIds' },
                    { data: 'status', name: 'status' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    {
                        targets: 1,
                        className: 'fw-medium',
                        render: (data, type, row) => type === 'display'
                            ? `${escapeHtml(row.stageCode || '—')} / ${escapeHtml(row.stepCode || '—')}`
                            : (data || '')
                    },
                    { targets: 2, className: 'text-center', render: (data, type) => type === 'display' ? num(data) : (data ?? '') },
                    { targets: 3, className: 'text-center', render: (data, type) => type === 'display' ? num(data) : (data ?? '') },
                    { targets: 4, className: 'text-center', render: (data, type) => type === 'display' ? num(data) : (data ?? '') },
                    { targets: 5, render: (data) => escapeHtml((Array.isArray(data) ? data : []).join(', ') || '—') },
                    {
                        targets: 6,
                        render: (data, type, row) => type === 'display'
                            ? `<span class="badge bg-label-${row.isActive ? 'success' : 'secondary'}">${escapeHtml(data || '')}</span>`
                            : (data || '')
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    t('CreateSlaRule', 'Add SLA Rule'),
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
                        openCreate();
                    });
                    setTimeout(() => { saveFilterArmed = true; }, 0);
                },
                drawCallback: function () {
                    window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount());
                }
            }
        });

        slaDt.on('column-visibility.dt', function () {
            window.DtDefaults?.updateVisualState?.(slaDt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(slaDt));
        });
        slaDt.on('search.dt order.dt column-reorder.dt columns-reordered.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(slaDt));
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
        el('wf-sla-submit')?.addEventListener('click', submitCreate);
    });
})();
