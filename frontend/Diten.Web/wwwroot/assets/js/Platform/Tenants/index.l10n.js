'use strict';

(function () {
    const payload = document.getElementById('tenants-l10n');
    const requiredKeys = [
        'Actions',
        'Active',
        'AddNewTenants',
        'Apply',
        'AreYouSure',
        'BulkDelete',
        'BulkDeleteConfirm',
        'BulkDeleteSuccess',
        'Cancel',
        'ColumnVisibility',
        'Delete',
        'ErrorOccurred',
        'Export',
        'Filter',
        'Import',
        'QuickView',
        'RecordDeleted',
        'Reactivate',
        'Reset',
        'SaveView',
        'Search',
        'ShowAll',
        'Status',
        'Suspend',
        'TenantReactivated',
        'TenantSuspended',
        'TenantCommercialQuotaTitle',
        'TenantCommercialQuotaError',
        'TenantCommercialQuotaUnauthorized',
        'LoginSecurityTitle',
        'LoginSecuritySaved',
        'Unknown',
        'ViewDetails'
    ];

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

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
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }

        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[Tenants] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
