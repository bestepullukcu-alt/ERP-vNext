'use strict';

(function () {
    const payload = document.getElementById('domain-management-l10n');
    if (!payload) {
        window.L10n = window.L10n || {};
        return;
    }

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        Object.keys(raw).forEach((key) => {
            normalized[toPascalCase(key)] = raw[key];
            if (raw[key] === undefined || raw[key] === key) {
                console.warn('[L10N WARNING] DomainManagement key fallback detected:', key);
            }
        });
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
    } catch (error) {
        console.error('DomainManagement localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
    }
})();
