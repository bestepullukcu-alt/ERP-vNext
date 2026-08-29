'use strict';

(function () {
    const payload = document.getElementById('accounts-l10n');
    const requiredKeys = [
        'AccountCategory', 'AccountCode', 'AccountName', 'AccountType', 'Actions',
        'Active', 'AddNew', 'Apply', 'AreYouSure', 'BulkDelete', 'BulkDeleteConfirm',
        'BulkDeleteSuccess', 'Cancel', 'ColumnVisibility', 'ComingSoon', 'Delete',
        'Description', 'Details', 'Edit', 'EditItem', 'ErrorOccurred', 'Export',
        'Filter', 'FormTitleCreate', 'FormTitleEdit', 'FormValidationError', 'Import',
        'NotAvailable', 'Passive', 'QuickView', 'RecordCreated', 'RecordDeleted',
        'RecordSaved', 'RecordUpdated', 'Reset', 'Save', 'SaveView', 'Search',
        'ShowAll', 'Status', 'Unknown', 'Update', 'ViewDetails'
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

    // The Razor payload serializes camelCase; the scripts read PascalCase.
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
        console.error('[Accounts] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
