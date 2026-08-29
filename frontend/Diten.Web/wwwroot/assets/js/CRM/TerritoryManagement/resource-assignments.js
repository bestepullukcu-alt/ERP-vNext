/**
 * MOD-0151 FU04 — Territory resource assignments (Details page).
 *
 * Assigns PEOPLE to territory nodes. There is no account-assignment call here and no such endpoint behind the
 * proxy (FU05). Ending an assignment is a status transition, never a delete.
 *
 * The grid follows the Golden Reference Compact DataTable v2 contract (inline filter host relocated into the
 * toolbar row, Action/ColVis/Filter/SaveView buttons, Add New as the primary button, skeleton loader).
 */
(function () {
    'use strict';

    var root = document.getElementById('territory-resource-data');
    var tableEl = document.getElementById('dt-resourceassignments');
    if (!root || !tableEl) { return; }

    var cfg = JSON.parse(root.textContent || '{}');
    var labels = cfg.labels || {};
    var base = '/CRM/TerritoryManagement/Models/' + cfg.modelId;

    var conflictPanel = document.getElementById('resource-conflict-panel');
    var offcanvasEl = document.getElementById('resource-assignment-offcanvas');
    var form = document.getElementById('resource-assignment-form');
    var offcanvas = offcanvasEl && window.bootstrap ? new bootstrap.Offcanvas(offcanvasEl) : null;

    var personalizationClient = window.personalizationClient;
    var personalizationContext = { moduleKey: 'CRM', pageKey: 'TerritoryResourceAssignments' };

    var lookups = null;
    var cache = [];
    var historyCache = [];
    var table = null;
    var appliedFilters = { status: [], position: [], node: [], coverage: [] };
    var savedViewRecord = null;
    var savedViewState = null;

    function esc(v) {
        var el = document.createElement('span');
        el.textContent = v === null || v === undefined ? '' : String(v);
        return el.innerHTML;
    }

    function token() {
        var i = document.querySelector('input[name="__RequestVerificationToken"]');
        return i ? i.value : '';
    }

    function toast(message, isError) {
        window.showToast?.(message || '', isError ? 'error' : 'success');
    }

    var chips = window.TerritoryFilterChips;
    var normalizeArray = chips.normalizeArray;
    var appliedFilterCount = function () { return chips.appliedFieldCount(appliedFilters); };

    // ---------------------------------------------------------------- cell renderers

    function statusBadge(status) {
        var tone = status === 'active' ? 'success'
            : status === 'proposed' ? 'info'
            : status === 'ended' ? 'secondary' : 'danger';
        var text = status === 'proposed' ? 'Planned' : status === 'active' ? 'Active' : status === 'ended' ? 'Ended' : status;
        return '<span class="badge bg-label-' + tone + '">' + esc(text) + '</span>';
    }

    function period(a) {
        var from = (a.validFrom || '').substring(0, 10);
        var to = a.validTo ? (a.validTo || '').substring(0, 10) : '—';
        var expired = a.isExpired ? ' <span class="badge bg-label-warning">' + esc(labels.expired) + '</span>' : '';
        return esc(from) + ' → ' + esc(to) + expired;
    }

    function renderTarget(row) {
        return row.territoryCode
            ? esc(row.territoryCode) + ' <small class="text-muted">(' + esc(row.territoryLevel) + ')</small>'
            : '<span class="text-muted">—</span>';
    }

    // ---------------------------------------------------------------- filter chips

    function populateSelect(id, values) {
        var select = document.getElementById(id);
        if (!select) { return; }
        select.replaceChildren();
        values.forEach(function (v) { select.add(new Option(v.text, v.value)); });
    }

    function initFilterSelect2() {
        chips.initSelect2('#filterResourceStatus, #filterResourcePosition, #filterResourceNode, #filterResourceCoverage');
    }

    function refreshFilterChips() {
        var distinct = function (prop) {
            return Array.from(new Set(cache.map(function (r) { return r[prop]; }).filter(Boolean))).sort();
        };
        populateSelect('filterResourceStatus', distinct('status').map(function (v) { return { value: v, text: v }; }));
        populateSelect('filterResourcePosition', distinct('positionCode').map(function (v) { return { value: v, text: v }; }));
        populateSelect('filterResourceNode', distinct('territoryCode').map(function (v) { return { value: v, text: v }; }));
        populateSelect('filterResourceCoverage', distinct('coverageScope').map(function (v) { return { value: v, text: v }; }));
        initFilterSelect2();
        $('#filterResourceStatus').val(appliedFilters.status).trigger('change');
        $('#filterResourcePosition').val(appliedFilters.position).trigger('change');
        $('#filterResourceNode').val(appliedFilters.node).trigger('change');
        $('#filterResourceCoverage').val(appliedFilters.coverage).trigger('change');
    }

    // ---------------------------------------------------------------- saved view

    function savedViewDefinition(record) {
        var raw = (record && (record.viewDefinition || record.ViewDefinition)) || {};
        if (typeof raw === 'string') { try { return JSON.parse(raw); } catch (_e) { return {}; } }
        return raw || {};
    }

    function loadSavedView() {
        if (!personalizationClient || !personalizationClient.getViews) { return Promise.resolve(); }
        return personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey)
            .then(function (response) {
                var views = Array.isArray(response) ? response : (response && (response.data || response.Data)) || [];
                savedViewRecord = views.find(function (v) { return v.isDefault === true || v.IsDefault === true; }) || views[0] || null;
                savedViewState = savedViewRecord ? savedViewDefinition(savedViewRecord) : null;
            })
            .catch(function (error) {
                if (!error || !error.authHandled) { console.error('[TerritoryResourceAssignments SaveView] Load failed.', error); }
            });
    }

    function viewState(api) {
        return {
            filters: appliedFilters,
            search: api.search(),
            colVis: [1, 2, 3, 4, 5, 6, 7, 8].reduce(function (result, index) {
                result[index] = api.column(index).visible();
                return result;
            }, {}),
            columnOrder: (api.colReorder && api.colReorder.order && api.colReorder.order()) || null
        };
    }

    function saveCurrentView(api) {
        if (!personalizationClient || !personalizationClient.saveView) { return Promise.resolve(); }
        var request = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: ((savedViewRecord && (savedViewRecord.viewName || savedViewRecord.ViewName)) || labels.saveView || 'Default').trim(),
            viewDefinition: viewState(api),
            isDefault: true,
            visibility: 'private'
        };

        var id = savedViewRecord && (savedViewRecord.id || savedViewRecord.Id);
        return (id ? personalizationClient.updateView(id, request) : personalizationClient.saveView(request))
            .then(function (saved) {
                savedViewRecord = (saved && (saved.data || saved.Data)) || saved || request;
                savedViewState = request.viewDefinition;
                document.querySelector('.dt-save-filter-btn')?.classList.add('d-none');
                window.showToast?.(labels.recordSaved || labels.saveView || '', 'success');
            });
    }

    // ---------------------------------------------------------------- grid

    function buildTable() {
        table = new DataTable(tableEl, window.DtDefaults.create({
            data: cache,
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'resourceDisplayName', name: 'resource' },
                { data: 'positionCode', name: 'position' },
                { data: 'territoryCode', name: 'territoryCode' },
                { data: 'coverageScope', name: 'coverageScope' },
                { data: 'businessUnitScopes', name: 'businessUnitScopes' },
                { data: 'isPrimary', name: 'isPrimary' },
                { data: 'status', name: 'status' },
                { data: 'validFrom', name: 'validFrom' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: function () { return ''; } },
                {
                    targets: 1,
                    render: function (data, type, row) {
                        if (type !== 'display') { return data || ''; }
                        return '<span class="fw-medium text-heading">' + esc(data) + '</span>'
                            + (row.resourceEmail ? '<br><small class="text-muted">' + esc(row.resourceEmail) + '</small>' : '');
                    }
                },
                {
                    targets: 2,
                    render: function (data, type, row) {
                        if (type !== 'display') { return data || ''; }
                        return '<span class="badge bg-label-primary">' + esc(data) + '</span>'
                            + ((row.positionTitle || row.positionName)
                                ? '<br><small class="text-muted">' + esc(row.positionTitle || row.positionName) + '</small>' : '');
                    }
                },
                { targets: 3, render: function (_d, type, row) { return type === 'display' ? renderTarget(row) : (row.territoryCode || ''); } },
                { targets: 4, render: function (data, type) { return type === 'display' ? '<small>' + esc(data) + '</small>' : (data || ''); } },
                {
                    targets: 5, orderable: false,
                    render: function (data, type) {
                        var scopes = data || [];
                        if (type !== 'display') { return scopes.join(' '); }
                        return scopes.length
                            ? scopes.map(function (s) { return '<span class="badge bg-label-info me-1">' + esc(s) + '</span>'; }).join('')
                            : '<span class="text-muted">—</span>';
                    }
                },
                {
                    targets: 6,
                    render: function (data, type) {
                        if (type !== 'display') { return data ? 1 : 0; }
                        return data ? '<i class="bx bx-check text-success"></i>' : '<i class="bx bx-minus text-muted"></i>';
                    }
                },
                { targets: 7, render: function (data, type) { return type === 'display' ? statusBadge(data) : (data || ''); } },
                { targets: 8, render: function (data, type, row) { return type === 'display' ? '<small>' + period(row) + '</small>' : (data || ''); } },
                {
                    targets: -1,
                    title: labels.actions || '',
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end',
                    render: function (id, _type, row) {
                        if (!cfg.canEdit) { return ''; }
                        var actions = [];
                        if (row.status === 'proposed') {
                            actions.push({ key: 'edit', className: 'js-edit-ra', icon: 'bx bx-edit', text: labels.edit || '', attrs: { 'data-id': id } });
                        }
                        if (row.status !== 'ended') {
                            actions.push({ key: 'endRa', className: 'js-end-ra', icon: 'bx bx-stop-circle', text: labels.endAssignment || '', attrs: { 'data-id': id, 'data-name': row.resourceDisplayName } });
                        }
                        if (row.status === 'active') {
                            actions.push({ key: 'replaceRa', className: 'js-replace-ra', icon: 'bx bx-user-plus', text: labels.replaceAssignment || '', attrs: { 'data-id': id } });
                            actions.push({ key: 'transferRa', className: 'js-transfer-ra', icon: 'bx bx-transfer', text: labels.transferAssignment || '', attrs: { 'data-id': id } });
                        }
                        if (row.status === 'proposed') {
                            actions.push({ key: 'deleteRa', className: 'js-delete-ra', icon: 'bx bx-trash', text: labels.deleteAssignment || '', attrs: { 'data-id': id, 'data-name': row.resourceDisplayName } });
                        }
                        return window.DitenDataTable.renderActions(actions);
                    }
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                cfg.canEdit ? (labels.assignResource || '') : null,
                {},
                {
                    // Grid-level conflict check lives in the Action dropdown (it is not a row action).
                    collectionBtns: [{
                        icon: 'bx-shield-quarter',
                        text: labels.checkConflicts || '',
                        className: 'js-validate-resource-conflicts',
                        action: function () { checkConflicts(); }
                    }],
                    filterBtn: {
                        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                        attr: { title: labels.filter || '', 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false' },
                        action: function () { }
                    },
                    saveFilterBtn: {
                        text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + esc(labels.saveView || '') + '</span>',
                        className: 'btn btn-label-primary d-none dt-save-filter-btn',
                        attr: { title: labels.saveView || '' },
                        action: function (_event, api) {
                            saveCurrentView(api || table).catch(function (error) {
                                if (!error || !error.authHandled) { console.error('[TerritoryResourceAssignments SaveView] Save failed.', error); }
                            });
                        }
                    }
                },
                { exportColumns: [1, 2, 3, 4, 5, 6, 7, 8], colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8] }
            ),
            language: { emptyTable: labels.noAssignments || '' },
            initComplete: function () {
                var api = this.api();
                var host = document.getElementById('inlineFilterHost');
                var filterButton = tableEl.closest('.dt-container')?.querySelector('.dt-filter-btn');
                var toolbar = filterButton?.closest('.dt-layout-row')
                    || filterButton?.closest('.row')
                    || filterButton?.closest('.dt-layout-end')?.parentElement;
                if (host && toolbar) {
                    toolbar.insertAdjacentElement('afterend', host);
                    host.classList.remove('px-6');
                    host.classList.add('px-3');
                }
                chips.bindToggle('resourceAssignmentsFilterBound');
                refreshFilterChips();
                if (savedViewState && savedViewState.search) { api.search(savedViewState.search); }
                Object.entries((savedViewState && savedViewState.colVis) || {}).forEach(function (entry) {
                    api.column(Number(entry[0])).visible(!!entry[1], false);
                });
                if (savedViewState && Array.isArray(savedViewState.columnOrder) && api.colReorder && api.colReorder.order) {
                    api.colReorder.order(savedViewState.columnOrder, true);
                }
                api.draw(false);

                if (cfg.canEdit) {
                    tableEl.closest('.dt-container')?.querySelector('.add-new')?.addEventListener('click', function (event) {
                        event.preventDefault();
                        openForm(null);
                    });
                }
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), appliedFilterCount());
            }
        }));

        table.on('column-visibility.dt column-reorder.dt columns-reordered.dt search.dt', function () {
            window.DtDefaults.updateVisualState(table, appliedFilterCount());
            document.querySelector('.dt-save-filter-btn')?.classList.remove('d-none');
        });

        // Scoped to THIS table: the page hosts only this grid, but keep the guard for parity with the module.
        $.fn.dataTable.ext.search.push(function (settings, _data, dataIndex, rowData) {
            if (settings.nTable !== tableEl) { return true; }
            var r = rowData || table.row(dataIndex).data();
            if (!r) { return true; }
            return (!appliedFilters.status.length || appliedFilters.status.includes(r.status))
                && (!appliedFilters.position.length || appliedFilters.position.includes(r.positionCode))
                && (!appliedFilters.node.length || appliedFilters.node.includes(r.territoryCode))
                && (!appliedFilters.coverage.length || appliedFilters.coverage.includes(r.coverageScope));
        });

        document.getElementById('btnFilterApply')?.addEventListener('click', function () {
            appliedFilters = {
                status: normalizeArray($('#filterResourceStatus').val()),
                position: normalizeArray($('#filterResourcePosition').val()),
                node: normalizeArray($('#filterResourceNode').val()),
                coverage: normalizeArray($('#filterResourceCoverage').val())
            };
            table.draw();
            document.querySelector('.dt-save-filter-btn')?.classList.remove('d-none');
            bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse')).hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', function (event) {
            event.preventDefault();
            appliedFilters = { status: [], position: [], node: [], coverage: [] };
            $('#filterResourceStatus, #filterResourcePosition, #filterResourceNode, #filterResourceCoverage').val(null).trigger('change');
            table.search('');
            table.draw();
            document.querySelector('.dt-save-filter-btn')?.classList.remove('d-none');
        });
    }

    function load() {
        return fetch(base + '/ResourceAssignments/Json', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.json(); })
            .then(function (p) {
                if (!p.success || !p.data) {
                    toast((p.errors && p.errors[0]) || labels.gatewayError, true);
                    cache = [];
                } else {
                    cache = p.data.items || [];
                }
                loadHistory();
                if (!table) {
                    buildTable();
                } else {
                    table.clear().rows.add(cache).draw(false);
                    refreshFilterChips();
                }
                $('#skeleton-loader').fadeOut(150);
            })
            .catch(function () {
                toast(labels.gatewayError, true);
                $('#skeleton-loader').fadeOut(150);
            });
    }

    function historyPointTone(status) {
        return status === 'active' ? 'success'
            : status === 'proposed' ? 'info'
            : status === 'ended' ? 'secondary'
            : 'danger';
    }

    function historyEventLabel(x) {
        if (x.transferReason) { return labels.transferAssignment || 'Transfer assignment'; }
        if (x.replacementReason) { return labels.replaceAssignment || 'Replace assignment'; }
        return labels.assignmentHistory || 'Assignment history';
    }

    function historyDate(x) {
        var value = x.updatedAt || x.createdAt || x.validFrom;
        if (!value) { return '—'; }
        var date = new Date(value);
        return Number.isNaN(date.getTime())
            ? esc(String(value).substring(0, 10))
            : esc(date.toLocaleString());
    }

    function renderHistory() {
        var history = document.getElementById('resource-history-panel');
        if (!history) { return; }

        if (!historyCache.length) {
            history.innerHTML = '<div class="text-center text-muted py-5">'
                + '<i class="bx bx-history bx-lg d-block mb-2"></i>'
                + esc(labels.noAssignmentHistory || 'No assignment history yet.')
                + '</div>';
            return;
        }

        var historical = historyCache.slice().sort(function (a, b) {
            var aDate = new Date(a.updatedAt || a.createdAt || a.validFrom || 0).getTime();
            var bDate = new Date(b.updatedAt || b.createdAt || b.validFrom || 0).getTime();
            return bDate - aDate;
        });

        history.innerHTML = '<ul class="timeline timeline-outline mb-0">'
            + historical.map(function (x) {
                var reason = x.transferReason || x.replacementReason || x.changeReason || '';
                var target = x.territoryCode
                    ? x.territoryCode + (x.territoryName ? ' — ' + x.territoryName : '')
                    : x.coverageScope;
                var businessUnits = (x.businessUnitScopes || []).map(function (scope) {
                    return '<span class="badge bg-label-primary me-1 mb-1">' + esc(scope) + '</span>';
                }).join('');
                var validity = esc(labels.validFrom || 'Valid from') + ': '
                    + esc((x.validFrom || '').substring(0, 10))
                    + ' · ' + esc(labels.validTo || 'Valid to') + ': '
                    + esc(x.validTo ? x.validTo.substring(0, 10) : '—');

                return '<li class="timeline-item timeline-item-transparent border-dashed">'
                    + '<span class="timeline-point timeline-point-' + historyPointTone(x.status) + '"></span>'
                    + '<div class="timeline-event">'
                    + '<div class="timeline-header mb-2">'
                    + '<div><h6 class="mb-1">' + esc(x.resourceDisplayName) + '</h6>'
                    + statusBadge(x.status) + '</div>'
                    + '<small class="text-body-secondary text-nowrap">' + historyDate(x) + '</small>'
                    + '</div>'
                    + '<p class="mb-1 fw-medium">' + esc(x.positionTitle || x.positionCode) + '</p>'
                    + '<small class="text-muted d-block mb-2">' + esc(x.positionCode) + '</small>'
                    + '<div class="d-flex align-items-start mb-2">'
                    + '<i class="bx bx-map me-2 mt-1 text-primary"></i>'
                    + '<div><small class="text-muted d-block">' + esc(labels.targetNode || 'Target node') + '</small>'
                    + '<span>' + esc(target) + '</span></div></div>'
                    + (businessUnits
                        ? '<div class="mb-2"><small class="text-muted d-block mb-1">'
                            + esc(labels.businessUnitScope || 'Business unit scope') + '</small>' + businessUnits + '</div>'
                        : '')
                    + '<small class="text-muted d-block mb-2">' + validity + '</small>'
                    + (reason
                        ? '<div class="alert alert-secondary py-2 px-3 mb-0">'
                            + '<small class="fw-medium d-block mb-1">' + esc(historyEventLabel(x)) + '</small>'
                            + '<span>' + esc(reason) + '</span></div>'
                        : '')
                    + '</div></li>';
            }).join('')
            + '</ul>';
    }

    function loadHistory() {
        fetch(base + '/ResourceAssignments/HistoryJson')
            .then(function (r) { return r.json(); })
            .then(function (result) {
            historyCache = result.success ? (result.data || []) : [];
            renderHistory();
        }).catch(function () {
            historyCache = cache.slice();
            renderHistory();
        });
    }

    // ---------------------------------------------------------------- conflicts

    function renderConflicts(d) {
        var html = '';
        if (d.conflictCount === 0 && d.warningCount === 0) {
            html = '<div class="alert alert-success py-2"><i class="bx bx-check-shield me-1"></i>' +
                esc(labels.noConflictsFound) + '</div>';
        } else {
            (d.conflicts || []).forEach(function (c) {
                html += '<div class="alert alert-danger py-2"><i class="bx bx-error-circle me-1"></i><strong>' +
                    esc(c.kind) + '</strong> — ' + esc(c.message) + '</div>';
            });
            (d.warnings || []).forEach(function (w) {
                html += '<div class="alert alert-warning py-2"><i class="bx bx-error me-1"></i><strong>' +
                    esc(w.kind) + '</strong> — ' + esc(w.message) + '</div>';
            });
        }
        conflictPanel.innerHTML = html;
        conflictPanel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    function checkConflicts() {
        var fd = new FormData();
        fd.append('__RequestVerificationToken', token());

        fetch(base + '/ResourceAssignments/ValidateConflicts', { method: 'POST', body: fd })
            .then(function (r) { return r.json(); })
            .then(function (p) {
                if (!p.success || !p.data) { toast((p.errors && p.errors[0]) || labels.gatewayError, true); return; }
                renderConflicts(p.data);
            })
            .catch(function () { toast(labels.gatewayError, true); });
    }

    // ---------------------------------------------------------------- form

    function fill(select, options, selected, includeEmpty) {
        var html = includeEmpty ? '<option value="">—</option>' : '';
        html += options.map(function (o) {
            return '<option value="' + esc(o.value) + '"' + (o.value === selected ? ' selected' : '') + '>' + esc(o.text) + '</option>';
        }).join('');
        select.innerHTML = html;
    }

    // Position select: option value is the position id; code/name ride along as data- attributes and are mirrored
    // into the hidden PositionCode/PositionName inputs the form posts.
    function fillPositions(positions, selectedId) {
        var select = document.getElementById('ra-position');
        select.innerHTML = '<option value="">—</option>' + positions.map(function (p) {
            return '<option value="' + esc(p.value) + '"'
                + ' data-code="' + esc(p.code) + '" data-name="' + esc(p.name) + '"'
                + (String(p.value) === String(selectedId) ? ' selected' : '') + '>' + esc(p.text) + '</option>';
        }).join('');
        syncPositionHidden();
    }

    function fillResources(resources, selectedId, selectedSnapshot) {
        var select = document.getElementById('ra-resource-picker');
        var options = resources.slice();
        if (selectedSnapshot && selectedId && !options.some(function (x) { return String(x.value) === String(selectedId); })) {
            options.unshift({
                value: selectedId,
                text: selectedSnapshot.resourceDisplayName,
                displayName: selectedSnapshot.resourceDisplayName,
                email: selectedSnapshot.resourceEmail || '',
                resourceType: 'user'
            });
        }
        select.innerHTML = '<option value="">—</option>' + options.map(function (resource) {
            return '<option value="' + esc(resource.value) + '"'
                + ' data-name="' + esc(resource.displayName || resource.text) + '"'
                + ' data-email="' + esc(resource.email || '') + '"'
                + ' data-resource-type="' + esc(resource.resourceType || 'user') + '"'
                + (String(resource.value) === String(selectedId) ? ' selected' : '') + '>'
                + esc(resource.text) + '</option>';
        }).join('');
    }

    function initFormSelect2() {
        if (!window.jQuery || !window.jQuery.fn || !window.jQuery.fn.select2 || !offcanvasEl) { return; }
        ['#ra-position', '#ra-coverage', '#ra-node', '#ra-business-units', '#ra-resource-type', '#ra-resource-picker']
            .forEach(function (selector) {
                var $select = window.jQuery(selector);
                if (!$select.length) { return; }
                if ($select.hasClass('select2-hidden-accessible')) { $select.select2('destroy'); }
                $select.select2({
                    dropdownParent: window.jQuery(offcanvasEl),
                    width: '100%',
                    allowClear: !$select.prop('required'),
                    closeOnSelect: !$select.prop('multiple'),
                    placeholder: '—'
                });
            });
    }

    function syncResourceHidden() {
        var picker = document.getElementById('ra-resource-picker');
        var option = picker.options[picker.selectedIndex];
        document.getElementById('ra-resource-id').value = option ? option.value : '';
        document.getElementById('ra-resource-name').value = option ? (option.getAttribute('data-name') || option.textContent || '') : '';
        document.getElementById('ra-resource-email').value = option ? (option.getAttribute('data-email') || '') : '';
        document.getElementById('ra-resource-type').value = 'user';
        if (window.jQuery) { window.jQuery('#ra-resource-type').trigger('change.select2'); }
    }

    function syncPositionHidden() {
        var select = document.getElementById('ra-position');
        var opt = select.options[select.selectedIndex];
        document.getElementById('ra-position-code').value = opt ? (opt.getAttribute('data-code') || '') : '';
        document.getElementById('ra-position-name').value = opt ? (opt.getAttribute('data-name') || '') : '';
    }

    function ensureLookups() {
        if (lookups) { return Promise.resolve(lookups); }
        return fetch(base + '/ResourceAssignments/lookups')
            .then(function (r) { return r.json(); })
            .then(function (d) { lookups = d; return lookups; });
    }

    /// Mirrors the backend metadata rules so the form does not offer a combination the API would reject.
    function applyScopeShape() {
        var scope = document.getElementById('ra-coverage').value;
        var nodeGroup = document.getElementById('ra-node-group');
        var buGroup = document.getElementById('ra-bu-group');
        var territoryScopes = ['exact-territory', 'territory-subtree'];
        var noScopeScopes = ['model-wide', 'all-business-scopes'];
        var businessRequiredScopes = ['business-unit', 'product-portfolio', 'business-scope'];

        var needsNode = territoryScopes.indexOf(scope) >= 0;
        nodeGroup.classList.toggle('d-none', scope !== '' && !needsNode);
        document.getElementById('ra-node').required = needsNode;
        if (!needsNode) {
            document.getElementById('ra-node').value = '';
            if (window.jQuery) { window.jQuery('#ra-node').trigger('change.select2'); }
        }

        var allowsBu = noScopeScopes.indexOf(scope) < 0;
        buGroup.classList.toggle('d-none', !allowsBu);
        document.getElementById('ra-business-units').required = businessRequiredScopes.indexOf(scope) >= 0;
        if (!allowsBu) {
            Array.prototype.forEach.call(document.getElementById('ra-business-units').options, function (o) { o.selected = false; });
            if (window.jQuery) { window.jQuery('#ra-business-units').trigger('change.select2'); }
        }
    }

    function openForm(a) {
        ensureLookups().then(function (lk) {
            form.reset();
            document.getElementById('ra-form-errors').classList.add('d-none');
            document.getElementById('ra-id').value = a ? a.id : '';
            document.getElementById('resource-assignment-offcanvas-title').textContent =
                a ? labels.editAssignment : labels.assignResource;

            fillPositions(lk.positions || [], a ? a.positionId : '');
            var notReady = document.getElementById('ra-position-not-ready');
            if (notReady) { notReady.classList.toggle('d-none', lk.positionReady !== false); }
            fill(document.getElementById('ra-coverage'), lk.coverageScopes || [], a ? a.coverageScope : '', true);
            fill(document.getElementById('ra-node'), lk.nodes || [], a ? a.territoryId : '', true);
            fill(document.getElementById('ra-business-units'), (lk.businessUnits || []), null, false);

            if (a && a.businessUnitScopes) {
                Array.prototype.forEach.call(document.getElementById('ra-business-units').options, function (o) {
                    o.selected = a.businessUnitScopes.indexOf(o.value) >= 0;
                });
            }

            // Resource: a real lookup when the platform exposes one, otherwise the documented PersonRef seam.
            var seamNote = document.getElementById('ra-resource-seam-note');
            if (lk.resourceLookupReady) {
                seamNote.classList.add('d-none');
            } else {
                seamNote.classList.remove('d-none');
            }
            fillResources(lk.resources || [], a ? a.resourceId : '', a);
            document.getElementById('ra-resource-type').value = 'user';
            document.getElementById('ra-primary').checked = a ? !!a.isPrimary : true;
            document.getElementById('ra-source').value = a ? a.assignmentSource : 'manual';
            document.getElementById('ra-reason').value = a && a.changeReason ? a.changeReason : '';
            document.getElementById('ra-from').value = a ? (a.validFrom || '').substring(0, 10) : (cfg.modelEffectiveFrom || '');
            document.getElementById('ra-to').value = a && a.validTo ? (a.validTo || '').substring(0, 10) : (cfg.modelEffectiveTo || '');

            initFormSelect2();
            syncResourceHidden();
            applyScopeShape();
            if (offcanvas) { offcanvas.show(); }
        });
    }

    function submit(event) {
        event.preventDefault();
        var errorBox = document.getElementById('ra-form-errors');
        errorBox.classList.add('d-none');

        var fd = new FormData(form);
        fd.set('IsPrimary', document.getElementById('ra-primary').checked ? 'true' : 'false');

        fetch(base + '/ResourceAssignments/SaveJson', { method: 'POST', body: fd })
            .then(function (r) { return r.json(); })
            .then(function (p) {
                if (!p.success) {
                    errorBox.innerHTML = (p.errors || [labels.gatewayError]).map(esc).join('<br>');
                    errorBox.classList.remove('d-none');
                    return;
                }
                if (offcanvas) { offcanvas.hide(); }
                var isUpdate = !!document.getElementById('ra-id').value;
                toast(isUpdate ? (labels.recordUpdated || labels.recordSaved) : (labels.recordCreated || labels.recordSaved), false);
                load();
            })
            .catch(function () {
                errorBox.textContent = labels.gatewayError;
                errorBox.classList.remove('d-none');
            });
    }

    function confirmThen(title, subtext, run) {
        if (window.showConfirm) {
            window.showConfirm(title, run, { type: 'danger', subtext: subtext || '', confirmButtonText: labels.confirm || title });
        } else if (window.confirm(title + ' ' + (subtext || ''))) {
            run();
        }
    }

    function post(url, extra) {
        var fd = new FormData();
        fd.append('__RequestVerificationToken', token());
        if (extra) {
            Object.keys(extra).forEach(function (k) {
                var value = extra[k];
                if (Array.isArray(value)) {
                    value.forEach(function (item) { fd.append(k, item); });
                } else {
                    fd.append(k, value === null || value === undefined ? '' : value);
                }
            });
        }
        fetch(url, { method: 'POST', body: fd })
            .then(function (r) { return r.json(); })
            .then(function (p) {
                if (!p.success) { toast((p.errors && p.errors[0]) || labels.gatewayError, true); return; }
                toast(labels.recordDeleted || labels.recordSaved, false);
                load();
            })
            .catch(function () { toast(labels.gatewayError, true); });
    }

    /**
     * Lifecycle prompts (End / Replace / Transfer) go through the platform-standard confirmation
     * (MOD-0013 window.showConfirm) — the same modal the Golden Reference Slim delete uses:
     * circled icon, centred title, entity badge, reversed btn-label-secondary cancel.
     * The extra fields are rendered into the confirmation body and validated inline, so the
     * confirm button stays disabled until every required field has a value.
     */
    function lifecyclePrompt(options, fields, onConfirm) {
        var values = {};

        if (typeof window.showConfirm !== 'function') { return; }

        var fieldsHtml = fields.map(function (f) {
            var control = f.type === 'select'
                ? '<select id="' + f.id + '" class="form-select">' + f.options + '</select>'
                : '<input id="' + f.id + '" type="' + esc(f.type || 'text') + '" class="form-control" value="'
                    + esc(f.value || '') + '" autocomplete="off">';
            return '<div class="mb-3">'
                + '<label class="form-label fw-medium mb-1" for="' + f.id + '">' + esc(f.label)
                + (f.required ? ' <span class="text-danger">*</span>' : '') + '</label>'
                + control
                + '<div class="invalid-feedback">' + esc(labels.fieldRequired || '') + '</div>'
                + '</div>';
        }).join('');

        var bodyHtml = '<div class="text-center text-muted">' + esc(options.subtext || '') + '</div>'
            + (options.entityName
                ? '<div class="text-center mt-2"><span class="badge bg-label-primary fs-6 py-2 px-3 fw-medium">'
                    + esc(options.entityName) + '</span></div>'
                : '')
            + '<div class="text-start mt-4">'
            + (options.note
                ? '<div class="alert alert-warning py-2 small"><i class="bx bx-plug me-1"></i>' + esc(options.note) + '</div>'
                : '')
            + fieldsHtml + '</div>';

        window.showConfirm(options.title, function () { onConfirm(values); }, {
            type: 'warning',
            width: '460px',
            subtext: bodyHtml,
            confirmButtonText: options.title,
            cancelButtonText: labels.cancel || '',
            didOpen: function (popup, SwalRef) {
                popup.querySelector('.swal2-html-container')?.classList.add('w-100', 'p-0', 'mx-0');
                // showConfirm wraps the body in a muted block; form controls must not inherit it.
                popup.querySelector('.swal2-html-container > .mb-2')?.classList.remove('mb-2', 'text-muted-500');
                var icon = popup.querySelector('.swal-icon-circle i');
                if (icon && options.icon) { icon.className = options.icon + ' text-warning'; }

                var controls = fields.map(function (f) { return popup.querySelector('#' + f.id); });

                function sync(markInvalid) {
                    var valid = true;
                    fields.forEach(function (f, i) {
                        var el = controls[i];
                        var value = el ? String(el.value || '').trim() : '';
                        values[f.name] = value;
                        // A picker carries its display fields as data- attributes on the option, the same way the
                        // Assign Resource offcanvas mirrors them into its hidden inputs.
                        if (f.optionData && el && el.tagName === 'SELECT') {
                            var opt = el.options[el.selectedIndex];
                            Object.keys(f.optionData).forEach(function (key) {
                                values[key] = opt ? (opt.getAttribute('data-' + f.optionData[key]) || '') : '';
                            });
                        }
                        var missing = !!f.required && !value;
                        if (missing) { valid = false; }
                        // Only paint red once the field has been touched, never on first open.
                        if (el && (markInvalid || !missing)) { el.classList.toggle('is-invalid', missing); }
                    });
                    if (valid) { SwalRef.enableConfirmButton(); } else { SwalRef.disableConfirmButton(); }
                }

                var $ = window.jQuery;
                var hasSelect2 = !!($ && $.fn && $.fn.select2);

                controls.forEach(function (el, i) {
                    if (!el) { return; }
                    if (hasSelect2 && fields[i].searchable && el.tagName === 'SELECT') {
                        // dropdownParent must be the popup, not the scrolling html container, or the list is clipped.
                        $(el).select2({
                            dropdownParent: $(popup),
                            width: '100%',
                            placeholder: '—',
                            allowClear: !fields[i].required
                        });
                        // select2 fires the change through jQuery, which never reaches native listeners.
                        $(el).on('change', function () { sync(true); });
                        return;
                    }
                    el.addEventListener('input', function () { sync(false); });
                    el.addEventListener('change', function () { sync(false); });
                    el.addEventListener('blur', function () { sync(true); });
                });

                sync(false);
                if (controls[0] && !fields[0].searchable) { controls[0].focus(); }
            }
        });
    }

    // ---------------------------------------------------------------- wiring

    if (form) {
        form.addEventListener('submit', submit);
        if (window.jQuery) {
            window.jQuery('#ra-coverage').on('change', applyScopeShape);
            window.jQuery('#ra-position').on('change', syncPositionHidden);
        } else {
            document.getElementById('ra-coverage').addEventListener('change', applyScopeShape);
            document.getElementById('ra-position').addEventListener('change', syncPositionHidden);
        }
        var picker = document.getElementById('ra-resource-picker');
        if (picker) {
            if (window.jQuery) { window.jQuery(picker).on('change', syncResourceHidden); }
            else { picker.addEventListener('change', syncResourceHidden); }
        }
    }

    // Row actions are delegated: the DataTable owns the tbody and re-renders it on every draw.
    document.addEventListener('click', function (event) {
        var edit = event.target.closest('.js-edit-ra');
        if (edit) {
            event.preventDefault();
            var found = cache.filter(function (x) { return String(x.id) === String(edit.dataset.id); })[0];
            if (found) { openForm(found); }
            return;
        }

        var del = event.target.closest('.js-delete-ra');
        if (del) {
            event.preventDefault();
            confirmThen(labels.deleteAssignment, del.dataset.name, function () {
                post(base + '/ResourceAssignments/' + del.dataset.id + '/DeleteJson');
            });
            return;
        }

        var end = event.target.closest('.js-end-ra');
        if (end) {
            event.preventDefault();
            lifecyclePrompt({
                title: labels.endAssignment,
                subtext: labels.endAssignmentConfirm,
                entityName: end.dataset.name,
                icon: 'bx bx-stop-circle'
            }, [
                { id: 'ra-life-date', name: 'effectiveDate', label: labels.effectiveDate, type: 'date', required: true, value: new Date().toISOString().substring(0, 10) },
                { id: 'ra-life-reason', name: 'reason', label: labels.changeReason, required: true }
            ], function (values) {
                post(base + '/ResourceAssignments/' + end.dataset.id + '/EndJson', values);
            });
            return;
        }

        var replace = event.target.closest('.js-replace-ra');
        if (replace) {
            event.preventDefault();
            var source = cache.find(function (x) { return String(x.id) === String(replace.dataset.id); });
            if (!source) { return; }
            ensureLookups().then(function (lk) {
                // Same person list and option shape as the Assign Resource offcanvas picker: the id is the option
                // value and the display name rides along as data-name, so the API still gets both.
                var resources = lk.resources || [];
                lifecyclePrompt({
                    title: labels.replaceAssignment,
                    subtext: labels.replaceAssignmentConfirm,
                    entityName: source.resourceDisplayName,
                    icon: 'bx bx-user-plus',
                    note: resources.length ? '' : labels.resourceLookupNotReady
                }, [
                    {
                        id: 'ra-replace-resource', name: 'resourceId', label: labels.selectUser, type: 'select',
                        required: true, searchable: true, optionData: { resourceDisplayName: 'name' },
                        options: '<option value="">—</option>' + resources.map(function (r) {
                            return '<option value="' + esc(r.value) + '"'
                                + ' data-name="' + esc(r.displayName || r.text) + '">'
                                + esc(r.text) + '</option>';
                        }).join('')
                    },
                    { id: 'ra-replace-date', name: 'effectiveDate', label: labels.effectiveDate, type: 'date', required: true, value: new Date().toISOString().substring(0, 10) },
                    { id: 'ra-replace-reason', name: 'reason', label: labels.changeReason, required: true }
                ], function (values) {
                    values.positionId = source.positionId || '';
                    values.positionCode = source.positionCode;
                    values.positionTitle = source.positionTitle || source.positionName;
                    post(base + '/ResourceAssignments/' + source.id + '/ReplaceJson', values);
                });
            });
            return;
        }

        var transfer = event.target.closest('.js-transfer-ra');
        if (transfer) {
            event.preventDefault();
            var transferSource = cache.find(function (x) { return String(x.id) === String(transfer.dataset.id); });
            if (!transferSource) { return; }
            ensureLookups().then(function (lk) {
                var nodeOptions = '<option value="">—</option>' + (lk.nodes || []).map(function (n) {
                    return '<option value="' + esc(n.value) + '">' + esc(n.text) + '</option>';
                }).join('');
                lifecyclePrompt({
                    title: labels.transferAssignment,
                    subtext: labels.transferAssignmentConfirm,
                    entityName: transferSource.resourceDisplayName,
                    icon: 'bx bx-transfer'
                }, [
                    { id: 'ra-transfer-node', name: 'targetTerritoryId', label: labels.targetNode, type: 'select', options: nodeOptions, required: true, searchable: true },
                    { id: 'ra-transfer-date', name: 'effectiveDate', label: labels.effectiveDate, type: 'date', required: true, value: new Date().toISOString().substring(0, 10) },
                    { id: 'ra-transfer-reason', name: 'reason', label: labels.changeReason, required: true }
                ], function (values) {
                    values.coverageScope = transferSource.coverageScope;
                    values.businessUnitScopeCodes = transferSource.businessUnitScopes || [];
                    post(base + '/ResourceAssignments/' + transferSource.id + '/TransferJson', values);
                });
            });
        }
    });

    loadSavedView().then(load);
})();
