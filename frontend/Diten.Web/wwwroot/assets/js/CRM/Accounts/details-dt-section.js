/**
 * MOD-0149 / MOD-0150 — Account 360 Details: shared DataTable v2 section factory.
 *
 * The Details page carries TWO v2 tables (Related Contacts, Related Accounts). Every selector the v2 state contract
 * uses — `.dt-filter-btn`, `.dt-save-filter-btn`, `.dt-search input`, the inline filter host — is therefore scoped to
 * the section's own table container / configured ids; a page-global lookup would paint one section's badges, filter
 * panel and Save View state onto the other.
 *
 * Implements the v2 state contract: stateSave:false, saved view loaded BEFORE construction, Save View persisted through
 * personalizationClient (filters + search + colVis + columnOrder + sorting; never page/pageLength), dirty-state driven
 * by the applied state, Reset returning to the factory baseline rather than the saved view.
 *
 * Rows are DOM-rendered from a server projection, so the DataTables row index matches the payload order and the inline
 * filter reads the projection row instead of re-parsing rendered cells.
 */
'use strict';

window.Crm360Section = (function () {
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilterValue = (value) => (Array.isArray(value) ? normalizeArray(value) : normalizeString(value));
    const hasFilterValue = (v) => (Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0);
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(normalizeString(actual));
    };

    /**
     * @param {object} options
     *  tableEl            — the <table data-dt-standard="v2"> element
     *  bodyEl             — <tbody> that receives the rendered rows
     *  rows               — server projection rows (array); index == DataTables row index
     *  renderRow(row)     — returns the <tr>…</tr> markup for one row
     *  totalColumnCount   — column count including control + actions
     *  saveViewColumns    — colVis/export/colvis-managed column indexes
     *  baseOrder          — default sorting, e.g. [[1, 'asc']]
     *  columnDefs         — DataTables columnDefs for this section
     *  pageKey            — personalization pageKey (moduleKey is always 'CRM')
     *  filters            — [{ selectId, pick(row) }] inline filter chips (multi-select)
     *  filterHostId       — inline filter host element id (section-scoped)
     *  filterCollapseId   — inline filter collapse element id (section-scoped)
     *  applyButtonId / resetButtonId — inline filter buttons (section-scoped)
     *  skeletonSelector   — optional skeleton element to hide once the table is ready
     *  addNewText         — toolbar "Add" label; null/empty renders no Add button
     *  onAddNew()         — toolbar Add handler
     *  rowActions         — { key: handler } passed to DitenDataTable.bindActionDispatcher
     *  l10n()             — returns the current window.L10n
     */
    async function create(options) {
        const {
            tableEl, bodyEl, rows = [], renderRow, totalColumnCount, saveViewColumns, baseOrder,
            columnDefs = [], pageKey, filters = [], filterHostId, filterCollapseId,
            applyButtonId, resetButtonId, skeletonSelector, addNewText, onAddNew, rowActions, l10n
        } = options;

        if (!tableEl || !bodyEl) return null;

        const L = () => (typeof l10n === 'function' ? (l10n() || {}) : (window.L10n || {}));
        const personalizationClient = window.personalizationClient;
        const personalizationContext = { moduleKey: 'CRM', pageKey };
        const defaultVisibleColumns = saveViewColumns;

        let dt = null;
        let defaultViewRecord = null;
        let defaultViewState = null;
        let saveFilterArmed = false;
        let appliedFilters = {};

        const filterKeys = filters.map((f) => f.selectId);
        const emptyFilters = () => filterKeys.reduce((acc, key) => { acc[key] = []; return acc; }, {});
        const normalizeFilters = (source) => filterKeys.reduce((acc, key) => {
            acc[key] = normalizeArray((source || {})[key]);
            return acc;
        }, {});
        appliedFilters = emptyFilters();

        // ── column state ────────────────────────────────────────────────────
        const normalizeColVis = (colVis) => {
            if (!colVis) return null;
            const n = {};
            if (Array.isArray(colVis)) {
                saveViewColumns.forEach((ci, pos) => {
                    if (typeof colVis[ci] === 'boolean') n[ci] = colVis[ci];
                    else if (typeof colVis[pos] === 'boolean') n[ci] = colVis[pos];
                });
            } else if (typeof colVis === 'object') {
                saveViewColumns.forEach((ci) => { if (typeof colVis[ci] === 'boolean') n[ci] = colVis[ci]; });
            }
            return Object.keys(n).length ? n : null;
        };
        const captureColVis = (api) => {
            const r = {};
            saveViewColumns.forEach((ci) => { try { r[ci] = !!api.column(ci).visible(); } catch (e) { } });
            return r;
        };
        const normalizeColOrder = (order) => {
            if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
            const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
            return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
        };
        const captureColOrder = (api) => { try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (e) { return null; } };
        const defaultColVis = () => saveViewColumns.reduce((a, ci) => { a[ci] = defaultVisibleColumns.includes(ci); return a; }, {});
        const defaultColOrder = () => Array.from({ length: totalColumnCount }, (_, i) => i);
        const applyColOrder = (api, order) => {
            const n = normalizeColOrder(order);
            if (n && typeof api?.colReorder?.order === 'function') api.colReorder.order(n, true);
        };
        const applyColVis = (api, colVis) => {
            const n = normalizeColVis(colVis);
            if (!n) return;
            saveViewColumns.forEach((ci) => {
                if (typeof n[ci] === 'boolean') { try { api.column(ci).visible(n[ci], false); } catch (e) { } }
            });
        };

        // ── section-scoped DOM lookups ──────────────────────────────────────
        const container = (api) => { try { return api.table().container(); } catch (e) { return document; } };
        const scopedQuery = (api, selector) => container(api).querySelector(selector);

        const getSearchVal = (api) => scopedQuery(api, '.dt-search input')?.value || '';
        const syncSearchInput = (api, v) => { const el = scopedQuery(api, '.dt-search input'); if (el) el.value = v || ''; };

        // ── view (de)serialization ──────────────────────────────────────────
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
            columnOrder: normalizeColOrder(v?.columnOrder) || defaultColOrder(),
            order: Array.isArray(v?.order) ? v.order : baseOrder
        });
        const normalizeViewState = (view) => ({
            filters: normalizeFilters(view?.filters || view || emptyFilters()),
            search: normalizeString(view?.search),
            colVis: normalizeColVis(view?.colVis) || defaultColVis(),
            columnOrder: normalizeColOrder(view?.columnOrder) || defaultColOrder(),
            order: Array.isArray(view?.order) ? view.order : baseOrder
        });
        // Reset never restores the saved view — it always returns to the factory baseline (v2 contract).
        const getResetBaselineState = () => normalizeViewState({
            filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: defaultColOrder(), order: baseOrder
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
                console.error(`[${pageKey} SaveView] Failed to load saved views.`, error);
                return null;
            }
        };
        const saveDefaultView = async (view) => {
            if (!personalizationClient?.saveView) return null;
            const normalizedView = normalizeViewState(view);
            const payload = {
                moduleKey: personalizationContext.moduleKey,
                pageKey: personalizationContext.pageKey,
                viewName: (getSavedViewName(defaultViewRecord) || L().SaveView || 'Default').trim(),
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

        const setSaveFilterVisible = (api, visible) => {
            const btn = scopedQuery(api, '.dt-save-filter-btn');
            if (!btn) return;
            btn.classList.toggle('d-none', !visible);
            window.DtDefaults?.refreshButtonGroupRadii?.();
        };
        const isDirtyComparedToDefault = (api) =>
            serializeView(getCurrentView(api)) !== serializeView(defaultViewState || getResetBaselineState());

        // ── inline filter ───────────────────────────────────────────────────
        const mountInlineFilter = (api) => {
            const host = document.getElementById(filterHostId);
            const filterBtn = scopedQuery(api, '.dt-filter-btn');
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
        const bindInlineFilterA11y = (api) => {
            const btn = scopedQuery(api, '.dt-filter-btn');
            const collapseEl = document.getElementById(filterCollapseId);
            if (!btn || !collapseEl || btn.dataset.bound) return;
            btn.dataset.bound = '1';
            collapseEl.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
            collapseEl.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
        };

        const registerTableFilters = () => {
            if (!window.jQuery?.fn?.dataTable?.ext?.search || tableEl.dataset.sectionFilterBound === '1') return;
            tableEl.dataset.sectionFilterBound = '1';
            $.fn.dataTable.ext.search.push((settings, _searchData, dataIndex) => {
                if (settings.nTable !== tableEl) return true;
                const row = rows[dataIndex];
                if (!row) return true;
                return filters.every((f) => matchesMultiFilter(appliedFilters[f.selectId], f.pick(row)));
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
                const label = L().Reset || '';
                const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + label + '" title="' + label + '">&times;</span>');
                $clearBtn.on('mousedown', (e) => { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
                $actions.append($clearBtn);
            }
        };

        const initSelect2Filters = () => {
            if (!window.jQuery || !$.fn.select2) return;
            const $body = $(document.body);
            filters.forEach((f) => {
                const $s = $(`#${f.selectId}`);
                if (!$s.length) return;
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
                $s.on('change.select2-summary', () => syncMultiSelectSummary($s));
                requestAnimationFrame(() => syncMultiSelectSummary($s));
            });
        };

        // Chip options = the distinct values present in THIS account's projection. MOD-0048 stays the source of truth
        // for which codes may exist; offering a code this account has no row for would only produce empty result sets.
        const loadFilterOptions = () => {
            filters.forEach((f) => {
                const select = document.getElementById(f.selectId);
                if (!select) return;
                const values = Array.from(new Set(rows.map(f.pick).map(normalizeString).filter(Boolean))).sort();
                select.innerHTML = '';
                values.forEach((value) => {
                    const opt = document.createElement('option');
                    opt.value = value;
                    opt.textContent = value;
                    select.appendChild(opt);
                });
            });
        };

        const syncFilterControls = (values) => {
            filters.forEach((f) => $(`#${f.selectId}`).val(normalizeArray(values[f.selectId])).trigger('change'));
        };
        const getAppliedFilterCount = () => filterKeys.filter((key) => hasFilterValue(appliedFilters[key])).length;

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
            loadFilterOptions();
            initSelect2Filters();
            applySavedTableState(api, defaultViewState || { filters: appliedFilters });

            document.getElementById(applyButtonId)?.addEventListener('click', () => {
                appliedFilters = filters.reduce((acc, f) => {
                    acc[f.selectId] = $(`#${f.selectId}`).val() || [];
                    return acc;
                }, {});
                api.draw();
                window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
                if (saveFilterArmed) setSaveFilterVisible(api, isDirtyComparedToDefault(api));
                const collapseEl = document.getElementById(filterCollapseId);
                if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
            });
            document.getElementById(resetButtonId)?.addEventListener('click', (e) => {
                e.preventDefault();
                applySavedTableState(api, getResetBaselineState());
                if (saveFilterArmed) setSaveFilterVisible(api, isDirtyComparedToDefault(api));
            });
        };

        // ── build ───────────────────────────────────────────────────────────
        const renderRows = () => { bodyEl.innerHTML = rows.map(renderRow).join(''); };

        const bindRowActions = () => {
            if (!rowActions || !window.DitenDataTable?.bindActionDispatcher) return;
            window.DitenDataTable.bindActionDispatcher({
                tableEl,
                getTable: () => dt,
                onRowAction: rowActions
            });
        };

        const hideSkeleton = () => {
            if (!skeletonSelector) return;
            const el = document.querySelector(skeletonSelector);
            if (el) el.style.display = 'none';
        };

        renderRows();
        registerTableFilters();

        if (!window.DataTable || !window.DtDefaults?.create || !window.DtDefaults?.exportButtons) {
            hideSkeleton();
            return null;
        }

        // v2 contract: the saved view is loaded BEFORE the DataTable is constructed.
        await loadDefaultView();

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L().Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L().SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L().SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(tableApi, false);
                        window.showToast?.(L().RecordSaved || L().SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error(`[${pageKey} SaveView] Failed to save default view.`, error);
                        window.showToast?.(L().ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        const config = {
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            order: baseOrder,
            columnDefs,
            buttons: window.DtDefaults.exportButtons(
                addNewText || null,
                { href: 'javascript:void(0);' },
                extraButtons,
                { exportColumns: saveViewColumns, colvisColumns: saveViewColumns }
            ),
            drawCallback: function () {
                window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount());
            },
            initComplete: function () {
                const api = this.api();
                hideSkeleton();
                mountInlineFilter(api);
                bindInlineFilterA11y(api);
                setupFilters(api);
                if (addNewText && typeof onAddNew === 'function') {
                    container(api).querySelector('.add-new')?.addEventListener('click', (event) => {
                        event.preventDefault();
                        onAddNew();
                    });
                }
                setTimeout(() => { saveFilterArmed = true; }, 0);
            }
        };

        dt = new DataTable(tableEl, window.DtDefaults.create(config));

        dt.on('column-visibility.dt', () => {
            window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(dt, isDirtyComparedToDefault(dt));
        });
        dt.on('search.dt order.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(dt, isDirtyComparedToDefault(dt));
        });
        dt.on('column-reorder.dt columns-reordered.dt', () => {
            window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(dt, isDirtyComparedToDefault(dt));
        });

        bindRowActions();

        return { api: dt };
    }

    return { create };
})();
