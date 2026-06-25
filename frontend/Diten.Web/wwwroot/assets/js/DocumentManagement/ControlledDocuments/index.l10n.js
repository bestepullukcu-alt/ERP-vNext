/**
 * MOD-0029-FU01 - Controlled Documents L10n bridge.
 * The _IndexL10n.cshtml payload (@Json.Serialize) emits camelCase keys; the page JS reads PascalCase
 * (e.g. L.CorrelationId). Normalize camelCase -> PascalCase before merging (QmsBaselines loader standard)
 * so every JS-side L10n key resolves.
 *
 * requiredKeys MUST stay in sync with _IndexL10n.cshtml (a missing key -> [L10N WARNING] + undefined lookup).
 */
'use strict';

(function () {
    const script = document.getElementById('controlleddocuments-l10n');
    if (!script) return;

    const requiredKeys = [
        'ControlledDocumentsTitle', 'PageDescription', 'Title', 'DocumentType', 'FolderPath', 'CurrentVersion',
        'CreatedAt', 'Actions', 'EmptyList', 'DocumentLibrary', 'Templates', 'FolderDocuments',
        'TypeSop', 'TypeWorkInstruction', 'TypePolicy', 'TypeForm', 'TypeTemplate', 'TypeOther',
        'StatusActive', 'StatusArchived', 'AddDocument', 'AddTemplate', 'ViewDetails', 'EditMetadata',
        'Download', 'ShareDocument', 'ShareTemplate', 'FolderShare', 'Description', 'Tags', 'EffectiveDate',
        'ReviewDate', 'ExpiryDate', 'Controlled', 'CollectionInstanceId', 'CompanyId', 'SelectFile', 'Save',
        'Cancel', 'VersionHistory', 'UploadNewVersion', 'Version', 'UploadedBy', 'UploadedAt', 'ChangeSummary',
        'VersionStatusActive', 'VersionStatusSuperseded', 'Reusable', 'Shareable', 'CopyableOnAdopt',
        'ReferenceOnly', 'AccessControl', 'ShareMode', 'ShareModeReference', 'ShareModeCopyOnAdopt',
        'TargetCompany', 'IncludeTemplates', 'BranchToShare', 'DryRun', 'Execute', 'FoldersIncluded',
        'TemplatesIncluded', 'TemplatesSkipped', 'DocumentCreated', 'VersionUploaded', 'ShareSuccess',
        'FileRequired', 'TitleRequired', 'NoAccess', 'AccessDenied', 'NotFound', 'CorrelationId',
        'ReasonValidationFailed', 'ReasonConflict', 'ReasonPermDenied', 'ReasonNotFound',
        'ReasonStorageUnavailable', 'ReasonFeatureDisabled', 'SaveView', 'Print', 'Copy', 'PDF', 'Search',
        'Export', 'Filter', 'Apply', 'Reset', 'ShowAll', 'ColumnVisibility', 'Status', 'NotAvailable',
        'Unknown', 'RecordSaved', 'ErrorOccurred'
    ];

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const parsed = JSON.parse(script.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(parsed || {})) {
            normalized[toPascalCase(key)] = parsed[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        const missing = requiredKeys.filter((k) => window.L10n[k] === undefined);
        if (missing.length) {
            console.warn('[L10N WARNING] ControlledDocuments missing keys:', missing.join(', '));
        }
    } catch (_) {
        window.L10n = window.L10n || {};
    }
})();
