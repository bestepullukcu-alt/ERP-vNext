'use strict';

// MOD-0220 — Legal Entities (tenant shell, Master Data). Rich list + Quick View + inline filter + Save View.
// Ports the GoldenReferenceCompact reference: the DataTable is client-side (serverSide:false) and loads the full
// list once; enum filters are multi-select Select2 chips filtered in-browser (no backend multi-value change),
// date filters use flatpickr, and the toolbar carries a personalization-backed Save View button.
// Create/Edit live on the full-page wizard (/LegalEntities/Wizard); the list only navigates there.
const LegalEntitiesList = (function () {
    let dt;
    let L = {};
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-legal-entities');
    const endpoint = '/LegalEntities/api';
    const wizardUrl = '/LegalEntities/Wizard';
    const detailsUrl = '/LegalEntities/Details';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MasterData', pageKey: 'LegalEntities' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';

    const totalColumnCount = 11;                                   // control(0) + 9 data + action(10)
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9];
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9];
    const baseOrder = [[1, 'asc']];

    let rowsData = [];
    const entityMap = {}; // legalEntityId -> { legalName, code } for parent resolution
    const lookupMaps = { legalForm: {}, organizationRole: {}, country: {}, currency: {} };
    const datePickers = {};

    const emptyFilters = () => ({ country: [], operationalStatus: [], evidenceStatus: [], baseCurrency: [], createdFrom: '', createdTo: '', incompleteOnly: false });
    let appliedFilters = emptyFilters();

    const loadL10n = () => {
        const node = document.getElementById('legal-entities-l10n');
        if (!node) return;
        try {
            const raw = JSON.parse(node.textContent || '{}');
            const toPascal = (key) => key.charAt(0).toUpperCase() + key.slice(1);
            Object.keys(raw).forEach((key) => { L[toPascal(key)] = raw[key]; });
        } catch (error) {
            console.error('[LegalEntities] L10n payload could not be parsed.', error);
        }
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const operationalOf = (row) => String(row.operationalStatus ?? row.OperationalStatus ?? row.lifecycleState ?? row.LifecycleState ?? '').toUpperCase();

    // ─── Normalizers (shared with the Save View serialization) ───────────────
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v).trim()));
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const sortNormalizedArray = (v) => normalizeArray(v).slice().sort((a, b) => a.localeCompare(b));
    const normalizeBool = (v) => v === true || v === 'true';
    const normalizeFilterValue = (v) => Array.isArray(v) ? sortNormalizedArray(v) : (typeof v === 'boolean' ? v : normalizeString(v));
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : (typeof v === 'boolean' ? v : normalizeString(v).length > 0);
    const normalizeFilters = (f) => {
        const s = f || {};
        return {
            country: normalizeArray(s.country),
            operationalStatus: normalizeArray(s.operationalStatus),
            evidenceStatus: normalizeArray(s.evidenceStatus),
            baseCurrency: normalizeArray(s.baseCurrency),
            createdFrom: normalizeString(s.createdFrom),
            createdTo: normalizeString(s.createdTo),
            incompleteOnly: normalizeBool(s.incompleteOnly)
        };
    };

    // ─── Client-side filter matchers ─────────────────────────────────────────
    const matchesMulti = (selected, actual) => {
        const norm = normalizeArray(selected).map((s) => s.toUpperCase());
        return !norm.length || norm.includes(normalizeString(actual).toUpperCase());
    };
    const matchesDateRange = (from, to, value) => {
        if (!from && !to) return true;
        const d = normalizeString(value).slice(0, 10);
        if (!d) return false;
        if (from && d < from) return false;
        if (to && d > to) return false;
        return true;
    };
    const matchesIncomplete = (only, score) => !only || Number(score ?? 0) < 100;

    const registerTableFilters = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.leFilterBound === '1') return;
        dtTableEl.dataset.leFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _searchData, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesMulti(appliedFilters.country, row.countryCode)
                && matchesMulti(appliedFilters.operationalStatus, operationalOf(row))
                && matchesMulti(appliedFilters.evidenceStatus, row.evidenceStatus)
                && matchesMulti(appliedFilters.baseCurrency, row.baseCurrencyCode)
                && matchesDateRange(appliedFilters.createdFrom, appliedFilters.createdTo, row.createdAt)
                && matchesIncomplete(appliedFilters.incompleteOnly, row.completenessScore);
        });
    };

    // ─── Status badge palettes ───────────────────────────────────────────────
    const operationalBadge = (status) => {
        const s = String(status || '').toUpperCase();
        const map = {
            DRAFT: { cls: 'bg-label-secondary', txt: L.StatusDraft || 'Draft' },
            INREVIEW: { cls: 'bg-label-info', txt: L.StatusInReview || 'In Review' },
            APPROVED: { cls: 'bg-label-info', txt: L.StatusApproved || 'Approved' },
            ACTIVE: { cls: 'bg-label-success', txt: L.StatusActive || 'Active' },
            SUSPENDED: { cls: 'bg-label-warning', txt: L.StatusSuspended || 'Suspended' },
            ARCHIVED: { cls: 'bg-label-secondary text-muted', txt: L.StatusArchived || 'Archived' }
        };
        const m = map[s] || { cls: 'bg-label-info', txt: s || '-' };
        return `<span class="badge ${m.cls}">${escapeHtml(m.txt)}</span>`;
    };
    const statutoryBadge = (status) => {
        const s = String(status || '').toUpperCase();
        const map = {
            REGISTERED: { cls: 'bg-label-success', txt: L.StatusRegistered || 'Registered' },
            PENDING: { cls: 'bg-label-warning', txt: L.StatusPending || 'Pending' },
            SUSPENDED: { cls: 'bg-label-warning', txt: L.StatusSuspended || 'Suspended' },
            DISSOLVED: { cls: 'bg-label-secondary text-muted', txt: L.StatusDissolved || 'Dissolved' }
        };
        const m = map[s];
        return m ? `<span class="badge ${m.cls}">${escapeHtml(m.txt)}</span>` : `<span class="text-muted">-</span>`;
    };

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue);
    };

    const patchLifecycle = (id, action, confirmKey, toastKey, name) => {
        const typeMap = { activate: 'primary', suspend: 'warning', archive: 'warning' };
        const btnMap = { activate: L.Activate, suspend: L.Suspend, archive: L.Archive };
        window.showConfirm?.(L[confirmKey] || L.AreYouSure, async () => {
            try {
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${action}`, { method: 'PATCH', headers: getAuthHeaders() });
                if (!response.ok) throw new Error(`${action} failed.`);
                reloadWithSuccessToast(toastKey, name);
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        }, { entityName: name, type: typeMap[action] || 'primary', confirmButtonText: btnMap[action] });
    };

    const deleteRow = (id, name) => {
        window.showConfirm?.(L.AreYouSure, async () => {
            try {
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { method: 'DELETE', headers: getAuthHeaders() });
                if (!response.ok) throw new Error('Delete failed.');
                reloadWithSuccessToast('RecordDeleted', name);
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        }, { entityName: name, type: 'danger', confirmButtonText: L.Delete });
    };

    const nameOf = (row) => row?.legalName || row?.LegalName || row?.code || row?.Code || '';

    const rowActionHandlers = {
        details: ({ id, row }) => { const rid = id || row?.id; if (rid) window.location.href = `${detailsUrl}/${encodeURIComponent(rid)}`; },
        edit: ({ id, row }) => { const rid = id || row?.id; if (rid) window.location.href = `${wizardUrl}/${encodeURIComponent(rid)}`; },
        activate: ({ id, row }) => { const rid = id || row?.id; if (rid) patchLifecycle(rid, 'activate', 'ActivateConfirm', 'RecordActivated', nameOf(row)); },
        suspend: ({ id, row }) => { const rid = id || row?.id; if (rid) patchLifecycle(rid, 'suspend', 'SuspendConfirm', 'RecordSuspended', nameOf(row)); },
        archive: ({ id, row }) => { const rid = id || row?.id; if (rid) patchLifecycle(rid, 'archive', 'ArchiveConfirm', 'RecordArchived', nameOf(row)); },
        delete: ({ id, row }) => { const rid = id || row?.id; if (rid) deleteRow(rid, nameOf(row)); }
    };

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };
    // Backend rows expose `legalEntityId` (not `id`); normalize so the shared dispatcher + renderers work.
    const normalizeRow = (row) => ({ ...row, id: row.id || row.Id || row.legalEntityId || row.LegalEntityId });
    const lookupLabel = (mapKey, code) => (code ? (lookupMaps[mapKey][String(code)] || code) : '-');
    const parentLabel = (parentId) => {
        if (!parentId) return '-';
        const p = entityMap[String(parentId)];
        return p ? (p.legalName ? `${p.code ? p.code + ' — ' : ''}${p.legalName}` : (p.code || parentId)) : parentId;
    };
    const rebuildEntityMap = () => {
        Object.keys(entityMap).forEach((k) => delete entityMap[k]);
        rowsData.forEach((r) => {
            const id = r.legalEntityId || r.LegalEntityId || r.id;
            if (id) entityMap[String(id)] = { legalName: r.legalName || r.LegalName || '', code: r.code || r.Code || '' };
        });
    };

    // ─── Lookups for table labels + filter selects (best-effort) ─────────────
    const fetchLookup = (mapKey, url) => fetch(url, { headers: getAuthHeaders() })
        .then((r) => r.ok ? r.json() : Promise.reject(r))
        .then((payload) => {
            const items = unwrapList(payload);
            items.forEach((it) => {
                const code = it.code ?? it.Code ?? it.value ?? it.Value;
                const name = it.name ?? it.Name ?? code;
                if (code != null) lookupMaps[mapKey][String(code)] = name;
            });
            return items;
        })
        .catch(() => []);

    const appendFilterOptions = (selectId, items) => {
        const select = document.getElementById(selectId);
        if (!select) return;
        select.innerHTML = ''; // multi-select uses data-placeholder, so no empty option
        (items || []).forEach((it) => {
            const code = it.code ?? it.Code ?? it.value ?? it.Value;
            const name = it.name ?? it.Name ?? code;
            if (code == null) return;
            const opt = document.createElement('option');
            opt.value = code;
            opt.textContent = `${name}`;
            select.appendChild(opt);
        });
    };

    const loadReferenceData = () => Promise.all([
        fetchLookup('legalForm', `${endpoint}/lookups/legal-form`),
        fetchLookup('organizationRole', `${endpoint}/lookups/organization-role`),
        fetchLookup('country', `${endpoint}/platform-lookups/countries`),
        fetchLookup('currency', `${endpoint}/platform-lookups/currencies`)
    ]).then(([, , countries, currencies]) => {
        appendFilterOptions('filterCountry', countries);
        appendFilterOptions('filterBaseCurrency', currencies);
    });

    // ─── Save View: state capture / (de)serialization ────────────────────────
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
        filters: Object.keys(v?.filters || {}).sort().reduce((acc, key) => { acc[key] = normalizeFilterValue(v.filters[key]); return acc; }, {}),
        search: normalizeString(v?.search),
        colVis: normalizeColVis(v?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(v?.columnOrder) || identityColOrder(),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || view || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || identityColOrder(),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getResetBaselineState = () => normalizeViewState({ filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: identityColOrder(), order: baseOrder });

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

    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || { filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: identityColOrder(), order: baseOrder };
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
            console.error('[LegalEntities SaveView] Failed to load saved views.', error);
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

    // ─── Inline filter mount + Select2 (multi) + flatpickr dates ─────────────
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

    const MULTI_FILTER_IDS = '#filterCountry, #filterOperationalStatus, #filterEvidenceStatus, #filterBaseCurrency';
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
        $(MULTI_FILTER_IDS).each(function () {
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
    const initDatePickers = () => {
        if (typeof window.flatpickr !== 'function') return;
        ['filterCreatedFrom', 'filterCreatedTo'].forEach((id) => {
            const el = document.getElementById(id);
            if (!el || datePickers[id]) return;
            datePickers[id] = window.flatpickr(el, { dateFormat: 'Y-m-d', altInput: true, altFormat: L.DateFormat || 'Y-m-d', allowInput: true });
        });
    };
    const dateVal = (id) => normalizeString(document.getElementById(id)?.value);
    const setDatePicker = (id, v) => { const fp = datePickers[id]; if (fp) fp.setDate(v || null, false); else { const el = document.getElementById(id); if (el) el.value = v || ''; } };

    const syncFilterControls = (values) => {
        $('#filterCountry').val(normalizeArray(values.country)).trigger('change');
        $('#filterOperationalStatus').val(normalizeArray(values.operationalStatus)).trigger('change');
        $('#filterEvidenceStatus').val(normalizeArray(values.evidenceStatus)).trigger('change');
        $('#filterBaseCurrency').val(normalizeArray(values.baseCurrency)).trigger('change');
        setDatePicker('filterCreatedFrom', values.createdFrom);
        setDatePicker('filterCreatedTo', values.createdTo);
        const inc = document.getElementById('filterIncompleteOnly');
        if (inc) inc.checked = !!values.incompleteOnly;
    };
    const getAppliedFilterCount = () => [
        appliedFilters.country, appliedFilters.operationalStatus, appliedFilters.evidenceStatus,
        appliedFilters.baseCurrency, appliedFilters.createdFrom, appliedFilters.createdTo, appliedFilters.incompleteOnly
    ].filter(hasFilterValue).length;

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
        initSelect2Filters();
        initDatePickers();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                country: $('#filterCountry').val() || [],
                operationalStatus: $('#filterOperationalStatus').val() || [],
                evidenceStatus: $('#filterEvidenceStatus').val() || [],
                baseCurrency: $('#filterBaseCurrency').val() || [],
                createdFrom: dateVal('filterCreatedFrom'),
                createdTo: dateVal('filterCreatedTo'),
                incompleteOnly: !!document.getElementById('filterIncompleteOnly')?.checked
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

    const initDataTable = () => {
        if (!dtTableEl || !window.DtDefaults) {
            console.error('[LegalEntities] DataTable element or DtDefaults not found.');
            return;
        }

        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
            action: () => toggleInlineFilter()
        };
        const saveFilterBtn = {
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
                    console.error('[LegalEntities SaveView] Failed to save default view.', error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }
        };

        const dtConfig = window.DtDefaults.create({
            processing: true,
            serverSide: false,
            stateSave: false,
            order: baseOrder,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: function (data, callback) {
                fetch(endpoint, { headers: getAuthHeaders() })
                    .then((response) => response.ok ? response.json() : Promise.reject(response))
                    .then((payload) => {
                        rowsData = unwrapList(payload).map(normalizeRow);
                        rebuildEntityMap();
                        callback({ data: rowsData });
                    })
                    .catch(() => {
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                        callback({ data: [] });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'legalName', name: 'legalName', render: escapeHtml },
                { data: 'code', name: 'code', render: (value) => `<span class="fw-medium font-monospace text-primary">${escapeHtml(value)}</span>` },
                { data: 'legalFormCode', name: 'legalForm', render: (v) => escapeHtml(lookupLabel('legalForm', v)) },
                { data: 'organizationRoleCode', name: 'orgRole', render: (v) => escapeHtml(lookupLabel('organizationRole', v)) },
                { data: 'countryCode', name: 'country', render: (v) => escapeHtml(lookupLabel('country', v)) },
                { data: 'baseCurrencyCode', name: 'baseCurrency', render: (v) => escapeHtml(lookupLabel('currency', v)) },
                { data: 'parentLegalEntityId', name: 'parent', render: (v) => escapeHtml(parentLabel(v)) },
                { data: 'statutoryStatus', name: 'statutoryStatus', render: (v) => statutoryBadge(v) },
                { data: 'operationalStatus', name: 'operationalStatus', render: (v, t, row) => operationalBadge(operationalOf(row)) },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (value, type, row) => {
                        const id = row.id;
                        const rowJson = JSON.stringify(row);
                        const status = operationalOf(row);
                        const baseAttrs = { 'data-id': id, 'data-json': rowJson };
                        const actions = [
                            { key: 'details', icon: 'bx bx-show', text: L.ViewDetails || '', attrs: baseAttrs },
                            { key: 'edit', icon: 'bx bx-edit', className: 'js-edit-item', text: L.Edit || '', attrs: baseAttrs }
                        ];
                        if (status && status !== 'ACTIVE') actions.push({ key: 'activate', icon: 'bx bx-check-circle', className: 'text-success', text: L.Activate || '', attrs: baseAttrs });
                        if (status === 'ACTIVE') actions.push({ key: 'suspend', icon: 'bx bx-pause-circle', className: 'text-warning', text: L.Suspend || '', attrs: baseAttrs });
                        if (status === 'ACTIVE' || status === 'SUSPENDED') actions.push({ key: 'archive', icon: 'bx bx-archive-in', className: 'text-warning', text: L.Archive || '', attrs: baseAttrs });
                        actions.push({ key: 'delete', icon: 'bx bx-trash', className: 'text-danger', text: L.Delete || '', attrs: baseAttrs });
                        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
                    }
                }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, responsivePriority: 1 },
                { targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNew || '', {}, { filterBtn, saveFilterBtn }, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                bindInlineFilterA11y();
                setupFilters(api);
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

        window.DitenDataTable?.bindActionDispatcher?.({ tableEl: dtTableEl, dt, onRowAction: rowActionHandlers });

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

    // ─── Add New → full-page wizard ──────────────────────────────────────────
    const bindAddNew = () => {
        document.addEventListener('click', (event) => {
            if (event.target.closest('.add-new')) {
                event.preventDefault();
                window.location.href = wizardUrl;
            }
        });
    };

    // After a wizard save redirects back here, surface the success toast it stashed.
    const flushWizardToast = () => {
        try {
            const msg = sessionStorage.getItem('le-toast');
            if (msg) { sessionStorage.removeItem('le-toast'); window.showToast?.(msg, 'success'); }
        } catch { /* ignore */ }
    };

    const init = async () => {
        loadL10n();
        flushWizardToast();
        bindAddNew();
        registerTableFilters();
        await loadDefaultView();
        await loadReferenceData();
        initDataTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => LegalEntitiesList.init());
