/**
 * MOD-0151 — Account assignment history grid (Golden Reference Compact DataTable v2).
 *
 * Read-only. The backend list is model-scoped, so a rule-scoped page filters client-side on appliedRuleCode —
 * which is exactly the field the apply step stamps onto every row it creates.
 */
(function () {
    'use strict';

    var root = document.getElementById('territory-history-data');
    var tableEl = document.getElementById('dt-assignmenthistory');
    if (!root || !tableEl) { return; }

    var cfg = JSON.parse(root.textContent || '{}');
    var labels = cfg.labels || {};
    var base = '/CRM/TerritoryManagement/Models/' + cfg.modelId;

    var rows = [];
    var table = null;
    var appliedFilters = { status: [], node: [], source: [] };

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

    var chips = window.TerritoryFilterChips;
    var normalizeArray = chips.normalizeArray;

    // The badge counts constrained FIELDS, like the golden pages — not how many values were picked inside them.
    var appliedFilterCount = function () { return chips.appliedFieldCount(appliedFilters); };

    function populateSelect(id, values) {
        var select = document.getElementById(id);
        if (!select) { return; }
        select.replaceChildren();
        values.forEach(function (v) { select.add(new Option(v, v)); });
    }

    function initFilterSelect2() {
        chips.initSelect2('#filterHistoryStatus, #filterHistoryNode, #filterHistorySource');
    }

    function refreshChips() {
        var distinct = function (prop) {
            return Array.from(new Set(rows.map(function (r) { return r[prop]; }).filter(Boolean))).sort();
        };
        populateSelect('filterHistoryStatus', distinct('assignmentStatus'));
        populateSelect('filterHistoryNode', distinct('territoryNodeCode'));
        populateSelect('filterHistorySource', distinct('assignmentSource'));
        initFilterSelect2();
        $('#filterHistoryStatus').val(appliedFilters.status).trigger('change');
        $('#filterHistoryNode').val(appliedFilters.node).trigger('change');
        $('#filterHistorySource').val(appliedFilters.source).trigger('change');
    }

    function statusBadge(status) {
        var tone = ({
            active: 'bg-label-success',
            proposed: 'bg-label-info',
            ended: 'bg-label-secondary',
            rejected: 'bg-label-danger'
        })[String(status || '').toLowerCase()] || 'bg-label-primary';
        return '<span class="badge ' + tone + '">' + esc(status || '—') + '</span>';
    }

    function buildTable() {
        table = new DataTable(tableEl, window.DtDefaults.create({
            data: rows,
            stateSave: false,
            colReorder: { columns: ':gt(0)' },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'accountCode', name: 'accountCode' },
                { data: 'accountDisplayName', name: 'accountDisplayName' },
                { data: 'territoryNodeCode', name: 'territoryNodeCode' },
                { data: 'assignmentStatus', name: 'assignmentStatus' },
                { data: 'appliedRuleCode', name: 'appliedRuleCode' },
                { data: 'assignmentSource', name: 'assignmentSource' },
                { data: 'effectiveFrom', name: 'effectiveFrom' },
                { data: 'effectiveTo', name: 'effectiveTo' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: function () { return ''; } },
                { targets: 1, render: function (data) { return '<span class="fw-medium text-heading">' + esc(data) + '</span>'; } },
                {
                    targets: 2,
                    render: function (data, type, row) {
                        return type === 'display'
                            ? '<a href="/CRM/Accounts/Details/' + esc(row.accountId) + '">' + esc(data) + '</a>'
                            : (data || '');
                    }
                },
                {
                    targets: 3,
                    render: function (data, type, row) {
                        return type === 'display'
                            ? esc(data) + ' <small class="text-muted">' + esc(row.territoryNodeName) + '</small>'
                            : (data || '');
                    }
                },
                { targets: 4, render: function (data, type) { return type === 'display' ? statusBadge(data) : (data || ''); } },
                { targets: 5, render: function (data, type) { return type === 'display' ? esc(data || '—') : (data || ''); } },
                { targets: 7, render: function (data, type) { return type === 'display' ? day(data) : (data || ''); } },
                { targets: 8, render: function (data, type) { return type === 'display' ? day(data) : (data || ''); } }
            ],
            buttons: window.DtDefaults.exportButtons(
                null,
                {},
                {
                    filterBtn: {
                        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                        attr: { title: labels.filter || '', 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false' },
                        action: function () { }
                    }
                },
                { exportColumns: [1, 2, 3, 4, 5, 6, 7, 8], colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8] }
            ),
            language: { emptyTable: labels.noHistory || '' },
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

                chips.bindToggle('assignmentHistoryFilterBound');
                refreshChips();
                api.draw(false);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), appliedFilterCount());
            }
        }));

        $.fn.dataTable.ext.search.push(function (settings, _data, dataIndex, rowData) {
            if (settings.nTable !== tableEl) { return true; }
            var row = rowData || table.row(dataIndex).data();
            if (!row) { return true; }
            return (!appliedFilters.status.length || appliedFilters.status.includes(row.assignmentStatus))
                && (!appliedFilters.node.length || appliedFilters.node.includes(row.territoryNodeCode))
                && (!appliedFilters.source.length || appliedFilters.source.includes(row.assignmentSource));
        });

        document.getElementById('btnFilterApply')?.addEventListener('click', function () {
            appliedFilters = {
                status: normalizeArray($('#filterHistoryStatus').val()),
                node: normalizeArray($('#filterHistoryNode').val()),
                source: normalizeArray($('#filterHistorySource').val())
            };
            table.draw();
            bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse')).hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', function (event) {
            event.preventDefault();
            appliedFilters = { status: [], node: [], source: [] };
            $('#filterHistoryStatus, #filterHistoryNode, #filterHistorySource').val(null).trigger('change');
            table.search('');
            table.draw();
        });
    }

    function load() {
        fetch(base + '/AccountAssignments/Json', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.json(); })
            .then(function (payload) {
                var items = (payload.data && payload.data.items) || [];
                // Rule-scoped page: keep only what this rule produced.
                rows = cfg.ruleCode
                    ? items.filter(function (a) { return a.appliedRuleCode === cfg.ruleCode; })
                    : items;

                if (!table) { buildTable(); } else { table.clear().rows.add(rows).draw(false); refreshChips(); }
                $('#skeleton-loader').fadeOut(150);
            })
            .catch(function () {
                rows = [];
                if (!table) { buildTable(); }
                $('#skeleton-loader').fadeOut(150);
            });
    }

    load();
})();
