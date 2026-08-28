/**
 * MOD-0151 FU04B — Plan vs Current comparison tab (Golden Reference Compact DataTable v2).
 *
 * READ-ONLY by construction: this file issues exactly one GET, renders a comparison grid and never posts. There is
 * no create / end / replace / transfer / apply path here — those stay in the neighbouring Resource Assignments
 * tab (FU04A).
 *
 * The table is built lazily on first tab activation: a DataTable initialised inside a hidden pane measures its
 * columns at zero width.
 */
(function () {
    'use strict';

    var root = document.getElementById('territory-plan-vs-current-data');
    var tableEl = document.getElementById('dt-planvscurrent');
    var tabButton = document.getElementById('tab-planvscurrent');
    if (!root || !tableEl || !tabButton) { return; }
    // Razor hot reload / partial script re-evaluation must not bind the tab or initialise its DataTable twice.
    // Keep the guard on the persistent DOM payload element so it survives a second execution of this IIFE.
    if (root.dataset.planVsCurrentBound === '1') { return; }
    root.dataset.planVsCurrentBound = '1';

    var cfg = JSON.parse(root.textContent || '{}');
    var labels = cfg.labels || {};
    var diffLabels = labels.diffTypes || {};
    var base = '/CRM/TerritoryManagement/Models/' + cfg.modelId;

    var rows = [];
    var table = null;
    var loaded = false;
    var appliedFilters = { diffType: [], node: [], businessUnit: [], position: [] };
    var effectiveAt = null;
    var personalizationClient = window.personalizationClient;
    var personalizationContext = { moduleKey: 'CRM', pageKey: 'TerritoryResourcePlanVsCurrent' };
    var savedViewRecord = null;
    var savedViewState = null;
    var saveViewArmed = false;
    var baseOrder = [[1, 'asc']];
    var saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8];
    var totalColumnCount = 10;

    function esc(value) {
        var el = document.createElement('span');
        el.textContent = value === null || value === undefined ? '' : String(value);
        return el.innerHTML;
    }

    function day(value) {
        if (!value) { return '—'; }
        var text = String(value);
        return text.length >= 10 ? text.slice(0, 10) : text;
    }

    function dash(value) { return value === null || value === undefined || value === '' ? '—' : value; }

    function stat(label, value, color, icon) {
        return '<div class="col-6 col-md-4">' +
            '<div class="card h-100">' +
            '<div class="card-body d-flex align-items-center gap-3 p-3">' +
            '<div class="avatar"><span class="avatar-initial rounded bg-label-' + (color || 'secondary') + '">' +
            '<i class="bx ' + esc(icon || 'bx-bar-chart') + '"></i></span></div>' +
            '<div>' +
            '<small class="text-muted d-block">' + esc(label) + '</small>' +
            '<h5 class="mb-0">' + esc(value) + '</h5>' +
            '</div>' +
            '</div></div></div>';
    }

    var chips = window.TerritoryFilterChips;
    var normalizeArray = chips ? chips.normalizeArray : function (v) { return v || []; };

    function appliedFilterCount() {
        return Object.keys(appliedFilters).filter(function (k) { return appliedFilters[k].length > 0; }).length
            + (effectiveAt ? 1 : 0);
    }

    // ---------------------------------------------------------------- saved view

    function normalizeString(value) {
        return typeof value === 'string' ? value.trim() : '';
    }

    function savedViewDefinition(record) {
        var raw = (record && (record.viewDefinition || record.ViewDefinition
            || record.viewDefinitionJson || record.ViewDefinitionJson)) || {};
        if (typeof raw === 'string') {
            try { return JSON.parse(raw); } catch (_error) { return {}; }
        }
        return raw || {};
    }

    function normalizeFilters(filters) {
        var value = filters || {};
        return {
            diffType: normalizeArray(value.diffType),
            node: normalizeArray(value.node),
            businessUnit: normalizeArray(value.businessUnit),
            position: normalizeArray(value.position)
        };
    }

    function factoryViewState() {
        return {
            filters: { diffType: [], node: [], businessUnit: [], position: [] },
            effectiveAt: '',
            search: '',
            colVis: saveViewColumnIndexes.reduce(function (result, index) {
                result[index] = true;
                return result;
            }, {}),
            columnOrder: Array.from({ length: totalColumnCount }, function (_value, index) { return index; }),
            order: baseOrder
        };
    }

    function normalizeViewState(state) {
        var fallback = factoryViewState();
        var value = state || {};
        return {
            filters: normalizeFilters(value.filters),
            effectiveAt: normalizeString(value.effectiveAt),
            search: normalizeString(value.search),
            colVis: value.colVis && typeof value.colVis === 'object' ? value.colVis : fallback.colVis,
            columnOrder: Array.isArray(value.columnOrder) && value.columnOrder.length === totalColumnCount
                ? value.columnOrder.map(Number) : fallback.columnOrder,
            order: Array.isArray(value.order) ? value.order : fallback.order
        };
    }

    function loadSavedView() {
        if (!personalizationClient || !personalizationClient.getViews) { return Promise.resolve(); }
        return personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey)
            .then(function (response) {
                var views = Array.isArray(response) ? response : (response && (response.data || response.Data)) || [];
                savedViewRecord = views.find(function (view) {
                    return view.isDefault === true || view.IsDefault === true;
                }) || views[0] || null;
                savedViewState = savedViewRecord ? normalizeViewState(savedViewDefinition(savedViewRecord)) : null;
                if (savedViewState) {
                    appliedFilters = normalizeFilters(savedViewState.filters);
                    effectiveAt = savedViewState.effectiveAt || null;
                }
            })
            .catch(function (error) {
                if (!error || !error.authHandled) {
                    console.error('[TerritoryResourcePlanVsCurrent SaveView] Load failed.', error);
                }
            });
    }

    function captureViewState(api) {
        var colVis = {};
        saveViewColumnIndexes.forEach(function (index) {
            try { colVis[index] = !!api.column(index).visible(); } catch (_error) { colVis[index] = true; }
        });
        return normalizeViewState({
            filters: appliedFilters,
            effectiveAt: effectiveAt || '',
            // Read the visible input as the Golden v2 contract does. DataTables debounces its internal search
            // state, while Save View dirty-state must follow what the user currently sees.
            search: api.table().container()?.querySelector('.dt-search input')?.value || '',
            colVis: colVis,
            columnOrder: api.colReorder && api.colReorder.order ? api.colReorder.order() : null,
            order: api.order()
        });
    }

    function serializedView(state) {
        return JSON.stringify(normalizeViewState(state));
    }

    function setSaveViewVisible(visible) {
        var button = tableEl.closest('.dt-container')?.querySelector('.dt-plan-vs-current-save-view');
        if (!button) { return; }
        button.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    }

    function syncSaveViewVisibility() {
        if (!saveViewArmed || !table) { return; }
        setSaveViewVisible(serializedView(captureViewState(table))
            !== serializedView(savedViewState || factoryViewState()));
    }

    function syncFilterControls() {
        $('#filterPvcDiffType').val(appliedFilters.diffType).trigger('change');
        $('#filterPvcNode').val(appliedFilters.node).trigger('change');
        $('#filterPvcBusinessUnit').val(appliedFilters.businessUnit).trigger('change');
        $('#filterPvcPosition').val(appliedFilters.position).trigger('change');
        var input = document.getElementById('filterPvcEffectiveAt');
        if (input) { input.value = effectiveAt || ''; }
    }

    function applyViewState(api, state) {
        var normalized = normalizeViewState(state);
        appliedFilters = normalizeFilters(normalized.filters);
        effectiveAt = normalized.effectiveAt || null;
        syncFilterControls();
        api.search(normalized.search || '');
        if (api.colReorder && api.colReorder.order) {
            api.colReorder.order(normalized.columnOrder, true);
        }
        saveViewColumnIndexes.forEach(function (index) {
            api.column(index).visible(normalized.colVis[index] !== false, false);
        });
        api.order(normalized.order || baseOrder).draw(false);
    }

    function saveCurrentView(api) {
        if (!personalizationClient || !personalizationClient.saveView) { return Promise.resolve(); }
        var state = captureViewState(api);
        var request = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: normalizeString(savedViewRecord && (savedViewRecord.viewName || savedViewRecord.ViewName))
                || normalizeString(labels.saveView) || 'Default',
            viewDefinition: state,
            isDefault: true,
            visibility: 'private'
        };
        var id = savedViewRecord && (savedViewRecord.id || savedViewRecord.Id);
        return (id ? personalizationClient.updateView(id, request) : personalizationClient.saveView(request))
            .then(function (saved) {
                savedViewRecord = (saved && (saved.data || saved.Data)) || saved || request;
                savedViewState = state;
                setSaveViewVisible(false);
                window.showToast?.(labels.recordSaved || labels.saveView || '', 'success');
            });
    }

    // A change type is a status-like fact, so it gets the same badge vocabulary the other territory grids use.
    function diffBadge(diffType) {
        var tone = ({
            unchanged: 'bg-label-secondary',
            replaced: 'bg-label-warning',
            transferredout: 'bg-label-warning',
            transferredin: 'bg-label-info',
            addedafteractivation: 'bg-label-primary',
            endedafteractivation: 'bg-label-dark',
            missingcurrent: 'bg-label-danger',
            datechanged: 'bg-label-info',
            scopechanged: 'bg-label-info',
            positionchanged: 'bg-label-info'
        })[String(diffType || '').toLowerCase()] || 'bg-label-primary';
        return '<span class="badge ' + tone + '">' + esc(diffLabels[diffType] || diffType || '—') + '</span>';
    }

    function resourceCell(name, id, isPrimary) {
        if (!name && !id) { return '<span class="text-muted">—</span>'; }
        var primary = isPrimary
            ? ' <span class="badge bg-label-success">' + esc(labels.primary || 'Primary') + '</span>'
            : '';
        return '<span class="fw-medium text-heading">' + esc(name || id) + '</span>' + primary;
    }

    function reasonCell(row) {
        var parts = [];
        if (row.replacementReason) { parts.push(esc(row.replacementReason)); }
        if (row.transferReason) { parts.push(esc(row.transferReason)); }
        if (!parts.length && row.changeReason) { parts.push(esc(row.changeReason)); }
        if (!parts.length) { return '<span class="text-muted">—</span>'; }
        return parts.join('<br>');
    }

    function detailField(icon, label, value) {
        return '<div class="backbone-preview-field">'
            + '<i class="bx ' + esc(icon) + '"></i><div class="min-w-0 flex-grow-1">'
            + '<div class="backbone-preview-label">' + esc(label) + '</div>'
            + '<div class="backbone-preview-value mt-1 text-break">' + (value || '<span class="text-muted">—</span>') + '</div>'
            + '</div></div>';
    }

    function detailSection(title, fields) {
        return '<section class="card backbone-preview-section p-4">'
            + '<h6 class="text-uppercase text-heading fw-semibold mb-3 backbone-preview-section-title">' + esc(title) + '</h6>'
            + '<div class="d-flex flex-column gap-4">' + fields.join('') + '</div></section>';
    }

    function assignmentSummary(name, from, to) {
        return esc(dash(name)) + '<span class="text-muted"> · ' + esc(day(from)) + ' → ' + esc(day(to)) + '</span>';
    }

    function populateDetailsOffcanvas(row) {
        if (!row) { return; }
        document.getElementById('pvc-details-title').textContent = diffLabels[row.diffType] || row.diffType || '—';
        document.getElementById('pvc-details-subtitle').textContent = row.territoryNodeCode || '—';

        var comparison = [
            detailField('bx-calendar-check', labels.planned || 'Planned',
                assignmentSummary(row.plannedResourceDisplayName, row.plannedEffectiveFrom, row.plannedEffectiveTo)),
            detailField('bx-user-check', labels.current || 'Current',
                assignmentSummary(row.currentResourceDisplayName, row.currentEffectiveFrom, row.currentEffectiveTo)),
            detailField('bx-briefcase-alt-2', labels.position || 'Position',
                esc(row.positionTitle || row.positionCode || '—')
                    + (row.positionCode ? '<small class="text-muted d-block">' + esc(row.positionCode) + '</small>' : ''))
        ];
        var evidence = [];
        [row.replacedAssignmentId, row.replacementAssignmentId].filter(Boolean).forEach(function (id) {
            evidence.push(detailField('bx-refresh', labels.replacementLink || 'Replacement', esc(id)));
        });
        [row.transferFromAssignmentId, row.transferToAssignmentId].filter(Boolean).forEach(function (id) {
            evidence.push(detailField('bx-transfer', labels.transferLink || 'Transfer', esc(id)));
        });
        evidence.push(detailField('bx-time-five', labels.changedAt || 'Changed at', esc(day(row.changedAt))));
        evidence.push(detailField('bx-user', labels.changedBy || 'Changed by', row.changedBy
            ? esc(row.changedBy)
            : '<span class="text-muted">' + esc(labels.changedByUnavailable || '—') + '</span>'));
        evidence.push(detailField('bx-link', labels.correlationId || 'Correlation id', esc(dash(row.correlationId))));
        if ((row.secondaryDifferences || []).length) {
            evidence.push(detailField('bx-list-plus', labels.alsoDiffers || 'Also differs',
                (row.secondaryDifferences || []).map(function (d) { return esc(diffLabels[d] || d); }).join(', ')));
        }
        if (row.legacyRoleCode) {
            evidence.push(detailField('bx-archive', labels.legacyRole || 'Legacy role (display only)', esc(row.legacyRoleCode)));
        }
        document.getElementById('pvc-details-body').innerHTML =
            detailSection(labels.planVsCurrent || 'Plan vs Current', comparison)
            + detailSection(labels.details || 'Details', evidence);
    }

    function populateSelect(id, values) {
        var select = document.getElementById(id);
        if (!select) { return; }
        var previous = $(select).val() || [];
        select.replaceChildren();
        values.forEach(function (v) { select.add(new Option(diffLabels[v] || v, v)); });
        $(select).val(previous).trigger('change');
    }

    function refreshChips() {
        var distinct = function (project) {
            return Array.from(new Set(rows.flatMap(project).filter(Boolean))).sort();
        };
        populateSelect('filterPvcDiffType', distinct(function (r) { return [r.diffType]; }));
        populateSelect('filterPvcNode', distinct(function (r) { return [r.territoryNodeCode]; }));
        populateSelect('filterPvcBusinessUnit', distinct(function (r) {
            return (r.businessUnitScopes || []).concat(r.currentBusinessUnitScopes || []);
        }));
        populateSelect('filterPvcPosition', distinct(function (r) { return [r.positionCode]; }));
        if (chips) {
            chips.initSelect2('#filterPvcDiffType, #filterPvcNode, #filterPvcBusinessUnit, #filterPvcPosition');
        }
        syncFilterControls();
    }

    function renderState(payload) {
        var host = document.getElementById('plan-vs-current-state');
        var stats = document.getElementById('plan-vs-current-stats');
        if (!host) { return; }

        var notices = [];
        if (payload.state === 'not-yet-activated') {
            notices.push('<div class="alert alert-warning py-2"><i class="bx bx-calendar-edit me-1"></i>'
                + esc(labels.notYetActivated || '') + '</div>');
        } else if (payload.state === 'not-captured') {
            notices.push('<div class="alert alert-secondary py-2"><i class="bx bx-camera-off me-1"></i>'
                + esc(labels.notCaptured || '') + '</div>');
        } else if (payload.state === 'load-failed') {
            notices.push('<div class="alert alert-danger py-2"><i class="bx bx-error-circle me-1"></i>'
                + esc(labels.gatewayError || '') + '</div>');
        }
        if (payload.isHistorical && payload.state === 'available') {
            notices.push('<div class="alert alert-secondary py-2"><i class="bx bx-archive me-1"></i>'
                + esc(labels.historical || '') + '</div>');
        }

        host.innerHTML = notices.join('');
        host.classList.toggle('d-none', notices.length === 0);

        if (stats) {
            var available = payload.state === 'available';
            stats.classList.toggle('d-none', !available);
            if (available) {
                var summary = payload.summary || {};
                stats.innerHTML =
                    stat(labels.plannedCount || 'Planned', summary.plannedCount || 0, 'primary', 'bx-calendar-check') +
                    stat(labels.currentCount || 'Current', summary.currentCount || 0, 'success', 'bx-user-check') +
                    stat(labels.changed || 'Changed', summary.changedCount || 0, 'warning', 'bx-git-compare');
            }
        }
    }

    function buildTable() {
        if (table) { return table; }
        if ($.fn.dataTable.isDataTable(tableEl)) {
            table = $(tableEl).DataTable();
            return table;
        }

        // Register the client-side predicate before construction. This lets the restored saved filters participate
        // in the first draw and avoids a second draw removing the inline-filter host mounted under the toolbar.
        $.fn.dataTable.ext.search.push(function (settings, _data, dataIndex, rowData) {
            if (settings.nTable !== tableEl) { return true; }
            var row = rowData || (table ? table.row(dataIndex).data() : null);
            if (!row) { return true; }
            var scopes = (row.businessUnitScopes || []).concat(row.currentBusinessUnitScopes || []);
            return (!appliedFilters.diffType.length || appliedFilters.diffType.includes(row.diffType))
                && (!appliedFilters.node.length || appliedFilters.node.includes(row.territoryNodeCode))
                && (!appliedFilters.businessUnit.length
                    || scopes.some(function (scope) { return appliedFilters.businessUnit.includes(scope); }))
                && (!appliedFilters.position.length || appliedFilters.position.includes(row.positionCode));
        });

        table = new DataTable(tableEl, window.DtDefaults.create({
            data: rows,
            processing: false,
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            order: baseOrder,
            columns: [
                { data: null, name: 'control', defaultContent: '' },
                { data: 'diffType', name: 'diffType' },
                { data: 'territoryNodeCode', name: 'territoryNodeCode' },
                { data: 'businessUnitScopes', name: 'businessUnitScopes' },
                { data: 'positionCode', name: 'positionCode' },
                { data: 'plannedResourceDisplayName', name: 'plannedResource' },
                { data: 'currentResourceDisplayName', name: 'currentResource' },
                { data: 'currentEffectiveFrom', name: 'effectiveDate' },
                { data: 'changeReason', name: 'reason' },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                {
                    targets: 0,
                    className: 'control',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 2,
                    render: function () { return ''; }
                },
                { targets: 1, render: function (data, type) { return type === 'display' ? diffBadge(data) : (data || ''); } },
                {
                    targets: 2,
                    render: function (data, type, row) {
                        if (type !== 'display') { return data || ''; }
                        var moved = row.currentTerritoryNodeCode && row.currentTerritoryNodeCode !== data
                            ? ' <i class="bx bx-right-arrow-alt"></i> <span class="text-muted">'
                                + esc(row.currentTerritoryNodeCode) + '</span>'
                            : '';
                        return esc(data || '—') + moved
                            + '<small class="text-muted d-block">' + esc(row.territoryNodeName || '') + '</small>';
                    }
                },
                {
                    targets: 3,
                    render: function (data, type) {
                        if (type !== 'display') { return (data || []).join(' '); }
                        return (data || []).length
                            ? (data || []).map(function (s) { return '<span class="badge bg-label-primary me-1">' + esc(s) + '</span>'; }).join('')
                            : '<span class="text-muted">—</span>';
                    }
                },
                {
                    targets: 4,
                    render: function (data, type, row) {
                        if (type !== 'display') { return data || ''; }
                        // Position, never Role — the canonical identity of an assignment slot (pack §22.4).
                        return '<span class="fw-medium">' + esc(row.positionTitle || data) + '</span>'
                            + '<small class="text-muted d-block">' + esc(data || '') + '</small>';
                    }
                },
                {
                    targets: 5,
                    render: function (data, type, row) {
                        return type === 'display'
                            ? resourceCell(data, row.plannedResourceId, row.plannedIsPrimary)
                            : (data || '');
                    }
                },
                {
                    targets: 6,
                    render: function (data, type, row) {
                        return type === 'display'
                            ? resourceCell(data, row.currentResourceId, row.currentIsPrimary)
                            : (data || '');
                    }
                },
                {
                    targets: 7,
                    render: function (data, type, row) {
                        return type === 'display'
                            ? day(data || row.plannedEffectiveFrom)
                            : (data || row.plannedEffectiveFrom || '');
                    }
                },
                { targets: 8, orderable: false, render: function (data, type, row) { return type === 'display' ? reasonCell(row) : (data || ''); } },
                {
                    targets: 9,
                    title: labels.actions || 'Actions',
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all',
                    render: function (_data, type, row) {
                        if (type !== 'display') { return ''; }
                        return window.DitenDataTable.renderActions([{
                            className: 'js-pvc-details me-1',
                            icon: 'bx bx-show',
                            attrs: {
                                'data-bs-toggle': 'offcanvas',
                                'data-bs-target': '#pvcDetailsOffcanvas',
                                'data-json': JSON.stringify(row),
                                'title': labels.details || 'Details'
                            }
                        }]);
                    }
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                null,
                {},
                {
                    filterBtn: {
                        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn dt-plan-vs-current-filter position-relative',
                        attr: {
                            title: labels.filter || '',
                            'aria-controls': 'pvcInlineFilterCollapse',
                            'aria-expanded': 'false'
                        },
                        action: function () { }
                    },
                    saveFilterBtn: {
                        text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">'
                            + esc(labels.saveView || '') + '</span>',
                        className: 'btn btn-label-primary d-none dt-save-filter-btn dt-plan-vs-current-save-view',
                        attr: { title: labels.saveView || '' },
                        action: function (_event, api) {
                            saveCurrentView(api || table).catch(function (error) {
                                if (!error || !error.authHandled) {
                                    console.error('[TerritoryResourcePlanVsCurrent SaveView] Save failed.', error);
                                }
                            });
                        }
                    }
                },
                { exportColumns: [1, 2, 3, 4, 5, 6, 7, 8], colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8] }
            ),
            language: { emptyTable: labels.noComparison || '' },
            initComplete: function () {
                var api = this.api();
                // Restore DataTable state before mounting the custom filter host. applyViewState draws once; doing
                // it after the mount would let DataTables replace the host's layout row.
                applyViewState(api, savedViewState || factoryViewState());
                var host = document.getElementById('pvcInlineFilterHost');
                var filterButton = tableEl.closest('.dt-container')?.querySelector('.dt-plan-vs-current-filter');
                var toolbar = filterButton?.closest('.dt-layout-row')
                    || filterButton?.closest('.row')
                    || filterButton?.closest('.dt-layout-end')?.parentElement;
                if (host && toolbar) {
                    toolbar.insertAdjacentElement('afterend', host);
                    host.classList.remove('px-6');
                    host.classList.add('px-3');
                }
                var collapseEl = document.getElementById('pvcInlineFilterCollapse');
                if (filterButton && collapseEl && filterButton.dataset.pvcFilterBound !== '1') {
                    filterButton.dataset.pvcFilterBound = '1';
                    filterButton.addEventListener('click', function (event) {
                        event.preventDefault();
                        event.stopPropagation();
                        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
                    });
                    collapseEl.addEventListener('shown.bs.collapse', function () {
                        filterButton.setAttribute('aria-expanded', 'true');
                    });
                    collapseEl.addEventListener('hidden.bs.collapse', function () {
                        filterButton.setAttribute('aria-expanded', 'false');
                    });
                }
                setTimeout(function () {
                    saveViewArmed = true;
                    syncSaveViewVisibility();
                }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), appliedFilterCount());
            }
        }));

        table.on('column-visibility.dt column-reorder.dt columns-reordered.dt search.dt order.dt', function () {
            window.DtDefaults.updateVisualState(table, appliedFilterCount());
            syncSaveViewVisibility();
        });

        document.getElementById('btnPvcFilterApply')?.addEventListener('click', function () {
            appliedFilters = {
                diffType: normalizeArray($('#filterPvcDiffType').val()),
                node: normalizeArray($('#filterPvcNode').val()),
                businessUnit: normalizeArray($('#filterPvcBusinessUnit').val()),
                position: normalizeArray($('#filterPvcPosition').val())
            };
            var picked = document.getElementById('filterPvcEffectiveAt')?.value || '';
            // effectiveAt changes WHICH assignment is current, so it is a server-side filter, not a grid filter.
            if (picked !== (effectiveAt || '')) {
                effectiveAt = picked || null;
                load();
                syncSaveViewVisibility();
                return;
            }
            table.draw();
            syncSaveViewVisibility();
            bootstrap.Collapse.getOrCreateInstance(
                document.getElementById('pvcInlineFilterCollapse'), { toggle: false }).hide();
        });

        document.getElementById('btnPvcFilterReset')?.addEventListener('click', function (event) {
            event.preventDefault();
            var hadEffectiveDate = !!effectiveAt;
            applyViewState(table, factoryViewState());
            if (hadEffectiveDate) { load(); }
            syncSaveViewVisibility();
        });

        return table;
    }

    function load() {
        var url = base + '/PlanVsCurrent/Json';
        var payloadReceived = false;
        if (effectiveAt) { url += '?effectiveAt=' + encodeURIComponent(effectiveAt + 'T00:00:00Z'); }

        return fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) {
                if (!r.ok) { throw new Error('Plan vs Current request failed with HTTP ' + r.status + '.'); }
                return r.json();
            })
            .then(function (payload) {
                var data = payload.data || {};
                payloadReceived = true;
                rows = data.rows || [];
                renderState(data);
                refreshChips();
                if (!table) { buildTable(); } else { table.clear().rows.add(rows).draw(false); }
                $('#plan-vs-current-skeleton').fadeOut(150);
            })
            .catch(function (error) {
                console.error('[TerritoryResourcePlanVsCurrent] Load/render failed.', error);
                // `not-captured` is a business state returned by the API. Never manufacture it from a client-side
                // rendering or transport exception after a valid comparison payload has already arrived.
                if (!payloadReceived) {
                    rows = [];
                    renderState({ state: 'load-failed' });
                    if (!table) { buildTable(); }
                }
                $('#plan-vs-current-skeleton').fadeOut(150);
            });
    }

    tabButton.addEventListener('shown.bs.tab', function () {
        if (!loaded) {
            loaded = true;
            loadSavedView().then(load);
            return;
        }
        if (table) { table.columns.adjust(); }
    });

    document.addEventListener('click', function (event) {
        var trigger = event.target.closest('.js-pvc-details');
        if (!trigger || !trigger.closest('#dt-planvscurrent')) { return; }
        var raw = trigger.getAttribute('data-json');
        if (!raw) { return; }
        try { populateDetailsOffcanvas(JSON.parse(raw)); }
        catch (error) { console.error('[TerritoryResourcePlanVsCurrent] Details payload is invalid.', error); }
    });
})();
