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

    function unwrapResponseData(json) {
        if (json?.data?.data) return json.data.data;
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
            var icon = renderIcon(action.icon, 'me-2');
            return '<a href="javascript:void(0);" class="dropdown-item ' + (action.className || '') + '" ' + attrs + '>' + icon + escapeHtml(action.text || '') + '</a>';
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

    return {
        bindActionDispatcher: bindActionDispatcher,
        bindBulkActions: bindBulkActions,
        bindBulkSelection: bindBulkSelection,
        clearSelection: clearSelection,
        createCrudTable: createCrudTable,
        getAuthHeaders: getAuthHeaders,
        getSelectedIds: getSelectedIds,
        reloadWithToast: reloadWithToast,
        renderActions: renderActions,
        renderStatusBadge: renderStatusBadge,
        unwrapResponseData: unwrapResponseData,
        updateBulkBar: updateBulkBar
    };
})();
