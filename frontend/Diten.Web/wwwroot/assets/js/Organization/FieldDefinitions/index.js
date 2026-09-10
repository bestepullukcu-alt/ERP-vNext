'use strict';

/*
 * MOD-0288-FU04 — the Organization field-definition list.
 *
 * Imitates `Tasks/FieldDefinitions/index.js` and shares no file with it (§2): the two screens configure
 * different modules and the first divergent requirement would otherwise break both.
 *
 * ── THREE RULES THAT MUST NOT BE COMMENTS ────────────────────────────────────────────────────────────────
 * MEASURED (Control Tower, MOD-0288-FU03): a forbidden fallback was pasted into that pack's form.js by hand
 * and 183 tests ran GREEN. The rule was written as a careful comment directly above the line it protected,
 * and a comment stops nobody. So the three rules below are PURE FUNCTIONS, exported at the bottom of this
 * file, and `tests/organization-field-definition-authoring.test.js` calls THESE — not a copy of them:
 *
 *   1. `Code` is read-only on edit          → guarded in _Form.cshtml + the view model (see that test file)
 *   2. at 50 active definitions the create  → `createGate()`
 *      button closes, it does not fail on save
 *   3. `read` alone yields a fully          → `surfacePermissions()` / `rowActionsFor()`
 *      read-only surface
 *
 * A test that re-implements the rule proves only that the copy works. These are the shipping functions.
 */
const OrganizationFieldDefinitionsList = (function () {
    // ── Rules. Pure, dependency-free, and exported. ──────────────────────────────────────────────────────

    /** FU02 `OrganizationFieldDefinitionRules.MaxActiveDefinitions`. Mirrored, not re-decided. */
    const MAX_ACTIVE_DEFINITIONS = 50;

    const PERMISSION_READ = 'platform.organization-units.custom-fields.read';
    const PERMISSION_MANAGE = 'platform.organization-units.custom-fields.manage';

    const isActiveRow = (row) => Boolean(row && (row.isActive ?? row.IsActive));

    /**
     * Whether the create path is open, and what is left.
     *
     * ⚠ THE GATE CLOSES BEFORE THE FORM, NOT ON SAVE. The server answers 409 at the limit, and letting the
     * user fill in nine fields to be told that afterwards is the difference between a rule and an ambush.
     * `remaining` is what the screen prints, so the number the user sees and the number that gates the button
     * cannot drift apart.
     */
    const createGate = (definitions, permissionKeys) => {
        const active = (definitions || []).filter(isActiveRow).length;
        const remaining = Math.max(0, MAX_ACTIVE_DEFINITIONS - active);
        if (!surfacePermissions(permissionKeys).canManage) {
            return { allowed: false, reason: 'permission', remaining };
        }
        return remaining === 0
            ? { allowed: false, reason: 'limit', remaining: 0 }
            : { allowed: true, reason: null, remaining };
    };

    /**
     * What this actor's surface is.
     *
     * ⚠ `read` IS NOT A WEAKER `manage` (§5). Seeing which fields exist is not permission to change the
     * tenant's data model, so a reader gets no create button, no row action that writes and no editable
     * control — not a disabled one, and not one that fails on submit.
     */
    const surfacePermissions = (permissionKeys) => {
        const keys = Array.isArray(permissionKeys) ? permissionKeys.map((k) => String(k).toLowerCase()) : [];
        const has = (key) => keys.indexOf(key) !== -1;
        return { canRead: has(PERMISSION_READ), canManage: has(PERMISSION_MANAGE) };
    };

    /**
     * The row actions this actor may see. Details is a read; Edit and Deactivate are writes.
     * Deactivation is offered only while the definition is active — FU02 has no re-activate route, so an
     * inactive row has no lifecycle action at all rather than one that answers 409.
     */
    const rowActionsFor = (row, permissionKeys) => {
        const actions = ['details'];
        if (!surfacePermissions(permissionKeys).canManage) {
            return actions;
        }
        actions.push('edit');
        if (isActiveRow(row)) {
            actions.push('deactivate');
        }
        return actions;
    };

    /**
     * A stored enum rendered through the label map, matched case-insensitively.
     *
     * ⚠ NOT RECONSTRUCTED FROM THE STRING. FU03 shipped a `titleCase()` that turned `GroupFunction` into
     * "Groupfunction", matched no option, and let the next save write `Department`. Two of the eight types
     * here — `MultilineText`, `SingleSelect` — are two words, so the same mistake is twice as easy. The
     * precedent's own `o.value === previous` is an exact match and is deliberately NOT copied.
     */
    const labelForEnum = (map, raw) => {
        const value = String(raw ?? '').trim();
        if (!value.length) return '';
        const found = Object.keys(map || {}).find((k) => k.toLowerCase() === value.toLowerCase());
        return found ? map[found] : value;
    };

    // ── Module state ─────────────────────────────────────────────────────────────────────────────────────
    let dt;
    let L = {};
    const dtTableEl = typeof document !== 'undefined'
        ? document.querySelector('.datatables-organizationfielddefinitions')
        : null;
    const endpoint = '/Organization/FieldDefinitions/api';
    const createUrl = '/Organization/FieldDefinitions/Create';
    const editUrl = '/Organization/FieldDefinitions/Edit';
    const detailsUrl = '/Organization/FieldDefinitions/Details';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8];
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 7, 8];
    const totalColumnCount = 10; // control(0) + select(1) + code..status(2-8) + action(9)
    const baseOrder = [[2, 'asc']];
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = typeof window !== 'undefined' ? window.personalizationClient : null;
    const personalizationContext = { moduleKey: 'Organization', pageKey: 'OrganizationFieldDefinitions' };
    let appliedFilters = { status: '', dataType: '', classification: '' };
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    let definitionsData = [];

    const permissionKeys = () => (typeof window !== 'undefined' && Array.isArray(window.__permissionSnapshot))
        ? window.__permissionSnapshot
        : [];

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));

    // Same-origin proxy; the token stays server-side in the HttpOnly cookie and never reaches this file.
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const loadL10n = () => { L = (typeof window !== 'undefined' && window.L10n) ? window.L10n : {}; };

    const typeLabels = () => ({
        Text: L.FieldTypeText, MultilineText: L.FieldTypeMultilineText, Integer: L.FieldTypeInteger,
        Decimal: L.FieldTypeDecimal, Boolean: L.FieldTypeBoolean, Date: L.FieldTypeDate,
        SingleSelect: L.FieldTypeSingleSelect, Reference: L.FieldTypeReference
    });
    const classificationLabels = () => ({
        Normal: L.ClassificationNormal, Internal: L.ClassificationInternal,
        Confidential: L.ClassificationConfidential, Restricted: L.ClassificationRestricted
    });

    const statusBadge = (active) => active
        ? `<span class="badge bg-label-success">${escapeHtml(L.Active || '')}</span>`
        : `<span class="badge bg-label-secondary">${escapeHtml(L.Passive || '')}</span>`;
    const yesNo = (value) => value
        ? `<span class="badge bg-label-primary">${escapeHtml(L.Required || '')}</span>`
        : '<span class="text-muted">-</span>';

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };

    const fetchDefinitions = () => fetch(endpoint, { headers: getAuthHeaders() })
        .then((response) => response.ok ? response.json() : Promise.reject(response))
        .then(unwrapList);

    const getAntiForgeryToken = () =>
        document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue, bulkOptions);
    };

    // ── Row lifecycle ────────────────────────────────────────────────────────────────────────────────────
    const deactivate = ({ row, id }) => {
        const rowId = id || row?.id || row?.Id;
        if (!rowId) return;
        // Belt and braces: the action is not rendered without `manage`, and it refuses to fire without it.
        if (!surfacePermissions(permissionKeys()).canManage) return;
        const entityName = row?.name || row?.Name || row?.code || row?.Code || '';
        const version = row?.version ?? row?.Version ?? 0;
        window.showConfirm?.(L.DeactivateConfirm || L.AreYouSure, async () => {
            try {
                const response = await fetch(
                    `${endpoint}/${encodeURIComponent(rowId)}/deactivate?expectedVersion=${encodeURIComponent(version)}`,
                    { method: 'POST', headers: getAuthHeaders() });
                if (!response.ok) throw new Error('Deactivate failed.');
                reloadWithSuccessToast('RecordDeactivated', entityName);
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        }, { entityName, type: 'warning', confirmButtonText: L.Deactivate });
    };

    const rowActionHandlers = {
        details: ({ id, row }) => {
            const rid = id || row?.id || row?.Id;
            if (rid) window.location.href = `${detailsUrl}/${encodeURIComponent(rid)}`;
        },
        edit: ({ id, row }) => {
            const rid = id || row?.id || row?.Id;
            if (rid) window.location.href = `${editUrl}/${encodeURIComponent(rid)}`;
        },
        deactivate
    };

    // ── Client-side filtering ────────────────────────────────────────────────────────────────────────────
    const applyClientFilter = (rows) => {
        let result = rows || [];
        if (appliedFilters.status) {
            const wantActive = appliedFilters.status === 'active';
            result = result.filter((r) => isActiveRow(r) === wantActive);
        }
        if (appliedFilters.dataType) {
            result = result.filter((r) => String(r.dataType ?? r.DataType ?? '').toLowerCase() === appliedFilters.dataType.toLowerCase());
        }
        if (appliedFilters.classification) {
            result = result.filter((r) => String(r.classification ?? r.Classification ?? '').toLowerCase() === appliedFilters.classification.toLowerCase());
        }
        return result;
    };

    // ── Save View: normalization + state capture / (de)serialization ─────────────────────────────────────
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v).trim()));
    const emptyFilters = () => ({ status: '', dataType: '', classification: '' });
    const normalizeFilters = (f) => ({
        status: normalizeString((f || {}).status),
        dataType: normalizeString((f || {}).dataType),
        classification: normalizeString((f || {}).classification)
    });

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
    const captureColVis = (api) => { const r = {}; saveViewColumnIndexes.forEach((ci) => { try { r[ci] = !!api.column(ci).visible(); } catch (e) { } }); return r; };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = defaultVisibleColumnIndexes.includes(ci); return a; }, {});
    const applyColVis = (api, colVis) => {
        const n = normalizeColVis(colVis);
        if (!n) return;
        saveViewColumnIndexes.forEach((ci) => { if (typeof n[ci] === 'boolean') { try { api.column(ci).visible(n[ci], false); } catch (e) { } } });
    };
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
        return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
    };
    const captureColOrder = (api) => { try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (e) { return null; } };
    const applyColOrder = (api, order) => { const n = normalizeColOrder(order); if (n && typeof api?.colReorder?.order === 'function') api.colReorder.order(n, true); };
    const identityColOrder = () => Array.from({ length: totalColumnCount }, (_, i) => i);

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
        filters: normalizeFilters(v?.filters),
        search: normalizeString(v?.search),
        colVis: normalizeColVis(v?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(v?.columnOrder) || identityColOrder(),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });
    const normalizeViewState = (v) => JSON.parse(serializeView(v));
    const getResetBaselineState = () => ({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
        order: baseOrder
    });

    const isSavedViewDefault = (sv) => Boolean(sv?.isDefault ?? sv?.IsDefault);
    const getSavedViewId = (sv) => sv?.id ?? sv?.Id ?? null;
    const getSavedViewName = (sv) => sv?.viewName ?? sv?.ViewName ?? '';
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
    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || getResetBaselineState();
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };
    const loadDefaultView = async () => {
        defaultViewRecord = null; defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[OrganizationFieldDefinitions SaveView] Failed to load saved views.', error);
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
        const savedResponse = existingId ? await personalizationClient.updateView(existingId, payload) : await personalizationClient.saveView(payload);
        const savedRecord = unwrapViewResponse(savedResponse);
        defaultViewRecord = savedRecord && typeof savedRecord === 'object' ? savedRecord : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };

    const setFilterValue = (id, value) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.value = value || '';
        if (window.jQuery?.fn?.select2) window.jQuery(el).val(value || '').trigger('change');
    };
    const syncFilterControls = (values) => {
        setFilterValue('filterStatus', values.status);
        setFilterValue('filterDataType', values.dataType);
        setFilterValue('filterClassification', values.classification);
    };
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
        dt?.ajax.reload(() => { window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount()); }, false);
    };

    const initSelect2Filters = () => {
        if (!window.jQuery?.fn?.select2) return;
        window.jQuery('#inlineFilterHost select.select2').each(function () {
            const $select = window.jQuery(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: window.jQuery(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                minimumResultsForSearch: Infinity,
                selectionCssClass: 'form-select form-select-sm',
                width: 'element'
            });
            // ⚠ NO change handler here. A filter the user is still choosing is not an applied filter, and
            // offering "save this view" while they scroll a dropdown asks them to save a state that is not on
            // screen yet. Save View is recalculated on Apply and on Reset, and nowhere else.
        });
    };

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter((value) => value !== '').length;

    const setupFilters = (api) => {
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: document.getElementById('filterStatus')?.value || '',
                dataType: document.getElementById('filterDataType')?.value || '',
                classification: document.getElementById('filterClassification')?.value || ''
            };
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
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

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById(filterCollapseId);
        if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;

        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-3');
        }
    };

    // ── The create gate on screen ────────────────────────────────────────────────────────────────────────
    /**
     * Apply `createGate` to the toolbar button. ⚠ Removed, not disabled-and-hopeful: at the limit the button
     * goes away and the remaining-capacity line says why, so nobody opens a form that cannot save.
     */
    const applyCreateGate = () => {
        const gate = createGate(definitionsData, permissionKeys());
        document.querySelectorAll('.add-new').forEach((btn) => {
            btn.classList.toggle('d-none', !gate.allowed);
        });
        const note = document.getElementById('fieldDefinitionCapacity');
        if (!note) return;
        if (gate.reason === 'limit') {
            note.textContent = L.DefinitionLimitReached || '';
            note.classList.remove('d-none');
        } else if (gate.allowed) {
            note.textContent = String(L.RemainingCapacity || '').replace('{0}', String(gate.remaining));
            note.classList.remove('d-none');
        } else {
            note.classList.add('d-none');
        }
    };

    // ── Quick view ───────────────────────────────────────────────────────────────────────────────────────
    const quickView = (row) => {
        if (!row) return;
        const lines = [
            `${escapeHtml(L.Code || '')}: ${escapeHtml(row.code || row.Code || '')}`,
            `${escapeHtml(L.Name || '')}: ${escapeHtml(row.name || row.Name || '')}`,
            `${escapeHtml(L.DataType || '')}: ${escapeHtml(labelForEnum(typeLabels(), row.dataType || row.DataType))}`,
            `${escapeHtml(L.Classification || '')}: ${escapeHtml(labelForEnum(classificationLabels(), row.classification || row.Classification))}`
        ];
        if (!isActiveRow(row)) lines.push(escapeHtml(L.InactiveDefinitionHelp || ''));
        window.showToast?.(lines.join(' · '), 'info');
    };

    const bindQuickView = () => {
        document.addEventListener('click', (event) => {
            const trigger = event.target.closest('.js-quick-view');
            if (!trigger) return;
            event.preventDefault();
            try { quickView(JSON.parse(trigger.getAttribute('data-json') || '{}')); }
            catch (error) { console.error('[OrganizationFieldDefinitions] Quick view payload could not be parsed.', error); }
        });
    };

    // ── Bulk deactivate ──────────────────────────────────────────────────────────────────────────────────
    /*
     * ⚠ THE SHARED SELECTION CONTRACT, NOT A SECOND ONE. `DitenDataTable.bindBulkSelection` reads checkbox
     * inputs (`.dt-checkboxes`) and the bar rendered by `_BulkActionBar.cshtml` (#bulkActionBar,
     * #bulkSelectedCount, #btnClearSelection). Measured on the live page: an earlier version of this file used
     * DataTables' own `select` API with `[data-bulk-bar]`, and NOTHING matched — the bar never appeared and the
     * action never fired, silently. Two selection mechanisms on one table is one too many.
     */
    const getSelectedIds = (ids) => (ids || [])
        .map((id) => {
            const row = definitionsData.find((d) => String(d.id ?? d.Id) === String(id));
            return row ? { id, expectedVersion: row.version ?? row.Version ?? 0 } : null;
        })
        .filter(Boolean);

    const bulkDeactivate = async (ids) => {
        if (!surfacePermissions(permissionKeys()).canManage) return;
        const items = getSelectedIds(ids);
        if (!items.length) return;
        window.showConfirm?.(L.BulkDeactivateConfirm || L.AreYouSure, async () => {
            try {
                const response = await fetch(`${endpoint}/bulk`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() },
                    body: JSON.stringify({ items })
                });
                if (!response.ok) throw new Error('Bulk deactivate failed.');
                reloadWithSuccessToast('BulkDeactivateSuccess');
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        }, { type: 'warning', confirmButtonText: L.BulkDeactivate });
    };

    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        bulkActionSelector: '[data-bulk-action]',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all',
        onBulkAction: { deactivate: ({ ids }) => bulkDeactivate(ids) }
    };

    // ── Table ────────────────────────────────────────────────────────────────────────────────────────────
    const initDataTable = () => {
        if (!dtTableEl || !window.DtDefaults) return;
        const canManage = surfacePermissions(permissionKeys()).canManage;

        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
            action: () => toggleInlineFilter()
        };
        const saveFilterBtn = {
            text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
            className: 'btn btn-label-secondary dt-save-filter-btn d-none',
            attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
            action: async () => {
                try {
                    await saveDefaultView(getCurrentView(dt));
                    setSaveFilterVisible(false);
                    window.showToast?.(L.RecordSaved || L.SaveView || '', 'success');
                } catch (error) {
                    console.error('[OrganizationFieldDefinitions SaveView] Save failed.', error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }
        };

        const dtConfig = window.DtDefaults.create({
            serverSide: false,
            stateSave: false,
            order: baseOrder,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: function (data, callback) {
                fetchDefinitions()
                    .then((rows) => {
                        definitionsData = rows || [];
                        applyCreateGate();
                        callback({ data: applyClientFilter(definitionsData) });
                    })
                    .catch(() => {
                        window.showToast?.(L.LoadFailed || L.ErrorOccurred || '', 'error');
                        callback({ data: [] });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                {
                    data: 'id', name: 'select',
                    render: (value) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${escapeHtml(value)}">`
                },
                { data: 'code', name: 'code', render: (value) => `<span class="fw-medium font-monospace text-primary">${escapeHtml(value)}</span>` },
                { data: 'name', name: 'name', render: escapeHtml },
                {
                    data: 'dataType', name: 'dataType',
                    render: (value) => escapeHtml(labelForEnum(typeLabels(), value))
                },
                { data: 'isRequired', name: 'isRequired', render: (value) => yesNo(value) },
                { data: 'isQueryable', name: 'isQueryable', render: (value) => yesNo(value) },
                {
                    data: 'classification', name: 'classification',
                    render: (value) => escapeHtml(labelForEnum(classificationLabels(), value))
                },
                { data: 'isActive', name: 'isActive', render: (value) => statusBadge(Boolean(value)) },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (value, type, row) => {
                        const id = row.id || row.Id;
                        const rowJson = JSON.stringify(row);
                        const allowed = rowActionsFor(row, permissionKeys());
                        const catalogue = {
                            details: { key: 'details', icon: 'bx bx-show', className: 'js-quick-view', text: L.ViewDetails || '', attrs: { 'data-id': id, 'data-json': rowJson } },
                            edit: { key: 'edit', icon: 'bx bx-edit', className: 'js-edit-item', text: L.Edit || '', attrs: { 'data-id': id, 'data-json': rowJson } },
                            deactivate: { key: 'deactivate', icon: 'bx bx-block', className: 'text-warning', text: L.Deactivate || '', attrs: { 'data-id': id, 'data-json': rowJson } }
                        };
                        const actions = allowed.map((key) => catalogue[key]).filter(Boolean);
                        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
                    }
                }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, className: 'dt-checkboxes-cell', searchable: false, orderable: false },
                { targets: 3, responsivePriority: 1 },
                { targets: 6, visible: false },
                { targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            // ⚠ The create button is offered here and then GATED by applyCreateGate() on every load: the
            // toolbar is built once, the limit and the permission are facts about the data and the actor.
            buttons: window.DtDefaults.exportButtons(canManage ? (L.AddNew || '') : '', {}, { filterBtn, saveFilterBtn }, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                setupFilters(api);
                applyCreateGate();
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        });

        if (L.Showing) {
            dtConfig.language = dtConfig.language || {};
            dtConfig.language.info = `${L.Showing} _START_ - _END_ / _TOTAL_`;
        }

        dt = new DataTable(dtTableEl, dtConfig);

        window.DitenDataTable?.bindActionDispatcher?.({
            tableEl: dtTableEl,
            dt,
            onRowAction: rowActionHandlers
        });

        window.DitenDataTable?.bindBulkSelection?.(dtTableEl, dt, bulkOptions);

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

    const bindAddNew = () => {
        document.addEventListener('click', (event) => {
            if (!event.target.closest('.add-new')) return;
            event.preventDefault();
            // The gate is re-asked at click time, not trusted from render time: the list may have reloaded.
            if (!createGate(definitionsData, permissionKeys()).allowed) return;
            window.location.href = createUrl;
        });
    };

    const boot = async () => {
        loadL10n();
        if (!dtTableEl) return;
        bindQuickView();
        bindAddNew();
        await loadDefaultView();
        initDataTable();
    };

    if (typeof document !== 'undefined' && dtTableEl) {
        if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
        else boot();
    }

    return {
        MAX_ACTIVE_DEFINITIONS,
        PERMISSION_READ,
        PERMISSION_MANAGE,
        createGate,
        surfacePermissions,
        rowActionsFor,
        labelForEnum
    };
})();

/*
 * The rules above are the ones the tests call. Exported for vitest under Node; harmless in the browser,
 * where `module` is undefined and the IIFE result is already on `OrganizationFieldDefinitionsList`.
 */
if (typeof module !== 'undefined' && module.exports) {
    module.exports = OrganizationFieldDefinitionsList;
}
