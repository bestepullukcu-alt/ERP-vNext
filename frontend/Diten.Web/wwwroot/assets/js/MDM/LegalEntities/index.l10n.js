'use strict';

(function () {
    const payload = document.getElementById('legal-entities-l10n');
    if (!payload) {
        window.L10n = window.L10n || {};
        return;
    }

    // ASP.NET Json.Serialize outputs camelCase keys by default.
    // JS code accesses PascalCase (e.g. L.AddNewCompany), so restore the first letter to uppercase.
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
    } catch (error) {
        console.error('LegalEntities localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
    }
})();
