/**
 * WP-VW-W2 (WEB-a) — the Visit Workspace's PURE rules: no DOM, no fetch of its own (the two-step save takes the
 * page's post function). Loaded before visit-workspace.js; the Web tests run these functions in Node.
 *
 *   STATUSES / statusStyle(code)      the 10 work statuses (W1 9 + draft) → tone, icon, dashed / faded / strike / locked
 *   countdown(deadline, nowMs)        hours + minutes left to reportDeadline (null when no deadline)
 *   showsCountdown(status)            missed / report_missing carry the countdown
 *   layoutFor(width)                  'list' under 768 px (the phone's day list), 'grid' otherwise
 *   actionsFor(visit, today, perms)   the detail panel's actions by status
 *   noteState(reason, note, max)      the note rule: requiresNote ⇒ required; counter; too long
 *   canSave(kind, form, max)          the dialog's Save button
 *   saveNotDone(post, form)           not done / reschedule = TWO calls; the second only when the first succeeded
 *   errorKey(codes) / errorText(...)  CRM refusal codes → the user's language
 *   contentRoles(items)               "what do I present": the first product promo, the others reminders
 *   filterVisits / loadFilters / saveFilters   the filters (remembered in the browser, never required)
 *   capacity(planned, cap), weekWindow(monday), lastReportOf(visits, visit), eventOf(visit, editable), unplannedBody(...)
 * WP-VW-W2 (WEB-b) — Plan mode:
 *   canPlanWeek(week, mode)           only a DRAFT week in Plan mode is edited; approved / past / Execute = read-only
 *   pinFromDrop(drop) / roundToStep   a calendar drop → the day pin: the day + the start on the 15-minute grid; all day = no time
 *   pinsWith(current, target, pin)    the week's pins after one visit pin (the update REPLACES that week's pins)
 *   pinKey / pinText / pinMoveCode    the pin codes (7) → the user's language; a moved time pin's likely reason
 *   dragItem / parseDragItem          a Targets row on the drag
 *   accountOptions(visits)            the institution filter: accountDisplayName ONLY (never a doctor's name)
 *   unplacedRows(week, L)             the "N visits did not fit" list (weeks[].unplaced[])
 *   sessionVersionOf(week)            the version approve / reopen expects (weeks[].sessionVersion), null = read it
 * Reason codes / labels are NOT here: they come from the reference set (GET reasons), never a local list.
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.VisitWorkspaceCore = api; }
}(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    // ── statuses (W1 priority order, then the workspace-only draft) ──────────────────────────────────────────
    const STATUSES = ['cancelled', 'not_done', 'rescheduled', 'reported', 'expired', 'report_missing', 'missed', 'today', 'planned', 'draft'];

    const STYLE = {
        cancelled: { tone: 'muted', icon: 'bx-block', strike: true },
        not_done: { tone: 'muted', icon: 'bx-x-circle' },
        rescheduled: { tone: 'muted', icon: 'bx-calendar-edit' },
        reported: { tone: 'success', icon: 'bx-check-circle' },
        expired: { tone: 'danger', icon: 'bx-lock-alt', locked: true },
        report_missing: { tone: 'warning', icon: 'bx-time-five' },
        missed: { tone: 'danger', icon: 'bx-error-circle' },
        today: { tone: 'today', icon: 'bx-play-circle' },
        planned: { tone: 'planned', icon: 'bx-calendar-check' },
        draft: { tone: 'draft', icon: 'bx-edit-alt', dashed: true, faded: true }
    };

    /** A status → its card look. An unknown code reads as planned (never a blank card). */
    const statusStyle = code => {
        const known = Object.prototype.hasOwnProperty.call(STYLE, code);
        const s = STYLE[known ? code : 'planned'];
        return {
            code: known ? code : 'planned',
            tone: s.tone,
            icon: s.icon,
            dashed: !!s.dashed,
            faded: !!s.faded,
            strike: !!s.strike,
            locked: !!s.locked,
            labelKey: 'Status_' + (known ? code : 'planned'),
            cssClass: 'vw-st-' + (known ? code : 'planned')
        };
    };

    // ── countdown ────────────────────────────────────────────────────────────────────────────────────────────
    const showsCountdown = status => status === 'missed' || status === 'report_missing';

    /** Time left to <paramref deadline> (ISO). Passed ⇒ { passed: true, hours: 0, minutes: 0 }. */
    const countdown = (deadline, nowMs) => {
        if (!deadline) { return null; }
        const end = Date.parse(deadline);
        if (isNaN(end)) { return null; }
        const left = end - nowMs;
        if (left <= 0) { return { passed: true, hours: 0, minutes: 0, totalMinutes: 0 }; }
        const total = Math.floor(left / 60000);
        return { passed: false, hours: Math.floor(total / 60), minutes: total % 60, totalMinutes: total };
    };

    // ── layout ───────────────────────────────────────────────────────────────────────────────────────────────
    const PHONE_MAX = 768;
    const layoutFor = width => (typeof width === 'number' && width < PHONE_MAX ? 'list' : 'grid');

    // ── detail panel actions ────────────────────────────────────────────────────────────────────────────────
    /**
     * By status (WP-VW-W2 WEB-a): today / planned → cancel (+ result today); missed → not done, reschedule;
     * report_missing → send the report; expired → locked (information only); draft → plan. perms = { manage, record }.
     */
    const actionsFor = (visit, today, perms) => {
        const p = perms || {};
        const status = visit && visit.workStatus;
        const out = [];
        if (!visit) { return out; }
        if (status === 'today' || status === 'planned') {
            if (p.manage && visit.plannedVisitId && visit.plannedDate >= today) { out.push('cancel'); }
            if (status === 'today' && visit.plannedVisitId) { out.push('result'); }
        } else if (status === 'missed') {
            if (p.record && visit.plannedVisitId) { out.push('notDone', 'reschedule'); }
        } else if (status === 'report_missing') {
            out.push('sendReport');
        } else if (status === 'expired') {
            out.push('locked');
        } else if (status === 'draft') {
            out.push('plan');
        }
        return out;
    };

    // ── dialogs ──────────────────────────────────────────────────────────────────────────────────────────────
    const DEFAULT_NOTE_MAX = 500;

    /** The note rule of a reason: requiresNote ⇒ required; a counter; over the limit ⇒ invalid. */
    const noteState = (reason, note, max) => {
        const limit = max || DEFAULT_NOTE_MAX;
        const text = String(note || '').trim();
        const required = !!(reason && reason.requiresNote);
        const tooLong = text.length > limit;
        return { required, length: text.length, max: limit, tooLong, valid: !tooLong && (!required || text.length > 0) };
    };

    /** kind = cancel | notDone | reschedule; form = { reason, note, date }. */
    const canSave = (kind, form, max) => {
        const f = form || {};
        if (!f.reason || !f.reason.code) { return false; }
        if (!noteState(f.reason, f.note, max).valid) { return false; }
        if (kind === 'reschedule' && !f.date) { return false; }
        return true;
    };

    const OUTCOME = { notDone: 'missed', reschedule: 'rescheduled' };

    /**
     * Not done / reschedule = TWO calls (Sözleşme eki): 1) the outcome as a draft, 2) the submit that finalises it (the
     * reschedule's new visit is born here). The second runs ONLY when the first succeeded; a repeated Save is safe (the
     * first overwrites the draft, the second finalises; inside 60 min the second is a no-op). Only reasonNote is sent.
     * post(path, body) → Promise<{ ok, status, body }>.  Resolves { ok, step, response }.
     */
    const saveNotDone = (post, form) => {
        const outcome = OUTCOME[form.kind];
        const first = {
            plannedVisitId: form.plannedVisitId,
            executionOutcome: outcome,
            reasonCode: form.reasonCode,
            reasonNote: form.reasonNote ? String(form.reasonNote).trim() : null
        };
        if (outcome === 'rescheduled') { first.rescheduleToDate = form.rescheduleToDate; }
        return Promise.resolve(post('/outcome', first)).then(r1 => {
            if (!r1 || !r1.ok) { return { ok: false, step: 1, response: r1 }; }
            return Promise.resolve(post('/reports', { plannedVisitId: form.plannedVisitId, executionOutcome: outcome }))
                .then(r2 => ({ ok: !!(r2 && r2.ok), step: 2, response: r2 }));
        });
    };

    // ── refusal codes → user text ─────────────────────────────────────────────────────────────────────────────
    const ERROR_CODES = [
        'visit_report_deadline_passed', 'visit_report_plan_cancelled', 'visit_reason_invalid',
        'visit_reason_note_required', 'visit_reason_note_too_long', 'visit_reschedule_date_invalid',
        'visit_report_reschedule_date_invalid', 'visit_reschedule_already_applied', 'visit_cancel_past_day',
        'unplanned_visit_today_only', 'reference_data_unavailable', 'visit_report_edit_window_closed',
        'visit_report_invalid_transition', 'resource_not_caller', 'visit_reason_applies_to_invalid',
        'visit_not_yet_due', 'planned_visit_invalid_transition', 'planned_visit_overlap'
    ];

    /** The first known code in an envelope's errors (the CRM puts the code next to the message). */
    const errorKey = codes => {
        const list = Array.isArray(codes) ? codes : [];
        const hit = list.find(c => ERROR_CODES.indexOf(c) > -1);
        return hit ? 'Err_' + hit : null;
    };

    /** The user's text for a failed response: the mapped code, else the generic failure (never a raw code). */
    const errorText = (response, L) => {
        const labels = L || {};
        const errors = response && response.body && Array.isArray(response.body.errors) ? response.body.errors : [];
        const key = errorKey(errors);
        if (key && labels[key]) { return labels[key]; }
        if (response && response.status === 503) { return labels.Err_reference_data_unavailable || labels.ActionFailed || ''; }
        return labels.ActionFailed || '';
    };

    // ── content ──────────────────────────────────────────────────────────────────────────────────────────────
    const productLabel = c => (c && (c.productName || c.productCode)) || '';

    /** "What do I present": the first product is the promo, the others reminders (the mockup rule). */
    const contentRoles = items => (items || []).map((c, i) => ({ name: productLabel(c), roleKey: i === 0 ? 'RolePromo' : 'RoleReminder' }));

    // ── filters ──────────────────────────────────────────────────────────────────────────────────────────────
    /** f = { statuses: [], accountId, productId } — empty = everything. */
    const filterVisits = (visits, f) => {
        const filter = f || {};
        const statuses = filter.statuses || [];
        return (visits || []).filter(v =>
            (statuses.length === 0 || statuses.indexOf(v.workStatus) > -1)
            && (!filter.accountId || v.accountId === filter.accountId)
            && (!filter.productId || (v.plannedContent || []).some(c => c.productId === filter.productId)));
    };

    const FILTER_KEY = 'diten.crm.visitWorkspace.filters';

    /** The remembered filters; storage may be absent or throw (private window) — then nothing is remembered. */
    const loadFilters = storage => {
        try {
            const raw = storage && storage.getItem(FILTER_KEY);
            const parsed = raw ? JSON.parse(raw) : null;
            if (!parsed || typeof parsed !== 'object') { return { statuses: [], accountId: '', productId: '' }; }
            return {
                statuses: Array.isArray(parsed.statuses) ? parsed.statuses.filter(s => STATUSES.indexOf(s) > -1) : [],
                accountId: typeof parsed.accountId === 'string' ? parsed.accountId : '',
                productId: typeof parsed.productId === 'string' ? parsed.productId : ''
            };
        } catch (e) {
            return { statuses: [], accountId: '', productId: '' };
        }
    };

    const saveFilters = (storage, f) => {
        try { if (storage) { storage.setItem(FILTER_KEY, JSON.stringify(f || {})); } return true; } catch (e) { return false; }
    };

    // ── weeks / days ─────────────────────────────────────────────────────────────────────────────────────────
    const pad = n => String(n).padStart(2, '0');
    const ymd = d => d.getUTCFullYear() + '-' + pad(d.getUTCMonth() + 1) + '-' + pad(d.getUTCDate());
    const addDays = (day, n) => { const d = new Date(day + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + n); return ymd(d); };
    const mondayOf = day => { const d = new Date(day + 'T00:00:00Z'); return addDays(day, -((d.getUTCDay() + 6) % 7)); };

    /** The read window around a week: two weeks back, two ahead (35 days ≤ the 42-day limit). */
    const weekWindow = monday => ({ from: addDays(monday, -14), to: addDays(monday, 20) });

    /** The capacity bar: planned / capacity in percent (an empty capacity is 0 %, over 100 % is flagged). */
    const capacity = (planned, cap) => {
        const p = Math.max(0, planned || 0);
        const c = Math.max(0, cap || 0);
        const pct = c === 0 ? 0 : Math.round((p / c) * 100);
        return { pct: Math.min(100, pct), over: c > 0 && p > c, raw: pct };
    };

    const REOPEN_MIN = 10;
    const reopenOk = reason => String(reason || '').trim().length >= REOPEN_MIN;

    /** The same target's latest earlier visit that carries a report (the panel's "from the previous visit"). */
    const lastReportOf = (visits, visit) => (visits || [])
        .filter(v => v !== visit && v.visitReportId && v.targetId === visit.targetId && v.plannedDate < visit.plannedDate)
        .sort((a, b) => (a.plannedDate < b.plannedDate ? 1 : -1))[0] || null;

    const visitKey = v => v.plannedVisitId || v.previewKey;

    /** A visit → a DitenCalendar event (zone UTC: the CRM times are wall clock). No time ⇒ an all-day card. WP-VW-W2
     *  (WEB-b) — editable (dragged to another day / time) only when the host says so: a draft visit in Plan mode. */
    const eventOf = (v, editable) => {
        const time = v.startTime || (v.isPinned ? v.pinnedTime : null);
        const base = { id: visitKey(v), title: v.targetDisplayName || '', kind: 'visit', editable: !!editable, classNames: ['vw-event', statusStyle(v.workStatus).cssClass] };
        if (!time) { return Object.assign(base, { allDay: true, date: v.plannedDate }); }
        const start = v.plannedDate + 'T' + time + ':00Z';
        const minutes = v.durationMinutes || 30;
        const end = v.endTime ? v.plannedDate + 'T' + v.endTime + ':00Z' : new Date(Date.parse(start) + minutes * 60000).toISOString();
        return Object.assign(base, { allDay: false, startUtc: start, endUtc: end });
    };

    /** The unplanned-visit create body: today, the chosen doctor and time, a generated code, unplanned: true. */
    const unplannedBody = (contactId, today, time, nowMs) => ({
        visitCode: 'UNP-' + today.replace(/-/g, '') + '-' + String(nowMs).slice(-6),
        targetType: 'contact',
        targetId: contactId,
        plannedDate: today,
        plannedStartTime: time || null,
        visitPurpose: 'medical-visit',
        visitType: 'field-visit',
        planStatus: 'planned',
        unplanned: true
    });

    // ── Plan mode (WP-VW-W2 WEB-b) ───────────────────────────────────────────────────────────────────────────
    /** Plan mode edits ONLY a draft week; an approved or past week — or Execute mode — is read-only (no drag, no drop). */
    const canPlanWeek = (week, mode) => mode === 'plan' && !!week && week.state === 'draft';

    const PIN_STEP = 15;
    const LAST_START = 23 * 60 + 45;
    /** Minutes → the nearest 15-minute mark (a half rounds up). */
    const roundToStep = minutes => Math.round(minutes / PIN_STEP) * PIN_STEP;
    const hhmm = m => pad(Math.floor(m / 60)) + ':' + pad(m % 60);

    /**
     * A calendar drop → the day pin. drop = { allDay, date } (the all-day row: a DAY pin, no time) or { startUtc } /
     * { date, time } (a time slot: the start rounded to the 15-minute grid — the CRM refuses anything off it). The
     * workspace calendar runs in 'UTC' wall clock, so the UTC parts ARE the tenant's day and time.
     */
    const pinFromDrop = drop => {
        if (!drop) { return null; }
        if (drop.allDay) { return drop.date ? { date: drop.date, startTime: null } : null; }
        const iso = drop.startUtc || (drop.date && drop.time ? drop.date + 'T' + drop.time + ':00Z' : null);
        const d = iso ? new Date(iso) : null;
        if (!d || isNaN(d)) { return null; }
        const minutes = roundToStep(d.getUTCHours() * 60 + d.getUTCMinutes());
        return { date: ymd(d), startTime: hhmm(Math.min(minutes, LAST_START)) };
    };

    /** The week's pins after a VISIT pin of `target` on `pin`: its earlier visit pin is replaced, every other pin kept. */
    const pinsWith = (current, target, pin) => (current || [])
        .filter(p => !((p.scope || 'visit') === 'visit' && p.targetType === target.targetType && p.targetId === target.targetId))
        .concat([{ targetType: target.targetType, targetId: target.targetId, contactId: target.contactId || null, date: pin.date, scope: 'visit', startTime: pin.startTime || null }]);

    const PIN_CODES = ['pin_time_invalid', 'pin_time_outside_hours', 'pin_time_conflict', 'pin_time_past_day_end', 'pin_overflow', 'pin_day_full', 'week_already_approved'];
    const pinKey = code => (PIN_CODES.indexOf(code) > -1 ? 'Pin_' + code : null);

    /** A refused pin / selection update → the user's text: the first pin code, else the workspace's refusal text. */
    const pinText = (response, L) => {
        const labels = L || {};
        const errors = response && response.body && Array.isArray(response.body.errors) ? response.body.errors : [];
        const hit = errors.map(pinKey).find(k => k && labels[k]);
        return hit ? labels[hit] : errorText(response, labels);
    };

    /** The calendar read carries no move reason: a time pin that sits EARLIER than asked was pulled back from the day's
     *  end (pin_time_past_day_end), a LATER one lost its time to another pin (pin_time_conflict); null = kept its time. */
    const pinMoveCode = v => (!v || !v.pinnedTime || !v.startTime || v.startTime === v.pinnedTime
        ? null : (v.startTime < v.pinnedTime ? 'pin_time_past_day_end' : 'pin_time_conflict'));

    const DRAG_PREFIX = 'doctor:';
    const dragItem = (contactId, accountId) => DRAG_PREFIX + contactId + '|' + (accountId || '');
    const parseDragItem = s => {
        const m = /^doctor:([^|]+)\|(.*)$/.exec(String(s || ''));
        return m ? { contactId: m[1], accountId: m[2] || null } : null;
    };

    /** The institution filter: { accountId: accountDisplayName } — a visit without accountDisplayName adds nothing (a
     *  doctor's name is never an institution); empty ⇒ the page hides the filter. */
    const accountOptions = visits => {
        const map = {};
        (visits || []).forEach(v => { if (v && v.accountId && v.accountDisplayName) { map[v.accountId] = v.accountDisplayName; } });
        return map;
    };

    /** "N visits did not fit": name, institution and the reason in the user's language (a pin code, a planning reason,
     *  else the generic text). */
    const unplacedRows = (week, L) => {
        const labels = L || {};
        return (week && Array.isArray(week.unplaced) ? week.unplaced : []).map(u => ({
            name: u.displayName || '—',
            account: u.accountDisplayName || '',
            reason: labels[pinKey(u.reason)] || labels['Reason_' + u.reason] || labels.UnplacedReasonOther || ''
        }));
    };

    const sessionVersionOf = week => (week && typeof week.sessionVersion === 'number' ? week.sessionVersion : null);

    return {
        STATUSES, statusStyle, countdown, showsCountdown, layoutFor, PHONE_MAX, actionsFor, noteState, canSave,
        saveNotDone, ERROR_CODES, errorKey, errorText, productLabel, contentRoles, filterVisits, loadFilters,
        saveFilters, FILTER_KEY, addDays, mondayOf, weekWindow, capacity, reopenOk, REOPEN_MIN, lastReportOf, visitKey,
        eventOf, unplannedBody,
        canPlanWeek, PIN_STEP, roundToStep, pinFromDrop, pinsWith, PIN_CODES, pinKey, pinText, pinMoveCode,
        dragItem, parseDragItem, accountOptions, unplacedRows, sessionVersionOf
    };
}));
