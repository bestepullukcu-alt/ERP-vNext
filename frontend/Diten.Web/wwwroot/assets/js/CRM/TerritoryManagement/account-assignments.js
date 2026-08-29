(function () {
    'use strict';
    var cfgEl = document.getElementById('territory-assignment-data');
    if (!cfgEl) return;
    var cfg = JSON.parse(cfgEl.textContent);
    var base = '/CRM/TerritoryManagement/Models/' + cfg.modelId;
    var body = document.getElementById('account-assignment-history-body');
    var form = document.getElementById('account-assignment-apply-form');
    var esc = function (v) { var d = document.createElement('div'); d.textContent = v == null ? '' : String(v); return d.innerHTML; };

    // Golden slim-create toast (same helper the resource-assignments form uses) instead of a blocking Swal modal.
    function toast(message, isError) {
        if (window.showToast) { window.showToast(message || '', isError ? 'error' : 'success'); }
        else if (isError) { window.alert(message); }
    }

    var fromInput = form ? form.querySelector('[name="EffectiveFrom"]') : null;
    var toInput = form ? form.querySelector('[name="EffectiveTo"]') : null;
    var modelFrom = cfg.modelEffectiveFrom || '';
    var modelTo = cfg.modelEffectiveTo || '';
    var nodeFromMap = {}; // territoryNodeId -> 'yyyy-MM-dd' effective-from (from AssignmentRules/lookups)

    // Highest effective-from among the currently ticked target nodes ('' when none/unknown). ISO strings sort.
    function selectedNodeFloor() {
        var floor = '';
        document.querySelectorAll('.preview-account-select:checked').forEach(function (x) {
            var f = nodeFromMap[x.dataset.nodeId];
            if (f && f > floor) { floor = f; }
        });
        return floor;
    }

    // EffectiveFrom must be >= the target node's own effective-from (and >= model start). Floor the picker + value
    // so applying to a node that only becomes effective later can't trip the backend "node must be effective" 409.
    function syncEffectiveFrom() {
        if (!fromInput) { return; }
        var nodeFloor = selectedNodeFloor();
        var floor = (nodeFloor && (!modelFrom || nodeFloor > modelFrom)) ? nodeFloor : modelFrom;
        if (!floor) { return; }
        fromInput.min = floor;
        if (!fromInput.value || fromInput.value < floor) {
            fromInput.value = floor;
            fromInput.dispatchEvent(new Event('change', { bubbles: true }));
        }
    }

    // Default the apply window to the model's own window and constrain the date pickers to it, so the backend
    // "Assignment effective window must stay inside the territory model window" rule can't be tripped by an
    // out-of-range date. The user is still free to pick any date within [floor, modelTo].
    (function initApplyWindow() {
        if (!form) { return; }
        if (fromInput) {
            if (modelFrom) { fromInput.min = modelFrom; if (!fromInput.value) { fromInput.value = modelFrom; } }
            if (modelTo) { fromInput.max = modelTo; }
        }
        if (toInput) {
            if (modelFrom) { toInput.min = modelFrom; }
            if (modelTo) { toInput.max = modelTo; if (!toInput.value) { toInput.value = modelTo; } }
        }
        // Let the required-fields tracker pick up the pre-filled values.
        [fromInput, toInput].forEach(function (el) { if (el) { el.dispatchEvent(new Event('change', { bubbles: true })); } });
    }());

    function loadHistory() {
        if (!body) return;
        fetch(base + '/AccountAssignments/Json').then(function (r) { return r.json(); }).then(function (p) {
            var items = p.data && p.data.items || [];
            body.innerHTML = items.length ? items.map(function (a) {
                return '<tr><td><a href="/CRM/Accounts/Details/' + esc(a.accountId) + '">' + esc(a.accountCode) + ' — ' + esc(a.accountDisplayName) +
                    '</a></td><td>' + esc(a.territoryNodeCode) + ' — ' + esc(a.territoryNodeName) + '</td><td>' +
                    esc((a.effectiveFrom || '').substring(0, 10)) + '</td><td>' + esc((a.effectiveTo || '').substring(0, 10) || '—') +
                    '</td><td><span class="badge bg-label-' + (a.assignmentStatus === 'active' ? 'success' : 'secondary') + '">' +
                    esc(a.assignmentStatus) + '</span></td><td>' + esc(a.appliedRuleCode || '—') + '</td></tr>';
            }).join('') : '<tr><td colspan="6" class="text-center text-muted py-3">No assignment history.</td></tr>';
        });
    }
    // Conflict Policy is reference-driven (MOD-0048 territory-conflict-policy) — same lookups endpoint the rule
    // form uses. Defaults to "block" (the historical fixed value); unpublished set → disable + not-ready notice.
    var policySelect = document.getElementById('apply-conflict-policy');
    function fillConflictPolicy(options, ready) {
        if (!policySelect) return;
        var warn = document.getElementById('apply-conflict-policy-not-ready');
        if (!ready || !options.length) {
            policySelect.innerHTML = '';
            policySelect.disabled = true;
            if (warn) warn.classList.remove('d-none');
            return;
        }
        policySelect.disabled = false;
        if (warn) warn.classList.add('d-none');
        var hasBlock = options.some(function (o) { return o.value === 'block'; });
        policySelect.innerHTML = options.map(function (o) {
            var sel = (hasBlock ? o.value === 'block' : false) ? ' selected' : '';
            return '<option value="' + esc(o.value) + '"' + sel + '>' + esc(o.text) + '</option>';
        }).join('');
        if (!hasBlock) policySelect.selectedIndex = 0;
        policySelect.dispatchEvent(new Event('change', { bubbles: true }));
    }
    if (form) {
        fetch(base + '/AssignmentRules/lookups').then(function (r) { return r.json(); })
            .then(function (lk) {
                fillConflictPolicy(lk.conflictPolicies || [], lk.conflictPolicyReady !== false);
                (lk.nodes || []).forEach(function (n) { if (n && n.effectiveFrom) { nodeFromMap[n.value] = n.effectiveFrom; } });
                syncEffectiveFrom(); // in case rows were already ticked before lookups resolved
            })
            .catch(function () { fillConflictPolicy([], false); });
    }
    function selected() {
        return Array.prototype.slice.call(document.querySelectorAll('.preview-account-select:checked')).map(function (x) {
            return { accountId: x.dataset.accountId, territoryNodeId: x.dataset.nodeId, ruleId: x.dataset.ruleId };
        });
    }
    document.addEventListener('change', function (e) {
        if (e.target.classList.contains('preview-account-select')) {
            var rows = selected();
            var selectedRowsInput = document.getElementById('apply-selected-rows');
            var applyButton = document.getElementById('apply-selected-button');
            if (selectedRowsInput) { selectedRowsInput.value = JSON.stringify(rows); }
            if (applyButton) { applyButton.disabled = rows.length === 0; }
            syncEffectiveFrom();
        }
    });
    window.addEventListener('territory-preview-ready', function (e) {
        var run = document.getElementById('apply-preview-run-id');
        if (run) run.value = e.detail.previewRunId || '';
    });
    var ov = document.getElementById('apply-override');
    if (ov) ov.addEventListener('change', function () {
        document.getElementById('override-reason-wrap').classList.toggle('d-none', !ov.checked);
        document.getElementById('apply-conflict-warning').classList.toggle('d-none', !ov.checked);
    });
    if (form) form.addEventListener('submit', function (e) {
        e.preventDefault();
        fetch(base + '/AccountAssignments/ApplyJson', { method: 'POST', body: new FormData(form) })
            .then(function (r) { return r.json(); }).then(function (p) {
                if (!p.success) throw new Error((p.errors || ['Apply failed.'])[0]);
                toast('Assignments applied.');
                var panel = document.getElementById('account-assignment-offcanvas');
                if (panel && window.bootstrap && bootstrap.Offcanvas) { bootstrap.Offcanvas.getOrCreateInstance(panel).hide(); }
                // Let the preview grid refresh its "Assigned To" column with the freshly persisted coverage.
                window.dispatchEvent(new CustomEvent('territory-assignments-applied'));
                loadHistory();
            }).catch(function (err) {
                toast(err.message, true);
            });
    });
    loadHistory();
}());
