/**
 * IS THIS RESPONSE JSON? — the ONE answer, for every caller in this application.
 *
 * ⚠ WHY THIS FILE EXISTS. Four scripts asked this question and all four asked it as a SUBSTRING TEST —
 * `contentType.includes('application/json')`. That test is wrong, and it is wrong TODAY, independently of any
 * server change: `'application/problem+json'.includes('application/json')` is FALSE. The substring
 * `application/json` does not occur in `application/problem+json` at all.
 *
 * `+json` is not an exotic spelling. RFC 6839 defines it as the STRUCTURED SYNTAX SUFFIX that says "this media
 * type is carried as JSON" — `application/problem+json` (RFC 9457 problem details),
 * `application/vnd.acme.thing+json`, and every vendor type built on JSON use it. A substring test rejects all
 * of them, so the moment any endpoint answers a `+json` type, the caller silently stops parsing a body it can
 * parse perfectly well and shows the user a raw JSON blob instead of the message inside it. That is exactly
 * what the Platform Administrators error toast did with the gateway's problem+json refusals.
 *
 * ⚠ WHY IT IS ITS OWN FILE and not a member of DtDefaults, where it briefly lived. personalization-client.js
 * is unit-tested standalone, and so is dt-defaults.js; hanging an HTTP rule off the DataTable module made a
 * one-line media-type question require the whole table stack to be present. This file has NO dependencies —
 * it is loaded first by both layouts and can be loaded alone in a test.
 */
'use strict';

window.DitenHttp = (function () {
    /*
     * THE RULE (RFC 6838 §4.2.8): strip parameters, then the response is JSON when the SUBTYPE is exactly
     * `json` or ENDS WITH the `+json` suffix. Deliberately NOT a substring or a bare /json/ test, both of
     * which say yes to `application/jsonp` — a different format that is not JSON and must not be parsed as it.
     */
    var isJsonMediaType = function (contentType) {
        // Parameters (`; charset=utf-8`) are not part of the media type's identity.
        var essence = String(contentType == null ? '' : contentType).split(';')[0].trim().toLowerCase();
        var slash = essence.indexOf('/');
        if (slash < 1 || slash === essence.length - 1) {
            return false;   // no type/subtype pair at all — not a media type
        }

        var subtype = essence.slice(slash + 1);
        return subtype === 'json' || (subtype.length > 5 && subtype.slice(-5) === '+json');
    };

    return {
        isJsonMediaType: isJsonMediaType
    };
})();
