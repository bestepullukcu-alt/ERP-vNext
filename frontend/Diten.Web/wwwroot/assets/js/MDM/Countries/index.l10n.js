'use strict';

(function () {
    const payload = document.getElementById('countries-l10n');
    if (!payload) {
        window.L10n = window.L10n || {};
        return;
    }

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
    } catch (error) {
        console.error('Countries localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
    }
})();
