'use strict';

/*
 * BL-531 / BL-512 FIX1 — the ONE select2 "ask the server as the reader types" transport, for every server-searched
 * picker: the people searches (task approver / reviewer, Task Center delegate window, every Meetings picker) and the
 * task form's record-backed custom fields.
 *
 * ⚠ select2 4.0.13's own contract (AjaxAdapter.query), and the reason this file exists rather than three copies of
 * it: the transport RETURNS the request object, and on failure select2 evaluates `'status' in request` before it
 * shows `errorLoading`. A transport that returns nothing makes that line throw; the dropdown then sits on
 * "searching…" forever and the failure is said nowhere (the BL-512 review finding, present in all three earlier
 * transports). So `transport` returns a request object: it has no `status` (a failure is never mistaken for an
 * abort), and its `abort()` — which select2 calls when the reader types again — silences the late answer.
 *
 * What a picker says, told apart in words:
 *  - fewer than two characters → "type at least two characters" (people searches only);
 *  - an empty answer → "nobody found";
 *  - a READ that failed → 429 has its own sentence ("too many searches"), anything else (403, 503, a dropped
 *    connection) is "the search could not be done" — never "nobody found" (BL-491's rule).
 *
 * `options({ search, text, exclude })` — the people searches. search(term) → Promise<{ ok, status, reasonCode, data: rows[] }>;
 *   text → { minimumLength, noResults, searching, unknown, rateLimited, failed }; exclude → ids (or a function
 *   returning them, read at each search) that are never offered.
 * `transport({ search, toResults, onFailure })` — the bare transport, for a source with its own rows.
 */
(function (global) {
    const MIN_LENGTH = 2;
    const DELAY_MS = 300;
    const RATE_LIMITED = 'PEOPLE_SEARCH_RATE_LIMITED';

    /** "Name — Position — Unit"; a row without a name reads as `unknown`, never as its id. */
    const label = (row, unknown) => [row?.displayName || unknown, row?.positionName, row?.organizationUnitName]
        .filter((part) => part && String(part).trim())
        .join(' — ');

    /** The one rule for a failed read's sentence: 429 is "too many searches", anything else "could not search". */
    const failureSentence = (res, words) => {
        const text = words || {};
        const rateLimited = res && (res.status === 429 || res.reasonCode === RATE_LIMITED);
        return (rateLimited ? text.rateLimited : text.failed) || text.failed || '';
    };

    /*
     * search(term) → Promise<{ ok, status, reasonCode, data }> (a bare array is read as a successful answer);
     * toResults(rows) → select2 results; onFailure(res) runs before select2 is told, so `errorLoading` can read it.
     */
    const transport = ({ search, toResults, onFailure }) => {
        let sequence = 0;
        return (params, success, fail) => {
            const mine = ++sequence;
            const request = { aborted: false, abort() { request.aborted = true; } };
            const live = () => !request.aborted && mine === sequence;   // stale: the reader has typed since
            const term = String(params?.data?.term || '').trim();
            const failed = (res, error) => {
                if (!live()) { return; }
                if (typeof onFailure === 'function') { onFailure(res); }
                fail(error ?? res);
            };
            // Asked NOW (not on a later tick), so a newer keystroke's request is the one in flight; a search that
            // throws instead of rejecting is still a failed read.
            let asked;
            try { asked = Promise.resolve(search(term)); } catch (error) { asked = Promise.reject(error); }
            asked
                .then((answer) => {
                    const res = Array.isArray(answer) ? { ok: true, status: 200, data: answer } : answer;
                    if (!res || !res.ok) { failed(res || { ok: false, status: 0 }); return; }
                    if (!live()) { return; }
                    success({ results: toResults(Array.isArray(res.data) ? res.data : []) });
                })
                .catch((error) => failed({ ok: false, status: 0 }, error));
            return request;
        };
    };

    const options = ({ search, text, exclude } = {}) => {
        const words = text || {};
        const excludedNow = () => (typeof exclude === 'function' ? exclude() : exclude || [])
            .map((id) => String(id || '').toLowerCase());
        let failure = '';

        /*
         * ATT-FIX1 — select2 counts the RAW input against minimumInputLength, the request is made with the TRIMMED
         * term: " a" passed select2's check, reached the server as "a", got a 400 and spent a permit of the shared
         * bucket. A trimmed term shorter than the minimum is therefore answered here — no request, and the words
         * are "type at least two characters".
         */
        const TOO_SHORT = { ok: false, status: 0, tooShort: true };
        const searchTrimmed = (term) => (term.length < MIN_LENGTH ? TOO_SHORT : search(term));

        return {
            minimumInputLength: MIN_LENGTH,
            ajax: {
                delay: DELAY_MS,
                transport: transport({
                    search: searchTrimmed,
                    onFailure: (res) => { failure = res && res.tooShort ? (words.minimumLength || '') : failureSentence(res, words); },
                    toResults: (rows) => {
                        failure = '';
                        const excluded = excludedNow();
                        return rows
                            .filter((row) => !excluded.includes(String(row.userId || '').toLowerCase()))
                            .map((row) => ({ id: row.userId, text: label(row, words.unknown) }));
                    }
                })
            },
            language: {
                inputTooShort: () => words.minimumLength || '',
                noResults: () => words.noResults || '',
                errorLoading: () => failure || words.failed || '',
                searching: () => words.searching || ''
            }
        };
    };

    global.DitenPeopleSearch = { MIN_LENGTH, DELAY_MS, label, failureSentence, transport, options };
})(typeof window !== 'undefined' ? window : globalThis);
