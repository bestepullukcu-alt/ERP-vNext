'use strict';

/*
 * MOD-0024 — same-origin API client. Every call goes to /Tasks/api/* on this app; the JWT lives in an HTTP-only
 * cookie the server attaches, so no token and no service port ever appears in the browser.
 */
(function (global) {
    const BASE = '/Tasks/api';

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
            // The upstream reason code is passed through so the UI can react precisely (e.g. a claim race).
            reasonCode: payload?.reason_code ?? payload?.reasonCode ?? null,
            data: payload?.data ?? null,
            errors: payload?.errors ?? []
        };
    };

    /*
     * Turns an API failure into the message the user should read, by REASON CODE rather than by passing server
     * text through — that is how the message stays in the user's language (the code→resx bridge).
     * Keys are camelCase because that is what the serialized l10n payload contains.
     */
    const REASON_CODE_MESSAGE_KEYS = {
        ORGANIZATION_UNIT_UNRESOLVED: 'errorOrganizationUnitUnresolved',
        TASK_ALREADY_CLAIMED: 'errorAlreadyClaimed',
        POSITION_NOT_ASSIGNABLE: 'errorPositionNotAssignable'
    };

    const failureMessage = (result) => {
        const t = (key) => global.TasksL10n?.t?.(key) ?? key;
        const byReason = REASON_CODE_MESSAGE_KEYS[result?.reasonCode];
        if (byReason) { return t(byReason); }
        if (result?.status === 403) { return t('errorNoAccess'); }
        if (result?.status === 0) { return t('errorUnavailable'); }
        return t('errorOccurred');
    };

    global.TasksApi = {
        REASON_CODE_MESSAGE_KEYS,
        failureMessage,
        list: () => request('GET', '/list'),
        get: (id) => request('GET', `/${id}`),
        create: (payload) => request('POST', '', payload),
        update: (id, payload) => request('PUT', `/${id}`, payload),
        transition: (id, action, payload) => request('POST', `/${id}/${action}`, payload || {}),
        assignablePositions: () => request('GET', '/assignable-positions'),
        assignablePeople: () => request('GET', '/assignable-people'),

        // ── Phase 2 ──────────────────────────────────────────────────────────
        // expectedVersion guards the checklist RUN, which has its own version separate from the task's.
        setChecklistItemState: (taskId, payload) =>
            request('POST', `/${taskId}/checklist/items/state`, payload),
        addChecklistItem: (taskId, payload) => request('POST', `/${taskId}/checklist/items`, payload),
        createFromTemplate: (payload) => request('POST', '/from-template', payload)
    };
})(typeof window !== 'undefined' ? window : globalThis);
