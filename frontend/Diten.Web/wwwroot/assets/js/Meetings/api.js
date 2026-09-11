'use strict';

/*
 * MOD-0357 S3 — same-origin API client, mirroring Tasks/api.js exactly (WP's own NASIL instruction). Every call
 * goes to /Meetings/api/* on this app; the JWT lives in an HTTP-only cookie the server attaches, so no token and
 * no service port ever appears in the browser.
 */
(function (global) {
    const BASE = '/Meetings/api';

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
        MEETING_AGENDA_ITEM_NOT_FOUND: 'errorAgendaItemNotFound',
        MEETING_AGENDA_REORDER_MISMATCH: 'errorAgendaReorderMismatch',
        MEETING_TYPE_NOT_FOUND: 'errorTypeNotFound',
        MEETING_TYPE_NAME_DUPLICATE: 'errorTypeNameDuplicate',
        MEETING_TYPE_IN_USE: 'errorTypeInUse',

        // ── S4 — the meeting↔task bridge ────────────────────────────────────
        MEETING_TASK_ALREADY_LINKED: 'errorTaskAlreadyLinked',
        MEETING_REVIEW_ALREADY_SCHEDULED: 'errorReviewAlreadyScheduled'
    };

    const isConcurrencyConflict = (result) =>
        result?.status === 409 && (!result.reasonCode || result.reasonCode === 'MEETING_CONCURRENCY_CONFLICT');

    const failureMessage = (result) => {
        const t = (key) => global.MeetingsL10n?.t?.(key) ?? key;
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

        // ── S4 — the meeting↔task bridge ────────────────────────────────────
        createTaskFromMeeting: (id, payload) => request('POST', `/${id}/tasks`, payload),
        linkExistingTask: (id, taskId, payload) => request('POST', `/${id}/tasks/${taskId}/link`, payload ?? {}),
        scheduleReviewMeetingForTask: (taskId, payload) =>
            request('POST', `/tasks/${taskId}/schedule-review-meeting`, payload),

        lookupAttendees: () => request('GET', '/lookups/attendees'),
        lookupTypes: () => request('GET', '/lookups/types'),

        // ── S8 — Meeting Types (types-manage) ───────────────────────────────
        typesList: () => request('GET', '/types'),
        typesGet: (id) => request('GET', `/types/${id}`),
        typesCreate: (payload) => request('POST', '/types', payload),
        typesUpdate: (id, payload) => request('PUT', `/types/${id}`, payload),
        typesDelete: (id) => request('DELETE', `/types/${id}`)
    };
})(typeof window !== 'undefined' ? window : globalThis);
