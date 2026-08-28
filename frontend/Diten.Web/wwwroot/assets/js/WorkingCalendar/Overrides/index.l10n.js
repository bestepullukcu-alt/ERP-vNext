'use strict';

(function () {
    const payload = document.getElementById('workingcalendaroverrides-l10n');
    const requiredKeys = [
        'Active', 'Actions', 'AddNew', 'Apply', 'AreYouSure', 'Archive', 'ArchiveConfirm',
        'Activate', 'ActivateConfirm', 'BulkDelete', 'BulkDeleteConfirm', 'CalendarCode',
        'CalendarName', 'CalendarYear', 'Cancel', 'ColumnVisibility', 'ComingSoon',
        'CountryCode', 'CountryInherited', 'DayCount', 'Description', 'Details', 'Edit', 'ErrorOccurred',
        'Export', 'Filter', 'Import', 'IncludeArchived', 'Inherited', 'InheritedFromCountry',
        'NoCalendarData', 'NotAvailable', 'Passive', 'QuickView', 'RecordCreated',
        'RecordSaved', 'RecordUpdated', 'Reset', 'ResolutionCalendarMissing',
        'ResolutionCountryUnknown', 'ResolutionResolved', 'ResolutionYearMissing', 'Save',
        'AddDay', 'EditDay', 'Recurrence', 'ArchiveDayConfirm', 'DayType', 'DayCode', 'DayName',
        'DayDate', 'ObservedDate', 'IsHalfDay', 'WorkingDayOverrideHint',
        'SaveView', 'ScopeType', 'Search', 'ShowAll', 'Status', 'StatusActive',
        'StatusArchived', 'StatusDraft', 'Unknown', 'ViewDetails', 'WeekendDays'
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

    // camelCase -> PascalCase. Skipping this leaves every window.L10n lookup undefined and toasts render as
    // "(undefined: <corrId>)".
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
        console.error('[WorkingCalendars] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
