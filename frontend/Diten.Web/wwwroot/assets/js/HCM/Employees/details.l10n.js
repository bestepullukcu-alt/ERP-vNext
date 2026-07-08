'use strict';

(function () {
    const payload = document.getElementById('employeemaster-detail-l10n');
    const requiredKeys = [
        'DependencyError', 'EmptyValue', 'ErrorOccurred', 'ForbiddenState',
        'GovernmentIdentifierAbsent', 'GovernmentIdentifierPresentMasked',
        'Loading', 'NotFoundState', 'SensitiveFieldsMasked', 'SensitiveFieldsSafeOnly'
    ];

    const logMissingKeys = (dictionary) => {
        requiredKeys.forEach((key) => {
            if (!dictionary[key]) {
                console.warn(`[L10N WARNING] Missing localization key: ${key}`);
            }
        });
    };

    if (!payload) {
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
        return;
    }

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        window.L10n = Object.assign({}, window.L10n || {}, raw);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[EmployeeMasterDetail] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
