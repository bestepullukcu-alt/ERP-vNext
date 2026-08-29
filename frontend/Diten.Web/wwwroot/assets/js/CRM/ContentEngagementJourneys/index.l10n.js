(function (window, document) {
    'use strict';
    let values = {};
    const payload = document.getElementById('content-engagement-journeys-l10n');
    try { values = JSON.parse(payload.textContent || '{}'); }
    catch (error) { console.error('[ContentEngagementJourneys] Localization payload could not be parsed.', error); }
    // camelCase → PascalCase merge into window.L10n so JS keys resolve (avoids "(undefined: corrId)" toasts).
    const toPascal = k => k.charAt(0).toUpperCase() + k.slice(1);
    const merged = {};
    Object.keys(values).forEach(k => { merged[k] = values[k]; merged[toPascal(k)] = values[k]; });
    window.L10n = Object.assign({}, window.L10n || {}, merged);
    window.ContentEngagementJourneysL10n = Object.freeze(values);
})(window, document);
