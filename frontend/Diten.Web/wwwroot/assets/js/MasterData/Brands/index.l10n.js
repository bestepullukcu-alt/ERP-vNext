(function (window, document) {
    'use strict';
    // The payload is emitted with PascalCase keys by _IndexL10n.cshtml and is merged into window.L10n verbatim.
    // Lower-casing or camel-casing here would make every window.L10n["BrandName"] lookup return undefined and
    // surface as a "(undefined: <correlationId>)" toast.
    let values = {};
    const payload = document.getElementById('brand-l10n');
    try { values = JSON.parse(payload.textContent || '{}'); }
    catch (error) { console.error('[Brands] Localization payload could not be parsed.', error); }
    window.L10n = Object.assign({}, window.L10n || {}, values);
    window.BrandL10n = Object.freeze(values);
})(window, document);
