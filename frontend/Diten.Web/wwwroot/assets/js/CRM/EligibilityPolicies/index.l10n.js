(function (window, document) {
    'use strict';
    let values = {};
    const payload = document.getElementById('policy-l10n');
    try { values = JSON.parse(payload.textContent || '{}'); }
    catch (error) { console.error('[EligibilityPolicies] Localization payload could not be parsed.', error); }
    window.L10n = Object.assign({}, window.L10n || {}, values);
    window.PolicyL10n = Object.freeze(values);
})(window, document);
