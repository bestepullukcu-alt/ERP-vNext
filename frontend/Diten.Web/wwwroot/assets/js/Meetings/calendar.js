'use strict';

/*
 * MOD-0357 S3b (2c) — the Meetings page's CALENDAR view (WP-UI-MEETINGS-CALENDAR-01).
 *
 * The same page, a second way to look at it: `?view=calendar` (the list stays the default and the URL says which
 * one is open). The calendar is the shared component (shared/diten-calendar.js), READ-ONLY: nothing on it moves.
 *
 * ── THE SAME SET AS THE LIST ──────────────────────────────────────────────────────────────────────────────
 * The rows come from the SAME list API the table reads (MeetingsApi.listQuery), narrowed to the visible range with
 * its own FromUtc/ToUtc, and then filtered by the SAME predicate the table uses (MeetingsList.matchesFilters) with
 * the SAME applied filters. A filter change redraws both. Every page of the answer is read (totalCount); a range the
 * API cannot give in full is SAID, never cut silently.
 *
 * ── HOW A MEETING IS DRAWN ────────────────────────────────────────────────────────────────────────────────
 * The reader's own answer comes from the calendar feed (GET /WorkCenterNext/api/calendar — the Task Center's feed,
 * an existing address): accepted (the organizer is an accepted attendee) = filled, not answered = dashed. A meeting
 * the reader is ON but the feed does not carry is one they DECLINED — not drawn. A cancelled meeting is not drawn.
 * A meeting the reader can see but is not on (a read-all reader) is drawn filled and quieter. Times are in the
 * TENANT zone the feed names; day shading (weekend, holiday, working hours) is the feed's `days`.
 *
 * ── INVITATIONS ───────────────────────────────────────────────────────────────────────────────────────────
 * The left panel lists the reader's pending invitations — the Task Center's own rows (GET /WorkCenterNext/api/
 * work-items, intent `meetingInvite`) drawn by the ONE shared card (shared/diten-invite-card.js). Accept and Decline
 * go to the existing respond endpoint. Accepting over one of the reader's plan blocks asks first (the feed's
 * `overlapsPlan`); declining asks nothing. Invitations are never dragged.
 */
(function (global) {
    const t = (key) => global.MeetingsL10n?.t?.(key) ?? key;
    const CALENDAR_ENDPOINT = '/WorkCenterNext/api/calendar';
    const WORK_ITEMS_ENDPOINT = '/WorkCenterNext/api/work-items';
    const PAGE_SIZE = 200;
    // 50 × 200 = 10 000 meetings in one visible range. Past that the answer is refused as incomplete, not cut.
    const MAX_PAGES = 50;
    // Platform serializes MeetingLifecycle as its ordinal (see index.js statusLabel): Cancelled = 1.
    const CANCELLED = [1, 'Cancelled'];

    const esc = (value) => String(value == null ? '' : value)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');

    const state = {
        view: 'list',
        controller: null,
        zone: null,
        range: null,
        feed: null,
        rows: [],
        invites: [],
        error: null,
        generation: 0,
        mounting: null
    };

    const el = (selector) => global.document.querySelector(selector);

    /* ── the view switch (URL) ──────────────────────────────────────────────────────────────────────────── */

    const viewFromUrl = () => (new URLSearchParams(global.location.search).get('view') === 'calendar' ? 'calendar' : 'list');

    const writeViewToUrl = (view) => {
        const url = new URL(global.location.href);
        if (view === 'calendar') { url.searchParams.set('view', 'calendar'); } else { url.searchParams.delete('view'); }
        global.history.replaceState(global.history.state, '', url.pathname + url.search + url.hash);
    };

    /* ── network (same-origin; the browser never names a service) ───────────────────────────────────────── */

    const getJson = async (url) => {
        let response;
        try {
            response = await global.fetch(url, { method: 'GET', headers: { Accept: 'application/json' }, credentials: 'same-origin' });
        } catch (_) {
            return { ok: false, status: 0, reasonCode: 'UNAVAILABLE', data: null };
        }
        let body = null;
        try { body = await response.json(); } catch (_) { /* an empty body is still an answer */ }
        return { ok: response.ok, status: response.status, reasonCode: body?.reason_code ?? body?.reasonCode ?? null, data: body?.data ?? null };
    };

    const fetchCalendar = (from, to) =>
        getJson(`${CALENDAR_ENDPOINT}?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`);

    /**
     * Every meeting of the list API that STARTS in [fromUtc, toUtc), all pages, then the page's own filter rule.
     * Returns null when the range cannot be read in full (an error, or more pages than MAX_PAGES).
     */
    const fetchListSet = async (fromUtc, toUtc, isStale = () => false) => {
        const filters = global.MeetingsList.getAppliedFilters();
        const params = { fromUtc, toUtc, pageSize: PAGE_SIZE };
        // What the API can narrow by itself, it does; the predicate below still decides (one rule, not two).
        if (filters.meetingType.length === 1) { params.meetingTypeId = filters.meetingType[0]; }
        if (filters.organizer.length === 1) { params.organizerUserId = filters.organizer[0]; }
        if (filters.iAmAttendee) { params.iAmAttendeeOnly = true; }
        if (filters.hasLinkedTasks) { params.hasLinkedTasksOnly = true; }

        const rows = [];
        for (let page = 1; page <= MAX_PAGES; page += 1) {
            // A range the reader has already paged away from stops asking (CT acceptance: five quick months were
            // five loops of up to 50 calls each).
            if (isStale()) { return null; }
            const result = await global.MeetingsApi.listQuery(Object.assign({}, params, { page }));
            if (!result.ok || !result.data) { return null; }
            const items = result.data.items || [];
            rows.push(...items);
            const total = Number(result.data.totalCount ?? rows.length);
            if (rows.length >= total || items.length === 0) {
                return rows.filter((row) => global.MeetingsList.matchesFilters(row, filters));
            }
        }
        return null;
    };

    /* ── events ─────────────────────────────────────────────────────────────────────────────────────────── */

    const eventsFor = (rows, feed) => {
        const mine = new Map(((feed && feed.meetings) || []).map((meeting) => [meeting.meetingId, meeting]));
        return rows
            .filter((row) => !CANCELLED.includes(row.lifecycle))
            .map((row) => {
                const own = mine.get(row.id);
                // I am on it and my own calendar does not carry it: I declined it. Not drawn.
                if (!own && row.iAmAttendee) { return null; }
                const response = own ? own.response : 'other';
                const kindClass = response === 'accepted' ? 'dc-meeting-accepted'
                    : response === 'pending' ? 'dc-meeting-pending' : 'dc-meeting-other';
                return {
                    id: row.id,
                    title: row.title,
                    kind: 'meeting',
                    allDay: false,
                    startUtc: row.startAt,
                    endUtc: row.endAt,
                    editable: false,
                    classNames: ['dc-meeting', kindClass],
                    extendedProps: { response }
                };
            })
            .filter(Boolean);
    };

    /* ── drawing ────────────────────────────────────────────────────────────────────────────────────────── */

    const renderNotes = () => {
        const host = el('[data-mc-calendar-notes]');
        if (!host) { return; }
        const error = state.error ? `<p class="mc-calendar-error" role="alert"><i class="bx bx-error-circle"></i>${esc(state.error)}</p>` : '';
        const unresolved = state.feed && global.DitenCalendar ? global.DitenCalendar.unresolvedNotice(state.feed.days) : '';
        host.innerHTML = error + unresolved;
    };

    const inviteModel = (dto) => {
        const action = (code) => (dto.actions || []).find((a) => a.code === code);
        const button = (code, tone, answer, labelKey) => {
            const a = action(code);
            if (!a) { return null; }
            return {
                tone,
                label: global.DitenInviteCard.label ? global.DitenInviteCard.label(labelKey) : labelKey,
                attrs: { 'data-mc-invite-answer': answer, 'data-mc-invite-id': dto.id },
                disabled: a.enabled === false
            };
        };
        return {
            id: dto.id,
            title: dto.title && typeof dto.title === 'object' ? (dto.title.text ?? '') : (dto.title ?? ''),
            typeName: dto.source?.objectType || '',
            when: dto.dueAt,
            zone: state.zone,
            organizerName: dto.requester?.displayName || t('unknownUser'),
            attrs: { 'data-mc-invite-open': dto.id, tabindex: '0', role: 'button' },
            buttons: [
                button('acceptInvite', 'accept', 'Accept', 'InviteAccept'),
                button('declineInvite', 'decline', 'Decline', 'InviteDecline')
            ].filter(Boolean)
        };
    };

    const renderInvites = () => {
        const list = el('[data-mc-invite-list]');
        const count = el('[data-mc-invite-count]');
        if (count) { count.textContent = String(state.invites.length); }
        if (!list) { return; }
        list.innerHTML = state.invites.length
            ? state.invites.map((dto) => global.DitenInviteCard.render(inviteModel(dto))).join('')
            : `<p class="mc-invites-empty">${esc(t('calendarInvitesEmpty'))}</p>`;
    };

    /* ── loading ────────────────────────────────────────────────────────────────────────────────────────── */

    const addDays = (isoDate, days) => {
        const date = new Date(`${isoDate}T00:00:00Z`);
        date.setUTCDate(date.getUTCDate() + days);
        return date.toISOString().slice(0, 10);
    };

    const loadInvites = async () => {
        const result = await getJson(WORK_ITEMS_ENDPOINT);
        const items = Array.isArray(result.data) ? result.data : (result.data?.items || []);
        state.invites = result.ok ? items.filter((item) => item.workIntent === 'meetingInvite') : [];
        renderInvites();
    };

    /**
     * One visible range: the feed (the reader's answers, the days, the zone) and the list's set. Only the LAST
     * request may draw — a reader paging week → week sends several, and a late answer must not paint an old week.
     */
    const loadRange = async (range) => {
        const generation = ++state.generation;
        state.range = range;
        const feedResult = await fetchCalendar(range.from, range.to);
        if (generation !== state.generation) { return false; }
        if (!feedResult.ok || !feedResult.data) {
            state.error = t('calendarLoadFailed');
            state.feed = null;
            state.controller?.setData([], []);
            renderNotes();
            return true;
        }
        const Z = global.DitenZonedTime;
        // From a day before the grid (a meeting that starts late the evening before still reaches into it) to the
        // end of its last day; the list API's window is StartAt ∈ [FromUtc, ToUtc).
        const fromUtc = Z.toUtcIso(addDays(range.from, -1), state.zone);
        const toUtc = Z.toUtcIso(addDays(range.to, 1), state.zone);
        const rows = await fetchListSet(fromUtc, toUtc, () => generation !== state.generation);
        if (generation !== state.generation) { return false; }
        state.feed = feedResult.data;
        if (rows === null) {
            state.error = t('calendarLoadFailed');
            state.rows = [];
            state.controller?.setData([], state.feed.days);
        } else {
            state.error = null;
            state.rows = rows;
            state.controller?.setData(eventsFor(rows, state.feed), state.feed.days);
        }
        renderNotes();
        return true;
    };

    const reload = async () => {
        if (state.range) { await loadRange(state.range); }
    };

    /* ── mounting ───────────────────────────────────────────────────────────────────────────────────────── */

    const navigate = (url) => { global.MeetingsCalendar.navigate(url); };

    const mount = async () => {
        const host = el('[data-mc-calendar-host]');
        if (!host || !global.DitenCalendar || state.controller) { return; }
        /*
         * The zone first (the Task Center's v2 F1 lesson): the calendar converts every time in the tenant's zone,
         * so it is built only once the feed has named it. One day's feed is enough to learn it.
         */
        const today = new Date(Date.now()).toISOString().slice(0, 10);
        const first = await fetchCalendar(today, today);
        const zone = first.ok && first.data && global.DitenZonedTime.isValidZone(first.data.timeZoneId) ? first.data.timeZoneId : null;
        if (!zone) {
            state.error = t('calendarLoadFailed');
            renderNotes();
            // Not built: the next switch to the calendar tries again instead of keeping a dead promise.
            state.mounting = null;
            return;
        }
        state.zone = zone;
        state.controller = global.DitenCalendar.create(host, {
            zone,
            view: 'month',
            date: global.DitenZonedTime.localDate(Date.now(), zone),
            editable: false,
            events: [],
            days: [],
            onRangeChange: (range) => {
                const key = `${range.from}|${range.to}`;
                if (state.range && `${state.range.from}|${state.range.to}` === key) { return; }
                loadRange({ from: range.from, to: range.to });
            },
            onEventClick: (id) => navigate(`/Meetings/${id}`)
        });
        global.__mcCalendar = state.controller;
        await loadInvites();
    };

    /* ── answering an invitation ────────────────────────────────────────────────────────────────────────── */

    const answersInFlight = new Set();

    const answer = async (meetingId, response) => {
        // One answer per invitation at a time: a double click must not send two (or ask twice).
        if (answersInFlight.has(meetingId)) { return { outcome: 'cancelled' }; }
        answersInFlight.add(meetingId);
        try {
            return await answerOnce(meetingId, response);
        } finally {
            answersInFlight.delete(meetingId);
        }
    };

    const answerOnce = async (meetingId, response) => {
        const dto = state.invites.find((invite) => invite.id === meetingId);
        if (response === 'Accept') {
            const go = await global.DitenInviteCard.confirmAcceptOverlap({
                meetingId,
                when: dto?.dueAt,
                feed: state.feed,
                fetchCalendar
            });
            if (!go) { return { outcome: 'cancelled' }; }
        }
        const result = await global.MeetingsApi.respond(meetingId, response);
        if (!result.ok) {
            global.DitenModal?.error?.({ title: t('errorOccurred'), message: global.MeetingsApi.failureMessage(result) });
            return { outcome: 'refused', reasonCode: result.reasonCode };
        }
        // Accepting says so; declining is quiet (the card simply goes).
        if (response === 'Accept') { global.DitenModal?.success?.({ title: t('inviteAccepted'), timer: 1200 }); }
        await loadInvites();
        await reload();
        return { outcome: 'done' };
    };

    /* ── the switch ─────────────────────────────────────────────────────────────────────────────────────── */

    const setView = (view, { writeUrl = true } = {}) => {
        state.view = view === 'calendar' ? 'calendar' : 'list';
        const list = el('[data-mc-list-section]');
        const calendar = el('[data-mc-calendar-section]');
        list?.classList.toggle('d-none', state.view !== 'list');
        calendar?.classList.toggle('d-none', state.view !== 'calendar');
        global.document.querySelectorAll('[data-mc-view]').forEach((button) => {
            const on = button.getAttribute('data-mc-view') === state.view;
            button.classList.toggle('active', on);
            button.setAttribute('aria-pressed', String(on));
        });
        if (writeUrl) { writeViewToUrl(state.view); }

        // ONE filter bar: it moves to whichever view is open, so the filters are the same object in both.
        const filterHost = el('#inlineFilterHost');
        const slot = el('[data-mc-calendar-filter-slot]');
        if (state.view === 'calendar') {
            if (filterHost && slot && filterHost.parentNode !== slot) { slot.appendChild(filterHost); }
            state.mounting = state.mounting || mount();
            if (state.controller) { state.controller.calendar.updateSize(); }
        } else {
            global.MeetingsList?.mountInlineFilter?.();
            global.MeetingsList?.adjustColumns?.();
        }
        return state.mounting || Promise.resolve();
    };

    const onClick = (event) => {
        const target = event.target;
        if (!target || !target.closest) { return; }
        const viewButton = target.closest('[data-mc-view]');
        if (viewButton) {
            event.preventDefault();
            setView(viewButton.getAttribute('data-mc-view'));
            return;
        }
        const filterButton = target.closest('[data-mc-calendar-filter]');
        if (filterButton) {
            event.preventDefault();
            global.MeetingsList?.toggleInlineFilter?.();
            return;
        }
        const answerButton = target.closest('[data-mc-invite-answer]');
        if (answerButton) {
            event.preventDefault();
            event.stopPropagation();
            global.__mcLastAnswer = answer(answerButton.getAttribute('data-mc-invite-id'), answerButton.getAttribute('data-mc-invite-answer'));
            return;
        }
        const card = target.closest('[data-mc-invite-open]');
        if (card) { navigate(`/Meetings/${card.getAttribute('data-mc-invite-open')}`); }
    };

    const onFiltersChanged = () => { if (state.controller) { reload(); } };

    // The invitation card is a role="button": Enter and Space open it like a click (its own buttons keep theirs).
    const onKeyDown = (event) => {
        if (event.key !== 'Enter' && event.key !== ' ') { return; }
        const card = event.target && event.target.closest ? event.target.closest('[data-mc-invite-open]') : null;
        if (!card || event.target !== card) { return; }
        event.preventDefault();
        navigate(`/Meetings/${card.getAttribute('data-mc-invite-open')}`);
    };

    const init = () => {
        global.document.addEventListener('click', onClick);
        global.document.addEventListener('keydown', onKeyDown);
        global.document.addEventListener('meetings:filters-changed', onFiltersChanged);
        return setView(viewFromUrl(), { writeUrl: false });
    };

    /** Undo init: the listeners and the calendar (a page leaves nothing bound behind it). */
    const destroy = () => {
        global.document.removeEventListener('click', onClick);
        global.document.removeEventListener('keydown', onKeyDown);
        global.document.removeEventListener('meetings:filters-changed', onFiltersChanged);
        if (state.controller) { state.controller.destroy(); }
        state.controller = null;
        state.mounting = null;
        state.range = null;
        global.__mcCalendar = null;
    };

    global.MeetingsCalendar = {
        init,
        destroy,
        setView,
        reload,
        eventsFor,
        fetchListSet,
        state,
        navigate: (url) => { global.location.href = url; }
    };

    if (global.document && global.document.readyState === 'loading') {
        global.document.addEventListener('DOMContentLoaded', () => global.MeetingsCalendar.init());
    } else if (global.document && !global.__mcNoAutoInit) {
        global.MeetingsCalendar.init();
    }
})(typeof window !== 'undefined' ? window : globalThis);
