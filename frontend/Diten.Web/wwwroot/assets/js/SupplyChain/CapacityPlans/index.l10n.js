'use strict';

// CapacityPlans — L10n bridge reader (golden-reference slim pattern, copied from Claims/index.l10n.js).
// Reads #capacity-plans-l10n into window.L10n and warns (never substitutes text) when a key is missing, so a missing resource is
// visible in the console instead of rendering an English fallback. The bridge is a Dictionary (MODULE-RECIPE 5.3), so the
// keys arrive exactly as the resx names them and are copied as they are.
// requiredKeys is EXACTLY the key list _IndexL10n.cshtml emits (shared + module); keep the two in step (F-1).
(function () {
    const payload = document.getElementById('capacity-plans-l10n');
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
        'Search',
        'BackToList',
        'FieldRequired',
        'CapacityPlansTitle',
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
        'InvalidLinkId',
        'SourceCapturedAt',
        'SourceChecksum',
        'WorkspaceTitle',
        'CreatedAt',
        'PlanProvenance',
        'CreateScenario',
        'OpenScenario',
        'ScenarioId',
        'ScenarioIdHelp',
        'ScenarioName',
        'ConstraintRefs',
        'ConstraintId',
        'ConstraintSource',
        'ConstraintVersion',
        'AddConstraintRef',
        'RemoveConstraintRef',
        'AdjustmentsHeader',
        'AdjResourceRef',
        'AdjPeriod',
        'AdjCapacityDelta',
        'AdjUom',
        'DecimalHelp',
        'AddAdjustment',
        'RemoveAdjustment',
        'EvaluateScenario',
        'EvaluationMode',
        'ModeFinite',
        'ModeInfinite',
        'ResourceRefs',
        'AddResourceRef',
        'RemoveResourceRef',
        'EvaluationPanel',
        'EvaluationId',
        'SubmittedAt',
        'CompletedAt',
        'RefreshEvaluation',
        'EvaluationPending',
        'BottlenecksHeader',
        'BottleneckResource',
        'BottleneckPeriod',
        'BottleneckRequired',
        'BottleneckAvailable',
        'BottleneckShortfall',
        'BottleneckUom',
        'NoBottlenecks',
        'StatusDraft',
        'StatusEvaluating',
        'StatusReady',
        'StatusApproved',
        'StatusArchived',
        'StatusEvaluated',
        'StatusAccepted',
        'StatusRunning',
        'StatusCompleted',
        'StatusFailed',
        'PlanCreated',
        'ScenarioCreated',
        'EvaluationSubmitted',
        'RequestPending',
        'Retry',
        'NotProvided',
        'EmptyState',
        'ListErrorState',
        'SupportReference',
        'ErrInvalidRequest',
        'ErrForbidden',
        'ErrUnauthenticated',
        'ErrCapacityPlanNotFound',
        'ErrCapacityScenarioNotFound',
        'ErrCapacityEvaluationNotFound',
        'ErrCapacityPlanAlreadyExists',
        'ErrCapacityPlanStateConflict',
        'ErrCapacityScenarioNameConflict',
        'ErrEvaluationAlreadyActive',
        'ErrInvalidConstraintReference',
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
        console.error('[CapacityPlans] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
