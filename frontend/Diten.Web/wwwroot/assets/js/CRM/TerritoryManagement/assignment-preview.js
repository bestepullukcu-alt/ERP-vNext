/**
 * MOD-0151 FU03 — Assignment Preview page.
 *
 * Runs the model's assignment rules and shows candidates + conflicts. Scope comes from the page: with a ruleId it
 * evaluates that one rule, without it the whole model.
 *
 * Nothing here assigns anything — the preview endpoint persists nothing. Applying the result is the separate
 * account-assignments surface below the preview.
 */
(function () {
    'use strict';

    var root = document.getElementById('territory-assignment-data');
    var previewCard = document.getElementById('assignment-preview-card');
    if (!root || !previewCard) { return; }

    var cfg = JSON.parse(root.textContent || '{}');
    var labels = cfg.labels || {};
    var base = '/CRM/TerritoryManagement/Models/' + cfg.modelId;

    function esc(value) {
        var el = document.createElement('span');
        el.textContent = value === null || value === undefined ? '' : String(value);
        return el.innerHTML;
    }

    function token() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function toast(message, type) {
        window.showToast?.(message || '', type || 'info');
    }

    function stat(label, value, color, icon) {
        return '<div class="col-6 col-md-3">' +
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

    function conflictBadge(status) {
        if (status === 'conflict-winner') { return '<span class="badge bg-label-warning">' + esc(labels.conflictWinner) + '</span>'; }
        if (status === 'conflict-loser') { return '<span class="badge bg-label-danger">' + esc(labels.conflictLoser) + '</span>'; }
        return '<span class="badge bg-label-success">' + esc(labels.noConflict) + '</span>';
    }

    // Golden compact DataTable v2 builder. First run creates the table; reruns just swap the data in place.
    var tables = {};

    // Persisted (already-applied) coverage from the separate AccountAssignments store (preview persists nothing).
    // Keyed by accountCode — a stable string on both sides — to sidestep any GUID casing/format difference.
    // Baked into each matched row's `assignedNodes` so the "Assigned To" column renders it from the row itself;
    // refreshed after an apply so the just-assigned account shows where it landed.
    function loadPersisted() {
        fetch(base + '/AccountAssignments/Json').then(function (r) { return r.json(); }).then(function (p) {
            var items = (p && p.data && p.data.items) || (p && p.items) || [];
            var map = {};
            items.forEach(function (a) {
                if (String(a.assignmentStatus).toLowerCase() !== 'active') { return; }
                var key = a.accountCode || a.accountId;
                (map[key] = map[key] || []).push({ nodeCode: a.territoryNodeCode, nodeName: a.territoryNodeName });
            });
            // Bake the coverage INTO each row's data so DataTables renders it from the row itself. Reading an
            // external map inside render + draw() left the cached cell HTML stale (render ran, DOM stayed "—").
            if (tables.matched) {
                tables.matched.rows().every(function () {
                    var r = this.data();
                    r.assignedNodes = map[r.accountCode] || map[r.accountId] || [];
                    this.data(r);
                });
                tables.matched.draw(false);
            }
        }).catch(function () { /* leave the column showing — */ });
    }
    // account-assignments.js fires this after a successful apply.
    window.addEventListener('territory-assignments-applied', loadPersisted);
    function renderTable(key, el, data, config) {
        if (!el || !window.DtDefaults || !window.DataTable) { return; }
        if (tables[key]) {
            tables[key].clear().rows.add(data).draw(false);
            return;
        }
        tables[key] = new DataTable(el, window.DtDefaults.create($.extend(true, { data: data, stateSave: false, processing: true }, config)));
    }

    // Bulk-action bar: reflects the ticked Matched-Accounts rows. Selection itself feeds the Apply form below.
    function updateBulkBar() {
        var boxes = document.querySelectorAll('.preview-account-select');
        var count = document.querySelectorAll('.preview-account-select:checked').length;
        var countEl = document.getElementById('preview-bulk-count');
        if (countEl) { countEl.textContent = count; }
        var bar = document.getElementById('preview-bulk-bar');
        if (bar) { bar.classList.toggle('d-none', count === 0); }
        var selectAll = document.getElementById('preview-select-all');
        if (selectAll) {
            selectAll.checked = count > 0 && count === boxes.length;
            selectAll.indeterminate = count > 0 && count < boxes.length;
        }
        // Mirror the count into the Apply offcanvas so it reads correctly the moment it opens.
        var applyCount = document.getElementById('apply-selected-count');
        if (applyCount) { applyCount.textContent = count; }
    }

    function renderPreview(d) {
        previewCard.classList.remove('d-none');

        // Say plainly which scope produced these numbers — and, for a single rule, why Conflicts is empty.
        // Rendered inside the main "Preview does not assign accounts." info alert, not as a separate box.
        var scope = document.getElementById('preview-scope');
        if (scope) {
            scope.classList.remove('d-none');
            scope.innerHTML = cfg.ruleCode
                ? esc(labels.previewScopeSingle || '') + ' <strong>' + esc(cfg.ruleCode) + '</strong>'
                    + '<br>' + esc(labels.singleRuleNoConflicts || '')
                : esc(labels.previewScopeAll || '');
        }

        var meta = (labels.generatedAt || 'Generated') + ': ' + new Date(d.generatedAt).toLocaleString();
        if (d.effectiveAt && d.effectiveAt !== d.generatedAt) {
            meta += ' · ' + (labels.evaluatedAsOf || 'as of') + ' ' + new Date(d.effectiveAt).toLocaleDateString();
        }
        document.getElementById('preview-meta').textContent = meta + ' · run ' + d.previewRunId;

        document.getElementById('preview-stats').innerHTML =
            stat(labels.candidates, d.totalCandidateAccounts, 'primary', 'bx-group') +
            stat(labels.conflicts, d.conflictCount, d.conflictCount ? 'danger' : 'success', 'bx-error-circle') +
            stat(labels.scannedAccounts, d.scannedAccounts + ' / ' + d.totalTenantAccounts, 'info', 'bx-search-alt') +
            stat(labels.evaluatedRules, d.evaluatedRuleCount + ' / ' + (d.evaluatedRuleCount + d.skippedRuleCount), 'warning', 'bx-filter-alt');

        var warnings = (d.warnings || []).map(function (w) {
            return '<div class="alert alert-warning py-2"><i class="bx bx-error me-1"></i>' + esc(w) + '</div>';
        });
        document.getElementById('preview-warnings').innerHTML = warnings.join('');

        // Display-time escaper for plain text columns; sort/filter get the raw value.
        function td(data, type) { return type === 'display' ? esc(data) : (data == null ? '' : data); }

        // ---- Matched Accounts (golden compact, bulk-action variant) ----
        var matched = d.matchedAccounts || [];
        document.getElementById('preview-matched-count').textContent = matched.length;
        renderTable('matched', document.getElementById('dt-preview-matched'), matched, {
            order: [[2, 'asc']],
            language: { emptyTable: labels.noMatches || '' },
            buttons: window.DtDefaults.exportButtons(null, {}, {}, { exportColumns: [2, 3, 4, 5, 6, 7, 8], colvisColumns: [3, 4, 5, 6, 7, 8] }),
            columns: [
                { data: null, defaultContent: '', className: 'control', orderable: false, searchable: false },
                {
                    data: null, orderable: false, searchable: false, className: 'dt-checkboxes-cell',
                    // Data attributes are the contract with account-assignments.js, which builds the apply payload.
                    render: function (_d, _t, row) {
                        return '<input type="checkbox" class="form-check-input preview-account-select"' +
                            ' data-account-id="' + esc(row.accountId) + '"' +
                            ' data-node-id="' + esc(row.targetTerritoryNodeId) + '"' +
                            ' data-rule-id="' + esc(row.ruleId) + '" />';
                    }
                },
                { data: 'accountCode', render: td },
                { data: 'accountName', render: td },
                { data: null, render: function (_d, type, row) { return type === 'display' ? esc(row.targetTerritoryCode) + ' — ' + esc(row.targetTerritoryName) : (row.targetTerritoryCode || ''); } },
                { data: 'ruleCode', render: td },
                { data: 'matchReason', render: function (data, type) { return type === 'display' ? '<small class="text-muted">' + esc(data) + '</small>' : (data || ''); } },
                { data: 'conflictStatus', render: function (data, type) { return type === 'display' ? conflictBadge(data) : (data || ''); } },
                {
                    // Persisted coverage for this account (green badge with the node it's actually assigned to), or — if none.
                    // Reads row.assignedNodes, which loadPersisted() bakes into each row from the AccountAssignments store.
                    data: 'assignedNodes', orderable: false, searchable: false,
                    render: function (list, type) {
                        if (!list || !list.length) { return type === 'display' ? '<span class="text-muted">—</span>' : ''; }
                        if (type !== 'display') { return list.map(function (x) { return x.nodeCode; }).join(' '); }
                        return list.map(function (x) {
                            return '<span class="badge bg-label-success me-1" title="' + esc(x.nodeName) + '">' +
                                '<i class="bx bx-map-pin me-1"></i>' + esc(x.nodeCode) + '</span>';
                        }).join('');
                    }
                }
            ]
        });

        // ---- Conflicts ----
        var conflicts = d.conflicts || [];
        document.getElementById('preview-conflict-count').textContent = conflicts.length;
        renderTable('conflicts', document.getElementById('dt-preview-conflicts'), conflicts, {
            order: [[1, 'asc']],
            language: { emptyTable: labels.noConflicts || '' },
            buttons: window.DtDefaults.exportButtons(null, {}, {}, { exportColumns: [1, 2, 4], colvisColumns: [2, 3, 4] }),
            columns: [
                { data: null, defaultContent: '', className: 'control', orderable: false, searchable: false },
                { data: 'accountCode', render: td },
                { data: 'accountName', render: td },
                {
                    data: null, orderable: false, render: function (_d, type, row) {
                        if (type !== 'display') { return (row.candidateTerritoryNodes || []).length; }
                        return (row.candidateTerritoryNodes || []).map(function (n) {
                            return '<div>' + (n.isWinner ? '<i class="bx bx-check text-success me-1"></i>' : '<i class="bx bx-x text-muted me-1"></i>') +
                                esc(n.territoryCode) + ' (' + esc(n.ruleCode) + ', p' + esc(n.priority) + ')</div>';
                        }).join('');
                    }
                },
                { data: 'conflictPolicy', render: function (data, type) { return type === 'display' ? '<span class="badge bg-label-warning">' + esc(data) + '</span>' : (data || ''); } },
                { data: 'resolutionSuggestion', render: function (data, type) { return type === 'display' ? '<small>' + esc(data) + '</small>' : (data || ''); } }
            ]
        });

        // ---- Rule Summary ----
        renderTable('rules', document.getElementById('dt-preview-rules'), d.criteriaSummary || [], {
            order: [[1, 'asc']],
            language: { emptyTable: labels.noMatches || '' },
            buttons: window.DtDefaults.exportButtons(null, {}, {}, { exportColumns: [1, 2, 3, 4, 6], colvisColumns: [2, 3, 4, 5, 6] }),
            columns: [
                { data: null, defaultContent: '', className: 'control', orderable: false, searchable: false },
                { data: 'ruleCode', render: td },
                { data: 'ruleType', render: td },
                { data: 'priority', render: td },
                { data: 'criteriaSummary', render: function (data, type) { return type === 'display' ? '<small class="text-muted">' + esc(data) + '</small>' : (data || ''); } },
                {
                    data: 'evaluated', render: function (data, type, row) {
                        if (type !== 'display') { return data ? 1 : 0; }
                        return data
                            ? '<span class="badge bg-label-success">' + esc(labels.yes) + '</span>'
                            : '<span class="badge bg-label-secondary" title="' + esc(row.skipReason) + '">' + esc(row.skipReason) + '</span>';
                    }
                },
                { data: 'matchCount', render: td }
            ]
        });

        updateBulkBar();
        loadPersisted(); // fill the "Assigned To" column from already-persisted coverage

        // account-assignments.js picks the run id up from here.
        window.dispatchEvent(new CustomEvent('territory-preview-ready', { detail: d }));
    }

    function runPreview() {
        var body = new FormData();
        body.append('__RequestVerificationToken', token());
        if (cfg.ruleId) { body.append('ruleId', cfg.ruleId); }

        var button = document.getElementById('js-run-preview');
        if (button) { button.disabled = true; }

        fetch(base + '/AssignmentPreview', { method: 'POST', body: body })
            .then(function (r) { return r.json(); })
            .then(function (payload) {
                if (!payload.success || !payload.data) {
                    toast((payload.errors && payload.errors[0]) || labels.gatewayError, 'error');
                    return;
                }
                renderPreview(payload.data);
            })
            .catch(function () { toast(labels.gatewayError, 'error'); })
            .finally(function () { if (button) { button.disabled = false; } });
    }

    document.getElementById('js-run-preview')?.addEventListener('click', runPreview);

    // Select-all for the apply surface below.
    document.getElementById('preview-select-all')?.addEventListener('change', function (event) {
        document.querySelectorAll('.preview-account-select').forEach(function (box) {
            box.checked = event.target.checked;
            box.dispatchEvent(new Event('change', { bubbles: true }));
        });
    });

    // Keep the bulk bar in sync with individual row ticks (rows are re-rendered by DataTables, so delegate).
    document.addEventListener('change', function (e) {
        if (e.target && e.target.classList && e.target.classList.contains('preview-account-select')) {
            updateBulkBar();
        }
    });

    // "Apply selected" opens the Apply offcanvas — the dates/override live there. The ticked rows already fed
    // the hidden fields via account-assignments.js, so the offcanvas is ready the moment it slides in.
    document.getElementById('preview-bulk-apply')?.addEventListener('click', function () {
        if (String(cfg.modelStatus || '').toLowerCase() !== 'active') {
            toast(labels.applyRequiresActiveModel || 'Activate this territory model before applying account assignments.', 'warning');
            return;
        }
        var panel = document.getElementById('account-assignment-offcanvas');
        if (!panel || !window.bootstrap || !bootstrap.Offcanvas) {
            toast(labels.gatewayError || 'The apply form is not available.', 'error');
            return;
        }
        updateBulkBar();
        bootstrap.Offcanvas.getOrCreateInstance(panel).show();
        panel.querySelector('[name="EffectiveFrom"]')?.focus();
    });

    document.getElementById('preview-bulk-clear')?.addEventListener('click', function () {
        document.querySelectorAll('.preview-account-select').forEach(function (box) {
            if (box.checked) { box.checked = false; box.dispatchEvent(new Event('change', { bubbles: true })); }
        });
        updateBulkBar();
    });

    // A DataTable built inside a hidden tab-pane measures its columns wrong; recalc when the tab is shown.
    document.querySelectorAll('[data-bs-toggle="tab"]').forEach(function (btn) {
        btn.addEventListener('shown.bs.tab', function () {
            Object.keys(tables).forEach(function (k) {
                try { tables[k].columns.adjust().responsive.recalc(); } catch (e) { /* not built yet */ }
            });
        });
    });

    // The page exists to show a preview, so run it immediately instead of making the user press a button.
    if (cfg.autoRun !== false) { runPreview(); }
})();
