'use strict';

/*
 * MOD-0024 recurrence rules — page dictionary.
 *
 * NOTE what is NOT here: the six Dt* keys. The DataTable chrome arrives from ONE shared payload the layout
 * emits and dt-defaults reads (BL-047b). Seeding them per page is what left the sibling field-definition screen
 * rendering "No data available in table" on a Turkish page.
 */
(function () {
    const payload = document.getElementById('taskrecurrencerules-l10n');
    const requiredKeys = [
        'Actions', 'Active', 'AddNew', 'Apply', 'AreYouSure', 'AssignmentPerson', 'AssignmentPool',
        'AssignmentTarget', 'Cancel', 'ColumnVisibility', 'ComingSoon', 'Delete', 'Edit', 'EditItem',
        'EndsAt', 'ErrorOccurred', 'Export', 'Filter', 'Frequency', 'FrequencyDaily', 'FrequencyMonthly',
        'FrequencyQuarterly', 'FrequencyWeekly', 'FrequencyYearly', 'Import', 'Interval', 'LastGeneratedAt',
        'Name', 'NeverGenerated', 'NotAvailable', 'OpenEnded', 'Passive', 'QuickView', 'RecordDeleted',
        'RecordSaved', 'Reset', 'SaveView', 'Search', 'StartsAt', 'Status', 'Unknown', 'ViewDetails', 'Details'
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
        console.error('[TaskRecurrenceRules] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
