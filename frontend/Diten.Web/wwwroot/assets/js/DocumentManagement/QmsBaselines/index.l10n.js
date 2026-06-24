'use strict';

(function () {
    const payload = document.getElementById('qmsbaselines-l10n');
    const requiredKeys = [
        'QmsBaselinesTitle', 'BaselineId', 'Version', 'DefinitionCount', 'CreatedAt',
        'PublishedAt', 'SnapshotHash', 'Actions', 'ViewDetails', 'StatusDraft',
        'StatusPublished', 'ImportWorkbook', 'CreateManualBaseline', 'CreateManualTitle',
        'ManualBaselineCreated', 'OpenDesigner', 'EmptyList', 'Status', 'Filter', 'Apply',
        'Reset', 'ShowAll', 'NotAvailable', 'Unknown', 'RunDryRun', 'CommitImport',
        'WorkbookRequired', 'SourceKeyRequired', 'BaselineVersionRequired', 'InvalidFileType',
        'DryRunValid', 'DryRunInvalid', 'CommitSuccess', 'SummaryTotalRows', 'SummaryImported',
        'SummarySkipped', 'SummaryErrors', 'SummaryWarnings', 'SummaryDuplicates', 'SummaryHierarchy',
        'Metadata', 'ManifestId', 'Definitions', 'NoDefinitions', 'Publish', 'PublishConfirm',
        'PublishSuccess', 'FullPath', 'Name', 'DesignerTitle', 'BaselineSection', 'BaselineName',
        'LifecycleSection', 'EffectiveDate', 'AddRoot', 'AddChild', 'EditNode', 'MoveNode',
        'DeleteNode', 'DeleteConfirm', 'DeleteSuccess', 'MoveSuccess', 'ValidateDraft',
        'DraftTreeValid', 'DraftTreeInvalid', 'NodeEditorTitle', 'DisplayOrder', 'PurposeScope',
        'RequiredByScope', 'AllowedDocClass', 'Classification', 'RetentionHint', 'AllowsManualChildren',
        'TemplatesAllowed', 'Mandatory', 'Protected', 'ParentNode', 'RootNode', 'RecordSaved',
        'ErrorOccurred', 'AccessDenied', 'NotFound',
        'CorrelationId', 'ReasonValidationFailed', 'ReasonConflict', 'ReasonPermDenied', 'ReasonNotFound'
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
        console.error('[QmsBaselines] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
