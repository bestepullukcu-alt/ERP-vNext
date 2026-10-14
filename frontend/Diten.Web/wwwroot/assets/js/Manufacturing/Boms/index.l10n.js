'use strict';

// MOD-0193 BOM list — reads the page's localized dictionary (_IndexL10n) into window.L10n and names any missing key.
(function () {
    const payload = document.getElementById('boms-l10n');
    const requiredKeys = [
        'Active', 'Passive', 'Unknown', 'Actions', 'Delete', 'Edit', 'ViewDetails', 'Details', 'QuickView', 'Import', 'ComingSoon', 'AreYouSure', 'Cancel', 'Search', 'Export', 'Filter',
        'Apply', 'Reset', 'ShowAll', 'SaveView', 'ColumnVisibility', 'Status', 'NotAvailable', 'RecordDeleted',
        'RecordSaved', 'ErrorOccurred', 'AddNew', 'ItemId', 'Version', 'Description', 'DeleteConfirm',
        'StatusDraft', 'StatusEffective', 'StatusSuperseded'
    ];

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);
    let dictionary = {};
    try {
        const raw = payload ? JSON.parse(payload.textContent || '{}') : {};
        Object.keys(raw).forEach((key) => { dictionary[toPascalCase(key)] = raw[key]; });
    } catch (error) {
        console.error('[Boms] Localization payload could not be parsed.', error);
    }

    window.L10n = Object.assign({}, window.L10n || {}, dictionary);
    requiredKeys.forEach((key) => {
        if (!window.L10n[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
    });
})();
