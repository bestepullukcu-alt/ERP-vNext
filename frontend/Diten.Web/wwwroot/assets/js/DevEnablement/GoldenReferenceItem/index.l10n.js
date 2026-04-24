'use strict';

(function () {
    const payload = document.getElementById('goldenreferenceitem-l10n');
    const requiredKeys = [
        'AddNew',
        'Actions',
        'Apply',
        'AreYouSure',
        'BulkDeleteConfirm',
        'BulkDeleteSuccess',
        'Code',
        'ComingSoon',
        'Description',
        'Edit',
        'EditItem',
        'ErrorOccurred',
        'Filter',
        'Import',
        'LevelPrefix',
        'Name',
        'NotAvailable',
        'QuickView',
        'RecordDeleted',
        'Reset',
        'SaveView',
        'ShowAll',
        'Status',
        'ViewDetails'
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

    // Helper to match the Diten standard of PascalCase access (L.AddNew) vs JSON camelCase (addNew)
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[GoldenReferenceItem] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
