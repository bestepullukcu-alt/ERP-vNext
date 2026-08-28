'use strict';

(function () {
    const payload = document.getElementById('cyclecapacities-l10n');
    const requiredKeys = [
        'Actions', 'Active', 'Apply', 'AreYouSure', 'ArchiveCycleCapacity', 'ArchiveCycleCapacityConfirm',
        'ArchivedFilter', 'ArchivedHidden', 'ArchivedOnly', 'BudgetDailySpendExceedsDay',
        'BudgetVisitMinutesZero', 'BulkDelete', 'BulkDeleteConfirm', 'CalendarCountryCode', 'Cancel',
        'ColumnVisibility', 'ContractUnavailable', 'CreateCycleCapacity', 'CycleCapacitiesTitle', 'CycleCode',
        'CycleName', 'CyclePeriod', 'DailyWorkMinutes', 'Description', 'Details', 'Edit', 'EmptyState',
        'ErrorOccurred', 'EstimateNoticeBody', 'EstimateNoticeTitle', 'Export', 'Filter', 'FormTitleCreate',
        'FormTitleEdit', 'FormValidationError', 'Fte', 'Import', 'Loading', 'MinutesPerVisit', 'MonthCount',
        'CalculationUnavailable', 'CalendarForbiddenBody', 'CalendarForbiddenTitle',
        'CalendarUnresolvedBody', 'CalendarUnresolvedTitle', 'DeductedDays', 'EstimateSectionHint',
        'FieldDays', 'Month', 'NoFieldDays', 'TotalVisitNumber', 'VisitMinutes',
        'WorkingDays',
        'NotAvailable', 'PageDescription', 'PeriodClosedLock', 'PeriodWindow', 'QuickView', 'RecordArchived',
        'RecordCreated', 'RecordSaved', 'RecordUpdated', 'Reset', 'Save', 'SaveView', 'Search',
        'SelectPlaceholder', 'ShowAll', 'Status', 'Unknown', 'Update', 'UpdatedAt', 'ViewDetails'
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
        console.error('[CycleCapacities] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
