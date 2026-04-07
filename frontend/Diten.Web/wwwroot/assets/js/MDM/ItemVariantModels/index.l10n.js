'use strict';

(function () {
    const payload = document.getElementById('item-variant-models-l10n');
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
        });
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
    } catch (error) {
        console.error('ItemVariantModels localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
    }
})();
