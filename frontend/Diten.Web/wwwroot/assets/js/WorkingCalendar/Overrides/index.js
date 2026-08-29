/**
 * Working Calendar Overrides (tenant layer) - DataTables Index Script.
 * Tenant surface: company holidays, closures and compensation working days layered on the country calendar.
 *
 * This list shows ONLY this tenant's own override rows. Country calendars never appear here — a tenant that has
 * authored nothing legitimately sees an empty list rather than someone else's defaults presented as editable.
 * The country layer's EFFECT is still visible, per date, through the resolution probe on the details page.
 *
 * No bulk action bar: this module is CRUD-minus-delete (archive only) and has no /bulk endpoint, so a bulk bar
 * would be wired to nothing. The golden bulk-delete contract is intentionally N/A here.
 */
'use strict';

const WorkingCalendarOverridesList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-workingcalendaroverrides');
    // Proxy profile: the same-origin MVC proxy resolves the caller's token server-side and forwards to the
    // Gateway. Browser script therefore holds no credential of any kind and never talks to a service port.
    const endpoint = '/WorkingCalendar/Overrides/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'WorkingCalendar', pageKey: 'Overrides' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5, 6, 7];
    const baseOrder = [[4, 'desc']];
    let appliedFilters = { country: [], year: [], status: [], archived: '' };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const getAuthHeaders = (includeJson = false) =>
        window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    /**
     * Pulls the real reason out of the API's error envelope.
     * The envelope is {data, statusCode, isSuccessful, errors:[...], reason_code} — there is NO `message` field, so
     * reading `body.message` always fell through to the generic toast and hid the actual 409/400 reason (duplicate
     * code, reserved day type, concurrency). `message` is still tried second for the ProblemDetails-shaped replies
     * the Gateway emits on its own (those carry `title`/`detail` instead).
     */
    const apiError = (body) =>
        (Array.isArray(body?.errors) && body.errors[0])
        || body?.message
        || body?.detail
        || L.ErrorOccurred;

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ country: [], year: [], status: [], archived: '' });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            country: normalizeArray(source.country),
            year: normalizeArray(source.year),
            status: normalizeArray(source.status),
            archived: normalizeString(source.archived)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(normalizeString(actual));
    };

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
        if (n && typeof api?.colReorder?.order === 'function') api.colReorder.order(n, true);
    };
    const applyColVis = (api, colVis) => {
        const n = normalizeColVis(colVis);
        if (!n) return;
        saveViewColumnIndexes.forEach((ci) => { if (typeof n[ci] === 'boolean') { try { api.column(ci).visible(n[ci], false); } catch (e) { } } });
    };

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
            console.error('[WorkingCalendarOverrides SaveView] Failed to load saved views.', error);
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
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.compactFilterBound === '1') return;
        dtTableEl.dataset.compactFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            // Archived rows are hidden unless explicitly asked for; history stays reachable, not in the way.
            if (normalizeString(appliedFilters.archived) !== 'true' && row.calendarStatus === 'archived') return false;
            return matchesMultiFilter(appliedFilters.country, row.countryCode)
                && matchesMultiFilter(appliedFilters.year, row.calendarYear != null ? String(row.calendarYear) : '')
                && matchesMultiFilter(appliedFilters.status, row.calendarStatus);
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
        $('#filterCountry, #filterYear, #filterStatus').each(function () {
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
        $('#filterArchived').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                allowClear: true
            });
        });
    };

    const syncFilterControls = (values) => {
        $('#filterCountry').val(normalizeArray(values.country)).trigger('change');
        $('#filterYear').val(normalizeArray(values.year)).trigger('change');
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterArchived').val(values.archived || '').trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.country, appliedFilters.year, appliedFilters.status, appliedFilters.archived].filter(hasFilterValue).length;

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

    const appendOptions = (select, items) => {
        select.innerHTML = '';
        items.forEach((item) => {
            if (!item?.value) return;
            const opt = document.createElement('option');
            opt.value = item.value;
            opt.textContent = item.text || item.value;
            select.appendChild(opt);
        });
    };

    /**
     * Filter vocabulary comes from the server: countries from the MOD-0048 reference set, statuses from the
     * module contract. Nothing here is a hardcoded list, so the filter can never drift from what the API accepts.
     */
    const loadLookupOptions = async () => {
        const countrySelect = document.getElementById('filterCountry');
        const yearSelect = document.getElementById('filterYear');
        const statusSelect = document.getElementById('filterStatus');
        if (!countrySelect || !yearSelect || !statusSelect) return;

        try {
            const [countryRes, contractRes] = await Promise.all([
                fetch(`${endpoint}/countries`, { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() }),
                fetch(`${endpoint}/contract`, { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() })
            ]);

            if (countryRes.ok) {
                const payload = await countryRes.json();
                const items = payload?.data || payload?.Data || payload || [];
                appendOptions(countrySelect, items.map((c) => ({ value: c.code || c.value, text: c.name || c.code })));
            }

            if (contractRes.ok) {
                const payload = await contractRes.json();
                const contract = payload?.data || payload?.Data || payload || {};
                const statuses = contract.statuses || contract.Statuses || [];
                appendOptions(statusSelect, statuses.map((s) => ({ value: s, text: statusLabel(s) })));
            }

            // Years are derived from the data itself rather than invented as a fixed range.
            const years = new Set();
            (dt?.rows?.().data?.().toArray?.() || []).forEach((r) => { if (r?.calendarYear) years.add(String(r.calendarYear)); });
            const currentYear = new Date().getFullYear();
            [currentYear - 1, currentYear, currentYear + 1].forEach((y) => years.add(String(y)));
            appendOptions(yearSelect, Array.from(years).sort().reverse().map((y) => ({ value: y, text: y })));
        } catch (error) {
            console.error('[WorkingCalendarOverrides Lookup] Failed.', error);
        }
    };

    const statusLabel = (status) => ({
        draft: L.StatusDraft || 'draft',
        active: L.StatusActive || 'active',
        archived: L.StatusArchived || 'archived'
    }[status] || status);

    const setupFilters = async (api) => {
        await loadLookupOptions();
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                country: $('#filterCountry').val() || [],
                year: $('#filterYear').val() || [],
                status: $('#filterStatus').val() || [],
                archived: document.getElementById('filterArchived')?.value || ''
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

    const getStatusMap = () => ({
        active: { title: statusLabel('active'), class: 'bg-label-success' },
        draft: { title: statusLabel('draft'), class: 'bg-label-warning' },
        archived: { title: statusLabel('archived'), class: 'bg-label-secondary' }
    });

    const renderWeekend = (row) => {
        const days = Array.isArray(row.effectiveWeekendDays) ? row.effectiveWeekendDays : [];
        if (!days.length) return `<span class="text-muted">${L.NotAvailable || '-'}</span>`;
        const text = days.map((d) => (L[d] || d)).join(', ');
        // The inherited case is labelled, never left to look like the row's own configuration.
        return row.weekendInherited
            ? `<span>${text}</span> <span class="badge bg-label-info ms-1">${L.Inherited || 'inherited'}</span>`
            : `<span>${text}</span>`;
    };

    /**
     * Lifecycle actions go through the SAME reload-then-toast lifecycle as every other mutating list action:
     * the table is reloaded from the server and only then is the toast shown. No local row surgery — a row whose
     * status changed must be re-read, because activation can also be refused server-side (409).
     */
    const reloadWithSuccessToast = (messageKey, interpolationValue) =>
        window.DitenDataTable.reloadWithToast(dt, dtTableEl, messageKey, interpolationValue);

    const postLifecycleAction = async (row, action, confirmText, successKey, confirmButtonText, type) => {
        if (!row?.id) return;
        window.showConfirm?.(confirmText, async () => {
            try {
                const res = await fetch(`${endpoint}/${row.id}/${action}`, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: getAuthHeaders(true),
                    body: JSON.stringify({ expectedVersion: row.version })
                });
                if (!res.ok) {
                    // 409 here is a real answer (another active calendar exists / row changed), not a crash.
                    const payload = await res.json().catch(() => null);
                    window.showToast?.(apiError(payload), 'error');
                    return;
                }
                reloadWithSuccessToast(successKey);
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred, 'error');
            }
        }, { entityName: row.calendarName, type: type, confirmButtonText: confirmButtonText });
    };

    const rowActionHandlers = {
        quickView: ({ id }) => {
            if (id) window.location.href = `/WorkingCalendar/Overrides/Details/${id}`;
        },
        edit: ({ id }) => {
            if (id) window.location.href = `/WorkingCalendar/Overrides/Edit/${id}`;
        },
        activate: ({ row }) =>
            postLifecycleAction(row, 'activate', L.ActivateConfirm || L.AreYouSure, 'RecordUpdated', L.Activate, 'warning'),
        archive: ({ row }) =>
            postLifecycleAction(row, 'archive', L.ArchiveConfirm || L.AreYouSure, 'RecordUpdated', L.Archive, 'danger')
    };

    // Quick View uses event delegation via closest('.js-quick-view') so re-drawn rows never lose their handler.
    const bindEvents = () => {
        if (!dtTableEl || dtTableEl.dataset.quickViewBound === '1') return;
        dtTableEl.dataset.quickViewBound = '1';
        dtTableEl.addEventListener('click', (event) => {
            const trigger = event.target.closest('.js-quick-view');
            if (!trigger) return;
            event.preventDefault();
            rowActionHandlers.quickView({ id: trigger.getAttribute('data-id') });
        });
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
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
                        console.error('[WorkingCalendarOverrides SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        // DitenDataTable wraps the DataTables v2 constructor and shared defaults:
        // new DataTable(...)
        // window.DtDefaults.create(...)
        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: endpoint,
                type: 'GET',
                xhrFields: { withCredentials: true }
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'calendarCode', name: 'calendarCode' },
                    { data: 'calendarName', name: 'calendarName' },
                    { data: 'countryCode', name: 'countryCode' },
                    { data: 'calendarYear', name: 'calendarYear' },
                    { data: 'effectiveWeekendDays', name: 'effectiveWeekendDays' },
                    { data: 'activeDayCount', name: 'activeDayCount' },
                    { data: 'calendarStatus', name: 'calendarStatus' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    {
                        // An inherited country calendar is shown so the tenant can see WHAT it is overriding. It is
                        // labelled on the name column rather than given a column of its own, so the badge travels
                        // with the row under column reorder and responsive collapse.
                        targets: 2,
                        render: (data, type, full) => {
                            const name = data ?? '';
                            if (type !== 'display' || !full.isReadOnly) return name;
                            return `<span class="text-muted">${name}</span>`
                                + ` <span class="badge bg-label-secondary ms-1">${L.CountryInherited || 'Country (inherited)'}</span>`;
                        }
                    },
                    { targets: 3, render: (data) => data ? `<span class="badge bg-label-primary">${data}</span>` : '' },
                    { targets: 5, orderable: false, render: (data, type, full) => type === 'display' ? renderWeekend(full) : (Array.isArray(data) ? data.join(',') : '') },
                    { targets: 6, className: 'text-center', render: (data) => `<span class="badge bg-label-secondary">${data ?? 0}</span>` },
                    {
                        targets: 7,
                        render: (data, type) => {
                            const status = getStatusMap()[data] || { title: L.Unknown, class: 'bg-label-primary' };
                            return type === 'display'
                                ? `<span class="badge ${status.class}">${status.title}</span>`
                                : status.title;
                        }
                    },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, "&#39;");
                            const quickView = {
                                key: 'quickView',
                                className: 'js-quick-view me-1',
                                icon: 'bx bx-show',
                                attrs: { 'data-id': full.id, 'title': L.QuickView }
                            };

                            // Inherited country row: readable, never writable. Only the read action is offered.
                            // This mirrors the backend rather than enforcing anything — a country row's id is not
                            // visible to the override get-by-id, so Edit/Activate/Archive would answer 404 anyway.
                            if (full.isReadOnly) {
                                return window.DitenDataTable.renderActions([quickView]);
                            }

                            return window.DitenDataTable.renderActions([
                                quickView,
                                {
                                    key: 'edit',
                                    className: 'js-edit-item',
                                    icon: 'bx bx-edit',
                                    text: L.Edit,
                                    attrs: { 'data-id': full.id, 'data-json': rowJson }
                                },
                                // Activation only makes sense while the calendar is still a draft.
                                ...(full.calendarStatus === 'draft' ? [{
                                    key: 'activate',
                                    className: 'js-activate-item',
                                    icon: 'bx bx-check-circle',
                                    text: L.Activate,
                                    attrs: { 'data-id': full.id, 'data-json': rowJson }
                                }] : []),
                                ...(full.calendarStatus !== 'archived' ? [{
                                    key: 'archive',
                                    className: 'text-danger',
                                    icon: 'bx bx-archive',
                                    text: L.Archive,
                                    attrs: { 'data-json': rowJson }
                                }] : [])
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew,
                    { href: '/WorkingCalendar/Overrides/Create' },
                    extraButtons,
                    { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/WorkingCalendar/Overrides/Create';
                    });
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

    return {
        init: function () {
            registerTableFilters();
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => WorkingCalendarOverridesList.init());
