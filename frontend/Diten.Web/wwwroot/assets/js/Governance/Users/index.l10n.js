'use strict';

(function () {
    const payload = document.getElementById('users-l10n');
    const requiredKeys = [
        'Active', 'Actions', 'AddNew', 'Apply', 'AreYouSure', 'Cancel', 'ColumnVisibility', 'Delete',
        'Edit', 'EditItem', 'Email', 'ErrorOccurred', 'Export', 'Filter', 'FirstName',
        'FormTitleCreate', 'FormTitleEdit', 'FormValidationError', 'LastName', 'NotAvailable',
        'Passive', 'QuickView', 'RecordCreated', 'RecordDeleted', 'RecordSaved', 'RecordUpdated',
        'Reset', 'Roles', 'Save', 'SaveView', 'Search', 'ShowAll', 'Status', 'Unknown', 'Update',
        'ViewDetails', 'Details'
    ];

    const logMissingKeys = (dictionary) => {
        requiredKeys.forEach((key) => {
            if (!dictionary[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
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
        for (const key of Object.keys(raw)) normalized[toPascalCase(key)] = raw[key];
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[Users] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
