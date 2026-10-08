/**
 * WP-VP-4D — the Visit Planning "Weeks" tab (brief §5, mockup "05 Haftalar"), plugged into the 4B page skeleton.
 *
 *   - Period strip: every week of the period — status (past / approved / draft / empty; a legacy plan's written weeks
 *     "approved (old plan)"), visit count, capacity bar (weekCapacity planned / capacity), holiday and half-day marks,
 *     a warning badge (moved / not placed), TODAY. A click selects the week for the whole page (VisitPlanningPage).
 *   - Week detail (the selected week): day by day — "N / daily cap", holiday, half day, idle time (4E days[]),
 *     over-capacity; a day opens into its institutions and visits (doctor + institution + product chips + ≈ minutes;
 *     the first 6, then "+N more"; "No visit falls on this day"); visits per product; the mixed-order note; moved / not
 *     placed (shifted + unscheduled + overflowProducts + pinOverflow, with local reasons); the week's actions (4B
 *     functions through the page: approve / rebuild / open the route / reopen); the doctors of the week with their period
 *     dots; an approved week's history (who = a name, never an id).
 *   - Move between days (4E day pins, the EXISTING session update — no new write): drag an institution (scope
 *     institution: its doctors + linked pharmacies) or one visit (asks "all of this institution, or only this one?"), or
 *     the keyboard way "Move to day…". Only a draft week of a writable plan, never onto a holiday / weekend / gone day.
 *     A pinned visit shows a pin (unpin), an auto-moved one its own mark; the preview's pinOverflow is said in one line.
 *   - A doctor click opens the doctor panel's period view (doctor-panel.js, 'doctor-panel:open').
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('visit-planning-details');
    const page = window.VisitPlanningPage || null;
    if (!root || !page || !document.getElementById('vp-tab-weeks')) return;

    const L = window.L10n || {};
    const VPF = window.VisitPlanningFormat; // WP-VP-4H — dates / numbers in the application's language
    const base = '/CRM/VisitPlanning/api';
    const sessionId = root.dataset.sessionId;
    const canGenerate = root.dataset.canGenerate === 'true';
    const canApply = root.dataset.canApply === 'true';
    const readOnly = root.dataset.readOnly === 'true';
    const DAY_PREVIEW_LIMIT = 6;
    const MOVABLE_WEEK_STATUSES = ['draft', 'empty'];
    const DROPPABLE_DAY_KINDS = ['working', 'half'];

    const el = id => document.getElementById(id);
    const esc = s => { const d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; };
    const fmt = (tpl, ...args) => args.reduce((t, a, i) => t.split('{' + i + '}').join(String(a)), String(tpl || ''));
    const request = (url, options) => {
        options = options || {};
        options.credentials = 'same-origin';
        options.headers = Object.assign({ Accept: 'application/json', 'Content-Type': 'application/json' }, options.headers || {});
        return fetch(url, options).then(r => r.text().then(text => {
            let body = null; try { body = text ? JSON.parse(text) : null; } catch (e) { body = null; }
            return { ok: r.ok, status: r.status, body };
        }));
    };
    const errorText = r => (r.body && r.body.errors && r.body.errors.length) ? r.body.errors.join(' · ') : (r.body && r.body.message) || ('HTTP ' + r.status);

    // ── dates: yyyy-MM-dd strings as LOCAL days; the same short format as the header ──
    const localDate = v => new Date(v + 'T00:00:00');
    const ymd = d => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    const addDays = (d, n) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + n);
    const todayYmd = () => ymd(new Date());
    const dm = d => VPF.dayMonth(d);
    const dayName = d => VPF.dayShort(d);
    const dayLabel = v => VPF.dayLabel(v);
    const weekTitle = w => fmt(L.WeekNumberLabel || '{0}', w.isoWeek);
    const weekRange = w => VPF.workRange(w.from || w.weekStart, w.to || w.weekStart);
    const hours = minutes => VPF.hours(Number(minutes || 0), L.HoursFormat || '{0} h');
    const mondayYmd = v => { const d = localDate(v); return ymd(addDays(d, -((d.getDay() + 6) % 7))); };

    // ── labels ──
    const STATUS_LABEL = { past: 'WeekStatusPast', approved: 'WeekStatusApproved', draft: 'WeekStatusDraft', empty: 'WeekStatusEmpty' };
    const STATUS_TONE = { past: 'secondary', approved: 'success', draft: 'primary', empty: 'warning' };
    const statusLabel = w => (w.storedStatus === 'legacy' ? (L.LegacyWeekLabel || '') : (L[STATUS_LABEL[w.status]] || w.status || '—'));
    // Moved / not placed reasons (3B shifted, unscheduled, 3C overflow products, 4E pin overflow) in the local language.
    const REASON_KEYS = {
        capacity_full: 'ReasonCapacityFull', holiday: 'ReasonHoliday', half_day: 'ReasonHalfDay', pin_overflow: 'ReasonPinOverflow',
        consent_blocked: 'ReasonConsentBlocked', period_exhausted: 'ReasonPeriodExhausted', missing_location: 'ReasonMissingLocation',
        no_feasible_availability_window: 'ReasonNoAvailability', duration_exceeds_working_day: 'ReasonTooLong',
        pin_day_full: 'ReasonPinDayFull', pin_outside_availability: 'ReasonPinAvailability',
        max_promo: 'ReasonMaxPromo', max_non_promo: 'ReasonMaxNonPromo',
        no_near_day: 'ReasonNoNearDay' // 4G — a far group on a light week waits for a near day
    };
    const reasonText = code => L[REASON_KEYS[code]] || L.ReasonOther || code || '—';

    // ── data: the preview + the plan (page skeleton), the plan's targets (names + 3D status), the signed-in person ──
    const preview = () => page.state.preview || {};
    const session = () => page.state.session || {};
    const slotsAll = () => (preview().scheduled || []);
    const weekIndexOf = ws => page.weeks().findIndex(w => w.weekStart === ws);
    let names = { accounts: {}, doctors: {} };
    let me = null;
    const loadTargets = () => request(base + '/sessions/' + encodeURIComponent(sessionId) + '/targets').then(r => {
        const d = r.ok && r.body && r.body.data;
        if (!d) return;
        const accounts = {}, doctors = {};
        (d.accounts || []).concat(d.pharmacies || []).forEach(a => { if (a && a.accountId) accounts[a.accountId] = { name: a.accountName || '—', type: a.accountType || '' }; });
        (d.doctors || []).forEach(x => { if (x && x.contactId) doctors[x.contactId] = { name: x.displayName || '—', specialty: x.specialty || '', accountId: x.accountId, status: x.status || {} }; });
        names = { accounts, doctors };
        render();
    }).catch(() => { });
    const loadMe = () => request(base + '/me').then(r => { me = (r.ok && r.body && r.body.data && (r.body.data.items || [])[0]) || null; }).catch(() => { });
    const accountName = id => (id && names.accounts[id] && names.accounts[id].name) || '—';
    const visitName = s => s.contactId
        ? (s.contactDisplayName || (names.doctors[s.contactId] && names.doctors[s.contactId].name) || '—')
        : accountName(s.targetId);
    const groupOf = s => s.groupKey || String(s.accountId || s.targetId || '').replace(/-/g, '');
    const groupName = slots => { const a = slots.find(s => s.accountId); return a ? accountName(a.accountId) : accountName(slots[0].targetId); };

    // ── 1 · the period strip (mockup "Dönem haftaları": a vertical 128px card per week) ──
    const holidaysOf = (w, p) => {
        const off = new Set(p.nonWorkingDates || []);
        const out = [];
        for (let d = localDate(w.from || w.weekStart); d <= localDate(w.to || w.weekStart); d = addDays(d, 1)) {
            if (d.getDay() !== 0 && d.getDay() !== 6 && off.has(ymd(d))) out.push(ymd(d));
        }
        return out;
    };
    const halfDaysOf = (w, p) => (p.halfDayDates || []).filter(x => x >= (w.from || w.weekStart) && x <= (w.to || w.weekStart));
    const warningsOf = (p, i, ws) => (p.shifted || []).filter(s => s.fromWeek === i).length
        + (p.unscheduled || []).filter(u => u.weekNumber === i).length
        + (p.pinOverflow || []).filter(o => mondayYmd(o.fromDate) === ws).length;
    const stripModel = () => {
        const p = preview();
        const today = todayYmd();
        return page.weeks().map((w, i) => {
            const wc = (p.weekCapacity || []).find(c => c.weekStart === w.weekStart);
            const pct = wc && wc.capacityMinutes > 0 ? Math.min(100, Math.round(wc.plannedMinutes / wc.capacityMinutes * 100)) : 0;
            return {
                week: w, ws: w.weekStart, title: weekTitle(w), range: weekRange(w), status: w.status, label: statusLabel(w),
                visits: w.visitCount || 0, pct, holidays: holidaysOf(w, p), halfDays: halfDaysOf(w, p), warnings: warningsOf(p, i, w.weekStart),
                isToday: today >= (w.from || w.weekStart) && today <= (w.to || w.weekStart), selected: w.weekStart === page.state.weekStart
            };
        });
    };
    // One card: "41. Hafta" + TODAY · the working days · the status badge · "176 visits" · the bar · holidays + warnings.
    // An empty week: a dashed card, "—" for the visits.
    const STRIP_CARD = 'flex:0 0 128px;';
    const stripCard = m => {
        const empty = m.status === 'empty';
        return '<button type="button" role="option" aria-selected="' + (m.selected ? 'true' : 'false') + '" data-ws="' + esc(m.ws) + '"' +
            ' class="btn border rounded text-start d-flex flex-column gap-1 px-3 py-2 vp-wk-item' + (m.selected ? ' border-primary bg-label-primary' : '') + (empty ? ' vp-wk-item--empty' : '') + '"' +
            ' style="' + STRIP_CARD + (empty ? 'border-style:dashed !important;' : '') + '">' +
            '<span class="d-flex justify-content-between align-items-center w-100"><span class="fw-semibold">' + esc(m.title) + '</span>' + (m.isToday ? '<span class="small fw-semibold text-primary">' + esc(L.TodayLabel || '') + '</span>' : '') + '</span>' +
            '<span class="small text-muted">' + esc(m.range) + '</span>' +
            '<span class="badge align-self-start bg-label-' + (m.week.storedStatus === 'legacy' ? 'success' : (STATUS_TONE[m.status] || 'secondary')) + '">' + esc(m.label) + '</span>' +
            '<span class="small">' + esc(empty ? '—' : fmt(L.VisitCountShort || '{0}', m.visits)) + '</span>' +
            '<span class="progress w-100" style="height:4px"><span class="progress-bar' + (m.pct >= 100 ? ' bg-warning' : '') + '" style="width:' + m.pct + '%"></span></span>' +
            '<span class="d-flex flex-wrap gap-1 small" style="min-height:18px">' +
            m.holidays.map(h => '<span class="text-danger" title="' + esc(L.HolidayMarker || '') + '"><i class="bx bx-calendar-x"></i>' + esc(dm(localDate(h))) + '</span>').join('') +
            m.halfDays.map(h => '<span class="text-warning" title="' + esc(L.HalfDayLabel || '') + '">½ ' + esc(dm(localDate(h))) + '</span>').join('') +
            (m.warnings ? '<span class="text-warning" title="' + esc(L.SlipTitle || '') + '"><i class="bx bx-error"></i>' + m.warnings + '</span>' : '') +
            '</span></button>';
    };
    const renderStrip = () => {
        const host = el('vp-wk-strip'); if (!host) return;
        const model = stripModel();
        setSummary(model);
        if (!model.length) { host.innerHTML = '<div class="text-muted small">' + esc(L.Loading || '…') + '</div>'; return; }
        host.innerHTML = model.map(stripCard).join('');
    };
    // "Türkiye 2026 Q4 Döngüsü · 13 hafta · 1 onaylı · 3 taslak · 8 boş · 1 geçmiş" — the period's name from the summary.
    const setSummary = model => {
        const n = el('vp-wk-summary'); if (!n) return;
        const count = st => model.filter(m => m.status === st).length;
        const period = el('vp-d-period') ? el('vp-d-period').textContent.trim() : '';
        const line = fmt(L.PeriodWeeksSummary || '{0}', model.length, count('approved'), count('draft'), count('empty'), count('past'));
        n.textContent = period && period !== '—' ? period + ' · ' + line : line;
    };

    // ── 2 · the selected week ──
    const daysOf = (w, p, slots) => {
        const off = new Set(p.nonWorkingDates || []);
        const half = new Set(p.halfDayDates || []);
        const wc = (p.weekCapacity || []).find(c => c.weekStart === w.weekStart);
        const from = w.from || w.weekStart, to = w.to || w.weekStart;
        const out = [];
        for (let d = localDate(w.weekStart), i = 0; i < 7; i++, d = addDays(d, 1)) {
            const date = ymd(d);
            const onDay = slots.filter(s => s.plannedDate === date).sort((a, b) => (a.sequenceOrder || 0) - (b.sequenceOrder || 0));
            const weekend = d.getDay() === 0 || d.getDay() === 6;
            if (weekend && !onDay.length) continue; // weekends without visits are hidden (as on the route)
            const inPeriod = date >= from && date <= to;
            const kind = !inPeriod ? 'out' : off.has(date) ? (weekend ? 'weekend' : 'holiday') : half.has(date) ? 'half' : 'working';
            const summary = (p.days || []).find(x => x.date === date) || null;
            const cap = wc && wc.dailyCap ? (kind === 'half' ? Math.floor(wc.dailyCap / 2) : kind === 'working' ? wc.dailyCap : 0) : null;
            out.push({ date, d, kind, slots: onDay, cap, summary });
        }
        return out;
    };

    // A product chip: its NAME (the code in the tooltip), role colour, source icon, a warning without approved content.
    const SOURCE_ICON = { play: 'bx-bulb', 'rep-pick': 'bx-user-check', 'last-visit': 'bx-history', portfolio: 'bx-briefcase' };
    const NO_CONTENT = ['no_approved_content', 'ambiguous_journey'];
    const chipHtml = c => {
        const promo = c.role !== 'non-promo';
        const warn = promo && (!c.journeyId || /^0{8}-/.test(String(c.journeyId)) || (c.warnings || []).some(x => NO_CONTENT.indexOf(x) > -1));
        return '<span class="badge ' + (promo ? 'bg-label-primary' : 'bg-transparent border text-body') + ' d-inline-flex align-items-center gap-1" title="' + esc([c.productCode, promo ? (L.LegendPromo || '') : (L.LegendNonPromo || '')].filter(Boolean).join(' · ')) + '">' +
            (SOURCE_ICON[c.source] ? '<i class="bx ' + SOURCE_ICON[c.source] + '"></i>' : '') + esc(VPF.productLabel(c)) +
            (warn ? '<i class="bx bx-error text-warning" aria-label="' + esc(L.NoApprovedContent || '') + '"></i>' : '') + '</span>';
    };
    const chipsOf = s => {
        const items = (s.contentItems || []).slice().sort((a, b) => (a.order || 0) - (b.order || 0));
        if (!items.length) return s.contactId ? '<span class="badge bg-label-warning">' + esc(L.NoProductBadge || '') + '</span>' : '';
        return items.map(chipHtml).join(' ');
    };
    const pinMark = s => s.autoPinned
        ? '<i class="bx bx-transfer-alt text-warning" title="' + esc(L.AutoPinnedHint || '') + '" aria-label="' + esc(L.AutoPinnedHint || '') + '"></i>'
        : (s.isPinned ? '<i class="bx bx-pin text-primary" title="' + esc(L.PinnedHint || '') + '" aria-label="' + esc(L.PinnedHint || '') + '"></i>' : '');

    // A day row is closed by default; open = its first DAY_PREVIEW_LIMIT visits + "+N more"; full = every visit.
    const openDays = new Set(), fullDays = new Set();
    // A visit: the name over its institution; on the right the product chips (names) and "≈ 22 min" ("no time" without
    // products); the 4D/4E move + pin controls stay.
    const visitLine = (s, movable) => {
        const idx = slotsAll().indexOf(s);
        const name = visitName(s);
        const sub = s.contactId ? accountName(s.accountId) : '';
        const nameHtml = s.contactId
            ? '<button type="button" class="btn btn-link p-0 text-start d-flex flex-column js-wk-doctor" data-cid="' + esc(s.contactId) + '" data-aid="' + esc(s.accountId || '') + '"><span class="fw-medium text-heading">' + esc(name) + '</span>' + (sub && sub !== '—' ? '<span class="small text-muted">' + esc(sub) + '</span>' : '') + '</button>'
            : '<span class="fw-medium text-heading">' + esc(name) + '</span>';
        const hasProducts = (s.contentItems || []).length > 0;
        return '<div class="d-flex align-items-center gap-2 py-2 border-bottom flex-wrap vp-wk-visit"' + (movable ? ' draggable="true" data-drag="visit" data-slot="' + idx + '"' : '') + '>' +
            (movable ? '<i class="bx bx-grid-vertical text-muted" aria-hidden="true"></i>' : '') + pinMark(s) +
            '<span class="flex-grow-1" style="min-width:0">' + nameHtml + '</span>' +
            '<span class="d-flex flex-wrap gap-1 align-items-center">' + chipsOf(s) +
            '<span class="small text-muted text-nowrap ms-1">' + esc(hasProducts || !s.contactId ? fmt(L.ApproxMinutes || '{0}', s.durationMinutes || 0) : (L.NoTimeShort || '')) + '</span></span>' +
            (movable ? '<button type="button" class="btn btn-sm btn-text-secondary px-1 js-wk-move" data-slot="' + idx + '" data-scope="visit" title="' + esc(L.MoveToDay || '') + '" aria-label="' + esc(L.MoveToDay || '') + '"><i class="bx bx-calendar-edit"></i></button>' : '') +
            (movable && s.isPinned ? '<button type="button" class="btn btn-sm btn-text-secondary px-1 js-wk-unpin" data-slot="' + idx + '" title="' + esc(L.Unpin || '') + '" aria-label="' + esc(L.Unpin || '') + '"><i class="bx bx-pin"></i><i class="bx bx-x small"></i></button>' : '') +
            '</div>';
    };
    // The day row: the mockup grid (chevron 18 · "Pzt 5 Eki" 96 · bar · 150 "36 / 36" + idle / over / holiday).
    const DAY_GRID = 'display:grid;grid-template-columns:18px 96px minmax(0,1fr) 150px;gap:12px;align-items:center';
    const dayRow = (day, movable) => {
        const s = day.summary;
        const badges = [];
        if (day.kind === 'holiday') badges.push('<span class="badge bg-label-danger">' + esc(L.HolidayMarker || '') + '</span>');
        if (day.kind === 'half') badges.push('<span class="badge bg-label-warning">' + esc(L.HalfDayLabel || '') + '</span>');
        if (s && s.idleMinutes > 0) badges.push('<span class="badge bg-label-secondary">' + esc(fmt(L.IdleTime || '{0}', hours(s.idleMinutes))) + '</span>');
        if (s && s.overCapacity) badges.push('<span class="badge bg-label-danger">' + esc(L.OverCapacity || '') + '</span>');
        const droppable = movable && DROPPABLE_DAY_KINDS.indexOf(day.kind) > -1 && day.date >= todayYmd();
        const pct = s && s.budgetMinutes > 0 ? Math.min(100, Math.round(s.plannedMinutes / s.budgetMinutes * 100))
            : (day.cap ? Math.min(100, Math.round(day.slots.length / day.cap * 100)) : 0);
        const open = openDays.has(day.date);
        const full = fullDays.has(day.date);

        // institutions of the day (group = the institution + its linked pharmacies), visits in route order
        const groups = [];
        day.slots.forEach(v => { const k = groupOf(v); let g = groups.find(x => x.key === k); if (!g) groups.push(g = { key: k, slots: [] }); g.slots.push(v); });
        let shown = 0;
        const limit = full ? Infinity : DAY_PREVIEW_LIMIT;
        const body = groups.map(g => {
            const lines = g.slots.filter(() => shown++ < limit);
            if (!lines.length) return '';
            const first = slotsAll().indexOf(g.slots[0]);
            return '<div class="pt-1"' + (movable ? ' draggable="true" data-drag="institution" data-slot="' + first + '"' : '') + '>' +
                '<div class="d-flex align-items-center gap-2 small text-muted text-uppercase pt-1">' + (movable ? '<i class="bx bx-grid-vertical" aria-hidden="true"></i>' : '') +
                '<span class="flex-grow-1">' + esc(groupName(g.slots)) + '</span>' +
                (movable ? '<button type="button" class="btn btn-sm btn-text-secondary px-1 js-wk-move" data-slot="' + first + '" data-scope="institution" title="' + esc(L.MoveInstitution || '') + '" aria-label="' + esc(L.MoveInstitution || '') + '"><i class="bx bx-calendar-edit"></i></button>' : '') +
                '</div>' + lines.map(v => visitLine(v, movable)).join('') + '</div>';
        }).join('');
        const more = day.slots.length - DAY_PREVIEW_LIMIT;
        const fullBar = pct >= 100;
        return '<div class="mb-1 vp-wk-day" data-date="' + esc(day.date) + '" data-droppable="' + (droppable ? '1' : '0') + '">' +
            '<button type="button" class="btn w-100 text-start p-1 js-wk-day" style="' + DAY_GRID + '" aria-expanded="' + (open ? 'true' : 'false') + '" data-date="' + esc(day.date) + '">' +
            '<i class="bx ' + (open ? 'bx-chevron-down' : 'bx-chevron-right') + ' text-muted"></i>' +
            '<span><strong class="fw-semibold">' + esc(dayName(day.d)) + '</strong> ' + esc(dm(day.d)) + '</span>' +
            '<span class="progress" style="height:10px"><span class="progress-bar ' + (s && s.overCapacity ? 'bg-danger' : (fullBar ? 'bg-warning' : 'bg-primary')) + '" style="width:' + pct + '%"></span></span>' +
            '<span class="d-flex justify-content-end align-items-center gap-1 flex-wrap text-nowrap small">' + esc(day.cap != null ? fmt(L.DayCapacityFormat || '{0} / {1}', day.slots.length, day.cap) : String(day.slots.length)) + ' ' + badges.join(' ') + '</span>' +
            '</button>' +
            '<div class="ms-4 ps-3 border-start' + (open ? '' : ' d-none') + '">' +
            (day.slots.length ? body + (!full && more > 0 ? '<button type="button" class="btn btn-sm btn-link px-0 js-wk-more" data-date="' + esc(day.date) + '">' + esc(fmt(L.MoreDoctors || '{0}', more)) + '</button>' : '')
                : '<div class="small text-muted py-2">' + esc(day.kind === 'holiday' ? (L.HolidayNoVisit || L.NoVisitThisDay || '') : (L.NoVisitThisDay || '')) + '</div>') +
            '</div></div>';
    };

    // Moved / not placed: shifted (with the target week), unscheduled, overflow products, pinned visits that moved.
    const slipItems = (p, i, ws, weekSlots) => {
        const out = [];
        const isoOf = k => { const w = page.weeks()[k]; return w ? weekTitle(w) : '—'; };
        const accOf = cid => (cid && names.doctors[cid] ? accountName(names.doctors[cid].accountId) : '');
        (p.shifted || []).filter(s => s.fromWeek === i).forEach(s => out.push({
            kind: 'shift', name: s.displayName || (s.contactId && names.doctors[s.contactId] ? names.doctors[s.contactId].name : accountName(s.targetId)),
            acc: accOf(s.contactId), code: s.reason, reason: reasonText(s.reason), result: fmt(L.MovedToWeek || '{0}', isoOf(s.toWeek))
        }));
        (p.unscheduled || []).filter(u => u.weekNumber === i).forEach(u => out.push({
            kind: 'unscheduled', name: u.contactId && names.doctors[u.contactId] ? names.doctors[u.contactId].name : accountName(u.targetId),
            acc: accOf(u.contactId), code: u.reason, reason: reasonText(u.reason), result: L.NotPlanned || ''
        }));
        weekSlots.forEach(s => (s.overflowProducts || []).forEach(o => out.push({
            kind: 'product', name: VPF.productLabel(o), acc: visitName(s), code: o.reason, reason: reasonText(o.reason), result: L.ToNextVisit || ''
        })));
        (p.pinOverflow || []).filter(o => mondayYmd(o.fromDate) === ws).forEach(o => out.push({
            kind: 'pin', name: o.displayName || (o.contactId && names.doctors[o.contactId] ? names.doctors[o.contactId].name : accountName(o.targetId)),
            acc: accOf(o.contactId), code: o.reason, reason: reasonText(o.reason), result: o.toDate ? fmt(L.MovedToDay || '{0}', dayLabel(o.toDate)) : (L.NotPlanned || '')
        }));
        return out;
    };
    const SLIP_TONE = { capacity_full: 'warning', no_near_day: 'info', pin_overflow: 'warning', consent_blocked: 'danger', period_exhausted: 'danger' };
    // A moved / not placed row (mockup): name over institution · reason badge · result (a package for a product).
    const slipRow = x => '<div class="d-flex justify-content-between align-items-center gap-3 border rounded px-3 py-2 flex-wrap vp-wk-slip" data-kind="' + x.kind + '">' +
        '<div class="d-flex flex-column" style="min-width:0"><span class="fw-medium text-heading">' + esc(x.name) + '</span>' + (x.acc && x.acc !== '—' ? '<span class="small">' + esc(x.acc) + '</span>' : '') + '</div>' +
        '<div class="d-flex gap-2 align-items-center flex-wrap"><span class="badge bg-label-' + (SLIP_TONE[x.code] || 'secondary') + '">' + esc(x.reason) + '</span>' +
        '<span class="small d-flex gap-1 align-items-center">' + (x.kind === 'product' ? '<i class="bx bx-package text-primary"></i>' : '') + esc(x.result) + '</span></div></div>';

    // The week's own actions — the same rule as the header (VisitPlanningPage.actionsFor) and the same functions.
    // Draft: rebuild + approve · approved: reopen · a week with visits: open the route. An empty week has none in its
    // head: its empty state offers "Build this week".
    const weekActions = w => {
        const legacy = page.isLegacy();
        const offered = page.actionsFor(w.status, legacy);
        const out = [];
        if (!legacy && canGenerate && MOVABLE_WEEK_STATUSES.indexOf(w.status) > -1 && w.status !== 'empty') out.push({ key: 'rebuild', label: L.RebuildWeek, icon: 'bx-refresh', tone: 'outline-secondary' });
        if (offered.indexOf('approveWeek') > -1 && canApply) out.push({ key: 'approve', label: L.ApproveWeek, icon: 'bx-check-double', tone: 'success' });
        if (offered.indexOf('reopenWeek') > -1 && canApply) out.push({ key: 'reopen', label: L.ReopenWeek, icon: 'bx-lock-open-alt', tone: 'outline-secondary' });
        if (!legacy && (w.visitCount || 0) > 0) out.push({ key: 'route', label: L.OpenRoute, icon: 'bx-map-alt', tone: 'primary' });
        return out;
    };
    const WEEK_SUB = { approved: 'WeekSubApproved', empty: 'WeekSubEmpty', past: 'StatusTextPast' };

    // The doctors of the week with their period strip (done / approved / draft / projected). "Done" = a visit with a
    // completed report (4G reportStatus); without the field a visit behind us counts as approved.
    const DONE_REPORT = ['completed', 'done', 'reported'];
    const visitState = (s, firstDraftWeek) => {
        if (s.reportStatus && DONE_REPORT.indexOf(String(s.reportStatus).toLowerCase()) > -1) return 'done';
        if (s.isFixed || s.plannedDate < todayYmd()) return 'approved';
        return (s.weekStart || mondayYmd(s.plannedDate)) === firstDraftWeek ? 'draft' : 'projected';
    };
    const STATE_LABEL = { done: 'StateDone', approved: 'StateApproved', draft: 'StateDraft', projected: 'StateProjected' };
    const STATE_BG = { done: 'bg-success', approved: 'bg-primary', draft: 'bg-label-primary', projected: 'bg-label-secondary' };
    const firstDraftWeekStart = () => { const w = page.weeks().find(x => x.status === 'draft'); return w ? w.weekStart : null; };
    const DOCTORS_LIMIT = 8;
    let allDoctorsShown = false;
    // "dönemde 3 · 1 / 2" (frequency · done / remaining); an unknown frequency reads "dönemde 1 (varsayılan)" (F4-2).
    const frequencyLine = (s, status) => {
        const src = status && status.requiredVisitCount != null ? status : s; // the period status (targets read) first
        const known = src.requiredVisitCount != null && src.frequencyStatus !== 'unknown';
        const freq = known ? fmt(L.FrequencyPerPeriod || '{0}', src.requiredVisitCount) : (L.FrequencyDefaultOne || '');
        const dr = status && status.done != null ? status.done + ' / ' + (status.remaining != null ? status.remaining : '—') : '';
        return dr ? freq + ' · ' + dr : freq;
    };
    const stripRects = (cid, firstDraft) => page.weeks().map(pw => {
        const v = slotsAll().find(x => x.contactId === cid && (x.weekStart || mondayYmd(x.plannedDate)) === pw.weekStart);
        const st = v ? visitState(v, firstDraft) : null;
        return '<span class="d-inline-block rounded-1 ' + (st ? STATE_BG[st] : 'bg-label-secondary opacity-50') + '" style="width:14px;height:6px" title="' + esc(weekTitle(pw) + (st ? ' · ' + (L[STATE_LABEL[st]] || st) : '')) + '"></span>';
    }).join('');
    const renderDoctors = (ws, weekSlots) => {
        const host = el('vp-wk-doctors'); if (!host) return;
        const firstDraft = firstDraftWeekStart();
        const doctors = [];
        weekSlots.filter(s => s.contactId).forEach(s => { if (!doctors.some(d => d.contactId === s.contactId)) doctors.push(s); });
        const shown = allDoctorsShown ? doctors : doctors.slice(0, DOCTORS_LIMIT);
        host.innerHTML = '<div class="d-flex justify-content-between align-items-center gap-2"><span class="fw-semibold text-uppercase text-heading">' + esc(L.WeekDoctorsHeading || '') + '</span><span class="badge bg-label-secondary">' + doctors.length + '</span></div>' +
            '<div class="small text-muted">' + esc(L.WeekDoctorsHint || '') + '</div>' +
            (doctors.length ? '<div class="d-flex flex-column">' + shown.map(s => {
                const doctor = names.doctors[s.contactId] || {};
                const sub = [doctor.specialty ? specLabelOf(doctor.specialty) : (s.contactSpecialty ? specLabelOf(s.contactSpecialty) : ''), accountName(s.accountId)].filter(x => x && x !== '—').join(' · ');
                return '<button type="button" class="btn text-start border-0 border-top rounded-0 px-1 py-2 js-wk-doctor vp-wk-doc" data-cid="' + esc(s.contactId) + '" data-aid="' + esc(s.accountId || '') + '" style="display:grid;grid-template-columns:minmax(0,1fr) auto;gap:6px 12px;align-items:center">' +
                    '<span class="d-flex flex-column" style="min-width:0"><span class="fw-medium text-heading">' + esc(visitName(s)) + '</span><span class="small text-muted text-truncate">' + esc(sub) + '</span></span>' +
                    '<span class="small text-nowrap text-end">' + esc(frequencyLine(s, doctor.status)) + '</span>' +
                    '<span class="d-flex gap-1" style="grid-column:1 / -1">' + stripRects(s.contactId, firstDraft) + '</span></button>';
            }).join('') + '</div>' : '<div class="small text-muted py-2">' + esc(L.NoDoctorsThisWeek || '') + '</div>') +
            (doctors.length > DOCTORS_LIMIT ? '<button type="button" class="btn btn-link px-0 align-self-start js-wk-all-docs">' + esc(allDoctorsShown ? (L.ShowLess || '') : fmt(L.ShowAllCount || '{0}', doctors.length)) + '</button>' : '') +
            '<div class="d-flex flex-wrap gap-3 small text-muted border-top pt-2">' + Object.keys(STATE_LABEL).map(k => '<span class="d-flex align-items-center gap-1"><span class="d-inline-block rounded-1 ' + STATE_BG[k] + '" style="width:10px;height:6px"></span>' + esc(L[STATE_LABEL[k]] || k) + '</span>').join('') + '</div>';
    };
    let specLabels = {};
    const specLabelOf = code => specLabels[code] || specLabels[String(code).toLowerCase()] || code;
    request(base + '/reference-labels').then(r => { specLabels = (r.ok && r.body && r.body.data && r.body.data.specialties) || {}; }).catch(() => { });

    // An approved week's history: who = a name, never an id (E4-4B-6).
    const GUID = /^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$/i;
    const whoName = by => {
        if (!by) return '—';
        const s = session();
        const same = (a, b) => String(a || '').toLowerCase() === String(b || '').toLowerCase();
        const isName = v => !!v && !GUID.test(v) && !/@/.test(v); // a name, not an id or an e-mail address
        if (same(by, s.resourceId) && isName(s.resourceDisplayName)) return s.resourceDisplayName;
        if (me && same(by, me.resourceId)) return isName(me.displayName) ? me.displayName : (L.HistoryYou || '');
        return GUID.test(String(by)) ? (L.HistoryOtherUser || '') : String(by);
    };
    const historyOf = ws => {
        const stored = (session().weeks || []).find(w => w.weekStart === ws);
        const fromPreview = page.week(ws);
        return (stored && stored.history) || (fromPreview && fromPreview.history) || [];
    };

    // The pinned visits that did not fit their day, in one line: "N visits did not fit this day → {day}" (Weeks + Route).
    const pinOverflowHtml = (p, ws) => {
        const moved = (p.pinOverflow || []).filter(o => mondayYmd(o.fromDate) === ws);
        if (!moved.length) return '';
        const byTarget = {};
        moved.forEach(o => { const k = o.toDate || ''; byTarget[k] = (byTarget[k] || 0) + 1; });
        return '<div class="alert alert-info py-2 small" role="status"><i class="bx bx-transfer-alt me-1"></i>' + Object.keys(byTarget).map(k =>
            esc(k ? fmt(L.PinOverflowMessage || '{0} {1}', byTarget[k], dayLabel(k)) : fmt(L.PinOverflowNextWeek || '{0}', byTarget[k]))).join(' · ') + '</div>';
    };

    // An empty week (WP-VP-4H, 3): ONLY the empty state — no day rows, no product or moved sections.
    const emptyWeekHtml = () => '<div class="border rounded text-center d-flex flex-column align-items-center gap-2 px-3 py-5" style="border-style:dashed !important">' +
        '<i class="bx bx-calendar-plus text-muted" style="font-size:2rem"></i>' +
        '<div class="fw-medium text-heading">' + esc(L.NoVisitThisWeek || '') + '</div>' +
        '<div class="small" style="max-width:380px">' + esc(L.EmptyWeekHint || '') + '</div>' +
        (canGenerate && !page.isLegacy() ? '<button type="button" class="btn btn-primary mt-1 js-wk-action" data-action="rebuild">' + esc(L.GenerateWeek || '') + '</button>' : '') +
        '</div>';

    const renderDetail = () => {
        const host = el('vp-wk-detail'); if (!host) return;
        const ws = page.state.weekStart;
        const w = page.week(ws);
        if (!w) { host.innerHTML = '<div class="text-muted small">' + esc(L.Loading || '…') + '</div>'; return; }
        const p = preview();
        const i = weekIndexOf(ws);
        const weekSlots = slotsAll().filter(s => (s.weekStart || mondayYmd(s.plannedDate)) === ws);
        const movable = canMove(w);
        const wc = (p.weekCapacity || []).find(c => c.weekStart === ws);
        const parts = [];
        renderDoctors(ws, weekSlots);

        // head: "41. Hafta · 5–9 Eki" + status, the status line, the actions
        const subKey = w.storedStatus === 'legacy' ? 'LegacyPlanBand' : WEEK_SUB[w.status];
        // 4D's locked hint kept: a writer on an approved week reads how to change it (reopen); a past week stays closed.
        const lockedHint = !movable && canGenerate && !readOnly && w.status === 'approved' && w.storedStatus !== 'legacy' ? (L.MoveLockedHint || '') : '';
        const sub = [subKey ? (L[subKey] || '') : (movable ? (L.MoveHint || '') : ''), lockedHint].filter(Boolean).join(' · ');
        parts.push('<div class="d-flex justify-content-between align-items-start flex-wrap gap-2"><div>' +
            '<div class="d-flex align-items-center gap-2"><span class="h5 mb-0">' + esc(weekTitle(w) + ' · ' + weekRange(w)) + '</span>' +
            '<span class="badge bg-label-' + (w.storedStatus === 'legacy' ? 'success' : (STATUS_TONE[w.status] || 'secondary')) + '">' + esc(statusLabel(w)) + '</span></div>' +
            '<div class="small text-muted mt-1">' + esc(sub) + '</div></div>' +
            '<div class="d-flex flex-wrap gap-2">' + weekActions(w).map(a => '<button type="button" class="btn btn-sm btn-' + a.tone + ' js-wk-action" data-action="' + a.key + '"><i class="bx ' + a.icon + ' me-1"></i>' + esc(a.label || '') + '</button>').join('') + '</div></div>');

        if (w.status === 'empty') { // (3) the empty state only
            parts.push(emptyWeekHtml());
            host.innerHTML = parts.join('');
            return;
        }

        // pinned visits that did not fit their day
        parts.push(pinOverflowHtml(p, ws));
        (p.pinWarnings || []).filter(x => x.weekStart === ws).forEach(x => parts.push('<div class="alert alert-warning py-2 small" role="status">' + esc(reasonText(x.code) + ' · ' + x.date) + '</div>'));

        // day by day
        parts.push('<div><div class="text-uppercase small text-muted mb-2">' + esc(L.DayByDay || '') + '</div>');
        const days = daysOf(w, p, weekSlots);
        parts.push(days.length ? days.map(d => dayRow(d, movable)).join('') : '<div class="small text-muted">' + esc(L.NoVisitThisWeek || '') + '</div>');
        parts.push('</div>');

        // visits per product (name + bold count; a reminder-only product white with a frame) + the mixed order
        const counts = (wc && wc.productVisitCounts) || [];
        parts.push('<div><div class="text-uppercase small text-muted mb-2">' + esc(L.ProductVisitsTitle || '') + '</div>' +
            (counts.length ? '<div class="d-flex flex-wrap gap-2">' + counts.map(c => '<span class="badge ' + ((c.promoVisits || 0) > 0 ? 'bg-label-primary' : 'bg-transparent border text-body') + ' d-inline-flex gap-1 align-items-center" title="' + esc([c.productCode, fmt(L.ProductVisitsSplit || '{0} {1}', c.promoVisits || 0, (c.visits || 0) - (c.promoVisits || 0))].filter(Boolean).join(' · ')) + '">' + esc(VPF.productLabel(c)) + ' <strong>' + (c.visits || 0) + '</strong></span>').join('') + '</div>'
                : '<div class="small text-muted">' + esc(L.NoProductVisits || '') + '</div>') +
            '<div class="small bg-label-secondary rounded px-3 py-2 mt-2 d-flex gap-2"><i class="bx bx-shuffle text-primary"></i><span>' + esc(L.MixedOrderNote || '') + '</span></div></div>');

        // moved / not placed
        const slips = slipItems(p, i, ws, weekSlots);
        parts.push('<div><div class="text-uppercase small text-muted mb-2">' + esc(fmt(L.SlipTitleCount || '{0}', slips.length)) + '</div>' +
            (slips.length ? '<div class="d-flex flex-column gap-2">' + slips.map(slipRow).join('') + '</div>'
                : '<div class="small text-muted">' + esc(L.NoSlips || '') + '</div>') + '</div>');

        if (movable && w.status === 'draft') {
            parts.push('<button type="button" class="btn btn-link px-0 align-self-start js-wk-edit-targets"><i class="bx bx-edit-alt me-1"></i>' + esc(L.EditWeekTargets || '') + '</button>');
        }

        // an approved / reopened week's history
        const history = historyOf(ws);
        if (history.length) {
            parts.push('<div><div class="text-uppercase small text-muted mb-2">' + esc(L.WeekHistoryTitle || '') + '</div><ul class="list-unstyled small mb-0">' +
                history.map(h => '<li class="py-1 border-bottom"><span class="fw-medium">' + esc(h.action === 'reopen' ? (L.HistoryReopened || '') : (L.HistoryApproved || '')) + '</span> · ' +
                    esc(whoName(h.by)) + ' · ' + esc(h.at ? VPF.dateTime(h.at) : '') + (h.reason ? '<div class="text-muted">' + esc(h.reason) + '</div>' : '') + '</li>').join('') + '</ul></div>');
        }

        host.innerHTML = parts.join('');
    };

    const render = () => { renderStrip(); renderDetail(); };

    // ── 9 · after approving / reopening from here (both reload the page) the user stays on the Weeks tab, on the same
    // week (F4-6). A one-shot, short-lived note in sessionStorage; the header's own actions keep their behaviour. ──
    const RETURN_KEY = 'vp-return-weeks';
    const RETURN_TTL_MS = 5 * 60 * 1000;
    const rememberWeeksReturn = () => {
        try { sessionStorage.setItem(RETURN_KEY, JSON.stringify({ session: sessionId, week: page.state.weekStart, at: Date.now() })); } catch (e) { /* no storage: no return */ }
    };
    let returnNote = (() => {
        try {
            const raw = sessionStorage.getItem(RETURN_KEY);
            sessionStorage.removeItem(RETURN_KEY);
            const note = raw ? JSON.parse(raw) : null;
            return note && note.session === sessionId && Date.now() - (note.at || 0) < RETURN_TTL_MS ? note : null;
        } catch (e) { return null; }
    })();
    const returnToWeeks = () => {
        if (!returnNote) return;
        const note = returnNote; returnNote = null;
        if (note.week && page.week(note.week)) page.selectWeek(note.week, 'force');
        const tab = el('vp-tab-weeks-btn'); if (tab && window.bootstrap) window.bootstrap.Tab.getOrCreateInstance(tab).show();
    };

    // ── 4 · move between days (4E day pins through the existing session update) ──
    // Only a draft (or empty) week of a writable, non-legacy plan.
    const canMove = w => !!w && canGenerate && !readOnly && !page.isLegacy() && MOVABLE_WEEK_STATUSES.indexOf(w.status) > -1;
    const sameTarget = (pin, s) => pin.targetType === s.targetType && String(pin.targetId).toLowerCase() === String(s.targetId).toLowerCase();
    const membersOf = (slot, weekSlots) => weekSlots.filter(s => groupOf(s) === groupOf(slot));
    const weekPins = ws => (((session().weeks || []).find(w => w.weekStart === ws) || {}).dayPins || [])
        .map(p => ({ targetType: p.targetType, targetId: p.targetId, contactId: p.contactId || null, date: p.date, scope: p.scope || 'visit' }));
    // A move REPLACES the pins of what it moves: institution → every member's pin goes, one institution pin comes;
    // visit → that visit's own pin goes, one visit pin comes (it wins over an institution pin of its group).
    const pinsAfterMove = (current, slot, scope, date, members) => {
        const touched = scope === 'institution' ? members : [slot];
        const kept = current.filter(p => !touched.some(m => sameTarget(p, m)));
        kept.push({ targetType: slot.targetType, targetId: slot.targetId, contactId: slot.contactId || null, date: date, scope: scope });
        return kept;
    };
    // Unpinning a visit drops its own pin and its group's institution pin.
    const pinsAfterUnpin = (current, slot, members) => current.filter(p => !sameTarget(p, slot)
        && !(p.scope === 'institution' && members.some(m => sameTarget(p, m))));
    const savePins = (ws, pins) => request(base + '/sessions/' + encodeURIComponent(sessionId), {
        method: 'PUT',
        body: JSON.stringify({ dayPins: { weekStart: ws, pins: pins }, expectedVersion: session().version })
    }).then(r => {
        if (!r.ok) { window.showToast?.(errorText(r), 'error'); return false; }
        window.showToast?.(L.DayPinSaved || '', 'success');
        page.request('reload-plan');
        return true;
    });
    const dropDays = w => daysOf(w, preview(), []).filter(d => DROPPABLE_DAY_KINDS.indexOf(d.kind) > -1 && d.date >= todayYmd());

    let pendingMove = null; // { ws, slot, scope, members, date? }
    const moveModal = () => el('vp-move-modal');
    const askMove = (slot, scope, fixedDate) => {
        const ws = page.state.weekStart, w = page.week(ws);
        if (!canMove(w) || !moveModal() || !window.bootstrap) return;
        const weekSlots = slotsAll().filter(s => (s.weekStart || mondayYmd(s.plannedDate)) === ws);
        const members = membersOf(slot, weekSlots);
        const others = members.filter(m => m !== slot);
        pendingMove = { ws, slot, scope, members, date: fixedDate || null };
        el('vp-move-who').textContent = scope === 'institution' ? groupName(members) : visitName(slot) + ' · ' + groupName(members);
        const daySel = el('vp-move-day');
        daySel.innerHTML = dropDays(w).filter(d => d.date !== slot.plannedDate).map(d => '<option value="' + esc(d.date) + '">' + esc(dayLabel(d.date)) + '</option>').join('');
        if (fixedDate) daySel.value = fixedDate;
        el('vp-move-day-wrap').classList.toggle('d-none', !!fixedDate);
        const ask = scope === 'visit' && others.length > 0;
        const q = el('vp-move-question');
        q.classList.toggle('d-none', !ask);
        q.textContent = ask ? fmt(L.MoveAllQuestion || '{0} {1}', others.filter(m => m.contactId).length, others.filter(m => !m.contactId).length) : '';
        el('vp-move-one').classList.toggle('d-none', !ask);
        el('vp-move-all').textContent = ask ? (L.MoveAll || '') : (L.MoveConfirm || '');
        window.bootstrap.Modal.getOrCreateInstance(moveModal()).show();
    };
    const commitMove = scope => {
        if (!pendingMove) return;
        const date = pendingMove.date || el('vp-move-day').value;
        if (!date) return;
        const { ws, slot, members } = pendingMove;
        pendingMove = null;
        window.bootstrap?.Modal.getInstance(moveModal())?.hide();
        savePins(ws, pinsAfterMove(weekPins(ws), slot, scope, date, members)).then(ok => { if (ok) page.emit('day-pin:moved', { date: date }); });
    };
    const dropOn = (slot, scope, date) => {
        const ws = page.state.weekStart, w = page.week(ws);
        if (!canMove(w)) return;
        const day = dropDays(w).find(d => d.date === date);
        if (!day || date === slot.plannedDate) return; // never onto a holiday / weekend / gone day, never onto itself
        const weekSlots = slotsAll().filter(s => (s.weekStart || mondayYmd(s.plannedDate)) === ws);
        const members = membersOf(slot, weekSlots);
        if (scope === 'visit' && members.length > 1) { askMove(slot, 'visit', date); return; }
        savePins(ws, pinsAfterMove(weekPins(ws), slot, scope === 'institution' ? 'institution' : 'visit', date, members))
            .then(ok => { if (ok) page.emit('day-pin:moved', { date: date }); });
    };

    // ── events ──
    const pane = el('vp-tab-weeks');
    pane.addEventListener('click', e => {
        const item = e.target.closest('.vp-wk-item');
        if (item) { page.selectWeek(item.dataset.ws, 'weeks'); return; }
        const doctor = e.target.closest('.js-wk-doctor');
        if (doctor) { page.emit('doctor-panel:open', { contactId: doctor.dataset.cid, accountId: doctor.dataset.aid || null }); return; }
        const move = e.target.closest('.js-wk-move');
        if (move) { e.stopPropagation(); const s = slotsAll()[Number(move.dataset.slot)]; if (s) askMove(s, move.dataset.scope === 'institution' ? 'institution' : 'visit'); return; }
        const unpin = e.target.closest('.js-wk-unpin');
        if (unpin) {
            const s = slotsAll()[Number(unpin.dataset.slot)]; const ws = page.state.weekStart;
            if (s && canMove(page.week(ws))) {
                const weekSlots = slotsAll().filter(x => (x.weekStart || mondayYmd(x.plannedDate)) === ws);
                savePins(ws, pinsAfterUnpin(weekPins(ws), s, membersOf(s, weekSlots)));
            }
            return;
        }
        if (e.target.closest('.js-wk-all-docs')) { allDoctorsShown = !allDoctorsShown; renderDetail(); return; }
        if (e.target.closest('.js-wk-edit-targets')) {
            const t = el('vp-tab-targets-btn'); if (t && window.bootstrap) window.bootstrap.Tab.getOrCreateInstance(t).show();
            return;
        }
        const more = e.target.closest('.js-wk-more');
        if (more) { fullDays.add(more.dataset.date); renderDetail(); return; }
        const dayBtn = e.target.closest('.js-wk-day');
        if (dayBtn) { const d = dayBtn.dataset.date; if (openDays.has(d)) { openDays.delete(d); fullDays.delete(d); } else openDays.add(d); renderDetail(); return; }
        const act = e.target.closest('.js-wk-action');
        if (act) {
            const k = act.dataset.action;
            if (k === 'approve') { rememberWeeksReturn(); page.request('approve-week'); }
            else if (k === 'rebuild') page.request('reload-plan');
            else if (k === 'reopen') { rememberWeeksReturn(); page.request('reopen-week'); }
            else if (k === 'route') {
                page.selectWeek(page.state.weekStart, 'force');
                const btn = el('vp-tab-route-btn'); if (btn && window.bootstrap) window.bootstrap.Tab.getOrCreateInstance(btn).show();
            }
        }
    });
    pane.addEventListener('dragstart', e => {
        const src = e.target.closest('[data-drag]'); if (!src || !canMove(page.week(page.state.weekStart))) { e.preventDefault(); return; }
        e.stopPropagation();
        e.dataTransfer.setData('text/plain', JSON.stringify({ slot: Number(src.dataset.slot), scope: src.dataset.drag }));
        e.dataTransfer.effectAllowed = 'move';
    });
    pane.addEventListener('dragover', e => {
        const day = e.target.closest('.vp-wk-day');
        if (day && day.dataset.droppable === '1') { e.preventDefault(); e.dataTransfer.dropEffect = 'move'; }
    });
    pane.addEventListener('drop', e => {
        const day = e.target.closest('.vp-wk-day'); if (!day || day.dataset.droppable !== '1') return;
        e.preventDefault();
        let data = null; try { data = JSON.parse(e.dataTransfer.getData('text/plain')); } catch (x) { data = null; }
        const s = data ? slotsAll()[data.slot] : null;
        if (s) dropOn(s, data.scope, day.dataset.date);
    });
    el('vp-move-all')?.addEventListener('click', () => commitMove(pendingMove && pendingMove.scope === 'visit' && !el('vp-move-one').classList.contains('d-none') ? 'institution' : (pendingMove ? pendingMove.scope : 'visit')));
    el('vp-move-one')?.addEventListener('click', () => commitMove('visit'));

    // ── WP-VP-4F — the Route moves stops with this same component ──
    // details.js asks: 'request:move-visit' { slot, scope, date } — a tab drop (date) or "Move to day…" (no date: the
    // dialog asks for the day). The same rules, the same question, the same day pins.
    page.on('request:move-visit', e => {
        if (!e || !e.slot) return;
        if (e.date) dropOn(e.slot, e.scope, e.date);
        else askMove(e.slot, e.scope === 'institution' ? 'institution' : 'visit');
    });
    // Every drawn route day (and every opened stop): its pin / auto-pin marks, "Remove the pin", "Move to day…" and the
    // overflow line — the Weeks controls, on the Route's cards. The cards themselves stay the Route's.
    const routeHost = () => el('vp-visit-cards');
    const routeControls = (slots, scope, lockedStop) => {
        const lead = slots.find(s => s.isPinned) || slots[0];
        // a pharmacy stop rides with its clinic on the route (its own lock): marks only, it moves with the clinic
        const movable = !lockedStop && canMove(page.week(page.state.weekStart));
        const pinned = slots.find(s => s.isPinned);
        const idx = slotsAll().indexOf(lead);
        return '<span class="js-route-pin d-inline-flex align-items-center gap-1">' + (pinned ? pinMark(pinned) : '') +
            (movable && pinned ? '<button type="button" class="btn btn-sm btn-text-secondary px-1 js-route-unpin" data-slot="' + slotsAll().indexOf(pinned) + '" title="' + esc(L.Unpin || '') + '" aria-label="' + esc(L.Unpin || '') + '"><i class="bx bx-pin"></i><i class="bx bx-x small"></i></button>' : '') +
            (movable ? '<button type="button" class="btn btn-sm btn-text-secondary px-1 js-route-move" data-slot="' + idx + '" data-scope="' + scope + '" title="' + esc(scope === 'institution' ? (L.MoveInstitution || '') : (L.MoveToDay || '')) + '" aria-label="' + esc(scope === 'institution' ? (L.MoveInstitution || '') : (L.MoveToDay || '')) + '"><i class="bx bx-calendar-edit"></i></button>' : '') +
            '</span>';
    };
    const decorateRoute = date => {
        const host = routeHost(); if (!host || !date) return;
        const day = slotsAll().filter(s => s.plannedDate === date);
        host.querySelectorAll('.js-route-pin').forEach(n => n.remove());
        host.querySelectorAll('.vp-block[data-acc]').forEach(block => {
            const slots = day.filter(s => String(s.accountId || s.targetId) === block.dataset.acc);
            const actions = block.querySelector('.inbox-row__actions');
            if (slots.length && actions) actions.insertAdjacentHTML('afterbegin', routeControls(slots, 'institution', !!block.closest('.vp-tl-row--pharmacy')));
        });
        host.querySelectorAll('.vp-visit[data-cid]').forEach(card => {
            const slots = day.filter(s => String(s.contactId || s.targetId) === card.dataset.cid);
            const line = card.querySelector('.inbox-row__line--primary');
            if (slots.length && line) line.insertAdjacentHTML('beforeend', routeControls(slots, 'visit'));
        });
        const note = el('vp-route-pin-overflow');
        if (note) note.innerHTML = pinOverflowHtml(preview(), page.state.weekStart);
    };
    page.on('route:day-rendered', e => decorateRoute(e && e.date));
    routeHost()?.addEventListener('click', e => {
        const move = e.target.closest('.js-route-move');
        const unpin = e.target.closest('.js-route-unpin');
        if (!move && !unpin) return;
        e.preventDefault(); e.stopPropagation();
        const s = slotsAll()[Number((move || unpin).dataset.slot)]; if (!s) return;
        if (move) { askMove(s, move.dataset.scope === 'institution' ? 'institution' : 'visit'); return; }
        const ws = page.state.weekStart;
        if (!canMove(page.week(ws))) return;
        const weekSlots = slotsAll().filter(x => (x.weekStart || mondayYmd(x.plannedDate)) === ws);
        savePins(ws, pinsAfterUnpin(weekPins(ws), s, membersOf(s, weekSlots)));
    });

    page.on('session', () => { render(); loadTargets(); });
    page.on('preview', () => { render(); returnToWeeks(); });
    page.on('week-change', () => { openDays.clear(); fullDays.clear(); allDoctorsShown = false; render(); });
    loadMe();
})(window, document);
