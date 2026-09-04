'use strict';

/*
 * MOD-0024 checklist templates — page dictionary.
 *
 * NOTE what is NOT here: the Dt* keys. The DataTable chrome arrives from ONE shared payload the layout emits and
 * dt-defaults reads (BL-047b). Seeding them per page is what left a sibling screen in this module rendering
 * "No data available in table" on a Turkish page.
 */
(function () {
    const payload = document.getElementById('taskchecklisttemplates-l10n');
    const requiredKeys = [
        'Actions', 'Active', 'AddNew', 'Apply', 'AreYouSure', 'BlockingCount', 'Cancel', 'Code',
        'ColumnVisibility', 'ComingSoon', 'Delete', 'Details', 'Edit', 'EditItem', 'ErrorOccurred', 'Export',
        'Filter', 'Gating', 'GatingAdvisory', 'GatingBlocking', 'Import', 'ItemCount', 'Name', 'NeverEdited',
        'NoBlockingHint', 'NotAvailable', 'Passive', 'QuickView', 'RecordDeleted', 'RecordSaved', 'Reset',
        'SaveView', 'Search', 'Status', 'Unknown', 'UpdatedAt', 'ViewDetails'
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
        console.error('[TaskChecklistTemplates] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
