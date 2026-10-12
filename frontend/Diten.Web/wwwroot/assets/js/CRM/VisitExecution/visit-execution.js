/*
 * MOD-0155 FU02 — Visit Report EXECUTION calendar.
 * Bespoke tenant-shell Day/Week calendar (NOT a Golden DataTable). Every call is a same-origin proxy to the Gateway
 * under /CRM/VisitExecution/api/*; the browser never sees a service URL or a bearer token. The rep views the FU01 plan
 * atoms in a window, marks each done/missed/rescheduled inline, records the immutable Visit Report and files amendments.
 * WP-E2E-FIX-1 — the cell and the report show WHAT to present (plannedContent), the presented stage is PICKED from the
 * planned journey (default: the planned stage) and travels with its journey/stage ids, a visit whose day has not come
 * cannot be completed / reported, refusal codes map to localized messages.
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
    var offcanvas = null;
    var NOT_PRESENTED = '__not_presented__';
    // WP-E2E-FIX-1 (E9-B1/B3) — the calendar items of the window, by plannedVisitId, so the report knows the plan.
    var itemsById = {};
    var reportPlan = null;   // the open report's planned journey/stage ({ journeyId, stageId, stageIndex, ... }) or null
    var reportStages = [];   // the planned journey's stages, in order (index = position)
    var reportToken = 0;     // guards against a late stages response of a previously opened report

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
    function toast(msg, type) { if (window.showToast) { window.showToast(msg, type || 'success'); } else { console.log(msg); } }
    function fmt(template, value) { return String(template || '{0}').replace('{0}', value); }

    // E9-B5 — a visit whose day has not come cannot be completed / reported (CRM answers 409 visit_not_yet_due too).
    function isNotYetDue(it) { return !!(it && it.plannedDate && it.plannedDate > iso(new Date())); }

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

    function outcomeLabel(code) { return (L.outcomes && L.outcomes[code]) || code; }

    function stageText(c) {
        return c.stageName || c.stageCode || (c.stageIndex != null ? fmt(L.stageFallback, c.stageIndex + 1) : '');
    }

    function renderCalendar(items, range) {
        var container = el('ve-calendar');
        container.innerHTML = '';
        var empty = el('ve-empty');
        empty.classList.toggle('d-none', items.length !== 0);

        itemsById = {};
        items.forEach(function (it) { itemsById[it.plannedVisitId] = it; });

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

    // E9-B2 — product chip + stage name per planned content item ("TUTUKON · Farkındalık"), not "#0".
    function contentChips(it) {
        var content = it.plannedContent || [];
        if (content.length) {
            return '<div class="d-flex flex-wrap gap-1 mt-1">' + content.map(function (c) {
                var stage = stageText(c);
                return '<span class="badge bg-label-primary">' + esc(c.productCode || '') + (stage ? ' · ' + esc(stage) : '') + '</span>';
            }).join('') + '</div>';
        }
        return it.plannedStageIndex != null
            ? '<div class="small text-muted">' + esc(fmt(L.stageFallback, it.plannedStageIndex + 1)) + '</div>'
            : '';
    }

    function renderCell(it) {
        var time = it.slotStartTime || it.plannedStartTime || '';
        var notDue = isNotYetDue(it);
        var actions = '';
        if (canRecord) {
            actions =
                '<div class="btn-group btn-group-sm mt-2 w-100" role="group">'
                + btn(it, 'completed', 'btn-outline-success', L.markCompleted, notDue)
                + btn(it, 'missed', 'btn-outline-danger', L.markMissed, notDue)
                + btn(it, 'rescheduled', 'btn-outline-warning', L.markRescheduled, false)
                + '</div>'
                + '<button type="button" class="btn btn-sm btn-primary w-100 mt-1" data-action="report" data-id="'
                + esc(it.plannedVisitId) + '"' + disabledAttr(notDue) + '>' + esc(L.report || 'Report') + '</button>'
                + (notDue ? '<div class="small text-muted mt-1"><i class="bx bx-time-five"></i> ' + esc(L.notYetDue) + '</div>' : '');
        }
        return ''
            + '<div class="card mb-2" data-planned-visit="' + esc(it.plannedVisitId) + '">'
            + '  <div class="card-body p-2">'
            + '    <div class="d-flex justify-content-between align-items-start">'
            + '      <div class="small fw-semibold">' + esc(it.visitCode) + '</div>' + stateBadge(it.reportState)
            + '    </div>'
            // WP-VP-2 (B-8) — the target's read-time name under the visit code (doctor / institution).
            + (it.targetDisplayName ? '    <div class="small">' + esc(it.targetDisplayName) + '</div>' : '')
            + '    <div class="small text-muted">' + esc(time) + ' · ' + esc(it.targetType) + '</div>'
            + contentChips(it)
            // E9-B6 — the localized outcome label, never the raw code.
            + (it.executionOutcome ? '<div class="small">' + esc(outcomeLabel(it.executionOutcome)) + '</div>' : '')
            + actions
            + '  </div>'
            + '</div>';
    }

    function disabledAttr(disabled) {
        return disabled ? ' disabled title="' + esc(L.notYetDue) + '"' : '';
    }

    function btn(it, outcome, cls, label, disabled) {
        return '<button type="button" class="btn ' + cls + '" data-action="outcome" data-outcome="' + outcome
            + '" data-id="' + esc(it.plannedVisitId) + '"' + disabledAttr(disabled) + '>' + esc(label || outcome) + '</button>';
    }

    // ── actions ──────────────────────────────────────────────────────────────────────────────────────────────────

    function onCellAction(e) {
        var b = e.currentTarget;
        if (b.disabled) { return; }
        var action = b.getAttribute('data-action');
        var plannedVisitId = b.getAttribute('data-id');
        if (action === 'outcome') { return recordOutcome(plannedVisitId, b.getAttribute('data-outcome')); }
        if (action === 'report') { return openReport(plannedVisitId); }
    }

    function sendOutcome(payload) {
        api('/outcome', { method: 'POST', body: JSON.stringify(payload) }).then(function (r) {
            if (r.ok) { toast(L.outcomeRecorded || 'Outcome recorded.'); loadCalendar(); }
            else { toast(errorOf(r) || L.actionFailed, 'error'); }
        });
    }

    function recordOutcome(plannedVisitId, outcome) {
        var payload = { plannedVisitId: plannedVisitId, executionOutcome: outcome };
        if (outcome === 'completed') {
            // E9-B6 — "completed" is confirmed first (missed / rescheduled already ask for a reason).
            var go = function () { sendOutcome(payload); };
            if (window.showConfirm) {
                window.showConfirm(L.confirmCompleted, go, { type: 'question', confirmButtonText: L.confirmCompletedButton });
            } else if (window.confirm(L.confirmCompleted)) {
                go();
            }
            return;
        }
        var reason = window.prompt(L.reasonPrompt || 'Reason code (e.g. doctor_unavailable):', 'doctor_unavailable');
        if (!reason) { return; }
        payload.reasonCode = reason.trim();
        if (outcome === 'rescheduled') {
            var to = window.prompt(L.reschedulePrompt || 'New date (yyyy-MM-dd):', '');
            if (to) { payload.rescheduleToDate = to.trim(); }
        }
        sendOutcome(payload);
    }

    // The report's planned journey/stage: the first planned content item, else the legacy single content reference.
    function planOf(it) {
        var first = it && it.plannedContent && it.plannedContent[0];
        if (first && first.journeyId) {
            return {
                journeyId: first.journeyId, journeyCode: first.journeyCode || null, stageId: first.stageId,
                stageIndex: first.stageIndex, stageCode: first.stageCode || null, stageName: first.stageName || null
            };
        }
        if (it && it.plannedJourneyId) {
            return {
                journeyId: it.plannedJourneyId, journeyCode: null, stageId: it.plannedStageId || null,
                stageIndex: it.plannedStageIndex, stageCode: null, stageName: null
            };
        }
        return null;
    }

    function openReport(plannedVisitId) {
        var item = itemsById[plannedVisitId] || null;
        var token = ++reportToken;
        el('ve-report-planned-visit-id').value = plannedVisitId;
        resetReportForm();
        renderPlannedContent(item);
        reportPlan = planOf(item);
        el('ve-actual-stage-block').classList.toggle('d-none', !reportPlan);
        el('ve-submit-report').disabled = !canRecord || isNotYetDue(item);
        if (isNotYetDue(item)) { el('ve-report-status').textContent = L.notYetDue || ''; }

        var stages = reportPlan ? loadStages(reportPlan) : Promise.resolve();
        var existing = api('/reports?plannedVisitId=' + encodeURIComponent(plannedVisitId)).then(function (r) {
            var data = r.body && (r.body.data || r.body);
            return (data && data.items) || [];
        });
        Promise.all([stages, existing]).then(function (res) {
            if (token !== reportToken) { return; }
            var items = res[1];
            if (items.length) { hydrateExistingReport(items[0]); }
            show();
        });
    }

    // E9-B2 — product → stage → step titles of the plan atom (read-only).
    function renderPlannedContent(item) {
        var host = el('ve-planned-content');
        var content = (item && item.plannedContent) || [];
        if (!content.length) { host.textContent = L.noPlannedContent || '—'; return; }
        host.innerHTML = content.map(function (c) {
            var steps = (c.steps || []).map(function (s) { return '<li>' + esc(s.title || '') + '</li>'; }).join('');
            return '<div class="mb-2">'
                + '<span class="badge bg-label-primary">' + esc(c.productCode || '') + '</span> '
                + '<span class="text-muted">' + esc(c.role || '') + '</span>'
                + '<div class="fw-semibold mt-1">' + esc(stageText(c)) + '</div>'
                + (steps ? '<ol class="mb-0 ps-3">' + steps + '</ol>' : '')
                + '</div>';
        }).join('');
    }

    // E9-B3 — the planned journey's stages as a picker; default = the planned stage, plus "not presented".
    function loadStages(plan) {
        reportStages = [];
        fillStageSelect(plan);
        return api('/journeys/' + encodeURIComponent(plan.journeyId) + '/stages').then(function (r) {
            var data = r.body && (r.body.data || r.body);
            var list = ((data && data.items) || []).slice().sort(function (a, b) { return (a.stageOrder || 0) - (b.stageOrder || 0); });
            // The position in the ordered stage list IS the stage index (the CRM resolver's rule).
            reportStages = list.map(function (s, i) {
                return { stageId: s.stageId, stageIndex: i, stageCode: s.stageCode || null, stageName: s.stageName || null };
            });
            fillStageSelect(plan);
        }).catch(function () { /* the planned stage alone stays selectable */ });
    }

    function fillStageSelect(plan) {
        var select = el('ve-actual-stage');
        select.innerHTML = '';
        var stages = reportStages.length
            ? reportStages
            : [{ stageId: plan.stageId, stageIndex: plan.stageIndex, stageCode: plan.stageCode, stageName: plan.stageName }];
        stages.forEach(function (s) {
            var o = document.createElement('option');
            o.value = s.stageId || '';
            o.setAttribute('data-index', s.stageIndex == null ? '' : String(s.stageIndex));
            o.setAttribute('data-code', s.stageCode || '');
            o.setAttribute('data-name', s.stageName || '');
            var label = (s.stageIndex != null ? (s.stageIndex + 1) + '. ' : '') + (s.stageName || s.stageCode || fmt(L.stageFallback, (s.stageIndex || 0) + 1));
            o.textContent = s.stageId && s.stageId === plan.stageId ? label + ' ' + (L.plannedStageSuffix || '') : label;
            select.appendChild(o);
        });
        var none = document.createElement('option');
        none.value = NOT_PRESENTED;
        none.textContent = L.stageNotPresented || '—';
        select.appendChild(none);
        select.value = plan.stageId || NOT_PRESENTED;
        updateStageHint();
    }

    function updateStageHint() {
        var hint = el('ve-actual-stage-hint');
        if (!reportPlan) { hint.textContent = ''; return; }
        var choice = stageChoice();
        hint.textContent = choice.matchedPlan ? (L.stageMatchesPlan || '') : (L.stageDiffersFromPlan || '');
    }

    // The presented stage as the report's actual content. "Not presented" / no planned journey → empty, unmatched.
    function stageChoice() {
        var empty = { journeyId: null, stageId: null, stageIndex: null, stageCode: null, matchedPlan: false, journeyDisplayName: null, stageDisplayName: null };
        if (!reportPlan) { return empty; }
        var select = el('ve-actual-stage');
        var option = select.options[select.selectedIndex];
        if (!option || option.value === NOT_PRESENTED || !option.value) { return empty; }
        var index = option.getAttribute('data-index');
        return {
            journeyId: reportPlan.journeyId,
            stageId: option.value,
            stageIndex: index === '' ? null : parseInt(index, 10),
            stageCode: option.getAttribute('data-code') || null,
            matchedPlan: option.value === reportPlan.stageId,
            journeyDisplayName: reportPlan.journeyCode,
            stageDisplayName: option.getAttribute('data-name') || null
        };
    }

    function resetReportForm() {
        ['ve-report-id', 've-report-version', 've-outcome-code', 've-feedback', 've-follow-up-notes', 've-amend-reason']
            .forEach(function (id) { el(id).value = ''; });
        el('ve-outcome-code').classList.remove('is-invalid');
        el('ve-follow-up').checked = false;
        el('ve-report-state').value = 'none';
        el('ve-samples').innerHTML = '';
        el('ve-actual-stage').innerHTML = '';
        el('ve-actual-stage-hint').textContent = '';
        el('ve-report-status').textContent = '';
        reportPlan = null;
        reportStages = [];
        toggleMode('none');
    }

    function hydrateExistingReport(report) {
        el('ve-report-id').value = report.visitReportId;
        el('ve-report-version').value = report.version;
        el('ve-report-state').value = report.reportStatus;
        el('ve-outcome-code').value = report.outcomeCode || '';
        el('ve-follow-up').checked = !!report.followUpRequired;
        // A finalised report shows the stage it recorded; a draft (outcome only) keeps the planned default.
        if (reportPlan && (report.reportStatus === 'submitted' || report.reportStatus === 'amended')) {
            var select = el('ve-actual-stage');
            var match = Array.prototype.find.call(select.options, function (o) {
                return report.actualStageIndex != null && o.getAttribute('data-index') === String(report.actualStageIndex);
            });
            select.value = match ? match.value : NOT_PRESENTED;
            updateStageHint();
        }
        toggleMode(report.reportStatus);
    }

    function toggleMode(state) {
        var finalised = state === 'submitted' || state === 'amended';
        el('ve-amend-block').classList.toggle('d-none', !finalised);
        el('ve-amend-report').classList.toggle('d-none', !finalised);
        el('ve-submit-report').classList.toggle('d-none', finalised);
        el('ve-report-status').textContent = finalised ? (L[state] || state) : el('ve-report-status').textContent;
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
        return {
            plannedVisitId: el('ve-report-planned-visit-id').value,
            contentActuals: stageChoice(),
            samples: collectSamples(),
            feedback: {
                doctorFeedback: el('ve-feedback').value.trim() || null,
                outcomeCode: el('ve-outcome-code').value.trim(),
                followUpRequired: el('ve-follow-up').checked,
                followUpNotes: el('ve-follow-up-notes').value.trim() || null
            }
        };
    }

    // E9-B4 — the required outcome code is checked before the round trip, with the same message the server code maps to.
    function validateReport() {
        var input = el('ve-outcome-code');
        var missing = !input.value.trim();
        input.classList.toggle('is-invalid', missing);
        if (missing) {
            el('ve-report-status').textContent = (L.errors && L.errors.visit_report_outcome_code_required) || L.actionFailed;
            input.focus();
        }
        return !missing;
    }

    function submitReport() {
        if (!validateReport()) { return; }
        api('/reports', { method: 'POST', body: JSON.stringify(reportBody()) }).then(function (r) {
            if (r.ok) { toast(L.reportSubmitted || 'Report submitted.'); hide(); loadCalendar(); }
            else { el('ve-report-status').textContent = errorOf(r) || L.actionFailed; }
        });
    }

    function amendReport() {
        if (!validateReport()) { return; }
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

    // E9-B4 — the refusal lives at the envelope ROOT: { data, errors: [message, code], statusCode }. A failed
    // Response<Guid> carries data = "00000000-…" (a truthy string), so reading `body.data || body` first never reached
    // `errors` and the rep saw only "İşlem başarısız". A known machine code maps to its localized message; otherwise the
    // server's own message. (The Web proxy turns a ProblemDetails refusal into the same envelope.)
    function errorOf(r) {
        var b = r.body;
        if (!b) { return null; }
        var errors = Array.isArray(b.errors) ? b.errors : [];
        var map = L.errors || {};
        for (var i = 0; i < errors.length; i++) {
            if (map[errors[i]]) { return map[errors[i]]; }
        }
        var message = errors.filter(function (e) { return typeof e === 'string' && !/^[a-z0-9_]+$/.test(e); })[0];
        return message || b.message || b.title || null;
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
        el('ve-actual-stage').addEventListener('change', updateStageHint);
        el('ve-outcome-code').addEventListener('input', function () { this.classList.remove('is-invalid'); });
        el('ve-submit-report').addEventListener('click', submitReport);
        el('ve-amend-report').addEventListener('click', amendReport);

        loadCalendar();
    }

    init();
})();
