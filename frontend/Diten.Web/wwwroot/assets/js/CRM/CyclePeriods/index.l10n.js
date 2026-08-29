'use strict';

(function () {
    const payload = document.getElementById('cycleperiods-l10n');
    const requiredKeys = [
        'Actions', 'Active', 'ActivateCyclePeriod', 'ActivateCyclePeriodConfirm', 'AmbiguousPeriod',
        'Apply', 'AreYouSure', 'BulkDelete', 'BulkDeleteConfirm', 'BusinessUnitId', 'Cancel',
        'CloseCyclePeriod', 'CloseCyclePeriodConfirm', 'ColumnVisibility', 'ContractUnavailable',
        'CreateCyclePeriod', 'CurrentPeriod', 'CycleCapacity', 'CycleCode', 'CycleName', 'CyclePeriodsTitle',
        'Description', 'Details', 'Edit', 'EmptyState', 'EndDate', 'ErrorOccurred', 'Export',
        'Filter', 'FormTitleCreate', 'FormTitleEdit', 'FormValidationError', 'GeneralInformation',
        'Import', 'Lifecycle', 'Loading', 'NoActivePeriod', 'NotAvailable', 'Passive', 'QuickView',
        'RecordActivated', 'RecordClosed', 'RecordCreated', 'RecordSaved', 'RecordUpdated', 'Reset',
        'Save', 'SaveView', 'Search', 'SequenceInYear', 'ShowAll', 'StartDate', 'Status',
        'StatusActive', 'StatusClosed', 'StatusDraft', 'TenantWide', 'Unknown', 'Update',
        'UpdatedAt', 'ViewDetails', 'Year',
        // FU07 scope keys
        'BusinessUnitFromTerritory', 'BusinessUnitFromVocabulary', 'BusinessUnitNoPlan', 'CountryScope',
        'LegalEntity', 'Scope', 'ScopeType', 'ScopeTypeBusinessUnit', 'ScopeTypeCountry',
        'ScopeTypeLegalEntity', 'ScopeTypeTenant', 'SelectPlaceholder'
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

    // The bridge MUST PascalCase every key: the JSON payload is serialized camelCase, and index.js reads PascalCase.
    // Skipping this is why a toast shows up as "(undefined: <correlationId>)".
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
        console.error('[CyclePeriods] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
