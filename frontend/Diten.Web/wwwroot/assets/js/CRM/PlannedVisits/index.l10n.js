'use strict';

(function () {
    const payload = document.getElementById('plannedvisits-l10n');
    const requiredKeys = [
        'Actions', 'Apply', 'AreYouSure', 'ArchivePlannedVisit', 'ArchivePlannedVisitConfirm', 'Cancel',
        'CancelPlannedVisit', 'CancelPlannedVisitConfirm', 'CancellationReasonPrompt', 'ColumnVisibility',
        'ConfirmPlannedVisit', 'ConfirmPlannedVisitConfirm', 'Consent', 'ConsentAllowed', 'ConsentBlocked',
        'ConsentNotApplicable', 'ConsentUnknown', 'ContentSourceManual', 'ContentSourceStrategy',
        'ContractUnavailable', 'CreatePlannedVisit', 'Details', 'Edit', 'EmptyState', 'ErrorOccurred', 'Export',
        'Filter', 'FormTitleCreate', 'FormTitleEdit', 'FormValidationError', 'FreqConflict', 'FreqNotApplicable',
        'FreqResolved', 'FreqUnknown', 'Frequency', 'Import', 'JourneyStage', 'Loading', 'NotAvailable',
        'PlannedDate', 'PlannedVisitsTitle', 'RecordArchived', 'RecordCancelled', 'RecordConfirmed',
        'RecordCreated', 'RecordUpdated', 'Reset', 'ResourceId', 'Save', 'SaveView', 'Search', 'SelectPlaceholder',
        'ShowAll', 'Status', 'StatusArchived', 'StatusCancelled', 'StatusConfirmed', 'StatusDraft', 'StatusPlanned',
        'Target', 'TargetId', 'TargetType', 'TargetTypeAccount', 'TargetTypeAccountContactLink', 'TargetTypeContact',
        'TargetTypePharmacy', 'VisitCode', 'VisitPurpose', 'VisitType', 'ViewDetails'
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

    // The bridge MUST PascalCase every key: the JSON payload is serialized camelCase, and the pages read PascalCase.
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
        console.error('[PlannedVisits] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
