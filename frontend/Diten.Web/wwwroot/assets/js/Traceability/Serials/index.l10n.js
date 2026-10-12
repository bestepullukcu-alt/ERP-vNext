'use strict';

// MOD-0174 P3 — merges the page vocabulary (Views/Traceability/Serials/_IndexL10n.cshtml: TraceL10nKeys.Serials, dotted resx
// names) into window.TraceL10n. A missing or empty value is reported, never replaced by hardcoded text.
(function () {
    const payload = document.getElementById('serials-l10n');
    window.TraceL10n = window.TraceL10n || {};
    if (!payload) {
        console.warn('[L10N WARNING] serials-l10n payload is missing.');
        return;
    }
    try {
        const raw = JSON.parse(payload.textContent || '{}');
        Object.keys(raw).forEach((key) => {
            if (!raw[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
        });
        window.TraceL10n = Object.assign({}, window.TraceL10n, raw);
    } catch (error) {
        console.error('[Serials] Localization payload could not be parsed.', error);
    }
})();
