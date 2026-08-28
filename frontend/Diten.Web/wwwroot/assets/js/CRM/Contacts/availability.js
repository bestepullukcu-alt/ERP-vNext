/**
 * Contact Availability — Lookup DataTable (read-only, Golden-Slim standard)
 * Diten ERP vNext | CRM/Contacts/Availability
 *
 * The availability lookup answers "when can this contact be visited at each account on a date".
 * Rows are server-rendered by Availability.cshtml; this file wraps them in a client-side DataTable
 * so the surface inherits the full Golden-Slim chrome: search, page length, INLINE FILTER (status /
 * weekday / account), SAVE VIEW (personalization), export, colvis, info, paging, responsive-modal.
 *
 * READ-ONLY by design — no Add New, no bulk, no row actions; the page never builds a route or a
 * visit plan (MOD-0155). Filtering is client-side on canonical row data (data-status / data-weekday
 * / data-account) so it never depends on the localised display text.
 */
'use strict';

const ContactAvailabilityLookup = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-availability-lookup');
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'ContactAvailabilityLookup' };
    const filterHostId = 'availabilityFilterHost';
    const filterCollapseId = 'availabilityFilterCollapse';

    // Column map (control at 0): 1 Account · 2 Weekday · 3 Window · 4 Preferred · 5 Avoid · 6 Status · 7 Reason.
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7];
    const totalColumnCount = 8;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6, 7];
    const baseOrder = [[1, 'asc']];

    let appliedFilters = { status: [], weekday: [], account: '' };
    let L = window.L10n || {};

    // ─── L10n bridge (embedded JSON → window.L10n, PascalCase) ────────────────
    const loadL10n = () => {
        const payload = document.getElementById('availability-l10n');
        if (!payload) { L = window.L10n || {}; return; }
        try {
            const raw = JSON.parse(payload.textContent || '{}');
            const toPascal = (k) => k.charAt(0).toUpperCase() + k.slice(1);
            const normalized = {};
            Object.keys(raw).forEach((k) => { normalized[toPascal(k)] = raw[k]; });
            window.L10n = Object.assign({}, window.L10n || {}, normalized);
        } catch (e) { /* fall through to whatever is on window.L10n */ }
        L = window.L10n || {};
    };

    const getAuthHeaders = (includeJson = false) =>
        window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    // ─── Normalize helpers ────────────────────────────────────────────────────
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v).trim()));
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ status: [], weekday: [], account: '' });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            status: normalizeArray(source.status),
            weekday: normalizeArray(source.weekday),
            account: normalizeString(source.account)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;

    // ─── Filter matching (canonical row data) ─────────────────────────────────
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(normalizeString(actual));
    };
    const matchesSingleFilter = (selected, actual) => {
        const norm = normalizeString(selected);
        return !norm || normalizeString(actual) === norm;
    };

    // ─── Column visibility / order helpers ────────────────────────────────────
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

    // ─── Search input helpers ─────────────────────────────────────────────────
    const getSearchVal = (api) => { try { return api.table().container().querySelector('.dt-search input')?.value || ''; } catch (e) { return ''; } };
    const syncSearchInput = (api, v) => { try { const el = api.table().container().querySelector('.dt-search input'); if (el) el.value = v || ''; } catch (e) { } };

    // ─── View state ───────────────────────────────────────────────────────────
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
            console.error('[ContactAvailabilityLookup SaveView] Failed to load saved views.', error);
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

    // ─── Inline filter UI ─────────────────────────────────────────────────────
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
        if (!window.jQuery?.fn?.dataTable?.ext?.search || !dtTableEl || dtTableEl.dataset.slimFilterBound === '1') return;
        dtTableEl.dataset.slimFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _searchData, dataIndex) => {
            if (settings.nTable !== dtTableEl) return true;
            const tr = settings.aoData?.[dataIndex]?.nTr || null;
            const ds = tr ? tr.dataset : {};
            return matchesMultiFilter(appliedFilters.status, ds.status)
                && matchesMultiFilter(appliedFilters.weekday, ds.weekday)
                && matchesSingleFilter(appliedFilters.account, ds.account);
        });
    };

    // ─── Select2 multi-summary chip ───────────────────────────────────────────
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

        const clampDropdown = () => {
            requestAnimationFrame(() => {
                const dd = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
                if (!dd) return;
                const rect = dd.getBoundingClientRect();
                const pad = 8;
                let dx = 0, dy = 0;
                if (rect.right > window.innerWidth - pad) dx -= rect.right - (window.innerWidth - pad);
                if (rect.left < pad) dx += pad - rect.left;
                if (rect.bottom > window.innerHeight - pad) dy -= rect.bottom - (window.innerHeight - pad);
                if (rect.top < pad) dy += pad - rect.top;
                if (!dx && !dy) return;
                const cs = window.getComputedStyle(dd);
                const baseLeft = parseFloat(cs.left) || rect.left + window.scrollX;
                const baseTop = parseFloat(cs.top) || rect.top + window.scrollY;
                if (dx) dd.style.left = `${baseLeft + dx}px`;
                if (dy) dd.style.top = `${baseTop + dy}px`;
                dd.style.transform = 'none';
            });
        };

        $('#filterAvailabilityStatus, #filterAvailabilityWeekday').each(function () {
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
            $s.on('select2:open', clampDropdown);
            $s.on('change.select2-summary', function () { syncMultiSelectSummary($s); });
            requestAnimationFrame(() => syncMultiSelectSummary($s));
        });

        $('#filterAvailabilityAccount').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                width: 'element',
                allowClear: true
            });
            $s.on('select2:open', clampDropdown);
        });
    };

    const syncFilterControls = (values) => {
        $('#filterAvailabilityStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterAvailabilityWeekday').val(normalizeArray(values.weekday)).trigger('change');
        $('#filterAvailabilityAccount').val(values.account || '').trigger('change');
    };

    const getAppliedFilterCount = () =>
        [appliedFilters.status, appliedFilters.weekday, appliedFilters.account].filter(hasFilterValue).length;

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
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });

        document.getElementById('btnAvailabilityFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterAvailabilityStatus').val() || [],
                weekday: $('#filterAvailabilityWeekday').val() || [],
                account: document.getElementById('filterAvailabilityAccount')?.value || ''
            };
            api.draw();
            window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
        });

        document.getElementById('btnAvailabilityFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            applySavedTableState(api, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });
    };

    // ─── DataTable init ───────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!window.DataTable || !window.DtDefaults) { console.error('[ContactAvailabilityLookup] DataTables/DtDefaults required.'); return; }

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
                action: async function (e, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || L.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[ContactAvailabilityLookup SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        // DOM-sourced client table: no ajax / no columns[] — DataTables reads the server-rendered <tbody>.
        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            stateSave: false,
            processing: false,
            order: baseOrder,
            colReorder: { columns: ':gt(0)' },
            columnDefs: [
                { targets: 0, className: 'control', orderable: false, searchable: false, responsivePriority: 2, render: () => '' }
            ],
            buttons: window.DtDefaults.exportButtons(
                null,
                {},
                extraButtons,
                { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: saveViewColumnIndexes }
            ),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterA11y();
                setupFilters(this.api());
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        }));

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

    // ─── Per-link grids (Availability schedule + Date exceptions) ─────────────
    // Read-only-ish grids: full golden-slim chrome (search / page-length / export / colvis / responsive) plus an
    // "Add" button whose click opens the matching create offcanvas (data-add-target). No inline filter / save-view
    // here — those are personalisation-keyed and there is one grid PER account link, so a shared view key would
    // collide; the lookup tab keeps the filter + save-view surface. Row actions stay server-POST forms.
    const initLinkTables = () => {
        if (!window.DataTable || !window.DtDefaults) return;
        document.querySelectorAll('.datatables-link-grid').forEach((el) => {
            if (el.dataset.dtInit === '1') return;
            el.dataset.dtInit = '1';

            const colCount = el.querySelectorAll('thead th').length;
            const midCols = []; // everything except the control (0) and the trailing actions column
            for (let i = 1; i < colCount - 1; i++) midCols.push(i);

            const addTarget = el.dataset.addTarget || '';
            const addText = el.dataset.addText || '';
            const hasAdd = !!addTarget;

            new DataTable(el, window.DtDefaults.create({
                stateSave: false,
                processing: false,
                order: [[1, 'asc']],
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columnDefs: [
                    { targets: 0, className: 'control', orderable: false, searchable: false, responsivePriority: 2, render: () => '' },
                    { targets: -1, orderable: false, searchable: false, className: 'cell-fit text-end all' }
                ],
                buttons: window.DtDefaults.exportButtons(
                    hasAdd ? addText : null,
                    hasAdd ? { 'data-bs-toggle': 'offcanvas', 'data-bs-target': addTarget } : {},
                    {},
                    { exportColumns: midCols, colvisColumns: midCols }
                )
            }));
        });
    };

    // A DataTable initialised inside a hidden tab-pane reports an unsettled layout; recompute widths / responsive
    // whenever any (outer or nested) tab becomes visible.
    const bindTabAdjust = () => {
        document.querySelectorAll('button[data-bs-toggle="tab"]').forEach((btn) => {
            if (btn.dataset.adjustBound === '1') return;
            btn.dataset.adjustBound = '1';
            btn.addEventListener('shown.bs.tab', () => {
                if (window.jQuery && $.fn.dataTable) {
                    try { $.fn.dataTable.tables({ visible: true, api: true }).columns.adjust().responsive.recalc(); } catch (e) { }
                }
            });
        });
    };

    // ─── Preserve the active tab across a POST→redirect ───────────────────────
    // Add availability / Add date exception (and row deactivate/archive) submit a server form that redirects and
    // full-reloads the page, which would otherwise snap back to the default (Availability Check) tab. Capture the
    // active outer tab + nested sub-tab on submit, then re-activate it after the reload.
    const TAB_KEY = 'crmContactAvailabilityActiveTab';

    const persistActiveTabOnSubmit = () => {
        document.addEventListener('submit', () => {
            try {
                const outer = document.querySelector('.tab-pane.active[id^="availability-"]');
                if (!outer) return;
                const sub = outer.querySelector('.tab-content .tab-pane.active');
                sessionStorage.setItem(TAB_KEY, JSON.stringify({ outer: outer.id, sub: sub ? sub.id : null }));
            } catch (e) { /* sessionStorage unavailable — non-fatal */ }
        }, true);
    };

    const restoreActiveTab = () => {
        let data = null;
        try { data = JSON.parse(sessionStorage.getItem(TAB_KEY) || 'null'); } catch (e) { data = null; }
        if (!data) return;
        try { sessionStorage.removeItem(TAB_KEY); } catch (e) { /* ignore */ }
        if (!window.bootstrap) return;
        const showTab = (paneId) => {
            if (!paneId) return;
            const btn = document.querySelector('[data-bs-toggle="tab"][data-bs-target="#' + paneId + '"]');
            if (btn) { try { bootstrap.Tab.getOrCreateInstance(btn).show(); } catch (e) { } }
        };
        showTab(data.outer);
        if (data.sub) { setTimeout(() => showTab(data.sub), 60); } // after the outer pane is visible
    };

    return {
        init: function () {
            loadL10n();
            if (dtTableEl) { // the lookup card only exists after a date has been queried
                registerTableFilters();
                initDataTable();
            }
            initLinkTables();
            bindTabAdjust();
            persistActiveTabOnSubmit();
            restoreActiveTab();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => ContactAvailabilityLookup.init());
