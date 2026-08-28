'use strict';

(function () {
    const payload = document.getElementById('contacts-l10n');
    const requiredKeys = [
        'Actions', 'Active', 'AddNew', 'Apply', 'AreYouSure', 'BulkDelete', 'BulkDeleteConfirm',
        'BulkDeleteSuccess', 'Cancel', 'ColumnVisibility', 'ComingSoon', 'ContactType', 'Delete',
        'Description', 'Details', 'DisplayName', 'Edit', 'EditItem', 'Email', 'ErrorOccurred', 'Export',
        'Filter', 'FormTitleCreate', 'FormTitleEdit', 'FormValidationError', 'Import', 'NotAvailable',
        'Passive', 'Phone', 'ProfessionalTitle', 'QuickView', 'RecordCreated', 'RecordDeleted',
        'RecordSaved', 'RecordUpdated', 'Reset', 'Save', 'SaveView', 'Search', 'ShowAll', 'Status',
        'Unknown', 'Update', 'ViewDetails',
        // MOD-0150 Import/Export Task 1
        'Download', 'DownloadTemplate', 'DownloadTemplateHint', 'ExportContacts', 'ExportOptions',
        'ExportIncludeLinks', 'ExportIncludeHistorical', 'ExportIncludeNotes', 'ExportIncludeAccounts',
        'ExportPiiWarning', 'ExportNotesWarning', 'ExportStarted', 'ImportComingSoon'
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
        console.error('[Contacts] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
