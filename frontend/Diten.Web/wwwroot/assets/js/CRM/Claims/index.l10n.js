(function (window, document) {
    'use strict';
    let values = {};
    const payload = document.getElementById('claim-l10n');
    try { values = JSON.parse(payload.textContent || '{}'); }
    catch (error) { console.error('[Claims] Localization payload could not be parsed.', error); }
    window.L10n = Object.assign({}, window.L10n || {}, values);
    window.ClaimL10n = Object.freeze(values);
})(window, document);
