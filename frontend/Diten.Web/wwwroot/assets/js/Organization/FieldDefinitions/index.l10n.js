'use strict';

// MOD-0288-FU04 — the window.L10n bridge for the Organization field-definition list.
// Same shape as the Task precedent's loader and sharing no file with it (§2).
(function () {
    const payload = document.getElementById('organizationfielddefinitions-l10n');
    const requiredKeys = [
        'Active', 'Actions', 'AddNew', 'Apply', 'AreYouSure', 'BulkDeactivate',
        'BulkDeactivateConfirm', 'BulkDeactivateSuccess', 'BulkDelete', 'BulkDeleteConfirm',
        'OptionRemove', 'OptionPlaceholder',
        'Cancel', 'Classification', 'ClassificationConfidential', 'ClassificationInternal',
        'ClassificationNormal', 'ClassificationRestricted', 'Code', 'ColumnVisibility',
        'DataType', 'Deactivate', 'DeactivateConfirm', 'DefinitionLimitReached', 'Details',
        'DisplayOrder', 'Edit', 'ErrorOccurred', 'Export', 'FieldTypeBoolean', 'FieldTypeDate',
        'FieldTypeDecimal', 'FieldTypeInteger', 'FieldTypeMultilineText', 'FieldTypeReference',
        'FieldTypeSingleSelect', 'FieldTypeText', 'Filter', 'Import', 'InactiveDefinitionHelp',
        'LoadFailed', 'Name', 'Passive', 'QuickView', 'Queryable', 'ReadOnlyNotice',
        'RecordDeactivated', 'RemainingCapacity', 'Required', 'Reset', 'SaveView', 'Search',
        'ShowAll', 'Showing', 'Status', 'Unknown', 'ViewDetails'
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
        console.error('[OrganizationFieldDefinitions] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
