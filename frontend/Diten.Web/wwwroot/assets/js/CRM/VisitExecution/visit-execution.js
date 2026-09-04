/*
 * MOD-0155 FU02 — Visit Report EXECUTION calendar.
 * Bespoke tenant-shell Day/Week calendar (NOT a Golden DataTable). Every call is a same-origin proxy to the Gateway
 * under /CRM/VisitExecution/api/*; the browser never sees a service URL or a bearer token. The rep views the FU01 plan
 * atoms in a window, marks each done/missed/rescheduled inline, records the immutable Visit Report and files amendments.
 */
(function () {
    'use strict';

    var root = document.getElementById('ve-root');
    if (!root) { return; }

    var L = window.VisitExecutionL10n || {};
    var base = '/CRM/VisitExecution/api';
    var canRecord = root.getAttribute('data-can-record') === 'true';
    var canAmend = root.getAttribute('data-can-amend') === 'true';
    var viewMode = 'day';
    var outcomeCodes = [];
    var offcanvas = null;

    function api(path, options) {
        options = options || {};
        options.credentials = 'same-origin';
        options.headers = Object.assign({ 'Content-Type': 'application/json' }, options.headers || {});
        return fetch(base + path, options).then(function (r) {
            return r.text().then(function (text) {
                var body = null;
                try { body = text ? JSON.parse(text) : null; } catch (e) { body = null; }
                return { ok: r.ok, status: r.status, body: body };
            });
        });
    }

    function el(id) { return document.getElementById(id); }
    function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; }
    function iso(d) { return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }
    function toast(msg) { if (window.L10n && window.showToast) { window.showToast(msg); } else { console.log(msg); } }

    function anchorDate() {
        var v = el('ve-anchor').value;
        return v ? new Date(v + 'T00:00:00') : new Date();
    }

    function windowRange() {
        var anchor = anchorDate();
        if (viewMode === 'day') { return { from: iso(anchor), to: iso(anchor) }; }
        var day = anchor.getDay();
        var monday = new Date(anchor); monday.setDate(anchor.getDate() - ((day + 6) % 7));
        var sunday = new Date(monday); sunday.setDate(monday.getDate() + 6);
        return { from: iso(monday), to: iso(sunday) };
    }

    // ── loaders ──────────────────────────────────────────────────────────────────────────────────────────────────

    function loadContract() {
        return api('/contract').then(function (r) {
            var vocab = r.body && (r.body.data || r.body);
            var v = vocab && vocab.vocabularies;
            // Outcome codes are reference-data-driven (F-RD); until the set is published the datalist stays empty
            // rather than showing a hardcoded fallback list.
            outcomeCodes = (vocab && vocab.outcomeCodes) || [];
            var dl = el('ve-outcome-codes');
            dl.innerHTML = '';
            outcomeCodes.forEach(function (c) { var o = document.createElement('option'); o.value = c; dl.appendChild(o); });
        }).catch(function () { /* contract is advisory for the dropdowns */ });
    }

    function loadCalendar() {
        var range = windowRange();
        var resource = el('ve-resource').value.trim();
        el('ve-window-label').textContent = range.from + (range.to !== range.from ? ' → ' + range.to : '');
        var qs = '?from=' + encodeURIComponent(range.from) + '&to=' + encodeURIComponent(range.to)
            + (resource ? '&resourceId=' + encodeURIComponent(resource) : '');
        return api('/calendar' + qs).then(function (r) {
            var data = r.body && (r.body.data || r.body);
            renderCalendar((data && data.items) || [], range);
        });
    }

    // ── rendering ────────────────────────────────────────────────────────────────────────────────────────────────

    function dateList(range) {
        var out = [], cur = new Date(range.from + 'T00:00:00'), end = new Date(range.to + 'T00:00:00');
        while (cur <= end) { out.push(iso(cur)); cur.setDate(cur.getDate() + 1); }
        return out;
    }

    function stateBadge(state) {
        var map = { none: 'bg-label-secondary', draft: 'bg-label-warning', submitted: 'bg-label-success', amended: 'bg-label-info' };
        var label = L[state] || state;
        return '<span class="badge ' + (map[state] || 'bg-label-secondary') + '">' + esc(label) + '</span>';
    }

    function renderCalendar(items, range) {
        var container = el('ve-calendar');
        container.innerHTML = '';
        var empty = el('ve-empty');
        empty.classList.toggle('d-none', items.length !== 0);

        var byDate = {};
        items.forEach(function (it) { (byDate[it.plannedDate] = byDate[it.plannedDate] || []).push(it); });

        dateList(range).forEach(function (day) {
            var col = document.createElement('div');
            col.className = 'flex-grow-1';
            col.style.minWidth = '260px';
            var header = '<div class="fw-semibold small text-muted mb-2 border-bottom pb-1">' + esc(day) + '</div>';
            var cells = (byDate[day] || []).map(renderCell).join('');
            col.innerHTML = header + (cells || '<div class="text-muted small">—</div>');
            container.appendChild(col);
        });

        Array.prototype.forEach.call(container.querySelectorAll('[data-action]'), function (btn) {
            btn.addEventListener('click', onCellAction);
        });
    }

    function renderCell(it) {
        var time = it.slotStartTime || it.plannedStartTime || '';
        var stage = it.plannedStageIndex != null ? ('#' + it.plannedStageIndex) : '';
        var actions = '';
        if (canRecord) {
            actions =
                '<div class="btn-group btn-group-sm mt-2 w-100" role="group">'
                + btn(it, 'completed', 'btn-outline-success', L.markCompleted)
                + btn(it, 'missed', 'btn-outline-danger', L.markMissed)
                + btn(it, 'rescheduled', 'btn-outline-warning', L.markRescheduled)
                + '</div>'
                + '<button type="button" class="btn btn-sm btn-primary w-100 mt-1" data-action="report" data-id="'
                + esc(it.plannedVisitId) + '">' + esc(L.report || 'Report') + '</button>';
        }
        return ''
            + '<div class="card mb-2" data-planned-visit="' + esc(it.plannedVisitId) + '">'
            + '  <div class="card-body p-2">'
            + '    <div class="d-flex justify-content-between align-items-start">'
            + '      <div class="small fw-semibold">' + esc(it.visitCode) + '</div>' + stateBadge(it.reportState)
            + '    </div>'
            + '    <div class="small text-muted">' + esc(time) + ' · ' + esc(it.targetType) + ' ' + esc(stage) + '</div>'
            + (it.executionOutcome ? '<div class="small">' + esc(it.executionOutcome) + '</div>' : '')
            + actions
            + '  </div>'
            + '</div>';
    }

    function btn(it, outcome, cls, label) {
        return '<button type="button" class="btn ' + cls + '" data-action="outcome" data-outcome="' + outcome
            + '" data-id="' + esc(it.plannedVisitId) + '">' + esc(label || outcome) + '</button>';
    }

    // ── actions ──────────────────────────────────────────────────────────────────────────────────────────────────

    function onCellAction(e) {
        var b = e.currentTarget;
        var action = b.getAttribute('data-action');
        var plannedVisitId = b.getAttribute('data-id');
        if (action === 'outcome') { return recordOutcome(plannedVisitId, b.getAttribute('data-outcome')); }
        if (action === 'report') { return openReport(plannedVisitId); }
    }

    function recordOutcome(plannedVisitId, outcome) {
        var payload = { plannedVisitId: plannedVisitId, executionOutcome: outcome };
        if (outcome !== 'completed') {
            var reason = window.prompt(L.reasonPrompt || 'Reason code (e.g. doctor_unavailable):', 'doctor_unavailable');
            if (!reason) { return; }
            payload.reasonCode = reason.trim();
            if (outcome === 'rescheduled') {
                var to = window.prompt(L.reschedulePrompt || 'New date (yyyy-MM-dd):', '');
                if (to) { payload.rescheduleToDate = to.trim(); }
            }
        }
        api('/outcome', { method: 'POST', body: JSON.stringify(payload) }).then(function (r) {
            if (r.ok) { toast(L.outcomeRecorded || 'Outcome recorded.'); loadCalendar(); }
            else { toast(errorOf(r) || L.actionFailed); }
        });
    }

    function openReport(plannedVisitId) {
        // Load the existing report (if any) so a finalised report opens in amend mode.
        el('ve-report-planned-visit-id').value = plannedVisitId;
        resetReportForm();
        api('/reports?plannedVisitId=' + encodeURIComponent(plannedVisitId)).then(function (r) {
            var data = r.body && (r.body.data || r.body);
            var items = (data && data.items) || [];
            if (items.length) { hydrateExistingReport(items[0]); }
            show();
        });
    }

    function resetReportForm() {
        ['ve-report-id', 've-report-version', 've-actual-stage-code', 've-actual-stage-index',
            've-outcome-code', 've-feedback', 've-follow-up-notes', 've-amend-reason'].forEach(function (id) { el(id).value = ''; });
        el('ve-matched-plan').checked = false;
        el('ve-follow-up').checked = false;
        el('ve-report-state').value = 'none';
        el('ve-samples').innerHTML = '';
        el('ve-planned-content').textContent = '—';
        el('ve-report-status').textContent = '';
        toggleMode('none');
    }

    function hydrateExistingReport(report) {
        el('ve-report-id').value = report.visitReportId;
        el('ve-report-version').value = report.version;
        el('ve-report-state').value = report.reportStatus;
        el('ve-outcome-code').value = report.outcomeCode || '';
        if (report.actualStageIndex != null) { el('ve-actual-stage-index').value = report.actualStageIndex; }
        el('ve-matched-plan').checked = !!report.matchedPlan;
        el('ve-follow-up').checked = !!report.followUpRequired;
        toggleMode(report.reportStatus);
    }

    function toggleMode(state) {
        var finalised = state === 'submitted' || state === 'amended';
        el('ve-amend-block').classList.toggle('d-none', !finalised);
        el('ve-amend-report').classList.toggle('d-none', !finalised);
        el('ve-submit-report').classList.toggle('d-none', finalised);
        el('ve-report-status').textContent = finalised ? (L[state] || state) : '';
    }

    function collectSamples() {
        var out = [];
        Array.prototype.forEach.call(el('ve-samples').querySelectorAll('[data-sample-row]'), function (row) {
            var type = row.querySelector('[data-sample-type]').value.trim();
            var qty = parseInt(row.querySelector('[data-sample-qty]').value, 10);
            if (type) { out.push({ itemType: type, quantity: isNaN(qty) ? 1 : qty }); }
        });
        return out;
    }

    function reportBody() {
        var idx = el('ve-actual-stage-index').value;
        return {
            plannedVisitId: el('ve-report-planned-visit-id').value,
            contentActuals: {
                stageCode: el('ve-actual-stage-code').value.trim() || null,
                stageIndex: idx === '' ? null : parseInt(idx, 10),
                matchedPlan: el('ve-matched-plan').checked
            },
            samples: collectSamples(),
            feedback: {
                doctorFeedback: el('ve-feedback').value.trim() || null,
                outcomeCode: el('ve-outcome-code').value.trim(),
                followUpRequired: el('ve-follow-up').checked,
                followUpNotes: el('ve-follow-up-notes').value.trim() || null
            }
        };
    }

    function submitReport() {
        api('/reports', { method: 'POST', body: JSON.stringify(reportBody()) }).then(function (r) {
            if (r.ok) { toast(L.reportSubmitted || 'Report submitted.'); hide(); loadCalendar(); }
            else { el('ve-report-status').textContent = errorOf(r) || L.actionFailed; }
        });
    }

    function amendReport() {
        var body = reportBody();
        body.reason = el('ve-amend-reason').value.trim();
        var version = parseInt(el('ve-report-version').value, 10);
        if (!isNaN(version)) { body.expectedVersion = version; }
        var reportId = el('ve-report-id').value;
        api('/reports/' + encodeURIComponent(reportId) + '/amend', { method: 'POST', body: JSON.stringify(body) })
            .then(function (r) {
                if (r.ok) { toast(L.reportAmended || 'Amendment filed.'); hide(); loadCalendar(); }
                else { el('ve-report-status').textContent = errorOf(r) || L.actionFailed; }
            });
    }

    function addSampleRow() {
        var row = document.createElement('div');
        row.className = 'row g-1 mb-1';
        row.setAttribute('data-sample-row', '1');
        row.innerHTML =
            '<div class="col-7"><input type="text" class="form-control form-control-sm" data-sample-type placeholder="' + esc(L.itemType || 'Item type') + '" /></div>'
            + '<div class="col-3"><input type="number" min="1" value="1" class="form-control form-control-sm" data-sample-qty placeholder="' + esc(L.qty || 'Qty') + '" /></div>'
            + '<div class="col-2"><button type="button" class="btn btn-sm btn-outline-danger w-100" data-remove-sample>&times;</button></div>';
        row.querySelector('[data-remove-sample]').addEventListener('click', function () { row.remove(); });
        el('ve-samples').appendChild(row);
    }

    function errorOf(r) {
        var b = r.body && (r.body.data || r.body);
        if (b && b.errors && b.errors.length) { return b.errors[0]; }
        if (b && b.message) { return b.message; }
        return null;
    }

    function show() { if (offcanvas) { offcanvas.show(); } }
    function hide() { if (offcanvas) { offcanvas.hide(); } }

    // ── wire up ──────────────────────────────────────────────────────────────────────────────────────────────────

    function setView(mode) {
        viewMode = mode;
        el('ve-view-day').classList.toggle('active', mode === 'day');
        el('ve-view-week').classList.toggle('active', mode === 'week');
        loadCalendar();
    }

    function init() {
        el('ve-anchor').value = iso(new Date());
        if (window.bootstrap && window.bootstrap.Offcanvas) {
            offcanvas = new window.bootstrap.Offcanvas(el('ve-report-offcanvas'));
        }
        el('ve-view-day').addEventListener('click', function () { setView('day'); });
        el('ve-view-week').addEventListener('click', function () { setView('week'); });
        el('ve-refresh').addEventListener('click', loadCalendar);
        el('ve-anchor').addEventListener('change', loadCalendar);
        el('ve-add-sample').addEventListener('click', addSampleRow);
        el('ve-submit-report').addEventListener('click', submitReport);
        el('ve-amend-report').addEventListener('click', amendReport);

        loadContract().then(loadCalendar);
    }

    init();
})();
