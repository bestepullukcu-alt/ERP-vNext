/**
 * WP-VW-W2 (WEB-a) — the Visit Workspace page (Execute mode): the week header, the calendar (week / day time grid on a
 * desktop, the day list on a phone), the cards, the filters, the detail panel and the cancel / not done / reschedule
 * dialog. Every rule lives in workspace-core.js (pure, tested in Node); every text comes from window.VisitWorkspaceL10n
 * (7 languages) or from the CRM (reason labels from the reference set, names, products). Dates / numbers in the
 * APPLICATION language through VisitPlanningFormat; every data name isolated (bidi) for RTL. Calls go to the
 * same-origin proxy /CRM/VisitWorkspace/api/*.
 * WP-VW-W2 (WEB-b) — Plan mode: the Targets panel (the Visit Planning rules, targets-core.js, and its proxies under
 * /CRM/VisitPlanning/api), a doctor dragged onto a day / time and a draft card moved there = a day pin with a start time
 * (the existing session update), apply products, the institution filter (accountDisplayName) and the unplaced list.
 * WP-VW-W2 (WEB-c) — the mockup's look: no week strip; ONE calendar card (‹ › Today + the range, the filters, Day /
 * Week / Month; FullCalendar's own toolbar hidden), the week inside it, the cards / day heads / month cells / legend
 * of the mockup, the detail panel's blocks, ONE E2 dialog with three tabs and the Targets panel's cards. Behaviour
 * (data, requests, the two-step save, the drag rules, the permissions) is unchanged.
 * WP-VW-W3 (WEB-a) — start the visit: the in_progress card (pulse dot + "N min"), the detail panel's Start / Back to
 * the presentation / Finish (/CRM/VisitPresentation), and E3 — the evidence dialog (photo: file or webcam, JPEG at most
 * 1600 px, quality 0.85; the browser position; the server's start time) whose Start follows the tenant setting
 * (contract.policy) and posts multipart to /api/execution/start. The distance is the SERVER's; this page only shows it.
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
        pharmacies: root.getAttribute('data-can-search-pharmacies') === 'true', // W4-FIX-c (G-3) — crm.account.read
        plan: root.getAttribute('data-can-plan') === 'true', // the Visit Planning read + generate keys
        execute: root.getAttribute('data-can-execute') === 'true' // WP-VW-W3 — crm.visit-execution.read + record
    };
    const presentationBase = root.getAttribute('data-presentation-url') || '/CRM/VisitPresentation';
    const reportBase = root.getAttribute('data-report-url') || '/CRM/VisitReport'; // WP-VW-W4 (W4-WEB-a)

    // ── tiny helpers ─────────────────────────────────────────────────────────────────────────────────────────
    const el = id => document.getElementById(id);
    const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const lang = () => F.culture().slice(0, 2).toLowerCase();
    const todayYmd = () => new Date().toISOString().slice(0, 10);
    const toast = (msg, type) => { if (window.showToast) { window.showToast(msg, type || 'success'); } };
    const hours = min => F.hours(min, L.HoursShort || '{0} h');
    const setText = (id, t) => { const n = el(id); if (n) { n.textContent = t; } };
    const showModal = id => { const m = el(id); if (m && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(m).show(); } };
    const hideModal = id => { const m = el(id); if (m && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(m).hide(); } };

    const asResult = r => r.text().then(t => {
        let b = null;
        try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; }
        return { ok: r.ok, status: r.status, body: b };
    });
    const call = (method, path, body, prefix) => fetch((prefix || base) + path, {
        method: method,
        credentials: 'same-origin',
        headers: Object.assign({ Accept: 'application/json' }, body ? { 'Content-Type': 'application/json' } : {}),
        body: body ? JSON.stringify(body) : undefined
    }).then(asResult).catch(() => ({ ok: false, status: 0, body: null }));
    // WP-VW-W3 — a multipart POST (the browser sets the boundary; no Content-Type of ours)
    const upload = (path, form) => fetch(base + path, { method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }, body: form })
        .then(asResult).catch(() => ({ ok: false, status: 0, body: null }));
    const get = path => call('GET', path);
    const post = (path, body) => call('POST', path, body);

    // ── state ────────────────────────────────────────────────────────────────────────────────────────────────
    const state = {
        week: C.mondayOf(todayYmd()),
        day: todayYmd(),
        view: 'week',   // WP-VW-W2 (WEB-c) — day | week | month (the card head's switch)
        range: null,    // the calendar's visible range { from, to } (the month view reads it)
        window: null,
        data: null,
        contract: null,
        execContract: null, // WP-VW-W3 — the visit execution contract (policy = the tenant's evidence setting)
        filters: C.loadFilters(safeStorage()),
        layout: C.layoutFor(window.innerWidth),
        calendar: null,
        current: null,
        mode: 'execute' // WP-VW-W2 (WEB-b) — 'plan' shows the Targets panel and edits a draft week
    };

    function safeStorage() { try { return window.localStorage; } catch (e) { return null; } }

    // ── loading ──────────────────────────────────────────────────────────────────────────────────────────────
    // The read window: the week view reads two weeks around (C.weekWindow); the month view reads its visible grid.
    const needed = () => (state.view === 'month' && state.range ? { from: state.range.from, to: state.range.to } : C.weekWindow(state.week));
    const covers = n => !!state.window && n.from >= state.window.from && n.to <= state.window.to;

    function load(force) {
        const want = needed();
        if (!force && state.data && covers(want)) { render(); return Promise.resolve(); }
        state.window = want;
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
        renderHeader();
        renderFilters();
        renderLegend();
        if (state.layout === 'list') { renderDayList(); } else { renderCalendar(); }
        renderPanel();
    }

    const STATE_KEY = { draft: 'WeekStateDraft', approved: 'WeekStateApproved', past: 'WeekStatePast', none: 'WeekStateNone' };
    const VIEWS = { day: 'timeGridDay', week: 'timeGridWeek', month: 'dayGridMonth' };
    const yearOf = ymd => String(ymd).slice(0, 4);

    /** The card head's range: "5 Eki – 9 Eki 2026" (week), "Per 8 Eki 2026" (day), "Ekim 2026" (month). */
    function rangeTitle() {
        if (state.view === 'month') {
            const mid = state.range ? C.addDays(state.range.from, 15) : state.week;
            try {
                return new Intl.DateTimeFormat(F.dateCulture(), { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(mid + 'T12:00:00Z'));
            } catch (e) { return mid.slice(0, 7); }
        }
        if (state.view === 'day') { return F.dayLabel(state.day) + ' ' + yearOf(state.day); }
        const friday = C.addDays(state.week, 4);
        return F.dayMonth(state.week) + ' – ' + F.dayMonth(friday) + ' ' + yearOf(friday);
    }

    function renderHeader() {
        // T1 — the period chip (W2-BE-d periodName; hidden without it)
        const period = el('vw-period-chip');
        if (period) {
            const name = state.data && state.data.periodName;
            period.innerHTML = name ? F.bidi(name) : '';
            period.classList.toggle('d-none', !name);
        }
        setText('vw-range-title', rangeTitle());
        document.querySelectorAll('#vw-views [data-view]').forEach(b => {
            const on = b.getAttribute('data-view') === state.view;
            b.classList.toggle('active', on);
            b.setAttribute('aria-pressed', String(on));
        });
        // T2 / L1 — the unplanned visit in BOTH modes (it is always today's)
        const unplanned = el('vw-unplanned-open');
        if (unplanned) { unplanned.classList.toggle('d-none', !perms.manage); }

        const w = weekOf(state.week);
        const head = el('vw-week-head');
        if (head) { head.classList.toggle('d-none', !w || state.view === 'month'); }
        if (!w) { setText('vw-week-title', ''); return; }
        // T4 / L3 — "42. Hafta · 12–16 Eki", the state chip, the capacity line + bar, did-not-fit, approve / reopen
        setText('vw-week-title', fmt(L.WeekLabel || '{0}', w.weekNumber) + ' · ' + F.workRange(w.weekStart, C.addDays(w.weekStart, 4)));
        const badge = el('vw-week-badge');
        if (badge) {
            badge.className = 'vw-state-chip vw-wst-' + w.state;
            badge.textContent = L[STATE_KEY[w.state]] || w.state;
        }
        const cap = C.capacity(w.plannedMinutes, w.capacityMinutes);
        const bar = el('vw-capacity-bar');
        if (bar) {
            bar.style.width = cap.pct + '%';
            bar.className = cap.over ? 'vw-over' : '';
            bar.setAttribute('aria-valuenow', String(cap.raw));
        }
        setText('vw-capacity-text', F.isolateRatios(fmt(L.CapacityWeekLine || '{0} · {1}', hours(w.capacityMinutes), hours(w.plannedMinutes))));
        const unplaced = el('vw-unplaced');
        if (unplaced) {
            unplaced.classList.toggle('d-none', !(w.unplacedCount > 0));
            unplaced.textContent = fmt(L.Unplaced || '{0}', w.unplacedCount);
        }
        const approve = el('vw-approve'), reopen = el('vw-reopen');
        if (approve) { approve.classList.toggle('d-none', !(perms.apply && w.canApprove)); }
        if (reopen) { reopen.classList.toggle('d-none', !(perms.apply && w.canReopen)); }
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
        // T8 — the status filter stays MULTI (user decision), labelled "All statuses" / "N statuses"
        const box = el('vw-filter-status');
        if (box && !box.dataset.ready) {
            box.innerHTML = C.STATUSES.map(s => '<label class="dropdown-item d-flex gap-2 align-items-center"><input type="checkbox" class="form-check-input m-0" value="' + s + '"'
                + (state.filters.statuses.indexOf(s) > -1 ? ' checked' : '') + '> <span class="vw-swatch vw-st-' + s + '"></span><span>' + esc(L['Status_' + s] || s) + '</span></label>').join('');
            box.dataset.ready = '1';
        }
        const n = state.filters.statuses.length;
        setText('vw-filter-status-label', n ? fmt(L.FilterStatusCount || '{0}', n) : (L.FilterAllStatuses || ''));
    }

    function fillSelect(id, map, allLabel, selected) {
        const s = el(id); if (!s) { return; }
        const entries = Object.keys(map).map(k => [k, map[k]]).sort((a, b) => String(a[1]).localeCompare(String(b[1]), F.culture()));
        s.innerHTML = '<option value="">' + esc(allLabel || '') + '</option>'
            + entries.map(e => '<option value="' + esc(e[0]) + '"' + (e[0] === selected ? ' selected' : '') + '>' + esc(F.isolate(e[1])) + '</option>').join('');
    }

    // T10 / L9 — the legend under the calendar: the status colours, pinned, unplanned, the product roles
    function renderLegend() {
        const box = el('vw-legend');
        if (!box || box.dataset.ready) { return; }
        box.innerHTML = C.LEGEND.map(code => '<span class="vw-leg"><span class="vw-swatch vw-st-' + code + (code === 'draft' ? ' vw-dashed' : '') + '"></span>' + esc(L['Status_' + code] || code) + '</span>').join('')
            + '<span class="vw-leg"><i class="bx bxs-pin"></i>' + esc(L.LegendPinned || '') + '</span>'
            + '<span class="vw-leg"><i class="bx bx-user-plus"></i>' + esc(L.LegendUnplanned || '') + '</span>'
            + '<span class="vw-leg"><span class="vw-pchip vw-pchip-promo">' + esc(L.LegendProduct || '') + '</span>' + esc(L.RolePromo || '') + '</span>'
            + '<span class="vw-leg"><span class="vw-pchip vw-pchip-reminder">' + esc(L.LegendProduct || '') + '</span>' + esc(L.RoleReminder || '') + '</span>';
        box.dataset.ready = '1';
    }

    // ── the card (T7 / L4) ───────────────────────────────────────────────────────────────────────────────────
    const bottomText = b => (b ? F.isolateRatios(fmt(L[b.key] || '', b.date ? F.dayMonth(b.args[0]) : b.args[0])) : '');

    function cardHtml(v) {
        const m = C.cardModel(v, { nowMs: Date.now(), narrow: state.mode === 'plan', visits: state.data ? state.data.visits : [] });
        const st = m.status;
        const icons = (m.pinned ? '<i class="bx bxs-pin" title="' + esc(L.Icon_Pinned || '') + (v.pinnedTime ? ' ' + esc(v.pinnedTime) : '') + '"></i>' : '')
            + (m.unplanned ? '<i class="bx bx-user-plus" title="' + esc(L.Icon_Unplanned || '') + '"></i>' : '')
            + (m.rescheduledFrom ? '<i class="bx bx-calendar-edit" title="' + esc(L.Icon_Rescheduled || '') + '"></i>' : '')
            + (m.live ? '<span class="vw-live" aria-hidden="true"></span>' : '') // WP-VW-W3 — in_progress: the pulse dot
            + '<i class="bx ' + m.icon + ' vw-st-icon" title="' + esc(L[st.labelKey] || '') + '"></i>';
        const chips = m.chips.map(c => '<span class="vw-pchip ' + (c.promo ? 'vw-pchip-promo' : 'vw-pchip-reminder') + '">' + F.bidi(c.name) + '</span>').join('');
        return '<div class="vw-card ' + st.cssClass + (st.dashed ? ' vw-dashed' : '') + (st.strike ? ' vw-strike' : '') + (m.compact ? ' vw-compact' : '') + (m.tiny ? ' vw-tiny' : '') + '" title="' + esc(m.time) + '">'
            + '<div class="vw-card-top"><span class="vw-time">' + esc(m.tiny ? m.shortTime : m.time) + '</span><span class="vw-icons">' + icons + '</span></div>'
            + '<div class="vw-name">' + F.bidi(v.targetDisplayName || '—') + '</div>'
            + (m.account ? '<div class="vw-acc">' + F.bidi(m.account) + '</div>' : '')
            + (chips ? '<div class="vw-chips">' + chips + '</div>' : '')
            + (m.bottom ? '<div class="vw-sub vw-countdown">' + esc(bottomText(m.bottom)) + '</div>' : '')
            + '</div>';
    }

    // T9 — a month cell: the day's visit count and one dot per status present; a click opens that day
    function monthHtml(id) {
        const cell = (state.monthCells || {})[id];
        if (!cell) { return ''; }
        return '<div class="vw-month-cell"><span class="vw-month-count">' + esc(fmt(L.MonthVisits || '{0}', cell.count)) + '</span>'
            + '<span class="vw-dots">' + cell.dots.map(code => '<span class="vw-dot vw-st-' + code + '" title="' + esc(L['Status_' + code] || code) + '"></span>').join('') + '</span></div>';
    }

    // ── desktop: the time grid (DitenCalendar over the vendored FullCalendar) ─────────────────────────────────
    const isMonthId = id => String(id || '').indexOf('m:') === 0;

    function renderCalendar() {
        const host = el('vw-calendar'), list = el('vw-day-list');
        if (list) { list.classList.add('d-none'); }
        if (!host) { return; }
        host.classList.remove('d-none');
        // WP-VW-W2 (WEB-b) — Plan mode on a draft week: the draft cards move; nothing else does (the month view: nothing)
        const editable = state.view !== 'month' && planEditable();
        let visits, events, dayFacts;
        if (state.view === 'month') {
            visits = C.filterVisits(state.data ? state.data.visits : [], state.filters);
            const dates = Array.from(new Set(visits.map(v => v.plannedDate))).sort();
            state.monthCells = {};
            dates.forEach(d => { state.monthCells['m:' + d] = C.monthCell(visits, d); });
            events = dates.map(d => ({ id: 'm:' + d, title: '', kind: 'month', allDay: true, date: d, classNames: ['vw-month-event'] }));
            dayFacts = state.data ? state.data.days : [];
        } else {
            visits = visitsOf(state.week);
            // T6 — no all-day row: a visit without a time sits at the day's first slot (marked), never hidden
            events = visits.map(v => {
                const e = C.eventOf(v, editable && v.workStatus === 'draft');
                if (e.allDay) {
                    e.allDay = false;
                    e.startUtc = v.plannedDate + 'T08:30:00Z';
                    e.endUtc = v.plannedDate + 'T09:00:00Z';
                    e.classNames = e.classNames.concat(['vw-untimed']);
                }
                return e;
            });
            dayFacts = daysOf(state.week);
        }
        const byId = {};
        visits.forEach(v => { byId[C.visitKey(v)] = v; });
        const days = dayFacts.map(d => ({ date: d.date, dayKind: d.isHoliday ? 'holiday' : (d.kind === 'weekend' ? 'weekend' : 'working'), holidayName: d.holidayName || L.HolidayLabel || '' }));
        // CT (live E4) — the lookup is set BEFORE DitenCalendar.create: FullCalendar draws the first events inside create
        // (eventContent → renderExtras), and a missing lookup threw there, so the grid never rendered.
        state.byId = byId;

        if (!state.calendar && window.DitenCalendar) {
            state.calendar = window.DitenCalendar.create(host, {
                zone: 'UTC', view: state.view, date: state.week, editable: editable, events: events, days: [],
                onExternalDrop: planDrop, // a Targets row dropped on a day / time
                onEventMove: planMove,    // a draft card moved to another day / time
                renderExtras: ev => (isMonthId(ev.id) ? monthHtml(ev.id) : ((state.byId || {})[ev.id] ? cardHtml(state.byId[ev.id]) : '')),
                onEventClick: id => {
                    if (isMonthId(id)) { gotoDay(String(id).slice(2)); return; }
                    openDetail((state.byId || {})[id]);
                },
                onRangeChange: rangeChanged
            });
            if (state.calendar) {
                const fc = state.calendar.calendar;
                fc.setOption('weekends', false);
                fc.setOption('headerToolbar', false); // T3 — the card head is ours (prev / next / today / the view switch)
                fc.setOption('allDaySlot', false);    // T6 — no all-day row
                fc.setOption('slotMinTime', '08:30:00'); // T6 — the hour labels at :30 (the mockup's grid)
                fc.setOption('slotMaxTime', '18:30:00');
                fc.setOption('scrollTime', '08:30:00');
                fc.setOption('slotLabelFormat', { hour: '2-digit', minute: '2-digit', hour12: false });
                fc.setOption('dayHeaderContent', arg => {
                    const day = arg.date.toISOString().slice(0, 10);
                    return { html: arg.view && arg.view.type === VIEWS.month ? '<span class="vw-month-dow">' + esc(F.dayShort(day)) + '</span>' : dayHeaderHtml(day) };
                });
            }
        }
        state.byId = byId;
        if (state.calendar) {
            if (typeof state.calendar.setEditable === 'function') { state.calendar.setEditable(editable); }
            state.calendar.setData(events, days);
            if (state.view !== 'month' && (state.calendar.date() < state.week || state.calendar.date() > C.addDays(state.week, 6))) { state.calendar.calendar.gotoDate(state.week); }
        }
        const empty = el('vw-empty');
        if (empty) { empty.classList.toggle('d-none', state.view === 'month' || visits.length > 0 || !!(weekOf(state.week) && weekOf(state.week).state !== 'none')); }
    }

    /** The calendar moved (our prev / next / today / view switch, or a day opened from the month view). */
    function rangeChanged(info) {
        const before = state.view + '|' + state.week;
        state.view = info.view || 'week';
        state.range = { from: info.from, to: info.to };
        if (state.view === 'day') { state.day = info.from; }
        state.week = C.mondayOf(state.view === 'month' ? (info.date || info.from) : info.from);
        if (before !== state.view + '|' + state.week || !covers(needed())) { load(false); } else { renderHeader(); }
    }

    function gotoDay(date) {
        if (state.calendar && state.calendar.calendar) { state.calendar.calendar.changeView(VIEWS.day, date); }
    }

    function switchView(view) {
        if (!VIEWS[view]) { return; }
        if (state.calendar && state.calendar.calendar && state.layout === 'grid') { state.calendar.calendar.changeView(VIEWS[view]); return; }
        state.view = view;
        render();
    }

    function navigate(step) {
        if (state.calendar && state.calendar.calendar && state.layout === 'grid') {
            const fc = state.calendar.calendar;
            if (step === 0) { fc.today(); } else if (step < 0) { fc.prev(); } else { fc.next(); }
            return;
        }
        if (step === 0) { state.week = C.mondayOf(todayYmd()); state.day = todayYmd(); } else { state.week = C.addDays(state.week, 7 * step); }
        load(false);
    }

    // T5 — a day column's head: the day + the Today badge, the holiday, the fill bar and "N visits · X h free"
    function dayHeaderHtml(date) {
        const d = (state.data ? state.data.days : []).find(x => x.date === date);
        const count = (state.data ? state.data.visits : []).filter(v => v.plannedDate === date && v.workStatus !== 'cancelled').length;
        const h = C.dayHead(d || { date: date }, count, todayYmd());
        const top = '<div class="vw-day-head"><div class="vw-day-top"><span class="vw-day-name' + (h.isToday ? ' vw-is-today' : '') + '">' + esc(F.dayLabel(date)) + '</span>'
            + (h.isToday ? '<span class="vw-today-badge">' + esc(L.TodayBadge || '') + '</span>' : '') + '</div>';
        if (!d) { return top + '</div>'; }
        if (h.holiday) { return top + '<span class="vw-holiday badge bg-label-danger">' + esc(d.holidayName || L.HolidayLabel || '') + '</span></div>'; }
        return top + '<div class="vw-day-bar"><span' + (h.over ? ' class="vw-over"' : '') + ' style="width:' + h.pct + '%"></span></div>'
            + '<div class="vw-day-load">' + esc(F.isolateRatios(fmt(L.DayLoad || '{0} · {1}', h.count, hours(h.freeMinutes)))) + '</div></div>';
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

    // ── the detail panel (D1–D6) ─────────────────────────────────────────────────────────────────────────────
    function lengthMinutes(v) {
        const a = /^(\d{1,2}):(\d{2})/.exec(v.startTime || ''), b = /^(\d{1,2}):(\d{2})/.exec(v.endTime || '');
        if (a && b) { return (Number(b[1]) * 60 + Number(b[2])) - (Number(a[1]) * 60 + Number(a[2])); }
        return v.durationMinutes || null;
    }

    function openDetail(v) {
        if (!v) { return; }
        state.current = v;
        const st = C.statusStyle(v.workStatus);
        // D1 — the status chip (+ pinned / unplanned), the name, the specialty LABEL (never its code) and the badges
        el('vw-detail-chips').innerHTML = '<span class="vw-status-chip ' + st.cssClass + (st.dashed ? ' vw-dashed' : '') + '"><i class="bx ' + st.icon + '"></i> ' + esc(L[st.labelKey] || v.workStatus) + '</span>'
            + (v.isPinned ? '<span class="vw-flag"><i class="bx bxs-pin"></i> ' + esc(L.DetailPinned || '') + (v.pinnedTime ? ' ' + esc(v.pinnedTime) : '') + '</span>' : '')
            + (v.source === 'unplanned' ? '<span class="vw-flag"><i class="bx bx-user-plus"></i> ' + esc(L.DetailUnplanned || '') + '</span>' : '');
        el('vw-detail-title').innerHTML = F.bidi(v.targetDisplayName || '—');
        const tags = (v.specialtyLabel ? '<span class="vw-spec-chip">' + F.bidi(v.specialtyLabel) + '</span>' : '')
            + (Array.isArray(v.badges) ? v.badges : []).map(b => '<span class="vw-seg-chip">' + F.bidi(b) + '</span>').join('');
        const tagBox = el('vw-detail-tags');
        tagBox.innerHTML = tags;
        tagBox.classList.toggle('d-none', !tags);

        // D2 — the institution block: the map placeholder, the name, the address, "Sal 6 Eki · 14:00–14:25 · 25 dk"
        el('vw-detail-account').innerHTML = F.bidi(v.accountDisplayName || '—');
        const address = el('vw-detail-address');
        address.innerHTML = v.accountAddress ? F.bidi(v.accountAddress) : '';
        address.classList.toggle('d-none', !v.accountAddress);
        const len = lengthMinutes(v);
        setText('vw-detail-when', [F.dayLabel(v.plannedDate), v.startTime ? v.startTime + (v.endTime ? '–' + v.endTime : '') : '', len ? fmt(L.MinutesShort || '{0}', len) : '']
            .filter(Boolean).join(' · '));

        // D3 — the status box (the mockup's words)
        const a = C.alertFor(v, Date.now(), -new Date().getTimezoneOffset());
        // WP-VW-W3 — a startable visit says what happens next (the evidence step, then the presentation)
        const textKey = a.textKey === 'AlertText_today' && C.canStart(v, perms) ? 'AlertText_today_start' : a.textKey;
        const title = fmt(L[a.titleKey] || L[st.labelKey] || '', ...a.args);
        const text = fmt(L[textKey] || '', ...a.args);
        const band = el('vw-detail-band');
        band.className = 'vw-alert ' + a.cssClass;
        band.innerHTML = '<i class="bx ' + a.icon + '"></i><div class="d-flex flex-column"><strong>' + esc(title) + '</strong>'
            + (text ? '<span>' + esc(text) + '</span>' : '') + (v.cancellationNote ? '<span>' + F.bidi(v.cancellationNote) + '</span>' : '') + '</div>';

        // D4 — what to present: numbered product cards (name + role + the stage · N content steps · ≈ step minutes)
        const items = v.plannedContent || [];
        el('vw-detail-content').innerHTML = items.length
            ? items.map((c, i) => {
                const promo = C.isPromo(c, i);
                const minutes = C.stepMinutes(c);
                const steps = (c.steps || []).length;
                const parts = [c.stageName ? F.bidi(c.stageName) : '', steps ? esc(fmt(L.ContentSteps || '{0}', steps)) : '', minutes != null ? esc(fmt(L.AboutMinutes || '{0}', minutes)) : ''].filter(Boolean);
                return '<div class="vw-prod"><span class="vw-prod-no">' + (i + 1) + '</span><div class="flex-grow-1" style="min-width:0">'
                    + '<div class="d-flex gap-1 align-items-center flex-wrap"><span class="vw-prod-name">' + F.bidi(C.productLabel(c)) + '</span>'
                    + '<span class="vw-role ' + (promo ? 'vw-role-promo' : 'vw-role-reminder') + '">' + esc(L[promo ? 'RolePromo' : 'RoleReminder'] || '') + '</span></div>'
                    + (parts.length ? '<div class="small">' + parts.join(' · ') + '</div>' : '') + '</div></div>';
            }).join('')
            : '<div class="text-muted">' + esc(L.NoContent || '') + '</div>';
        // the duration (user decision): the steps' minutes > the planned length > nothing
        const est = C.estimate(v);
        const estBox = el('vw-detail-estimate');
        const promos = items.filter((c, i) => C.isPromo(c, i)).length;
        const reminders = items.length - promos; // the mockup: no "+ 0 reminder"
        estBox.innerHTML = est ? '<i class="bx bx-stopwatch"></i> ' + esc(reminders > 0 ? fmt(L.EstimateLine || '{2}', promos, reminders, est.minutes) : fmt(L.EstimateLineNoReminder || '{1}', promos, est.minutes)) : '';
        estBox.classList.toggle('d-none', !est);

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

    // D4 — "Sıklık: dönemde N · done / remaining" at the section's head
    function renderFrequency(v) {
        const box = el('vw-detail-frequency');
        box.textContent = '';
        const w = weekOf(v.weekStart);
        if (!w || !w.planningSessionId || !v.contactId) { return; }
        get('/sessions/' + encodeURIComponent(w.planningSessionId) + '/targets?weekStart=' + encodeURIComponent(v.weekStart)).then(r => {
            if (state.current !== v || !r.ok || !r.body || !r.body.data) { return; }
            const doc = (r.body.data.doctors || []).find(d => d.contactId === v.contactId);
            const s = doc && doc.status;
            if (!s || s.requiredVisitCount == null) { return; }
            box.textContent = fmt(L.DetailFrequency || '{0} · {1}', s.requiredVisitCount, F.ratio(s.done || 0, s.remaining != null ? s.remaining : '—'));
        });
    }

    const ACTION = {
        cancel: { label: 'Action_cancel', tone: 'outline-danger', icon: 'bx-block' },
        result: { label: 'Action_result', tone: 'primary', icon: 'bx-play-circle' },
        notDone: { label: 'Action_notDone', tone: 'primary', icon: 'bx-x-circle' },
        reschedule: { label: 'Action_reschedule', tone: 'outline-primary', icon: 'bx-calendar-edit' },
        sendReport: { label: 'Action_sendReport', tone: 'primary', icon: 'bx-edit' },
        plan: { label: 'Action_plan', tone: 'outline-primary', icon: 'bx-edit' },
        // WP-VW-W3 (WEB-a)
        start: { label: 'Action_start', tone: 'primary', icon: 'bx-play-circle' },
        presentation: { label: 'Action_presentation', tone: 'primary', icon: 'bx-slideshow' },
        finish: { label: 'Action_finish', tone: 'outline-primary', icon: 'bx-stop-circle' }
    };

    // D6 — the actions at the panel's foot, full width
    function renderActions(v) {
        const box = el('vw-detail-actions');
        const keys = C.visitActions(v, todayYmd(), perms); // WP-VW-W3 — the W2 actions + start / presentation / finish
        box.innerHTML = keys.map(k => {
            if (k === 'start') { return '<button type="button" class="btn btn-primary vw-act-full" data-start="1"><i class="bx ' + ACTION.start.icon + '"></i> ' + esc(L[ACTION.start.label] || '') + '</button>'; }
            if (k === 'presentation' || k === 'finish') {
                const a = ACTION[k];
                return '<a class="btn btn-' + a.tone + ' flex-fill" href="' + esc(C.presentationUrl(presentationBase, v.plannedVisitId, k === 'finish')) + '"><i class="bx ' + a.icon + '"></i> ' + esc(L[a.label] || '') + '</a>';
            }
            if (k === 'locked') { return '<div class="vw-locked-note"><i class="bx bx-lock-alt"></i> ' + esc(L.LockedInfo || '') + '</div>'; }
            const a = ACTION[k];
            // WP-VW-W4 (W4-WEB-a) — "send the report" opens the report wizard v2 of this visit; "result" (today, no execution) stays v1
            if (k === 'sendReport' && v.plannedVisitId) { return '<a class="btn btn-' + a.tone + ' flex-fill" href="' + esc(C.reportUrl(reportBase, v.plannedVisitId)) + '"><i class="bx ' + a.icon + '"></i> ' + esc(L[a.label] || '') + '</a>'; }
            if (k === 'result' || k === 'sendReport') { return '<a class="btn btn-' + a.tone + ' flex-fill" href="' + esc(executionUrl) + '"><i class="bx ' + a.icon + '"></i> ' + esc(L[a.label] || '') + '</a>'; }
            if (k === 'plan') {
                const w = weekOf(v.weekStart);
                const href = w && w.planningSessionId ? planningUrl + '/Details/' + encodeURIComponent(w.planningSessionId) : planningUrl;
                return '<a class="btn btn-' + a.tone + ' flex-fill" href="' + esc(href) + '"><i class="bx ' + a.icon + '"></i> ' + esc(L[a.label] || '') + '</a>';
            }
            return '<button type="button" class="btn btn-' + a.tone + ' flex-fill" data-dialog="' + k + '"><i class="bx ' + a.icon + '"></i> ' + esc(L[a.label] || '') + '</button>';
        }).join('');
        box.classList.toggle('d-none', !keys.length);
    }

    // ── E2 (P1 / P2): ONE dialog, three tabs — cancel · not done · reschedule ─────────────────────────────────
    const APPLIES = { cancel: 'cancel', notDone: 'missed', reschedule: 'reschedule' };
    const TITLE = { cancel: 'DlgCancelTitle', notDone: 'DlgNotDoneTitle', reschedule: 'DlgRescheduleTitle' };
    const TAB = { cancel: 'DlgTab_cancel', notDone: 'DlgTab_notDone', reschedule: 'DlgTab_reschedule' };
    const MEAN = { cancel: 'DlgMean_cancel', notDone: 'DlgMean_notDone', reschedule: 'DlgMean_reschedule' };
    const CONFIRM = { cancel: 'DlgConfirm_cancel', notDone: 'DlgConfirm_notDone', reschedule: 'DlgConfirm_reschedule' };
    const dlg = { kind: null, reasons: [], reason: null, date: null, saving: false };

    function maxNote() { return (state.contract && state.contract.maxNoteLength) || 500; }

    function openDialog(kind) {
        const v = state.current; if (!v) { return; }
        dlg.kind = kind; dlg.reasons = []; dlg.reason = null; dlg.date = null; dlg.saving = false;
        el('vw-dlg-title').textContent = L[TITLE[kind]] || '';
        // P1 — "Dr. Kerem Aslan · 8 Eki Perşembe 11:30"
        el('vw-dlg-target').innerHTML = F.bidi(v.targetDisplayName || '—') + ' · ' + esc(F.dayMonth(v.plannedDate) + ' ' + F.weekdayLong(v.plannedDate) + (v.startTime ? ' ' + v.startTime : ''));
        el('vw-dlg-tabs').innerHTML = C.dialogTabs(v, todayYmd(), perms).map(t => '<button type="button" role="tab" class="vw-tab' + (t.key === kind ? ' active' : '') + '" data-tab="' + t.key + '" aria-selected="' + (t.key === kind) + '"'
            + (t.enabled ? '' : ' disabled') + '>' + esc(L[TAB[t.key]] || '') + '</button>').join('');
        setText('vw-dlg-mean', L[MEAN[kind]] || '');
        const save = el('vw-dlg-save');
        save.textContent = L[CONFIRM[kind]] || '';
        save.className = 'btn ' + (kind === 'cancel' ? 'btn-danger' : 'btn-primary');
        el('vw-dlg-note').value = '';
        el('vw-dlg-error').classList.add('d-none');
        el('vw-dlg-date-box').classList.toggle('d-none', kind !== 'reschedule');
        el('vw-dlg-reasons').innerHTML = '<div class="text-muted small">' + esc(L.Loading || '') + '</div>';
        el('vw-dlg-days').innerHTML = '';
        syncDialog();
        showModal('vw-dialog');

        get('/reasons?appliesTo=' + APPLIES[kind] + '&lang=' + encodeURIComponent(lang())).then(r => {
            if (dlg.kind !== kind) { return; }
            if (!r.ok || !r.body || !r.body.data) {
                el('vw-dlg-reasons').innerHTML = '<div class="text-danger small">' + esc(L.ReasonsUnavailable || C.errorText(r, L)) + '</div>';
                return;
            }
            dlg.reasons = r.body.data.items || [];
            el('vw-dlg-reasons').innerHTML = dlg.reasons.map((x, i) =>
                '<label class="vw-reason"><input class="form-check-input m-0" type="radio" name="vw-reason" value="' + esc(x.code) + '" id="vw-reason-' + i + '"> '
                + '<span>' + F.bidi(x.label) + '</span></label>').join('');
            syncDialog();
        });

        if (kind === 'reschedule') {
            get('/reschedule-options?plannedVisitId=' + encodeURIComponent(v.plannedVisitId)).then(r => {
                if (dlg.kind !== kind) { return; }
                const daysList = r.ok && r.body && r.body.data ? r.body.data.days || [] : [];
                // P2 — a 4-column grid of day cards: the day, a mini bar, "4 visits · 4,9 h free"
                el('vw-dlg-days').innerHTML = daysList.length
                    ? daysList.map(d => {
                        const cap = C.capacity(d.plannedMinutes, d.capacityMinutes);
                        const tone = cap.raw > 85 ? 'vw-hi' : cap.raw > 60 ? 'vw-mid' : 'vw-lo';
                        return '<button type="button" class="vw-day-option" data-date="' + esc(d.date) + '">'
                            + '<span class="vw-do-day">' + esc(F.dayLabel(d.date)) + '</span>'
                            + '<span class="vw-mini"><span class="' + tone + '" style="width:' + cap.pct + '%"></span></span>'
                            + '<span class="vw-do-load">' + esc(F.isolateRatios(fmt(L.DayLoad || '{0} · {1}', d.plannedCount, hours(Math.max(0, d.capacityMinutes - d.plannedMinutes))))) + '</span>'
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
                hideModal('vw-dialog');
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

    // ── WP-VW-W3 (WEB-a) — E3: start the visit (photo, location, the server's start time) ─────────────────────
    // The Start button follows the tenant setting (contract.policy: photo required / optional, location required / info)
    // and what is still missing; the server decides the distance, the accuracy and the time. On success the presentation
    // page opens. Nothing is kept in the browser: the photo is a JPEG blob in memory until the dialog closes.
    const e3 = { visit: null, photo: null, photoUrl: null, photoCapturedAt: null, location: { status: 'pending' }, busy: false, stream: null, open: false };
    const policy = () => (state.execContract && state.execContract.policy) || null;
    // WP-VW-W3E (W3E-WEB-a) — the institution QR of this start: mode (off | optional | required), the box state, the read
    // text (sent as read — never parsed here), the pre-check answer (qr-check writes nothing), the camera.
    const qr = { mode: 'off', state: 'idle', payload: null, scannedAt: null, check: null, first: null, stream: null, timer: null, detector: null, seq: 0 };
    const e3State = () => ({ photo: e3.photo, location: e3.location, busy: e3.busy, kind: 'in_person', qr: { state: qr.state, payload: qr.payload, scannedAt: qr.scannedAt } });
    const show = (id, on) => { const n = el(id); if (n) { n.classList.toggle('d-none', !on); } };

    function openStart() {
        const v = state.current;
        if (!v || !C.canStart(v, perms)) { return; }
        stopCamera(); clearPhoto();
        e3.visit = v; e3.busy = false; e3.open = true;
        el('vw-e3-target').innerHTML = [F.bidi(v.targetDisplayName || '—'), v.accountDisplayName ? F.bidi(v.accountDisplayName) : '',
            esc(F.dayLabel(v.plannedDate) + (v.startTime ? ' ' + v.startTime + (v.endTime ? '–' + v.endTime : '') : ''))].filter(Boolean).join(' · ');
        const needs = C.evidenceNeeds(policy());
        setText('vw-e3-photo-need', '· ' + (needs.photo ? L.E3Required || '' : L.E3Optional || ''));
        setText('vw-e3-loc-need', '· ' + (needs.location ? L.E3Required || '' : L.E3Optional || ''));
        setText('vw-e3-webcam-label', state.layout === 'list' ? L.E3CameraPhone || '' : L.E3Webcam || '');
        showError('vw-e3-photo-error', '');
        showError('vw-e3-error', '');
        qrOpen(v);
        show('vw-e3-device', C.showDeviceLine(policy()));
        const panel = el('vw-detail');
        if (panel && window.bootstrap) { window.bootstrap.Offcanvas.getOrCreateInstance(panel).hide(); }
        showModal('vw-e3');
        locate();
        if (state.layout === 'list') { openCamera(); } // the phone (mockup "E3 · Kamera + konum"): the back camera at once
    }

    function closeStart() {
        e3.open = false; e3.visit = null;
        stopCamera(); clearPhoto(); qrStopCamera();
    }

    function showError(id, text, hint) {
        const n = el(id); if (!n) { return; }
        n.innerHTML = text ? esc(text) + (hint ? '<div class="small">' + esc(hint) + '</div>' : '') : '';
        n.classList.toggle('d-none', !text);
    }

    // the browser position: pending → ok | denied | unavailable | timeout (the distance is the server's)
    function locate() {
        e3.location = { status: 'pending' };
        syncStart();
        const visit = e3.visit;
        const geo = window.navigator && window.navigator.geolocation;
        if (!geo) { e3.location = { status: 'unavailable' }; syncStart(); return; }
        geo.getCurrentPosition(pos => {
            if (e3.visit !== visit) { return; }
            e3.location = { status: 'ok', latitude: pos.coords.latitude, longitude: pos.coords.longitude, accuracyMeters: pos.coords.accuracy };
            syncStart();
        }, err => {
            if (e3.visit !== visit) { return; }
            e3.location = { status: C.geoErrorStatus(err) };
            syncStart();
        }, { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
    }

    function syncStart() {
        const pol = policy();
        const lv = C.locationView(e3.location, pol);
        const chip = el('vw-e3-loc-chip');
        if (chip) { chip.className = 'vw-loc-chip vw-tone-' + lv.tone; chip.textContent = L[lv.labelKey] || ''; }
        const detail = lv.textKey ? fmt(L[lv.textKey] || '', ...lv.args.map(x => F.isolate(x))) : '';
        const note = lv.status === 'ok' ? (lv.required ? '' : L.Loc_infoNote || '') : (lv.status === 'pending' ? '' : (lv.required ? L.Loc_requiredMissing || '' : L.Loc_infoNote || ''));
        setText('vw-e3-loc-text', [detail, note].filter(Boolean).join(' '));
        show('vw-e3-loc-retry', lv.retry);
        // the photo block: empty → camera → taken
        show('vw-e3-empty', !e3.photo && !e3.stream);
        show('vw-e3-camera', !e3.photo && !!e3.stream);
        show('vw-e3-shutter-row', !e3.photo && !!e3.stream);
        show('vw-e3-taken', !!e3.photo);
        // "Missing: photo, location" / "The start time is taken from the server."
        const missing = C.startMissing(pol, e3State());
        setText('vw-e3-hint', missing.length ? fmt(L.E3Missing || '{0}', missing.map(k => L['E3Missing_' + k] || k).join(', ')) : (L.E3HintReady || ''));
        const begin = el('vw-e3-begin');
        if (begin) { begin.disabled = !C.canBegin(pol, e3State()); begin.setAttribute('aria-busy', String(e3.busy)); }
    }

    function clearPhoto() {
        if (e3.photoUrl && window.URL && window.URL.revokeObjectURL) { window.URL.revokeObjectURL(e3.photoUrl); }
        e3.photo = null; e3.photoUrl = null; e3.photoCapturedAt = null;
        const img = el('vw-e3-preview'); if (img) { img.removeAttribute && img.removeAttribute('src'); }
    }

    function setPhoto(blob, capturedAt) {
        const max = state.execContract && state.execContract.maxEvidenceBytes;
        if (max && blob.size > max) { showError('vw-e3-photo-error', L.Photo_tooLarge || ''); syncStart(); return; }
        clearPhoto(); stopCamera();
        e3.photo = blob;
        e3.photoCapturedAt = capturedAt || null;
        e3.photoUrl = window.URL && window.URL.createObjectURL ? window.URL.createObjectURL(blob) : null;
        const img = el('vw-e3-preview'); if (img && e3.photoUrl) { img.src = e3.photoUrl; }
        showError('vw-e3-photo-error', '');
        syncStart();
    }

    /** Any image (file or video frame) → a JPEG: the longest edge at most 1600 px, quality 0.85. */
    function toJpeg(source, width, height) {
        const size = C.photoSize(width, height);
        const canvas = document.createElement('canvas');
        canvas.width = size.width; canvas.height = size.height;
        const ctx = canvas.getContext && canvas.getContext('2d');
        if (!ctx || !canvas.toBlob) { return Promise.reject(new Error('canvas')); }
        ctx.drawImage(source, 0, 0, size.width, size.height);
        return new Promise((resolve, reject) => canvas.toBlob(b => (b ? resolve(b) : reject(new Error('blob'))), C.PHOTO_TYPE, C.PHOTO_QUALITY));
    }

    function photoFromFile(file) {
        if (!file) { return; }
        if (!/^image\//.test(file.type || '')) { showError('vw-e3-photo-error', L.Photo_invalid || ''); return; }
        const url = window.URL.createObjectURL(file);
        const img = new window.Image();
        img.onload = () => {
            toJpeg(img, img.naturalWidth, img.naturalHeight)
                .then(b => setPhoto(b, null), () => showError('vw-e3-photo-error', L.Photo_invalid || ''))
                .then(() => window.URL.revokeObjectURL(url));
        };
        img.onerror = () => { window.URL.revokeObjectURL(url); showError('vw-e3-photo-error', L.Photo_invalid || ''); };
        img.src = url;
    }

    // the webcam (getUserMedia): the phone asks for the back camera; a refusal / no camera is said, the upload stays
    function openCamera() {
        if (qr.stream) { qrStopCamera(); qr.state = 'idle'; qrRender(); } // one camera at a time
        const md = window.navigator && window.navigator.mediaDevices;
        if (!md || !md.getUserMedia) { showError('vw-e3-photo-error', L[C.cameraErrorKey({ name: 'unsupported' })] || ''); return; }
        showError('vw-e3-photo-error', '');
        md.getUserMedia(C.cameraConstraints(state.layout)).then(stream => {
            if (!e3.open) { stream.getTracks().forEach(t => t.stop()); return; }
            e3.stream = stream;
            const video = el('vw-e3-video');
            if (video) { video.srcObject = stream; if (video.play) { Promise.resolve(video.play()).catch(() => {}); } }
            syncStart();
        }, err => showError('vw-e3-photo-error', L[C.cameraErrorKey(err)] || ''));
    }

    function shoot() {
        const video = el('vw-e3-video');
        if (!video || !video.videoWidth) { return; }
        toJpeg(video, video.videoWidth, video.videoHeight).then(b => setPhoto(b, new Date().toISOString()), () => showError('vw-e3-photo-error', L.Photo_invalid || ''));
    }

    function stopCamera() {
        if (e3.stream) { e3.stream.getTracks().forEach(t => t.stop()); e3.stream = null; }
        const video = el('vw-e3-video'); if (video) { video.srcObject = null; }
    }

    function showStartError(r) {
        const e = C.startError(r);
        const d = C.distanceParts(e.distanceMeters);
        const distance = d ? fmt(L[d.key] || '{0}', d.key === 'DistanceKm' ? F.number(d.value, 1) : d.value) : '—';
        showError('vw-e3-error', fmt(L[e.key] || L.ActionFailed || '', F.isolate(distance)), e.hintKey ? L[e.hintKey] || '' : '');
    }

    /** "Start the presentation": POST start (multipart); success (or a visit already started) opens the presentation. */
    function beginVisit() {
        const v = e3.visit;
        if (!v || !C.canBegin(policy(), e3State())) { return; }
        e3.busy = true; syncStart(); showError('vw-e3-error', '');
        const form = new window.FormData();
        C.startFields(v.plannedVisitId, { location: e3.location, photo: e3.photo, photoName: 'visit-photo.jpg', photoCapturedAt: e3.photoCapturedAt, qr: e3State().qr })
            .forEach(f => (f.length > 2 ? form.append(f[0], f[1], f[2]) : form.append(f[0], f[1])));
        upload('/execution/start', form).then(r => {
            e3.busy = false;
            if (r.ok || C.startError(r).code === 'visit_already_started') {
                window.location.assign(C.presentationUrl(presentationBase, v.plannedVisitId, false));
                return;
            }
            syncStart();
            showStartError(r);
        });
    }

    // ── WP-VW-W3E (W3E-WEB-a) — the institution QR at the start (S1–S8) ─────────────────────────────────────────
    // The box follows the tenant setting (contract.policy.qrMode; a remote visit / off: no box) and the server's pre-check
    // (POST /execution/qr-check, which writes nothing); the start decides again. The camera: BarcodeDetector when the
    // browser has it, else the local jsQR (no CDN); the phone scans full screen with the back camera.
    const qrNow = () => new Date().toISOString();
    const qrClock = iso => { const d = new Date(iso || ''); return isNaN(d) ? '' : String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0'); };

    function qrOpen(v) {
        qrStopCamera();
        qr.mode = C.qrMode(policy(), 'in_person');
        qr.state = 'idle'; qr.payload = null; qr.scannedAt = null; qr.check = null; qr.first = null; qr.seq++;
        show('vw-e3-qr', qr.mode !== 'off');
        if (qr.mode === 'off') { return; }
        setText('vw-e3-qr-need', '· ' + (L['QrMode_' + qr.mode] || ''));
        qrRender();
        // what the institution has before anything is read: required + no code in the chain → start without a penalty (S6)
        post('/execution/qr-check', { plannedVisitId: v.plannedVisitId }).then(r => {
            if (e3.visit !== v || !r.ok || !r.body || !r.body.data) { return; }
            qr.first = r.body.data;
            if (qr.state === 'idle' && r.body.data.status === 'not_issued' && qr.mode === 'required') { qr.state = 'noPrinted'; qrRender(); }
        });
    }

    function qrBox(cls, inner) { return '<div class="vw-qr-box ' + (cls || '') + '">' + inner + '</div>'; }
    const qrBtn = (action, text, icon, outline) => '<button type="button" class="vw-qr-btn' + (outline ? ' vw-qr-btn-o' : '') + '" data-qr="' + action + '">' + (icon ? '<i class="bx ' + icon + '"></i>' : '') + esc(text || '') + '</button>';
    const qrLink = (action, text, small) => '<button type="button" class="vw-qr-link' + (small ? ' vw-qr-link-sm' : '') + '" data-qr="' + action + '">' + esc(text || '') + '</button>';

    function qrRender() {
        if (qr.mode === 'off') { show('vw-e3-qr', false); syncStart(); return; }
        const box = el('vw-e3-qr-box');
        const v = e3.visit || {};
        const inline = qr.state === 'camera' && state.layout !== 'list';
        show('vw-e3-qr-cam', inline);
        show('vw-e3-qr-full', qr.state === 'camera' && state.layout === 'list');
        let html = '';
        switch (qr.state) {
            case 'camera': html = ''; break;
            case 'checking':
                html = qrBox('', '<div class="vw-qr-ico"><i class="bx bx-loader-alt bx-spin"></i></div><div class="vw-qr-txt">' + esc(L.QrIdle) + '</div>'); break;
            case 'ok': {
                const name = qr.check && qr.check.accountName ? qr.check.accountName : v.accountDisplayName || v.targetDisplayName || '';
                const sub = qr.check && qr.check.matchedVia === 'parent'
                    ? fmt(esc(L.QrOkParent), F.bidi(v.accountDisplayName || v.targetDisplayName || ''), esc(qrClock(qr.scannedAt)))
                    : fmt(esc(L.QrOkSelf), esc(qrClock(qr.scannedAt)));
                html = qrBox('vw-qr-ok', '<i class="bx bx-check-circle vw-qr-mark-ok"></i><div class="vw-qr-txt"><div class="vw-qr-strong">' + fmt(esc(L.QrOk), F.bidi(name)) + '</div><div class="vw-qr-sub">' + sub + '</div></div>' + qrLink('scan', L.QrRescan));
                break;
            }
            case 'scanned':
                html = qrBox('vw-qr-info', '<i class="bx bx-info-circle vw-qr-mark-info"></i><div class="vw-qr-txt">' + esc(L.QrScannedUnchecked) + '</div>' + qrLink('scan', L.QrRescan)); break;
            case 'wrong': {
                const n = qr.check && qr.check.accountName;
                html = qrBox('vw-qr-warn', '<i class="bx bx-error-circle vw-qr-mark-warn"></i><div class="vw-qr-txt">' + (n ? fmt(esc(L.QrWrong), F.bidi(n)) : esc(L.QrWrongUnknown)) + '</div>' + qrBtn('scan', L.QrRescan, 'bx-camera'));
                break;
            }
            case 'invalid':
                html = qrBox('vw-qr-warn', '<i class="bx bx-error-circle vw-qr-mark-warn"></i><div class="vw-qr-txt">' + esc(L.QrInvalid) + '</div>' + qrBtn('scan', L.QrRescan, 'bx-camera')); break;
            case 'unverifiable':
                html = qrBox('vw-qr-warn', '<i class="bx bx-error-circle vw-qr-mark-warn"></i><div class="vw-qr-txt">' + esc(L.QrUnverifiable) + '</div>' + qrBtn('scan', L.QrRescan, 'bx-camera')); break;
            case 'noperm':
                html = qrBox('vw-qr-box-top', '<div class="vw-qr-ico vw-qr-ico-off"><i class="bx bx-camera-off"></i></div><div class="vw-qr-txt" style="display:flex;flex-direction:column;gap:6px"><div class="vw-qr-strong">' + esc(L.QrNoPermTitle)
                    + '</div><ol class="vw-qr-steps"><li>' + esc(L.QrNoPerm1) + '</li><li>' + esc(L.QrNoPerm2) + '</li><li>' + esc(L.QrNoPerm3) + '</li></ol><div class="vw-qr-row" style="margin-top:6px">'
                    + qrBtn('scan', L.QrRetry) + qrLink('nocode', L.QrNoCode) + '</div></div>'); break;
            case 'nocamera':
                html = qrBox('', '<div class="vw-qr-ico vw-qr-ico-off"><i class="bx bx-camera-off"></i></div><div class="vw-qr-txt"><div>' + esc(L.QrNoCamera) + '</div>' + qrLink('nocode', L.QrNoCode, true) + '</div>'); break;
            case 'nocodeOpt':
                html = qrBox('vw-qr-box-col', '<div>' + esc(L.QrNoCodeOpt) + '</div><div class="vw-qr-row">' + qrBtn('skip', L.QrSkip, null, true) + qrLink('back', L.QrBack) + '</div>'); break;
            case 'nocodeReq':
                html = qrBox('vw-qr-box-col', '<div>' + esc(L.QrNoCodeReq) + '</div><div class="vw-qr-row"><span class="vw-qr-ask"><i class="bx bx-support"></i>' + esc(L.QrAskException) + '</span>' + qrLink('back', L.QrBack) + '</div>'); break;
            case 'noPrinted':
                html = qrBox('vw-qr-info vw-qr-box-top', '<i class="bx bx-info-circle vw-qr-mark-info"></i><div class="vw-qr-txt">' + esc(L.QrNoPrinted) + '</div>'); break;
            case 'skipped':
                html = qrBox('', '<i class="bx bx-minus-circle vw-qr-mark-off"></i><div class="vw-qr-txt">' + esc(L.QrSkipped) + '</div>' + qrLink('scan', L.QrScan)); break;
            default:
                html = qrBox('', '<div class="vw-qr-ico"><i class="bx bx-qr-scan"></i></div><div class="vw-qr-txt"><div>' + esc(L.QrIdle) + '</div>' + qrLink('nocode', L.QrNoCode, true) + '</div>'
                    + qrBtn('scan', L.QrScan, 'bx-camera'));
        }

        box.innerHTML = html;
        show('vw-e3-qr-box', qr.state !== 'camera');
        syncStart();
    }

    let jsQrLoading = null;
    function loadJsQr() {
        if (window.jsQR) { return Promise.resolve(true); }
        if (jsQrLoading) { return jsQrLoading; }
        const src = (el('vw-e3') && el('vw-e3').getAttribute('data-jsqr')) || '';
        jsQrLoading = new Promise(resolve => {
            const tag = document.createElement('script');
            tag.src = src; tag.async = true;
            tag.onload = () => resolve(!!window.jsQR);
            tag.onerror = () => resolve(false);
            document.head.appendChild(tag);
        });
        return jsQrLoading;
    }

    /** BarcodeDetector (qr_code) when the browser has it, else the local jsQR. */
    function qrDecoder() {
        const BD = window.BarcodeDetector;
        const viaJsQr = () => loadJsQr().then(ok => (ok ? { jsqr: true } : null));
        if (!BD || !BD.getSupportedFormats) { return viaJsQr(); }
        return BD.getSupportedFormats().then(f => (f && f.indexOf('qr_code') > -1 ? { detector: new BD({ formats: ['qr_code'] }) } : viaJsQr()), viaJsQr);
    }

    function qrScan() {
        if (!e3.open) { return; }
        stopCamera(); // the photo camera off: one camera at a time
        const md = window.navigator && window.navigator.mediaDevices;
        if (!md || !md.getUserMedia) { qr.state = 'nocamera'; qrRender(); return; }
        qrStopCamera();
        const seq = ++qr.seq;
        md.getUserMedia(C.cameraConstraints('list')).then(stream => { // the BACK camera (a QR on a desk), any device
            if (seq !== qr.seq || !e3.open) { stream.getTracks().forEach(t => t.stop()); return; }
            qr.stream = stream; qr.state = 'camera'; qrRender();
            const video = el(state.layout === 'list' ? 'vw-e3-qr-full-video' : 'vw-e3-qr-video');
            if (video) { video.srcObject = stream; if (video.play) { Promise.resolve(video.play()).catch(() => {}); } }
            qrDecoder().then(dec => {
                if (seq !== qr.seq) { return; }
                if (!dec) { qrStopCamera(); qr.state = 'nocamera'; qrRender(); return; }
                qr.detector = dec;
                qrTick(video, seq);
            });
        }, err => {
            if (seq !== qr.seq) { return; }
            const name = err && err.name;
            qr.state = name === 'NotAllowedError' || name === 'SecurityError' ? 'noperm' : 'nocamera';
            qrRender();
        });
    }

    let qrCanvas = null;
    function qrTick(video, seq) {
        if (seq !== qr.seq || !qr.stream) { return; }
        const again = () => { qr.timer = window.setTimeout(() => qrTick(video, seq), 250); };
        if (!video || video.readyState < 2 || !video.videoWidth) { again(); return; }
        if (qr.detector && qr.detector.detector) {
            qr.detector.detector.detect(video).then(codes => {
                const text = codes && codes.length ? codes[0].rawValue : null;
                if (text) { qrFound(text, seq); } else { again(); }
            }, again);
            return;
        }

        if (!window.jsQR) { again(); return; }
        const w = Math.min(640, video.videoWidth), h = Math.round(video.videoHeight * w / video.videoWidth);
        qrCanvas = qrCanvas || document.createElement('canvas');
        qrCanvas.width = w; qrCanvas.height = h;
        const ctx = qrCanvas.getContext('2d', { willReadFrequently: true });
        ctx.drawImage(video, 0, 0, w, h);
        const found = window.jsQR(ctx.getImageData(0, 0, w, h).data, w, h, { inversionAttempts: 'attemptBoth' });
        if (found && found.data) { qrFound(found.data, seq); } else { again(); }
    }

    /** A code read: the camera off, the text kept as read, the server's pre-check (nothing written) decides the box. */
    function qrFound(text, seq) {
        if (seq !== qr.seq) { return; }
        qrStopCamera();
        const mine = qr.seq; // a rescan / cancel / close in the meantime makes this answer stale
        qr.payload = text; qr.scannedAt = qrNow(); qr.state = 'checking'; qrRender();
        const visit = e3.visit;
        post('/execution/qr-check', { plannedVisitId: visit.plannedVisitId, qrPayload: text }).then(r => {
            if (mine !== qr.seq || e3.visit !== visit) { return; }
            if (!r.ok || !r.body || !r.body.data) { qr.check = null; qr.state = 'scanned'; qrRender(); return; } // the start checks it
            qr.check = r.body.data;
            const view = C.qrView(r.body.data, qr.mode);
            qr.state = view.state === 'idle' ? 'scanned' : view.state;
            qrRender();
        });
    }

    function qrStopCamera() {
        qr.seq++;
        if (qr.timer) { window.clearTimeout(qr.timer); qr.timer = null; }
        if (qr.stream) { qr.stream.getTracks().forEach(t => t.stop()); qr.stream = null; }
        ['vw-e3-qr-video', 'vw-e3-qr-full-video'].forEach(id => { const v = el(id); if (v) { v.srcObject = null; } });
        show('vw-e3-qr-full', false);
        show('vw-e3-qr-cam', false);
    }

    function qrAction(action) {
        if (action === 'scan') { qrScan(); return; }
        if (action === 'cancel') { qrStopCamera(); qr.state = qr.payload ? (qr.check ? C.qrView(qr.check, qr.mode).state : 'scanned') : 'idle'; qrRender(); return; }
        if (action === 'nocode') { qrStopCamera(); qr.payload = null; qr.check = null; qr.state = C.qrNoCodeState(qr.mode, qr.first); qrRender(); return; }
        if (action === 'skip') { if (qr.mode === 'optional') { qr.payload = null; qr.state = 'skipped'; qrRender(); } return; }
        if (action === 'back') { qr.state = 'idle'; qrRender(); }
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
        showModal('vw-reopen-modal');
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
            if (r.ok) { hideModal('vw-reopen-modal'); toast(L.ReopenDone || ''); load(true); } else { toast(C.errorText(r, L), 'error'); }
        });
    }

    function showUnplaced() {
        const w = weekOf(state.week); if (!w) { return; }
        el('vw-unplaced-body').textContent = fmt(L.UnplacedDetail || '{0}', w.unplacedCount);
        // WP-VW-W2 (WEB-b) — the list itself (weeks[].unplaced[], BE-c): name, institution, the reason in the user's language
        const list = el('vw-unplaced-list');
        if (list) {
            list.innerHTML = C.unplacedRows(w, L).map(u => '<li class="list-group-item px-0 d-flex justify-content-between gap-2"><div><div class="fw-medium">' + F.bidi(u.name) + '</div>'
                + (u.account ? '<div class="small text-muted">' + F.bidi(u.account) + '</div>' : '')
                + '<div class="small">' + esc(u.reason) + '</div></div></li>').join('');
        }
        showModal('vw-unplaced-modal');
    }

    // ── unplanned visit (today only) ─────────────────────────────────────────────────────────────────────────
    // W4-FIX-c (G-3) — the target is a doctor (contact) or a pharmacy (a pharmacy-type account); (G-4) the body sends the
    // time window (start + end) or no time at all — see workspace-core unplannedBody.
    const unp = { type: 'contact', targetId: null, timer: null };

    function setUnplannedType(type) {
        unp.type = type === 'pharmacy' && perms.pharmacies ? 'pharmacy' : 'contact';
        unp.targetId = null;
        el('vw-unp-search').value = '';
        el('vw-unp-results').innerHTML = '';
        el('vw-unp-save').disabled = true;
        const pharmacy = unp.type === 'pharmacy';
        el('vw-unp-search-label').textContent = (pharmacy ? L.UnpPharmacyLabel : L.UnpDoctorLabel) || '';
        el('vw-unp-search').setAttribute('placeholder', (pharmacy ? L.UnpPharmacySearch : L.UnpDoctorSearch) || '');
        const radio = el(pharmacy ? 'vw-unp-type-pharmacy' : 'vw-unp-type-contact'); if (radio) { radio.checked = true; }
    }

    function openUnplanned() {
        // the pharmacy choice appears only with crm.account.read; the doctor flow is the same as before
        el('vw-unp-type-box').classList.toggle('d-none', !perms.pharmacies);
        setUnplannedType('contact');
        el('vw-unp-time').value = '';
        el('vw-unp-day').textContent = F.dayLabel(todayYmd());
        el('vw-unp-error').classList.add('d-none');
        el('vw-unp-save').disabled = true;
        showModal('vw-unplanned-modal');
    }

    function searchContacts() {
        const q = el('vw-unp-search').value.trim();
        const pharmacy = unp.type === 'pharmacy';
        clearTimeout(unp.timer);
        unp.targetId = null;
        el('vw-unp-save').disabled = true;
        if (q.length < 2 || !(pharmacy ? perms.pharmacies : perms.contacts)) { el('vw-unp-results').innerHTML = ''; return; }
        unp.timer = setTimeout(() => {
            const asked = unp.type;
            const path = pharmacy ? '/pharmacies/search?search=' + encodeURIComponent(q) : '/contacts/search?search=' + encodeURIComponent(q) + '&limit=10';
            get(path).then(r => {
                if (asked !== unp.type) { return; } // the type changed while the search was on its way
                const d = r.ok && r.body ? r.body.data : null;
                const items = Array.isArray(d) ? d : (d && Array.isArray(d.items) ? d.items : []);
                el('vw-unp-results').innerHTML = items.length
                    ? items.map(c => (pharmacy
                        // a pharmacy: its name + its code / territory, so two branches of one chain are told apart
                        ? '<button type="button" class="list-group-item list-group-item-action" data-pharmacy="' + esc(c.id) + '">' + F.bidi(c.accountName || c.id)
                            + ([c.accountCode, c.territoryNodeName].filter(Boolean).length ? ' <span class="small text-muted">' + F.bidi([c.accountCode, c.territoryNodeName].filter(Boolean).join(' · ')) + '</span>' : '') + '</button>'
                        : '<button type="button" class="list-group-item list-group-item-action" data-contact="' + esc(c.id) + '">' + F.bidi(c.displayName) + '</button>')).join('')
                    : '<div class="text-muted small p-2">' + esc(L.NoMatches || '') + '</div>';
            });
        }, 250);
    }

    function saveUnplanned() {
        if (!unp.targetId) { return; }
        el('vw-unp-save').disabled = true;
        post('/planned-visits/unplanned', C.unplannedBody({ type: unp.type, id: unp.targetId }, todayYmd(), el('vw-unp-time').value || null, Date.now())).then(r => {
            el('vw-unp-save').disabled = false;
            if (r.ok) { hideModal('vw-unplanned-modal'); toast(L.Saved || ''); load(true); return; }
            const box = el('vw-unp-error'); box.textContent = C.errorText(r, L); box.classList.remove('d-none');
        });
    }

    // ── Plan mode (WP-VW-W2 WEB-b) ───────────────────────────────────────────────────────────────────────────
    // The Targets panel next to the calendar runs on the Visit Planning page's OWN rules (targets-core.js — the same
    // module that page uses, no copy) and its own proxies (/CRM/VisitPlanning/api: the session update, the 3D doctor
    // read, the product search, the reference labels). Only a DRAFT week is edited; an approved / past week is read-only
    // with the reopen hint. A doctor dropped on a day / time, or a draft card moved there, is a day pin (+ start time on
    // the 15-minute grid) through the EXISTING session update.
    const plan = { sessionId: null, week: null, session: null, accounts: [], accountId: null, accountCounts: {}, quick: 'due', rows: {}, counts: {}, specLabels: {}, picked: [], results: [] };
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
        const layout = el('vw-layout');
        if (layout) { layout.classList.toggle('vw-with-panel', planOn && state.layout === 'grid'); }
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
            ((mine.ok && mine.body && mine.body.data && mine.body.data.items) || []).forEach(a => {
                const id = a.accountId || a.id;
                add(id, a.accountName || a.name);
                if (id && a.activeContactCount != null) { plan.accountCounts[id] = a.activeContactCount; }
            });
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
                if (quick === 'all') { plan.accountCounts[aid] = plan.rows[key].length; }
            });
        };
        return Promise.all([read('all'), read(plan.quick)]).then(renderPanel);
    }

    // L6 — this week's place of a doctor: its first draft / planned visit of the week ("Sal 10:15")
    function whenThisWeek(contactId) {
        const v = (state.data ? state.data.visits : []).filter(x => x.contactId === contactId && x.weekStart === state.week && x.workStatus !== 'cancelled')
            .sort((a, b) => (a.plannedDate + (a.startTime || '')) < (b.plannedDate + (b.startTime || '')) ? -1 : 1)[0];
        return v ? F.dayShort(v.plannedDate) + (v.startTime ? ' ' + v.startTime : '') : '';
    }

    function renderPanel() {
        if (state.mode !== 'plan' || !el('vw-tg-list')) { return; }
        const w = weekOf(state.week);
        const editable = planEditable();
        // L5 — "HEDEFLER" + "42. Hafta · sürükleyip güne bırakın"
        setText('vw-tg-title', w ? fmt(L.TargetsSub || '{0}', weekText(w)) : '');
        const lockText = !w || !w.planningSessionId ? (L.TargetsNoPlan || '')
            : w.state === 'approved' ? fmt(L.PlanReadOnlyWeek || '{0}', weekText(w))
            : w.state === 'past' ? fmt(L.PlanReadOnlyPast || '{0}', weekText(w)) : '';
        const lock = el('vw-tg-locked');
        if (lock) { lock.innerHTML = lockText ? '<i class="bx bx-lock-alt"></i> ' + esc(lockText) : ''; lock.classList.toggle('d-none', !lockText); }
        const sel = el('vw-tg-account');
        if (sel) {
            // L5 — the institution with its doctor count
            sel.innerHTML = plan.accounts.map(a => '<option value="' + esc(a.id) + '"' + (a.id === plan.accountId ? ' selected' : '') + '>' + esc(F.isolate(a.name))
                + (plan.accountCounts[a.id] != null ? ' (' + plan.accountCounts[a.id] + ')' : '') + '</option>').join('');
            sel.disabled = !plan.accounts.length;
        }

        const all = plan.rows[doctorKey(plan.accountId, 'all')] || [];
        const visible = plan.rows[doctorKey(plan.accountId, plan.quick)] || [];
        const split = TC.splitPlanFirst(all, visible, inPlan);
        const counts = TC.quickCounts(all, plan.counts[doctorKey(plan.accountId, 'all')], inPlan);
        // L7 — the quick filters: small, one line
        el('vw-tg-quick').innerHTML = QUICK.map(q => '<button type="button" class="vw-quick-btn' + (q === plan.quick ? ' active' : '')
            + ' vw-tg-quick" data-quick="' + q + '" aria-pressed="' + (q === plan.quick) + '">' + esc(L[QUICK_KEY[q]] || q) + ' (' + counts[q] + ')</button>').join('');

        const products = cid => {
            const saved = savedContacts().find(c => c.contactId === cid);
            return ((saved && saved.products) || []).map((p, i) => '<span class="vw-pchip ' + (TC.roleOf(p) === TC.ROLE_PROMO ? 'vw-pchip-promo' : 'vw-pchip-reminder') + '">'
                + F.bidi(p.productName || p.productCode || '') + '</span>').join('');
        };
        // L6 — the doctor card: ⋮⋮ handle, the bold name, the coloured specialty chip, the planned product chips,
        // "dönemde N · done / remaining · Sal 10:15" and the segment chips
        const row = r => {
            const st = r.status || {};
            const can = editable && !r.blocked;
            const base = st.requiredVisitCount != null && st.frequencyStatus !== 'unknown' ? fmt(L.FrequencyPerPeriod || '{0}', st.requiredVisitCount)
                : (st.frequencyDefault === 'weekly' ? (L.FrequencyDefaultWeekly || '') : '');
            const ratio = st.done != null || st.remaining != null ? F.ratio(st.done || 0, st.remaining != null ? st.remaining : '—') : '';
            const freq = [base, ratio].filter(Boolean).join(' · ');
            const when = whenThisWeek(r.contactId);
            const spec = r.specialty ? (plan.specLabels[r.specialty] || plan.specLabels[String(r.specialty).toLowerCase()] || r.specialty) : '';
            return '<div class="vw-tg-row' + (inPlan(r.contactId) ? ' vw-tg-on' : '') + (can ? ' vw-draggable' : '') + '" draggable="' + (can ? 'true' : 'false')
                + '" data-cid="' + esc(r.contactId) + '">'
                + '<input class="form-check-input vw-tg-check" type="checkbox" data-cid="' + esc(r.contactId) + '"' + (inPlan(r.contactId) ? ' checked' : '')
                + (can ? '' : ' disabled') + ' aria-label="' + esc(r.name) + '">'
                + '<div class="flex-grow-1" style="min-width:0">'
                + '<div class="vw-tg-top"><span class="vw-tg-name">' + F.bidi(r.name) + '</span>' + (can ? '<i class="bx bx-grid-vertical vw-handle" aria-hidden="true"></i>' : '') + '</div>'
                + '<div class="vw-tg-chips">' + (spec ? '<span class="vw-spec-chip">' + F.bidi(spec) + '</span>' : '') + products(r.contactId) + '</div>'
                + '<div class="vw-tg-meta">' + (freq ? '<span>' + esc(freq) + '</span>' : '') + (when ? '<span class="vw-tg-when">' + esc(when) + '</span>' : '')
                + (st.segmentBadges || []).map(n => '<span class="vw-seg-chip">' + F.bidi(n) + '</span>').join('') + '</div>'
                + '</div></div>';
        };
        const head = t => '<div class="vw-tg-group">' + esc(t) + '</div>';
        el('vw-tg-list').innerHTML = (split.plan.length
            ? head(fmt(L.PlanDoctorsHeading || '{0}', split.plan.length)) + split.plan.map(row).join('') + (split.others.length ? head(L.OtherDoctorsHeading || '') : '')
            : '') + split.others.map(row).join('')
            + (split.plan.length + split.others.length ? '' : '<div class="text-muted small p-2">' + esc(plan.accountId ? (L.NoDoctors || '') : (L.TargetsNoPlan || '')) + '</div>');

        const selectable = split.others.filter(r => !r.blocked);
        const selAll = el('vw-tg-select-all');
        if (selAll) { selAll.innerHTML = '<i class="bx bx-check-double"></i> ' + esc(fmt(L.SelectAll || '{0}', selectable.length)); selAll.disabled = !editable || !selectable.length; }
        const apply = el('vw-tg-apply');
        if (apply) { apply.innerHTML = '<i class="bx bx-package"></i> ' + esc(fmt(L.ApplyProducts || '{0}', split.plan.length)); apply.disabled = !editable || !split.plan.length; }
        // L8 — the selection summary card: big numbers + the product distribution of the week
        const s = plan.session || {};
        setText('vw-tg-summary-head', w ? fmt(L.SummaryTitle || '{0}', weekText(w)) : '');
        el('vw-tg-summary').innerHTML = [[(s.selectedContacts || []).length, L.SummaryDoctors], [(s.selectedPharmacyIds || []).length, L.SummaryPharmacies], [(s.selectedAccountIds || []).length, L.SummaryAccounts]]
            .map(x => '<div><div class="vw-sum-n">' + x[0] + '</div><div class="vw-sum-l">' + esc(x[1] || '') + '</div></div>').join('');
        const dist = C.productDistribution((state.data ? state.data.visits : []).filter(v => v.weekStart === state.week));
        el('vw-tg-dist').innerHTML = dist.length ? dist.map(d => F.bidi(d.name) + ' ' + d.count).join(' · ') : esc(L.NoSelection || '');
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
            // the read's own reason (W2-BE-d pinMoveReason) first; the estimate only without it
            const code = v && v.pinMoveReason ? v.pinMoveReason : C.pinMoveCode(v);
            if (code && L[C.pinKey(code)]) { toast(L[C.pinKey(code)], 'warning'); }
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
        showModal('vw-products-modal');
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
                + '<span class="d-flex gap-1 align-items-center"><span class="vw-role ' + (i === 0 ? 'vw-role-promo' : 'vw-role-reminder') + '">' + esc(L[i === 0 ? 'RolePromo' : 'RoleReminder'] || '') + '</span>'
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
        writePlan(TC.selectionUpdate(plan.session, savedContacts(), changes), L.ProductsApplied || '').then(ok => { if (ok) { hideModal('vw-products-modal'); } });
    }

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────
    root.addEventListener('click', e => {
        const day = e.target.closest('.vw-list-day');
        if (day) { state.day = day.getAttribute('data-day'); renderDayList(); return; }
        const card = e.target.closest('.vw-list-card');
        if (card) { openDetail(state.byId[card.getAttribute('data-key')]); return; }
        const view = e.target.closest('[data-view]');
        if (view) { switchView(view.getAttribute('data-view')); }
    });
    document.addEventListener('click', e => {
        const s = e.target.closest('[data-start]');
        if (s) { openStart(); return; } // WP-VW-W3 — E3
        const d = e.target.closest('[data-dialog]');
        if (d) { openDialog(d.getAttribute('data-dialog')); return; }
        const tab = e.target.closest('[data-tab]');
        if (tab) { if (!tab.disabled) { openDialog(tab.getAttribute('data-tab')); } return; }
        const opt = e.target.closest('.vw-day-option');
        if (opt) {
            dlg.date = opt.getAttribute('data-date');
            document.querySelectorAll('.vw-day-option').forEach(b => b.classList.toggle('active', b === opt));
            syncDialog(); return;
        }
        const c = e.target.closest('[data-contact],[data-pharmacy]');
        if (c) {
            unp.targetId = c.getAttribute('data-contact') || c.getAttribute('data-pharmacy');
            document.querySelectorAll('#vw-unp-results [data-contact], #vw-unp-results [data-pharmacy]').forEach(b => b.classList.toggle('active', b === c));
            el('vw-unp-save').disabled = false;
        }
    });
    document.addEventListener('change', e => {
        if (e.target && e.target.name === 'vw-unp-type') { setUnplannedType(e.target.value); return; } // W4-FIX-c (G-3)
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
    on('vw-prev', 'click', () => navigate(-1));
    on('vw-next', 'click', () => navigate(1));
    on('vw-today', 'click', () => navigate(0));

    // WP-VW-W3 (WEB-a) — E3
    on('vw-e3-upload', 'click', () => { const f = el('vw-e3-file'); if (f) { f.value = ''; f.click(); } });
    on('vw-e3-file', 'change', e => photoFromFile(e.target.files && e.target.files[0]));
    on('vw-e3-webcam', 'click', openCamera);
    on('vw-e3-shutter', 'click', shoot);
    on('vw-e3-retake', 'click', () => { clearPhoto(); syncStart(); });
    on('vw-e3-loc-retry', 'click', locate);
    on('vw-e3-begin', 'click', beginVisit);
    // WP-VW-W3E (W3E-WEB-a) — the QR box / camera buttons (delegated: the box is redrawn per state)
    ['vw-e3-qr', 'vw-e3-qr-full'].forEach(id => on(id, 'click', e => {
        const b = e.target && e.target.closest ? e.target.closest('[data-qr]') : null;
        if (b) { e.preventDefault(); qrAction(b.getAttribute('data-qr')); }
    }));
    on('vw-e3', 'hidden.bs.modal', closeStart);

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
    if (perms.execute) { get('/execution/contract').then(r => { if (r.ok && r.body && r.body.data) { state.execContract = r.body.data; if (e3.open) { syncStart(); } } }); }
    load(true);
}(window, document));
