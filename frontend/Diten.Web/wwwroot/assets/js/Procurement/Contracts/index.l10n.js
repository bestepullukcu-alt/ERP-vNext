'use strict';

(function () {
    const payload = document.getElementById('contracts-l10n');
    const requiredKeys = [
        'Active', 'Passive', 'Unknown', 'Actions', 'AddNew', 'Apply', 'AreYouSure',
        'BulkDelete', 'BulkDeleteConfirm', 'BulkDeleteSuccess', 'Cancel', 'ColumnVisibility',
        'ComingSoon', 'ContractId', 'ContractTitle', 'Delete', 'Description', 'Details', 'Edit', 'EditItem',
        'EffectiveFrom', 'ErrorOccurred', 'Export', 'Filter', 'FormTitleCreate', 'FormTitleEdit',
        'FormValidationError', 'Import', 'LevelPrefix', 'NotAvailable',
        'QuickView', 'RecordCreated', 'RecordDeleted', 'RecordSaved',
        'RecordUpdated', 'Reset', 'Save', 'SaveView', 'Search', 'ShowAll', 'Status',
        'StatusDraft', 'StatusInReview', 'StatusActive', 'StatusExpired', 'StatusTerminated',
        'SupplierId', 'Update', 'ViewDetails',
        'ClauseLibrary', 'ClauseCategory', 'ClauseTitle', 'ClauseBody', 'ClauseId', 'AddClause', 'ClauseCreated'
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
        console.error('[Contracts] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
