/**
 * WP-VW-W2 (WEB-a) — the Visit Workspace page (Execute mode): the week strip, the week header, the calendar (week /
 * day time grid on a desktop, the day list on a phone), the cards, the filters, the detail panel and the cancel /
 * not done / reschedule dialogs. Every rule lives in workspace-core.js (pure, tested in Node); every text comes from
 * window.VisitWorkspaceL10n (7 languages) or from the CRM (reason labels from the reference set, names, products).
 * Dates / numbers in the APPLICATION language through VisitPlanningFormat; every data name isolated (bidi) for RTL.
 * Calls go to the same-origin proxy /CRM/VisitWorkspace/api/*.
 * WP-VW-W2 (WEB-b) — Plan mode: the Targets panel (the Visit Planning rules, targets-core.js, and its proxies under
 * /CRM/VisitPlanning/api), a doctor dragged onto a day / time and a draft card moved there = a day pin with a start time
 * (the existing session update), apply products, the institution filter (accountDisplayName) and the unplaced list.
 */
(function (window, document) {
    'use strict';

    const C = window.VisitWorkspaceCore;
    const TC = window.VisitPlanningTargetsCore || null; // WP-VW-W2 (WEB-b) — Plan mode needs the shared Targets rules
    const F = window.VisitPlanningFormat;
    const L = window.VisitWorkspaceL10n || {};
    const root = document.getElementById('vw-root');
    if (!C || !F || !root) { return; }

    const base = root.getAttribute('data-api-base') || '/CRM/VisitWorkspace/api';
    const planningUrl = root.getAttribute('data-planning-url') || '/CRM/VisitPlanning';
    const executionUrl = root.getAttribute('data-execution-url') || '/CRM/VisitExecution';
    const planBase = root.getAttribute('data-planning-api-base') || '/CRM/VisitPlanning/api';
    const perms = {
        manage: root.getAttribute('data-can-manage') === 'true',
        record: root.getAttribute('data-can-record') === 'true',
        apply: root.getAttribute('data-can-apply') === 'true',
        contacts: root.getAttribute('data-can-search-contacts') === 'true',
        plan: root.getAttribute('data-can-plan') === 'true' // the Visit Planning read + generate keys
    };

    // ── tiny helpers ─────────────────────────────────────────────────────────────────────────────────────────
    const el = id => document.getElementById(id);
    const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const lang = () => F.culture().slice(0, 2).toLowerCase();
    const todayYmd = () => new Date().toISOString().slice(0, 10);
    const toast = (msg, type) => { if (window.showToast) { window.showToast(msg, type || 'success'); } };
    const hours = min => F.hours(min, L.HoursShort || '{0} h');
    const setText = (id, t) => { const n = el(id); if (n) { n.textContent = t; } };

    const call = (method, path, body, prefix) => fetch((prefix || base) + path, {
        method: method,
        credentials: 'same-origin',
        headers: Object.assign({ Accept: 'application/json' }, body ? { 'Content-Type': 'application/json' } : {}),
        body: body ? JSON.stringify(body) : undefined
    }).then(r => r.text().then(t => {
        let b = null;
        try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; }
        return { ok: r.ok, status: r.status, body: b };
    })).catch(() => ({ ok: false, status: 0, body: null }));
    const get = path => call('GET', path);
    const post = (path, body) => call('POST', path, body);

    // ── state ────────────────────────────────────────────────────────────────────────────────────────────────
    const state = {
        week: C.mondayOf(todayYmd()),
        day: todayYmd(),
        window: null,
        data: null,
        contract: null,
        filters: C.loadFilters(safeStorage()),
        layout: C.layoutFor(window.innerWidth),
        calendar: null,
        current: null,
        mode: 'execute' // WP-VW-W2 (WEB-b) — 'plan' shows the Targets panel and edits a draft week
    };

    function safeStorage() { try { return window.localStorage; } catch (e) { return null; } }

    // ── loading ──────────────────────────────────────────────────────────────────────────────────────────────
    const inWindow = monday => state.window && monday >= state.window.from && C.addDays(monday, 6) <= state.window.to;

    function load(force) {
        if (!force && state.data && inWindow(state.week)) { render(); return Promise.resolve(); }
        state.window = C.weekWindow(state.week);
        setBusy(true);
        return get('/calendar?from=' + state.window.from + '&to=' + state.window.to).then(r => {
            setBusy(false);
            if (!r.ok || !r.body || !r.body.data) {
                state.data = null;
                showNotice(C.errorText(r, L) || L.CalendarLoadFailed || '');
                return;
            }
            state.data = r.body.data;
            showNotice('');
            render();
        });
    }

    function setBusy(on) { const s = el('vw-busy'); if (s) { s.classList.toggle('d-none', !on); } }
    function showNotice(text) {
        const n = el('vw-notice'); if (!n) { return; }
        n.textContent = text || '';
        n.classList.toggle('d-none', !text);
    }

    // ── rendering ────────────────────────────────────────────────────────────────────────────────────────────
    const weekOf = monday => (state.data ? state.data.weeks.find(w => w.weekStart === monday) : null) || null;
    const daysOf = monday => (state.data ? state.data.days.filter(d => C.mondayOf(d.date) === monday) : []);
    const visitsOf = monday => C.filterVisits((state.data ? state.data.visits : []).filter(v => v.weekStart === monday), state.filters);

    function render() {
        renderMode();
        renderStrip();
        renderHeader();
        renderFilters();
        if (state.layout === 'list') { renderDayList(); } else { renderCalendar(); }
        renderPanel();
    }

    const STATE_KEY = { draft: 'WeekStateDraft', approved: 'WeekStateApproved', past: 'WeekStatePast', none: 'WeekStateNone' };
    const STATE_TONE = { draft: 'primary', approved: 'success', past: 'secondary', none: 'light' };

    function renderStrip() {
        const strip = el('vw-week-strip'); if (!strip || !state.data) { return; }
        strip.innerHTML = state.data.weeks.map(w => {
            const active = w.weekStart === state.week;
            return '<button type="button" class="btn btn-sm ' + (active ? 'btn-primary' : 'btn-outline-secondary') + ' vw-strip-week" data-week="' + esc(w.weekStart) + '">'
                + '<span class="d-block fw-medium">' + esc(fmt(L.WeekLabel || '{0}', w.weekNumber)) + '</span>'
                + '<span class="d-block small">' + esc(F.workRange(w.weekStart, C.addDays(w.weekStart, 4))) + '</span>'
                + '<span class="badge bg-label-' + STATE_TONE[w.state] + ' mt-1">' + esc(L[STATE_KEY[w.state]] || w.state) + '</span>'
                + '</button>';
        }).join('');
    }

    function renderHeader() {
        const w = weekOf(state.week);
        const title = el('vw-week-title'), badge = el('vw-week-badge'), bar = el('vw-capacity-bar'), barText = el('vw-capacity-text');
        if (!w) { if (title) { title.textContent = ''; } return; }
        if (title) { title.textContent = fmt(L.WeekLabel || '{0}', w.weekNumber) + ' · ' + F.workRange(w.weekStart, C.addDays(w.weekStart, 4)); }
        if (badge) {
            badge.className = 'badge bg-label-' + STATE_TONE[w.state];
            badge.textContent = L[STATE_KEY[w.state]] || w.state;
        }
        const cap = C.capacity(w.plannedMinutes, w.capacityMinutes);
        if (bar) {
            bar.style.width = cap.pct + '%';
            bar.className = 'progress-bar ' + (cap.over ? 'bg-danger' : 'bg-primary');
            bar.setAttribute('aria-valuenow', String(cap.raw));
        }
        if (barText) { barText.textContent = F.isolateRatios(fmt(L.CapacityLabel || '{0} / {1}', hours(w.plannedMinutes), hours(w.capacityMinutes))); }

        const unplaced = el('vw-unplaced');
        if (unplaced) {
            unplaced.classList.toggle('d-none', !(w.unplacedCount > 0));
            unplaced.textContent = fmt(L.Unplaced || '{0}', w.unplacedCount);
        }
        const approve = el('vw-approve'), reopen = el('vw-reopen');
        if (approve) { approve.classList.toggle('d-none', !(perms.apply && w.canApprove)); }
        if (reopen) { reopen.classList.toggle('d-none', !(perms.apply && w.canReopen)); }
        const unplanned = el('vw-unplanned-open');
        if (unplanned) {
            const today = todayYmd();
            unplanned.classList.toggle('d-none', !(perms.manage && today >= w.weekStart && today <= C.addDays(w.weekStart, 6)));
        }
    }

    function renderFilters() {
        const visits = state.data ? state.data.visits : [];
        const products = {};
        visits.forEach(v => { (v.plannedContent || []).forEach(c => { products[c.productId] = C.productLabel(c); }); });
        // WP-VW-W2 (WEB-b) — the institution filter reads accountDisplayName ONLY (never a doctor's name); none = hidden
        const accounts = C.accountOptions(visits);
        const hasAccounts = Object.keys(accounts).length > 0;
        if (!hasAccounts && state.filters.accountId) { state.filters.accountId = ''; }
        fillSelect('vw-filter-account', accounts, L.FilterAllAccounts, state.filters.accountId);
        const accountSelect = el('vw-filter-account');
        if (accountSelect) { accountSelect.classList.toggle('d-none', !hasAccounts); }
        fillSelect('vw-filter-product', products, L.FilterAllProducts, state.filters.productId);
        const box = el('vw-filter-status');
        if (box && !box.dataset.ready) {
            box.innerHTML = C.STATUSES.map(s => '<label class="dropdown-item d-flex gap-2 align-items-center"><input type="checkbox" class="form-check-input m-0" value="' + s + '"'
                + (state.filters.statuses.indexOf(s) > -1 ? ' checked' : '') + '> <span>' + esc(L['Status_' + s] || s) + '</span></label>').join('');
            box.dataset.ready = '1';
        }
        const count = el('vw-filter-status-count');
        if (count) { count.textContent = state.filters.statuses.length ? String(state.filters.statuses.length) : ''; }
    }

    function fillSelect(id, map, allLabel, selected) {
        const s = el(id); if (!s) { return; }
        const entries = Object.keys(map).map(k => [k, map[k]]).sort((a, b) => String(a[1]).localeCompare(String(b[1]), F.culture()));
        s.innerHTML = '<option value="">' + esc(allLabel || '') + '</option>'
            + entries.map(e => '<option value="' + esc(e[0]) + '"' + (e[0] === selected ? ' selected' : '') + '>' + esc(F.isolate(e[1])) + '</option>').join('');
    }

    // ── the card ────────────────────────────────────────────────────────────────────────────────────────────
    function cardHtml(v) {
        const st = C.statusStyle(v.workStatus);
        const icons = (v.isPinned ? '<i class="bx bx-pin" title="' + esc(L.Icon_Pinned || '') + (v.pinnedTime ? ' ' + esc(v.pinnedTime) : '') + '"></i>' : '')
            + (v.source === 'unplanned' ? '<i class="bx bx-walk" title="' + esc(L.Icon_Unplanned || '') + '"></i>' : '')
            + (v.rescheduledFromPlannedVisitId ? '<i class="bx bx-calendar-edit" title="' + esc(L.Icon_Rescheduled || '') + '"></i>' : '');
        const products = (v.plannedContent || []).map(c => '<span class="vw-chip">' + F.bidi(C.productLabel(c)) + '</span>').join('');
        let countdownHtml = '';
        if (C.showsCountdown(v.workStatus)) {
            const cd = C.countdown(v.reportDeadline, Date.now());
            if (cd) { countdownHtml = '<div class="vw-countdown">' + esc(cd.passed ? (L.CountdownPassed || '') : F.isolateRatios(fmt(L.CountdownLeft || '{0} {1}', cd.hours, cd.minutes))) + '</div>'; }
        }
        const time = v.startTime ? '<span class="vw-time">' + esc(v.startTime) + (v.endTime ? '–' + esc(v.endTime) : '') + '</span>' : '';
        return '<div class="vw-card ' + st.cssClass + (st.dashed ? ' vw-dashed' : '') + (st.faded ? ' vw-faded' : '') + (st.strike ? ' vw-strike' : '') + '">'
            + '<div class="vw-card-top"><i class="bx ' + st.icon + '"></i>' + time + '<span class="vw-icons">' + icons + '</span></div>'
            + '<div class="vw-name">' + F.bidi(v.targetDisplayName || '—') + '</div>'
            + '<div class="vw-status">' + esc(L[st.labelKey] || v.workStatus) + '</div>'
            + (products ? '<div class="vw-chips">' + products + '</div>' : '')
            + countdownHtml
            + '</div>';
    }

    // ── desktop: the time grid (DitenCalendar over the vendored FullCalendar) ─────────────────────────────────
    function renderCalendar() {
        const host = el('vw-calendar'), list = el('vw-day-list');
        if (list) { list.classList.add('d-none'); }
        if (!host) { return; }
        host.classList.remove('d-none');
        const visits = visitsOf(state.week);
        const byId = {};
        visits.forEach(v => { byId[C.visitKey(v)] = v; });
        // WP-VW-W2 (WEB-b) — Plan mode on a draft week: the draft cards move; nothing else does
        const editable = planEditable();
        const events = visits.map(v => C.eventOf(v, editable && v.workStatus === 'draft'));
        const days = daysOf(state.week).map(d => ({ date: d.date, dayKind: d.isHoliday ? 'holiday' : (d.kind === 'weekend' ? 'weekend' : 'working'), holidayName: d.holidayName || L.HolidayLabel || '' }));
        // CT (live E4) — the lookup is set BEFORE DitenCalendar.create: FullCalendar draws the first events inside create
        // (eventContent → renderExtras), and a missing lookup threw there, so the grid never rendered.
        state.byId = byId;

        if (!state.calendar && window.DitenCalendar) {
            state.calendar = window.DitenCalendar.create(host, {
                zone: 'UTC', view: 'week', date: state.week, editable: editable, events: events, days: [],
                onExternalDrop: planDrop, // a Targets row dropped on a day / time
                onEventMove: planMove,    // a draft card moved to another day / time
                renderExtras: ev => { const v = (state.byId || {})[ev.id]; return v ? cardHtml(v) : ''; },
                onEventClick: id => openDetail((state.byId || {})[id]),
                onRangeChange: info => {
                    const monday = C.mondayOf(info.from);
                    if (monday !== state.week) { state.week = monday; load(false); }
                }
            });
            if (state.calendar) {
                const fc = state.calendar.calendar;
                fc.setOption('weekends', false);
                fc.setOption('headerToolbar', { start: 'prev,next today', center: 'title', end: 'timeGridWeek,timeGridDay' });
                fc.setOption('slotMinTime', '08:00:00');
                fc.setOption('slotMaxTime', '19:00:00');
                fc.setOption('dayHeaderContent', arg => ({ html: dayHeaderHtml(arg.date.toISOString().slice(0, 10)) }));
            }
        }
        state.byId = byId;
        if (state.calendar) {
            if (typeof state.calendar.setEditable === 'function') { state.calendar.setEditable(editable); }
            state.calendar.setData(events, days);
            if (state.calendar.date() < state.week || state.calendar.date() > C.addDays(state.week, 6)) { state.calendar.calendar.gotoDate(state.week); }
        }
        const empty = el('vw-empty');
        if (empty) { empty.classList.toggle('d-none', visits.length > 0 || !!(weekOf(state.week) && weekOf(state.week).state !== 'none')); }
    }

    function dayHeaderHtml(date) {
        const d = (state.data ? state.data.days : []).find(x => x.date === date);
        const head = '<div class="vw-day-head"><span class="fw-medium">' + esc(F.dayLabel(date)) + '</span>';
        if (!d) { return head + '</div>'; }
        if (d.isHoliday) { return head + '<span class="badge bg-label-danger">' + esc(d.holidayName || L.HolidayLabel || '') + '</span></div>'; }
        const count = (state.data.visits || []).filter(v => v.plannedDate === date && v.workStatus !== 'cancelled').length;
        return head + '<span class="small text-muted">' + esc(F.isolateRatios(fmt(L.DayLoad || '{0} · {1}', count, hours(d.freeMinutes)))) + '</span></div>';
    }

    // ── phone: the day list ──────────────────────────────────────────────────────────────────────────────────
    function renderDayList() {
        const host = el('vw-calendar'), list = el('vw-day-list');
        if (host) { host.classList.add('d-none'); }
        if (!list) { return; }
        list.classList.remove('d-none');
        if (state.day < state.week || state.day > C.addDays(state.week, 6)) { state.day = state.week; }
        const tabs = [0, 1, 2, 3, 4].map(i => {
            const day = C.addDays(state.week, i);
            return '<button type="button" class="btn btn-sm ' + (day === state.day ? 'btn-primary' : 'btn-outline-secondary') + ' vw-list-day" data-day="' + day + '">' + esc(F.dayLabel(day)) + '</button>';
        }).join('');
        const visits = C.filterVisits((state.data ? state.data.visits : []).filter(v => v.plannedDate === state.day), state.filters);
        state.byId = {};
        visits.forEach(v => { state.byId[C.visitKey(v)] = v; });
        list.innerHTML = '<div class="d-flex gap-1 flex-wrap mb-2">' + tabs + '</div>'
            + '<div class="mb-2">' + dayHeaderHtml(state.day) + '</div>'
            + (visits.length
                ? visits.map(v => '<button type="button" class="vw-list-card" data-key="' + esc(C.visitKey(v)) + '">' + cardHtml(v) + '</button>').join('')
                : '<div class="text-muted small p-3">' + esc(L.NoVisitDay || '') + '</div>');
    }

    // ── the detail panel ─────────────────────────────────────────────────────────────────────────────────────
    const BAND = { missed: 'BandMissed', expired: 'BandExpired', report_missing: 'BandReportMissing', draft: 'BandDraft', cancelled: 'BandCancelled' };

    function openDetail(v) {
        if (!v) { return; }
        state.current = v;
        const st = C.statusStyle(v.workStatus);
        el('vw-detail-title').innerHTML = F.bidi(v.targetDisplayName || '—');
        el('vw-detail-sub').textContent = F.dayLabel(v.plannedDate) + (v.startTime ? ' · ' + v.startTime : '');

        const band = el('vw-detail-band');
        const cd = C.showsCountdown(v.workStatus) ? C.countdown(v.reportDeadline, Date.now()) : null;
        band.className = 'vw-band ' + st.cssClass;
        band.innerHTML = '<i class="bx ' + st.icon + '"></i> <span class="fw-medium">' + esc(L[st.labelKey] || v.workStatus) + '</span>'
            + (BAND[v.workStatus] ? '<div class="small">' + esc(L[BAND[v.workStatus]] || '') + '</div>' : '')
            + (cd ? '<div class="small">' + esc(cd.passed ? (L.CountdownPassed || '') : F.isolateRatios(fmt(L.CountdownLeft || '{0} {1}', cd.hours, cd.minutes))) + '</div>' : '')
            + (v.cancellationNote ? '<div class="small">' + F.bidi(v.cancellationNote) + '</div>' : '');

        const roles = C.contentRoles(v.plannedContent);
        el('vw-detail-content').innerHTML = roles.length
            ? roles.map(r => '<li class="d-flex justify-content-between gap-2"><span>' + F.bidi(r.name) + '</span><span class="badge ' + (r.roleKey === 'RolePromo' ? 'bg-label-primary' : 'bg-label-secondary') + '">' + esc(L[r.roleKey] || '') + '</span></li>').join('')
            : '<li class="text-muted">' + esc(L.NoContent || '') + '</li>';

        renderPrevious(v);
        renderFrequency(v);
        renderActions(v);

        const panel = el('vw-detail');
        if (panel && window.bootstrap) { window.bootstrap.Offcanvas.getOrCreateInstance(panel).show(); }
    }

    function renderPrevious(v) {
        const box = el('vw-detail-previous');
        const prev = C.lastReportOf(state.data ? state.data.visits : [], v);
        box.classList.add('d-none');
        if (!prev) { return; }
        get('/reports/' + encodeURIComponent(prev.visitReportId)).then(r => {
            if (state.current !== v || !r.ok || !r.body || !r.body.data) { return; }
            const rep = r.body.data;
            const feedback = rep.feedback && rep.feedback.doctorFeedback;
            el('vw-detail-previous-body').innerHTML = '<div class="small text-muted">' + esc(F.dayLabel(prev.plannedDate)) + ' · ' + esc(L['Outcome_' + (rep.executionOutcome || '')] || '') + '</div>'
                + (feedback ? '<div>' + F.bidi(feedback) + '</div>' : '');
            box.classList.remove('d-none');
        });
    }

    function renderFrequency(v) {
        const box = el('vw-detail-frequency');
        box.classList.add('d-none');
        const w = weekOf(v.weekStart);
        if (!w || !w.planningSessionId || !v.contactId) { return; }
        get('/sessions/' + encodeURIComponent(w.planningSessionId) + '/targets?weekStart=' + encodeURIComponent(v.weekStart)).then(r => {
            if (state.current !== v || !r.ok || !r.body || !r.body.data) { return; }
            const doc = (r.body.data.doctors || []).find(d => d.contactId === v.contactId);
            const s = doc && doc.status;
            if (!s || s.requiredVisitCount == null) { return; }
            box.textContent = F.isolateRatios(fmt(L.Frequency || '{0} / {1} · {2}', s.done || 0, s.requiredVisitCount, s.planned || 0));
            box.classList.remove('d-none');
        });
    }

    const ACTION = {
        cancel: { label: 'Action_cancel', tone: 'outline-danger', icon: 'bx-block' },
        result: { label: 'Action_result', tone: 'primary', icon: 'bx-play-circle' },
        notDone: { label: 'Action_notDone', tone: 'outline-danger', icon: 'bx-x-circle' },
        reschedule: { label: 'Action_reschedule', tone: 'primary', icon: 'bx-calendar-edit' },
        sendReport: { label: 'Action_sendReport', tone: 'warning', icon: 'bx-send' },
        plan: { label: 'Action_plan', tone: 'outline-primary', icon: 'bx-edit' }
    };

    function renderActions(v) {
        const box = el('vw-detail-actions');
        const keys = C.actionsFor(v, todayYmd(), perms);
        box.innerHTML = keys.map(k => {
            if (k === 'locked') { return '<div class="alert alert-secondary mb-0 small"><i class="bx bx-lock-alt"></i> ' + esc(L.LockedInfo || '') + '</div>'; }
            const a = ACTION[k];
            if (k === 'result' || k === 'sendReport') { return '<a class="btn btn-' + a.tone + '" href="' + esc(executionUrl) + '"><i class="bx ' + a.icon + '"></i> ' + esc(L[a.label] || '') + '</a>'; }
            if (k === 'plan') {
                const w = weekOf(v.weekStart);
                const href = w && w.planningSessionId ? planningUrl + '/Details/' + encodeURIComponent(w.planningSessionId) : planningUrl;
                return '<a class="btn btn-' + a.tone + '" href="' + esc(href) + '"><i class="bx ' + a.icon + '"></i> ' + esc(L[a.label] || '') + '</a>';
            }
            return '<button type="button" class="btn btn-' + a.tone + '" data-dialog="' + k + '"><i class="bx ' + a.icon + '"></i> ' + esc(L[a.label] || '') + '</button>';
        }).join('');
    }

    // ── E2 dialogs: cancel / not done / reschedule ───────────────────────────────────────────────────────────
    const APPLIES = { cancel: 'cancel', notDone: 'missed', reschedule: 'reschedule' };
    const TITLE = { cancel: 'DlgCancelTitle', notDone: 'DlgNotDoneTitle', reschedule: 'DlgRescheduleTitle' };
    const dlg = { kind: null, reasons: [], reason: null, date: null, saving: false };

    function maxNote() { return (state.contract && state.contract.maxNoteLength) || 500; }

    function openDialog(kind) {
        const v = state.current; if (!v) { return; }
        dlg.kind = kind; dlg.reasons = []; dlg.reason = null; dlg.date = null; dlg.saving = false;
        el('vw-dlg-title').textContent = L[TITLE[kind]] || '';
        el('vw-dlg-target').innerHTML = F.bidi(v.targetDisplayName || '—');
        el('vw-dlg-note').value = '';
        el('vw-dlg-error').classList.add('d-none');
        el('vw-dlg-date-box').classList.toggle('d-none', kind !== 'reschedule');
        el('vw-dlg-reasons').innerHTML = '<div class="text-muted small">' + esc(L.Loading || '') + '</div>';
        el('vw-dlg-days').innerHTML = '';
        syncDialog();
        const modal = el('vw-dialog');
        if (modal && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(modal).show(); }

        get('/reasons?appliesTo=' + APPLIES[kind] + '&lang=' + encodeURIComponent(lang())).then(r => {
            if (dlg.kind !== kind) { return; }
            if (!r.ok || !r.body || !r.body.data) {
                el('vw-dlg-reasons').innerHTML = '<div class="text-danger small">' + esc(L.ReasonsUnavailable || C.errorText(r, L)) + '</div>';
                return;
            }
            dlg.reasons = r.body.data.items || [];
            el('vw-dlg-reasons').innerHTML = dlg.reasons.map((x, i) =>
                '<label class="form-check"><input class="form-check-input" type="radio" name="vw-reason" value="' + esc(x.code) + '" id="vw-reason-' + i + '"> '
                + '<span class="form-check-label">' + F.bidi(x.label) + '</span></label>').join('');
            syncDialog();
        });

        if (kind === 'reschedule') {
            get('/reschedule-options?plannedVisitId=' + encodeURIComponent(v.plannedVisitId)).then(r => {
                if (dlg.kind !== kind) { return; }
                const daysList = r.ok && r.body && r.body.data ? r.body.data.days || [] : [];
                el('vw-dlg-days').innerHTML = daysList.length
                    ? daysList.map(d => {
                        const cap = C.capacity(d.plannedMinutes, d.capacityMinutes);
                        return '<button type="button" class="vw-day-option" data-date="' + esc(d.date) + '">'
                            + '<span class="fw-medium">' + esc(F.dayLabel(d.date)) + '</span>'
                            + '<span class="small text-muted">' + esc(F.isolateRatios(fmt(L.RescheduleDayLoad || '{0} · {1} / {2}', d.plannedCount, hours(d.plannedMinutes), hours(d.capacityMinutes)))) + '</span>'
                            + '<span class="progress vw-mini"><span class="progress-bar ' + (cap.over ? 'bg-danger' : 'bg-primary') + '" style="width:' + cap.pct + '%"></span></span>'
                            + '</button>';
                    }).join('')
                    : '<div class="text-muted small">' + esc(L.RescheduleNoDays || '') + '</div>';
            });
        }
    }

    function syncDialog() {
        const note = el('vw-dlg-note').value;
        const ns = C.noteState(dlg.reason, note, maxNote());
        el('vw-dlg-note-count').textContent = F.ratio(ns.length, ns.max);
        el('vw-dlg-note-required').classList.toggle('d-none', !ns.required);
        el('vw-dlg-note').classList.toggle('is-invalid', ns.tooLong || (ns.required && ns.length === 0 && note.length > 0));
        el('vw-dlg-save').disabled = dlg.saving || !C.canSave(dlg.kind, { reason: dlg.reason, note: note, date: dlg.date }, maxNote());
    }

    function showDialogError(r) {
        const box = el('vw-dlg-error');
        box.textContent = C.errorText(r, L);
        box.classList.remove('d-none');
    }

    function saveDialog() {
        const v = state.current;
        const note = el('vw-dlg-note').value.trim();
        if (!v || !C.canSave(dlg.kind, { reason: dlg.reason, note: note, date: dlg.date }, maxNote())) { return; }
        dlg.saving = true; syncDialog();
        const done = r => {
            dlg.saving = false;
            if (r.ok) {
                const modal = el('vw-dialog');
                if (modal && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(modal).hide(); }
                const panel = el('vw-detail');
                if (panel && window.bootstrap) { window.bootstrap.Offcanvas.getOrCreateInstance(panel).hide(); }
                toast(L.Saved || '');
                load(true);
                return;
            }
            syncDialog();
            showDialogError(r.response || r);
        };
        if (dlg.kind === 'cancel') {
            post('/planned-visits/' + encodeURIComponent(v.plannedVisitId) + '/cancel', { reasonCode: dlg.reason.code, note: note || null }).then(done);
            return;
        }
        C.saveNotDone(post, {
            kind: dlg.kind, plannedVisitId: v.plannedVisitId, reasonCode: dlg.reason.code, reasonNote: note || null,
            rescheduleToDate: dlg.date
        }).then(done);
    }

    // ── approve / reopen the week (the existing Visit Planning endpoints) ─────────────────────────────────────
    function withSession(w) {
        return get('/sessions/' + encodeURIComponent(w.planningSessionId)).then(r => (r.ok && r.body && r.body.data ? r.body.data : null));
    }

    // WP-VW-W2 (WEB-b) — the week's sessionVersion from the calendar read (BE-c); only without it the session is read.
    function versionOf(w) {
        const known = C.sessionVersionOf(w);
        return known != null ? Promise.resolve(known) : withSession(w).then(s => (s ? s.version : null));
    }

    function approveWeek() {
        const w = weekOf(state.week); if (!w || !w.planningSessionId) { return; }
        const btn = el('vw-approve'); btn.disabled = true;
        versionOf(w).then(version => {
            if (version == null) { btn.disabled = false; toast(L.ActionFailed || '', 'error'); return null; }
            return post('/apply', { planningSessionId: w.planningSessionId, expectedVersion: version, weekStart: w.weekStart });
        }).then(r => {
            if (!r) { return; }
            btn.disabled = false;
            if (r.ok) { toast(L.ApproveDone || ''); load(true); } else { toast(C.errorText(r, L), 'error'); }
        });
    }

    function openReopen() {
        const w = weekOf(state.week); if (!w) { return; }
        el('vw-reopen-title').textContent = fmt(L.ReopenTitle || '{0}', fmt(L.WeekLabel || '{0}', w.weekNumber));
        el('vw-reopen-reason').value = '';
        syncReopen();
        const m = el('vw-reopen-modal');
        if (m && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(m).show(); }
    }

    function syncReopen() {
        const v = el('vw-reopen-reason').value;
        el('vw-reopen-confirm').disabled = !C.reopenOk(v);
        el('vw-reopen-count').textContent = F.ratio(v.trim().length, C.REOPEN_MIN);
    }

    function confirmReopen() {
        const w = weekOf(state.week);
        const reason = el('vw-reopen-reason').value.trim();
        if (!w || !C.reopenOk(reason)) { return; }
        const btn = el('vw-reopen-confirm'); btn.disabled = true;
        versionOf(w).then(version => {
            if (version == null) { btn.disabled = false; toast(L.ActionFailed || '', 'error'); return null; }
            return post('/sessions/' + encodeURIComponent(w.planningSessionId) + '/weeks/' + encodeURIComponent(w.weekStart) + '/reopen',
                { reason: reason, expectedVersion: version });
        }).then(r => {
            if (!r) { return; }
            btn.disabled = false;
            if (r.ok) {
                const m = el('vw-reopen-modal');
                if (m && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(m).hide(); }
                toast(L.ReopenDone || ''); load(true);
            } else { toast(C.errorText(r, L), 'error'); }
        });
    }

    function showUnplaced() {
        const w = weekOf(state.week); if (!w) { return; }
        el('vw-unplaced-body').textContent = fmt(L.UnplacedDetail || '{0}', w.unplacedCount);
        // WP-VW-W2 (WEB-b) — the list itself (weeks[].unplaced[], BE-c): name, institution, the reason in the user's language
        const list = el('vw-unplaced-list');
        if (list) {
            list.innerHTML = C.unplacedRows(w, L).map(u => '<li class="list-group-item px-0"><div class="fw-medium">' + F.bidi(u.name) + '</div>'
                + (u.account ? '<div class="small text-muted">' + F.bidi(u.account) + '</div>' : '')
                + '<div class="small">' + esc(u.reason) + '</div></li>').join('');
        }
        const m = el('vw-unplaced-modal');
        if (m && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(m).show(); }
    }

    // ── unplanned visit (today only) ─────────────────────────────────────────────────────────────────────────
    const unp = { contactId: null, timer: null };

    function openUnplanned() {
        unp.contactId = null;
        el('vw-unp-search').value = '';
        el('vw-unp-results').innerHTML = '';
        el('vw-unp-time').value = '';
        el('vw-unp-day').textContent = F.dayLabel(todayYmd());
        el('vw-unp-error').classList.add('d-none');
        el('vw-unp-save').disabled = true;
        const m = el('vw-unplanned-modal');
        if (m && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(m).show(); }
    }

    function searchContacts() {
        const q = el('vw-unp-search').value.trim();
        clearTimeout(unp.timer);
        if (q.length < 2 || !perms.contacts) { el('vw-unp-results').innerHTML = ''; return; }
        unp.timer = setTimeout(() => {
            get('/contacts/search?search=' + encodeURIComponent(q) + '&limit=10').then(r => {
                const items = r.ok && r.body && Array.isArray(r.body.data) ? r.body.data : [];
                el('vw-unp-results').innerHTML = items.length
                    ? items.map(c => '<button type="button" class="list-group-item list-group-item-action" data-contact="' + esc(c.id) + '">' + F.bidi(c.displayName) + '</button>').join('')
                    : '<div class="text-muted small p-2">' + esc(L.NoMatches || '') + '</div>';
            });
        }, 250);
    }

    function saveUnplanned() {
        if (!unp.contactId) { return; }
        el('vw-unp-save').disabled = true;
        post('/planned-visits/unplanned', C.unplannedBody(unp.contactId, todayYmd(), el('vw-unp-time').value || null, Date.now())).then(r => {
            el('vw-unp-save').disabled = false;
            if (r.ok) {
                const m = el('vw-unplanned-modal');
                if (m && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(m).hide(); }
                toast(L.Saved || ''); load(true);
                return;
            }
            const box = el('vw-unp-error'); box.textContent = C.errorText(r, L); box.classList.remove('d-none');
        });
    }


    // ── Plan mode (WP-VW-W2 WEB-b) ───────────────────────────────────────────────────────────────────────────
    // The Targets panel next to the calendar runs on the Visit Planning page's OWN rules (targets-core.js — the same
    // module that page uses, no copy) and its own proxies (/CRM/VisitPlanning/api: the session update, the 3D doctor
    // read, the product search, the reference labels). Only a DRAFT week is edited; an approved / past week is read-only
    // with the reopen hint. A doctor dropped on a day / time, or a draft card moved there, is a day pin (+ start time on
    // the 15-minute grid) through the EXISTING session update.
    const plan = { sessionId: null, week: null, session: null, accounts: [], accountId: null, quick: 'due', rows: {}, counts: {}, specLabels: {}, picked: [], results: [] };
    const planCall = (method, path, body) => call(method, path, body, planBase);
    const QUICK = ['due', 'never', 'all'];
    const QUICK_KEY = { due: 'QuickDue', never: 'QuickNever', all: 'QuickAll' };
    const weekText = w => fmt(L.WeekLabel || '{0}', w.weekNumber);
    const savedContacts = () => (plan.session && plan.session.selectedContacts) || [];
    const inPlan = cid => savedContacts().some(c => c.contactId === cid && (!plan.accountId || !c.accountId || c.accountId === plan.accountId));
    const weekPins = ws => ((((plan.session && plan.session.weeks) || []).find(x => x.weekStart === ws) || {}).dayPins || []).map(TC.pinInput);
    const doctorKey = (aid, quick) => aid + '|' + quick + '|' + state.week;
    const planEditable = () => !!TC && perms.plan && state.layout === 'grid' && C.canPlanWeek(weekOf(state.week), state.mode);

    function setMode(mode) {
        state.mode = mode === 'plan' && perms.plan && TC ? 'plan' : 'execute';
        render();
        if (state.calendar && state.calendar.calendar && typeof state.calendar.calendar.updateSize === 'function') { state.calendar.calendar.updateSize(); }
    }

    function renderMode() {
        const planOn = state.mode === 'plan';
        const exec = el('vw-mode-execute'), planBtn = el('vw-mode-plan');
        if (exec) { exec.classList.toggle('active', !planOn); exec.setAttribute('aria-pressed', String(!planOn)); }
        if (planBtn) {
            planBtn.classList.toggle('active', planOn); planBtn.setAttribute('aria-pressed', String(planOn));
            planBtn.classList.toggle('d-none', !perms.plan || !TC);
        }
        const panel = el('vw-plan-panel');
        if (panel) { panel.classList.toggle('d-none', !(planOn && state.layout === 'grid')); }
        const main = el('vw-main-col');
        if (main) { main.className = planOn && state.layout === 'grid' ? 'col-12 col-md-8' : 'col-12'; }
        if (planOn) { loadPlan(false); }
    }

    function loadPlan(force) {
        const w = weekOf(state.week);
        const sid = w && w.planningSessionId ? w.planningSessionId : null;
        if (!force && sid === plan.sessionId && plan.week === state.week && (plan.session || !sid)) { renderPanel(); return Promise.resolve(); }
        plan.sessionId = sid; plan.week = state.week; plan.rows = {}; plan.counts = {};
        if (!sid) { plan.session = null; plan.accounts = []; plan.accountId = null; renderPanel(); return Promise.resolve(); }
        return Promise.all([
            planCall('GET', '/sessions/' + encodeURIComponent(sid)),
            planCall('GET', '/sessions/' + encodeURIComponent(sid) + '/targets?weekStart=' + encodeURIComponent(state.week)),
            planCall('GET', '/my-accounts?pageSize=50'),
            Object.keys(plan.specLabels).length ? Promise.resolve(null) : planCall('GET', '/reference-labels')
        ]).then(res => {
            if (plan.sessionId !== sid) { return null; }
            const s = res[0], t = res[1], mine = res[2], labels = res[3];
            plan.session = s.ok && s.body && s.body.data ? s.body.data : null;
            if (labels && labels.ok && labels.body && labels.body.data) { plan.specLabels = labels.body.data.specialties || {}; }
            const seen = {};
            plan.accounts = [];
            const add = (id, name) => { if (id && !seen[id]) { seen[id] = true; plan.accounts.push({ id: id, name: name || '—' }); } };
            ((t.ok && t.body && t.body.data && t.body.data.accounts) || []).forEach(a => add(a.accountId, a.accountName));
            ((mine.ok && mine.body && mine.body.data && mine.body.data.items) || []).forEach(a => add(a.accountId || a.id, a.accountName || a.name));
            if (!plan.accountId || !seen[plan.accountId]) { plan.accountId = plan.accounts.length ? plan.accounts[0].id : null; }
            return loadDoctors();
        });
    }

    function loadDoctors() {
        const aid = plan.accountId, sid = plan.sessionId;
        if (!aid || !sid) { renderPanel(); return Promise.resolve(); }
        const read = quick => {
            const key = doctorKey(aid, quick);
            if (plan.rows[key]) { return Promise.resolve(); }
            return planCall('GET', '/my-accounts/' + encodeURIComponent(aid) + '/doctors?planningSessionId=' + encodeURIComponent(sid)
                + '&quick=' + quick + '&pageSize=200&weekStart=' + encodeURIComponent(state.week)).then(r => {
                const d = r.ok && r.body && r.body.data;
                plan.rows[key] = d && Array.isArray(d.items) ? d.items.map(TC.doctorRow) : [];
                plan.counts[key] = d && d.quickCounts && typeof d.quickCounts === 'object' ? d.quickCounts : null;
            });
        };
        return Promise.all([read('all'), read(plan.quick)]).then(renderPanel);
    }

    function renderPanel() {
        if (state.mode !== 'plan' || !el('vw-tg-list')) { return; }
        const w = weekOf(state.week);
        const editable = planEditable();
        setText('vw-tg-title', w ? fmt(L.TargetsTitle || '{0}', weekText(w)) : '');
        const lockText = !w || !w.planningSessionId ? (L.TargetsNoPlan || '')
            : w.state === 'approved' ? fmt(L.PlanReadOnlyWeek || '{0}', weekText(w))
            : w.state === 'past' ? fmt(L.PlanReadOnlyPast || '{0}', weekText(w)) : '';
        const lock = el('vw-tg-locked');
        if (lock) { lock.textContent = lockText; lock.classList.toggle('d-none', !lockText); }
        const sel = el('vw-tg-account');
        if (sel) {
            sel.innerHTML = plan.accounts.map(a => '<option value="' + esc(a.id) + '"' + (a.id === plan.accountId ? ' selected' : '') + '>' + esc(F.isolate(a.name)) + '</option>').join('');
            sel.disabled = !plan.accounts.length;
        }

        const all = plan.rows[doctorKey(plan.accountId, 'all')] || [];
        const visible = plan.rows[doctorKey(plan.accountId, plan.quick)] || [];
        const split = TC.splitPlanFirst(all, visible, inPlan);
        const counts = TC.quickCounts(all, plan.counts[doctorKey(plan.accountId, 'all')], inPlan);
        el('vw-tg-quick').innerHTML = QUICK.map(q => '<button type="button" class="btn btn-sm ' + (q === plan.quick ? 'btn-primary' : 'btn-outline-secondary')
            + ' vw-tg-quick" data-quick="' + q + '" aria-pressed="' + (q === plan.quick) + '">' + esc(L[QUICK_KEY[q]] || q) + ' (' + counts[q] + ')</button>').join('');

        const row = r => {
            const st = r.status || {};
            const can = editable && !r.blocked;
            const freq = st.requiredVisitCount != null && st.frequencyStatus !== 'unknown' ? fmt(L.FrequencyPerPeriod || '{0}', st.requiredVisitCount)
                : (st.frequencyDefault === 'weekly' ? (L.FrequencyDefaultWeekly || '') : '');
            const spec = r.specialty ? (plan.specLabels[r.specialty] || plan.specLabels[String(r.specialty).toLowerCase()] || r.specialty) : '';
            return '<div class="vw-tg-row d-flex gap-2 align-items-start py-2 border-bottom' + (can ? ' vw-draggable' : '') + '" draggable="' + (can ? 'true' : 'false')
                + '" data-cid="' + esc(r.contactId) + '">'
                + '<input class="form-check-input mt-1 vw-tg-check" type="checkbox" data-cid="' + esc(r.contactId) + '"' + (inPlan(r.contactId) ? ' checked' : '')
                + (can ? '' : ' disabled') + ' aria-label="' + esc(r.name) + '">'
                + '<div class="flex-grow-1" style="min-width:0"><div class="fw-medium">' + F.bidi(r.name) + '</div>'
                + '<div class="small text-muted d-flex flex-wrap gap-2">' + (spec ? '<span>' + F.bidi(spec) + '</span>' : '') + (freq ? '<span>' + esc(freq) + '</span>' : '') + '</div>'
                + '<div class="d-flex flex-wrap gap-1 mt-1">' + (st.dueThisWeek ? '<span class="badge bg-label-primary">' + esc(L.DueThisWeek || '') + '</span>' : '')
                + (st.segmentBadges || []).map(n => '<span class="badge bg-label-info">' + F.bidi(n) + '</span>').join('') + '</div></div>'
                + (can ? '<i class="bx bx-grid-vertical text-muted" aria-hidden="true"></i>' : '') + '</div>';
        };
        const head = t => '<div class="small text-muted text-uppercase fw-semibold mt-2">' + esc(t) + '</div>';
        el('vw-tg-list').innerHTML = (split.plan.length
            ? head(fmt(L.PlanDoctorsHeading || '{0}', split.plan.length)) + split.plan.map(row).join('') + (split.others.length ? head(L.OtherDoctorsHeading || '') : '')
            : '') + split.others.map(row).join('')
            + (split.plan.length + split.others.length ? '' : '<div class="text-muted small p-2">' + esc(plan.accountId ? (L.NoDoctors || '') : (L.TargetsNoPlan || '')) + '</div>');

        const selectable = split.others.filter(r => !r.blocked);
        const selAll = el('vw-tg-select-all');
        if (selAll) { selAll.textContent = fmt(L.SelectAll || '{0}', selectable.length); selAll.disabled = !editable || !selectable.length; }
        const apply = el('vw-tg-apply');
        if (apply) { apply.textContent = fmt(L.ApplyProducts || '{0}', split.plan.length); apply.disabled = !editable || !split.plan.length; }
        const s = plan.session || {};
        setText('vw-tg-summary', fmt(L.SelectionSummary || '{0} · {1} · {2}', (s.selectedContacts || []).length, (s.selectedPharmacyIds || []).length, (s.selectedAccountIds || []).length));
    }

    /** The EXISTING session update; on success the panel and the calendar read again (the engine re-placed the week). */
    function writePlan(body, okText) {
        if (!plan.sessionId) { return Promise.resolve(false); }
        return planCall('PUT', '/sessions/' + encodeURIComponent(plan.sessionId), body).then(r => {
            if (!r.ok) { toast(C.pinText(r, L), 'error'); return false; }
            toast(okText || L.TargetsSaved || '');
            return loadPlan(true).then(() => load(true)).then(() => true);
        });
    }

    const doctorOf = cid => {
        const r = (plan.rows[doctorKey(plan.accountId, 'all')] || []).find(x => x.contactId === cid);
        return { contactId: cid, accountId: plan.accountId, accountContactLinkId: r ? r.linkId : null };
    };

    function toggleDoctor(cid, on) {
        if (!planEditable() || !plan.session) { return; }
        writePlan(TC.selectionUpdate(plan.session, savedContacts(), [{ doctor: doctorOf(cid), remove: !on }]));
    }

    function selectAllDoctors() {
        if (!planEditable() || !plan.session) { return; }
        const visible = plan.rows[doctorKey(plan.accountId, plan.quick)] || [];
        const add = TC.splitPlanFirst([], visible, inPlan).others.filter(r => !r.blocked).map(r => ({ doctor: doctorOf(r.contactId) }));
        if (add.length) { writePlan(TC.selectionUpdate(plan.session, savedContacts(), add)); }
    }

    /** A doctor (or a draft visit) pinned to a day / time of a DRAFT week; a doctor not in the plan joins it. */
    function pinTo(target, accountId, drop, week) {
        const pin = C.pinFromDrop(drop);
        if (!pin || !TC || !perms.plan || !plan.session || !C.canPlanWeek(week, state.mode)) { return Promise.resolve(false); }
        const joins = target.targetType === 'contact' && !savedContacts().some(c => c.contactId === target.contactId);
        const body = joins
            ? TC.selectionUpdate(plan.session, savedContacts(), [{ doctor: { contactId: target.contactId, accountId: accountId || null, accountContactLinkId: null } }])
            : { expectedVersion: plan.session.version };
        body.dayPins = { weekStart: week.weekStart, pins: C.pinsWith(weekPins(week.weekStart), target, pin) };
        return writePlan(body, L.PinSaved || '').then(ok => {
            if (!ok || !pin.startTime) { return ok; }
            const v = (state.data ? state.data.visits : []).find(x => x.weekStart === week.weekStart && x.targetId === target.targetId && x.pinnedTime);
            const code = C.pinMoveCode(v);
            if (code) { toast(L[C.pinKey(code)] || '', 'warning'); }
            return ok;
        });
    }

    function planDrop(info) {
        const item = C.parseDragItem(info && info.itemId);
        const day = info && (info.date || String(info.startUtc || '').slice(0, 10));
        const week = day ? weekOf(C.mondayOf(day)) : null;
        if (!item || !week || week.planningSessionId !== plan.sessionId) { return; }
        pinTo({ targetType: 'contact', targetId: item.contactId, contactId: item.contactId }, item.accountId, info, week);
    }

    function planMove(info) {
        const v = (state.byId || {})[info.id];
        const pin = C.pinFromDrop(info);
        const week = v ? weekOf(v.weekStart) : null;
        if (!v || v.workStatus !== 'draft' || !pin || !C.canPlanWeek(week, state.mode) || C.mondayOf(pin.date) !== v.weekStart) {
            if (info.revert) { info.revert(); }
            return;
        }
        pinTo({ targetType: v.targetType, targetId: v.targetId, contactId: v.contactId || null }, v.accountId, info, week)
            .then(ok => { if (!ok && info.revert) { info.revert(); } });
    }

    // ── apply products: the first is the promo, the others reminders (the shared rule); ADDED to each plan doctor ──
    let productTimer = null;
    function openProducts() {
        if (!planEditable()) { return; }
        plan.picked = []; plan.results = [];
        el('vw-prod-search').value = '';
        renderProducts();
        const m = el('vw-products-modal');
        if (m && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(m).show(); }
    }

    function searchProducts() {
        const q = el('vw-prod-search').value.trim();
        clearTimeout(productTimer);
        productTimer = setTimeout(() => planCall('GET', '/products?search=' + encodeURIComponent(q) + '&pageSize=20').then(r => {
            plan.results = r.ok && r.body && Array.isArray(r.body.options) ? r.body.options : [];
            renderProducts();
        }), 250);
    }

    function renderProducts() {
        const picked = TC.rolesByOrder(plan.picked);
        el('vw-prod-picked').innerHTML = picked.length
            ? picked.map((p, i) => '<li class="d-flex justify-content-between align-items-center gap-2 py-1"><span>' + F.bidi(C.productLabel(p)) + '</span>'
                + '<span class="d-flex gap-1 align-items-center"><span class="badge ' + (i === 0 ? 'bg-label-primary' : 'bg-label-secondary') + '">' + esc(L[i === 0 ? 'RolePromo' : 'RoleReminder'] || '') + '</span>'
                + '<button type="button" class="btn btn-sm btn-icon btn-text-secondary vw-prod-remove" data-pid="' + esc(p.productId) + '" aria-label="' + esc(L.RemoveProduct || '') + '"><i class="bx bx-x"></i></button></span></li>').join('')
            : '<li class="text-muted small">' + esc(L.NoProductPicked || '') + '</li>';
        el('vw-prod-results').innerHTML = plan.results.map(p => '<button type="button" class="list-group-item list-group-item-action vw-prod-add" data-pid="' + esc(p.productId) + '"'
            + (plan.picked.some(x => x.productId === p.productId) ? ' disabled' : '') + '>' + F.bidi(C.productLabel(p)) + '</button>').join('');
        el('vw-prod-apply').disabled = !plan.picked.length;
    }

    function applyProducts() {
        const targets = TC.splitPlanFirst(plan.rows[doctorKey(plan.accountId, 'all')] || [], [], inPlan).plan;
        if (!targets.length || !plan.picked.length || !plan.session) { return; }
        const chosen = TC.rolesByOrder(plan.picked);
        const changes = targets.map(r => {
            const saved = savedContacts().find(c => c.contactId === r.contactId);
            return { doctor: doctorOf(r.contactId), products: TC.unionPicks(saved && saved.products, chosen) };
        });
        writePlan(TC.selectionUpdate(plan.session, savedContacts(), changes), L.ProductsApplied || '').then(ok => {
            const m = el('vw-products-modal');
            if (ok && m && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(m).hide(); }
        });
    }

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────
    root.addEventListener('click', e => {
        const week = e.target.closest('.vw-strip-week');
        if (week) { state.week = week.getAttribute('data-week'); load(false); return; }
        const day = e.target.closest('.vw-list-day');
        if (day) { state.day = day.getAttribute('data-day'); renderDayList(); return; }
        const card = e.target.closest('.vw-list-card');
        if (card) { openDetail(state.byId[card.getAttribute('data-key')]); }
    });
    document.addEventListener('click', e => {
        const d = e.target.closest('[data-dialog]');
        if (d) { openDialog(d.getAttribute('data-dialog')); return; }
        const opt = e.target.closest('.vw-day-option');
        if (opt) {
            dlg.date = opt.getAttribute('data-date');
            document.querySelectorAll('.vw-day-option').forEach(b => b.classList.toggle('active', b === opt));
            syncDialog(); return;
        }
        const c = e.target.closest('[data-contact]');
        if (c) {
            unp.contactId = c.getAttribute('data-contact');
            document.querySelectorAll('#vw-unp-results [data-contact]').forEach(b => b.classList.toggle('active', b === c));
            el('vw-unp-save').disabled = false;
        }
    });
    document.addEventListener('change', e => {
        if (e.target && e.target.name === 'vw-reason') {
            dlg.reason = dlg.reasons.find(x => x.code === e.target.value) || null;
            syncDialog();
        }
    });

    const on = (id, ev, fn) => { const n = el(id); if (n) { n.addEventListener(ev, fn); } };
    on('vw-dlg-note', 'input', syncDialog);
    on('vw-dlg-save', 'click', saveDialog);
    on('vw-reopen-reason', 'input', syncReopen);
    on('vw-reopen-confirm', 'click', confirmReopen);
    on('vw-reopen', 'click', openReopen);
    on('vw-approve', 'click', approveWeek);
    on('vw-unplaced', 'click', showUnplaced);
    on('vw-unplanned-open', 'click', openUnplanned);
    on('vw-unp-search', 'input', searchContacts);
    on('vw-unp-save', 'click', saveUnplanned);
    on('vw-prev', 'click', () => { state.week = C.addDays(state.week, -7); load(false); });
    on('vw-next', 'click', () => { state.week = C.addDays(state.week, 7); load(false); });
    on('vw-today', 'click', () => { state.week = C.mondayOf(todayYmd()); state.day = todayYmd(); load(false); });

    // WP-VW-W2 (WEB-b) — Plan mode
    on('vw-mode-execute', 'click', () => setMode('execute'));
    on('vw-mode-plan', 'click', () => setMode('plan'));
    on('vw-tg-account', 'change', e => { plan.accountId = e.target.value; loadDoctors(); });
    on('vw-tg-select-all', 'click', selectAllDoctors);
    on('vw-tg-apply', 'click', openProducts);
    on('vw-prod-search', 'input', searchProducts);
    on('vw-prod-apply', 'click', applyProducts);
    const panelNode = el('vw-plan-panel');
    if (panelNode) {
        panelNode.addEventListener('click', e => {
            const q = e.target.closest('.vw-tg-quick');
            if (q) { plan.quick = q.getAttribute('data-quick'); loadDoctors(); }
        });
        panelNode.addEventListener('change', e => {
            const cb = e.target.closest('.vw-tg-check');
            if (cb) { toggleDoctor(cb.getAttribute('data-cid'), cb.checked); }
        });
        panelNode.addEventListener('dragstart', e => {
            const row = e.target.closest('.vw-tg-row');
            if (!row || row.getAttribute('draggable') !== 'true' || !planEditable() || !window.DitenCalendar) { return; }
            e.dataTransfer.setData(window.DitenCalendar.DRAG_TYPE, C.dragItem(row.getAttribute('data-cid'), plan.accountId));
            e.dataTransfer.effectAllowed = 'copyMove';
        });
    }
    const productsNode = el('vw-products-modal');
    if (productsNode) {
        productsNode.addEventListener('click', e => {
            const addBtn = e.target.closest('.vw-prod-add');
            if (addBtn) {
                const p = plan.results.find(x => x.productId === addBtn.getAttribute('data-pid'));
                if (p && !plan.picked.some(x => x.productId === p.productId)) { plan.picked.push(p); renderProducts(); }
                return;
            }
            const rm = e.target.closest('.vw-prod-remove');
            if (rm) { plan.picked = plan.picked.filter(x => x.productId !== rm.getAttribute('data-pid')); renderProducts(); }
        });
    }

    const persist = () => { C.saveFilters(safeStorage(), state.filters); render(); };
    on('vw-filter-account', 'change', e => { state.filters.accountId = e.target.value; persist(); });
    on('vw-filter-product', 'change', e => { state.filters.productId = e.target.value; persist(); });
    on('vw-filter-status', 'change', () => {
        state.filters.statuses = Array.from(document.querySelectorAll('#vw-filter-status input:checked')).map(i => i.value);
        persist();
    });

    window.addEventListener('resize', () => {
        const next = C.layoutFor(window.innerWidth);
        if (next !== state.layout) { state.layout = next; render(); }
    });

    get('/contract').then(r => { if (r.ok && r.body && r.body.data) { state.contract = r.body.data; } });
    load(true);
}(window, document));
