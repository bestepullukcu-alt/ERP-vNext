'use strict';

window.DitenDataTable = (function () {
    function escapeAttr(value) {
        return String(value ?? '').replace(/"/g, '&quot;');
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function renderAttrs(attrs) {
        return Object.entries(attrs || {})
            .filter(function (entry) { return entry[1] !== undefined && entry[1] !== null && entry[1] !== false; })
            .map(function (entry) { return entry[0] + '="' + escapeAttr(entry[1]) + '"'; })
            .join(' ');
    }

    function renderIcon(icon, className) {
        if (!icon) return '';
        if (String(icon).trim().charAt(0) === '<') return icon;
        return '<i class="' + escapeAttr(icon) + ' ' + (className || 'icon-md') + '"></i>';
    }

    function normalizeAction(action, row) {
        if (!action || action.visible === false) return null;
        var attrs = Object.assign({}, action.attrs || {});
        if (action.key && !attrs['data-row-action']) attrs['data-row-action'] = action.key;
        if (row?.id && !attrs['data-id']) attrs['data-id'] = row.id;
        if (action.confirm != null && !attrs['data-confirm']) attrs['data-confirm'] = action.confirm;
        if (action.endpoint && !attrs['data-endpoint']) attrs['data-endpoint'] = action.endpoint;
        if (action.method && !attrs['data-method']) attrs['data-method'] = action.method;
        return Object.assign({}, action, { attrs: attrs });
    }

    function getTenantId() {
        return (window.CurrentUser || {}).tenantId || null;
    }

    function getAuthHeaders(includeJson) {
        var headers = {};
        var tenantId = getTenantId();
        if (tenantId) headers['X-Tenant-Id'] = tenantId;
        if (includeJson) headers['Content-Type'] = 'application/json';
        return headers;
    }

    // Turns any of our list envelopes into the ROW ARRAY DataTables expects.
    // The `items` branch is not optional: the standard list envelope is
    // `{ data: { totalCount, items: [...] } }`, and without it the `[json.data]`
    // fallback below hands DataTables ONE row that is the envelope itself — which
    // surfaces as "Requested unknown parameter '<field>' for row 0" on every column,
    // because the envelope has none of the item's fields. Keep `items` ahead of that
    // fallback; the fallback is only for a single-object payload.
    function unwrapResponseData(json) {
        if (json?.data?.data) return json.data.data;
        if (Array.isArray(json?.data?.items)) return json.data.items;
        if (Array.isArray(json?.items)) return json.items;
        if (json?.data) return Array.isArray(json.data) ? json.data : [json.data];
        return Array.isArray(json) ? json : [];
    }

    function renderStatusBadge(value, map) {
        var status = (map || {})[String(!!value)] || { title: value ? 'Active' : 'Passive', class: 'bg-label-primary' };
        return '<span class="badge ' + status.class + '">' + (status.title || '') + '</span>';
    }

    function renderActions(actions) {
        var visibleActions = Array.isArray(actions) ? actions.map(normalizeAction).filter(Boolean) : [];
        if (!visibleActions.length) return '';

        var primary = visibleActions[0];
        var menuItems = visibleActions.slice(1).map(function (action) {
            var attrs = renderAttrs(action.attrs);
            var icon = renderIcon(action.icon, 'dt-action-icon');
            return '<a href="javascript:void(0);" class="dropdown-item dt-action-item ' + (action.className || '') + '" ' + attrs + '>' + icon + escapeHtml(action.text || '') + '</a>';
        }).join('');

        var primaryAttrs = renderAttrs(primary.attrs);
        var primaryButtonClass = primary.buttonClass || primary.className || '';

        return [
            '<div class="d-flex align-items-center">',
            '<a href="javascript:;" class="btn btn-icon ' + primaryButtonClass + '" ' + primaryAttrs + '>' + renderIcon(primary.icon) + '</a>',
            menuItems
                ? '<a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a><div class="dropdown-menu dropdown-menu-end m-0">' + menuItems + '</div>'
                : '',
            '</div>'
        ].join('');
    }

    function parseRowJson(trigger) {
        var raw = trigger?.getAttribute?.('data-json');
        if (!raw) return null;
        try { return JSON.parse(raw.replace(/&#39;/g, "'")); } catch (e) { return null; }
    }

    function resolveRowData(trigger, dt) {
        var row = parseRowJson(trigger);
        if (row) return row;
        var rowEl = trigger?.closest?.('tr');
        if (rowEl?.classList.contains('child')) rowEl = rowEl.previousElementSibling;
        try { return rowEl && dt?.row ? dt.row(rowEl).data() : null; } catch (e) { return null; }
    }

    function bindActionDispatcher(options) {
        var tableEl = options?.tableEl;
        var handlers = options?.onRowAction || {};
        var selector = options?.rowActionSelector || '[data-row-action]';
        if (!tableEl || tableEl.dataset.ditenRowActionsBound === '1') return;
        tableEl.dataset.ditenRowActionsBound = '1';

        document.addEventListener('click', function (event) {
            var trigger = event.target.closest(selector);
            if (!trigger) return;

            var inTable = !!trigger.closest('#' + tableEl.id);
            var inResponsiveModal = !!trigger.closest('.modal.dtr-bs-modal');
            if (!inTable && !inResponsiveModal) return;

            var actionKey = trigger.getAttribute('data-row-action');
            var handler = handlers[actionKey];
            if (typeof handler !== 'function') return;

            event.preventDefault();
            event.stopPropagation();

            var dt = typeof options.getTable === 'function' ? options.getTable() : options.dt;
            var row = resolveRowData(trigger, dt);
            handler({
                actionKey: actionKey,
                row: row,
                id: trigger.getAttribute('data-id') || row?.id || null,
                table: dt,
                event: event,
                trigger: trigger
            });
        });
    }

    function bindBulkActions(tableEl, dt, options) {
        var bulkBar = document.querySelector(options?.bulkBarSelector || '#bulkActionBar');
        var handlers = options?.onBulkAction || {};
        var selector = options?.bulkActionSelector || '[data-bulk-action]';
        if (!bulkBar || bulkBar.dataset.ditenBulkActionsBound === '1') return;
        bulkBar.dataset.ditenBulkActionsBound = '1';

        bulkBar.addEventListener('click', function (event) {
            var trigger = event.target.closest(selector);
            if (!trigger) return;

            var actionKey = trigger.getAttribute('data-bulk-action');
            var handler = handlers[actionKey];
            if (typeof handler !== 'function') return;

            event.preventDefault();
            event.stopPropagation();

            var selectedIds = getSelectedIds(tableEl, options?.checkboxSelector);
            if (trigger.getAttribute('data-requires-selection') !== 'false' && !selectedIds.length) return;

            handler({
                actionKey: actionKey,
                ids: selectedIds,
                table: dt,
                event: event,
                trigger: trigger
            });
        });
    }

    function updateBulkBar(tableEl, options) {
        var selectedIds = getSelectedIds(tableEl, options?.checkboxSelector);
        var bulkBar = document.querySelector(options?.bulkBarSelector || '#bulkActionBar');
        var bulkCount = document.querySelector(options?.bulkCountSelector || '#bulkSelectedCount');
        if (bulkBar) bulkBar.classList.toggle('d-none', selectedIds.length === 0);
        if (bulkCount) bulkCount.textContent = String(selectedIds.length);

        var headerCb = tableEl?.querySelector(options?.selectAllSelector || '.dt-checkboxes-select-all');
        if (!headerCb) return selectedIds;

        var total = tableEl.querySelectorAll('tbody ' + (options?.checkboxSelector || '.dt-checkboxes')).length;
        headerCb.checked = selectedIds.length > 0 && selectedIds.length === total;
        headerCb.indeterminate = selectedIds.length > 0 && selectedIds.length < total;
        return selectedIds;
    }

    function getSelectedIds(tableEl, checkboxSelector) {
        return Array.from(tableEl?.querySelectorAll((checkboxSelector || '.dt-checkboxes') + ':checked') || [])
            .map(function (cb) { return cb.value; });
    }

    function clearSelection(tableEl, options) {
        tableEl?.querySelectorAll(options?.checkboxSelector || '.dt-checkboxes').forEach(function (cb) {
            cb.checked = false;
            cb.closest('tr')?.classList.remove('selected');
        });
        var headerCb = tableEl?.querySelector(options?.selectAllSelector || '.dt-checkboxes-select-all');
        if (headerCb) {
            headerCb.checked = false;
            headerCb.indeterminate = false;
        }
        updateBulkBar(tableEl, options);
    }

    function bindBulkSelection(tableEl, dt, options) {
        if (!window.jQuery || !tableEl || tableEl.dataset.ditenBulkBound === '1') return;
        tableEl.dataset.ditenBulkBound = '1';

        $(tableEl).on('change', options?.checkboxSelector || '.dt-checkboxes', function () {
            $(this).closest('tr').toggleClass('selected', this.checked);
            updateBulkBar(tableEl, options);
        });

        $(tableEl).on('change', options?.selectAllSelector || '.dt-checkboxes-select-all', function () {
            var checked = this.checked;
            tableEl.querySelectorAll('tbody ' + (options?.checkboxSelector || '.dt-checkboxes')).forEach(function (cb) {
                cb.checked = checked;
                cb.closest('tr')?.classList.toggle('selected', checked);
            });
            updateBulkBar(tableEl, options);
        });

        document.querySelector(options?.clearSelectionSelector || '#btnClearSelection')?.addEventListener('click', function () {
            clearSelection(tableEl, options);
        });

        dt?.on?.('draw.dt', function () {
            updateBulkBar(tableEl, options);
        });

        bindBulkActions(tableEl, dt, options);
    }

    function reloadWithToast(dt, tableEl, messageKey, interpolationValue, options) {
        clearSelection(tableEl, options);
        dt.ajax.reload(function () {
            var l = window.L10n || {};
            var msg = interpolationValue
                ? (l[messageKey] || '').replace('{0}', interpolationValue)
                : (l[messageKey] || messageKey);
            window.showToast?.(msg, 'success');
        }, false);
    }

    function createCrudTable(options) {
        if (!options?.tableEl) return null;
        if (options.dataMode !== undefined) assertDataMode(options.dataMode);
        if (!window.DtDefaults) throw new Error('DtDefaults is required before DitenDataTable.createCrudTable.');

        var ajax = options.ajax || {};
        var config = Object.assign({}, options.config || {}, {
            ajax: Object.assign({}, ajax, {
                dataSrc: ajax.dataSrc || unwrapResponseData,
                headers: Object.assign({}, getAuthHeaders(), ajax.headers || {})
            })
        });

        var dt = new DataTable(options.tableEl, window.DtDefaults.create(config));
        bindBulkSelection(options.tableEl, dt, options.bulk || {});
        bindActionDispatcher(Object.assign({}, options.actions || {}, {
            tableEl: options.tableEl,
            dt: dt
        }));
        return dt;
    }


    // ═══════════════════════════════════════════════════════════════════════════════════════════════════════
    // THE LIST FACTORY — behaviour in one place, the page writes only its own business (BL-440 package 2).
    //
    // MEASURED 2026-09-23: Golden Slim index.js 991 lines, Compact 685, Users 1162; 79 of the 108 top-level
    // names in Users were copied from Slim — the Save View state machine, column visibility/order, filter
    // normalisation, the inline filter bar, the responsive-modal return, form submission, offcanvas instances.
    // Every screen rewrote them; wherever the copies diverged a bug was born. Owner decision: the list is a
    // component. The page hands over columns, renderers, `populate`, its form fields and its endpoints; the
    // factory owns everything below. Package 1 made the markup a component (_ListShell); this is the JS half.
    //
    // ⚠ WHY `createList` AND NOT A HEAVIER `createCrudTable`. 84 files call `createCrudTable` today without a
    // data mode. Making that call throw would take 82 screens down in one line — the very failure mode BL-440
    // opens with (one line in dt-defaults.js dropped 138 pages). So the fail-closed contract lives on the
    // factory entry, `createList`, which every migrated page uses; `createCrudTable` stays the thin wrapper the
    // un-migrated pages already depend on (it validates `dataMode` when handed one, and never invents one).
    //
    // ⚠ NO ESM, NO NEW GLOBAL, NO `global.`. Everything hangs off window.DitenDataTable and runs with a
    // browser's globals and nothing more (tests/list-factory-runs-in-a-browser.test.js).
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════════

    var DATA_MODES = ['client', 'server'];

    function assertDataMode(mode) {
        if (mode === undefined || mode === null || mode === '') {
            throw new Error("DitenDataTable: dataMode is required ('client' | 'server') — an undeclared list is what the data_mode rule exists to catch.");
        }
        if (DATA_MODES.indexOf(mode) === -1) {
            throw new Error("DitenDataTable: dataMode must be 'client' | 'server', got '" + mode + "'.");
        }
        return mode;
    }

    function L() { return window.L10n || {}; }

    // ── normalize() — frontend-datatable-template.md "normalize() Standardı (Mekanik)" ──────────────────
    function normalizeScalar(value) {
        if (value === null || value === undefined) return '';
        if (typeof value === 'boolean') return value ? 'true' : 'false';
        if (typeof value === 'number') return Number.isFinite(value) ? String(value) : '';
        return String(value).trim();
    }

    function normalizeArray(value) {
        var list = Array.isArray(value) ? value : [value];
        var seen = [];
        list.forEach(function (item) {
            var s = normalizeScalar(item);
            if (s && seen.indexOf(s) === -1) seen.push(s);
        });
        return seen;
    }

    function normalizeOrder(order, fallback) {
        if (!Array.isArray(order) || !order.length) return fallback;
        var pairs = order
            .filter(function (pair) { return Array.isArray(pair) && pair.length >= 2; })
            .map(function (pair) { return [Number(pair[0]), String(pair[1]).toLowerCase()]; })
            .filter(function (pair) { return Number.isInteger(pair[0]) && (pair[1] === 'asc' || pair[1] === 'desc'); });
        return pairs.length ? pairs : fallback;
    }

    /**
     * The view-state machine of one list: what is captured, how it is normalised, what "dirty" means.
     * Pure — no DOM, no network — so the standard can be measured directly (tests/list-factory-state.test.js).
     *
     * spec = { fields: [{ key, kind: 'multi'|'single'|'text' }], saveViewColumnIndexes, defaultVisibleColumnIndexes,
     *          totalColumnCount, baseOrder }
     */
    function createViewState(spec) {
        var fields = Array.isArray(spec?.fields) ? spec.fields : [];
        var saveViewColumnIndexes = Array.isArray(spec?.saveViewColumnIndexes) ? spec.saveViewColumnIndexes.slice() : [];
        var defaultVisible = Array.isArray(spec?.defaultVisibleColumnIndexes) ? spec.defaultVisibleColumnIndexes : saveViewColumnIndexes;
        var totalColumnCount = Number(spec?.totalColumnCount) || 0;
        var baseOrder = normalizeOrder(spec?.baseOrder, [[2, 'asc']]);

        function isMulti(field) { return field.kind === 'multi'; }

        function emptyFilters() {
            var out = {};
            fields.forEach(function (field) { out[field.key] = isMulti(field) ? [] : ''; });
            return out;
        }

        function normalizeFilters(filters) {
            var source = filters || {};
            var out = {};
            fields.forEach(function (field) {
                out[field.key] = isMulti(field) ? normalizeArray(source[field.key]) : normalizeScalar(source[field.key]);
            });
            return out;
        }

        function hasFilterValue(value) {
            return Array.isArray(value) ? normalizeArray(value).length > 0 : normalizeScalar(value).length > 0;
        }

        function appliedFilterCount(filters) {
            var n = normalizeFilters(filters);
            return fields.filter(function (field) { return hasFilterValue(n[field.key]); }).length;
        }

        function defaultColVis() {
            var out = {};
            saveViewColumnIndexes.forEach(function (ci) { out[ci] = defaultVisible.indexOf(ci) !== -1; });
            return out;
        }

        function normalizeColVis(colVis) {
            if (!colVis) return null;
            var out = {};
            if (Array.isArray(colVis)) {
                saveViewColumnIndexes.forEach(function (ci, pos) {
                    if (typeof colVis[ci] === 'boolean') out[ci] = colVis[ci];
                    else if (typeof colVis[pos] === 'boolean') out[ci] = colVis[pos];
                });
            } else if (typeof colVis === 'object') {
                saveViewColumnIndexes.forEach(function (ci) { if (typeof colVis[ci] === 'boolean') out[ci] = colVis[ci]; });
            }
            return Object.keys(out).length ? out : null;
        }

        function identityOrder() {
            return Array.from({ length: totalColumnCount }, function (_, i) { return i; });
        }

        function normalizeColOrder(order) {
            if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
            var n = order.map(Number).filter(function (i) { return Number.isInteger(i) && i >= 0 && i < totalColumnCount; });
            return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
        }

        function normalizeViewState(view) {
            return {
                filters: normalizeFilters(view?.filters),
                search: normalizeScalar(view?.search),
                colVis: normalizeColVis(view?.colVis) || defaultColVis(),
                columnOrder: normalizeColOrder(view?.columnOrder) || identityOrder(),
                order: normalizeOrder(view?.order, baseOrder)
            };
        }

        function serializeView(view) {
            var n = normalizeViewState(view);
            var sortedFilters = {};
            Object.keys(n.filters).sort().forEach(function (key) { sortedFilters[key] = n.filters[key]; });
            var sortedColVis = {};
            Object.keys(n.colVis).sort(function (a, b) { return Number(a) - Number(b); }).forEach(function (key) { sortedColVis[key] = n.colVis[key]; });
            return JSON.stringify({ filters: sortedFilters, search: n.search, colVis: sortedColVis, columnOrder: n.columnOrder, order: n.order });
        }

        function baseline() {
            return normalizeViewState({ filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: identityOrder(), order: baseOrder });
        }

        function isDirty(applied, saved) {
            return serializeView(applied) !== serializeView(saved || baseline());
        }

        // Saved-view records come back in either casing and with the definition as an object or a JSON string.
        function fromSavedRecord(record) {
            var raw = record?.viewDefinition ?? record?.ViewDefinition ?? {};
            if (typeof raw === 'string') { try { raw = JSON.parse(raw); } catch (e) { raw = {}; } }
            raw = raw || {};
            return {
                filters: normalizeFilters(raw.filters || raw),
                search: normalizeScalar(raw.search),
                colVis: normalizeColVis(raw.colVis),
                columnOrder: normalizeColOrder(raw.columnOrder),
                order: Array.isArray(raw.order) ? raw.order : null
            };
        }

        return {
            fields: fields,
            saveViewColumnIndexes: saveViewColumnIndexes,
            totalColumnCount: totalColumnCount,
            baseOrder: baseOrder,
            emptyFilters: emptyFilters,
            normalizeFilters: normalizeFilters,
            hasFilterValue: hasFilterValue,
            appliedFilterCount: appliedFilterCount,
            defaultColVis: defaultColVis,
            normalizeColVis: normalizeColVis,
            normalizeColOrder: normalizeColOrder,
            identityOrder: identityOrder,
            normalizeViewState: normalizeViewState,
            serializeView: serializeView,
            baseline: baseline,
            isDirty: isDirty,
            fromSavedRecord: fromSavedRecord
        };
    }

    // ── DataTable ↔ view state ──────────────────────────────────────────────────────────────────────────
    function searchInput(api) {
        try { return api.table().container().querySelector('.dt-search input'); } catch (e) { return null; }
    }

    function captureView(api, state, appliedFilters) {
        var colVis = {};
        state.saveViewColumnIndexes.forEach(function (ci) { try { colVis[ci] = !!api.column(ci).visible(); } catch (e) { } });
        var columnOrder = null;
        try { columnOrder = state.normalizeColOrder(api.colReorder?.order?.()); } catch (e) { }
        return {
            filters: Object.assign({}, appliedFilters),
            search: normalizeScalar(searchInput(api)?.value || api.search()),
            colVis: colVis,
            columnOrder: columnOrder,
            order: api.order()
        };
    }

    function applyViewToTable(api, state, view) {
        var s = state.normalizeViewState(view);
        if (typeof api.colReorder?.order === 'function') api.colReorder.order(s.columnOrder, true);
        state.saveViewColumnIndexes.forEach(function (ci) {
            if (typeof s.colVis[ci] === 'boolean') { try { api.column(ci).visible(s.colVis[ci], false); } catch (e) { } }
        });
        api.search(s.search);
        var input = searchInput(api);
        if (input) input.value = s.search;
        api.order(s.order);
        try { api.columns.adjust(); } catch (e) { }
        try { api.responsive?.recalc?.(); } catch (e) { }
        api.draw(false);
        return s;
    }

    // ── Saved view persistence — only ever through the shared personalizationClient ─────────────────────
    function createSavedViewStore(context, state) {
        var record = null;
        var saved = null;
        var client = function () { return window.personalizationClient; };
        var id = function (r) { return r?.id || r?.Id || r?._id || null; };

        return {
            get saved() { return saved; },
            load: async function () {
                record = null;
                saved = null;
                if (!client()?.getViews) return null;
                try {
                    var views = await client().getViews(context.moduleKey, context.pageKey);
                    var items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
                    record = Array.isArray(items)
                        ? (items.find(function (v) { return v?.isDefault === true || v?.IsDefault === true; }) || items[0] || null)
                        : null;
                    saved = record ? state.fromSavedRecord(record) : null;
                    return saved;
                } catch (error) {
                    if (error?.authHandled) return null;
                    console.error('[' + context.pageKey + ' SaveView] Failed to load saved views.', error);
                    return null;
                }
            },
            save: async function (view) {
                if (!client()?.saveView) return null;
                var normalized = state.normalizeViewState(view);
                var payload = {
                    moduleKey: context.moduleKey,
                    pageKey: context.pageKey,
                    viewName: (record?.viewName || record?.ViewName || L().SaveView || 'Default').trim(),
                    viewDefinition: normalized,
                    isDefault: true,
                    visibility: 'private'
                };
                var existingId = id(record);
                var response = existingId ? await client().updateView(existingId, payload) : await client().saveView(payload);
                var savedRecord = response?.data || response?.Data || response;
                record = savedRecord && typeof savedRecord === 'object' ? savedRecord : Object.assign({}, record || {}, payload);
                saved = normalized;
                return saved;
            }
        };
    }

    // ── Inline filter bar — frontend-ui-ux 24–25, the same on every list ────────────────────────────────
    function clampDropdown() {
        requestAnimationFrame(function () {
            var dd = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
            if (!dd) return;
            var rect = dd.getBoundingClientRect();
            var pad = 8;
            var dx = 0, dy = 0;
            if (rect.right > window.innerWidth - pad) dx -= rect.right - (window.innerWidth - pad);
            if (rect.left < pad) dx += pad - rect.left;
            if (rect.bottom > window.innerHeight - pad) dy -= rect.bottom - (window.innerHeight - pad);
            if (rect.top < pad) dy += pad - rect.top;
            if (!dx && !dy) return;
            var cs = window.getComputedStyle(dd);
            var baseLeft = parseFloat(cs.left) || rect.left + window.scrollX;
            var baseTop = parseFloat(cs.top) || rect.top + window.scrollY;
            if (dx) dd.style.left = (baseLeft + dx) + 'px';
            if (dy) dd.style.top = (baseTop + dy) + 'px';
            dd.style.transform = 'none';
        });
    }

    function syncMultiSelectSummary($select) {
        var $container = $select.next('.select2-container');
        var $rendered = $container.find('.select2-selection__rendered');
        var $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;

        var $summary = $selection.find('.dt-inline-filter-multi__summary');
        var $actions = $selection.find('.dt-inline-filter-multi__actions');
        var $count = $selection.find('.dt-inline-filter-multi__count');
        var $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) { $summary = $('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = $('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }

        var placeholder = normalizeScalar($select.data('placeholder'));
        var selectedValues = normalizeArray($select.val());
        var selectedTexts = ($select.select2('data') || []).map(function (i) { return normalizeScalar(i.text); }).filter(Boolean);

        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));

        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            var $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + escapeAttr(L().Reset || '') + '" title="' + escapeAttr(L().Reset || '') + '">&times;</span>');
            $clearBtn.on('mousedown', function (e) { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clearBtn);
        }
    }

    function initSelect2Field(field) {
        if (!window.jQuery || !$.fn.select2) return;
        var $s = $('#' + field.id);
        if (!$s.length || field.kind === 'text') return;
        if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
        var common = {
            dropdownParent: $(document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            selectionCssClass: 'form-select form-select-sm',
            placeholder: $s.data('placeholder') || '',
            minimumResultsForSearch: Infinity,
            width: 'element'
        };
        if (field.kind === 'multi') {
            $s.select2(Object.assign(common, { containerCssClass: 'dt-inline-filter-multi', closeOnSelect: false }));
            $s.on('change.select2-summary', function () { syncMultiSelectSummary($s); });
            requestAnimationFrame(function () { syncMultiSelectSummary($s); });
        } else {
            $s.select2(Object.assign(common, { allowClear: true }));
        }
        $s.on('select2:open', clampDropdown);
    }

    function readStagedFilters(fields) {
        var out = {};
        fields.forEach(function (field) {
            var el = document.getElementById(field.id);
            if (field.kind === 'multi') out[field.key] = (window.jQuery ? $(el).val() : null) || [];
            else out[field.key] = el?.value || '';
        });
        return out;
    }

    function writeFilterControls(fields, values) {
        fields.forEach(function (field) {
            var el = document.getElementById(field.id);
            if (!el) return;
            var value = field.kind === 'multi' ? normalizeArray(values[field.key]) : (values[field.key] || '');
            if (window.jQuery) $(el).val(value).trigger('change');
            else el.value = value;
        });
    }

    function defaultMatcher(field) {
        return function (row, value) {
            var actual = normalizeScalar(row?.[field.key]);
            if (field.kind === 'multi') {
                var wanted = normalizeArray(value);
                return !wanted.length || wanted.indexOf(actual) !== -1;
            }
            var single = normalizeScalar(value);
            return !single || actual === single;
        };
    }

    function normalizeFields(fields) {
        return (Array.isArray(fields) ? fields : []).map(function (field) {
            var id = String(field.id || '');
            var key = field.key || (id.indexOf('filter') === 0 ? id.charAt(6).toLowerCase() + id.slice(7) : id);
            var out = { id: id, key: key, kind: field.kind || 'single' };
            out.matches = typeof field.matches === 'function' ? field.matches : defaultMatcher(out);
            return out;
        });
    }

    function mountInlineFilter(hostId) {
        var host = document.getElementById(hostId);
        var filterBtn = document.querySelector('.dt-filter-btn');
        var toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    }

    function collapseOf(collapseId) {
        var el = document.getElementById(collapseId);
        return el ? bootstrap.Collapse.getOrCreateInstance(el, { toggle: false }) : null;
    }

    function bindInlineFilterA11y(collapseId) {
        var btn = document.querySelector('.dt-filter-btn');
        var collapseEl = document.getElementById(collapseId);
        if (!btn || !collapseEl || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        collapseEl.addEventListener('shown.bs.collapse', function () { btn.setAttribute('aria-expanded', 'true'); });
        collapseEl.addEventListener('hidden.bs.collapse', function () { btn.setAttribute('aria-expanded', 'false'); });
    }

    // ── Responsive modal return: an offcanvas opened FROM the responsive row modal gives it back on cancel ──
    function createResponsiveReturn() {
        var returnModalEl = null;
        var suppress = false;
        return {
            close: function (returnOnClose) {
                var modalEl = document.querySelector('.modal.dtr-bs-modal.show');
                if (!modalEl) return false;
                if (returnOnClose) { returnModalEl = modalEl; suppress = false; }
                var modal = bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
                else modalEl.querySelector('[data-bs-dismiss="modal"], .btn-close')?.click();
                return true;
            },
            suppress: function () { suppress = true; returnModalEl = null; },
            restore: function () {
                if (!returnModalEl || suppress) { returnModalEl = null; suppress = false; return; }
                var modalEl = returnModalEl;
                returnModalEl = null;
                window.setTimeout(function () { bootstrap.Modal.getOrCreateInstance(modalEl).show(); }, 120);
            }
        };
    }

    function offcanvasOf(id) {
        var el = document.getElementById(id);
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    }

    function fillSelect(select, items, options) {
        if (!select) return;
        select.innerHTML = '';
        if (options?.showAllText !== undefined) {
            var showAll = document.createElement('option');
            showAll.value = '';
            showAll.textContent = options.showAllText || '';
            select.appendChild(showAll);
        }
        (Array.isArray(items) ? items : []).forEach(function (item) {
            if (item?.value === null || item?.value === undefined || item.value === '') return;
            var opt = document.createElement('option');
            opt.value = String(item.value);
            opt.textContent = item.text || String(item.value);
            select.appendChild(opt);
        });
    }

    // ── Server mode: the wire contract (WP-UI-LIST-SERVER-01, BL-440 package 3) ─────────────────────────────
    //
    // REQUEST. DataTables' serverSide request (`start, length, search{value}, order[{column,dir}], columns[{data,…}]`,
    // `draw`) becomes ONE flat query: start, length, search, orderBy (the sorted column's `data` name), orderDir
    // (asc|desc), draw, and every APPLIED filter by its key — a multi filter as a repeated parameter
    // (status=Active&status=Passive), a single one once (priority=70). DataTables' `columns[i][…]` noise is never sent:
    // the service reads names, not a positional column dump. Built as a string so jQuery sends exactly this.
    //
    // RESPONSE. The service envelope `{ …, data: { items, total, filteredTotal } }` becomes what DataTables reads:
    // `{ draw, recordsTotal: total, recordsFiltered: filteredTotal, data: items }`. `filteredTotal` > rows is paging.
    function toServerQuery(dtRequest, fields, appliedFilters) {
        var parts = [];
        var add = function (key, value) { parts.push(encodeURIComponent(key) + '=' + encodeURIComponent(value)); };
        var d = dtRequest || {};
        add('start', Math.max(0, Number(d.start) || 0));
        add('length', Number(d.length) > 0 ? Number(d.length) : 10);
        var search = normalizeScalar(d.search && typeof d.search === 'object' ? d.search.value : d.search);
        if (search) add('search', search);
        var first = Array.isArray(d.order) ? d.order[0] : null;
        var column = first && Array.isArray(d.columns) ? d.columns[Number(first.column)] : null;
        // A column the page declared not orderable (control, checkbox, actions) never becomes a sort key.
        if (column && column.orderable !== false && typeof column.data === 'string' && column.data) {
            add('orderBy', column.data);
            add('orderDir', String(first.dir).toLowerCase() === 'desc' ? 'desc' : 'asc');
        }
        add('draw', Number(d.draw) || 0);
        var filters = appliedFilters || {};
        (Array.isArray(fields) ? fields : []).forEach(function (field) {
            if (field.kind === 'multi') {
                normalizeArray(filters[field.key]).forEach(function (value) { add(field.key, value); });
            } else {
                var single = normalizeScalar(filters[field.key]);
                if (single) add(field.key, single);
            }
        });
        return parts.join('&');
    }

    function toDataTablesResponse(json, draw) {
        var page = json && typeof json === 'object' ? (json.data || json.Data || {}) : {};
        var items = Array.isArray(page.items) ? page.items : (Array.isArray(page.Items) ? page.Items : []);
        var total = Number(page.total ?? page.Total);
        var filteredTotal = Number(page.filteredTotal ?? page.FilteredTotal);
        if (!Number.isFinite(total) || !Number.isFinite(filteredTotal)) {
            // A missing count is a broken server contract, not a zero: say so, and show what did arrive.
            console.error('[DitenDataTable] server-mode response carries no total/filteredTotal — the pager cannot be trusted.', json);
        }
        return {
            draw: Number(draw) || 0,
            recordsTotal: Number.isFinite(total) ? total : items.length,
            recordsFiltered: Number.isFinite(filteredTotal) ? filteredTotal : items.length,
            data: items
        };
    }

    // jQuery has already appended the query to the GET url when dataFilter runs; the draw is read back from it so an
    // out-of-order answer keeps ITS draw number (DataTables drops a response older than the latest request).
    function drawOfRequest(ajaxSettings, fallback) {
        var text = String(ajaxSettings?.url || '') + '&' + (typeof ajaxSettings?.data === 'string' ? ajaxSettings.data : '');
        var match = /[?&]draw=(\d+)/.exec(text);
        return match ? Number(match[1]) : fallback;
    }

    /**
     * createList(options) — the list component. Returns a Promise of the list handle.
     *
     * options = {
     *   tableEl, dataMode ('client' | 'server'), ajax, bulk, actions, config (columns/columnDefs/…),
     *   onResponse(json) — server mode: called with every list envelope (the page's summary/KPI source);
     *                      the last one is also on handle.lastResponse.
     *   toolbar:   { addNewText, addNewAttr, onAddNew, exportColumns, colvisColumns, extraButtons },
     *   filters:   { hostId, collapseId, applyBtn, resetBtn, fields: [{ id, key, kind, matches(row, value) }], loadOptions() },
     *   savedView: { moduleKey, pageKey, saveViewColumnIndexes, defaultVisibleColumnIndexes, baseOrder },
     *   quickView: { offcanvasId, populate(row), actionKey ('quickView') },
     *   form:      { formId, offcanvasId, alertId, saveBtnId, labelId, submit(payload, isEdit, ctx), load(id, ctx), reset(), actionKey ('edit') }
     * }
     */
    async function createList(options) {
        if (!options?.tableEl) throw new Error('DitenDataTable.createList: tableEl is required.');
        assertDataMode(options.dataMode);
        if (!window.DtDefaults) throw new Error('DtDefaults is required before DitenDataTable.createList.');

        var tableEl = options.tableEl;
        var filters = options.filters || {};
        var fields = normalizeFields(filters.fields);
        var hostId = filters.hostId || 'inlineFilterHost';
        var collapseId = filters.collapseId || 'inlineFilterCollapse';
        var savedViewSpec = options.savedView || {};
        var totalColumnCount = Array.isArray(options.config?.columns) ? options.config.columns.length : Number(savedViewSpec.totalColumnCount) || 0;
        var state = createViewState({
            fields: fields,
            saveViewColumnIndexes: savedViewSpec.saveViewColumnIndexes,
            defaultVisibleColumnIndexes: savedViewSpec.defaultVisibleColumnIndexes,
            totalColumnCount: totalColumnCount,
            baseOrder: savedViewSpec.baseOrder
        });
        var store = createSavedViewStore({ moduleKey: savedViewSpec.moduleKey, pageKey: savedViewSpec.pageKey }, state);
        var responsive = createResponsiveReturn();
        var appliedFilters = state.emptyFilters();
        var armed = false;
        var dt = null;
        var handle = {};
        var isServer = options.dataMode === 'server';
        var lastDraw = 0;

        function filterCount() { return state.appliedFilterCount(appliedFilters); }
        function refreshVisual(api) { window.DtDefaults.updateVisualState(api || dt, filterCount()); }
        function setSaveVisible(visible) {
            var btn = document.querySelector('.dt-save-filter-btn');
            if (!btn) return;
            btn.classList.toggle('d-none', !visible);
            window.DtDefaults.refreshButtonGroupRadii?.();
        }
        function syncDirty(api) {
            if (!armed) return;
            setSaveVisible(state.isDirty(captureView(api || dt, state, appliedFilters), store.saved));
        }
        function applyState(api, view) {
            /*
             * ⚠ ORDER MATTERS (measured live, 2026-09-23): applyViewToTable ends in api.draw(), and that draw fires the
             * search/order/column events which recompute the dirty state from `appliedFilters`. Assigning the filters
             * AFTER the draw meant the recompute saw the old (empty) filters against the saved view and showed the
             * Save View button on a freshly loaded, un-dirty page. The filters are the first thing set now.
             */
            var s = state.normalizeViewState(view);
            appliedFilters = s.filters;
            applyViewToTable(api, state, s);
            writeFilterControls(fields, appliedFilters);
            refreshVisual(api);
            syncDirty(api);
        }

        // The client-side filter hook — registered ONCE per table, scoped to this table only. CLIENT MODE ONLY: in
        // server mode the filters travel as query parameters and the browser never filters a row (a hook here would
        // filter the current page a second time, and silently, on whatever the server did not already drop).
        if (!isServer && fields.length && window.jQuery?.fn?.dataTable?.ext?.search && tableEl.dataset.ditenFilterBound !== '1') {
            tableEl.dataset.ditenFilterBound = '1';
            $.fn.dataTable.ext.search.push(function (settings, _sd, dataIndex, rowData) {
                if (settings.nTable !== tableEl) return true;
                var row = rowData || dt?.row(dataIndex)?.data?.() || null;
                if (!row) return true;
                return fields.every(function (field) { return field.matches(row, appliedFilters[field.key]); });
            });
        }

        // `await loadDefaultView()` BEFORE the table is built (State Standard).
        await store.load();
        // Server mode: the FIRST request already carries the saved filters (else the page asks for, and shows, an
        // unfiltered page before the saved view lands).
        if (isServer && store.saved) appliedFilters = state.normalizeFilters(store.saved.filters);

        var toolbar = options.toolbar || {};
        var l = L();
        var extraButtons = Object.assign({
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: l.Import, 'data-bs-toggle': 'tooltip' },
                action: function () { window.showToast?.(l.ComingSoon, 'warning'); }
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: l.Filter, 'aria-controls': collapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: function () { collapseOf(collapseId)?.toggle(); }
            },
            // ALWAYS rendered; `d-none` until the applied state is dirty against the saved view / baseline.
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (l.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: l.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    var tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        await store.save(captureView(tableApi, state, appliedFilters));
                        setSaveVisible(false);
                        window.showToast?.(l.RecordSaved || l.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[' + (savedViewSpec.pageKey || tableEl.id) + ' SaveView] Failed to save default view.', error);
                        window.showToast?.(l.ErrorOccurred || '', 'error');
                    }
                }
            }
        }, toolbar.extraButtons || {});

        var pageConfig = options.config || {};
        var pageInitComplete = pageConfig.initComplete;
        var pageDrawCallback = pageConfig.drawCallback;
        var config = Object.assign({}, pageConfig, {
            // Persistence is ONLY the Save View button: no automatic 2-hour state restore (State Standard).
            stateSave: false,
            colReorder: pageConfig.colReorder || { columns: ':gt(1):not(:last-child)' },
            buttons: pageConfig.buttons || window.DtDefaults.exportButtons(
                toolbar.addNewText !== undefined ? toolbar.addNewText : l.AddNew,
                toolbar.addNewAttr || {},
                extraButtons,
                { exportColumns: toolbar.exportColumns, colvisColumns: toolbar.colvisColumns }
            ),
            initComplete: function (settings, json) {
                var api = this.api();
                mountInlineFilter(hostId);
                bindInlineFilterA11y(collapseId);
                void setupFilters(api);
                var addNew = document.querySelector('.add-new');
                if (addNew && typeof toolbar.onAddNew === 'function') {
                    addNew.addEventListener('click', function (e) { e.preventDefault(); toolbar.onAddNew(e); });
                }
                if (typeof pageInitComplete === 'function') pageInitComplete.call(this, settings, json);
                setTimeout(function () { armed = true; }, 0);
            },
            drawCallback: function (settings) {
                refreshVisual(this.api());
                if (typeof pageDrawCallback === 'function') pageDrawCallback.call(this, settings);
            }
        });

        async function setupFilters(api) {
            if (typeof filters.loadOptions === 'function') {
                try { await filters.loadOptions({ fillSelect: fillSelect, headers: getAuthHeaders() }); }
                catch (error) { console.error('[' + (savedViewSpec.pageKey || tableEl.id) + ' Lookup] Failed.', error); }
            }
            fields.forEach(initSelect2Field);
            applyState(api, store.saved || { filters: appliedFilters });

            // Apply: staged → applied, redraw, close the panel; Save View follows the APPLIED state only.
            document.getElementById(filters.applyBtn || 'btnFilterApply')?.addEventListener('click', function () {
                appliedFilters = state.normalizeFilters(readStagedFilters(fields));
                api.draw();
                refreshVisual(api);
                syncDirty(api);
                collapseOf(collapseId)?.hide();
            });
            // Reset: NEVER back to the saved view — always the factory baseline.
            document.getElementById(filters.resetBtn || 'btnFilterReset')?.addEventListener('click', function (e) {
                e.preventDefault();
                // Server mode: the baseline is a NEW query, so it starts on page one (applyViewToTable keeps the page).
                if (isServer) api.page?.(0);
                applyState(api, state.baseline());
                syncDirty(api);
            });
        }

        // Quick view: the factory opens, closes, returns to the responsive modal and parses the row; the page populates.
        var quickView = options.quickView;
        var form = options.form;
        var rowHandlers = Object.assign({}, options.actions?.onRowAction || {});
        if (quickView?.offcanvasId) {
            rowHandlers[quickView.actionKey || 'quickView'] = rowHandlers[quickView.actionKey || 'quickView'] || function (ctx) {
                handle.showQuickView(ctx.row, ctx.trigger);
            };
            document.getElementById(quickView.offcanvasId)?.addEventListener('hidden.bs.offcanvas', responsive.restore);
        }
        if (form?.formId) {
            rowHandlers[form.actionKey || 'edit'] = rowHandlers[form.actionKey || 'edit'] || function (ctx) {
                if (ctx.id) handle.openEdit(String(ctx.id), ctx.trigger);
            };
            document.getElementById(form.offcanvasId)?.addEventListener('hidden.bs.offcanvas', responsive.restore);
            document.getElementById(form.saveBtnId || 'btnSave')?.addEventListener('click', function () { handle.submitForm(); });
        }

        var ajax = options.ajax;
        if (isServer) {
            config.serverSide = true;
            config.processing = true;
            // The FIRST request is already a real query: the saved view's (or the baseline's) order and search. Left to
            // DataTables, the first request sorts by column 0 — the control column, `data: 'id'`, which no service
            // whitelists (measured with the vendored DataTables: orderBy=id on draw 1).
            var initialState = state.normalizeViewState(store.saved || {});
            config.order = initialState.order;
            if (initialState.search) config.search = Object.assign({}, config.search || {}, { search: initialState.search });
            ajax = Object.assign({}, options.ajax || {}, {
                dataSrc: 'data',
                data: function (dtRequest) {
                    lastDraw = Number(dtRequest?.draw) || lastDraw;
                    return toServerQuery(dtRequest, fields, appliedFilters);
                },
                dataFilter: function (raw) {
                    var json;
                    try { json = typeof raw === 'string' ? JSON.parse(raw) : raw; } catch (e) { return raw; }
                    handle.lastResponse = json;
                    if (typeof options.onResponse === 'function') {
                        try { options.onResponse(json); } catch (error) { console.error('[' + (savedViewSpec.pageKey || tableEl.id) + '] onResponse failed.', error); }
                    }
                    return JSON.stringify(toDataTablesResponse(json, drawOfRequest(this, lastDraw)));
                }
            });
        }

        dt = createCrudTable({
            tableEl: tableEl,
            dataMode: options.dataMode,
            bulk: options.bulk,
            ajax: ajax,
            actions: Object.assign({}, options.actions || {}, { onRowAction: rowHandlers }),
            config: config
        });

        dt.on('column-visibility.dt column-reorder.dt columns-reordered.dt', function () { refreshVisual(dt); syncDirty(dt); });
        dt.on('search.dt order.dt', function () { syncDirty(dt); });

        function fromResponsiveModal(trigger) { return !!trigger?.closest?.('.modal.dtr-bs-modal'); }
        var editingId = null;

        function resetForm() {
            var formEl = document.getElementById(form.formId);
            if (!formEl) return;
            formEl.classList.remove('was-validated');
            formEl.querySelectorAll('.is-invalid').forEach(function (el) { el.classList.remove('is-invalid'); });
            formEl.querySelectorAll('.invalid-feedback').forEach(function (el) { el.textContent = ''; });
            document.getElementById(form.alertId)?.classList.add('d-none');
            if (typeof form.reset === 'function') form.reset(formEl);
        }

        function showFormErrors(errors) {
            var alertEl = document.getElementById(form.alertId);
            if (!alertEl) return;
            alertEl.innerHTML = Array.isArray(errors)
                ? errors.map(function (e) { return '<div>' + escapeHtml(e) + '</div>'; }).join('')
                : (errors || L().FormValidationError || '');
            alertEl.classList.remove('d-none');
        }

        function setFormTitle(isEdit) {
            var l10n = L();
            var label = document.getElementById(form.labelId || (form.offcanvasId + 'Label'));
            if (label) label.textContent = isEdit ? (l10n.FormTitleEdit || l10n.EditItem || l10n.Edit || '') : (l10n.FormTitleCreate || l10n.AddNew || '');
            var saveBtn = document.getElementById(form.saveBtnId || 'btnSave');
            if (saveBtn) saveBtn.textContent = isEdit ? (l10n.Update || l10n.Save || '') : (l10n.Save || '');
        }

        Object.assign(handle, {
            dt: dt,
            tableEl: tableEl,
            state: state,
            get appliedFilters() { return appliedFilters; },
            dataMode: options.dataMode,
            lastResponse: handle.lastResponse || null,
            reload: function (messageKey, interpolationValue) {
                reloadWithToast(dt, tableEl, messageKey, interpolationValue, options.bulk || {});
            },
            showQuickView: function (row, trigger) {
                if (!row || typeof quickView?.populate !== 'function') return;
                quickView.populate(row);
                var wasModalOpen = responsive.close(fromResponsiveModal(trigger));
                window.setTimeout(function () { offcanvasOf(quickView.offcanvasId)?.show(); }, wasModalOpen ? 160 : 0);
            },
            openCreate: function () {
                if (!form?.formId) return;
                editingId = null;
                resetForm();
                setFormTitle(false);
                offcanvasOf(form.offcanvasId)?.show();
            },
            openEdit: async function (id, trigger) {
                if (!form?.formId || !id) return;
                var wasModalOpen = responsive.close(fromResponsiveModal(trigger));
                var open = async function () {
                    editingId = id;
                    resetForm();
                    setFormTitle(true);
                    try {
                        if (typeof form.load === 'function') await form.load(id, { headers: getAuthHeaders() });
                    } catch (error) {
                        console.error('[' + (savedViewSpec.pageKey || tableEl.id) + '] Failed to load item for edit.', error);
                        window.showToast?.(L().ErrorOccurred, 'error');
                        return;
                    }
                    offcanvasOf(form.offcanvasId)?.show();
                };
                if (wasModalOpen) window.setTimeout(open, 160); else await open();
            },
            submitForm: async function () {
                var formEl = document.getElementById(form?.formId);
                if (!formEl) return;
                formEl.classList.add('was-validated');
                if (!formEl.checkValidity()) { showFormErrors([L().FormValidationError || '']); return; }
                var isEdit = !!editingId;
                var saveBtn = document.getElementById(form.saveBtnId || 'btnSave');
                if (saveBtn) saveBtn.disabled = true;
                try {
                    var token = formEl.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                    var headers = Object.assign({ 'RequestVerificationToken': token }, getAuthHeaders());
                    var json = await form.submit(new FormData(formEl), isEdit, { editingId: editingId, headers: headers });
                    if (json?.success) {
                        responsive.suppress();
                        offcanvasOf(form.offcanvasId)?.hide();
                        handle.reload(isEdit ? 'RecordUpdated' : 'RecordCreated');
                    } else {
                        showFormErrors(json?.errors);
                    }
                } catch (error) {
                    console.error('[' + (savedViewSpec.pageKey || tableEl.id) + '] Form submit failed.', error);
                    showFormErrors([L().ErrorOccurred]);
                } finally {
                    if (saveBtn) saveBtn.disabled = false;
                }
            }
        });

        return handle;
    }

    return {
        bindActionDispatcher: bindActionDispatcher,
        bindBulkActions: bindBulkActions,
        bindBulkSelection: bindBulkSelection,
        clearSelection: clearSelection,
        createCrudTable: createCrudTable,
        createList: createList,
        createViewState: createViewState,
        assertDataMode: assertDataMode,
        normalizeScalar: normalizeScalar,
        normalizeArray: normalizeArray,
        fillSelect: fillSelect,
        syncMultiSelectSummary: syncMultiSelectSummary,
        getAuthHeaders: getAuthHeaders,
        getSelectedIds: getSelectedIds,
        reloadWithToast: reloadWithToast,
        renderActions: renderActions,
        renderStatusBadge: renderStatusBadge,
        unwrapResponseData: unwrapResponseData,
        toServerQuery: toServerQuery,
        toDataTablesResponse: toDataTablesResponse,
        updateBulkBar: updateBulkBar
    };
})();
