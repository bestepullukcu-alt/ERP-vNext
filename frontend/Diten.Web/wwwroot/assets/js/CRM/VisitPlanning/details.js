/**
 * MOD-0155-FU05 Visit Planning — session Details ("Road Map Details").
 * Targets are a master→detail pair of Golden-Compact DataTables (clinic/hospital accounts → their doctors); saving them
 * writes the session's selectedAccountIds + selectedContacts. The engine then previews a weekly road map: the route tab
 * shows the SELECTED week's Mon→Fri, Monday-first day tabs (weekends hidden, holidays disabled — from the working
 * calendar, Sat/Sun fallback), and visit cards. The default week is the calendar week containing the NEXT Monday.
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('visit-planning-details');
    if (!root) return;

    const L = window.L10n || {};
    const base = '/CRM/VisitPlanning/api';
    const sessionId = root.dataset.sessionId;
    const canGenerate = root.dataset.canGenerate === 'true';
    const canApply = root.dataset.canApply === 'true';

    const WORK_START = 9 * 60, WORK_END = 18 * 60;
    const CLINIC_TYPES = ['clinic', 'hospital'];
    // ?week= is the chosen week's Monday (yyyy-MM-dd) set by Create; Details resolves it to a route week.
    const requestedMonday = (() => { const w = new URLSearchParams(window.location.search).get('week'); return /^\d{4}-\d{2}-\d{2}$/.test(w || '') ? w : null; })();

    let currentVersion = null;
    let sessionData = null;
    let lastPreview = null;
    let scheduled = [];
    let activeWeek = 0;
    let dayBlocks = [];  // the active day's account blocks (index-addressable for expand + drag-reorder)
    const periodMap = {};

    const el = id => document.getElementById(id);
    const headers = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const esc = s => { const d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; };
    const opt = (value, label) => { const o = document.createElement('option'); o.value = value; o.textContent = label; return o; };
    const setText = (id, v) => { const n = el(id); if (n) n.textContent = v; };

    const api = (path, options) => {
        options = options || {};
        options.credentials = 'same-origin';
        options.headers = Object.assign(headers(), options.headers || {});
        return fetch(base + path, options).then(r => r.text().then(text => {
            let body = null; try { body = text ? JSON.parse(text) : null; } catch (e) { body = null; }
            return { ok: r.ok, status: r.status, body };
        }));
    };
    const getAuthHeaders = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const listItems = body => { if (!body) return []; const d = body.data !== undefined ? body.data : body; return Array.isArray(d) ? d : (d && Array.isArray(d.items) ? d.items : []); };
    const errorText = r => (r.body && r.body.errors && r.body.errors.length) ? r.body.errors.join(' · ') : (r.body && r.body.message) || ('HTTP ' + r.status);
    const setStatus = (text, isError) => { const n = el('vp-detail-status'); if (n) { n.textContent = text || ''; n.className = 'small mb-3 ' + (isError ? 'text-danger' : 'text-muted'); } };

    const statusLabel = v => ({ draft: L.StatusDraft, committed: L.StatusCommitted }[v] || v || '—');
    const statusTone = v => ({ committed: 'success', draft: 'primary' }[v] || 'secondary');
    // ISO-8601 week number (Thursday-based, Monday start) — the same labelling Create uses.
    const isoWeek = date => { const d = new Date(date); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() + 3 - ((d.getDay() + 6) % 7)); const week1 = new Date(d.getFullYear(), 0, 4); return 1 + Math.round(((d - week1) / 86400000 - 3 + ((week1.getDay() + 6) % 7)) / 7); };
    const weekNumberLabel = n => (L.WeekNumberLabel || '{0}. ' + (L.WeekLabel || 'Week')).replace('{0}', n);
    const isoLabelOf = week => {
        const mon = weekMonday(week);
        if (mon) return weekNumberLabel(isoWeek(mon));
        // No route rows yet for this week — label from the SAVED target week's Monday so it reads "41. Hafta", not "Week 1".
        const saved = sessionData && sessionData.targetWeekStart;
        if (/^\d{4}-\d{2}-\d{2}$/.test(saved || '')) return weekNumberLabel(isoWeek(new Date(saved)));
        return (L.WeekLabel || 'Week') + ' ' + (week + 1);
    };
    const setWeekLabel = week => setText('vp-d-week', isoLabelOf(week));

    const loadPeriods = () => api('/cycle-periods').then(r => {
        listItems(r.body).forEach(p => { const id = p.cyclePeriodId || p.id; if (id) periodMap[id] = p.cycleName || p.cycleCode || p.name || id; });
    }).catch(() => {});

    const loadSession = () => api('/sessions/' + sessionId).then(r => {
        if (!r.ok || !r.body || !r.body.data) { setStatus(errorText(r), true); return; }
        sessionData = r.body.data;
        currentVersion = sessionData.version;
        const sidStr = String(sessionData.planningSessionId || sessionId);
        const title = sessionData.sessionName || sessionData.name || (L.SessionsTitle || 'Draft plan');
        setText('vp-plan-title', title);
        setText('vp-plan-title-crumb', title);
        setText('vp-plan-code', '#' + sidStr.slice(0, 8));
        setText('vp-d-period', periodMap[sessionData.cyclePeriodId] || sessionData.cyclePeriodId || '—');
        setText('vp-d-rep', sessionData.resourceDisplayName || sessionData.resourceId || '—');
        const st = String(sessionData.status || '').toLowerCase();
        const badge = el('vp-d-status');
        if (badge) { badge.textContent = statusLabel(st); badge.className = 'badge bg-label-' + statusTone(st); }
        setText('vp-d-status-strip', statusLabel(st));
        // Header WEEK label from the SAVED week's Monday — computed directly from the date, so the real ISO week
        // ("41. Hafta") shows even before any route is generated (never the "Week 1" fallback).
        if (/^\d{4}-\d{2}-\d{2}$/.test(sessionData.targetWeekStart || '')) {
            setText('vp-d-week', weekNumberLabel(isoWeek(new Date(sessionData.targetWeekStart))));
        }
    });

    const applyDefaultTab = () => {
        const has = sessionData && (((sessionData.selectedContacts || []).length) || ((sessionData.selectedAccountIds || []).length));
        if (has && window.bootstrap) { const btn = el('vp-tab-route-btn'); if (btn) try { window.bootstrap.Tab.getOrCreateInstance(btn).show(); } catch (e) { /* no-op */ } }
    };

    // ── date helpers ──
    const nextMonday = () => { const d = new Date(); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() + ((1 - d.getDay() + 7) % 7)); return d; };
    const mondayOf = date => { const d = new Date(date); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() - ((d.getDay() + 6) % 7)); return d; };
    const dayOrder = dateStr => { const d = new Date(dateStr); return isNaN(d) ? 99 : (d.getDay() + 6) % 7; }; // Mon=0 … Sun=6
    const dayLong = dateStr => { const d = new Date(dateStr); return isNaN(d) ? '—' : d.toLocaleDateString(undefined, { weekday: 'long' }); };
    const parseHM = t => { const m = /^(\d{1,2}):(\d{2})/.exec(String(t || '')); return m ? (parseInt(m[1], 10) * 60 + parseInt(m[2], 10)) : null; };
    const ymd = d => { const dt = (d instanceof Date) ? d : new Date(d); return isNaN(dt) ? '' : dt.getFullYear() + '-' + String(dt.getMonth() + 1).padStart(2, '0') + '-' + String(dt.getDate()).padStart(2, '0'); };
    const DAY_ABBR = { 0: 'DayShortSun', 1: 'DayShortMon', 2: 'DayShortTue', 3: 'DayShortWed', 4: 'DayShortThu', 5: 'DayShortFri', 6: 'DayShortSat' };
    const FALLBACK_ABBR = { 0: 'Sun', 1: 'Mon', 2: 'Tue', 3: 'Wed', 4: 'Thu', 5: 'Fri', 6: 'Sat' };
    const dayAbbr = wd => L[DAY_ABBR[wd]] || FALLBACK_ABBR[wd];

    // ── working calendar (weekends + holidays); Sat/Sun fallback ──
    const WD_NAME = { sunday: 0, monday: 1, tuesday: 2, wednesday: 3, thursday: 4, friday: 5, saturday: 6 };
    let wcWeekend = new Set([0, 6]);
    let wcHolidays = new Set();
    let wcYearLoaded = null, wcOk = false;
    const parseWorkingCalendar = body => {
        const d = (body && (body.data !== undefined ? body.data : body)) || {};
        const cal = Array.isArray(d) ? d[0] : ((d.items && d.items[0]) || d);
        if (!cal) return false;
        const weekend = new Set();
        (Array.isArray(cal.weekendDays || cal.WeekendDays || cal.weekend) ? (cal.weekendDays || cal.WeekendDays || cal.weekend) : []).forEach(w => {
            if (typeof w === 'number') weekend.add(w);
            else { const k = String(w).toLowerCase(); if (k in WD_NAME) weekend.add(WD_NAME[k]); else if (/^\d+$/.test(k)) weekend.add(parseInt(k, 10)); }
        });
        const holidays = new Set();
        const days = cal.days || cal.Days || cal.holidays || cal.Holidays || cal.nonWorkingDays || cal.NonWorkingDays || [];
        (Array.isArray(days) ? days : []).forEach(h => {
            const raw = (h && (h.date || h.Date || h.day || h.holidayDate)) || (typeof h === 'string' ? h : null);
            const t = h && (h.type || h.Type || h.dayType);
            if (raw) { const s = ymd(raw); if (s && (!t || /holiday|public|nonwork|tatil/i.test(String(t)))) holidays.add(s); }
        });
        if (weekend.size) wcWeekend = weekend;
        wcHolidays = holidays;
        return true;
    };
    const loadWorkingCalendar = year => {
        if (wcYearLoaded === year) return Promise.resolve();
        const country = (sessionData && (sessionData.countryCode || sessionData.country || sessionData.countryId)) || 'TR';
        return api('/working-calendar?country=' + encodeURIComponent(country) + '&year=' + year)
            .then(r => { wcOk = !!(r.ok && r.body && parseWorkingCalendar(r.body)); wcYearLoaded = year; })
            .catch(() => { wcYearLoaded = year; wcWeekend = new Set([0, 6]); wcHolidays = new Set(); });
    };

    // ── road map ──
    const weeksOf = rows => Array.from(new Set(rows.map(r => r.weekNumber))).sort((a, b) => a - b);
    const weekMonday = week => { const dates = scheduled.filter(r => r.weekNumber === week).map(r => new Date(r.plannedDate)).filter(d => !isNaN(d)); return dates.length ? mondayOf(new Date(Math.min.apply(null, dates))) : null; };

    // The week the plan was SAVED against (session.targetWeekStart, a Monday yyyy-MM-dd) — preferred over the ?week hint.
    const savedMonday = () => { const w = sessionData && sessionData.targetWeekStart; return /^\d{4}-\d{2}-\d{2}$/.test(w || '') ? w : null; };

    const defaultWeek = rows => {
        const weeks = weeksOf(rows);
        const saved = savedMonday();
        if (saved) { const hit = weeks.find(w => { const m = weekMonday(w); return m && ymd(m) === saved; }); if (hit != null) return hit; }
        if (requestedMonday) { const hit = weeks.find(w => { const m = weekMonday(w); return m && ymd(m) === requestedMonday; }); if (hit != null) return hit; }
        const nm = nextMonday();
        for (const w of weeks) {
            const mon = weekMonday(w); if (!mon) continue;
            const sun = new Date(mon); sun.setDate(sun.getDate() + 6);
            if (nm >= mon && nm <= sun) return w;
        }
        if (weeks.length) { const firstMon = weekMonday(weeks[0]); if (firstMon && nm < firstMon) return weeks[0]; return weeks[weeks.length - 1]; }
        return 0;
    };

    const visitTypeBadge = row => {
        const t = String(row.targetType || '').toLowerCase();
        const at = String(row.accountType || row.targetAccountType || '').toLowerCase();
        const isPharmacy = t === 'pharmacy' || at === 'pharmacy' || row.isPharmacy === true;
        return isPharmacy
            ? '<span class="badge bg-label-warning text-uppercase">' + esc(L.PharmacyVisit || 'Pharmacy visit') + '</span>'
            : '<span class="badge bg-label-info text-uppercase">' + esc(L.PhysicianVisit || 'Physician visit') + '</span>';
    };
    // Account-based label: resolve the visit's account to its name (from the targets cache); never show a raw id.
    const targetName = row => {
        const acc = accountSource.find(a => a.id === (row.accountId || row.targetId));
        return acc ? acc.name : (row.accountName || row.targetDisplayName || row.targetName || (L.PhysicianVisit || 'Visit'));
    };

    // ── Route-block model helpers (module scope so both the initial render and the manual re-flow can reuse them). ──
    // Lunch = a pause OVERLAPPING the backend lunch window (13:00–14:00, RouteOptimizationDefaults); a big gap outside
    // it is travel, not lunch. Keep this window in sync with IRouteOptimizationDefaultsProvider so the divider matches
    // where the optimizer actually reserved the break.
    const LUNCH_START = 13 * 60, LUNCH_END = 14 * 60, LUNCH_DUR = 45;
    const isLunch = (from, to) => from != null && to != null && from < LUNCH_END && to > LUNCH_START && (to - from) >= 30;
    const hm = m => (m == null ? '' : String(Math.floor(m / 60)).padStart(2, '0') + ':' + String(Math.round(m) % 60).padStart(2, '0'));
    // Travel time = Haversine × RoadFactor ÷ speed — the same v1 model the FU03 optimizer uses (1.3, 40 km/h).
    const ROAD_FACTOR = 1.3, SPEED_KM_MIN = 40 / 60;
    // A hop is treated as a WALK whenever it is walkable in ≤ WALK_MAX_MIN minutes at ~4.5 km/h (not just 1-min hops).
    // Otherwise it is a drive (road speed × detour factor). The divider shows a walk vs car icon/label accordingly.
    const WALK_MAX_MIN = 15, WALK_KM_PER_MIN = 4.5 / 60;
    const haversineKm = (a, b) => {
        if (!a || !b || a.lat == null || b.lat == null || a.lng == null || b.lng == null) return null;
        const R = 6371, tr = d => d * Math.PI / 180;
        const dLat = tr(b.lat - a.lat), dLng = tr(b.lng - a.lng);
        const s = Math.sin(dLat / 2) ** 2 + Math.cos(tr(a.lat)) * Math.cos(tr(b.lat)) * Math.sin(dLng / 2) ** 2;
        return 2 * R * Math.asin(Math.sqrt(s));
    };
    // One hop between two accounts → { min, walk }. Close hops are a walk (slower pace, no road detour factor); farther
    // hops are a drive. null when either account has no coordinates.
    const travelLeg = (idA, idB) => {
        const a = accountSource.find(x => x.id === idA), b = accountSource.find(x => x.id === idB);
        const km = haversineKm(a, b);
        if (km == null) return null;
        const walkMin = Math.round(km / WALK_KM_PER_MIN);
        return walkMin <= WALK_MAX_MIN
            ? { min: Math.max(1, walkMin), walk: true }
            : { min: Math.max(1, Math.round(km * ROAD_FACTOR / SPEED_KM_MIN)), walk: false };
    };
    const travelMin = (idA, idB) => { const l = travelLeg(idA, idB); return l ? l.min : null; };

    let activeDayOrderVal = null;   // the active day's Mon-first index (kept across a backend re-preview)
    let manualOrder = null;         // the rep's manual target-id sequence (null = engine optimum); sent to /preview + /apply
    let manualIsUser = false;       // true only after a USER drag/reorder (shows "reset to optimal"); auto-grouping is false
    let autoGroupDone = false;      // guards the one-shot "group pharmacies with their clinic" pass after the first preview

    // Friendly empty state (no button) — self-contained SVG illustration + title + hint. Used before a preview and for
    // an empty working day.
    const emptyStateHtml = () =>
        '<div class="vp-empty d-flex flex-column align-items-center justify-content-center text-center">' +
        '<svg width="96" height="84" viewBox="0 0 96 84" fill="none" aria-hidden="true">' +
        '<rect x="20" y="12" width="56" height="44" rx="8" fill="rgba(var(--bs-primary-rgb),.08)"></rect>' +
        '<path d="M30 52 L46 24 L60 52" stroke="rgba(var(--bs-primary-rgb),.35)" stroke-width="2" fill="none"></path>' +
        '<circle cx="34" cy="42" r="3.5" fill="var(--bs-primary)"></circle>' +
        '<circle cx="48" cy="32" r="3.5" fill="var(--bs-primary)"></circle>' +
        '<circle cx="62" cy="40" r="3.5" fill="var(--bs-primary)"></circle></svg>' +
        '<h5 class="mb-1 mt-3">' + esc(L.NoPlanTitle || 'No plan yet') + '</h5>' +
        '<div class="text-muted small" style="max-width:340px;">' + esc(L.NoPlanDesc || L.NoPreviewYet || '') + '</div></div>';

    // Render one working day of the current ENGINE `scheduled` result (optimal OR the manual-order re-plan — both come
    // from the backend). Group the day's visits into account blocks (split at lunch) with travel + lunch dividers; each
    // block is an inbox-row card that expands into its doctor visits. No client-side scheduling math — the times are the
    // backend's (constraint-honored: availability windows, working hours, lunch, travel, multi-day).
    const renderDay = order => {
        const rows = scheduled.filter(s => s.weekNumber === activeWeek && dayOrder(s.plannedDate) === order)
            .sort((a, b) => (a.sequenceOrder || 0) - (b.sequenceOrder || 0) || (parseHM(a.startTime) || 0) - (parseHM(b.startTime) || 0));
        const mon = weekMonday(activeWeek);
        const dayDate = mon ? new Date(mon.getTime() + order * 86400000) : (rows[0] && new Date(rows[0].plannedDate));
        setText('vp-day-plan-title', (dayDate ? dayLong(dayDate) + ' ' : '') + (L.DayPlan || 'Plan'));

        // Genuine idle = the working-window time before the first visit + after the last. Gaps BETWEEN stops are drive
        // time, never "open". An idle segment counts as an open slot only when it is long enough to fit another visit.
        const OPEN_SLOT_MIN = 30;
        let fStart = null, lEnd = null;
        rows.forEach(r => { const s = parseHM(r.startTime), e = parseHM(r.endTime); if (s != null) fStart = (fStart == null) ? s : Math.min(fStart, s); if (e != null) lEnd = (lEnd == null) ? e : Math.max(lEnd, e); });
        const headIdle = (fStart == null) ? 0 : Math.max(0, fStart - WORK_START);
        const tailIdle = (lEnd == null) ? 0 : Math.max(0, WORK_END - lEnd);

        const warns = [];
        if (rows.some(r => { const s = parseHM(r.startTime), e = parseHM(r.endTime); return (s != null && s < WORK_START) || (e != null && e > WORK_END); }))
            warns.push('<div class="alert alert-warning py-2 small mb-2"><i class="bx bx-error-circle me-1"></i>' + esc(L.OutsideHoursWarning || 'Some visits are scheduled outside working hours.') + '</div>');
        if ((headIdle + tailIdle) >= OPEN_SLOT_MIN) warns.push('<div class="alert alert-info py-2 small mb-2"><i class="bx bx-info-circle me-1"></i>' + esc(L.OpenSlotsInfo || 'You still have open time slots.') + '</div>');
        if (el('vp-route-warnings')) el('vp-route-warnings').innerHTML = warns.join('');

        activeDayOrderVal = order;
        el('vp-reset-optimal')?.classList.toggle('d-none', !manualIsUser);
        const host = el('vp-visit-cards'); if (!host) return;
        if (!rows.length) { dayBlocks = []; if (el('vp-day-summary')) el('vp-day-summary').innerHTML = ''; if (el('vp-map-panel')) el('vp-map-panel').innerHTML = ''; host.innerHTML = emptyStateHtml(); return; }

        // Group consecutive same-account visits into a block; a lunch gap splits the block + inserts a break divider.
        const blocks = []; let cur = null; let prevEnd = null;
        rows.forEach(r => {
            const s = parseHM(r.startTime), e = parseHM(r.endTime);
            const acc = r.accountId || r.targetId;
            const lunch = (cur && prevEnd != null) ? isLunch(prevEnd, s) : false;
            if (cur && (acc !== cur.acc || lunch)) { blocks.push(cur); if (lunch) { const lf = Math.max(prevEnd, LUNCH_START), lt = Math.min(s, LUNCH_END); blocks.push({ type: 'break', from: hm(lf), to: hm(lt) }); } cur = null; }
            if (!cur) cur = { type: 'account', acc: acc, name: targetName(r), badge: visitTypeBadge(r), start: r.startTime, end: r.endTime, count: 0, firstOrder: r.sequenceOrder, lastOrder: r.sequenceOrder, visits: [] };
            cur.end = r.endTime; cur.count++; cur.lastOrder = r.sequenceOrder;
            cur.visits.push({ contactId: r.contactId || r.targetContactId || r.targetId || '', start: r.startTime, end: r.endTime, order: r.sequenceOrder, badge: visitTypeBadge(r), name: r.contactDisplayName || '', specialty: r.contactSpecialty || '' });
            if (e != null) prevEnd = e;
        });
        if (cur) blocks.push(cur);
        // Surface genuine idle as danger "boş" cards in the timeline (head before the first visit, tail after the last) —
        // only when long enough to matter. Small between-stop gaps are drive time, not idle, so they get no card. When an
        // idle span crosses the lunch window it is split idle → lunch → idle, so the break still shows inside the free time.
        const idleWithLunch = (a, b) => {
            if (a < LUNCH_END && b > LUNCH_START) {
                const lf = Math.max(a, LUNCH_START), lt = Math.min(b, LUNCH_END), out = [];
                if (lf - a >= 1) out.push({ type: 'idle', from: hm(a), to: hm(lf), mins: lf - a });
                out.push({ type: 'break', from: hm(lf), to: hm(lt) });
                if (b - lt >= 1) out.push({ type: 'idle', from: hm(lt), to: hm(b), mins: b - lt });
                return out;
            }
            return [{ type: 'idle', from: hm(a), to: hm(b), mins: b - a }];
        };
        if (headIdle >= OPEN_SLOT_MIN && fStart != null) idleWithLunch(WORK_START, fStart).reverse().forEach(x => blocks.unshift(x));
        if (tailIdle >= OPEN_SLOT_MIN && lEnd != null) idleWithLunch(lEnd, WORK_END).forEach(x => blocks.push(x));
        renderDaySummary(rows, blocks);
        renderMapPanel(rows, blocks);
        renderBlocks(blocks);
    };

    // Collect the rep's desired target-id sequence for the WHOLE active week from the current render: the active day's
    // blocks in their visual (dragged) order, the other days in their backend order. Sent to the engine as
    // manualVisitOrder — the ENGINE re-schedules in this order honoring availability/hours/lunch/travel/multi-day.
    // activeAccOrder (optional): an explicit account-id order for the active day (from the map's stop-order list). When
    // omitted, the active day's order is read from the timeline's .vp-block DOM order (a card drag).
    const collectManualOrder = activeAccOrder => {
        const byDay = {};
        scheduled.filter(s => s.weekNumber === activeWeek).forEach(s => {
            const d = dayOrder(s.plannedDate);
            (byDay[d] = byDay[d] || []).push({ seq: s.sequenceOrder || 0, tid: s.contactId || s.targetId });
        });
        Object.keys(byDay).forEach(d => byDay[d].sort((a, b) => a.seq - b.seq));
        const activeTargets = [];
        // A linked pharmacy travels WITH its clinic/hospital: when a stop is emitted, its same-day linked pharmacies are
        // appended right after it, and the pharmacy's own standalone block is skipped (placed once). So dragging the
        // hospital moves the pharmacy with it, wherever the pharmacy block currently sits.
        const activeDayPharma = {};
        dayBlocks.forEach(b => { if (selectedPharmacies[b.acc]) activeDayPharma[b.acc] = true; });
        // Each active-day pharmacy is attached to a PARENT clinic so it travels with it on reorder. Prefer the
        // account_relationship (relatedByAccount); fall back to the geographically nearest clinic block (that is exactly
        // how the links were created), which keeps it robust even if the relationship map has not loaded yet.
        const clinicBlocks = dayBlocks.filter(b => !isPharmacyAcc(b.acc));
        const parentByRel = {};
        Object.keys(relatedByAccount).forEach(hid => (relatedByAccount[hid] || []).forEach(ph => { if (selectedPharmacies[ph.id]) parentByRel[ph.id] = hid; }));
        const pharmaOf = {};
        dayBlocks.forEach(b => {
            if (!isPharmacyAcc(b.acc) || !activeDayPharma[b.acc]) return;
            let parent = parentByRel[b.acc];
            if (!parent || !clinicBlocks.some(c => c.acc === parent)) {
                const pc = accCoord(b.acc); let best = null, bd = Infinity;
                clinicBlocks.forEach(c => { const cc = accCoord(c.acc); if (pc && cc) { const d = haversineKm(pc, cc); if (d != null && d < bd) { bd = d; best = c.acc; } } });
                parent = best;
            }
            if (parent) (pharmaOf[parent] = pharmaOf[parent] || []).push(b.acc);
        });
        const placed = {};
        const emit = b => {
            if (!b) return;
            if (selectedPharmacies[b.acc]) { if (!placed[b.acc]) { activeTargets.push(b.acc); placed[b.acc] = true; } return; }
            (b.visits || []).forEach(v => { if (v.contactId) activeTargets.push(v.contactId); });
            (pharmaOf[b.acc] || []).forEach(pid => { if (!placed[pid]) { activeTargets.push(pid); placed[pid] = true; } });
        };
        if (activeAccOrder && activeAccOrder.length) {
            activeAccOrder.forEach(accId => emit(dayBlocks.find(x => x.acc === accId)));
        } else {
            const host = el('vp-visit-cards');
            if (host) host.querySelectorAll('.vp-block').forEach(bl => emit(dayBlocks[parseInt(bl.dataset.idx, 10)]));
        }
        const order = [];
        for (let d = 0; d < 7; d++) {
            if (d === activeDayOrderVal) activeTargets.forEach(t => { if (t) order.push(t); });
            else if (byDay[d]) byDay[d].forEach(x => { if (x.tid) order.push(x.tid); });
        }
        return order;
    };
    // A manual account/doctor reorder re-issues a BACKEND preview with the new order (constraint-honored re-plan).
    const onManualReorder = () => { manualOrder = collectManualOrder(); manualIsUser = true; preview(); };

    // Whole-plan order that keeps each pharmacy right after its PARENT clinic (account_relationship, else the nearest
    // clinic on that day — the way the links were created), clinics kept in the engine's current optimized sequence.
    const buildGroupedOrder = () => {
        const parentByRel = {};
        Object.keys(relatedByAccount).forEach(hid => (relatedByAccount[hid] || []).forEach(ph => { if (selectedPharmacies[ph.id]) parentByRel[ph.id] = hid; }));
        // GLOBAL sequence across all weeks/days (as the engine ordered it) — so a pharmacy can land on its clinic's day
        // even if the engine first scheduled it elsewhere.
        const rows = scheduled.slice().sort((a, b) => (a.weekNumber - b.weekNumber) || (dayOrder(a.plannedDate) - dayOrder(b.plannedDate)) || ((a.sequenceOrder || 0) - (b.sequenceOrder || 0)));
        const seq = [];
        rows.forEach(r => { const acc = r.accountId || r.targetId; if (seq[seq.length - 1] !== acc) seq.push(acc); });
        const clinicList = [];
        seq.forEach(a => { if (!isPharmacyAcc(a) && clinicList.indexOf(a) < 0) clinicList.push(a); });
        // Each pharmacy → its parent clinic: the relationship when that clinic is in the plan; else the nearest plan clinic.
        const pharmaOf = {};
        seq.filter(a => isPharmacyAcc(a)).forEach(pa => {
            let parent = parentByRel[pa];
            if (!parent || clinicList.indexOf(parent) < 0) {
                const pc = accCoord(pa); let best = null, bd = Infinity;
                clinicList.forEach(c => { const cc = accCoord(c); if (pc && cc) { const d = haversineKm(pc, cc); if (d != null && d < bd) { bd = d; best = c; } } });
                parent = best;
            }
            if (parent) (pharmaOf[parent] = pharmaOf[parent] || []).push(pa);
        });
        const order = [], placed = {};
        clinicList.forEach(acc => {
            placed[acc] = true;
            rows.filter(r => (r.accountId || r.targetId) === acc).forEach(r => { const t = r.contactId || r.targetId; if (t) order.push(t); });
            (pharmaOf[acc] || []).forEach(pa => { if (!placed[pa]) { order.push(pa); placed[pa] = true; } });
        });
        seq.filter(a => isPharmacyAcc(a)).forEach(pa => { if (!placed[pa]) { order.push(pa); placed[pa] = true; } });
        return order;
    };
    // One-shot after the first preview: if pharmacies are not already grouped with their clinic, re-plan in the grouped
    // order. Deterministic (grouping is a pure function of the clinic sequence + links), so refreshes stop reshuffling.
    const maybeAutoGroup = () => {
        if (autoGroupDone || manualOrder || !scheduled.length || !Object.keys(selectedPharmacies).length) return;
        autoGroupDone = true;
        // The account_relationship is authoritative for pharmacy→clinic (proximity can pick the wrong clinic), so load
        // every plan clinic's links FIRST (cached — cheap if already fetched), then group.
        const clinicIds = Array.from(new Set(scheduled.map(s => s.accountId || s.targetId).filter(a => a && !isPharmacyAcc(a))));
        Promise.all(clinicIds.map(fetchRelatedPharmacies)).then(() => {
            const grouped = buildGroupedOrder();
            const current = scheduled.slice().sort((a, b) => (a.weekNumber - b.weekNumber) || (dayOrder(a.plannedDate) - dayOrder(b.plannedDate)) || ((a.sequenceOrder || 0) - (b.sequenceOrder || 0))).map(s => s.contactId || s.targetId);
            if (grouped.length && grouped.join(',') !== current.join(',')) { manualOrder = grouped; preview(); }
        });
    };

    // ── Cross-day move (Seçenek 2): drag an account card onto ANOTHER day's tab to move it to that day. ──
    // Re-order the WHOLE week so the dragged block's targets sit at the FRONT of the drop day's group, then re-plan on
    // the backend. The engine fills days in order, so this lands them on that day WHEN IT HAS ROOM (a hard day-pin would
    // need engine day-assignment — backlog). draggingBlockIdx is set by the block Sortable's onStart below.
    let draggingBlockIdx = null;   // the .vp-block being dragged (its dayBlocks index), read by a day-tab drop
    let crossDayMove = false;      // set true by a tab drop so the block Sortable's onEnd skips its within-day reorder
    const moveBlockToDay = (blockIdx, targetDay) => {
        const b = dayBlocks[blockIdx];
        if (!b || !b.visits || targetDay === activeDayOrderVal) return;
        const moved = b.visits.map(v => v.contactId).filter(Boolean);
        if (!moved.length) return;
        const movedSet = new Set(moved);
        const order = collectManualOrder().filter(t => !movedSet.has(t));
        // Insert the moved targets right before the first remaining target already on/after the drop day, so they head
        // that day's group. dayOfTarget maps a target id to the day it is CURRENTLY on (before the move).
        const dayOfTarget = {};
        scheduled.filter(s => s.weekNumber === activeWeek).forEach(s => { const t = s.contactId || s.targetId; if (t && dayOfTarget[t] == null) dayOfTarget[t] = dayOrder(s.plannedDate); });
        let insertAt = order.length;
        for (let i = 0; i < order.length; i++) { const d = dayOfTarget[order[i]]; if (d != null && d >= targetDay) { insertAt = i; break; } }
        order.splice(insertAt, 0, ...moved);
        manualOrder = order;
        manualIsUser = true;
        preview();
    };

    // "Xh YYm" (en) / "Xs YYdk" (tr) from a minute count; under an hour drops the hour part.
    const fmtDur = m => { m = Math.max(0, Math.round(m)); const h = Math.floor(m / 60), mm = m % 60; const H = L.HourAbbrev || 'h', M = L.MinuteAbbrev || 'm'; return h ? (h + H + ' ' + String(mm).padStart(2, '0') + M) : (mm + M); };

    // Per-day report bar: visits, stops, field time (Σ time at stops), free time. Free = the working-window time OUTSIDE
    // the [first start → last end] span, i.e. the unbooked head before the first visit + tail after the last. The gaps
    // BETWEEN stops are drive time (the rep is on the road), and lunch sits inside the span — neither counts as free.
    const renderDaySummary = (rows, blocks) => {
        const host = el('vp-day-summary'); if (!host) return;
        const accBlocks = blocks.filter(b => b.type === 'account');
        let fieldMin = 0, firstStart = null, lastEnd = null;
        accBlocks.forEach(b => {
            const s = parseHM(b.start), e = parseHM(b.end);
            if (s != null && e != null) fieldMin += Math.max(0, e - s);
            if (s != null) firstStart = (firstStart == null) ? s : Math.min(firstStart, s);
            if (e != null) lastEnd = (lastEnd == null) ? e : Math.max(lastEnd, e);
        });
        const free = (firstStart == null || lastEnd == null) ? 0
            : Math.max(0, firstStart - WORK_START) + Math.max(0, WORK_END - lastEnd);
        const seg = (label, val, i) => '<div class="col-6 col-md-3 p-3' + (i ? ' border-start' : '') + '"><div class="text-muted text-uppercase small mb-1">' + esc(label) + '</div><h4 class="mb-0 font-monospace">' + esc(val) + '</h4></div>';
        host.innerHTML = '<div class="card"><div class="card-body p-0"><div class="row g-0">' +
            seg(L.SummaryVisits || 'Visits', rows.length, 0) +
            seg(L.SummaryStops || 'Stops', accBlocks.length, 1) +
            seg(L.SummaryFieldTime || 'Field time', fmtDur(fieldMin), 2) +
            seg(L.SummaryFreeTime || 'Free time', fmtDur(free), 3) +
            '</div></div></div>';
    };

    // ── View preferences (map / visit numbers / drive times) — persisted per browser. ──
    const PREFS_KEY = 'vp-route-prefs';
    const prefs = (() => { try { return Object.assign({ map: true, visitNums: true, driveTimes: true }, JSON.parse(localStorage.getItem(PREFS_KEY) || '{}')); } catch (e) { return { map: true, visitNums: true, driveTimes: true }; } })();
    const savePrefs = () => { try { localStorage.setItem(PREFS_KEY, JSON.stringify(prefs)); } catch (e) { /* private mode */ } };
    const applyPrefs = () => {
        const mapCol = el('vp-map-col'), main = el('vp-route-main'), view = el('vp-route-view');
        if (mapCol) mapCol.classList.toggle('d-none', !prefs.map);
        if (main) { main.classList.toggle('col-lg-8', !!prefs.map); main.classList.toggle('col-lg-12', !prefs.map); }
        if (view) { view.classList.toggle('vp-hide-visitnums', !prefs.visitNums); view.classList.toggle('vp-hide-drivetimes', !prefs.driveTimes); }
        const set = (id, v) => { const n = el(id); if (n) n.checked = v; };
        set('vp-toggle-map', prefs.map); set('vp-toggle-visitnums', prefs.visitNums); set('vp-toggle-drivetimes', prefs.driveTimes);
    };

    // ── Map panel: a real Leaflet + OSM map (same vendored stack as the Account/Contact pickers) with numbered stop
    // markers + a route polyline, total drive time, and a reorderable stop list. ──
    const accCoord = accId => { const a = accountSource.find(x => x.id === accId); if (!a) return null; const la = (typeof a.lat === 'number') ? a.lat : null, ln = (typeof a.lng === 'number') ? a.lng : null; return (la != null && ln != null) ? { lat: la, lng: ln } : null; };
    let mapInstance = null, mapLatLngs = null;
    const themePrimary = () => (getComputedStyle(document.body).getPropertyValue('--bs-primary') || '#696cff').trim() || '#696cff';
    // Toggle native fullscreen on the map canvas; Leaflet is re-sized by the fullscreenchange listener (wired once).
    const toggleMapFullscreen = () => {
        const c = el('vp-map-canvas'); if (!c) return;
        if (document.fullscreenElement) { document.exitFullscreen?.(); }
        else { (c.requestFullscreen || c.webkitRequestFullscreen)?.call(c); }
    };
    // Reorder from the map: a marker dropped near a stop is re-sequenced right after that nearest stop, then the engine
    // re-plans in the new order (constraint-honored). Pharmacies still travel with their clinic via collectManualOrder.
    const reorderFromMarker = (accOrder, draggedAcc, latlng) => {
        const rest = accOrder.filter(a => a !== draggedAcc);
        let bestI = -1, bestD = Infinity;
        rest.forEach((acc, i) => { const c = accCoord(acc); if (!c) return; const d = haversineKm({ lat: latlng.lat, lng: latlng.lng }, c); if (d != null && d < bestD) { bestD = d; bestI = i; } });
        rest.splice(bestI + 1, 0, draggedAcc);
        manualOrder = collectManualOrder(rest); manualIsUser = true; preview();
    };
    // Build (or rebuild) the Leaflet map into #vp-map-canvas from the active day's stops. Old instance is disposed first.
    const buildRouteMap = accBlocks => {
        if (mapInstance) { try { mapInstance.remove(); } catch (e) { /* already gone */ } mapInstance = null; }
        const div = el('vp-map-canvas'); if (!div) return;
        const pts = accBlocks.map((b, i) => ({ n: i + 1, name: b.name, acc: b.acc, c: accCoord(b.acc) })).filter(p => p.c);
        if (!window.L || !pts.length) {
            div.innerHTML = '<div class="text-muted small text-center d-flex align-items-center justify-content-center h-100">' + esc(L.MapNoCoords || 'No coordinates to plot') + '</div>';
            return;
        }
        div.innerHTML = '';
        const map = window.L.map(div, { zoomControl: true, attributionControl: true, scrollWheelZoom: false });
        mapInstance = map;
        window.L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(map);
        const latlngs = pts.map(p => [p.c.lat, p.c.lng]);
        mapLatLngs = latlngs;
        if (pts.length > 1) window.L.polyline(latlngs, { color: themePrimary(), weight: 3, opacity: .75, dashArray: '5 5' }).addTo(map);
        const accOrder = accBlocks.map(b => b.acc);
        pts.forEach(p => {
            const ph = isPharmacyAcc(p.acc);
            const icon = window.L.divIcon({ className: 'vp-map-pin' + (ph ? ' vp-map-pin--pharmacy' : ''), html: '<span>' + p.n + '</span>', iconSize: [26, 26], iconAnchor: [13, 13] });
            // Only clinic/hospital markers drag (and only when the plan is editable); a pharmacy follows its clinic.
            const drag = !!canApply && !ph;
            const m = window.L.marker([p.c.lat, p.c.lng], { icon: icon, draggable: drag }).addTo(map).bindTooltip(p.n + '. ' + p.name, { direction: 'top' });
            if (drag) m.on('dragend', () => reorderFromMarker(accOrder, p.acc, m.getLatLng()));
        });
        // Fullscreen control (Leaflet bar button, top-right).
        const FsControl = window.L.Control.extend({
            onAdd: function () {
                const a = window.L.DomUtil.create('a', 'leaflet-bar leaflet-control vp-map-fs');
                a.href = '#'; a.title = L.MapFullscreen || 'Full screen'; a.setAttribute('role', 'button');
                a.innerHTML = '<i class="bx bx-fullscreen"></i>';
                window.L.DomEvent.on(a, 'click', window.L.DomEvent.stop).on(a, 'click', toggleMapFullscreen);
                return a;
            }
        });
        map.addControl(new FsControl({ position: 'topright' }));
        const bounds = window.L.latLngBounds(latlngs);
        const fit = () => { try { map.invalidateSize(); if (pts.length === 1) map.setView(latlngs[0], 14); else map.fitBounds(bounds, { padding: [24, 24], maxZoom: 15 }); } catch (e) { /* hidden container */ } };
        fit();
        setTimeout(fit, 80); // container may have just gained its size
    };
    // Re-fit after the map panel is revealed (a hidden Leaflet container renders at 0×0 until invalidated).
    const refreshMap = () => { if (!mapInstance) return; setTimeout(() => { try { mapInstance.invalidateSize(); if (mapLatLngs && mapLatLngs.length) mapInstance.fitBounds(window.L.latLngBounds(mapLatLngs), { padding: [24, 24], maxZoom: 15 }); } catch (e) { /* noop */ } }, 60); };
    const currentStopAccs = () => { const p = el('vp-map-panel'); return p ? Array.prototype.slice.call(p.querySelectorAll('.vp-stop')).map(li => li.dataset.acc) : []; };
    const applyStopOrder = () => { manualOrder = collectManualOrder(currentStopAccs()); manualIsUser = true; preview(); };
    const wireStopList = () => {
        const panel = el('vp-map-panel'); const list = panel && panel.querySelector('.vp-stop-list'); if (!list) return;
        if (window.Sortable) window.Sortable.create(list, { handle: '.vp-stop-handle', animation: 150, ghostClass: 'vp-block-ghost', onEnd: applyStopOrder });
        const move = (li, ref, before) => { if (ref) { li.parentNode.insertBefore(before ? li : ref, before ? ref : li); applyStopOrder(); } };
        list.querySelectorAll('.vp-stop-up').forEach(btn => btn.addEventListener('click', () => { const li = btn.closest('.vp-stop'); move(li, li.previousElementSibling, true); }));
        list.querySelectorAll('.vp-stop-down').forEach(btn => btn.addEventListener('click', () => { const li = btn.closest('.vp-stop'); move(li, li.nextElementSibling, false); }));
    };
    const renderMapPanel = (rows, blocks) => {
        const host = el('vp-map-panel'); if (!host) return;
        const accBlocks = blocks.filter(b => b.type === 'account');
        let travelTot = 0; for (let i = 1; i < accBlocks.length; i++) { const tm = travelMin(accBlocks[i - 1].acc, accBlocks[i].acc); if (tm != null) travelTot += tm; }
        // Stop rows reuse the WorkCenterNext checklist row look (.diten-checkitem: bordered box + grip + stacked move
        // chevrons + text), keeping the vp-stop* hooks that wireStopList / Sortable drive.
        // A pharmacy stop is LOCKED: it belongs to its clinic and moves with it, so it gets no grip and no arrows
        // (a lock glyph instead). Only clinic/hospital stops are reorderable; the pharmacy follows via collectManualOrder.
        const stopRows = accBlocks.map((b, i) => {
            const ph = isPharmacyAcc(b.acc);
            return '<li class="diten-checkitem vp-stop' + (ph ? ' vp-stop--locked' : '') + '" data-acc="' + esc(b.acc) + '">' +
                (ph
                    ? '<span class="diten-checkitem-grip" aria-hidden="true" style="cursor:default;opacity:.4;"><i class="bx bx-lock-alt"></i></span>'
                    : '<span class="diten-checkitem-grip vp-stop-handle" aria-hidden="true"><i class="bx bx-grid-vertical"></i></span>' +
                      '<span class="diten-checkitem-move">' +
                      '<button type="button" class="diten-checkitem-btn vp-stop-up" aria-label="up"><i class="bx bx-chevron-up"></i></button>' +
                      '<button type="button" class="diten-checkitem-btn vp-stop-down" aria-label="down"><i class="bx bx-chevron-down"></i></button>' +
                      '</span>') +
                '<span class="badge ' + (ph ? 'bg-label-warning' : 'bg-label-primary') + ' flex-shrink-0">' + (i + 1) + '</span>' +
                '<span class="diten-checkitem-text">' + esc(b.name) + '</span>' +
                '<span class="badge bg-label-secondary flex-shrink-0">' + b.count + '</span>' +
                '</li>';
        }).join('');
        host.innerHTML =
            '<div class="card"><div class="card-body">' +
            '<div class="text-muted text-uppercase small mb-2">' + esc(L.MapView || 'Map view') + '</div>' +
            '<div class="vp-map-canvas rounded mb-3" id="vp-map-canvas"></div>' +
            '<div class="d-flex align-items-center justify-content-between border-top pt-3 mb-3">' +
            '<span class="text-muted small">' + esc(L.TotalDriveTime || 'Total drive time') + '</span>' +
            '<span class="fw-medium">' + (travelTot ? ('~' + travelTot + ' ' + (L.MinuteAbbrev || 'm')) : '—') + '</span></div>' +
            '<div class="d-flex align-items-center justify-content-between mb-2"><span class="fw-semibold small">' + esc(L.StopOrder || 'Stop order') + '</span><span class="text-muted" style="font-size:.72rem;">' + esc(L.DragOrArrows || 'drag or use arrows') + '</span></div>' +
            '<ul class="list-unstyled mb-0 d-flex flex-column gap-2 vp-stop-list">' + (stopRows || '<li class="text-muted small py-2">—</li>') + '</ul>' +
            '</div></div>';
        wireStopList();
        buildRouteMap(accBlocks);
    };

    // Render a [account | break | account …] block list: travel dividers between consecutive accounts, lunch dividers
    // from break entries, each account as an inbox-row card. Populates dayBlocks (account blocks only, index-addressable).
    const renderBlocks = blocks => {
        const host = el('vp-visit-cards'); if (!host) return;
        dayBlocks = [];
        const parts = []; let prevAcc = null; let pendingBreak = null;
        // A timeline row = [time gutter | rail+dot | body]. The body keeps the EXISTING card/divider markup untouched;
        // only account rows are draggable (data-idx + .vp-tl-row--account), so SortableJS still finds them as direct children.
        const dot = cls => '<span class="vp-tl-dot' + (cls ? ' ' + cls : '') + '"></span>';
        const tlRow = (time, dotHtml, bodyHtml, extraCls, idxAttr) =>
            '<div class="vp-tl-row' + (extraCls ? ' ' + extraCls : '') + '"' + (idxAttr != null ? ' data-idx="' + idxAttr + '"' : '') + '>' +
            '<div class="vp-tl-time">' + (time ? esc(time) : '') + '</div>' +
            '<div class="vp-tl-rail">' + (dotHtml || '') + '</div>' +
            '<div class="vp-tl-body">' + bodyHtml + '</div></div>';
        const lunchDivider = brk => '<div class="vp-route-divider vp-route-divider--lunch"><i class="bx bx-restaurant"></i><span>' + esc(L.LunchBreak || 'Öğle arası') + ' · ' + esc(brk.from) + '–' + esc(brk.to) + '</span></div>';
        const travelDivider = leg => '<div class="vp-tl-drive text-muted small"><i class="bx ' + (leg.walk ? 'bx-walk' : 'bx-car') + ' me-1"></i>~' + leg.min + ' ' + esc(leg.walk ? (L.WalkMinutes || 'dk yürüme') : (L.TravelMinutes || 'dk yol')) + '</div>';
        const idleCard = b => '<div class="vp-route-idle"><span class="fw-medium">' + esc(b.from) + '–' + esc(b.to) + '</span><span class="ms-2">' + esc(L.FreeSlotLabel || 'free') + ' · ' + esc(fmtDur(b.mins)) + '</span></div>';
        blocks.forEach(b => {
            // A lunch break is deferred so the travel divider (below) is drawn FIRST — the rep drives, then eats, then
            // arrives — instead of showing lunch ahead of the road it overlaps.
            if (b.type === 'break') { pendingBreak = b; return; }
            if (b.type === 'idle') { if (pendingBreak) { parts.push(tlRow(pendingBreak.from, dot('vp-tl-dot--lunch'), lunchDivider(pendingBreak), 'vp-tl-row--lunch')); pendingBreak = null; } parts.push(tlRow(b.from, dot('vp-tl-dot--idle'), idleCard(b), 'vp-tl-row--idle')); return; }
            if (prevAcc && prevAcc !== b.acc) {
                const leg = travelLeg(prevAcc, b.acc);
                if (leg) parts.push(tlRow('', '', travelDivider(leg), 'vp-tl-row--travel'));
            }
            if (pendingBreak) { parts.push(tlRow(pendingBreak.from, dot('vp-tl-dot--lunch'), lunchDivider(pendingBreak), 'vp-tl-row--lunch')); pendingBreak = null; }
            const idx = dayBlocks.length; dayBlocks.push(b);
            parts.push(tlRow(b.start, dot(''), accountBlockHtml(b, idx), 'vp-tl-row--account' + (isPharmacyAcc(b.acc) ? ' vp-tl-row--pharmacy' : ''), idx));
            prevAcc = b.acc;
        });
        if (pendingBreak) parts.push(tlRow(pendingBreak.from, dot('vp-tl-dot--lunch'), lunchDivider(pendingBreak), 'vp-tl-row--lunch'));
        host.innerHTML = parts.join('');
        // Single-visit stops show their one contact inline inside the account card — fill those holders straight away.
        dayBlocks.forEach((b, i) => {
            if (b.count !== 1) return;
            const holder = host.querySelector('.vp-inline-contact[data-idx="' + i + '"]');
            if (holder) fillInlineContact(i, holder);
        });
    };

    // Fill a single-visit account card's inline contact line: avatar + name + specialty (no separate card).
    const fillInlineContact = (idx, holder) => {
        const block = dayBlocks[idx]; if (!block || !holder) return;
        const v = block.visits[0]; if (!v) return;
        // Paint immediately from the plan payload (link-independent name/specialty); a photo, if any, arrives via the
        // account-contacts fetch and re-paints. When the plan lacked a name (old backend), the fetch fills it in.
        const paint = (photo, fbName, fbSpec) => {
            const name = v.name || fbName || (L.ColContact || 'Doctor');
            const specialty = v.specialty || fbSpec || '';
            const av = '<div class="avatar avatar-xs flex-shrink-0">' +
                (photo ? '<img src="' + esc(photo) + '" alt="" class="rounded-circle">' : '<span class="avatar-initial rounded-circle bg-label-secondary"><i class="bx bx-user"></i></span>') + '</div>';
            const spec = specialty ? '<span class="badge bg-label-info text-uppercase">' + esc(specialty) + '</span>' : '';
            holder.innerHTML = av + '<span class="fw-medium small text-truncate">' + esc(name) + '</span>' + spec;
        };
        paint('', '', '');
        fetchAccountContacts(block.acc).then(() => {
            const c = (contactsByAccount[block.acc] || []).find(x => x.contactId === v.contactId) || {};
            if (c.photo || (!v.name && c.name)) paint(c.photo || '', c.name, c.specialty);
        });
    };

    // Account block → inbox-row card. Whole card is the expand toggle; a chevron mirrors the state. The doctor visits
    // render lazily into the sibling .vp-block-detail on first expand.
    const accTypeOf = accId => { const a = accountSource.find(x => x.id === accId); return a ? (a.type || '') : ''; };
    // A pharmacy is bound to its clinic and is NOT independently reorderable — it always travels with its clinic.
    const isPharmacyAcc = accId => !!selectedPharmacies[accId] || accTypeOf(accId) === 'pharmacy';
    const accountBlockHtml = (b, idx) => {
        const at = accTypeOf(b.acc);
        const typeBadge = at ? '<span class="badge inbox-row__type inbox-row__badge-outline inbox-row__badge--type-default flex-shrink-0">' + esc(at) + '</span>' : '';
        const countBadge = '<span class="badge bg-label-secondary flex-shrink-0">' + b.count + ' ' + esc(L.VisitsWord || 'ziyaret') + '</span>';
        const orderTxt = b.count > 1 ? ('#' + esc(b.firstOrder) + '–' + esc(b.lastOrder)) : ('#' + esc(b.firstOrder));
        // A single-visit stop has nothing to fold open — no chevron, not a toggle; its one contact is shown INLINE inside
        // this card (an extra line, filled after render), with NO separate contact card. Multi-visit stops keep the chevron
        // and their expandable .vp-block-detail list.
        const single = b.count === 1;
        // An account-level stop (a pharmacy target: contactId falls back to the account id) has no doctor to show inline.
        const v0 = b.visits && b.visits[0];
        const hasContact = single && v0 && v0.contactId && v0.contactId !== b.acc;
        const chevron = single ? '' : '<i class="bx bx-chevron-right vp-block-chevron flex-shrink-0"></i>';
        const inlineContact = hasContact ? '<div class="inbox-row__line d-flex align-items-center gap-2 mt-1 vp-inline-contact" data-idx="' + idx + '" style="order:2;"></div>' : '';
        // Single-visit cards stack account → contact → time; multi-visit keep the default account → time.
        const timeOrder = single ? ' style="order:3;"' : '';
        // A pharmacy is locked to its clinic: no drag grip (a lock glyph), so it can only move by moving its clinic.
        const locked = isPharmacyAcc(b.acc);
        const grip = locked
            ? '<i class="bx bx-lock-alt text-muted flex-shrink-0" style="opacity:.5;" aria-hidden="true"></i>'
            : '<i class="bx bx-grid-vertical text-muted vp-block-handle flex-shrink-0" role="button" aria-label="reorder" style="cursor:grab;"></i>';
        return '<article class="inbox-row p-3 vp-block" data-acc="' + esc(b.acc) + '" data-idx="' + idx + '"' + (single ? '' : ' role="button" style="cursor:pointer;"') + '>' +
            '<div class="inbox-row__main">' +
            '<div class="inbox-row__line inbox-row__line--primary d-flex align-items-center gap-2 flex-wrap">' +
            grip +
            chevron +
            '<h5 class="inbox-row__title mb-0 text-truncate">' + esc(b.name) + '</h5>' + typeBadge + countBadge +
            '</div>' +
            '<div class="inbox-row__line inbox-row__line--secondary text-muted"' + timeOrder + '><span class="inbox-row__meta-item"><i class="bx bx-time-five inbox-row__calendar-icon"></i><span>' + esc(b.start) + '–' + esc(b.end) + '</span></span></div>' +
            inlineContact +
            '</div>' +
            '<div class="inbox-row__actions d-flex flex-column align-items-end gap-1 flex-shrink-0">' + b.badge + '<div class="text-muted small vp-visit-num">' + orderTxt + '</div></div>' +
            '</article>' +
            (single ? '' : '<div class="vp-block-detail ms-4 mt-2 mb-2 d-flex flex-column gap-2 d-none" data-idx="' + idx + '"></div>');
    };

    const visitCardHtml = (v, infoMap) => {
        const info = infoMap[v.contactId] || {};
        // Name/specialty come from the plan payload (link-independent); account-contacts is only a fallback + the photo.
        const dn = v.name || info.name || (L.ColContact || 'Doctor');
        const specialty = v.specialty || info.specialty;
        const spec = specialty ? '<span class="badge bg-label-info text-uppercase">' + esc(specialty) + '</span>' : '';
        // Avatar: the contact photo when present, else a person-icon placeholder.
        const avatar = '<div class="avatar avatar-sm me-2 flex-shrink-0">' +
            (info.photo ? '<img src="' + esc(info.photo) + '" alt="" class="rounded-circle">' : '<span class="avatar-initial rounded-circle bg-label-secondary"><i class="bx bx-user"></i></span>') +
            '</div>';
        return '<article class="inbox-row p-2 vp-visit" data-cid="' + esc(v.contactId) + '">' +
            '<div class="me-2 d-flex align-items-center flex-shrink-0"><i class="bx bx-grid-vertical text-muted vp-visit-handle" role="button" aria-label="reorder" style="cursor:grab;"></i></div>' +
            avatar +
            '<div class="inbox-row__main">' +
            '<div class="inbox-row__line inbox-row__line--primary d-flex align-items-center gap-2 flex-wrap">' +
            '<span class="badge bg-label-secondary vp-visit-order vp-visit-num flex-shrink-0">#' + esc(v.order) + '</span>' +
            '<h6 class="inbox-row__title mb-0 text-truncate">' + esc(dn) + '</h6>' +
            '</div>' +
            '<div class="inbox-row__line inbox-row__line--secondary text-muted"><span class="inbox-row__meta-item"><i class="bx bx-time-five inbox-row__calendar-icon"></i><span>' + esc(v.start) + '–' + esc(v.end) + '</span></span></div>' +
            '</div>' +
            '<div class="inbox-row__actions flex-shrink-0">' + spec + '</div>' +
            '</article>';
    };

    // Lazily build a block's doctor cards (resolve contactId→name+specialty) + wire SortableJS drag-reorder.
    const buildBlockDetail = (idx, detail) => {
        const block = dayBlocks[idx]; if (!block || !detail) return;
        fetchAccountContacts(block.acc).then(() => {
            const infoMap = (contactsByAccount[block.acc] || []).reduce((m, c) => { m[c.contactId] = { name: c.name, specialty: c.specialty, photo: c.photo }; return m; }, {});
            detail.innerHTML = block.visits.map(v => visitCardHtml(v, infoMap)).join('');
            wireBlockSortable(idx, detail);
        });
    };

    const wireBlockSortable = (idx, detail) => {
        if (!window.Sortable) return; // SortableJS included per-page on Details; if absent, cards just don't drag.
        window.Sortable.create(detail, {
            handle: '.vp-visit-handle', animation: 150, ghostClass: 'vp-visit-ghost',
            onEnd: () => {
                // Reorder the block's visits to the new DOM order, then re-issue a BACKEND preview with the manual order —
                // the ENGINE re-schedules (availability/hours/lunch/travel/multi-day), not the client.
                const block = dayBlocks[idx]; if (!block) return;
                const cards = Array.prototype.slice.call(detail.querySelectorAll('.vp-visit'));
                const reordered = cards.map(card => block.visits.find(x => String(x.contactId) === card.dataset.cid)).filter(Boolean);
                if (reordered.length) block.visits = reordered;
                onManualReorder();
            }
        });
    };

    const renderWeek = week => {
        activeWeek = week;
        setWeekLabel(week);
        const weekRows = scheduled.filter(s => s.weekNumber === week);
        const mon = weekMonday(week);

        // header range = Mon → Fri actual dates
        const range = el('vp-route-range');
        if (range) {
            if (mon) {
                const fri = new Date(mon.getTime() + 4 * 86400000);
                const dd = d => d.toLocaleDateString(undefined, { day: '2-digit' });
                range.textContent = dd(mon) + '–' + dd(fri) + ' ' + fri.toLocaleDateString(undefined, { month: 'short' }) + ' · ' + (L.YearLabel || 'Year') + ' ' + mon.getFullYear() + ' · ' + weekNumberLabel(isoWeek(mon));
            } else { range.textContent = isoLabelOf(week); }
        }

        // day tabs: Mon-first, weekends hidden, holidays disabled
        const tabs = el('vp-day-tabs');
        const rendered = [];
        if (tabs && mon) {
            let html = '';
            for (let i = 0; i < 7; i++) {
                const date = new Date(mon.getTime() + i * 86400000);
                const wd = date.getDay();
                if (wcWeekend.has(wd)) continue; // weekend hidden
                const isHoliday = wcHolidays.has(ymd(date));
                rendered.push({ order: i, disabled: isHoliday });
                const chipLabel = dayAbbr(wd) + ' ' + date.getDate() + ' ' + date.toLocaleDateString('en-US', { month: 'short' }) + ', ' + String(date.getFullYear()).slice(-2);
                html += '<li class="nav-item mb-1 mb-sm-0"><button type="button" class="nav-link small border shadow-none wc-tab-compact' + (isHoliday ? ' disabled' : '') + '" data-day="' + i + '"' + (isHoliday ? ' aria-disabled="true"' : '') + '>' +
                    esc(chipLabel) + (isHoliday ? ' <span class="badge bg-label-secondary ms-1">' + esc(L.HolidayMarker || 'holiday') + '</span>' : '') + '</button></li>';
            }
            tabs.innerHTML = html;
        }

        // active day = the day we were on (kept across a re-preview), else today, else the first enabled day.
        const todayOrder = (new Date().getDay() + 6) % 7;
        const enabled = rendered.filter(d => !d.disabled);
        let active = enabled.some(d => d.order === activeDayOrderVal) ? activeDayOrderVal
            : (enabled.some(d => d.order === todayOrder) ? todayOrder : (enabled.length ? enabled[0].order : null));
        if (tabs && active != null) tabs.querySelectorAll('.nav-link').forEach(b => b.classList.toggle('active', parseInt(b.dataset.day, 10) === active));

        if (active != null) renderDay(active);
        else { setText('vp-day-plan-title', '—'); if (el('vp-visit-cards')) el('vp-visit-cards').innerHTML = '<div class="text-muted">' + esc(L.RouteEmpty || L.NoPreviewYet || '') + '</div>'; if (el('vp-route-warnings')) el('vp-route-warnings').innerHTML = ''; }
    };

    const buildWeekSelector = () => {
        const sel = el('vp-week'); if (!sel) return;
        const weeks = weeksOf(scheduled);
        if (weeks.length <= 1) { sel.classList.add('d-none'); return; }
        sel.classList.remove('d-none');
        sel.innerHTML = weeks.map(w => '<option value="' + w + '">' + esc(isoLabelOf(w)) + '</option>').join('');
        sel.value = String(defaultWeek(scheduled));
        sel.onchange = () => { const w = parseInt(sel.value, 10); const mon = weekMonday(w); loadWorkingCalendar(mon ? mon.getFullYear() : new Date().getFullYear()).then(() => renderWeek(w)); };
    };

    const renderPreview = p => {
        lastPreview = p;
        const sd = p.supplyDemand || {};
        const badge = el('vp-supply-badge');
        if (badge) { badge.textContent = sd.status || '—'; badge.className = 'badge ' + (sd.status === 'over-planned' ? 'bg-label-warning' : 'bg-label-success'); }
        setText('vp-sd-supply', sd.supply == null ? '—' : sd.supply);
        setText('vp-sd-demand', sd.demand == null ? '—' : sd.demand);
        setText('vp-sd-scheduled', sd.scheduledCount || 0);
        setText('vp-sd-unscheduled', sd.unscheduledCount || 0);
        if (el('vp-territory-warnings')) el('vp-territory-warnings').innerHTML = (p.territoryWarnings || []).map(w =>
            '<span class="badge bg-label-warning me-1">' + (L.OutOfTerritory || 'Out of territory') + ': ' + esc(String(w.accountId || '').slice(0, 8)) + '</span>').join('');

        scheduled = p.scheduled || [];
        // Keep the current week on a re-preview (manual reorder); pick the default only on the first render.
        const weeks = weeksOf(scheduled);
        const def = (activeDayOrderVal != null && weeks.indexOf(activeWeek) > -1) ? activeWeek
            : (scheduled.length ? defaultWeek(scheduled) : 0);
        const mon = weekMonday(def);
        buildWeekSelector();
        loadWorkingCalendar(mon ? mon.getFullYear() : new Date().getFullYear()).then(() => renderWeek(def));
    };

    const preview = () => {
        setStatus(L.Loading || '…');
        const body = { planningSessionId: sessionId };
        if (manualOrder && manualOrder.length) body.manualVisitOrder = manualOrder; // engine honors this sequence
        return api('/preview', { method: 'POST', body: JSON.stringify(body) }).then(r => {
            if (r.ok && r.body && r.body.data) { renderPreview(r.body.data); setStatus(''); maybeAutoGroup(); }
            else { setStatus(L.PreviewFailed || errorText(r), true); }
        });
    };

    // ── Targets master-detail DataTables ──
    let accountSource = [];
    let targetAccounts = [];
    const contactsByAccount = {};
    const selectedContacts = {}; // "accId|conId" -> {contactId, accountId, accountContactLinkId}
    const selectedPharmacies = {}; // pharmacyId -> {id, name} — flat set; written to selectedPharmacyIds on save
    const relatedByAccount = {};   // accountId -> [{id, name, relType}] linked pharmacies (cached)
    let activeAccountId = null;
    let accountsDt = null, contactsDt = null;
    const selKey = (a, c) => a + '|' + c;
    const accName = a => a.accountName || a.name || a.id;
    const accType = a => a.accountType || a.type || '';
    const accLat = a => { const v = (a.latitude != null ? a.latitude : a.Latitude); return typeof v === 'number' ? v : null; };
    const accLng = a => { const v = (a.longitude != null ? a.longitude : a.Longitude); return typeof v === 'number' ? v : null; };
    // City + MOD-0151 territory-node coverage projection. The account list DTO exposes territoryNodeName / cityRef
    // (camelCase in JSON); the old keys (city/territoryName) never matched, so the column read blank.
    const accCity = a => {
        const terr = a.territoryNodeName || a.TerritoryNodeName || a.territoryNodeCode || '';
        const rawCity = a.cityRef || a.CityRef || a.city || a.cityName || (a.address && a.address.city) || '';
        const city = (typeof rawCity === 'string' && rawCity.indexOf('-') > -1) ? rawCity.split('-').pop() : rawCity; // "TR-55-SAMSUN" → "SAMSUN"
        return [city, terr].filter(Boolean).join(' · ');
    };
    // Street address for a pharmacy card: AddressLine + district when present.
    const accAddr = a => {
        const line = a.addressLine || a.AddressLine || '';
        const dist = a.districtRef || a.DistrictRef || '';
        const d = (typeof dist === 'string' && dist.indexOf('-') > -1) ? dist.split('-').pop() : dist;
        return [line, d].filter(Boolean).join(', ');
    };

    // No bulk pre-load — 16k+ clinics/hospitals never fit a client picker (the old 500+500 cap is exactly why "not all
    // accounts show"). The add picker searches the FULL universe server-side (below) and caches each result into
    // accountSource; edit-preselect resolves saved ids from that cache (id fallback when a saved account isn't cached).
    const loadAccountSource = () => Promise.resolve();

    // Server-side searchable select2: type a name → GET /accounts?search=… (all clinics/hospitals), cache the hits so
    // addAccount() can resolve the pick. Initialised once; later calls just clear the current selection.
    const fillAddAccountPicker = () => {
        const sel = el('vp-add-account'); if (!sel || !window.jQuery || !window.jQuery.fn.select2) return;
        const $s = window.jQuery(sel);
        if (sel.dataset.ajaxInit === '1') { $s.val(null).trigger('change.select2'); return; }
        sel.dataset.ajaxInit = '1';
        if (!sel.querySelector('option')) { sel.innerHTML = '<option value=""></option>'; }
        $s.select2({
            width: '100%', dropdownParent: window.jQuery(document.body), placeholder: sel.dataset.placeholder || '',
            minimumInputLength: 1,
            ajax: {
                delay: 250,
                transport: function (params, success, failure) {
                    api('/accounts?search=' + encodeURIComponent((params.data && params.data.term) || '') + '&pageSize=30')
                        .then(success).catch(failure);
                },
                processResults: function (r) {
                    const targeted = {}; targetAccounts.forEach(function (t) { targeted[t.id] = true; });
                    const results = [];
                    listItems(r && r.body).forEach(function (a) {
                        const id = a.accountId || a.id; if (!id || targeted[id]) return;
                        const type = String(accType(a)).toLowerCase();
                        if (type && CLINIC_TYPES.indexOf(type) === -1) return;
                        const item = { id: id, name: accName(a), type: accType(a), city: accCity(a), lat: accLat(a), lng: accLng(a) };
                        if (!accountSource.find(function (x) { return x.id === id; })) accountSource.push(item);
                        results.push({ id: id, text: item.name + (item.type ? ' — ' + item.type : '') });
                    });
                    return { results: results };
                }
            }
        });
    };

    const accountsConfig = () => ({
        data: targetAccounts, stateSave: false, searching: true, paging: true, pageLength: 10, lengthChange: false, info: true,
        buttons: [], // inline picker: no Action dropdown / column-visibility toolbar
        columns: [{ data: 'name' }, { data: 'type' }, { data: 'city' }, { data: null }],
        columnDefs: [
            { targets: 0, render: (v, t) => t === 'display' ? '<span class="fw-medium text-heading">' + esc(v) + '</span>' : (v || '') },
            { targets: 1, render: v => v ? '<span class="badge bg-label-info">' + esc(v) + '</span>' : '—' },
            { targets: 2, render: v => esc(v || '—') },
            { targets: 3, orderable: false, searchable: false, className: 'cell-fit text-end', render: (v, t, row) => canGenerate ? '<button type="button" class="btn btn-sm btn-icon btn-label-danger js-remove-account" data-id="' + esc(row.id) + '" title="' + esc(L.RemoveTarget || 'Remove') + '"><i class="bx bx-x"></i></button>' : '' }
        ],
        language: { emptyTable: L.NoTargetAccounts || '—' }
    });

    const buildAccountsDt = () => {
        const tableEl = el('dt-vp-accounts'); if (!tableEl) return;
        if (accountsDt) { accountsDt.clear(); accountsDt.rows.add(targetAccounts).draw(false); return; }
        accountsDt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(accountsConfig()) : accountsConfig());
        tableEl.addEventListener('click', e => {
            const rm = e.target.closest('.js-remove-account');
            if (rm) { e.stopPropagation(); removeAccount(rm.dataset.id); return; }
            const tr = e.target.closest('tbody tr'); if (!tr) return;
            const data = accountsDt.row(tr).data(); if (data && data.id) showContacts(data.id);
        });
    };

    // "general-surgery" / "general_surgery" → "General Surgery".
    const prettify = s => String(s || '').replace(/[-_]+/g, ' ').replace(/\s+/g, ' ').trim().replace(/\b\w/g, ch => ch.toUpperCase());
    const contactsConfig = list => ({
        data: list, stateSave: false, searching: true, paging: true, pageLength: 10, lengthChange: false, info: true,
        buttons: [], // inline picker: no Action dropdown / column-visibility toolbar
        columns: [{ data: null }, { data: 'name' }, { data: 'specialty' }, { data: 'linkId' }],
        columnDefs: [
            { targets: 0, orderable: false, className: 'cell-fit', render: (v, t, row) => '<div class="form-check mb-0"><input class="form-check-input js-contact-check" type="checkbox" data-cid="' + esc(row.contactId) + '"' + (selectedContacts[selKey(activeAccountId, row.contactId)] ? ' checked' : '') + (canGenerate ? '' : ' disabled') + '></div>' },
            { targets: 1, render: (v, t) => t === 'display' ? '<span class="fw-medium">' + esc(v) + '</span>' : (v || '') },
            { targets: 2, render: (v, t) => t === 'display' ? (v ? '<span class="badge bg-label-info">' + esc(prettify(v)) + '</span>' : '—') : (v || '') },
            { targets: 3, render: v => v ? '<span class="text-muted small font-monospace">' + esc(String(v).slice(0, 8)) + '</span>' : '—' }
        ],
        language: { emptyTable: L.PickAccountForContacts || '—' }
    });

    const renderContactsDt = list => {
        const tableEl = el('dt-vp-contacts'); if (!tableEl) return;
        if (contactsDt) { contactsDt.destroy(); contactsDt = null; tableEl.querySelector('tbody')?.remove(); }
        contactsDt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(contactsConfig(list)) : contactsConfig(list));
        if (!tableEl.dataset.bound) {
            tableEl.dataset.bound = '1';
            tableEl.addEventListener('change', e => {
                const cb = e.target.closest('.js-contact-check'); if (!cb || !activeAccountId) return;
                const cid = cb.dataset.cid; const k = selKey(activeAccountId, cid);
                if (cb.checked) {
                    const c = (contactsByAccount[activeAccountId] || []).find(x => x.contactId === cid);
                    selectedContacts[k] = { contactId: cid, accountId: activeAccountId, accountContactLinkId: (c && c.linkId) || null };
                } else { delete selectedContacts[k]; }
                refreshTargetsUi();
            });
        }
    };

    // Fetch (and cache) one account's linked contacts as [{contactId, name, specialty, linkId}]. Shared by the Targets
    // detail table and the Route-tab block expansion (doctor-name resolution). Fail-soft → [].
    const fetchAccountContacts = accountId => {
        if (contactsByAccount[accountId]) return Promise.resolve(contactsByAccount[accountId]);
        return api('/accounts/' + accountId + '/contacts?pageSize=500').then(r => {
            const seen = {}; const list = [];
            listItems(r.body).forEach(row => {
                const cid = row.contactId || (row.contact && row.contact.id) || row.id;
                if (!cid || seen[cid]) return; seen[cid] = true;
                const name = row.contactName || row.displayName || row.fullName || row.name || (row.contact && (row.contact.displayName || row.contact.fullName || row.contact.name)) || cid;
                const linkId = row.accountContactLinkId || row.linkId || row.accountContactId || (row.contactId ? row.id : null);
                const specialty = row.specialty || row.specialization || (row.contact && (row.contact.specialty || row.contact.specialization)) || '';
                const photo = row.photoDataUri || (row.contact && row.contact.photoDataUri) || '';
                list.push({ contactId: cid, name, specialty, linkId, photo });
            });
            contactsByAccount[accountId] = list; return list;
        }).catch(() => { contactsByAccount[accountId] = []; return []; });
    };

    // Fetch (and cache) a clinic/hospital's linked PHARMACIES from the Account-360 related-accounts projection. Only
    // pharmacy-typed related accounts are offered as visit targets. Fail-soft → [].
    const fetchRelatedPharmacies = accountId => {
        if (relatedByAccount[accountId]) return Promise.resolve(relatedByAccount[accountId]);
        return api('/accounts/' + accountId + '/related-accounts?pageSize=100').then(r => {
            const list = [];
            listItems(r.body).forEach(row => {
                const type = String(row.relatedAccountType || row.accountType || '').toLowerCase();
                if (type !== 'pharmacy') return;
                const id = row.relatedAccountId || row.targetAccountId || row.accountId;
                if (!id) return;
                list.push({ id: id, name: row.relatedAccountName || id, relType: row.effectiveLabelCode || row.relationshipType || '', code: row.relatedAccountCode || row.accountCode || '' });
            });
            relatedByAccount[accountId] = list; return list;
        }).catch(() => { relatedByAccount[accountId] = []; return []; });
    };

    // Render the linked-pharmacy checkboxes for one account; ticking adds the pharmacy to the pharmacy-target set.
    const renderPharmacies = accountId => {
        const host = el('vp-pharmacies'); if (!host) return;
        host.innerHTML = '<div class="text-muted small">' + esc(L.Loading || '…') + '</div>';
        fetchRelatedPharmacies(accountId).then(list => {
            if (!list.length) { host.innerHTML = '<div class="text-muted small">' + esc(L.NoLinkedPharmacies || 'No linked pharmacies.') + '</div>'; return; }
            const card = p => {
                const on = !!selectedPharmacies[p.id];
                const src = accountSource.find(a => a.id === p.id) || {};
                const addr = src.addr || '';
                return '<div class="col-12 col-md-6"><label class="vp-pharm-card d-flex gap-2 p-3 h-100' + (on ? ' vp-pharm-card--on' : '') + '">' +
                    '<input class="form-check-input mt-0 flex-shrink-0 js-pharmacy-check" type="checkbox" data-pid="' + esc(p.id) + '" data-pname="' + esc(p.name) + '"' + (on ? ' checked' : '') + (canGenerate ? '' : ' disabled') + '>' +
                    '<span style="min-width:0" class="flex-grow-1">' +
                    '<span class="d-flex align-items-center gap-2 flex-wrap mb-1"><span class="fw-medium text-truncate">' + esc(p.name) + '</span>' + (p.relType ? '<span class="badge bg-label-warning text-uppercase">' + esc(p.relType) + '</span>' : '') + '</span>' +
                    '<span class="text-muted small d-block text-truncate vp-pharm-addr" data-pid="' + esc(p.id) + '">' + esc(addr || '—') + '</span>' +
                    (p.code ? '<span class="text-muted small font-monospace">' + esc(p.code) + '</span>' : '') +
                    '</span></label></div>';
            };
            host.innerHTML = '<div class="row g-3">' + list.map(card).join('') + '</div>';
            // Fill missing street addresses progressively (related-accounts projection carries no address).
            list.forEach(p => { const src = accountSource.find(a => a.id === p.id); if (!src || src.addr === undefined || src.addr === '') resolveAccount(p.id).then(a => { const n = host.querySelector('.vp-pharm-addr[data-pid="' + p.id + '"]'); if (n && a && a.addr) n.textContent = a.addr; }); });
        });
    };

    // ── Selection summary (right column): counts + removable chips + header subtitle ──
    const cName = (aid, cid) => { const c = (contactsByAccount[aid] || []).find(x => x.contactId === cid); return c ? c.name : cid; };
    const cSpec = (aid, cid) => { const c = (contactsByAccount[aid] || []).find(x => x.contactId === cid); return c ? c.specialty : ''; };
    const chip = (kind, attr, title, sub) =>
        '<div class="d-flex align-items-center gap-2 vp-selchip" data-kind="' + kind + '" ' + attr + '>' +
        '<span class="flex-grow-1" style="min-width:0"><span class="fw-medium small d-block text-truncate">' + esc(title) + '</span>' +
        (sub ? '<span class="text-muted" style="font-size:.72rem;">' + esc(sub) + '</span>' : '') + '</span>' +
        (canGenerate ? '<button type="button" class="btn btn-icon btn-text-secondary vp-selchip-x flex-shrink-0" aria-label="remove"><i class="bx bx-x"></i></button>' : '') + '</div>';
    const renderSelectionChips = () => {
        const host = el('vp-selection-chips'); if (!host) return;
        const parts = [];
        Object.keys(selectedContacts).forEach(k => { const s = selectedContacts[k]; parts.push(chip('doctor', 'data-k="' + esc(k) + '"', cName(s.accountId, s.contactId), prettify(cSpec(s.accountId, s.contactId)))); });
        Object.keys(selectedPharmacies).forEach(pid => parts.push(chip('pharmacy', 'data-pid="' + esc(pid) + '"', selectedPharmacies[pid].name || pid, L.StatPharmacies || 'pharmacy')));
        host.innerHTML = parts.length ? parts.join('') : '<div class="text-muted small">—</div>';
    };
    const refreshTargetsUi = () => {
        const docN = Object.keys(selectedContacts).length, phN = Object.keys(selectedPharmacies).length, accN = targetAccounts.length;
        setText('vp-accounts-count', String(accN));
        setText('vp-sum-doctors', docN); setText('vp-sum-pharm', phN); setText('vp-sum-accounts', accN);
        const acc = targetAccounts.find(a => a.id === activeAccountId);
        setText('vp-targets-subtitle', (acc ? acc.name + ' · ' : '') + docN + ' ' + (L.StatDoctors || 'doctors') + ', ' + phN + ' ' + (L.StatPharmacies || 'pharmacies') + ' ' + (L.SelectedSuffix || 'selected'));
        renderSelectionChips();
    };
    // Specialty filter pills for the active account's doctors (Tümü + one per distinct specialty).
    const renderSpecialtyPills = list => {
        const host = el('vp-specialty-pills'); if (!host) return;
        const specs = Array.from(new Set((list || []).map(c => c.specialty).filter(Boolean)));
        if (!specs.length) { host.innerHTML = ''; return; }
        const pill = (val, label, on) => '<button type="button" class="btn btn-sm ' + (on ? 'btn-primary' : 'btn-label-secondary') + ' vp-spec-pill" data-spec="' + esc(val) + '">' + esc(label) + '</button>';
        host.innerHTML = '<span class="text-muted small me-1">' + esc(L.ColSpecialty || 'Specialty') + '</span>' + pill('', L.AllLabel || 'All', true) + specs.map(s => pill(s, prettify(s), false)).join('');
    };
    const selectAllDoctors = () => {
        if (!contactsDt || !activeAccountId) return;
        contactsDt.rows({ search: 'applied' }).data().each(row => {
            const cid = row.contactId; const k = selKey(activeAccountId, cid);
            const c = (contactsByAccount[activeAccountId] || []).find(x => x.contactId === cid);
            selectedContacts[k] = { contactId: cid, accountId: activeAccountId, accountContactLinkId: (c && c.linkId) || null };
        });
        contactsDt.draw(false); refreshTargetsUi();
    };
    const clearSelection = () => {
        Object.keys(selectedContacts).forEach(k => delete selectedContacts[k]);
        Object.keys(selectedPharmacies).forEach(k => delete selectedPharmacies[k]);
        if (contactsDt) contactsDt.draw(false);
        if (activeAccountId) renderPharmacies(activeAccountId);
        refreshTargetsUi();
    };

    const showContacts = accountId => {
        activeAccountId = accountId;
        el('vp-contacts-panel')?.classList.remove('d-none');
        const acc = targetAccounts.find(a => a.id === accountId);
        setText('vp-contacts-for', acc ? acc.name : '—');
        // meta line: city · N doctors · M linked pharmacies (filled progressively as the fetches resolve).
        const meta = { city: acc ? (acc.city || '') : '', docs: null, ph: null };
        const paintMeta = () => setText('vp-contacts-meta', [meta.city, meta.docs != null ? meta.docs + ' ' + (L.StatDoctors || 'doctors') : null, meta.ph != null ? meta.ph + ' ' + (L.LinkedPharmacies || 'linked pharmacies') : null].filter(Boolean).join(' · ') || '—');
        paintMeta();
        fetchAccountContacts(accountId).then(list => {
            renderContactsDt(list);
            renderSpecialtyPills(list);
            setText('vp-doctors-count', String(list.length));
            meta.docs = list.length; paintMeta();
            const h = el('vp-contacts-hint'); if (h) h.textContent = list.length ? '' : (L.PickAccountForContacts || '');
            const s = el('vp-doctor-search'); if (s) s.value = '';
        });
        fetchRelatedPharmacies(accountId).then(list => { setText('vp-pharm-count', String(list.length)); meta.ph = list.length; paintMeta(); });
        renderPharmacies(accountId);
        refreshTargetsUi();
    };

    const addAccount = id => {
        if (!id || targetAccounts.some(a => a.id === id)) return;
        const src = accountSource.find(a => a.id === id); if (!src) return;
        targetAccounts.push({ id: src.id, name: src.name, type: src.type, city: src.city, lat: src.lat, lng: src.lng });
        buildAccountsDt(); fillAddAccountPicker(); refreshTargetsUi();
    };
    const removeAccount = id => {
        targetAccounts = targetAccounts.filter(a => a.id !== id);
        Object.keys(selectedContacts).forEach(k => { if (k.indexOf(id + '|') === 0) delete selectedContacts[k]; });
        if (activeAccountId === id) { activeAccountId = null; setText('vp-contacts-for', '—'); setText('vp-contacts-meta', '—'); if (contactsDt) { contactsDt.clear().draw(); } el('vp-contacts-panel')?.classList.add('d-none'); }
        buildAccountsDt(); fillAddAccountPicker(); refreshTargetsUi();
    };

    // Resolve one saved account id → its master row (name/type/city). Fail-soft: keep an id-named row on any failure so
    // the table still lists it. Caches into accountSource so addAccount()/click handlers can reuse it.
    const resolveAccount = id => {
        const cached = accountSource.find(a => a.id === id);
        if (cached && cached.addr !== undefined) return Promise.resolve({ id: cached.id, name: cached.name, type: cached.type, city: cached.city, addr: cached.addr });
        return api('/accounts/' + id).then(r => {
            const row = (r.ok && r.body && (r.body.data !== undefined ? r.body.data : r.body)) || null;
            const built = row ? { id, name: accName(row), type: accType(row), city: accCity(row), addr: accAddr(row), lat: accLat(row), lng: accLng(row) } : { id, name: id, type: '', city: '', addr: '' };
            const ex = accountSource.find(a => a.id === id); if (ex) { ex.addr = built.addr; ex.city = ex.city || built.city; }
            if (!accountSource.find(a => a.id === id)) accountSource.push(built);
            return built;
        }).catch(() => { const built = { id, name: id, type: '', city: '' }; if (!accountSource.find(a => a.id === id)) accountSource.push(built); return built; });
    };

    const seedTargets = () => {
        (sessionData && sessionData.selectedContacts || []).forEach(c => {
            const cid = c.contactId || c; const aid = c.accountId || '';
            if (cid && aid) selectedContacts[selKey(aid, cid)] = { contactId: cid, accountId: aid, accountContactLinkId: c.accountContactLinkId || null };
        });
        // Hydrate saved pharmacy targets (flat id set) + resolve their coordinates so the route can place them.
        (sessionData && sessionData.selectedPharmacyIds || []).forEach(pid => { const id = pid.id || pid; if (id) { selectedPharmacies[id] = { id: id, name: id }; resolveAccount(id).then(a => { if (a && selectedPharmacies[id]) selectedPharmacies[id].name = a.name; }); } });
        const ids = (sessionData && sessionData.selectedAccountIds) || [];
        // Resolve saved account ids in parallel so names/type/city render after reload (not raw GUIDs).
        return Promise.all(ids.map(resolveAccount)).then(rows => {
            targetAccounts = rows;
            buildAccountsDt(); fillAddAccountPicker(); refreshTargetsUi();
            // Eagerly load every clinic/hospital's linked pharmacies so the pharmacy→clinic map is complete BEFORE any
            // reorder — otherwise a pharmacy stays behind when its clinic moves (the map was only lazy-loaded on expand).
            targetAccounts.forEach(a => fetchRelatedPharmacies(a.id));
            if (targetAccounts.length) showContacts(targetAccounts[0].id);
        });
    };

    const saveTargets = () => {
        if (!sessionData) return;
        const contacts = Object.keys(selectedContacts)
            .filter(k => targetAccounts.some(a => a.id === selectedContacts[k].accountId))
            .map(k => selectedContacts[k]);
        const payload = {
            cyclePeriodId: sessionData.cyclePeriodId, resourceId: sessionData.resourceId,
            resourceType: sessionData.resourceType || 'person', resourceDisplayName: sessionData.resourceDisplayName || null,
            selectedAccountIds: targetAccounts.map(a => a.id), selectedPharmacyIds: Object.keys(selectedPharmacies),
            selectedContacts: contacts, segmentId: sessionData.segmentId || null, campaignId: sessionData.campaignId || null,
            strategyTemplateId: sessionData.strategyTemplateId || null, expectedVersion: currentVersion
        };
        const note = el('vp-targets-status'); if (note) note.textContent = L.Loading || '…';
        api('/sessions/' + sessionId, { method: 'PUT', body: JSON.stringify(payload) }).then(r => {
            if (r.ok) { window.showToast?.(L.TargetsSaved || 'Saved', 'success'); if (note) note.textContent = ''; loadSession().then(preview); }
            else { if (note) note.textContent = ''; window.showToast?.(errorText(r), 'error'); }
        });
    };

    // ── apply / replan ── (apply = "Bu haftanın planı olarak kaydet"; it persists the manual order on the session)
    const apply = () => {
        // A wholly empty plan (no visits on any day) has nothing to commit — block it. Partly-empty weeks are fine.
        if (!scheduled.length) { window.showToast?.(L.EmptyPlanBlocked || 'This plan has no visits, so it cannot be saved.', 'error'); return Promise.resolve(); }
        const body = { planningSessionId: sessionId, expectedVersion: currentVersion };
        if (manualOrder && manualOrder.length) body.manualVisitOrder = manualOrder;
        return api('/apply', { method: 'POST', body: JSON.stringify(body) }).then(r => {
            if (r.ok && r.body && r.body.data) { window.showToast?.((L.Applied || 'Applied') + ' (' + (r.body.data.scheduledCount || 0) + ')', 'success'); loadSession(); }
            else { window.showToast?.(errorText(r), 'error'); }
        });
    };
    const replan = () => {
        if (!lastPreview) { window.showToast?.(L.PreviewFailed || '', 'error'); return; }
        const contactIds = (lastPreview.content || []).map(c => c.contactId).filter(Boolean);
        if (!contactIds.length) return;
        const body = { planningSessionId: sessionId, affectedContactIds: contactIds };
        if (manualOrder && manualOrder.length) body.manualVisitOrder = manualOrder;
        api('/re-plan', { method: 'POST', body: JSON.stringify(body) }).then(r => {
            if (r.ok) { window.showToast?.(L.Replanned || 'Re-planned', 'success'); preview(); }
            else { window.showToast?.(errorText(r), 'error'); }
        });
    };

    // ── wiring ──
    el('vp-day-tabs')?.addEventListener('click', e => {
        const btn = e.target.closest('[data-day]'); if (!btn || btn.classList.contains('disabled')) return;
        el('vp-day-tabs').querySelectorAll('.nav-link').forEach(b => b.classList.toggle('active', b === btn));
        renderDay(parseInt(btn.dataset.day, 10));
    });
    // Seçenek 2 — drop an account card (SortableJS native drag) onto ANOTHER day's tab to move it there. Uses native
    // HTML5 dragover/drop, which fire on external elements during a SortableJS drag; degrades silently if Sortable is in
    // fallback mode. draggingBlockIdx is set while a block is being dragged.
    (() => {
        const tabs = el('vp-day-tabs'); if (!tabs) return;
        const dropAt = e => e.target.closest('[data-day]');
        const hi = t => { t.style.outline = '2px dashed var(--bs-primary, #696cff)'; t.style.outlineOffset = '2px'; };
        const unhi = t => { t.style.outline = ''; t.style.outlineOffset = ''; };
        tabs.addEventListener('dragover', e => {
            const t = dropAt(e);
            if (t && draggingBlockIdx != null && !t.classList.contains('disabled')) { e.preventDefault(); hi(t); }
        });
        tabs.addEventListener('dragleave', e => { const t = dropAt(e); if (t) unhi(t); });
        tabs.addEventListener('drop', e => {
            const t = dropAt(e);
            if (!t || draggingBlockIdx == null || t.classList.contains('disabled')) return;
            e.preventDefault(); unhi(t);
            crossDayMove = true;                 // the block Sortable's onEnd will skip its within-day reorder
            moveBlockToDay(draggingBlockIdx, parseInt(t.dataset.day, 10));
        });
    })();
    // Expand / collapse a route account block → reveal its doctor visits (built lazily on first expand). Bound once;
    // the host innerHTML is replaced per day, but this delegated listener survives.
    el('vp-visit-cards')?.addEventListener('click', e => {
        if (e.target.closest('.vp-block-handle')) return; // the grip starts a drag, never a toggle
        const blockEl = e.target.closest('.vp-block'); if (!blockEl) return;
        const idx = blockEl.dataset.idx;
        if (dayBlocks[parseInt(idx, 10)] && dayBlocks[parseInt(idx, 10)].count === 1) return; // single-visit stops aren't collapsible
        const detail = el('vp-visit-cards').querySelector('.vp-block-detail[data-idx="' + idx + '"]');
        if (!detail) return;
        const chev = blockEl.querySelector('.vp-block-chevron');
        const collapsed = detail.classList.contains('d-none');
        detail.classList.toggle('d-none', !collapsed);
        if (chev) { chev.classList.toggle('bx-chevron-down', collapsed); chev.classList.toggle('bx-chevron-right', !collapsed); }
        if (collapsed && detail.dataset.built !== '1') { detail.dataset.built = '1'; buildBlockDetail(parseInt(idx, 10), detail); }
    });
    // Account-block drag-reorder → re-issue a BACKEND preview with the new manual order. Wired ONCE on the host;
    // SortableJS delegates so it survives the per-render innerHTML swaps. Only .vp-block cards drag (via their grip).
    if (window.Sortable && el('vp-visit-cards')) {
        window.Sortable.create(el('vp-visit-cards'), {
            draggable: '.vp-tl-row--account', filter: '.vp-tl-row--pharmacy', handle: '.vp-block-handle', animation: 150, ghostClass: 'vp-block-ghost',
            onStart: evt => { draggingBlockIdx = parseInt(evt.item.dataset.idx, 10); crossDayMove = false; },
            // A drop onto a day tab (cross-day move) already re-planned via moveBlockToDay — skip the within-day reorder.
            onEnd: () => { const wasCrossDay = crossDayMove; crossDayMove = false; draggingBlockIdx = null; if (!wasCrossDay) onManualReorder(); }
        });
    }
    // View toggles (map / visit numbers / drive times) — persisted; only show/hide, no re-plan. The map panel is always
    // populated by renderDay, so flipping it back on reveals ready content.
    const bindToggle = (id, key) => { el(id)?.addEventListener('change', function () { prefs[key] = this.checked; savePrefs(); applyPrefs(); }); };
    el('vp-toggle-map')?.addEventListener('change', function () { prefs.map = this.checked; savePrefs(); applyPrefs(); if (this.checked) refreshMap(); });
    bindToggle('vp-toggle-visitnums', 'visitNums');
    bindToggle('vp-toggle-drivetimes', 'driveTimes');
    applyPrefs();
    // Resize Leaflet when the map canvas enters/exits native fullscreen (bound once).
    document.addEventListener('fullscreenchange', () => { if (mapInstance) setTimeout(() => { try { mapInstance.invalidateSize(); mapInstance.fitBounds(window.L.latLngBounds(mapLatLngs), { padding: [24, 24], maxZoom: 15 }); } catch (e) { /* noop */ } }, 120); });
    // Linked-pharmacy toggles (delegated; the host persists across per-account re-renders). Ticking adds the pharmacy
    // to the pharmacy-target set and resolves its coordinates into accountSource so the route can place it.
    el('vp-pharmacies')?.addEventListener('change', e => {
        const cb = e.target.closest('.js-pharmacy-check'); if (!cb) return;
        const pid = cb.dataset.pid;
        if (cb.checked) { selectedPharmacies[pid] = { id: pid, name: cb.dataset.pname }; resolveAccount(pid); }
        else { delete selectedPharmacies[pid]; }
        const card = cb.closest('.vp-pharm-card'); if (card) card.classList.toggle('vp-pharm-card--on', cb.checked);
        refreshTargetsUi();
    });
    // ── Targets: doctor search, select-all, clear, specialty pills, selection-chip removal ──
    el('vp-doctor-search')?.addEventListener('input', function () { if (contactsDt) contactsDt.search(this.value || '').draw(); });
    el('vp-select-all-doctors')?.addEventListener('click', selectAllDoctors);
    el('vp-clear-selection')?.addEventListener('click', clearSelection);
    el('vp-specialty-pills')?.addEventListener('click', e => {
        const btn = e.target.closest('.vp-spec-pill'); if (!btn || !contactsDt) return;
        el('vp-specialty-pills').querySelectorAll('.vp-spec-pill').forEach(b => { b.classList.toggle('btn-primary', b === btn); b.classList.toggle('btn-label-secondary', b !== btn); });
        const spec = btn.dataset.spec;
        contactsDt.column(2).search(spec ? '^' + spec + '$' : '', true, false).draw();
    });
    el('vp-selection-chips')?.addEventListener('click', e => {
        const x = e.target.closest('.vp-selchip-x'); if (!x) return;
        const chipEl = x.closest('.vp-selchip'); if (!chipEl) return;
        if (chipEl.dataset.kind === 'pharmacy') { delete selectedPharmacies[chipEl.dataset.pid]; if (activeAccountId) renderPharmacies(activeAccountId); }
        else { delete selectedContacts[chipEl.dataset.k]; if (contactsDt) contactsDt.draw(false); }
        refreshTargetsUi();
    });
    // "Optimal rotaya dön": drop the manual order and re-preview WITHOUT manualVisitOrder → the engine's optimum.
    el('vp-reset-optimal')?.addEventListener('click', () => { manualOrder = null; manualIsUser = false; autoGroupDone = false; preview(); });
    el('vp-preview')?.addEventListener('click', preview);
    if (canGenerate) {
        el('vp-save-targets')?.addEventListener('click', saveTargets);
        // #vp-add-account is a select2 — it raises `change` via jQuery.trigger, which native addEventListener misses.
        // Bind through jQuery when present, keep the native listener as a no-select2 fallback.
        const addFromPicker = function () { const v = this.value; if (v) { addAccount(v); if (window.jQuery) { window.jQuery(this).val('').trigger('change.select2'); } } };
        if (window.jQuery && window.jQuery.fn.select2) {
            window.jQuery('#vp-add-account').on('change', addFromPicker);
        } else {
            el('vp-add-account')?.addEventListener('change', addFromPicker);
        }
    }
    if (canApply) { el('vp-apply')?.addEventListener('click', apply); el('vp-replan')?.addEventListener('click', replan); }

    // Boot: names → session header → account source + targets (master/detail) → default tab → route preview.
    loadPeriods()
        .then(loadSession)
        .then(loadAccountSource)
        .then(seedTargets)
        .then(applyDefaultTab)
        .then(preview);
})(window, document);
