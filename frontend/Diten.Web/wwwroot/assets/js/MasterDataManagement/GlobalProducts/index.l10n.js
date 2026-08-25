'use strict';

(function () {
    const payload = document.getElementById('globalproducts-l10n');
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);
    const requiredKeys = [
        'Actions', 'Active', 'AddNew', 'Apply', 'AreYouSure', 'Cancel', 'ColumnVisibility',
        'ComingSoon', 'CreateConfirmation', 'CreateSuccessWithCode', 'Details',
        'ErrorConflict', 'ErrorForbidden', 'ErrorGateway', 'ErrorNotFound',
        'ErrorUnauthorized', 'ErrorValidation', 'Export', 'Filter', 'FormTitleCreate',
        'GlobalProductNameRequired', 'Import', 'LifecycleDraft',
        'LifecycleIdentityApproved', 'LifecyclePendingIdentityApproval',
        'LifecycleRetired', 'NotAvailable', 'Passive', 'QuickView', 'RecordCreated', 'RecordSaved', 'Reset',
        'Save', 'SaveView', 'Search', 'ShowAll', 'Status', 'Unknown', 'ViewDetails'
    ];

    const warnMissing = (dictionary) => requiredKeys.forEach((key) => {
        if (!dictionary[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
    });

    if (!payload) {
        window.L10n = window.L10n || {};
        warnMissing(window.L10n);
        return;
    }

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        warnMissing(window.L10n);
    } catch (error) {
        console.error('[GlobalProducts] Localization payload could not be parsed.', error);
    }
})();
