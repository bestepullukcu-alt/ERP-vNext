'use strict';

// MOD-0174 P4 — merges the page vocabulary (Views/Traceability/LotMigration/_IndexL10n.cshtml: TraceL10nKeys.LotMigration, dotted resx
// names) into window.TraceL10n. A missing or empty value is reported, never replaced by hardcoded text.
(function () {
    const payload = document.getElementById('lotmigration-l10n');
    window.TraceL10n = window.TraceL10n || {};
    if (!payload) {
        console.warn('[L10N WARNING] lotmigration-l10n payload is missing.');
        return;
    }
    try {
        const raw = JSON.parse(payload.textContent || '{}');
        Object.keys(raw).forEach((key) => {
            if (!raw[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
        });
        window.TraceL10n = Object.assign({}, window.TraceL10n, raw);
    } catch (error) {
        console.error('[LotMigration] Localization payload could not be parsed.', error);
    }
})();
