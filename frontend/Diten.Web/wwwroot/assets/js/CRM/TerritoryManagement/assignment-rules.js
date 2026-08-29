/**
 * MOD-0151 FU03 — Territory assignment rules grid.
 *
 * The rules grid follows the Golden Reference Compact DataTable v2 contract (inline filter host relocated into the
 * toolbar row, Action/ColVis/Filter/SaveView buttons, Add New as the primary button, skeleton loader).
 *
 * Preview and assignment history are their own pages (pack §18 surfaces #6 and #7); the row actions navigate to
 * them. Gateway-only: every call goes to the Diten.Web proxy actions, which forward to :5000.
 */
(function () {
    'use strict';

    var root = document.getElementById('territory-assignment-data');
    var tableEl = document.getElementById('dt-assignmentrules');
    if (!root || !tableEl) { return; }

    var cfg = JSON.parse(root.textContent || '{}');
    var labels = cfg.labels || {};
    var base = '/CRM/TerritoryManagement/Models/' + cfg.modelId;

    var offcanvasEl = document.getElementById('assignment-rule-offcanvas');
    var form = document.getElementById('assignment-rule-form');
    var offcanvas = offcanvasEl && window.bootstrap ? new bootstrap.Offcanvas(offcanvasEl) : null;

    var personalizationClient = window.personalizationClient;
    var personalizationContext = { moduleKey: 'CRM', pageKey: 'TerritoryAssignmentRules' };

    var lookups = null;
    var rules = [];
    var table = null;
    var appliedFilters = { ruleType: [], conflictPolicy: [], enabled: [] };
    var savedViewRecord = null;
    var savedViewState = null;

    function esc(value) {
        var el = document.createElement('span');
        el.textContent = value === null || value === undefined ? '' : String(value);
        return el.innerHTML;
    }

    function token() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    /// Golden Reference Compact feedback: a toast, never a modal. (The shared DitenDataTable.reloadWithToast helper
    /// is for ajax-backed grids; this grid holds its rows client-side, so it toasts and reloads them itself — the
    /// same shape hierarchy.js uses.)
    function toast(message, isError) {
        window.showToast?.(message || '', isError ? 'error' : 'success');
    }

    var chips = window.TerritoryFilterChips;
    var normalizeArray = chips.normalizeArray;

    var appliedFilterCount = function () { return chips.appliedFieldCount(appliedFilters); };

    // ---------------------------------------------------------------- filter chips

    function populateSelect(id, values) {
        var select = document.getElementById(id);
        if (!select) { return; }
        select.replaceChildren();
        values.forEach(function (v) { select.add(new Option(v.text, v.value)); });
    }

    function initFilterSelect2() {
        chips.initSelect2('#filterRuleType, #filterConflictPolicy, #filterRuleEnabled');
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
                if (!error || !error.authHandled) { console.error('[TerritoryAssignmentRules SaveView] Load failed.', error); }
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

        // An existing record is UPDATED — posting it again through saveView would create a second default view,
        // which is why the second save appeared to do nothing.
        var id = savedViewRecord && (savedViewRecord.id || savedViewRecord.Id);
        return (id ? personalizationClient.updateView(id, request) : personalizationClient.saveView(request))
            .then(function (saved) {
                savedViewRecord = (saved && (saved.data || saved.Data)) || saved || request;
                savedViewState = request.viewDefinition;
                document.querySelector('.dt-save-filter-btn')?.classList.add('d-none');
                window.showToast?.(labels.recordSaved || labels.saveView || '', 'success');
            });
    }

    // ---------------------------------------------------------------- rules grid

    /// Rule ids that a model-wide preview reported as fighting over the same account. Loaded in the background
    /// after the grid renders, because a conflict only exists BETWEEN rules — the per-rule preview can never show
    /// one, and this grid is the only place the user sees all rules at once.
    var conflictRuleIds = new Set();

    function loadConflictBadges() {
        if (!rules.length) { return; }

        var body = new FormData();
        body.append('__RequestVerificationToken', token());

        fetch(base + '/AssignmentPreview', { method: 'POST', body: body })
            .then(function (r) { return r.json(); })
            .then(function (payload) {
                if (!payload.success || !payload.data) { return; }
                conflictRuleIds = new Set();
                (payload.data.conflicts || []).forEach(function (c) {
                    (c.conflictingRuleIds || []).forEach(function (id) { conflictRuleIds.add(String(id)); });
                });
                if (table && conflictRuleIds.size) { table.rows().invalidate().draw(false); }
            })
            .catch(function () { /* advisory only — a failed badge lookup must not break the grid */ });
    }

    function renderTarget(row) {
        return row.territoryCode
            ? esc(row.territoryCode) + ' <small class="text-muted">' + esc(row.territoryName) + '</small>'
            : '<span class="text-danger">' + esc(labels.missingNode || '') + '</span>';
    }

    function buildTable() {
        table = new DataTable(tableEl, window.DtDefaults.create({
            data: rules,
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'ruleCode', name: 'ruleCode' },
                { data: 'name', name: 'name' },
                { data: 'territoryCode', name: 'territoryCode' },
                { data: 'ruleType', name: 'ruleType' },
                { data: 'criteriaSummary', name: 'criteriaSummary' },
                { data: 'priority', name: 'priority' },
                { data: 'conflictPolicy', name: 'conflictPolicy' },
                { data: 'isEnabled', name: 'isEnabled' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: function () { return ''; } },
                {
                    targets: 1,
                    render: function (data, type, row) {
                        if (type !== 'display') { return data || ''; }
                        var badge = conflictRuleIds.has(String(row.id))
                            ? ' <span class="badge bg-label-danger" title="' + esc(labels.ruleHasConflict || '') + '">'
                                + '<i class="bx bx-error-circle"></i></span>'
                            : '';
                        return '<span class="fw-medium text-heading">' + esc(data) + '</span>' + badge;
                    }
                },
                { targets: 3, render: function (_d, type, row) { return type === 'display' ? renderTarget(row) : (row.territoryCode || ''); } },
                {
                    targets: 4,
                    render: function (data, type) {
                        return type === 'display' ? '<span class="badge bg-label-info">' + esc(data) + '</span>' : (data || '');
                    }
                },
                { targets: 5, render: function (data, type) { return type === 'display' ? '<small class="text-muted">' + esc(data) + '</small>' : (data || ''); } },
                {
                    targets: 8,
                    render: function (data, type) {
                        if (type !== 'display') { return data ? 1 : 0; }
                        return data
                            ? '<span class="badge bg-label-success">' + esc(labels.enabled) + '</span>'
                            : '<span class="badge bg-label-secondary">' + esc(labels.disabled) + '</span>';
                    }
                },
                {
                    targets: -1,
                    title: labels.actions || '',
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end',
                    render: function (id, _type, row) {
                        // Previewing a single rule only needs read access — it writes nothing.
                        // Preview and history are read-only screens, so read access is enough to open them.
                        var actions = [{
                            key: 'previewRule',
                            className: 'js-preview-rule',
                            icon: 'bx bx-play-circle',
                            text: labels.previewThisRule || '',
                            attrs: { 'data-id': id, 'data-code': row.ruleCode }
                        }, {
                            key: 'historyRule',
                            className: 'js-history-rule',
                            icon: 'bx bx-history',
                            text: labels.historyThisRule || '',
                            attrs: { 'data-id': id, 'data-code': row.ruleCode }
                        }];

                        if (cfg.canEditRules) {
                            actions.push({ key: 'edit', className: 'js-edit-rule', icon: 'bx bx-edit', text: labels.edit || '', attrs: { 'data-id': id } });
                            actions.push({ key: 'deleteRule', className: 'js-delete-rule', icon: 'bx bx-trash', text: labels.deleteRule || '', attrs: { 'data-id': id, 'data-code': row.ruleCode } });
                        }

                        return window.DitenDataTable.renderActions(actions);
                    }
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                cfg.canEditRules ? (labels.createRule || '') : null,
                {},
                {
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
                                if (!error || !error.authHandled) { console.error('[TerritoryAssignmentRules SaveView] Save failed.', error); }
                            });
                        }
                    }
                },
                { exportColumns: [1, 2, 3, 4, 5, 6, 7, 8], colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8] }
            ),
            language: { emptyTable: labels.noRules || '' },
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
                chips.bindToggle('assignmentRulesFilterBound');
                refreshFilterChips();
                if (savedViewState && savedViewState.search) { api.search(savedViewState.search); }
                Object.entries((savedViewState && savedViewState.colVis) || {}).forEach(function (entry) {
                    api.column(Number(entry[0])).visible(!!entry[1], false);
                });
                if (savedViewState && Array.isArray(savedViewState.columnOrder) && api.colReorder && api.colReorder.order) {
                    api.colReorder.order(savedViewState.columnOrder, true);
                }
                api.draw(false);

                if (cfg.canEditRules) {
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

        // Scoped to THIS table: the page hosts other grids (preview, assignment history).
        $.fn.dataTable.ext.search.push(function (settings, _data, dataIndex, rowData) {
            if (settings.nTable !== tableEl) { return true; }
            var row = rowData || table.row(dataIndex).data();
            if (!row) { return true; }
            var enabledKey = row.isEnabled ? 'true' : 'false';
            return (!appliedFilters.ruleType.length || appliedFilters.ruleType.includes(row.ruleType))
                && (!appliedFilters.conflictPolicy.length || appliedFilters.conflictPolicy.includes(row.conflictPolicy))
                && (!appliedFilters.enabled.length || appliedFilters.enabled.includes(enabledKey));
        });

        document.getElementById('btnFilterApply')?.addEventListener('click', function () {
            appliedFilters = {
                ruleType: normalizeArray($('#filterRuleType').val()),
                conflictPolicy: normalizeArray($('#filterConflictPolicy').val()),
                enabled: normalizeArray($('#filterRuleEnabled').val())
            };
            table.draw();
            document.querySelector('.dt-save-filter-btn')?.classList.remove('d-none');
            bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse')).hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', function (event) {
            event.preventDefault();
            appliedFilters = { ruleType: [], conflictPolicy: [], enabled: [] };
            $('#filterRuleType, #filterConflictPolicy, #filterRuleEnabled').val(null).trigger('change');
            table.search('');
            table.draw();
            document.querySelector('.dt-save-filter-btn')?.classList.remove('d-none');
        });
    }

    function refreshFilterChips() {
        var distinct = function (prop) {
            return Array.from(new Set(rules.map(function (r) { return r[prop]; }).filter(Boolean))).sort();
        };
        populateSelect('filterRuleType', distinct('ruleType').map(function (v) { return { value: v, text: v }; }));
        populateSelect('filterConflictPolicy', distinct('conflictPolicy').map(function (v) { return { value: v, text: v }; }));
        populateSelect('filterRuleEnabled', [
            { value: 'true', text: labels.enabled || 'Enabled' },
            { value: 'false', text: labels.disabled || 'Disabled' }
        ]);
        initFilterSelect2();
        $('#filterRuleType').val(appliedFilters.ruleType).trigger('change');
        $('#filterConflictPolicy').val(appliedFilters.conflictPolicy).trigger('change');
        $('#filterRuleEnabled').val(appliedFilters.enabled).trigger('change');
    }

    function loadRules() {
        return fetch(base + '/AssignmentRules/Json', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.json(); })
            .then(function (payload) {
                if (!payload.success || !payload.data) {
                    toast((payload.errors && payload.errors[0]) || labels.gatewayError, true);
                    rules = [];
                } else {
                    rules = payload.data.items || [];
                }

                if (!table) {
                    buildTable();
                } else {
                    table.clear().rows.add(rules).draw(false);
                    refreshFilterChips();
                }
                $('#skeleton-loader').fadeOut(150);
                loadConflictBadges();
            })
            .catch(function () {
                toast(labels.gatewayError, true);
                $('#skeleton-loader').fadeOut(150);
            });
    }

    // ---------------------------------------------------------------- rule form

    /// Pre-fills the rule code on create, mirroring the node form's TN generator. It is only a suggestion — the
    /// field stays editable, and the code is immutable only AFTER the rule exists.
    function createDefaultRuleCode() {
        var now = new Date();
        var pad = function (value) { return String(value).padStart(2, '0'); };
        return 'AR-' + now.getFullYear() + pad(now.getMonth() + 1) + pad(now.getDate())
            + '-' + pad(now.getHours()) + pad(now.getMinutes()) + pad(now.getSeconds());
    }

    /// Single-value select. A leading blank option is what lets select2 show its placeholder.
    function fillSelect(select, options, selected) {
        select.innerHTML = '<option value="">—</option>' + options.map(function (o) {
            return '<option value="' + esc(o.value) + '"' + (o.value === selected ? ' selected' : '') + '>' + esc(o.text) + '</option>';
        }).join('');
    }

    /// Multi-value select for the criteria fields — no blank option, selection is what marks the values.
    function fillMultiSelect(select, options, selectedValues) {
        var chosen = (selectedValues || []).map(function (v) { return String(v).toLowerCase(); });
        select.innerHTML = (options || []).map(function (o) {
            var isSelected = chosen.indexOf(String(o.value).toLowerCase()) >= 0;
            return '<option value="' + esc(o.value) + '"' + (isSelected ? ' selected' : '') + '>' + esc(o.text) + '</option>';
        }).join('');
    }

    /// select2 inside an offcanvas must anchor its dropdown to the offcanvas, otherwise it renders clipped/behind.
    function initFormSelect2(ids) {
        if (!window.jQuery || !$.fn.select2 || !offcanvasEl) { return; }
        ids.forEach(function (id) {
            var $select = $('#' + id);
            if (!$select.length) { return; }
            if ($select.hasClass('select2-hidden-accessible')) { $select.select2('destroy'); }
            $select.select2({
                dropdownParent: $(offcanvasEl),
                placeholder: $select.data('placeholder') || '',
                width: '100%',
                allowClear: !$select.prop('multiple'),
                closeOnSelect: !$select.prop('multiple')
            });
        });
    }

    var CRITERIA_FIELDS = [
        { id: 'rule-countries', key: 'countryRefs' },
        { id: 'rule-cities', key: 'cityRefs' },
        { id: 'rule-districts', key: 'districtRefs' },
        { id: 'rule-account-types', key: 'accountTypes' },
        { id: 'rule-account-categories', key: 'accountCategories' },
        { id: 'rule-account-statuses', key: 'accountStatuses' }
    ];

    function ensureLookups() {
        if (lookups) { return Promise.resolve(lookups); }
        return fetch(base + '/AssignmentRules/lookups')
            .then(function (r) { return r.json(); })
            .then(function (data) { lookups = data; return lookups; });
    }

    function openForm(rule) {
        ensureLookups().then(function (lk) {
            form.reset();
            document.getElementById('rule-form-errors').classList.add('d-none');
            document.getElementById('rule-id').value = rule ? rule.id : '';
            document.getElementById('assignment-rule-offcanvas-title').textContent =
                rule ? labels.editRule : labels.createRule;

            // The hint explains the pre-filled code; on edit the field is locked, so it would only mislead.
            document.getElementById('rule-code-hint')?.classList.toggle('d-none', !!rule);

            fillSelect(document.getElementById('rule-territory'), lk.nodes || [], rule ? rule.territoryId : '');
            fillSelect(document.getElementById('rule-type'), lk.ruleTypes || [], rule ? rule.ruleType : '');
            fillSelect(document.getElementById('rule-policy'), lk.conflictPolicies || [], rule ? rule.conflictPolicy : '');

            if (rule) {
                document.getElementById('rule-code').value = rule.ruleCode;
                document.getElementById('rule-code').readOnly = true;
                document.getElementById('rule-name').value = rule.name;
                document.getElementById('rule-priority').value = rule.priority;
                document.getElementById('rule-enabled').checked = !!rule.isEnabled;
                document.getElementById('rule-from').value = (rule.effectiveFrom || '').substring(0, 10);
                document.getElementById('rule-to').value = (rule.effectiveTo || '').substring(0, 10);
            } else {
                document.getElementById('rule-code').readOnly = false;
                document.getElementById('rule-code').value = createDefaultRuleCode();
                document.getElementById('rule-priority').value = 100;
                document.getElementById('rule-enabled').checked = true;
                document.getElementById('rule-from').value = cfg.modelEffectiveFrom || '';
                document.getElementById('rule-to').value = cfg.modelEffectiveTo || '';
            }

            // Criteria options always come from the lookups payload; the rule's own values only decide what is
            // pre-selected. A value that is no longer published simply does not come back — which is the point.
            var criteria = (rule && rule.criteria) || {};
            var lkCriteria = lk.criteria || {};
            CRITERIA_FIELDS.forEach(function (field) {
                var select = document.getElementById(field.id);
                if (select) { fillMultiSelect(select, lkCriteria[field.key] || [], criteria[field.key] || []); }
            });

            var notReady = document.getElementById('rule-criteria-not-ready');
            if (notReady) {
                var missing = lk.criteriaNotReady || [];
                notReady.classList.toggle('d-none', missing.length === 0);
                notReady.textContent = missing.length
                    ? (labels.criteriaNotReady || '') + ' ' + missing.join(', ')
                    : '';
            }

            initFormSelect2(['rule-territory', 'rule-type', 'rule-policy']
                .concat(CRITERIA_FIELDS.map(function (f) { return f.id; })));

            if (offcanvas) { offcanvas.show(); }
        });
    }

    function submitForm(event) {
        event.preventDefault();
        var errorBox = document.getElementById('rule-form-errors');
        errorBox.classList.add('d-none');

        var body = new FormData(form);
        body.set('IsEnabled', document.getElementById('rule-enabled').checked ? 'true' : 'false');

        fetch(base + '/AssignmentRules/SaveJson', { method: 'POST', body: body })
            .then(function (r) { return r.json(); })
            .then(function (payload) {
                if (!payload.success) {
                    errorBox.innerHTML = (payload.errors || [labels.gatewayError]).map(esc).join('<br>');
                    errorBox.classList.remove('d-none');
                    return;
                }
                if (offcanvas) { offcanvas.hide(); }
                // Create vs update gets its own message, like the golden grids' RecordCreated / RecordUpdated.
                var isUpdate = !!document.getElementById('rule-id').value;
                toast(isUpdate ? (labels.recordUpdated || labels.recordSaved) : (labels.recordCreated || labels.recordSaved), false);
                loadRules();
            })
            .catch(function () {
                errorBox.textContent = labels.gatewayError;
                errorBox.classList.remove('d-none');
            });
    }

    /// Uses the shell's shared confirm — the same dialog every Golden Reference Compact grid shows for a delete,
    /// including the danger styling and the entity name in the prompt.
    function deleteRule(id, code) {
        window.showConfirm?.(labels.deleteRule || '', function () {
            var body = new FormData();
            body.append('__RequestVerificationToken', token());
            fetch(base + '/AssignmentRules/' + id + '/DeleteJson', { method: 'POST', body: body })
                .then(function (r) { return r.json(); })
                .then(function (payload) {
                    if (!payload.success) {
                        toast((payload.errors && payload.errors[0]) || labels.gatewayError, true);
                        return;
                    }
                    toast(labels.recordDeleted || labels.recordSaved, false);
                    loadRules();
                })
                .catch(function () { toast(labels.gatewayError, true); });
        }, {
            entityName: code || String(id),
            type: 'danger',
            subtext: labels.deleteRuleConfirm || '',
            confirmButtonText: labels.deleteRule || labels.confirm || ''
        });
    }

    // ---------------------------------------------------------------- wiring

    if (form) { form.addEventListener('submit', submitForm); }

    // Row actions are delegated: the DataTable owns the tbody and re-renders it on every draw.
    document.addEventListener('click', function (event) {
        var previewBtn = event.target.closest('.js-preview-rule');
        if (previewBtn) {
            event.preventDefault();
            window.location.href = base + '/AssignmentRules/' + previewBtn.dataset.id + '/Preview';
            return;
        }

        var historyBtn = event.target.closest('.js-history-rule');
        if (historyBtn) {
            event.preventDefault();
            window.location.href = base + '/AssignmentRules/' + historyBtn.dataset.id + '/History';
            return;
        }

        var editBtn = event.target.closest('.js-edit-rule');
        if (editBtn) {
            event.preventDefault();
            var rule = rules.find(function (x) { return String(x.id) === String(editBtn.dataset.id); });
            if (rule) { openForm(rule); }
            return;
        }

        var deleteBtn = event.target.closest('.js-delete-rule');
        if (deleteBtn) {
            event.preventDefault();
            deleteRule(deleteBtn.dataset.id, deleteBtn.dataset.code);
        }
    });

    loadSavedView().then(loadRules);
})();
