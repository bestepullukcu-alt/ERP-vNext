// WP-ROLES-CLOSE-01 — one way to say a refusal in the reader's language (Roles, Role Permissions, User Roles).
//
// AuthService tags a refusal with a stable code and keeps its English sentence; the screen maps the code to one of
// its own resx keys. This is the Users screen's bridge (ERROR_CODE_KEYS), lifted out so three screens do not write it
// three times. It reads both shapes a refusal arrives in:
//   - a governance proxy's answer        { success:false, errors, errorCode, local }
//   - the Response envelope, direct call { isSuccessful:false, errors, errorCodes:[{ code }] }
//
// A refusal WITHOUT a known code never shows the server's sentence (it is English whatever the reader's language):
// the screen says its general error sentence and the console gets a warning. The one exception is `local: true` —
// a sentence this application produced itself, already localized (signed out, not permitted, form validation).
(function (global) {
    'use strict';

    function codeOf(json) {
        if (!json || typeof json !== 'object') return null;
        if (typeof json.errorCode === 'string' && json.errorCode) return json.errorCode;
        var codes = Array.isArray(json.errorCodes) ? json.errorCodes : [];
        for (var i = 0; i < codes.length; i++) {
            if (codes[i] && typeof codes[i].code === 'string' && codes[i].code) return codes[i].code;
        }
        return null;
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

    global.DitenRefusal = { codeOf: codeOf, message: message };
})(typeof window !== 'undefined' ? window : this);
