'use strict';

(function () {
    const payload = document.getElementById('pvg-case-intake-triage-l10n');
    if (!payload) return;

    // The bridge MUST PascalCase every key: the JSON payload is serialized camelCase, and the module's scripts read PascalCase.
    const toPascalCase = key => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign(window.L10n || {}, normalized);
        window.PvgCaseIntakeTriageL10n = window.L10n;
    } catch (error) {
        window.PvgCaseIntakeTriageL10n = {};
    }
})();
