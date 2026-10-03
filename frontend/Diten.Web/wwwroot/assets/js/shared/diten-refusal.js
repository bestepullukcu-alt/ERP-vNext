// WP-ROLES-CLOSE-01 — one way to say a refusal in the reader's language (Roles, Role Permissions, User Roles).
//
// AuthService tags a refusal with a stable code and keeps its English sentence; the screen maps the code to one of
// its own resx keys. This is the Users screen's bridge (ERROR_CODE_KEYS), lifted out so three screens do not write it
// three times. It reads both shapes a refusal arrives in:
//   - a governance proxy's answer        { success:false, errors, errorCode, errorCodes:['CODE', …], local }
//   - the Response envelope, direct call { isSuccessful:false, errors, errorCodes:[{ code }] }
// A refusal can carry SEVERAL codes (a form with two mistakes): messages() says every one of them.
//
// A refusal WITHOUT a known code never shows the server's sentence (it is English whatever the reader's language):
// the screen says its general error sentence and the console gets a warning. The one exception is `local: true` —
// a sentence this application produced itself, already localized (signed out, not permitted, form validation).
(function (global) {
    'use strict';

    /** Every code the refusal carries, in order, once each. */
    function codesOf(json) {
        if (!json || typeof json !== 'object') return [];
        var found = [];
        var add = function (code) { if (typeof code === 'string' && code && found.indexOf(code) < 0) found.push(code); };
        (Array.isArray(json.errorCodes) ? json.errorCodes : []).forEach(function (entry) {
            add(entry && typeof entry === 'object' ? entry.code : entry);
        });
        add(json.errorCode);
        return found;
    }

    function codeOf(json) {
        var codes = codesOf(json);
        return codes.length ? codes[0] : null;
    }

    /**
     * @param {object} json   the refusal as it arrived
     * @param {object} keys   code → resx key of the calling screen (e.g. { ROLE_NOT_FOUND: 'ErrorRoleNotFound' })
     * @param {object} l10n   the screen's labels (window.L10n)
     * @param {string} scope  the screen's name, for the console warning
     * @returns {string} one sentence in the reader's language
     */
    function message(json, keys, l10n, scope) {
        var labels = l10n || {};
        var code = codeOf(json);
        var key = code ? (keys || {})[code] : null;
        if (key && labels[key]) return labels[key];

        var own = json && json.local === true && Array.isArray(json.errors) ? json.errors[0] : null;
        if (typeof own === 'string' && own) return own;

        if (global.console && typeof global.console.warn === 'function') {
            global.console.warn('[' + (scope || 'refusal') + '] A refusal arrived without a known code; the general sentence is shown.',
                { code: code, errors: json && json.errors });
        }
        return labels.ErrorOccurred || '';
    }

    /**
     * Every sentence of a refusal — one per code the screen has a sentence for, in the order they arrived. A refusal
     * with no such code is the single sentence message() gives (this application's own, or the general one).
     * @returns {string[]} never empty
     */
    function messages(json, keys, l10n, scope) {
        var labels = l10n || {};
        var known = codesOf(json)
            .map(function (code) { return labels[(keys || {})[code]]; })
            .filter(function (text) { return typeof text === 'string' && text; });
        return known.length ? known : [message(json, keys, l10n, scope)];
    }

    global.DitenRefusal = { codeOf: codeOf, codesOf: codesOf, message: message, messages: messages };
})(typeof window !== 'undefined' ? window : this);
