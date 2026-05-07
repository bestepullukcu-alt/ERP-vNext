'use strict';

(function () {
    const payload = document.getElementById('subscription-plans-l10n');
    const requiredKeys = [
        'Activate',
        'Active',
        'Cancel',
        'CreatePlan',
        'Currency',
        'Deactivate',
        'Default',
        'Edit',
        'EmptyState',
        'ErrorOccurred',
        'Features',
        'Monthly',
        'Modules',
        'PaidPlans',
        'Passive',
        'Quotas',
        'SubscriptionPlansTitle',
        'Trial',
        'TrialDurationDays',
        'Yearly'
    ];

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    const logMissingKeys = (dictionary) => {
        requiredKeys.forEach((key) => {
            if (!dictionary[key] || dictionary[key] === key) {
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
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }

        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[SubscriptionPlans] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();

