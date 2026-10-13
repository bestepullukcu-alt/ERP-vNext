'use strict';

// MOD-0022 E5W1 — merges the e-signature vocabulary (Views/ESignature/_ESignatureL10n.cshtml, ESignature.Keys) into
// window.ESignL10n. A missing or empty value is reported, never replaced by hardcoded text (JS-016).
(function () {
    const payload = document.getElementById('esign-l10n');
    window.ESignL10n = window.ESignL10n || {};
    if (!payload) {
        console.warn('[L10N WARNING] esign-l10n payload is missing.');
        return;
    }
    try {
        const raw = JSON.parse(payload.textContent || '{}');
        Object.keys(raw).forEach((key) => {
            if (!raw[key] || raw[key] === key) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
        });
        window.ESignL10n = Object.assign({}, window.ESignL10n, raw);
    } catch (error) {
        console.error('[ESign] Localization payload could not be parsed.', error);
    }
})();
