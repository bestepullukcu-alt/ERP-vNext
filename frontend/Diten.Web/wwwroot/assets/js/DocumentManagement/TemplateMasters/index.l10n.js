'use strict';

(function () {
    const payload = document.getElementById('templatemasters-l10n');
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    if (!payload) {
        window.L10n = window.L10n || {};
        return;
    }

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        Object.keys(raw).forEach((key) => {
            normalized[toPascalCase(key)] = raw[key];
        });
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
    } catch (error) {
        console.error('[TemplateMasters] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
    }
})();
