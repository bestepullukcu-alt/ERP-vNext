(function (window, document) {
    'use strict';
    // Keys stay PascalCase exactly as emitted by _IndexL10n.cshtml. Re-casing them here would make every
    // window.L10n["ProductName"] lookup return undefined and show up as "(undefined: <correlationId>)" toasts.
    let values = {};
    const payload = document.getElementById('product-l10n');
    try { values = JSON.parse(payload.textContent || '{}'); }
    catch (error) { console.error('[Products] Localization payload could not be parsed.', error); }
    window.L10n = Object.assign({}, window.L10n || {}, values);
    window.ProductL10n = Object.freeze(values);
})(window, document);
