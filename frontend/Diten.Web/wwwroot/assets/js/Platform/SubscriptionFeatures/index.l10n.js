'use strict';

(function () {
    const payload = document.getElementById('subscription-features-l10n');
    const requiredKeys = [
        'Active',
        'AddOn',
        'Archive',
        'Archived',
        'AvailableInPlans',
        'Cancel',
        'Category',
        'CreateFeature',
        'DataChangedReloadRequired',
        'DisplayName',
        'Draft',
        'Edit',
        'EmptyState',
        'FeatureCode',
        'FeatureSlug',
        'Included',
        'NoResultsState',
        'NotAvailable',
        'Preview',
        'RecordCreated',
        'RecordUpdated',
        'RequiredField',
        'Save',
        'SubscriptionFeaturesTitle'
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
        console.error('[SubscriptionFeatures] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
