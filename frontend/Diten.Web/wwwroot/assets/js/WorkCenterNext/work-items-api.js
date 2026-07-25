'use strict';

/*
 * WC-1b — the REAL work-item data source for WorkCenterNext.
 *
 * Fetches the WC-1 projection through the SAME-ORIGIN proxy (/WorkCenterNext/api/work-items). The browser never
 * addresses a service port and never sees a token — the proxy reads the JWT from the HTTP-only cookie server-side.
 *
 * This module is deliberately separate from app.js so the fetch + mapping logic is unit-testable (app.js itself
 * has no test coverage). It maps the canonical projection onto the EXISTING presentation mapper
 * (WorkCenterNextData.toPresentation) rather than duplicating it.
 */
(function (global) {
    const ENDPOINT = '/WorkCenterNext/api/work-items';

    const STATUS = {
        OK: 'ok',
        UNAUTHORIZED: 'unauthorized',   // 401 — no/expired session
        FORBIDDEN: 'forbidden',         // 403 — permission not granted (expected until the MOD-0018 grant)
        UNAVAILABLE: 'unavailable',     // 503 / network — gateway or Platform down
        ERROR: 'error'
    };

    // The projection carries a full ISO instant, while the presentation mapper's SLA maths expects a date-only
    // string (it appends 'T00:00:00'). Normalize here so the shared mapper stays untouched.
    const toDateOnly = (value) => {
        if (!value) { return null; }
        const text = String(value);
        const match = /^(\d{4}-\d{2}-\d{2})/.exec(text);
        return match ? match[1] : null;
    };

    /*
     * Adapt one canonical projection item to what the presentation mapper consumes.
     * DEC-2: the executable contract does not model `escalation`; the UI already renders a boolean `escalated`
     * signal chip, so the additive projection field is folded onto it. No contract or backend change.
     */
    const adaptProjection = (dto) => {
        const item = Object.assign({}, dto);
        item.dueAt = toDateOnly(dto.dueAt);
        item.escalated = !!(dto.escalated || (dto.escalation && dto.escalation.escalated));
        return item;
    };

    // Validate against the EXECUTABLE AUTHORITY. An item that fails is dropped (never rendered broken) and
    // reported, so a contract drift is visible instead of silently mis-rendering.
    const validateItems = (items) => {
        const contract = global.WorkCenterNextContract;
        if (!contract || typeof contract.validateWorkItem !== 'function') {
            return { valid: items, errors: [] };
        }

        const valid = [];
        const errors = [];
        items.forEach((item) => {
            const result = contract.validateWorkItem(item);
            if (result.valid) {
                valid.push(item);
            } else {
                errors.push(...result.errors);
            }
        });
        return { valid, errors };
    };

    /* Map a raw projection payload (already-parsed array) to presentation items via the shared mapper. */
    const mapPayload = (payloadItems) => {
        const source = Array.isArray(payloadItems) ? payloadItems : [];
        const adapted = source.map(adaptProjection);
        const { valid, errors } = validateItems(adapted);
        const mapper = global.WorkCenterNextData && global.WorkCenterNextData.toPresentation;
        // Provenance is stated explicitly: these are REAL projection items, never showcase fixtures.
        const items = typeof mapper === 'function'
            ? valid.map((item) => mapper(item, { provenance: 'api' }))
            : valid;
        return { items, errors };
    };

    /* Unwrap the Response<T> envelope the Platform API returns. */
    const unwrap = (payload) => {
        if (Array.isArray(payload)) { return payload; }
        if (payload && Array.isArray(payload.data)) { return payload.data; }
        return [];
    };

    const classify = (httpStatus) => {
        if (httpStatus === 401) { return STATUS.UNAUTHORIZED; }
        if (httpStatus === 403) { return STATUS.FORBIDDEN; }
        if (httpStatus === 503 || httpStatus === 502 || httpStatus === 504) { return STATUS.UNAVAILABLE; }
        return STATUS.ERROR;
    };

    const fetchWorkItems = async () => {
        let response;
        try {
            response = await global.fetch(ENDPOINT, {
                method: 'GET',
                headers: { Accept: 'application/json' },
                credentials: 'same-origin'
            });
        } catch (_) {
            // Network-level failure: treat as dependency-unavailable so the shell shows Retry, not a crash.
            return { status: STATUS.UNAVAILABLE, httpStatus: 0, items: [], errors: [] };
        }

        if (!response.ok) {
            return { status: classify(response.status), httpStatus: response.status, items: [], errors: [] };
        }

        let payload = null;
        try {
            payload = await response.json();
        } catch (_) {
            return { status: STATUS.ERROR, httpStatus: response.status, items: [], errors: [] };
        }

        const mapped = mapPayload(unwrap(payload));
        return { status: STATUS.OK, httpStatus: response.status, items: mapped.items, errors: mapped.errors };
    };

    global.WorkCenterNextApi = {
        ENDPOINT,
        STATUS,
        toDateOnly,
        adaptProjection,
        mapPayload,
        unwrap,
        classify,
        fetchWorkItems
    };
})(typeof window !== 'undefined' ? window : globalThis);
