/**
 * WP-VW-W2 (WEB-a) — the Visit Workspace page (Execute mode): the week strip, the week header, the calendar (week /
 * day time grid on a desktop, the day list on a phone), the cards, the filters, the detail panel and the cancel /
 * not done / reschedule dialogs. Every rule lives in workspace-core.js (pure, tested in Node); every text comes from
 * window.VisitWorkspaceL10n (7 languages) or from the CRM (reason labels from the reference set, names, products).
 * Dates / numbers in the APPLICATION language through VisitPlanningFormat; every data name isolated (bidi) for RTL.
 * Calls go to the same-origin proxy /CRM/VisitWorkspace/api/*.
 */
(function (window, document) {
    'use strict';

    const C = window.VisitWorkspaceCore;
    const F = window.VisitPlanningFormat;
    const L = window.VisitWorkspaceL10n || {};
    const root = document.getElementById('vw-root');
    if (!C || !F || !root) { return; }

    const base = root.getAttribute('data-api-base') || '/CRM/VisitWorkspace/api';
    const planningUrl = root.getAttribute('data-planning-url') || '/CRM/VisitPlanning';
    const executionUrl = root.getAttribute('data-execution-url') || '/CRM/VisitExecution';
    const perms = {
        manage: root.getAttribute('data-can-manage') === 'true',
        record: root.getAttribute('data-can-record') === 'true',
        apply: root.getAttribute('data-can-apply') === 'true',
        contacts: root.getAttribute('data-can-search-contacts') === 'true'
    };

    // ── tiny helpers ─────────────────────────────────────────────────────────────────────────────────────────
    const el = id => document.getElementById(id);
    const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const lang = () => F.culture().slice(0, 2).toLowerCase();
    const todayYmd = () => new Date().toISOString().slice(0, 10);
    const toast = (msg, type) => { if (window.showToast) { window.showToast(msg, type || 'success'); } };
    const hours = min => F.hours(min, L.HoursShort || '{0} h');

    const call = (method, path, body) => fetch(base + path, {
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
        current: null
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
        renderStrip();
        renderHeader();
        renderFilters();
        if (state.layout === 'list') { renderDayList(); } else { renderCalendar(); }
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
        const accounts = {}, products = {};
        visits.forEach(v => {
            if (v.accountId) { accounts[v.accountId] = accounts[v.accountId] || v.targetDisplayName || ''; }
            (v.plannedContent || []).forEach(c => { products[c.productId] = C.productLabel(c); });
        });
        fillSelect('vw-filter-account', accounts, L.FilterAllAccounts, state.filters.accountId);
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
        const events = visits.map(C.eventOf);
        const days = daysOf(state.week).map(d => ({ date: d.date, dayKind: d.isHoliday ? 'holiday' : (d.kind === 'weekend' ? 'weekend' : 'working'), holidayName: d.holidayName || L.HolidayLabel || '' }));

        if (!state.calendar && window.DitenCalendar) {
            state.calendar = window.DitenCalendar.create(host, {
                zone: 'UTC', view: 'week', date: state.week, editable: false, events: events, days: [],
                renderExtras: ev => { const v = state.byId[ev.id]; return v ? cardHtml(v) : ''; },
                onEventClick: id => openDetail(state.byId[id]),
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

    function approveWeek() {
        const w = weekOf(state.week); if (!w || !w.planningSessionId) { return; }
        const btn = el('vw-approve'); btn.disabled = true;
        withSession(w).then(s => {
            if (!s) { btn.disabled = false; toast(L.ActionFailed || '', 'error'); return null; }
            return post('/apply', { planningSessionId: w.planningSessionId, expectedVersion: s.version, weekStart: w.weekStart });
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
        withSession(w).then(s => {
            if (!s) { btn.disabled = false; toast(L.ActionFailed || '', 'error'); return null; }
            return post('/sessions/' + encodeURIComponent(w.planningSessionId) + '/weeks/' + encodeURIComponent(w.weekStart) + '/reopen',
                { reason: reason, expectedVersion: s.version });
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
