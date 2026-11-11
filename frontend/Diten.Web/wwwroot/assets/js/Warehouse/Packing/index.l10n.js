'use strict';

// MOD-0178 DL-6 — merges the Packing page's words (Views/Warehouse/Packing/Index.cshtml, #packing-l10n) into window.L10n. The Warehouse.*
// vocabulary arrives as L10n.Words (keys kept as written). A missing key is reported, never replaced by hardcoded text.
(function () {
    const payload = document.getElementById('packing-l10n');
    const requiredKeys = ['AreYouSure', 'Words'];
    const logMissingKeys = (dictionary) => requiredKeys.forEach((key) => { if (!dictionary[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`); });

    if (!payload) {
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
        return;
    }

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);
    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) normalized[toPascalCase(key)] = raw[key];
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[Packing] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
