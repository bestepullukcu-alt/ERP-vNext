'use strict';

(function (global) {
    const f = global.WorkCenterNextFixtureFactory;
    if (!f) { throw new Error('WorkCenterNextFixtureFactory is required.'); }
    const { resource, action, disabledAction, source, base } = f;
    const context = (schemaId, fields) => ({
        schemaId,
        sections: [{ code: 'decisionContext', importance: 'primary', fields }]
    });
    const field = (code, labelKey, valueType, value, importance) => ({
        code, label: resource(labelKey), valueType, value, importance: importance || 'primary'
    });

    const fixtures = [
        base('ES-OBJ-APP-01', 'approval', 'FixtureTitleEsObjectiveApproval', {
            assignmentMode: 'approval',
            normalizedStatus: 'Pending',
            nativeStatus: { code: 'PENDING_APPROVAL', label: resource('StatusPending') },
            source: source('enterprise-strategy', 'StrategicObjective', 'OBJ-104', {
                sourceSystem: 'Diten.EnterpriseStrategyService',
                processInstanceId: 'OBJ-104-APPROVAL-01',
                deepLink: '/EnterpriseStrategyBusinessPerformance/Objectives/Details/OBJ-104'
            }),
            lifecycleOwner: { providerCode: 'workflow' },
            businessContext: context('esbp.objective-approval.v1', [
                field('objective', 'ContextObjective', 'text', 'Increase regional revenue'),
                field('approvalRoute', 'DetailLifecycleOwner', 'status', 'IndividualApprover')
            ]),
            concurrency: { kind: 'version', token: '17' },
            actions: [action('approve', { requiresConfirmation: true, requiresEvidence: true }), action('reject', { requiresReason: true })],
            primaryActionCode: 'approve',
            overflowActionCodes: ['reject'],
            expectation: { surfaceMode: 'decision', readOnly: false, primaryActionCode: 'approve' }
        }),
        base('ES-OBJ-REV-01', 'review', 'FixtureTitleEsObjectiveReview', {
            normalizedStatus: 'Pending',
            nativeStatus: { code: 'REVIEW_DUE', label: resource('StatusPending') },
            source: source('enterprise-strategy', 'StrategicObjective', 'OBJ-104', {
                sourceSystem: 'Diten.EnterpriseStrategyService',
                processInstanceId: 'OBJ-104-REVIEW-2026-Q3',
                deepLink: '/EnterpriseStrategyBusinessPerformance/Objectives/Details/OBJ-104'
            }),
            businessContext: context('esbp.objective-review.v1', [
                field('objective', 'ContextObjective', 'text', 'Increase regional revenue'),
                field('period', 'ContextPeriodicReview', 'text', '2026 Q3'),
                field('nextReview', 'WaitingExpectedUntil', 'date', '2026-09-30', 'secondary')
            ]),
            concurrency: { kind: 'version', token: '18' },
            actions: [action('signoff', { requiresConfirmation: true }), action('requestInfo', { requiresReason: true })],
            primaryActionCode: 'signoff',
            secondaryActionCodes: ['requestInfo'],
            expectation: { surfaceMode: 'review', readOnly: false, primaryActionCode: 'signoff' }
        }),
        base('ES-DMD-REV-01', 'review', 'FixtureTitleEsDemandReview', {
            normalizedStatus: 'Pending',
            nativeStatus: { code: 'SUBMITTED', label: resource('StatusPending') },
            source: source('enterprise-strategy', 'DemandIdea', 'IC-2026-0042', {
                sourceSystem: 'Diten.EnterpriseStrategyService',
                processInstanceId: 'IC-2026-0042-REVIEW-01',
                deepLink: '/DemandIdeas/Details/IC-2026-0042'
            }),
            businessContext: context('esbp.demand-review.v1', [
                field('demand', 'ContextDemandIdea', 'text', 'Regional analytics capability'),
                field('reviewDue', 'WaitingExpectedUntil', 'date', '2026-08-07')
            ]),
            concurrency: { kind: 'version', token: '5' },
            actions: [action('signoff', { requiresConfirmation: true }), action('return', { requiresReason: true })],
            primaryActionCode: 'signoff',
            overflowActionCodes: ['return'],
            expectation: { surfaceMode: 'review', readOnly: false, primaryActionCode: 'signoff' }
        }),
        base('ES-DEC-TASK-01', 'task', 'FixtureTitleEsDecompositionTask', {
            normalizedStatus: 'InProgress',
            taskLifecycle: 'Open',
            nativeStatus: { code: 'DRAFT', label: resource('LifecycleOpen') },
            workItemCapabilities: ['execution', 'dependencies', 'activity', 'businessContext', 'relatedRecords'],
            dependencies: [],
            source: source('enterprise-strategy', 'DecompositionNode', 'NODE-77', {
                sourceSystem: 'Diten.EnterpriseStrategyService',
                deepLink: '/DecompositionStructures/NODE-77'
            }),
            businessContext: context('esbp.decomposition-task.v1', [
                field('node', 'ContextDecomposition', 'text', 'Validate regional operating model')
            ]),
            concurrency: { kind: 'version', token: '3' },
            actions: [action('start'), action('reassign', { requiresReason: true })],
            primaryActionCode: 'start',
            overflowActionCodes: ['reassign'],
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'start' }
        }),
        base('ES-DEC-BLOCK-01', 'task', 'FixtureTitleEsDecompositionBlocked', {
            workItemCapabilities: ['execution', 'dependencies', 'activity', 'businessContext', 'relatedRecords'],
            dependencies: [{ id: 'NODE-70', title: resource('FixtureDependencyValidation'), type: 'FinishToStart', state: 'in-progress', direction: 'pred', blocking: true }],
            source: source('enterprise-strategy', 'DecompositionNode', 'NODE-78', {
                sourceSystem: 'Diten.EnterpriseStrategyService',
                deepLink: '/DecompositionStructures/NODE-78'
            }),
            actions: [disabledAction('start', 'VALIDATION_BLOCKED', 'ActionDisabledDependencyBlocked')],
            blockedState: { blocked: true, affectedActionCodes: ['start'], blockers: [{ code: 'VALIDATION_BLOCKED', label: resource('FixtureDependencyValidation') }] },
            primaryActionCode: 'start',
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'start', criticalBannerCode: 'hardBlocked' }
        })
    ];

    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.enterpriseStrategy = fixtures;
})(typeof window !== 'undefined' ? window : globalThis);
