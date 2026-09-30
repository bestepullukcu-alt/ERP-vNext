/*
 * MOD-0280-FU01 T2b (U4, U5) — the approver's read-only week.
 *
 * Draws the rows × days with each row's source, the person's corrections of captured time (with the value first
 * measured), the marks, and — for a CORRECTION revision — what it changes against the approved revision in force.
 * Approve and Return go to /TimeEntry/Approvals/api/decisions/{approve|reject}, which the web tier forwards to the
 * Task Center's own work-item action (MOD-0023). A return without a reason is stopped here AND refused by MOD-0023.
 * No inline style (FG-003).
 */
(function (root) {
    'use strict';

    var doc = root.document;
    var core = root.TimeEntryCore;
    var state = { week: null, decided: false, rejecting: false };

    function L() { return root.L10n || {}; }
    function t(key) { return L()[key] || key; }
    function fmt(text) {
        var args = Array.prototype.slice.call(arguments, 1);
        return args.reduce(function (out, value, i) { return out.split('{' + i + '}').join(String(value)); }, String(text || ''));
    }
    function el(tag, cls, text) {
        var node = doc.createElement(tag);
        if (cls) { node.className = cls; }
        if (text !== undefined && text !== null) { node.textContent = text; }
        return node;
    }
    function byId(id) { return doc.getElementById(id); }
    function clear(node) { while (node && node.firstChild) { node.removeChild(node.firstChild); } return node; }
    function hm(minutes) { return core.formatMinutes(minutes) || '0:00'; }
    function lang() { return doc.documentElement.getAttribute('lang') || 'en'; }
    function day(iso, opts) {
        try { return new Intl.DateTimeFormat(lang(), Object.assign({ timeZone: 'UTC' }, opts)).format(new Date(iso + 'T00:00:00Z')); } catch (e) { return iso; }
    }
    function instant(iso) {
        try { return new Intl.DateTimeFormat(lang(), { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso)); } catch (e) { return iso || ''; }
    }
    function announce(message, kind) {
        var line = byId('taNotice');
        if (!line) { return; }
        line.textContent = message || '';
        line.className = 'time-entry-notice time-entry-notice-' + (kind || 'info');
    }
    function failure(status, reasonCode) {
        return core.failureMessage({ status: status, reasonCode: reasonCode }, t);
    }
    function targetLabel(row) {
        if (row.taskItemId) { return row.taskTitle || t('UnreadableTask'); }
        return row.categoryCode;
    }

    function request(method, url, body) {
        var options = { method: method, credentials: 'same-origin', headers: { 'Accept': 'application/json' } };
        if (body !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }
        return root.fetch(url, options).then(function (response) {
            return response.text().then(function (text) {
                var parsed = null;
                try { parsed = text ? JSON.parse(text) : null; } catch (e) { parsed = null; }
                return { ok: response.ok, status: response.status, data: parsed && parsed.data !== undefined ? parsed.data : parsed, reasonCode: parsed && (parsed.reason_code || parsed.reasonCode) || null };
            });
        }, function () { return { ok: false, status: 0, data: null, reasonCode: null }; });
    }

    // ── render ─────────────────────────────────────────────────────────────────────────────────────────────────

    function renderHeader(week) {
        byId('taTitle').textContent = (week.displayName || '—') + ' · ' + week.weekKey;
        byId('taMeta').textContent = fmt(t('Revision'), week.revisionNumber) + ' · ' + fmt(t('SubmittedAt'), instant(week.submittedAtUtc));
        var marks = clear(byId('taMarks'));
        var add = function (key, dates) {
            var badge = el('span', 'badge bg-label-warning me-1 time-entry-mark-badge', t(key) + (dates && dates.length ? ' · ' + dates.map(function (d) { return day(d, { weekday: 'short', day: 'numeric' }); }).join(', ') : ''));
            marks.appendChild(badge);
        };
        if ((week.flaggedDates || []).length) { add('MarkFlagged', week.flaggedDates); }
        if ((week.autoClosedDates || []).length) { add('MarkAutoClosed', week.autoClosedDates); }
        if ((Number(week.outsideWorkingMinutes) || 0) > 0) { add('MarkOutsideHours', null); }
        if ((week.holidayDates || []).length) { add('MarkHoliday', week.holidayDates); }
        var inForce = byId('taInForce');
        inForce.hidden = !week.inForceRevisionNumber;
        inForce.textContent = week.inForceRevisionNumber ? fmt(t('InForceNote'), week.inForceRevisionNumber) : '';
    }

    function renderGrid(week) {
        var rows = core.buildRows(week);
        var days = week.days || [];
        var table = el('table', 'table time-entry-grid');
        var head = el('thead');
        var hr = el('tr');
        hr.appendChild(el('th', 'time-entry-row-label', t('ColumnWork')));
        days.forEach(function (d) {
            var th = el('th', 'time-entry-day' + (d.dayKind !== 'workingDay' ? ' time-entry-day-weekend' : ''));
            th.appendChild(el('span', 'time-entry-day-name', day(d.date, { weekday: 'short' })));
            th.appendChild(el('span', 'time-entry-day-date', day(d.date, { day: 'numeric', month: 'short' })));
            hr.appendChild(th);
        });
        hr.appendChild(el('th', 'time-entry-total-col', t('ColumnTotal')));
        head.appendChild(hr);
        table.appendChild(head);

        var body = el('tbody');
        rows.forEach(function (row) {
            var tr = el('tr', 'time-entry-row');
            var th = el('th', 'time-entry-row-label');
            th.appendChild(el('span', row.taskItemId && !row.taskTitle ? 'time-entry-row-title time-entry-row-title-unreadable' : 'time-entry-row-title', targetLabel(row)));
            th.appendChild(el('span', 'badge time-entry-source time-entry-source-' + String(row.source).toLowerCase(), t('Source' + row.source)));
            tr.appendChild(th);
            days.forEach(function (d) {
                var cell = row.cells[d.date];
                var td = el('td', 'time-entry-cell');
                if (cell) {
                    td.appendChild(el('span', 'time-entry-cell-value', hm(cell.minutes)));
                    // The person's correction of captured time is shown WITH what was first measured.
                    if (cell.capturedMinutes !== null && cell.capturedMinutes !== undefined) {
                        var mark = el('span', 'time-entry-mark time-entry-mark-edited', t('Corrected') + ' · ' + fmt(t('CapturedValue'), hm(cell.capturedMinutes)));
                        mark.setAttribute('data-captured', String(cell.capturedMinutes));
                        td.appendChild(mark);
                    }
                    if (cell.note) { td.setAttribute('title', cell.note); }
                }
                tr.appendChild(td);
            });
            tr.appendChild(el('td', 'time-entry-total-col', hm(core.rowTotal(row))));
            body.appendChild(tr);
        });
        table.appendChild(body);

        var totals = core.dayTotals(rows, days.map(function (d) { return d.date; }));
        var foot = el('tfoot');
        var tr = el('tr', 'time-entry-totals');
        tr.appendChild(el('th', 'time-entry-row-label', t('RowTotal')));
        days.forEach(function (d) {
            var td = el('td', 'time-entry-day-total' + (d.isFlagged ? ' time-entry-day-flagged' : ''), hm(totals[d.date]));
            tr.appendChild(td);
        });
        tr.appendChild(el('td', 'time-entry-total-col', hm(week.totalMinutes)));
        foot.appendChild(tr);
        table.appendChild(foot);

        var wrap = el('div', 'table-responsive time-entry-grid-wrap');
        wrap.appendChild(table);
        clear(byId('taGrid')).appendChild(wrap);
    }

    function renderChanges(week) {
        var card = byId('taChangesCard');
        var host = clear(byId('taChanges'));
        card.hidden = !week.correctionOfRevision;
        if (!week.correctionOfRevision) { return; }
        var changes = week.correctionChanges || [];
        if (!changes.length) {
            host.appendChild(el('p', 'text-muted mb-0', t('NoChanges')));
            return;
        }
        var table = el('table', 'table table-sm time-entry-changes');
        var head = el('tr');
        [t('ColDay'), t('ColWork'), t('ColBefore'), t('ColAfter')].forEach(function (h) { head.appendChild(el('th', null, h)); });
        var thead = el('thead'); thead.appendChild(head); table.appendChild(thead);
        var body = el('tbody');
        changes.forEach(function (c) {
            var tr = el('tr', 'time-entry-change' + (c.previousMinutes === null ? ' time-entry-change-added' : c.currentMinutes === null ? ' time-entry-change-removed' : ''));
            tr.appendChild(el('td', null, day(c.localDate, { weekday: 'short', day: 'numeric', month: 'short' })));
            var work = el('td');
            work.appendChild(el('span', null, targetLabel(c) + ' '));
            work.appendChild(el('span', 'badge time-entry-source time-entry-source-' + String(c.source).toLowerCase(), t('Source' + c.source)));
            tr.appendChild(work);
            tr.appendChild(el('td', 'time-entry-change-before', c.previousMinutes === null ? t('Added') : hm(c.previousMinutes)));
            tr.appendChild(el('td', 'time-entry-change-after', c.currentMinutes === null ? t('Removed') : hm(c.currentMinutes)));
            body.appendChild(tr);
        });
        table.appendChild(body);
        host.appendChild(table);
    }

    function renderDecision() {
        var host = clear(byId('taDecision'));
        var page = byId('timeApprovalWeek');
        var week = state.week;
        if (state.decided || !week || !week.approvalTaskId) { return; }
        var button = function (cls, text, id, onClick) {
            var b = el('button', cls, text);
            b.type = 'button'; b.id = id;
            b.addEventListener('click', onClick);
            host.appendChild(b);
            return b;
        };
        if (page.getAttribute('data-can-approve') === 'true') {
            button('btn btn-success', t('Approve'), 'taApprove', approve);
        }
        if (page.getAttribute('data-can-reject') === 'true') {
            button('btn btn-label-danger', t('Reject'), 'taReject', function () { state.rejecting = true; renderDecision(); byId('taRejectReason').focus(); });
        }
        if (state.rejecting) {
            var form = el('div', 'time-entry-correction-form');
            form.id = 'taRejectForm';
            var label = el('label', 'form-label', t('RejectReason'));
            label.setAttribute('for', 'taRejectReason');
            var field = el('textarea', 'form-control');
            field.id = 'taRejectReason'; field.rows = 3; field.maxLength = 1000;
            var error = el('div', 'invalid-feedback time-entry-inline-error');
            error.id = 'taRejectError'; error.hidden = true;
            var row = el('div', 'time-entry-correction-actions');
            var send = el('button', 'btn btn-sm btn-danger', t('Reject'));
            send.type = 'button'; send.id = 'taRejectSend';
            send.addEventListener('click', function () { reject(field.value); });
            var cancel = el('button', 'btn btn-sm btn-label-secondary', t('Cancel'));
            cancel.type = 'button';
            cancel.addEventListener('click', function () { state.rejecting = false; renderDecision(); });
            row.appendChild(send); row.appendChild(cancel);
            form.appendChild(label); form.appendChild(field); form.appendChild(error); form.appendChild(row);
            host.appendChild(form);
        }
    }

    // ── decisions ──────────────────────────────────────────────────────────────────────────────────────────────

    function decide(decision, comment) {
        var week = state.week;
        return request('POST', '/TimeEntry/Approvals/api/decisions/' + decision, {
            approvalTaskId: week.approvalTaskId, expectedVersion: week.approvalTaskVersion, comment: comment || null
        }).then(function (result) {
            if (!result.ok) {
                announce(failure(result.status, result.reasonCode), 'warning');
                if (typeof root.showToast === 'function') { root.showToast(failure(result.status, result.reasonCode), 'error'); }
                return result;
            }
            state.decided = true;
            state.rejecting = false;
            var message = decision === 'approve' ? t('ApprovedToast') : t('RejectedToast');
            announce(message);
            if (typeof root.showToast === 'function') { root.showToast(message, 'success'); }
            renderDecision();
            return result;
        });
    }

    function approve() {
        var go = function () { decide('approve', null); };
        if (typeof root.showConfirm === 'function') { root.showConfirm(t('ApproveConfirm'), go); } else { go(); }
    }

    /** A return needs its reason: stopped HERE before anything is sent (MOD-0023 refuses it too — R5). */
    function reject(reason) {
        var text = String(reason || '').trim();
        var error = byId('taRejectError');
        if (!text) {
            if (error) { error.textContent = t('RejectReasonRequired'); error.hidden = false; }
            return Promise.resolve({ ok: false });
        }
        return decide('reject', text);
    }

    function load() {
        var page = byId('timeApprovalWeek');
        var weekId = page.getAttribute('data-week-id');
        return request('GET', '/TimeEntry/Approvals/api/' + encodeURIComponent(weekId)).then(function (result) {
            byId('taLoading').hidden = true;
            if (!result.ok) {
                announce(result.status === 404 ? t('WeekNotAvailable') : failure(result.status, result.reasonCode), 'warning');
                return result;
            }
            state.week = result.data;
            byId('taContent').hidden = false;
            renderHeader(state.week);
            renderGrid(state.week);
            renderChanges(state.week);
            renderDecision();
            return result;
        });
    }

    function init() {
        if (!byId('timeApprovalWeek')) { return Promise.resolve(null); }
        return load();
    }

    root.TimeApprovalWeek = { init: init, approve: approve, reject: reject, state: function () { return state; } };

    if (doc.readyState === 'loading') { doc.addEventListener('DOMContentLoaded', init); }
    else if (!root.__timeApprovalNoAutoInit) { init(); }
})(typeof window !== 'undefined' ? window : globalThis);
