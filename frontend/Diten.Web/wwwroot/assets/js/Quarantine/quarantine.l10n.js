'use strict';

// MOD-0175 Q5 — merges the quarantine vocabulary (Views/Quarantine/_QuarantineL10n.cshtml, Quarantine.Keys) into
// window.QuarantineL10n. A missing or empty value is reported, never replaced by hardcoded text (JS-016).
(function () {
    const payload = document.getElementById('quarantine-l10n');
    window.QuarantineL10n = window.QuarantineL10n || {};
    if (!payload) {
        console.warn('[L10N WARNING] quarantine-l10n payload is missing.');
        return;
    }
    try {
        const raw = JSON.parse(payload.textContent || '{}');
        Object.keys(raw).forEach((key) => {
            if (!raw[key] || raw[key] === key) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
        });
        window.QuarantineL10n = Object.assign({}, window.QuarantineL10n, raw);
    } catch (error) {
        console.error('[Quarantine] Localization payload could not be parsed.', error);
    }
})();
