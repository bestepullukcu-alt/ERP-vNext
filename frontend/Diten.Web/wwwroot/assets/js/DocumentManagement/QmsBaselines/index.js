/**
 * MOD-0028-FU03 - Documentation Structures (QMS) list (TenantShell, read-only).
 * Aligned to the Golden Compact DataTable standard: DtDefaults.create toolbar (search, export,
 * colVis, inline filter button, colReorder) + Save View (personalizationClient). Read-only:
 * row actions are Designer (DRAFT) and Details; no bulk/checkbox/CRUD. Consumes the FU02 backend
 * exclusively through the same-origin proxy (/DocumentManagementQmsBaselines/list -> Gateway 5000).
 */
'use strict';

const QmsBaselinesList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const tableEl = document.getElementById('dt-qmsbaselines');
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'DocumentManagement', pageKey: 'QmsBaselines' };
    const filterCollapseId = 'inlineFilterCollapse';
    const filterHostId = 'inlineFilterHost';
    const canCreateManual = !!window.QmsBaselinesPerms?.canCreateManual;
    const createManualUrl = window.QmsBaselinesPerms?.createManualUrl || '/DocumentManagementQmsBaselines/CreateManual';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5, 6, 7];
    const baseOrder = [[5, 'desc']];
    let appliedFilters = { status: [] };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    // ── renderers ───────────────────────────────────────────────────────────
    const text = (v, fallback) => (v === null || v === undefined || v === '' ? (fallback || '-') : String(v));
    // Two-line stacked date/time cell (project standard, cf. Platform/AuditLog): "Jun 16, 26" + muted "02:55 PM".
    const formatDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        if (Number.isNaN(d.getTime())) return String(v).slice(0, 10);
        const locale = window.CurrentLanguage || undefined;
        const datePart = new Intl.DateTimeFormat(locale, { month: 'short', day: '2-digit', year: '2-digit' }).format(d);
        const timePart = new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', hour12: true }).format(d);
        return `<span class="d-inline-flex flex-column lh-sm"><span>${datePart}</span><small class="text-muted">${timePart}</small></span>`;
    };
    const shortHash = (v) => (v ? `${String(v).slice(0, 12)}…` : '-');
    const statusBadge = (status) => {
        const s = String(status || '').toUpperCase();
        if (s === 'PUBLISHED') return `<span class="badge bg-label-success">${text(L.StatusPublished, 'Published')}</span>`;
        if (s === 'DRAFT') return `<span class="badge bg-label-warning">${text(L.StatusDraft, 'Draft')}</span>`;
        return `<span class="badge bg-label-secondary">${text(L.Unknown, status)}</span>`;
    };

    // ── filter helpers ──────────────────────────────────────────────────────
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i)).toUpperCase()).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s.toUpperCase()] : [];
    };
    const emptyFilters = () => ({ status: [] });
    const normalizeFilters = (filters) => ({ status: normalizeArray((filters || {}).status) });
    const hasFilterValue = (v) => normalizeArray(v).length > 0;
    const getAppliedFilterCount = () => [appliedFilters.status].filter(hasFilterValue).length;
    const matchesStatusFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(String(actual || '').toUpperCase());
    };

    // ── colVis / colOrder / view serialization (Golden Compact standard) ─────
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
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = defaultVisibleColumnIndexes.includes(ci); return a; }, {});
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
        return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
    };
    const captureColOrder = (api) => { try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (e) { return null; } };
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
        filters: { status: normalizeArray((v?.filters || {}).status) },
        search: normalizeString(v?.search),
        colVis: normalizeColVis(v?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(v?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || view || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getResetBaselineState = () => normalizeViewState({
        filters: emptyFilters(), search: '', colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder
    });

    // ── Save View (personalization) ─────────────────────────────────────────
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
        return normalizeViewState({
            filters: normalizeFilters(d.filters || d),
            search: normalizeString(d.search),
            colVis: normalizeColVis(d.colVis),
            columnOrder: normalizeColOrder(d.columnOrder),
            order: Array.isArray(d.order) ? d.order : null
        });
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
            console.error('[QmsBaselines SaveView] Failed to load saved views.', error);
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

    // ── inline filter (Status only) ─────────────────────────────────────────
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
        if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
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
        if (!window.jQuery?.fn?.dataTable?.ext?.search || tableEl?.dataset.qmsFilterBound === '1') return;
        tableEl.dataset.qmsFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== tableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesStatusFilter(appliedFilters.status, row.status);
        });
    };
    // Golden Compact inline-filter multi-select: placeholder summary + count badge + clear button
    // (the selected values are NOT shown as Select2 chips). Styles live in backbone-custom.css.
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
        const $s = $('#filterStatus');
        if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
        $s.select2({
            dropdownParent: $(document.body),
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
    };
    const syncFilterControls = (values) => {
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
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
        try { api.columns.adjust(); } catch (e) { }
        try { api.responsive?.recalc?.(); } catch (e) { }
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };
    const setupFilters = (api) => {
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = { status: normalizeArray($('#filterStatus').val() || []) };
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

    // ── table ───────────────────────────────────────────────────────────────
    const init = async () => {
        if (!tableEl || !window.jQuery || !window.jQuery.fn.DataTable || !window.DtDefaults || !window.DitenDataTable) {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
            return;
        }
        syncL10n();
        registerTableFilters();
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
                        console.error('[QmsBaselines SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        dt = new DataTable(tableEl, window.DtDefaults.create({
            ajax: {
                url: '/DocumentManagementQmsBaselines/list',
                type: 'GET',
                xhrFields: { withCredentials: true },
                dataSrc: function (json) {
                    if (json && json.isSuccessful === false) {
                        window.showToast?.(L.ErrorOccurred || 'Error', 'error');
                        return [];
                    }
                    return (json && (json.data || json.Data)) || [];
                }
            },
            language: { emptyTable: text(L.EmptyList, '') },
            order: baseOrder,
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            columns: [
                { data: 'id', orderable: false, searchable: false, className: 'control', render: () => '' },
                { data: 'baselineReleaseId', render: (d) => `<span class="fw-medium text-heading">${text(d)}</span>` },
                { data: 'baselineVersion', className: 'all' },
                { data: 'status', render: (d, type) => (type === 'display' ? statusBadge(d) : d) },
                { data: 'definitionCount' },
                { data: 'createdAt', render: (d, type) => (type === 'display' ? formatDate(d) : d) },
                { data: 'publishedAt', render: (d, type) => (type === 'display' ? formatDate(d) : d) },
                { data: 'snapshotHash', orderable: false, render: (d, type) => (type === 'display' ? `<code>${shortHash(d)}</code>` : (d || '')) },
                {
                    data: 'id',
                    orderable: false,
                    searchable: false,
                    className: 'cell-fit text-end pe-3 all',
                    render: (id, _type, row) => {
                        // Golden Compact standard: primary icon button + three-dots dropdown for the rest.
                        const actions = [
                            { key: 'details', className: 'btn-text-secondary', icon: 'bx bx-show', text: text(L.ViewDetails, 'Details'), attrs: { 'data-id': id } }
                        ];
                        if (String(row?.status || '').toUpperCase() === 'DRAFT') {
                            actions.push({ key: 'designer', icon: 'bx bx-git-branch', text: text(L.OpenDesigner, 'Designer'), attrs: { 'data-id': id } });
                        }
                        return window.DitenDataTable.renderActions(actions);
                    }
                }
            ],
            // "Create Manual Baseline" rendered as the toolbar Add New primary button (after the filter group).
            buttons: window.DtDefaults.exportButtons(
                canCreateManual ? (L.CreateManualBaseline || 'Create Manual Baseline') : null,
                canCreateManual ? { href: createManualUrl, title: L.CreateManualBaseline, 'data-bs-toggle': 'tooltip' } : null,
                extraButtons,
                { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] }
            ),
            initComplete: function () {
                document.getElementById('skeleton-loader')?.classList.add('d-none');
                mountInlineFilter();
                bindInlineFilterA11y();
                setupFilters(this.api());
                document.querySelector('.add-new')?.addEventListener('click', (e) => {
                    e.preventDefault();
                    window.location.href = createManualUrl;
                });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                document.getElementById('skeleton-loader')?.classList.add('d-none');
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

        // Standard row-action dispatch (read-only navigations) for renderActions keys.
        window.DitenDataTable.bindActionDispatcher({
            tableEl,
            dt,
            onRowAction: {
                details: ({ id }) => { if (id) window.location.href = `/DocumentManagementQmsBaselines/Details/${id}`; },
                designer: ({ id }) => { if (id) window.location.href = `/DocumentManagementQmsBaselines/Designer/${id}`; }
            }
        });
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => QmsBaselinesList.init());
