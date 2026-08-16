'use strict';

(function () {
    const payload = document.getElementById('pvg-case-intake-triage-l10n');
    if (!payload) return;

    try {
        const parsed = JSON.parse(payload.textContent || '{}');
        window.L10n = Object.assign(window.L10n || {}, parsed);
        window.PvgCaseIntakeTriageL10n = window.L10n;
    } catch (error) {
        window.PvgCaseIntakeTriageL10n = {};
    }
})();
