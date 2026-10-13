'use strict';

// MOD-0192 Capacity — L10n bridge normalizer for the entry page (golden-reference slim pattern). DRAFT overlay — not built.
// Reads #capacity-index-l10n, normalizes names to PascalCase into window.L10n and warns (never substitutes text) when a key is
// missing, so a missing resource is visible in the console instead of rendering an English fallback (pack §23.9).
(function () {
    const payload = document.getElementById('capacity-index-l10n');
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
        'CapacityPlansPageDescription',
        'NoPlanListNote',
        'CreatePlan',
        'OpenPlan',
        'OpenPlanTitle',
        'PlanIdHelp',
        'PlanIdInvalid',
        'FormTitleCreate',
        'HorizonOrderInvalid',
        'DemandReferenceHelp',
        'SourceCapturedAtHelp',
        'RequiredFilled',
        'PlanCreated'
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
