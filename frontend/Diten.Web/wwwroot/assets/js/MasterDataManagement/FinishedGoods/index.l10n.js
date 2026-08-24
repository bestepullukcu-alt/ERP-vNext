'use strict';

(function () {
    const payload = document.getElementById('finishedgoods-l10n');
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);
    const requiredKeys = [
        'Actions', 'AddNew', 'Apply', 'Cancel', 'ColumnVisibility', 'CreateConfirmation',
        'CreatePending', 'CreatePendingWithCode', 'CreateSuccessWithCode', 'ErrorConflict',
        'ErrorForbidden', 'ErrorGateway', 'ErrorNotFound', 'ErrorUnauthorized',
        'ErrorValidation', 'Export', 'Filter', 'GskuNotReferenceable', 'GskuRequired',
        'LifecycleDraft', 'LifecycleIdentityApproved',
        'LifecyclePendingIdentityApproval', 'LifecycleRetired', 'NotAvailable',
        'QuickView', 'RecordSaved', 'Reset', 'Save', 'SaveView', 'Search', 'Status',
        'Unknown', 'ViewDetails'
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
        console.error('[FinishedGoods] Localization payload could not be parsed.', error);
    }
})();
