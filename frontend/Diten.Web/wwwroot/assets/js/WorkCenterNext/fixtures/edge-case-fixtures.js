'use strict';

(function (global) {
    const f = global.WorkCenterNextFixtureFactory;
    if (!f) { throw new Error('WorkCenterNextFixtureFactory is required.'); }
    const { resource, action, disabledAction, source, base } = f;

    const fixtures = [
        base('WC-EDGE-STALE', 'approval', 'FixtureTitleStale', {
            assignmentMode: 'approval',
            normalizedStatus: 'Pending',
            nativeStatus: { code: 'PENDING_APPROVAL', label: resource('StatusPending') },
            systemState: 'stale',
            actions: [disabledAction('approve', 'STALE_PROJECTION', 'ActionDisabledStaleProjection')],
            primaryActionCode: 'approve',
            source: source('workflow', 'ApprovalInstance', 'APR-STALE'),
            expectation: { surfaceMode: 'recovery', readOnly: true, criticalBannerCode: 'stale', primaryActionCode: 'approve' }
        }),
        base('WC-EDGE-UNAVAILABLE', 'issue', 'FixtureTitleUnavailable', {
            systemState: 'sourceUnavailable',
            actions: [disabledAction('resolve', 'SOURCE_UNAVAILABLE', 'ActionDisabledSourceUnavailable')],
            primaryActionCode: 'resolve',
            source: source('incident', 'Incident', 'INC-404'),
            expectation: { surfaceMode: 'recovery', readOnly: true, criticalBannerCode: 'sourceUnavailable', primaryActionCode: 'resolve' }
        }),
        base('WC-EDGE-AUTHORITY', 'review', 'FixtureTitleAuthorityEnded', {
            normalizedStatus: 'Pending',
            nativeStatus: { code: 'REVIEW_REQUESTED', label: resource('StatusPending') },
            systemState: 'authorityEnded',
            businessContext: { sections: [] },
            actions: [disabledAction('signoff', 'AUTHORITY_ENDED', 'ActionDisabledAuthorityEnded')],
            primaryActionCode: 'signoff',
            source: source('workflow', 'ReviewInstance', 'RVW-ENDED'),
            expectation: { surfaceMode: 'readonly', readOnly: true, criticalBannerCode: 'authorityEnded', primaryActionCode: 'signoff' }
        }),
        base('WC-EDGE-RECONCILIATION', 'exception', 'FixtureTitleReconciliation', {
            systemState: 'reconciliationRequired',
            actions: [disabledAction('resolve', 'RECONCILIATION_REQUIRED', 'ActionDisabledReconciliationRequired')],
            primaryActionCode: 'resolve',
            source: source('integration-monitoring', 'ReconciliationException', 'REC-18'),
            expectation: { surfaceMode: 'recovery', readOnly: true, criticalBannerCode: 'reconciliationRequired', primaryActionCode: 'resolve' }
        }),
        base('WC-EDGE-DEEPLINK', 'approval', 'FixtureTitleDeepLink', {
            assignmentMode: 'approval',
            normalizedStatus: 'Pending',
            nativeStatus: { code: 'SOURCE_DECISION_REQUIRED', label: resource('StatusPending') },
            actionDepth: 'deeplink',
            source: source('project-governance', 'ProjectGovernanceDecision', 'PRJ-44', { deepLink: '/PPM/Projects/PRJ-44' }),
            actions: [],
            primaryActionCode: null,
            expectation: { surfaceMode: 'deeplink', readOnly: true, primaryActionCode: null }
        })
    ];

    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.edgeCases = fixtures;
})(typeof window !== 'undefined' ? window : globalThis);
