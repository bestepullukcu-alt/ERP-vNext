'use strict';

/*
 * MOD-0357 S3 — same-origin API client, mirroring Tasks/api.js exactly (WP's own NASIL instruction). Every call
 * goes to /Meetings/api/* on this app; the JWT lives in an HTTP-only cookie the server attaches, so no token and
 * no service port ever appears in the browser.
 */
(function (global) {
    const BASE = '/Meetings/api';
    const LIST_PAGE_SIZE = 200;   // = MeetingListLimits.MaxPageSize on the server
    const LIST_MAX_PAGES = 50;

    const request = async (method, path, body) => {
        let response;
        try {
            response = await global.fetch(`${BASE}${path}`, {
                method,
                headers: body ? { 'Content-Type': 'application/json', Accept: 'application/json' }
                              : { Accept: 'application/json' },
                credentials: 'same-origin',
                body: body ? JSON.stringify(body) : undefined
            });
        } catch (_) {
            return { ok: false, status: 0, reasonCode: 'UNAVAILABLE', data: null };
        }

        let payload = null;
        try { payload = await response.json(); } catch (_) { /* 204 and empty bodies are fine */ }

        return {
            ok: response.ok,
            status: response.status,
            reasonCode: payload?.reason_code ?? payload?.reasonCode ?? null,
            data: payload?.data ?? null,
            errors: payload?.errors ?? []
        };
    };

    /*
     * AC6 — every MEETING_* and MEETING_TYPE_* reason code Platform can return, mapped to its OWN message key. A
     * guard test (meetings-reason-code-bridge.test.js) asserts this list is exhaustive against
     * Diten.Platform's MeetingReasonCodes and that no key is written twice (BL-351's own lesson: a JS object
     * literal keeps only the LAST value for a repeated key, so a duplicate silently drops the first sentence).
     */
    const REASON_CODE_MESSAGE_KEYS = {
        MEETING_NOT_FOUND: 'errorMeetingNotFound',
        MEETING_END_BEFORE_START: 'errorEndBeforeStart',
        MEETING_CANCELLED: 'errorMeetingCancelled',
        MEETING_COMPLETED: 'errorMeetingCompleted',
        MEETING_CANCELLATION_REASON_REQUIRED: 'errorCancellationReasonRequired',
        MEETING_CONCURRENCY_CONFLICT: 'errorConcurrencyConflict',
        MEETING_SELF_FOLLOW_UP: 'errorSelfFollowUp',
        MEETING_FOLLOW_UP_NOT_FOUND: 'errorFollowUpNotFound',
        MEETING_ORGANIZER_INVALID: 'errorOrganizerInvalid',
        MEETING_ATTENDEE_NOT_ELIGIBLE: 'errorAttendeeNotEligible',
        MEETING_ATTENDEE_DUPLICATE: 'errorAttendeeDuplicate',
        MEETING_ATTENDEE_NOT_FOUND: 'errorAttendeeNotFound',
        MEETING_ATTENDEE_IS_ORGANIZER: 'errorAttendeeIsOrganizer',
        MEETING_AGENDA_ITEM_NOT_FOUND: 'errorAgendaItemNotFound',
        MEETING_AGENDA_REORDER_MISMATCH: 'errorAgendaReorderMismatch',
        MEETING_TYPE_NOT_FOUND: 'errorTypeNotFound',
        MEETING_TYPE_NAME_DUPLICATE: 'errorTypeNameDuplicate',
        MEETING_TYPE_IN_USE: 'errorTypeInUse',

        // ── S4 — the meeting↔task bridge ────────────────────────────────────
        MEETING_TASK_ALREADY_LINKED: 'errorTaskAlreadyLinked',
        MEETING_REVIEW_ALREADY_SCHEDULED: 'errorReviewAlreadyScheduled',

        // ── S5 — invitation response ─────────────────────────────────────────
        MEETING_INVITATION_RESPONSE_INVALID: 'errorInvitationResponseInvalid',

        // ── S6 — minutes ───────────────────────────────────────────────────
        MEETING_MINUTES_PUBLISHED: 'errorMinutesPublished',
        MEETING_MINUTES_NOT_PUBLISHED: 'errorMinutesNotPublished',
        MEETING_MINUTES_CORRECTION_REASON_REQUIRED: 'errorMinutesCorrectionReasonRequired',
        MEETING_MINUTES_CONCURRENCY_CONFLICT: 'errorMinutesConcurrencyConflict',
        MEETING_DECISION_NOT_FOUND: 'errorDecisionNotFound',

        // ── S11 — recurring meeting series ───────────────────────────────────
        MEETING_SERIES_NOT_FOUND: 'errorSeriesNotFound',
        MEETING_SERIES_NAME_DUPLICATE: 'errorSeriesNameDuplicate',
        MEETING_SERIES_INVALID_WINDOW: 'errorSeriesInvalidWindow',
        MEETING_SERIES_INTERVAL_INVALID: 'errorSeriesIntervalInvalid',
        MEETING_SERIES_ORGANIZER_REQUIRED: 'errorSeriesOrganizerRequired',

        // ── S12 — meeting report & action register ───────────────────────────
        MEETING_REPORT_INVALID_PERIOD: 'errorReportInvalidPeriod',
        MEETING_REPORT_EXPORT_TOO_LARGE: 'errorReportExportTooLarge'
    };

    const isConcurrencyConflict = (result) =>
        result?.status === 409 && (!result.reasonCode || result.reasonCode === 'MEETING_CONCURRENCY_CONFLICT');

    /*
     * BL-531 — the attendee search shares the task approver picker's people-search contract (BL-512); its two codes
     * are not MeetingReasonCodes, so they live in their own map (the bridge guard above stays exact).
     */
    const PEOPLE_SEARCH_MESSAGE_KEYS = {
        PEOPLE_SEARCH_TOO_SHORT: 'peopleSearchMinimumLength',
        PEOPLE_SEARCH_RATE_LIMITED: 'errorPeopleSearchRateLimited'
    };

    const failureMessage = (result) => {
        const t = (key) => global.MeetingsL10n?.t?.(key) ?? key;
        const byPeopleSearch = PEOPLE_SEARCH_MESSAGE_KEYS[result?.reasonCode];
        if (byPeopleSearch) { return t(byPeopleSearch); }
        const byReason = REASON_CODE_MESSAGE_KEYS[result?.reasonCode];
        if (byReason) { return t(byReason); }
        if (result?.reasonCode) {
            global.console?.warn?.(
                `[MeetingsApi] no message key for reason code "${result.reasonCode}"; showing the generic error.`);
        }
        if (result?.status === 403) { return t('errorNoAccess'); }
        if (result?.status === 0) { return t('errorUnavailable'); }
        return t('errorOccurred');
    };

    global.MeetingsApi = {
        REASON_CODE_MESSAGE_KEYS,
        isConcurrencyConflict,
        failureMessage,
        list: (query) => request('GET', query ? `/list?${query}` : '/list'),
        /*
         * WP-UI-MEETINGS-CALENDAR-01 — the list API with its filter, the way the same-origin proxy takes it: the
         * WHOLE query string as ONE `query` parameter (MeetingsController.ApiList(string? query) forwards `?{query}`
         * upstream). A bare `?fromUtc=…` never reaches Platform — measured on the proxy's signature: `list('pageSize=1000')`
         * above loses its pageSize the same way.
         */
        listQuery: (params) => request('GET', `/list?query=${encodeURIComponent(new URLSearchParams(params).toString())}`),
        /*
         * ATT-FIX1 — every meeting the reader may see, page by page: the server answers at most LIST_PAGE_SIZE rows
         * per call (MeetingListLimits.MaxPageSize), so a caller that wants the whole set pages through it here rather
         * than asking for 1000 at once. `data` is the plain array of rows; a failed page fails the whole read.
         */
        listAll: async (params = {}) => {
            const rows = [];
            for (let page = 1; page <= LIST_MAX_PAGES; page += 1) {
                const query = new URLSearchParams(Object.assign({}, params, { page, pageSize: LIST_PAGE_SIZE })).toString();
                const res = await request('GET', `/list?query=${encodeURIComponent(query)}`);
                if (!res.ok || !res.data) { return Object.assign({}, res, { data: rows }); }
                const items = Array.isArray(res.data.items) ? res.data.items : [];
                rows.push(...items);
                const total = Number(res.data.totalCount ?? rows.length);
                if (rows.length >= total || items.length === 0) { break; }
            }
            return { ok: true, status: 200, reasonCode: null, data: rows };
        },
        get: (id) => request('GET', `/${id}`),
        create: (payload) => request('POST', '', payload),
        update: (id, payload) => request('PUT', `/${id}`, payload),
        cancel: (id, payload) => request('POST', `/${id}/cancel`, payload),
        reassignOrganizer: (id, payload) => request('POST', `/${id}/reassign-organizer`, payload),
        addAttendees: (id, payload) => request('POST', `/${id}/attendees`, payload),
        removeAttendee: (id, userId) => request('DELETE', `/${id}/attendees/${userId}`),
        addAgendaItem: (id, payload) => request('POST', `/${id}/agenda`, payload),
        reorderAgenda: (id, payload) => request('PUT', `/${id}/agenda/order`, payload),
        updateAgendaItem: (id, itemId, payload) => request('PUT', `/${id}/agenda/${itemId}`, payload),
        deleteAgendaItem: (id, itemId) => request('DELETE', `/${id}/agenda/${itemId}`),
        linkedTasks: (id) => request('GET', `/${id}/tasks`),

        // S5, K5 — Accept/Decline. `response` is exactly 'Accept' or 'Decline'.
        respond: (id, response) => request('POST', `/${id}/respond`, { response }),

        // ── S6 — minutes ──────────────────────────────────────────────────
        getMinutes: (id) => request('GET', `/${id}/minutes`),
        saveMinutesDraft: (id, payload) => request('PUT', `/${id}/minutes/draft`, payload),
        publishMinutes: (id, payload) => request('POST', `/${id}/minutes/publish`, payload),
        correctPublishedMinutes: (id, payload) => request('POST', `/${id}/minutes/correct`, payload),

        // ── S4 — the meeting↔task bridge ────────────────────────────────────
        createTaskFromMeeting: (id, payload) => request('POST', `/${id}/tasks`, payload),
        linkExistingTask: (id, taskId, payload) => request('POST', `/${id}/tasks/${taskId}/link`, payload ?? {}),
        scheduleReviewMeetingForTask: (taskId, payload) =>
            request('POST', `/tasks/${taskId}/schedule-review-meeting`, payload),

        // ── S7 — continuation scheduling ─────────────────────────────────────
        scheduleFollowUp: (id, payload) => request('POST', `/${id}/follow-up`, payload),

        // BL-531 — SEARCH-ONLY (≥ 2 characters, ≤ 20 rows of four fields); the whole directory is never asked for.
        // `data` is the plain array of rows, unwrapped HERE once, the way TasksApi.decisionMakers does it.
        lookupAttendees: async ({ search } = {}) => {
            const res = await request('GET', `/lookups/attendees?search=${encodeURIComponent(search ?? '')}`);
            return Object.assign({}, res, { data: Array.isArray(res.data?.people) ? res.data.people : [] });
        },
        lookupTypes: () => request('GET', '/lookups/types'),

        // ── S8 — Meeting Types (types-manage) ───────────────────────────────
        typesList: () => request('GET', '/types'),
        typesGet: (id) => request('GET', `/types/${id}`),
        typesCreate: (payload) => request('POST', '/types', payload),
        typesUpdate: (id, payload) => request('PUT', `/types/${id}`, payload),
        typesDelete: (id) => request('DELETE', `/types/${id}`),

        // ── S11 — Meeting Series (series-manage) ─────────────────────────────
        seriesList: () => request('GET', '/series'),
        seriesGet: (id) => request('GET', `/series/${id}`),
        seriesCreate: (payload) => request('POST', '/series', payload),
        seriesUpdate: (id, payload) => request('PUT', `/series/${id}`, payload),
        seriesDelete: (id) => request('DELETE', `/series/${id}`)
    };
})(typeof window !== 'undefined' ? window : globalThis);
