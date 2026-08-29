/**
 * MOD-0151 FU02 — Territory Models list (Golden Reference "slim" DataTable).
 *   - List: client-side DataTable fed from the paged Gateway endpoint (/api/crm/territory-models).
 *   - Create / Edit → offcanvas panel (AJAX submit, no page navigation) — the "slim" (<=8 field) pattern.
 *   - View → navigates to the model detail / hierarchy page.
 *   - No bulk / delete surface: FU02 backend exposes only model+node draft management.
 *   - Save View: personalizationClient via /api/personalization/*  |  L10n: window.L10n (PascalCase).
 * All API traffic goes through the Gateway; the CRM service is never called directly.
 */
'use strict';

const TerritoryModelsList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-territorymodels');
    const apiUrl = window.API?.crm ?? window.ApiBaseUrl;
    const canManage = window.TerritoryCanManage === true;
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'TerritoryModels' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8];
    const totalColumnCount = 10;
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8];
    const baseOrder = [[1, 'asc']];
    let appliedFilters = { status: [], countryScope: [] };
    let L = window.L10n || {};

    // ─── Offcanvas state ────────────────────────────────────────────────────
    let editingId = null;

    const getOcCreateEditInstance = () => {
        const el = document.getElementById('offcanvasCreateEdit');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    const getOcDetailsInstance = () => {
        const element = document.getElementById('offcanvasDetailsPreview');
        return element ? bootstrap.Offcanvas.getOrCreateInstance(element) : null;
    };

    // ─── FU02A scope reference lookups (Country single + Business Unit multi) ──
    // Country + Business Unit options are loaded from MOD-0048 published-values via the controller — NO hardcoded
    // fallback. When a set is unpublished the field renders empty with a "reference data not ready" warning.
    let scopeLookupsPromise = null;
    let countryReady = false;
    let businessUnitReady = false;

    const fillSelectOptions = (selectEl, items, includeBlank) => {
        if (!selectEl) return;
        selectEl.innerHTML = '';
        if (includeBlank) {
            const blank = document.createElement('option');
            blank.value = '';
            blank.textContent = '';
            selectEl.appendChild(blank);
        }
        (Array.isArray(items) ? items : []).forEach((it) => {
            if (!it || !it.value) return;
            const opt = document.createElement('option');
            opt.value = it.value;
            opt.textContent = it.text || it.value;
            selectEl.appendChild(opt);
        });
    };

    const initOffcanvasSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const parent = $('#offcanvasCreateEdit');
        ['#tmCountryScope', '#tmBusinessUnits'].forEach((sel) => {
            const $el = $(sel);
            if (!$el.length) return;
            if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
            $el.select2({
                dropdownParent: parent,
                placeholder: $el.data('placeholder') || '',
                allowClear: !$el.prop('multiple'),
                width: '100%'
            });
        });
    };

    const loadScopeLookups = () => {
        if (scopeLookupsPromise) return scopeLookupsPromise;
        scopeLookupsPromise = (async () => {
            let data = { countries: [], businessUnits: [], countryReady: false, businessUnitReady: false };
            try {
                const res = await fetch('/CRM/TerritoryManagement/Models/lookups', {
                    credentials: 'same-origin',
                    headers: getAuthHeaders()
                });
                if (res.ok) data = await res.json();
            } catch (error) {
                console.error('[TerritoryModels] Scope lookups failed.', error);
            }
            countryReady = !!data.countryReady;
            businessUnitReady = !!data.businessUnitReady;

            fillSelectOptions(document.getElementById('tmCountryScope'), data.countries, true);
            fillSelectOptions(document.getElementById('tmBusinessUnits'), data.businessUnits, false);

            document.getElementById('tmCountryNotReady')?.classList.toggle('d-none', countryReady);
            document.getElementById('tmBusinessUnitNotReady')?.classList.toggle('d-none', businessUnitReady);
            document.getElementById('tmBusinessUnitHelp')?.classList.toggle('d-none', !businessUnitReady);

            const buSelect = document.getElementById('tmBusinessUnits');
            if (buSelect) buSelect.disabled = !businessUnitReady;

            initOffcanvasSelect2();
        })();
        return scopeLookupsPromise;
    };

    const setScopeValue = (selectId, value) => {
        const el = document.getElementById(selectId);
        if (!el) return;
        if (window.jQuery && $(el).hasClass('select2-hidden-accessible')) {
            $(el).val(Array.isArray(value) ? value : (value || '')).trigger('change');
            return;
        }
        if (Array.isArray(value)) {
            Array.from(el.options).forEach((o) => { o.selected = value.includes(o.value); });
        } else {
            el.value = value || '';
        }
    };

    // ─── L10n ───────────────────────────────────────────────────────────────
    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    // ─── Auth ────────────────────────────────────────────────────────────────
    const getAuthHeaders = (includeJson = false) =>
        window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const getAntiForgeryToken = () =>
        document.querySelector('#formTerritoryModel input[name="__RequestVerificationToken"]')?.value || '';

    // ─── Normalize helpers ───────────────────────────────────────────────────
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ status: [], countryScope: [] });
    const normalizeFilters = (filters) => ({
        status: normalizeArray((filters || {}).status),
        countryScope: normalizeArray((filters || {}).countryScope)
    });
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(normalizeString(actual));
    };

    // ─── Column visibility / order helpers ──────────────────────────────────
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
        if (!n || typeof api?.colReorder?.order !== 'function') return;
        api.colReorder.order(n, true);
    };
    const applyColVis = (api, colVis) => {
        const n = normalizeColVis(colVis);
        if (!n) return;
        saveViewColumnIndexes.forEach((ci) => { if (typeof n[ci] === 'boolean') { try { api.column(ci).visible(n[ci], false); } catch (e) { } } });
    };

    // ─── Search input ────────────────────────────────────────────────────────
    const getSearchVal = (api) => { try { return api.table().container().querySelector('.dt-search input')?.value || ''; } catch (e) { return ''; } };
    const syncSearchInput = (api, v) => { try { const el = api.table().container().querySelector('.dt-search input'); if (el) el.value = v || ''; } catch (e) { } };

    // ─── View state ──────────────────────────────────────────────────────────
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
            console.error('[TerritoryModels SaveView] Failed to load saved views.', error);
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

    // ─── Inline filter UI ────────────────────────────────────────────────────
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
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.tmFilterBound === '1') return;
        dtTableEl.dataset.tmFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesMultiFilter(appliedFilters.status, row.status)
                && matchesMultiFilter(appliedFilters.countryScope, row.countryScope);
        });
    };

    // ─── Select2 multi-summary ───────────────────────────────────────────────
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
        $('#filterStatus, #filterCountryScope').each(function () {
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

    // Statuses are owned by the backend lifecycle; derive the chip options from the loaded rows.
    const populateStatusOptions = (api) => {
        const select = document.getElementById('filterStatus');
        if (!select) return;
        const seen = new Set();
        try {
            api.column(3).data().each((v) => { const s = normalizeString(v); if (s) seen.add(s); });
        } catch (e) { }
        const current = normalizeArray($('#filterStatus').val());
        select.innerHTML = '';
        Array.from(seen).sort((a, b) => a.localeCompare(b)).forEach((val) => {
            const opt = document.createElement('option');
            opt.value = val;
            opt.textContent = val.charAt(0).toUpperCase() + val.slice(1);
            select.appendChild(opt);
        });
        if (current.length) $('#filterStatus').val(current);
    };

    const populateCountryScopeOptions = (api) => {
        const select = document.getElementById('filterCountryScope');
        if (!select) return;
        const seen = new Set();
        try {
            api.rows().data().each((row) => {
                const value = normalizeString(row?.countryScope);
                if (value) seen.add(value);
            });
        } catch (e) { }
        const current = normalizeArray($('#filterCountryScope').val());
        select.innerHTML = '';
        Array.from(seen).sort((a, b) => a.localeCompare(b)).forEach((value) => {
            const option = document.createElement('option');
            option.value = value;
            option.textContent = value;
            select.appendChild(option);
        });
        if (current.length) $('#filterCountryScope').val(current);
    };

    const syncFilterControls = (values) => {
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterCountryScope').val(normalizeArray(values.countryScope)).trigger('change');
    };
    const getAppliedFilterCount = () => [appliedFilters.status, appliedFilters.countryScope].filter(hasFilterValue).length;

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

    const setupFilters = (api) => {
        populateStatusOptions(api);
        populateCountryScopeOptions(api);
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterStatus').val() || [],
                countryScope: $('#filterCountryScope').val() || []
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

    // ─── Presentation ─────────────────────────────────────────────────────────
    const statusBadgeClass = (status) => ({
        draft: 'bg-label-secondary',
        active: 'bg-label-success',
        expired: 'bg-label-danger',
        archived: 'bg-label-dark',
        superseded: 'bg-label-warning',
        retired: 'bg-label-dark',
        inactive: 'bg-label-secondary'
    })[normalizeString(status).toLowerCase()] || 'bg-label-primary';

    const executeLifecycleAction = (id, action, label, entityName) => {
        if (!id || !action) return;
        const typeByAction = {
            activate: 'success',
            deactivate: 'warning',
            archive: 'warning',
            'delete-draft': 'danger'
        };
        window.showConfirm?.(label || L.ConfirmLifecycleAction, async () => {
            try {
                const response = await fetch(`${apiUrl}/api/crm/territory-models/${encodeURIComponent(id)}/${action}`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: getAuthHeaders(true),
                    body: JSON.stringify({
                        reason: label || action,
                        correlationId: `ui-territory-life-${crypto.randomUUID?.() || Date.now()}`
                    })
                });
                if (!response.ok) {
                    let message = L.ErrorOccurred;
                    const payload = await response.json();
                    message = payload?.errors?.join(', ') || payload?.Errors?.join(', ') || message;
                    window.showToast?.(message, 'error');
                    return;
                }
                window.DitenDataTable.reloadWithToast(dt, dtTableEl, 'RecordUpdated');
            } catch (error) {
                console.error('[TerritoryModels] Lifecycle action failed.', error);
                window.showToast?.(L.ErrorOccurred, 'error');
            }
        }, {
            entityName: entityName || String(id),
            type: typeByAction[action] || 'info',
            subtext: L.LifecycleConfirmation,
            confirmButtonText: label || L.Confirm
        });
    };

    const formatDate = (value) => {
        if (!value) return '—';
        const s = String(value);
        return s.length >= 10 ? s.slice(0, 10) : s;
    };

    const getBusinessUnitScopeCodes = (row) => normalizeArray(
        (Array.isArray(row?.businessScopes) ? row.businessScopes : [])
            .filter((scope) => normalizeString(scope?.scopeType).toLowerCase() === 'business-unit')
            .map((scope) => scope?.scopeCode)
    );

    const escapeHtml = (value) => {
        const element = document.createElement('span');
        element.textContent = value ?? '';
        return element.innerHTML;
    };

    const renderScopeList = (values, type) => {
        const normalized = normalizeArray(values);
        const text = normalized.join(', ');
        if (type !== 'display') return text;
        return normalized.length
            ? normalized.map((value) => `<span class="badge bg-label-info me-1">${escapeHtml(value)}</span>`).join('')
            : '—';
    };

    const createDefaultModelCode = () => {
        const now = new Date();
        const pad = (value) => String(value).padStart(2, '0');
        return `TM-${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`;
    };

    const openDetailsOffcanvas = async (id) => {
        if (!id) return;
        try {
            const response = await fetch(`/CRM/TerritoryManagement/Models/${id}/Json`, {
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            const json = await response.json();
            if (!json.success || !json.data) throw new Error('Failed to load model details.');

            const data = json.data;
            document.getElementById('tm-details-title').textContent = data.name || '—';
            document.getElementById('tm-details-subtitle').textContent = data.modelCode || '—';
            document.getElementById('tm-details-code').textContent = data.modelCode || '—';
            document.getElementById('tm-details-version').textContent = data.versionNumber ?? '—';
            document.getElementById('tm-details-country').textContent = data.countryScope || '—';
            document.getElementById('tm-details-business-units').textContent =
                normalizeArray(data.businessUnitScopes).join(', ') || '—';
            document.getElementById('tm-details-period').textContent =
                `${data.effectiveFrom || '—'} / ${data.effectiveTo || '—'}`;
            document.getElementById('tm-details-change-reason').textContent = data.changeReason || '—';

            const status = document.getElementById('tm-details-status');
            status.textContent = data.status || '—';
            status.className = `badge ${statusBadgeClass(data.status)}`;

            const hierarchyButton = document.getElementById('tm-details-hierarchy');
            hierarchyButton.dataset.modelId = id;
            getOcDetailsInstance()?.show();
        } catch (error) {
            console.error('[TerritoryModels] Failed to load model details.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
        }
    };

    // ─── Create/Edit offcanvas ────────────────────────────────────────────────
    const resetCreateEditForm = () => {
        const form = document.getElementById('formTerritoryModel');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        document.getElementById('tmId').value = '';
        document.getElementById('tmBasedOnModelId').value = '';
        document.getElementById('tmModelCode').value = createDefaultModelCode();
        document.getElementById('tmModelCode').readOnly = false;
        document.getElementById('tmName').value = '';
        document.getElementById('tmEffectiveFrom').value = '';
        document.getElementById('tmEffectiveTo').value = '';
        document.getElementById('tmChangeReason').value = '';
        document.getElementById('formTerritoryModelAlert').classList.add('d-none');
    };

    const openCreateOffcanvas = async () => {
        editingId = null;
        resetCreateEditForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleCreate || L.AddNew || '';
        const saveBtn = document.getElementById('btnSaveModel');
        if (saveBtn) saveBtn.textContent = L.Save || '';

        await loadScopeLookups();
        setScopeValue('tmCountryScope', '');
        setScopeValue('tmBusinessUnits', []);

        getOcCreateEditInstance()?.show();
    };

    const openEditOffcanvas = async (id) => {
        if (!id) return;
        editingId = id;
        resetCreateEditForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleEdit || L.Edit || '';
        const saveBtn = document.getElementById('btnSaveModel');
        if (saveBtn) saveBtn.textContent = L.Update || L.Save || '';

        try {
            const res = await fetch(`/CRM/TerritoryManagement/Models/${id}/Json`, {
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            const json = await res.json();
            if (!json.success || !json.data) throw new Error('Failed to load model.');

            const d = json.data;
            document.getElementById('tmId').value = d.id || '';
            document.getElementById('tmModelCode').value = d.modelCode || '';
            document.getElementById('tmModelCode').readOnly = true; // code is immutable on edit
            document.getElementById('tmName').value = d.name || '';
            document.getElementById('tmEffectiveFrom').value = d.effectiveFrom || '';
            document.getElementById('tmEffectiveTo').value = d.effectiveTo || '';
            document.getElementById('tmChangeReason').value = d.changeReason || '';

            // Scope selectors: load reference options first, then apply the saved values.
            await loadScopeLookups();
            setScopeValue('tmCountryScope', d.countryScope || '');
            setScopeValue('tmBusinessUnits', Array.isArray(d.businessUnitScopes) ? d.businessUnitScopes : []);
        } catch (error) {
            console.error('[TerritoryModels] Failed to load model for edit.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
            return;
        }

        getOcCreateEditInstance()?.show();
    };

    const openCloneOffcanvas = async (id) => {
        if (!id) return;
        editingId = null;
        resetCreateEditForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.CreateDraftVersion || L.FormTitleCreate || '';
        const saveBtn = document.getElementById('btnSaveModel');
        if (saveBtn) saveBtn.textContent = L.Save || '';

        try {
            const res = await fetch(`/CRM/TerritoryManagement/Models/${id}/Json`, {
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            const json = await res.json();
            if (!json.success || !json.data) throw new Error('Failed to load source model.');
            const d = json.data;
            document.getElementById('tmBasedOnModelId').value = id;
            document.getElementById('tmName').value = `${d.name || d.modelCode || ''} v${(Number(d.versionNumber) || 1) + 1}`.trim();
            document.getElementById('tmEffectiveFrom').value = new Date().toISOString().slice(0, 10);
            document.getElementById('tmEffectiveTo').value = d.effectiveTo || '';
            document.getElementById('tmChangeReason').value = L.CreateDraftVersion || '';
            await loadScopeLookups();
            setScopeValue('tmCountryScope', d.countryScope || '');
            setScopeValue('tmBusinessUnits', Array.isArray(d.businessUnitScopes) ? d.businessUnitScopes : []);
            getOcCreateEditInstance()?.show();
        } catch (error) {
            console.error('[TerritoryModels] Failed to prepare draft version.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
        }
    };

    const showFormErrors = (errors) => {
        const alertEl = document.getElementById('formTerritoryModelAlert');
        if (!alertEl) return;
        alertEl.innerHTML = Array.isArray(errors) ? errors.map((e) => `<div>${e}</div>`).join('') : (errors || L.FormValidationError || '');
        alertEl.classList.remove('d-none');
    };

    const submitCreateEditForm = async () => {
        const form = document.getElementById('formTerritoryModel');
        if (!form) return;

        form.classList.add('was-validated');
        if (!form.checkValidity()) {
            showFormErrors([L.FormValidationError || '']);
            return;
        }

        const formData = new FormData(form);
        const isEdit = !!editingId;
        const url = isEdit
            ? `/CRM/TerritoryManagement/Models/${editingId}/EditJson`
            : '/CRM/TerritoryManagement/Models/CreateJson';

        const saveBtn = document.getElementById('btnSaveModel');
        if (saveBtn) saveBtn.disabled = true;

        try {
            const res = await fetch(url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken(),
                    ...getAuthHeaders()
                },
                body: formData
            });
            const json = await res.json();

            if (json.success) {
                getOcCreateEditInstance()?.hide();
                window.DitenDataTable.reloadWithToast(dt, dtTableEl, isEdit ? 'RecordUpdated' : 'RecordCreated');
            } else {
                showFormErrors(json.errors);
            }
        } catch (error) {
            console.error('[TerritoryModels] Form submit failed.', error);
            showFormErrors([L.ErrorOccurred]);
        } finally {
            if (saveBtn) saveBtn.disabled = false;
        }
    };

    // ─── Row actions (View / Edit) ─────────────────────────────────────────────
    const rowActionHandlers = {
        quickView: ({ id }) => {
            if (id) openDetailsOffcanvas(String(id));
        },
        hierarchy: ({ id }) => {
            if (id) window.location.href = `/CRM/TerritoryManagement/Models/${id}`;
        },
        assignmentRules: ({ id }) => {
            if (id) window.location.href = `/CRM/TerritoryManagement/Models/${id}/AssignmentRules`;
        },
        resourceAssignments: ({ id }) => {
            if (id) window.location.href = `/CRM/TerritoryManagement/Models/${id}/ResourceAssignments`;
        },
        edit: ({ id }) => {
            if (id) openEditOffcanvas(String(id));
        },
        cloneDraft: ({ id }) => {
            if (id) openCloneOffcanvas(String(id));
        },
        activate: ({ id, row }) => executeLifecycleAction(
            id, 'activate', L.Activate, [row?.modelCode, row?.name].filter(Boolean).join(' — ')
        ),
        deactivate: ({ id, row }) => executeLifecycleAction(
            id, 'deactivate', L.Deactivate, [row?.modelCode, row?.name].filter(Boolean).join(' — ')
        ),
        archive: ({ id, row }) => executeLifecycleAction(
            id, 'archive', L.Archive, [row?.modelCode, row?.name].filter(Boolean).join(' — ')
        ),
        deleteDraft: ({ id, row }) => executeLifecycleAction(
            id, 'delete-draft', L.DeleteDraft, [row?.modelCode, row?.name].filter(Boolean).join(' — ')
        )
    };

    // ─── DataTable init ───────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[TerritoryModels] window.API.crm (or window.ApiBaseUrl) is required.'); return; }

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
                        console.error('[TerritoryModels SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            actions: { onRowAction: rowActionHandlers },
            ajax: {
                // The CRM list endpoint caps pageSize at 200; larger values fall back to 25.
                // The grid filters client-side, so request the supported maximum and unwrap data.items.
                url: apiUrl + '/api/crm/territory-models?page=1&pageSize=200',
                type: 'GET',
                xhrFields: { withCredentials: true },
                dataSrc: (json) => json?.data?.items ?? json?.Data?.Items ?? []
            },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'modelCode', name: 'modelCode' },
                    { data: 'name', name: 'name' },
                    { data: 'status', name: 'status' },
                    { data: 'versionNumber', name: 'versionNumber' },
                    { data: 'countryScope', name: 'countryScope' },
                    {
                        data: null,
                        name: 'businessUnitScope',
                        render: (_data, type, row) => renderScopeList(getBusinessUnitScopeCodes(row), type)
                    },
                    { data: 'effectiveFrom', name: 'effectiveFrom' },
                    { data: 'effectiveTo', name: 'effectiveTo' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    {
                        targets: 3,
                        render: (data, type, row) => {
                            const val = row?.isExpired ? (row.computedStatus || 'expired') : (data ?? '');
                            if (type !== 'display') return val;
                            if (!val) return '';
                            return `<span class="badge ${statusBadgeClass(val)}">${val}</span>`;
                        }
                    },
                    { targets: 5, render: (data, type) => renderScopeList(data ? [data] : [], type) },
                    { targets: 7, render: (data, type) => type === 'display' ? formatDate(data) : (data ?? '') },
                    { targets: 8, render: (data, type) => type === 'display' ? formatDate(data) : (data ?? '') },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all text-end',
                        render: (data, type, full) => {
                            const actions = [
                                {
                                    key: 'quickView',
                                    className: 'js-quick-view me-1',
                                    icon: 'bx bx-show',
                                    attrs: { 'title': L.QuickView || L.ViewDetails }
                                },
                                {
                                    key: 'hierarchy',
                                    className: 'js-hierarchy me-1',
                                    icon: 'bx bx-sitemap',
                                    text: L.TerritoryHierarchy,
                                    attrs: { 'title': L.TerritoryHierarchy }
                                },
                                // Pack §18 surfaces #6 and #8 — their own screens, reachable per model.
                                // Read-only visibility is enough to open them; the pages gate editing themselves.
                                {
                                    key: 'assignmentRules',
                                    className: 'js-assignment-rules me-1',
                                    icon: 'bx bx-filter-alt',
                                    text: L.AssignmentRules,
                                    attrs: { 'title': L.AssignmentRules }
                                },
                                {
                                    key: 'resourceAssignments',
                                    className: 'js-resource-assignments me-1',
                                    icon: 'bx bx-group',
                                    text: L.ResourceAssignments,
                                    attrs: { 'title': L.ResourceAssignments }
                                }
                            ];
                            if (canManage) {
                                const stored = normalizeString(full.storedStatus || full.status).toLowerCase();
                                const expired = full.isExpired === true;
                                if (stored === 'draft' || stored === 'inactive') {
                                    actions.push({ key: 'activate', icon: 'bx bx-play-circle', text: L.Activate });
                                }
                                if (stored === 'active') {
                                    actions.push({ key: 'cloneDraft', icon: 'bx bx-copy', text: L.CreateDraftVersion });
                                    actions.push({ key: 'deactivate', icon: 'bx bx-pause-circle', text: L.Deactivate });
                                }
                                if (stored === 'inactive') {
                                    actions.push({ key: 'cloneDraft', icon: 'bx bx-copy', text: L.CreateDraftVersion });
                                }
                                if (stored === 'inactive' || expired) {
                                    actions.push({ key: 'archive', icon: 'bx bx-archive', text: L.Archive });
                                }
                                if (stored === 'draft') {
                                    actions.push({ key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L.Edit });
                                    actions.push({ key: 'deleteDraft', icon: 'bx bx-trash', text: L.DeleteDraft });
                                }
                            }
                            return window.DitenDataTable.renderActions(actions);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    canManage ? (L.AddNew || '') : null,
                    {},
                    extraButtons,
                    { exportColumns: [1, 2, 3, 4, 5, 6, 7, 8], colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupFilters(this.api());
                    if (canManage) {
                        document.querySelector('.add-new')?.addEventListener('click', (e) => {
                            e.preventDefault();
                            openCreateOffcanvas();
                        });
                    }
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

    const bindEvents = () => {
        document.getElementById('btnSaveModel')?.addEventListener('click', submitCreateEditForm);
        document.getElementById('tm-details-hierarchy')?.addEventListener('click', (event) => {
            const id = event.currentTarget.dataset.modelId;
            if (id) window.location.href = `/CRM/TerritoryManagement/Models/${id}`;
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

document.addEventListener('DOMContentLoaded', () => TerritoryModelsList.init());
