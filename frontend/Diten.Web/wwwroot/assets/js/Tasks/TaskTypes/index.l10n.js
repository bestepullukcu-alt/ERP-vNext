'use strict';

(function () {
    const payload = document.getElementById('tasktypes-l10n');
    /*
     * ⚠ THIS SURFACE'S OWN KEYS, not the sibling's. The list was inherited from the field-definition screen and
     * still named ValueType, Section, Label, Required, Optional, BulkDelete… — none of which this screen has.
     * A "missing key" warning for a key the screen does not use trains people to ignore the warnings, which is
     * the only thing this list exists to prevent.
     */
    const requiredKeys = [
        'Active', 'Activate', 'AddNew', 'Actions', 'Apply', 'AreYouSure', 'Cancel', 'Code',
        'ColumnVisibility', 'ComingSoon', 'Deactivate', 'DeactivateConfirm', 'Description',
        'Details', 'Edit', 'ErrorOccurred', 'Export', 'Filter', 'FormTitleCreate', 'FormTitleEdit',
        'FormValidationError', 'GqmsDomain', 'Import', 'Name', 'NotAvailable', 'Passive',
        'QualityEventNo', 'QualityEventYes', 'QuickView', 'RecordActivated', 'RecordClass',
        'RecordCreated', 'RecordDeactivated', 'RecordSaved', 'RecordUpdated', 'Reset', 'Save',
        'SaveView', 'Search', 'ShowAll', 'Status', 'Unknown', 'Update', 'ViewDetails'
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
        console.error('[TaskTypes] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
