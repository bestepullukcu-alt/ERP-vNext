'use strict';

// MOD-0192 Capacity — L10n bridge normalizer for the plan workspace (golden-reference slim pattern). DRAFT overlay — not built.
// Reads #capacity-details-l10n, normalizes names to PascalCase into window.L10n and warns (never substitutes text) when a key is
// missing, so a missing resource is visible in the console instead of rendering an English fallback (pack §23.9).
(function () {
    const payload = document.getElementById('capacity-details-l10n');
    const requiredKeys = [
        'Cancel',
        'Close',
        'Copy',
        'ErrorOccurred',
        'CapacityPlansTitle',
        'PlanId',
        'PlanName',
        'HorizonStart',
        'HorizonEnd',
        'DemandPlanId',
        'DemandPlanVersion',
        'SourceCapturedAt',
        'SourceChecksum',
        'Status',
        'PlanStatusDraft',
        'PlanStatusEvaluating',
        'PlanStatusReady',
        'PlanStatusApproved',
        'PlanStatusArchived',
        'NotProvided',
        'SupportReference',
        'CopyDone',
        'RequestPending',
        'RequiredMissing',
        'ErrInvalidRequest',
        'ErrInvalidCorrelationId',
        'ErrUnauthenticated',
        'ErrForbidden',
        'ErrUnknownCapacityPlan',
        'ErrUnknownCapacityScenario',
        'ErrUnknownCapacityEvaluation',
        'ErrCapacityPlanAlreadyExists',
        'ErrCapacityScenarioNameConflict',
        'ErrCapacityPlanStateConflict',
        'ErrEvaluationAlreadyActive',
        'ErrIdempotencyKeyReused',
        'ErrInvalidDemandReference',
        'ErrInvalidConstraintReference',
        'ErrDependencyUnavailable',
        'ErrCommitResultUnresolved',
        'PlanDetailsTitle',
        'BackToPlans',
        'PlanSummary',
        'Horizon',
        'Provenance',
        'CreatedAt',
        'SummaryErrorState',
        'Retry',
        'SelectOption',
        'Scenarios',
        'CreateScenario',
        'CreateScenarioTitle',
        'OpenScenario',
        'ScenarioId',
        'ScenarioIdHelp',
        'ScenarioIdInvalid',
        'ScenarioName',
        'ScenarioStatusLabel',
        'ScenarioStatusDraft',
        'ScenarioStatusEvaluating',
        'ScenarioStatusEvaluated',
        'ScenarioStatusArchived',
        'ScenarioEmptyState',
        'ScenarioErrorState',
        'ScenarioCreated',
        'SessionIdNote',
        'ConstraintRefs',
        'ConstraintRefsHelp',
        'ConstraintId',
        'Source',
        'SourceVersion',
        'Adjustments',
        'AdjustmentsHelp',
        'ResourceRef',
        'Period',
        'AvailableCapacityDelta',
        'AvailableCapacityDeltaHelp',
        'DecimalInvalid',
        'UomId',
        'AddRow',
        'RemoveRow',
        'RowIncomplete',
        'NoneListed',
        'Evaluate',
        'EvaluateTitle',
        'EvaluationMode',
        'EvaluationModeFinite',
        'EvaluationModeInfinite',
        'ResourceRefs',
        'ResourceRefsHelp',
        'ResourceRefsMissing',
        'Evaluation',
        'EvaluationId',
        'EvaluationStatusLabel',
        'EvaluationStatusAccepted',
        'EvaluationStatusRunning',
        'EvaluationStatusCompleted',
        'EvaluationStatusFailed',
        'SubmittedAt',
        'CompletedAt',
        'EvaluationEmptyState',
        'EvaluationErrorState',
        'EvaluationSubmitted',
        'Refresh',
        'RefreshNote',
        'Bottlenecks',
        'RequiredCapacity',
        'AvailableCapacity',
        'Shortfall',
        'EmptyBottlenecks'
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
        console.error('[Capacity] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
