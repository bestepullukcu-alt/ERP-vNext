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
    const dm = d => d.toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
    const dayName = d => d.toLocaleDateString(undefined, { weekday: 'long' });
    const dayLabel = v => { const d = localDate(v); return isNaN(d) ? v : dayName(d) + ' ' + dm(d); };
    const weekTitle = w => fmt(L.WeekNumberLabel || '{0}', w.isoWeek);
    const weekRange = w => { const f = localDate(w.from || w.weekStart), t = localDate(w.to || w.weekStart); return isNaN(f) || isNaN(t) ? '' : dm(f) + ' – ' + dm(t); };
    const hours = minutes => fmt(L.HoursFormat || '{0} h', (Math.round(Number(minutes || 0) / 6) / 10).toLocaleString(undefined, { maximumFractionDigits: 1 }));
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
        max_promo: 'ReasonMaxPromo', max_non_promo: 'ReasonMaxNonPromo'
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

    // ── 1 · the period strip ──
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
    const renderStrip = () => {
        const host = el('vp-wk-strip'); if (!host) return;
        const model = stripModel();
        setSummary(model);
        if (!model.length) { host.innerHTML = '<div class="text-muted small">' + esc(L.Loading || '…') + '</div>'; return; }
        host.innerHTML = model.map(m => '<button type="button" role="option" aria-selected="' + (m.selected ? 'true' : 'false') + '" class="btn text-start border p-2 flex-shrink-0 vp-wk-item' + (m.selected ? ' border-primary bg-label-primary' : '') + '" data-ws="' + esc(m.ws) + '" style="min-width:150px">' +
            '<span class="d-flex justify-content-between align-items-center gap-2"><span class="fw-medium">' + esc(m.title) + '</span>' + (m.isToday ? '<span class="badge bg-primary">' + esc(L.TodayLabel || '') + '</span>' : '') + '</span>' +
            '<span class="d-block small text-muted">' + esc(m.range) + '</span>' +
            '<span class="d-flex justify-content-between align-items-center mt-1 gap-1"><span class="badge bg-label-' + (m.week.storedStatus === 'legacy' ? 'success' : (STATUS_TONE[m.status] || 'secondary')) + '">' + esc(m.label) + '</span>' +
            '<span class="small">' + esc(fmt(L.VisitCountShort || '{0}', m.visits)) + '</span></span>' +
            '<span class="progress mt-2 d-flex" style="height:4px"><span class="progress-bar' + (m.pct >= 100 ? ' bg-warning' : '') + '" style="width:' + m.pct + '%"></span></span>' +
            '<span class="d-flex flex-wrap gap-1 mt-1">' +
            m.holidays.map(h => '<span class="badge bg-label-danger" title="' + esc(L.HolidayMarker || '') + '">' + esc(dm(localDate(h))) + '</span>').join('') +
            m.halfDays.map(h => '<span class="badge bg-label-warning" title="' + esc(L.HalfDayLabel || '') + '">½ ' + esc(dm(localDate(h))) + '</span>').join('') +
            (m.warnings ? '<span class="badge bg-label-warning" title="' + esc(L.SlipTitle || '') + '"><i class="bx bx-error"></i> ' + m.warnings + '</span>' : '') +
            '</span></button>').join('');
    };
    const setSummary = model => {
        const n = el('vp-wk-summary'); if (!n) return;
        const count = st => model.filter(m => m.status === st).length;
        n.textContent = fmt(L.PeriodWeeksSummary || '{0}', model.length, count('approved'), count('draft'), count('empty'), count('past'));
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

    const chipHtml = c => {
        const promo = c.role !== 'non-promo';
        return '<span class="badge rounded-pill ' + (promo ? 'bg-label-primary' : 'bg-transparent border text-body') + '" title="' + esc(promo ? (L.LegendPromo || '') : (L.LegendNonPromo || '')) + '">' + esc(c.productCode || '—') + '</span>';
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
    const visitLine = (s, movable) => {
        const idx = slotsAll().indexOf(s);
        const name = visitName(s);
        const nameHtml = s.contactId
            ? '<button type="button" class="btn btn-link p-0 text-start fw-medium js-wk-doctor" data-cid="' + esc(s.contactId) + '" data-aid="' + esc(s.accountId || '') + '">' + esc(name) + '</button>'
            : '<span class="fw-medium">' + esc(name) + '</span>';
        return '<div class="d-flex align-items-center gap-2 py-1 ps-3 vp-wk-visit"' + (movable ? ' draggable="true" data-drag="visit" data-slot="' + idx + '"' : '') + '>' +
            (movable ? '<i class="bx bx-grid-vertical text-muted" aria-hidden="true"></i>' : '') + pinMark(s) +
            '<span class="flex-grow-1" style="min-width:0">' + nameHtml + ' <span class="d-inline-flex flex-wrap gap-1 align-middle">' + chipsOf(s) + '</span></span>' +
            '<span class="small text-muted text-nowrap">' + esc(fmt(L.ApproxMinutes || '{0}', s.durationMinutes || 0)) + '</span>' +
            (movable ? '<button type="button" class="btn btn-sm btn-text-secondary px-1 js-wk-move" data-slot="' + idx + '" data-scope="visit" title="' + esc(L.MoveToDay || '') + '" aria-label="' + esc(L.MoveToDay || '') + '"><i class="bx bx-calendar-edit"></i></button>' : '') +
            (movable && s.isPinned ? '<button type="button" class="btn btn-sm btn-text-secondary px-1 js-wk-unpin" data-slot="' + idx + '" title="' + esc(L.Unpin || '') + '" aria-label="' + esc(L.Unpin || '') + '"><i class="bx bx-pin"></i><i class="bx bx-x small"></i></button>' : '') +
            '</div>';
    };
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
            return '<div class="border-top pt-1"' + (movable ? ' draggable="true" data-drag="institution" data-slot="' + first + '"' : '') + '>' +
                '<div class="d-flex align-items-center gap-2 small text-muted text-uppercase fw-semibold py-1">' + (movable ? '<i class="bx bx-grid-vertical" aria-hidden="true"></i>' : '') +
                '<span class="flex-grow-1">' + esc(groupName(g.slots)) + '</span>' +
                (movable ? '<button type="button" class="btn btn-sm btn-text-secondary px-1 js-wk-move" data-slot="' + first + '" data-scope="institution" title="' + esc(L.MoveInstitution || '') + '" aria-label="' + esc(L.MoveInstitution || '') + '"><i class="bx bx-calendar-edit"></i></button>' : '') +
                '</div>' + lines.map(v => visitLine(v, movable)).join('') + '</div>';
        }).join('');
        const more = day.slots.length - DAY_PREVIEW_LIMIT;
        return '<div class="border rounded mb-2 vp-wk-day" data-date="' + esc(day.date) + '" data-droppable="' + (droppable ? '1' : '0') + '">' +
            '<button type="button" class="btn w-100 text-start d-flex align-items-center gap-3 p-2 js-wk-day" aria-expanded="' + (open ? 'true' : 'false') + '" data-date="' + esc(day.date) + '">' +
            '<span class="fw-medium" style="min-width:150px">' + esc(dayName(day.d)) + ' <span class="text-muted small">' + esc(dm(day.d)) + '</span></span>' +
            '<span class="flex-grow-1"><span class="progress d-flex" style="height:6px"><span class="progress-bar' + (s && s.overCapacity ? ' bg-danger' : '') + '" style="width:' + pct + '%"></span></span></span>' +
            '<span class="small text-nowrap">' + esc(day.cap != null ? fmt(L.DayCapacityFormat || '{0} / {1}', day.slots.length, day.cap) : String(day.slots.length)) + '</span>' +
            badges.join(' ') + '</button>' +
            '<div class="px-2 pb-2' + (open ? '' : ' d-none') + '">' +
            (day.slots.length ? body + (!full && more > 0 ? '<button type="button" class="btn btn-sm btn-link px-3 js-wk-more" data-date="' + esc(day.date) + '">' + esc(fmt(L.MoreDoctors || '{0}', more)) + '</button>' : '')
                : '<div class="small text-muted px-3 py-1">' + esc(day.kind === 'holiday' ? (L.HolidayNoVisit || L.NoVisitThisDay || '') : (L.NoVisitThisDay || '')) + '</div>') +
            '</div></div>';
    };

    // Moved / not placed: shifted (with the target week), unscheduled, overflow products, pinned visits that moved.
    const slipItems = (p, i, ws, weekSlots) => {
        const out = [];
        const isoOf = k => { const w = page.weeks()[k]; return w ? weekTitle(w) : '—'; };
        (p.shifted || []).filter(s => s.fromWeek === i).forEach(s => out.push({
            kind: 'shift', name: s.displayName || (s.contactId && names.doctors[s.contactId] ? names.doctors[s.contactId].name : accountName(s.targetId)),
            reason: reasonText(s.reason), result: fmt(L.MovedToWeek || '{0}', isoOf(s.toWeek))
        }));
        (p.unscheduled || []).filter(u => u.weekNumber === i).forEach(u => out.push({
            kind: 'unscheduled', name: u.contactId && names.doctors[u.contactId] ? names.doctors[u.contactId].name : accountName(u.targetId),
            reason: reasonText(u.reason), result: L.NotPlanned || ''
        }));
        weekSlots.forEach(s => (s.overflowProducts || []).forEach(o => out.push({
            kind: 'product', name: (o.productCode || '—') + ' · ' + visitName(s), reason: reasonText(o.reason), result: L.ToNextVisit || ''
        })));
        (p.pinOverflow || []).filter(o => mondayYmd(o.fromDate) === ws).forEach(o => out.push({
            kind: 'pin', name: o.displayName || (o.contactId && names.doctors[o.contactId] ? names.doctors[o.contactId].name : accountName(o.targetId)),
            reason: reasonText(o.reason), result: o.toDate ? fmt(L.MovedToDay || '{0}', dayLabel(o.toDate)) : (L.NotPlanned || '')
        }));
        return out;
    };
    const SLIP_ICON = { shift: 'bx-right-arrow-alt', unscheduled: 'bx-block', product: 'bx-package', pin: 'bx-transfer-alt' };

    // The week's own actions — the same rule as the header (VisitPlanningPage.actionsFor) and the same functions.
    const weekActions = w => {
        const legacy = page.isLegacy();
        const offered = page.actionsFor(w.status, legacy);
        const out = [];
        if (offered.indexOf('approveWeek') > -1 && canApply) out.push({ key: 'approve', label: L.ApproveWeek, icon: 'bx-check-double', tone: 'success' });
        if (!legacy && canGenerate && MOVABLE_WEEK_STATUSES.indexOf(w.status) > -1) out.push({ key: 'rebuild', label: L.RebuildWeek, icon: 'bx-refresh', tone: 'label-primary' });
        if (!legacy && (w.visitCount || 0) > 0) out.push({ key: 'route', label: L.OpenRoute, icon: 'bx-map-alt', tone: 'label-secondary' });
        if (offered.indexOf('reopenWeek') > -1 && canApply) out.push({ key: 'reopen', label: L.ReopenWeek, icon: 'bx-lock-open-alt', tone: 'label-warning' });
        return out;
    };

    // The doctors of the week with their period dots (presented / approved / draft / projected).
    const visitState = (s, firstDraftWeek) => {
        if (s.plannedDate < todayYmd()) return 'presented';
        if (s.isFixed) return 'approved';
        return (s.weekStart || mondayYmd(s.plannedDate)) === firstDraftWeek ? 'draft' : 'projected';
    };
    const STATE_LABEL = { presented: 'StatePresented', approved: 'StateApproved', draft: 'StateDraft', projected: 'StateProjected' };
    const STATE_TONE = { presented: 'secondary', approved: 'success', draft: 'primary', projected: 'info' };
    const firstDraftWeekStart = () => { const w = page.weeks().find(x => x.status === 'draft'); return w ? w.weekStart : null; };

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

        // head: title + status + actions
        parts.push('<div class="d-flex justify-content-between align-items-start flex-wrap gap-2 mb-3"><div>' +
            '<div class="d-flex align-items-center gap-2"><span class="h5 mb-0">' + esc(weekTitle(w) + ' · ' + weekRange(w)) + '</span>' +
            '<span class="badge bg-label-' + (w.storedStatus === 'legacy' ? 'success' : (STATUS_TONE[w.status] || 'secondary')) + '">' + esc(statusLabel(w)) + '</span></div>' +
            '<div class="small text-muted mt-1">' + esc(movable ? (L.MoveHint || '') : (MOVABLE_WEEK_STATUSES.indexOf(w.status) > -1 ? '' : (L.MoveLockedHint || ''))) + '</div></div>' +
            '<div class="d-flex flex-wrap gap-2">' + weekActions(w).map(a => '<button type="button" class="btn btn-sm btn-' + a.tone + ' js-wk-action" data-action="' + a.key + '"><i class="bx ' + a.icon + ' me-1"></i>' + esc(a.label || '') + '</button>').join('') + '</div></div>');

        // pinned visits that did not fit their day
        const moved = (p.pinOverflow || []).filter(o => mondayYmd(o.fromDate) === ws);
        if (moved.length) {
            const byTarget = {};
            moved.forEach(o => { const k = o.toDate || ''; byTarget[k] = (byTarget[k] || 0) + 1; });
            parts.push('<div class="alert alert-info py-2 small" role="status"><i class="bx bx-transfer-alt me-1"></i>' + Object.keys(byTarget).map(k =>
                esc(k ? fmt(L.PinOverflowMessage || '{0} {1}', byTarget[k], dayLabel(k)) : fmt(L.PinOverflowNextWeek || '{0}', byTarget[k]))).join(' · ') + '</div>');
        }
        (p.pinWarnings || []).filter(x => x.weekStart === ws).forEach(x => parts.push('<div class="alert alert-warning py-2 small" role="status">' + esc(reasonText(x.code) + ' · ' + x.date) + '</div>'));

        // day by day
        parts.push('<div class="text-uppercase small fw-semibold text-muted mb-2">' + esc(L.DayByDay || '') + '</div>');
        const days = daysOf(w, p, weekSlots);
        parts.push(days.length ? days.map(d => dayRow(d, movable)).join('') : '<div class="small text-muted">' + esc(L.NoVisitThisWeek || '') + '</div>');

        // visits per product + the mixed order
        const counts = (wc && wc.productVisitCounts) || [];
        parts.push('<div class="row g-4 mt-1"><div class="col-12 col-lg-6">' +
            '<div class="text-uppercase small fw-semibold text-muted mb-2">' + esc(L.ProductVisitsTitle || '') + '</div>' +
            (counts.length ? '<div class="d-flex flex-wrap gap-2">' + counts.map(c => '<span class="badge bg-label-primary" title="' + esc(fmt(L.ProductVisitsSplit || '{0} {1}', c.promoVisits || 0, (c.visits || 0) - (c.promoVisits || 0))) + '">' + esc(c.productCode || '—') + ' <strong>' + (c.visits || 0) + '</strong></span>').join('') + '</div>'
                : '<div class="small text-muted">' + esc(L.NoProductVisits || '') + '</div>') +
            '<div class="small text-muted mt-2"><i class="bx bx-shuffle me-1"></i>' + esc(L.MixedOrderNote || '') + '</div></div>');

        // moved / not placed
        const slips = slipItems(p, i, ws, weekSlots);
        parts.push('<div class="col-12 col-lg-6"><div class="text-uppercase small fw-semibold text-muted mb-2">' + esc(fmt(L.SlipTitleCount || '{0}', slips.length)) + '</div>' +
            (slips.length ? '<ul class="list-unstyled small mb-0">' + slips.map(x => '<li class="d-flex gap-2 py-1 border-bottom vp-wk-slip" data-kind="' + x.kind + '"><i class="bx ' + SLIP_ICON[x.kind] + ' text-warning"></i><span class="flex-grow-1"><span class="fw-medium">' + esc(x.name) + '</span> <span class="text-muted">· ' + esc(x.reason) + '</span></span><span class="text-nowrap">' + esc(x.result) + '</span></li>').join('') + '</ul>'
                : '<div class="small text-muted">' + esc(L.NoSlips || '') + '</div>') + '</div></div>');

        // the doctors of the week + their period dots
        const firstDraft = firstDraftWeekStart();
        const doctors = [];
        weekSlots.filter(s => s.contactId).forEach(s => { if (!doctors.some(d => d.contactId === s.contactId)) doctors.push(s); });
        parts.push('<div class="mt-4"><div class="text-uppercase small fw-semibold text-muted mb-1">' + esc(fmt(L.WeekDoctorsTitle || '{0}', doctors.length)) + '</div>' +
            '<div class="small text-muted mb-2">' + esc(L.WeekDoctorsHint || '') + '</div>' +
            (doctors.length ? '<div class="list-group list-group-flush">' + doctors.map(s => {
                const freq = s.requiredVisitCount != null && s.frequencyStatus !== 'unknown'
                    ? fmt(L.FrequencyPerPeriod || '{0}', s.requiredVisitCount) : (L.FrequencyNone || '');
                const dots = page.weeks().map(pw => {
                    const v = slotsAll().find(x => x.contactId === s.contactId && (x.weekStart || mondayYmd(x.plannedDate)) === pw.weekStart);
                    if (!v) return '<span class="badge rounded-pill bg-label-secondary opacity-25" title="' + esc(weekTitle(pw)) + '">&nbsp;</span>';
                    const st = visitState(v, firstDraft);
                    return '<span class="badge rounded-pill bg-label-' + STATE_TONE[st] + '" title="' + esc(weekTitle(pw) + ' · ' + (L[STATE_LABEL[st]] || st)) + '">&nbsp;</span>';
                }).join('');
                return '<button type="button" class="list-group-item list-group-item-action d-flex align-items-center gap-3 js-wk-doctor" data-cid="' + esc(s.contactId) + '" data-aid="' + esc(s.accountId || '') + '">' +
                    '<span class="flex-grow-1" style="min-width:0"><span class="fw-medium d-block text-truncate">' + esc(visitName(s)) + '</span><span class="small text-muted">' + esc(accountName(s.accountId)) + ' · ' + esc(freq) + '</span></span>' +
                    '<span class="d-flex gap-1 flex-wrap">' + dots + '</span></button>';
            }).join('') + '</div>' : '<div class="small text-muted">' + esc(L.NoDoctorsThisWeek || '') + '</div>') +
            '<div class="d-flex flex-wrap gap-3 small text-muted mt-2">' + Object.keys(STATE_LABEL).map(k => '<span><span class="badge rounded-pill bg-label-' + STATE_TONE[k] + '">&nbsp;</span> ' + esc(L[STATE_LABEL[k]] || k) + '</span>').join('') + '</div></div>');

        // an approved / reopened week's history
        const history = historyOf(ws);
        if (history.length) {
            parts.push('<div class="mt-4"><div class="text-uppercase small fw-semibold text-muted mb-2">' + esc(L.WeekHistoryTitle || '') + '</div><ul class="list-unstyled small mb-0">' +
                history.map(h => '<li class="py-1 border-bottom"><span class="fw-medium">' + esc(h.action === 'reopen' ? (L.HistoryReopened || '') : (L.HistoryApproved || '')) + '</span> · ' +
                    esc(whoName(h.by)) + ' · ' + esc(h.at ? new Date(h.at).toLocaleString() : '') + (h.reason ? '<div class="text-muted">' + esc(h.reason) + '</div>' : '') + '</li>').join('') + '</ul></div>');
        }

        host.innerHTML = parts.join('');
    };

    const render = () => { renderStrip(); renderDetail(); };

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
        if (!r.ok) { window.showToast?.(errorText(r), 'error'); return; }
        window.showToast?.(L.DayPinSaved || '', 'success');
        page.request('reload-plan');
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
        savePins(ws, pinsAfterMove(weekPins(ws), slot, scope, date, members));
    };
    const dropOn = (slot, scope, date) => {
        const ws = page.state.weekStart, w = page.week(ws);
        if (!canMove(w)) return;
        const day = dropDays(w).find(d => d.date === date);
        if (!day || date === slot.plannedDate) return; // never onto a holiday / weekend / gone day, never onto itself
        const weekSlots = slotsAll().filter(s => (s.weekStart || mondayYmd(s.plannedDate)) === ws);
        const members = membersOf(slot, weekSlots);
        if (scope === 'visit' && members.length > 1) { askMove(slot, 'visit', date); return; }
        savePins(ws, pinsAfterMove(weekPins(ws), slot, scope === 'institution' ? 'institution' : 'visit', date, members));
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
        const more = e.target.closest('.js-wk-more');
        if (more) { fullDays.add(more.dataset.date); renderDetail(); return; }
        const dayBtn = e.target.closest('.js-wk-day');
        if (dayBtn) { const d = dayBtn.dataset.date; if (openDays.has(d)) { openDays.delete(d); fullDays.delete(d); } else openDays.add(d); renderDetail(); return; }
        const act = e.target.closest('.js-wk-action');
        if (act) {
            const k = act.dataset.action;
            if (k === 'approve') page.request('approve-week');
            else if (k === 'rebuild') page.request('reload-plan');
            else if (k === 'reopen') page.request('reopen-week');
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

    page.on('session', () => { render(); loadTargets(); });
    page.on('preview', () => render());
    page.on('week-change', () => { openDays.clear(); fullDays.clear(); render(); });
    loadMe();
})(window, document);
