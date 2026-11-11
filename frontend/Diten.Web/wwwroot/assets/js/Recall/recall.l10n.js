'use strict';

// MOD-0177 R6 — merges the recall vocabulary (Views/Recall/_RecallL10n.cshtml, Recall.Keys) into window.RecallL10n. A missing or empty
// value is reported, never replaced by hardcoded text (JS-016).
(function () {
    const payload = document.getElementById('recall-l10n');
    window.RecallL10n = window.RecallL10n || {};
    if (!payload) {
        console.warn('[L10N WARNING] recall-l10n payload is missing.');
        return;
    }
    try {
        const raw = JSON.parse(payload.textContent || '{}');
        Object.keys(raw).forEach((key) => {
            if (!raw[key] || raw[key] === key) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
        });
        window.RecallL10n = Object.assign({}, window.RecallL10n, raw);
    } catch (error) {
        console.error('[Recall] Localization payload could not be parsed.', error);
    }
})();
