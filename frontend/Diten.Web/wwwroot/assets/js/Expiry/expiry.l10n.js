'use strict';

// MOD-0176 F4 — merges the expiry vocabulary (Views/Expiry/_ExpiryL10n.cshtml, Expiry.Keys) into window.ExpiryL10n. A missing or empty
// value is reported, never replaced by hardcoded text (JS-016).
(function () {
    const payload = document.getElementById('expiry-l10n');
    window.ExpiryL10n = window.ExpiryL10n || {};
    if (!payload) {
        console.warn('[L10N WARNING] expiry-l10n payload is missing.');
        return;
    }
    try {
        const raw = JSON.parse(payload.textContent || '{}');
        Object.keys(raw).forEach((key) => {
            if (!raw[key] || raw[key] === key) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
        });
        window.ExpiryL10n = Object.assign({}, window.ExpiryL10n, raw);
    } catch (error) {
        console.error('[Expiry] Localization payload could not be parsed.', error);
    }
})();
