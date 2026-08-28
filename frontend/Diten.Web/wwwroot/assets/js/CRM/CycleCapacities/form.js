/**
 * MOD-0155-FU06 Cycle Capacity — the Create / Edit page (Golden Compact).
 *
 * Three things, and deliberately nothing more.
 *
 * 1. It ADVISES on the two budget rules the runtime enforces — the visit divisor must be greater than zero, and the
 *    fixed per-day charges must leave something of the working day. Both are enforced server-side; this only stops an
 *    author submitting a shape that is already known to be refused. It never blocks the form on its own authority,
 *    because a client-side rule that disagreed with the runtime would be a second source of truth.
 *
 * 2. It shows a LIVE ESTIMATE while the author types: a debounced POST to a stateless preview endpoint, written
 *    back INTO the month rows themselves (FU07 — FU06 rendered a second table underneath, so the same month appeared
 *    twice on one page). Only the READ-ONLY cells are touched, so an author's cursor is never taken out of the field
 *    they are typing in. The arithmetic is NOT done here — the browser sends the numbers and the server answers using
 *    the same estimator and the same pure calculator the SAVED record uses. That is the whole point: a figure computed
 *    in JavaScript would eventually disagree with the one the detail page shows, and the author would trust the wrong
 *    one.
 *
 * 3. It FILLS DOWN from the first month: typing a deduction into January copies it into the months below, because
 *    that is what an author does by hand otherwise. A cell the author has edited themselves is marked and is never
 *    overwritten again — the convenience must not be able to destroy typed work.
 *
 * 4. It reloads the page when the period picker changes on a NEW capacity, so the month rows come back derived from
 *    the newly chosen period's window. The month SET is a fact of the period rather than something an author
 *    composes.
 *
 * There is no FTE handling anywhere in this file. The input is rendered disabled, and neither the save nor the preview
 * request carries an FTE — the server stamps the configured average on both paths, so the preview and the saved
 * record cannot be built on different numbers.
 */
(function (window, document) {
    'use strict';

    const form = document.getElementById('cycleCapacityForm');
    if (!form) return;

    const L = window.CycleCapacitiesL10n || window.L10n || {};
    const PREVIEW_URL = '/CRM/CycleCapacities/api/capacities/calculation-preview';
    const DEBOUNCE_MS = 500;

    const warningEl = document.getElementById('budgetWarning');
    const dailyWorkMinutesEl = document.querySelector('input[name="DailyWorkMinutes"]');
    const visitMinuteEls = Array.from(form.querySelectorAll('.js-visit-minute'));
    const dayMinuteEls = Array.from(form.querySelectorAll('.js-day-minute'));
    const periodSelect = document.getElementById('cyclePeriodSelect');
    const monthsTableEl = document.getElementById('capacityMonthsTable');
    const noticeEl = document.getElementById('livePreviewNotice');
    const totalEl = document.getElementById('livePreviewTotal');

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));

    const num = el => {
        const value = Number(el?.value);
        return Number.isFinite(value) ? value : 0;
    };

    const sum = els => els.reduce((total, el) => total + num(el), 0);

    // ── 1. budget advice ─────────────────────────────────────────────────────────────────────────────────────────

    /// Mirrors the runtime's two write-path rules. Advisory only — the server decides.
    const evaluateBudget = () => {
        if (!warningEl) return true;

        const messages = [];
        const minutesPerVisit = sum(visitMinuteEls);
        const dailySpend = sum(dayMinuteEls);
        const dailyWorkMinutes = num(dailyWorkMinutesEl);

        // A visit that costs no time would make the capacity infinite, so the divisor is refused before it can reach
        // the arithmetic.
        if (visitMinuteEls.length > 0 && minutesPerVisit <= 0) {
            messages.push(L.BudgetVisitMinutesZero || '');
        }

        // A day whose fixed charges already consume it leaves no time for any visit — a modelling error rather than a
        // capacity of zero.
        if (dailyWorkMinutes > 0 && dailySpend >= dailyWorkMinutes) {
            messages.push(L.BudgetDailySpendExceedsDay || '');
        }

        const text = messages.filter(Boolean).join(' ');
        warningEl.textContent = text;
        warningEl.classList.toggle('d-none', text.length === 0);
        return messages.length === 0;
    };

    // ── 2. live estimate ─────────────────────────────────────────────────────────────────────────────────────────

    const valueOf = name => document.querySelector(`[name="${name}"]`)?.value ?? '';

    /// The period the form is pinned to. Read from whichever control is present: the picker on a fresh create, the
    /// hidden input once a period is chosen or on edit.
    const cyclePeriodId = () => {
        const fields = Array.from(document.querySelectorAll('[name="CyclePeriodId"]'));
        for (const field of fields) {
            const value = (field.value || '').trim();
            if (value) return value;
        }
        return '';
    };

    /// The month rows, read straight out of the grid. Indices are read from the input NAMES rather than from position,
    /// for the same reason the model uses (Year, MonthNumber) instead of an array slot.
    const readMonths = () => {
        const rows = new Map();
        form.querySelectorAll('#capacityMonthsTable [name^="Months["]').forEach(field => {
            const match = /^Months\[(\d+)\]\.(\w+)$/.exec(field.getAttribute('name') || '');
            if (!match) return;
            const [, index, prop] = match;
            if (!rows.has(index)) rows.set(index, {});
            rows.get(index)[prop] = Number(field.value);
        });

        return Array.from(rows.values())
            .filter(r => Number.isFinite(r.Year) && Number.isFinite(r.MonthNumber))
            .map(r => ({
                year: r.Year,
                monthNumber: r.MonthNumber,
                meetingDays: Number.isFinite(r.MeetingDays) ? r.MeetingDays : 0,
                trainingDays: Number.isFinite(r.TrainingDays) ? r.TrainingDays : 0,
                vacationDays: Number.isFinite(r.VacationDays) ? r.VacationDays : 0,
                microTargetingDayCount: Number.isFinite(r.MicroTargetingDayCount) ? r.MicroTargetingDayCount : 0,
                microTargetingDuration: Number.isFinite(r.MicroTargetingDuration) ? r.MicroTargetingDuration : 0
            }));
    };

    /// The payload — and the guard. An incomplete form asks NOTHING: a request that cannot produce a meaningful answer
    /// is a wasted round trip and a confusing flash of "unavailable" while someone is still filling the first field.
    const buildPreviewPayload = () => {
        const periodId = cyclePeriodId();
        if (!periodId) return null;

        const months = readMonths();
        if (months.length === 0) return null;

        const dailyWorkMinutes = num(dailyWorkMinutesEl);
        if (dailyWorkMinutes <= 0) return null;

        // The divisor rule, checked here too: without it the server would answer, but every month would read zero and
        // the author would think the model is broken rather than incomplete.
        if (sum(visitMinuteEls) <= 0) return null;

        return {
            cyclePeriodId: periodId,
            calendarCountryCode: (valueOf('CalendarCountryCode') || '').trim() || null,
            dailyWorkMinutes,
            promoProductTime: Number(valueOf('PromoProductTime')) || 0,
            nonPromoProductTime: Number(valueOf('NonPromoProductTime')) || 0,
            travelingTime: Number(valueOf('TravelingTime')) || 0,
            reportDuration: Number(valueOf('ReportDuration')) || 0,
            quizDuration: Number(valueOf('QuizDuration')) || 0,
            months
        };
    };

    const EMPTY = '\u2014';

    /// The read-only cells of one month row, addressed by (year, month) rather than by row position — the same reason
    /// the model identifies a month that way instead of by array slot.
    const cellsFor = key => {
        const row = monthsTableEl?.querySelector(`tr[data-month-key="${key}"]`);
        if (!row) return null;
        const pick = name => row.querySelector(`[data-cell="${name}"]`);
        return {
            workingDays: pick('workingDays'),
            nonWorkingDays: pick('nonWorkingDays'),
            deductedDays: pick('deductedDays'),
            fieldDays: pick('fieldDays'),
            visitMinutes: pick('visitMinutes'),
            totalVisitNumber: pick('totalVisitNumber'),
            noFieldDays: pick('noFieldDays')
        };
    };

    const allMonthKeys = () =>
        Array.from(monthsTableEl?.querySelectorAll('tr[data-month-key]') || [])
            .map(row => row.getAttribute('data-month-key'));

    /// Wipes every computed cell back to an em dash. Used whenever there is no usable answer — a stale number left
    /// standing next to freshly typed inputs is worse than no number at all.
    const clearComputed = () => {
        allMonthKeys().forEach(key => {
            const cells = cellsFor(key);
            if (!cells) return;
            ['workingDays', 'nonWorkingDays', 'deductedDays', 'fieldDays', 'visitMinutes', 'totalVisitNumber']
                .forEach(name => { if (cells[name]) cells[name].textContent = EMPTY; });
            cells.noFieldDays?.classList.add('d-none');
        });
        if (totalEl) totalEl.textContent = EMPTY;
    };

    const showNotice = html => {
        if (!noticeEl) return;
        noticeEl.innerHTML = html || '';
        noticeEl.classList.toggle('d-none', !html);
    };

    const notice = (tone, title, body, detail) => `
        <div class="alert alert-${tone} mb-0">
            <div class="fw-semibold">${esc(title)}</div>
            <div class="small">${esc(body)}</div>
            ${detail ? `<div class="small text-muted mt-1">${esc(detail)}</div>` : ''}
        </div>`;

    /// The resolved estimate, written into the rows it belongs to. Read-only cells only.
    const renderResolved = data => {
        showNotice('');

        (data.months || []).forEach(m => {
            const cells = cellsFor(`${m.year}-${String(m.monthNumber).padStart(2, '0')}`);
            if (!cells) return;
            if (cells.workingDays) cells.workingDays.textContent = m.workingDays;
            if (cells.nonWorkingDays) cells.nonWorkingDays.textContent = m.nonWorkingDays;
            if (cells.deductedDays) cells.deductedDays.textContent = m.deductedDays;
            if (cells.fieldDays) cells.fieldDays.textContent = m.fieldDays;
            if (cells.visitMinutes) cells.visitMinutes.textContent = m.visitMinutes;
            if (cells.totalVisitNumber) cells.totalVisitNumber.textContent = m.totalVisitNumber;
            // A month with no field days is FLAGGED, never hidden: zero visits is a real answer and the reader needs
            // to see which month produced it.
            cells.noFieldDays?.classList.toggle('d-none', m.fieldDays !== 0);
        });

        if (totalEl) totalEl.textContent = data.totalVisitNumber;
    };

    /// An unresolved answer. Shown AS unresolved and never as a zero — the Details page draws the same distinction,
    /// and a "0" here would be a number the author could act on.
    const renderUnresolved = data => {
        clearComputed();

        if (!data) {
            showNotice(notice('secondary', L.CalculationUnavailable || '', '', null));
            return;
        }

        showNotice(data.resolution === 'calendar_forbidden'
            ? notice('danger', L.CalendarForbiddenTitle, L.CalendarForbiddenBody, data.reason)
            : notice('warning', L.CalendarUnresolvedTitle, L.CalendarUnresolvedBody, data.reason));
    };

    // Only the newest answer may paint. Without this an earlier, slower response can land after a later one and show
    // an estimate for numbers the author has already changed.
    let previewToken = 0;

    const requestPreview = async () => {
        const payload = buildPreviewPayload();
        if (!payload) {
            // Nothing to ask yet. The computed cells are cleared rather than left showing an answer to a question the
            // form no longer poses.
            clearComputed();
            showNotice('');
            return;
        }

        const token = ++previewToken;

        try {
            const response = await fetch(PREVIEW_URL, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            const envelope = await response.json().catch(() => null);
            if (token !== previewToken) return;

            // An unresolved estimate arrives as 503 WITH a body: that body is what lets the page explain itself, so
            // the status alone is never the whole answer.
            if (response.ok && envelope?.data?.resolution === 'resolved') {
                renderResolved(envelope.data);
            } else {
                renderUnresolved(envelope?.data);
            }
        } catch (error) {
            if (token !== previewToken) return;
            renderUnresolved(null);
        }
    };

    let debounceHandle = null;
    const schedulePreview = () => {
        window.clearTimeout(debounceHandle);
        debounceHandle = window.setTimeout(requestPreview, DEBOUNCE_MS);
    };

    // ── 3. fill-down ─────────────────────────────────────────────────────────────────────────────────────────────

    const monthRows = () => Array.from(monthsTableEl?.querySelectorAll('tr[data-month-key]') || []);

    /// The model property an input binds to — "MeetingDays" out of "Months[3].MeetingDays". Read from the NAME rather
    /// than from column position, for the same reason the model identifies a month by (Year, MonthNumber): a column
    /// that moves must not silently start filling a different field.
    const propertyOf = el => /^Months\[\d+\]\.(\w+)$/.exec(el.getAttribute('name') || '')?.[1] || '';

    /// Has the author typed in this cell themselves? Once true it stays true: fill-down may seed an untouched cell,
    /// never revise a decision someone already made. An author who wants the seeded value back simply retypes it —
    /// their edit still wins, which is the whole point.
    const isTouched = el => el.dataset.touched === '1';

    /// Copies the FIRST month's value down into the months below it. Untouched cells only.
    /// <returns>true when something actually changed, so the caller knows whether to re-estimate.</returns>
    const fillDown = source => {
        const rows = monthRows();
        if (rows.length < 2 || !rows[0].contains(source)) {
            return false;
        }

        const property = propertyOf(source);
        if (!property) {
            return false;
        }

        let changed = false;

        rows.slice(1).forEach(row => {
            const target = row.querySelector(`.js-month-input[name$=".${property}"]`);
            if (!target || isTouched(target) || target.value === source.value) {
                return;
            }

            // Assigning .value does NOT raise an input event, which is exactly what is wanted here: the copy must not
            // look like the author typing, or every filled cell would immediately mark itself touched and the next
            // fill-down would do nothing.
            target.value = source.value;
            changed = true;
        });

        return changed;
    };

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────────

    const onInputChanged = event => {
        const el = event?.target;

        if (el?.classList?.contains('js-month-input')) {
            const rows = monthRows();
            if (rows.length > 0 && !rows[0].contains(el)) {
                // Anything the author types outside the first month is theirs from now on.
                el.dataset.touched = '1';
            } else {
                fillDown(el);
            }
        }

        evaluateBudget();
        schedulePreview();
    };

    const watched = [
        dailyWorkMinutesEl,
        ...visitMinuteEls,
        ...dayMinuteEls,
        document.querySelector('[name="CalendarCountryCode"]'),
        ...Array.from(form.querySelectorAll('#capacityMonthsTable input'))
    ].filter(Boolean);

    watched.forEach(el => {
        el.addEventListener('input', onInputChanged);
        el.addEventListener('change', onInputChanged);
    });

    evaluateBudget();
    // One estimate on load, so an author editing an existing capacity sees where it stands before touching anything.
    requestPreview();

    // ── 3. period change on a NEW capacity ───────────────────────────────────────────────────────────────────────

    // Picking a period reloads the create page for that period, so the server derives the month rows from its window.
    // Nothing the author typed is lost that the server cannot rebuild: at this point the only thing on the form is the
    // (empty) month grid the previous selection produced.
    if (periodSelect) {
        periodSelect.addEventListener('change', () => {
            const value = (periodSelect.value || '').trim();
            if (!value) return;
            // The origin rides along: reloading to seed the month rows must not quietly turn a period-grid visit into
            // a capacity-list one, which is what dropping returnTo here would do.
            const returnTo = (document.querySelector('[name="ReturnTo"]')?.value || '').trim();
            const origin = returnTo ? `&returnTo=${encodeURIComponent(returnTo)}` : '';
            window.location.assign(`/CRM/CycleCapacities/Create?cyclePeriodId=${encodeURIComponent(value)}${origin}`);
        });
    }
})(window, document);
