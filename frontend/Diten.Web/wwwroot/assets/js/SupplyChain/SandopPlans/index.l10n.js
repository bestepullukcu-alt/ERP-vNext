'use strict';

// SandopPlans — L10n bridge reader (golden-reference slim pattern, copied from Claims/index.l10n.js).
// Reads #sandop-plans-l10n into window.L10n and warns (never substitutes text) when a key is missing, so a missing resource is
// visible in the console instead of rendering an English fallback. The bridge is a Dictionary (MODULE-RECIPE 5.3), so the
// keys arrive exactly as the resx names them and are copied as they are.
// requiredKeys is EXACTLY the key list _IndexL10n.cshtml emits (shared + module); keep the two in step (F-1).
(function () {
    const payload = document.getElementById('sandop-plans-l10n');
    const requiredKeys = [
        'LegalEntity',
        'LegalEntityUnavailable',
        'LegalEntityNone',
        'LegalEntityRequired',
        'Status',
        'Save',
        'Cancel',
        'Close',
        'Copy',
        'Copied',
        'Loading',
        'Back',
        'SelectPlaceholder',
        'ErrorOccurred',
        'RecordSaved',
        'AreYouSure',
        'BackToList',
        'FieldRequired',
        'SandopPlansTitle',
        'PageDescription',
        'CreatePlan',
        'OpenPlan',
        'PlanId',
        'PlanIdHelp',
        'PlanName',
        'HorizonStart',
        'HorizonEnd',
        'DemandPlanId',
        'DemandPlanVersion',
        'ValidationHorizonOrder',
        'ValidationCommentLength',
        'WorkspaceTitle',
        'CurrentSnapshot',
        'CurrentMarker',
        'CreatedAt',
        'SnapshotsHeader',
        'CaptureSnapshot',
        'CapturedAt',
        'SnapshotProvenance',
        'SupplyInputRefsCount',
        'SourceCapturedAt',
        'SourceChecksum',
        'SupplyInputRefs',
        'RefSource',
        'RefResourceId',
        'RefResourceVersion',
        'AddSupplyInputRef',
        'RemoveSupplyInputRef',
        'SignOffsHeader',
        'RecordSignOff',
        'SignOffSnapshot',
        'SignOffRole',
        'SignOffDecision',
        'SignOffComment',
        'DecidedBy',
        'DecidedAt',
        'SignOffNeedsSnapshot',
        'SignOffFinalNote',
        'RoleDemandPlanning',
        'RoleSupplyPlanning',
        'RoleFinance',
        'RoleOperations',
        'RoleExecutive',
        'DecisionApproved',
        'DecisionRejected',
        'StatusDraft',
        'StatusInReview',
        'StatusApproved',
        'StatusRejected',
        'StatusArchived',
        'PlanCreated',
        'SnapshotCaptured',
        'SignOffRecorded',
        'RequestPending',
        'Retry',
        'NotProvided',
        'EmptyState',
        'ListErrorState',
        'SupportReference',
        'ErrInvalidRequest',
        'ErrForbidden',
        'ErrUnauthenticated',
        'ErrSandopPlanNotFound',
        'ErrSandopPlanAlreadyExists',
        'ErrSandopPlanStateConflict',
        'ErrSandopSignOffStateConflict',
        'ErrSignOffAlreadyRecorded',
        'ErrInvalidSnapshotReference',
        'ErrInvalidDemandReference',
        'ErrIdempotencyKeyReused',
        'ErrDependencyUnavailable',
        'ErrInvalidCorrelationId',
        'ErrCommitResultUnresolved',
        'ErrInternalError'
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

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        window.L10n = Object.assign({}, window.L10n || {}, raw);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[SandopPlans] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
