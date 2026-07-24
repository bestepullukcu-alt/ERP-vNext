'use strict';

(function (global) {
    const resolveTriggerResponse = (fixture, interactionState) => {
        const validation = global.WorkCenterNextContract?.validateTrigger(fixture);
        if (!validation?.valid) {
            return {
                invalid: true,
                validationErrors: validation?.errors || [],
                surfaceMode: 'triggerResponse',
                primaryActionCode: null,
                secondaryActionCodes: [],
                overflowActionCodes: [],
                notice: { code: 'fixtureInvalid', labelKey: 'FixtureInvalidTitle' },
                responseBehavior: 'refresh'
            };
        }
        return {
            invalid: false,
            surfaceMode: 'triggerResponse',
            primaryActionCode: fixture.primaryActionCode ?? null,
            secondaryActionCodes: (fixture.secondaryActionCodes || []).slice(),
            overflowActionCodes: (fixture.overflowActionCodes || []).slice(),
            notice: null,
            responseBehavior: fixture.responseBehavior || 'refresh',
            submittingActionCode: interactionState?.submittingActionCode || null
        };
    };

    global.WorkCenterNextTriggerResponseResolver = { resolveTriggerResponse };
})(typeof window !== 'undefined' ? window : globalThis);
