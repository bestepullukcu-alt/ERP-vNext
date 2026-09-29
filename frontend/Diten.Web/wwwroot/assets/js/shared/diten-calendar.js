'use strict';

/*
 * DitenCalendar — the ONE calendar component (WP-UI-CALENDAR-VIEW-01, BL-365 #4, MOD-0357 S3b).
 *
 * A thin wrapper over the vendored FullCalendar 6.1.15 (assets/vendor/libs/fullcalendar, loaded only by
 * Views/Shared/_CalendarAssets.cshtml, only on pages that draw a calendar — never a CDN). The Task Center is its
 * first host; the Meetings page (2c) is the second. It knows nothing about tasks or meetings: a host hands it
 * events, day types and callbacks, and gets back what the reader did — a day, or a UTC start and a length.
 *
 * ── WHAT IT DECIDES, AND WHAT IT DOES NOT ─────────────────────────────────────────────────────────────────
 * It decides HOW a calendar looks and behaves: month / week / day, Monday first, 15-minute grid, the chrome in the
 * page language (SharedResource, 7 languages, through the #diten-calendar-l10n payload — no FullCalendar locale
 * file), dates formatted by the browser's Intl for that language, right-to-left for Arabic. It decides NOTHING
 * about the work: conflicts, cutting a block at the end of the day, working windows — all come from the engine;
 * the host passes them in and this draws them.
 *
 * ── TIME ZONE ─────────────────────────────────────────────────────────────────────────────────────────────
 * The calendar shows the TENANT's day, whatever zone the reader's machine is in. FullCalendar runs in 'UTC' mode
 * and is handed wall-clock times (DitenZonedTime.toCalendar); everything it hands back is turned into a real UTC
 * instant in the tenant zone (DitenZonedTime.fromCalendar) before a host sees it.
 *
 * ── DRAGGING IN FROM OUTSIDE ──────────────────────────────────────────────────────────────────────────────
 * The vendored bundle exports Calendar and the four plugins but NOT the interaction plugin's Draggable, and
 * rebuilding it would mean downloading packages. So a card is dragged in with the browser's own HTML5 drag and
 * drop: the host puts the item id on the drag (`DitenCalendar.DRAG_TYPE`), and the drop is resolved against the
 * calendar's own cells — `.fc-daygrid-day[data-date]` for a day, `.fc-timegrid-col[data-date]` plus the 15-minute
 * slot row under the pointer for a time. Moving and resizing what is already ON the calendar is FullCalendar's
 * own interaction. Touch devices do not do HTML5 drag; the host's "Planla" button is that path.
 */
(function (global) {
    const DRAG_TYPE = 'text/x-diten-calendar-item';
    const VIEWS = { month: 'dayGridMonth', week: 'timeGridWeek', day: 'timeGridDay' };
    const VIEW_NAMES = { dayGridMonth: 'month', timeGridWeek: 'week', timeGridDay: 'day' };
    const RTL_LANGUAGES = ['ar', 'he', 'fa', 'ur'];
    const STEP_MINUTES = 15;
    /*
     * The week and day views get a FIXED height and scroll INSIDE (opening at 08:00), the month view grows with its
     * rows. With height 'auto' the whole 24 h was laid out on the page — measured live 2026-09-29: 96 rows × 52 px
     * (the vendored Sneat CSS gives every slot `block-size: 4em`, written for 30-minute rows) = a 5 000 px week, 10:00
     * far below the fold, and a card could not be dragged from the panel to an afternoon hour without scrolling mid-drag.
     * The slot height itself is set back to a quarter-hour size under `.dc-calendar` in backbone-custom.css.
     */
    const TIMEGRID_HEIGHT = 720;

    /** The chrome strings, from the page payload (SharedResource via _CalendarAssets.cshtml). */
    const readLabels = () => {
        const node = global.document && global.document.getElementById('diten-calendar-l10n');
        if (!node) { return {}; }
        try { return JSON.parse(node.textContent || '{}'); } catch (_) { return {}; }
    };

    const label = (labels, key) => (labels && Object.prototype.hasOwnProperty.call(labels, key) ? labels[key] : key);

    const pageLanguage = () => {
        const lang = (global.document && global.document.documentElement.lang) || 'tr';
        return lang.slice(0, 2).toLowerCase();
    };

    const isRtl = (lang) => RTL_LANGUAGES.indexOf(lang) >= 0;

    /**
     * A runtime locale built from the resx strings — no external locale file. FullCalendar uses its `code` for
     * Intl (month names, weekday names, times), `direction` for layout, and the strings for its buttons.
     */
    const buildLocale = (lang, labels) => ({
        code: lang,
        week: { dow: 1, doy: 4 },
        direction: isRtl(lang) ? 'rtl' : 'ltr',
        buttonText: {
            today: label(labels, 'CalendarToday'),
            month: label(labels, 'CalendarMonth'),
            week: label(labels, 'CalendarWeek'),
            day: label(labels, 'CalendarDay'),
            prev: label(labels, 'CalendarPrevious'),
            next: label(labels, 'CalendarNext')
        },
        buttonHints: {
            today: label(labels, 'CalendarToday'),
            prev: label(labels, 'CalendarPrevious'),
            next: label(labels, 'CalendarNext')
        },
        viewHint: (buttonText) => buttonText,
        allDayText: label(labels, 'CalendarAllDay'),
        moreLinkText: (n) => String(label(labels, 'CalendarMore')).split('{0}').join(String(n)),
        noEventsText: label(labels, 'CalendarNoEvents'),
        weekText: label(labels, 'CalendarWeek')
    });

    const zoneOf = (zone) => (global.DitenZonedTime && global.DitenZonedTime.isValidZone(zone) ? zone : 'UTC');

    const minutesBetween = (start, end) => Math.round((end.getTime() - start.getTime()) / 60000);

    const snap = (minutes) => Math.max(STEP_MINUTES, Math.round(minutes / STEP_MINUTES) * STEP_MINUTES);

    /** Host event → FullCalendar event input (wall clock in the tenant zone). */
    const toInput = (event, zone) => {
        const Z = global.DitenZonedTime;
        const base = {
            id: event.id,
            title: event.title,
            classNames: event.classNames || [],
            editable: !!event.editable,
            startEditable: !!event.editable,
            durationEditable: !!event.editable && !event.allDay,
            extendedProps: Object.assign({ kind: event.kind || 'item' }, event.extendedProps || {})
        };
        if (event.display) { base.display = event.display; }
        if (event.allDay) {
            base.allDay = true;
            base.start = event.date;
            if (event.endDate) { base.end = event.endDate; }
            return base;
        }
        base.allDay = false;
        base.start = Z.toCalendar(event.startUtc, zone);
        base.end = Z.toCalendar(event.endUtc, zone);
        return base;
    };

    /**
     * Working windows → FullCalendar businessHours, per weekday. In the week and day views every weekday occurs
     * once, so a per-date window maps exactly; a day with no window (weekend, holiday) gets no entry and is drawn
     * as non-working. In the month view hours are not drawn at all.
     */
    const businessHoursFor = (days, zone) => {
        // NO day facts at all (a read-only tab, or the feed not answered yet) is "nothing to draw", not "no hour is
        // working": the impossible entry below would shade every day of the month grey (measured live, CT 2026-09-29).
        if (!days || !days.length) { return false; }
        const Z = global.DitenZonedTime;
        const hours = [];
        (days || []).forEach((day) => {
            (day.windows || []).forEach((window) => {
                const start = Z.toWall(window.startAt, zone);
                const end = Z.toWall(window.endAt, zone);
                const dow = new Date(day.date + 'T00:00:00Z').getUTCDay();
                hours.push({ daysOfWeek: [dow], startTime: start.slice(11, 16), endTime: end.slice(11, 16) });
            });
        });
        // An EMPTY array would mean "everything is business hours" to FullCalendar; one impossible entry means
        // "nothing is" — which is the truth for a range with no working window at all.
        return hours.length ? hours : [{ daysOfWeek: [], startTime: '00:00', endTime: '00:00' }];
    };

    /** Named holidays as all-day background events, so the name is on the day in every view. */
    const holidayEvents = (days) => (days || [])
        .filter((day) => day.dayKind === 'holiday')
        .map((day) => ({
            id: 'dc-holiday-' + day.date,
            title: day.holidayName || '',
            start: day.date,
            allDay: true,
            display: 'background',
            classNames: ['dc-holiday-band'],
            extendedProps: { kind: 'holiday' }
        }));

    /**
     * Where a native drop landed: a DAY (month cell, or the all-day row of week/day) or a TIME (a timegrid column
     * plus the 15-minute slot row under the pointer). Null when it landed on neither.
     */
    const resolveDrop = (root, target, clientY) => {
        const dayCell = target && target.closest ? target.closest('.fc-daygrid-day[data-date]') : null;
        if (dayCell) { return { allDay: true, date: dayCell.getAttribute('data-date') }; }

        const column = target && target.closest ? target.closest('.fc-timegrid-col[data-date]') : null;
        if (!column) { return null; }
        const rows = Array.from(root.querySelectorAll('td.fc-timegrid-slot-lane[data-time]'));
        const row = rows.find((candidate) => {
            const rect = candidate.getBoundingClientRect();
            return clientY >= rect.top && clientY < rect.bottom;
        });
        if (!row) { return null; }
        return { allDay: false, date: column.getAttribute('data-date'), time: row.getAttribute('data-time').slice(0, 5) };
    };

    const insideRect = (element, x, y) => {
        if (!element || typeof element.getBoundingClientRect !== 'function') { return false; }
        const rect = element.getBoundingClientRect();
        return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom && rect.width > 0;
    };

    /**
     * Mount a calendar in `host`.
     *
     * options:
     *   zone, view ('month'|'week'|'day'), date ('YYYY-MM-DD'), editable (bool),
     *   events [{ id, title, kind, allDay, date | startUtc+endUtc, editable, classNames, extendedProps }],
     *   days [{ date, dayKind, holidayName, windows:[{startAt,endAt}] }],
     *   labels (override of the payload), language (override of <html lang>),
     *   renderExtras(eventApi) → HTML string appended inside an event (buttons, chips),
     *   onRangeChange({ view, from, to, date }), onEventClick(id),
     *   onEventMove({ id, kind, allDay, date, startUtc, durationMinutes, revert }),
     *   onEventResize({ id, startUtc, durationMinutes, revert }),
     *   dropOutTarget (element, or a function returning it), onDragOut({ id, kind, revert }),
     *   onExternalDrop({ itemId, allDay, date, startUtc })
     */
    const create = (host, options) => {
        const FC = global.Calendar;
        if (!host || typeof FC !== 'function') {
            if (global.console && global.console.error) {
                global.console.error('[DitenCalendar] FullCalendar is not loaded on this page — include '
                    + 'Views/Shared/_CalendarAssets.cshtml before this script.');
            }
            return null;
        }

        const opts = options || {};
        const zone = zoneOf(opts.zone);
        const lang = (opts.language || pageLanguage());
        const labels = Object.assign({}, readLabels(), opts.labels || {});
        // Mutable: a planning host turns editing on only once it knows the zone it is converting in (see setEditable).
        let editable = !!opts.editable;
        let days = opts.days || [];

        const eventInputs = (events) => (events || []).map((event) => toInput(event, zone)).concat(holidayEvents(days));

        const dayClass = (date) => {
            const iso = date.toISOString().slice(0, 10);
            const day = days.find((d) => d.date === iso);
            if (!day) { return []; }
            if (day.dayKind === 'holiday') { return ['dc-day-holiday']; }
            if (day.dayKind === 'weekend') { return ['dc-day-weekend']; }
            return [];
        };

        const Z = global.DitenZonedTime;
        const handlers = {
            datesSet: (info) => {
                if (typeof opts.onRangeChange !== 'function') { return; }
                const from = info.start.toISOString().slice(0, 10);
                const to = new Date(info.end.getTime() - 86400000).toISOString().slice(0, 10);
                opts.onRangeChange({
                    view: VIEW_NAMES[info.view.type] || 'month',
                    from,
                    to,
                    date: calendar.getDate().toISOString().slice(0, 10)
                });
            },
            eventDrop: (info) => {
                if (typeof opts.onEventMove !== 'function') { info.revert(); return; }
                const event = info.event;
                const kind = event.extendedProps.kind;
                if (event.allDay) {
                    opts.onEventMove({ id: event.id, kind, allDay: true, date: event.start.toISOString().slice(0, 10), revert: info.revert });
                    return;
                }
                const length = event.end
                    ? minutesBetween(event.start, event.end)
                    : (event.extendedProps.defaultMinutes || 60);
                opts.onEventMove({
                    id: event.id,
                    kind,
                    allDay: false,
                    startUtc: Z.fromCalendar(event.start, zone),
                    durationMinutes: snap(length),
                    revert: info.revert
                });
            },
            eventResize: (info) => {
                if (typeof opts.onEventResize !== 'function') { info.revert(); return; }
                const event = info.event;
                opts.onEventResize({
                    id: event.id,
                    kind: event.extendedProps.kind,
                    startUtc: Z.fromCalendar(event.start, zone),
                    durationMinutes: snap(minutesBetween(event.start, event.end)),
                    revert: info.revert
                });
            },
            eventDragStop: (info) => {
                const js = info.jsEvent || {};
                // Resolved at the moment of the drop: a host that re-renders around a kept calendar replaces the
                // element, so a reference captured at creation would point at a node no longer on the page.
                const target = typeof opts.dropOutTarget === 'function' ? opts.dropOutTarget() : opts.dropOutTarget;
                if (typeof opts.onDragOut === 'function' && insideRect(target, js.clientX, js.clientY)) {
                    opts.onDragOut({ id: info.event.id, kind: info.event.extendedProps.kind, revert: () => {} });
                }
            },
            eventClick: (info) => {
                // A button INSIDE an event (accept / decline an invitation) is the page's own action — the host's
                // delegated click handler takes it; opening the item as well would answer twice.
                // ⚠ FullCalendar draws EVERY event as an <a> with no href (measured, 6.1.15), so a bare
                // closest('a') matches the event itself and no click ever opened anything (CT acceptance). Only a
                // real control inside the event counts: a button, or a link that goes somewhere.
                const inner = info.jsEvent && info.jsEvent.target && info.jsEvent.target.closest
                    ? info.jsEvent.target.closest('button, a[href]') : null;
                if (inner && inner !== info.el) { return; }
                if (typeof opts.onEventClick === 'function') { opts.onEventClick(info.event.id, info.event.extendedProps.kind); }
            },
            eventContent: (arg) => {
                // A background band (a holiday) shows its NAME and nothing else — FullCalendar draws no content of
                // its own once this callback exists, so the name is written here or not at all.
                if (arg.event.display === 'background') {
                    const band = global.document.createElement('span');
                    band.className = 'dc-band-title';
                    band.textContent = arg.event.title || '';
                    return { domNodes: [band] };
                }
                const root = global.document.createElement('div');
                root.className = 'dc-event-body';
                const time = arg.timeText ? `<span class="dc-event-time">${escapeHtml(arg.timeText)}</span>` : '';
                root.innerHTML = time + `<span class="dc-event-title">${escapeHtml(arg.event.title)}</span>`
                    + (typeof opts.renderExtras === 'function' ? (opts.renderExtras(arg.event) || '') : '');
                return { domNodes: [root] };
            },
            dayCellClassNames: (arg) => dayClass(arg.date),
            dayHeaderClassNames: (arg) => dayClass(arg.date)
        };

        // The component's own class: its CSS (slot height, event body) is scoped under it, never under a host page.
        host.classList.add('dc-calendar');

        const calendar = new FC(host, {
            plugins: [global.dayGridPlugin, global.timegridPlugin, global.interactionPlugin].filter(Boolean),
            timeZone: 'UTC',
            locales: [buildLocale(lang, labels)],
            locale: lang,
            direction: isRtl(lang) ? 'rtl' : 'ltr',
            firstDay: 1,
            initialView: VIEWS[opts.view] || VIEWS.month,
            initialDate: opts.date || undefined,
            headerToolbar: { start: 'prev,next today', center: 'title', end: 'dayGridMonth,timeGridWeek,timeGridDay' },
            views: {
                dayGridMonth: { height: 'auto' },
                timeGridWeek: { height: TIMEGRID_HEIGHT },
                timeGridDay: { height: TIMEGRID_HEIGHT }
            },
            slotDuration: '00:15:00',
            snapDuration: '00:15:00',
            slotLabelInterval: '01:00',
            scrollTime: '08:00:00',
            nowIndicator: true,
            /*
             * "Now" in the TENANT's wall clock, like every other time this calendar shows (v2 F3). Without it the
             * calendar's today and its now-line come from the reader's machine clock read as UTC — at 22:30 UTC an
             * Istanbul reader's calendar still said "yesterday".
             */
            now: () => Z.toCalendar(Date.now(), zone),
            dayMaxEvents: true,
            editable,
            eventStartEditable: editable,
            eventDurationEditable: editable,
            droppable: false,
            eventOverlap: true,
            businessHours: businessHoursFor(days, zone),
            events: eventInputs(opts.events),
            datesSet: handlers.datesSet,
            eventDrop: handlers.eventDrop,
            eventResize: handlers.eventResize,
            eventDragStop: handlers.eventDragStop,
            eventClick: handlers.eventClick,
            eventContent: handlers.eventContent,
            dayCellClassNames: handlers.dayCellClassNames,
            dayHeaderClassNames: handlers.dayHeaderClassNames
        });

        /* ── native HTML5 drop from outside (see the header) ─────────────────────────────────────────── */
        const onDragOver = (event) => {
            if (!editable || typeof opts.onExternalDrop !== 'function') { return; }
            const types = event.dataTransfer && event.dataTransfer.types;
            if (types && Array.prototype.indexOf.call(types, DRAG_TYPE) < 0) { return; }
            event.preventDefault();
        };
        const onDrop = (event) => {
            if (!editable || typeof opts.onExternalDrop !== 'function') { return; }
            const itemId = event.dataTransfer && event.dataTransfer.getData(DRAG_TYPE);
            if (!itemId) { return; }
            const spot = resolveDrop(host, event.target, event.clientY);
            if (!spot) { return; }
            event.preventDefault();
            opts.onExternalDrop(spot.allDay
                ? { itemId, allDay: true, date: spot.date }
                : { itemId, allDay: false, date: spot.date, startUtc: Z.toUtcIso(`${spot.date}T${spot.time}`, zone) });
        };
        host.addEventListener('dragover', onDragOver);
        host.addEventListener('drop', onDrop);

        calendar.render();

        return {
            calendar,
            zone,
            host,
            handlers,
            /** Turn planning on or off without rebuilding: FullCalendar's own drag AND the native drop from outside. */
            setEditable: (next) => {
                editable = !!next;
                calendar.setOption('editable', editable);
                calendar.setOption('eventStartEditable', editable);
                calendar.setOption('eventDurationEditable', editable);
            },
            isEditable: () => editable,
            setData: (events, nextDays) => {
                if (nextDays) {
                    days = nextDays;
                    calendar.setOption('businessHours', businessHoursFor(days, zone));
                }
                calendar.removeAllEvents();
                eventInputs(events).forEach((input) => calendar.addEvent(input));
            },
            view: () => VIEW_NAMES[calendar.view.type] || 'month',
            date: () => calendar.getDate().toISOString().slice(0, 10),
            destroy: () => {
                host.removeEventListener('dragover', onDragOver);
                host.removeEventListener('drop', onDrop);
                calendar.destroy();
            }
        };
    };

    const escapeHtml = (value) => String(value == null ? '' : value)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');

    global.DitenCalendar = { create, DRAG_TYPE, STEP_MINUTES, TIMEGRID_HEIGHT, resolveDrop, businessHoursFor, buildLocale };
})(typeof window !== 'undefined' ? window : globalThis);
