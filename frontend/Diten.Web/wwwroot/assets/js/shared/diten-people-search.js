'use strict';

/*
 * BL-531 (WP-MEETINGS-ATTENDEE-SEARCH-01) — one select2 "search the people directory" setup for every Meetings
 * picker (create, details "add attendee", "change organizer" window, series, report filter).
 *
 * The server side is BL-512's search-only contract: at least two characters, at most 20 rows of four fields
 * (userId, displayName, positionName, organizationUnitName), and a 429 once a person searches too fast. So:
 *  - nothing is fetched when a picker opens — select2 asks only after two typed characters (and a short pause);
 *  - an answer that arrives after the reader has typed again is dropped (it would overwrite the newer one);
 *  - three answers are told apart in words: "type at least two characters", "nobody found", and a READ that failed
 *    (403, 429, dropped connection) — a failure is never shown as "nobody" (BL-491's rule);
 *  - ids the caller passes in `exclude` are never offered (compared case-insensitively); `exclude` may be a
 *    function, read at each search, for a picker whose excluded people change while the page is open.
 *
 * `options({ search, text, exclude })` returns the select2 settings to merge in:
 *   search(term)  → Promise<{ ok, status, reasonCode, data: rows[] }>
 *   text          → { hint?, minimumLength, noResults, searching, unknown, failure(res) → sentence }
 */
(function (global) {
    const MIN_LENGTH = 2;
    const DELAY_MS = 300;

    /** "Name — Position — Unit"; a row without a name reads as `unknown`, never as its id. */
    const label = (row, unknown) => [row?.displayName || unknown, row?.positionName, row?.organizationUnitName]
        .filter((part) => part && String(part).trim())
        .join(' — ');

    const options = ({ search, text, exclude } = {}) => {
        const words = text || {};
        const excludedNow = () => (typeof exclude === 'function' ? exclude() : exclude || [])
            .map((id) => String(id || '').toLowerCase());
        let sequence = 0;
        let failure = '';

        return {
            minimumInputLength: MIN_LENGTH,
            ajax: {
                delay: DELAY_MS,
                /*
                 * ⚠ select2 4.0.13's own contract (AjaxAdapter.query): the transport RETURNS the request object, and on
                 * failure select2 reads `'status' in request` before it shows `errorLoading`. A transport that returns
                 * nothing makes that line throw, and the dropdown then sits on "searching…" forever — a failed read
                 * that says nothing (the BL-512 review finding). So a request object is returned: it has no `status`
                 * (a failure is never mistaken for an abort), and its `abort()` — which select2 calls when the reader
                 * types again — silences the late answer.
                 */
                transport: (params, success, fail) => {
                    const mine = ++sequence;
                    const request = { aborted: false, abort() { request.aborted = true; } };
                    const live = () => !request.aborted && mine === sequence;
                    const term = String(params?.data?.term || '').trim();
                    Promise.resolve()
                        .then(() => search(term))
                        .then((res) => {
                            if (!live()) { return; }   // stale: the reader has typed since
                            if (!res || !res.ok) {
                                failure = typeof words.failure === 'function' ? words.failure(res || { ok: false, status: 0 }) : '';
                                fail(res);
                                return;
                            }
                            failure = '';
                            const excluded = excludedNow();
                            success({
                                results: (Array.isArray(res.data) ? res.data : [])
                                    .filter((row) => !excluded.includes(String(row.userId || '').toLowerCase()))
                                    .map((row) => ({ id: row.userId, text: label(row, words.unknown) }))
                            });
                        })
                        .catch((error) => {
                            if (!live()) { return; }
                            failure = typeof words.failure === 'function' ? words.failure({ ok: false, status: 0 }) : '';
                            fail(error);
                        });
                    return request;
                }
            },
            language: {
                inputTooShort: () => words.minimumLength || '',
                noResults: () => words.noResults || '',
                errorLoading: () => failure || words.error || '',
                searching: () => words.searching || ''
            }
        };
    };

    global.DitenPeopleSearch = { MIN_LENGTH, DELAY_MS, label, options };
})(typeof window !== 'undefined' ? window : globalThis);
