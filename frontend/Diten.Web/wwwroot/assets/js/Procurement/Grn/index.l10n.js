'use strict';

(function () {
    const payload = document.getElementById('grn-l10n');
    const requiredKeys = [
        'Active', 'Passive', 'Unknown', 'Actions', 'AddNew', 'Apply', 'AreYouSure',
        'BulkDelete', 'BulkDeleteConfirm', 'BulkDeleteSuccess', 'Cancel', 'ColumnVisibility',
        'ComingSoon', 'Delete', 'Description', 'Details', 'Edit', 'EditItem',
        'ErrorOccurred', 'Export', 'Filter', 'FormTitleCreate', 'FormTitleEdit',
        'FormValidationError', 'GrnId', 'Import', 'LevelPrefix', 'NotAvailable', 'PoId',
        'QuickView', 'ReceivedAt', 'RecordCreated', 'RecordDeleted', 'RecordSaved',
        'RecordUpdated', 'Reset', 'Save', 'SaveView', 'Search', 'ShowAll', 'Status',
        'StatusDraft', 'StatusPosted', 'StatusReversed', 'Update', 'ViewDetails'
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
        console.error('[Grn] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
