/**
 * WP-CYC-UI-2 Cycle Capacity — the Create / Edit page (mockup 05: two columns, live summary).
 *
 * What this file does, and — more importantly — what it does NOT do.
 *
 * 1. LIVE SUMMARY. While the author types, ONE debounced POST goes to the same-origin preview proxy. The proxy builds
 *    the CRM's preview body (typical triple all-or-none) and answers with the CRM's calculation PLUS a `summary` the
 *    Web server derived from it. This file only PAINTS that answer: the visit number, the day / minute steps, the
 *    blockers and warnings (as codes, localised here), and the read-only cells of the month table.
 *    There is NO capacity arithmetic in this file. A figure computed in JavaScript would eventually disagree with the
 *    one the CRM stores, and the author would trust the wrong one.
 *    K-4: when the calendar did not resolve, the summary carries no number and the page shows none — never a weekday
 *    estimate, never a zero.
 *
 * 2. ONE REQUEST AT A TIME. Typing schedules a request DEBOUNCE_MS after the last keystroke; a newer request aborts the
 *    older one, an answer that is no longer the newest is dropped, and a payload identical to the last one answered is
 *    not sent again.
 *
 * 3. FTE (K-5). The month FTE is authorable (0–1, step 0.05). The visible input has no name; its value travels in an
 *    invariant hidden twin, and the month is marked TOUCHED the moment the author changes it — only touched months are
 *    sent on save, so an untouched month keeps its stored value.
 *
 * 4. "APPLY TO ALL MONTHS" copies the bulk row into every month (an FTE copied this way counts as authored).
 *
 * 5. KEYBOARD. Esc in a month cell restores the value it had when it received focus; the bulk row and the summary's
 *    retry button are ordinary focusable controls in document order.
 */
(function (window, document) {
    'use strict';

    const form = document.getElementById('cycleCapacityForm');
    if (!form) return;

    const L = window.CycleCapacitiesL10n || window.L10n || {};
    const CODES = L.Codes || {};
    const PREVIEW_URL = '/CRM/CycleCapacities/api/capacities/calculation-preview';
    const DEBOUNCE_MS = 450;
    const EMPTY = '—';

    const isNew = form.dataset.isNew === 'true';
    const readOnly = form.dataset.readOnly === 'true';
    const lang = document.documentElement.lang || undefined;
    const numberFormat = new Intl.NumberFormat(lang);
    const fteFormat = new Intl.NumberFormat(lang, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    const summaryEl = document.getElementById('capacitySummary');
    const monthsTableEl = document.getElementById('capacityMonthsTable');
    const totalEl = document.getElementById('livePreviewTotal');
    const formulaEl = document.querySelector('#typicalFormula [data-formula-text]');
    const saveBtn = document.getElementById('btnSaveCapacity');
    const periodSelect = document.getElementById('cyclePeriodSelect');

    const fmt = v => (v === null || v === undefined || Number.isNaN(Number(v))) ? EMPTY : numberFormat.format(Number(v));
    const intOrNull = v => {
        const text = String(v ?? '').trim();
        if (text === '') return null;
        const n = Number(text);
        return Number.isFinite(n) ? Math.trunc(n) : null;
    };
    const decOrNull = v => {
        const text = String(v ?? '').trim().replace(',', '.');
        if (text === '') return null;
        const n = Number(text);
        return Number.isFinite(n) ? n : null;
    };
    const field = name => form.querySelector(`[name="${name}"]`);
    const valueOf = name => field(name)?.value ?? '';

    /// "12 h 30 min" for a minute count — a unit conversion for the reader, not a capacity figure.
    const hoursHint = minutes => {
        if (minutes === null || minutes === undefined) return '';
        const h = Math.floor(Number(minutes) / 60);
        const m = Number(minutes) % 60;
        return (L.HoursMinutesFormat || '{h} h {m} min').replace('{h}', numberFormat.format(h)).replace('{m}', numberFormat.format(m));
    };

    const codeText = note => {
        const text = CODES[note.code] || CODES.unknown || note.code;
        if (!note.year || !note.monthNumber) return text;
        const label = new Date(Date.UTC(note.year, note.monthNumber - 1, 1))
            .toLocaleDateString(lang, { month: 'long', year: 'numeric', timeZone: 'UTC' });
        return `${label}: ${text}`;
    };

    // ── state → preview input ────────────────────────────────────────────────────────────────────────────────────

    const cyclePeriodId = () => {
        for (const el of form.querySelectorAll('[name="CyclePeriodId"]')) {
            const value = (el.value || '').trim();
            if (value) return value;
        }
        return '';
    };

    const monthRows = () => Array.from(monthsTableEl?.querySelectorAll('tr[data-month-key]') || []);

    /// One month row → the preview's month. Inputs are read by NAME (Months[i].Prop), never by column position.
    const readMonth = row => {
        const get = prop => row.querySelector(`[name$=".${prop}"]`);
        return {
            year: intOrNull(get('Year')?.value),
            monthNumber: intOrNull(get('MonthNumber')?.value),
            meetingDays: intOrNull(get('MeetingDays')?.value),
            trainingDays: intOrNull(get('TrainingDays')?.value),
            vacationDays: intOrNull(get('VacationDays')?.value),
            microTargetingDayCount: intOrNull(get('MicroTargetingDayCount')?.value),
            microTargetingDuration: intOrNull(get('MicroTargetingDuration')?.value),
            // The FTE on SCREEN — the preview must be built on what the author sees.
            fte: decOrNull(row.querySelector('.js-fte-input')?.value)
        };
    };

    const buildPreviewInput = () => {
        const periodId = cyclePeriodId();
        const months = monthRows().map(readMonth).filter(m => m.year && m.monthNumber);
        if (!periodId || months.length === 0) return null;
        return {
            cyclePeriodId: periodId,
            calendarCountryCode: (valueOf('CalendarCountryCode') || '').trim() || null,
            dailyWorkMinutes: intOrNull(valueOf('DailyWorkMinutes')),
            promoProductTime: intOrNull(valueOf('PromoProductTime')),
            nonPromoProductTime: intOrNull(valueOf('NonPromoProductTime')),
            travelingTime: intOrNull(valueOf('TravelingTime')),
            reportDuration: intOrNull(valueOf('ReportDuration')),
            quizDuration: intOrNull(valueOf('QuizDuration')),
            typicalPromoCount: intOrNull(valueOf('TypicalPromoCount')),
            typicalNonPromoCount: intOrNull(valueOf('TypicalNonPromoCount')),
            reportMinutesPerVisit: intOrNull(valueOf('ReportMinutesPerVisit')),
            maxPromoProducts: intOrNull(valueOf('MaxPromoProducts')),
            maxNonPromoProducts: intOrNull(valueOf('MaxNonPromoProducts')),
            isNew,
            months
        };
    };

    // ── summary painting ─────────────────────────────────────────────────────────────────────────────────────────

    const part = name => summaryEl?.querySelector(`[data-summary="${name}"]`);
    const setState = state => {
        summaryEl?.querySelectorAll('[data-summary-state]').forEach(el => el.classList.toggle('d-none', el.dataset.summaryState !== state));
        part('live')?.setAttribute('aria-busy', state === 'loading' ? 'true' : 'false');
    };

    const CALENDAR_BADGE = {
        resolved: ['success', 'CalendarResolvedShort'],
        calendar_unresolved: ['warning', 'CalendarUnresolvedShort'],
        calendar_forbidden: ['danger', 'CalendarForbiddenShort'],
        unavailable: ['secondary', 'CalendarUnknownShort']
    };

    const paintBadge = status => {
        const badge = part('calendarBadge');
        if (!badge) return;
        const [tone, key] = CALENDAR_BADGE[status] || CALENDAR_BADGE.unavailable;
        badge.className = `badge bg-label-${tone}`;
        badge.textContent = L[key] || EMPTY;
    };

    const fillList = (name, notes) => {
        const list = part(name);
        if (!list) return;
        list.innerHTML = '';
        notes.forEach(n => {
            const li = document.createElement('li');
            li.textContent = codeText(n);
            list.appendChild(li);
        });
    };

    /// Why there is no number: the blockers, or the calendar's own answer (K-4), or "unavailable".
    const noNumberReason = summary => {
        if ((summary.blocks || []).length > 0) return L.NoNumberBlocked || '';
        if (summary.calendarStatus === 'calendar_forbidden') return L.CalendarForbiddenBody || '';
        if (summary.calendarStatus === 'calendar_unresolved') {
            const reasons = (summary.reasonCodes || []).map(code => CODES[code]).filter(Boolean);
            return [L.CalendarUnresolvedBody || ''].concat(reasons).join(' ');
        }
        return L.CalculationUnavailable || '';
    };

    const paintSummary = summary => {
        setState('content');
        paintBadge(summary.calendarStatus);
        const has = summary.visits !== null && summary.visits !== undefined;

        part('visits').textContent = has ? fmt(summary.visits) : EMPTY;
        const noNumber = part('noNumber');
        noNumber.textContent = has ? '' : noNumberReason(summary);
        noNumber.classList.toggle('d-none', has);

        part('workingDays').textContent = fmt(summary.workingDays);
        part('deductedDays').textContent = summary.deductedDays === null || summary.deductedDays === undefined ? EMPTY : `−${fmt(summary.deductedDays)}`;
        part('fieldDays').textContent = fmt(summary.fieldDays);
        const remaining = part('remainingMinutes');
        remaining.textContent = summary.remainingMinutes === null || summary.remainingMinutes === undefined ? EMPTY : `${fmt(summary.remainingMinutes)} ${L.UnitMinutesShort || ''}`;
        remaining.title = hoursHint(summary.remainingMinutes);
        part('typicalVisitMinutes').textContent = summary.typicalVisitMinutes ? `${fmt(summary.typicalVisitMinutes)} ${L.UnitMinutesShort || ''}` : EMPTY;
        part('averageFte').textContent = summary.averageFte === null || summary.averageFte === undefined ? EMPTY : fteFormat.format(summary.averageFte);

        const blocks = summary.blocks || [];
        const warnings = summary.warnings || [];
        fillList('blockList', blocks);
        fillList('warningList', warnings);
        part('blocks').classList.toggle('d-none', blocks.length === 0);
        part('warnings').classList.toggle('d-none', warnings.length === 0);
        part('allOk').classList.toggle('d-none', !(has && blocks.length === 0 && warnings.length === 0));

        // A shape the CRM would refuse cannot be saved from here (the CRM still decides on save).
        if (saveBtn) saveBtn.disabled = blocks.length > 0;
    };

    const daysInMonth = (year, month) => new Date(Date.UTC(year, month, 0)).getUTCDate();

    const cellsFor = key => {
        const row = monthsTableEl?.querySelector(`tr[data-month-key="${key}"]`);
        if (!row) return null;
        const pick = name => row.querySelector(`[data-cell="${name}"]`);
        return {
            workingDays: pick('workingDays'), nonWorkingDays: pick('nonWorkingDays'), deductedDays: pick('deductedDays'),
            fieldDays: pick('fieldDays'), remainingMinutes: pick('remainingMinutes'), totalVisitNumber: pick('totalVisitNumber'),
            noFieldDays: pick('noFieldDays'), partial: pick('partial')
        };
    };

    const COMPUTED = ['workingDays', 'nonWorkingDays', 'deductedDays', 'fieldDays', 'remainingMinutes', 'totalVisitNumber'];

    const clearCells = () => {
        monthRows().forEach(row => {
            const cells = cellsFor(row.dataset.monthKey);
            if (!cells) return;
            COMPUTED.forEach(name => { if (cells[name]) { cells[name].textContent = EMPTY; cells[name].removeAttribute('title'); } });
            cells.noFieldDays?.classList.add('d-none');
            cells.partial?.classList.add('d-none');
        });
        if (totalEl) totalEl.textContent = EMPTY;
    };

    /// The CRM's month rows, written into the read-only cells — only when the summary carries a number (K-4).
    const paintMonths = (data, summary) => {
        clearCells();
        if (!data || summary.visits === null || summary.visits === undefined) return;
        (data.months || []).forEach(m => {
            const cells = cellsFor(`${m.year}-${String(m.monthNumber).padStart(2, '0')}`);
            if (!cells) return;
            cells.workingDays.textContent = fmt(m.workingDays);
            cells.nonWorkingDays.textContent = fmt(m.nonWorkingDays);
            cells.deductedDays.textContent = fmt(m.deductedDays);
            cells.fieldDays.textContent = fmt(m.fieldDays);
            cells.remainingMinutes.textContent = fmt(m.remainingMinutes);
            cells.remainingMinutes.title = hoursHint(m.remainingMinutes);
            cells.totalVisitNumber.textContent = fmt(m.totalVisitNumber);
            // Flagged, never hidden: zero visits is a real answer.
            cells.noFieldDays?.classList.toggle('d-none', m.deductedDays <= m.workingDays);
            cells.partial?.classList.toggle('d-none', !(m.calendarDays > 0 && m.calendarDays < daysInMonth(m.year, m.monthNumber)));
        });
        if (totalEl) totalEl.textContent = fmt(summary.visits);
    };

    /// "2×12 + 1×5 + 5 = 34 min": the INPUTS on the left, the CRM's divisor on the right.
    const paintFormula = (input, summary) => {
        if (!formulaEl) return;
        const complete = input && input.typicalPromoCount !== null && input.typicalNonPromoCount !== null && input.reportMinutesPerVisit !== null;
        if (!complete || !summary?.typicalVisitMinutes || summary.visitModel !== 'typical') {
            formulaEl.textContent = EMPTY;
            return;
        }
        formulaEl.textContent = `${fmt(input.typicalPromoCount)}×${fmt(input.promoProductTime)} + ${fmt(input.typicalNonPromoCount)}×${fmt(input.nonPromoProductTime)} + ${fmt(input.reportMinutesPerVisit)} = ${fmt(summary.typicalVisitMinutes)} ${L.UnitMinutesShort || ''}`;
    };

    // ── the request: debounced, single, newest wins ──────────────────────────────────────────────────────────────

    let debounceHandle = null;
    let inFlight = null;
    let token = 0;
    let lastAnswered = null;

    const requestPreview = async () => {
        const input = buildPreviewInput();
        if (!input) {
            clearCells();
            setState('idle');
            return;
        }

        const body = JSON.stringify(input);
        if (body === lastAnswered) return;

        inFlight?.abort();
        const controller = new AbortController();
        inFlight = controller;
        const mine = ++token;
        setState('loading');

        try {
            const response = await fetch(PREVIEW_URL, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
                body,
                signal: controller.signal
            });
            const envelope = await response.json().catch(() => null);
            if (mine !== token) return;
            if (!envelope?.summary) {
                setState('error');
                return;
            }
            lastAnswered = body;
            paintSummary(envelope.summary);
            paintMonths(envelope.data, envelope.summary);
            paintFormula(input, envelope.summary);
        } catch (error) {
            if (error?.name === 'AbortError' || mine !== token) return;
            setState('error');
        } finally {
            if (inFlight === controller) inFlight = null;
        }
    };

    const schedulePreview = () => {
        window.clearTimeout(debounceHandle);
        debounceHandle = window.setTimeout(requestPreview, DEBOUNCE_MS);
    };

    // ── FTE (K-5) ────────────────────────────────────────────────────────────────────────────────────────────────

    /// Marks a month's FTE as authored: the hidden twin carries the invariant value and the touched flag.
    const touchFte = (row, value) => {
        const input = row.querySelector('.js-fte-input');
        if (input && value !== undefined) input.value = String(value).trim();
        const parsed = decOrNull(input?.value);
        const twin = row.querySelector('[data-fte-text]');
        if (twin) twin.value = parsed === null ? '' : String(parsed);
        const flag = row.querySelector('[data-fte-touched]');
        if (flag) flag.value = 'true';
        const badge = row.querySelector('[data-cell="fteSource"]');
        if (badge) {
            badge.className = 'badge bg-label-primary';
            badge.textContent = badge.dataset.authoredText || badge.textContent;
        }
    };

    // ── apply to all months ──────────────────────────────────────────────────────────────────────────────────────

    const applyToAll = () => {
        const bulk = Array.from(document.querySelectorAll('.js-bulk-input')).filter(el => String(el.value).trim() !== '');
        if (bulk.length === 0) return;
        monthRows().forEach(row => {
            bulk.forEach(source => {
                const prop = source.dataset.prop;
                if (prop === 'Fte') {
                    touchFte(row, source.value);
                    return;
                }
                const target = row.querySelector(`.js-month-input[name$=".${prop}"]`);
                if (target) target.value = source.value;
            });
        });
        bulk.forEach(el => { el.value = ''; });
        schedulePreview();
    };

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────────

    if (!readOnly) {
        form.querySelectorAll('.js-live, .js-month-input, [name="CalendarCountryCode"]').forEach(el => {
            el.addEventListener('input', schedulePreview);
            el.addEventListener('change', schedulePreview);
        });

        form.querySelectorAll('.js-fte-input').forEach(el => {
            el.addEventListener('input', () => touchFte(el.closest('tr'), undefined));
        });

        // Esc in a month cell restores the value it had on focus.
        form.querySelectorAll('.js-month-input').forEach(el => {
            el.addEventListener('focus', () => { el.dataset.focusValue = el.value; });
            el.addEventListener('keydown', event => {
                if (event.key !== 'Escape' || el.dataset.focusValue === undefined) return;
                event.preventDefault();
                el.value = el.dataset.focusValue;
                el.dispatchEvent(new Event('input', { bubbles: true }));
            });
        });

        document.getElementById('applyToAllMonths')?.addEventListener('click', applyToAll);

        // A blocked form is not submitted (the button is disabled; Enter in a field must not get round it).
        form.addEventListener('submit', event => {
            if (saveBtn?.disabled) event.preventDefault();
        });
    }

    summaryEl?.querySelector('[data-summary-retry]')?.addEventListener('click', () => {
        lastAnswered = null;
        requestPreview();
    });

    // Picking a period on a NEW capacity reloads the create page for it, so the server derives the month rows from its
    // window. The origin rides along so a period-grid visit is not quietly turned into a capacity-list one.
    if (periodSelect) {
        periodSelect.addEventListener('change', () => {
            const value = (periodSelect.value || '').trim();
            if (!value) return;
            const returnTo = (valueOf('ReturnTo') || '').trim();
            const origin = returnTo ? `&returnTo=${encodeURIComponent(returnTo)}` : '';
            window.location.assign(`/CRM/CycleCapacities/Create?cyclePeriodId=${encodeURIComponent(value)}${origin}`);
        });
    }

    // One estimate on load, so an author editing an existing capacity sees where it stands before touching anything.
    requestPreview();
})(window, document);
